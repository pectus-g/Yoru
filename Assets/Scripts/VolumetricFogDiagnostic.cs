using UnityEngine;
using System.Reflection;

/// <summary>
/// DIAGNOSTIC TOOL for Kronnect Volumetric Fog & Mist (Built-in Pipeline)
/// Add this to any GameObject, then use the context menu or keyboard shortcuts to test fog.
/// 
/// USAGE:
/// 1. Add this script to any GameObject (e.g., YORU_BalanceSystem or Main Camera)
/// 2. Enter Play mode
/// 3. Press F9 to FORCE THICK FOG (very visible test)
/// 4. Press F10 to RESTORE original settings
/// 5. Press F11 to LOG all current fog values
/// 
/// If fog appears with F9, then Kronnect is working - we just need correct values.
/// If fog does NOT appear with F9, there's a rendering/setup issue.
/// </summary>
public class VolumetricFogDiagnostic : MonoBehaviour
{
    [Header("=== FOG REFERENCE ===")]
    [Tooltip("Will auto-find if not assigned")]
    public MonoBehaviour volumetricFogComponent;
    
    [Header("=== STATUS ===")]
    [SerializeField] private bool fogFound = false;
    [SerializeField] private string fogStatus = "Not checked";
    
    [Header("=== CURRENT VALUES (Read Only) ===")]
    [SerializeField] private float currentDensity;
    [SerializeField] private float currentHeight;
    [SerializeField] private float currentBaselineHeight;
    [SerializeField] private float currentAlpha;
    [SerializeField] private float currentSkyHaze;
    [SerializeField] private float currentSkyAlpha;
    [SerializeField] private Color currentColor;
    
    [Header("=== TEST SETTINGS ===")]
    [Tooltip("Density for thick fog test")]
    public float testDensity = 0.8f;
    [Tooltip("Height for test (should be above player)")]
    public float testHeight = 200f;
    [Tooltip("Base height for test")]
    public float testBaselineHeight = 0f;
    [Tooltip("Alpha for test")]
    public float testAlpha = 1f;
    [Tooltip("Color for test fog")]
    public Color testColor = new Color(0.5f, 0.5f, 0.6f, 1f);
    
    // Cached reflection info
    private PropertyInfo densityProp;
    private PropertyInfo heightProp;
    private PropertyInfo baselineHeightProp;
    private PropertyInfo alphaProp;
    private PropertyInfo colorProp;
    private PropertyInfo skyHazeProp;
    private PropertyInfo skyAlphaProp;
    private PropertyInfo noiseStrengthProp;
    private PropertyInfo speedProp;
    
    // Original values for restore
    private float originalDensity;
    private float originalHeight;
    private float originalBaselineHeight;
    private float originalAlpha;
    private Color originalColor;
    private float originalSkyHaze;
    private float originalSkyAlpha;
    private bool hasStoredOriginals = false;
    
    void Start()
    {
        FindAndAnalyzeFog();
    }
    
