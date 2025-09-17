using UnityEngine;
using UnityEngine.AI;
using Cinemachine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class CatPlayerSetup : MonoBehaviour
{
    [Header("Setup Configuration")]
    public bool autoSetupOnStart = true;
    public GameObject catPrefab;
    public string catPlayerTag = "Player";
    
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float jumpHeight = 2.5f;
    public float doubleJumpHeight = 2.0f;
    
    [Header("Camera Settings")]
    public float mouseSensitivity = 100f;
    public bool enableAutoFollow = true;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupCatPlayer();
        }
    }
    
    [ContextMenu("Setup Cat Player")]
    public void SetupCatPlayer()
    {
        GameObject player = this.gameObject;
        
        // Ensure we have the basic components
        SetupCharacterController(player);
        SetupNavMeshAgent(player);
        SetupCatPlayerController(player);
        SetupAnimationManager(player);
        SetupCameraTarget(player);
        SetupCinemachine(player);
        SetupSmoothCamera(player);
        
        // Set proper tag and layer
        player.tag = catPlayerTag;
        
        Debug.Log($"Cat player setup complete on {player.name}!");
    }
    
    void SetupCharacterController(GameObject player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = player.AddComponent<CharacterController>();
        }
        
        // Set appropriate values for a cat
        cc.height = 1.8f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.08f;
        
        Debug.Log("CharacterController setup complete");
    }
    
    void SetupNavMeshAgent(GameObject player)
    {
        NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = player.AddComponent<NavMeshAgent>();
        }
        
        // Configure for cat movement
        agent.speed = runSpeed;
        agent.acceleration = 15f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;
        agent.height = 1.8f;
        agent.radius = 0.5f;
        agent.baseOffset = 0f;
        
        // Important: Let our controller handle position and rotation
        agent.updatePosition = false;
        agent.updateRotation = false;
        
        Debug.Log("NavMeshAgent setup complete");
    }
    
    void SetupAnimationManager(GameObject player)
    {
        // Remove old animation components
        SimpleCatAnimator oldSimple = player.GetComponent<SimpleCatAnimator>();
        if (oldSimple != null)
        {
            DestroyImmediate(oldSimple);
            Debug.Log("Removed old SimpleCatAnimator");
        }
        
        CatAnimationManager oldComplex = player.GetComponent<CatAnimationManager>();
        if (oldComplex != null)
        {
            DestroyImmediate(oldComplex);
            Debug.Log("Removed old CatAnimationManager");
        }
        
        FixedCatAnimator oldFixed = player.GetComponent<FixedCatAnimator>();
        if (oldFixed != null)
        {
            DestroyImmediate(oldFixed);
            Debug.Log("Removed old FixedCatAnimator");
        }
        
        // Add the manual animation system
        ManualCatAnimator manualAnimator = player.GetComponent<ManualCatAnimator>();
        if (manualAnimator == null)
        {
            manualAnimator = player.AddComponent<ManualCatAnimator>();
        }
        
        // Find and configure the cat animator
        Animator catAnimator = player.GetComponentInChildren<Animator>();
        if (catAnimator != null)
        {
            manualAnimator.catAnimator = catAnimator;
            
            // Force disable root motion
            catAnimator.applyRootMotion = false;
            catAnimator.updateMode = AnimatorUpdateMode.Normal;
            catAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            
            Debug.Log("Manual animation system setup complete with animator: " + catAnimator.name);
            Debug.Log("Now you can manually assign animations in the Inspector!");
        }
        else
        {
            Debug.LogWarning("No Animator found in children! Make sure your cat model has an Animator component.");
        }
    }
    
    void SetupCatPlayerController(GameObject player)
    {
        CatPlayerController controller = player.GetComponent<CatPlayerController>();
        if (controller == null)
        {
            controller = player.AddComponent<CatPlayerController>();
        }
        
        // Configure movement settings
        controller.walkSpeed = walkSpeed;
        controller.runSpeed = runSpeed;
        controller.jumpHeight = jumpHeight;
        controller.doubleJumpHeight = doubleJumpHeight;
        controller.rotationSpeed = 720f;
        controller.gravity = -20f;
        controller.groundedGravity = -2f;
        
        // Setup camera target reference
        Transform cameraTarget = player.transform.Find("CameraTarget");
        if (cameraTarget != null)
        {
            controller.cameraFollowTarget = cameraTarget;
        }
        
        Debug.Log("CatPlayerController setup complete");
    }
    
    void SetupCameraTarget(GameObject player)
    {
        Transform cameraTarget = player.transform.Find("CameraTarget");
        if (cameraTarget == null)
        {
            GameObject targetGO = new GameObject("CameraTarget");
            targetGO.transform.SetParent(player.transform);
            targetGO.transform.localPosition = new Vector3(0, 1.5f, 0);
            cameraTarget = targetGO.transform;
        }
        
        // Update the cat player controller reference
        CatPlayerController controller = player.GetComponent<CatPlayerController>();
        if (controller != null)
        {
            controller.cameraFollowTarget = cameraTarget;
        }
        
        Debug.Log("Camera target setup complete");
    }
    
    void SetupCinemachine(GameObject player)
    {
        // Find or create Cinemachine FreeLook camera
        CinemachineFreeLook freeLook = FindObjectOfType<CinemachineFreeLook>();
        
        if (freeLook == null)
        {
            // Create new FreeLook camera
            GameObject freeLookGO = new GameObject("CM FreeLook Camera");
            freeLookGO.transform.SetParent(player.transform);
            freeLook = freeLookGO.AddComponent<CinemachineFreeLook>();
            
            // Add required rigs
            freeLook.m_Orbits = new CinemachineFreeLook.Orbit[]
            {
                new CinemachineFreeLook.Orbit(3f, 8f),    // Top rig
                new CinemachineFreeLook.Orbit(1.5f, 5f),  // Middle rig  
                new CinemachineFreeLook.Orbit(0.5f, 3f)   // Bottom rig
            };
        }
        
        // Configure the camera
        Transform cameraTarget = player.transform.Find("CameraTarget");
        if (cameraTarget != null)
        {
            freeLook.Follow = cameraTarget;
            freeLook.LookAt = cameraTarget;
        }
        
        // Configure smooth movement
        freeLook.m_XAxis.m_MaxSpeed = 0f; // Controlled by our script
        freeLook.m_YAxis.m_MaxSpeed = 0f; // Controlled by our script
        freeLook.m_XAxis.m_AccelTime = 0.1f;
        freeLook.m_XAxis.m_DecelTime = 0.1f;
        freeLook.m_YAxis.m_AccelTime = 0.1f;
        freeLook.m_YAxis.m_DecelTime = 0.1f;
        
        // Update the controller reference
        CatPlayerController controller = player.GetComponent<CatPlayerController>();
        if (controller != null)
        {
            controller.freeLookCamera = freeLook;
        }
        
        Debug.Log("Cinemachine FreeLook camera setup complete");
    }
    
    void SetupSmoothCamera(GameObject player)
    {
        SmoothCameraController smoothCam = player.GetComponent<SmoothCameraController>();
        if (smoothCam == null)
        {
            smoothCam = player.AddComponent<SmoothCameraController>();
        }
        
        // Configure smooth camera settings
        smoothCam.mouseSensitivity = mouseSensitivity;
        smoothCam.enableAutoFollow = enableAutoFollow;
        smoothCam.autoFollowDelay = 2f;
        smoothCam.smoothTime = 0.1f;
        
        // Set FreeLook camera reference
        CinemachineFreeLook freeLook = FindObjectOfType<CinemachineFreeLook>();
        if (freeLook != null)
        {
            smoothCam.freeLookCamera = freeLook;
        }
        
        Debug.Log("Smooth camera controller setup complete");
    }
    
    [ContextMenu("Validate Setup")]
    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        GameObject player = this.gameObject;
        
        Debug.Log("=== Cat Player Setup Validation ===");
        
        // Check required components
        Debug.Log($"CharacterController: {(player.GetComponent<CharacterController>() != null ? "✓" : "✗")}");
        Debug.Log($"NavMeshAgent: {(player.GetComponent<NavMeshAgent>() != null ? "✓" : "✗")}");
        Debug.Log($"CatPlayerController: {(player.GetComponent<CatPlayerController>() != null ? "✓" : "✗")}");
        Debug.Log($"ManualCatAnimator: {(player.GetComponent<ManualCatAnimator>() != null ? "✓" : "✗")}");
        Debug.Log($"SmoothCameraController: {(player.GetComponent<SmoothCameraController>() != null ? "✓" : "✗")}");
        
        // Check camera setup
        Transform cameraTarget = player.transform.Find("CameraTarget");
        Debug.Log($"Camera Target: {(cameraTarget != null ? "✓" : "✗")}");
        
        CinemachineFreeLook freeLook = FindObjectOfType<CinemachineFreeLook>();
        Debug.Log($"Cinemachine FreeLook: {(freeLook != null ? "✓" : "✗")}");
        
        // Check tag
        Debug.Log($"Player Tag: {(player.tag == catPlayerTag ? "✓" : "✗")} (Current: {player.tag})");
        
        Debug.Log("=== Validation Complete ===");
    }
    
    [ContextMenu("Fix Animation Issues")]
    public void FixAnimationIssues()
    {
        GameObject player = this.gameObject;
        
        Debug.Log("=== Fixing Animation Issues ===");
        
        // Find the cat animator
        Animator catAnimator = player.GetComponentInChildren<Animator>();
        if (catAnimator != null)
        {
            // Disable root motion to prevent movement conflicts
            catAnimator.applyRootMotion = false;
            Debug.Log("✓ Disabled root motion");
            
            // Check the Simple Cat Animator
            SimpleCatAnimator simpleAnimator = player.GetComponent<SimpleCatAnimator>();
            if (simpleAnimator != null)
            {
                simpleAnimator.catAnimator = catAnimator;
                // Force re-detection of animations
                simpleAnimator.RedetectAnimations();
                Debug.Log("✓ Re-detected animations");
            }
            
            // Check the player controller
            CatPlayerController controller = player.GetComponent<CatPlayerController>();
            if (controller != null)
            {
                // Make sure movement direction is correct
                controller.rotationSpeed = 720f; // Faster rotation to fix backwards movement
                Debug.Log("✓ Fixed rotation speed");
            }
        }
        
        Debug.Log("=== Animation Issues Fixed ===");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CatPlayerSetup))]
public class CatPlayerSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        CatPlayerSetup setup = (CatPlayerSetup)target;
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Setup Cat Player", GUILayout.Height(40)))
        {
            setup.SetupCatPlayer();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Validate Setup", GUILayout.Height(30)))
        {
            setup.ValidateSetup();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Fix Animation Issues", GUILayout.Height(30)))
        {
            setup.FixAnimationIssues();
        }
    }
}
#endif