using UnityEngine;

/// <summary>
/// Universal Enemy Health, works for all enemy tiers.
/// Updated: passes hit type (heavy/light) to EnemyCombat for stagger.
/// Updated: optional NON-LETHAL mode, a boss that should yield instead of dying (the Komainu).
///          When non-lethal, reaching the yield threshold fires OnYield (KomainuBoss handles the
///          bow + turn-to-stone) and the enemy is never destroyed. Default OFF: normal enemies
///          behave exactly as before. Also adds SetInvulnerable for "not currently engaged" gating.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    
    [Header("Hit Reaction")]
    [Tooltip("Damage at or above this in a single hit triggers a STAGGER (big interrupt); anything below triggers a quick HIT-REACT flinch. Heavy attacks also always stagger. Set this between your light and heavy player-attack damage values.")]
    [SerializeField] private int staggerDamageThreshold = 15;

    [Header("Stagger Punish Window (boss tuning, optional)")]
    [Tooltip("Damage multiplier applied to hits landing WHILE this enemy is already in the Stagger state. 1 = off (default, no change). Oni boss: 1.5 — stagger becomes a reward window: open it with a heavy hit, then punish for bonus damage.")]
    [SerializeField] private float staggerDamageMultiplier = 1f;
    [Tooltip("If OFF, hits that would normally stagger do NOT re-trigger stagger while the enemy is already staggered (no timer reset, no chain-lock). They still deal (multiplied) damage. Default ON = original behavior.")]
    [SerializeField] private bool allowRestagger = true;

    [Header("Non-Lethal (boss yield)")]
    [Tooltip("If true, this enemy is NEVER killed: when worn down to 'Yield Health Threshold' it fires OnYield instead of dying, and ignores all further damage. Used by the Komainu (its KomainuBoss plays the bow + turn-to-stone). Leave OFF for normal enemies.")]
    [SerializeField] private bool nonLethal = false;
    [Tooltip("HP at or below which a non-lethal enemy yields. 1 = yields when almost worn out. Ignored unless Non Lethal is on.")]
    [SerializeField] private int yieldHealthThreshold = 1;
    
    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 2f;
    [SerializeField] private bool dropItemOnDeath = false;
    
    [Header("Death Effects")]
    [SerializeField] private GameObject deathParticlePrefab;
    [SerializeField] private float particleYOffset = 1f;
    
    [Header("Animation")]
    [SerializeField] private bool useAnimations = true;
    
    private Animator animator;
    private EnemyCombat enemyCombat;
    private bool isDead = false;
    private bool hasYielded = false;
    private bool invulnerable = false;
    
    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
        
        Debug.Log($"{gameObject.name} initialized with {currentHealth} HP");
    }
    
    /// <summary>
    /// Standard damage, light hit (quick flinch).
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false);
    }
    
    /// <summary>
    /// Damage with hit type, heavy hits trigger stagger.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy)
    {
        if (isDead) return;
        if (hasYielded) return;     // non-lethal: ignores all hits once it has yielded
        if (invulnerable) return;   // not currently engaged (dormant statue / patrolling / mid-yield)

        // Hallucination gate, while Nopperabō's Mushroom attack is active, Yoru deals zero
        // damage to EVERY enemy. All three TakeDamage overloads funnel through here, so this
        // single check covers normal combos, dash, parry counter, and positional damage.
        if (HallucinationEffect.IsActive)
        {
            Debug.Log($"{gameObject.name}: damage blocked by hallucination ({damage} ignored)");
            return;
        }

        // Stagger punish window — hits landing while ALREADY staggered deal bonus damage.
        // Checked before the damage is applied (and before any new stagger is triggered), so the
        // hit that OPENS the window never gets the bonus, only the follow-up punish hits do.
        bool wasStaggered = enemyCombat != null
            && enemyCombat.GetCurrentState() == EnemyCombat.EnemyState.Stagger;
        if (wasStaggered && staggerDamageMultiplier > 1f)
        {
            int baseDamage = damage;
            damage = Mathf.RoundToInt(damage * staggerDamageMultiplier);
            Debug.Log($"{gameObject.name} punish window! {baseDamage} → {damage} (x{staggerDamageMultiplier:F1})");
        }

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} damage{(isHeavy ? " (HEAVY)" : "")}. HP: {currentHealth}/{maxHealth}");
        
        FlashRed();

        // Non-lethal: worn down instead of killed. Fire OnYield once and stop taking damage.
        // The bow + turn-to-stone is handled by KomainuBoss (it listens to OnYield).
        if (nonLethal)
        {
            if (currentHealth <= yieldHealthThreshold)
            {
                hasYielded = true;
                Debug.Log($"{gameObject.name} yielded (non-lethal) at {currentHealth} HP");
                OnYield?.Invoke(this);
                return; // no death, no stagger after yielding
            }
            // Above the yield line, fall through to normal stagger/flinch so the fight reads normally.
        }
        else if (currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // Notify combat script about hit type. Big damage staggers (long interrupt); small/medium
        // damage flinches (hit-react). Heavy attacks always stagger regardless of the number.
        if (enemyCombat != null)
        {
            if (isHeavy || damage >= staggerDamageThreshold)
            {
                // Re-stagger gate: while already staggered, a second big hit must not reset the
                // stagger timer (chain-lock). With allowRestagger OFF it only deals its damage;
                // the window runs out on its own schedule.
                if (!wasStaggered || allowRestagger)
                    enemyCombat.TriggerStagger();
            }
            else
            {
                enemyCombat.TriggerHitReact();
            }
        }

        // Boss layer hook — fires AFTER the generic reaction so a listener (OniBoss) can refine
        // what just happened (e.g. swap the flinch clip by damage size). See OnDamaged docs.
        OnDamaged?.Invoke(damage, isHeavy);
    }
    
    /// <summary>
    /// Overload for positional damage (backwards compatibility).
    /// </summary>
    public void TakeDamage(int damage, Vector3 damageSourcePosition)
    {
        TakeDamage(damage, false);
    }
    
    public void Heal(int amount)
    {
        if (isDead) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"{gameObject.name} healed {amount}. HP: {currentHealth}/{maxHealth}");
    }
    
    public void InstantKill()
    {
        if (isDead) return;
        currentHealth = 0;
        Die();
    }
    
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        hasYielded = false;
        Debug.Log($"[Health] {gameObject.name} reset to {maxHealth} HP");
    }

    /// <summary>
    /// Toggle damage immunity. The Komainu's KomainuBoss turns this ON while the lion is a dormant
    /// statue or patrolling (you engage it through the gate, not by hitting it) and OFF during the
    /// fight. Default OFF, so normal enemies are always damageable.
    /// </summary>
    public void SetInvulnerable(bool value) => invulnerable = value;
    
    /// <summary>
    /// Fired exactly once when this enemy dies, on both death paths (EnemyDeathEffect
    /// and the inline fallback). InteractableEnemy listens to stamp the Memory
    /// Parchment; quest DEFEAT_ENEMY steps route through the same hook.
    /// </summary>
    public event System.Action<EnemyHealth> OnDied;

    /// <summary>
    /// Fired once when a NON-LETHAL enemy is worn down to its yield threshold instead of dying.
    /// KomainuBoss listens to this to play the bow + turn-to-stone ending.
    /// </summary>
    public event System.Action<EnemyHealth> OnYield;

    /// <summary>
    /// Fired after a hit is fully applied (final damage, heavy flag), AFTER the generic
    /// stagger/hit-react has been triggered. Boss layers (OniBoss) listen to this to add their own
    /// flavor on top — e.g. tiered reaction clips — without any boss logic living in this script.
    /// NOT fired on the killing blow (that's OnDied) or on blocked/ignored hits.
    /// Same layering pattern as OnYield.
    /// </summary>
    public event System.Action<int, bool> OnDamaged;

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDied?.Invoke(this);
        
        // Use EnemyDeathEffect if available
        var deathEffect = GetComponent<EnemyDeathEffect>();
        if (deathEffect != null)
        {
            deathEffect.StartDeathSequence();
            return;
        }
        
        Debug.Log($"💀 {gameObject.name} died!");
        
        // Notify combat system
        if (enemyCombat != null)
        {
            enemyCombat.SetState(EnemyCombat.EnemyState.Dead);
        }
        
        SpawnDeathParticles();
        
        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
        
        DisableMovement();
        
        if (dropItemOnDeath)
            DropItems();
        
        Destroy(gameObject, deathDelay);
    }
    
    private void SpawnDeathParticles()
    {
        if (deathParticlePrefab == null) return;
        
        Vector3 spawnPos = transform.position + new Vector3(0, particleYOffset, 0);
        GameObject particles = Instantiate(deathParticlePrefab, spawnPos, Quaternion.identity);
        
        ParticleSystem ps = particles.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            Destroy(particles, ps.main.duration + 0.5f);
        }
        else
        {
            Destroy(particles, 3f);
        }
    }
    
    private void DisableMovement()
    {
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
    
    // Flash stacking guard. Without it, two hits inside the 0.1s flash window make the SECOND
    // coroutine cache "red" as the original colour, so the enemy either strobes or stays red
    // forever. Same class of bug CombatFeedbackManager already fixes for hitstop. Matters a lot
    // here because the beyblade and the aerial spin tick damage several times a second.
    private Coroutine activeFlash;
    private Renderer[] cachedRenderers;
    private Material[] cachedMaterials;
    private Color[] cachedOriginalColors;

    private void CacheRenderers()
    {
        if (cachedRenderers != null) return;

        cachedRenderers = GetComponentsInChildren<Renderer>();
        cachedMaterials = new Material[cachedRenderers.Length];
        cachedOriginalColors = new Color[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            cachedMaterials[i] = cachedRenderers[i].material;
            cachedOriginalColors[i] = cachedMaterials[i].color;
        }
    }

    private void RestoreColors()
    {
        if (cachedMaterials == null) return;
        for (int i = 0; i < cachedMaterials.Length; i++)
            if (cachedMaterials[i] != null)
                cachedMaterials[i].color = cachedOriginalColors[i];
    }

    private void FlashRed()
    {
        CacheRenderers();
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        if (activeFlash != null) { StopCoroutine(activeFlash); RestoreColors(); }
        activeFlash = StartCoroutine(FlashColorCoroutine(Color.red, 0.1f));
    }

    /// <summary>
    /// One flash implementation for both colours. Always restores to the colours captured the
    /// very first time, never to whatever happened to be on the material when this hit landed.
    /// </summary>
    private System.Collections.IEnumerator FlashColorCoroutine(Color flash, float duration)
    {
        for (int i = 0; i < cachedMaterials.Length; i++)
            if (cachedMaterials[i] != null)
                cachedMaterials[i].color = flash;

        // Real time — a 0.1s flash must not stretch to a full second because Yoru is aiming.
        yield return new WaitForSecondsRealtime(duration);

        RestoreColors();
        activeFlash = null;
    }
    
    /// <summary>
    /// Quick white flash used as "hit confirmed" visual during Telegraph/Attack states.
    /// Shorter duration than FlashRed so it doesn't linger.
    /// </summary>
    public void FlashWhite()
    {
        CacheRenderers();
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        if (activeFlash != null) { StopCoroutine(activeFlash); RestoreColors(); }
        activeFlash = StartCoroutine(FlashColorCoroutine(Color.white, 0.05f));
    }
    
    private void DropItems()
    {
        Debug.Log($"{gameObject.name} dropped items!");
    }
    
    public void SetHealth(int newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
        if (currentHealth <= 0 && !isDead)
            Die();
    }
    
    // Getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsAlive() => !isDead;
    public bool IsAtFullHealth() => currentHealth >= maxHealth;
    public bool HasYielded => hasYielded;
    public bool IsInvulnerable => invulnerable;
}