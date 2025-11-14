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
    private Animator animator;
    
    private Vector3 velocity;
    private bool isGrounded;
    private int jumpCount;
    private bool canMultiJump;
    
    // Cache to avoid redundant animator calls
    private float lastSpeed = -1f;
    private bool lastRunning4Legs;
    private bool lastGrounded = true;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }
    
    private void Start()
    {
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }
    
    private void Update()
    {
        // Inventory check
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsInventoryOpen())
        {
            if (lastSpeed != 0f)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsRunning4Legs", false);
                lastSpeed = 0f;
                lastRunning4Legs = false;
            }
            return;
        }
        
        // Ground check
        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 
            controller.bounds.extents.y + groundCheckDistance, groundLayer);
        
        if (!wasGrounded && isGrounded)
        {
            jumpCount = 0;
            canMultiJump = false;
        }
        
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
        
        // Only update animator when ground state changes
        if (isGrounded != lastGrounded)
        {
            animator.SetBool("IsGrounded", isGrounded);
            lastGrounded = isGrounded;
        }
        
        // Input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool isMoving = (h * h + v * v) > 0.01f;
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
        bool isRunning = isMoving && shiftHeld;
        
        // Jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded && jumpCount == 0)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpCount = 1;
                
                if (isRunning)
                {
                    animator.SetTrigger("Jump4Legs");
                    canMultiJump = true;
                }
                else
                {
                    animator.SetTrigger("Jump2Legs");
                    canMultiJump = false;
                }
            }
            else if (!isGrounded && canMultiJump && jumpCount < 3)
            {
                float power = (jumpCount == 1) ? doubleJumpHeight : tripleJumpHeight;
                velocity.y = Mathf.Sqrt(power * -2f * gravity);
                jumpCount++;
                animator.SetTrigger("Jump4Legs");
            }
        }
        
        // Movement
        if (isMoving)
        {
            Vector3 forward = cameraController != null ? cameraController.GetCameraForward() : transform.forward;
            Vector3 right = cameraController != null ? cameraController.GetCameraRight() : transform.right;
            
            Vector3 moveDir = (forward * v + right * h).normalized;
            float speed = isRunning ? runSpeed : walkSpeed;
            
            controller.Move(moveDir * speed * Time.deltaTime);
            
            if (moveDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, 
                    Quaternion.LookRotation(moveDir), Time.deltaTime * rotationSpeed);
            }
            
            // Only update animator when values change
            float targetSpeed = isRunning ? 2f : 1f;
            if (lastSpeed != targetSpeed)
            {
                animator.SetFloat("Speed", targetSpeed);
                lastSpeed = targetSpeed;
            }
            
            if (lastRunning4Legs != isRunning)
            {
                animator.SetBool("IsRunning4Legs", isRunning);
                lastRunning4Legs = isRunning;
            }
        }
        else
        {
            // Only update when changing to idle
            if (lastSpeed != 0f)
            {
                animator.SetFloat("Speed", 0f);
                lastSpeed = 0f;
            }
            
            if (lastRunning4Legs)
            {
                animator.SetBool("IsRunning4Legs", false);
                lastRunning4Legs = false;
            }
        }
        
        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}