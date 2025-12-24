using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// YORU: Complete COZY 3 Integration - V7 (TWO-LAYER SYSTEM)
/// 
/// TWO-LAYER WEATHER SYSTEM:
/// Layer 1 (Atmosphere): Balance -5 to +5 controls TIME (sky appearance)
///   - Weather stays CLEAR so you can see stars, galaxy, moon!
/// Layer 2 (Weather): Only kicks in when WeatherStage > 0
///   - WeatherStage activates when |balance| >= 5 AND total rings > 5
/// 
/// DARK PATH SKY PROGRESSION:
///   Balance -1: 4 PM (late afternoon)
///   Balance -2: 6 PM (sunset)
///   Balance -3: 8 PM (dusk, stars emerging)
///   Balance -4: 10 PM (night, stars + galaxy)
///   Balance -5: Midnight (full stars, galaxy, supernova!)
///   
/// WEATHER ESCALATION (only when WeatherStage > 0):
///   Stage 1: Partly Cloudy
///   Stage 2: Overcast
///   Stage 3: Light Rain
///   Stage 4: Heavy Rain
///   Stage 5: Thunder Storm
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
    [SerializeField] private float dark1Hour = 16f;  // 4 PM
    [Tooltip("Balance -2")]
    [SerializeField] private float dark2Hour = 18f;  // 6 PM (sunset)
    [Tooltip("Balance -3")]
    [SerializeField] private float dark3Hour = 20f;  // 8 PM (dusk, stars emerging)
    [Tooltip("Balance -4")]
    [SerializeField] private float dark4Hour = 22f;  // 10 PM (night, galaxy visible)
    [Tooltip("Balance -5 or darker")]
    [SerializeField] private float dark5Hour = 0f;   // Midnight (full stars, galaxy!)
    
    [Header("=== TIME SETTINGS (Light Path) ===")]
    [Tooltip("Balance +1")]
    [SerializeField] private float light1Hour = 10f;  // 10 AM
    [Tooltip("Balance +2")]
    [SerializeField] private float light2Hour = 9f;   // 9 AM
    [Tooltip("Balance +3")]
    [SerializeField] private float light3Hour = 8f;   // 8 AM (sunrise)
    [Tooltip("Balance +4")]
    [SerializeField] private float light4Hour = 7f;   // 7 AM (golden hour)
    [Tooltip("Balance +5 or lighter")]
    [SerializeField] private float light5Hour = 6f;   // 6 AM (dawn glow)
    
    [Header("=== ECLIPSE SETTINGS ===")]
    [Tooltip("Eclipse hour - 15.5 = 3:30 PM")]
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
                        {
                            found.Add(mat);
                        }
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
                {
                    found.Add(mat);
                }
            }
        }
        
        foreach (var mat in manualGrassMaterials)
        {
            if (mat != null && !found.Contains(mat))
            {
                found.Add(mat);
            }
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
                    Debug.Log($"[YORU] ✓ Eclipse Module found! (FORCED to {FormatHourFloat(FORCED_ECLIPSE_HOUR)})");
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
        Debug.Log("[YORU] ===== STATUS (V7 Two-Layer) =====");
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
        
        // Get balance and weather stage from WorldStateManager
        int balance = right - left;
        int weatherStage = 0;
        
        if (WorldStateManager.Instance != null)
        {
            weatherStage = WorldStateManager.Instance.WeatherStage;
        }
        else
        {
            // Fallback calculation if no WorldStateManager
            int total = left + right;
            if (Mathf.Abs(balance) >= 5 && total > 5)
            {
                weatherStage = Mathf.Min(total - 5, 5);
            }
        }
        
        // TIME - Based on clamped balance (-5 to +5)
        float hour = isEclipse ? FORCED_ECLIPSE_HOUR : GetHourForBalance(balance);
        SetTime(hour);
        
        // WEATHER - TWO-LAYER SYSTEM
        // Layer 1: Keep CLEAR for balance -5 to +5 (so stars/galaxy visible!)
        // Layer 2: Only apply weather profiles when weatherStage > 0
        WeatherProfile weather = SelectWeatherTwoLayer(balance, weatherStage, isEclipse);
        SetWeather(weather);
        
        // ECLIPSE - ONLY at 5L/5R
        SetEclipse(isEclipse ? 1.0f : 0f);
        
        // WIND - Based on weather stage, not just balance
        float windIntensity = CalculateWindIntensityTwoLayer(balance, weatherStage);
        ApplyWind(windIntensity);
        
        if (logChanges)
        {
            string eclipseStr = isEclipse ? " [ECLIPSE]" : "";
            string weatherStageStr = weatherStage > 0 ? $" (Stage {weatherStage})" : "";
            Debug.Log($"[YORU] {left}L/{right}R → {FormatHourFloat(hour)}, {weather?.name}{weatherStageStr}, wind:{windIntensity:F2}{eclipseStr}");
        }
    }
    
    #region TIME - TWO LAYER
    
    /// <summary>
    /// Get time of day based on balance.
    /// Dark balance = evening/night (so you see stars/galaxy!)
    /// Light balance = morning
    /// </summary>
    float GetHourForBalance(int balance)
    {
        // Clamp to -5 to +5 range for atmosphere
        int clamped = Mathf.Clamp(balance, -5, 5);
        
        switch (clamped)
        {
            case -5: return dark5Hour;   // Midnight - full stars/galaxy!
            case -4: return dark4Hour;   // 10 PM - stars + galaxy
            case -3: return dark3Hour;   // 8 PM - dusk, stars emerging
            case -2: return dark2Hour;   // 6 PM - sunset
            case -1: return dark1Hour;   // 4 PM - late afternoon
            case 0:  return neutralHour; // Noon
            case 1:  return light1Hour;  // 10 AM
            case 2:  return light2Hour;  // 9 AM
            case 3:  return light3Hour;  // 8 AM - sunrise
            case 4:  return light4Hour;  // 7 AM - golden hour
            case 5:  return light5Hour;  // 6 AM - dawn glow
            default: return neutralHour;
        }
    }
    
    void SetTime(float hours)
    {
        if (cozy?.timeModule == null) return;
        
        // Handle midnight (0 or 24)
        while (hours >= 24) hours -= 24;
        while (hours < 0) hours += 24;
        
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.RoundToInt((hours - h) * 60f);
        cozy.timeModule.currentTime = new MeridiemTime(h, m);
    }
    
    #endregion
    
    #region WEATHER - TWO LAYER
    
    /// <summary>
    /// TWO-LAYER WEATHER SELECTION
    /// 
    /// Layer 1 (Balance -5 to +5): CLEAR weather - see the beautiful sky!
    /// Layer 2 (WeatherStage > 0): Weather profiles kick in
    /// </summary>
    WeatherProfile SelectWeatherTwoLayer(int balance, int weatherStage, bool isEclipse)
    {
        // Eclipse always clear
        if (isEclipse)
            return clearWeather;
        
        // Layer 1: No weather escalation yet - keep sky CLEAR!
        if (weatherStage == 0)
        {
            // Light path gets clear/mostly clear
            if (balance >= 0)
                return clearWeather;
            
            // Dark path ALSO gets clear - so you can see stars/galaxy!
            // The atmosphere (time of day) creates the mood, not clouds
            return clearWeather;
        }
        
        // Layer 2: Weather escalation based on stage
        bool isDarkPath = balance < 0;
        
        if (isDarkPath)
        {
            return GetDarkWeatherForStage(weatherStage);
        }
        else
        {
            return GetLightWeatherForStage(weatherStage);
        }
    }
    
    /// <summary>
    /// Dark path weather escalation (clouds, rain, storm)
    /// Only called when WeatherStage > 0
    /// </summary>
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
    
    /// <summary>
    /// Light path weather - stays clear but could add particles/effects
    /// </summary>
    WeatherProfile GetLightWeatherForStage(int stage)
    {
        // Light path stays clear - you add particles via COZY FX profiles
        // The "divine glow" comes from post-processing bloom, not weather
        return clearWeather ?? mostlyClearWeather;
    }
    
    void SetWeather(WeatherProfile profile)
    {
        if (weatherModule?.ecosystem == null || profile == null) return;
        weatherModule.ecosystem.SetWeather(profile);
    }
    
    #endregion
    
    #region WIND - TWO LAYER
    
    /// <summary>
    /// Wind intensity based on weather stage, not just balance.
    /// Calm until weather escalation kicks in.
    /// </summary>
    float CalculateWindIntensityTwoLayer(int balance, int weatherStage)
    {
        // Eclipse - very calm
        if (balance == 0 && WorldStateManager.Instance?.IsEclipse == true)
            return 0.1f;
        
        // Base wind - gentle
        float baseWind = 0.2f;
        
        // Light path - stays gentle
        if (balance >= 0)
            return baseWind;
        
        // Dark path without weather escalation - still gentle
        if (weatherStage == 0)
            return Mathf.Lerp(baseWind, 0.35f, Mathf.Abs(balance) / 5f);
        
        // Dark path WITH weather escalation - gets intense!
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
            
            // Builtin/Grass shader properties
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
            if (mat.HasProperty("_Frequency"))
                mat.SetFloat("_Frequency", Mathf.Lerp(1f, 3f, intensity));
            if (mat.HasProperty("_Amplitude"))
                mat.SetFloat("_Amplitude", intensity * stormMultiplier);
        }
    }
    
    #endregion
    
    #region UTILITY
    
    string FormatHourFloat(float hour)
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
    }
    
    #endregion
}