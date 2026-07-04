using System.Collections;
using UnityEngine;

/// <summary>
/// YORU climbing system. Breath of the Wild style auto grab, plus a wall run addition.
/// Cat (Yoru) form only.
///
/// DESIGN
///   - Grab is contextual, no dedicated key:
///       * On the ground, Yoru attaches only when he MOVES INTO a climbable surface
///         (pressing toward the wall), so walking along or away from a cliff base never grabs.
///       * In the air, Yoru attaches on any contact with a climbable surface (no angle gate).
///         This is the reliable fall save, a fall never slips past a climbable wall.
///   - No falling while on the wall. With no stamina yet, Yoru leaves only by letting go (C),
///     or by mantling at the top. Stamina drops in later at the single ClimbAllowed / speed gate.
///   - Low poly rock is forgiving: a miss frame or one briefly too flat polygon never freezes
///     the animation or ends the climb. Movement and animation continue on the last good wall,
///     an angled corner probe follows curved faces while strafing, and only a persistent loss
///     (Wall Loss Forgive Time) exits.
///   - Mantle is guarded: it needs a minimum climb time first, the ledge ground must be flat
///     enough to stand on, and the head probe is a small sphere so a polygon crease on the
///     face never reads as the top. A bump on the cliff can not fake a mantle.
///   - BODY ON THE WALL (visual only). The Zelda approach: the collision capsule and the
///     visible body are two separate things. While climbing, the visible body (bodyYoru) is
///     tilted to lie on the sampled rock surface and nudged out along the surface normal, so
///     the mesh stops sinking into slopes and cliffs. The CharacterController capsule is NEVER
///     moved or resized by this script, so combat, dodge and normal movement are untouched.
///     The pose eases in on grab, eases out on release, and fully restores after.
///   - SOFT TRANSITIONS. Nothing snaps: the turn to face the wall eases in through the same
///     Slerp that runs during the climb, the climb layer weight fades in and out instead of
///     switching, the first climb pose is picked from the input actually held (no forced idle
///     flash), and a short idle return delay keeps a move playing across brief input gaps so
///     the pose does not flicker to idle between key presses.
///   - This script NEVER touches PlayerMovement. On grab it disables PlayerMovement (so its
///     Update and FixedUpdate stop while it owns no Move) and disables PlayerCombat (no attacks
///     on the wall), runs its OWN controller.Move, then re-enables both on exit. Disabled
///     MonoBehaviours still expose their public methods, so other scripts are unaffected.
///   - Animation is driven entirely from code, the same way EnemyCombat does it:
///     CrossFadeInFixedTime by cached hash on the Climb layer, only when the target state
///     changes, so the clip does not restart every frame. A minimum state hold time stops
///     rapid strobing between states. No transition arrows are wired.
///
/// CONTROLS (on the wall)
///   W / S        climb up / down            (wall relative)
///   A / D        climb sideways             (ClimbSidewayL / ClimbSidewayR)
///   Shift + A/D  wall run                   (ClimbWallRunL / ClimbWallRunR)
///   Shift + W/S  faster climb (speed only, no separate clip)
///   Space        climb hop up               (BOTW fast climb, stays on the wall)
///   C            let go                     (detach and drop, never a trap)
///   top reached  auto mantle                (ClimbMantle, then control returns)
///
/// SETUP
///   1. Put this component on the player root (same GameObject as PlayerMovement, the
///      CharacterController, PlayerCombat, FormController and the Animator).
///   2. Assign the Climbable Mask to your new Climbable layer.
///   3. Climb Layer Name must match the Animator layer ("ClimbLayer"). The index is resolved
///      automatically by name in Awake, with Climb Layer Index used only as a fallback.
///   4. Rename the Animator state "ClimpUp" to "ClimbUp" so it matches Climb Up State below,
///      or change the field to match your state name. All other state names already match.
///   5. The eight climb states must sit directly in the Climb layer (not inside a sub state
///      machine), so the state name hash resolves.
///   6. Body Visual auto finds the child named "bodyYoru". Only assign it manually if that
///      child is ever renamed.
///
/// FX
///   This controller fires five climb moments through ClimbFX: Grab, Hop, LetGo, MantleStart,
///   MantleLand. The per step hand and foot effects are fired by animation events on the clips
///   (see ClimbFX for the slot names), so the frame timing stays in your hands.
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
    [Tooltip("Yoru's visible body child (bodyYoru). While climbing it is tilted onto the rock surface and nudged out of it, VISUAL ONLY, the capsule is never moved. Auto found by name if left empty.")]
    [SerializeField] private Transform bodyVisual;

    #endregion

    #region Animator

    [Header("Animator")]
    [Tooltip("Name of the dedicated climb layer. The index is resolved from this in Awake.")]
    [SerializeField] private string climbLayerName = "ClimbLayer";
    [Tooltip("Fallback index used only if the name above is not found. Base, Combat, Cinematic, Climb means 3.")]
    [SerializeField] private int climbLayerIndex = 3;
    [Tooltip("CrossFade blend time between climb states, in seconds. Raise toward 0.2 for softer blends.")]
    [SerializeField] private float climbBlend = 0.15f;
    [Tooltip("Minimum time a climb state plays before it may switch to another. Stops the animation strobing when input or the wall cast flickers frame to frame. Grab and mantle ignore this.")]
    [SerializeField] private float animMinStateTime = 0.15f;
    [Tooltip("Seconds to fade the climb layer in on grab and out on release, instead of switching it on and off instantly.")]
    [SerializeField] private float layerFadeTime = 0.2f;
    [Tooltip("How long input must stay released before the pose returns to ClimbIdle. Keeps the current move playing across brief input gaps, which removes the idle flicker that hides the sideway clips.")]
    [SerializeField] private float idleReturnDelay = 0.2f;

    [Header("Climb State Names (must match the Animator states)")]
    [SerializeField] private string climbIdleState = "ClimbIdle";
    [SerializeField] private string climbUpState = "ClimbUp";
    [SerializeField] private string climbDownState = "ClimbDown";
    [SerializeField] private string climbSidewayLState = "ClimbSidewayL";
    [SerializeField] private string climbSidewayRState = "ClimbSidewayR";
    [SerializeField] private string climbWallRunLState = "ClimbWallRunL";
    [SerializeField] private string climbWallRunRState = "ClimbWallRunR";
    [SerializeField] private string climbMantleState = "ClimbMantle";

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
    [Tooltip("When the forward wall cast misses while strafing, retry once angled this many degrees toward the strafe direction, so Yoru follows curved faces around corners instead of losing the wall.")]
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
    [Tooltip("How quickly Yoru rotates to face the wall.")]
    [SerializeField] private float faceWallTurnSpeed = 14f;
    [SerializeField] private float inputDeadzone = 0.1f;

    [Header("Body On The Wall (visual only, capsule untouched)")]
    [Tooltip("How far the visible body is pushed out along the rock surface, in meters. This pulls the sunken mesh OUT to the surface, it does not hover Yoru. Raise a little if the belly still dips into rock, lower toward 0 if the paws ever hover.")]
    [SerializeField] private float bodyOutwardOffset = 0.15f;
    [Tooltip("Seconds for the visible body to ease onto the wall pose on grab and back to normal on release. Also smooths the surface tilt across low poly polygon edges.")]
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
    [Tooltip("Logs grab checks and setup to the Console. Turn off once climbing works.")]
    [SerializeField] private bool debugLogs = true;

    #endregion

    #region Runtime State

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
    private float lastStateChangeTime;
    private float noInputTimer;
    private float layerWeightCurrent;
    private Coroutine mantleRoutine;

    // Body on the wall (visual only).
    private bool bodyPoseActive;
    private float bodyBlend;
    private Vector3 bodyBaseLocalPos;
    private Quaternion bodyBaseLocalRot;
    private Vector3 bodyPlaneNormal = Vector3.forward;

    // Geometry of the two surface samples under the body, from the root along the wall's
    // up direction. These describe the cat's proportions, not feel, so they are constants.
    private const float BodySampleLow = 0.3f;
    private const float BodySampleHigh = 1.1f;
    private const float BodyCastStartOut = 0.8f;
    private const float BodyCastLength = 1.8f;
    // Cap on how far the visible body may tilt onto a flat-ish polygon, in degrees.
    private const float MaxBodyTiltAngle = 55f;

    #endregion

    #region Cached Hashes

    private int idleHash;
    private int upHash;
    private int downHash;
    private int sideLHash;
    private int sideRHash;
    private int runLHash;
    private int runRHash;
    private int mantleHash;
    private int currentStateHash;

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
        if (bodyVisual == null) bodyVisual = FindBodyVisual();

        if (animator != null)
        {
            int resolved = animator.GetLayerIndex(climbLayerName);
            if (resolved >= 0) climbLayerIndex = resolved;
        }

        idleHash = Animator.StringToHash(climbIdleState);
        upHash = Animator.StringToHash(climbUpState);
        downHash = Animator.StringToHash(climbDownState);
        sideLHash = Animator.StringToHash(climbSidewayLState);
        sideRHash = Animator.StringToHash(climbSidewayRState);
        runLHash = Animator.StringToHash(climbWallRunLState);
        runRHash = Animator.StringToHash(climbWallRunRState);
        mantleHash = Animator.StringToHash(climbMantleState);
        currentStateHash = -1;
        lastStateChangeTime = -999f;
        layerWeightCurrent = 0f;

        if (debugLogs)
        {
            string maskInfo = climbableMask.value == 0 ? "EMPTY" : climbableMask.value.ToString();
            Debug.Log($"[ClimbController] Active on {name}. ClimbLayer index={climbLayerIndex}, mask={maskInfo}, " +
                $"animator={(animator != null)}, playerMovement={(playerMovement != null)}, climbFX={(climbFX != null)}, " +
                $"bodyVisual={(bodyVisual != null ? bodyVisual.name : "NOT FOUND")}.");
            if (climbableMask.value == 0)
                Debug.LogError("[ClimbController] Climbable Mask is not set. Assign it to the Climbable layer in the Inspector, or climbing can never trigger.");
            if (bodyVisual == null)
                Debug.LogWarning("[ClimbController] Body Visual not found (child named 'bodyYoru'). Climbing still works, but the visible body will not be aligned onto the rock surface.");
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

        TryStartClimb();
    }

    private void LateUpdate()
    {
        // After the Animator has written this frame's pose, lay the visible body onto the
        // sampled rock surface. Runs after release too, until the ease out reaches zero.
        ApplyBodyWallPose(Time.deltaTime);
    }

    private void OnDisable()
    {
        // Safety: never leave the visible body tilted or the climb layer up if the component
        // is disabled mid climb.
        RestoreBodyPose();
        layerWeightCurrent = 0f;
        if (animator != null) animator.SetLayerWeight(climbLayerIndex, 0f);
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

        // Re-acquire the wall in the facing direction so we follow curved faces.
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        bool found = Physics.SphereCast(origin, wallCheckRadius, transform.forward, out RaycastHit hit,
            wallCheckDistance, climbableMask, QueryTriggerInteraction.Ignore);

        // Outside corner while strafing: the straight ahead cast slides off the silhouette
        // edge of the rock. Retry once angled toward the strafe direction so Yoru hugs the
        // curve instead of losing the wall.
        if (!found && Mathf.Abs(h) > inputDeadzone)
        {
            Vector3 angled = Quaternion.AngleAxis(cornerProbeAngle * Mathf.Sign(h), Vector3.up) * transform.forward;
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
            // onto a mid face polygon and do NOT freeze the animation: movement and animation
            // continue on the last good wall below, and only a persistent loss exits.
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

        // Face the wall (the only turn, there is no snap anywhere).
        Quaternion target = Quaternion.LookRotation(Flatten(-wallNormal));
        transform.rotation = Quaternion.Slerp(transform.rotation, target, faceWallTurnSpeed * dt);

        // Wall plane basis.
        Vector3 wallUp = (Vector3.up - wallNormal * Vector3.Dot(Vector3.up, wallNormal)).normalized;
        Vector3 wallRight = Vector3.Cross(wallUp, wallNormal).normalized;
        if (Vector3.Dot(wallRight, transform.right) < 0f) wallRight = -wallRight;

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

        SampleBodyPlane(dt, wallUp);
        UpdateClimbAnimation(v, h, sprint);

        // Reached a ledge while climbing up.
        if (mantleAllowed && v > inputDeadzone && TryDetectLedge(out Vector3 topPos))
        {
            StartMantle(topPos);
        }
    }

    private bool TryDetectLedge(out Vector3 topPos)
    {
        topPos = Vector3.zero;
        Vector3 headOrigin = transform.position + Vector3.up * topProbeHeight;

        // If the wall still continues at head height, there is no ledge yet. A small sphere
        // instead of a thin ray, so a polygon crease on the face can not slip through and
        // read as open air.
        if (Physics.SphereCast(headOrigin, 0.15f, transform.forward, out _, ledgeForwardProbe,
                climbableMask, QueryTriggerInteraction.Ignore))
            return false;

        // No wall above: look for ground just over the lip.
        Vector3 overLip = headOrigin + transform.forward * ledgeForwardProbe + Vector3.up * 0.2f;
        if (Physics.Raycast(overLip, Vector3.down, out RaycastHit g, ledgeDownProbe, climbableMask,
                QueryTriggerInteraction.Ignore))
        {
            // Only mantle onto ground flat enough to stand on. A steep hit here is a bump on
            // the cliff face, not the top, and mantling onto it buries Yoru in the rock.
            if (g.normal.y < mantleMinGroundNormalY) return false;
            topPos = g.point;
            return true;
        }
        return false;
    }

    #endregion

    #region Body On The Wall (visual only)

    /// <summary>
    /// Finds the visible body child by name. Direct child first, then a deep search, so a
    /// reparented model still resolves without any manual setup.
    /// </summary>
    private Transform FindBodyVisual()
    {
        Transform direct = transform.Find("bodyYoru");
        if (direct != null) return direct;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t != transform && t.name == "bodyYoru") return t;
        }
        return null;
    }

    /// <summary>
    /// Samples the rock under the body at two heights along the wall's up direction and keeps
    /// a smoothed surface normal. Two samples give both the lean of a mountain face and the
    /// local curve of something like a tree trunk, and the smoothing stops the body popping
    /// when a low poly polygon edge crosses under it.
    /// </summary>
    private void SampleBodyPlane(float dt, Vector3 wallUp)
    {
        if (bodyVisual == null) return;

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

        float k = Mathf.Clamp01(dt / Mathf.Max(0.01f, bodyAlignTime));
        bodyPlaneNormal = Vector3.Slerp(bodyPlaneNormal, sampled, k).normalized;
    }

    /// <summary>
    /// Lays the visible body onto the sampled surface: a tilt from the upright vertical wall
    /// assumption onto the real surface plane, pivoted at the root so the paws stay planted,
    /// plus a small outward nudge along the surface normal. Blended in and out over Body Align
    /// Time. On a truly vertical wall the tilt is zero, so nothing that already looks right
    /// changes there. The pose is rebuilt every frame from the base local pose captured at
    /// grab, so nothing compounds, and it fully restores once the blend reaches zero.
    /// </summary>
    private void ApplyBodyWallPose(float dt)
    {
        if (bodyVisual == null) return;

        bool wantPose = isClimbing && !isMantling && bodyPoseActive;
        float blendTarget = wantPose ? 1f : 0f;
        bodyBlend = Mathf.MoveTowards(bodyBlend, blendTarget, dt / Mathf.Max(0.01f, bodyAlignTime));

        if (!bodyPoseActive) return;

        if (bodyBlend <= 0.0001f && !wantPose)
        {
            RestoreBodyPose();
            return;
        }

        // Base pose in world space, rebuilt from the captured local pose (never from the
        // already modified transform), so this frame's tilt never stacks on last frame's.
        Transform parent = bodyVisual.parent;
        Vector3 baseWorldPos = parent != null ? parent.TransformPoint(bodyBaseLocalPos) : bodyBaseLocalPos;
        Quaternion baseWorldRot = (parent != null ? parent.rotation : Quaternion.identity) * bodyBaseLocalRot;

        // The animation is authored against an upright wall. Tilt by exactly the difference
        // between that upright plane and the sampled real one, capped for safety.
        Vector3 uprightNormal = Flatten(bodyPlaneNormal);
        Quaternion tilt = Quaternion.identity;
        if (uprightNormal.sqrMagnitude > 0.0001f)
        {
            Quaternion fullTilt = Quaternion.FromToRotation(uprightNormal, bodyPlaneNormal);
            tilt = Quaternion.RotateTowards(Quaternion.identity, fullTilt, MaxBodyTiltAngle);
        }
        Quaternion frameTilt = Quaternion.Slerp(Quaternion.identity, tilt, bodyBlend);

        Vector3 pivot = transform.position;
        Vector3 pos = pivot + frameTilt * (baseWorldPos - pivot)
            + bodyPlaneNormal * (bodyOutwardOffset * bodyBlend);
        Quaternion rot = frameTilt * baseWorldRot;
        bodyVisual.SetPositionAndRotation(pos, rot);
    }

    /// <summary>Puts the visible body back exactly where the prefab has it and stops posing.</summary>
    private void RestoreBodyPose()
    {
        if (bodyPoseActive && bodyVisual != null)
        {
            bodyVisual.localPosition = bodyBaseLocalPos;
            bodyVisual.localRotation = bodyBaseLocalRot;
        }
        bodyPoseActive = false;
        bodyBlend = 0f;
    }

    #endregion

    #region Mantle

    private void StartMantle(Vector3 topPos)
    {
        if (debugLogs) Debug.Log("[ClimbController] Mantling over the top.");
        isMantling = true;
        PlayClimbState(mantleHash, true);
        if (climbFX != null) climbFX.Play(ClimbFX.MantleStart);
        if (mantleRoutine != null) StopCoroutine(mantleRoutine);
        mantleRoutine = StartCoroutine(MantleRoutine(topPos));
    }

    private IEnumerator MantleRoutine(Vector3 topPos)
    {
        Vector3 start = transform.position;
        Vector3 forwardInset = Flatten(-wallNormal) * mantleForwardInset;
        // Small lift so the capsule bottom lands ON the ledge, not intersecting it.
        Vector3 end = topPos + forwardInset + Vector3.up * 0.05f;
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

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;

        // No snap to face the wall. The Slerp in UpdateClimb turns Yoru in over the first
        // fraction of a second, and the wall loss forgive window covers the cast while the
        // facing settles. The climb layer weight fades in through TickLayerWeightFade.

        // Capture the visible body's rest pose once, so it can be posed onto the surface and
        // later restored exactly. A quick re-grab mid ease-out keeps the original capture.
        if (bodyVisual != null && !bodyPoseActive)
        {
            bodyBaseLocalPos = bodyVisual.localPosition;
            bodyBaseLocalRot = bodyVisual.localRotation;
            bodyPoseActive = true;
        }
        bodyPlaneNormal = wallNormal;

        hopTimer = 0f;
        wallLossTimer = 0f;
        climbTime = 0f;
        noInputTimer = 0f;
        currentStateHash = -1;

        // First pose comes from the input actually held, so a moving grab goes straight into
        // the matching move instead of flashing through ClimbIdle first.
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        PlayClimbState(SelectClimbState(v, h, sprint), true);

        if (climbFX != null) climbFX.Play(ClimbFX.Grab);
        if (debugLogs) Debug.Log("[ClimbController] Grabbed wall, entering climb.");
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
    /// Leaves the climb. By default the climb layer fades out and the visible body eases back
    /// over Body Align Time. Pass instant for hard exits (form change to human), which snaps
    /// the layer to zero and restores the body immediately.
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
        }
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        currentStateHash = -1;
    }

    #endregion

    #region Animation

    /// <summary>
    /// Fades the climb layer toward 1 while climbing and toward 0 otherwise, over Layer Fade
    /// Time, replacing the old instant on and off switch. Runs every frame from Update so the
    /// fade out continues after the climb has already ended.
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
    /// Picks the climb state for the input held right now. W and S win over A and D, Shift
    /// turns sideways into a wall run, no input means ClimbIdle.
    /// </summary>
    private int SelectClimbState(float v, float h, bool sprint)
    {
        if (Mathf.Abs(v) > inputDeadzone || hopTimer > 0f)
            return (v >= 0f || hopTimer > 0f) ? upHash : downHash;
        if (Mathf.Abs(h) > inputDeadzone)
            return sprint ? (h < 0f ? runLHash : runRHash) : (h < 0f ? sideLHash : sideRHash);
        return idleHash;
    }

    private void UpdateClimbAnimation(float v, float h, bool sprint)
    {
        int target = SelectClimbState(v, h, sprint);

        // Idle only after the input has been released for a moment. A one or two frame gap
        // between key presses keeps the current move playing instead of flickering through
        // ClimbIdle, which is what made the sideway clips hard to see.
        if (target == idleHash)
        {
            noInputTimer += Time.deltaTime;
            if (noInputTimer < idleReturnDelay) return;
        }
        else
        {
            noInputTimer = 0f;
        }

        PlayClimbState(target);
    }

    /// <summary>
    /// CrossFades to a climb state by hash, only when it changes, so the clip does not restart
    /// every frame. A minimum hold time (Anim Min State Time) stops rapid strobing between
    /// states; grab and mantle pass force to bypass it. Same pattern as EnemyCombat. Logs the
    /// state name so the Console narrates which climb animation is running.
    /// </summary>
    private void PlayClimbState(int stateHash, bool force = false)
    {
        if (stateHash == currentStateHash) return;
        if (animator == null) return;
        if (!force && Time.time - lastStateChangeTime < animMinStateTime) return;
        animator.CrossFadeInFixedTime(stateHash, climbBlend, climbLayerIndex);
        currentStateHash = stateHash;
        lastStateChangeTime = Time.time;
        if (debugLogs) Debug.Log($"[ClimbController] Anim -> {StateNameForHash(stateHash)}");
    }

    /// <summary>Readable name for a cached climb state hash, for the debug narration.</summary>
    private string StateNameForHash(int hash)
    {
        if (hash == idleHash) return climbIdleState;
        if (hash == upHash) return climbUpState;
        if (hash == downHash) return climbDownState;
        if (hash == sideLHash) return climbSidewayLState;
        if (hash == sideRHash) return climbSidewayRState;
        if (hash == runLHash) return climbWallRunLState;
        if (hash == runRHash) return climbWallRunRState;
        if (hash == mantleHash) return climbMantleState;
        return "Unknown";
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

    /// <summary>True while Yoru is on a wall. Other systems can read this.</summary>
    public bool IsClimbing => isClimbing;

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isClimbing ? Color.cyan : Color.yellow;
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Gizmos.DrawWireSphere(origin + transform.forward * wallCheckDistance, wallCheckRadius);
        Gizmos.DrawLine(origin, origin + transform.forward * wallCheckDistance);
    }

    #endregion
}