using UnityEngine;

/// <summary>
/// Universal Enemy Health — works for all enemy tiers.
/// Updated: passes hit type (heavy/light) to EnemyCombat for stagger.
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
    
    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
        
        Debug.Log($"{gameObject.name} initialized with {currentHealth} HP");
    }
    
    /// <summary>
    /// Standard damage — light hit (quick flinch).
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, false);
    }
    
    /// <summary>
    /// Damage with hit type — heavy hits trigger stagger.
    /// </summary>
    public void TakeDamage(int damage, bool isHeavy)
    {
        if (isDead) return;

        // Hallucination gate — while Nopperabō's Mushroom attack is active, Yoru deals zero
        // damage to EVERY enemy. All three TakeDamage overloads funnel through here, so this
        // single check covers normal combos, dash, parry counter, and positional damage.
        if (HallucinationEffect.IsActive)
        {
            Debug.Log($"{gameObject.name}: damage blocked by hallucination ({damage} ignored)");
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"{gameObject.name} took {damage} damage{(isHeavy ? " (HEAVY)" : "")}. HP: {currentHealth}/{maxHealth}");
        
        FlashRed();
        
        if (currentHealth <= 0)
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
                enemyCombat.TriggerStagger();
            }
            else
            {
                enemyCombat.TriggerHitReact();
            }
        }
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
        Debug.Log($"[Health] {gameObject.name} reset to {maxHealth} HP");
    }
    
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        
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
    
    private void FlashRed()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
            StartCoroutine(FlashRedCoroutine(renderers));
    }
    
    /// <summary>
    /// Quick white flash used as "hit confirmed" visual during Telegraph/Attack states.
    /// Shorter duration than FlashRed so it doesn't linger.
    /// </summary>
    public void FlashWhite()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
            StartCoroutine(FlashWhiteCoroutine(renderers));
    }
    
    private System.Collections.IEnumerator FlashRedCoroutine(Renderer[] renderers)
    {
        Color[] originals = new Color[renderers.Length];
        Material[] materials = new Material[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;
            originals[i] = materials[i].color;
            materials[i].color = Color.red;
        }
        
        yield return new WaitForSeconds(0.1f);
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (materials[i] != null)
                materials[i].color = originals[i];
        }
    }
    
    private System.Collections.IEnumerator FlashWhiteCoroutine(Renderer[] renderers)
    {
        Color[] originals = new Color[renderers.Length];
        Material[] materials = new Material[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;
            originals[i] = materials[i].color;
            materials[i].color = Color.white;
        }
        
        yield return new WaitForSeconds(0.05f);
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (materials[i] != null)
                materials[i].color = originals[i];
        }
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
}