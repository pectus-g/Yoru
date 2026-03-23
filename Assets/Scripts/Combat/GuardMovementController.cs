using UnityEngine;

/// <summary>
/// YORU Guard Movement Controller — Phase 3C v3
/// Handles horizontal movement and rotation during Q guard.
/// Gravity is handled by PlayerMovement.FixedUpdate (never skipped during guard).
///
/// Key design:
///   - Facing direction locks to MOVEMENT DIRECTION at time Q is pressed
///     (not transform.forward — if player was pressing D, guard faces right)
///   - Input is projected onto locked direction via dot product
///     (if locked from D, then D=forward, A=backward, W/S=ignored)
///   - No rotation during guard
///   - Horizontal movement only — gravity lives in PlayerMovement
/// </summary>
public class GuardMovementController : MonoBehaviour
{
    [Header("Guard Movement")]
    [SerializeField] private float guardWalkSpeed = 0.75f;

    private CharacterController controller;
    private Transform cachedTransform;
    private Camera mainCamera;

    private bool isGuardActive;
    private Vector3 lockedForward;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cachedTransform = transform;
    }

    /// <summary>
    /// Called by PlayerCombat.StartGuard(). Locks facing direction and enables guard movement.
    /// forwardDirection is the camera-relative movement direction at the moment Q was pressed.
    /// </summary>
    public void EnableGuard(Vector3 forwardDirection)
    {
        isGuardActive = true;

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
    }

    private void Update()
    {
        if (!isGuardActive || controller == null) return;

        // Enforce locked rotation every frame
        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);

        // Get camera-relative input direction from current WASD
        Vector3 inputDir = GetCameraRelativeInput();

        // Project input onto locked direction — positive = forward, negative = backward
        float projection = Vector3.Dot(inputDir, lockedForward);

        Vector3 move = Vector3.zero;
        if (projection > 0.3f)
            move = lockedForward * guardWalkSpeed;
        else if (projection < -0.3f)
            move = -lockedForward * guardWalkSpeed;

        // Horizontal movement only — no vertical component
        // Gravity is handled by PlayerMovement.FixedUpdate
        move.y = 0f;

        if (move.sqrMagnitude > 0.001f)
            controller.Move(move * Time.deltaTime);
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

    public bool IsGuardActive() => isGuardActive;
    public Vector3 GetLockedForward() => lockedForward;
}