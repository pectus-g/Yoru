using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// YORU: Complete COZY 3 Integration - V10 (FIXED SATELLITE MODULE)
/// 
/// FIXES IN V10:
/// - Proper COZY module naming: CozySatelliteModule
/// - Access satellite profiles array for moon phase control
/// - Comprehensive reflection search for all possible moon offset fields
/// - Better debug output for troubleshooting
/// 
/// MOON PHASES (Dark Path):
///   1L = Crescent moon
///   2L = Quarter moon  
///   3L = Gibbous moon
///   4L = Nearly full
///   5L = Full moon (but 6L+ has clouds covering it)
///   
/// GRADUAL ECLIPSE (when diff ≤ 1 and total ≥ threshold):
///   3L/3R, 3L/4R, 4L/3R → Slight eclipse
///   4L/4R, 4L/5R, 5L/4R → Strong eclipse  
///   5L/5R → FULL eclipse
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
    [Tooltip("Balance 0 = 9 AM (neutral morning)")]
    [SerializeField] private float neutralHour = 9f;
    [Tooltip("Balance -1")]
    [SerializeField] private float dark1Hour = 16f;
    [Tooltip("Balance -2")]
    [SerializeField] private float dark2Hour = 18f;
    [Tooltip("Balance -3")]
    [SerializeField] private float dark3Hour = 20f;
    [Tooltip("Balance -4")]
    [SerializeField] private float dark4Hour = 22f;
    [Tooltip("Balance -5 or darker")]
    [SerializeField] private float dark5Hour = 0f;
    
    [Header("=== TIME SETTINGS (Light Path) ===")]
    [Tooltip("Balance +1")]
    [SerializeField] private float light1Hour = 10f;
    [Tooltip("Balance +2")]
    [SerializeField] private float light2Hour = 11f;
    [Tooltip("Balance +3")]
    [SerializeField] private float light3Hour = 11.5f;
    [Tooltip("Balance +4")]
    [SerializeField] private float light4Hour = 12f;
    [Tooltip("Balance +5")]
    [SerializeField] private float light5Hour = 12.25f;
    
    [Header("=== MOON PHASE SETTINGS ===")]
    [Tooltip("Moon cycle offset for each left ring count (0=new moon, 14=full moon in 28-day cycle)")]
    [SerializeField] private float moonOffset0Rings = 0f;    // No left rings = new moon (invisible)
    [SerializeField] private float moonOffset1Ring = 5f;     // Crescent
    [SerializeField] private float moonOffset2Rings = 7f;    // Quarter
    [SerializeField] private float moonOffset3Rings = 10f;   // Gibbous
    [SerializeField] private float moonOffset4Rings = 12f;   // Nearly full
    [SerializeField] private float moonOffset5PlusRings = 14f; // Full moon
    
    [Header("=== GRADUAL ECLIPSE SETTINGS ===")]
    // Eclipse requires BOTH tails to have rings AND be close (diff ≤ 1)
    
    [Header("Eclipse Intensity by Combination")]
    [Tooltip("3L/2R or 2L/3R (total=5, diff=1)")]
    [SerializeField, Range(0f, 1f)] private float eclipse3L2R = 0.20f;
    [Tooltip("3L/3R (total=6, diff=0)")]
    [SerializeField, Range(0f, 1f)] private float eclipse3L3R = 0.40f;
    [Tooltip("3L/4R or 4L/3R (total=7, diff=1)")]
    [SerializeField, Range(0f, 1f)] private float eclipse4L3R = 0.50f;
    [Tooltip("4L/4R (total=8, diff=0)")]
    [SerializeField, Range(0f, 1f)] private float eclipse4L4R = 0.60f;
    [Tooltip("5L/4R or 4L/5R (total=9, diff=1)")]
    [SerializeField, Range(0f, 1f)] private float eclipse5L4R = 0.75f;
    [Tooltip("5L/5R - FULL Eclipse")]
    [SerializeField, Range(0f, 1f)] private float eclipseFull = 1.0f;
    
    [Header("=== NEW TIME SETTINGS ===")]
    [Tooltip("Sunset hour (diff=2, dark winning, both have rings)")]
    [SerializeField] private float sunsetHour = 18.5f;
    [Tooltip("Sunrise hour (diff=2, light winning, both have rings)")]
    [SerializeField] private float sunriseHour = 6.5f;
    [Tooltip("Night hour (diff>2, dark winning)")]
    [SerializeField] private float nightHour = 0f;
    [Tooltip("Bright day hour (diff>2, light winning)")]
    [SerializeField] private float brightDayHour = 12f;
    
    [Header("=== ECLIPSE TIME ===")]
    [Tooltip("Time of day during full eclipse (5L/5R)")]
    [SerializeField] private float eclipseHour = 16f;
    
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
    
    // Eclipse via reflection
    private Component eclipseComponent;
    private FieldInfo eclipseRatioField;
    private bool eclipseReady = false;
    
    // Satellite (Moon) via reflection - V10: Correct COZY access path
    // satellites is SatelliteProfile[] directly on CozySatelliteModule
    private object satelliteModule;              // The CozySatelliteModule component
    private object satelliteProfile;             // The SatelliteProfile (satellites[0])
    private FieldInfo rotationPeriodOffsetField; // rotationPeriodOffset on SatelliteProfile
    private bool satelliteReady = false;
    
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
        FindEclipseModule();
        FindSatelliteModule();
        
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
    
    #region MATERIAL FINDING
    
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
        
        // Manual materials
        foreach (var mat in manualGrassMaterials)
            if (mat != null) found.Add(mat);
        
        // From terrain detail prototypes
        if (sceneTerrain != null)
        {
            var data = sceneTerrain.terrainData;
            if (data != null && data.detailPrototypes != null)
            {
                foreach (var proto in data.detailPrototypes)
                {
                    if (proto.prototype != null)
                    {
                        var mr = proto.prototype.GetComponent<MeshRenderer>();
                        if (mr != null && mr.sharedMaterial != null)
                            found.Add(mr.sharedMaterial);
                    }
                }
            }
        }
        
        grassMaterials.AddRange(found);
        
        if (logChanges)
            Debug.Log($"[YORU] Found {found.Count} grass materials");
    }
    
    bool HasWindProperty(Material mat)
    {
        string[] windProps = { "_Sway_Wind_Intensity", "_Wind_Intensity", "_WindIntensity", 
                               "_WindSpeed", "_WaveSpeed", "_SwaySpeed", "_Sway" };
        foreach (var prop in windProps)
            if (mat.HasProperty(prop)) return true;
        return false;
    }
    
    #endregion
    
    #region ECLIPSE MODULE
    
    void FindEclipseModule()
    {
        if (cozy == null) return;
        
        // Search on COZY object for Eclipse module
        Component[] allComponents = cozy.gameObject.GetComponents<Component>();
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;
            System.Type compType = comp.GetType();
            if (compType.Name.Contains("Eclipse"))
            {
                SetupEclipseComponent(comp, compType);
                if (eclipseReady) return;
            }
        }
        
        // Search children
        Component[] childComponents = cozy.gameObject.GetComponentsInChildren<Component>(true);
        foreach (var comp in childComponents)
        {
            if (comp == null) continue;
            System.Type compType = comp.GetType();
            if (compType.Name.Contains("Eclipse"))
            {
                SetupEclipseComponent(comp, compType);
                if (eclipseReady) return;
            }
        }
    }
    
    void SetupEclipseComponent(Component comp, System.Type compType)
    {
        eclipseComponent = comp;
        
        // Try different field names
        string[] possibleFieldNames = { "eclipseRatio", "eclipseAmount", "eclipse", "ratio", "intensity" };
        
        foreach (string fieldName in possibleFieldNames)
        {
            eclipseRatioField = compType.GetField(fieldName, 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (eclipseRatioField != null) break;
        }
        
        if (eclipseRatioField != null)
        {
            eclipseReady = true;
            Debug.Log($"[YORU] ✓ Eclipse Module ready (field: {eclipseRatioField.Name})");
        }
    }
    
    void SetEclipse(float ratio)
    {
        if (!eclipseReady || eclipseComponent == null || eclipseRatioField == null)
            return;
        
        ratio = Mathf.Clamp01(ratio);
        
        try
        {
            object currentValue = eclipseRatioField.GetValue(eclipseComponent);
            float currentFloat = System.Convert.ToSingle(currentValue);
            
            if (Mathf.Abs(currentFloat - ratio) > 0.01f)
            {
                if (logChanges)
                    Debug.Log($"[YORU] Eclipse: {currentFloat:F2} → {ratio:F2}");
                
                if (eclipseRatioField.FieldType == typeof(float))
                    eclipseRatioField.SetValue(eclipseComponent, ratio);
                else if (eclipseRatioField.FieldType == typeof(double))
                    eclipseRatioField.SetValue(eclipseComponent, (double)ratio);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[YORU] Eclipse error: {e.Message}");
        }
    }
    
    float CalculateGradualEclipse(int leftRings, int rightRings)
    {
        // Eclipse requires BOTH tails to have rings AND be balanced (diff ≤ 1)
        // Minimum: both need at least 2 rings (smallest combo is 3L/2R or 2L/3R)
        
        int minRings = Mathf.Min(leftRings, rightRings);
        int maxRings = Mathf.Max(leftRings, rightRings);
        int diff = maxRings - minRings;
        
        // Must have diff ≤ 1 for eclipse
        if (diff > 1)
            return 0f;
        
        // Check specific combinations (ordered by intensity)
        // 5L/5R = 100% full eclipse
        if (minRings == 5 && maxRings == 5)
            return eclipseFull;
        
        // 5L/4R or 4L/5R = 75%
        if (minRings == 4 && maxRings == 5)
            return eclipse5L4R;
        
        // 4L/4R = 60%
        if (minRings == 4 && maxRings == 4)
            return eclipse4L4R;
        
        // 4L/3R or 3L/4R = 50%
        if (minRings == 3 && maxRings == 4)
            return eclipse4L3R;
        
        // 3L/3R = 40%
        if (minRings == 3 && maxRings == 3)
            return eclipse3L3R;
        
        // 3L/2R or 2L/3R = 20%
        if (minRings == 2 && maxRings == 3)
            return eclipse3L2R;
        
        // No eclipse for lower combinations
        return 0f;
    }
    
    #endregion
    
    #region SATELLITE (MOON) MODULE - V10 FIXED
    
    // Based on COZY source (CozySatelliteModule.cs):
    // public SatelliteProfile[] satellites = new SatelliteProfile[0];
    // satellites[0] IS the SatelliteProfile directly (not an intermediate object)
    // SatelliteProfile has: int rotationPeriodOffset (controls moon phase!)
    // Moon phase formula: phase = Floor(((AbsoluteDay + rotationPeriodOffset + 1) % rotationPeriod) / (rotationPeriod / 8))
    
    void FindSatelliteModule()
    {
        if (cozy == null) return;
        
        Debug.Log("[YORU] Searching for Satellite Module (V10 - correct path)...");
        
        // Step 1: Find CozySatelliteModule component - IT'S ON A CHILD OBJECT "Modules"!
        Component[] allComponents = cozy.gameObject.GetComponentsInChildren<Component>(true);
        Component satModule = null;
        
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;
            if (comp.GetType().Name == "CozySatelliteModule")
            {
                satModule = comp;
                Debug.Log($"[YORU] Found CozySatelliteModule on: {comp.gameObject.name}");
                break;
            }
        }
        
        if (satModule == null)
        {
            Debug.LogWarning("[YORU] CozySatelliteModule not found on COZY Weather Sphere");
            return;
        }
        
        satelliteModule = satModule;
        System.Type moduleType = satModule.GetType();
        
        // Step 2: Get 'satellites' array (SatelliteProfile[])
        FieldInfo satellitesField = moduleType.GetField("satellites", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (satellitesField == null)
        {
            Debug.LogWarning("[YORU] 'satellites' field not found on CozySatelliteModule");
            PrintTypeMembers(moduleType);
            return;
        }
        
        object satellitesArray = satellitesField.GetValue(satModule);
        if (satellitesArray == null)
        {
            Debug.LogWarning("[YORU] satellites array is null");
            return;
        }
        
        // Step 3: Get first satellite profile (main moon) - satellites IS the SatelliteProfile array!
        if (satellitesArray is System.Array arr)
        {
            if (arr.Length == 0)
            {
                Debug.LogWarning("[YORU] satellites array is empty - no moon configured in COZY Satellite Module");
                return;
            }
            
            satelliteProfile = arr.GetValue(0);
            Debug.Log($"[YORU] Found main moon profile: {satelliteProfile.GetType().Name}");
        }
        else if (satellitesArray is System.Collections.IList list)
        {
            if (list.Count == 0)
            {
                Debug.LogWarning("[YORU] satellites list is empty - no moon configured in COZY Satellite Module");
                return;
            }
            
            satelliteProfile = list[0];
            Debug.Log($"[YORU] Found main moon profile: {satelliteProfile.GetType().Name}");
        }
        else
        {
            Debug.LogWarning($"[YORU] satellites is unexpected type: {satellitesArray.GetType().Name}");
            return;
        }
        
        if (satelliteProfile == null)
        {
            Debug.LogWarning("[YORU] First satellite profile is null");
            return;
        }
        
        // Step 4: Get rotationPeriodOffset field from SatelliteProfile
        System.Type profileType = satelliteProfile.GetType();
        rotationPeriodOffsetField = profileType.GetField("rotationPeriodOffset", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (rotationPeriodOffsetField == null)
        {
            Debug.LogWarning("[YORU] 'rotationPeriodOffset' field not found on SatelliteProfile");
            PrintTypeMembers(profileType);
            return;
        }
        
        satelliteReady = true;
        Debug.Log($"[YORU] ✓ Satellite Module ready! rotationPeriodOffset field found (type: {rotationPeriodOffsetField.FieldType.Name})");
        
        // Log current value
        object currentVal = rotationPeriodOffsetField.GetValue(satelliteProfile);
        Debug.Log($"[YORU] Current rotationPeriodOffset: {currentVal}");
    }
    
    void PrintTypeMembers(System.Type type)
    {
        Debug.Log($"  Fields on {type.Name}:");
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Debug.Log($"    {f.Name} ({f.FieldType.Name})");
        }
    }
    
    void SetMoonPhase(int leftRings)
    {
        if (!satelliteReady || satelliteProfile == null || rotationPeriodOffsetField == null)
            return;
        
        // Calculate rotationPeriodOffset based on left rings
        // With rotationPeriod = 28 and 8 phases:
        // Phase 0 = new moon (offset ~0)
        // Phase 4 = full moon (offset ~14)
        int offset = GetRotationPeriodOffsetForLeftRings(leftRings);
        
        try
        {
            object currentValue = rotationPeriodOffsetField.GetValue(satelliteProfile);
            int currentInt = System.Convert.ToInt32(currentValue);
            
            if (currentInt != offset)
            {
                rotationPeriodOffsetField.SetValue(satelliteProfile, offset);
                
                if (logChanges)
                    Debug.Log($"[YORU] Moon rotationPeriodOffset: {currentInt} → {offset} (for {leftRings}L)");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[YORU] Moon phase error: {e.Message}");
        }
    }
    
    int GetRotationPeriodOffsetForLeftRings(int leftRings)
    {
        // Moon phase formula: phase = Floor(((AbsoluteDay + rotationPeriodOffset + 1) % 28) / 3.5)
        // We want to control phase based on left rings
        // Assuming AbsoluteDay can be any value, we offset to get desired phase
        // 
        // For simplicity, we set offset to directly map to phase * 3.5
        // 0L = new moon (phase 0) → offset makes result ~0
        // 1L = waxing crescent (phase 1) → offset for ~3.5
        // 2L = first quarter (phase 2) → offset for ~7
        // 3L = waxing gibbous (phase 3) → offset for ~10.5
        // 4L = nearly full (phase 3-4) → offset for ~12
        // 5L = full moon (phase 4) → offset for ~14
        
        switch (leftRings)
        {
            case 0: return Mathf.RoundToInt(moonOffset0Rings);     // new moon
            case 1: return Mathf.RoundToInt(moonOffset1Ring);      // crescent
            case 2: return Mathf.RoundToInt(moonOffset2Rings);     // quarter
            case 3: return Mathf.RoundToInt(moonOffset3Rings);     // gibbous
            case 4: return Mathf.RoundToInt(moonOffset4Rings);     // nearly full
            default: return Mathf.RoundToInt(moonOffset5PlusRings); // full moon (5+)
        }
    }
    
    #endregion
    
    #region STATE APPLICATION
    
    void OnRingsChanged(int left, int right)
    {
        ApplyFullState(left, right);
    }
    
    void ApplyFullState(int leftRings, int rightRings)
    {
        if (cozy == null) return;
        
        int balance = rightRings - leftRings;
        int weatherStage = WorldStateManager.Instance?.WeatherStage ?? 0;
        
        // Eclipse (V10: Gradual based on both tails being high and balanced)
        float eclipseIntensity = CalculateGradualEclipse(leftRings, rightRings);
        bool hasEclipse = eclipseIntensity > 0f;
        
        // Time - NEW LOGIC based on difference and which side is winning
        float hour = GetTimeForRings(leftRings, rightRings, eclipseIntensity);
        SetTime(hour);
        
        // Weather
        var weather = SelectWeatherTwoLayer(balance, weatherStage, hasEclipse);
        SetWeather(weather);
        
        // Apply eclipse
        SetEclipse(eclipseIntensity);
        
        // Moon phase (based on left rings)
        SetMoonPhase(leftRings);
        
        // Wind
        float windIntensity = CalculateWindIntensity(balance, weatherStage);
        ApplyWind(windIntensity);
        
        // Log
        if (logChanges)
        {
            string weatherName = weather?.name ?? "None";
            string stageInfo = weatherStage > 0 ? $" [Stage {weatherStage}]" : "";
            string eclipseInfo = eclipseIntensity > 0 ? $" [Eclipse: {Mathf.RoundToInt(eclipseIntensity * 100)}%]" : "";
            Debug.Log($"[YORU] {leftRings}L/{rightRings}R → {FormatHour(hour)}, {weatherName}, wind:{windIntensity:F2}{stageInfo}{eclipseInfo}");
        }
        
        lastLeft = leftRings;
        lastRight = rightRings;
    }
    
    void LogStatus()
    {
        Debug.Log("[YORU] ===== STATUS (V10) =====");
        Debug.Log($"  Weather Module: {(weatherModule != null ? "✓" : "✗")}");
        Debug.Log($"  Eclipse Module: {(eclipseReady ? "✓" : "✗")}");
        Debug.Log($"  Satellite Module: {(satelliteReady ? "✓" : "✗")}");
        Debug.Log($"  WindZone: {(sceneWindZone != null ? "✓" : "✗")}");
        Debug.Log($"  Terrain: {(sceneTerrain != null ? "✓" : "✗")}");
        Debug.Log($"  Foliage Materials: {foliageMaterials.Count}");
        Debug.Log($"  Grass Materials: {grassMaterials.Count}");
        Debug.Log("============================");
    }
    
    #endregion
    
    #region TIME
    
    float GetTimeForRings(int leftRings, int rightRings, float eclipseIntensity)
    {
        int diff = Mathf.Abs(leftRings - rightRings);
        bool darkWinning = leftRings > rightRings;
        bool lightWinning = rightRings > leftRings;
        bool bothHaveRings = leftRings > 0 && rightRings > 0;
        
        // 1. Eclipse time - when eclipse is happening
        if (eclipseIntensity > 0f)
        {
            return eclipseHour;
        }
        
        // 2. Transitional (diff = 2, BOTH tails have rings)
        //    Dark winning → SUNSET
        //    Light winning → SUNRISE
        if (diff == 2 && bothHaveRings)
        {
            return darkWinning ? sunsetHour : sunriseHour;
        }
        
        // 3. Committed to path (diff > 2)
        //    Dark winning → NIGHT with visible moon
        //    Light winning → BRIGHT DAY
        if (diff > 2)
        {
            return darkWinning ? nightHour : brightDayHour;
        }
        
        // 4. Fallback: diff < 2 without eclipse, or one side = 0
        //    Use neutral/gradual time based on balance
        int balance = rightRings - leftRings;
        return GetTimeForBalance(balance);
    }
    
    float GetTimeForBalance(int balance)
    {
        if (balance == 0) return neutralHour;
        
        if (balance < 0)
        {
            switch (balance)
            {
                case -1: return dark1Hour;
                case -2: return dark2Hour;
                case -3: return dark3Hour;
                case -4: return dark4Hour;
                default: return dark5Hour;
            }
        }
        else
        {
            switch (balance)
            {
                case 1: return light1Hour;
                case 2: return light2Hour;
                case 3: return light3Hour;
                case 4: return light4Hour;
                default: return light5Hour;
            }
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
        
        if (weatherStage == 0)
            return clearWeather;
        
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
        if (WorldStateManager.Instance?.IsEclipse == true)
            return 0.1f;
        
        float baseWind = 0.2f;
        
        if (balance >= 0)
            return baseWind;
        
        if (weatherStage == 0)
            return Mathf.Lerp(baseWind, 0.35f, Mathf.Abs(balance) / 5f);
        
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
            
            if (mat.HasProperty("_Sway_Wind_Intensity")) mat.SetFloat("_Sway_Wind_Intensity", intensity * mult);
            if (mat.HasProperty("_Sway_Wind_Speed")) mat.SetFloat("_Sway_Wind_Speed", speed);
            if (mat.HasProperty("_Wiggle_Wind_Intensity")) mat.SetFloat("_Wiggle_Wind_Intensity", intensity * 0.5f * mult);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Small")) mat.SetFloat("_Wiggle_Wind_Speed_Small", speed * 0.5f);
            if (mat.HasProperty("_Wiggle_Wind_Speed_Large")) mat.SetFloat("_Wiggle_Wind_Speed_Large", speed * 0.8f);
            if (mat.HasProperty("_Wind_Intensity")) mat.SetFloat("_Wind_Intensity", intensity * mult);
            if (mat.HasProperty("_Wind_Speed")) mat.SetFloat("_Wind_Speed", speed);
            if (mat.HasProperty("_WindSpeed")) mat.SetFloat("_WindSpeed", speed);
            if (mat.HasProperty("_WindIntensity")) mat.SetFloat("_WindIntensity", intensity * mult);
            if (mat.HasProperty("_WindScale")) mat.SetFloat("_WindScale", Mathf.Lerp(0.5f, 2f, intensity));
            if (mat.HasProperty("_Wind_Large_Intensity")) mat.SetFloat("_Wind_Large_Intensity", intensity * mult);
            if (mat.HasProperty("_Wind_Small_Intensity")) mat.SetFloat("_Wind_Small_Intensity", intensity * 0.5f * mult);
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
    
    [ContextMenu("Test: Force Full Eclipse")]
    public void ForceEclipseNow()
    {
        SetTime(eclipseHour);
        if (clearWeather != null && weatherModule?.ecosystem != null)
            weatherModule.ecosystem.SetWeather(clearWeather);
        SetEclipse(1.0f);
        Debug.Log("[TEST] Full Eclipse!");
    }
    
    [ContextMenu("Test: Partial Eclipse (0.3)")]
    public void TestPartialEclipse()
    {
        SetEclipse(0.3f);
        Debug.Log("[TEST] Partial Eclipse 0.3");
    }
    
    [ContextMenu("Test: Full Moon")]
    public void TestFullMoon()
    {
        SetMoonPhase(5);
        SetTime(0f); // Midnight to see the moon
        Debug.Log("[TEST] Full Moon at midnight");
    }
    
    [ContextMenu("Refresh Materials")]
    public void RefreshMaterials()
    {
        foliageMaterials.Clear();
        grassMaterials.Clear();
        FindFoliageMaterials();
        FindGrassMaterialsFromTerrain();
    }
    
    [ContextMenu("Print All COZY Modules")]
    public void PrintAllCozyModules()
    {
        if (cozy == null)
        {
            Debug.Log("[YORU] COZY not found");
            return;
        }
        
        Debug.Log("[YORU] === ALL COZY COMPONENTS ===");
        
        Component[] components = cozy.gameObject.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            var type = comp.GetType();
            Debug.Log($"Component: {type.Name} ({type.Namespace})");
        }
        
        Debug.Log("[YORU] === CHILD COMPONENTS ===");
        Component[] childComponents = cozy.gameObject.GetComponentsInChildren<Component>(true);
        foreach (var comp in childComponents)
        {
            if (comp == null) continue;
            var type = comp.GetType();
            if (type.Namespace != null && type.Namespace.Contains("Cozy"))
                Debug.Log($"Child: {comp.gameObject.name} → {type.Name}");
        }
        
        Debug.Log("============================");
    }
    
    [ContextMenu("Print Satellite Module Details")]
    public void PrintSatelliteDetails()
    {
        if (satelliteModule == null)
        {
            Debug.Log("[YORU] Satellite module not found");
            return;
        }
        
        Debug.Log($"[YORU] === SATELLITE MODULE ===");
        Debug.Log($"  Module: {satelliteModule.GetType().Name}");
            
        if (satelliteProfile != null)
        {
            Debug.Log($"  SatelliteProfile: {satelliteProfile.GetType().Name}");
            if (rotationPeriodOffsetField != null)
            {
                object val = rotationPeriodOffsetField.GetValue(satelliteProfile);
                Debug.Log($"  rotationPeriodOffset: {val}");
            }
            
            // Print all fields on the profile for debugging
            PrintTypeMembers(satelliteProfile.GetType());
        }
        else
        {
            Debug.Log($"  SatelliteProfile: null (no moon in satellites array)");
        }
        
        Debug.Log($"  satelliteReady: {satelliteReady}");
    }
    
    #endregion
}