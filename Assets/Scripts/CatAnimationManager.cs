using UnityEngine;

public class CatAnimationManager : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator catAnimator;
    
    [Header("Animation Parameters")]
    public string speedParameterName = "Speed";
    public string isMovingParameterName = "IsMoving";
    public string isRunningParameterName = "IsRunning";
    public string isGroundedParameterName = "IsGrounded";
    public string jumpTriggerName = "Jump";
    public string landTriggerName = "Land";
    
    [Header("Animation Settings")]
    public float animationSmoothTime = 0.15f;
    public bool disableRootMotion = true;
    
    // Internal state tracking
    private CatPlayerController playerController;
    private float currentSpeed = 0f;
    private float targetSpeed = 0f;
    private bool wasGrounded = true;
    
    void Awake()
    {
        // Find components
        if (catAnimator == null)
            catAnimator = GetComponentInChildren<Animator>();
            
        playerController = GetComponent<CatPlayerController>();
        if (playerController == null)
            playerController = GetComponentInParent<CatPlayerController>();
    }
    
    void Start()
    {
        if (catAnimator != null)
        {
            // Configure animator settings
            catAnimator.applyRootMotion = !disableRootMotion;
            catAnimator.updateMode = AnimatorUpdateMode.Normal;
            catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            
            // Initialize parameters
            SetBoolSafe(isMovingParameterName, false);
            SetBoolSafe(isRunningParameterName, false);
            SetBoolSafe(isGroundedParameterName, true);
            SetFloatSafe(speedParameterName, 0f);
        }
        
        Debug.Log("Cat Animation Manager initialized");
    }
    
    void Update()
    {
        if (catAnimator == null || playerController == null) return;
        
        UpdateAnimationParameters();
        HandleJumpAnimations();
    }
    
    void UpdateAnimationParameters()
    {
        // Get movement state from player controller
        bool isMoving = playerController.IsMoving;
        bool isRunning = playerController.IsRunning;
        bool isGrounded = playerController.IsGrounded;
        
        // Calculate target speed
        if (isRunning)
            targetSpeed = 1.0f;
        else if (isMoving)
            targetSpeed = 0.5f;
        else
            targetSpeed = 0f;
        
        // Smooth speed transitions
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime / animationSmoothTime);
        
        // Set animator parameters
        SetBoolSafe(isMovingParameterName, isMoving);
        SetBoolSafe(isRunningParameterName, isRunning);
        SetBoolSafe(isGroundedParameterName, isGrounded);
        SetFloatSafe(speedParameterName, currentSpeed);
    }
    
    void HandleJumpAnimations()
    {
        bool isGrounded = playerController.IsGrounded;
        
        // Trigger jump animation when jumping
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            SetTriggerSafe(jumpTriggerName);
        }
        
        // Trigger landing animation when landing
        if (!wasGrounded && isGrounded)
        {
            SetTriggerSafe(landTriggerName);
        }
        
        wasGrounded = isGrounded;
    }
    
    // Safe parameter setting methods
    void SetBoolSafe(string parameterName, bool value)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetBool(parameterName, value);
        }
    }
    
    void SetFloatSafe(string parameterName, float value)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetFloat(parameterName, value);
        }
    }
    
    void SetTriggerSafe(string parameterName)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetTrigger(parameterName);
        }
    }
    
    bool HasParameter(string parameterName)
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null) 
            return false;
        
        foreach (var param in catAnimator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }
    
    // Public methods for external control
    public void PlayCustomAnimation(string triggerName)
    {
        SetTriggerSafe(triggerName);
    }
    
    public void SetAnimationSpeed(float multiplier)
    {
        if (catAnimator != null)
            catAnimator.speed = Mathf.Clamp(multiplier, 0.1f, 3f);
    }
    
    // Cat-specific animations
    public void PlayMeow()
    {
        SetTriggerSafe("Meow");
    }
    
    public void PlaySit()
    {
        SetTriggerSafe("Sit");
    }
    
    public void PlayIdle()
    {
        SetTriggerSafe("Idle");
    }
    
    // Debug methods
    [ContextMenu("Test Walk Animation")]
    public void TestWalkAnimation()
    {
        SetBoolSafe(isMovingParameterName, true);
        SetBoolSafe(isRunningParameterName, false);
        SetFloatSafe(speedParameterName, 0.5f);
    }
    
    [ContextMenu("Test Run Animation")]
    public void TestRunAnimation()
    {
        SetBoolSafe(isMovingParameterName, true);
        SetBoolSafe(isRunningParameterName, true);
        SetFloatSafe(speedParameterName, 1f);
    }
    
    [ContextMenu("Test Jump Animation")]
    public void TestJumpAnimation()
    {
        SetTriggerSafe(jumpTriggerName);
    }
    
    [ContextMenu("Test Idle Animation")]
    public void TestIdleAnimation()
    {
        SetBoolSafe(isMovingParameterName, false);
        SetBoolSafe(isRunningParameterName, false);
        SetFloatSafe(speedParameterName, 0f);
    }
}