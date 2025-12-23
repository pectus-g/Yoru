using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// YORU: Complete COZY 3 Integration - V6
/// 
/// Changes:
/// - Eclipse at 3PM (15:00) - sun higher in sky
/// - Proper grass detection from terrain detail prefabs
/// - Excludes rock shaders that just have "grass" in name
/// </summary>
public class YoruCozyIntegration : MonoBehaviour
{
    [Header("=== WEATHER PROFILES ===")]
    public WeatherProfile clearWeather;
    public WeatherProfile mostlyClearWeather;
    public WeatherProfile partlyCloudyWeather;
    public WeatherProfile overcastWeather;
    public WeatherProfile lightRainWeather;
    public WeatherProfile heavyRainWeather;
    public WeatherProfile thunderStormWeather;
    
    [Header("=== TIME SETTINGS ===")]
    [SerializeField] private int neutralHour = 12;
    [SerializeField] private int sunset5LeftHour = 18;
    [SerializeField] private int midnight10LeftHour = 0;
    [SerializeField] private int morning5RightHour = 9;
    [SerializeField] private int lateMorning10RightHour = 11;
    
    [Header("=== ECLIPSE SETTINGS ===")]
    [Tooltip("Eclipse hour - 15.5 = 3:30 PM (sweet spot)")]
    [SerializeField] private float eclipseHour = 15.5f;
    
    // Force eclipse hour in case Inspector has old cached value
    private const float FORCED_ECLIPSE_HOUR = 15.5f;
    
    [Header("=== WIND SETTINGS ===")]
    public WindZone sceneWindZone;
    public Terrain sceneTerrain;
    [SerializeField] private float calmWindSpeed = 0.5f;
    [SerializeField] private float stormWindSpeed = 3f;
    [SerializeField] private float maxStormWindSpeed = 6f;
    
    [Header("=== MATERIALS (Auto-Found) ===")]
    [Tooltip("Tree/plant foliage materials")]
    public List<Material> foliageMaterials = new List<Material>();
    [Tooltip("Grass/plant materials from terrain details")]
    public List<Material> grassMaterials = new List<Material>();
    
    [Header("=== MANUAL GRASS MATERIALS ===")]
    [Tooltip("Drag grass materials here if auto-detection doesn't find them (e.g. M_Eastlands_Grass_Medium_01)")]
    public List<Material> manualGrassMaterials = new List<Material>();
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    // COZY References
    private CozyWeather cozy;
    private CozyWeatherModule weatherModule;
    
    // Eclipse - DIRECT component reference
    private Component eclipseComponent;
    private FieldInfo eclipseRatioField;
    private bool eclipseReady = false;
    
    // State
    private int lastLeft = -1;
    private int lastRight = -1;
    
    void Start()
    {
        InitializeCozy();
        
        if (cozy == null)
        {
            Debug.LogError("[YORU] COZY not found!");
            enabled = false;
            return;
        }
        
        AutoFindComponents();
        FindEclipseComponentDirect();
        
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            ApplyFullState(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        else
        {
            ApplyFullState(0, 0);
        }
        
        LogStatus();
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
    }
    
    void InitializeCozy()
    {
        cozy = CozyWeather.instance;
        if (cozy != null)
        {
            weatherModule = cozy.weatherModule;
        }
    }
    
    void AutoFindComponents()
    {
        if (sceneTerrain == null)
            sceneTerrain = Terrain.activeTerrain;
        
        if (sceneWindZone == null)
            sceneWindZone = FindObjectOfType<WindZone>();
        
        if (foliageMaterials.Count == 0)
            FindFoliageMaterials();
        
        if (grassMaterials.Count == 0)
            FindGrassMaterialsFromTerrain();
    }
    
    /// <summary>
    /// Find tree/plant foliage materials from scene renderers
    /// </summary>
    void FindFoliageMaterials()
    {
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<Material> found = new HashSet<Material>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                
                string shaderName = mat.shader.name.ToLower();
                
                // Only actual foliage shaders (trees, plants) - NOT rocks
                if ((shaderName.Contains("foliage") || 
                     shaderName.Contains("trunk") ||
                     shaderName.Contains("leaves")) &&
                    !shaderName.Contains("rock"))
                {
                    if (HasWindProperty(mat))
                    {
                        found.Add(mat);
                    }
                }
            }
        }
        
        foliageMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Found {found.Count} foliage materials");
    }
    
    /// <summary>
    /// Find grass materials from terrain detail prototypes.
    /// These are the actual grass mesh prefabs placed on terrain.
    /// Also searches scene for grass materials with Builtin/Grass shader.
    /// Also searches terrain layers for grass textures.
    /// </summary>
    void FindGrassMaterialsFromTerrain()
    {
        HashSet<Material> found = new HashSet<Material>();
        
        // FIRST: Search terrain detail prototypes
        if (sceneTerrain != null && sceneTerrain.terrainData != null)
        {
            var td = sceneTerrain.terrainData;
            
            // Search detail prototypes (mesh grass)
            foreach (var proto in td.detailPrototypes)
            {
                if (proto.prototype == null) continue;
                
                var renderers = proto.prototype.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null) continue;
                        
                        string shaderName = mat.shader.name.ToLower();
                        string matName = mat.name.ToLower();
                        
                        // Dreamscape Foliage OR Builtin/Grass shader OR material name contains grass
                        bool isGrassShader = shaderName.Contains("foliage") || 
                                            shaderName.Contains("grass") ||
                                            shaderName.Contains("dreamscape");
                        bool isGrassMat = matName.Contains("grass");
                        bool isRock = shaderName.Contains("rock");
                        
                        if ((isGrassShader || isGrassMat) && !isRock)
                        {
                            // Add regardless of wind properties - we'll try to animate it anyway
                            found.Add(mat);
                            if (logChanges)
                            {
                                bool hasWind = HasWindProperty(mat);
                                Debug.Log($"[YORU] Found grass (terrain detail): {mat.name} ({mat.shader.name}) Wind:{hasWind}");
                            }
                        }
                    }
                }
            }
            
            // Check terrain detail prototypes for prototype render mode materials
            // (GPU instanced grass uses different rendering)
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                var proto = td.detailPrototypes[i];
                
                // Log the detail prototype info for debugging
                if (logChanges && proto.prototype != null)
                {
                    Debug.Log($"[YORU] Detail prototype {i}: {proto.prototype.name}, RenderMode: {proto.renderMode}");
                }
            }
        }
        
        // SECOND: Search ALL scene renderers for grass materials
        // This catches grass that isn't in terrain details
        var sceneRenderers = FindObjectsOfType<Renderer>();
        foreach (var renderer in sceneRenderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                
                // Use the new IsGrassMaterial check - more inclusive
                if (IsGrassMaterial(mat) && !found.Contains(mat))
                {
                    found.Add(mat);
                    if (logChanges)
                    {
                        bool hasWind = HasWindProperty(mat);
                        Debug.Log($"[YORU] Found grass (scene): {mat.name} ({mat.shader.name}) Wind:{hasWind}");
                    }
                }
            }
        }
        
        // THIRD: Add any manually assigned grass materials
        foreach (var mat in manualGrassMaterials)
        {
            if (mat != null && !found.Contains(mat))
            {
                found.Add(mat);
                if (logChanges)
                    Debug.Log($"[YORU] Found grass (manual): {mat.name} ({mat.shader.name})");
            }
        }
        
        grassMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Found {found.Count} total grass materials");
        
        // If no grass found with wind properties, warn user
        if (found.Count == 0)
        {
            Debug.LogWarning("[YORU] No grass materials with wind properties found! " +
                "Drag your grass materials into 'Manual Grass Materials' in the Inspector.");
        }
    }
    
    bool HasWindProperty(Material mat)
    {
        // Check all known wind property variants
        return mat.HasProperty("_Wind_Intensity") ||
               mat.HasProperty("_Sway_Wind_Intensity") ||
               mat.HasProperty("_WindIntensity") ||
               mat.HasProperty("_Wind_Large_Intensity") ||
               mat.HasProperty("_WindSpeed") ||
               mat.HasProperty("_Wind_Speed") ||
               // Additional grass-specific properties
               mat.HasProperty("_WaveSpeed") ||
               mat.HasProperty("_WaveStrength") ||
               mat.HasProperty("_SwaySpeed") ||
               mat.HasProperty("_SwayAmount") ||
               mat.HasProperty("_Sway") ||
               mat.HasProperty("_WindDirection") ||
               mat.HasProperty("_WindStrength");
    }
    
    /// <summary>
    /// Check if material is likely a grass material by name/shader
    /// </summary>
    bool IsGrassMaterial(Material mat)
    {
        if (mat == null || mat.shader == null) return false;
        
        string shaderName = mat.shader.name.ToLower();
        string matName = mat.name.ToLower();
        
        // EXCLUDE rock shaders entirely (even if material name contains "grass")
        if (shaderName.Contains("rock") || shaderName.Contains("m_rocks"))
            return false;
        
        // Is it a grass shader?
        if (shaderName.Contains("grass") || shaderName.Contains("builtin/grass"))
            return true;
        
        // Is it a foliage shader AND named like grass?
        if (shaderName.Contains("foliage") && matName.Contains("grass"))
            return true;
        
        return false;
    }
    
    #region ECLIPSE - DIRECT COMPONENT ACCESS
    
    void FindEclipseComponentDirect()
    {
        if (cozy == null) return;
        
        Component[] allComponents = cozy.gameObject.GetComponents<Component>();
        
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;
            
            System.Type compType = comp.GetType();
            if (compType.Name == "EclipseModule")
            {
                eclipseComponent = comp;
                eclipseRatioField = compType.GetField("eclipseRatio", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (eclipseRatioField != null)
                {
                    eclipseReady = true;
                    Debug.Log($"[YORU] ✓ Eclipse Module found! (FORCED to {FormatHourFloat(FORCED_ECLIPSE_HOUR)})");
                }
                return;
            }
        }
        
        // Check children
        Component[] childComponents = cozy.gameObject.GetComponentsInChildren<Component>(true);
        foreach (var comp in childComponents)
        {
            if (comp == null) continue;
            
            System.Type compType = comp.GetType();
            if (compType.Name == "EclipseModule")
            {
                eclipseComponent = comp;
                eclipseRatioField = compType.GetField("eclipseRatio", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (eclipseRatioField != null)
                {
                    eclipseReady = true;
                    Debug.Log($"[YORU] ✓ Eclipse Module found in children! (FORCED to {FormatHourFloat(FORCED_ECLIPSE_HOUR)})");
                }
                return;
            }
        }
        
        Debug.LogWarning("[YORU] Eclipse Module not found");
    }
    
    void SetEclipse(float intensity)
    {
        if (!eclipseReady || eclipseComponent == null || eclipseRatioField == null)
            return;
        
        try
        {
            float currentValue = (float)eclipseRatioField.GetValue(eclipseComponent);
            eclipseRatioField.SetValue(eclipseComponent, intensity);
            
            if (logChanges && Mathf.Abs(currentValue - intensity) > 0.01f)
                Debug.Log($"[YORU] Eclipse: {currentValue:F2} → {intensity:F2}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[YORU] Eclipse error: {e.Message}");
        }
    }
    
    #endregion
    
    void LogStatus()
    {
        Debug.Log("[YORU] ===== STATUS =====");
        Debug.Log($"  Weather Module: {(weatherModule != null ? "✓" : "✗")}");
        Debug.Log($"  Eclipse Module: {(eclipseReady ? "✓" : "✗")} (FORCED to {FormatHourFloat(FORCED_ECLIPSE_HOUR)})");
        Debug.Log($"  WindZone: {(sceneWindZone != null ? "✓" : "○")}");
        Debug.Log($"  Terrain: {(sceneTerrain != null ? "✓" : "○")}");
        Debug.Log($"  Foliage Materials: {foliageMaterials.Count}");
        Debug.Log($"  Grass Materials: {grassMaterials.Count}");
        Debug.Log("========================");
    }
    
    void OnRingsChanged(int left, int right)
    {
        if (left == lastLeft && right == lastRight) return;
        lastLeft = left;
        lastRight = right;
        ApplyFullState(left, right);
    }
    
    void ApplyFullState(int left, int right)
    {
        bool isEclipse = (left == 5 && right == 5);
        
        // TIME - Eclipse at 3:30 PM (use forced constant to override any cached Inspector value)
        float hour = isEclipse ? FORCED_ECLIPSE_HOUR : (float)CalculateHour(left, right);
        SetTime(hour);
        
        // WEATHER
        WeatherProfile weather = SelectWeather(left, right);
        SetWeather(weather);
        
        // ECLIPSE - ONLY at 5L/5R
        SetEclipse(isEclipse ? 1.0f : 0f);
        
        // WIND
        float windIntensity = CalculateWindIntensity(left, right);
        ApplyWind(windIntensity);
        
        if (logChanges)
        {
            string eclipseStr = isEclipse ? " [ECLIPSE]" : "";
            Debug.Log($"[YORU] {left}L/{right}R → {FormatHourFloat(hour)}, {weather?.name}, wind:{windIntensity:F2}{eclipseStr}");
        }
    }
    
    #region TIME
    
    int CalculateHour(int left, int right)
    {
        int total = left + right;
        if (total == 0) return neutralHour;
        
        float darkHour, lightHour;
        
        if (left <= 5)
            darkHour = Mathf.Lerp(neutralHour, sunset5LeftHour, left / 5f);
        else
            darkHour = Mathf.Lerp(sunset5LeftHour, midnight10LeftHour < sunset5LeftHour ? 24f : midnight10LeftHour, (left - 5) / 5f);
        
        if (right <= 5)
            lightHour = Mathf.Lerp(neutralHour, morning5RightHour, right / 5f);
        else
            lightHour = Mathf.Lerp(morning5RightHour, lateMorning10RightHour, (right - 5) / 5f);
        
        float leftWeight = (float)left / total;
        float rightWeight = (float)right / total;
        int hour = Mathf.RoundToInt((darkHour * leftWeight) + (lightHour * rightWeight));
        
        while (hour >= 24) hour -= 24;
        while (hour < 0) hour += 24;
        
        return hour;
    }
    
    void SetTime(float hours)
    {
        if (cozy?.timeModule == null) return;
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.RoundToInt((hours - h) * 60f);
        cozy.timeModule.currentTime = new MeridiemTime(h, m);
    }
    
    #endregion
    
    #region WEATHER
    
    WeatherProfile SelectWeather(int left, int right)
    {
        if (left == 5 && right == 5) return clearWeather;
        if (left == 0 && right > 0)
            return right <= 3 ? clearWeather : (mostlyClearWeather ?? clearWeather);
        if (right == 0 && left > 0)
            return GetDarkPathWeather(left);
        if (left > right)
            return GetDarkPathWeather(left - right);
        return mostlyClearWeather ?? clearWeather;
    }
    
    WeatherProfile GetDarkPathWeather(int darkness)
    {
        if (darkness <= 2) return partlyCloudyWeather ?? clearWeather;
        if (darkness <= 5) return overcastWeather ?? partlyCloudyWeather ?? clearWeather;
        if (darkness <= 7) return lightRainWeather ?? overcastWeather ?? clearWeather;
        if (darkness <= 9) return heavyRainWeather ?? lightRainWeather ?? clearWeather;
        return thunderStormWeather ?? heavyRainWeather ?? clearWeather;
    }
    
    void SetWeather(WeatherProfile profile)
    {
        if (weatherModule?.ecosystem == null || profile == null) return;
        weatherModule.ecosystem.SetWeather(profile);
    }
    
    #endregion
    
    #region WIND
    
    float CalculateWindIntensity(int left, int right)
    {
        if (left == 5 && right == 5) return 0.1f;
        if (left == 0) return 0.2f;
        
        int effective = Mathf.Max(0, left - right);
        
        if (effective <= 5)
            return Mathf.Lerp(0.2f, 0.5f, effective / 5f);
        else if (effective <= 7)
            return Mathf.Lerp(0.5f, 0.8f, (effective - 5) / 2f);
        else
            return Mathf.Lerp(0.8f, 1.0f, (effective - 7) / 3f);
    }
    
    void ApplyWind(float intensity)
    {
        ApplyWindZone(intensity);
        ApplyMaterialWind(foliageMaterials, intensity);
        ApplyMaterialWind(grassMaterials, intensity);
        ApplyTerrainGrassWind(intensity);
    }
    
    void ApplyTerrainGrassWind(float intensity)
    {
        if (sceneTerrain == null) return;
        
        // Unity terrain built-in grass wind (for billboard/texture grass)
        float speed = Mathf.Lerp(0.5f, 3f, intensity);
        float strength = Mathf.Lerp(0.3f, 1.5f, intensity);
        float amount = Mathf.Lerp(0.3f, 1f, intensity);
        
        sceneTerrain.terrainData.wavingGrassSpeed = speed;
        sceneTerrain.terrainData.wavingGrassStrength = strength;
        sceneTerrain.terrainData.wavingGrassAmount = amount;
    }
    
    void ApplyWindZone(float intensity)
    {
        if (sceneWindZone == null) return;
        
        float speed = Mathf.Lerp(calmWindSpeed, maxStormWindSpeed, intensity);
        sceneWindZone.windMain = speed;
        sceneWindZone.windTurbulence = Mathf.Lerp(0.1f, 0.8f, intensity);
        sceneWindZone.windPulseMagnitude = Mathf.Lerp(0.1f, 0.5f, intensity);
        sceneWindZone.windPulseFrequency = Mathf.Lerp(0.1f, 0.3f, intensity);
    }
    
    void ApplyMaterialWind(List<Material> materials, float intensity)
    {
        float speed = Mathf.Lerp(calmWindSpeed, stormWindSpeed, intensity);
        float stormMultiplier = intensity > 0.7f ? Mathf.Lerp(1f, 2f, (intensity - 0.7f) / 0.3f) : 1f;
        
        foreach (var mat in materials)
        {
            if (mat == null) continue;
            
            // Polyart Dreamscape Foliage shader properties
            if (mat.HasProperty("_Sway_Wind_Intensity"))
                mat.SetFloat("_Sway_Wind_Intensity", intensity * stormMultiplier);
            if (mat.HasProperty("_Sway_Wind_Speed"))
                mat.SetFloat("_Sway_Wind_Speed", speed);
            if (mat.HasProperty("_Wiggle_Wind_Intensity"))
                mat.SetFloat("_Wiggle_Wind_Intensity", intensity * 0.5f * stormMultiplier);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Small"))
                mat.SetFloat("_Wiggle_Wind_Speed_Small", speed * 0.5f);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Large"))
                mat.SetFloat("_Wiggle_Wind_Speed_Large", speed * 0.8f);
            
            // Trunk shader
            if (mat.HasProperty("_Wind_Intensity"))
                mat.SetFloat("_Wind_Intensity", intensity * stormMultiplier);
            if (mat.HasProperty("_Wind_Speed"))
                mat.SetFloat("_Wind_Speed", speed);
            
            // Plants/Foliage shader
            if (mat.HasProperty("_WindSpeed"))
                mat.SetFloat("_WindSpeed", speed);
            if (mat.HasProperty("_WindIntensity"))
                mat.SetFloat("_WindIntensity", intensity * stormMultiplier);
            if (mat.HasProperty("_WindScale"))
                mat.SetFloat("_WindScale", Mathf.Lerp(0.5f, 2f, intensity));
            
            // Large/Small intensity
            if (mat.HasProperty("_Wind_Large_Intensity"))
                mat.SetFloat("_Wind_Large_Intensity", intensity * stormMultiplier);
            if (mat.HasProperty("_Wind_Small_Intensity"))
                mat.SetFloat("_Wind_Small_Intensity", intensity * 0.5f * stormMultiplier);
            
            // Builtin/Grass shader properties (Polyart Dreamscape)
            if (mat.HasProperty("_WaveSpeed"))
                mat.SetFloat("_WaveSpeed", speed);
            if (mat.HasProperty("_WaveStrength"))
                mat.SetFloat("_WaveStrength", intensity * stormMultiplier);
            if (mat.HasProperty("_SwaySpeed"))
                mat.SetFloat("_SwaySpeed", speed);
            if (mat.HasProperty("_SwayAmount"))
                mat.SetFloat("_SwayAmount", intensity * stormMultiplier);
            if (mat.HasProperty("_Sway"))
                mat.SetFloat("_Sway", intensity * stormMultiplier);
            if (mat.HasProperty("_WindStrength"))
                mat.SetFloat("_WindStrength", intensity * stormMultiplier);
            
            // Additional common grass wind properties
            if (mat.HasProperty("_Frequency"))
                mat.SetFloat("_Frequency", Mathf.Lerp(1f, 3f, intensity));
            if (mat.HasProperty("_Amplitude"))
                mat.SetFloat("_Amplitude", intensity * stormMultiplier);
        }
    }
    
    #endregion
    
    #region UTILITY
    
    string FormatHour(int hour)
    {
        string ampm = hour >= 12 ? "PM" : "AM";
        int display = hour > 12 ? hour - 12 : (hour == 0 ? 12 : hour);
        return $"{display}:00 {ampm}";
    }
    
    string FormatHourFloat(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.RoundToInt((hour - h) * 60f);
        string ampm = h >= 12 ? "PM" : "AM";
        int display = h > 12 ? h - 12 : (h == 0 ? 12 : h);
        return $"{display}:{m:D2} {ampm}";
    }
    
    #endregion
    
    #region CONTEXT MENU
    
    [ContextMenu("Test: Force Eclipse NOW")]
    public void ForceEclipseNow()
    {
        if (!eclipseReady)
        {
            Debug.LogError("[TEST] Eclipse not ready");
            return;
        }
        
        SetTime(FORCED_ECLIPSE_HOUR);
        
        if (clearWeather != null && weatherModule?.ecosystem != null)
            weatherModule.ecosystem.SetWeather(clearWeather);
        
        eclipseRatioField.SetValue(eclipseComponent, 1.0f);
        
        Debug.Log($"[TEST] Eclipse at {FormatHourFloat(FORCED_ECLIPSE_HOUR)} - LOOK AT THE SUN!");
    }
    
    [ContextMenu("Test: Severe Storm Wind")]
    public void TestStormWind()
    {
        ApplyWind(1.0f);
        Debug.Log("[TEST] SEVERE STORM wind applied!");
    }
    
    [ContextMenu("Refresh All Materials")]
    public void RefreshMaterials()
    {
        foliageMaterials.Clear();
        grassMaterials.Clear();
        FindFoliageMaterials();
        FindGrassMaterialsFromTerrain();
    }
    
    [ContextMenu("Print Status")]
    public void PrintStatus()
    {
        LogStatus();
        
        Debug.Log("=== GRASS MATERIALS ===");
        foreach (var mat in grassMaterials)
        {
            if (mat != null)
                Debug.Log($"  {mat.name} ({mat.shader.name})");
        }
    }
    
    [ContextMenu("Debug: Dump Grass Shader Properties")]
    public void DumpGrassShaderProperties()
    {
        Debug.Log("=== GRASS SHADER PROPERTY DUMP ===");
        
        // Find all grass materials in scene
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<Material> checkedMats = new HashSet<Material>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || checkedMats.Contains(mat)) continue;
                checkedMats.Add(mat);
                
                if (!IsGrassMaterial(mat)) continue;
                
                Debug.Log($"\n--- {mat.name} ({mat.shader.name}) ---");
                
                // List all float properties we check for
                string[] propNames = new string[] {
                    "_Wind_Intensity", "_Sway_Wind_Intensity", "_WindIntensity",
                    "_Wind_Large_Intensity", "_WindSpeed", "_Wind_Speed",
                    "_WaveSpeed", "_WaveStrength", "_SwaySpeed", "_SwayAmount",
                    "_Sway", "_WindDirection", "_WindStrength", "_Frequency",
                    "_Amplitude", "_Wiggle_Wind_Intensity", "_Wiggle_Wind_Speed_Small",
                    "_Wiggle_Wind_Speed_Large", "_WindScale", "_Wind_Small_Intensity",
                    "_Sway_Wind_Speed"
                };
                
                foreach (var prop in propNames)
                {
                    if (mat.HasProperty(prop))
                    {
                        float val = mat.GetFloat(prop);
                        Debug.Log($"  ✓ {prop} = {val}");
                    }
                }
            }
        }
        
        Debug.Log("=== END DUMP ===");
    }
    
    #endregion
}