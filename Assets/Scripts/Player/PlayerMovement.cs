using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float speedDampTime = 0.2f; // Smooth animation blending
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    [SerializeField] private float jumpForwardSpeed = 3f; // Forward momentum during jump
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private Animator animator;
    
    private Vector3 velocity;
    private Vector3 jumpMomentum; // Stores horizontal movement during jump
    private bool isGrounded;
    private int jumpCount = 0;
    private bool canMultiJump = false;
    
    // Animation state
    private float currentAnimSpeed;
    private float animSpeedVelocity; // For SmoothDamp
    private bool isJumping = false;
    private float jumpTimer = 0f;
    
    // Input caching for smoother movement
    private float smoothedHorizontal;
    private float smoothedVertical;
    private float inputSmoothVelocityX;
    private float inputSmoothVelocityY;
    
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }
    
    void Start()
    {
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
    
    void Update()
    {
        // Check inventory
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            UpdateAnimationSpeed(0f);
            return;
        }
        
        // Ground check
        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        
        // Landing detection
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
        
        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpCount = 0;
        }
        
        // Get raw input
        float rawH = Input.GetAxis("Horizontal");
        float rawV = Input.GetAxis("Vertical");
        
        // Smooth the input for better animation blending
        smoothedHorizontal = Mathf.SmoothDamp(smoothedHorizontal, rawH, ref inputSmoothVelocityX, 0.1f);
        smoothedVertical = Mathf.SmoothDamp(smoothedVertical, rawV, ref inputSmoothVelocityY, 0.1f);
        
        bool isMoving = Mathf.Abs(smoothedHorizontal) > 0.01f || Mathf.Abs(smoothedVertical) > 0.01f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);
        
        // Check if we're in a jump animation
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        isJumping = stateInfo.IsName("JumpWith2Legs") || stateInfo.IsName("JumpWith4Legs");
        
        // Track jump animation time
        if (isJumping)
        {
            jumpTimer += Time.deltaTime;
            if (jumpTimer > 1.5f) // Force exit if stuck
            {
                animator.Play("Locomotion", 0, 0f);
                isJumping = false;
                jumpTimer = 0f;
            }
        }
        else
        {
            jumpTimer = 0f;
        }
        
        // Calculate movement direction
        Vector3 moveDirection = Vector3.zero;
        if (cameraController != null)
        {
            Vector3 forward = cameraController.GetCameraForward();
            Vector3 right = cameraController.GetCameraRight();
            moveDirection = (forward * smoothedVertical + right * smoothedHorizontal).normalized;
        }
        else
        {
            moveDirection = (transform.forward * smoothedVertical + transform.right * smoothedHorizontal).normalized;
        }
        
        // Handle jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
            {
                // Store movement direction for forward jump
                jumpMomentum = moveDirection * (isRunning ? jumpForwardSpeed * 1.5f : jumpForwardSpeed);
                
                // Apply vertical jump force
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpCount = 1;
                
                // Clear animation states
                animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime); // Quick blend to 0
                animator.ResetTrigger("Jump2Legs");
                animator.ResetTrigger("Jump4Legs");
                
                if (isRunning)
                {
                    canMultiJump = true;
                    animator.Play("JumpWith4Legs", 0, 0f);
                    Debug.Log("🐾 4-LEG JUMP with forward momentum");
                }
                else
                {
                    canMultiJump = false;
                    animator.Play("JumpWith2Legs", 0, 0f);
                    Debug.Log("🚶 2-LEG JUMP with forward momentum");
                }
            }
            else if (!isGrounded && canMultiJump && jumpCount < 3)
            {
                // Multi-jump
                jumpMomentum = moveDirection * jumpForwardSpeed * 1.2f; // Slight forward boost
                float power = (jumpCount == 1) ? doubleJumpHeight : tripleJumpHeight;
                velocity.y = Mathf.Sqrt(power * -2f * gravity);
                jumpCount++;
                
                animator.Play("JumpWith4Legs", 0, 0f);
                Debug.Log($"⬆️ MULTI-JUMP #{jumpCount}");
            }
        }
        
        // Apply movement
        Vector3 horizontalMovement = Vector3.zero;
        
        if (isGrounded && !isJumping)
        {
            // Normal ground movement
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            horizontalMovement = moveDirection * currentSpeed * Time.deltaTime;
            jumpMomentum = Vector3.zero; // Clear jump momentum when grounded
        }
        else if (!isGrounded)
        {
            // Air movement - use jump momentum
            horizontalMovement = jumpMomentum * Time.deltaTime;
        }
        
        // Apply horizontal movement
        if (horizontalMovement.magnitude > 0.001f)
        {
            controller.Move(horizontalMovement);
            
            // Rotate to face movement direction (even in air)
            if (!isJumping || !isGrounded)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection != Vector3.zero ? moveDirection : transform.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        
        // Update animator with smooth speed transitions
        UpdateAnimatorSmooth(isMoving, isRunning);
        animator.SetBool("IsGrounded", isGrounded);
    }
    
    private void OnLanded()
    {
        Debug.Log("✅ LANDED!");
        jumpCount = 0;
        canMultiJump = false;
        jumpMomentum = Vector3.zero;
        jumpTimer = 0f;
        
        // Force animation reset
        if (isJumping)
        {
            animator.Play("Locomotion", 0, 0f);
        }
    }
    
    private void UpdateAnimatorSmooth(bool isMoving, bool isRunning)
    {
        // Don't update speed during jump animations
        if (isJumping) return;
        
        // Calculate target speed based on movement
        float targetSpeed = 0f;
        
        if (isMoving)
        {
            // Use the magnitude of smoothed input for even smoother transitions
            float inputMagnitude = Mathf.Sqrt(smoothedHorizontal * smoothedHorizontal + smoothedVertical * smoothedVertical);
            inputMagnitude = Mathf.Clamp01(inputMagnitude);
            
            if (isRunning)
            {
                targetSpeed = Mathf.Lerp(1.5f, 2f, inputMagnitude); // Blend between fast walk and run
            }
            else
            {
                targetSpeed = Mathf.Lerp(0.5f, 1f, inputMagnitude); // Blend between slow and normal walk
            }
        }
        
        // Use SmoothDamp for very smooth transitions
        currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, targetSpeed, ref animSpeedVelocity, speedDampTime);
        
        // Apply to animator
        animator.SetFloat("Speed", currentAnimSpeed);
    }
    
    private void UpdateAnimationSpeed(float target)
    {
        currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, target, ref animSpeedVelocity, speedDampTime);
        animator.SetFloat("Speed", currentAnimSpeed);
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || controller == null) return;
        
        // Ground indicator
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position - Vector3.up * (controller.height / 2f), 0.3f);
        
        // Jump momentum vector
        if (jumpMomentum.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, jumpMomentum);
        }
    }
}