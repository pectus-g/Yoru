using UnityEngine;
using System;

/// <summary>
/// YORU: Volumetric Fog Controller - 29-STATE RING-BASED SYSTEM
/// 
/// Controls Kronnect's Volumetric Fog & Mist 2 based on ring values.
/// Listens to OnRingsChanged for precise state mapping.
/// 
/// 29 PRESETS (same as PostProcessController):
/// - Neutral, Sunset, Sunrise
/// - Dark1-5, Dark+Stage1-5
/// - Light1-5, Light+Stage1-5  
/// - Eclipse 20/40/50/60/75/100%
/// 
/// REQUIRES: Volumetric Fog & Mist 2 by Kronnect (Built-in Pipeline version)
/// </summary>
public class VolumetricFogController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class FogPreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        public string triggerCondition = "";
        
        [Header("Fog Settings")]
        [Range(0, 1)] public float density = 0.3f;
        [Range(0, 100)] public float height = 6f;
        [Range(0, 100)] public float baseHeight = 0f;
        public Color fogColor = Color.gray;
        
        [Header("God Rays / Light Scattering")]
        public bool enableGodRays = true;
        [Range(0, 0.2f)] public float godRayIntensity = 0.03f;
        
        [Header("Sky Haze")]
        [Range(0, 50)] public float skyHaze = 15f;
        
        public static FogPreset Lerp(FogPreset a, FogPreset b, float t)
        {
            return new FogPreset
            {
                stateName = b.stateName,
                density = Mathf.Lerp(a.density, b.density, t),
                height = Mathf.Lerp(a.height, b.height, t),
                baseHeight = Mathf.Lerp(a.baseHeight, b.baseHeight, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                enableGodRays = t > 0.5f ? b.enableGodRays : a.enableGodRays,
                godRayIntensity = Mathf.Lerp(a.godRayIntensity, b.godRayIntensity, t),
                skyHaze = Mathf.Lerp(a.skyHaze, b.skyHaze, t)
            };
        }
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== VOLUMETRIC FOG REFERENCE ===")]
    [Tooltip("Reference to the VolumetricFog component. Will auto-find if null.")]
    [SerializeField] private MonoBehaviour volumetricFog;
    [SerializeField] private bool autoFind = true;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private FogPreset neutralPreset = new FogPreset
    {
        stateName = "Neutral",
        triggerCondition = "0L/0R, 1L/1R, 2L/2R",
        density = 0.30f,
        height = 6f,
        fogColor = new Color(0.72f, 0.77f, 0.77f),
        enableGodRays = true,
        godRayIntensity = 0.03f,
        skyHaze = 15f
    };
    
    [Header("=== SUNSET ===")]
    [SerializeField] private FogPreset sunsetPreset = new FogPreset
    {
        stateName = "Sunset",
        triggerCondition = "1L/0R, 3L/1R, 4L/2R, 5L/3R, 6L/4R",
        density = 0.35f,
        height = 8f,
        fogColor = new Color(0.90f, 0.66f, 0.43f), // Orange
        enableGodRays = true,
        godRayIntensity = 0.06f,
        skyHaze = 18f
    };
    
    [Header("=== SUNRISE ===")]
    [SerializeField] private FogPreset sunrisePreset = new FogPreset
    {
        stateName = "Sunrise",
        triggerCondition = "0L/1R, 1L/3R, 2L/4R, 3L/5R, 4L/6R",
        density = 0.32f,
        height = 10f,
        fogColor = new Color(0.90f, 0.77f, 0.66f), // Peach
        enableGodRays = true,
        godRayIntensity = 0.05f,
        skyHaze = 16f
    };
    
    [Header("=== DARK PATH ===")]
    [SerializeField] private FogPreset dark1Preset = new FogPreset
    {
        stateName = "Dark1",
        triggerCondition = "diff > 2, dark winning, L small",
        density = 0.35f,
        height = 7f,
        fogColor = new Color(0.55f, 0.55f, 0.63f),
        enableGodRays = true,
        godRayIntensity = 0.025f,
        skyHaze = 18f
    };
    
    [SerializeField] private FogPreset dark2Preset = new FogPreset
    {
        stateName = "Dark2",
        triggerCondition = "diff > 2, L >= 2",
        density = 0.40f,
        height = 6f,
        fogColor = new Color(0.48f, 0.48f, 0.58f),
        enableGodRays = true,
        godRayIntensity = 0.02f,
        skyHaze = 20f
    };
    
    [SerializeField] private FogPreset dark3Preset = new FogPreset
    {
        stateName = "Dark3",
        triggerCondition = "diff > 2, L >= 3",
        density = 0.45f,
        height = 5f,
        fogColor = new Color(0.40f, 0.40f, 0.50f),
        enableGodRays = false,
        godRayIntensity = 0f,
        skyHaze = 25f
    };
    
    [SerializeField] private FogPreset dark4Preset = new FogPreset
    {
        stateName = "Dark4",
        triggerCondition = "diff > 2, L >= 4",
        density = 0.50f,
        height = 4f,
        fogColor = new Color(0.32f, 0.32f, 0.42f),
        enableGodRays = false,
        godRayIntensity = 0f,
        skyHaze = 30f
    };
    
    [SerializeField] private FogPreset dark5Preset = new FogPreset
    {
        stateName = "Dark5 (Midnight)",
        triggerCondition = "diff > 2, L >= 5",
        density = 0.55f,
        height = 3f,
        fogColor = new Color(0.24f, 0.24f, 0.34f),
        enableGodRays = false,
        godRayIntensity = 0f,
        skyHaze = 35f
    };
    
    [Header("=== DARK ESCALATION (Storms) ===")]
    [SerializeField] private FogPreset darkStage1Preset = new FogPreset
    {
        stateName = "Dark+Stage1 (Partly Cloudy)",
        triggerCondition = "L >= 6, dark committed",
        density = 0.58f,
        height = 3f,
        baseHeight = 2f,
        fogColor = new Color(0.24f, 0.26f, 0.34f),
        enableGodRays = false,
        skyHaze = 38f
    };
    
    [SerializeField] private FogPreset darkStage2Preset = new FogPreset
    {
        stateName = "Dark+Stage2 (Overcast)",
        triggerCondition = "L >= 7",
        density = 0.62f,
        height = 2f,
        baseHeight = 4f,
        fogColor = new Color(0.22f, 0.25f, 0.32f),
        enableGodRays = false,
        skyHaze = 40f
    };
    
    [SerializeField] private FogPreset darkStage3Preset = new FogPreset
    {
        stateName = "Dark+Stage3 (Light Rain)",
        triggerCondition = "L >= 8",
        density = 0.68f,
        height = 2f,
        baseHeight = 6f,
        fogColor = new Color(0.20f, 0.24f, 0.30f),
        enableGodRays = false,
        skyHaze = 42f
    };
    
    [SerializeField] private FogPreset darkStage4Preset = new FogPreset
    {
        stateName = "Dark+Stage4 (Heavy Rain)",
        triggerCondition = "L >= 9",
        density = 0.75f,
        height = 2f,
        baseHeight = 8f,
        fogColor = new Color(0.18f, 0.22f, 0.28f),
        enableGodRays = false,
        skyHaze = 45f
    };
    
    [SerializeField] private FogPreset darkStage5Preset = new FogPreset
    {
        stateName = "Dark+Stage5 (THUNDERSTORM)",
        triggerCondition = "L = 10",
        density = 0.85f,
        height = 1f,
        baseHeight = 10f,
        fogColor = new Color(0.16f, 0.18f, 0.24f),
        enableGodRays = false,
        skyHaze = 50f
    };
    
    [Header("=== LIGHT PATH ===")]
    [SerializeField] private FogPreset light1Preset = new FogPreset
    {
        stateName = "Light1",
        triggerCondition = "diff < -2, light winning, R small",
        density = 0.25f,
        height = 8f,
        fogColor = new Color(0.77f, 0.77f, 0.72f),
        enableGodRays = true,
        godRayIntensity = 0.035f,
        skyHaze = 12f
    };
    
    [SerializeField] private FogPreset light2Preset = new FogPreset
    {
        stateName = "Light2",
        triggerCondition = "diff < -2, R >= 2",
        density = 0.22f,
        height = 10f,
        fogColor = new Color(0.83f, 0.83f, 0.77f),
        enableGodRays = true,
        godRayIntensity = 0.04f,
        skyHaze = 10f
    };
    
    [SerializeField] private FogPreset light3Preset = new FogPreset
    {
        stateName = "Light3",
        triggerCondition = "diff < -2, R >= 3",
        density = 0.18f,
        height = 12f,
        fogColor = new Color(0.90f, 0.90f, 0.83f),
        enableGodRays = true,
        godRayIntensity = 0.05f,
        skyHaze = 8f
    };
    
    [SerializeField] private FogPreset light4Preset = new FogPreset
    {
        stateName = "Light4",
        triggerCondition = "diff < -2, R >= 4",
        density = 0.15f,
        height = 15f,
        fogColor = new Color(0.94f, 0.94f, 0.88f),
        enableGodRays = true,
        godRayIntensity = 0.06f,
        skyHaze = 6f
    };
    
    [SerializeField] private FogPreset light5Preset = new FogPreset
    {
        stateName = "Light5 (Heavenly)",
        triggerCondition = "diff < -2, R >= 5",
        density = 0.12f,
        height = 20f,
        fogColor = new Color(1.0f, 0.97f, 0.90f),
        enableGodRays = true,
        godRayIntensity = 0.08f,
        skyHaze = 5f
    };
    
    [Header("=== LIGHT ESCALATION (Divine) ===")]
    [SerializeField] private FogPreset lightStage1Preset = new FogPreset
    {
        stateName = "Light+Stage1 (Divine Beginning)",
        triggerCondition = "R >= 6, light committed",
        density = 0.10f,
        height = 25f,
        fogColor = new Color(1.0f, 0.98f, 0.90f),
        enableGodRays = true,
        godRayIntensity = 0.10f,
        skyHaze = 4f
    };
    
    [SerializeField] private FogPreset lightStage2Preset = new FogPreset
    {
        stateName = "Light+Stage2 (Radiant)",
        triggerCondition = "R >= 7",
        density = 0.08f,
        height = 30f,
        fogColor = new Color(1.0f, 0.99f, 0.90f),
        enableGodRays = true,
        godRayIntensity = 0.12f,
        skyHaze = 3f
    };
    
    [SerializeField] private FogPreset lightStage3Preset = new FogPreset
    {
        stateName = "Light+Stage3 (Glorious)",
        triggerCondition = "R >= 8",
        density = 0.06f,
        height = 40f,
        fogColor = new Color(1.0f, 0.99f, 0.90f),
        enableGodRays = true,
        godRayIntensity = 0.14f,
        skyHaze = 2f
    };
    
    [SerializeField] private FogPreset lightStage4Preset = new FogPreset
    {
        stateName = "Light+Stage4 (Transcendent)",
        triggerCondition = "R >= 9",
        density = 0.04f,
        height = 50f,
        fogColor = new Color(1.0f, 1.0f, 0.90f),
        enableGodRays = true,
        godRayIntensity = 0.16f,
        skyHaze = 1f
    };
    
    [SerializeField] private FogPreset lightStage5Preset = new FogPreset
    {
        stateName = "Light+Stage5 (MAXIMUM DIVINE)",
        triggerCondition = "R = 10",
        density = 0.02f,
        height = 60f,
        fogColor = new Color(1.0f, 1.0f, 0.94f),
        enableGodRays = true,
        godRayIntensity = 0.20f,
        skyHaze = 0.5f
    };
    
    [Header("=== ECLIPSE STATES ===")]
    [SerializeField] private FogPreset eclipse20Preset = new FogPreset
    {
        stateName = "Eclipse 20%",
        triggerCondition = "2L/3R or 3L/2R",
        density = 0.38f,
        height = 12f,
        fogColor = new Color(0.66f, 0.66f, 0.77f),
        enableGodRays = true,
        godRayIntensity = 0.02f,
        skyHaze = 20f
    };
    
    [SerializeField] private FogPreset eclipse40Preset = new FogPreset
    {
        stateName = "Eclipse 40%",
        triggerCondition = "3L/3R",
        density = 0.42f,
        height = 15f,
        fogColor = new Color(0.58f, 0.58f, 0.72f),
        enableGodRays = true,
        godRayIntensity = 0.015f,
        skyHaze = 22f
    };
    
    [SerializeField] private FogPreset eclipse50Preset = new FogPreset
    {
        stateName = "Eclipse 50%",
        triggerCondition = "3L/4R or 4L/3R",
        density = 0.45f,
        height = 18f,
        fogColor = new Color(0.50f, 0.50f, 0.68f),
        enableGodRays = false,
        skyHaze = 25f
    };
    
    [SerializeField] private FogPreset eclipse60Preset = new FogPreset
    {
        stateName = "Eclipse 60%",
        triggerCondition = "4L/4R",
        density = 0.48f,
        height = 20f,
        fogColor = new Color(0.42f, 0.42f, 0.64f),
        enableGodRays = false,
        skyHaze = 28f
    };
    
    [SerializeField] private FogPreset eclipse75Preset = new FogPreset
    {
        stateName = "Eclipse 75%",
        triggerCondition = "4L/5R or 5L/4R",
        density = 0.52f,
        height = 25f,
        fogColor = new Color(0.36f, 0.36f, 0.60f),
        enableGodRays = false,
        skyHaze = 30f
    };
    
    [SerializeField] private FogPreset eclipseFullPreset = new FogPreset
    {
        stateName = "Eclipse FULL (100%)",
        triggerCondition = "5L/5R (Perfect Balance)",
        density = 0.55f,
        height = 30f,
        fogColor = new Color(0.30f, 0.30f, 0.56f),
        enableGodRays = false,
        skyHaze = 32f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private string currentPresetName = "None";
    [SerializeField] private int debugLeftRings;
    [SerializeField] private int debugRightRings;
    
    #endregion
    
    #region Private Fields
    
    private FogPreset currentPreset;
    private FogPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    
    // Cached reflection info for Kronnect fog
    private System.Type fogType;
    private System.Reflection.PropertyInfo densityProperty;
    private System.Reflection.PropertyInfo heightProperty;
    private System.Reflection.PropertyInfo baseHeightProperty;
    private System.Reflection.PropertyInfo colorProperty;
    private System.Reflection.PropertyInfo sunLightScatteringProperty;
    private System.Reflection.PropertyInfo skyHazeProperty;
    private bool reflectionInitialized;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        FindVolumetricFog();
        InitializeReflection();
        InitializeState();
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
    }
    
    void Update()
    {
        if (isTransitioning)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            if (transitionProgress >= 1f)
            {
                transitionProgress = 1f;
                currentPreset = targetPreset;
                isTransitioning = false;
            }
            
            var lerped = FogPreset.Lerp(currentPreset, targetPreset, transitionProgress);
            ApplyPreset(lerped);
        }
    }
    
    #endregion
    
    #region Setup
    
    void FindVolumetricFog()
    {
        if (volumetricFog == null && autoFind)
        {
            var fogComponents = FindObjectsOfType<MonoBehaviour>();
            foreach (var comp in fogComponents)
            {
                if (comp.GetType().Name == "VolumetricFog")
                {
                    volumetricFog = comp;
                    if (logChanges) Debug.Log("[VolumetricFogController] Found VolumetricFog component");
                    break;
                }
            }
        }
    }
    
    void InitializeReflection()
    {
        if (volumetricFog == null) return;
        
        fogType = volumetricFog.GetType();
        
        densityProperty = fogType.GetProperty("density");
        heightProperty = fogType.GetProperty("height");
        baseHeightProperty = fogType.GetProperty("baselineHeight") ?? fogType.GetProperty("baseHeight");
        colorProperty = fogType.GetProperty("color") ?? fogType.GetProperty("albedo");
        sunLightScatteringProperty = fogType.GetProperty("sunLightScattering") ?? fogType.GetProperty("lightScattering");
        skyHazeProperty = fogType.GetProperty("skyHaze");
        
        reflectionInitialized = (densityProperty != null);
        
        if (logChanges)
        {
            Debug.Log($"[VolumetricFogController] Reflection init: density={densityProperty != null}, height={heightProperty != null}");
        }
    }
    
    void InitializeState()
    {
        currentPreset = neutralPreset;
        targetPreset = neutralPreset;
        currentPresetName = neutralPreset.stateName;
        ApplyPreset(currentPreset);
    }
    
    void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            
            // Apply initial state
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        else
        {
            Debug.LogWarning("[VolumetricFogController] WorldStateManager not found!");
        }
    }
    
    #endregion
    
    #region Event Handler
    
    void OnRingsChanged(int leftRings, int rightRings)
    {
        debugLeftRings = leftRings;
        debugRightRings = rightRings;
        
        targetPreset = GetPresetForRings(leftRings, rightRings);
        currentPresetName = targetPreset.stateName;
        
        if (currentPreset != targetPreset)
        {
            transitionProgress = 0f;
            isTransitioning = true;
            
            if (logChanges)
                Debug.Log($"[VolumetricFogController] {leftRings}L/{rightRings}R → {targetPreset.stateName}");
        }
    }
    
    #endregion
    
    #region State Resolution (Same logic as PostProcessController)
    
    FogPreset GetPresetForRings(int L, int R)
    {
        int diff = L - R;
        int absDiff = Mathf.Abs(diff);
        int minRings = Mathf.Min(L, R);
        
        // === ECLIPSE STATES (Both tails high, balanced) ===
        if (absDiff <= 1 && minRings >= 2)
        {
            if (L == 5 && R == 5) return eclipseFullPreset;
            if ((L == 5 && R == 4) || (L == 4 && R == 5)) return eclipse75Preset;
            if (L == 4 && R == 4) return eclipse60Preset;
            if ((L == 4 && R == 3) || (L == 3 && R == 4)) return eclipse50Preset;
            if (L == 3 && R == 3) return eclipse40Preset;
            if ((L == 3 && R == 2) || (L == 2 && R == 3)) return eclipse20Preset;
        }
        
        // === SUNSET (1L/0R OR diff=2, dark winning, both have rings) ===
        if (L == 1 && R == 0) return sunsetPreset;
        if (diff == 2 && L > 0 && R > 0) return sunsetPreset;
        
        // === SUNRISE (diff=-2, light winning, both have rings) ===
        if (diff == -2 && L > 0 && R > 0) return sunrisePreset;
        
        // === DARK ESCALATION (L >= 6, committed to dark) ===
        if (L >= 6 && diff > 0)
        {
            int stage = L - 5;
            switch (stage)
            {
                case 1: return darkStage1Preset;
                case 2: return darkStage2Preset;
                case 3: return darkStage3Preset;
                case 4: return darkStage4Preset;
                default: return darkStage5Preset;
            }
        }
        
        // === LIGHT ESCALATION (R >= 6, committed to light) ===
        if (R >= 6 && diff < 0)
        {
            int stage = R - 5;
            switch (stage)
            {
                case 1: return lightStage1Preset;
                case 2: return lightStage2Preset;
                case 3: return lightStage3Preset;
                case 4: return lightStage4Preset;
                default: return lightStage5Preset;
            }
        }
        
        // === DARK PATH (diff > 2, dark winning) ===
        if (diff > 2)
        {
            if (L >= 5) return dark5Preset;
            if (L >= 4) return dark4Preset;
            if (L >= 3) return dark3Preset;
            if (L >= 2) return dark2Preset;
            return dark1Preset;
        }
        
        // === LIGHT PATH (diff < -2, light winning) ===
        if (diff < -2)
        {
            if (R >= 5) return light5Preset;
            if (R >= 4) return light4Preset;
            if (R >= 3) return light3Preset;
            if (R >= 2) return light2Preset;
            return light1Preset;
        }
        
        // === MILD DARK (diff 1-2, dark winning, no eclipse) ===
        if (diff > 0 && diff <= 2 && minRings < 2)
        {
            return dark1Preset;
        }
        
        // === MILD LIGHT (diff 1-2, light winning, no eclipse) ===
        if (diff < 0 && absDiff <= 2 && minRings < 2)
        {
            return light1Preset;
        }
        
        // === NEUTRAL (default) ===
        return neutralPreset;
    }
    
    #endregion
    
    #region Apply Preset
    
    void ApplyPreset(FogPreset preset)
    {
        if (!reflectionInitialized || volumetricFog == null) return;
        
        try
        {
            densityProperty?.SetValue(volumetricFog, preset.density);
            heightProperty?.SetValue(volumetricFog, preset.height);
            baseHeightProperty?.SetValue(volumetricFog, preset.baseHeight);
            colorProperty?.SetValue(volumetricFog, preset.fogColor);
            
            if (sunLightScatteringProperty != null)
            {
                sunLightScatteringProperty.SetValue(volumetricFog, 
                    preset.enableGodRays ? preset.godRayIntensity : 0f);
            }
            
            skyHazeProperty?.SetValue(volumetricFog, preset.skyHaze);
        }
        catch (Exception e)
        {
            if (logChanges)
                Debug.LogWarning($"[VolumetricFogController] Apply error: {e.Message}");
        }
    }
    
    #endregion
    
    #region Context Menu Tests
    
    [ContextMenu("Test: Neutral")]
    void TestNeutral() => TestPreset(neutralPreset);
    
    [ContextMenu("Test: Sunset")]
    void TestSunset() => TestPreset(sunsetPreset);
    
    [ContextMenu("Test: Sunrise")]
    void TestSunrise() => TestPreset(sunrisePreset);
    
    [ContextMenu("Test: Dark5 (Midnight)")]
    void TestDark5() => TestPreset(dark5Preset);
    
    [ContextMenu("Test: Dark+Stage5 (Thunderstorm)")]
    void TestDarkStage5() => TestPreset(darkStage5Preset);
    
    [ContextMenu("Test: Light5 (Heavenly)")]
    void TestLight5() => TestPreset(light5Preset);
    
    [ContextMenu("Test: Light+Stage5 (Divine)")]
    void TestLightStage5() => TestPreset(lightStage5Preset);
    
    [ContextMenu("Test: Eclipse FULL")]
    void TestEclipseFull() => TestPreset(eclipseFullPreset);
    
    void TestPreset(FogPreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        isTransitioning = false;
        currentPresetName = preset.stateName;
        ApplyPreset(preset);
        if (logChanges) Debug.Log($"[VolumetricFogController] Testing: {preset.stateName}");
    }
    
    #endregion
    
    #region Public API
    
    public void ForceRefresh()
    {
        if (WorldStateManager.Instance != null)
        {
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    public FogPreset GetCurrentPreset() => currentPreset;
    public string GetCurrentPresetName() => currentPresetName;
    
    #endregion
}