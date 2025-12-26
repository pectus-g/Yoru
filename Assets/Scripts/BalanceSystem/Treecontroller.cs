using UnityEngine;
using System;

/// <summary>
/// YORU: Tree Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Controls tree appearance and movement for ALL 22 unique states.
/// Similar structure to FoliageController but with tree-specific settings.
/// 
/// Features:
/// - Wind intensity and speed control
/// - Leaf color tinting
/// - Trunk sway amount
/// - Per-state tweakable presets in Inspector
/// </summary>
public class TreeController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class TreePreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Wind Settings")]
        [Range(0, 2)] public float windIntensity = 0.5f;
        [Range(0.1f, 3f)] public float windSpeed = 1f;
        [Range(0, 1)] public float turbulence = 0.3f;
        
        [Header("Trunk Movement")]
        [Range(0, 2)] public float trunkSwayAmount = 1f;
        [Range(0, 2)] public float branchSwayAmount = 1f;
        
        [Header("Leaf Color")]
        public Color leafTint = Color.white;
        [Range(0, 2)] public float leafSaturation = 1f;
        
        [Header("Bark Color")]
        public Color barkTint = Color.white;
        
        public static TreePreset Lerp(TreePreset a, TreePreset b, float t)
        {
            return new TreePreset
            {
                stateName = b.stateName,
                windIntensity = Mathf.Lerp(a.windIntensity, b.windIntensity, t),
                windSpeed = Mathf.Lerp(a.windSpeed, b.windSpeed, t),
                turbulence = Mathf.Lerp(a.turbulence, b.turbulence, t),
                trunkSwayAmount = Mathf.Lerp(a.trunkSwayAmount, b.trunkSwayAmount, t),
                branchSwayAmount = Mathf.Lerp(a.branchSwayAmount, b.branchSwayAmount, t),
                leafTint = Color.Lerp(a.leafTint, b.leafTint, t),
                leafSaturation = Mathf.Lerp(a.leafSaturation, b.leafSaturation, t),
                barkTint = Color.Lerp(a.barkTint, b.barkTint, t)
            };
        }
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private WindZone windZone;
    [SerializeField] private bool autoFindWindZone = true;
    
    [Header("=== SHADER PROPERTIES ===")]
    [Tooltip("Global shader property for tree leaf tint")]
    [SerializeField] private string leafTintProperty = "_TreeLeafTint";
    [SerializeField] private string leafSaturationProperty = "_TreeLeafSaturation";
    [SerializeField] private string barkTintProperty = "_TreeBarkTint";
    [SerializeField] private string windIntensityProperty = "_TreeWindIntensity";
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private TreePreset neutralPreset = new TreePreset
    {
        stateName = "Neutral",
        windIntensity = 0.4f,
        windSpeed = 1f,
        turbulence = 0.2f,
        trunkSwayAmount = 0.8f,
        branchSwayAmount = 1f,
        leafTint = Color.white,
        leafSaturation = 1f,
        barkTint = Color.white
    };
    
    [Header("=== LIGHT PATH (Balance +1 to +5) ===")]
    [SerializeField] private TreePreset light1Preset = new TreePreset
    {
        stateName = "Light1 (Golden Hour)",
        windIntensity = 0.35f,
        windSpeed = 0.9f,
        leafTint = new Color(1f, 1f, 0.95f),
        leafSaturation = 1.1f
    };
    
    [SerializeField] private TreePreset light2Preset = new TreePreset
    {
        stateName = "Light2 (Warm)",
        windIntensity = 0.3f,
        windSpeed = 0.85f,
        leafTint = new Color(1f, 1f, 0.9f),
        leafSaturation = 1.15f
    };
    
    [SerializeField] private TreePreset light3Preset = new TreePreset
    {
        stateName = "Light3 (Bright)",
        windIntensity = 0.25f,
        windSpeed = 0.8f,
        leafTint = new Color(0.95f, 1f, 0.85f),
        leafSaturation = 1.2f
    };
    
    [SerializeField] private TreePreset light4Preset = new TreePreset
    {
        stateName = "Light4 (Brighter)",
        windIntensity = 0.2f,
        windSpeed = 0.75f,
        leafTint = new Color(0.9f, 1f, 0.8f),
        leafSaturation = 1.25f
    };
    
    [SerializeField] private TreePreset light5Preset = new TreePreset
    {
        stateName = "Light5 (Heavenly)",
        windIntensity = 0.15f,
        windSpeed = 0.7f,
        leafTint = new Color(0.85f, 1f, 0.75f),
        leafSaturation = 1.3f
    };
    
    [Header("=== LIGHT PATH ESCALATION (Stage 1-5, Divine) ===")]
    [SerializeField] private TreePreset lightStage1Preset = new TreePreset
    {
        stateName = "Light5+Stage1",
        windIntensity = 0.12f,
        windSpeed = 0.65f,
        leafTint = new Color(0.82f, 1f, 0.72f),
        leafSaturation = 1.32f
    };
    
    [SerializeField] private TreePreset lightStage2Preset = new TreePreset
    {
        stateName = "Light5+Stage2",
        windIntensity = 0.1f,
        windSpeed = 0.6f,
        leafTint = new Color(0.8f, 1f, 0.7f),
        leafSaturation = 1.35f
    };
    
    [SerializeField] private TreePreset lightStage3Preset = new TreePreset
    {
        stateName = "Light5+Stage3",
        windIntensity = 0.08f,
        windSpeed = 0.55f,
        leafTint = new Color(0.78f, 1f, 0.68f),
        leafSaturation = 1.38f
    };
    
    [SerializeField] private TreePreset lightStage4Preset = new TreePreset
    {
        stateName = "Light5+Stage4",
        windIntensity = 0.06f,
        windSpeed = 0.5f,
        leafTint = new Color(0.75f, 1f, 0.65f),
        leafSaturation = 1.4f
    };
    
    [SerializeField] private TreePreset lightStage5Preset = new TreePreset
    {
        stateName = "Light5+Stage5 (DIVINE)",
        windIntensity = 0.05f,
        windSpeed = 0.45f,
        leafTint = new Color(0.72f, 1f, 0.62f),
        leafSaturation = 1.45f
    };
    
    [Header("=== DARK PATH (Balance -1 to -5) ===")]
    [SerializeField] private TreePreset dark1Preset = new TreePreset
    {
        stateName = "Dark1 (Late Afternoon)",
        windIntensity = 0.45f,
        windSpeed = 1.05f,
        leafTint = new Color(0.95f, 0.92f, 0.85f),
        leafSaturation = 0.95f
    };
    
    [SerializeField] private TreePreset dark2Preset = new TreePreset
    {
        stateName = "Dark2 (Sunset)",
        windIntensity = 0.5f,
        windSpeed = 1.1f,
        leafTint = new Color(0.9f, 0.85f, 0.75f),
        leafSaturation = 0.9f
    };
    
    [SerializeField] private TreePreset dark3Preset = new TreePreset
    {
        stateName = "Dark3 (Dusk)",
        windIntensity = 0.55f,
        windSpeed = 1.15f,
        leafTint = new Color(0.85f, 0.8f, 0.7f),
        leafSaturation = 0.85f
    };
    
    [SerializeField] private TreePreset dark4Preset = new TreePreset
    {
        stateName = "Dark4 (Night)",
        windIntensity = 0.6f,
        windSpeed = 1.2f,
        leafTint = new Color(0.75f, 0.72f, 0.65f),
        leafSaturation = 0.75f
    };
    
    [SerializeField] private TreePreset dark5Preset = new TreePreset
    {
        stateName = "Dark5 (Midnight)",
        windIntensity = 0.65f,
        windSpeed = 1.25f,
        leafTint = new Color(0.65f, 0.62f, 0.55f),
        leafSaturation = 0.65f
    };
    
    [Header("=== DARK PATH ESCALATION (Stage 1-5, Stormy) ===")]
    [SerializeField] private TreePreset darkStage1Preset = new TreePreset
    {
        stateName = "Dark5+Stage1 (Partly Cloudy)",
        windIntensity = 0.75f,
        windSpeed = 1.35f,
        turbulence = 0.35f,
        leafTint = new Color(0.6f, 0.58f, 0.52f),
        leafSaturation = 0.6f
    };
    
    [SerializeField] private TreePreset darkStage2Preset = new TreePreset
    {
        stateName = "Dark5+Stage2 (Overcast)",
        windIntensity = 0.85f,
        windSpeed = 1.45f,
        turbulence = 0.4f,
        leafTint = new Color(0.55f, 0.53f, 0.48f),
        leafSaturation = 0.55f
    };
    
    [SerializeField] private TreePreset darkStage3Preset = new TreePreset
    {
        stateName = "Dark5+Stage3 (Light Rain)",
        windIntensity = 0.95f,
        windSpeed = 1.55f,
        turbulence = 0.5f,
        leafTint = new Color(0.5f, 0.48f, 0.45f),
        leafSaturation = 0.52f
    };
    
    [SerializeField] private TreePreset darkStage4Preset = new TreePreset
    {
        stateName = "Dark5+Stage4 (Heavy Rain)",
        windIntensity = 1.1f,
        windSpeed = 1.7f,
        turbulence = 0.6f,
        leafTint = new Color(0.45f, 0.43f, 0.4f),
        leafSaturation = 0.5f
    };
    
    [SerializeField] private TreePreset darkStage5Preset = new TreePreset
    {
        stateName = "Dark5+Stage5 (THUNDERSTORM)",
        windIntensity = 1.3f,
        windSpeed = 1.9f,
        turbulence = 0.75f,
        trunkSwayAmount = 1.5f,
        branchSwayAmount = 1.8f,
        leafTint = new Color(0.4f, 0.38f, 0.35f),
        leafSaturation = 0.45f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private TreePreset eclipsePreset = new TreePreset
    {
        stateName = "Eclipse",
        windIntensity = 0.7f,
        windSpeed = 1.3f,
        turbulence = 0.4f,
        leafTint = new Color(0.7f, 0.6f, 0.8f), // Purple tint
        leafSaturation = 0.8f,
        barkTint = new Color(0.85f, 0.8f, 0.9f)
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    #endregion
    
    #region Private Fields
    
    private TreePreset currentPreset;
    private TreePreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    private WorldStateManager.AtmosphereState currentAtmosphere;
    private int currentWeatherStage;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        FindReferences();
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
            
            var lerped = TreePreset.Lerp(currentPreset, targetPreset, transitionProgress);
            ApplyPreset(lerped);
        }
    }
    
    #endregion
    
    #region Setup
    
    void FindReferences()
    {
        if (windZone == null && autoFindWindZone)
        {
            windZone = FindObjectOfType<WindZone>();
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
            Debug.Log($"[TreeController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    TreePreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
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
    
    void ApplyPreset(TreePreset preset)
    {
        // Wind Zone
        if (windZone != null)
        {
            windZone.windMain = preset.windIntensity;
            windZone.windTurbulence = preset.turbulence;
        }
        
        // Global shader properties
        Shader.SetGlobalColor(leafTintProperty, preset.leafTint);
        Shader.SetGlobalFloat(leafSaturationProperty, preset.leafSaturation);
        Shader.SetGlobalColor(barkTintProperty, preset.barkTint);
        Shader.SetGlobalFloat(windIntensityProperty, preset.windIntensity);
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() { targetPreset = neutralPreset; currentPreset = neutralPreset; ApplyPreset(neutralPreset); }
    
    [ContextMenu("Preview: Light5+Stage5")]
    void PreviewLightMax() { targetPreset = lightStage5Preset; currentPreset = lightStage5Preset; ApplyPreset(lightStage5Preset); }
    
    [ContextMenu("Preview: Dark5+Stage5")]
    void PreviewDarkMax() { targetPreset = darkStage5Preset; currentPreset = darkStage5Preset; ApplyPreset(darkStage5Preset); }
    
    [ContextMenu("Preview: Eclipse")]
    void PreviewEclipse() { targetPreset = eclipsePreset; currentPreset = eclipsePreset; ApplyPreset(eclipsePreset); }
    
    #endregion
}