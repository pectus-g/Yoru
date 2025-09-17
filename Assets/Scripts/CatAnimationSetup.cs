using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

[System.Serializable]
public class CatAnimationSetup : MonoBehaviour
{
    [Header("Animation Configuration")]
    public Animator catAnimator;
    public string animatorControllerPath = "Assets/Cats/Cat_Fat/CatAnimatorController.controller";
    
    [Header("Animation Clips (Auto-detected)")]
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;
    public AnimationClip jumpClip;
    public AnimationClip landClip;
    
    [Header("Animation Settings")]
    public bool useRootMotion = false;
    public float walkToRunThreshold = 0.5f;
    public float animationBlendTime = 0.25f;
    
    void Start()
    {
        if (catAnimator == null)
            catAnimator = GetComponentInChildren<Animator>();
            
        SetupAnimationController();
    }
    
    [ContextMenu("Setup Cat Animations")]
    public void SetupAnimationController()
    {
        if (catAnimator == null)
        {
            Debug.LogError("No Animator component found! Please assign the cat's Animator.");
            return;
        }
        
        // Configure animator settings
        catAnimator.applyRootMotion = useRootMotion;
        catAnimator.updateMode = AnimatorUpdateMode.Normal;
        catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        
        CreateAnimatorController();
        ConnectToPlayerController();
        
        Debug.Log("Cat animation setup complete!");
    }
    
    void CreateAnimatorController()
    {
#if UNITY_EDITOR
        // Create new Animator Controller
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(animatorControllerPath);
        
        // Add parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
        
        // Get the root state machine
        var rootStateMachine = controller.layers[0].stateMachine;
        
        // Create states
        var idleState = rootStateMachine.AddState("Idle");
        var walkState = rootStateMachine.AddState("Walk");
        var runState = rootStateMachine.AddState("Run");
        var jumpState = rootStateMachine.AddState("Jump");
        
        // Set default state
        rootStateMachine.defaultState = idleState;
        
        // Try to find animation clips from the existing demo controller or FBX files
        FindAnimationClips();
        
        // Assign animation clips to states
        if (idleClip != null) idleState.motion = idleClip;
        if (walkClip != null) walkState.motion = walkClip;
        if (runClip != null) runState.motion = runClip;
        if (jumpClip != null) jumpState.motion = jumpClip;
        
        // Create transitions
        CreateTransitions(rootStateMachine, idleState, walkState, runState, jumpState);
        
        // Assign the controller to the animator
        catAnimator.runtimeAnimatorController = controller;
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Created Animator Controller at: {animatorControllerPath}");
#endif
    }
    
    void FindAnimationClips()
    {
        // Look for animation clips in the existing demo controller
        var demoController = catAnimator.runtimeAnimatorController as AnimatorController;
        if (demoController != null)
        {
            // Extract clips from the demo controller
            var clips = demoController.animationClips;
            foreach (var clip in clips)
            {
                string clipName = clip.name.ToLower();
                
                if (clipName.Contains("idle") && idleClip == null)
                    idleClip = clip;
                else if (clipName.Contains("walk") && walkClip == null)
                    walkClip = clip;
                else if (clipName.Contains("run") && runClip == null)
                    runClip = clip;
                else if (clipName.Contains("jump") && jumpClip == null)
                    jumpClip = clip;
                else if (clipName.Contains("land") && landClip == null)
                    landClip = clip;
            }
        }
        
        // If no clips found, try to find them in FBX files
        if (idleClip == null || walkClip == null)
        {
            FindClipsInFBXFiles();
        }
    }
    
