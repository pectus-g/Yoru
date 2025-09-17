using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 8f;
    public float rotationSpeed = 20f;

    [Header("Gravity")]
    public float gravity = -20f;       // must be negative
    public float groundedGravity = -2f;

    [Header("Jump")]
    public float jumpHeight = 2.0f;        // ground jump height (m)
    public float doubleJumpHeight = 1.6f;  // air jump height (m)
    public int extraAirJumps = 1;          // 1 = double jump

    CharacterController controller;
    UnityEngine.AI.NavMeshAgent agent;
    Vector3 velocity;          // we only use Y, but keep vector
    int airJumpsUsed = 0;

    // Track grounded transition to detect LANDING only
    bool wasGrounded = false;

    void Awake() {
        controller = GetComponent<CharacterController>()
                  ?? GetComponentInChildren<CharacterController>()
                  ?? GetComponentInParent<CharacterController>();

        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        Debug.Log($"[PlayerMovement] on {name}  controller:{(controller!=null)}  agent:{(agent!=null)}", this);
    }
void Start()
{
    // drop a ray from slightly above and place the player on hit point
    if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out var hit, 10f, ~0))
        transform.position = hit.point;
}

    void Update()
    {
         if (!controller)
        {
            Debug.LogError($"[PlayerMovement] CharacterController missing on {name}", this);
            return; // prevents the NullReferenceException
        }

        // If you keep NavMeshAgent, don't drive movement with both at once:
        if (agent && agent.enabled) return; // let the agent move it, not the controller

        // --- Input (WASD/Arrow keys)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v).normalized;

        // --- Camera-relative direction
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight   = Camera.main.transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 move = camForward * input.y + camRight * input.x;

        // --- Rotate toward movement
        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // ---------------------------
        // Grounding & Landing logic
        // ---------------------------
        // Treat as "actually grounded" only if controller says grounded AND we're not moving upward
        bool isActuallyGrounded = controller.isGrounded && velocity.y <= 0.05f;

        // LANDING detection: transition from not grounded to grounded
        if (!wasGrounded && isActuallyGrounded)
        {
            airJumpsUsed = 0;                    // reset air jumps only on landing
            if (velocity.y < 0f) velocity.y = groundedGravity; // stick to ground
        }

        // While on ground, keep a small downward force so we don't float
        if (isActuallyGrounded && velocity.y < 0f)
            velocity.y = groundedGravity;

        // ---------------------------
        // Jump / Double Jump
        // ---------------------------
        if (Input.GetButtonDown("Jump")) // Space by default in old Input System
        {
            if (isActuallyGrounded)
            {
                // Ground jump
                velocity.y = CalcJumpVelocity(jumpHeight);
            }
            else if (airJumpsUsed < extraAirJumps)
            {
                // Air jump(s)
                airJumpsUsed++;

                // Clear downward momentum so the air jump is snappy
                if (velocity.y < 0f) velocity.y = 0f;

                float hJump = (airJumpsUsed == 1) ? doubleJumpHeight : Mathf.Max(0.5f, doubleJumpHeight * 0.85f);
                velocity.y = CalcJumpVelocity(hJump);
            }
        }

        // ---------------------------
        // Horizontal + Vertical move
        // ---------------------------
        // Apply gravity AFTER jump decisions
        velocity.y += gravity * Time.deltaTime;

        // Move once with combined motion (reduces grounding weirdness)
        Vector3 motion = move * moveSpeed;
        motion.y = velocity.y;               // vertical from gravity/jumps
        controller.Move(motion * Time.deltaTime);

        // Update "wasGrounded" at end of frame
        wasGrounded = isActuallyGrounded;
    }

    // v0 = sqrt(2 * -g * h)
    float CalcJumpVelocity(float height)
    {
        return Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0.01f, height));
    }

    // Optional: enforce sane defaults if the Inspector zeroed anything
    void OnValidate()
    {
        if (gravity >= 0f) gravity = -20f;
        if (groundedGravity >= 0f) groundedGravity = -2f;
        if (jumpHeight <= 0f) jumpHeight = 2.0f;
        if (doubleJumpHeight <= 0f) doubleJumpHeight = 1.6f;
        if (extraAirJumps < 1) extraAirJumps = 1;
    }
}
