using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Right tail air shot, bow style. Locked spec Aug 5.
///
/// Flow:
///  - Hold R while airborne in a 2 LEG jump (base layer state JumpWith2Legs): the whole game clock
///    slows instantly. No animation yet. The 4 leg jump refuses the ability.
///  - Press and HOLD the fire mouse button (RMB): the draw state (RightTail_Fast) plays frames
///    0 to 4 at its real pace and stops frozen on frame 4. Holding RMB also turns the camera
///    exactly like normal play, and that camera turn is the aiming. The camera scripts are not
///    touched, not suppressed, not called. Nothing here talks to ThirdPersonCamera.
///  - RELEASE RMB: the motion runs from frame 4 to the end and the arrow fires WHEN THE MOTION
///    FINISHES, from the right tail tip, toward the screen centre. Press RMB again for the next
///    shot, from frame 0 again. As many shots as fit in the jump.
///  - No timer. Only releasing R or landing ends the ability.
///  - Landing or releasing R while the shot motion is running: the motion finishes at normal
///    pace and still fires (the slow ends immediately, the clip does not).
///  - Landing while frozen on frame 4 (or still drawing): the slow ends, the motion completes on
///    the ground at normal pace and fires at the end.
///  - Releasing R while frozen on frame 4 in the air: clean cancel, no arrow.
///  - LMB does nothing while the ability or its shot motion is active. PlayerCombat carries a
///    matching gate that returns out of HandleInput while IsAiming or IsShotRunning is true.
///
/// HARD DEPENDENCY, both must be in the project or the feature misbehaves:
///  1. This file.
///  2. The gate block near the top of PlayerCombat.HandleInput reading the two statics below.
///     Without it, LMB during the slow starts the air swirl / heavy charge and dirties the
///     combat layer. If you click LMB during the slow and do NOT see
///     "[PlayerCombat] Combat input BLOCKED during tail air shot." the gate is missing.
///
/// Animator setup, one time, in the Animator window:
///  1. Parameters tab: add a FLOAT named TailCastSpeed, default 1.
///  2. Combat layer, select the RightTail_Fast state: tick the Parameter box on the Speed
///     Multiplier and pick TailCastSpeed. Leave the state's own Speed at 1.
///  This script drives that parameter: 0 = frozen on frame 4, otherwise castPace divided by the
///  current time scale, so the clip NEVER slows down with the world and plays at the same real
///  pace on the ground. Animator.speed is never touched, hitstop owns it. A missing parameter is
///  reported loudly in the Console at Start.
///
/// Inspector on the player after updating (scene values override code defaults):
///  - Draw State Name: RightTail_Fast  (old scenes still say LeftTail_Fast, flip it by hand)
///  - Fire Mouse Button: 1 (RMB, already the serialized value)
///  - Right Tail Tip: leave empty, it auto finds Tail6_R_end_end and logs what it found.
///
/// Kept from the baseline because they are correct: fixedDeltaTime scaled together with
/// timeScale (PlayerMovement moves in FixedUpdate), the CinemachineBrain IgnoreTimeScale toggle
/// during the slow restored exactly on exit, every exit crossfades the combat layer back to
/// Combat_Empty, and the reticle as a root canvas at true screen centre.
///
/// Height snap suspects from the old build are designed out: no updateMode switching, no per
/// frame Play re pinning (frame 4 is pinned with a single Play call), no Animator.speed use.
/// Every phase change logs Y, timeScale and fixedDeltaTime so a bad frame names itself.
/// </summary>
public class TailAimController : MonoBehaviour
{
    #region Inspector
    [Header("Input")]
    [Tooltip("Hold this while airborne in a 2 leg jump to slow time. R by default.")]
    [SerializeField] private KeyCode drawKey = KeyCode.R;
    [Tooltip("Mouse button that draws and shoots. 1 is the right mouse button. Hold to draw and aim, release to shoot.")]
    [SerializeField] private int fireMouseButton = 1;