    void Update()
    {
        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.F9))
        {
            ForceThickFog();
        }
        if (Input.GetKeyDown(KeyCode.F10))
        {
            RestoreOriginalFog();
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            LogAllFogValues();
        }
        
        // Continuously update current values display
        if (fogFound && volumetricFogComponent != null)
        {
            UpdateCurrentValuesDisplay();
        }
    }
    
    [ContextMenu("1. Find and Analyze Fog")]
    public void FindAndAnalyzeFog()
    {
        Debug.Log("=== VOLUMETRIC FOG DIAGNOSTIC ===");
        
        // Find the VolumetricFog component
        if (volumetricFogComponent == null)
        {
            // Search on all cameras first
            foreach (var cam in Camera.allCameras)
            {
                var fog = FindFogOnGameObject(cam.gameObject);
                if (fog != null)
                {
                    volumetricFogComponent = fog;
                    Debug.Log($"✓ Found VolumetricFog on camera: {cam.name}");
                    break;
                }
            }
            
            // If not on camera, search everywhere
            if (volumetricFogComponent == null)
            {
                var allMonos = FindObjectsOfType<MonoBehaviour>();
                foreach (var mono in allMonos)
                {
                    if (mono.GetType().Name == "VolumetricFog")
                    {
                        volumetricFogComponent = mono;
                        Debug.Log($"✓ Found VolumetricFog on: {mono.gameObject.name}");
                        break;
                    }
                }
            }
        }
        
        if (volumetricFogComponent == null)
        {
            fogFound = false;
            fogStatus = "ERROR: VolumetricFog component not found!";
            Debug.LogError(fogStatus);
            Debug.LogError("Make sure you have the Kronnect Volumetric Fog & Mist component added to your camera.");
            return;
        }
        
        // Get reflection info for properties
        var fogType = volumetricFogComponent.GetType();
        densityProp = fogType.GetProperty("density");
        heightProp = fogType.GetProperty("height");
        baselineHeightProp = fogType.GetProperty("baselineHeight");
        alphaProp = fogType.GetProperty("alpha");
        colorProp = fogType.GetProperty("color");
        skyHazeProp = fogType.GetProperty("skyHaze");
        skyAlphaProp = fogType.GetProperty("skyAlpha");
        noiseStrengthProp = fogType.GetProperty("noiseStrength");
        speedProp = fogType.GetProperty("speed");
        
        // Check which properties exist
        Debug.Log($"Properties found:");
        Debug.Log($"  density: {(densityProp != null ? "✓" : "✗")}");
        Debug.Log($"  height: {(heightProp != null ? "✓" : "✗")}");
        Debug.Log($"  baselineHeight: {(baselineHeightProp != null ? "✓" : "✗")}");
        Debug.Log($"  alpha: {(alphaProp != null ? "✓" : "✗")}");
        Debug.Log($"  color: {(colorProp != null ? "✓" : "✗")}");
        Debug.Log($"  skyHaze: {(skyHazeProp != null ? "✓" : "✗")}");
        Debug.Log($"  skyAlpha: {(skyAlphaProp != null ? "✓" : "✗")}");
        
        if (densityProp != null && heightProp != null)
        {
            fogFound = true;
            fogStatus = "✓ Fog component found and properties accessible";
            StoreOriginalValues();
            LogAllFogValues();
        }
        else
        {
            fogFound = false;
            fogStatus = "ERROR: Required properties not found on VolumetricFog";
            Debug.LogError(fogStatus);
        }
    }
    
    private MonoBehaviour FindFogOnGameObject(GameObject go)
    {
        var components = go.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp != null && comp.GetType().Name == "VolumetricFog")
            {
                return comp;
            }
        }
        return null;
    }
    
    private void StoreOriginalValues()
    {
        if (!fogFound || volumetricFogComponent == null) return;
        
        try
        {
            if (densityProp != null) originalDensity = (float)densityProp.GetValue(volumetricFogComponent);
            if (heightProp != null) originalHeight = (float)heightProp.GetValue(volumetricFogComponent);
            if (baselineHeightProp != null) originalBaselineHeight = (float)baselineHeightProp.GetValue(volumetricFogComponent);
            if (alphaProp != null) originalAlpha = (float)alphaProp.GetValue(volumetricFogComponent);
            if (colorProp != null) originalColor = (Color)colorProp.GetValue(volumetricFogComponent);
            if (skyHazeProp != null) originalSkyHaze = (float)skyHazeProp.GetValue(volumetricFogComponent);
            if (skyAlphaProp != null) originalSkyAlpha = (float)skyAlphaProp.GetValue(volumetricFogComponent);
            
            hasStoredOriginals = true;
            Debug.Log("✓ Original fog values stored");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to store original values: {e.Message}");
        }
    }
    
    private void UpdateCurrentValuesDisplay()
    {
        try
        {
            if (densityProp != null) currentDensity = (float)densityProp.GetValue(volumetricFogComponent);
            if (heightProp != null) currentHeight = (float)heightProp.GetValue(volumetricFogComponent);
            if (baselineHeightProp != null) currentBaselineHeight = (float)baselineHeightProp.GetValue(volumetricFogComponent);
            if (alphaProp != null) currentAlpha = (float)alphaProp.GetValue(volumetricFogComponent);
            if (colorProp != null) currentColor = (Color)colorProp.GetValue(volumetricFogComponent);
            if (skyHazeProp != null) currentSkyHaze = (float)skyHazeProp.GetValue(volumetricFogComponent);
            if (skyAlphaProp != null) currentSkyAlpha = (float)skyAlphaProp.GetValue(volumetricFogComponent);
        }
        catch { }
    }
    
    [ContextMenu("2. Force THICK FOG (F9)")]
    public void ForceThickFog()
    {
        if (!fogFound || volumetricFogComponent == null)
        {
            Debug.LogError("Cannot force fog - component not found. Run 'Find and Analyze Fog' first.");
            return;
        }
        
        Debug.Log("=== FORCING THICK FOG TEST ===");
        
        try
        {
            // Store originals if we haven't
            if (!hasStoredOriginals) StoreOriginalValues();
            
            // Set EXTREME values to force visibility
            if (densityProp != null)
            {
                densityProp.SetValue(volumetricFogComponent, testDensity);
                Debug.Log($"  density: {originalDensity} → {testDensity}");
            }
            
            if (heightProp != null)
            {
                heightProp.SetValue(volumetricFogComponent, testHeight);
                Debug.Log($"  height: {originalHeight} → {testHeight}");
            }
            
            if (baselineHeightProp != null)
            {
                baselineHeightProp.SetValue(volumetricFogComponent, testBaselineHeight);
                Debug.Log($"  baselineHeight: {originalBaselineHeight} → {testBaselineHeight}");
            }
            
            if (alphaProp != null)
            {
                alphaProp.SetValue(volumetricFogComponent, testAlpha);
                Debug.Log($"  alpha: {originalAlpha} → {testAlpha}");
            }
            
            if (colorProp != null)
            {
                colorProp.SetValue(volumetricFogComponent, testColor);
                Debug.Log($"  color: {originalColor} → {testColor}");
            }
            
            // Also increase sky haze
            if (skyHazeProp != null)
            {
                skyHazeProp.SetValue(volumetricFogComponent, 100f);
                Debug.Log($"  skyHaze: {originalSkyHaze} → 100");
            }
            
            if (skyAlphaProp != null)
            {
                skyAlphaProp.SetValue(volumetricFogComponent, 1f);
                Debug.Log($"  skyAlpha: {originalSkyAlpha} → 1");
            }
            
            Debug.Log("=== THICK FOG APPLIED ===");
            Debug.Log("If you DON'T see thick fog now, there's a rendering issue.");
            Debug.Log("Check: Camera depth texture, shader compatibility, etc.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to set fog values: {e.Message}");
        }
    }
    
    [ContextMenu("3. Restore Original Fog (F10)")]
    public void RestoreOriginalFog()
    {
        if (!hasStoredOriginals)
        {
            Debug.LogWarning("No original values stored - cannot restore.");
            return;
        }
        
        Debug.Log("=== RESTORING ORIGINAL FOG ===");
        
        try
        {
            if (densityProp != null) densityProp.SetValue(volumetricFogComponent, originalDensity);
            if (heightProp != null) heightProp.SetValue(volumetricFogComponent, originalHeight);
            if (baselineHeightProp != null) baselineHeightProp.SetValue(volumetricFogComponent, originalBaselineHeight);
            if (alphaProp != null) alphaProp.SetValue(volumetricFogComponent, originalAlpha);
            if (colorProp != null) colorProp.SetValue(volumetricFogComponent, originalColor);
            if (skyHazeProp != null) skyHazeProp.SetValue(volumetricFogComponent, originalSkyHaze);
            if (skyAlphaProp != null) skyAlphaProp.SetValue(volumetricFogComponent, originalSkyAlpha);
            
            Debug.Log("✓ Original fog values restored");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to restore fog values: {e.Message}");
        }
    }
    
    [ContextMenu("4. Log All Fog Values (F11)")]
    public void LogAllFogValues()
    {
        if (!fogFound || volumetricFogComponent == null)
        {
            Debug.LogError("Fog not found - run Find and Analyze first");
            return;
        }
        
        Debug.Log("=== CURRENT VOLUMETRIC FOG VALUES ===");
        
        var fogType = volumetricFogComponent.GetType();
        
        // Log all important properties
        string[] importantProps = {
            "density", "noiseStrength", "noiseFinalMultiplier", "noiseSparse",
            "height", "heightFallOff", "baselineHeight", "baselineRelativeToCamera",
            "alpha", "color", "specularColor", "specularIntensity",
            "distance", "distanceFallOff", "maxFogLength",
            "skyHaze", "skyAlpha", "skyColor", "skyDepth",
            "lightScatteringEnabled", "lightScatteringDiffusion",
            "speed", "turbulenceStrength"
        };
        
        foreach (var propName in importantProps)
        {
            var prop = fogType.GetProperty(propName);
            if (prop != null)
            {
                try
                {
                    var value = prop.GetValue(volumetricFogComponent);
                    Debug.Log($"  {propName}: {value}");
                }
                catch
                {
                    Debug.Log($"  {propName}: <error reading>");
                }
            }
        }
        
        // Also log component enabled state
        Debug.Log($"  Component Enabled: {volumetricFogComponent.enabled}");
        Debug.Log($"  GameObject Active: {volumetricFogComponent.gameObject.activeInHierarchy}");
        
        // Log camera info
        var cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"=== CAMERA INFO ===");
            Debug.Log($"  Main Camera: {cam.name}");
            Debug.Log($"  Position: {cam.transform.position}");
            Debug.Log($"  Depth Texture Mode: {cam.depthTextureMode}");
            Debug.Log($"  Clear Flags: {cam.clearFlags}");
            Debug.Log($"  Far Clip: {cam.farClipPlane}");
        }
        
        // Log player position if found
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Debug.Log($"=== PLAYER INFO ===");
            Debug.Log($"  Position: {player.transform.position}");
            Debug.Log($"  Height (Y): {player.transform.position.y}");
        }
    }
    
    [ContextMenu("5. Check Fog Geometry Section")]
    public void CheckFogGeometry()
    {
        if (!fogFound || volumetricFogComponent == null)
        {
            Debug.LogError("Fog not found");
            return;
        }
        
        Debug.Log("=== FOG GEOMETRY CHECK ===");
        
        var fogType = volumetricFogComponent.GetType();
        
        // Check for fog area properties
        string[] geometryProps = {
            "fogAreaTopology", "fogAreaRadius", "fogAreaPosition", 
            "fogAreaDepth", "fogAreaHeight", "isFogAreaActive",
            "fogVoidTopology", "fogVoidRadius", "fogVoidPosition"
        };
        
        foreach (var propName in geometryProps)
        {
            var prop = fogType.GetProperty(propName);
            if (prop != null)
            {
                try
                {
                    var value = prop.GetValue(volumetricFogComponent);
                    Debug.Log($"  {propName}: {value}");
                }
                catch
                {
                    Debug.Log($"  {propName}: <error>");
                }
            }
        }
    }
    
    void OnGUI()
    {
        // Show status overlay
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== VOLUMETRIC FOG DIAGNOSTIC ===");
        GUILayout.Label($"Status: {fogStatus}");
        
        if (fogFound)
        {
            GUILayout.Label($"Density: {currentDensity:F2}");
            GUILayout.Label($"Height: {currentHeight:F1}");
            GUILayout.Label($"Base Height: {currentBaselineHeight:F1}");
            GUILayout.Label($"Alpha: {currentAlpha:F2}");
            GUILayout.Space(10);
            GUILayout.Label("F9 = Force Thick Fog | F10 = Restore | F11 = Log Values");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}