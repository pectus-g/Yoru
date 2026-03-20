using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Phase 3A v6
/// Changes from v5:
///   - Dodge→walk transition fix: early exit when movement input detected after 75% of dodge
///   - Dodge Dash: Alt + Forward + LMB = forward dodge that deals 20 damage along path
///     DodgeDash_2Leg (3.0m) and DodgeDash_4Leg (3.5m), i-frames active during dash
///   - VFX fields for dodge (trail) and dodge dash (damage trail)
///   - SFX hooks via CombatSFXManager for all combat actions
///   - CombatFeedbackManager hooks: hitstop + camera shake + VFX at contact point
///   - Hit detection debug logs removed (attacks confirmed working)
///   - GetAnimator() public getter for CombatFeedbackManager hitstop
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
    [SerializeField] private string dodge2LegState = "Dodge_2Leg";
    [SerializeField] private string dodge4LegState = "Dodge_4Leg";

    [Header("Animation State Names — Dodge Dash")]
    [SerializeField] private string dodgeDash2LegState = "DodgeDash_2Leg";
    [SerializeField] private string dodgeDash4LegState = "DodgeDash_4Leg";

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
    [SerializeField] private float dodge2LegDistance = 3.0f;
    [SerializeField] private float dodge4LegDistance = 2.5f;

    [Header("Dodge Dash — Distances & Damage")]
    [SerializeField] private float dodgeDash2LegDistance = 3.0f;
    [SerializeField] private float dodgeDash4LegDistance = 3.5f;
    [SerializeField] private int dodgeDashDamage = 20;
    [Tooltip("Hit range along the dodge dash path")]
    [SerializeField] private float dodgeDashHitRange = 1.8f;

    [Header("Dodge — Timing")]
    [Tooltip("Fallback duration if clip length can't be read.")]
    [SerializeField] private float dodgeFallbackDuration = 0.87f;
    [Tooltip("Normalized anim time when i-frames BEGIN")]
    [SerializeField] private float iFrameStart = 0.08f;
    [Tooltip("Normalized anim time when i-frames END")]
    [SerializeField] private float iFrameEnd = 0.35f;
    [Tooltip("Normalized time after which movement input can end dodge early (0.75 = 75%)")]
    [SerializeField] private float dodgeEarlyExitThreshold = 0.75f;

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

    [Header("Dodge/Dash VFX — Assign in Inspector")]
    [Tooltip("Trail effect spawned at feet on normal dodge")]
    [SerializeField] private GameObject dodgeTrailVFXPrefab;
    [Tooltip("Damage trail effect spawned during dodge dash")]
    [SerializeField] private GameObject dodgeDashTrailVFXPrefab;
    [SerializeField] private float dodgeVFXLifetime = 1.5f;

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
    private bool isDodgeDashing; // true = dodge dash (deals damage), false = normal dodge
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
            Debug.LogWarning("[Combat] WARNING: AttackPoint not assigned in Inspector! Auto-created at (0,1,1).");
        }

        // Combat layer weight stays at 1 always
        if (animator != null)
            animator.SetLayerWeight(combatLayerIndex, 1f);

        DebugLog("PlayerCombat initialized — Phase 3A v6");
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

        // Safety: dodge timeout
        if (isDodging && Time.time - dodgeStartTime > currentDodgeDuration + 1.0f)
        {
            DebugLog("Safety: dodge timeout");
            EndDodge();
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();

        // Lock rotation during dodge
        if (isDodging)
            cachedTransform.rotation = dodgeLockedRotation;
    }

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
    /// Dodge direction = WASD input direction (camera-relative).
    /// If LMB is also held AND direction is forward → Dodge Dash (deals damage).
    /// Otherwise → normal frontflip dodge.
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

        // Read WASD input
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Build camera-relative direction from input
        Vector3 dodgeDir = GetInputDirectionCameraRelative(h, v);

        // Stance
        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        // Check for Dodge Dash: Alt + Forward direction + LMB held
        bool isForward = v > 0.1f;
        bool lmbHeld = Input.GetMouseButton(0);

        if (isForward && lmbHeld)
        {
            PerformDodgeDash(is4Leg, dodgeDir);
        }
        else
        {
            PerformDodge(is4Leg, dodgeDir);
        }

        return true;
    }

    /// <summary>
    /// Converts WASD input into a camera-relative world direction.
    /// If no input, returns camera forward.
    /// </summary>
    private Vector3 GetInputDirectionCameraRelative(float h, float v)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        if (mainCamera != null)
        {
            camForward = mainCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            camRight = mainCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }

        // No input = dodge forward
        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
            return camForward;

        Vector3 dir = camForward * v + camRight * h;
        return dir.normalized;
    }

    /// <summary>
    /// Normal dodge — frontflip, no damage, WASD direction.
    /// </summary>
    private void PerformDodge(bool is4Leg, Vector3 moveDir)
    {
        // Cancel attack if dodge-cancelling from combo 1-2
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

        UnlockPosition();

        isDodging = true;
        isDodgeDashing = false;
        dodgeStartTime = Time.time;
        currentDodgeDuration = dodgeFallbackDuration;

        // Face the dodge direction, then frontflip that way
        dodgeLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dodgeLockedRotation;

        string animState = is4Leg ? dodge4LegState : dodge2LegState;
        float distance = is4Leg ? dodge4LegDistance : dodge2LegDistance;

        animator.Play(animState, combatLayerIndex, 0f);

        DebugLog($"Dodge: {animState} ({distance}m, {(is4Leg ? "4leg" : "2leg")})");

        // Dodge VFX
        SpawnDodgeVFX(dodgeTrailVFXPrefab);

        // Dodge SFX
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayDodge();

        if (dodgeCoroutine != null)
            StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, distance, false));
    }

    /// <summary>
    /// Dodge Dash — forward dodge that deals 20 damage along the path.
    /// Alt + Forward + LMB. Uses DodgeDash animation clips.
    /// </summary>
    private void PerformDodgeDash(bool is4Leg, Vector3 moveDir)
    {
        // Cancel attack if dodge-cancelling from combo 1-2
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

        UnlockPosition();

        isDodging = true;
        isDodgeDashing = true;
        dodgeStartTime = Time.time;
        currentDodgeDuration = dodgeFallbackDuration;

        dodgeLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dodgeLockedRotation;

        string animState = is4Leg ? dodgeDash4LegState : dodgeDash2LegState;
        float distance = is4Leg ? dodgeDash4LegDistance : dodgeDash2LegDistance;

        animator.Play(animState, combatLayerIndex, 0f);

        DebugLog($"Dodge Dash: {animState} ({distance}m, {dodgeDashDamage} dmg, {(is4Leg ? "4leg" : "2leg")})");

        // Dodge Dash VFX — damage trail
        SpawnDodgeVFX(dodgeDashTrailVFXPrefab);

        // Dodge SFX
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayDodge();

        if (dodgeCoroutine != null)
            StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, distance, true));
    }

    /// <summary>
    /// Smoothstep dodge movement with early exit and optional damage (dodge dash).
    /// After dodgeEarlyExitThreshold (75%), if WASD input detected, exits early.
    /// If isDash=true, deals dodgeDashDamage to enemies along the path (once per enemy).
    /// </summary>
    private IEnumerator DodgeMovement(Vector3 direction, float distance, bool isDash)
    {
        yield return null;

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

        // Track enemies already hit during dodge dash (no double-hits)
        System.Collections.Generic.HashSet<int> hitEnemyIDs = null;
        if (isDash)
            hitEnemyIDs = new System.Collections.Generic.HashSet<int>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t); // Smoothstep
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);
                if (!characterController.isGrounded)
                    move.y = Physics.gravity.y * Time.deltaTime;
                characterController.Move(move);
            }

            // Dodge Dash: deal damage to enemies along path (once per enemy)
            if (isDash && t >= iFrameStart && t <= iFrameEnd)
            {
                DealDodgeDashDamage(hitEnemyIDs);
            }

            // Early exit: movement input in tail end of dodge
            if (t >= dodgeEarlyExitThreshold)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
                {
                    DebugLog($"Dodge early exit at {t * 100f:F0}% (movement input)");
                    break;
                }
            }

            yield return null;
        }

        EndDodge();
    }

    /// <summary>
    /// Deals dodgeDashDamage to enemies within dodgeDashHitRange of attackPoint.
    /// Each enemy can only be hit once per dash (tracked by instance ID).
    /// </summary>
    private void DealDodgeDashDamage(System.Collections.Generic.HashSet<int> hitEnemyIDs)
    {
        Collider[] enemies = Physics.OverlapSphere(attackPoint.position, dodgeDashHitRange, enemyLayer);

        foreach (Collider enemy in enemies)
        {
            int id = enemy.gameObject.GetInstanceID();
            if (hitEnemyIDs.Contains(id)) continue;

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                hitEnemyIDs.Add(id);
                enemyHealth.TakeDamage(dodgeDashDamage, false);
                DebugLog($"Dodge Dash hit {enemy.name} for {dodgeDashDamage}");

                // Feedback: contact point VFX + hitstop + SFX
                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);

                if (CombatFeedbackManager.Instance != null)
                {
                    Animator enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator == null)
                        enemyAnimator = enemy.GetComponentInChildren<Animator>();

                    CombatFeedbackManager.Instance.PlayHitFeedback(
                        contactPoint, false, animator, enemyAnimator);
                }

                if (CombatSFXManager.Instance != null)
                    CombatSFXManager.Instance.PlayImpact(false);
            }
        }
    }

    private void EndDodge()
    {
        isDodging = false;
        isDodgeDashing = false;
        dodgeCoroutine = null;

        // Fast crossfade for snappy transition to locomotion
        if (animator != null)
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.05f, combatLayerIndex);

        DebugLog("Dodge ended");
    }

    public bool IsInDodgeIFrames()
    {
        if (!isDodging || animator == null)
            return false;

        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= iFrameStart && normalizedTime <= iFrameEnd;
    }

    /// <summary>
    /// Spawn dodge VFX prefab at Yoru's feet. Null-safe.
    /// </summary>
    private void SpawnDodgeVFX(GameObject prefab)
    {
        if (prefab == null) return;

        Vector3 pos = cachedTransform.position;
        pos.y += 0.1f;
        GameObject vfx = Instantiate(prefab, pos, cachedTransform.rotation);
        Destroy(vfx, dodgeVFXLifetime);
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
            isDodgeDashing = false;
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
            animator.Play(animState, combatLayerIndex, 0f);
            DebugLog($"Hit react: {animState} ({duration}s)");
        }

        // Player-hit feedback
        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.PlayPlayerHitFeedback(isHeavy);
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayPlayerHit(isHeavy);

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

        // Only commitment moves lock position
        if (currentComboStep == 3)
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

                // Feedback: contact point VFX + hitstop + camera shake
                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);

                if (CombatFeedbackManager.Instance != null)
                {
                    Animator enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator == null)
                        enemyAnimator = enemy.GetComponentInChildren<Animator>();

                    CombatFeedbackManager.Instance.PlayHitFeedback(
                        contactPoint, isHeavy, animator, enemyAnimator);
                }

                // Impact SFX
                if (CombatSFXManager.Instance != null)
                {
                    bool isCombo3 = !isAerialAttack && currentComboStep == 3;
                    CombatSFXManager.Instance.PlayImpact(isHeavy, isCombo3);
                }
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

    /// <summary>
    /// Animation Event: play swing whoosh SFX.
    /// Add on combo clips at the start of the swing arc.
    /// </summary>
    public void SFX_Swing()
    {
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlaySwing(currentComboStep);
    }

    /// <summary>
    /// Animation Event: play heavy swing whoosh SFX.
    /// Add on the HeavyAttack clip.
    /// </summary>
    public void SFX_SwingHeavy()
    {
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlaySwing(0);
    }
    #endregion

    #region Animation Events — Combat Flow
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
            isDodgeDashing = false;
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
    public bool IsDodgeDashing() => isDodgeDashing;
    public Animator GetAnimator() => animator;
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