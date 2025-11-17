using UnityEngine;

public class TrailerCombatController : MonoBehaviour
{
    [Header("References")]
    private Animator animator;
    
    [Header("Projectile Prefabs")]
    public GameObject leftTailProjectilePrefab;
    public GameObject rightTailProjectilePrefab;
    public Transform projectileSpawnPoint;
    
    [Header("Particle Effects")]
    public ParticleSystem circleParticle;
    
    [Header("Combat Settings")]
    public float attackCooldown = 0.8f;
    public float tailCastCooldown = 2f;
    private float lastAttackTime = 0f;
    private float lastLeftTailTime = 0f;
    private float lastRightTailTime = 0f;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        
        if (animator == null)
        {
            Debug.LogError("❌ TrailerCombatController: No Animator found!");
        }
        else
        {
            Debug.Log("✅ TrailerCombatController: Animator found!");
        }
        
        // Auto-create spawn point if not assigned
        if (projectileSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("ProjectileSpawnPoint");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = new Vector3(0, 1.5f, 1f);
            projectileSpawnPoint = spawnObj.transform;
        }
    }
    
    void Update()
    {
        // Skip if inventory is open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
            return;
        
        // === COMBAT ATTACKS (NO MOUSE BUTTONS) ===
        
        // Q KEY - Single Paw Attack
        if (Input.GetKeyDown(KeyCode.Q) && Time.time > lastAttackTime + attackCooldown)
        {
            Debug.Log("🐾 Q pressed - Triggering Attack");
            animator.SetTrigger("Attack");
            lastAttackTime = Time.time;
        }
        
        // E KEY - Double Paw Attack
        if (Input.GetKeyDown(KeyCode.E) && Time.time > lastAttackTime + attackCooldown)
        {
            Debug.Log("🐾🐾 E pressed - Triggering AttackHeavy");
            animator.SetTrigger("AttackHeavy");
            lastAttackTime = Time.time;
        }
        
        // KEY 1 - LEFT TAIL CAST (Dark/Fire) 🔥
        if (Input.GetKeyDown(KeyCode.Alpha1) && Time.time > lastLeftTailTime + tailCastCooldown)
        {
            Debug.Log("🔥 1 pressed - Triggering TailCastLeft");
            animator.SetTrigger("TailCastLeft");
            lastLeftTailTime = Time.time;
        }
        
        // KEY 2 - RIGHT TAIL CAST (Light) ✨
        if (Input.GetKeyDown(KeyCode.Alpha2) && Time.time > lastRightTailTime + tailCastCooldown)
        {
            Debug.Log("✨ 2 pressed - Triggering TailCastRight");
            animator.SetTrigger("TailCastRight");
            lastRightTailTime = Time.time;
        }
        
        // === CINEMATIC ANIMATIONS ===
        
        // KEY 3 - Circle Activation
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("⭕ 3 pressed - Triggering CircleActivation");
            animator.SetTrigger("CircleActivation");
            
            if (circleParticle != null)
            {
                circleParticle.transform.position = transform.position;
                circleParticle.Play();
            }
        }
        
        // L KEY - Heart Tails
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("💕 L pressed - Triggering HeartTails");
            animator.SetTrigger("HeartTails");
        }
        
        // K KEY - Soul Absorption
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("🔮 K pressed - Triggering AbsorbSoul");
            animator.SetTrigger("AbsorbSoul");
        }
        
        // J KEY - Freeing Soul
        if (Input.GetKeyDown(KeyCode.J))
        {
            Debug.Log("✨ J pressed - Triggering FreeSoul");
            animator.SetTrigger("FreeSoul");
        }
        
        // H KEY - Scared Cat
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("😱 H pressed - Triggering Scared");
            animator.SetTrigger("Scared");
        }
        
        // O KEY - Sleep
        if (Input.GetKeyDown(KeyCode.O))
        {
            Debug.Log("😴 O pressed - Triggering Sleep");
            animator.SetTrigger("Sleep");
        }
        
        // P KEY - Wake Up
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("😺 P pressed - Triggering WakeUp");
            animator.SetTrigger("WakeUp");
        }
    }
    
    // === ANIMATION EVENTS ===
    
    public void SpawnLeftTailProjectile()
    {
        if (leftTailProjectilePrefab != null && projectileSpawnPoint != null)
        {
            Instantiate(leftTailProjectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
            Debug.Log("🔥 LEFT TAIL PROJECTILE SPAWNED!");
        }
    }
    
    public void SpawnRightTailProjectile()
    {
        if (rightTailProjectilePrefab != null && projectileSpawnPoint != null)
        {
            Instantiate(rightTailProjectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
            Debug.Log("✨ RIGHT TAIL PROJECTILE SPAWNED!");
        }
    }
    
    public void PlayCircleParticle()
    {
        if (circleParticle != null)
        {
            circleParticle.transform.position = transform.position;
            circleParticle.Play();
        }
    }
}