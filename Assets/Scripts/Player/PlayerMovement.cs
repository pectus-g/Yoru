using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotationSpeed = 10f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    [SerializeField] private bool allowDoubleJump = true;
    [SerializeField] private bool allowTripleJump = true;
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Animation Smoothing")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    
    private CharacterController controller;
    private ThirdPersonCamera cameraController;
    private Animator animator;
    private Vector3 velocity;
    private bool isGrounded;
    private int jumpCount = 0;
    private bool wasRunningWhenJumped = false;
    
    // For smooth animation transitions
    private float currentAnimSpeed;
    private float animSpeedVelocity;
    
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        animator = GetComponent<Animator>();
        
        // Disable NavMeshAgent if present
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        
        // Check if animator is properly set up
        if (animator == null)
        {
            Debug.LogError("❌ No Animator component found on Player!");
        }
        else
        {
            Debug.Log("✅ Animator connected successfully!");
        }
    }
    
    private void Update()
    {
        // Block movement if inventory is open
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            // Set idle animation when inventory is open
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsRunning", false);
                animator.SetBool("IsRunning4Legs", false);
            }
            return;
        }
        
        // Ground check
        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 
            groundCheckDistance + 0.1f, groundLayer);
        
        // Handle landing
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
            wasRunningWhenJumped = false;
        }
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            jumpCount = 0;
        }
        
        // Update grounded state
        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
        }
        
        // Check movement and running state
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;
        
        // === JUMP WITH CORRECT TRIGGERS ===
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded && jumpCount == 0)
            {
                Jump(jumpHeight);
                wasRunningWhenJumped = isRunning;
                
                // USE THE CORRECT TRIGGER NAMES!
                if (animator != null)
                {
                    if (isRunning)
                    {
                        animator.SetTrigger("Jump4Legs");  // THIS MATCHES YOUR ANIMATOR
                        animator.SetBool("JumpingOn4Legs", true);
                        Debug.Log("🐾 Jump4Legs triggered!");
                    }
                    else
                    {
                        animator.SetTrigger("Jump2Legs");  // THIS MATCHES YOUR ANIMATOR
                        animator.SetBool("JumpingOn4Legs", false);
                        Debug.Log("🚶 Jump2Legs triggered!");
                    }
                }
            }
            else if (allowDoubleJump && jumpCount == 1)
            {
                Jump(doubleJumpHeight);
                if (animator != null)
                {
                    if (wasRunningWhenJumped)
                        animator.SetTrigger("Jump4Legs");
                    else
                        animator.SetTrigger("Jump2Legs");
                }
            }
            else if (allowTripleJump && jumpCount == 2)
            {
                Jump(tripleJumpHeight);
                if (animator != null)
                {
                    if (wasRunningWhenJumped)
                        animator.SetTrigger("Jump4Legs");
                    else
                        animator.SetTrigger("Jump2Legs");
                }
            }
        }
        
        // === MOVEMENT WITH CORRECT PARAMETERS ===
        if (isMoving)
        {
            Vector3 cameraForward = cameraController != null ? 
                cameraController.GetCameraForward() : transform.forward;
            Vector3 cameraRight = cameraController != null ? 
                cameraController.GetCameraRight() : transform.right;
            
            Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
            
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            
            controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            
            // Rotate character to face movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                    Time.deltaTime * rotationSpeed);
            }
            
            // Update animation based on movement
            if (animator != null)
            {
                if (isRunning)
                {
                    // Running on 4 legs
                    currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, 2f, 
                        ref animSpeedVelocity, animationSmoothTime);
                    animator.SetFloat("Speed", currentAnimSpeed);
                    animator.SetBool("IsRunning", true);
                    animator.SetBool("IsRunning4Legs", true);  // THIS WAS MISSING!
                }
                else
                {
                    // Walking on 2 legs
                    currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, 1f, 
                        ref animSpeedVelocity, animationSmoothTime);
                    animator.SetFloat("Speed", currentAnimSpeed);
                    animator.SetBool("IsRunning", false);
                    animator.SetBool("IsRunning4Legs", false);
                }
            }
        }
        else
        {
            // Idle animation
            if (animator != null)
            {
                currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, 0f, 
                    ref animSpeedVelocity, animationSmoothTime);
                animator.SetFloat("Speed", currentAnimSpeed);
                animator.SetBool("IsRunning", false);
                animator.SetBool("IsRunning4Legs", false);
            }
        }
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    private void OnLanded()
    {
        Debug.Log("🦶 Player landed!");
    }
    
    private void Jump(float height)
    {
        velocity.y = Mathf.Sqrt(height * 2f * -gravity);
        jumpCount++;
        Debug.Log($"🚀 Jumping! Count: {jumpCount}");
    }
    
    // Public methods for other scripts
    public bool IsMoving()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        return Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
    }
    
    public bool IsRunning()
    {
        return IsMoving() && Input.GetKey(KeyCode.LeftShift);
    }
    
    public bool IsInAir()
    {
        return !isGrounded;
    }
    
    public float GetCurrentSpeed()
    {
        return currentAnimSpeed;
    }
    
    // Debug display
    void OnGUI()
    {
        if (animator != null)
        {
            GUI.Box(new Rect(10, 10, 250, 140), "Animation Debug");
            GUI.Label(new Rect(15, 30, 240, 20), $"Speed: {animator.GetFloat("Speed"):F2}");
            GUI.Label(new Rect(15, 50, 240, 20), $"IsRunning: {animator.GetBool("IsRunning")}");
            GUI.Label(new Rect(15, 70, 240, 20), $"IsRunning4Legs: {animator.GetBool("IsRunning4Legs")}");
            GUI.Label(new Rect(15, 90, 240, 20), $"IsGrounded: {animator.GetBool("IsGrounded")}");
            GUI.Label(new Rect(15, 110, 240, 20), $"JumpingOn4Legs: {animator.GetBool("JumpingOn4Legs")}");
        }
    }
}