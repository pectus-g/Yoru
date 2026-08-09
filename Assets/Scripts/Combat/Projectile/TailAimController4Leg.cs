using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// FOUR LEG air shot. Aug 8.
///
/// This is a SEPARATE, SELF CONTAINED copy of the 2 leg right tail shot. It does not inherit from
/// TailAimController, does not call into it, and does not share a single animator parameter with it.
/// TailAimController.cs is untouched. If this file is deleted the 2 leg ability still works exactly
/// as it does today.
///
/// What is different from the 2 leg version, and only these things:
///  1. The jump gate is inverted. This ability needs a FOUR leg jump (base layer JumpWith4Legs).
///     Seeing JumpWith2Legs clears the permission. The 4 leg jump is the running jump and every
///     double and triple jump, so the whole triple jump airtime is covered.
///  2. The clip is Combat_Combo3_Spin_4legs, played from a state named shoot_4Leg on the combat
///     layer. That clip is 0.80s at 30 samples, so 24 frames. It pauses on FRAME 5.
///  3. Its speed multiplier runs off its OWN float parameter, Shoot4LegSpeed. It must not be
///     TailCastSpeed. Two scripts writing one float every frame would fight and the pause would
///     flicker.
///  4. There is a TIMER on the slow, Max Slow Seconds, counted in REAL seconds from the moment R
///     goes down. A triple jump plus the fall can take a long time and the slow must not outstay
///     its welcome. When it runs out the clock snaps back exactly the way it does on landing, which
///     is the behaviour already approved for the 2 leg version. A shot you already committed to is
///     never lost: the motion finishes at normal pace and still fires.
///
/// Everything else is the locked spec, unchanged:
///  - Hold R while airborne in a 4 leg jump: the whole game clock slows instantly. No animation yet.
///  - Press and HOLD the fire mouse button: shoot_4Leg plays frames 0 to 5 at its real pace and
///    stops frozen on frame 5. Holding the button also turns the camera exactly like normal play,
///    and that camera turn is the aiming. Nothing here talks to ThirdPersonCamera.
///  - RELEASE the button: the motion runs on and the arrow leaves on its own fire frame, from the
///    tail tip you assigned, toward the screen centre. Press again for the next shot. As many shots
///    as fit inside the slow budget.
///  - Landing, releasing R mid motion, or the timer running out mid motion: the slow ends, the
///    motion completes at normal pace and still fires.
///  - Releasing R while frozen on frame 5 in the air: clean cancel, no arrow.
///
/// FIRE MOUSE BUTTON. Default 1, the right mouse button, identical to the 2 leg version. Set it to
/// 0 in the Inspector if you want the LEFT button to draw and shoot instead. No code change is
/// needed for that: PlayerCombat already stands all combat input down while this ability is live,
/// so LMB cannot start the air swirl or the heavy charge while you are holding R.
///
/// HARD DEPENDENCY, both must be in the project or the feature misbehaves:
///  1. This file.
///  2. The gate block near the top of PlayerCombat.HandleInput and the air pose pin in
///     PlayerCombat.UpdateAirPoseHeightPin must BOTH read the two statics below alongside
///     TailAimController's. The pin one matters as much as the input one: without it her body
///     sinks and rises once per shot, which is the bug fixed in aug7.
///
/// Animator setup, one time, in the Animator window:
///  1. Parameters tab: add a FLOAT named Shoot4LegSpeed, default 1.
///  2. Combat layer: add a state named shoot_4Leg with motion Combat_Combo3_Spin_4legs.
///  3. Select that state, tick the Parameter box on the Speed Multiplier and pick Shoot4LegSpeed.
///     Leave the state's own Speed at 1.
///  Do NOT reuse the orphan Combo3_4Leg state. Nothing references it today and it should stay that
///  way, so a later ground combo can claim it without disturbing this.
///
/// Inspector on the player:
///  - Tail Tip: YOU assign it. There is no auto find, on purpose. An empty slot is a red error at
///    Start and no arrow is ever fired, so a wrong bone can never be picked for you.
///  - The anchor MUST be under Cat_All_10_Tails_v4. The bodyYoru skeleton is switched off and is
///    never posed by the animator, so anything parented there sits frozen in its bind pose forever.
///    The Start log prints the full path so one glance settles it.
///
/// Never creates rendering objects. No lights, no materials, no shaders. The aim VFX slot takes an
/// object already sitting in the scene and this script only ticks its checkbox on and off. The only
/// runtime objects it makes at all are the two debug spawn marker balls, which are opt in and are
/// meant to be turned off once the tip is proven.
/// </summary>
public class TailAimController4Leg : MonoBehaviour
{
    #region Inspector
    [Header("Input")]
    [Tooltip("Hold this while airborne in a 4 leg jump to slow time. R by default, the same key as the 2 leg shot. The two abilities can never both be live because they gate on opposite jump types.")]
    [SerializeField] private KeyCode drawKey = KeyCode.R;
    [Tooltip("Mouse button that draws and shoots. 1 is the right mouse button, the same as the 2 leg shot. Set it to 0 for the left button. Hold to draw and aim, release to shoot.")]
    [SerializeField] private int fireMouseButton = 1;

