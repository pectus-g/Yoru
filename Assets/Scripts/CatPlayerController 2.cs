using UnityEngine;
using UnityEngine.AI;
using Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class CatPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float rotationSpeed = 720f;
    public float inputDeadzone = 0.1f;
    
    [Header("Jump Settings")]
    public float jumpHeight = 2.5f;
    public float doubleJumpHeight = 2.0f;
    public float gravity = -20f;
    public float groundedGravity = -2f;
    
    [Header("NavMesh Click-to-Move")]
    public bool enableClickToMove = true;
    public KeyCode clickToMoveButton = KeyCode.Mouse0;
    public KeyCode clickToMoveModifier = KeyCode.LeftControl;
    public LayerMask groundLayerMask = -1;
    public float clickRayDistance = 100f;
    
    [Header("Ground Detection")]
    public LayerMask groundLayers = -1;
    public float groundCheckRadius = 0.3f;
    public float groundCheckDistance = 0.1f;
    
    [Header("Camera & Audio")]
    public CinemachineFreeLook freeLookCamera;
    public Transform cameraFollowTarget;
    
    // Components
    private CharacterController characterController;
    private NavMeshAgent navMeshAgent;
    private Animator catAnimator;
    
    // Movement state
    private Vector3 velocity;
    private bool isGrounded;
    private bool hasDoubleJumped;
    private bool isMoving;
    private bool isRunning;
    
    // Input state
    private Vector2 movementInput;
    private bool jumpInput;
    private bool runInput;
    
    // NavMesh state
    private bool isUsingNavMesh;
    
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        catAnimator = GetComponentInChildren<Animator>();
        
        SetupNavMeshAgent();
        SetupCamera();
    }
    
    void SetupNavMeshAgent()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
            navMeshAgent.speed = runSpeed;
            navMeshAgent.acceleration = 15f;
            navMeshAgent.angularSpeed = 360f;
            navMeshAgent.stoppingDistance = 0.5f;
        }
    }
    
    void SetupCamera()
    {
        if (freeLookCamera == null)
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();
            
        if (freeLookCamera != null && cameraFollowTarget != null)
        {
            freeLookCamera.Follow = cameraFollowTarget;
            freeLookCamera.LookAt = cameraFollowTarget;
            
            // Smooth camera settings
            freeLookCamera.m_XAxis.m_MaxSpeed = 200f;
            freeLookCamera.m_YAxis.m_MaxSpeed = 2f;
            freeLookCamera.m_XAxis.m_AccelTime = 0.1f;
            freeLookCamera.m_XAxis.m_DecelTime = 0.1f;
            freeLookCamera.m_YAxis.m_AccelTime = 0.1f;
            freeLookCamera.m_YAxis.m_DecelTime = 0.1f;
        }
    }
    
    void Update()
    {
        HandleInput();
        HandleGroundCheck();
        HandleClickToMove();
        HandleMovement();
        HandleAnimations();
    }
    
    void HandleInput()
    {
        // Movement input
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");
        
        // Jump input
        jumpInput = Input.GetButtonDown("Jump");
        
        // Run input
        runInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        // Cancel NavMesh movement if player gives manual input
        if (movementInput.magnitude > inputDeadzone && isUsingNavMesh)
        {
            CancelNavMeshMovement();
        }
    }
    
    void HandleGroundCheck()
    {
        Vector3 spherePosition = transform.position - Vector3.up * (characterController.height * 0.5f - characterController.radius);
        isGrounded = Physics.CheckSphere(spherePosition, groundCheckRadius, groundLayers);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = groundedGravity;
            hasDoubleJumped = false;
        }
    }
    
    void HandleClickToMove()
    {
        if (!enableClickToMove || navMeshAgent == null) return;
        
        bool modifierPressed = Input.GetKey(clickToMoveModifier);
        bool clickPressed = Input.GetKeyDown(clickToMoveButton);
        
        if (modifierPressed && clickPressed)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, clickRayDistance, groundLayerMask))
            {
                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                {
                    navMeshAgent.SetDestination(navHit.position);
                    isUsingNavMesh = true;
                    navMeshAgent.isStopped = false;
                }
            }
        }
    }
    
    void HandleMovement()
    {
        Vector3 horizontalMovement = Vector3.zero;
        
        if (movementInput.magnitude > inputDeadzone)
        {
            // Manual movement
            HandleManualMovement();
        }
        else if (isUsingNavMesh && navMeshAgent.hasPath)
        {
            // NavMesh movement
            HandleNavMeshMovement();
        }
        
        // Handle jumping
        HandleJumping();
        
        // Apply gravity
        if (!isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        // Move the character
        Vector3 finalMovement = horizontalMovement + Vector3.up * velocity.y;
        characterController.Move(finalMovement * Time.deltaTime);
        
        // Sync NavMesh agent position
        if (navMeshAgent != null)
        {
            navMeshAgent.nextPosition = transform.position;
        }
    }
    
    void HandleManualMovement()
    {
        // Get camera-relative direction
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;
        
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
        
        Vector3 moveDirection = cameraForward * movementInput.y + cameraRight * movementInput.x;
        moveDirection.Normalize();
        
        // Determine speed
        float currentSpeed = runInput ? runSpeed : walkSpeed;
        isRunning = runInput && movementInput.magnitude > inputDeadzone;
        isMoving = movementInput.magnitude > inputDeadzone;
        
        // Apply movement
        Vector3 horizontalMovement = moveDirection * currentSpeed;
        characterController.Move(horizontalMovement * Time.deltaTime);
        
        // Rotate character towards movement direction
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    void HandleNavMeshMovement()
    {
        if (navMeshAgent.pathPending) return;
        
        Vector3 desiredVelocity = navMeshAgent.desiredVelocity;
        desiredVelocity.y = 0f;
        
        float speed = desiredVelocity.magnitude;
        isMoving = speed > 0.1f;
        isRunning = speed > walkSpeed + 1f;
        
        // Move with CharacterController
        characterController.Move(desiredVelocity * Time.deltaTime);
        
        // Rotate towards movement direction
        if (desiredVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // Check if destination reached
        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
        {
            CancelNavMeshMovement();
        }
    }
    
    void HandleJumping()
    {
        if (jumpInput)
        {
            if (isGrounded)
            {
                // First jump
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                hasDoubleJumped = false;
            }
            else if (!hasDoubleJumped)
            {
                // Double jump
                velocity.y = Mathf.Sqrt(doubleJumpHeight * -2f * gravity);
                hasDoubleJumped = true;
            }
        }
    }
    
    void HandleAnimations()
    {
        // Animation handling is now managed by CatAnimationManager
        // This keeps the controller focused on movement logic
        // The CatAnimationManager will read our public properties and handle animations
    }
    
    void CancelNavMeshMovement()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
        }
        isUsingNavMesh = false;
    }
    
    // Public methods for other scripts
    public bool IsGrounded => isGrounded;
    public bool IsMoving => isMoving;
    public bool IsRunning => isRunning;
    public Vector3 Velocity => velocity;
    
    void OnDrawGizmosSelected()
    {
        // Draw ground check sphere
        Vector3 spherePosition = transform.position - Vector3.up * (characterController != null ? 
            (characterController.height * 0.5f - characterController.radius) : 1f);
        
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(spherePosition, groundCheckRadius);
    }
}