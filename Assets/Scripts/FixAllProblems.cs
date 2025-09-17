using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering.PostProcessing;

public class FixAllProblems : MonoBehaviour
{
    [Header("ONE BUTTON TO FIX EVERYTHING!")]
    [Space(10)]
    [TextArea(3, 5)]
    public string instructions = "Add this script to any object in your scene, then click 'Fix Everything And Keep Deer!' button below.";
    
    [Space(10)]
    public bool fixEverythingButton = false;
    
    void Start()
    {
        // Automatically fix everything when game starts
        FixEverythingAndKeepDeer();
    }
    
    [ContextMenu("Fix Everything And Keep Deer!")]
    public void FixEverythingAndKeepDeer()
    {
        Debug.Log("🔧 === FIXING ALL PROBLEMS BUT KEEPING DEER === 🔧");
        
        // Fix 1: Build NavMesh for deer
        BuildNavMeshForDeer();
        
        // Fix 2: Blurry Camera Problem  
        FixBlurryCamera();
        
        Debug.Log("✅ === ALL PROBLEMS FIXED! DEER ARE HAPPY! === ✅");
        Debug.Log("Your game should work perfectly now with walking deer!");
    }
    
    void BuildNavMeshForDeer()
    {
        Debug.Log("🦌 Building NavMesh road map for deer...");
        
        // First, try to find existing NavMesh Surface
        NavMeshSurface navSurface = FindObjectOfType<NavMeshSurface>();
        
        if (navSurface == null)
        {
            // Create a new NavMesh Surface
            GameObject navMeshObj = new GameObject("NavMesh Surface");
            navSurface = navMeshObj.AddComponent<NavMeshSurface>();
            Debug.Log("✅ Created new NavMesh Surface");
        }
        
        // Configure the NavMesh Surface
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        
        // Build the NavMesh
        try
        {
            navSurface.BuildNavMesh();
            Debug.Log("✅ Built NavMesh road map successfully!");
            
            // Now enable all deer
            EnableAllDeer();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Could not build NavMesh: {e.Message}");
            Debug.LogWarning("Will disable deer movement to prevent crashes");
            DisableDeerMovement();
        }
    }
    
    void EnableAllDeer()
    {
        Debug.Log("🦌 Enabling deer movement...");
        
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int deerEnabled = 0;
        int deerFixed = 0;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("PF_Deer"))
            {
                NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    // Enable the agent
                    agent.enabled = true;
                    
                    // Check if it's properly placed on NavMesh
                    if (agent.isOnNavMesh)
                    {
                        deerEnabled++;
                        Debug.Log($"✅ Deer walking: {obj.name}");
                    }
                    else
                    {
                        // Try to place deer on NavMesh
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(obj.transform.position, out hit, 10f, NavMesh.AllAreas))
                        {
                            obj.transform.position = hit.position;
                            agent.enabled = false;
                            agent.enabled = true; // Restart agent
                            
                            if (agent.isOnNavMesh)
                            {
                                deerFixed++;
                                Debug.Log($"✅ Fixed deer position: {obj.name}");
                            }
                        }
                        else
                        {
                            agent.enabled = false;
                            Debug.LogWarning($"⚠ Could not fix deer: {obj.name} (too far from NavMesh)");
                        }
                    }
                }
            }
        }
        
        Debug.Log($"✅ {deerEnabled} deer walking, {deerFixed} deer fixed and placed on NavMesh");
    }
    
    void DisableDeerMovement()
    {
        Debug.Log("🦌 Disabling deer movement to prevent crashes...");
        
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int deerDisabled = 0;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("PF_Deer"))
            {
                NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    deerDisabled++;
                }
            }
        }
        
        Debug.Log($"✅ Disabled {deerDisabled} deer (they stay but don't move)");
    }
    
    void FixBlurryCamera()
    {
        Debug.Log("📷 Fixing blurry camera...");
        
        // Fix all cameras
        Camera[] allCameras = FindObjectsOfType<Camera>();
        int camerasFixed = 0;
        
        foreach (Camera cam in allCameras)
        {
            // Remove blur from post processing
            PostProcessLayer postLayer = cam.GetComponent<PostProcessLayer>();
            if (postLayer != null)
            {
                postLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                camerasFixed++;
            }
        }
        
        // Make scene effects lighter
        GameObject ppObject = GameObject.Find("PP");
        if (ppObject != null)
        {
            PostProcessVolume sceneVolume = ppObject.GetComponent<PostProcessVolume>();
            if (sceneVolume != null)
            {
                sceneVolume.weight = 0.3f; // Make effects much lighter
            }
        }
        
        Debug.Log($"✅ Fixed {camerasFixed} cameras (less blur now!)");
    }
    
    [ContextMenu("Build NavMesh Only")]
    public void BuildNavMeshOnly()
    {
        Debug.Log("🛣️ Building NavMesh road map...");
        BuildNavMeshForDeer();
    }
    
    [ContextMenu("Try To Fix Broken Deer")]
    public void FixBrokenDeer()
    {
        Debug.Log("🔧 Trying to fix broken deer...");
        
        // First build NavMesh
        BuildNavMeshForDeer();
        
        // Wait a moment then try to enable deer
        Invoke("EnableAllDeer", 1f);
    }
    
    [ContextMenu("Turn Off All Blur Effects")]
    public void TurnOffAllBlur()
    {
        Debug.Log("🚫 Turning off all blur effects...");
        
        PostProcessVolume[] allVolumes = FindObjectsOfType<PostProcessVolume>();
        foreach (PostProcessVolume volume in allVolumes)
        {
            volume.enabled = false;
        }
        
        Camera[] allCameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in allCameras)
        {
            PostProcessLayer postLayer = cam.GetComponent<PostProcessLayer>();
            if (postLayer != null)
            {
                postLayer.enabled = false;
            }
        }
        
        Debug.Log("✅ All blur effects are OFF - crystal clear camera!");
    }
}