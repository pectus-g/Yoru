using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 50;
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    
    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 2f;
    [SerializeField] private bool dropItemOnDeath = false;
    
    [Header("Death Effects")]
    [SerializeField] private GameObject deathParticlePrefab; // NEW!
    [SerializeField] private float particleYOffset = 1f; // Spawn particles at center of enemy
    
    [Header("Animation")]
    [SerializeField] private bool useAnimations = true;
    private Animator animator;
    
    private bool isDead = false;
    
    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        
        if (useAnimations && animator == null)
        {
            Debug.LogWarning($"{gameObject.name} has useAnimations enabled but no Animator component!");
        }
        
        Debug.Log($"{gameObject.name} initialized with {currentHealth} HP");
    }
    
    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (useAnimations && animator != null)
        {
            animator.SetTrigger("Hit");
        }
        
        FlashRed();
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void TakeDamage(int damage, Vector3 damageSourcePosition)
    {
        TakeDamage(damage);
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
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        Debug.Log($"💀 {gameObject.name} died!");
        
        // Notify combat system about death
        EnemyCombat combat = GetComponent<EnemyCombat>();
        if (combat != null)
        {
            combat.SetState(EnemyCombat.EnemyState.Dead);
        }
        
        // Play death animation
        if (useAnimations && animator != null)
        {
            animator.SetTrigger("Die");
        }
        
        // SPAWN DEATH PARTICLES! NEW!
        SpawnDeathParticles();
        
        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
        
        DisableMovement();
        
        if (dropItemOnDeath)
        {
            DropItems();
        }
        
        // Destroy enemy after delay
        Destroy(gameObject, deathDelay);
    }
    
    // NEW FUNCTION!
    private void SpawnDeathParticles()
    {
        if (deathParticlePrefab != null)
        {
            // Calculate spawn position (center of enemy)
            Vector3 spawnPosition = transform.position + new Vector3(0, particleYOffset, 0);
            
            // Instantiate particles
            GameObject particles = Instantiate(deathParticlePrefab, spawnPosition, Quaternion.identity);
            
            // Play the particle system
            ParticleSystem ps = particles.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                
                // Destroy particle object after it finishes (2 seconds + buffer)
                Destroy(particles, ps.main.duration + 0.5f);
            }
            else
            {
                // No particle system found, just destroy after 3 seconds
                Destroy(particles, 3f);
            }
            
            Debug.Log($"✨ Spawned death particles for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no death particle prefab assigned!");
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
        {
            StartCoroutine(FlashRedCoroutine(renderers));
        }
    }
    
    private System.Collections.IEnumerator FlashRedCoroutine(Renderer[] renderers)
    {
        Color[] originalColors = new Color[renderers.Length];
        Material[] materials = new Material[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;
            originalColors[i] = materials[i].color;
            materials[i].color = Color.red;
        }
        
        yield return new WaitForSeconds(0.1f);
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (materials[i] != null)
            {
                materials[i].color = originalColors[i];
            }
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
        {
            Die();
        }
    }
    
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
    }
    
    // Public getters
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsAlive() => !isDead;
    public bool IsAtFullHealth() => currentHealth >= maxHealth;
}