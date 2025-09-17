using UnityEngine;

public class ManualCatAnimator : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator catAnimator;
    
    [Header("Manual Animation Assignment")]
    [Tooltip("Drag the exact animation clips you want to use")]
    public AnimationClip idleAnimation;
    public AnimationClip walkAnimation;
    public AnimationClip runAnimation;
    public AnimationClip jumpAnimation;
    
    [Header("Animation Names (Alternative)")]
    [Tooltip("Or type the exact names of animations you want")]
    public string idleAnimationName = "";
    public string walkAnimationName = "";
    public string runAnimationName = "";
    public string jumpAnimationName = "";
    
    [Header("Settings")]
    [Range(0.1f, 1f)]
    public float transitionTime = 0.25f;
    public bool debugMode = true;
    public bool useNamesInsteadOfClips = false;
    
    [Header("Current State (Read Only)")]
    public string currentAnimation = "None";
    public string lastPlayedAnimation = "None";
    
    // Components
    private CatPlayerController playerController;
    
    // State tracking
    private bool wasGrounded = true;
    private float lastMoveTime = 0f;
    private float idleDelay = 0.2f;
    
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
            // Force disable root motion
            catAnimator.applyRootMotion = false;
            catAnimator.updateMode = AnimatorUpdateMode.Normal;
            catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            
            // List all available animations
            ListAllAnimations();
            
            // Start with idle
            PlayIdle();
        }
        
        Debug.Log("Manual Cat Animator initialized");
    }
    
    void Update()
    {
        if (catAnimator == null || playerController == null) return;
        
        UpdateAnimations();
    }
    
    void UpdateAnimations()
    {
        bool isMoving = playerController.IsMoving;
        bool isRunning = playerController.IsRunning;
        bool isGrounded = playerController.IsGrounded;
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        // Track movement for idle delay
        if (isMoving)
            lastMoveTime = Time.time;
        
        // Handle jump
        if (jumpPressed && isGrounded)
        {
            PlayJump();
            return;
        }
        
        // Handle landing transition
        if (!wasGrounded && isGrounded)
        {
            if (isRunning)
                PlayRun();
            else if (isMoving)
                PlayWalk();
            else
                PlayIdle();
                
            wasGrounded = isGrounded;
            return;
        }
        
        // Handle ground animations
        if (isGrounded)
        {
            if (isRunning)
            {
                if (currentAnimation != "Run")
                    PlayRun();
            }
            else if (isMoving)
            {
                if (currentAnimation != "Walk")
                    PlayWalk();
            }
            else
            {
                // Only go to idle after delay
                if (Time.time - lastMoveTime > idleDelay && currentAnimation != "Idle")
                {
                    PlayIdle();
                }
            }
        }
        
        wasGrounded = isGrounded;
    }
    
    public void PlayIdle()
    {
        if (useNamesInsteadOfClips && !string.IsNullOrEmpty(idleAnimationName))
        {
            PlayAnimationByName(idleAnimationName, "Idle");
        }
        else if (idleAnimation != null)
        {
            PlayAnimationClip(idleAnimation, "Idle");
        }
        else if (debugMode)
        {
            Debug.LogWarning("No idle animation assigned!");
        }
    }
    
    public void PlayWalk()
    {
        if (useNamesInsteadOfClips && !string.IsNullOrEmpty(walkAnimationName))
        {
            PlayAnimationByName(walkAnimationName, "Walk");
        }
        else if (walkAnimation != null)
        {
            PlayAnimationClip(walkAnimation, "Walk");
        }
        else if (debugMode)
        {
            Debug.LogWarning("No walk animation assigned! Using idle instead.");
            PlayIdle();
        }
    }
    
    public void PlayRun()
    {
        if (useNamesInsteadOfClips && !string.IsNullOrEmpty(runAnimationName))
        {
            PlayAnimationByName(runAnimationName, "Run");
        }
        else if (runAnimation != null)
        {
            PlayAnimationClip(runAnimation, "Run");
        }
        else
        {
            // Fallback to walk
            if (debugMode)
                Debug.LogWarning("No run animation assigned! Using walk instead.");
            PlayWalk();
        }
    }
    
    public void PlayJump()
    {
        if (useNamesInsteadOfClips && !string.IsNullOrEmpty(jumpAnimationName))
        {
            PlayAnimationByName(jumpAnimationName, "Jump");
        }
        else if (jumpAnimation != null)
        {
            PlayAnimationClip(jumpAnimation, "Jump");
        }
        else if (debugMode)
        {
            Debug.LogWarning("No jump animation assigned!");
        }
    }
    
    void PlayAnimationClip(AnimationClip clip, string stateName)
    {
        if (clip == null || catAnimator == null) return;
        
        if (currentAnimation == stateName) return; // Already playing
        
        try
        {
            catAnimator.CrossFade(clip.name, transitionTime);
            currentAnimation = stateName;
            lastPlayedAnimation = clip.name;
            
            if (debugMode)
                Debug.Log($"Playing {stateName}: {clip.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to play animation {clip.name}: {e.Message}");
        }
    }
    
    void PlayAnimationByName(string animationName, string stateName)
    {
        if (string.IsNullOrEmpty(animationName) || catAnimator == null) return;
        
        if (currentAnimation == stateName) return; // Already playing
        
        try
        {
            catAnimator.CrossFade(animationName, transitionTime);
            currentAnimation = stateName;
            lastPlayedAnimation = animationName;
            
            if (debugMode)
                Debug.Log($"Playing {stateName}: {animationName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to play animation {animationName}: {e.Message}");
        }
    }
    
    [ContextMenu("List All Available Animations")]
    public void ListAllAnimations()
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("No animator or controller found!");
            return;
        }
        
        Debug.Log("=== ALL AVAILABLE ANIMATIONS ===");
        AnimationClip[] clips = catAnimator.runtimeAnimatorController.animationClips;
        
        for (int i = 0; i < clips.Length; i++)
        {
            Debug.Log($"{i + 1}. \"{clips[i].name}\" (Length: {clips[i].length:F1}s)");
        }
        
        Debug.Log("==================================");
        Debug.Log("Copy the exact names above and paste them into the animation name fields, or drag the clips from your FBX files into the animation clip fields.");
    }
    
    [ContextMenu("Test Idle Animation")]
    public void TestIdle()
    {
        currentAnimation = ""; // Force change
        PlayIdle();
    }
    
    [ContextMenu("Test Walk Animation")]
    public void TestWalk()
    {
        currentAnimation = "";
        PlayWalk();
    }
    
    [ContextMenu("Test Run Animation")]
    public void TestRun()
    {
        currentAnimation = "";
        PlayRun();
    }
    
    [ContextMenu("Test Jump Animation")]
    public void TestJump()
    {
        currentAnimation = "";
        PlayJump();
    }
    
    [ContextMenu("Stop All Animations")]
    public void StopAnimations()
    {
        if (catAnimator != null)
        {
            catAnimator.speed = 0f;
            Debug.Log("All animations stopped");
        }
    }
    
    [ContextMenu("Resume Animations")]
    public void ResumeAnimations()
    {
        if (catAnimator != null)
        {
            catAnimator.speed = 1f;
            Debug.Log("Animations resumed");
        }
    }
    
    // Helper method to find and assign animations automatically
    [ContextMenu("Auto-Find and Assign Animations")]
    public void AutoFindAnimations()
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null) return;
        
        AnimationClip[] clips = catAnimator.runtimeAnimatorController.animationClips;
        
        Debug.Log("=== AUTO-FINDING ANIMATIONS ===");
        
        foreach (AnimationClip clip in clips)
        {
            string clipName = clip.name.ToLower();
            
            // Find idle (prefer non-caress)
            if (idleAnimation == null && clipName.Contains("idle") && !clipName.Contains("caress"))
            {
                idleAnimation = clip;
                idleAnimationName = clip.name;
                Debug.Log($"Auto-assigned Idle: {clip.name}");
            }
            
            // Find walk (avoid backwards)
            if (walkAnimation == null && clipName.Contains("walk") && !clipName.Contains("_b_"))
            {
                walkAnimation = clip;
                walkAnimationName = clip.name;
                Debug.Log($"Auto-assigned Walk: {clip.name}");
            }
            
            // Find run (avoid jump-run)
            if (runAnimation == null && clipName.Contains("run") && !clipName.Contains("jump"))
            {
                runAnimation = clip;
                runAnimationName = clip.name;
                Debug.Log($"Auto-assigned Run: {clip.name}");
            }
            
            // Find jump
            if (jumpAnimation == null && clipName.Contains("jump"))
            {
                jumpAnimation = clip;
                jumpAnimationName = clip.name;
                Debug.Log($"Auto-assigned Jump: {clip.name}");
            }
        }
        
        Debug.Log("=== AUTO-FIND COMPLETE ===");
        Debug.Log("Check the Inspector to see assigned animations, or manually change them!");
    }
}