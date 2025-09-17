using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CatAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    public string speedParameter = "Speed";
    public string isMovingParameter = "IsMoving";
    public string isRunningParameter = "IsRunning";
    public string isGroundedParameter = "IsGrounded";
    public string jumpTrigger = "Jump";
    public string landTrigger = "Land";
    
    [Header("Animation Settings")]
    public float animationSmoothTime = 0.1f;
    public float speedMultiplier = 1f;
    
    private Animator catAnimator;
    private CatPlayerController playerController;
    
    // Animation state tracking
    private bool wasGrounded = true;
    private float currentAnimSpeed;
    private float targetAnimSpeed;
    
    void Awake()
    {
        catAnimator = GetComponent<Animator>();
        playerController = GetComponentInParent<CatPlayerController>();
        
        if (playerController == null)
            playerController = GetComponent<CatPlayerController>();
    }
    
    void Start()
    {
        // Initialize animation parameters
        if (catAnimator != null)
        {
            SetBoolParameter(isMovingParameter, false);
            SetBoolParameter(isRunningParameter, false);
            SetBoolParameter(isGroundedParameter, true);
            SetFloatParameter(speedParameter, 0f);
        }
    }
    
    void Update()
    {
        if (catAnimator == null || playerController == null) return;
        
        UpdateAnimationParameters();
        HandleLandingAnimation();
    }
    
    void UpdateAnimationParameters()
    {
        bool isMoving = playerController.IsMoving;
        bool isRunning = playerController.IsRunning;
        bool isGrounded = playerController.IsGrounded;
        
        // Set boolean parameters
        SetBoolParameter(isMovingParameter, isMoving);
        SetBoolParameter(isRunningParameter, isRunning);
        SetBoolParameter(isGroundedParameter, isGrounded);
        
        // Calculate target speed
        if (isRunning)
            targetAnimSpeed = 1f;
        else if (isMoving)
            targetAnimSpeed = 0.5f;
        else
            targetAnimSpeed = 0f;
        
        // Smooth speed transition
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, Time.deltaTime / animationSmoothTime);
        SetFloatParameter(speedParameter, currentAnimSpeed * speedMultiplier);
    }
    
    void HandleLandingAnimation()
    {
        bool isGrounded = playerController.IsGrounded;
        
        // Trigger jump animation
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            TriggerAnimation(jumpTrigger);
        }
        
        // Trigger landing animation
        if (!wasGrounded && isGrounded)
        {
            TriggerAnimation(landTrigger);
        }
        
        wasGrounded = isGrounded;
    }
    
    // Helper methods for safe parameter setting
    void SetBoolParameter(string parameterName, bool value)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetBool(parameterName, value);
        }
    }
    
    void SetFloatParameter(string parameterName, float value)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetFloat(parameterName, value);
        }
    }
    
    void SetIntParameter(string parameterName, int value)
    {
        if (HasParameter(parameterName))
        {
            catAnimator.SetInteger(parameterName, value);
        }
    }
    
    void TriggerAnimation(string triggerName)
    {
        if (HasParameter(triggerName))
        {
            catAnimator.SetTrigger(triggerName);
        }
    }
    
    bool HasParameter(string parameterName)
    {
        if (catAnimator == null || string.IsNullOrEmpty(parameterName)) return false;
        
        foreach (AnimatorControllerParameter param in catAnimator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }
    
    // Public methods for external control
    public void PlaySpecialAnimation(string triggerName)
    {
        TriggerAnimation(triggerName);
    }
    
    public void SetAnimationSpeed(float speed)
    {
        speedMultiplier = Mathf.Clamp(speed, 0.1f, 3f);
    }
    
    public void SetSmoothTime(float smoothTime)
    {
        animationSmoothTime = Mathf.Clamp(smoothTime, 0.01f, 1f);
    }
    
    // Cat-specific animation methods
    public void PlayIdleAnimation()
    {
        TriggerAnimation("Idle");
    }
    
    public void PlayMeowAnimation()
    {
        TriggerAnimation("Meow");
    }
    
    public void PlaySitAnimation()
    {
        TriggerAnimation("Sit");
    }
    
    public void PlaySleepAnimation()
    {
        TriggerAnimation("Sleep");
    }
    
    void OnValidate()
    {
        animationSmoothTime = Mathf.Clamp(animationSmoothTime, 0.01f, 1f);
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 3f);
    }
}