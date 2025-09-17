using UnityEngine;

public class FixedCatAnimator : MonoBehaviour
{
    [Header("Animator Reference")]
    public Animator catAnimator;
    
    [Header("Animation State")]
    public string currentState = "None";
    public bool debugAnimations = true;
    
    [Header("Animation Clips (Auto-Found)")]
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;
    public AnimationClip jumpClip;
    
    [Header("Animation Settings")]
    [Range(0.1f, 1f)]
    public float transitionTime = 0.3f;
    public bool forceAnimationPlay = true;
    
    // Components
    private CatPlayerController playerController;
    
    // State tracking
    private string lastAnimation = "";
    private bool wasGrounded = true;
    private float lastMoveTime = 0f;
    private float idleDelay = 0.1f; // Small delay before going to idle
    
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
            // Force disable root motion to prevent weird movement
            catAnimator.applyRootMotion = false;
            catAnimator.updateMode = AnimatorUpdateMode.Normal;
            catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            
            // Find the best animations
            FindBestAnimations();
            
            // Start with idle
            ForcePlayAnimation(idleClip, "Idle");
        }
        
        Debug.Log("Fixed Cat Animator initialized");
    }
    
    void FindBestAnimations()
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null) return;
        
        AnimationClip[] clips = catAnimator.runtimeAnimatorController.animationClips;
        
        Debug.Log("=== Finding Best Animations ===");
        
        foreach (AnimationClip clip in clips)
        {
            string clipName = clip.name.ToLower();
            
            // Find BEST idle (avoid caress, prefer simple idle)
            if (idleClip == null || IsBetterIdleClip(clip))
            {
                if (clipName.Contains("idle") && !clipName.Contains("caress"))
                {
                    idleClip = clip;
                    Debug.Log($"Better idle found: {clip.name}");
                }
            }
            
            // Find BEST walk (avoid backwards walk)
            if (walkClip == null || IsBetterWalkClip(clip))
            {
                if (clipName.Contains("walk") && !clipName.Contains("_b_") && !clipName.Contains("back"))
                {
                    walkClip = clip;
                    Debug.Log($"Better walk found: {clip.name}");
                }
            }
            
            // Find BEST run (avoid jump-run)
            if (runClip == null || IsBetterRunClip(clip))
            {
                if (clipName.Contains("run") && !clipName.Contains("jump"))
                {
                    runClip = clip;
                    Debug.Log($"Better run found: {clip.name}");
                }
            }
            
            // Find jump animation
            if (jumpClip == null && clipName.Contains("jump"))
            {
                jumpClip = clip;
                Debug.Log($"Jump found: {clip.name}");
            }
        }
        
        // Fallback: if no good walk, use any walk
        if (walkClip == null)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip.name.ToLower().Contains("walk"))
                {
                    walkClip = clip;
                    Debug.Log($"Fallback walk: {clip.name}");
                    break;
                }
            }
        }
        
        // Use walk as run if no run found
        if (runClip == null && walkClip != null)
        {
            runClip = walkClip;
            Debug.Log("Using walk animation for run");
        }
        
        LogFinalAnimations();
    }
    
    bool IsBetterIdleClip(AnimationClip clip)
    {
        if (idleClip == null) return true;
        
        string newName = clip.name.ToLower();
        string currentName = idleClip.name.ToLower();
        
        // Prefer simple idle over caress idle
        if (currentName.Contains("caress") && !newName.Contains("caress"))
            return true;
            
        return false;
    }
    
    bool IsBetterWalkClip(AnimationClip clip)
    {
        if (walkClip == null) return true;
        
        string newName = clip.name.ToLower();
        string currentName = walkClip.name.ToLower();
        
        // Prefer forward walk over backward walk
        if (currentName.Contains("_b_") && !newName.Contains("_b_"))
            return true;
            
        return false;
    }
    
    bool IsBetterRunClip(AnimationClip clip)
    {
        if (runClip == null) return true;
        
        string newName = clip.name.ToLower();
        string currentName = runClip.name.ToLower();
        
        // Prefer pure run over jump-run
        if (currentName.Contains("jump") && !newName.Contains("jump"))
            return true;
            
        return false;
    }
    
    void LogFinalAnimations()
    {
        Debug.Log("=== Final Animation Setup ===");
        Debug.Log($"Idle: {(idleClip ? idleClip.name : "MISSING!")}");
        Debug.Log($"Walk: {(walkClip ? walkClip.name : "MISSING!")}");
        Debug.Log($"Run: {(runClip ? runClip.name : "MISSING!")}");
        Debug.Log($"Jump: {(jumpClip ? jumpClip.name : "MISSING!")}");
        Debug.Log("===============================");
    }
    
    void Update()
    {
        if (catAnimator == null || playerController == null) return;
        
        UpdateAnimationState();
    }
    
    void UpdateAnimationState()
    {
        bool isMoving = playerController.IsMoving;
        bool isRunning = playerController.IsRunning;
        bool isGrounded = playerController.IsGrounded;
        bool jumpPressed = Input.GetButtonDown("Jump");
        
        // Track movement for idle delay
        if (isMoving)
            lastMoveTime = Time.time;
        
        // Handle jump
        if (jumpPressed && isGrounded && jumpClip != null)
        {
            ForcePlayAnimation(jumpClip, "Jump");
            return;
        }
        
        // Handle landing transition
        if (!wasGrounded && isGrounded)
        {
            // Just landed, choose ground animation
            if (isRunning && runClip != null)
                ForcePlayAnimation(runClip, "Run");
            else if (isMoving && walkClip != null)
                ForcePlayAnimation(walkClip, "Walk");
            else if (idleClip != null)
                ForcePlayAnimation(idleClip, "Idle");
            
            wasGrounded = isGrounded;
            return;
        }
        
        // Handle ground animations only when grounded
        if (isGrounded)
        {
            if (isRunning && runClip != null)
            {
                if (currentState != "Run")
                    ForcePlayAnimation(runClip, "Run");
            }
            else if (isMoving && walkClip != null)
            {
                if (currentState != "Walk")
                    ForcePlayAnimation(walkClip, "Walk");
            }
            else
            {
                // Only go to idle after a small delay to prevent flicker
                if (Time.time - lastMoveTime > idleDelay && currentState != "Idle" && idleClip != null)
                {
                    ForcePlayAnimation(idleClip, "Idle");
                }
            }
        }
        
        wasGrounded = isGrounded;
    }
    
    void ForcePlayAnimation(AnimationClip clip, string stateName)
    {
        if (clip == null || catAnimator == null) return;
        
        // Only change if different animation
        if (currentState == stateName && !forceAnimationPlay) return;
        
        try
        {
            if (forceAnimationPlay)
            {
                // Direct play with crossfade
                catAnimator.CrossFade(clip.name, transitionTime);
            }
            else
            {
                // Instant play
                catAnimator.Play(clip.name);
            }
            
            currentState = stateName;
            lastAnimation = clip.name;
            
            if (debugAnimations)
                Debug.Log($"Playing {stateName}: {clip.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to play animation {clip.name}: {e.Message}");
        }
    }
    
    // Manual animation controls
    [ContextMenu("Force Idle")]
    public void ForceIdle()
    {
        if (idleClip != null)
            ForcePlayAnimation(idleClip, "Force Idle");
    }
    
    [ContextMenu("Force Walk")]
    public void ForceWalk()
    {
        if (walkClip != null)
            ForcePlayAnimation(walkClip, "Force Walk");
    }
    
    [ContextMenu("Force Run")]
    public void ForceRun()
    {
        if (runClip != null)
            ForcePlayAnimation(runClip, "Force Run");
    }
    
    [ContextMenu("Force Jump")]
    public void ForceJump()
    {
        if (jumpClip != null)
            ForcePlayAnimation(jumpClip, "Force Jump");
    }
    
    [ContextMenu("Redetect Best Animations")]
    public void RedetectAnimations()
    {
        idleClip = walkClip = runClip = jumpClip = null;
        FindBestAnimations();
    }
    
    [ContextMenu("List All Animations")]
    public void ListAllAnimations()
    {
        if (catAnimator == null || catAnimator.runtimeAnimatorController == null) return;
        
        Debug.Log("=== All Available Animations ===");
        AnimationClip[] clips = catAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            Debug.Log($"{i + 1}. {clips[i].name}");
        }
        Debug.Log("==================================");
    }
}