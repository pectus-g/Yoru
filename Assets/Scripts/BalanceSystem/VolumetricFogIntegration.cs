using UnityEngine;
using System;

/// <summary>
/// YORU: Volumetric Fog Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Controls Kronnect's Volumetric Fog & Mist 2 for ALL 22 unique states.
/// 
/// Features:
/// - Fog density, height, and color control
/// - God rays / light scattering for light path
/// - Per-state tweakable presets in Inspector
/// - Smooth transitions between states
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
        density = 0.3f,
        height = 6f,
        fogColor = new Color(0.89f, 0.89f, 0.89f),
        enableGodRays = true,
        godRayIntensity = 0.03f,
        skyHaze = 15f
    };
    
    [Header("=== LIGHT PATH (Balance +1 to +5) ===")]
    [SerializeField] private FogPreset light1Preset = new FogPreset
    {
        stateName = "Light1",
        density = 0.25f,
        height = 8f,
        fogColor = new Color(0.95f, 0.92f, 0.85f),
        enableGodRays = true,
        godRayIntensity = 0.035f,
        skyHaze = 12f
    };
    
    [SerializeField] private FogPreset light2Preset = new FogPreset
    {
        stateName = "Light2",
        density = 0.22f,
        height = 10f,
        fogColor = new Color(0.97f, 0.94f, 0.86f),
        enableGodRays = true,
        godRayIntensity = 0.04f,
        skyHaze = 10f
    };
    
    [SerializeField] private FogPreset light3Preset = new FogPreset
    {
        stateName = "Light3",
        density = 0.2f,
        height = 12f,
        fogColor = new Color(0.99f, 0.96f, 0.87f),
        enableGodRays = true,
        godRayIntensity = 0.045f,
        skyHaze = 9f
    };
    
    [SerializeField] private FogPreset light4Preset = new FogPreset
    {
        stateName = "Light4",
        density = 0.18f,
        height = 16f,
        fogColor = new Color(1f, 0.97f, 0.88f),
        enableGodRays = true,
        godRayIntensity = 0.05f,
        skyHaze = 8f
    };
    
    [SerializeField] private FogPreset light5Preset = new FogPreset
    {
        stateName = "Light5 (Heavenly)",
        density = 0.15f,
        height = 20f,
        fogColor = new Color(1f, 0.98f, 0.9f),
        enableGodRays = true,
        godRayIntensity = 0.055f,
        skyHaze = 7f
    };
    
    [Header("=== LIGHT PATH ESCALATION (Stage 1-5, Divine God Rays) ===")]
    [SerializeField] private FogPreset lightStage1Preset = new FogPreset
    {
        stateName = "Light5+Stage1",
        density = 0.18f,
        height = 25f,
        fogColor = new Color(1f, 0.96f, 0.8f),
        enableGodRays = true,
        godRayIntensity = 0.07f,
        skyHaze = 10f
    };
    
    [SerializeField] private FogPreset lightStage2Preset = new FogPreset
    {
        stateName = "Light5+Stage2",
        density = 0.2f,
        height = 30f,
        fogColor = new Color(1f, 0.94f, 0.7f),
        enableGodRays = true,
        godRayIntensity = 0.08f,
        skyHaze = 12f
    };
    
    [SerializeField] private FogPreset lightStage3Preset = new FogPreset
    {
        stateName = "Light5+Stage3",
        density = 0.22f,
        height = 40f,
        fogColor = new Color(1f, 0.91f, 0.6f),
        enableGodRays = true,
        godRayIntensity = 0.09f,
        skyHaze = 14f
    };
    
    [SerializeField] private FogPreset lightStage4Preset = new FogPreset
    {
        stateName = "Light5+Stage4",
        density = 0.25f,
        height = 50f,
        fogColor = new Color(1f, 0.88f, 0.5f),
        enableGodRays = true,
        godRayIntensity = 0.1f,
        skyHaze = 16f
    };
    
    [SerializeField] private FogPreset lightStage5Preset = new FogPreset
    {
        stateName = "Light5+Stage5 (DIVINE)",
        density = 0.28f,
        height = 60f,
        fogColor = new Color(1f, 0.86f, 0.4f),
        enableGodRays = true,
        godRayIntensity = 0.12f,
        skyHaze = 18f
    };
    
    [Header("=== DARK PATH (Balance -1 to -5) ===")]
    [SerializeField] private FogPreset dark1Preset = new FogPreset
    {
        stateName = "Dark1",
        density = 0.35f,
        height = 5f,
        fogColor = new Color(0.75f, 0.78f, 0.85f),
        enableGodRays = true,
        godRayIntensity = 0.02f,
        skyHaze = 18f
    };
    
    [SerializeField] private FogPreset dark2Preset = new FogPreset
    {
        stateName = "Dark2",
        density = 0.4f,
        height = 4f,
        fogColor = new Color(0.65f, 0.69f, 0.78f),
        enableGodRays = true,
        godRayIntensity = 0.015f,
        skyHaze = 20f
    };
    
    [SerializeField] private FogPreset dark3Preset = new FogPreset
    {
        stateName = "Dark3",
        density = 0.45f,
        height = 4f,
        fogColor = new Color(0.55f, 0.6f, 0.7f),
        enableGodRays = true,
        godRayIntensity = 0.01f,
        skyHaze = 22f
    };
    
    [SerializeField] private FogPreset dark4Preset = new FogPreset
    {
        stateName = "Dark4",
        density = 0.5f,
        height = 3f,
        fogColor = new Color(0.45f, 0.51f, 0.62f),
        enableGodRays = true,
        godRayIntensity = 0.005f,
        skyHaze = 24f
    };
    
    [SerializeField] private FogPreset dark5Preset = new FogPreset
    {
        stateName = "Dark5 (Midnight)",
        density = 0.55f,
        height = 3f,
        fogColor = new Color(0.35f, 0.42f, 0.55f),
        enableGodRays = true,
        godRayIntensity = 0.003f,
        skyHaze = 26f
    };
    
    [Header("=== DARK PATH ESCALATION (Stage 1-5, Stormy) ===")]
    [SerializeField] private FogPreset darkStage1Preset = new FogPreset
    {
        stateName = "Dark5+Stage1 (Partly Cloudy)",
        density = 0.58f,
        height = 3f,
        fogColor = new Color(0.31f, 0.37f, 0.5f),
        enableGodRays = false,
        skyHaze = 28f
    };
    
    [SerializeField] private FogPreset darkStage2Preset = new FogPreset
    {
        stateName = "Dark5+Stage2 (Overcast)",
        density = 0.62f,
        height = 2f,
        fogColor = new Color(0.27f, 0.33f, 0.45f),
        enableGodRays = false,
        skyHaze = 30f
    };
    
    [SerializeField] private FogPreset darkStage3Preset = new FogPreset
    {
        stateName = "Dark5+Stage3 (Light Rain)",
        density = 0.68f,
        height = 2f,
        fogColor = new Color(0.23f, 0.28f, 0.4f),
        enableGodRays = false,
        skyHaze = 32f
    };
    
    [SerializeField] private FogPreset darkStage4Preset = new FogPreset
    {
        stateName = "Dark5+Stage4 (Heavy Rain)",
        density = 0.75f,
        height = 2f,
        fogColor = new Color(0.2f, 0.23f, 0.35f),
        enableGodRays = false,
        skyHaze = 35f
    };
    
    [SerializeField] private FogPreset darkStage5Preset = new FogPreset
    {
        stateName = "Dark5+Stage5 (THUNDERSTORM)",
        density = 0.82f,
        height = 1f,
        fogColor = new Color(0.16f, 0.18f, 0.3f),
        enableGodRays = false,
        skyHaze = 40f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private FogPreset eclipsePreset = new FogPreset
    {
        stateName = "Eclipse",
        density = 0.35f,
        height = 80f,
        fogColor = new Color(0.5f, 0.4f, 0.6f),
        enableGodRays = true,
        godRayIntensity = 0.04f,
        skyHaze = 20f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    #endregion
    
    #region Private Fields
    
    private FogPreset currentPreset;
    private FogPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    private WorldStateManager.AtmosphereState currentAtmosphere;
    private int currentWeatherStage;
    
    // Cached reflection info for Kronnect fog
    private System.Type fogType;
    private System.Reflection.PropertyInfo densityProperty;
    private System.Reflection.PropertyInfo heightProperty;
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
            WorldStateManager.Instance.OnStateChanged.RemoveListener(OnAtmosphereChanged);
            WorldStateManager.Instance.OnWeatherStageChanged.RemoveListener(OnWeatherStageChanged);
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
            // Try to find Kronnect's VolumetricFog component
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
        
        // Get properties via reflection (for Kronnect Volumetric Fog & Mist 2)
        densityProperty = fogType.GetProperty("density");
        heightProperty = fogType.GetProperty("height");
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
        ApplyPreset(currentPreset);
    }
    
    void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.AddListener(OnAtmosphereChanged);
            WorldStateManager.Instance.OnWeatherStageChanged.AddListener(OnWeatherStageChanged);
            OnAtmosphereChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnAtmosphereChanged(WorldStateManager.AtmosphereState state)
    {
        currentAtmosphere = state;
        UpdateTargetPreset();
    }
    
    void OnWeatherStageChanged(int stage)
    {
        currentWeatherStage = stage;
        UpdateTargetPreset();
    }
    
    void UpdateTargetPreset()
    {
        targetPreset = GetPresetForState(currentAtmosphere, currentWeatherStage);
        transitionProgress = 0f;
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[VolumetricFogController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    FogPreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
    {
        if (atmosphere == WorldStateManager.AtmosphereState.Eclipse)
            return eclipsePreset;
        
        if (weatherStage > 0)
        {
            if (atmosphere == WorldStateManager.AtmosphereState.Dark5)
            {
                switch (weatherStage)
                {
                    case 1: return darkStage1Preset;
                    case 2: return darkStage2Preset;
                    case 3: return darkStage3Preset;
                    case 4: return darkStage4Preset;
                    default: return darkStage5Preset;
                }
            }
            else if (atmosphere == WorldStateManager.AtmosphereState.Light5)
            {
                switch (weatherStage)
                {
                    case 1: return lightStage1Preset;
                    case 2: return lightStage2Preset;
                    case 3: return lightStage3Preset;
                    case 4: return lightStage4Preset;
                    default: return lightStage5Preset;
                }
            }
        }
        
        switch (atmosphere)
        {
            case WorldStateManager.AtmosphereState.Dark5: return dark5Preset;
            case WorldStateManager.AtmosphereState.Dark4: return dark4Preset;
            case WorldStateManager.AtmosphereState.Dark3: return dark3Preset;
            case WorldStateManager.AtmosphereState.Dark2: return dark2Preset;
            case WorldStateManager.AtmosphereState.Dark1: return dark1Preset;
            case WorldStateManager.AtmosphereState.Light1: return light1Preset;
            case WorldStateManager.AtmosphereState.Light2: return light2Preset;
            case WorldStateManager.AtmosphereState.Light3: return light3Preset;
            case WorldStateManager.AtmosphereState.Light4: return light4Preset;
            case WorldStateManager.AtmosphereState.Light5: return light5Preset;
            default: return neutralPreset;
        }
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
    
    #region Context Menu
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() { currentPreset = targetPreset = neutralPreset; ApplyPreset(neutralPreset); }
    
    [ContextMenu("Preview: Light5+Stage5")]
    void PreviewLightMax() { currentPreset = targetPreset = lightStage5Preset; ApplyPreset(lightStage5Preset); }
    
    [ContextMenu("Preview: Dark5+Stage5")]
    void PreviewDarkMax() { currentPreset = targetPreset = darkStage5Preset; ApplyPreset(darkStage5Preset); }
    
    [ContextMenu("Preview: Eclipse")]
    void PreviewEclipse() { currentPreset = targetPreset = eclipsePreset; ApplyPreset(eclipsePreset); }
    
    #endregion
}