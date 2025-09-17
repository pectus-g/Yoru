using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.PostProcessing;

public class SimpleFixScript : MonoBehaviour
{
    [Header("SIMPLE FIX - NO FREEZING!")]
    [Space(10)]
    [TextArea(3, 5)]
    public string instructions = "This script is safe and simple. Click the buttons below to fix problems one by one.";
    
    void Start()
    {
        // Automatically fix the most important problems
        FixDeerCrashing();
        FixBlurryCamera();
        Debug.Log("✅ Basic fixes applied automatically!");
    }
    
    [ContextMenu("Fix Deer Crashing")]
    public void FixDeerCrashing()
    {
        Debug.Log("🦌 Fixing deer to stop crashes...");
        
        // Find all deer and turn off their NavMeshAgent to stop errors
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int deerFixed = 0;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("PF_Deer"))
            {
                NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                    deerFixed++;
                    Debug.Log($"✅ Fixed deer: {obj.name}");
                }
            }
        }
        
        Debug.Log($"✅ Fixed {deerFixed} deer - no more crashes!");
        Debug.Log("Deer will stay in place but won't crash the game.");
    }
    
    [ContextMenu("Fix Blurry Camera")]
    public void FixBlurryCamera()
    {
        Debug.Log("📷 Fixing blurry camera...");
        
        // Fix camera blur
        Camera[] allCameras = FindObjectsOfType<Camera>();
        int camerasFixed = 0;
        
        foreach (Camera cam in allCameras)
        {
            PostProcessLayer postLayer = cam.GetComponent<PostProcessLayer>();
            if (postLayer != null)
            {
                postLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                camerasFixed++;
                Debug.Log($"✅ Fixed camera blur: {cam.name}");
            }
        }
        
        // Make scene effects lighter
        GameObject ppObject = GameObject.Find("PP");
        if (ppObject != null)
        {
            PostProcessVolume volume = ppObject.GetComponent<PostProcessVolume>();
            if (volume != null)
            {
                volume.weight = 0.3f;
                Debug.Log("✅ Made scene effects lighter");
            }
        }
        
        Debug.Log($"✅ Fixed {camerasFixed} cameras - less blurry now!");
    }
    
    [ContextMenu("Turn Off All Post Effects")]
    public void TurnOffAllPostEffects()
    {
        Debug.Log("🚫 Turning off all post effects...");
        
        // Turn off all post processing volumes
        PostProcessVolume[] volumes = FindObjectsOfType<PostProcessVolume>();
        foreach (PostProcessVolume volume in volumes)
        {
            volume.enabled = false;
            Debug.Log($"✅ Turned off: {volume.name}");
        }
        
        // Turn off camera post processing
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();
            if (layer != null)
            {
                layer.enabled = false;
                Debug.Log($"✅ Turned off camera effects: {cam.name}");
            }
        }
        
        Debug.Log("✅ All post effects OFF - crystal clear camera!");
    }
    
    [ContextMenu("List All Deer in Scene")]
    public void ListAllDeer()
    {
        Debug.Log("🦌 === ALL DEER IN SCENE ===");
        
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int deerCount = 0;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("PF_Deer"))
            {
                NavMeshAgent agent = obj.GetComponent<NavMeshAgent>();
                string status = agent != null ? (agent.enabled ? "MOVING" : "DISABLED") : "NO AGENT";
                
                Debug.Log($"{deerCount + 1}. {obj.name} - Status: {status}");
                deerCount++;
            }
        }
        
        Debug.Log($"=== FOUND {deerCount} DEER TOTAL ===");
    }
}