using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System;

/// <summary>
/// YORU: Post-Process Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Handles ALL 22 unique atmosphere states with per-state presets.
/// Supports manual tweaking workflow - each state has fully editable settings.
/// 
/// DARK PATH has post-processing too:
/// - Cool temperatures, desaturation
/// - Increased vignette and contrast for drama
/// - Chromatic aberration for stormy stages
/// 
/// LIGHT PATH:
/// - Warm temperatures, enhanced saturation
/// - Bloom escalation for divine stages
/// - Golden vignette tints
/// 
/// Requires: Post Processing Stack v2 (Built-in Pipeline)
/// </summary>
[RequireComponent(typeof(PostProcessVolume))]
public class PostProcessController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class PostProcessPreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Color Grading")]
        public bool colorGradingEnabled = true;
        [Range(-100, 100)] public float temperature = 0f;
        [Range(-100, 100)] public float tint = 0f;
        [Range(-100, 100)] public float saturation = 0f;
        [Range(-100, 100)] public float contrast = 0f;
        
        [Header("Bloom")]
        public bool bloomEnabled = true;
        [Range(0, 3)] public float bloomIntensity = 0.5f;
        [Range(0, 2)] public float bloomThreshold = 1f;
        public Color bloomColor = Color.white;
        
        [Header("Vignette")]
        public bool vignetteEnabled = true;
        [Range(0, 1)] public float vignetteIntensity = 0.3f;
        [Range(0, 1)] public float vignetteSmoothness = 0.5f;
        public Color vignetteColor = Color.black;
        
        [Header("Chromatic Aberration (for drama)")]
        public bool chromaticEnabled = false;
        [Range(0, 1)] public float chromaticIntensity = 0f;
        
        [Header("Grain (for atmosphere)")]
        public bool grainEnabled = false;
        [Range(0, 1)] public float grainIntensity = 0f;
        
        public static PostProcessPreset Lerp(PostProcessPreset a, PostProcessPreset b, float t)
        {
            return new PostProcessPreset
            {
                stateName = b.stateName,
                colorGradingEnabled = t > 0.5f ? b.colorGradingEnabled : a.colorGradingEnabled,
                temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                tint = Mathf.Lerp(a.tint, b.tint, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                bloomEnabled = t > 0.5f ? b.bloomEnabled : a.bloomEnabled,
                bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t),
                bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t),
                bloomColor = Color.Lerp(a.bloomColor, b.bloomColor, t),
                vignetteEnabled = t > 0.5f ? b.vignetteEnabled : a.vignetteEnabled,
                vignetteIntensity = Mathf.Lerp(a.vignetteIntensity, b.vignetteIntensity, t),
                vignetteSmoothness = Mathf.Lerp(a.vignetteSmoothness, b.vignetteSmoothness, t),
                vignetteColor = Color.Lerp(a.vignetteColor, b.vignetteColor, t),
                chromaticEnabled = t > 0.5f ? b.chromaticEnabled : a.chromaticEnabled,
                chromaticIntensity = Mathf.Lerp(a.chromaticIntensity, b.chromaticIntensity, t),
                grainEnabled = t > 0.5f ? b.grainEnabled : a.grainEnabled,
                grainIntensity = Mathf.Lerp(a.grainIntensity, b.grainIntensity, t)
            };
        }
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private PostProcessVolume volume;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private PostProcessPreset neutralPreset = new PostProcessPreset
    {
        stateName = "Neutral (Balance 0)",
        temperature = 0, tint = 0, saturation = 0, contrast = 0,
        bloomIntensity = 0.3f, bloomThreshold = 1f,
        vignetteIntensity = 0.25f, vignetteColor = Color.black
    };
    
    [Header("=== LIGHT PATH (Balance +1 to +5) ===")]
    [SerializeField] private PostProcessPreset light1Preset = new PostProcessPreset
    {
        stateName = "Light1 (+1, Golden Hour)",
        temperature = 8, saturation = 5,
        bloomIntensity = 0.35f, bloomThreshold = 0.95f,
        vignetteIntensity = 0.22f
    };
    
    [SerializeField] private PostProcessPreset light2Preset = new PostProcessPreset
    {
        stateName = "Light2 (+2, Warm Afternoon)",
        temperature = 12, saturation = 8,
        bloomIntensity = 0.4f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.2f
    };
    
    [SerializeField] private PostProcessPreset light3Preset = new PostProcessPreset
    {
        stateName = "Light3 (+3, Bright)",
        temperature = 16, saturation = 10,
        bloomIntensity = 0.45f, bloomThreshold = 0.85f,
        vignetteIntensity = 0.18f
    };
    
    [SerializeField] private PostProcessPreset light4Preset = new PostProcessPreset
    {
        stateName = "Light4 (+4, Brighter)",
        temperature = 20, saturation = 12,
        bloomIntensity = 0.5f, bloomThreshold = 0.8f,
        vignetteIntensity = 0.15f
    };
    
    [SerializeField] private PostProcessPreset light5Preset = new PostProcessPreset
    {
        stateName = "Light5 (+5, Heavenly)",
        temperature = 25, tint = 2, saturation = 15,
        bloomIntensity = 0.6f, bloomThreshold = 0.75f,
        vignetteIntensity = 0.12f, vignetteColor = new Color(1f, 0.9f, 0.7f, 1f)
    };
    
    [Header("=== LIGHT PATH ESCALATION (Stage 1-5, Divine Glow) ===")]
    [SerializeField] private PostProcessPreset lightStage1Preset = new PostProcessPreset
    {
        stateName = "Light5+Stage1 (+6, Divine Beginning)",
        temperature = 28, tint = 3, saturation = 16,
        bloomIntensity = 0.7f, bloomThreshold = 0.7f,
        vignetteIntensity = 0.1f, vignetteColor = new Color(1f, 0.84f, 0f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage2Preset = new PostProcessPreset
    {
        stateName = "Light5+Stage2 (+7, Radiant)",
        temperature = 32, tint = 4, saturation = 18,
        bloomIntensity = 0.8f, bloomThreshold = 0.65f,
        vignetteIntensity = 0.08f, vignetteColor = new Color(1f, 0.78f, 0.14f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage3Preset = new PostProcessPreset
    {
        stateName = "Light5+Stage3 (+8, Glorious)",
        temperature = 36, tint = 5, saturation = 20,
        bloomIntensity = 0.9f, bloomThreshold = 0.6f,
        vignetteIntensity = 0.06f, vignetteColor = new Color(1f, 0.71f, 0.28f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage4Preset = new PostProcessPreset
    {
        stateName = "Light5+Stage4 (+9, Transcendent)",
        temperature = 42, tint = 6, saturation = 22,
        bloomIntensity = 1.0f, bloomThreshold = 0.55f,
        vignetteIntensity = 0.04f, vignetteColor = new Color(1f, 0.66f, 0.42f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage5Preset = new PostProcessPreset
    {
        stateName = "Light5+Stage5 (+10, MAXIMUM DIVINE)",
        temperature = 50, tint = 8, saturation = 25,
        bloomIntensity = 1.2f, bloomThreshold = 0.5f, bloomColor = new Color(1f, 0.95f, 0.85f),
        vignetteIntensity = 0.02f, vignetteColor = new Color(1f, 0.6f, 0.56f, 1f)
    };
    
    [Header("=== DARK PATH (Balance -1 to -5) ===")]
    [SerializeField] private PostProcessPreset dark1Preset = new PostProcessPreset
    {
        stateName = "Dark1 (-1, Late Afternoon)",
        temperature = -5, tint = 2, saturation = -3, contrast = 3,
        bloomIntensity = 0.28f, bloomThreshold = 1f,
        vignetteIntensity = 0.28f, vignetteColor = new Color(0.1f, 0.06f, 0.12f)
    };
    
    [SerializeField] private PostProcessPreset dark2Preset = new PostProcessPreset
    {
        stateName = "Dark2 (-2, Sunset)",
        temperature = -8, tint = 4, saturation = -6, contrast = 5,
        bloomIntensity = 0.25f, bloomThreshold = 1f,
        vignetteIntensity = 0.32f, vignetteColor = new Color(0.15f, 0.06f, 0.17f)
    };
    
    [SerializeField] private PostProcessPreset dark3Preset = new PostProcessPreset
    {
        stateName = "Dark3 (-3, Dusk)",
        temperature = -12, tint = 6, saturation = -10, contrast = 8,
        bloomIntensity = 0.22f, bloomThreshold = 0.95f,
        vignetteIntensity = 0.36f, vignetteColor = new Color(0.2f, 0.06f, 0.22f)
    };
    
    [SerializeField] private PostProcessPreset dark4Preset = new PostProcessPreset
    {
        stateName = "Dark4 (-4, Night)",
        temperature = -16, tint = 8, saturation = -15, contrast = 10,
        bloomIntensity = 0.2f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.4f, vignetteColor = new Color(0.25f, 0.06f, 0.27f)
    };
    
    [SerializeField] private PostProcessPreset dark5Preset = new PostProcessPreset
    {
        stateName = "Dark5 (-5, Midnight - Visible!)",
        temperature = -20, tint = 10, saturation = -20, contrast = 15,
        bloomIntensity = 0.35f, bloomThreshold = 0.7f, // Higher bloom so lights glow
        vignetteIntensity = 0.35f, vignetteColor = new Color(0.05f, 0.03f, 0.09f)
    };
    
    [Header("=== DARK PATH ESCALATION (Stage 1-5, Stormy Drama) ===")]
    [SerializeField] private PostProcessPreset darkStage1Preset = new PostProcessPreset
    {
        stateName = "Dark5+Stage1 (-6, Partly Cloudy)",
        temperature = -22, tint = 11, saturation = -22, contrast = 16,
        bloomIntensity = 0.32f, bloomThreshold = 0.75f,
        vignetteIntensity = 0.38f, vignetteColor = new Color(0.05f, 0.03f, 0.09f),
        chromaticEnabled = true, chromaticIntensity = 0.05f
    };
    
    [SerializeField] private PostProcessPreset darkStage2Preset = new PostProcessPreset
    {
        stateName = "Dark5+Stage2 (-7, Overcast)",
        temperature = -25, tint = 12, saturation = -25, contrast = 18,
        bloomIntensity = 0.28f, bloomThreshold = 0.8f,
        vignetteIntensity = 0.42f, vignetteColor = new Color(0.04f, 0.02f, 0.08f),
        chromaticEnabled = true, chromaticIntensity = 0.1f
    };
    
    [SerializeField] private PostProcessPreset darkStage3Preset = new PostProcessPreset
    {
        stateName = "Dark5+Stage3 (-8, Light Rain)",
        temperature = -28, tint = 14, saturation = -28, contrast = 20,
        bloomIntensity = 0.25f, bloomThreshold = 0.85f,
        vignetteIntensity = 0.45f, vignetteColor = new Color(0.04f, 0.02f, 0.07f),
        chromaticEnabled = true, chromaticIntensity = 0.15f,
        grainEnabled = true, grainIntensity = 0.1f
    };
    
    [SerializeField] private PostProcessPreset darkStage4Preset = new PostProcessPreset
    {
        stateName = "Dark5+Stage4 (-9, Heavy Rain)",
        temperature = -32, tint = 16, saturation = -32, contrast = 24,
        bloomIntensity = 0.22f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.5f, vignetteColor = new Color(0.03f, 0.02f, 0.06f),
        chromaticEnabled = true, chromaticIntensity = 0.25f,
        grainEnabled = true, grainIntensity = 0.2f
    };
    
    [SerializeField] private PostProcessPreset darkStage5Preset = new PostProcessPreset
    {
        stateName = "Dark5+Stage5 (-10, THUNDERSTORM)",
        temperature = -40, tint = 20, saturation = -40, contrast = 30,
        bloomIntensity = 0.2f, bloomThreshold = 0.95f,
        vignetteIntensity = 0.55f, vignetteColor = new Color(0.02f, 0.01f, 0.05f),
        chromaticEnabled = true, chromaticIntensity = 0.4f,
        grainEnabled = true, grainIntensity = 0.3f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private PostProcessPreset eclipsePreset = new PostProcessPreset
    {
        stateName = "Eclipse (5L + 5R, Dramatic)",
        temperature = 5, tint = -15, saturation = -10, contrast = 20,
        bloomIntensity = 0.5f, bloomThreshold = 0.6f, bloomColor = new Color(0.8f, 0.6f, 1f),
        vignetteIntensity = 0.45f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.08f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private WorldStateManager.AtmosphereState currentAtmosphere;
    [SerializeField] private int currentWeatherStage;
    
    #endregion
    
    #region Private Fields
    
    private PostProcessPreset currentPreset;
    private PostProcessPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    
    // Post-process effect references
    private ColorGrading colorGrading;
    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private Grain grain;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        SetupVolume();
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
            else
            {
                currentPreset = PostProcessPreset.Lerp(currentPreset, targetPreset, transitionProgress);
            }
            ApplyPreset(currentPreset);
        }
    }
    
    #endregion
    
    #region Setup
    
    void SetupVolume()
    {
        if (volume == null)
            volume = GetComponent<PostProcessVolume>();
        
        if (volume == null)
        {
            Debug.LogError("[PostProcessController] No PostProcessVolume found!");
            enabled = false;
            return;
        }
        
        if (volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        }
        
        // Get or add effects
        if (!volume.profile.TryGetSettings(out colorGrading))
        {
            colorGrading = volume.profile.AddSettings<ColorGrading>();
        }
        colorGrading.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out bloom))
        {
            bloom = volume.profile.AddSettings<Bloom>();
        }
        bloom.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out vignette))
        {
            vignette = volume.profile.AddSettings<Vignette>();
        }
        vignette.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out chromaticAberration))
        {
            chromaticAberration = volume.profile.AddSettings<ChromaticAberration>();
        }
        
        if (!volume.profile.TryGetSettings(out grain))
        {
            grain = volume.profile.AddSettings<Grain>();
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
            Debug.Log($"[PostProcessController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    PostProcessPreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
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
    
    void ApplyPreset(PostProcessPreset preset)
    {
        // Color Grading
        if (colorGrading != null)
        {
            colorGrading.enabled.Override(preset.colorGradingEnabled);
            colorGrading.temperature.Override(preset.temperature);
            colorGrading.tint.Override(preset.tint);
            colorGrading.saturation.Override(preset.saturation);
            colorGrading.contrast.Override(preset.contrast);
        }
        
        // Bloom
        if (bloom != null)
        {
            bloom.enabled.Override(preset.bloomEnabled);
            bloom.intensity.Override(preset.bloomIntensity);
            bloom.threshold.Override(preset.bloomThreshold);
            bloom.color.Override(preset.bloomColor);
        }
        
        // Vignette
        if (vignette != null)
        {
            vignette.enabled.Override(preset.vignetteEnabled);
            vignette.intensity.Override(preset.vignetteIntensity);
            vignette.smoothness.Override(preset.vignetteSmoothness);
            vignette.color.Override(preset.vignetteColor);
        }
        
        // Chromatic Aberration
        if (chromaticAberration != null)
        {
            chromaticAberration.enabled.Override(preset.chromaticEnabled);
            chromaticAberration.intensity.Override(preset.chromaticIntensity);
        }
        
        // Grain
        if (grain != null)
        {
            grain.enabled.Override(preset.grainEnabled);
            grain.intensity.Override(preset.grainIntensity);
        }
    }
    
    #endregion
    
    #region Context Menu (For Testing)
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() => PreviewPreset(neutralPreset);
    
    [ContextMenu("Preview: Light5")]
    void PreviewLight5() => PreviewPreset(light5Preset);
    
    [ContextMenu("Preview: Light5+Stage5 (Divine)")]
    void PreviewLightMax() => PreviewPreset(lightStage5Preset);
    
    [ContextMenu("Preview: Dark5")]
    void PreviewDark5() => PreviewPreset(dark5Preset);
    
    [ContextMenu("Preview: Dark5+Stage5 (Storm)")]
    void PreviewDarkMax() => PreviewPreset(darkStage5Preset);
    
    [ContextMenu("Preview: Eclipse")]
    void PreviewEclipse() => PreviewPreset(eclipsePreset);
    
    void PreviewPreset(PostProcessPreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        transitionProgress = 1f;
        isTransitioning = false;
        ApplyPreset(preset);
        if (logChanges) Debug.Log($"[PostProcessController] Preview: {preset.stateName}");
    }
    
    #endregion
    
    #region Public API
    
    public void ForceUpdateState()
    {
        if (WorldStateManager.Instance != null)
        {
            OnAtmosphereChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    public PostProcessPreset GetCurrentPreset() => currentPreset;
    
    #endregion
}