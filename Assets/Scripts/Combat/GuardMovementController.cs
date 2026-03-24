using UnityEngine;

/// <summary>
/// YORU Guard Movement Controller — Phase 3C v5
/// Handles horizontal movement calculation and rotation during Q guard.
/// Gravity AND the single controller.Move() are handled by PlayerMovement.FixedUpdate.
///
/// v4: Single-Move architecture (no controller.Move in this script).
/// v5: Camera vectors frozen at guard start. Rotating camera during guard no longer
///     flips forward/backward walk direction. Input projection is stable.
///
/// Key design:
///   - Facing direction locks to MOVEMENT DIRECTION at time Q is pressed
///   - Camera forward/right FROZEN at guard start — camera rotation during guard
///     does NOT change which direction counts as "forward" or "backward"
///   - Input projected onto locked direction via dot product
///   - No rotation during guard
///   - Horizontal velocity calculated in Update, consumed by PlayerMovement in FixedUpdate
/// </summary>
public class GuardMovementController : MonoBehaviour
{
    [Header("Guard Movement")]
    [SerializeField] private float guardWalkSpeed = 0.75f;

    private Transform cachedTransform;
    private Camera mainCamera;

    private bool isGuardActive;
    private Vector3 lockedForward;
    private Vector3 cachedHorizontalVelocity;

    // Frozen at guard start — prevents camera rotation from flipping walk direction
    private Vector3 guardCamForward;
    private Vector3 guardCamRight;

    private void Awake()
    {
        cachedTransform = transform;
    }

    /// <summary>
    /// Called by PlayerCombat.StartGuard(). Locks facing direction and freezes camera vectors.
    /// </summary>
    public void EnableGuard(Vector3 forwardDirection)
    {
        isGuardActive = true;
        cachedHorizontalVelocity = Vector3.zero;

        lockedForward = forwardDirection;
        lockedForward.y = 0f;
        lockedForward.Normalize();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Freeze camera vectors — rotating camera during guard won't flip walk direction
        if (mainCamera != null)
        {
            guardCamForward = mainCamera.transform.forward;
            guardCamForward.y = 0f;
            guardCamForward.Normalize();
            guardCamRight = mainCamera.transform.right;
            guardCamRight.y = 0f;
            guardCamRight.Normalize();
        }
        else
        {
            guardCamForward = Vector3.forward;
            guardCamRight = Vector3.right;
        }
    }

    /// <summary>
    /// Called by PlayerCombat.EndGuard(). Returns movement control to PlayerMovement.
    /// </summary>
    public void DisableGuard()
    {
        isGuardActive = false;
        cachedHorizontalVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (!isGuardActive) return;

        // Enforce locked rotation every frame
        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);

        // Calculate horizontal velocity using FROZEN camera vectors
        Vector3 inputDir = GetGuardRelativeInput();
        float projection = Vector3.Dot(inputDir, lockedForward);

        if (projection > 0.3f)
            cachedHorizontalVelocity = lockedForward * guardWalkSpeed;
        else if (projection < -0.3f)
            cachedHorizontalVelocity = -lockedForward * guardWalkSpeed;
        else
            cachedHorizontalVelocity = Vector3.zero;

        cachedHorizontalVelocity.y = 0f;
    }

    private void LateUpdate()
    {
        if (!isGuardActive) return;

        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);
    }

    /// <summary>
    /// Input direction using FROZEN camera vectors (captured at guard start).
    /// Camera rotation during guard doesn't flip walk direction.
    /// </summary>
    private Vector3 GetGuardRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
            return Vector3.zero;

        Vector3 dir = guardCamForward * v + guardCamRight * h;
        return dir.normalized;
    }

    /// <summary>
    /// Returns the forward/backward projection of current input onto the locked guard direction.
    /// Used by PlayerCombat.UpdateGuardAnimation() to pick the right anim.
    /// </summary>
    public float GetGuardInputProjection()
    {
        if (!isGuardActive) return 0f;
        Vector3 inputDir = GetGuardRelativeInput();
        return Vector3.Dot(inputDir, lockedForward);
    }

    /// <summary>
    /// Returns the cached horizontal velocity for PlayerMovement to combine with gravity
    /// in a single controller.Move() call.
    /// </summary>
    public Vector3 GetGuardHorizontalVelocity()
    {
        if (!isGuardActive) return Vector3.zero;
        return cachedHorizontalVelocity;
    }

    public bool IsGuardActive() => isGuardActive;
    public Vector3 GetLockedForward() => lockedForward;
}