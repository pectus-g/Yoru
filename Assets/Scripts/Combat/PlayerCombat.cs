using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Direct animation control with hit reactions + knockback.
/// Combat Layer stays at weight 1 always.
/// Hit reactions block attacks but allow movement (no PlayerMovement changes).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;

    [Header("Animation State Names")]
    [SerializeField] private string combo1StateName = "Combo1";
    [SerializeField] private string combo2StateName = "Combo2";
    [SerializeField] private string combo3StateName = "Combo3";
    [SerializeField] private string heavyStateName = "HeavyAttack";
    [SerializeField] private string combatIdleStateName = "Combat_Idle";

    [Header("Hit Reaction State Names")]
    [SerializeField] private string hitReactLight2Leg = "HitReact_Light_2Leg";
    [SerializeField] private string hitReactLight4Leg = "HitReact_Light_4Leg";
    [SerializeField] private string hitReactHeavy2Leg = "HitReact_Heavy_2Leg";
    [SerializeField] private string hitReactHeavy4Leg = "HitReact_Running_4Leg";

    [Header("Hit Reaction Timing")]
    [SerializeField] private float lightHitReactDuration = 0.65f;
    [SerializeField] private float heavyHitReactDuration = 1.0f;

    [Header("Knockback Pull")]
    [Tooltip("How far the player gets pulled toward attacker on Hair Lash hit")]
    [SerializeField] private float pullDistance = 2.5f;
    [Tooltip("How long the pull takes (smooth over this many seconds)")]
    [SerializeField] private float pullDuration = 0.15f;

    [Header("Layer Settings")]
    [SerializeField] private int combatLayerIndex = 1;

    [Header("Combo Settings")]
    [SerializeField] private float comboWindowTime = 2.0f;
    [SerializeField] private float attackCooldown = 0.1f;

    [Header("Damage")]
    [SerializeField] private int combo1Damage = 5;
    [SerializeField] private int combo2Damage = 8;
    [SerializeField] private int combo3Damage = 15;
    [SerializeField] private int heavyDamageMin = 20;
    [SerializeField] private int heavyDamageMax = 40;
    [SerializeField] private float heavyChargeTimeMax = 1.5f;
    [SerializeField] private int aerialSpinDamage = 12;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX")]
    [SerializeField] private ParticleSystem spinVFX;

    [Header("Combat VFX")]
    [Tooltip("VFX for light hit reaction")]
    [SerializeField] private ParticleSystem lightHitVFX;
    [Tooltip("VFX for heavy hit reaction")]
    [SerializeField] private ParticleSystem heavyHitVFX;
    [Tooltip("VFX for combo 1")]
    [SerializeField] private ParticleSystem combo1VFX;
    [Tooltip("VFX for combo 2")]
    [SerializeField] private ParticleSystem combo2VFX;
    [Tooltip("VFX for combo 3")]
    [SerializeField] private ParticleSystem combo3VFX;
    [Tooltip("VFX for heavy attack")]
    [SerializeField] private ParticleSystem heavyAttackVFX;

    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
    #endregion

    #region Private Fields
    private CharacterController characterController;
    private Transform cachedTransform;
    private PlayerMovement playerMovement;

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

    // Hit reaction
    private bool isInHitReaction;
    private float hitReactionEndTime;

    // Pullback coroutine
    private Coroutine pullCoroutine;

    // Animation hashes
    private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        characterController = GetComponent<CharacterController>();
        cachedTransform = transform;
        playerMovement = GetComponent<PlayerMovement>();

        if (attackPoint == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(cachedTransform);
            ap.transform.localPosition = new Vector3(0f, 1f, 1f);
            attackPoint = ap.transform;
            DebugLog("Created AttackPoint automatically");
        }

        if (animator != null)
        {
            animator.SetLayerWeight(combatLayerIndex, 1f);
        }

        if (spinVFX == null)
        {
            DebugLog("⚠️ Spin VFX not assigned!");
        }

        DebugLog("PlayerCombat initialized (Direct Animation Control)");
    }

    private void Update()
    {
        EnforcePositionLock();
        UpdateHitReaction();
        HandleInput();
        CheckGroundedStatus();

        // Safety timeout
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("⚠️ SAFETY: Attack timeout, resetting");
            ForceResetCombat();
        }

        // Debug reset
        if (Input.GetKeyDown(KeyCode.R))
        {
            DebugLog("DEBUG: Manual reset");
            ForceResetCombat();
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();
    }

    private void EnforcePositionLock()
    {
        if (!lockPosition || characterController == null || !wasGroundedWhenLocked)
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

    #region Hit Reaction
    /// <summary>
    /// Backward compatible — no knockback.
    /// </summary>
    public void PlayHitReaction(bool isHeavy)
    {
        PlayHitReaction(isHeavy, Vector3.zero);
    }

    /// <summary>
    /// Called by PlayerHealth when taking damage.
    /// Cancels current attack, plays flinch via animator.Play (immediate, no blend issues).
    /// Smoothly pulls player toward attacker over pullDuration seconds.
    /// </summary>
    public void PlayHitReaction(bool isHeavy, Vector3 attackerPos)
    {
        // Reset combat state directly (no ReturnToIdle — avoids double transition)
        isAttacking = false;
        isChargingHeavy = false;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        currentComboStep = 0;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        storedHeavyChargePercent = 0f;
        UnlockPosition();
        VFX_SpinStop();

        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        // Smooth pullback toward attacker over time
        if (attackerPos != Vector3.zero && characterController != null)
        {
            Vector3 pullDir = attackerPos - cachedTransform.position;
            pullDir.y = 0f;

            if (pullDir.sqrMagnitude > 0.01f)
            {
                // Stop any existing pull
                if (pullCoroutine != null)
                    StopCoroutine(pullCoroutine);

                pullCoroutine = StartCoroutine(SmoothPull(pullDir.normalized, pullDistance, pullDuration));
                DebugLog($"↩️ Pulling toward attacker ({pullDistance}m over {pullDuration}s)");
            }
        }

        // Pick animation based on stance and severity
        bool is4Leg = playerMovement != null && playerMovement.IsRunning();
        string animState;
        float duration;

        if (isHeavy)
        {
            animState = is4Leg ? hitReactHeavy4Leg : hitReactHeavy2Leg;
            duration = heavyHitReactDuration;
            PlayVFX(heavyHitVFX);
        }
        else
        {
            animState = is4Leg ? hitReactLight4Leg : hitReactLight2Leg;
            duration = lightHitReactDuration;
            PlayVFX(lightHitVFX);
        }

        // USE animator.Play() — immediate, no blend issues from empty/idle states
        // CrossFadeInFixedTime was being swallowed when blending from Combat_Idle
        if (animator != null)
        {
            animator.Play(animState, combatLayerIndex, 0f);
            animator.Play(animState, 0, 0f); // Also play on base layer to prevent locomotion override
            DebugLog($"🤕 Hit React: {animState} ({duration}s) [animator.Play]");
        }

        isInHitReaction = true;
        hitReactionEndTime = Time.time + duration;
    }

    /// <summary>
    /// Smoothly pulls the player in a direction over time.
    /// Feels like getting yanked by the hair lash.
    /// </summary>
    private IEnumerator SmoothPull(Vector3 direction, float distance, float duration)
    {
        float elapsed = 0f;
        float moved = 0f;

        while (elapsed < duration && moved < distance)
        {
            elapsed += Time.deltaTime;

            // Ease-out curve — fast start, slow finish (feels like impact)
            float t = elapsed / duration;
            float speedMultiplier = Mathf.Lerp(2f, 0.2f, t);
            float step = (distance / duration) * speedMultiplier * Time.deltaTime;

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(direction * step);
                moved += step;
            }

            yield return null;
        }

        pullCoroutine = null;
    }

    /// <summary>
    /// Tick hit reaction timer. Returns to idle when done.
    /// </summary>
    private void UpdateHitReaction()
    {
        if (isInHitReaction && Time.time >= hitReactionEndTime)
        {
            isInHitReaction = false;
            ReturnToIdle();
            DebugLog("Hit reaction ended");
        }
    }
    #endregion

    #region Input
    private void HandleInput()
    {
        // Block all combat input during hit reaction
        if (isInHitReaction) return;

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
            if (currentComboStep < 3)
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

        if (currentComboStep > 0 && Time.time - lastAttackTime > comboWindowTime)
        {
            currentComboStep = 0;
            DebugLog("Combo window expired");
        }

        currentComboStep++;
        if (currentComboStep > 3)
            currentComboStep = 1;

        DebugLog($">>> COMBO {currentComboStep} <<< Damage: {GetComboDamage(currentComboStep)}");

        if (currentComboStep == 3)
        {
            UnlockPosition();
            DebugLog("GROUND SPIN - movement allowed");
        }
        else
        {
            LockPositionNow();
        }

        string stateName = GetComboStateName(currentComboStep);
        PlayCombatAnimation(stateName);

        switch (currentComboStep)
        {
            case 1: PlayVFX(combo1VFX); break;
            case 2: PlayVFX(combo2VFX); break;
            case 3: PlayVFX(combo3VFX); break;
        }

        animator.SetInteger(HashComboStep, currentComboStep);
        animator.SetBool(HashIsAttacking, true);

        isAttacking = true;
        isAerialAttack = false;
        canQueueNextAttack = false;
        nextAttackQueued = false;
        lastAttackTime = Time.time;
    }

    private string GetComboStateName(int step)
    {
        switch (step)
        {
            case 1: return combo1StateName;
            case 2: return combo2StateName;
            case 3: return combo3StateName;
            default: return combo1StateName;
        }
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

    #region Aerial Spin
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

        UnlockPosition();

        PlayCombatAnimation(combo3StateName);

        animator.SetInteger(HashComboStep, 3);
        animator.SetBool(HashIsAttacking, true);

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
        DebugLog("Charging HEAVY...");
    }

    private void ReleaseHeavyAttack()
    {
        attackStartTime = Time.time;

        storedHeavyChargePercent = Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, storedHeavyChargePercent));

        DebugLog($">>> HEAVY <<< {storedHeavyChargePercent * 100f:F0}% = {damage} dmg");

        LockPositionNow();

        PlayCombatAnimation(heavyStateName);
        PlayVFX(heavyAttackVFX);

        animator.SetBool(HashIsAttacking, true);

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

    #region Animation Playback
    private void PlayCombatAnimation(string stateName)
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(stateName, 0.05f, combatLayerIndex);
        DebugLog($"Playing: {stateName}");
    }

    private void ReturnToIdle()
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(combatIdleStateName, 0.1f, combatLayerIndex);
        DebugLog("Returning to Combat_Idle");
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
        int damage = isAerialAttack ? aerialSpinDamage : GetComboDamage(currentComboStep);
        bool isFinisher = !isAerialAttack && currentComboStep == 3;
        DealDamageInRange(damage, isFinisher);  // combo 3 finisher triggers stagger
    }

    public void DealHeavyDamage()
    {
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, storedHeavyChargePercent));
        DealDamageInRange(damage, true);
    }

    private void DealDamageInRange(int damage, bool isHeavy)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, isHeavy);
                DebugLog($"Hit {enemy.name} for {damage}{(isHeavy ? " (HEAVY)" : "")}!");
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
    }

    public void VFX_SpinStop()
    {
        if (spinVFX != null)
        {
            spinVFX.Stop();
            DebugLog("🌀 SPIN VFX STOP");
        }
    }
    
    private void PlayVFX(ParticleSystem vfx)
    {
        if (vfx != null) vfx.Play();
    }
    #endregion

    #region Animation Events
    public void OnCanQueueNextAttack()
    {
        canQueueNextAttack = true;
        DebugLog($"Can queue (combo {currentComboStep})");

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
        ReturnToIdle();
        animator.SetBool(HashIsAttacking, false);
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
        isInHitReaction = false;

        if (pullCoroutine != null)
        {
            StopCoroutine(pullCoroutine);
            pullCoroutine = null;
        }

        UnlockPosition();
        VFX_SpinStop();
        ReturnToIdle();

        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
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
    public bool IsInHitReaction() => isInHitReaction;
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Combat] {message}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo || attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
    #endregion
}