    [Header("Slow Motion")]
    [Tooltip("Game time scale while the ability is active. 0.1 is a 10x slow. The physics clock is scaled with it so the slow stays smooth.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowFactor = 0.1f;

    [Header("Animation")]
    [Tooltip("Animator state of the cast clip on the combat layer. Must be RightTail_Fast. Old scene values may still say LeftTail_Fast, flip this by hand in the Inspector.")]
    [SerializeField] private string drawStateName = "RightTail_Fast";
    [Tooltip("Combat layer index. PlayerCombat uses 1.")]
    [SerializeField] private int combatLayerIndex = 1;
    [Tooltip("Blend time into the draw, in real seconds. Kept tiny so the draw feels crisp.")]
    [SerializeField] private float drawCrossfade = 0.08f;
    [Tooltip("Combat layer state to crossfade back to on every exit. Combat_Empty releases the layer so base locomotion shows through.")]
    [SerializeField] private string exitStateName = "Combat_Empty";
    [Tooltip("Blend time out of the cast on exit, in real seconds.")]
    [SerializeField] private float exitCrossfade = 0.12f;
    [Tooltip("Float parameter on the animator wired as the RightTail_Fast state's speed multiplier. 0 freezes the pose, this script drives it.")]
    [SerializeField] private string castSpeedParamName = "TailCastSpeed";
    [Tooltip("YOUR pace knob. How fast the cast plays in real time, no matter how slow the world is. 1 = the clip's authored pace, 2 = twice as fast.")]
    [SerializeField] private float castPace = 1f;
    [Tooltip("The frame the draw freezes on as the ready pose.")]
    [SerializeField] private int readyFrame = 4;
    [Tooltip("The frame the arrow actually leaves the tail, read off the clip in the Preview window. The motion keeps playing to the end after it, so the throw follows through. Nudge this by one if the arrow reads early or late.")]
    [SerializeField] private int fireFrame = 12;
    [Tooltip("Total frames of the cast clip. RightTail_Fast is 1.40s at 30 samples = 42.")]
    [SerializeField] private int clipFrameCount = 42;
    [Tooltip("Turn Yoru to face the aim direction while drawing and holding the ready pose.")]
    [SerializeField] private bool faceAimWhileDrawing = true;

    [Header("Jump Gate")]
    [Tooltip("Base layer state of the 2 leg jump. Only this jump may use the ability.")]
    [SerializeField] private string twoLegJumpStateName = "JumpWith2Legs";
    [Tooltip("Base layer state of the 4 leg jump. Seeing it clears the permission for this airtime.")]
    [SerializeField] private string fourLegJumpStateName = "JumpWith4Legs";

    [Header("Bolt")]
    [Tooltip("Prefab with a TailProjectile component. Spawned from the right tail tip when the motion finishes.")]
    [SerializeField] private GameObject boltPrefab;
    [Tooltip("Where the arrow is born. Drag any transform here (an empty child parented to the very end of the right tail is ideal). Left blank it auto finds, in order: the name below, then RightTailVFX, then the deepest right tail bone. Whatever it lands on is printed in the Console at Start.")]
    [SerializeField] private Transform rightTailTip;
    [Tooltip("First name tried when Right Tail Tip is empty. RightTailVFX is the anchor YoruVFXManager already uses for right tail effects, so the arrow leaves from the same place as the tail VFX.")]
    [SerializeField] private string rightTailTipBoneName = "RightTailVFX";
    [Tooltip("How far ahead the straight aim point sits when no enemy is locked.")]
    [SerializeField] private float aimRayDistance = 60f;

    [Header("Targeting")]
    [Tooltip("How far to look for an enemy to snap onto.")]
    [SerializeField] private float targetingRange = 60f;
    [Tooltip("An enemy this close to the screen centre (in pixels) gets snapped as the locked target.")]
    [SerializeField] private float snapRadiusPixels = 120f;
    [Tooltip("Layers searched for lockable enemies. Set to your Enemy layer.")]
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Layers that block line of sight and the aim ray (Ground + Interactable, never Enemy or Player).")]
    [SerializeField] private LayerMask environmentMask = ~0;
    [Tooltip("Require a clear line of sight before an enemy can be locked.")]
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Behaviour")]
    [Tooltip("Real seconds of cooldown on ability entry after a shot. Production value 3, zero for testing.")]
    [SerializeField] private float cooldownAfterFire = 0f;
    [Tooltip("Console diagnostics for every phase change, with height and clock values.")]
    [SerializeField] private bool debugLogs = true;

    [Header("Spawn Marker, for looking at only")]
    [Tooltip("Drops two coloured balls the moment the arrow is born so you can SEE where it came from. GREEN sticks to the spot the arrow is fired from and rides the tail. MAGENTA stays put in the world at the exact place the arrow was born. Look at them and tell me which of these you see: (a) green is on her tail tip and magenta is on top of it, (b) green is on her tail tip but magenta is somewhere else, (c) green is not on her tail tip at all. Turn this off once you have looked.")]
    [SerializeField] private bool showSpawnMarker = true;
    [Tooltip("How long the balls stay up, in real seconds.")]
    [SerializeField] private float spawnMarkerSeconds = 2f;
    [Tooltip("How big the balls are, in metres.")]
    [SerializeField] private float spawnMarkerSize = 0.08f;

    [Header("Reticle")]
    [Tooltip("Optional crosshair sprite. A simple dot is generated if left blank.")]
    [SerializeField] private Sprite reticleSprite;
    [Tooltip("Optional lock marker sprite. A simple ring is generated if left blank.")]
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Color reticleColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color reticleLockedColor = new Color(1f, 0.5f, 0.2f, 0.95f);
    [SerializeField] private Color lockMarkerColor = new Color(1f, 0.4f, 0.2f, 0.95f);
    [SerializeField] private float reticleSize = 18f;
    [SerializeField] private float lockMarkerSize = 64f;
    #endregion

    #region State
    /// <summary>True while the slow motion ability is active (from R entry until cancel, exit or handoff to the finisher). PlayerCombat's gate reads this.</summary>
    public static bool IsAiming { get; private set; }

    /// <summary>True while a released shot motion is still running after the ability itself ended (landed or R released mid motion). PlayerCombat's gate reads this too, so nothing stomps the combat layer before the arrow is out.</summary>
    public static bool IsShotRunning { get; private set; }

    private enum Phase { Inactive, Slow, Drawing, Ready, Casting, Finisher }
    private Phase phase = Phase.Inactive;

    private Animator animator;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private FormController formController;
    private CinemachineBrain cinemachineBrain;
    private Camera mainCamera;

    private float lastFireTime = -999f;
    private bool lastJumpWas2Leg;

    // Clock and camera brain state cached on entry and restored exactly on every exit path.
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private bool cachedBrainIgnoreTimeScale;

    private int drawStateHash;
    private int exitStateHash;
    private int twoLegJumpHash;
    private int fourLegJumpHash;
    private int castSpeedParamHash;

    // The point the arrow flies toward. Refreshed while drawing, holding and casting, frozen for
    // the finisher so a shot that outlives the aim still goes where she aimed last.
    private Vector3 lastAimPoint;

    private Transform lockedTarget;
    private Collider lockedCollider;

    // Reusable buffer for the target scan so aiming does not allocate every frame.
    private readonly Collider[] targetBuffer = new Collider[16];

    // Reticle UI, built at runtime as a root canvas so it never inherits the player's transform.
    private Canvas reticleCanvas;
    private Image reticleImage;
    private Image lockImage;

    // True once this shot's arrow has left the tail, so the release cannot fire twice even if the
    // motion is interrupted, handed to the finisher or replayed.
    private bool shotFired;

    // The fire frame is detected in Update, but the arrow is spawned in LateUpdate, after the
    // Animator has posed the skeleton for THIS frame. Reading the tail tip in Update would give
    // last frame's pose, and during a fast throw the tip travels a long way in one frame.
    private bool arrowPending;

    private float ReadyNormalized => clipFrameCount > 0 ? (float)readyFrame / clipFrameCount : 0.095f;
    private float FireNormalized => clipFrameCount > 0 ? (float)fireFrame / clipFrameCount : 0.286f;
    private const float EndNormalized = 0.99f;
    #endregion

    #region Unity
    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
        formController = GetComponent<FormController>();
        mainCamera = Camera.main;
        cinemachineBrain = FindObjectOfType<CinemachineBrain>();

        drawStateHash = Animator.StringToHash(drawStateName);
        exitStateHash = Animator.StringToHash(exitStateName);
        twoLegJumpHash = Animator.StringToHash(twoLegJumpStateName);
        fourLegJumpHash = Animator.StringToHash(fourLegJumpStateName);
        castSpeedParamHash = Animator.StringToHash(castSpeedParamName);

        if (rightTailTip == null) FindRightTailTip();
        BuildReticle();
    }

    private void Start()
    {
        // Loud setup checks so a missed import or a missed Animator click is visible in the log
        // instead of silently misbehaving.
        bool paramFound = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == castSpeedParamHash && p.type == AnimatorControllerParameterType.Float)
            {
                paramFound = true;
                break;
            }
        }
        if (!paramFound)
            Debug.LogWarning("[TailAirShot] Animator float parameter '" + castSpeedParamName
                + "' is MISSING. Add it in the Animator Parameters tab and tick the Speed Multiplier"
                + " Parameter box on the " + drawStateName + " state, or the draw cannot pause on frame "
                + readyFrame + ".");

