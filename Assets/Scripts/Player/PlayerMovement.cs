using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 8f;
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer = ~0;
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private Animator animator; // ← THIS WAS MISSING!
    
    private Vector3 velocity;
    private bool isGrounded;
    private int jumpCount = 0;
    private int maxJumps = 1;
    
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        animator = GetComponent<Animator>(); // ← THIS WAS MISSING!
        
        if (animator == null)
        {
            Debug.LogError("❌ NO ANIMATOR COMPONENT FOUND!");
        }
        else
        {
            Debug.Log("✅ Animator found and connected!");
        }
        
        // Disable NavMeshAgent if present
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
    
    private void Update()
    {
        // Skip if inventory open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsRunning4Legs", false);
            }
            return;
        }
        
        // === GROUND CHECK ===
        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(
            transform.position, 
            Vector3.down, 
            controller.bounds.extents.y + groundCheckDistance, 
            groundLayer
        );
        
        // Reset jump when landing
        if (!wasGrounded && isGrounded)
        {
            jumpCount = 0;
            maxJumps = 1;
            Debug.Log("✅ LANDED!");
        }
        
        // Apply small downward force when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        // Update animator grounded state
        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
        }
        
        // === INPUT ===
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = (h * h + v * v) > 0.01f;
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift);
        bool isRunning = isMoving && wantsToRun;
        
        // === JUMP SYSTEM ===
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // First jump - determine type
            if (isGrounded && jumpCount == 0)
            {
                PerformJump(jumpHeight);
                
                if (animator != null)
                {
                    if (isRunning)
                    {
                        // 4-leg jump - allow triple jump
                        animator.SetTrigger("Jump4Legs");
                        maxJumps = 3;
                        Debug.Log("🐾 4-LEG JUMP!");
                    }
                    else
                    {
                        // 2-leg jump - single jump only
                        animator.SetTrigger("Jump2Legs");
                        maxJumps = 1;
                        Debug.Log("🚶 2-LEG JUMP!");
                    }
                }
            }
            // Multi-jump (only for 4-leg jumps)
            else if (!isGrounded && jumpCount < maxJumps)
            {
                if (maxJumps == 3)
                {
                    if (jumpCount == 1)
                    {
                        PerformJump(doubleJumpHeight);
                        if (animator != null) animator.SetTrigger("Jump4Legs");
                        Debug.Log("⬆️ DOUBLE JUMP!");
                    }
                    else if (jumpCount == 2)
                    {
                        PerformJump(tripleJumpHeight);
                        if (animator != null) animator.SetTrigger("Jump4Legs");
                        Debug.Log("⬆️⬆️ TRIPLE JUMP!");
                    }
                }
            }
        }
        
        // === MOVEMENT ===
        if (isMoving)
        {
            // Get camera directions
            Vector3 camForward = cameraController != null ? 
                cameraController.GetCameraForward() : transform.forward;
            Vector3 camRight = cameraController != null ? 
                cameraController.GetCameraRight() : transform.right;
            
            // Calculate movement direction
            Vector3 moveDir = (camForward * v + camRight * h).normalized;
            
            // Determine speed
            float moveSpeed = isRunning ? runSpeed : walkSpeed;
            
            // Move the character
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
            
            // Rotate character
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * rotationSpeed
                );
            }
            
            // === UPDATE ANIMATOR ===
            if (animator != null)
            {
                animator.SetFloat("Speed", isRunning ? 2f : 1f);
                animator.SetBool("IsRunning4Legs", isRunning);
            }
        }
        else
        {
            // === IDLE ===
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsRunning4Legs", false);
            }
        }
        
        // === APPLY GRAVITY ===
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    private void PerformJump(float height)
    {
        velocity.y = Mathf.Sqrt(height * -2f * gravity);
        jumpCount++;
    }
    
    // === DEBUG DISPLAY ===
    private void OnGUI()
    {
        if (animator == null) return;
        
        GUI.Box(new Rect(10, 10, 340, 220), "🐱 Cat Debug");
        GUI.Label(new Rect(20, 35, 320, 20), $"Speed: {animator.GetFloat("Speed"):F2}");
        GUI.Label(new Rect(20, 55, 320, 20), $"IsRunning4Legs: {animator.GetBool("IsRunning4Legs")}");
        GUI.Label(new Rect(20, 75, 320, 20), $"IsGrounded: {animator.GetBool("IsGrounded")}");
        GUI.Label(new Rect(20, 95, 320, 20), $"Jump: {jumpCount}/{maxJumps}");
        GUI.Label(new Rect(20, 115, 320, 20), $"Type: {(maxJumps == 3 ? "4-LEGS" : "2-LEGS")}");
        GUI.Label(new Rect(20, 135, 320, 20), $"Grounded: {isGrounded}");
        GUI.Label(new Rect(20, 155, 320, 20), $"Velocity Y: {velocity.y:F2}");
        GUI.Label(new Rect(20, 175, 320, 20), $"Shift: {Input.GetKey(KeyCode.LeftShift)}");
        GUI.Label(new Rect(20, 195, 320, 20), $"Moving: {(Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)}");
    }
    
    private void OnDrawGizmos()
    {
        if (controller == null) return;
        
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * (controller.bounds.extents.y + groundCheckDistance);
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.15f);
    }
}