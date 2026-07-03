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
    private Coroutine mantleRoutine;

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

        if (debugLogs)
        {
            string maskInfo = climbableMask.value == 0 ? "EMPTY" : climbableMask.value.ToString();
            Debug.Log($"[ClimbController] Active on {name}. ClimbLayer index={climbLayerIndex}, mask={maskInfo}, " +
                $"animator={(animator != null)}, playerMovement={(playerMovement != null)}, climbFX={(climbFX != null)}.");
            if (climbableMask.value == 0)
                Debug.LogError("[ClimbController] Climbable Mask is not set. Assign it to the Climbable layer in the Inspector, or climbing can never trigger.");
        }
    }

    private void Update()
    {
        if (regrabTimer > 0f) regrabTimer -= Time.deltaTime;

        if (isClimbing)
        {
            if (isMantling) return; // the mantle coroutine owns movement
            UpdateClimb();
            return;
        }

        TryStartClimb();
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

        // Safety: dropped out of cat form mid climb.
        if (formController != null && formController.IsHuman)
        {
            EndClimb();
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

        // Face the wall.
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
        float axisHoldDistance = controller.radius + controller.skinWidth + wallSurfaceGap;
        float currentDist = Vector3.Dot(origin - wallPoint, wallNormal);
        Vector3 stickVel = wallNormal * ((axisHoldDistance - currentDist) * surfaceStickSpeed);

        controller.Move((vertVel + horizVel + stickVel) * dt);

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
        if (animator != null) animator.SetLayerWeight(climbLayerIndex, 1f);

        // Snap to face the wall so the first climb pose reads correctly.
        transform.rotation = Quaternion.LookRotation(Flatten(-wallNormal));

        hopTimer = 0f;
        wallLossTimer = 0f;
        climbTime = 0f;
        currentStateHash = -1;
        PlayClimbState(idleHash, true);
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

    private void EndClimb()
    {
        isClimbing = false;
        isMantling = false;
        if (mantleRoutine != null)
        {
            StopCoroutine(mantleRoutine);
            mantleRoutine = null;
        }
        if (animator != null) animator.SetLayerWeight(climbLayerIndex, 0f);
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        currentStateHash = -1;
    }

    #endregion

    #region Animation

    private void UpdateClimbAnimation(float v, float h, bool sprint)
    {
        int target;
        if (Mathf.Abs(v) > inputDeadzone || hopTimer > 0f)
        {
            target = (v >= 0f || hopTimer > 0f) ? upHash : downHash;
        }
        else if (Mathf.Abs(h) > inputDeadzone)
        {
            if (sprint) target = h < 0f ? runLHash : runRHash;
            else target = h < 0f ? sideLHash : sideRHash;
        }
        else
        {
            target = idleHash;
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