        if (debugLogs)
            Debug.Log("[TailAirShot] Ready. drawState=" + drawStateName
                + " tailTip=" + (rightTailTip != null ? rightTailTip.name : "NOT FOUND")
                + " castParam=" + castSpeedParamName + (paramFound ? " (found)" : " (MISSING)"));
    }

    private void OnDisable()
    {
        // Never leave the game stuck in slow motion if this is turned off mid ability.
        HardCancel("component disabled");
    }

    private void OnDestroy()
    {
        // The reticle canvas is a root object, so it does not die with the player automatically.
        if (reticleCanvas != null) Destroy(reticleCanvas.gameObject);
    }

    private void Update()
    {
        // Yoru form only. In Granny form the tail abilities are disabled.
        if (formController != null && formController.IsHuman)
        {
            HardCancel("Granny form");
            UpdateCastParam();
            return;
        }

        UpdateJumpLatch();

        switch (phase)
        {
            case Phase.Inactive: TickInactive(); break;
            case Phase.Slow: TickSlow(); break;
            case Phase.Drawing: TickDrawing(); break;
            case Phase.Ready: TickReady(); break;
            case Phase.Casting: TickCasting(); break;
            case Phase.Finisher: TickFinisher(); break;
        }

        UpdateCastParam();
    }

    private void LateUpdate()
    {
        // Spawn here, once the skeleton is posed for this frame, so the arrow starts exactly at
        // the tail tip you can see rather than one frame behind it.
        if (arrowPending)
        {
            arrowPending = false;
            SpawnArrow();
        }

        if (phase != Phase.Drawing && phase != Phase.Ready) return;
        if (faceAimWhileDrawing) FaceAim();
        UpdateReticlePositions();
    }
    #endregion

    #region Phase ticks
    private void TickInactive()
    {
        // Refusal diagnostics only on the press frame and only in the air, so a pre jump R hold
        // on the ground does not spam. This is the log that answers "why did it not trigger".
        if (debugLogs && Input.GetKeyDown(drawKey)
            && playerMovement != null && playerMovement.IsAirborne())
        {
            string why = WhyRefused();
            if (why != null) Log("entry refused: " + why);
        }

        if (!Input.GetKey(drawKey)) return;
        if (WhyRefused() != null) return;

        EnterAbility();
    }

    private void TickSlow()
    {
        // Landing with nothing playing: clean exit, no arrow.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ExitAbility("landed, nothing drawn"); return; }

        // R released with nothing playing: clean exit.
        if (!Input.GetKey(drawKey)) { ExitAbility("R released, nothing drawn"); return; }

        // RMB pressed: start the draw. The camera keeps doing its own vanilla RMB work in parallel.
        if (Input.GetMouseButtonDown(fireMouseButton)) StartDraw();
    }

    private void TickDrawing()
    {
        // Landing while drawing: the slow ends, the motion completes on the ground and fires.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ToFinisher("landed during draw"); return; }

        // R released while drawing, still airborne: clean cancel, no arrow.
        if (!Input.GetKey(drawKey)) { CancelDraw("R released during draw"); return; }

        // RMB released before frame 4: a quick shot. Skip the pause and let it run to the end.
        if (!Input.GetMouseButton(fireMouseButton)) { BeginCast("released early, quick shot"); return; }

        // Reached frame 4: pin it exactly once and freeze.
        if (InDrawState(out AnimatorStateInfo info) && info.normalizedTime >= ReadyNormalized)
        {
            animator.Play(drawStateHash, combatLayerIndex, ReadyNormalized);
            phase = Phase.Ready;
            Log("READY, frozen on frame " + readyFrame);
        }

        UpdateLock();
        lastAimPoint = GetAimPoint();
    }

    private void TickReady()
    {
        if (!InDrawState(out _)) { Interrupted("ready pose"); return; }

        // Landing on the frozen pose: the slow ends, the motion completes on the ground and fires.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ToFinisher("landed on ready pose"); return; }

        // R released on the frozen pose in the air: clean cancel, no arrow.
        if (!Input.GetKey(drawKey)) { CancelDraw("R released on ready pose"); return; }

        // RMB released: loose. The motion runs and the arrow fires when it finishes.
        if (!Input.GetMouseButton(fireMouseButton)) { BeginCast("released from ready pose"); return; }

        UpdateLock();
        lastAimPoint = GetAimPoint();
    }

    private void TickCasting()
    {
        if (!InDrawState(out AnimatorStateInfo info)) { Interrupted("cast"); return; }

        // Landing or dropping R mid motion: aim ends now, the motion keeps running and still fires.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ToFinisher("landed mid motion"); return; }
        if (!Input.GetKey(drawKey)) { ToFinisher("R released mid motion"); return; }

        lastAimPoint = GetAimPoint();

        // The arrow leaves the tail on its own frame, not at the end of the clip, so the throw and
        // the shot read as one motion. The rest of the clip is the follow through.
        if (!shotFired && info.normalizedTime >= FireNormalized)
        {
            arrowPending = true;
            shotFired = true;
        }

        if (info.normalizedTime >= EndNormalized)
        {
            // Release the layer, stay in the ability, wait for the next RMB press.
            CrossfadeToExit(scaledToWorld: true);
            phase = Phase.Slow;
            Log("back to slow, ready for next shot");
        }
    }

    private void TickFinisher()
    {
        if (!InDrawState(out AnimatorStateInfo info)) { Interrupted("finisher"); return; }

        if (!shotFired && info.normalizedTime >= FireNormalized)
        {
            arrowPending = true;
            shotFired = true;
        }

        if (info.normalizedTime >= EndNormalized)
        {
            CrossfadeToExit(scaledToWorld: false);
            IsShotRunning = false;
            phase = Phase.Inactive;
            Log("finisher done, fully idle");
        }
    }
    #endregion

    #region Ability flow
    /// <summary>Null when entry is legal, otherwise the reason, which is also what the refusal log prints.</summary>
    private string WhyRefused()
    {
        if (playerMovement == null || !playerMovement.IsAirborne()) return "not airborne";
        if (!lastJumpWas2Leg) return "this airtime is not a 2 leg jump";
        if (Time.unscaledTime - lastFireTime < cooldownAfterFire) return "cooldown";

        if (playerCombat != null && (playerCombat.IsAttacking() || playerCombat.IsChargingHeavy()
            || playerCombat.IsDodging() || playerCombat.IsDashing()
            || playerCombat.IsGuarding() || playerCombat.IsInHitReaction()))
            return "combat busy (attack/heavy/dodge/dash/guard/hit)";

        return null;
    }

    private void EnterAbility()
    {
        phase = Phase.Slow;
        IsAiming = true;
        lockedTarget = null;
        lockedCollider = null;
        lastAimPoint = transform.position + transform.forward * aimRayDistance;

        // Slow the game clock AND the physics clock together. PlayerMovement moves in FixedUpdate,
        // so leaving fixedDeltaTime unscaled makes her fall in visible steps instead of smoothly.
        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = cachedFixedDeltaTime * slowFactor;

        // Camera damping on real time for the duration of the slow only, restored on exit.
        // This is the only camera adjacent thing this script touches and it is the approved one.
        if (cinemachineBrain != null)
        {
            cachedBrainIgnoreTimeScale = cinemachineBrain.IgnoreTimeScale;
            cinemachineBrain.IgnoreTimeScale = true;
        }

        Log("ENTER, slow on, waiting for RMB, drawState=" + drawStateName);
    }

    private void StartDraw()
    {
        phase = Phase.Drawing;
        shotFired = false;
        arrowPending = false;
        animator.SetLayerWeight(combatLayerIndex, 1f);
        // The blend duration is passed in scaled time so it costs the same REAL time at any
        // time scale. The clip itself runs through the speed parameter, never through the world clock.
        float blend = drawCrossfade * Mathf.Max(Time.timeScale, 0.01f);
        animator.CrossFadeInFixedTime(drawStateHash, blend, combatLayerIndex, 0f);
        ShowReticle(true);
        Log("DRAW started");
    }

    /// <summary>RMB released: the motion runs from wherever it is to the end and fires there.</summary>
    private void BeginCast(string reason)
    {
        phase = Phase.Casting;
        ShowReticle(false);
        Log("SHOT released (" + reason + ")");
    }

    /// <summary>Clean cancel of a draw or the frozen pose: no arrow, layer released, ability over.</summary>
    private void CancelDraw(string reason)
    {
        RestoreClocksAndBrain();
        IsAiming = false;
        ShowReticle(false);
        CrossfadeToExit(scaledToWorld: false);
        phase = Phase.Inactive;
        Log("CANCEL (" + reason + "), no arrow");
    }

    /// <summary>The aim ends now but the motion must complete and fire. Used for landing and for R released mid motion.</summary>
    private void ToFinisher(string reason)
    {
        RestoreClocksAndBrain();
        IsAiming = false;
        IsShotRunning = true;
        ShowReticle(false);
        phase = Phase.Finisher;
        Log("FINISHER (" + reason + "), motion completes and fires");
    }

    /// <summary>Clean end of the ability from the Slow phase, nothing was drawn.</summary>
    private void ExitAbility(string reason)
    {
        RestoreClocksAndBrain();
        IsAiming = false;
        ShowReticle(false);
        phase = Phase.Inactive;
        Log("EXIT (" + reason + ")");
    }

    /// <summary>The draw state got stomped (a hit reaction is the usual cause). Tear down safely, no arrow.</summary>
    private void Interrupted(string where)
    {
        if (IsAiming) RestoreClocksAndBrain();
        IsAiming = false;
        IsShotRunning = false;
        ShowReticle(false);
        phase = Phase.Inactive;
        Log("INTERRUPTED during " + where + ", no arrow");
    }

    /// <summary>Full safety teardown for disable and form change, restores everything it may have touched.</summary>
    private void HardCancel(string reason)
    {
        if (phase == Phase.Inactive) return;
        if (IsAiming) RestoreClocksAndBrain();
        IsAiming = false;
        IsShotRunning = false;
        ShowReticle(false);
        if (animator != null && animator.isActiveAndEnabled)
            CrossfadeToExit(scaledToWorld: false);
        phase = Phase.Inactive;
        Log("HARD CANCEL (" + reason + ")");
    }

    private void RestoreClocksAndBrain()
    {
        // Restore both clocks to their exact cached values. Never assume 1 and 0.02, so this stays
        // polite if some other system (Flurry Rush) had its own scale running.
        Time.timeScale = cachedTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime;

        if (cinemachineBrain != null)
            cinemachineBrain.IgnoreTimeScale = cachedBrainIgnoreTimeScale;
    }

    private void CrossfadeToExit(bool scaledToWorld)
    {
        // Always leave the combat layer on Combat_Empty, the same idle PlayerCombat returns to,
        // so the layer releases and base locomotion shows through instead of pinning the pose.
        if (animator == null) return;
        animator.SetLayerWeight(combatLayerIndex, 1f);
        float blend = scaledToWorld ? exitCrossfade * Mathf.Max(Time.timeScale, 0.01f) : exitCrossfade;
        animator.CrossFadeInFixedTime(exitStateHash, blend, combatLayerIndex);
    }

    /// <summary>Drives the state speed multiplier every frame. 0 freezes the ready pose. While the
    /// motion runs it is castPace divided by the world time scale, so the clip plays at the same
    /// REAL pace during the slow and on the ground, and never slows down with the world. Neutral 1
    /// when idle so manual previews and debug tools play the clip normally.</summary>
    private void UpdateCastParam()
    {
        if (animator == null) return;
        float value;
        switch (phase)
        {
            case Phase.Ready: value = 0f; break;
            case Phase.Drawing:
            case Phase.Casting:
            case Phase.Finisher:
                value = castPace / Mathf.Max(Time.timeScale, 0.01f);
                break;
            default: value = 1f; break;
        }
        animator.SetFloat(castSpeedParamHash, value);
    }

    /// <summary>True while the combat layer is in (or blending into) the draw state.</summary>
    private bool InDrawState(out AnimatorStateInfo info)
    {
        info = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
        if (info.shortNameHash == drawStateHash) return true;
        if (animator.IsInTransition(combatLayerIndex))
        {
            info = animator.GetNextAnimatorStateInfo(combatLayerIndex);
            if (info.shortNameHash == drawStateHash) return true;
        }
        return false;
    }

    /// <summary>Tracks whether this airtime belongs to a 2 leg jump by reading the base layer.
    /// Seeing the 4 leg jump state clears it, landing clears it. No PlayerMovement changes.</summary>
    private void UpdateJumpLatch()
    {
        if (playerMovement == null || animator == null) return;
        if (!playerMovement.IsAirborne()) { lastJumpWas2Leg = false; return; }

        int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (animator.IsInTransition(0))
            current = animator.GetNextAnimatorStateInfo(0).shortNameHash;

        if (current == twoLegJumpHash) lastJumpWas2Leg = true;
        else if (current == fourLegJumpHash) lastJumpWas2Leg = false;
    }
    #endregion

    #region Arrow
    private void SpawnArrow()
    {
        Vector3 spawn = rightTailTip != null ? rightTailTip.position : transform.position + Vector3.up;
        Vector3 dir = lastAimPoint - spawn;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        lastFireTime = Time.unscaledTime;

        // Runs before the bolt check on purpose, so the markers still appear even if the prefab
        // slot is empty and there is no arrow to look at.
        if (showSpawnMarker) DropSpawnMarkers(spawn);

        if (boltPrefab == null)
        {
            Debug.LogWarning("[TailAirShot] FIRE but no Bolt Prefab is assigned on TailAimController.");
            return;
        }

        GameObject bolt = Instantiate(boltPrefab, spawn, Quaternion.LookRotation(dir));
        TailProjectile proj = bolt.GetComponent<TailProjectile>();
        if (proj != null) proj.Launch(dir);

        Log("FIRE, arrow away, dir=" + dir.ToString("F2"));
    }

    /// <summary>
    /// Diagnostic only, nothing here touches how the game plays. Two balls go up when the arrow is
    /// born: a green one stuck to the transform the arrow is fired from, so it rides her tail and
    /// shows where the game thinks the tip is, and a magenta one left standing in the world at the
    /// exact point the arrow came out of. Comparing the two answers the question on its own. It
    /// also kicks off the drift check below.
    /// </summary>
    private void DropSpawnMarkers(Vector3 spawn)
    {
        if (rightTailTip != null)
        {
            GameObject tipBall = MakeMarkerBall("TailTipMarker", Color.green);
            tipBall.transform.SetParent(rightTailTip, false);
            tipBall.transform.localPosition = Vector3.zero;
            tipBall.transform.localRotation = Quaternion.identity;

            // The tail bones carry the rig's own scale, so the ball has to be divided by it or it
            // comes out either invisible or enormous.
            Vector3 boneScale = rightTailTip.lossyScale;
            float size = Mathf.Max(0.01f, spawnMarkerSize);
            tipBall.transform.localScale = new Vector3(
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.x)),
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.y)),
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.z)));

            StartCoroutine(KillAfterRealSeconds(tipBall, spawnMarkerSeconds));
        }

        GameObject spawnBall = MakeMarkerBall("ArrowSpawnMarker", Color.magenta);
        spawnBall.transform.position = spawn;
        StartCoroutine(KillAfterRealSeconds(spawnBall, spawnMarkerSeconds));

        StartCoroutine(ReportSpawnDriftAtEndOfFrame(spawn));
    }

    /// <summary>A plain unlit looking ball with no collider and no shadows, so it cannot affect anything.</summary>
    private GameObject MakeMarkerBall(string ballName, Color color)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = ballName;

        Collider col = ball.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = ball.GetComponent<Renderer>();
        if (rend != null)
        {
            // Both property names, so the colour lands whichever render pipeline the project is on.
            rend.material.color = color;
            if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", color);
            if (rend.material.HasProperty("_EmissionColor"))
            {
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", color);
            }
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        ball.transform.localScale = Vector3.one * Mathf.Max(0.01f, spawnMarkerSize);
        return ball;
    }

    /// <summary>Real seconds, not game seconds, so the balls do not linger for twenty seconds during the slow.</summary>
    private IEnumerator KillAfterRealSeconds(GameObject go, float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
        if (go != null) Destroy(go);
    }

    /// <summary>
    /// Reads the tail tip again at the very end of the SAME frame the arrow was born on.
    ///
    /// PlayerCombat moves her body in its own LateUpdate (the air pose height pin) and this script
    /// reads the tail in its LateUpdate. Unity does not promise which of the two runs first. If
    /// PlayerCombat ran second, the arrow was born from where her tail was BEFORE her body was put
    /// at the right height, which would put it well off the tip. This log measures exactly that:
    /// the drift number is roughly how far off the arrow is, and a big one names the cause.
    /// </summary>
    private IEnumerator ReportSpawnDriftAtEndOfFrame(Vector3 spawn)
    {
        yield return new WaitForEndOfFrame();
        if (rightTailTip == null) yield break;

        Vector3 settled = rightTailTip.position;
        float drift = Vector3.Distance(settled, spawn);
        Debug.Log("[TailAirShot] spawn drift check: arrow born at " + spawn.ToString("F3")
            + ", tail tip at end of frame " + settled.ToString("F3")
            + ", drift " + drift.ToString("F3") + "m"
            + (drift > 0.05f
                ? "  <<< THE ARROW IS BORN FROM A STALE POSITION, this is the miss"
                : "  (spawn point is current, the tip is not the problem)"));
    }
    #endregion

    #region Targeting
    /// <summary>Pick the enemy nearest the screen centre within the snap radius and cache it plus its collider.</summary>
    private void UpdateLock()
    {
        lockedTarget = null;
        lockedCollider = null;
        if (mainCamera == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, targetingRange, targetBuffer, enemyLayer);
        if (count == 0) return;

        Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float bestPixelDist = snapRadiusPixels;

        for (int i = 0; i < count; i++)
        {
            Collider col = targetBuffer[i];
            if (col == null) continue;

            EnemyHealth eh = col.GetComponentInParent<EnemyHealth>();
            if (eh != null && (eh.IsDead() || eh.IsInvulnerable)) continue;

            Vector3 worldCentre = col.bounds.center;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCentre);
            if (screenPos.z <= 0f) continue; // behind the camera

            float pixelDist = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), screenCentre);
            if (pixelDist > bestPixelDist) continue;

            if (requireLineOfSight && !HasLineOfSight(worldCentre)) continue;

            bestPixelDist = pixelDist;
            lockedTarget = col.transform;
            lockedCollider = col;
        }
    }

    private bool HasLineOfSight(Vector3 targetPoint)
    {
        Vector3 eye = transform.position + Vector3.up * 0.6f;
        if (Physics.Linecast(eye, targetPoint, out RaycastHit hit, environmentMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != transform.root) return false; // an obstacle blocks the line
        }
        return true;
    }

    /// <summary>The world point the arrow flies toward: the locked enemy, or whatever sits at the
    /// screen centre. The ray ORIGIN is pushed forward to Yoru's depth, so ground between the
    /// camera and Yoru can never become the target and send the arrow backward.</summary>
    private Vector3 GetAimPoint()
    {
        if (lockedTarget != null)
            return lockedCollider != null ? lockedCollider.bounds.center : lockedTarget.position;

        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            float toYoru = Vector3.Dot(transform.position - ray.origin, ray.direction);
            Vector3 origin = ray.GetPoint(Mathf.Max(toYoru, 0f));
            if (Physics.Raycast(origin, ray.direction, out RaycastHit hit, aimRayDistance, environmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return origin + ray.direction * aimRayDistance;
        }

        return transform.position + transform.forward * aimRayDistance;
    }

    private void FaceAim()
    {
        if (mainCamera == null) return;
        Vector3 flat = mainCamera.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(flat);
    }
    #endregion

    #region Reticle UI
    private void BuildReticle()
    {
        // Root object, no parent. Parenting the canvas under the player made a ScreenSpaceOverlay
        // canvas inherit the player transform, which pushed the crosshair off screen centre and
        // onto Yoru herself.
        GameObject canvasGo = new GameObject("TailAimReticle");
        reticleCanvas = canvasGo.AddComponent<Canvas>();
        reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        reticleCanvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite dot = reticleSprite != null ? reticleSprite : MakeCircleSprite(64, 0f);
        Sprite ring = lockSprite != null ? lockSprite : MakeCircleSprite(64, 0.72f);

        reticleImage = MakeImage("Crosshair", dot, reticleColor, reticleSize);
        lockImage = MakeImage("LockMarker", ring, lockMarkerColor, lockMarkerSize);

        ShowReticle(false);
    }

    private Image MakeImage(string imageName, Sprite sprite, Color color, float size)
    {
        GameObject go = new GameObject(imageName);
        go.transform.SetParent(reticleCanvas.transform, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private void ShowReticle(bool show)
    {
        if (reticleCanvas != null) reticleCanvas.enabled = show;
        if (!show && lockImage != null) lockImage.enabled = false;
    }

    private void UpdateReticlePositions()
    {
        if (reticleImage != null)
            reticleImage.color = lockedTarget != null ? reticleLockedColor : reticleColor;

        if (lockImage == null) return;

        if (lockedTarget != null && mainCamera != null)
        {
            Vector3 worldCentre = lockedCollider != null ? lockedCollider.bounds.center : lockedTarget.position;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldCentre);
            if (screenPos.z > 0f)
            {
                lockImage.enabled = true;
                lockImage.rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
            }
            else
            {
                lockImage.enabled = false;
            }
        }
        else
        {
            lockImage.enabled = false;
        }
    }

    /// <summary>Generate a simple sprite. inner01 of 0 makes a filled dot, above 0 makes a ring.</summary>
    private Sprite MakeCircleSprite(int size, float inner01)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f;
        float outer = c;
        float innerR = inner01 * outer;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                bool on = d <= outer && d >= innerR;
                tex.SetPixel(x, y, on ? Color.white : clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    #endregion

    #region Helpers
    /// <summary>Locate the right tail tip bone to spawn the arrow from, by exact name first, then by best guess.</summary>
    private void FindRightTailTip()
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);

        // Ordered by how close each one sits to the visible end of the tail. RightTailVFX is the
        // anchor YoruVFXManager already spawns right tail effects from, so the arrow and the tail
        // VFX agree. The plain bone chain ends at Tail6_R_end_end_end; Tail6_R_end_end is one
        // joint short of the tip, which is why a bolt spawned there reads as coming from mid tail.
        string[] preferred =
        {
            rightTailTipBoneName,
            "RightTailVFX",
            "Tail6_R_end_end_end",
            "Tail6_R_end_end"
        };

        foreach (string wanted in preferred)
        {
            if (string.IsNullOrEmpty(wanted)) continue;
            foreach (Transform t in bones)
            {
                if (t.name == wanted) { rightTailTip = t; return; }
            }
        }

        // Last resort: the deepest right tail transform in the hierarchy.
        Transform best = null;
        int bestDepth = -1;
        foreach (Transform t in bones)
        {
            if (!t.name.Contains("Tail") || !t.name.Contains("_R")) continue;

            int depth = 0;
            for (Transform p = t; p != null; p = p.parent) depth++;
            if (depth > bestDepth) { bestDepth = depth; best = t; }
        }

        rightTailTip = best;
        if (rightTailTip == null)
            Debug.LogWarning("[TailAirShot] No right tail tip found. Assign Right Tail Tip in the Inspector.");
    }

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log("[TailAirShot] " + msg
            + " | y=" + transform.position.y.ToString("F2")
            + " ts=" + Time.timeScale.ToString("F2")
            + " fdt=" + Time.fixedDeltaTime.ToString("F4"));
    }

    #endregion
}