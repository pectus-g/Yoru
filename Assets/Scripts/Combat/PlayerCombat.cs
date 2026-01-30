using UnityEngine;

/// <summary>
/// YORU Combat System
/// - Combo 1 & 2 (paw attacks): Lock position when grounded
/// - Combo 3 (spin): Allow movement, VFX via animation events
/// - Aerial attacks: Configurable behavior
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;
    
    [Header("Combo Settings")]
    [SerializeField] private float comboWindowTime = 1.5f;
    [SerializeField] private float attackCooldown = 0.1f;
    
    [Header("Damage")]
    [SerializeField] private int combo1Damage = 10;
    [SerializeField] private int combo2Damage = 20;
    [SerializeField] private int combo3Damage = 35;
    [SerializeField] private int heavyDamageMin = 50;
    [SerializeField] private int heavyDamageMax = 80;
    [SerializeField] private float heavyChargeTimeMax = 1.5f;
    [SerializeField] private int aerialDamage = 15;
    
    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    
    [Header("VFX")]
    [SerializeField] private ParticleSystem spinVFX;
    
    [Header("Aerial Combat")]
    [Tooltip("How to handle attacks while airborne")]
    [SerializeField] private AerialAttackMode aerialMode = AerialAttackMode.SingleSwipe;
    
    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 3f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
    #endregion
    
    #region Enums
    public enum AerialAttackMode
    {
        Disabled,           // No attacks while airborne
        SingleSwipe,        // Only combo 1, no combo progression
        FullCombo           // Allow full combo in air (current behavior)
    }
    #endregion
    
    #region Private Fields
    private CharacterController characterController;
    private Transform cachedTransform;
    
    // Combo state
    private int currentComboStep;
    private float lastAttackTime;
    private bool isAttacking;
    private bool canQueueNextAttack;
    private bool nextAttackQueued;
    private bool isAerialAttack;
    
    // Heavy attack
    private bool isChargingHeavy;
    private float heavyChargeStartTime;
    
    // Input
    private float attackButtonHoldTime;
    
    // Safety
    private float attackStartTime;
    
    // Position lock
    private bool lockPosition;
    private Vector3 lockedPosition;
    private float lockedYRotation;
    
    // Animation hashes
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    private static readonly int HashHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
    #endregion
    
    #region Unity Lifecycle
    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        
        characterController = GetComponent<CharacterController>();
        cachedTransform = transform;
        
        if (attackPoint == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(cachedTransform);
            ap.transform.localPosition = new Vector3(0f, 1f, 1f);
            attackPoint = ap.transform;
            DebugLog("Created AttackPoint automatically");
        }
        
        DebugLog("PlayerCombat initialized");
    }
    
    private void Update()
    {
        HandleInput();
        
        // Safety timeout
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("SAFETY: Attack stuck, forcing reset");
            ForceResetCombat();
        }
        
        // Debug reset
        if (Input.GetKeyDown(KeyCode.R))
        {
            DebugLog("DEBUG: Manual combat reset");
            ForceResetCombat();
        }
    }
    
    private void LateUpdate()
    {
        // Only lock position when grounded and lock is active
        if (lockPosition && characterController != null && characterController.isGrounded)
        {
            characterController.enabled = false;
            cachedTransform.position = lockedPosition;
            cachedTransform.rotation = Quaternion.Euler(0f, lockedYRotation, 0f);
            characterController.enabled = true;
        }
    }
    #endregion
    
    #region Input
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            attackButtonHoldTime = 0f;
        }
        
        if (Input.GetMouseButton(0))
        {
            attackButtonHoldTime += Time.deltaTime;
            
            // Heavy charge only when grounded
            bool isGrounded = characterController != null && characterController.isGrounded;
            if (attackButtonHoldTime >= 0.3f && !isChargingHeavy && !isAttacking && isGrounded)
            {
                StartHeavyCharge();
            }
        }
        
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
    #endregion
    
    #region Combo System
    private void TryComboAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;
        
        bool isGrounded = characterController != null && characterController.isGrounded;
        
        // Handle aerial attack restrictions
        if (!isGrounded)
        {
            switch (aerialMode)
            {
                case AerialAttackMode.Disabled:
                    DebugLog("Aerial attacks disabled");
                    return;
                    
                case AerialAttackMode.SingleSwipe:
                    // Only allow one attack in air, no combo
                    if (isAerialAttack || isAttacking)
                    {
                        DebugLog("Already did aerial attack");
                        return;
                    }
                    PerformAerialAttack();
                    return;
                    
                case AerialAttackMode.FullCombo:
                    // Fall through to normal combo
                    break;
            }
        }
        else
        {
            // Reset aerial flag when grounded
            isAerialAttack = false;
        }
        
        // Queue attack if currently attacking
        if (isAttacking)
        {
            if (canQueueNextAttack && currentComboStep < 3)
            {
                nextAttackQueued = true;
                DebugLog($"Queued combo {currentComboStep + 1}");
            }
            return;
        }
        
        PerformComboAttack();
    }
    
    private void PerformAerialAttack()
    {
        attackStartTime = Time.time;
        isAerialAttack = true;
        currentComboStep = 1; // Always combo 1 animation for aerial
        
        DebugLog($">>> AERIAL ATTACK <<< Damage: {aerialDamage}");
        
        if (animator != null)
        {
            animator.SetInteger(HashComboStep, 1);
            animator.SetTrigger(HashAttack);
            animator.SetBool(HashIsAttacking, true);
        }
        
        isAttacking = true;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        lastAttackTime = Time.time;
    }
    
    private void PerformComboAttack()
    {
        attackStartTime = Time.time;
        
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
        
        DebugLog($">>> COMBO {currentComboStep} <<< Damage: {GetComboDamage(currentComboStep)}");
        
        // Position locking based on attack type
        if (currentComboStep == 3)
        {
            // SPIN - unlock position
            UnlockPosition();
            DebugLog("SPIN ATTACK - movement allowed");
        }
        else
        {
            // PAW ATTACKS - lock if grounded
            bool isGrounded = characterController != null && characterController.isGrounded;
            if (isGrounded)
            {
                LockPosition();
            }
        }
        
        // Trigger animation
        if (animator != null)
        {
            animator.SetInteger(HashComboStep, currentComboStep);
            animator.SetTrigger(HashAttack);
            animator.SetBool(HashIsAttacking, true);
        }
        
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
    #endregion
    
    #region Heavy Attack
    private void StartHeavyCharge()
    {
        isChargingHeavy = true;
        heavyChargeStartTime = Time.time;
        currentComboStep = 0; // Reset combo when charging heavy
        DebugLog("Charging HEAVY ATTACK...");
    }
    
    private void ReleaseHeavyAttack()
    {
        attackStartTime = Time.time;
        
        float chargePercent = Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, chargePercent));
        
        DebugLog($">>> HEAVY ATTACK <<< Charge: {chargePercent * 100f:F0}% Damage: {damage}");
        
        // Lock position if grounded
        bool isGrounded = characterController != null && characterController.isGrounded;
        if (isGrounded)
        {
            LockPosition();
        }
        
        if (animator != null)
        {
            animator.SetTrigger(HashHeavyAttack);
            animator.SetBool(HashIsAttacking, true);
        }
        
        isChargingHeavy = false;
        isAttacking = true;
        lastAttackTime = Time.time;
        currentComboStep = 0;
    }
    
    public float GetHeavyChargePercent()
    {
        if (!isChargingHeavy) return 0f;
        return Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
    }
    #endregion
    
    #region Position Lock
    private void LockPosition()
    {
        if (!lockPosition)
        {
            lockPosition = true;
            lockedPosition = cachedTransform.position;
            lockedYRotation = cachedTransform.eulerAngles.y;
            DebugLog("Position LOCKED");
        }
    }
    
    private void UnlockPosition()
    {
        if (lockPosition)
        {
            lockPosition = false;
            DebugLog("Position UNLOCKED");
        }
    }
    #endregion
    
    #region Hit Detection
    public void DealDamage()
    {
        int damage;
        if (isAerialAttack)
        {
            damage = aerialDamage;
        }
        else
        {
            damage = GetComboDamage(currentComboStep);
        }
        DealDamageInRange(damage);
    }
    
    public void DealHeavyDamage()
    {
        float chargePercent = GetHeavyChargePercent();
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, chargePercent));
        DealDamageInRange(damage);
    }
    
    private void DealDamageInRange(int damage)
    {
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
    #endregion
    
    #region VFX - Call via Animation Events
    /// <summary>
    /// Call via Animation Event at START of spin animation (frame 0)
    /// </summary>
    public void VFX_SpinStart()
    {
        if (spinVFX != null)
        {
            spinVFX.Play();
            DebugLog(">>> SPIN VFX START <<<");
        }
    }
    
    /// <summary>
    /// Call via Animation Event at END of spin animation (last frame)
    /// </summary>
    public void VFX_SpinStop()
    {
        if (spinVFX != null)
        {
            spinVFX.Stop();
            DebugLog(">>> SPIN VFX STOP <<<");
        }
    }
    
    // Legacy method - keep for compatibility
    public void TriggerSpinVFX()
    {
        VFX_SpinStart();
    }
    #endregion
    
    #region Animation Events
    /// <summary>
    /// Call via Animation Event at ~60% of attack animation
    /// This allows queuing the next combo hit
    /// </summary>
    public void OnCanQueueNextAttack()
    {
        canQueueNextAttack = true;
        DebugLog($"Can queue next attack (current: combo {currentComboStep})");
        
        if (nextAttackQueued)
        {
            nextAttackQueued = false;
            DebugLog("Processing queued attack!");
            PerformComboAttack();
        }
    }
    
    /// <summary>
    /// Call via Animation Event at END of attack animation (last frame)
    /// </summary>
    public void OnAttackEnd()
    {
        if (!isAttacking)
        {
            return;
        }
        
        DebugLog($"Attack END (was combo {currentComboStep})");
        
        isAttacking = false;
        canQueueNextAttack = false;
        lastAttackTime = Time.time;
        
        // Reset aerial flag when attack ends
        if (isAerialAttack)
        {
            isAerialAttack = false;
        }
        
        UnlockPosition();
        
        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
        }
    }
    #endregion
    
    #region Reset
    public void ForceResetCombat()
    {
        isAttacking = false;
        isChargingHeavy = false;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        currentComboStep = 0;
        attackStartTime = 0f;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        
        UnlockPosition();
        VFX_SpinStop();
        
        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
            animator.ResetTrigger(HashAttack);
            animator.ResetTrigger(HashHeavyAttack);
        }
        
        DebugLog("Combat state force reset");
    }
    #endregion
    
    #region Public Getters
    public bool IsAttacking() => isAttacking;
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    public bool IsAerialAttack() => isAerialAttack;
    #endregion
    
    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Combat] {message}");
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo) return;
        
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + transform.forward + Vector3.up, attackRange);
        }
    }
    #endregion
}