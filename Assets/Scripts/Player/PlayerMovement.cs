using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float speedDampTime = 0.25f;
    
    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float doubleJumpHeight = 1.5f;
    [SerializeField] private float tripleJumpHeight = 1.2f;
    [SerializeField] private float jumpForwardSpeed = 4f;
    
    [Header("Timing")]
    [SerializeField] private float multiJumpWindow = 0.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    
    [Header("Physics")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float fallMultiplier = 1.5f;
    
    [Header("VFX")]
    [SerializeField] private bool enableVFX = true;
    #endregion
    
    #region Private Fields
    // Cached components (allocated once)
    private PlayerCombat playerCombat;

    private CharacterController controller;
    private Animator animator;
    private ThirdPersonCamera cameraController;
    private YoruVFXManager vfxManager; // VFX Integration
    private Transform cachedTransform;
    
    // Cached animator hashes (much faster than strings)
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int locomotionHash = Animator.StringToHash("Locomotion");
    private readonly int jump2LegsHash = Animator.StringToHash("JumpWith2Legs");
    private readonly int jump4LegsHash = Animator.StringToHash("JumpWith4Legs");
    
    // State flags (use bitwise flags for multiple states)
    private enum PlayerState
    {
        Grounded = 1 << 0,
        Jumping = 1 << 1,
        Running = 1 << 2,
        Moving = 1 << 3,
        Landing = 1 << 4
    }
    private PlayerState currentState;
    
    // Physics state
    private Vector3 velocity;
    private Vector3 jumpMomentum;
    private Vector3 moveDirection;
    
    // Jump state
    private byte jumpCount; // byte is smaller than int
    private float jumpWindowTimer;
    private float coyoteTimer;
    private bool wasRunningForJump; // Track if running when started jumping
    
    // Animation state
    private float currentSpeed;
    private float speedVelocity;
    private int currentAnimStateHash;
    
    // Constants
    private const float MIN_MOVE_THRESHOLD = 0.01f;
    private const float GROUND_CHECK_DISTANCE = 0.1f;
    private const float ANIMATION_CROSS_FADE = 0.05f;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {playerCombat = GetComponent<PlayerCombat>();
        // Cache all components once
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        cachedTransform = transform;
        
        // Get VFX Manager if enabled
        if (enableVFX)
        {
            vfxManager = GetComponent<YoruVFXManager>();
            if (vfxManager == null && enableVFX)
            {
                Debug.LogWarning("VFX enabled but YoruVFXManager not found!");
            }
        }
    }
    
    private void Start()
    {
        cameraController = FindObjectOfType<ThirdPersonCamera>();
        
        // Disable NavMesh if present
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent) agent.enabled = false;
    }
    
    private void Update()
    {
        // Handle input and state updates
        HandleInput();
        UpdateState();
        UpdateAnimation();
    }
    
    private void FixedUpdate()
    {
        // Physics should be in FixedUpdate for consistency
        ApplyMovement();
        ApplyGravity();
        controller.Move(velocity * Time.fixedDeltaTime);
    }
    #endregion
    
    #region Input & State Management
    private void HandleInput()
    {
        // Early exit for inventory
        if (InventoryUI.Instance?.IsInventoryOpen() ?? false)
        {
            SetState(PlayerState.Moving, false);
            return;
        }
        
        // Get input once per frame
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        // Calculate movement efficiently
        bool isMoving = (h * h + v * v) > MIN_MOVE_THRESHOLD;
        SetState(PlayerState.Moving, isMoving);
        
        if (isMoving)
        {
            // Only calculate direction if moving
            if (cameraController != null)
            {
                Vector3 forward = cameraController.GetCameraForward();
                Vector3 right = cameraController.GetCameraRight();
                moveDirection = (forward * v + right * h).normalized;
            }
            else
            {
                moveDirection.Set(h, 0, v);
                moveDirection.Normalize();
            }
            
            SetState(PlayerState.Running, Input.GetKey(KeyCode.LeftShift));
        }
        else
        {
            SetState(PlayerState.Running, false);
        }
        
        // Jump input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryJump();
        }
    }
    
    private void UpdateState()
    {
        // Ground check
        bool wasGrounded = HasState(PlayerState.Grounded);
        bool isGrounded = controller.isGrounded;
        SetState(PlayerState.Grounded, isGrounded);
        
        // Landing detection
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }
        else if (wasGrounded && !isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        
        // Update timers
        if (jumpWindowTimer > 0)
            jumpWindowTimer -= Time.deltaTime;
        
        if (coyoteTimer > 0)
            coyoteTimer -= Time.deltaTime;
        
        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }
    #endregion
    
    #region Movement & Physics
    private void ApplyMovement()
    {
        if (!HasState(PlayerState.Moving))
        {
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, Time.fixedDeltaTime * 5f);
            return;
        }
        
        // Only calculate when needed
        if (HasState(PlayerState.Grounded) && !HasState(PlayerState.Jumping))
        {
            float speed = HasState(PlayerState.Running) ? runSpeed : walkSpeed;
            Vector3 movement = moveDirection * speed * Time.fixedDeltaTime;
            controller.Move(movement);
            
            // Rotation
            if (moveDirection.sqrMagnitude > MIN_MOVE_THRESHOLD)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection);
                cachedTransform.rotation = Quaternion.Slerp(cachedTransform.rotation, targetRot, 
                    rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else if (!HasState(PlayerState.Grounded))
        {
            // Air movement
            controller.Move(jumpMomentum * Time.fixedDeltaTime);
        }
    }
    
    private void ApplyGravity()
    {
        // Apply gravity with optional fall multiplier
        float gravityMult = (velocity.y < 0) ? fallMultiplier : 1f;
        velocity.y += gravity * gravityMult * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -50f); // Terminal velocity
    }
    #endregion
    
    #region Jump System
    private void TryJump()
    {if (playerCombat != null && playerCombat.IsAttacking() && !playerCombat.IsAerialAttack()) return;
        // First jump
        if (jumpCount == 0 && (HasState(PlayerState.Grounded) || coyoteTimer > 0))
        {
            bool isRunning = HasState(PlayerState.Running);
            wasRunningForJump = isRunning; // Remember if we were running for multi-jump
            PerformJump(jumpHeight, isRunning);
            jumpCount = 1;
            jumpWindowTimer = multiJumpWindow;
            
            // VFX for first jump
            vfxManager?.OnJump(jumpCount);
        }
        // Multi-jump (only if was running and within window)
        else if (jumpCount > 0 && jumpCount < 3 && jumpWindowTimer > 0 && wasRunningForJump)
        {
            float power = (jumpCount == 1) ? doubleJumpHeight : tripleJumpHeight;
            PerformJump(power, true);
            jumpCount++;
            jumpWindowTimer = (jumpCount < 3) ? multiJumpWindow : 0;
            
            // VFX for multi-jump
            vfxManager?.OnJump(jumpCount);
            
            if (jumpCount >= 3)
            {
                Debug.Log("🚫 Max jumps reached!");
            }
        }
    }
    
    private void PerformJump(float power, bool isFourLegged)
    {
        velocity.y = Mathf.Sqrt(power * -2f * gravity);
        
        // Set momentum
        if (HasState(PlayerState.Moving))
        {
            float momentumSpeed = isFourLegged ? jumpForwardSpeed * 1.5f : jumpForwardSpeed;
            jumpMomentum = moveDirection * momentumSpeed;
        }
        else
        {
            jumpMomentum = cachedTransform.forward * 0.5f;
        }
        
        // Trigger animation efficiently
        SetState(PlayerState.Jumping, true);
        int animHash = isFourLegged ? jump4LegsHash : jump2LegsHash;
        animator.CrossFadeInFixedTime(animHash, ANIMATION_CROSS_FADE, 0);
        
        // Debug output
        if (isFourLegged)
            Debug.Log($"🐾 4-LEG JUMP #{jumpCount}");
        else
            Debug.Log($"🚶 2-LEG JUMP");
    }
    
    private void OnLanded()
    {
        Debug.Log($"✅ LANDED! (was jump #{jumpCount})");
        
        jumpCount = 0;
        jumpWindowTimer = 0;
        wasRunningForJump = false;
        jumpMomentum *= 0.5f; // Gradual stop
        
        SetState(PlayerState.Jumping, false);
        SetState(PlayerState.Landing, true);
        
        // Force locomotion state
        animator.CrossFadeInFixedTime(locomotionHash, 0.2f, 0);
        
        // Clear landing state after a moment
        Invoke(nameof(ClearLandingState), 0.3f);
        
        // VFX handles landing automatically by watching grounded state
    }
    
    private void ClearLandingState()
    {
        SetState(PlayerState.Landing, false);
    }
    #endregion
    
    #region Animation
    private void UpdateAnimation()
    {
        // Skip if jumping or landing
        if (HasState(PlayerState.Jumping | PlayerState.Landing))
            return;
        
        // Calculate target speed
        float targetSpeed = 0f;
        if (HasState(PlayerState.Moving) && HasState(PlayerState.Grounded))
        {
            targetSpeed = HasState(PlayerState.Running) ? 2f : 1f;
        }
        
        // Smooth transition (only update when changed)
        if (Mathf.Abs(currentSpeed - targetSpeed) > 0.01f)
        {
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, speedDampTime);
            animator.SetFloat(speedHash, currentSpeed);
        }
        
        // Update grounded state (only when changed)
        animator.SetBool(isGroundedHash, HasState(PlayerState.Grounded));
    }
    #endregion
    
    #region Helper Methods
    private void SetState(PlayerState state, bool value)
    {
        if (value)
            currentState |= state;
        else
            currentState &= ~state;
    }
    
    private bool HasState(PlayerState state)
    {
        return (currentState & state) != 0;
    }
    #endregion
    
    #region Debug
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || controller == null) return;
        
        Gizmos.color = HasState(PlayerState.Grounded) ? Color.green : Color.red;
        Gizmos.DrawWireSphere(cachedTransform.position - Vector3.up * (controller.height * 0.5f), 0.3f);
        
        // Jump momentum visualization
        if (jumpMomentum.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cachedTransform.position, jumpMomentum);
        }
        
        // Jump window visualization
        if (wasRunningForJump && jumpCount > 0 && jumpWindowTimer > 0)
        {
            float percent = jumpWindowTimer / multiJumpWindow;
            Gizmos.color = percent > 0.5f ? Color.green : percent > 0.2f ? Color.yellow : Color.red;
            Gizmos.DrawWireCube(cachedTransform.position + Vector3.up * 2, 
                                new Vector3(percent * 2f, 0.1f, 0.1f));
        }
    }
    #endif
    #endregion
}