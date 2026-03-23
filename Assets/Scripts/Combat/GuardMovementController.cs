using UnityEngine;

/// <summary>
/// YORU Guard Movement Controller — Phase 3C
/// Handles all movement during Q guard. Exists as a separate script because
/// guard movement rules are fundamentally different from normal PlayerMovement:
///   - Facing direction locks when Q is pressed (no rotation at all)
///   - W = forward along locked direction, S = backward
///   - A/D ignored (no strafe during guard)
///   - No camera-relative rotation
///   - Normal S-key face-camera behavior disabled
///   - Gravity applied via CharacterController.Move() to prevent floating/sinking
///
/// PlayerMovement.FixedUpdate skips during guard (same pattern as dodge/dash).
/// This script owns ALL movement while guard is active.
/// </summary>
public class GuardMovementController : MonoBehaviour
{
    [Header("Guard Movement")]
    [SerializeField] private float guardWalkSpeed = 0.75f;

    [Header("Physics")]
    [SerializeField] private float gravity = -15f;

    private CharacterController controller;
    private Transform cachedTransform;

    private bool isGuardActive;
    private Vector3 lockedForward;  // direction Yoru was facing when Q was pressed
    private Vector3 lockedRight;    // perpendicular (unused for movement, kept for reference)
    private float verticalVelocity; // gravity accumulator

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cachedTransform = transform;
    }

    /// <summary>
    /// Called by PlayerCombat.StartGuard(). Locks facing direction and enables guard movement.
    /// </summary>
    public void EnableGuard(Vector3 forwardDirection)
    {
        isGuardActive = true;

        // Lock the facing direction — Yoru will NOT rotate during guard
        lockedForward = forwardDirection;
        lockedForward.y = 0f;
        lockedForward.Normalize();

        lockedRight = Vector3.Cross(Vector3.up, lockedForward);

        // Reset vertical velocity — don't carry momentum into guard
        verticalVelocity = controller != null && controller.isGrounded ? -2f : 0f;
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

        // Enforce locked rotation every frame — NO rotation during guard
        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);

        // Read W/S input (A/D ignored per design)
        float v = Input.GetAxisRaw("Vertical");

        // Movement along locked direction only
        Vector3 move = Vector3.zero;
        if (v > 0.1f)
            move = lockedForward * guardWalkSpeed;
        else if (v < -0.1f)
            move = -lockedForward * guardWalkSpeed;

        // Apply gravity — THIS prevents foot-underground and floating
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // Small downward force keeps grounded
        else
            verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;

        // Move via CharacterController (respects collisions, slopes, grounding)
        controller.Move(move * Time.deltaTime);
    }

    /// <summary>
    /// Called by PlayerMovement.LateUpdate() or similar — enforces locked rotation.
    /// Also called here in LateUpdate as a safety net.
    /// </summary>
    private void LateUpdate()
    {
        if (!isGuardActive) return;

        // Double-enforce rotation lock — nothing should rotate Yoru during guard
        if (lockedForward.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.LookRotation(lockedForward);
    }

    public bool IsGuardActive() => isGuardActive;
}