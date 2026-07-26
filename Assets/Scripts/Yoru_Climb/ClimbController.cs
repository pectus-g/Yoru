using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// YORU climbing system, Zelda style rebuild. Breath of the Wild style auto grab, blended
/// directional movement on the wall, and a wall run addition. Cat (Yoru) form only.
///
/// REBUILD R1. This replaces the discrete state version. The climb clips are authored FOR
/// the wall (upright, paws reaching forward, all four paws coplanar), so the code never
/// rotates or re-poses the body per bone. The code's only jobs are: place Yoru on the wall,
/// orient the whole character root to the wall surface, move him, and feed the blend tree.
///
/// DESIGN
///   - Grab is contextual, no dedicated key:
///       * On the ground, Yoru attaches only when he MOVES INTO a climbable surface
///         (pressing toward the wall), so walking along or away from a cliff base never grabs.
///       * In the air, Yoru attaches on any contact with a climbable surface (no angle gate).
///         This is the reliable fall save, a fall never slips past a climbable wall.
///   - No falling while on the wall. With no stamina yet, Yoru leaves only by letting go (C),
///     or by mantling at the top. Stamina drops in later at the single ClimbAllowed / speed gate.
///   - ANIMATION IS ONE BLEND TREE, not eight switched states. The input becomes two floats,
///     ClimbX (sideways, plus or minus 2 when Shift wall running) and ClimbY (up and down),
///     smoothed toward the held keys. The ClimbMove blend tree mixes Idle, Up, Down, Sideways
///     and WallRun continuously from those floats. There is no side state that can fail to
///     fire, no minimum state time, no idle flicker, and no visible cut between moves,
///     everything blends. Run Tools, YORU, Build Climb Blend Tree once to build the layer.
///   - ORIENTATION IS ROOT ONLY. The character root turns so Yoru faces into the wall, and
///     on a leaning face the root tilts with the surface, so the coplanar paws stay on the
///     rock. The surface normal used for orientation is sampled at two heights and smoothed,
///     and the turn rate is capped, so an inward corner turns Yoru around it smoothly instead
///     of the old paper flip. The CharacterController capsule never rotates (Unity keeps it
///     upright), so collision is untouched. After the climb the root eases back upright.
///   - Low poly rock is forgiving: a miss frame or one briefly too flat polygon never freezes
///     the animation or ends the climb. Movement continues on the last good wall, an angled
///     corner probe follows curved faces while strafing, and only a persistent loss
///     (Wall Loss Forgive Time) exits.
///   - Mantle is guarded: it needs a minimum climb time first, the ledge ground must be flat
///     enough to stand on, and the head probe is a small sphere so a polygon crease on the
///     face never reads as the top. The landing spot is pushed far enough onto the ledge that
///     the whole capsule clears the lip, its ground height is re probed there, and the mantle
///     is REFUSED outright (Yoru keeps climbing) when the landing, the top of the rise, or
///     the rise itself is blocked by anything solid on ANY layer.
///   - BODY DEPTH (visual only). The clips sit on the wall as authored, so Body Outward
///     Offset defaults to 0 and nothing is shifted. It stays as the single tuning value: if
///     the paws ever sink slightly, raise it, if they hover, go slightly negative. It moves
///     the model containers along the surface normal only, there is no rotation in it.
///   - This script NEVER touches PlayerMovement. On grab it disables PlayerMovement and
///     PlayerCombat, runs its OWN controller.Move, then re-enables both on exit. Disabled
///     MonoBehaviours still expose their public methods, so other scripts are unaffected.
///
/// CONTROLS (on the wall)
///   W / S        climb up / down            (wall relative)
///   A / D        climb sideways             (blended, both directions)
///   Shift + A/D  wall run                   (same sideways movement, faster)
///   Shift + W/S  faster climb               (speed only, no separate clip)
///   Space        climb hop up               (BOTW fast climb, stays on the wall)
///   C            let go                     (detach and drop, never a trap)
///   top reached  auto mantle                (ClimbMantle, then control returns)
///
/// SETUP
///   1. This component sits on the player root (same GameObject as PlayerMovement, the
///      CharacterController, PlayerCombat, FormController and the Animator).
///   2. Run Tools, YORU, Build Climb Blend Tree once. It rebuilds the ClimbLayer with the
///      ClimbMove blend tree, the ClimbMantle state and the ClimbX / ClimbY parameters.
///   3. Climbable Mask stays on the Climbable layer.
///   4. Body Visual is an optional override. Left empty, every model container under the
///      player root is found automatically by its skinned meshes.
///
/// FX
///   Fires five climb moments through ClimbFX: Grab, Hop, LetGo, MantleStart, MantleLand.
///   Per step hand and foot effects stay as animation events on the clips (see ClimbFX).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClimbController : MonoBehaviour
{
    #region Inspector References

    [Header("References (auto found if left empty)")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private FormController formController;
    [Tooltip("Per animation climb VFX and SFX library. Auto found if left empty.")]
    [SerializeField] private ClimbFX climbFX;
    [Tooltip("Used only to read the ground grab direction (camera relative WASD). Defaults to Camera.main.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Optional override for the body depth target. Leave empty to auto find every model container under the player root by its skinned meshes. VISUAL ONLY, the capsule is never moved.")]
    [SerializeField] private Transform bodyVisual;

    #endregion

    #region Animator

    [Header("Animator")]
    [Tooltip("Name of the dedicated climb layer. The index is resolved from this in Awake.")]
    [SerializeField] private string climbLayerName = "ClimbLayer";
    [Tooltip("Fallback index used only if the name above is not found. Base, Combat, Cinematic, Climb means 3.")]
    [SerializeField] private int climbLayerIndex = 3;
    [Tooltip("Name of the blend tree state on the climb layer. Built by Tools, YORU, Build Climb Blend Tree.")]
    [SerializeField] private string climbMoveState = "ClimbMove";
    [Tooltip("Name of the mantle state on the climb layer.")]
    [SerializeField] private string climbMantleState = "ClimbMantle";
    [Tooltip("Float parameter for sideways blend. Minus 2 to 2, where 1 is sideways walk and 2 is wall run.")]
    [SerializeField] private string climbXParam = "ClimbX";
    [Tooltip("Float parameter for vertical blend. Minus 1 to 1.")]
    [SerializeField] private string climbYParam = "ClimbY";
    [Tooltip("CrossFade seconds into ClimbMove on grab and into ClimbMantle at the top.")]
    [SerializeField] private float climbBlend = 0.15f;
    [Tooltip("Seconds to fade the climb layer in on grab and out on release, instead of switching it on and off instantly.")]
    [SerializeField] private float layerFadeTime = 0.2f;
    [Tooltip("How fast the blend values chase the held keys, in units per second. Higher reacts faster, lower blends softer. At 6, a full stop to wall run swing takes about a third of a second.")]
    [SerializeField] private float inputResponse = 6f;

    #endregion

    #region Detection Settings

    [Header("Surface Detection")]
    [Tooltip("Which layer(s) count as climbable. Set to your Climbable layer.")]
    [SerializeField] private LayerMask climbableMask;
    [Tooltip("How far forward to look for a wall.")]
    [SerializeField] private float wallCheckDistance = 0.6f;
    [Tooltip("Radius of the forward sphere cast. Helps with corners and uneven faces.")]
    [SerializeField] private float wallCheckRadius = 0.35f;
    [Tooltip("Height above the feet to cast from. Keeps a tiny step from counting as a wall.")]
    [SerializeField] private float wallCheckHeight = 1.0f;
    [Tooltip("ENTERING a climb: surface counts as a wall when its normal Y is at or below this. Lower means only steeper faces climb. While already climbing, Sustain Max Normal Y applies instead.")]
    [SerializeField] private float maxClimbableNormalY = 0.4f;
    [Tooltip("Reject ceilings: surface ignored when its normal Y is below this.")]
    [SerializeField] private float minClimbableNormalY = -0.3f;
    [Tooltip("WHILE climbing, surfaces stay climbable up to this normal Y before the forgive timer starts. Higher than Max Climbable Normal Y so one flat-ish low poly polygon crossed while strafing does not end the climb.")]
    [SerializeField] private float sustainMaxNormalY = 0.55f;
    [Tooltip("When the wall cast misses while strafing, retry once angled this many degrees toward the strafe direction, so Yoru follows curved faces around corners instead of losing the wall.")]
    [SerializeField] private float cornerProbeAngle = 35f;
    [Tooltip("On the ground, how aligned movement must be with 'into the wall' to grab, in degrees.")]
    [SerializeField] private float groundGrabAngle = 50f;

    #endregion

    #region Movement Settings

    [Header("Climb Movement")]
    [SerializeField] private float climbUpSpeed = 2.2f;
    [SerializeField] private float climbDownSpeed = 2.2f;
    [SerializeField] private float climbSidewaySpeed = 1.6f;
    [Tooltip("Sideways speed while Shift is held (wall run).")]
    [SerializeField] private float wallRunSpeed = 4.5f;
    [Tooltip("Up and down speed multiplier while Shift is held.")]
    [SerializeField] private float sprintClimbMultiplier = 1.7f;
    [Tooltip("Gap kept between the SURFACE of Yoru's capsule and the wall. The real hold distance is controller radius + skin width + this, so the capsule can never sit inside the rock.")]
    [SerializeField] private float wallSurfaceGap = 0.05f;
    [Tooltip("How quickly Yoru corrects to the stick distance.")]
    [SerializeField] private float surfaceStickSpeed = 8f;
    [SerializeField] private float inputDeadzone = 0.1f;

    [Header("Orientation On The Wall (root only, never per bone)")]
    [Tooltip("Maximum turn rate of the character root, in degrees per second. Caps how fast Yoru rotates around an inward corner, which is what removes the paper flip. 540 turns a right angle corner in about a sixth of a second.")]
    [SerializeField] private float wallTurnDegreesPerSecond = 540f;
    [Tooltip("Seconds of smoothing on the surface normal used for orientation. Soaks up low poly polygon edges so the root never pops when a facet changes under the body.")]
    [SerializeField] private float surfaceNormalSmoothTime = 0.15f;
    [Tooltip("Seconds to ease the root back upright after leaving the wall, needed when a leaning face tilted it. PlayerMovement's own facing turn takes over as soon as Yoru moves.")]
    [SerializeField] private float uprightRecoverTime = 0.25f;

    [Header("Body Depth (visual only, capsule untouched)")]
    [Tooltip("Depth of the visible cat on the wall, in meters. The clips are wall authored, so this stays 0. If the paws ever sink into the rock, raise it slightly. If they hover, go slightly negative. Moves the body along the surface normal only, no rotation.")]
    [SerializeField] private float bodyOutwardOffset = 0f;
    [Tooltip("Seconds for the body depth shift to ease in on grab and back out on release.")]
    [SerializeField] private float bodyAlignTime = 0.15f;

    [Header("Climb Hop (Space)")]
    [SerializeField] private float climbHopSpeed = 4.5f;
    [SerializeField] private float climbHopDuration = 0.25f;

    [Header("Let Go (C)")]
    [Tooltip("Horizontal push away from the wall when letting go. ApplyExternalPull is horizontal only.")]
    [SerializeField] private float letGoPushForce = 3.5f;
    [SerializeField] private float letGoPushDuration = 0.18f;

    [Header("Mantle (top of wall)")]
    [SerializeField] private float mantleDuration = 0.6f;
    [Tooltip("Forward inset onto the ledge so Yoru does not finish on the very edge.")]
    [SerializeField] private float mantleForwardInset = 0.3f;
    [Tooltip("Height above the feet to probe for the top of the wall.")]
    [SerializeField] private float topProbeHeight = 1.9f;
    [SerializeField] private float ledgeForwardProbe = 0.6f;
    [SerializeField] private float ledgeDownProbe = 1.5f;
    [Tooltip("The ledge ground must be at least this flat (normal Y) to mantle onto. Stops a bump on the cliff face from reading as the top and mantling Yoru into the rock.")]
    [SerializeField] private float mantleMinGroundNormalY = 0.6f;
    [Tooltip("A climb must last at least this long before a mantle can trigger, so a fresh grab at the base of a wall can never instantly convert into a mantle.")]
    [SerializeField] private float minClimbTimeBeforeMantle = 0.3f;

    [Header("Re-grab")]
    [Tooltip("After letting go, jumping or mantling, ignore the air grab for this long so Yoru does not instantly re-stick.")]
    [SerializeField] private float regrabSuppressTime = 0.35f;
    [Tooltip("On low poly rock the wall cast can hit a briefly too flat polygon or miss for a frame. The climb keeps the last good wall for this long before letting go, instead of dropping Yoru onto a mid face polygon.")]
    [SerializeField] private float wallLossForgiveTime = 0.25f;

    #endregion

    #region Debug

    [Header("Debug")]
    [Tooltip("Logs grab checks, blend values and setup to the Console. Turn off once climbing works.")]
    [SerializeField] private bool debugLogs = true;

    #endregion

    #region Runtime State

    private const string BuildVersion = "Rebuild R2";

    private CharacterController controller;
    private bool wallWasFound;
    private bool isClimbing;
    private bool isMantling;
    private Vector3 wallNormal;
    private Vector3 wallPoint;
    private float hopTimer;
    private float regrabTimer;
    private float wallLossTimer;
    private float climbTime;
    private float layerWeightCurrent;
    private Coroutine mantleRoutine;

    // Blend tree drive. Current smoothed values fed to the animator every climb frame.
    private float climbX;
    private float climbY;
    private float blendLogTimer;

    // Orientation. The smoothed surface normal the root aligns to. Separate from wallNormal
    // (the raw cast result that drives movement and stick), so orientation stays calm across
    // polygon edges while movement stays responsive.
    private Vector3 orientNormal = Vector3.forward;
    private bool recoveringUpright;

    // Body depth (visual only). bodyRoots holds every model container found under the player
    // root. posedRoots holds the containers actually captured this climb (only the ones
    // active at grab), with their rest poses for the exact restore.
    private readonly List<Transform> bodyRoots = new List<Transform>();
    private readonly List<Transform> posedRoots = new List<Transform>();
    private readonly List<Vector3> posedBasePos = new List<Vector3>();
    private readonly List<Quaternion> posedBaseRot = new List<Quaternion>();
    private bool bodyPoseActive;
    private float bodyBlend;

    // Scratch buffer for the mantle fit checks, reused so those checks never allocate.
    private readonly Collider[] mantleOverlapBuffer = new Collider[8];

    // Geometry of the two surface samples under the body, from the root along the wall's
    // up direction. These describe the cat's proportions, not feel, so they are constants.
    private const float BodySampleLow = 0.3f;
    private const float BodySampleHigh = 1.1f;
    private const float BodyCastStartOut = 0.8f;
    private const float BodyCastLength = 1.8f;
    // Safety clamp on the visual push along the surface normal, in meters.
    private const float MaxBodyShift = 0.8f;
    // Lift above the probed ground for the mantle landing, keeps the capsule bottom from
    // starting the stand intersected with the ledge.
    private const float MantleLandLift = 0.05f;

    #endregion

    #region Cached Hashes

    private int moveHash;
    private int mantleHash;
    private int xHash;
    private int yHash;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
        if (formController == null) formController = GetComponent<FormController>();
        if (climbFX == null) climbFX = GetComponent<ClimbFX>();
        if (climbFX == null) climbFX = GetComponentInChildren<ClimbFX>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        BuildBodyRoots();

        if (animator != null)
        {
            int resolved = animator.GetLayerIndex(climbLayerName);
            if (resolved >= 0) climbLayerIndex = resolved;
        }

        moveHash = Animator.StringToHash(climbMoveState);
        mantleHash = Animator.StringToHash(climbMantleState);
        xHash = Animator.StringToHash(climbXParam);
        yHash = Animator.StringToHash(climbYParam);
        layerWeightCurrent = 0f;

        if (debugLogs)
        {
            string maskInfo = climbableMask.value == 0 ? "EMPTY" : climbableMask.value.ToString();
            Debug.Log($"[ClimbController] {BuildVersion} active on {name}. ClimbLayer index={climbLayerIndex}, mask={maskInfo}, " +
                $"animator={(animator != null)}, playerMovement={(playerMovement != null)}, climbFX={(climbFX != null)}, " +
                $"bodyRoots=[{string.Join(", ", bodyRoots.ConvertAll(t => t.name))}].");
            if (climbableMask.value == 0)
                Debug.LogError("[ClimbController] Climbable Mask is not set. Assign it to the Climbable layer in the Inspector, or climbing can never trigger.");
            if (animator != null && !HasParameter(xHash))
                Debug.LogError($"[ClimbController] Animator has no '{climbXParam}' parameter. Run Tools, YORU, Build Climb Blend Tree once, then play again.");
        }
    }

    private void Update()
    {
        TickLayerWeightFade(Time.deltaTime);

        if (regrabTimer > 0f) regrabTimer -= Time.deltaTime;

        if (isClimbing)
        {
            if (isMantling) return; // the mantle coroutine owns movement
            UpdateClimb();
            return;
        }

        TickUprightRecover(Time.deltaTime);
        TryStartClimb();
    }

    private void LateUpdate()
    {
        // After the Animator has written this frame's pose, apply the body depth shift.
        // Runs after release too, until the ease out reaches zero.
        ApplyBodyWallPose(Time.deltaTime);
    }

    private void OnDisable()
    {
        // Safety: never leave the visible body shifted, the root tilted, or the climb layer
        // up if the component is disabled mid climb.
        RestoreBodyPose();
        layerWeightCurrent = 0f;
        if (animator != null) animator.SetLayerWeight(climbLayerIndex, 0f);
        if (Application.isPlaying) SnapUpright();
    }

    #endregion

    #region Grab Detection

    private void TryStartClimb()
    {
        // Cat form only.
        if (formController != null && formController.IsHuman) return;
        // Do not snag mid attack or while guarding.
        if (playerCombat != null && (playerCombat.IsAttacking() || playerCombat.IsGuarding())) return;

        Vector3 moveDir = GetCameraRelativeInput();
        Vector3 castDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Flatten(transform.forward);

        if (!TryFindWall(castDir, out Vector3 normal, out Vector3 point))
        {
            wallWasFound = false;
            return;
        }

        bool airborne = playerMovement != null && playerMovement.IsAirborne();
        bool steepEnough = IsClimbableNormal(normal);
        bool intoWall = IsMovingIntoWall(moveDir, normal);

        // Log once each time a climbable layer surface comes into range, with the deciding values.
        if (debugLogs && !wallWasFound)
            Debug.Log($"[ClimbController] Wall in range. normal.y={normal.y:F2} steepEnough={steepEnough} airborne={airborne} pressingIntoWall={intoWall}");
        wallWasFound = true;

        if (!steepEnough) return;

        if (airborne)
        {
            // Air grab: always snap, no angle gate. Suppressed briefly after letting go or jumping.
            if (regrabTimer > 0f) return;
            StartClimb(normal, point);
        }
        else
        {
            // Ground grab: only when pressing into the wall.
            if (moveDir.sqrMagnitude <= 0.01f) return;
            if (!intoWall) return;
            StartClimb(normal, point);
        }
    }

    private bool TryFindWall(Vector3 dir, out Vector3 normal, out Vector3 point)
    {
        normal = Vector3.zero;
        point = Vector3.zero;
        if (dir.sqrMagnitude < 0.001f) return false;

        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        if (Physics.SphereCast(origin, wallCheckRadius, dir, out RaycastHit hit,
                wallCheckDistance, climbableMask, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            point = hit.point;
            return true;
        }
        return false;
    }

    private bool IsClimbableNormal(Vector3 n)
    {
        // Steep enough to be a wall, not a floor, not a ceiling. Used for ENTERING a climb.
        return n.y <= maxClimbableNormalY && n.y >= minClimbableNormalY;
    }

    private bool IsSustainableNormal(Vector3 n)
    {
        // While already on the wall, tolerate flatter polygons before the forgive timer starts.
        return n.y <= sustainMaxNormalY && n.y >= minClimbableNormalY;
    }

    private bool IsMovingIntoWall(Vector3 moveDir, Vector3 normal)
    {
        Vector3 m = Flatten(moveDir);
        Vector3 n = Flatten(normal);
        if (m.sqrMagnitude < 0.001f || n.sqrMagnitude < 0.001f) return false;
        return Vector3.Dot(m, -n) >= Mathf.Cos(groundGrabAngle * Mathf.Deg2Rad);
    }

    #endregion

    #region Climb Update

    private void UpdateClimb()
    {
        float dt = Time.deltaTime;
        climbTime += dt;

        // Safety: dropped out of cat form mid climb. Instant exit, no eased pose on a human.
        if (formController != null && formController.IsHuman)
        {
            EndClimb(true);
            return;
        }

        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        bool sprint = Input.GetKey(KeyCode.LeftShift);

        // Let go.
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (debugLogs) Debug.Log("[ClimbController] Letting go, C pressed.");
            LetGo();
            return;
        }

        // Climb hop (BOTW fast climb).
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hopTimer = climbHopDuration;
            if (climbFX != null) climbFX.Play(ClimbFX.Hop);
        }
        if (hopTimer > 0f) hopTimer -= dt;

        // A fresh grab must climb for a moment before any mantle can trigger.
        bool mantleAllowed = climbTime >= minClimbTimeBeforeMantle;

        // Re-acquire the wall toward the last known surface, so we follow curved faces.
        // Cast toward the wall itself rather than transform.forward, because on a leaning
        // face the pitched root's forward would aim into the ground.
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Vector3 intoWall = -wallNormal;
        bool found = Physics.SphereCast(origin, wallCheckRadius, intoWall, out RaycastHit hit,
            wallCheckDistance, climbableMask, QueryTriggerInteraction.Ignore);

        // Outside corner while strafing: the straight cast slides off the silhouette edge of
        // the rock. Retry once angled toward the strafe direction so Yoru hugs the curve.
        Vector3 wallUpAxis = WallUpFrom(wallNormal);
        if (!found && Mathf.Abs(h) > inputDeadzone)
        {
            Vector3 angled = Quaternion.AngleAxis(cornerProbeAngle * Mathf.Sign(h), wallUpAxis) * intoWall;
            found = Physics.SphereCast(origin, wallCheckRadius, angled, out hit,
                wallCheckDistance, climbableMask, QueryTriggerInteraction.Ignore);
        }

        bool goodSurface = found && IsSustainableNormal(hit.normal);

        if (goodSurface)
        {
            wallLossTimer = 0f;
            wallNormal = hit.normal;
            wallPoint = hit.point;
        }
        else
        {
            // Wall missing, or one low poly polygon briefly reads too flat. Do NOT drop Yoru
            // onto a mid face polygon and do NOT freeze the animation: movement continues on
            // the last good wall below, and only a persistent loss exits.
            if (mantleAllowed && !found && v > inputDeadzone && TryDetectLedge(out Vector3 ledge))
            {
                StartMantle(ledge);
                return;
            }

            wallLossTimer += dt;
            if (wallLossTimer >= wallLossForgiveTime)
            {
                if (debugLogs)
                {
                    if (found) Debug.Log($"[ClimbController] Letting go, surface left climbable range (normal.y={hit.normal.y:F2}).");
                    else Debug.Log("[ClimbController] Letting go, wall lost.");
                }
                LetGo();
                return;
            }
            // Fall through on the last good wallNormal / wallPoint for the forgive window.
        }

        // Orient the root to the wall surface: smoothed normal, capped turn rate.
        SampleSurfaceNormal(dt);
        ApplyWallOrientation(dt);

        // Wall plane basis for movement, from the raw cast normal so movement stays responsive.
        Vector3 wallUp = WallUpFrom(wallNormal);
        Vector3 wallRight = Vector3.Cross(wallUp, wallNormal).normalized;
        if (Vector3.Dot(wallRight, transform.right) < 0f) wallRight = -wallRight;

        // INNER corner and dent handling. The re-acquire cast above aims at the CURRENT wall,
        // so at a concave corner it keeps succeeding while the capsule grinds against the
        // second wall, and nothing ever turns Yoru. Probe along the direction he is actually
        // trying to move. When a DIFFERENT climbable wall sits in the path, adopt it as the
        // current wall, and the capped turn orientation walks him around the corner or through
        // the dent on its own. Small radius on purpose, a wide sphere overlaps at start this
        // close to the corner and Unity returns degenerate contact data for it. The sustain
        // gate keeps floors and ceilings from ever being adopted.
        Vector3 desiredDir = Vector3.zero;
        if (Mathf.Abs(v) > inputDeadzone) desiredDir += wallUp * Mathf.Sign(v);
        if (Mathf.Abs(h) > inputDeadzone) desiredDir += wallRight * Mathf.Sign(h);
        if (desiredDir.sqrMagnitude > 0.001f)
        {
            desiredDir.Normalize();
            float aheadLen = controller.radius + controller.skinWidth + wallSurfaceGap + 0.35f;
            if (Physics.SphereCast(origin, 0.15f, desiredDir, out RaycastHit ahead, aheadLen,
                    climbableMask, QueryTriggerInteraction.Ignore)
                && IsSustainableNormal(ahead.normal)
                && Vector3.Dot(ahead.normal, wallNormal) < 0.95f)
            {
                wallNormal = ahead.normal;
                wallPoint = ahead.point;
                wallLossTimer = 0f;
                // Rebuild the basis on the adopted wall so this frame already moves along it
                // instead of grinding into it.
                wallUp = WallUpFrom(wallNormal);
                wallRight = Vector3.Cross(wallUp, wallNormal).normalized;
                if (Vector3.Dot(wallRight, transform.right) < 0f) wallRight = -wallRight;
                if (debugLogs) Debug.Log($"[ClimbController] Inner corner: adopted wall in move direction (normal.y={ahead.normal.y:F2}).");
            }
        }

        // Vertical movement.
        float vSpeed = (v > 0f ? climbUpSpeed : climbDownSpeed) * (sprint ? sprintClimbMultiplier : 1f);
        Vector3 vertVel = wallUp * (v * vSpeed);
        if (hopTimer > 0f) vertVel += wallUp * climbHopSpeed;

        // Horizontal movement (wall run when sprinting).
        float hSpeed = sprint ? wallRunSpeed : climbSidewaySpeed;
        Vector3 horizVel = wallRight * (h * hSpeed);

        // Keep Yoru at the hold distance: capsule radius + skin + gap, measured on the capsule axis.
        // Using the real controller size is what stops the capsule from sinking into the rock.
        // Measure to the TRUE surface point straight along the normal: the sphere cast contact
        // point can sit off to the side on angled low poly faces, which skews the distance.
        Vector3 measurePoint = wallPoint;
        if (Physics.Raycast(origin, -wallNormal, out RaycastHit axial, wallCheckDistance + 1f,
                climbableMask, QueryTriggerInteraction.Ignore))
            measurePoint = axial.point;
        float axisHoldDistance = controller.radius + controller.skinWidth + wallSurfaceGap;
        float currentDist = Vector3.Dot(origin - measurePoint, wallNormal);
        Vector3 stickVel = wallNormal * ((axisHoldDistance - currentDist) * surfaceStickSpeed);

        controller.Move((vertVel + horizVel + stickVel) * dt);

        UpdateClimbAnimation(v, h, sprint, dt);

        // Reached a ledge while climbing up.
        if (mantleAllowed && v > inputDeadzone && TryDetectLedge(out Vector3 landPos))
        {
            StartMantle(landPos);
        }
    }

    private bool TryDetectLedge(out Vector3 landPos)
    {
        landPos = Vector3.zero;

        // Probe along the horizontal facing into the wall, not transform.forward, because on
        // a leaning face the pitched root's forward aims below the lip.
        Vector3 face = FlattenSafe(-wallNormal, transform.forward);
        Vector3 headOrigin = transform.position + Vector3.up * topProbeHeight;

        // If the wall still continues at head height, there is no ledge yet. A small sphere
        // instead of a thin ray, so a polygon crease on the face can not slip through and
        // read as open air.
        if (Physics.SphereCast(headOrigin, 0.15f, face, out _, ledgeForwardProbe,
                climbableMask, QueryTriggerInteraction.Ignore))
            return false;

        // No wall above: look for ground just over the lip.
        Vector3 overLip = headOrigin + face * ledgeForwardProbe + Vector3.up * 0.2f;
        if (!Physics.Raycast(overLip, Vector3.down, out RaycastHit g, ledgeDownProbe, climbableMask,
                QueryTriggerInteraction.Ignore))
            return false;

        // Only mantle onto ground flat enough to stand on. A steep hit here is a bump on
        // the cliff face, not the top, and mantling onto it buries Yoru in the rock.
        if (g.normal.y < mantleMinGroundNormalY) return false;

        // Landing spot: pushed far enough onto the ledge that the WHOLE capsule clears the
        // lip. The ledge ray lands roughly at the lip, and an inset smaller than the capsule
        // radius leaves Yoru perched half over the edge, so the inset is floored at the radius.
        float inset = Mathf.Max(mantleForwardInset, controller.radius + 0.1f);
        Vector3 candidate = g.point + Flatten(-wallNormal) * inset + Vector3.up * MantleLandLift;

        // Ground height at the ACTUAL landing spot, on any solid layer: the ledge can rise or
        // dip between the lip and here, and a prop top counts as ground for the fit check.
        if (Physics.Raycast(candidate + Vector3.up * 1f, Vector3.down, out RaycastHit landG, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (landG.normal.y < mantleMinGroundNormalY) return false;
            candidate = landG.point + Vector3.up * MantleLandLift;
        }

        // Refuse the mantle outright when it can not end cleanly, and Yoru just keeps
        // climbing. The capsule must fit at the landing spot and at the top of the rise, and
        // the rise itself must be clear. These see EVERY solid layer (own colliders filtered
        // out), so a bush or crate on the lip blocks the mantle instead of being ground
        // through mid path.
        if (!CapsuleFitsAt(candidate)) return false;
        Vector3 riseTop = new Vector3(transform.position.x, candidate.y, transform.position.z);
        if (!CapsuleFitsAt(riseTop)) return false;
        if (!RiseIsClear(transform.position, candidate.y)) return false;

        landPos = candidate;
        return true;
    }

    /// <summary>
    /// True when Yoru's capsule fits at the given feet position with nothing solid inside it,
    /// on ANY layer. Own colliders are filtered out, triggers ignored, and the radius shrunk
    /// by the skin width so a grazing wall does not read as a blocker.
    /// </summary>
    private bool CapsuleFitsAt(Vector3 feetPos)
    {
        float radius = Mathf.Max(0.05f, controller.radius - controller.skinWidth);
        Vector3 center = feetPos + controller.center;
        float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 top = center + Vector3.up * half;
        Vector3 bottom = center - Vector3.up * half;
        int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, mantleOverlapBuffer,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = mantleOverlapBuffer[i];
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    /// <summary>
    /// True when the straight rise from the current feet position up to the landing height is
    /// clear on ANY layer. Colliders the capsule already overlaps at the start (the wall it
    /// hugs, its own controller) are ignored by the sweep, so only real geometry over Yoru's
    /// head, like an overhang or a branch, refuses the mantle.
    /// </summary>
    private bool RiseIsClear(Vector3 fromFeet, float toFeetY)
    {
        float rise = toFeetY - fromFeet.y;
        if (rise <= 0.01f) return true;
        float radius = Mathf.Max(0.05f, controller.radius - controller.skinWidth);
        Vector3 center = fromFeet + controller.center;
        float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 top = center + Vector3.up * half;
        Vector3 bottom = center - Vector3.up * half;
        return !Physics.CapsuleCast(top, bottom, radius, Vector3.up, out _, rise,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    #endregion

    #region Orientation (root only)

    /// <summary>
    /// Samples the rock under the body at two heights along the wall's up direction and keeps
    /// a smoothed surface normal for orientation. Two samples give both the lean of a mountain
    /// face and the local curve of something like a tree trunk, and the smoothing stops the
    /// root popping when a low poly polygon edge crosses under the body.
    /// </summary>
    private void SampleSurfaceNormal(float dt)
    {
        Vector3 wallUp = WallUpFrom(wallNormal);
        Vector3 castDir = -wallNormal;
        Vector3 baseOrigin = transform.position + wallNormal * BodyCastStartOut;
        bool hitHigh = Physics.Raycast(baseOrigin + wallUp * BodySampleHigh, castDir, out RaycastHit high,
            BodyCastLength, climbableMask, QueryTriggerInteraction.Ignore);
        bool hitLow = Physics.Raycast(baseOrigin + wallUp * BodySampleLow, castDir, out RaycastHit low,
            BodyCastLength, climbableMask, QueryTriggerInteraction.Ignore);

        Vector3 sampled;
        if (hitHigh && hitLow) sampled = (high.normal + low.normal).normalized;
        else if (hitHigh) sampled = high.normal;
        else if (hitLow) sampled = low.normal;
        else sampled = wallNormal;

        float k = Mathf.Clamp01(dt / Mathf.Max(0.01f, surfaceNormalSmoothTime));
        orientNormal = Vector3.Slerp(orientNormal, sampled, k).normalized;
    }

    /// <summary>
    /// Turns the WHOLE character root to the wall: facing into the surface, body up along it.
    /// On a vertical wall this is exactly the old yaw turn. On a leaning face the root also
    /// pitches with the surface, so the clips authored for a flat wall land their coplanar
    /// paws on the actual rock. Turn rate is capped, so a sudden normal change at an inward
    /// corner rotates Yoru around the corner smoothly instead of flipping. Root ONLY, no bone
    /// is ever touched, and the collision capsule stays upright by Unity's own rules.
    /// </summary>
    private void ApplyWallOrientation(float dt)
    {
        Vector3 up = WallUpFrom(orientNormal);
        if (up.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(-orientNormal, up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, wallTurnDegreesPerSecond * dt);
    }

    /// <summary>
    /// Eases the root back upright after the climb (a leaning face leaves it pitched).
    /// PlayerMovement only ever yaws, so nothing else removes that pitch when standing still.
    /// Runs only until upright is reached, then stops, it never fights the movement turn.
    /// </summary>
    private void TickUprightRecover(float dt)
    {
        if (!recoveringUpright) return;
        Vector3 fwd = FlattenSafe(transform.forward, Vector3.forward);
        Quaternion target = Quaternion.LookRotation(fwd, Vector3.up);
        float step = 180f * dt / Mathf.Max(0.01f, uprightRecoverTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, step);
        if (Quaternion.Angle(transform.rotation, target) < 0.1f)
        {
            transform.rotation = target;
            recoveringUpright = false;
        }
    }

    private void SnapUpright()
    {
        Vector3 fwd = FlattenSafe(transform.forward, Vector3.forward);
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        recoveringUpright = false;
    }

    /// <summary>World up projected onto the wall plane of the given normal, the climb's up direction.</summary>
    private static Vector3 WallUpFrom(Vector3 normal)
    {
        Vector3 up = Vector3.up - normal * Vector3.Dot(Vector3.up, normal);
        return up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.zero;
    }

    #endregion

    #region Body Depth (visual only)

    /// <summary>
    /// Collects the model containers for the depth shift: the direct children of the player
    /// root that carry skinned meshes anywhere in their subtree. Matching by what actually
    /// renders, instead of by name, is what makes this follow the real cat (the rendered rig
    /// lives under Cat_All_10_Tails_v4, while bodyYoru holds an older duplicate body plus the
    /// paw VFX anchors, and both should ride the same shift). If Body Visual is assigned in
    /// the Inspector, only that container is used.
    /// </summary>
    private void BuildBodyRoots()
    {
        bodyRoots.Clear();
        if (bodyVisual != null)
        {
            bodyRoots.Add(bodyVisual);
            return;
        }
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                bodyRoots.Add(child);
        }
    }

    /// <summary>
    /// Applies the body depth shift. The clips are wall authored, so with Body Outward Offset
    /// at 0 (the default) this does exactly nothing. It exists as the single tuning value:
    /// each captured container is rebuilt from its rest pose and shifted along the smoothed
    /// surface normal, no rotation. Blended in on grab, out on release, restored exactly.
    /// </summary>
    private void ApplyBodyWallPose(float dt)
    {
        if (posedRoots.Count == 0) return;

        bool wantPose = isClimbing && !isMantling && bodyPoseActive;
        float blendTarget = wantPose ? 1f : 0f;
        bodyBlend = Mathf.MoveTowards(bodyBlend, blendTarget, dt / Mathf.Max(0.01f, bodyAlignTime));

        if (bodyBlend <= 0.0001f && !wantPose)
        {
            RestoreBodyPose();
            return;
        }

        float shift = Mathf.Clamp(bodyOutwardOffset, -MaxBodyShift, MaxBodyShift);
        Vector3 outward = orientNormal * (shift * bodyBlend);

        for (int i = 0; i < posedRoots.Count; i++)
        {
            Transform root = posedRoots[i];
            if (root == null) continue;

            // Rest pose in world space, rebuilt from the captured local pose (never from the
            // already shifted transform), so this frame's shift never stacks on last frame's.
            Transform parent = root.parent;
            Vector3 baseWorldPos = parent != null ? parent.TransformPoint(posedBasePos[i]) : posedBasePos[i];
            Quaternion baseWorldRot = (parent != null ? parent.rotation : Quaternion.identity) * posedBaseRot[i];

            root.SetPositionAndRotation(baseWorldPos + outward, baseWorldRot);
        }
    }

    /// <summary>Puts every posed container back exactly where the prefab has it and stops posing.</summary>
    private void RestoreBodyPose()
    {
        for (int i = 0; i < posedRoots.Count; i++)
        {
            if (posedRoots[i] == null) continue;
            posedRoots[i].localPosition = posedBasePos[i];
            posedRoots[i].localRotation = posedBaseRot[i];
        }
        posedRoots.Clear();
        posedBasePos.Clear();
        posedBaseRot.Clear();
        bodyPoseActive = false;
        bodyBlend = 0f;
    }

    #endregion

    #region Mantle

    private void StartMantle(Vector3 landPos)
    {
        if (debugLogs) Debug.Log("[ClimbController] Mantling over the top.");
        isMantling = true;
        if (animator != null) animator.CrossFadeInFixedTime(mantleHash, climbBlend, climbLayerIndex);
        if (climbFX != null) climbFX.Play(ClimbFX.MantleStart);
        if (mantleRoutine != null) StopCoroutine(mantleRoutine);
        mantleRoutine = StartCoroutine(MantleRoutine(landPos));
    }

    private IEnumerator MantleRoutine(Vector3 landPos)
    {
        Vector3 start = transform.position;
        // The landing spot arrives fully validated from TryDetectLedge: inset past the lip,
        // ground height re probed there, capsule fit and rise clearance already checked.
        Vector3 end = landPos;
        Vector3 riseTop = new Vector3(start.x, end.y, start.z);

        float t = 0f;
        while (t < mantleDuration)
        {
            t += Time.deltaTime;
            float frac = Mathf.Clamp01(t / mantleDuration);

            // First 60 percent rises along the face, last 40 percent moves forward onto the ledge.
            Vector3 pos;
            if (frac < 0.6f) pos = Vector3.Lerp(start, riseTop, frac / 0.6f);
            else pos = Vector3.Lerp(riseTop, end, (frac - 0.6f) / 0.4f);

            // Move through the CharacterController so collision resolves every step.
            // Setting transform.position directly bypasses collision and can bury Yoru in the rock.
            controller.Move(pos - transform.position);
            yield return null;
        }

        controller.Move(end - transform.position);
        mantleRoutine = null;
        if (climbFX != null) climbFX.Play(ClimbFX.MantleLand);
        EndClimb();
        regrabTimer = regrabSuppressTime;
    }

    #endregion

    #region Enter / Exit

    private void StartClimb(Vector3 normal, Vector3 point)
    {
        isClimbing = true;
        isMantling = false;
        wallNormal = normal;
        wallPoint = point;
        orientNormal = normal;
        recoveringUpright = false;

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;

        // Capture each active model container's rest pose once, so the depth shift can be
        // applied and later restored exactly. Containers inactive at grab (for example the
        // hidden human model) are skipped. A quick re-grab mid ease-out keeps the original capture.
        if (!bodyPoseActive && bodyRoots.Count > 0)
        {
            posedRoots.Clear();
            posedBasePos.Clear();
            posedBaseRot.Clear();
            foreach (Transform root in bodyRoots)
            {
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                posedRoots.Add(root);
                posedBasePos.Add(root.localPosition);
                posedBaseRot.Add(root.localRotation);
            }
            bodyPoseActive = posedRoots.Count > 0;
        }

        hopTimer = 0f;
        wallLossTimer = 0f;
        climbTime = 0f;
        blendLogTimer = 0f;

        // The blend values start ON the held input, not at idle, so a moving grab goes
        // straight into the matching motion with no idle flash. The layer weight fades in
        // through TickLayerWeightFade, that is the softness on entry.
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        climbX = TargetX(h, sprint);
        climbY = TargetY(v);
        if (animator != null)
        {
            animator.SetFloat(xHash, climbX);
            animator.SetFloat(yHash, climbY);
            animator.CrossFadeInFixedTime(moveHash, climbBlend, climbLayerIndex);
        }

        if (climbFX != null) climbFX.Play(ClimbFX.Grab);
        if (debugLogs) Debug.Log($"[ClimbController] Grabbed wall, entering climb. Blend start x={climbX:F2} y={climbY:F2}, containers held: {posedRoots.Count}.");
    }

    private void LetGo()
    {
        if (climbFX != null) climbFX.Play(ClimbFX.LetGo);
        EndClimb();
        if (playerMovement != null)
        {
            Vector3 push = Flatten(wallNormal) * letGoPushForce;
            playerMovement.ApplyExternalPull(push, letGoPushDuration);
        }
        regrabTimer = regrabSuppressTime;
    }

    /// <summary>
    /// Leaves the climb. By default the climb layer fades out, the body depth eases back, and
    /// the root eases upright over Upright Recover Time. Pass instant for hard exits (form
    /// change to human), which snaps everything immediately.
    /// </summary>
    private void EndClimb(bool instant = false)
    {
        isClimbing = false;
        isMantling = false;
        if (mantleRoutine != null)
        {
            StopCoroutine(mantleRoutine);
            mantleRoutine = null;
        }
        if (instant)
        {
            layerWeightCurrent = 0f;
            if (animator != null) animator.SetLayerWeight(climbLayerIndex, 0f);
            RestoreBodyPose();
            SnapUpright();
        }
        else
        {
            recoveringUpright = true;
        }
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
    }

    #endregion

    #region Animation

    /// <summary>
    /// Fades the climb layer toward 1 while climbing and toward 0 otherwise, over Layer Fade
    /// Time, replacing an instant on and off switch. Runs every frame from Update so the fade
    /// out continues after the climb has already ended.
    /// </summary>
    private void TickLayerWeightFade(float dt)
    {
        if (animator == null) return;
        float target = isClimbing ? 1f : 0f;
        if (Mathf.Approximately(layerWeightCurrent, target)) return;
        layerWeightCurrent = Mathf.MoveTowards(layerWeightCurrent, target, dt / Mathf.Max(0.01f, layerFadeTime));
        animator.SetLayerWeight(climbLayerIndex, layerWeightCurrent);
    }

    /// <summary>
    /// Drives the blend tree: the two floats chase the held keys at Input Response speed.
    /// Sideways target is 1 for a walk, 2 for a wall run (Shift). Vertical target is 1 up,
    /// minus 1 down, and a hop holds it at 1 so the hop reads as climbing. Releasing the keys
    /// lets both drift back to 0, which IS the idle, so brief gaps between key presses barely
    /// move the pose instead of flickering it.
    /// </summary>
    private void UpdateClimbAnimation(float v, float h, bool sprint, float dt)
    {
        if (animator == null) return;

        float tx = TargetX(h, sprint);
        float ty = TargetY(v);
        if (hopTimer > 0f) ty = Mathf.Max(ty, 1f);

        climbX = Mathf.MoveTowards(climbX, tx, inputResponse * dt);
        climbY = Mathf.MoveTowards(climbY, ty, inputResponse * dt);
        animator.SetFloat(xHash, climbX);
        animator.SetFloat(yHash, climbY);

        // One narration line per second while climbing, so a stale build or a wiring problem
        // shows itself immediately in the Console.
        if (debugLogs)
        {
            blendLogTimer += dt;
            if (blendLogTimer >= 1f)
            {
                blendLogTimer = 0f;
                Debug.Log($"[ClimbBlend] x={climbX:F2} y={climbY:F2} (targets {tx:F0},{ty:F0}) layerW={layerWeightCurrent:F2} orientNormalY={orientNormal.y:F2}");
            }
        }
    }

    private float TargetX(float h, bool sprint)
    {
        if (Mathf.Abs(h) <= inputDeadzone) return 0f;
        return Mathf.Sign(h) * (sprint ? 2f : 1f);
    }

    private float TargetY(float v)
    {
        if (Mathf.Abs(v) <= inputDeadzone) return 0f;
        return Mathf.Sign(v);
    }

    private bool HasParameter(int hash)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].nameHash == hash) return true;
        return false;
    }

    #endregion

    #region Utility

    private Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 camF = cameraTransform != null ? Flatten(cameraTransform.forward) : Vector3.forward;
        Vector3 camR = cameraTransform != null ? Flatten(cameraTransform.right) : Vector3.right;

        Vector3 dir = camF * v + camR * h;
        return dir;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    /// <summary>Flatten with a fallback, for directions that can point almost straight up or down.</summary>
    private static Vector3 FlattenSafe(Vector3 v, Vector3 fallback)
    {
        Vector3 f = Flatten(v);
        if (f.sqrMagnitude > 0.0001f) return f;
        f = Flatten(fallback);
        return f.sqrMagnitude > 0.0001f ? f : Vector3.forward;
    }

    /// <summary>True while Yoru is on a wall. Other systems can read this.</summary>
    public bool IsClimbing => isClimbing;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isClimbing ? Color.cyan : Color.yellow;
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Vector3 dir = isClimbing ? -wallNormal : transform.forward;
        Gizmos.DrawWireSphere(origin + dir * wallCheckDistance, wallCheckRadius);
        Gizmos.DrawLine(origin, origin + dir * wallCheckDistance);
    }

    #endregion
}
