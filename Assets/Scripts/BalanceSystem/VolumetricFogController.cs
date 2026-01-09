using UnityEngine;
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
                lightScatteringDiffusion = Mathf.Lerp(a.lightScatteringDiffusion, b.lightScatteringDiffusion, t)
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
        triggerCondition = "0L/0R, 1L/1R, 2L/2R",
        density = 0.15f,
        height = 150f,           // Fog extends from 0 to 150
        baselineHeight = 0f,
        fogColor = new Color(0.85f, 0.88f, 0.92f),  // Light bluish-gray
        alpha = 1f,
        noiseStrength = 0.4f,
        skyHaze = 20f,
        skyAlpha = 0.6f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.5f
    };
    
    [Header("=== SUNSET ===")]
    [SerializeField] private FogPreset sunsetPreset = new FogPreset
    {
        stateName = "Sunset",
        triggerCondition = "1L/0R, 3L/1R, 4L/2R, 5L/3R, 6L/4R",
        density = 0.20f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.70f, 0.50f),  // Warm orange
        alpha = 1f,
        noiseStrength = 0.45f,
        skyHaze = 35f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.6f
    };
    
    [Header("=== SUNRISE ===")]
    [SerializeField] private FogPreset sunrisePreset = new FogPreset
    {
        stateName = "Sunrise",
        triggerCondition = "0L/1R, 1L/3R, 2L/4R, 3L/5R, 4L/6R",
        density = 0.18f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.80f, 0.70f),  // Soft peach
        alpha = 1f,
        noiseStrength = 0.4f,
        skyHaze = 30f,
        skyAlpha = 0.65f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.55f
    };
    
    [Header("=== DARK PATH (Increasingly ominous) ===")]
    [SerializeField] private FogPreset dark1Preset = new FogPreset
    {
        stateName = "Dark1",
        triggerCondition = "diff > 2, dark winning, L small",
        density = 0.22f,
        height = 160f,
        baselineHeight = 0f,
        fogColor = new Color(0.65f, 0.65f, 0.70f),  // Cool gray
        alpha = 1f,
        noiseStrength = 0.5f,
        skyHaze = 25f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.45f
    };
    
    [SerializeField] private FogPreset dark2Preset = new FogPreset
    {
        stateName = "Dark2",
        triggerCondition = "diff > 2, L >= 2",
        density = 0.28f,
        height = 170f,
        baselineHeight = 0f,
        fogColor = new Color(0.55f, 0.55f, 0.62f),  // Darker gray
        alpha = 1f,
        noiseStrength = 0.55f,
        skyHaze = 30f,
        skyAlpha = 0.75f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.4f
    };
    
    [SerializeField] private FogPreset dark3Preset = new FogPreset
    {
        stateName = "Dark3",
        triggerCondition = "diff > 2, L >= 3",
        density = 0.35f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.45f, 0.45f, 0.52f),  // Deep gray
        alpha = 1f,
        noiseStrength = 0.6f,
        skyHaze = 40f,
        skyAlpha = 0.8f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.3f
    };
    
    [SerializeField] private FogPreset dark4Preset = new FogPreset
    {
        stateName = "Dark4",
        triggerCondition = "diff > 2, L >= 4",
        density = 0.42f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.35f, 0.35f, 0.42f),  // Very dark
        alpha = 1f,
        noiseStrength = 0.65f,
        skyHaze = 50f,
        skyAlpha = 0.85f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.2f
    };
    
    [SerializeField] private FogPreset dark5Preset = new FogPreset
    {
        stateName = "Dark5 (Midnight)",
        triggerCondition = "diff > 2, L >= 5",
        density = 0.50f,
        height = 220f,
        baselineHeight = 0f,
        fogColor = new Color(0.25f, 0.25f, 0.32f),  // Near black
        alpha = 1f,
        noiseStrength = 0.7f,
        skyHaze = 60f,
        skyAlpha = 0.9f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.1f
    };
    
    [Header("=== DARK ESCALATION (Storms) ===")]
    [SerializeField] private FogPreset darkStage1Preset = new FogPreset
    {
        stateName = "Dark+Stage1 (Partly Cloudy)",
        triggerCondition = "L >= 6, dark committed",
        density = 0.55f,
        height = 250f,
        baselineHeight = 0f,
        fogColor = new Color(0.30f, 0.32f, 0.38f),
        alpha = 1f,
        noiseStrength = 0.72f,
        skyHaze = 65f,
        skyAlpha = 0.92f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.1f
    };
    
    [SerializeField] private FogPreset darkStage2Preset = new FogPreset
    {
        stateName = "Dark+Stage2 (Overcast)",
        triggerCondition = "L >= 7",
        density = 0.60f,
        height = 280f,
        baselineHeight = 0f,
        fogColor = new Color(0.28f, 0.30f, 0.35f),
        alpha = 1f,
        noiseStrength = 0.75f,
        skyHaze = 70f,
        skyAlpha = 0.95f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0.05f
    };
    
    [SerializeField] private FogPreset darkStage3Preset = new FogPreset
    {
        stateName = "Dark+Stage3 (Light Rain)",
        triggerCondition = "L >= 8",
        density = 0.68f,
        height = 300f,
        baselineHeight = 0f,
        fogColor = new Color(0.25f, 0.28f, 0.32f),
        alpha = 1f,
        noiseStrength = 0.78f,
        skyHaze = 75f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f
    };
    
    [SerializeField] private FogPreset darkStage4Preset = new FogPreset
    {
        stateName = "Dark+Stage4 (Heavy Rain)",
        triggerCondition = "L >= 9",
        density = 0.75f,
        height = 350f,
        baselineHeight = 0f,
        fogColor = new Color(0.22f, 0.25f, 0.30f),
        alpha = 1f,
        noiseStrength = 0.82f,
        skyHaze = 85f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f
    };
    
    [SerializeField] private FogPreset darkStage5Preset = new FogPreset
    {
        stateName = "Dark+Stage5 (THUNDERSTORM)",
        triggerCondition = "L = 10",
        density = 0.85f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(0.18f, 0.20f, 0.25f),
        alpha = 1f,
        noiseStrength = 0.9f,
        skyHaze = 100f,
        skyAlpha = 1f,
        enableLightScattering = false,
        lightScatteringDiffusion = 0f
    };
    
    [Header("=== LIGHT PATH (Increasingly ethereal) ===")]
    [SerializeField] private FogPreset light1Preset = new FogPreset
    {
        stateName = "Light1",
        triggerCondition = "diff < -2, light winning, R small",
        density = 0.12f,
        height = 160f,
        baselineHeight = 0f,
        fogColor = new Color(0.90f, 0.90f, 0.88f),  // Warm white
        alpha = 1f,
        noiseStrength = 0.35f,
        skyHaze = 18f,
        skyAlpha = 0.55f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.55f
    };
    
    [SerializeField] private FogPreset light2Preset = new FogPreset
    {
        stateName = "Light2",
        triggerCondition = "diff < -2, R >= 2",
        density = 0.10f,
        height = 170f,
        baselineHeight = 0f,
        fogColor = new Color(0.92f, 0.92f, 0.88f),
        alpha = 1f,
        noiseStrength = 0.3f,
        skyHaze = 15f,
        skyAlpha = 0.5f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.6f
    };
    
    [SerializeField] private FogPreset light3Preset = new FogPreset
    {
        stateName = "Light3",
        triggerCondition = "diff < -2, R >= 3",
        density = 0.08f,
        height = 180f,
        baselineHeight = 0f,
        fogColor = new Color(0.95f, 0.95f, 0.90f),
        alpha = 1f,
        noiseStrength = 0.25f,
        skyHaze = 12f,
        skyAlpha = 0.45f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.65f
    };
    
    [SerializeField] private FogPreset light4Preset = new FogPreset
    {
        stateName = "Light4",
        triggerCondition = "diff < -2, R >= 4",
        density = 0.06f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.98f, 0.97f, 0.92f),  // Golden white
        alpha = 1f,
        noiseStrength = 0.2f,
        skyHaze = 10f,
        skyAlpha = 0.4f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.7f
    };
    
    [SerializeField] private FogPreset light5Preset = new FogPreset
    {
        stateName = "Light5 (Heavenly)",
        triggerCondition = "diff < -2, R >= 5",
        density = 0.05f,
        height = 220f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.98f, 0.93f),  // Divine glow
        alpha = 1f,
        noiseStrength = 0.15f,
        skyHaze = 8f,
        skyAlpha = 0.35f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.75f
    };
    
    [Header("=== LIGHT ESCALATION (Divine) ===")]
    [SerializeField] private FogPreset lightStage1Preset = new FogPreset
    {
        stateName = "Light+Stage1 (Blessed)",
        triggerCondition = "R >= 6, light committed",
        density = 0.04f,
        height = 250f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.98f, 0.90f),
        alpha = 1f,
        noiseStrength = 0.12f,
        skyHaze = 6f,
        skyAlpha = 0.3f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.78f
    };
    
    [SerializeField] private FogPreset lightStage2Preset = new FogPreset
    {
        stateName = "Light+Stage2 (Sacred)",
        triggerCondition = "R >= 7",
        density = 0.03f,
        height = 280f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 0.99f, 0.92f),
        alpha = 1f,
        noiseStrength = 0.1f,
        skyHaze = 5f,
        skyAlpha = 0.25f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.82f
    };
    
    [SerializeField] private FogPreset lightStage3Preset = new FogPreset
    {
        stateName = "Light+Stage3 (Radiant)",
        triggerCondition = "R >= 8",
        density = 0.025f,
        height = 300f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 0.95f),
        alpha = 1f,
        noiseStrength = 0.08f,
        skyHaze = 4f,
        skyAlpha = 0.2f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.85f
    };
    
    [SerializeField] private FogPreset lightStage4Preset = new FogPreset
    {
        stateName = "Light+Stage4 (Transcendent)",
        triggerCondition = "R >= 9",
        density = 0.02f,
        height = 350f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 0.97f),
        alpha = 1f,
        noiseStrength = 0.05f,
        skyHaze = 3f,
        skyAlpha = 0.15f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.9f
    };
    
    [SerializeField] private FogPreset lightStage5Preset = new FogPreset
    {
        stateName = "Light+Stage5 (DIVINE)",
        triggerCondition = "R = 10",
        density = 0.015f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(1f, 1f, 1f),  // Pure white
        alpha = 1f,
        noiseStrength = 0.03f,
        skyHaze = 2f,
        skyAlpha = 0.1f,
        enableLightScattering = true,
        lightScatteringDiffusion = 1f
    };
    
    [Header("=== ECLIPSE STATES (Mystical balance) ===")]
    [SerializeField] private FogPreset eclipse20Preset = new FogPreset
    {
        stateName = "Eclipse 20%",
        triggerCondition = "2L/3R or 3L/2R",
        density = 0.25f,
        height = 200f,
        baselineHeight = 0f,
        fogColor = new Color(0.60f, 0.55f, 0.65f),  // Purple tint
        alpha = 1f,
        noiseStrength = 0.5f,
        skyHaze = 30f,
        skyAlpha = 0.7f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.5f
    };
    
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
        lightScatteringDiffusion = 0.45f
    };
    
    [SerializeField] private FogPreset eclipse50Preset = new FogPreset
    {
        stateName = "Eclipse 50%",
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
        lightScatteringDiffusion = 0.4f
    };
    
    [SerializeField] private FogPreset eclipse60Preset = new FogPreset
    {
        stateName = "Eclipse 60%",
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
        lightScatteringDiffusion = 0.35f
    };
    
    [SerializeField] private FogPreset eclipse75Preset = new FogPreset
    {
        stateName = "Eclipse 75%",
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
        lightScatteringDiffusion = 0.3f
    };
    
    [SerializeField] private FogPreset eclipseFullPreset = new FogPreset
    {
        stateName = "Eclipse FULL (5L/5R)",
        triggerCondition = "5L/5R - Total Eclipse",
        density = 0.65f,
        height = 400f,
        baselineHeight = 0f,
        fogColor = new Color(0.30f, 0.25f, 0.45f),  // Deep purple
        alpha = 1f,
        noiseStrength = 0.8f,
        skyHaze = 90f,
        skyAlpha = 0.95f,
        enableLightScattering = true,
        lightScatteringDiffusion = 0.25f
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
        
        reflectionInitialized = (densityProperty != null && heightProperty != null);
        
        if (logChanges)
        {
            Debug.Log($"[VolumetricFogController] Reflection init: density={densityProperty != null}, " +
                      $"height={heightProperty != null}, baselineHeight={baseHeightProperty != null}, " +
                      $"alpha={alphaProperty != null}, noiseStrength={noiseStrengthProperty != null}");
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