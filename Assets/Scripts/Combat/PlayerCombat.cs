using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Phase 3A v3: Camera-relative dodge, auto clip-length, i-frame support.
/// Combat Layer stays at weight 1 always. Animations via CrossFadeInFixedTime / animator.Play.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;

    [Header("Animation State Names — Combo")]
    [SerializeField] private string combo1StateName = "Combo1";
    [SerializeField] private string combo2StateName = "Combo2";
    [SerializeField] private string combo3StateName = "Combo3";
    [SerializeField] private string heavyStateName = "HeavyAttack";
    [SerializeField] private string combatIdleStateName = "Combat_Idle";

    [Header("Animation State Names — Hit Reaction")]
    [SerializeField] private string hitReactLight2Leg = "HitReact_Light_2Leg";
    [SerializeField] private string hitReactLight4Leg = "HitReact_Light_4Leg";
    [SerializeField] private string hitReactHeavy2Leg = "HitReact_Heavy_2Leg";
    [SerializeField] private string hitReactHeavy4Leg = "HitReact_Running_4Leg";

    [Header("Animation State Names — Dodge")]
    [SerializeField] private string dodgeBackflip2LegState = "Dodge_Backflip_2Leg";
    [SerializeField] private string dodgeBackflip4LegState = "Dodge_Backflip_4Leg";
    [SerializeField] private string dodge2LegState = "Dodge_2Leg";
    [SerializeField] private string dodge4LegState = "Dodge_4Leg";

    [Header("Hit Reaction Timing")]
    [SerializeField] private float lightHitReactDuration = 0.3f;
    [SerializeField] private float heavyHitReactDuration = 0.5f;

    [Header("Knockback Pull")]
    [SerializeField] private float pullDistance = 0.5f;
    [SerializeField] private float pullDuration = 0.15f;

    [Header("Layer Settings")]
    [SerializeField] private int combatLayerIndex = 1;

    [Header("Combo Settings")]
    [SerializeField] private float comboWindowTime = 2.0f;
    [SerializeField] private float attackCooldown = 0.1f;

    [Header("Damage")]
    [SerializeField] private int combo1Damage = 10;
    [SerializeField] private int combo2Damage = 20;
    [SerializeField] private int combo3Damage = 35;
    [SerializeField] private int heavyDamageMin = 50;
    [SerializeField] private int heavyDamageMax = 80;
    [SerializeField] private float heavyChargeTimeMax = 1.5f;
    [SerializeField] private int aerialSpinDamage = 25;

    [Header("Dodge — Distances")]
    [SerializeField] private float backflip2LegDistance = 2.0f;
    [SerializeField] private float frontflip2LegDistance = 3.0f;
    [SerializeField] private float backflip4LegDistance = 2.5f;
    [SerializeField] private float frontflip4LegDistance = 2.5f;

    [Header("Dodge — Timing")]
    [Tooltip("Fallback duration if clip length can't be read. Actual clip length is read automatically.")]
    [SerializeField] private float dodgeFallbackDuration = 0.87f;
    [Tooltip("Normalized anim time when i-frames BEGIN")]
    [SerializeField] private float iFrameStart = 0.08f;
    [Tooltip("Normalized anim time when i-frames END")]
    [SerializeField] private float iFrameEnd = 0.35f;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX")]
    [SerializeField] private ParticleSystem spinVFX;

    [Header("Combat VFX")]
    [SerializeField] private ParticleSystem lightHitVFX;
    [SerializeField] private ParticleSystem heavyHitVFX;
    [SerializeField] private ParticleSystem combo1VFX;
    [SerializeField] private ParticleSystem combo2VFX;
    [SerializeField] private ParticleSystem combo3VFX;
    [SerializeField] private ParticleSystem heavyAttackVFX;

    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 4f;

    [Header("Combat Targeting (Soft Lock-On)")]
    [SerializeField] private float targetingRange = 8f;
    [SerializeField] private float targetingAngle = 90f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
    #endregion

    #region Private Fields
    private CharacterController characterController;
    private Transform cachedTransform;
    private PlayerMovement playerMovement;
    private Camera mainCamera;

    // Combo
    private int currentComboStep;
    private float lastAttackTime;
    private bool isAttacking;
    private bool canQueueNextAttack;
    private int queuedClicks;

    // Aerial
    private bool isAerialAttack;
    private bool hasUsedAerialAttack;

    // Heavy
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

    // Dodge
    private bool isDodging;
    private float dodgeStartTime;
    private float currentDodgeDuration;
    private Quaternion dodgeLockedRotation;
    private Coroutine dodgeCoroutine;

    // Pull
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
        mainCamera = Camera.main;

        if (attackPoint == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(cachedTransform);
            ap.transform.localPosition = new Vector3(0f, 1f, 1f);
            attackPoint = ap.transform;
            Debug.LogWarning("[Combat] WARNING: AttackPoint not assigned in Inspector! Auto-created at (0,1,1). " +
                "Drag a child transform onto the AttackPoint field in PlayerCombat Inspector for accurate hit detection.");
        }

        if (animator != null)
            animator.SetLayerWeight(combatLayerIndex, 1f);

        DebugLog("PlayerCombat initialized");
    }

    private void Update()
    {
        EnforcePositionLock();
        UpdateHitReaction();
        HandleInput();
        CheckGroundedStatus();

        // Process queued clicks after attack ends
        if (!isAttacking && !isInHitReaction && !isDodging
            && queuedClicks > 0 && currentComboStep > 0 && currentComboStep < 3)
        {
            queuedClicks--;
            PerformGroundCombo();
        }

        // Safety: attack timeout
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("Safety: attack timeout");
            ForceResetCombat();
        }

        // Safety: dodge timeout (fallback + generous buffer)
        if (isDodging && Time.time - dodgeStartTime > currentDodgeDuration + 1.0f)
        {
            DebugLog("Safety: dodge timeout");
            EndDodge();
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();

        // Lock rotation during dodge — prevents PlayerMovement from turning Yoru mid-flip
        if (isDodging)
            cachedTransform.rotation = dodgeLockedRotation;
    }

    /// <summary>
    /// Fix 2: isDodging gate prevents position snap from overriding dodge movement.
    /// </summary>
    private void EnforcePositionLock()
    {
        if (!lockPosition || isDodging || characterController == null || !wasGroundedWhenLocked)
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

    #region Input
    private void HandleInput()
    {
        if (isInHitReaction || isDodging) return;

        // Dodge input — checked first so it can cancel combo 1-2
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (TryDodge()) return;
        }

        bool isGrounded = characterController != null && characterController.isGrounded;

        if (Input.GetMouseButtonDown(0))
            attackButtonHoldTime = 0f;

        if (Input.GetMouseButton(0))
        {
            attackButtonHoldTime += Time.deltaTime;

            if (attackButtonHoldTime >= 0.3f && !isChargingHeavy && !isAttacking && isGrounded)
                StartHeavyCharge();
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
                    TryGroundCombo();
                else
                    TryAerialSpin();
            }

            attackButtonHoldTime = 0f;
        }
    }
    #endregion

    #region Dodge System
    /// <summary>
    /// Attempts a dodge. Returns true if dodge was performed.
    /// Direction is camera-relative (BOTW style).
    /// </summary>
    private bool TryDodge()
    {
        if (characterController == null || !characterController.isGrounded)
            return false;

        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return false;
        }

        // Direction from input keys — camera-relative
        bool wHeld = Input.GetKey(KeyCode.W);
        bool sHeld = Input.GetKey(KeyCode.S);
        bool isForward = wHeld && !sHeld;

        // Stance: check both PlayerMovement AND raw Shift key (macOS can drop Shift when Alt pressed)
        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        // Calculate move direction from camera
        Vector3 camForward = GetCameraForwardFlat();
        Vector3 moveDir = isForward ? camForward : -camForward;

        PerformDodge(isForward, is4Leg, moveDir);
        return true;
    }

    /// <summary>
    /// Camera forward flattened to XZ plane.
    /// </summary>
    private Vector3 GetCameraForwardFlat()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 camFwd = mainCamera.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f)
                return camFwd.normalized;
        }

        Vector3 fwd = cachedTransform.forward;
        fwd.y = 0f;
        return fwd.normalized;
    }

    private void PerformDodge(bool isForward, bool is4Leg, Vector3 moveDir)
    {
        // Cancel attack state if dodge-cancelling from combo 1-2
        if (isAttacking)
        {
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            VFX_SpinStop();
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (isChargingHeavy)
        {
            isChargingHeavy = false;
            attackButtonHoldTime = 0f;
        }

        // Fix 2: unlock position FIRST
        UnlockPosition();

        isDodging = true;
        dodgeStartTime = Time.time;
        currentDodgeDuration = dodgeFallbackDuration; // Will be overridden by actual clip length

        // Lock rotation for entire dodge
        if (isForward)
        {
            dodgeLockedRotation = Quaternion.LookRotation(moveDir);
            cachedTransform.rotation = dodgeLockedRotation;
        }
        else
        {
            dodgeLockedRotation = cachedTransform.rotation;
        }

        // Select animation and distance
        string animState;
        float distance;

        if (isForward)
        {
            animState = is4Leg ? dodge4LegState : dodge2LegState;
            distance = is4Leg ? frontflip4LegDistance : frontflip2LegDistance;
        }
        else
        {
            animState = is4Leg ? dodgeBackflip4LegState : dodgeBackflip2LegState;
            distance = is4Leg ? backflip4LegDistance : backflip2LegDistance;
        }

        animator.Play(animState, combatLayerIndex, 0f);

        DebugLog($"Dodge: {animState} ({distance}m {(isForward ? "fwd" : "back")}, {(is4Leg ? "4leg" : "2leg")})");

        if (dodgeCoroutine != null)
            StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, distance));
    }

    /// <summary>
    /// Smoothstep movement via CharacterController.Move().
    /// Waits 1 frame to read actual animation clip length from the animator,
    /// then spreads movement over that exact duration so movement matches animation.
    /// </summary>
    private IEnumerator DodgeMovement(Vector3 direction, float distance)
    {
        // Wait 1 frame for animator to register the new state
        yield return null;

        // Read actual clip length from animator — avoids hardcoding per-dodge durations
        float duration = dodgeFallbackDuration;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (stateInfo.length > 0.1f)
                duration = stateInfo.length;
        }
        currentDodgeDuration = duration;

        DebugLog($"Dodge duration: {duration:F3}s (from clip)");

        float elapsed = 0f;
        float previousEased = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smoothstep: slow start (wind-up) → fast middle (the flip) → slow end (landing)
            float eased = t * t * (3f - 2f * t);

            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);

                if (!characterController.isGrounded)
                    move.y = Physics.gravity.y * Time.deltaTime;

                characterController.Move(move);
            }

            yield return null;
        }

        EndDodge();
    }

    private void EndDodge()
    {
        isDodging = false;
        dodgeCoroutine = null;
        ReturnToIdle();
        DebugLog("Dodge ended");
    }

    /// <summary>
    /// Called by PlayerHealth.TakeDamage() — checks if Yoru is in dodge i-frame window.
    /// </summary>
    public bool IsInDodgeIFrames()
    {
        if (!isDodging || animator == null)
            return false;

        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= iFrameStart && normalizedTime <= iFrameEnd;
    }
    #endregion

    #region Hit Reaction
    public void PlayHitReaction(bool isHeavy)
    {
        PlayHitReaction(isHeavy, Vector3.zero);
    }

    public void PlayHitReaction(bool isHeavy, Vector3 attackerPos)
    {
        bool is4Leg = playerMovement != null && playerMovement.IsRunning();

        isAttacking = false;
        isChargingHeavy = false;
        canQueueNextAttack = false;
        queuedClicks = 0;
        currentComboStep = 0;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        storedHeavyChargePercent = 0f;
        UnlockPosition();
        VFX_SpinStop();

        if (isDodging)
        {
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
        }

        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (attackerPos != Vector3.zero && characterController != null)
        {
            Vector3 pullDir = attackerPos - cachedTransform.position;
            pullDir.y = 0f;

            if (pullDir.sqrMagnitude > 0.01f)
            {
                cachedTransform.rotation = Quaternion.LookRotation(pullDir.normalized);

                if (pullCoroutine != null)
                    StopCoroutine(pullCoroutine);
                pullCoroutine = StartCoroutine(SmoothPull(pullDir.normalized, pullDistance, pullDuration));
            }
        }

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

        if (animator != null)
        {
            // Play on combat layer ONLY — base layer doesn't have these states
            // (removed base layer play that caused "Animator.GotoState: State could not be found")
            animator.Play(animState, combatLayerIndex, 0f);
            DebugLog($"Hit react: {animState} ({duration}s)");
        }

        isInHitReaction = true;
        hitReactionEndTime = Time.time + duration;
    }

    private IEnumerator SmoothPull(Vector3 direction, float distance, float duration)
    {
        float elapsed = 0f;
        float moved = 0f;

        while (elapsed < duration && moved < distance)
        {
            elapsed += Time.deltaTime;
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

    private void UpdateHitReaction()
    {
        if (isInHitReaction && Time.time >= hitReactionEndTime)
        {
            isInHitReaction = false;
            ReturnToIdle();
        }
    }
    #endregion

    #region Combat Targeting
    private void FaceNearestEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(cachedTransform.position, targetingRange, enemyLayer);
        if (nearby.Length == 0) return;

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider col in nearby)
        {
            EnemyHealth eh = col.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead()) continue;

            Vector3 dirToEnemy = col.transform.position - cachedTransform.position;
            dirToEnemy.y = 0f;
            float dist = dirToEnemy.magnitude;
            if (dist < 0.1f) continue;

            float angle = Vector3.Angle(cachedTransform.forward, dirToEnemy);
            if (angle > targetingAngle) continue;

            float score = dist + angle * 0.02f;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = col.transform;
            }
        }

        if (bestTarget != null)
        {
            Vector3 lookDir = bestTarget.position - cachedTransform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
                cachedTransform.rotation = Quaternion.LookRotation(lookDir);
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
            if (currentComboStep < 3 && queuedClicks < 2)
            {
                queuedClicks++;
                DebugLog($"Queued click #{queuedClicks} (combo {currentComboStep})");
            }
            return;
        }

        PerformGroundCombo();
    }

    private void PerformGroundCombo()
    {
        attackStartTime = Time.time;
        FaceNearestEnemy();

        if (currentComboStep > 0 && Time.time - lastAttackTime > comboWindowTime)
        {
            currentComboStep = 0;
            DebugLog("Combo window expired");
        }

        currentComboStep++;
        if (currentComboStep > 3)
            currentComboStep = 1;

        DebugLog($"Combo {currentComboStep} — {GetComboDamage(currentComboStep)} dmg");

        if (currentComboStep == 3)
            UnlockPosition();
        else
            LockPositionNow();

        PlayCombatAnimation(GetComboStateName(currentComboStep));

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
        if (hasUsedAerialAttack || isAttacking)
            return;
        PerformAerialSpin();
    }

    private void PerformAerialSpin()
    {
        attackStartTime = Time.time;
        FaceNearestEnemy();

        hasUsedAerialAttack = true;
        isAerialAttack = true;
        currentComboStep = 3;

        DebugLog($"Aerial spin — {aerialSpinDamage} dmg");

        UnlockPosition();
        PlayCombatAnimation(combo3StateName);

        animator.SetInteger(HashComboStep, 3);
        animator.SetBool(HashIsAttacking, true);

        isAttacking = true;
        canQueueNextAttack = false;
        queuedClicks = 0;
        lastAttackTime = Time.time;
    }
    #endregion

    #region Heavy Attack
    private void StartHeavyCharge()
    {
        isChargingHeavy = true;
        heavyChargeStartTime = Time.time;
        currentComboStep = 0;
        DebugLog("Charging heavy...");
    }

    private void ReleaseHeavyAttack()
    {
        attackStartTime = Time.time;
        FaceNearestEnemy();

        storedHeavyChargePercent = Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
        int damage = Mathf.RoundToInt(Mathf.Lerp(heavyDamageMin, heavyDamageMax, storedHeavyChargePercent));

        DebugLog($"Heavy — {storedHeavyChargePercent * 100f:F0}% = {damage} dmg");

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
    }

    private void ReturnToIdle()
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(combatIdleStateName, 0.1f, combatLayerIndex);
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
    }

    private void UnlockPosition()
    {
        if (!lockPosition) return;
        lockPosition = false;
        wasGroundedWhenLocked = false;
    }
    #endregion

    #region Hit Detection
    public void DealDamage()
    {
        int damage = isAerialAttack ? aerialSpinDamage : GetComboDamage(currentComboStep);
        bool isFinisher = !isAerialAttack && currentComboStep == 3;
        DealDamageInRange(damage, isFinisher);
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
                DebugLog($"Hit {enemy.name} for {damage}{(isHeavy ? " (heavy)" : "")}");
            }
        }
    }
    #endregion

    #region VFX — Animation Events
    public void VFX_SpinStart()
    {
        if (spinVFX != null) spinVFX.Play();
    }

    public void VFX_SpinStop()
    {
        if (spinVFX != null) spinVFX.Stop();
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

        if (queuedClicks > 0)
        {
            queuedClicks--;
            PerformGroundCombo();
        }
    }

    public void OnAttackEnd()
    {
        if (!isAttacking) return;

        isAttacking = false;
        canQueueNextAttack = false;
        lastAttackTime = Time.time;

        if (currentComboStep >= 3 || isAerialAttack || currentComboStep == 0)
            queuedClicks = 0;

        if (isAerialAttack)
            isAerialAttack = false;

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
        queuedClicks = 0;
        currentComboStep = 0;
        attackStartTime = 0f;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        hasUsedAerialAttack = false;
        storedHeavyChargePercent = 0f;
        isInHitReaction = false;

        if (isDodging)
        {
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
        }

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

        DebugLog("Combat reset");
    }
    #endregion

    #region Public Getters
    public bool IsAttacking() => isAttacking;
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    public bool IsAerialAttack() => isAerialAttack;
    public bool IsPositionLocked() => lockPosition;
    public bool IsInHitReaction() => isInHitReaction;
    public bool IsDodging() => isDodging;
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