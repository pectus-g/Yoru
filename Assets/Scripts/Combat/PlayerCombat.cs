using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Phase 3A v8
/// Changes from v7:
///   - Dodge (Alt) = evasive frontflip with arc. No damage. One per jump in air (time window).
///   - Dash (RMB) = aggressive flat rush with damage. Separate move. One per jump in air (time window).
///   - Old LMB-during-dodge conversion REMOVED.
///   - Flip stutter fix: animator.Play() at start, CrossFade at end.
///   - Infinite air flip bug fixed.
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

    [Header("Animation State Names — Dodge (frontflip)")]
    [SerializeField] private string dodge2LegState = "Dodge_2Leg";
    [SerializeField] private string dodge4LegState = "Dodge_4Leg";

    [Header("Animation State Names — Dash (rush)")]
    [SerializeField] private string dash2LegState = "DodgeDash_2Leg";
    [SerializeField] private string dash4LegState = "DodgeDash_4Leg";

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

    [Header("Dodge — Distances (frontflip)")]
    [Tooltip("Forward distance for 2-leg frontflip")]
    [SerializeField] private float dodge2LegDistance = 3.0f;
    [Tooltip("Forward distance for 4-leg frontflip")]
    [SerializeField] private float dodge4LegDistance = 2.5f;

    [Header("Dodge — Arc")]
    [Tooltip("Height of the frontflip arc. 0 = flat, 1.5 = noticeable hop, 3 = big leap")]
    [SerializeField] private float dodgeHeight = 1.5f;

    [Header("Dodge — Timing")]
    [SerializeField] private float dodgeFallbackDuration = 0.87f;
    [SerializeField] private float iFrameStart = 0.08f;
    [SerializeField] private float iFrameEnd = 0.35f;
    [SerializeField] private float dodgeEarlyExitThreshold = 0.75f;

    [Header("Dash — Distances (RMB rush)")]
    [Tooltip("Forward distance for 2-leg dash")]
    [SerializeField] private float dash2LegDistance = 4.0f;
    [Tooltip("Forward distance for 4-leg dash")]
    [SerializeField] private float dash4LegDistance = 5.0f;

    [Header("Dash — Damage")]
    [SerializeField] private int dashDamage = 20;
    [SerializeField] private float dashHitRange = 1.8f;

    [Header("Dash — Timing")]
    [SerializeField] private float dashFallbackDuration = 0.5f;
    [Tooltip("I-frame start for dash (normalized 0-1)")]
    [SerializeField] private float dashIFrameStart = 0.05f;
    [Tooltip("I-frame end for dash (normalized 0-1)")]
    [SerializeField] private float dashIFrameEnd = 0.40f;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

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
    private YoruVFXManager vfxManager;
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

    // Dodge (frontflip — Alt)
    private bool isDodging;
    private float dodgeStartTime;
    private float currentDodgeDuration;
    private Quaternion dodgeLockedRotation;
    private Coroutine dodgeCoroutine;
    private bool hasUsedAirDodge; // one flip per jump

    // Dash (rush — RMB)
    private bool isDashing;
    private float dashStartTime;
    private float currentDashDuration;
    private Quaternion dashLockedRotation;
    private Coroutine dashCoroutine;
    private bool hasUsedAirDash; // one dash per jump

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
        vfxManager = GetComponent<YoruVFXManager>();
        mainCamera = Camera.main;

        if (attackPoint == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(cachedTransform);
            ap.transform.localPosition = new Vector3(0f, 1f, 1f);
            attackPoint = ap.transform;
            Debug.LogWarning("[Combat] WARNING: AttackPoint not assigned in Inspector! Auto-created at (0,1,1).");
        }

        if (animator != null)
            animator.SetLayerWeight(combatLayerIndex, 1f);

        DebugLog("PlayerCombat initialized — Phase 3A v8");
    }

    private void Update()
    {
        EnforcePositionLock();
        UpdateHitReaction();
        HandleInput();
        CheckGroundedStatus();

        if (!isAttacking && !isInHitReaction && !isDodging && !isDashing
            && queuedClicks > 0 && currentComboStep > 0 && currentComboStep < 3)
        {
            queuedClicks--;
            PerformGroundCombo();
        }

        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("Safety: attack timeout");
            ForceResetCombat();
        }

        if (isDodging && Time.time - dodgeStartTime > currentDodgeDuration + 1.0f)
        {
            DebugLog("Safety: dodge timeout");
            EndDodge();
        }

        if (isDashing && Time.time - dashStartTime > currentDashDuration + 1.0f)
        {
            DebugLog("Safety: dash timeout");
            EndDash();
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();
        if (isDodging)
            cachedTransform.rotation = dodgeLockedRotation;
        if (isDashing)
            cachedTransform.rotation = dashLockedRotation;
    }

    private void EnforcePositionLock()
    {
        if (!lockPosition || isDodging || isDashing || characterController == null || !wasGroundedWhenLocked)
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
            // Reset air dodge/dash on landing
            hasUsedAirDodge = false;
            hasUsedAirDash = false;
        }
    }
    #endregion

    #region Input
    private void HandleInput()
    {
        if (isInHitReaction) return;
        if (isDodging || isDashing) return;

        // Dodge input (Alt) — evasive frontflip, can cancel combo 1-2
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (TryDodge()) return;
        }

        // Dash input (RMB) — aggressive rush with damage
        if (Input.GetMouseButtonDown(1))
        {
            if (TryDash()) return;
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

    #region Dodge System (Alt — evasive frontflip with arc)
    private bool TryDodge()
    {
        if (characterController == null) return false;

        bool isGrounded = characterController.isGrounded;

        // Air dodge: needs jump time window + not used yet this jump
        if (!isGrounded)
        {
            if (hasUsedAirDodge) return false;
            if (playerMovement == null || playerMovement.GetJumpWindowTimer() <= 0f) return false;
        }

        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return false;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dodgeDir = GetInputDirectionCameraRelative(h, v);

        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        if (!isGrounded) hasUsedAirDodge = true;

        PerformDodge(is4Leg, dodgeDir);
        return true;
    }

    private void PerformDodge(bool is4Leg, Vector3 moveDir)
    {
        // Cancel attack if dodge-cancelling from combo 1-2
        if (isAttacking)
        {
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            if (vfxManager != null) vfxManager.PlaySpinStop();
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
        dodgeStartTime = Time.time;
        currentDodgeDuration = dodgeFallbackDuration;

        dodgeLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dodgeLockedRotation;

        string animState = is4Leg ? dodge4LegState : dodge2LegState;
        float distance = is4Leg ? dodge4LegDistance : dodge2LegDistance;

        // Fix: instant Play instead of CrossFade to avoid stutter at start
        animator.Play(animState, combatLayerIndex, 0f);

        DebugLog($"Dodge: {animState} ({distance}m, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dodgeCoroutine != null) StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, distance));
    }

    private IEnumerator DodgeMovement(Vector3 direction, float distance)
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

        float elapsed = 0f;
        float previousEased = 0f;
        float previousArc = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);

                // Vertical arc for frontflip
                if (dodgeHeight > 0f)
                {
                    float arc = Mathf.Sin(t * Mathf.PI) * dodgeHeight;
                    float arcDelta = arc - previousArc;
                    previousArc = arc;
                    move.y += arcDelta;
                }
                else if (!characterController.isGrounded)
                {
                    move.y = Physics.gravity.y * Time.deltaTime;
                }

                characterController.Move(move);
            }

            // Early exit on movement input (last 25%)
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

    private void EndDodge()
    {
        isDodging = false;
        dodgeCoroutine = null;
        if (animator != null)
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.15f, combatLayerIndex);
        DebugLog("Dodge ended");
    }

    public bool IsInDodgeIFrames()
    {
        if (!isDodging || animator == null) return false;
        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= iFrameStart && normalizedTime <= iFrameEnd;
    }
    #endregion

    #region Dash System (RMB — aggressive flat rush with damage)
    private bool TryDash()
    {
        if (characterController == null) return false;
        if (isDodging || isDashing) return false;

        bool isGrounded = characterController.isGrounded;

        // Air dash: needs jump time window + not used yet this jump
        if (!isGrounded)
        {
            if (hasUsedAirDash) return false;
            if (playerMovement == null || playerMovement.GetJumpWindowTimer() <= 0f) return false;
        }

        // Can cancel combo 1-2 into dash
        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return false;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dashDir = GetInputDirectionCameraRelative(h, v);

        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        if (!isGrounded) hasUsedAirDash = true;

        PerformDash(is4Leg, dashDir);
        return true;
    }

    private void PerformDash(bool is4Leg, Vector3 moveDir)
    {
        // Cancel attack if dash-cancelling from combo 1-2
        if (isAttacking)
        {
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            if (vfxManager != null) vfxManager.PlaySpinStop();
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (isChargingHeavy)
        {
            isChargingHeavy = false;
            attackButtonHoldTime = 0f;
        }

        UnlockPosition();

        isDashing = true;
        dashStartTime = Time.time;
        currentDashDuration = dashFallbackDuration;

        dashLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dashLockedRotation;

        string animState = is4Leg ? dash4LegState : dash2LegState;
        float distance = is4Leg ? dash4LegDistance : dash2LegDistance;

        // Instant play for snappy start
        animator.Play(animState, combatLayerIndex, 0f);

        DebugLog($"Dash: {animState} ({distance}m, {dashDamage} dmg, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeDashTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = StartCoroutine(DashMovement(moveDir, distance));
    }

    private IEnumerator DashMovement(Vector3 direction, float distance)
    {
        yield return null;

        float duration = dashFallbackDuration;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (stateInfo.length > 0.1f)
                duration = stateInfo.length;
        }
        currentDashDuration = duration;

        float elapsed = 0f;
        float previousEased = 0f;

        // Track enemies already hit
        var hitEnemyIDs = new System.Collections.Generic.HashSet<int>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                // Flat movement — NO vertical arc, only gravity if airborne
                Vector3 move = direction * (distance * frameDelta);
                if (!characterController.isGrounded)
                    move.y = Physics.gravity.y * Time.deltaTime;

                characterController.Move(move);
            }

            // Deal damage along path
            DealDashDamage(hitEnemyIDs);

            yield return null;
        }

        EndDash();
    }

    private void DealDashDamage(System.Collections.Generic.HashSet<int> hitEnemyIDs)
    {
        Collider[] enemies = Physics.OverlapSphere(attackPoint.position, dashHitRange, enemyLayer);

        foreach (Collider enemy in enemies)
        {
            int id = enemy.gameObject.GetInstanceID();
            if (hitEnemyIDs.Contains(id)) continue;

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                hitEnemyIDs.Add(id);
                enemyHealth.TakeDamage(dashDamage, false);
                DebugLog($"Dash hit {enemy.name} for {dashDamage}");

                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);
                if (CombatFeedbackManager.Instance != null)
                {
                    Animator enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator == null)
                        enemyAnimator = enemy.GetComponentInChildren<Animator>();
                    CombatFeedbackManager.Instance.PlayHitFeedback(contactPoint, false, animator, enemyAnimator);
                }
                if (CombatSFXManager.Instance != null)
                    CombatSFXManager.Instance.PlayImpact(false);
            }
        }
    }

    private void EndDash()
    {
        isDashing = false;
        dashCoroutine = null;
        if (animator != null)
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.15f, combatLayerIndex);
        DebugLog("Dash ended");
    }

    public bool IsInDashIFrames()
    {
        if (!isDashing || animator == null) return false;
        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= dashIFrameStart && normalizedTime <= dashIFrameEnd;
    }
    #endregion

    #region Shared — Camera Direction
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

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
            return camForward;

        Vector3 dir = camForward * v + camRight * h;
        return dir.normalized;
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
        if (vfxManager != null) vfxManager.PlaySpinStop();

        if (isDodging)
        {
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
        }

        if (isDashing)
        {
            isDashing = false;
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
                dashCoroutine = null;
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
                if (pullCoroutine != null) StopCoroutine(pullCoroutine);
                pullCoroutine = StartCoroutine(SmoothPull(pullDir.normalized, pullDistance, pullDuration));
            }
        }

        string animState;
        float duration;

        if (isHeavy)
        {
            animState = is4Leg ? hitReactHeavy4Leg : hitReactHeavy2Leg;
            duration = heavyHitReactDuration;
        }
        else
        {
            animState = is4Leg ? hitReactLight4Leg : hitReactLight2Leg;
            duration = lightHitReactDuration;
        }

        if (vfxManager != null) vfxManager.PlayHitReactVFX(isHeavy);

        if (animator != null)
        {
            animator.Play(animState, combatLayerIndex, 0f);
            DebugLog($"Hit react: {animState} ({duration}s)");
        }

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
        if (Time.time - lastAttackTime < attackCooldown) return;

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
        if (currentComboStep > 3) currentComboStep = 1;

        DebugLog($"Combo {currentComboStep} — {GetComboDamage(currentComboStep)} dmg");

        if (currentComboStep == 3) LockPositionNow();

        PlayCombatAnimation(GetComboStateName(currentComboStep));

        if (vfxManager != null) vfxManager.PlayComboVFX(currentComboStep);

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
        if (hasUsedAerialAttack || isAttacking) return;
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
        if (vfxManager != null) vfxManager.PlayHeavyAttackVFX();
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

                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);
                if (CombatFeedbackManager.Instance != null)
                {
                    Animator enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator == null)
                        enemyAnimator = enemy.GetComponentInChildren<Animator>();
                    CombatFeedbackManager.Instance.PlayHitFeedback(contactPoint, isHeavy, animator, enemyAnimator);
                }
                if (CombatSFXManager.Instance != null)
                {
                    bool isCombo3 = !isAerialAttack && currentComboStep == 3;
                    CombatSFXManager.Instance.PlayImpact(isHeavy, isCombo3);
                }
            }
        }
    }
    #endregion

    #region VFX/SFX Animation Events
    public void VFX_SpinStart()
    {
        if (vfxManager != null) vfxManager.PlaySpinStart();
    }

    public void VFX_SpinStop()
    {
        if (vfxManager != null) vfxManager.PlaySpinStop();
    }

    public void SFX_Swing()
    {
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlaySwing(currentComboStep);
    }

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
        if (isAerialAttack) isAerialAttack = false;
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
        if (isDashing)
        {
            isDashing = false;
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
                dashCoroutine = null;
            }
        }
        if (pullCoroutine != null)
        {
            StopCoroutine(pullCoroutine);
            pullCoroutine = null;
        }

        UnlockPosition();
        if (vfxManager != null) vfxManager.PlaySpinStop();
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
    public bool IsDashing() => isDashing;
    public Animator GetAnimator() => animator;
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Combat] {message}");
    }
private void OnDisable()
{
    Debug.LogError("[Combat] PlayerYoru_Def was DEACTIVATED!", gameObject);
}
    private void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo || attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
    #endregion
}