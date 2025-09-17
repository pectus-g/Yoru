using UnityEngine;

public class SimpleCatAnimator : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator catAnimator;
    
    [Header("Animation Detection")]
    public bool autoDetectAnimations = true;
    public string[] idleClipNames = {"idle", "Idle", "IDLE", "idle_01"};
    public string[] walkClipNames = {"walk", "Walk", "WALK", "walk_01"};
    public string[] runClipNames = {"run", "Run", "RUN", "run_01"};
    public string[] jumpClipNames = {"jump", "Jump", "JUMP", "jump_01"};
    
    [Header("Animation Control")]
    public bool useDirectPlayback = true;
    public float crossFadeTime = 0.2f;
    
    // Components
    private CatPlayerController playerController;
    
    // Animation clips found
    private AnimationClip idleClip;
    private AnimationClip walkClip;
    private AnimationClip runClip;
    private AnimationClip jumpClip;
    
    // Animation state
    private string currentAnimation = "";
    private bool wasGrounded = true;
    
    void Awake()
    {
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
            // Configure animator to prevent movement conflicts
            catAnimator.applyRootMotion = false;
            catAnimator.updateMode = AnimatorUpdateMode.Normal;
            catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            
            if (autoDetectAnimations)
            {
                DetectAnimationClips();
            }
            
            // Start with idle
            PlayIdleAnimation();
        }
        
        Debug.Log("Simple Cat Animator initialized");
    }
    
    void Update()
    {
        if (catAnimator == null || playerController == null) return;
        
        UpdateAnimations();
    }
    
    void DetectAnimationClips()
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null) return;
        
        AnimationClip[] clips = catAnimator.runtimeAnimatorController.animationClips;
        
        foreach (AnimationClip clip in clips)
        {
            string clipName = clip.name.ToLower();
            
            // Check for idle animations
            if (idleClip == null)
            {
                foreach (string idleName in idleClipNames)
                {
                    if (clipName.Contains(idleName.ToLower()))
                    {
                        idleClip = clip;
                        Debug.Log($"Found idle animation: {clip.name}");
                        break;
                    }
                }
            }
            
            // Check for walk animations
            if (walkClip == null)
            {
                foreach (string walkName in walkClipNames)
                {
                    if (clipName.Contains(walkName.ToLower()))
                    {
                        walkClip = clip;
                        Debug.Log($"Found walk animation: {clip.name}");
                        break;
                    }
                }
            }
            
            // Check for run animations
            if (runClip == null)
            {
                foreach (string runName in runClipNames)
                {
                    if (clipName.Contains(runName.ToLower()))
                    {
                        runClip = clip;
                        Debug.Log($"Found run animation: {clip.name}");
                        break;
                    }
                }
            }
            
            // Check for jump animations
            if (jumpClip == null)
            {
                foreach (string jumpName in jumpClipNames)
                {
                    if (clipName.Contains(jumpName.ToLower()))
                    {
                        jumpClip = clip;
                        Debug.Log($"Found jump animation: {clip.name}");
                        break;
                    }
                }
            }
        }
        
        // Log what we found
        LogFoundAnimations();
    }
    
    void LogFoundAnimations()
    {
        Debug.Log("=== Animation Detection Results ===");
        Debug.Log($"Idle: {(idleClip != null ? idleClip.name : "Not Found")}");
        Debug.Log($"Walk: {(walkClip != null ? walkClip.name : "Not Found")}");
        Debug.Log($"Run: {(runClip != null ? runClip.name : "Not Found")}");
        Debug.Log($"Jump: {(jumpClip != null ? jumpClip.name : "Not Found")}");
        Debug.Log("=====================================");
    }
    
    void UpdateAnimations()
    {
        bool isMoving = playerController.IsMoving;
        bool isRunning = playerController.IsRunning;
        bool isGrounded = playerController.IsGrounded;
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        // Handle jump animation
        if (jumpPressed && isGrounded)
        {
            PlayJumpAnimation();
        }
        // Handle landing (transition from air to ground)
        else if (!wasGrounded && isGrounded)
        {
            // Just landed, choose appropriate ground animation
            if (isRunning)
                PlayRunAnimation();
            else if (isMoving)
                PlayWalkAnimation();
            else
                PlayIdleAnimation();
        }
        // Handle normal movement animations (only when grounded)
        else if (isGrounded)
        {
            if (isRunning)
                PlayRunAnimation();
            else if (isMoving)
                PlayWalkAnimation();
            else
                PlayIdleAnimation();
        }
        
        wasGrounded = isGrounded;
    }
    
    void PlayIdleAnimation()
    {
        if (idleClip != null && currentAnimation != idleClip.name)
        {
            if (useDirectPlayback)
                catAnimator.CrossFade(idleClip.name, crossFadeTime);
            else
                catAnimator.Play(idleClip.name);
                
            currentAnimation = idleClip.name;
            Debug.Log("Playing idle animation");
        }
    }
    
    void PlayWalkAnimation()
    {
        AnimationClip clipToPlay = walkClip ?? idleClip; // Fallback to idle if no walk
        
        if (clipToPlay != null && currentAnimation != clipToPlay.name)
        {
            if (useDirectPlayback)
                catAnimator.CrossFade(clipToPlay.name, crossFadeTime);
            else
                catAnimator.Play(clipToPlay.name);
                
            currentAnimation = clipToPlay.name;
            Debug.Log("Playing walk animation");
        }
    }
    
    void PlayRunAnimation()
    {
        AnimationClip clipToPlay = runClip ?? walkClip ?? idleClip; // Fallback chain
        
        if (clipToPlay != null && currentAnimation != clipToPlay.name)
        {
            if (useDirectPlayback)
                catAnimator.CrossFade(clipToPlay.name, crossFadeTime);
            else
                catAnimator.Play(clipToPlay.name);
                
            currentAnimation = clipToPlay.name;
            Debug.Log("Playing run animation");
        }
    }
    
    void PlayJumpAnimation()
    {
        AnimationClip clipToPlay = jumpClip ?? idleClip; // Fallback to idle if no jump
        
        if (clipToPlay != null)
        {
            if (useDirectPlayback)
                catAnimator.CrossFade(clipToPlay.name, 0.1f);
            else
                catAnimator.Play(clipToPlay.name);
                
            currentAnimation = clipToPlay.name;
            Debug.Log("Playing jump animation");
        }
    }
    
    // Public methods for manual control
    public void ForceIdleAnimation()
    {
        currentAnimation = ""; // Reset so it will play
        PlayIdleAnimation();
    }
    
    public void ForceWalkAnimation()
    {
        currentAnimation = "";
        PlayWalkAnimation();
    }
    
    public void ForceRunAnimation()
    {
        currentAnimation = "";
        PlayRunAnimation();
    }
    
    public void ForceJumpAnimation()
    {
        currentAnimation = "";
        PlayJumpAnimation();
    }
    
    // Context menu for testing
    [ContextMenu("Test Idle")]
    public void TestIdle() { ForceIdleAnimation(); }
    
    [ContextMenu("Test Walk")]
    public void TestWalk() { ForceWalkAnimation(); }
    
    [ContextMenu("Test Run")]
    public void TestRun() { ForceRunAnimation(); }
    
    [ContextMenu("Test Jump")]
    public void TestJump() { ForceJumpAnimation(); }
    
    [ContextMenu("Detect Animations Again")]
    public void RedetectAnimations() 
    { 
        idleClip = walkClip = runClip = jumpClip = null;
        DetectAnimationClips(); 
    }
}