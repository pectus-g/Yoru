using UnityEngine;
using System;

/// <summary>
/// YORU: Foliage Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Controls vegetation appearance based on ALL 22 karma states.
/// 
/// Features:
/// - Wind intensity: Calm on light, strong on dark escalation
/// - Color tinting: Vibrant green → brown/dead
/// - Sway speed: Peaceful → frantic
/// - Per-state tweakable presets
/// 
/// Works with: Global shader properties, WindZone, Terrain
/// </summary>
public class FoliageController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class FoliagePreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Wind Settings")]
        [Range(0, 3)] public float windIntensity = 0.5f;
        [Range(0, 3)] public float swaySpeed = 1f;
        [Range(0, 2)] public float turbulence = 0.3f;
        
        [Header("Color Settings")]
        public Color foliageTint = new Color(0.45f, 0.6f, 0.35f);
        public Color grassTint = new Color(0.5f, 0.6f, 0.3f);
        [Range(0, 2)] public float saturation = 1f;
        [Range(0, 2)] public float brightness = 1f;
        
        public static FoliagePreset Lerp(FoliagePreset a, FoliagePreset b, float t)
        {
            return new FoliagePreset
            {
                stateName = b.stateName,
                windIntensity = Mathf.Lerp(a.windIntensity, b.windIntensity, t),
                swaySpeed = Mathf.Lerp(a.swaySpeed, b.swaySpeed, t),
                turbulence = Mathf.Lerp(a.turbulence, b.turbulence, t),
                foliageTint = Color.Lerp(a.foliageTint, b.foliageTint, t),
                grassTint = Color.Lerp(a.grassTint, b.grassTint, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                brightness = Mathf.Lerp(a.brightness, b.brightness, t)
            };
        }
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private WindZone windZone;
    [SerializeField] private bool controlWindZone = true;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.1f, 3f)] private float transitionSpeed = 0.5f;
    
    [Header("=== SHADER PROPERTIES ===")]
    [SerializeField] private string globalFoliageTintProperty = "_GlobalFoliageTint";
    [SerializeField] private string globalGrassTintProperty = "_GlobalGrassTint";
    [SerializeField] private string globalWindIntensityProperty = "_GlobalWindIntensity";
    [SerializeField] private string globalSwaySpeedProperty = "_GlobalSwaySpeed";
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private FoliagePreset neutralPreset = new FoliagePreset
    {
        stateName = "Neutral",
        windIntensity = 0.5f, swaySpeed = 1f, turbulence = 0.3f,
        foliageTint = new Color(0.45f, 0.6f, 0.35f),
        grassTint = new Color(0.5f, 0.6f, 0.3f),
        saturation = 1f, brightness = 1f
    };
    
    [Header("=== LIGHT PATH (Vibrant, Calm) ===")]
    [SerializeField] private FoliagePreset light1Preset = new FoliagePreset
    {
        stateName = "Light1 (Golden Hour)",
        windIntensity = 0.45f, swaySpeed = 0.9f, turbulence = 0.25f,
        foliageTint = new Color(0.48f, 0.65f, 0.38f),
        grassTint = new Color(0.55f, 0.65f, 0.32f),
        saturation = 1.05f, brightness = 1.05f
    };
    
    [SerializeField] private FoliagePreset light2Preset = new FoliagePreset
    {
        stateName = "Light2 (Warm)",
        windIntensity = 0.4f, swaySpeed = 0.85f, turbulence = 0.22f,
        foliageTint = new Color(0.5f, 0.7f, 0.4f),
        grassTint = new Color(0.58f, 0.7f, 0.35f),
        saturation = 1.1f, brightness = 1.08f
    };
    
    [SerializeField] private FoliagePreset light3Preset = new FoliagePreset
    {
        stateName = "Light3 (Bright)",
        windIntensity = 0.35f, swaySpeed = 0.8f, turbulence = 0.2f,
        foliageTint = new Color(0.52f, 0.75f, 0.42f),
        grassTint = new Color(0.6f, 0.75f, 0.38f),
        saturation = 1.15f, brightness = 1.1f
    };
    
    [SerializeField] private FoliagePreset light4Preset = new FoliagePreset
    {
        stateName = "Light4 (Brighter)",
        windIntensity = 0.3f, swaySpeed = 0.75f, turbulence = 0.18f,
        foliageTint = new Color(0.55f, 0.8f, 0.45f),
        grassTint = new Color(0.62f, 0.8f, 0.4f),
        saturation = 1.2f, brightness = 1.12f
    };
    
    [SerializeField] private FoliagePreset light5Preset = new FoliagePreset
    {
        stateName = "Light5 (Heavenly)",
        windIntensity = 0.25f, swaySpeed = 0.7f, turbulence = 0.15f,
        foliageTint = new Color(0.58f, 0.85f, 0.48f),
        grassTint = new Color(0.65f, 0.85f, 0.42f),
        saturation = 1.25f, brightness = 1.15f
    };
    
    [Header("=== LIGHT ESCALATION (Divine Calm) ===")]
    [SerializeField] private FoliagePreset lightStage1Preset = new FoliagePreset
    {
        stateName = "Light5+Stage1",
        windIntensity = 0.22f, swaySpeed = 0.65f, turbulence = 0.12f,
        foliageTint = new Color(0.6f, 0.87f, 0.5f),
        grassTint = new Color(0.68f, 0.87f, 0.45f),
        saturation = 1.28f, brightness = 1.18f
    };
    
    [SerializeField] private FoliagePreset lightStage2Preset = new FoliagePreset
    {
        stateName = "Light5+Stage2",
        windIntensity = 0.2f, swaySpeed = 0.6f, turbulence = 0.1f,
        foliageTint = new Color(0.62f, 0.9f, 0.52f),
        grassTint = new Color(0.7f, 0.9f, 0.48f),
        saturation = 1.3f, brightness = 1.2f
    };
    
    [SerializeField] private FoliagePreset lightStage3Preset = new FoliagePreset
    {
        stateName = "Light5+Stage3",
        windIntensity = 0.18f, swaySpeed = 0.55f, turbulence = 0.08f,
        foliageTint = new Color(0.65f, 0.92f, 0.55f),
        grassTint = new Color(0.72f, 0.92f, 0.5f),
        saturation = 1.32f, brightness = 1.22f
    };
    
    [SerializeField] private FoliagePreset lightStage4Preset = new FoliagePreset
    {
        stateName = "Light5+Stage4",
        windIntensity = 0.15f, swaySpeed = 0.5f, turbulence = 0.06f,
        foliageTint = new Color(0.68f, 0.95f, 0.58f),
        grassTint = new Color(0.75f, 0.95f, 0.52f),
        saturation = 1.35f, brightness = 1.25f
    };
    
    [SerializeField] private FoliagePreset lightStage5Preset = new FoliagePreset
    {
        stateName = "Light5+Stage5 (Maximum Divine)",
        windIntensity = 0.1f, swaySpeed = 0.45f, turbulence = 0.05f,
        foliageTint = new Color(0.7f, 1f, 0.6f),
        grassTint = new Color(0.78f, 1f, 0.55f),
        saturation = 1.4f, brightness = 1.3f
    };
    
    [Header("=== DARK PATH (Dying, Windy) ===")]
    [SerializeField] private FoliagePreset dark1Preset = new FoliagePreset
    {
        stateName = "Dark1 (Late Afternoon)",
        windIntensity = 0.55f, swaySpeed = 1.05f, turbulence = 0.35f,
        foliageTint = new Color(0.42f, 0.55f, 0.35f),
        grassTint = new Color(0.48f, 0.55f, 0.3f),
        saturation = 0.95f, brightness = 0.95f
    };
    
    [SerializeField] private FoliagePreset dark2Preset = new FoliagePreset
    {
        stateName = "Dark2 (Sunset)",
        windIntensity = 0.6f, swaySpeed = 1.1f, turbulence = 0.4f,
        foliageTint = new Color(0.4f, 0.5f, 0.35f),
        grassTint = new Color(0.45f, 0.48f, 0.3f),
        saturation = 0.9f, brightness = 0.9f
    };
    
    [SerializeField] private FoliagePreset dark3Preset = new FoliagePreset
    {
        stateName = "Dark3 (Dusk)",
        windIntensity = 0.65f, swaySpeed = 1.15f, turbulence = 0.45f,
        foliageTint = new Color(0.38f, 0.45f, 0.38f),
        grassTint = new Color(0.42f, 0.42f, 0.32f),
        saturation = 0.85f, brightness = 0.85f
    };
    
    [SerializeField] private FoliagePreset dark4Preset = new FoliagePreset
    {
        stateName = "Dark4 (Night)",
        windIntensity = 0.7f, swaySpeed = 1.2f, turbulence = 0.5f,
        foliageTint = new Color(0.35f, 0.4f, 0.4f),
        grassTint = new Color(0.38f, 0.36f, 0.32f),
        saturation = 0.8f, brightness = 0.8f
    };
    
    [SerializeField] private FoliagePreset dark5Preset = new FoliagePreset
    {
        stateName = "Dark5 (Midnight)",
        windIntensity = 0.75f, swaySpeed = 1.25f, turbulence = 0.55f,
        foliageTint = new Color(0.32f, 0.35f, 0.42f),
        grassTint = new Color(0.35f, 0.3f, 0.28f),
        saturation = 0.75f, brightness = 0.75f
    };
    
    [Header("=== DARK ESCALATION (Stormy, Chaotic) ===")]
    [SerializeField] private FoliagePreset darkStage1Preset = new FoliagePreset
    {
        stateName = "Dark5+Stage1 (Cloudy)",
        windIntensity = 0.85f, swaySpeed = 1.35f, turbulence = 0.6f,
        foliageTint = new Color(0.3f, 0.32f, 0.4f),
        grassTint = new Color(0.32f, 0.28f, 0.26f),
        saturation = 0.7f, brightness = 0.72f
    };
    
    [SerializeField] private FoliagePreset darkStage2Preset = new FoliagePreset
    {
        stateName = "Dark5+Stage2 (Overcast)",
        windIntensity = 0.95f, swaySpeed = 1.45f, turbulence = 0.7f,
        foliageTint = new Color(0.28f, 0.3f, 0.38f),
        grassTint = new Color(0.3f, 0.26f, 0.24f),
        saturation = 0.65f, brightness = 0.68f
    };
    
    [SerializeField] private FoliagePreset darkStage3Preset = new FoliagePreset
    {
        stateName = "Dark5+Stage3 (Light Rain)",
        windIntensity = 1.1f, swaySpeed = 1.55f, turbulence = 0.8f,
        foliageTint = new Color(0.26f, 0.28f, 0.35f),
        grassTint = new Color(0.28f, 0.24f, 0.22f),
        saturation = 0.6f, brightness = 0.65f
    };
    
    [SerializeField] private FoliagePreset darkStage4Preset = new FoliagePreset
    {
        stateName = "Dark5+Stage4 (Heavy Rain)",
        windIntensity = 1.3f, swaySpeed = 1.7f, turbulence = 0.9f,
        foliageTint = new Color(0.24f, 0.26f, 0.32f),
        grassTint = new Color(0.26f, 0.22f, 0.2f),
        saturation = 0.55f, brightness = 0.6f
    };
    
    [SerializeField] private FoliagePreset darkStage5Preset = new FoliagePreset
    {
        stateName = "Dark5+Stage5 (STORM)",
        windIntensity = 1.5f, swaySpeed = 1.9f, turbulence = 1f,
        foliageTint = new Color(0.22f, 0.24f, 0.28f),
        grassTint = new Color(0.24f, 0.2f, 0.18f),
        saturation = 0.5f, brightness = 0.55f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private FoliagePreset eclipsePreset = new FoliagePreset
    {
        stateName = "Eclipse",
        windIntensity = 0.3f, swaySpeed = 0.4f, turbulence = 0.2f,
        foliageTint = new Color(0.4f, 0.35f, 0.5f),
        grassTint = new Color(0.42f, 0.38f, 0.48f),
        saturation = 0.85f, brightness = 0.85f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    #endregion
    
    #region Private Fields
    
    private FoliagePreset currentPreset;
    private FoliagePreset targetPreset;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
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
        if (!isTransitioning) return;
        
        float t = transitionSpeed * Time.deltaTime;
        currentPreset = FoliagePreset.Lerp(currentPreset, targetPreset, t);
        ApplyPreset(currentPreset);
        
        if (Mathf.Abs(currentPreset.windIntensity - targetPreset.windIntensity) < 0.01f)
        {
            currentPreset = targetPreset;
            ApplyPreset(currentPreset);
            isTransitioning = false;
        }
    }
    
    #endregion
    
    #region Setup
    
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
    
    private WorldStateManager.AtmosphereState currentAtmosphere;
    private int currentWeatherStage;
    
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
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[FoliageController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    FoliagePreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
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
    
    void ApplyPreset(FoliagePreset preset)
    {
        // Global shader properties
        Shader.SetGlobalColor(globalFoliageTintProperty, preset.foliageTint);
        Shader.SetGlobalColor(globalGrassTintProperty, preset.grassTint);
        Shader.SetGlobalFloat(globalWindIntensityProperty, preset.windIntensity);
        Shader.SetGlobalFloat(globalSwaySpeedProperty, preset.swaySpeed);
        
        // WindZone control
        if (controlWindZone && windZone != null)
        {
            windZone.windMain = preset.windIntensity * 2f;
            windZone.windTurbulence = preset.turbulence;
            windZone.windPulseMagnitude = preset.windIntensity * 0.5f;
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() => PreviewPreset(neutralPreset);
    
    [ContextMenu("Preview: Light5+Stage5")]
    void PreviewLightMax() => PreviewPreset(lightStage5Preset);
    
    [ContextMenu("Preview: Dark5+Stage5")]
    void PreviewDarkMax() => PreviewPreset(darkStage5Preset);
    
    void PreviewPreset(FoliagePreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        isTransitioning = false;
        ApplyPreset(preset);
        if (logChanges) Debug.Log($"[FoliageController] Preview: {preset.stateName}");
    }
    
    #endregion
}