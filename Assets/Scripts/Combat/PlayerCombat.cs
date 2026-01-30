using UnityEngine;

/// <summary>
/// PlayerCombat.cs - YORU Combat System
/// Handles combo attacks, heavy attacks, and hit detection
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform attackPoint; // Empty GameObject in front of Yoru
    
    [Header("=== COMBO SETTINGS ===")]
    [SerializeField] private float comboWindowTime = 1.0f;
    [SerializeField] private float attackCooldown = 0.1f;
    
    [Header("=== DAMAGE VALUES ===")]
    [SerializeField] private int combo1Damage = 10;
    [SerializeField] private int combo2Damage = 20;
    [SerializeField] private int combo3Damage = 35;
    [SerializeField] private int heavyDamageMin = 50;
    [SerializeField] private int heavyDamageMax = 80;
    [SerializeField] private float heavyChargeTimeMax = 1.5f;
    [SerializeField] private int diveDamage = 45;
    [SerializeField] private int pounceDamage = 40;
    
    [Header("=== HITBOX SETTINGS ===")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
    
    // Combo State
    private int currentComboStep = 0;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private bool canQueueNextAttack = false;
    private bool nextAttackQueued = false;
    
    // Heavy Attack State
    private bool isChargingHeavy = false;
    private float heavyChargeStartTime = 0f;
    
    // Input Tracking
    private float attackButtonHoldTime = 0f;
    
    // Animation Hashes (Performance optimization)
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimComboStep = Animator.StringToHash("ComboStep");
    private static readonly int AnimHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");
    
    // ==================== UNITY METHODS ====================
    
    void Start()
    {
        // Auto-find references
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        
        // Create attack point if not assigned
        if (attackPoint == null)
        {
            GameObject ap = new GameObject("AttackPoint");
            ap.transform.SetParent(transform);
            ap.transform.localPosition = new Vector3(0, 1f, 1f); // In front of player
            attackPoint = ap.transform;
            DebugLog("Created AttackPoint automatically");
        }
        
        DebugLog("PlayerCombat initialized!");
    }
    
    void Update()
    {
        HandleAttackInput();
    }
    
    // ==================== INPUT HANDLING ====================
    
    private void HandleAttackInput()
    {
        // Mouse button pressed this frame
        if (Input.GetMouseButtonDown(0))
        {
            attackButtonHoldTime = 0f;
        }
        
        // Mouse button held
        if (Input.GetMouseButton(0))
        {
            attackButtonHoldTime += Time.deltaTime;
            
            // Start charging heavy after 0.3 seconds
            if (attackButtonHoldTime >= 0.3f && !isChargingHeavy && !isAttacking)
            {
                StartHeavyCharge();
            }
        }
        
        // Mouse button released
        if (Input.GetMouseButtonUp(0))
        {
            if (isChargingHeavy)
            {
                ReleaseHeavyAttack();
            }
            else if (attackButtonHoldTime < 0.3f)
            {
                TryComboAttack();
            }
            
            attackButtonHoldTime = 0f;
        }
    }
    
    // ==================== COMBO SYSTEM ====================
    
    private void TryComboAttack()
    {
        // Check cooldown
        if (Time.time - lastAttackTime < attackCooldown)
            return;
        
        // If attacking, queue next attack
        if (isAttacking)
        {
            if (canQueueNextAttack && currentComboStep < 3)
            {
                nextAttackQueued = true;
                DebugLog("Queued next attack");
            }
            return;
        }
        
        PerformComboAttack();
    }
    
    private void PerformComboAttack()
    {
        // Check combo window
        if (currentComboStep > 0 && Time.time - lastAttackTime > comboWindowTime)
        {
            currentComboStep = 0;
            DebugLog("Combo window expired, resetting");
        }
        
        // Advance combo
        currentComboStep++;
        if (currentComboStep > 3)
            currentComboStep = 1;
        
        int damage = GetComboDamage(currentComboStep);
        DebugLog($">>> COMBO {currentComboStep} <<< Damage: {damage}");
        
        // Trigger animation
        if (animator != null)
        {
            animator.SetInteger(AnimComboStep, currentComboStep);
            animator.SetTrigger(AnimAttack);
            animator.SetBool(AnimIsAttacking, true);
        }
        
        // Update state
        isAttacking = true;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        lastAttackTime = Time.time;
    }
    
    private int GetComboDamage(int step)
    {
        switch (step)
        {
            case 1: return combo1Damage;
            case 2: return combo2Damage;
            case 3: return combo3Damage;
            default: return combo1Damage;
        }
    }
    
    // ==================== HEAVY ATTACK ====================
    
    private void StartHeavyCharge()
    {
        isChargingHeavy = true;
        heavyChargeStartTime = Time.time;
        DebugLog("Charging HEAVY ATTACK...");
        
        // TODO: Add charge VFX/sound here
    }
    
    private void ReleaseHeavyAttack()
    {
        float chargeTime = Time.time - heavyChargeStartTime;
        float chargePercent = Mathf.Clamp01(chargeTime / heavyChargeTimeMax);
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, chargePercent));
        
        DebugLog($">>> HEAVY ATTACK <<< Charge: {chargePercent * 100:F0}% Damage: {damage}");
        
        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger(AnimHeavyAttack);
            animator.SetBool(AnimIsAttacking, true);
        }
        
        isChargingHeavy = false;
        isAttacking = true;
        lastAttackTime = Time.time;
        currentComboStep = 0; // Reset combo
    }
    
    public float GetHeavyChargePercent()
    {
        if (!isChargingHeavy) return 0f;
        return Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
    }
    
    // ==================== HIT DETECTION ====================
    
    /// <summary>
    /// Call this from Animation Event when attack should deal damage
    /// </summary>
    public void DealDamage()
    {
        int damage = GetComboDamage(currentComboStep);
        DealDamageInRange(damage);
    }
    
    /// <summary>
    /// Call this from Animation Event for heavy attack damage
    /// </summary>
    public void DealHeavyDamage()
    {
        float chargePercent = GetHeavyChargePercent();
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, chargePercent));
        DealDamageInRange(damage);
    }
    
    private void DealDamageInRange(int damage)
    {
        // Find all enemies in attack range
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);
        
        foreach (Collider enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                DebugLog($"Hit {enemy.name} for {damage} damage!");
            }
        }
        
        if (hitEnemies.Length == 0)
        {
            DebugLog("Attack missed (no enemies in range)");
        }
    }
    
    // ==================== ANIMATION EVENTS ====================
    // Add these as Animation Events in Unity
    
    /// <summary>
    /// Called when player can input next combo attack
    /// </summary>
    public void OnCanQueueNextAttack()
    {
        canQueueNextAttack = true;
        
        if (nextAttackQueued)
        {
            nextAttackQueued = false;
            PerformComboAttack();
        }
    }
    
    /// <summary>
    /// Called when attack animation ends
    /// </summary>
    public void OnAttackEnd()
    {
        DebugLog($"Attack END");
        isAttacking = false;
        canQueueNextAttack = false;
        
        if (animator != null)
            animator.SetBool(AnimIsAttacking, false);
    }
    
    // ==================== PUBLIC GETTERS ====================
    
    public bool IsAttacking() => isAttacking;
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    
    // ==================== DEBUG ====================
    
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[PlayerCombat] {message}");
    }
    
    // Draw attack range in Scene view
    void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo) return;
        
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
        else
        {
            // Draw in front of player if no attack point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.forward * 1f + Vector3.up, attackRange);
        }
    }
}

