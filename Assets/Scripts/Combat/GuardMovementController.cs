using UnityEngine;

/// <summary>
/// YORU Guard Movement Controller — Phase 3C v4
/// Handles horizontal movement calculation and rotation during Q guard.
/// Gravity AND the single controller.Move() are handled by PlayerMovement.FixedUpdate.
///
/// v4 CHANGE (feet underground fix):
///   v3 called controller.Move() in Update for horizontal, while PlayerMovement called
///   controller.Move() in FixedUpdate for gravity. Two Move calls per frame caused
///   CharacterController grounded state to oscillate → feet clipping underground + landing VFX spam.
///   v4 NEVER calls controller.Move(). It caches the desired horizontal velocity, and
///   PlayerMovement reads it via GetGuardHorizontalVelocity() to combine with gravity
///   into a SINGLE controller.Move() call.
///
/// Key design:
///   - Facing direction locks to MOVEMENT DIRECTION at time Q is pressed
///     (not transform.forward — if player was pressing D, guard faces right)
///   - Input is projected onto locked direction via dot product
///     (if locked from D, then D=forward, A=backward, W/S=ignored)
///   - No rotation during guard
///   - Horizontal velocity calculated in Update (frame-rate input), consumed in FixedUpdate (physics)
/// </summary>
public class GuardMovementController : MonoBehaviour
{
    [Header("Guard Movement")]
    [SerializeField] private float guardWalkSpeed = 0.75f;

    private Transform cachedTransform;
    private Camera mainCamera;

    private bool isGuardActive;
    private Vector3 lockedForward;
    private Vector3 cachedHorizontalVelocity; // calculated in Update, read by PlayerMovement in FixedUpdate

    private void Awake()
    {
        cachedTransform = transform;
    }

    /// <summary>
    /// Called by PlayerCombat.StartGuard(). Locks facing direction and enables guard movement.
    /// forwardDirection is the camera-relative movement direction at the moment Q was pressed.
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

        // Calculate horizontal velocity from input — cached for PlayerMovement to read
        Vector3 inputDir = GetCameraRelativeInput();
        float projection = Vector3.Dot(inputDir, lockedForward);

        if (projection > 0.3f)
            cachedHorizontalVelocity = lockedForward * guardWalkSpeed;
        else if (projection < -0.3f)
            cachedHorizontalVelocity = -lockedForward * guardWalkSpeed;
        else
            cachedHorizontalVelocity = Vector3.zero;

        // Ensure no vertical component ever leaks in
        cachedHorizontalVelocity.y = 0f;
    }

    private void LateUpdate()
    {
        if (!isGuardActive) return;

        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);
    }

    /// <summary>
    /// Compute camera-relative direction from raw WASD input.
    /// Returns Vector3.zero if no input.
    /// </summary>
    private Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
            return Vector3.zero;

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

        Vector3 dir = camForward * v + camRight * h;
        return dir.normalized;
    }

    /// <summary>
    /// Returns the forward/backward projection of current input onto the locked guard direction.
    /// Used by PlayerCombat.UpdateGuardAnimation() to pick the right anim.
    /// </summary>
    public float GetGuardInputProjection()
    {
        if (!isGuardActive) return 0f;
        Vector3 inputDir = GetCameraRelativeInput();
        return Vector3.Dot(inputDir, lockedForward);
    }

    /// <summary>
    /// Returns the cached horizontal velocity for PlayerMovement to combine with gravity
    /// in a single controller.Move() call. NEVER call controller.Move() from this script.
    /// </summary>
    public Vector3 GetGuardHorizontalVelocity()
    {
        if (!isGuardActive) return Vector3.zero;
        return cachedHorizontalVelocity;
    }

    public bool IsGuardActive() => isGuardActive;
    public Vector3 GetLockedForward() => lockedForward;
}