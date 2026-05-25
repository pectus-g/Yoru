using UnityEngine;
using UnityEngine.Serialization;
using System;

/// <summary>
/// YORU: Volumetric Fog Controller - 29-STATE RING-BASED SYSTEM
/// 
/// Controls Kronnect's Volumetric Fog & Mist 2 based on ring values.
/// Listens to OnRingsChanged for precise state mapping.
/// 
/// TERRAIN HEIGHT: Player spawns around Y=100, fog must cover that altitude!
/// - baselineHeight: Where fog STARTS (bottom of fog layer)
/// - height: How TALL the fog layer is (extends upward from baseline)
/// - Fog exists from baselineHeight to (baselineHeight + height)
/// 
/// For player at Y=100: baselineHeight=0, height=200 means fog covers Y=0 to Y=200
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
        
        [Header("Fog Geometry")]
        [Tooltip("Fog density - how thick/opaque")]
        [Range(0, 1)] public float density = 0.3f;
        
        [Tooltip("Height of fog layer (extends upward from baselineHeight)")]
        [Range(0, 500)] public float height = 150f;
        
        [Tooltip("Where fog layer starts (Y coordinate)")]
        [Range(-100, 200)] public float baselineHeight = 0f;
        
        [Header("Fog Appearance")]
        public Color fogColor = Color.gray;
        
        [Tooltip("Alpha/opacity of fog")]
        [Range(0, 1)] public float alpha = 1f;
        
        [Header("Noise")]
        [Range(0, 1)] public float noiseStrength = 0.5f;
        
        [Header("Sky Haze")]
        [Tooltip("Haze at horizon/sky")]
        [Range(0, 100)] public float skyHaze = 15f;
        
        [Tooltip("Sky haze opacity")]
        [Range(0, 1)] public float skyAlpha = 0.8f;
        
        [Header("Light Scattering (God Rays)")]
        public bool enableLightScattering = true;
        
        [Range(0, 1)] public float lightScatteringDiffusion = 0.5f;
        
        [Tooltip("Shafts Intensity - controls god ray brightness")]
        [Range(0, 0.2f)] public float lightScatteringExposure = 0.03f;
        
        public static FogPreset Lerp(FogPreset a, FogPreset b, float t)
        {
            return new FogPreset
            {
                stateName = b.stateName,
                density = Mathf.Lerp(a.density, b.density, t),
                height = Mathf.Lerp(a.height, b.height, t),
                baselineHeight = Mathf.Lerp(a.baselineHeight, b.baselineHeight, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                alpha = Mathf.Lerp(a.alpha, b.alpha, t),
                noiseStrength = Mathf.Lerp(a.noiseStrength, b.noiseStrength, t),
                skyHaze = Mathf.Lerp(a.skyHaze, b.skyHaze, t),
                skyAlpha = Mathf.Lerp(a.skyAlpha, b.skyAlpha, t),
                enableLightScattering = t > 0.5f ? b.enableLightScattering : a.enableLightScattering,
                lightScatteringDiffusion = Mathf.Lerp(a.lightScatteringDiffusion, b.lightScatteringDiffusion, t),
                lightScatteringExposure = Mathf.Lerp(a.lightScatteringExposure, b.lightScatteringExposure, t)
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
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private string currentPresetName = "None";
    [SerializeField] private int debugLeftRings;
    [SerializeField] private int debugRightRings;
    
    // =====================================================
    // PRESETS - All heights configured for player at Y=100
    // Fog range: baselineHeight to (baselineHeight + height)
    // =====================================================
    
    [Header("=== NEUTRAL (0L/0R) ===")]
    [SerializeField] private FogPreset neutralPreset = new FogPreset
    {
        stateName = "Neutral",
        triggerCondition = "0L/0R, 1L/1R",
        density = 0.15f,
        height = 150f,
        baselineHeight = 0f,
        fogColor = new Color(0.85f, 0.88f, 0.92f),
        alpha = 1f,
        noiseStrength = 0.4f,
        skyHaze = 20f,
        skyAlpha = 0.6f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.5f,
        lightScatteringExposure = 0.05f
    };
    
    [Header("=== SUNSET ===")]
    [SerializeField] private FogPreset sunsetPreset = new FogPreset
    {
        stateName = "Sunset",
        triggerCondition = "1L/0R, 3L/1R, 4L/2R (max <= 4)",
        density = 0.20f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.70f, 0.50f),
        alpha = 1f,
        noiseStrength = 0.45f,
        skyHaze = 35f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.6f,
        lightScatteringExposure = 0.12f
    };
    
    [Header("=== SUNRISE ===")]
    [SerializeField] private FogPreset sunrisePreset = new FogPreset
    {
        stateName = "Sunrise",
        triggerCondition = "0L/1R, 1L/3R, 2L/4R (max <= 4)",
        density = 0.18f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.80f, 0.70f),
        alpha = 1f,
        noiseStrength = 0.4f,
        skyHaze = 30f,
        skyAlpha = 0.65f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.55f,
        lightScatteringExposure = 0.10f
    };
    
    [Header("=== DARK PATH (Increasingly ominous) ===")]
    [SerializeField] private FogPreset dark1Preset = new FogPreset
    {
        stateName = "Dark1",
        triggerCondition = "diff = 1, dark winning",
        density = 0.22f,
        height = 160f,
        baselineHeight = 0f,
        fogColor = new Color(0.65f, 0.65f, 0.70f),
        alpha = 1f,
        noiseStrength = 0.5f,
        skyHaze = 25f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.45f,
        lightScatteringExposure = 0.04f
    };
    
    [SerializeField] private FogPreset dark2Preset = new FogPreset
    {
        stateName = "Dark2",
        triggerCondition = "diff = 2, dark winning",
        density = 0.28f,
        height = 170f,
        baselineHeight = 0f,
        fogColor = new Color(0.55f, 0.55f, 0.62f),
        alpha = 1f,
        noiseStrength = 0.55f,
        skyHaze = 30f,
        skyAlpha = 0.75f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.4f,
        lightScatteringExposure = 0.03f
    };
    
    [SerializeField] private FogPreset dark3Preset = new FogPreset
    {
        stateName = "Dark3",
        triggerCondition = "diff = 3, dark winning",
        density = 0.35f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.45f, 0.45f, 0.52f),
        alpha = 1f,
        noiseStrength = 0.6f,
        skyHaze = 40f,
        skyAlpha = 0.8f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.3f,
        lightScatteringExposure = 0.02f
    };
    
    [SerializeField] private FogPreset dark4Preset = new FogPreset
    {
        stateName = "Dark4",
        triggerCondition = "diff = 4, dark winning",
        density = 0.42f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.35f, 0.35f, 0.42f),
        alpha = 1f,
        noiseStrength = 0.65f,
        skyHaze = 50f,
        skyAlpha = 0.85f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.2f,
        lightScatteringExposure = 0.01f
    };
    
    [SerializeField] private FogPreset dark5Preset = new FogPreset
    {
        stateName = "Dark5 (Midnight)",
        triggerCondition = "diff = 5, dark winning",
        density = 0.50f,
        height = 220f,
        baselineHeight = 0f,
        fogColor = new Color(0.25f, 0.25f, 0.32f),
        alpha = 1f,
        noiseStrength = 0.7f,
        skyHaze = 60f,
        skyAlpha = 0.9f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.1f,
        lightScatteringExposure = 0.005f
    };
    
    [Header("=== DARK ESCALATION (Storms) ===")]
    [SerializeField] private FogPreset darkStage1Preset = new FogPreset
    {
        stateName = "Dark+Stage1 (Partly Cloudy)",
        triggerCondition = "diff = 6, dark committed",
        density = 0.55f,
        height = 250f,
        baselineHeight = 0f,
        fogColor = new Color(0.30f, 0.32f, 0.38f),
        alpha = 1f,
        noiseStrength = 0.72f,
        skyHaze = 65f,
        skyAlpha = 0.92f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.1f,
        lightScatteringExposure = 0.003f
    };
    
    [SerializeField] private FogPreset darkStage2Preset = new FogPreset
    {
        stateName = "Dark+Stage2 (Overcast)",
        triggerCondition = "diff = 7",
        density = 0.60f,
        height = 280f,
        baselineHeight = 0f,
        fogColor = new Color(0.28f, 0.30f, 0.35f),
        alpha = 1f,
        noiseStrength = 0.75f,
        skyHaze = 70f,
        skyAlpha = 0.95f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.05f,
        lightScatteringExposure = 0f
    };
    
    [SerializeField] private FogPreset darkStage3Preset = new FogPreset
    {
        stateName = "Dark+Stage3 (Light Rain)",
        triggerCondition = "diff = 8",
        density = 0.68f,
        height = 300f,
        baselineHeight = 0f,
        fogColor = new Color(0.25f, 0.28f, 0.32f),
        alpha = 1f,
        noiseStrength = 0.78f,
        skyHaze = 75f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f,
        lightScatteringExposure = 0f
    };
    
    [SerializeField] private FogPreset darkStage4Preset = new FogPreset
    {
        stateName = "Dark+Stage4 (Heavy Rain)",
        triggerCondition = "diff = 9",
        density = 0.75f,
        height = 350f,
        baselineHeight = 0f,
        fogColor = new Color(0.22f, 0.25f, 0.30f),
        alpha = 1f,
        noiseStrength = 0.82f,
        skyHaze = 85f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f,
        lightScatteringExposure = 0f
    };
    
    [SerializeField] private FogPreset darkStage5Preset = new FogPreset
    {
        stateName = "Dark+Stage5 (THUNDERSTORM)",
        triggerCondition = "diff = 10",
        density = 0.85f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(0.18f, 0.20f, 0.25f),
        alpha = 1f,
        noiseStrength = 0.9f,
        skyHaze = 100f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f,
        lightScatteringExposure = 0f
    };
    
    [Header("=== LIGHT PATH (Increasingly ethereal) ===")]
    [SerializeField] private FogPreset light1Preset = new FogPreset
    {
        stateName = "Light1",
        triggerCondition = "diff = 1, light winning",
        density = 0.12f,
        height = 160f,
        baselineHeight = 0f,
        fogColor = new Color(0.90f, 0.90f, 0.88f),
        alpha = 1f,
        noiseStrength = 0.35f,
        skyHaze = 18f,
        skyAlpha = 0.55f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.55f,
        lightScatteringExposure = 0.06f
    };
    
    [SerializeField] private FogPreset light2Preset = new FogPreset
    {
        stateName = "Light2",
        triggerCondition = "diff = 2, light winning",
        density = 0.10f,
        height = 170f,
        baselineHeight = 0f,
        fogColor = new Color(0.92f, 0.92f, 0.88f),
        alpha = 1f,
        noiseStrength = 0.3f,
        skyHaze = 15f,
        skyAlpha = 0.5f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.6f,
        lightScatteringExposure = 0.08f
    };
    
    [SerializeField] private FogPreset light3Preset = new FogPreset
    {
        stateName = "Light3",
        triggerCondition = "diff = 3, light winning",
        density = 0.08f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.95f, 0.90f),
        alpha = 1f,
        noiseStrength = 0.25f,
        skyHaze = 12f,
        skyAlpha = 0.45f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.65f,
        lightScatteringExposure = 0.10f
    };
    
    [SerializeField] private FogPreset light4Preset = new FogPreset
    {
        stateName = "Light4",
        triggerCondition = "diff = 4, light winning",
        density = 0.06f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.98f, 0.97f, 0.92f),
        alpha = 1f,
        noiseStrength = 0.2f,
        skyHaze = 10f,
        skyAlpha = 0.4f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.7f,
        lightScatteringExposure = 0.12f
    };
    
    [SerializeField] private FogPreset light5Preset = new FogPreset
    {
        stateName = "Light5 (Heavenly)",
        triggerCondition = "diff = 5, light winning",
        density = 0.05f,
        height = 220f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.98f, 0.93f),
        alpha = 1f,
        noiseStrength = 0.15f,
        skyHaze = 8f,
        skyAlpha = 0.35f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.75f,
        lightScatteringExposure = 0.14f
    };
    
    [Header("=== LIGHT ESCALATION (Divine) ===")]
    [SerializeField] private FogPreset lightStage1Preset = new FogPreset
    {
        stateName = "Light+Stage1 (Blessed)",
        triggerCondition = "diff = 6, light committed",
        density = 0.04f,
        height = 250f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.98f, 0.90f),
        alpha = 1f,
        noiseStrength = 0.12f,
        skyHaze = 6f,
        skyAlpha = 0.3f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.78f,
        lightScatteringExposure = 0.15f
    };
    
    [SerializeField] private FogPreset lightStage2Preset = new FogPreset
    {
        stateName = "Light+Stage2 (Sacred)",
        triggerCondition = "diff = 7",
        density = 0.03f,
        height = 280f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.99f, 0.92f),
        alpha = 1f,
        noiseStrength = 0.1f,
        skyHaze = 5f,
        skyAlpha = 0.25f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.82f,
        lightScatteringExposure = 0.16f
    };
    
    [SerializeField] private FogPreset lightStage3Preset = new FogPreset
    {
        stateName = "Light+Stage3 (Radiant)",
        triggerCondition = "diff = 8",
        density = 0.025f,
        height = 300f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 0.95f),
        alpha = 1f,
        noiseStrength = 0.08f,
        skyHaze = 4f,
        skyAlpha = 0.2f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.85f,
        lightScatteringExposure = 0.17f
    };
    
    [SerializeField] private FogPreset lightStage4Preset = new FogPreset
    {
        stateName = "Light+Stage4 (Transcendent)",
        triggerCondition = "diff = 9",
        density = 0.02f,
        height = 350f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 0.97f),
        alpha = 1f,
        noiseStrength = 0.05f,
        skyHaze = 3f,
        skyAlpha = 0.15f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.9f,
        lightScatteringExposure = 0.18f
    };
    
    [SerializeField] private FogPreset lightStage5Preset = new FogPreset
    {
        stateName = "Light+Stage5 (DIVINE)",
        triggerCondition = "diff = 10",
        density = 0.015f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 1f),
        alpha = 1f,
        noiseStrength = 0.03f,
        skyHaze = 2f,
        skyAlpha = 0.1f,
        enableLightScattering = true,
        lightScatteringDiffusion = 1f,
        lightScatteringExposure = 0.2f
    };
    
    [Header("=== ECLIPSE STATES (7-stage gradient, see GDD §5) ===")]
    [Tooltip("Stage 1 (15%) - 2L/2R - subtle eclipse hint")]
    [SerializeField] private FogPreset eclipse15Preset = new FogPreset
    {
        stateName = "Eclipse 15%",
        triggerCondition = "2L/2R",
        density = 0.22f,
        height = 190f,
        baselineHeight = 0f,
        fogColor = new Color(0.65f, 0.60f, 0.68f),
        alpha = 1f,
        noiseStrength = 0.48f,
        skyHaze = 27f,
        skyAlpha = 0.68f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.52f,
        lightScatteringExposure = 0.045f
    };
    
    [FormerlySerializedAs("eclipse20Preset")]
    [Tooltip("Stage 2 (25%) - 2L/3R or 3L/2R")]
    [SerializeField] private FogPreset eclipse25Preset = new FogPreset
    {
        stateName = "Eclipse 25%",
        triggerCondition = "2L/3R or 3L/2R",
        density = 0.25f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.60f, 0.55f, 0.65f),
        alpha = 1f,
        noiseStrength = 0.5f,
        skyHaze = 30f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.5f,
        lightScatteringExposure = 0.04f
    };
    
    [Tooltip("Stage 3 (40%) - 3L/3R")]
    [SerializeField] private FogPreset eclipse40Preset = new FogPreset
    {
        stateName = "Eclipse 40%",
        triggerCondition = "3L/3R",
        density = 0.32f,
        height = 220f,
        baselineHeight = 0f,
        fogColor = new Color(0.55f, 0.48f, 0.62f),
        alpha = 1f,
        noiseStrength = 0.55f,
        skyHaze = 40f,
        skyAlpha = 0.75f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.45f,
        lightScatteringExposure = 0.035f
    };
    
    [FormerlySerializedAs("eclipse50Preset")]
    [Tooltip("Stage 4 (55%) - 3L/4R or 4L/3R")]
    [SerializeField] private FogPreset eclipse55Preset = new FogPreset
    {
        stateName = "Eclipse 55%",
        triggerCondition = "3L/4R or 4L/3R",
        density = 0.38f,
        height = 250f,
        baselineHeight = 0f,
        fogColor = new Color(0.50f, 0.42f, 0.58f),
        alpha = 1f,
        noiseStrength = 0.6f,
        skyHaze = 50f,
        skyAlpha = 0.8f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.4f,
        lightScatteringExposure = 0.03f
    };
    
    [FormerlySerializedAs("eclipse60Preset")]
    [Tooltip("Stage 5 (70%) - 4L/4R")]
    [SerializeField] private FogPreset eclipse70Preset = new FogPreset
    {
        stateName = "Eclipse 70%",
        triggerCondition = "4L/4R",
        density = 0.45f,
        height = 280f,
        baselineHeight = 0f,
        fogColor = new Color(0.45f, 0.38f, 0.55f),
        alpha = 1f,
        noiseStrength = 0.65f,
        skyHaze = 60f,
        skyAlpha = 0.85f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.35f,
        lightScatteringExposure = 0.025f
    };
    
    [FormerlySerializedAs("eclipse75Preset")]
    [Tooltip("Stage 6 (85%) - 4L/5R or 5L/4R")]
    [SerializeField] private FogPreset eclipse85Preset = new FogPreset
    {
        stateName = "Eclipse 85%",
        triggerCondition = "4L/5R or 5L/4R",
        density = 0.55f,
        height = 320f,
        baselineHeight = 0f,
        fogColor = new Color(0.38f, 0.32f, 0.50f),
        alpha = 1f,
        noiseStrength = 0.72f,
        skyHaze = 75f,
        skyAlpha = 0.9f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.3f,
        lightScatteringExposure = 0.02f
    };
    
    [Tooltip("Stage 7 (100%) - 5L/5R - full eclipse")]
    [SerializeField] private FogPreset eclipseFullPreset = new FogPreset
    {
        stateName = "Eclipse FULL (5L/5R)",
        triggerCondition = "5L/5R - Total Eclipse",
        density = 0.65f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(0.30f, 0.25f, 0.45f),
        alpha = 1f,
        noiseStrength = 0.8f,
        skyHaze = 90f,
        skyAlpha = 0.95f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.25f,
        lightScatteringExposure = 0.015f
    };
    
    #endregion
    
    #region Private Fields
    
    private FogPreset currentPreset;
    private FogPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning = false;
    
    // Reflection cache
    private System.Type fogType;
    private System.Reflection.PropertyInfo densityProperty;
    private System.Reflection.PropertyInfo heightProperty;
    private System.Reflection.PropertyInfo baseHeightProperty;
    private System.Reflection.PropertyInfo colorProperty;
    private System.Reflection.PropertyInfo alphaProperty;
    private System.Reflection.PropertyInfo noiseStrengthProperty;
    private System.Reflection.PropertyInfo skyHazeProperty;
    private System.Reflection.PropertyInfo skyAlphaProperty;
    private System.Reflection.PropertyInfo lightScatteringEnabledProperty;
    private System.Reflection.PropertyInfo lightScatteringDiffusionProperty;
    private System.Reflection.PropertyInfo lightScatteringExposureProperty;
    private bool reflectionInitialized = false;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        FindVolumetricFog();
        InitializeReflection();
        InitializeState();
        SubscribeToEvents();
    }
    
    void Update()
    {
        if (isTransitioning)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            
            if (transitionProgress >= 1f)
            {
                transitionProgress = 1f;
                isTransitioning = false;
                currentPreset = targetPreset;
            }
            
            FogPreset lerpedPreset = FogPreset.Lerp(currentPreset, targetPreset, transitionProgress);
            ApplyPreset(lerpedPreset);
        }
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
    }
    
    #endregion
    
    #region Initialization
    
    void FindVolumetricFog()
    {
        if (volumetricFog != null) return;
        
        if (autoFind)
        {
            // Search on all cameras
            foreach (var cam in Camera.allCameras)
            {
                var components = cam.GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "VolumetricFog")
                    {
                        volumetricFog = comp;
                        if (logChanges) Debug.Log("[VolumetricFogController] Found VolumetricFog component on " + cam.name);
                        return;
                    }
                }
            }
            
            // Fallback: search everywhere
            var allMonos = FindObjectsOfType<MonoBehaviour>();
            foreach (var mono in allMonos)
            {
                if (mono != null && mono.GetType().Name == "VolumetricFog")
                {
                    volumetricFog = mono;
                    if (logChanges) Debug.Log("[VolumetricFogController] Found VolumetricFog component");
                    break;
                }
            }
        }
        
        if (volumetricFog == null)
        {
            Debug.LogWarning("[VolumetricFogController] VolumetricFog component not found!");
        }
    }
    
    void InitializeReflection()
    {
        if (volumetricFog == null) return;
        
        fogType = volumetricFog.GetType();
        
        // Get all the properties we need
        densityProperty = fogType.GetProperty("density");
        heightProperty = fogType.GetProperty("height");
        baseHeightProperty = fogType.GetProperty("baselineHeight");
        colorProperty = fogType.GetProperty("color");
        alphaProperty = fogType.GetProperty("alpha");
        noiseStrengthProperty = fogType.GetProperty("noiseStrength");
        skyHazeProperty = fogType.GetProperty("skyHaze");
        skyAlphaProperty = fogType.GetProperty("skyAlpha");
        lightScatteringEnabledProperty = fogType.GetProperty("lightScatteringEnabled");
        lightScatteringDiffusionProperty = fogType.GetProperty("lightScatteringDiffusion");
        lightScatteringExposureProperty = fogType.GetProperty("lightScatteringExposure");
        
        reflectionInitialized = (densityProperty != null && heightProperty != null);
        
        if (logChanges)
        {
            Debug.Log($"[VolumetricFogController] Reflection init: density={densityProperty != null}, " +
                      $"height={heightProperty != null}, baselineHeight={baseHeightProperty != null}, " +
                      $"alpha={alphaProperty != null}, noiseStrength={noiseStrengthProperty != null}, " +
                      $"lightScatteringExposure={lightScatteringExposureProperty != null}");
        }
    }
    
    void InitializeState()
    {
        currentPreset = neutralPreset;
        targetPreset = neutralPreset;
        currentPresetName = neutralPreset.stateName;
        ApplyPreset(currentPreset);
        
        if (logChanges)
        {
            Debug.Log($"[VolumetricFogController] Initialized with Neutral preset (height={neutralPreset.height}, baselineHeight={neutralPreset.baselineHeight})");
        }
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
        
        FogPreset newTarget = GetPresetForRings(leftRings, rightRings);
        
        if (newTarget.stateName != targetPreset.stateName)
        {
            targetPreset = newTarget;
            currentPresetName = targetPreset.stateName;
            transitionProgress = 0f;
            isTransitioning = true;
            
            if (logChanges)
            {
                Debug.Log($"[VolumetricFogController] {leftRings}L/{rightRings}R → {targetPreset.stateName} " +
                          $"(density={targetPreset.density:F2}, height={targetPreset.height}, baselineHeight={targetPreset.baselineHeight})");
            }
        }
    }
    
    #endregion
    
    #region State Resolution
    
    /// <summary>
    /// Resolve a preset from raw ring counts.
    /// Cascade matches WorldStateManager exactly (first match wins):
    /// 1. Eclipse (min>=2 AND diff<=1) -> stage 1-7
    /// 2. Sunset  ((1L/0R) OR (diff=2, dark wins, both, max<=4))
    /// 3. Sunrise ((0L/1R) OR (diff=2, light wins, both, max<=4))
    /// 4. Escalation (diff>=6) -> DarkStage/LightStage by (diff-5)
    /// 5. Path (diff 1-5) -> Dark/Light by diff
    /// 6. Neutral (L==R, not eclipse)
    /// </summary>
    FogPreset GetPresetForRings(int L, int R)
    {
        int diff = Mathf.Abs(L - R);
        int minRings = Mathf.Min(L, R);
        int maxRings = Mathf.Max(L, R);
        bool darkWinning = L > R;
        bool lightWinning = R > L;
        bool bothHaveRings = L > 0 && R > 0;
        
        // === PRIORITY 1: ECLIPSE (7 stages, see GDD §5) ===
        if (minRings >= 2 && diff <= 1)
        {
            if (L == 5 && R == 5)                       return eclipseFullPreset; // Stage 7
            if (minRings == 4 && maxRings == 5)         return eclipse85Preset;   // Stage 6
            if (L == 4 && R == 4)                       return eclipse70Preset;   // Stage 5
            if (minRings == 3 && maxRings == 4)         return eclipse55Preset;   // Stage 4
            if (L == 3 && R == 3)                       return eclipse40Preset;   // Stage 3
            if (minRings == 2 && maxRings == 3)         return eclipse25Preset;   // Stage 2
            if (L == 2 && R == 2)                       return eclipse15Preset;   // Stage 1
        }
        
        // === PRIORITY 2: SUNSET ===
        if ((L == 1 && R == 0) ||
            (diff == 2 && darkWinning && bothHaveRings && maxRings <= 4))
        {
            return sunsetPreset;
        }
        
        // === PRIORITY 3: SUNRISE ===
        if ((L == 0 && R == 1) ||
            (diff == 2 && lightWinning && bothHaveRings && maxRings <= 4))
        {
            return sunrisePreset;
        }
        
        // === PRIORITY 4: ESCALATION (diff >= 6) ===
        if (diff >= 6)
        {
            int stage = Mathf.Clamp(diff - 5, 1, 5);
            if (darkWinning)
            {
                switch (stage)
                {
                    case 1: return darkStage1Preset;
                    case 2: return darkStage2Preset;
                    case 3: return darkStage3Preset;
                    case 4: return darkStage4Preset;
                    default: return darkStage5Preset;
                }
            }
            else // lightWinning (diff >= 6 guarantees L != R)
            {
                switch (stage)
                {
                    case 1: return lightStage1Preset;
                    case 2: return lightStage2Preset;
                    case 3: return lightStage3Preset;
                    case 4: return lightStage4Preset;
                    default: return lightStage5Preset;
                }
            }
        }
        
        // === PRIORITY 5: PATH (diff 1-5) ===
        if (diff >= 1)
        {
            if (darkWinning)
            {
                switch (diff)
                {
                    case 1: return dark1Preset;
                    case 2: return dark2Preset;
                    case 3: return dark3Preset;
                    case 4: return dark4Preset;
                    default: return dark5Preset; // diff == 5
                }
            }
            else // lightWinning
            {
                switch (diff)
                {
                    case 1: return light1Preset;
                    case 2: return light2Preset;
                    case 3: return light3Preset;
                    case 4: return light4Preset;
                    default: return light5Preset; // diff == 5
                }
            }
        }
        
        // === PRIORITY 6: NEUTRAL ===
        return neutralPreset;
    }
    
    #endregion
    
    #region Apply Preset
    
    void ApplyPreset(FogPreset preset)
    {
        if (!reflectionInitialized || volumetricFog == null) return;
        
        try
        {
            // Core fog geometry
            densityProperty?.SetValue(volumetricFog, preset.density);
            heightProperty?.SetValue(volumetricFog, preset.height);
            baseHeightProperty?.SetValue(volumetricFog, preset.baselineHeight);
            
            // Appearance
            colorProperty?.SetValue(volumetricFog, preset.fogColor);
            alphaProperty?.SetValue(volumetricFog, preset.alpha);
            noiseStrengthProperty?.SetValue(volumetricFog, preset.noiseStrength);
            
            // Sky
            skyHazeProperty?.SetValue(volumetricFog, preset.skyHaze);
            skyAlphaProperty?.SetValue(volumetricFog, preset.skyAlpha);
            
            // Light scattering
            lightScatteringEnabledProperty?.SetValue(volumetricFog, preset.enableLightScattering);
            lightScatteringDiffusionProperty?.SetValue(volumetricFog, preset.lightScatteringDiffusion);
            lightScatteringExposureProperty?.SetValue(volumetricFog, preset.lightScatteringExposure);
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
    
    [ContextMenu("Test: Dark+Stage4 (Heavy Rain)")]
    void TestDarkStage4() => TestPreset(darkStage4Preset);
    
    [ContextMenu("Test: Dark+Stage5 (Thunderstorm)")]
    void TestDarkStage5() => TestPreset(darkStage5Preset);
    
    [ContextMenu("Test: Light5 (Heavenly)")]
    void TestLight5() => TestPreset(light5Preset);
    
    [ContextMenu("Test: Light+Stage5 (Divine)")]
    void TestLightStage5() => TestPreset(lightStage5Preset);
    
    [ContextMenu("Test: Eclipse FULL")]
    void TestEclipseFull() => TestPreset(eclipseFullPreset);
    
    [ContextMenu("Log Current Fog Values")]
    void LogCurrentValues()
    {
        if (volumetricFog == null)
        {
            Debug.Log("[VolumetricFogController] No fog component found");
            return;
        }
        
        Debug.Log("=== CURRENT FOG VALUES ===");
        Debug.Log($"  density: {densityProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  height: {heightProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  baselineHeight: {baseHeightProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  alpha: {alphaProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  color: {colorProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  noiseStrength: {noiseStrengthProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  skyHaze: {skyHazeProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  skyAlpha: {skyAlphaProperty?.GetValue(volumetricFog)}");
        Debug.Log($"  lightScatteringExposure: {lightScatteringExposureProperty?.GetValue(volumetricFog)}");
    }
    
    void TestPreset(FogPreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        isTransitioning = false;
        transitionProgress = 1f;
        currentPresetName = preset.stateName;
        ApplyPreset(preset);
        Debug.Log($"[VolumetricFogController] Testing: {preset.stateName} (density={preset.density}, height={preset.height}, baselineHeight={preset.baselineHeight})");
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