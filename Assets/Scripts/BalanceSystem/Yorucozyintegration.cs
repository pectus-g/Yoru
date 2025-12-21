using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System.Collections.Generic;

/// <summary>
/// YORU: Complete COZY 3 Integration
/// 
/// ECLIPSE:
/// - ONLY triggers at exactly 5L/5R (balance)
/// - eclipseRatio = 1.0 (full eclipse)
/// - Time = 5:00 PM (17:00) so sun is visible lower in sky
/// 
/// WIND:
/// - Uses Polyart WindSway shader properties
/// - Also updates COZY's CZY_ global properties
/// - Also updates terrain grass wind
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
    [Tooltip("Time for 0 rings (neutral noon)")]
    [SerializeField] private int neutralHour = 12;
    [Tooltip("Time for 5 left rings (sunset)")]
    [SerializeField] private int sunset5LeftHour = 18;
    [Tooltip("Time for 10 left rings (midnight)")]
    [SerializeField] private int midnight10LeftHour = 0;
    [Tooltip("Time for 5 right rings (morning)")]
    [SerializeField] private int morning5RightHour = 9;
    [Tooltip("Time for 10 right rings (late morning)")]
    [SerializeField] private int lateMorning10RightHour = 11;
    
    [Header("=== ECLIPSE SETTINGS ===")]
    [Tooltip("Hour when eclipse appears (5L/5R only)")]
    [SerializeField] private int eclipseHour = 17; // 5 PM - lower in sky
    
    [Header("=== WIND SETTINGS ===")]
    [Tooltip("Your scene WindZone (for trees)")]
    public WindZone sceneWindZone;
    [Tooltip("Your scene Terrain (for grass)")]
    public Terrain sceneTerrain;
    [SerializeField] private float calmWindSpeed = 0.3f;
    [SerializeField] private float maxWindSpeed = 4f;
    
    [Header("=== POLYART SHADER WIND ===")]
    [Tooltip("Enable Polyart shader wind control")]
    public bool usePolyartWind = true;
    [Tooltip("List of materials using Polyart foliage shaders")]
    public List<Material> polyartFoliageMaterials = new List<Material>();
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    // COZY References
    private CozyWeather cozy;
    private CozyWeatherModule weatherModule;
    
    // Eclipse - via reflection
    private object eclipseModule;
    private System.Reflection.FieldInfo eclipseRatioField;
    private bool eclipseReady = false;
    
    // State tracking
    private int lastLeft = -1;
    private int lastRight = -1;
    
    void Start()
    {
        InitializeCozy();
        AutoFindComponents();
        
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
        if (cozy == null)
        {
            Debug.LogError("[YORU] CozyWeather not found!");
            enabled = false;
            return;
        }
        
        weatherModule = cozy.weatherModule;
        FindEclipseModule();
    }
    
    void AutoFindComponents()
    {
        if (sceneTerrain == null)
            sceneTerrain = Terrain.activeTerrain;
        
        if (sceneWindZone == null)
            sceneWindZone = FindObjectOfType<WindZone>();
        
        // Auto-find Polyart materials if list is empty
        if (polyartFoliageMaterials.Count == 0)
        {
            FindPolyartMaterials();
        }
    }
    
    void FindPolyartMaterials()
    {
        // Find all renderers in scene with Polyart shaders
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<Material> found = new HashSet<Material>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name.ToLower();
                    if (shaderName.Contains("polyart") || 
                        shaderName.Contains("dreamscape") ||
                        shaderName.Contains("pa_") ||
                        shaderName.Contains("foliage") ||
                        shaderName.Contains("tree"))
                    {
                        found.Add(mat);
                    }
                }
            }
        }
        
        polyartFoliageMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Auto-found {found.Count} Polyart/foliage materials");
    }
    
    void FindEclipseModule()
    {
        try
        {
            System.Type eclipseType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                eclipseType = assembly.GetType("DistantLands.Cozy.EclipseModule");
                if (eclipseType != null) break;
            }
            
            if (eclipseType == null)
            {
                Debug.LogWarning("[YORU] Eclipse module type not found");
                return;
            }
            
            var getModuleMethod = typeof(CozyWeather).GetMethod("GetModule");
            var genericMethod = getModuleMethod.MakeGenericMethod(eclipseType);
            eclipseModule = genericMethod.Invoke(cozy, null);
            
            if (eclipseModule == null)
            {
                Debug.LogWarning("[YORU] Eclipse module not active in COZY");
                return;
            }
            
            eclipseRatioField = eclipseType.GetField("eclipseRatio", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (eclipseRatioField != null)
            {
                eclipseReady = true;
                Debug.Log("[YORU] Eclipse module ready!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[YORU] Eclipse setup error: {e.Message}");
        }
    }
    
    void LogStatus()
    {
        Debug.Log("[YORU] ===== STATUS =====");
        Debug.Log($"  Weather Module: {(weatherModule != null ? "✓" : "✗")}");
        Debug.Log($"  Eclipse Module: {(eclipseReady ? "✓" : "✗")}");
        Debug.Log($"  WindZone: {(sceneWindZone != null ? "✓" : "○")}");
        Debug.Log($"  Terrain: {(sceneTerrain != null ? "✓" : "○")}");
        Debug.Log($"  Polyart Materials: {polyartFoliageMaterials.Count}");
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
        // Check for eclipse condition FIRST
        bool isEclipse = (left == 5 && right == 5);
        
        // Set time
        int hour;
        if (isEclipse)
        {
            hour = eclipseHour; // 5 PM for eclipse visibility
        }
        else
        {
            hour = CalculateHour(left, right);
        }
        SetTime(hour);
        
        // Set weather
        WeatherProfile weather = SelectWeather(left, right);
        SetWeather(weather);
        
        // Set eclipse - ONLY at 5L/5R with full intensity
        if (isEclipse)
        {
            SetEclipse(1.0f); // Full eclipse
        }
        else
        {
            SetEclipse(0f); // No eclipse
        }
        
        // Set wind
        float wind = CalculateWind(left, right);
        SetAllWind(wind);
        
        if (logChanges)
        {
            string eclipseStr = isEclipse ? " [ECLIPSE ACTIVE]" : "";
            Debug.Log($"[YORU] {left}L/{right}R → {FormatHour(hour)}, {weather?.name}, wind:{wind:F2}{eclipseStr}");
        }
    }
    
    #region TIME
    int CalculateHour(int left, int right)
    {
        int total = left + right;
        if (total == 0) return neutralHour;
        
        // Calculate based on dominant side
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
    
    void SetTime(int hours)
    {
        if (cozy?.timeModule == null) return;
        cozy.timeModule.currentTime = new MeridiemTime(hours, 0);
    }
    #endregion
    
    #region WEATHER
    WeatherProfile SelectWeather(int left, int right)
    {
        // Eclipse = clear sky so you can see it
        if (left == 5 && right == 5) return clearWeather;
        
        // Light path = clear/mostly clear
        if (left == 0 && right > 0)
            return right <= 3 ? clearWeather : (mostlyClearWeather ?? clearWeather);
        
        // Dark path = increasingly stormy
        if (right == 0 && left > 0)
            return GetDarkPathWeather(left);
        
        // Mixed = based on net darkness
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
    
    #region ECLIPSE
    void SetEclipse(float intensity)
    {
        if (!eclipseReady || eclipseModule == null || eclipseRatioField == null)
        {
            if (intensity > 0 && logChanges)
                Debug.LogWarning("[YORU] Eclipse module not ready - assign Eclipse Profile in COZY!");
            return;
        }
        
        try
        {
            eclipseRatioField.SetValue(eclipseModule, intensity);
            if (intensity > 0 && logChanges)
                Debug.Log($"[YORU] Eclipse ratio set to {intensity:F2}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[YORU] Eclipse error: {e.Message}");
        }
    }
    #endregion
    
    #region WIND
    float CalculateWind(int left, int right)
    {
        // No wind at neutral or eclipse
        if (left == 0 || (left == 5 && right == 5)) return 0f;
        
        // Wind increases with darkness
        int effective = Mathf.Max(0, left - right);
        if (effective <= 5)
            return Mathf.Lerp(0f, 0.3f, effective / 5f);
        return Mathf.Lerp(0.3f, 1f, (effective - 5) / 5f);
    }
    
    void SetAllWind(float normalized)
    {
        // 1. Unity WindZone (affects SpeedTree and some tree shaders)
        SetWindZone(normalized);
        
        // 2. Terrain grass wind
        SetTerrainGrassWind(normalized);
        
        // 3. Polyart shader wind
        SetPolyartWind(normalized);
        
        // 4. COZY global shader properties
        SetCozyWindGlobals(normalized);
    }
    
    void SetWindZone(float normalized)
    {
        if (sceneWindZone == null) return;
        
        float speed = Mathf.Lerp(calmWindSpeed, maxWindSpeed, normalized);
        sceneWindZone.windMain = speed;
        sceneWindZone.windTurbulence = speed * 0.3f;
        sceneWindZone.windPulseMagnitude = speed * 0.2f;
    }
    
    void SetTerrainGrassWind(float normalized)
    {
        if (sceneTerrain == null) return;
        
        TerrainData td = sceneTerrain.terrainData;
        if (td == null) return;
        
        td.wavingGrassSpeed = Mathf.Lerp(0.3f, 1.5f, normalized);
        td.wavingGrassStrength = Mathf.Lerp(0.3f, 1f, normalized);
        td.wavingGrassAmount = Mathf.Lerp(0.3f, 1f, normalized);
    }
    
    void SetPolyartWind(float normalized)
    {
        if (!usePolyartWind) return;
        
        float speed = Mathf.Lerp(calmWindSpeed, maxWindSpeed, normalized);
        
        // Polyart WindSway shader properties (common names)
        // These are SET GLOBALLY so they affect ALL materials using these properties
        Shader.SetGlobalFloat("_WindSpeed", speed);
        Shader.SetGlobalFloat("_WindStrength", normalized);
        Shader.SetGlobalFloat("_SwayAmount", normalized);
        Shader.SetGlobalFloat("_SwaySpeed", speed);
        Shader.SetGlobalFloat("_Sway", normalized);
        
        // Also try per-material if we have specific materials
        foreach (var mat in polyartFoliageMaterials)
        {
            if (mat == null) continue;
            
            if (mat.HasProperty("_WindSpeed"))
                mat.SetFloat("_WindSpeed", speed);
            if (mat.HasProperty("_WindStrength"))
                mat.SetFloat("_WindStrength", normalized);
            if (mat.HasProperty("_SwayAmount"))
                mat.SetFloat("_SwayAmount", normalized);
            if (mat.HasProperty("_SwaySpeed"))
                mat.SetFloat("_SwaySpeed", speed);
            if (mat.HasProperty("_Sway"))
                mat.SetFloat("_Sway", normalized);
        }
    }
    
    void SetCozyWindGlobals(float normalized)
    {
        float speed = Mathf.Lerp(calmWindSpeed, maxWindSpeed, normalized);
        
        // COZY's standard global shader properties
        Shader.SetGlobalFloat("CZY_WindSpeed", speed);
        Shader.SetGlobalFloat("CZY_WindMultiplier", normalized);
        Shader.SetGlobalFloat("CZY_WindDirection", 0f); // You can make this dynamic if needed
    }
    #endregion
    
    #region UTILITY
    string FormatHour(int hour)
    {
        string ampm = hour >= 12 ? "PM" : "AM";
        int display = hour > 12 ? hour - 12 : (hour == 0 ? 12 : hour);
        return $"{display}:00 {ampm}";
    }
    #endregion
    
    #region CONTEXT MENU
    
    [ContextMenu("Test: Eclipse (5L/5R at 5PM)")]
    public void TestEclipse()
    {
        SetTime(eclipseHour);
        SetWeather(clearWeather);
        SetEclipse(1.0f);
        SetAllWind(0f);
        Debug.Log($"[TEST] Eclipse at {eclipseHour}:00 - LOOK AT THE SUN!");
    }
    
    [ContextMenu("Test: No Eclipse (noon)")]
    public void TestNoEclipse()
    {
        SetTime(12);
        SetWeather(clearWeather);
        SetEclipse(0f);
    }
    
    [ContextMenu("Test: Max Wind")]
    public void TestMaxWind()
    {
        SetAllWind(1f);
        Debug.Log("[TEST] Max wind applied to all systems");
    }
    
    [ContextMenu("Test: No Wind")]
    public void TestNoWind()
    {
        SetAllWind(0f);
        Debug.Log("[TEST] Wind disabled");
    }
    
    [ContextMenu("Find Polyart Materials")]
    public void ContextFindMaterials()
    {
        polyartFoliageMaterials.Clear();
        FindPolyartMaterials();
    }
    
    [ContextMenu("Print Shader Properties (First Material)")]
    public void PrintShaderProperties()
    {
        if (polyartFoliageMaterials.Count == 0)
        {
            Debug.LogWarning("No Polyart materials found");
            return;
        }
        
        var mat = polyartFoliageMaterials[0];
        if (mat == null || mat.shader == null)
        {
            Debug.LogWarning("Material or shader is null");
            return;
        }
        
        Debug.Log($"=== SHADER: {mat.shader.name} ===");
        Debug.Log($"Material: {mat.name}");
        
        // List all properties
        int count = mat.shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            string propName = mat.shader.GetPropertyName(i);
            var propType = mat.shader.GetPropertyType(i);
            Debug.Log($"  [{i}] {propName} ({propType})");
        }
    }
    
    [ContextMenu("Print All Foliage Shader Names")]
    public void PrintAllShaderNames()
    {
        var renderers = FindObjectsOfType<Renderer>();
        HashSet<string> shaderNames = new HashSet<string>();
        
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name.ToLower();
                    if (shaderName.Contains("foliage") || 
                        shaderName.Contains("tree") || 
                        shaderName.Contains("grass") ||
                        shaderName.Contains("leaf") ||
                        shaderName.Contains("polyart") ||
                        shaderName.Contains("dreamscape") ||
                        shaderName.Contains("pa_"))
                    {
                        shaderNames.Add($"{mat.shader.name} (Material: {mat.name})");
                    }
                }
            }
        }
        
        Debug.Log($"=== FOLIAGE SHADERS IN SCENE ({shaderNames.Count}) ===");
        foreach (var name in shaderNames)
        {
            Debug.Log($"  {name}");
        }
    }
    
    [ContextMenu("Print Status")]
    public void PrintStatus()
    {
        LogStatus();
        
        if (eclipseReady && eclipseRatioField != null && eclipseModule != null)
        {
            try
            {
                var val = eclipseRatioField.GetValue(eclipseModule);
                Debug.Log($"Current Eclipse Ratio: {val}");
            }
            catch { }
        }
        
        if (sceneTerrain != null)
        {
            var td = sceneTerrain.terrainData;
            Debug.Log($"Terrain Grass Wind: speed={td.wavingGrassSpeed:F2}, strength={td.wavingGrassStrength:F2}");
        }
    }
    
    #endregion
}