using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// YORU: Complete COZY 3 Integration - V8 (FIXED LIGHT PATH)
/// 
/// KEY INSIGHT:
/// - COZY time controls the SKY appearance
/// - Dark path goes toward NIGHT (see stars/galaxy)
/// - Light path goes from GOLDEN HOUR to BRIGHTEST (noon)
/// 
/// DARK PATH: Time progresses toward night (see stars/galaxy)
///   Balance -1: 4 PM → -2: 6 PM → -3: 8 PM → -4: 10 PM → -5: Midnight
///   
/// LIGHT PATH: Golden hour → Peak brightness (heavenly!)
///   Balance +1: 2:30 PM (golden hour, warm)
///   Balance +2: 2:00 PM (warm afternoon)
///   Balance +3: 1:30 PM (getting brighter)
///   Balance +4: 1:00 PM (bright)
///   Balance +5: 12:15 PM (maximum brightness, divine!)
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
    
    [Header("=== TIME SETTINGS (Dark Path) ===")]
    [Tooltip("Balance 0 = Noon")]
    [SerializeField] private float neutralHour = 12f;
    [Tooltip("Balance -1")]
    [SerializeField] private float dark1Hour = 16f;   // 4 PM - late afternoon
    [Tooltip("Balance -2")]
    [SerializeField] private float dark2Hour = 18f;   // 6 PM - sunset
    [Tooltip("Balance -3")]
    [SerializeField] private float dark3Hour = 20f;   // 8 PM - dusk, stars emerging
    [Tooltip("Balance -4")]
    [SerializeField] private float dark4Hour = 22f;   // 10 PM - night, galaxy visible
    [Tooltip("Balance -5 or darker")]
    [SerializeField] private float dark5Hour = 0f;    // Midnight - full stars!
    
    [Header("=== TIME SETTINGS (Light Path) - GOLDEN TO BRIGHTEST ===")]
    [Tooltip("Balance +1 - Golden hour (warm, beautiful)")]
    [SerializeField] private float light1Hour = 14.5f;  // 2:30 PM - golden hour, warm light
    [Tooltip("Balance +2 - Warm afternoon")]
    [SerializeField] private float light2Hour = 14f;    // 2:00 PM - warm afternoon
    [Tooltip("Balance +3 - Getting brighter")]
    [SerializeField] private float light3Hour = 13.5f;  // 1:30 PM - transitioning brighter
    [Tooltip("Balance +4 - Bright")]
    [SerializeField] private float light4Hour = 13f;    // 1:00 PM - bright
    [Tooltip("Balance +5 - HEAVENLY (maximum brightness, sun overhead!)")]
    [SerializeField] private float light5Hour = 12.25f; // 12:15 PM - peak brightness, divine!
    
    [Header("=== ECLIPSE SETTINGS ===")]
    [SerializeField] private float eclipseHour = 15.5f;
    private const float FORCED_ECLIPSE_HOUR = 15.5f;
    
    [Header("=== WIND SETTINGS ===")]
    public WindZone sceneWindZone;
    public Terrain sceneTerrain;
    [SerializeField] private float calmWindSpeed = 0.5f;
    [SerializeField] private float stormWindSpeed = 3f;
    [SerializeField] private float maxStormWindSpeed = 6f;
    
    [Header("=== MATERIALS (Auto-Found) ===")]
    public List<Material> foliageMaterials = new List<Material>();
    public List<Material> grassMaterials = new List<Material>();
    
    [Header("=== MANUAL GRASS MATERIALS ===")]
    public List<Material> manualGrassMaterials = new List<Material>();
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    // COZY References
    private CozyWeather cozy;
    private CozyWeatherModule weatherModule;
    
    // Eclipse
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
            weatherModule = cozy.weatherModule;
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
                
                if ((shaderName.Contains("foliage") || 
                     shaderName.Contains("trunk") ||
                     shaderName.Contains("leaves")) &&
                    !shaderName.Contains("rock"))
                {
                    if (HasWindProperty(mat))
                        found.Add(mat);
                }
            }
        }
        
        foliageMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Found {found.Count} foliage materials");
    }
    
    void FindGrassMaterialsFromTerrain()
    {
        HashSet<Material> found = new HashSet<Material>();
        
        if (sceneTerrain != null && sceneTerrain.terrainData != null)
        {
            var td = sceneTerrain.terrainData;
            
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
                        
                        bool isGrassShader = shaderName.Contains("foliage") || 
                                            shaderName.Contains("grass") ||
                                            shaderName.Contains("dreamscape");
                        bool isGrassMat = matName.Contains("grass");
                        bool isRock = shaderName.Contains("rock");
                        
                        if ((isGrassShader || isGrassMat) && !isRock)
                            found.Add(mat);
                    }
                }
            }
        }
        
        var sceneRenderers = FindObjectsOfType<Renderer>();
        foreach (var renderer in sceneRenderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                if (IsGrassMaterial(mat) && !found.Contains(mat))
                    found.Add(mat);
            }
        }
        
        foreach (var mat in manualGrassMaterials)
        {
            if (mat != null && !found.Contains(mat))
                found.Add(mat);
        }
        
        grassMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Found {found.Count} total grass materials");
    }
    
    bool HasWindProperty(Material mat)
    {
        return mat.HasProperty("_Wind_Intensity") ||
               mat.HasProperty("_Sway_Wind_Intensity") ||
               mat.HasProperty("_WindIntensity") ||
               mat.HasProperty("_Wind_Large_Intensity") ||
               mat.HasProperty("_WindSpeed") ||
               mat.HasProperty("_Wind_Speed") ||
               mat.HasProperty("_WaveSpeed") ||
               mat.HasProperty("_WaveStrength") ||
               mat.HasProperty("_SwaySpeed") ||
               mat.HasProperty("_SwayAmount") ||
               mat.HasProperty("_Sway") ||
               mat.HasProperty("_WindDirection") ||
               mat.HasProperty("_WindStrength");
    }
    
    bool IsGrassMaterial(Material mat)
    {
        if (mat == null || mat.shader == null) return false;
        
        string shaderName = mat.shader.name.ToLower();
        string matName = mat.name.ToLower();
        
        if (shaderName.Contains("rock") || shaderName.Contains("m_rocks"))
            return false;
        
        if (shaderName.Contains("grass") || shaderName.Contains("builtin/grass"))
            return true;
        
        if (shaderName.Contains("foliage") && matName.Contains("grass"))
            return true;
        
        return false;
    }
    
    #region ECLIPSE
    
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
                    Debug.Log($"[YORU] ✓ Eclipse Module found!");
                }
                return;
            }
        }
        
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
                    Debug.Log($"[YORU] ✓ Eclipse Module found in children!");
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
        Debug.Log("[YORU] ===== STATUS (V8) =====");
        Debug.Log($"  Weather Module: {(weatherModule != null ? "✓" : "✗")}");
        Debug.Log($"  Eclipse Module: {(eclipseReady ? "✓" : "✗")}");
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
        
        // Get weather stage from WorldStateManager
        int weatherStage = 0;
        int balance = right - left;
        
        if (WorldStateManager.Instance != null)
            weatherStage = WorldStateManager.Instance.WeatherStage;
        else if (Mathf.Abs(balance) >= 5 && (left + right) > 5)
            weatherStage = Mathf.Min((left + right) - 5, 5);
        
        // TIME - Based on balance
        float hour = isEclipse ? FORCED_ECLIPSE_HOUR : GetHourForBalance(balance);
        SetTime(hour);
        
        // WEATHER - Two-layer system
        WeatherProfile weather = SelectWeatherTwoLayer(balance, weatherStage, isEclipse);
        SetWeather(weather);
        
        // ECLIPSE
        SetEclipse(isEclipse ? 1.0f : 0f);
        
        // WIND
        float windIntensity = CalculateWindIntensity(balance, weatherStage);
        ApplyWind(windIntensity);
        
        if (logChanges)
        {
            string stageStr = weatherStage > 0 ? $" [Stage {weatherStage}]" : "";
            string eclipseStr = isEclipse ? " [ECLIPSE]" : "";
            Debug.Log($"[YORU] {left}L/{right}R → {FormatHour(hour)}, {weather?.name}, wind:{windIntensity:F2}{stageStr}{eclipseStr}");
        }
    }
    
    #region TIME
    
    float GetHourForBalance(int balance)
    {
        int clamped = Mathf.Clamp(balance, -5, 5);
        
        switch (clamped)
        {
            // DARK PATH - progresses toward night
            case -5: return dark5Hour;   // Midnight
            case -4: return dark4Hour;   // 10 PM
            case -3: return dark3Hour;   // 8 PM
            case -2: return dark2Hour;   // 6 PM
            case -1: return dark1Hour;   // 4 PM
            
            // NEUTRAL
            case 0:  return neutralHour; // Noon
            
            // LIGHT PATH - stays bright!
            case 1:  return light1Hour;  // 11:30 AM
            case 2:  return light2Hour;  // 11 AM
            case 3:  return light3Hour;  // 10:30 AM
            case 4:  return light4Hour;  // 10 AM
            case 5:  return light5Hour;  // 10 AM (stays bright!)
            
            default: return neutralHour;
        }
    }
    
    void SetTime(float hours)
    {
        if (cozy?.timeModule == null) return;
        
        while (hours >= 24) hours -= 24;
        while (hours < 0) hours += 24;
        
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.RoundToInt((hours - h) * 60f);
        cozy.timeModule.currentTime = new MeridiemTime(h, m);
    }
    
    #endregion
    
    #region WEATHER
    
    WeatherProfile SelectWeatherTwoLayer(int balance, int weatherStage, bool isEclipse)
    {
        if (isEclipse)
            return clearWeather;
        
        // Layer 1: No weather escalation - CLEAR sky
        if (weatherStage == 0)
            return clearWeather;
        
        // Layer 2: Weather escalation
        if (balance < 0)
            return GetDarkWeatherForStage(weatherStage);
        else
            return GetLightWeatherForStage(weatherStage);
    }
    
    WeatherProfile GetDarkWeatherForStage(int stage)
    {
        switch (stage)
        {
            case 1: return partlyCloudyWeather ?? clearWeather;
            case 2: return overcastWeather ?? partlyCloudyWeather ?? clearWeather;
            case 3: return lightRainWeather ?? overcastWeather ?? clearWeather;
            case 4: return heavyRainWeather ?? lightRainWeather ?? clearWeather;
            case 5: return thunderStormWeather ?? heavyRainWeather ?? clearWeather;
            default: return clearWeather;
        }
    }
    
    WeatherProfile GetLightWeatherForStage(int stage)
    {
        // Light path stays clear
        return clearWeather ?? mostlyClearWeather;
    }
    
    void SetWeather(WeatherProfile profile)
    {
        if (weatherModule?.ecosystem == null || profile == null) return;
        weatherModule.ecosystem.SetWeather(profile);
    }
    
    #endregion
    
    #region WIND
    
    float CalculateWindIntensity(int balance, int weatherStage)
    {
        // Eclipse - calm
        if (WorldStateManager.Instance?.IsEclipse == true)
            return 0.1f;
        
        float baseWind = 0.2f;
        
        // Light path - gentle
        if (balance >= 0)
            return baseWind;
        
        // Dark path without weather - gentle breeze
        if (weatherStage == 0)
            return Mathf.Lerp(baseWind, 0.35f, Mathf.Abs(balance) / 5f);
        
        // Dark path with weather - escalates
        switch (weatherStage)
        {
            case 1: return 0.4f;
            case 2: return 0.55f;
            case 3: return 0.7f;
            case 4: return 0.85f;
            case 5: return 1.0f;
            default: return baseWind;
        }
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
        
        sceneTerrain.terrainData.wavingGrassSpeed = Mathf.Lerp(0.5f, 3f, intensity);
        sceneTerrain.terrainData.wavingGrassStrength = Mathf.Lerp(0.3f, 1.5f, intensity);
        sceneTerrain.terrainData.wavingGrassAmount = Mathf.Lerp(0.3f, 1f, intensity);
    }
    
    void ApplyWindZone(float intensity)
    {
        if (sceneWindZone == null) return;
        
        sceneWindZone.windMain = Mathf.Lerp(calmWindSpeed, maxStormWindSpeed, intensity);
        sceneWindZone.windTurbulence = Mathf.Lerp(0.1f, 0.8f, intensity);
        sceneWindZone.windPulseMagnitude = Mathf.Lerp(0.1f, 0.5f, intensity);
        sceneWindZone.windPulseFrequency = Mathf.Lerp(0.1f, 0.3f, intensity);
    }
    
    void ApplyMaterialWind(List<Material> materials, float intensity)
    {
        float speed = Mathf.Lerp(calmWindSpeed, stormWindSpeed, intensity);
        float mult = intensity > 0.7f ? Mathf.Lerp(1f, 2f, (intensity - 0.7f) / 0.3f) : 1f;
        
        foreach (var mat in materials)
        {
            if (mat == null) continue;
            
            // Polyart Dreamscape
            if (mat.HasProperty("_Sway_Wind_Intensity")) mat.SetFloat("_Sway_Wind_Intensity", intensity * mult);
            if (mat.HasProperty("_Sway_Wind_Speed")) mat.SetFloat("_Sway_Wind_Speed", speed);
            if (mat.HasProperty("_Wiggle_Wind_Intensity")) mat.SetFloat("_Wiggle_Wind_Intensity", intensity * 0.5f * mult);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Small")) mat.SetFloat("_Wiggle_Wind_Speed_Small", speed * 0.5f);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Large")) mat.SetFloat("_Wiggle_Wind_Speed_Large", speed * 0.8f);
            
            // Trunk
            if (mat.HasProperty("_Wind_Intensity")) mat.SetFloat("_Wind_Intensity", intensity * mult);
            if (mat.HasProperty("_Wind_Speed")) mat.SetFloat("_Wind_Speed", speed);
            
            // Generic
            if (mat.HasProperty("_WindSpeed")) mat.SetFloat("_WindSpeed", speed);
            if (mat.HasProperty("_WindIntensity")) mat.SetFloat("_WindIntensity", intensity * mult);
            if (mat.HasProperty("_WindScale")) mat.SetFloat("_WindScale", Mathf.Lerp(0.5f, 2f, intensity));
            if (mat.HasProperty("_Wind_Large_Intensity")) mat.SetFloat("_Wind_Large_Intensity", intensity * mult);
            if (mat.HasProperty("_Wind_Small_Intensity")) mat.SetFloat("_Wind_Small_Intensity", intensity * 0.5f * mult);
            
            // Grass
            if (mat.HasProperty("_WaveSpeed")) mat.SetFloat("_WaveSpeed", speed);
            if (mat.HasProperty("_WaveStrength")) mat.SetFloat("_WaveStrength", intensity * mult);
            if (mat.HasProperty("_SwaySpeed")) mat.SetFloat("_SwaySpeed", speed);
            if (mat.HasProperty("_SwayAmount")) mat.SetFloat("_SwayAmount", intensity * mult);
            if (mat.HasProperty("_Sway")) mat.SetFloat("_Sway", intensity * mult);
            if (mat.HasProperty("_WindStrength")) mat.SetFloat("_WindStrength", intensity * mult);
            if (mat.HasProperty("_Frequency")) mat.SetFloat("_Frequency", Mathf.Lerp(1f, 3f, intensity));
            if (mat.HasProperty("_Amplitude")) mat.SetFloat("_Amplitude", intensity * mult);
        }
    }
    
    #endregion
    
    #region UTILITY
    
    string FormatHour(float hour)
    {
        while (hour >= 24) hour -= 24;
        while (hour < 0) hour += 24;
        
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.RoundToInt((hour - h) * 60f);
        string ampm = h >= 12 ? "PM" : "AM";
        int display = h > 12 ? h - 12 : (h == 0 ? 12 : h);
        return $"{display}:{m:D2} {ampm}";
    }
    
    #endregion
    
    #region CONTEXT MENU
    
    [ContextMenu("Test: Force Eclipse")]
    public void ForceEclipseNow()
    {
        if (!eclipseReady) return;
        SetTime(FORCED_ECLIPSE_HOUR);
        if (clearWeather != null && weatherModule?.ecosystem != null)
            weatherModule.ecosystem.SetWeather(clearWeather);
        eclipseRatioField.SetValue(eclipseComponent, 1.0f);
        Debug.Log("[TEST] Eclipse!");
    }
    
    [ContextMenu("Refresh Materials")]
    public void RefreshMaterials()
    {
        foliageMaterials.Clear();
        grassMaterials.Clear();
        FindFoliageMaterials();
        FindGrassMaterialsFromTerrain();
    }
    
    #endregion
}