    void FindClipsInFBXFiles()
    {
        // Look in the animation FBX files
        string[] fbxPaths = {
            "Assets/Cats/Cat_Fat/CatFat/FBX/Anim/CatFat_anim_RM.fbx",
            "Assets/Cats/Cat_Fat/CatFat/FBX/Anim/CatFat_anim_IP.fbx",
            "Assets/Cats/Cat_Fat/CatFat/FBX/CatFat.fbx"
        };
        
        foreach (string path in fbxPaths)
        {
#if UNITY_EDITOR
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    string clipName = clip.name.ToLower();
                    
                    if (clipName.Contains("idle") && idleClip == null)
                        idleClip = clip;
                    else if (clipName.Contains("walk") && walkClip == null)
                        walkClip = clip;
                    else if (clipName.Contains("run") && runClip == null)
                        runClip = clip;
                    else if (clipName.Contains("jump") && jumpClip == null)
                        jumpClip = clip;
                    else if (clipName.Contains("land") && landClip == null)
                        landClip = clip;
                }
            }
#endif
        }
    }
    
#if UNITY_EDITOR
    void CreateTransitions(AnimatorStateMachine sm, AnimatorState idle, AnimatorState walk, AnimatorState run, AnimatorState jump)
    {
        // Idle to Walk
        var idleToWalk = idle.AddTransition(walk);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleToWalk.AddCondition(AnimatorConditionMode.Less, walkToRunThreshold, "Speed");
        idleToWalk.duration = animationBlendTime;
        
        // Walk to Idle
        var walkToIdle = walk.AddTransition(idle);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        walkToIdle.duration = animationBlendTime;
        
        // Walk to Run
        var walkToRun = walk.AddTransition(run);
        walkToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        walkToRun.AddCondition(AnimatorConditionMode.Greater, walkToRunThreshold, "Speed");
        walkToRun.duration = animationBlendTime;
        
        // Run to Walk
        var runToWalk = run.AddTransition(walk);
        runToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        runToWalk.duration = animationBlendTime;
        
        // Run to Idle
        var runToIdle = run.AddTransition(idle);
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        runToIdle.duration = animationBlendTime;
        
        // Any State to Jump
        var anyToJump = sm.AddAnyStateTransition(jump);
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anyToJump.duration = 0.1f;
        
        // Jump back to movement states
        var jumpToIdle = jump.AddTransition(idle);
        jumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        jumpToIdle.duration = 0.2f;
        
        var jumpToWalk = jump.AddTransition(walk);
        jumpToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        jumpToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        jumpToWalk.duration = 0.2f;
        
        var jumpToRun = jump.AddTransition(run);
        jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        jumpToRun.duration = 0.2f;
    }
#endif
    
    void ConnectToPlayerController()
    {
        // Add the CatAnimationController to the same GameObject
        var animController = GetComponent<CatAnimationController>();
        if (animController == null)
        {
            animController = gameObject.AddComponent<CatAnimationController>();
        }
        
        // Configure the animation controller to work with our cat
        if (catAnimator != null)
        {
            // The CatAnimationController will automatically find the animator
            Debug.Log("Connected CatAnimationController to player");
        }
    }
    
    [ContextMenu("Test Animations")]
    public void TestAnimations()
    {
        if (catAnimator == null) return;
        
        Debug.Log("Testing cat animations...");
        Debug.Log($"Idle Clip: {(idleClip != null ? idleClip.name : "Not Found")}");
        Debug.Log($"Walk Clip: {(walkClip != null ? walkClip.name : "Not Found")}");
        Debug.Log($"Run Clip: {(runClip != null ? runClip.name : "Not Found")}");
        Debug.Log($"Jump Clip: {(jumpClip != null ? jumpClip.name : "Not Found")}");
        
        // Test setting animator parameters
        if (catAnimator.runtimeAnimatorController != null)
        {
            catAnimator.SetBool("IsMoving", true);
            catAnimator.SetFloat("Speed", 0.5f);
            Debug.Log("Set test animation parameters");
        }
    }
    
    void OnValidate()
    {
        walkToRunThreshold = Mathf.Clamp01(walkToRunThreshold);
        animationBlendTime = Mathf.Clamp(animationBlendTime, 0.1f, 1f);
    }
}