    [Header("Slow Motion")]
    [Tooltip("Game time scale while the ability is active. 0.1 is a 10x slow. The physics clock is scaled with it so the slow stays smooth.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowFactor = 0.1f;
    [Tooltip("How long the slow may last, in REAL seconds, counted from the moment you press R. A triple jump plus the fall can take a long time and the slow must not outstay its welcome. When this runs out the clock snaps straight back, exactly the way it does when you land. Any shot already released still finishes and still fires. Set to 0 to switch the timer off entirely.")]
    [SerializeField] private float maxSlowSeconds = 3f;

    [Header("Animation")]
    [Tooltip("Animator state of the cast clip on the combat layer. Must be shoot_4Leg, playing Combat_Combo3_Spin_4legs.")]
    [SerializeField] private string drawStateName = "shoot_4Leg";
    [Tooltip("Combat layer index. PlayerCombat uses 1.")]
    [SerializeField] private int combatLayerIndex = 1;
    [Tooltip("Blend time into the draw, in real seconds. Kept tiny so the draw feels crisp.")]
    [SerializeField] private float drawCrossfade = 0.08f;
    [Tooltip("Combat layer state to crossfade back to on every exit. Combat_Empty releases the layer so base locomotion shows through.")]
    [SerializeField] private string exitStateName = "Combat_Empty";
    [Tooltip("Blend time out of the cast on exit, in real seconds.")]
    [SerializeField] private float exitCrossfade = 0.12f;
    [Tooltip("Float parameter on the animator wired as the shoot_4Leg state's speed multiplier. 0 freezes the pose, this script drives it. It must NOT be TailCastSpeed, that one belongs to the 2 leg shot.")]
    [SerializeField] private string castSpeedParamName = "Shoot4LegSpeed";
    [Tooltip("YOUR pace knob. How fast the cast plays in real time, no matter how slow the world is. 1 = the clip's authored pace, 2 = twice as fast. This clip is short, 0.80s, so try below 1 if the draw feels rushed.")]
    [SerializeField] private float castPace = 1f;
    [Tooltip("The frame the draw freezes on as the ready pose. 5, read off the Preview window.")]
    [SerializeField] private int readyFrame = 5;
    [Tooltip("The frame the arrow actually leaves the tail. 10 is a starting point, taken from the same proportion the 2 leg shot uses. Scrub the clip and nudge it if the arrow reads early or late. The motion keeps playing to the end after it, so the whip follows through.")]
    [SerializeField] private int fireFrame = 10;
    [Tooltip("Total frames of the cast clip. Combat_Combo3_Spin_4legs is 0.80s at 30 samples = 24. Confirm it by dragging the Preview slider to the end and reading the frame number.")]
    [SerializeField] private int clipFrameCount = 24;
    [Tooltip("Turn Yoru to face the aim direction while drawing and holding the ready pose. This clip is a SPIN, so the pose is already turning on its own and stacking a transform turn on top of it may read oddly. It cannot move the shot, the arrow direction comes from the camera. Untick it and compare.")]
    [SerializeField] private bool faceAimWhileDrawing = true;
    [Tooltip("How fast she turns to follow the camera while you are aiming, in degrees per REAL second. Real seconds on purpose: during the slow the world runs at a tenth speed but your hand on the mouse does not.")]
    [SerializeField] private float aimTurnSpeed = 720f;

    [Header("Jump Gate")]
    [Tooltip("Base layer state of the 4 leg jump. Only this jump may use the ability. It is the running jump and every double and triple jump, so the whole triple jump airtime is covered.")]
    [SerializeField] private string fourLegJumpStateName = "JumpWith4Legs";
    [Tooltip("Base layer state of the 2 leg jump. Seeing it clears the permission for this airtime, so the 2 leg ability owns that jump on its own.")]
    [SerializeField] private string twoLegJumpStateName = "JumpWith2Legs";

    [Header("Bolt")]
    [Tooltip("Prefab with a TailProjectile component. Spawned from the tail tip on the fire frame.")]
    [SerializeField] private GameObject boltPrefab;
    [Tooltip("Where the arrow is born. YOU set this. The script never goes looking on its own. Drag in an empty child of the tail bone you want, moved out by hand to the fluffy end. It MUST live under Cat_All_10_Tails_v4, never under bodyYoru, which is switched off and frozen in its bind pose. Left empty, the Console says so at Start and no arrow is ever fired.")]
    [SerializeField] private Transform tailTip;
    [Tooltip("How far ahead the straight aim point sits when no enemy is locked.")]
    [SerializeField] private float aimRayDistance = 60f;

    [Header("Aim VFX")]
    [Tooltip("Your own effect, already sitting in the scene, ideally a child of the same tail tip object above. Leave it unticked in the Hierarchy. This script only ticks it on while she is aiming and unticks it the moment she looses the shot. It is never copied, never moved, never rebuilt, and nothing about lighting, materials or shaders is touched.")]
    [SerializeField] private GameObject aimVFX;

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
    [Tooltip("Drops two coloured balls the moment the arrow is born so you can SEE where it came from. GREEN sticks to the spot the arrow is fired from and rides the tail. MAGENTA stays put in the world at the exact place the arrow was born. Green on the tail with magenta on top of it means the tip is correct. Turn this off once you have looked.")]
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
    /// <summary>True while the 4 leg slow motion ability is active. PlayerCombat's input gate and its air pose pin both read this alongside the 2 leg flags.</summary>
    public static bool IsAiming { get; private set; }

    /// <summary>True while a released 4 leg shot motion is still running after the ability itself ended (landed, R released mid motion, or the slow timer ran out). PlayerCombat reads this too, so nothing stomps the combat layer before the arrow is out.</summary>
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
    private bool lastJumpWas4Leg;

    // Real time stamp of the R press. The slow budget is measured against this, never against
    // Time.time, which is crawling at a tenth speed for the whole of the ability.
    private float abilityStartRealTime;

    // One budget per airtime, and it stays spent until she touches the ground.
    //
    // Without this the timer does nothing at all. Every expiry path ends with the ability inactive
    // while she is still in the air and still holding R, so the very next frame the entry check
    // passes again, a fresh budget is stamped, and the slow comes straight back. It would loop for
    // the whole fall. The 2 leg version never needed a latch like this because its only two exits
    // are landing and letting go of R, and both of those fail the entry check on their own.
    private bool slowBudgetSpentThisAirtime;

    // Clock and camera brain state cached on entry and restored exactly on every exit path.
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private bool cachedBrainIgnoreTimeScale;

    private int drawStateHash;
    private int exitStateHash;
    private int fourLegJumpHash;
    private int twoLegJumpHash;
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
    // last frame's pose, and during a fast whip the tip travels a long way in one frame.
    private bool arrowPending;

    private float ReadyNormalized => clipFrameCount > 0 ? (float)readyFrame / clipFrameCount : 0.208f;
    private float FireNormalized => clipFrameCount > 0 ? (float)fireFrame / clipFrameCount : 0.417f;
    private const float EndNormalized = 0.99f;

    /// <summary>True once the real time slow budget is spent. A budget of 0 switches the timer off.</summary>
    private bool SlowBudgetSpent => maxSlowSeconds > 0.001f
        && Time.unscaledTime - abilityStartRealTime >= maxSlowSeconds;
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
        fourLegJumpHash = Animator.StringToHash(fourLegJumpStateName);
        twoLegJumpHash = Animator.StringToHash(twoLegJumpStateName);
        castSpeedParamHash = Animator.StringToHash(castSpeedParamName);

        BuildReticle();
    }

    private void Start()
    {
        // Loud setup checks so a missed Animator click is visible in the log instead of silently
        // misbehaving. Every one of these has cost a session before.
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
            Debug.LogWarning("[TailAirShot4Leg] Animator float parameter '" + castSpeedParamName
                + "' is MISSING. Add it in the Animator Parameters tab and tick the Speed Multiplier"
                + " Parameter box on the " + drawStateName + " state, or the draw cannot pause on frame "
                + readyFrame + ".");

        // Sharing the 2 leg parameter would make the two scripts fight over one float every frame.
        if (castSpeedParamName == "TailCastSpeed")
            Debug.LogError("[TailAirShot4Leg] Cast Speed Param Name is set to TailCastSpeed, which"
                + " belongs to the 2 leg shot. Both scripts would write it every frame and the frame "
                + readyFrame + " pause would flicker. Set it back to Shoot4LegSpeed.");

        // The tip is yours, so an empty slot is an error and not something to paper over.
        if (tailTip == null)
            Debug.LogError("[TailAirShot4Leg] Tail Tip is EMPTY on TailAimController4Leg. Nothing will"
                + " be fired. Drag the tail tip object into that slot in the Inspector.");
        else if (IsUnderDeadSkeleton(tailTip))
            Debug.LogError("[TailAirShot4Leg] Tail Tip is parented under bodyYoru, which is SWITCHED"
                + " OFF and is never posed by the animator. It will sit frozen in its bind pose and"
                + " every arrow will be born in the same wrong place. Move the anchor under"
                + " Cat_All_10_Tails_v4.\n  current path: " + FullPath(tailTip));

        if (debugLogs)
            Debug.Log("[TailAirShot4Leg] Ready. drawState=" + drawStateName
                + " castParam=" + castSpeedParamName + (paramFound ? " (found)" : " (MISSING)")
                + " readyFrame=" + readyFrame + " fireFrame=" + fireFrame + " frames=" + clipFrameCount
                + " slowBudget=" + (maxSlowSeconds > 0.001f ? maxSlowSeconds + "s real" : "off")
                + "\n  arrow leaves: " + FullPath(tailTip)
                + "\n  aim VFX: " + (aimVFX != null ? FullPath(aimVFX.transform) : "none assigned"));
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
        // Driven off the phase in one place rather than sprinkled through every exit, so there is no
        // way out of the ability that can leave your effect stuck on.
        UpdateAimVFX();

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

        // Slow budget spent with nothing playing: same clean exit as landing.
        if (SlowBudgetSpent) { slowBudgetSpentThisAirtime = true; ExitAbility("slow timer ran out, nothing drawn"); return; }

        // R released with nothing playing: clean exit.
        if (!Input.GetKey(drawKey)) { ExitAbility("R released, nothing drawn"); return; }

        // Fire button pressed: start the draw. The camera keeps doing its own vanilla work in parallel.
        if (Input.GetMouseButtonDown(fireMouseButton)) StartDraw();
    }

    private void TickDrawing()
    {
        // Landing while drawing: the slow ends, the motion completes on the ground and fires.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ToFinisher("landed during draw"); return; }

        // Slow budget spent while drawing: treated exactly like landing. The shot is already
        // committed, so it finishes at normal pace and still fires. You never lose a draw to the clock.
        if (SlowBudgetSpent) { slowBudgetSpentThisAirtime = true; ToFinisher("slow timer ran out during draw"); return; }

        // R released while drawing, still airborne: clean cancel, no arrow.
        if (!Input.GetKey(drawKey)) { CancelDraw("R released during draw"); return; }

        // Button released before the ready frame: a quick shot. Skip the pause and run to the end.
        if (!Input.GetMouseButton(fireMouseButton)) { BeginCast("released early, quick shot"); return; }

        // Reached the ready frame: pin it exactly once and freeze.
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

        // Slow budget spent on the frozen pose: same as landing, the motion completes and fires.
        if (SlowBudgetSpent) { slowBudgetSpentThisAirtime = true; ToFinisher("slow timer ran out on ready pose"); return; }

        // R released on the frozen pose in the air: clean cancel, no arrow.
        if (!Input.GetKey(drawKey)) { CancelDraw("R released on ready pose"); return; }

        // Button released: loose. The motion runs and the arrow leaves on the fire frame.
        if (!Input.GetMouseButton(fireMouseButton)) { BeginCast("released from ready pose"); return; }

        UpdateLock();
        lastAimPoint = GetAimPoint();
    }

    private void TickCasting()
    {
        if (!InDrawState(out AnimatorStateInfo info)) { Interrupted("cast"); return; }

        // Landing, dropping R, or the clock running out mid motion: aim ends now, the motion keeps
        // running and still fires.
        if (playerMovement != null && !playerMovement.IsAirborne()) { ToFinisher("landed mid motion"); return; }
        if (SlowBudgetSpent) { slowBudgetSpentThisAirtime = true; ToFinisher("slow timer ran out mid motion"); return; }
        if (!Input.GetKey(drawKey)) { ToFinisher("R released mid motion"); return; }

        lastAimPoint = GetAimPoint();

        // The arrow leaves the tail on its own frame, not at the end of the clip, so the whip and
        // the shot read as one motion. The rest of the clip is the follow through.
        if (!shotFired && info.normalizedTime >= FireNormalized)
        {
            arrowPending = true;
            shotFired = true;
        }

        if (info.normalizedTime >= EndNormalized)
        {
            // Release the layer, stay in the ability, wait for the next press.
            CrossfadeToExit(scaledToWorld: true);
            phase = Phase.Slow;
            Log("back to slow, ready for next shot, "
                + Mathf.Max(0f, maxSlowSeconds - (Time.unscaledTime - abilityStartRealTime)).ToString("F2")
                + "s of slow left");
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
        if (!lastJumpWas4Leg) return "this airtime is not a 4 leg jump";
        if (slowBudgetSpentThisAirtime) return "the slow budget for this jump is spent, land to get it back";
        if (Time.unscaledTime - lastFireTime < cooldownAfterFire) return "cooldown";

        // Belt and braces. The two abilities gate on opposite jump types so they cannot overlap,
        // but if one ever did leak through, two scripts owning Time.timeScale would be very hard
        // to unpick from a log.
        if (TailAimController.IsAiming || TailAimController.IsShotRunning)
            return "the 2 leg tail shot is already running";

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

        // Real time, not game time. Game time is about to start crawling at a tenth speed and the
        // budget is meant to be measured in seconds you actually sit through.
        abilityStartRealTime = Time.unscaledTime;

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

        Log("ENTER 4 leg, slow on for up to "
            + (maxSlowSeconds > 0.001f ? maxSlowSeconds + "s real" : "no limit")
            + ", waiting for the fire button, drawState=" + drawStateName);
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

    /// <summary>Fire button released: the motion runs from wherever it is and fires on the fire frame.</summary>
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

    /// <summary>The aim ends now but the motion must complete and fire. Used for landing, for R released mid motion, and for the slow timer running out.</summary>
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

    /// <summary>Tracks whether this airtime belongs to a 4 leg jump by reading the base layer.
    /// Seeing the 2 leg jump state clears it, landing clears it. PlayerMovement is not touched.
    /// The running jump and every double and triple jump all crossfade to JumpWith4Legs, so the
    /// permission survives the whole triple jump.</summary>
    private void UpdateJumpLatch()
    {
        if (playerMovement == null || animator == null) return;
        // Landing is also what gives the slow budget back. One budget per jump, spent until her
        // feet touch the ground, so letting go of R and pressing it again cannot buy more slow.
        if (!playerMovement.IsAirborne())
        {
            lastJumpWas4Leg = false;
            slowBudgetSpentThisAirtime = false;
            return;
        }

        int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (animator.IsInTransition(0))
            current = animator.GetNextAnimatorStateInfo(0).shortNameHash;

        if (current == fourLegJumpHash) lastJumpWas4Leg = true;
        else if (current == twoLegJumpHash) lastJumpWas4Leg = false;
    }
    #endregion

    #region Arrow
    private void SpawnArrow()
    {
        if (tailTip == null)
        {
            Debug.LogError("[TailAirShot4Leg] FIRE but Tail Tip is EMPTY. No arrow. The script will"
                + " not guess a bone for you. Assign it in the Inspector.");
            return;
        }

        Vector3 spawn = tailTip.position;
        Vector3 dir = lastAimPoint - spawn;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        lastFireTime = Time.unscaledTime;

        // Runs before the bolt check on purpose, so the markers still appear even if the prefab
        // slot is empty and there is no arrow to look at.
        if (showSpawnMarker) DropSpawnMarkers(spawn);

        if (boltPrefab == null)
        {
            Debug.LogWarning("[TailAirShot4Leg] FIRE but no Bolt Prefab is assigned on TailAimController4Leg.");
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
        if (tailTip != null)
        {
            GameObject tipBall = MakeMarkerBall("TailTipMarker4Leg", Color.green);
            tipBall.transform.SetParent(tailTip, false);
            tipBall.transform.localPosition = Vector3.zero;
            tipBall.transform.localRotation = Quaternion.identity;

            // The tail bones carry the rig's own scale, so the ball has to be divided by it or it
            // comes out either invisible or enormous.
            Vector3 boneScale = tailTip.lossyScale;
            float size = Mathf.Max(0.01f, spawnMarkerSize);
            tipBall.transform.localScale = new Vector3(
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.x)),
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.y)),
                size / Mathf.Max(0.0001f, Mathf.Abs(boneScale.z)));

            StartCoroutine(KillAfterRealSeconds(tipBall, spawnMarkerSeconds));
        }

        GameObject spawnBall = MakeMarkerBall("ArrowSpawnMarker4Leg", Color.magenta);
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
        if (tailTip == null) yield break;

        Vector3 settled = tailTip.position;
        float drift = Vector3.Distance(settled, spawn);
        Debug.Log("[TailAirShot4Leg] spawn drift check: arrow born at " + spawn.ToString("F3")
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

    /// <summary>
    /// Turns her body to follow the camera while aiming.
    ///
    /// This only changes which way her BODY points. Where the arrow goes is worked out from the
    /// camera in GetAimPoint, never from her rotation, so nothing here can move your shot.
    ///
    /// The speed is measured in real seconds on purpose. During the slow the world runs at a tenth
    /// speed but your hand on the mouse does not, and the camera is already running on real time,
    /// so measuring her turn in game time would make her crawl ten times behind your hand.
    /// </summary>
    private void FaceAim()
    {
        if (mainCamera == null) return;
        Vector3 flat = mainCamera.transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion wanted = Quaternion.LookRotation(flat);

        if (aimTurnSpeed <= 0f)
        {
            transform.rotation = wanted;
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, wanted, aimTurnSpeed * Time.unscaledDeltaTime);
    }
    #endregion

    #region Reticle UI
    private void BuildReticle()
    {
        // Root object, no parent. Parenting the canvas under the player made a ScreenSpaceOverlay
        // canvas inherit the player transform, which pushed the crosshair off screen centre and
        // onto Yoru herself. Named separately from the 2 leg one so the Hierarchy stays readable.
        GameObject canvasGo = new GameObject("TailAimReticle4Leg");
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
    /// <summary>
    /// Ticks your aim effect on while she is aiming and off the instant she looses the shot.
    ///
    /// Aiming is the fire button held, which is the Drawing and Ready phases. Reading the phase
    /// every frame instead of switching it at each exit means no cancel, landing, interruption,
    /// timer expiry or form change can strand it switched on. The object itself is never copied,
    /// moved or rebuilt, only its checkbox is touched.
    /// </summary>
    private void UpdateAimVFX()
    {
        if (aimVFX == null) return;

        bool aiming = phase == Phase.Drawing || phase == Phase.Ready;
        if (aimVFX.activeSelf != aiming) aimVFX.SetActive(aiming);
    }

    /// <summary>
    /// The player carries two complete skeletons with identical bone names. bodyYoru is switched off
    /// and is never posed by the animator, so a bone under it sits frozen in its bind pose forever
    /// and every arrow is born in the same wrong place. This walks up the parents and names it at
    /// Start rather than letting it become a week of guessing.
    /// </summary>
    private static bool IsUnderDeadSkeleton(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.name == "bodyYoru") return true;
        return false;
    }

    /// <summary>Full hierarchy path, so the Console names exactly which object and which skeleton.</summary>
    private static string FullPath(Transform t)
    {
        if (t == null) return "NOT ASSIGNED";
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log("[TailAirShot4Leg] " + msg
            + " | y=" + transform.position.y.ToString("F2")
            + " ts=" + Time.timeScale.ToString("F2")
            + " fdt=" + Time.fixedDeltaTime.ToString("F4"));
    }
    #endregion
}
