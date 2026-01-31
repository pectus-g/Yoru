using UnityEngine;

/// <summary>
/// YORU Combat System
/// - Ground: 3-hit combo (paw, paw, spin)
/// - Air: Single spin attack (once per jump)
/// - Heavy attack: Hold M1 on ground
/// 
/// Uses Layer Weight to control Combat Layer - eliminates transition freeze!
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;
    
    [Header("Layer Settings")]
    [Tooltip("Index of Combat Layer in Animator (usually 1)")]
    [SerializeField] private int combatLayerIndex = 1;
    
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
    [SerializeField] private int aerialSpinDamage = 25;
    
    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;
    
    [Header("VFX")]
    [Tooltip("Drag your spin ParticleSystem here!")]
    [SerializeField] private ParticleSystem spinVFX;
    
    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 3f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
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
    
    // Aerial
    private bool isAerialAttack;
    private bool hasUsedAerialAttack;
    
    // Heavy attack
    private bool isChargingHeavy;
    private float heavyChargeStartTime;
    private float storedHeavyChargePercent;
    
    // Input
    private float attackButtonHoldTime;
    
    // Safety
    private float attackStartTime;
    
    // Position lock
    private bool lockPosition;
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private bool wasGroundedWhenLocked;
    
    // Animation hashes
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    private static readonly int HashHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int HashAerialSpin = Animator.StringToHash("AerialSpin");
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
        
        // Start with Combat Layer OFF
        SetCombatLayerWeight(0f);
        
        // Check if spinVFX is assigned
        if (spinVFX == null)
        {
            DebugLog("⚠️ Spin VFX not assigned in Inspector!");
        }
        
        DebugLog("PlayerCombat initialized (Layer Weight Control)");
    }
    
    private void Update()
    {
        // Enforce position lock BEFORE anything else
        EnforcePositionLock();
        
        HandleInput();
        CheckGroundedStatus();
        
        // Safety timeout
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("⚠️ SAFETY: Attack stuck, forcing reset");
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
        // Also enforce in LateUpdate to catch any movement
        EnforcePositionLock();
    }
    
    private void EnforcePositionLock()
    {
        if (!lockPosition || characterController == null)
            return;
        
        if (!wasGroundedWhenLocked)
            return;
        
        characterController.enabled = false;
        cachedTransform.position = lockedPosition;
        cachedTransform.rotation = lockedRotation;
        characterController.enabled = true;
    }
    
    private void CheckGroundedStatus()
    {
        if (characterController != null && characterController.isGrounded)
        {
            if (hasUsedAerialAttack && !isAttacking)
            {
                hasUsedAerialAttack = false;
                isAerialAttack = false;
            }
        }
    }
    #endregion
    
    #region Layer Weight Control
    /// <summary>
    /// Controls Combat Layer weight - this is the key to eliminating freeze!
    /// Weight 1 = Combat Layer active (shows attacks)
    /// Weight 0 = Combat Layer off (Base Layer shows through)
    /// </summary>
    private void SetCombatLayerWeight(float weight)
    {
        if (animator != null)
        {
            animator.SetLayerWeight(combatLayerIndex, weight);
            DebugLog($"Combat Layer Weight → {weight}");
        }
    }
    #endregion
    
    #region Input
    private void HandleInput()
    {
        bool isGrounded = characterController != null && characterController.isGrounded;
        
        if (Input.GetMouseButtonDown(0))
        {
            attackButtonHoldTime = 0f;
        }
        
        if (Input.GetMouseButton(0))
        {
            attackButtonHoldTime += Time.deltaTime;
            
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
                if (isGrounded)
                {
                    TryGroundCombo();
                }
                else
                {
                    TryAerialSpin();
                }
            }
            
            attackButtonHoldTime = 0f;
        }
    }
    #endregion
    
    #region Ground Combo
    private void TryGroundCombo()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;
        
        if (isAttacking)
        {
            if (canQueueNextAttack && currentComboStep < 3)
            {
                nextAttackQueued = true;
                DebugLog($"Queued combo {currentComboStep + 1}");
            }
            return;
        }
        
        PerformGroundCombo();
    }
    
    private void PerformGroundCombo()
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
        
        // TURN ON COMBAT LAYER
        SetCombatLayerWeight(1f);
        
        // Position lock based on attack type
        if (currentComboStep == 3)
        {
            UnlockPosition();
            DebugLog("GROUND SPIN - movement allowed");
        }
        else
        {
            LockPositionNow();
        }
        
        // Trigger animation
        if (animator != null)
        {
            animator.SetInteger(HashComboStep, currentComboStep);
            animator.SetTrigger(HashAttack);
            animator.SetBool(HashIsAttacking, true);
        }
        
        isAttacking = true;
        isAerialAttack = false;
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
    
    #region Aerial Spin Attack
    private void TryAerialSpin()
    {
        if (hasUsedAerialAttack)
        {
            DebugLog("Already used aerial spin this jump");
            return;
        }
        
        if (isAttacking)
        {
            DebugLog("Already attacking");
            return;
        }
        
        PerformAerialSpin();
    }
    
    private void PerformAerialSpin()
    {
        attackStartTime = Time.time;
        hasUsedAerialAttack = true;
        isAerialAttack = true;
        currentComboStep = 3;
        
        DebugLog($">>> AERIAL SPIN <<< Damage: {aerialSpinDamage}");
        
        // TURN ON COMBAT LAYER
        SetCombatLayerWeight(1f);
        
        // No position lock in air
        UnlockPosition();
        
        // Trigger spin animation
        if (animator != null)
        {
            animator.SetInteger(HashComboStep, 3);
            animator.SetTrigger(HashAerialSpin);
            animator.SetBool(HashIsAttacking, true);
        }
        
        isAttacking = true;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        lastAttackTime = Time.time;
    }
    #endregion
    
    #region Heavy Attack
    private void StartHeavyCharge()
    {
        isChargingHeavy = true;
        heavyChargeStartTime = Time.time;
        currentComboStep = 0;
        DebugLog("Charging HEAVY ATTACK...");
    }
    
    private void ReleaseHeavyAttack()
    {
        attackStartTime = Time.time;
        
        storedHeavyChargePercent = Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, storedHeavyChargePercent));
        
        DebugLog($">>> HEAVY ATTACK <<< Charge: {storedHeavyChargePercent * 100f:F0}% Damage: {damage}");
        
        // TURN ON COMBAT LAYER
        SetCombatLayerWeight(1f);
        
        // Lock position
        LockPositionNow();
        
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
    private void LockPositionNow()
    {
        lockPosition = true;
        lockedPosition = cachedTransform.position;
        lockedRotation = cachedTransform.rotation;
        wasGroundedWhenLocked = characterController != null && characterController.isGrounded;
        
        if (characterController != null)
        {
            characterController.enabled = false;
            cachedTransform.position = lockedPosition;
            cachedTransform.rotation = lockedRotation;
            characterController.enabled = true;
        }
        
        DebugLog("Position LOCKED");
    }
    
    private void UnlockPosition()
    {
        if (lockPosition)
        {
            lockPosition = false;
            wasGroundedWhenLocked = false;
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
            damage = aerialSpinDamage;
        }
        else
        {
            damage = GetComboDamage(currentComboStep);
        }
        DealDamageInRange(damage);
    }
    
    public void DealHeavyDamage()
    {
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, storedHeavyChargePercent));
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
    }
    #endregion
    
    #region VFX - Animation Events
    public void VFX_SpinStart()
    {
        if (spinVFX != null)
        {
            spinVFX.Play();
            DebugLog("🌀 SPIN VFX START");
        }
        else
        {
            DebugLog("⚠️ VFX_SpinStart called but spinVFX is NULL!");
        }
    }
    
    public void VFX_SpinStop()
    {
        if (spinVFX != null)
        {
            spinVFX.Stop();
            DebugLog("🌀 SPIN VFX STOP");
        }
    }
    #endregion
    
    #region Animation Events
    public void OnCanQueueNextAttack()
    {
        canQueueNextAttack = true;
        DebugLog($"Can queue (current: combo {currentComboStep})");
        
        if (nextAttackQueued)
        {
            nextAttackQueued = false;
            DebugLog("Processing queued attack!");
            PerformGroundCombo();
        }
    }
    
    public void OnAttackEnd()
    {
        if (!isAttacking)
            return;
        
        string attackType = isAerialAttack ? "AERIAL SPIN" : (currentComboStep == 0 ? "HEAVY" : $"combo {currentComboStep}");
        DebugLog($"Attack END ({attackType})");
        
        isAttacking = false;
        canQueueNextAttack = false;
        lastAttackTime = Time.time;
        
        if (isAerialAttack)
        {
            isAerialAttack = false;
        }
        
        UnlockPosition();
        
        // TURN OFF COMBAT LAYER - Base Layer takes over instantly!
        SetCombatLayerWeight(0f);
        
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
        hasUsedAerialAttack = false;
        storedHeavyChargePercent = 0f;
        
        UnlockPosition();
        VFX_SpinStop();
        
        // TURN OFF COMBAT LAYER
        SetCombatLayerWeight(0f);
        
        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
            animator.ResetTrigger(HashAttack);
            animator.ResetTrigger(HashHeavyAttack);
            animator.ResetTrigger(HashAerialSpin);
        }
        
        DebugLog("Combat RESET");
    }
    #endregion
    
    #region Public Getters
    public bool IsAttacking() => isAttacking;
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    public bool IsAerialAttack() => isAerialAttack;
    public bool IsPositionLocked() => lockPosition;
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
    }
    #endregion
}