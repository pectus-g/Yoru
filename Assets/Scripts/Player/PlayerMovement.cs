using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float speedDampTime = 0.25f; // Smooth animation blending
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    [SerializeField] private float jumpForwardSpeed = 4f; // Forward momentum during jump
    [SerializeField] private float landingBlendTime = 0.3f; // Smooth landing transition
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private Animator animator;
    
    private Vector3 velocity;
    private Vector3 jumpMomentum; // Stores horizontal movement during jump
    private bool isGrounded;
    private bool wasGrounded;
    private int jumpCount = 0;
    private bool canMultiJump = false;
    
    // Animation state
    private float currentAnimSpeed;
    private float animSpeedVelocity; // For SmoothDamp
    private bool isJumping = false;
    private float jumpTimer = 0f;
    private float landingTimer = 0f;
    private bool isLanding = false;
    
    // Movement state tracking
    private Vector3 lastMoveDirection;
    private bool wasMovingBeforeJump = false;
    private float lastGroundSpeed = 0f;
    
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
            currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, 0f, ref animSpeedVelocity, speedDampTime);
            animator.SetFloat("Speed", currentAnimSpeed);
            return;
        }
        
        // Ground check
        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;
        
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);
        
        // Calculate movement direction
        Vector3 moveDirection = Vector3.zero;
        if (isMoving)
        {
            if (cameraController != null)
            {
                Vector3 forward = cameraController.GetCameraForward();
                Vector3 right = cameraController.GetCameraRight();
                moveDirection = (forward * vertical + right * horizontal).normalized;
            }
            else
            {
                moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;
            }
            lastMoveDirection = moveDirection; // Store for jump direction
        }
        
        // Check animation state
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool wasJumping = isJumping;
        isJumping = stateInfo.IsName("JumpWith2Legs") || stateInfo.IsName("JumpWith4Legs");
        
        // Landing detection
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
        
        // Detect when jumping animation ends in air (your 2-leg jump issue)
        if (wasJumping && !isJumping && !isGrounded)
        {
            // Force back to jump animation if still in air
            if (jumpCount == 1 && !canMultiJump)
            {
                animator.CrossFade("JumpWith2Legs", 0.1f, 0, 0.9f); // Start near end
            }
            else if (canMultiJump)
            {
                animator.CrossFade("JumpWith4Legs", 0.1f, 0, 0.9f);
            }
        }
        
        // Handle landing transition
        if (isLanding)
        {
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0)
            {
                isLanding = false;
            }
        }
        
        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            if (!isLanding) jumpCount = 0;
        }
        
        // Jump animation timeout
        if (isJumping)
        {
            jumpTimer += Time.deltaTime;
            if (jumpTimer > 2f) // Safety timeout
            {
                animator.CrossFade("Locomotion", 0.2f);
                isJumping = false;
                jumpTimer = 0f;
            }
        }
        else
        {
            jumpTimer = 0f;
        }
        
        // Handle jump input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded && jumpCount == 0)
            {
                // Store state before jump
                wasMovingBeforeJump = isMoving;
                lastGroundSpeed = currentAnimSpeed;
                
                // Calculate jump momentum - FIXED for first 4-leg jump
                if (isMoving)
                {
                    // Use actual movement direction for forward jump
                    jumpMomentum = moveDirection * (isRunning ? jumpForwardSpeed * 1.5f : jumpForwardSpeed);
                }
                else
                {
                    // Standing jump - minimal forward movement
                    jumpMomentum = transform.forward * 0.5f;
                }
                
                // Apply vertical jump force
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpCount = 1;
                
                // Start jump animation
                if (isRunning)
                {
                    canMultiJump = true;
                    animator.CrossFade("JumpWith4Legs", 0.05f);
                    Debug.Log("🐾 4-LEG JUMP with forward momentum");
                }
                else
                {
                    canMultiJump = false;
                    animator.CrossFade("JumpWith2Legs", 0.05f);
                    Debug.Log("🚶 2-LEG JUMP");
                }
            }
            else if (!isGrounded && canMultiJump && jumpCount < 3)
            {
                // Multi-jump - maintain or add momentum
                if (isMoving)
                {
                    jumpMomentum = moveDirection * jumpForwardSpeed * 1.2f;
                }
                
                float power = (jumpCount == 1) ? doubleJumpHeight : tripleJumpHeight;
                velocity.y = Mathf.Sqrt(power * -2f * gravity);
                jumpCount++;
                
                animator.CrossFade("JumpWith4Legs", 0.05f);
                Debug.Log($"⬆️ MULTI-JUMP #{jumpCount}");
            }
        }
        
        // Apply movement
        Vector3 horizontalMovement = Vector3.zero;
        
        if (isGrounded && !isJumping && !isLanding)
        {
            // Normal ground movement
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            horizontalMovement = moveDirection * currentSpeed * Time.deltaTime;
            
            // Clear jump momentum gradually when grounded
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, Time.deltaTime * 5f);
        }
        else if (!isGrounded || isLanding)
        {
            // Air movement - use jump momentum
            horizontalMovement = jumpMomentum * Time.deltaTime;
        }
        
        // Apply horizontal movement
        if (horizontalMovement.magnitude > 0.001f)
        {
            controller.Move(horizontalMovement);
            
            // Rotate to face movement direction
            if (moveDirection.magnitude > 0.01f && (!isJumping || !isGrounded))
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        
        // Update animator - ALWAYS update to prevent freezing
        UpdateAnimatorAlways(isMoving, isRunning);
        animator.SetBool("IsGrounded", isGrounded);
    }
    
    private void OnLanded()
    {
        Debug.Log("✅ LANDED!");
        
        // Start landing transition
        isLanding = true;
        landingTimer = landingBlendTime;
        
        // Smoothly blend back to locomotion
        if (isJumping)
        {
            // Use CrossFade for smooth transition instead of Play
            animator.CrossFade("Locomotion", landingBlendTime);
        }
        
        // Restore movement speed if was moving before jump
        if (wasMovingBeforeJump)
        {
            currentAnimSpeed = lastGroundSpeed * 0.7f; // Start at 70% to blend up
        }
        
        // Clear jump state after landing
        jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, 0.5f); // Gradual stop
        jumpTimer = 0f;
    }
    
    private void UpdateAnimatorAlways(bool isMoving, bool isRunning)
    {
        // ALWAYS update speed, even during jumps, to prevent freezing
        float targetSpeed = 0f;
        
        // During landing, blend back to movement
        if (isLanding && wasMovingBeforeJump)
        {
            targetSpeed = lastGroundSpeed;
        }
        // During jump, maintain some speed value for blend
        else if (isJumping)
        {
            // Keep a small speed value to prevent freeze
            targetSpeed = currentAnimSpeed * 0.9f; // Gradually reduce but don't zero
        }
        // Normal ground movement
        else if (isMoving && isGrounded && !isJumping)
        {
            targetSpeed = isRunning ? 2f : 1f;
        }
        
        // ALWAYS smooth the speed, never set directly to prevent freezing
        currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, targetSpeed, ref animSpeedVelocity, speedDampTime);
        
        // Ensure we never get stuck at exactly 0 (idle freeze fix)
        if (currentAnimSpeed < 0.01f && targetSpeed == 0f)
        {
            currentAnimSpeed = 0f; // Clean zero for idle
        }
        
        // Always update the animator
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
        
        // Landing state
        if (isLanding)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}