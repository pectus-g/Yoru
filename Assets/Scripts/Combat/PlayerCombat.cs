using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Phase 3C v26
/// v20: EndDash/EndDodge StopCoroutine fix (insufficient — coroutines silently crash)
/// v21: ACTUAL FIX — time-based flag clearing that doesn't depend on coroutines:
///   - Dodge/dash timeouts tightened: +0.3s grace (was +1.0s) plus 2s absolute hard cap
///   - Movement-stuck safety: 0.5s WASD held + any flag stuck → force reset (was 1.5s + required no combat keys)
///   - These catch ALL orphan scenarios regardless of how the coroutine dies
/// v22: Guard movement-stuck false positive fix.
///   - Movement-stuck safety previously included isGuarding in the blocking-flag set.
///   - Holding Q + WASD to guard-walk (legitimate 0.75x speed via GuardMovementController)
///     after 1s caused the safety to ForceResetCombat → EndGuard, killing guard even
///     though Q was still held.
///   - Guard is excluded from the blocking-flag set. Three other safeties remain in place:
///     Q-released stuck check (lines ~342), animator-orphan detection (lines ~365),
///     and PlayerMovement's guard-routing in FixedUpdate. No protection is lost.
///   - Also caught up the init-log version string (was stuck at "v17").
/// v23: Parry animation restructure — separate Parry_Start clip from Parry idle loop.
///   - New parryStartState SerializeField (default "Parry_Start") — plays once when Q is pressed.
///   - parryIdleState is now a clean loop (no baked intro).
///   - parryIntroLength field repurposed: was "intro length within combined clip", now means
///     "duration of Parry_Start clip in seconds". Same field — Inspector value preserved.
///   - parryIntroComplete now genuinely flips false→true when start clip finishes
///     (previously only ever set to false in StartGuard, never used).
///   - UpdateGuardAnimation gains a Phase 1 / Phase 2 split:
///     Phase 1 = wait for Parry_Start to complete; Phase 2 = normal idle/walk selection.
///   - REMOVED: ~35-line "Parry idle loop skip" block (no longer needed with clean idle loop).
///   - REMOVED: special introAlreadyPlayed CrossFade with normalizedTimeOffset arg
///     (plain CrossFadeInFixedTime is correct for the new design).
///   - New blend-time case for parryStartState → idle/walk: 0.1s short blend
///     (start clip ends in parry pose, idle/walk begin in parry pose, so the blend is tight).
///   - Inspector action required: verify parryStartState matches your Animator state name,
///     and update parryIntroLength to match the actual duration of your Parry_Start clip.
/// v24: Q-release grace window — fixes parry breaking when pressing A/D while Q is held.
///   - Root cause: keyboard ghosting / N-key rollover. On many keyboards, pressing Q + A
///     or Q + D simultaneously causes the hardware to drop Q for 1+ frames. Unity sees
///     this as Input.GetKeyUp(Q) firing and ends guard, even though Q is still held.
///   - Fix: replace Input.GetKeyUp(Q) with polling-based detection plus an 80ms grace.
///     If Q reports as released but comes back within 80ms, treat as continuous hold.
///     If Q stays released past 80ms, end guard normally.
///   - Two new fields: qReleaseGraceTime (Inspector tunable, default 0.08s) and
///     qReleaseStartTime (runtime, tracks when Q first appeared released).
///   - 80ms is below human key-tap perception (~100ms) so guard doesn't feel sticky.
///     Above any keyboard ghost blip (typically 17-50ms at 60fps).
///   - The 0.5s guardStuckTimer safety net is unchanged and still catches true stuck cases.
/// v25: Parry guard offset smooth ramp on BOTH directions — fixes visible level snap on
///      idle->walk and the abrupt body drop on guard exit.
///   - Root cause: v22 used instant snap-up plus a 10-units-per-second descent. With any
///     non-trivial guardModelYOffset value, the body Y change happens in a single frame
///     while the animator CrossFade blends the pose over 0.1-0.25s. Body and animation
///     were moving on different timelines: at walk start the body would pop up before the
///     pose finished blending in (visible level change), and at guard exit the body would
///     drop out under the model in functionally one frame (abrupt pop). Trying to
///     compensate by lowering the offset value also lowered the lift below what the walk
///     clip actually needed, leaving paw tips clipping the ground.
///   - Fix: ramp the offset smoothly on both directions at a duration that approximately
///     matches the parry CrossFade blend times. Body Y and animation pose now move on the
///     same timeline. Once the ramp is smooth, guardModelYOffset can be tuned up to match
///     the walk clip's actual baked paw depth without the level change being visible.
///   - New Inspector field: guardOffsetRampDuration (default 0.2s). Speed is computed
///     dynamically as guardModelYOffset / guardOffsetRampDuration per second so the
///     visible ramp time stays constant regardless of the offset value chosen.
///   - Tuning order after applying:
///       1. Leave guardOffsetRampDuration at 0.2s for first test.
///       2. Bump guardModelYOffset up until paw tips visibly clear the ground during walk.
///          Likely target range 0.10-0.15 based on screenshot evidence at offset=0.05.
///       3. If walk-start still feels like a pop, raise guardOffsetRampDuration to 0.25s.
///          If guard-exit feels floaty, lower it to 0.15s.
///       4. Watch the idle->walk and guard-end transitions: body and paws should look like
///          one continuous motion, not a body pop with a separate paw shift.
///   - Nothing else in v22, v23 or v24 logic is touched. Single-block edit in LateUpdate.
/// v26: Parry guard offset gating fix — apply the lift across the ENTIRE guard envelope,
///      not just during walk states. Fixes the visible body rise at idle->walk transition.
///   - Root cause: v22 (carried into v25) gated guardModelYOffset on the walk states only
///     (parryWalkForwardState / parryWalkBackwardState). That gating was correct for the OLD
///     combined parry clip design where the idle pose had paws planted at ground level and
///     only the walk pose had paws baked low. With the v23 clean-clip rebuild, BOTH the
///     Parry_Start and Parry Idle clips have paws baked below the body root at the same
///     depth as the walk clips. So while idle, body was at original Y and paws were buried.
///     When walking started, body lifted to original Y + 0.10-0.15. The lift was correct
///     for walk but wrong for idle, so the player saw idle-clip paws clipping through
///     terrain followed by a body rise the moment WASD was pressed. That body rise was
///     the "getting up too early" symptom — body was anticipating the walk a fraction
///     ahead of the pose blend.
///   - Fix: gate guardModelYOffset on isGuarding (the whole guard envelope) instead of
///     on isGuardWalking (walks only). Body now sits at the lifted Y from Q-press through
///     Q-release. No visible level change between Parry_Start, Parry Idle, and the walks.
///     The smooth ramp introduced in v25 is unchanged and now does the work it was
///     designed for: covering the entry transition (Q press, ramp from 0 to offset over
///     guardOffsetRampDuration) and the exit transition (Q release, ramp back to 0).
///   - The walk-state detection variable (isGuardWalking) is removed from the LateUpdate
///     block since it's no longer needed there. currentGuardAnim is still used elsewhere
///     in UpdateGuardAnimation for state selection, so that field stays.
///   - Tuning context for guardOffsetRampDuration after this change:
///       Guard entry CrossFade is 0.15s (PlayGuardAnim). Guard exit CrossFade is 0.1s
///       (ReturnToIdle). Setting guardOffsetRampDuration around 0.10-0.15s keeps body Y
///       and pose blend roughly synchronized in both directions. Lower than 0.1 = body
///       drops faster than pose on exit (Yoru rises out of parry early). Higher than
///       ~0.2 = body lingers up after pose has settled (Yoru stays floating briefly).
///   - guardModelYOffset must equal the actual depth the parry clips bake the paws below
///     the body root. Likely 0.10-0.15 based on the screenshot evidence. Tune until paw
///     tips visibly clear the terrain in idle, walk, and Parry_Start.
///   - Nothing else in v22, v23, v24, or v25 logic is touched. Single-block edit in
///     LateUpdate plus a tooltip update on guardModelYOffset.
/// Confirmed via logs: isDashing and isDodging both get orphaned by rapid input.
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

    [Header("Animation State Names — Guard/Parry")]
    [SerializeField] private string parryStartState = "Parry_Start";
    [SerializeField] private string parryIdleState = "Parry";
    [SerializeField] private string parryWalkForwardState = "Parry_WalkForward";
    [SerializeField] private string parryWalkBackwardState = "Parry_WalkBackward";

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

    [Header("Guard/Parry")]
    [Tooltip("Time window after Q press where a hit triggers perfect parry")]
    [SerializeField] private float perfectParryWindow = 0.2f;
    [Tooltip("Fraction of damage blocked by regular guard (0.7 = 70% blocked, 30% gets through)")]
    [SerializeField] private float guardDamageReduction = 0.7f;
    [Tooltip("Damage dealt to enemy on perfect parry counter")]
    [SerializeField] private int parryCounterDamage = 15;
    [Tooltip("Duration enemy is staggered after perfect parry")]
    [SerializeField] private float parryStaggerDuration = 1.2f;
    [Tooltip("Range to find closest attacking enemy for parry counter")]
    [SerializeField] private float parryCounterRange = 5f;
    [Tooltip("Duration of Parry_Start clip in seconds. After this elapses, idle/walk anims take over based on current input. Set this to match the actual length of your Parry_Start animation clip.")]
    [SerializeField] private float parryIntroLength = 1.26f;
    [Tooltip("Grace window for Q-release detection. If Q reports as released but is pressed back down within this time, treat as continuous hold. Mitigates keyboard ghosting (Q dropped for 1+ frames when pressing A/D simultaneously). 0.08s default — below human key-tap perception (~0.1s) so guard doesn't feel sticky, above any keyboard ghost blip (~0.02-0.05s). Bump to 0.12 if interruptions still occur.")]
    [SerializeField] private float qReleaseGraceTime = 0.08f;
    [Tooltip("Y offset applied to bodyYoru during the ENTIRE guard envelope (Parry_Start + Parry Idle + parry walks) to lift paw tips off the ground. The v23 parry clips bake paw tips slightly below the body root in all three states at the same depth, so this single value covers all of them. Set this to the actual depth the clips bake the paws below the body root. Likely range 0.10-0.15. Set to 0 to disable. Pair with guardOffsetRampDuration so body and animation pose move together on guard entry and exit. v26: was previously gated on walk states only, which produced a visible body rise at idle->walk.")]
    [SerializeField] private float guardModelYOffset = 0.15f;
    [Tooltip("Duration in seconds for guardModelYOffset to ramp up (entering walk) or ramp down (exiting walk/guard). Should approximately match the parry CrossFade blend times (0.1s for start->walk, 0.2s for idle->walk, 0.25s for walk->idle). 0.2s is a sensible default that covers most transitions. Lower = snappier transition, higher = floatier transition. v25 fix: replaced the old instant-up + 10-units/sec-descent behavior with this single tunable ramp on both directions.")]
    [SerializeField] private float guardOffsetRampDuration = 0.2f;
    [Tooltip("Y offset applied to bodyYoru during dash to lift paw tips off ground")]
    [SerializeField] private float dashModelYOffset = 0.1f;
    [Tooltip("Visual model root (auto-finds bodyYoru). Offset during guard/dash for paw clipping fix.")]
    [SerializeField] private Transform visualModelRoot;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 2f;

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
    private GuardMovementController guardMovement;
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

    // Dodge (frontflip — C)
    private bool isDodging;
    private float dodgeStartTime;
    private float currentDodgeDuration;
    private Quaternion dodgeLockedRotation;
    private Coroutine dodgeCoroutine;
    private bool hasUsedAirDodge;
    private float dodgeEndTime;

    // Dash (rush — MMB)
    private bool isDashing;
    private float dashStartTime;
    private float currentDashDuration;
    private Quaternion dashLockedRotation;
    private Coroutine dashCoroutine;
    private bool hasUsedAirDash;

    // Guard/Parry (Q)
    private bool isGuarding;
    private float guardStartTime;
    private float guardEndTime;       // cooldown — prevents rapid Q tap from corrupting Animator
    private string currentGuardAnim;
    private float lastCombatCrossFadeTime; // tracks last CrossFade on combat layer for health check
    private bool parryIntroComplete;       // true once Parry anim has played past the intro frames
    private Vector3 originalModelLocalPos;  // cached to restore after guard/dash Y offset
    private float guardStuckTimer;          // tracks how long isGuarding is true while Q not held
    private float qReleaseStartTime = -1f;  // -1 = Q held; >=0 = timestamp when Q first appeared released (v24 grace window)
    private float heavyStuckTimer;          // tracks how long isChargingHeavy is true while LMB not held
    private float guardIdleDebounceTimer;   // prevents flicker during quick walk direction changes (W→S passes through 0)
    private bool modelOffsetActive;         // true when bodyYoru Y offset is applied
    private float currentModelYOffset;      // current interpolated offset for smooth transitions
    private float combatIdleSettledTimer;   // tracks how long Animator combat layer has been in idle
    private float movementStuckTimer;       // tracks how long WASD held while movement blocked by combat flags

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
        guardMovement = GetComponent<GuardMovementController>();
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

        if (guardMovement == null)
            Debug.LogWarning("[Combat] WARNING: GuardMovementController not found! Add it to PlayerYoru_Def.");

        // Auto-find visual model root for guard/dash Y offset (paw clipping fix)
        if (visualModelRoot == null)
        {
            visualModelRoot = cachedTransform.Find("bodyYoru");
            if (visualModelRoot == null)
                Debug.LogWarning("[Combat] WARNING: visualModelRoot (bodyYoru) not found! Assign in Inspector.");
        }
        if (visualModelRoot != null)
            originalModelLocalPos = visualModelRoot.localPosition;

        DebugLog("PlayerCombat initialized — Phase 3C v26");
    }

    private void Update()
    {
        EnforcePositionLock();
        UpdateHitReaction();
        HandleInput();
        CheckGroundedStatus();

        if (isGuarding)
            UpdateGuardAnimation();

        // Continuous soft lock-on during attacks — Yoru tracks nearest enemy while feet stay planted.
        // Like God of War: if enemy teleports behind mid-combo, Yoru turns to face them.
        // Position lock (combo 3, heavy) only locks position, not rotation (see EnforcePositionLock).
        if (isAttacking && !isInHitReaction)
            FaceNearestEnemy();

        if (!isAttacking && !isInHitReaction && !isDodging && !isDashing && !isGuarding
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

        // Dodge timeout — tightened from +1.0s to +0.3s, plus 2s absolute hard cap
        if (isDodging)
        {
            float dodgeElapsed = Time.time - dodgeStartTime;
            if (dodgeElapsed > currentDodgeDuration + 0.3f || dodgeElapsed > 2.0f)
            {
                DebugLog($"Safety: dodge timeout ({dodgeElapsed:F2}s, expected {currentDodgeDuration:F2}s)");
                EndDodge();
            }
        }

        // Dash timeout — same tightening
        if (isDashing)
        {
            float dashElapsed = Time.time - dashStartTime;
            if (dashElapsed > currentDashDuration + 0.3f || dashElapsed > 2.0f)
            {
                DebugLog($"Safety: dash timeout ({dashElapsed:F2}s, expected {currentDashDuration:F2}s)");
                EndDash();
            }
        }

        // Guard safety — if Q not held but isGuarding stuck, Q release was missed (rapid input)
        // Uses accumulator instead of timestamp: only counts continuous frames where Q is up
        if (isGuarding)
        {
            if (!Input.GetKey(KeyCode.Q))
            {
                guardStuckTimer += Time.deltaTime;
                if (guardStuckTimer > 0.5f)
                {
                    DebugLog("Safety: guard stuck (Q released but isGuarding true) — forcing EndGuard");
                    EndGuard();
                    guardStuckTimer = 0f;
                }
            }
            else
            {
                guardStuckTimer = 0f; // Q is held, not stuck
            }
        }
        else
        {
            guardStuckTimer = 0f;
        }

        // Heavy charge safety — if LMB not held but isChargingHeavy stuck
        if (isChargingHeavy)
        {
            if (!Input.GetMouseButton(0))
            {
                heavyStuckTimer += Time.deltaTime;
                if (heavyStuckTimer > 0.5f)
                {
                    DebugLog("Safety: heavy charge stuck (LMB released but isChargingHeavy true) — resetting");
                    isChargingHeavy = false;
                    attackButtonHoldTime = 0f;
                    ReturnToIdle();
                    heavyStuckTimer = 0f;
                }
            }
            else
            {
                heavyStuckTimer = 0f;
            }
        }
        else
        {
            heavyStuckTimer = 0f;
        }

        // === Animator-driven orphan flag detection ===
        // If the combat layer has settled in CombatIdle for 0.3s+ but any flag is stuck → reset.
        // Timer is reset to 0 at every action start (StartGuard, PerformDodge, etc.) so it
        // never false-positives during the CrossFade transition from idle into the action.
        if (animator != null)
        {
            AnimatorStateInfo combatState = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            bool animatorInIdle = combatState.IsName(combatIdleStateName)
                && !animator.IsInTransition(combatLayerIndex);

            if (animatorInIdle)
                combatIdleSettledTimer += Time.deltaTime;
            else
                combatIdleSettledTimer = 0f;

            bool anyFlagStuck = isAttacking || isDodging || isDashing || isGuarding
                || isChargingHeavy || isInHitReaction;

            if (combatIdleSettledTimer > 0.3f && anyFlagStuck)
            {
                DebugLog($"Safety: Animator idle but flags stuck (atk={isAttacking} dod={isDodging} dsh={isDashing} grd={isGuarding} hvy={isChargingHeavy} hit={isInHitReaction}) — forcing reset");
                ForceResetCombat();
                combatIdleSettledTimer = 0f;
            }
        }

        // === Movement-stuck safety net ===
        // If WASD held for 1.0s while any combat flag blocks movement → force reset.
        // Timer resets at every action start (PerformDodge, PerformDash, etc.) so it
        // only fires AFTER the expected action duration. 1.0s is longer than any single
        // action (max is dodge at 0.87s) but shorter than the old 1.5s.
        //
        // GUARD IS EXCLUDED. Guard allows legitimate movement at 0.75x via
        // GuardMovementController, so holding Q+WASD is normal play, not a stuck state.
        // Guard has its own safeties: the Q-released stuck check above (lines ~312)
        // and the animator-orphan detection (lines ~365) catch real stuck-guard cases.
        {
            bool anyFlagBlocking = isAttacking || isChargingHeavy || isDodging || isDashing;
            bool wasdHeld = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

            if (anyFlagBlocking && wasdHeld)
            {
                movementStuckTimer += Time.deltaTime;
                if (movementStuckTimer > 1.0f)
                {
                    DebugLog($"Safety: movement stuck 1.0s (atk={isAttacking} dod={isDodging} dsh={isDashing} hvy={isChargingHeavy} hit={isInHitReaction} animSpeed={animator?.speed}) — forcing reset");
                    if (animator != null) animator.speed = 1f;
                    ForceResetCombat();
                    movementStuckTimer = 0f;
                }
            }
            else
            {
                movementStuckTimer = 0f;
            }
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();
        if (isDodging)
            cachedTransform.rotation = dodgeLockedRotation;
        if (isDashing)
            cachedTransform.rotation = dashLockedRotation;

        // Per-frame model Y offset — prevents paw tips from clipping underground.
        // Runs in LateUpdate so it applies AFTER animation poses are set.
        // Guard: offset applied for the entire isGuarding window (v26 — was walk-only in v22-v25).
        // Dash: offset applied for the isDashing window.
        // Both use the smooth ramp introduced in v25 (single MoveTowards on entry and exit).
        if (visualModelRoot != null)
        {
            // v26: offset applies during the ENTIRE guard envelope, not just walks.
            // The v23 clean parry clips have paws baked below the body root in Parry_Start,
            // Parry Idle, AND the walks at the same depth. v22's walk-only gating produced
            // a visible body rise at idle->walk because idle was at original Y and walk was
            // at original + offset. Body now sits at lifted Y from Q-press through Q-release,
            // so there's no level change within the guard envelope. The smooth ramp from v25
            // (the MoveTowards block below) handles the entry and exit transitions only.
            float targetOffset = 0f;
            if (isGuarding)
            {
                targetOffset = guardModelYOffset;
            }
            else if (isDashing)
            {
                targetOffset = dashModelYOffset;
            }

            // v25: Smooth ramp on BOTH directions (was: instant up, fast descent at 10 units/sec).
            // The old design tried to prevent a 1-2 frame paw dip at walk-start by snapping the body
            // up instantly, but with the v23 clean walk clips the snap itself became the visible
            // problem: body popped up while the animation pose was still blending in idle, producing
            // a visible level change. The 10-units/sec descent at walk-end was so fast it
            // functionally snapped down too, producing an abrupt body drop on guard exit.
            // Ramp duration matches the parry CrossFade blend times (0.1-0.25s) so body Y and the
            // animation pose move on the same timeline. Speed is computed as offset / duration so
            // the visible ramp time stays constant regardless of the offset value chosen.
            // Fallback to the old fast rate if either field is configured at zero, so the behavior
            // degrades gracefully rather than getting stuck mid-ramp.
            float rampSpeed = (guardModelYOffset > 0.001f && guardOffsetRampDuration > 0.001f)
                ? guardModelYOffset / guardOffsetRampDuration
                : 10f;
            currentModelYOffset = Mathf.MoveTowards(currentModelYOffset, targetOffset, Time.deltaTime * rampSpeed);

            bool needsOffset = currentModelYOffset > 0.001f;
            if (needsOffset)
            {
                Vector3 pos = originalModelLocalPos;
                pos.y += currentModelYOffset;
                visualModelRoot.localPosition = pos;
                modelOffsetActive = true;
            }
            else if (modelOffsetActive)
            {
                visualModelRoot.localPosition = originalModelLocalPos;
                currentModelYOffset = 0f;
                modelOffsetActive = false;
            }
        }
    }

    private void EnforcePositionLock()
    {
        if (!lockPosition || isDodging || isDashing || characterController == null || !wasGroundedWhenLocked)
            return;
        characterController.enabled = false;
        cachedTransform.position = lockedPosition;
        // Rotation NOT locked — FaceNearestEnemy needs to track enemies during combo 3 and heavy
        characterController.enabled = true;
    }

    private void CheckGroundedStatus()
    {
        if (characterController != null && characterController.isGrounded)
        {
            // Force-end aerial attacks on landing.
            // OnAttackEnd() is an animation event on the Combo3 clip, but when Yoru lands
            // mid-clip the base layer crossfade can interrupt combat layer playback, causing
            // the event to never fire → isAttacking stuck permanently → movement frozen.
            // 0.15s grace prevents false trigger on the frame right after jump (isGrounded
            // can be stale for 1-2 frames before CharacterController.Move applies velocity).
            if (isAerialAttack && isAttacking && Time.time - attackStartTime > 0.15f)
            {
                DebugLog("Aerial spin: force-ending on landing (animation event unreliable mid-air)");
                OnAttackEnd();
            }

            if (hasUsedAerialAttack && !isAttacking)
            {
                hasUsedAerialAttack = false;
                isAerialAttack = false;
            }
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

        // === GUARD INPUT (Q key) ===
        // Cooldown prevents rapid Q tap from corrupting Animator (same principle as dodge cooldown)
        if (Input.GetKeyDown(KeyCode.Q) && !isGuarding && Time.time - guardEndTime > 0.2f)
        {
            if (characterController != null && characterController.isGrounded)
            {
                StartGuard();
                return;
            }
        }

        // During guard: Q overrides ALL combat actions. Only release Q ends guard.
        // Same as Sekiro — hold block = hold block, nothing interrupts it.
        //
        // v24: Q-release uses grace-period polling instead of GetKeyUp.
        // Reason: keyboard ghosting / N-key rollover on many keyboards drops Q for 1+
        // frames when A or D is pressed simultaneously. GetKeyUp fires on that single
        // dropped frame, ending guard even though Q is still physically held.
        // Polling with grace: only end guard if Q has been continuously released for
        // qReleaseGraceTime (default 0.08s). Brief ghost blips (typically <0.05s) are
        // ignored; intentional releases (>0.08s, well below ~0.1s human tap perception)
        // end guard normally.
        if (isGuarding)
        {
            if (Input.GetKey(KeyCode.Q))
            {
                qReleaseStartTime = -1f; // Q is held, clear any pending release
            }
            else
            {
                if (qReleaseStartTime < 0f)
                {
                    qReleaseStartTime = Time.time; // first frame Q reported released — start grace
                }
                else if (Time.time - qReleaseStartTime >= qReleaseGraceTime)
                {
                    EndGuard();
                    qReleaseStartTime = -1f;
                    return;
                }
            }
            return; // Q held (or pending release within grace) — guard overrides everything
        }

        // Dodge input (C key)
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (TryDodge()) return;
        }

        // Dash input (Middle Mouse)
        if (Input.GetMouseButtonDown(2))
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

    #region Guard/Parry System (Q — Sekiro-style)
    private void StartGuard()
    {
        combatIdleSettledTimer = 0f; // prevent orphan detection from killing this action
        movementStuckTimer = 0f;
        // Can cancel combo 1-2 into guard
        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return;
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

        isGuarding = true;
        guardStartTime = Time.time;
        currentGuardAnim = "";
        parryIntroComplete = false;
        guardIdleDebounceTimer = 0f;
        qReleaseStartTime = -1f; // v24: clear any stale grace state from prior guard

        PlayGuardAnim(parryStartState);

        // Lock guard facing to the direction player is currently moving
        // If pressing D → guard faces right. If no input → fall back to transform.forward.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 guardDir;
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
            guardDir = GetInputDirectionCameraRelative(h, v);
        else
            guardDir = cachedTransform.forward;

        if (guardMovement != null)
            guardMovement.EnableGuard(guardDir);

        DebugLog($"Guard START (perfect parry window: {perfectParryWindow}s)");
    }

    private void EndGuard()
    {
        if (!isGuarding) return;

        isGuarding = false;
        guardEndTime = Time.time;
        currentGuardAnim = "";
        qReleaseStartTime = -1f; // v24: clear grace state on exit

        if (guardMovement != null)
            guardMovement.DisableGuard();

        ReturnToIdle();
        DebugLog("Guard END");
    }

    private void UpdateGuardAnimation()
    {
        // === Phase 1: Start animation ===
        // While Parry_Start is still playing, leave it alone — don't override with idle/walk.
        // Once start clip completes, parryIntroComplete flips true and Phase 2 takes over.
        // Time-based detection: simpler than animator state polling, matches existing parryIntroLength field.
        // Note: hitstop (animator.speed=0) pauses animation playback but Time.time keeps ticking,
        // so a perfect-parry triggered during the start phase may slightly truncate the start clip.
        // That's an acceptable tradeoff — perfect parry is a dramatic moment, the start phase has
        // already played partially, and gameplay flow matters more than animation purity.
        if (!parryIntroComplete)
        {
            if (Time.time - guardStartTime >= parryIntroLength)
            {
                parryIntroComplete = true;
                // Fall through to Phase 2 — pick idle/walk based on current input
            }
            else
            {
                return; // Start clip still playing; do nothing
            }
        }

        // === Phase 2: Idle / walk selection ===
        // Projection onto locked guard direction — same threshold (0.3) as GuardMovementController
        // so animation and movement always agree. Below 0.3 = no movement AND no anim switch.
        float projection = 0f;
        if (guardMovement != null)
            projection = guardMovement.GetGuardInputProjection();

        bool anyDirectionHeld = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

        string targetAnim;

        if (projection > 0.3f)
        {
            targetAnim = parryWalkForwardState;
            guardIdleDebounceTimer = 0f;
        }
        else if (projection < -0.3f)
        {
            targetAnim = parryWalkBackwardState;
            guardIdleDebounceTimer = 0f;
        }
        else if (anyDirectionHeld)
        {
            // Keys held but perpendicular to guard axis (projection between -0.3 and 0.3).
            // Keep whatever anim is currently playing — don't disrupt.
            // If still on parryStartState (just finished this frame), default to idle.
            targetAnim = currentGuardAnim;
            if (targetAnim == "" || targetAnim == parryStartState) targetAnim = parryIdleState;
            guardIdleDebounceTimer = 0f;
        }
        else
        {
            // No keys held at all. If currently walking, debounce before switching to idle.
            // This prevents 1-2 frame flicker when switching W→S (projection passes through 0).
            bool currentlyWalking = currentGuardAnim == parryWalkForwardState
                || currentGuardAnim == parryWalkBackwardState;

            if (currentlyWalking)
            {
                guardIdleDebounceTimer += Time.deltaTime;
                if (guardIdleDebounceTimer > 0.1f)
                {
                    targetAnim = parryIdleState;
                    guardIdleDebounceTimer = 0f;
                }
                else
                {
                    targetAnim = currentGuardAnim; // hold walk a bit longer
                }
            }
            else
            {
                targetAnim = parryIdleState;
                guardIdleDebounceTimer = 0f;
            }
        }

        if (targetAnim != currentGuardAnim)
        {
            // Blend times tuned per transition type
            float blendTime;
            if (currentGuardAnim == parryStartState)
                blendTime = 0.1f; // start → idle/walk: short blend (start clip ends in parry pose, idle begins in parry pose)
            else if (targetAnim == parryIdleState)
                blendTime = 0.25f; // walk → idle: smooth return
            else if (currentGuardAnim == parryIdleState || currentGuardAnim == "")
                blendTime = 0.2f;  // idle → walk: slightly longer to cover foot transition
            else
                blendTime = 0.15f; // walk ↔ walk (forward ↔ backward): quick

            currentGuardAnim = targetAnim;
            if (animator != null)
            {
                // Plain CrossFade — no normalizedTimeOffset needed since the new parryIdleState
                // is a clean loop (no baked intro to skip past).
                animator.CrossFadeInFixedTime(targetAnim, blendTime, combatLayerIndex);
                lastCombatCrossFadeTime = Time.time;
            }
        }

        // NOTE: The old "Parry idle loop skip" block (~35 lines) has been removed in v23.
        // It was a workaround for the previous design where parryIdleState contained both
        // the intro and the looping idle, and we had to prevent the clip from wrapping back
        // to frame 0 (which would replay the intro). With the new Parry_Start as a separate
        // one-shot clip, parryIdleState is now a clean loop and needs no intervention.
    }

    private void PlayGuardAnim(string stateName)
    {
        currentGuardAnim = stateName;
        if (animator != null)
        {
            animator.CrossFadeInFixedTime(stateName, 0.15f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
    }

    public bool IsInPerfectParryWindow()
    {
        return isGuarding && (Time.time - guardStartTime) <= perfectParryWindow;
    }

    public void OnPerfectParry(Vector3 attackerPos)
    {
        DebugLog("PERFECT PARRY!");

        EnemyCombat closestEnemy = FindClosestAttackingEnemy();
        if (closestEnemy != null)
        {
            closestEnemy.TriggerStagger(parryStaggerDuration);
            DebugLog($"Parry stagger: {closestEnemy.name} for {parryStaggerDuration}s");

            EnemyHealth enemyHealth = closestEnemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(parryCounterDamage, true);
                DebugLog($"Parry counter damage: {parryCounterDamage}");
            }
        }

        // Feedback — pass both animators for hitstop
        if (CombatFeedbackManager.Instance != null)
        {
            Animator enemyAnimator = closestEnemy != null ? closestEnemy.GetComponent<Animator>() : null;
            if (enemyAnimator == null && closestEnemy != null)
                enemyAnimator = closestEnemy.GetComponentInChildren<Animator>();
            CombatFeedbackManager.Instance.PlayParryFeedback(cachedTransform.position, animator, enemyAnimator);
        }
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayParryClang();
    }

    public void OnGuardHit(bool isHeavy)
    {
        DebugLog($"Guard blocked ({guardDamageReduction * 100f:F0}% reduced)");

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.PlayGuardFeedback();
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayGuardBlock();
    }

    private EnemyCombat FindClosestAttackingEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(cachedTransform.position, parryCounterRange, enemyLayer);
        EnemyCombat closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider col in nearby)
        {
            EnemyCombat ec = col.GetComponent<EnemyCombat>();
            if (ec == null) continue;

            EnemyHealth eh = col.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead()) continue;

            float dist = Vector3.Distance(cachedTransform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = ec;
            }
        }

        return closest;
    }
    #endregion

    #region Dodge System (C — evasive frontflip with arc)
    private bool TryDodge()
    {
        if (characterController == null) return false;
        if (Time.time - dodgeEndTime < 0.15f) return false;

        bool isGrounded = characterController.isGrounded;

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
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
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

        animator.CrossFadeInFixedTime(animState, 0.12f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;

        DebugLog($"Dodge: {animState} ({distance}m, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dodgeCoroutine != null) StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, distance));
    }

    private IEnumerator DodgeMovement(Vector3 direction, float distance)
    {
        float duration = dodgeFallbackDuration;
        bool needsClipUpdate = true;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (stateInfo.length > 0.1f)
            {
                duration = stateInfo.length;
                needsClipUpdate = false;
            }
        }
        currentDodgeDuration = duration;

        float elapsed = 0f;
        float previousEased = 0f;
        float previousArc = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (needsClipUpdate && animator != null)
            {
                AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (si.length > 0.1f)
                {
                    duration = si.length;
                    currentDodgeDuration = duration;
                }
                needsClipUpdate = false;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);

                if (dodgeHeight > 0f)
                {
                    // Use eased t (not raw t) for zero-velocity arc endpoints:
                    // sin(smoothstep(t) * PI) has derivative=0 at t=0 and t=1,
                    // eliminating sudden Y jolts that cause camera overshoot
                    float arc = Mathf.Sin(eased * Mathf.PI) * dodgeHeight;
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
        if (dodgeCoroutine != null)
        {
            StopCoroutine(dodgeCoroutine);
            dodgeCoroutine = null;
        }
        dodgeEndTime = Time.time;
        if (animator != null)
        {
            // 0.25s blend for smoother frontflip→sprint/idle transition
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.25f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
        DebugLog("Dodge ended");
    }

    public bool IsInDodgeIFrames()
    {
        if (!isDodging || animator == null) return false;
        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= iFrameStart && normalizedTime <= iFrameEnd;
    }
    #endregion

    #region Dash System (MMB — aggressive flat rush with damage)
    private bool TryDash()
    {
        if (characterController == null) return false;
        if (isDodging || isDashing) return false;

        bool isGrounded = characterController.isGrounded;

        if (!isGrounded)
        {
            if (hasUsedAirDash) return false;
            if (playerMovement == null || playerMovement.GetJumpWindowTimer() <= 0f) return false;
        }

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
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
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

        animator.CrossFadeInFixedTime(animState, 0.03f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;

        DebugLog($"Dash: {animState} ({distance}m, {dashDamage} dmg, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeDashTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = StartCoroutine(DashMovement(moveDir, distance));
    }

    private IEnumerator DashMovement(Vector3 direction, float distance)
    {
        float duration = dashFallbackDuration;
        bool needsClipUpdate = true;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (stateInfo.length > 0.1f)
            {
                duration = stateInfo.length;
                needsClipUpdate = false;
            }
        }
        currentDashDuration = duration;

        float elapsed = 0f;
        float previousEased = 0f;
        var hitEnemyIDs = new System.Collections.Generic.HashSet<int>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (needsClipUpdate && animator != null)
            {
                AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (si.length > 0.1f)
                {
                    duration = si.length;
                    currentDashDuration = duration;
                }
                needsClipUpdate = false;
            }

            float t = Mathf.Clamp01(elapsed / duration);
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
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        dodgeEndTime = Time.time;
        if (animator != null)
        {
            // 0.2s blend for smoother dash→sprint/idle transition
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.2f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
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
        combatIdleSettledTimer = 0f;
        if (!gameObject.activeInHierarchy) return;

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

        if (isGuarding) EndGuard();

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
            animator.CrossFadeInFixedTime(animState, 0.02f, combatLayerIndex, 0f);
            lastCombatCrossFadeTime = Time.time;
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
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
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
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
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
        combatIdleSettledTimer = 0f;
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
        lastCombatCrossFadeTime = Time.time;
    }

    private void ReturnToIdle()
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(combatIdleStateName, 0.1f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;
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
        dodgeEndTime = 0f;
        guardStuckTimer = 0f;
        heavyStuckTimer = 0f;
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        guardIdleDebounceTimer = 0f;

        if (isGuarding) EndGuard();

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
            animator.speed = 1f; // Defense in depth: always restore speed on combat reset
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }
        DebugLog("Combat reset");
    }
    #endregion

    #region Public Getters
    public bool IsAttacking()
    {
        // Self-heal: if isAttacking stuck longer than maxAttackDuration, force-clear.
        // Covers aerial spin landing where OnAttackEnd animation event may not fire
        // (e.g. combat layer crossfade interrupts the clip before the event frame).
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("[Self-heal] isAttacking force-cleared (exceeded maxAttackDuration)");
            ForceResetCombat();
            return false;
        }
        return isAttacking;
    }
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    public bool IsAerialAttack() => isAerialAttack;
    public bool IsPositionLocked() => lockPosition;
    public bool IsInHitReaction() => isInHitReaction;
    public bool IsDodging()
    {
        // Self-heal: if isDodging stuck past expected duration + 0.5s grace, force-clear.
        // Coroutines can die silently (rapid input, component disable, etc.) without
        // reaching EndDodge(). This getter is called every frame by PlayerMovement line 222,
        // so the flag self-corrects within one frame of exceeding the deadline.
        if (isDodging && Time.time - dodgeStartTime > currentDodgeDuration + 0.5f)
        {
            DebugLog($"[Self-heal] isDodging force-cleared ({Time.time - dodgeStartTime:F2}s, expected {currentDodgeDuration:F2}s)");
            EndDodge();
            return false;
        }
        return isDodging;
    }
    public bool IsDashing()
    {
        // Self-heal: same pattern as IsDodging.
        if (isDashing && Time.time - dashStartTime > currentDashDuration + 0.5f)
        {
            DebugLog($"[Self-heal] isDashing force-cleared ({Time.time - dashStartTime:F2}s, expected {currentDashDuration:F2}s)");
            EndDash();
            return false;
        }
        return isDashing;
    }
    public bool IsGuarding() => isGuarding;
    public float GetDodgeEndTime() => dodgeEndTime;
    public float GetGuardDamageReduction() => guardDamageReduction;
    public int GetParryCounterDamage() => parryCounterDamage;
    public Animator GetAnimator() => animator;
    public float GetAnimatorSpeed() => animator != null ? animator.speed : -1f;
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