using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using System;

/// <summary>
/// YORU: Post-Process Controller V5 - DIFF-BASED CASCADE (matches WorldStateManager)
/// 
/// Covers ALL 66 possible ring combinations with 28 presets.
/// State resolution is determined by abs(L - R) (the diff), with Eclipse and
/// Sunset/Sunrise as priority overrides at specific combos. Right rings cancel
/// left symmetrically.
/// 
/// CASCADE (first match wins):
///   1. Eclipse     min(L,R) >= 2 AND diff <= 1                (7-stage: 15/25/40/55/70/85/100)
///   2. Sunset      (1L/0R) OR (diff=2, dark wins, both have rings, max <= 4)
///   3. Sunrise     (0L/1R) OR (diff=2, light wins, both have rings, max <= 4)
///   4. Escalation  diff >= 6  -> DarkStage(diff-5) or LightStage(diff-5)
///   5. Path        diff 1-5   -> Dark(diff) or Light(diff)
///   6. Neutral     L == R (and not eclipse)
/// 
/// CATEGORIES:
/// - Neutral (1):           0L/0R, 1L/1R
/// - Sunset (1):            1L/0R, 3L/1R, 4L/2R
/// - Sunrise (1):           0L/1R, 1L/3R, 2L/4R
/// - Dark Path (5):         Dark1-5 (diff 1-5, dark winning)
/// - Dark Escalation (5):   DarkStage1-5 (diff 6-10, dark winning)
/// - Light Path (5):        Light1-5 (diff 1-5, light winning)
/// - Light Escalation (5):  LightStage1-5 (diff 6-10, light winning)
/// - Eclipse Gradual (7):   Eclipse 15, 25, 40, 55, 70, 85, Full
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
    
    // ==========================================
    // NEUTRAL (1 preset)
    // Combos: 0L/0R, 1L/1R
    // Note: 2L/2R is now Eclipse Stage 1 (15%), not Neutral.
    // ==========================================
    [Header("=== NEUTRAL ===")]
    [SerializeField] private PostProcessPreset neutralPreset = new PostProcessPreset
    {
        stateName = "Neutral (0L/0R, 1L/1R)",
        temperature = 0, tint = 0, saturation = 0, contrast = 0,
        bloomIntensity = 0.3f, bloomThreshold = 1f,
        vignetteIntensity = 0.25f, vignetteColor = Color.black
    };
    
    // ==========================================
    // SUNSET (1 preset)
    // Combos: 1L/0R, 3L/1R, 4L/2R (max <= 4 cap)
    // Warm oranges, golden hour feel
    // ==========================================
    [Header("=== SUNSET ===")]
    [SerializeField] private PostProcessPreset sunsetPreset = new PostProcessPreset
    {
        stateName = "Sunset (1L/0R, 3L/1R, 4L/2R)",
        temperature = 35, tint = 15, saturation = 10, contrast = 8,
        bloomIntensity = 0.5f, bloomThreshold = 0.8f, bloomColor = new Color(1f, 0.85f, 0.6f),
        vignetteIntensity = 0.3f, vignetteColor = new Color(0.3f, 0.1f, 0.05f)
    };
    
    // ==========================================
    // SUNRISE (1 preset)
    // Combos: 0L/1R, 1L/3R, 2L/4R (max <= 4 cap)
    // Soft pinks and golds, hopeful
    // ==========================================
    [Header("=== SUNRISE ===")]
    [SerializeField] private PostProcessPreset sunrisePreset = new PostProcessPreset
    {
        stateName = "Sunrise (0L/1R, 1L/3R, 2L/4R)",
        temperature = 25, tint = -8, saturation = 12, contrast = 5,
        bloomIntensity = 0.45f, bloomThreshold = 0.85f, bloomColor = new Color(1f, 0.9f, 0.8f),
        vignetteIntensity = 0.22f, vignetteColor = new Color(0.2f, 0.1f, 0.15f)
    };
    
    // ==========================================
    // ECLIPSE - 7-STAGE GRADUAL (7 presets)
    // Stage 1 (15%): 2L/2R                                  - subtle purple wash
    // Stage 2 (25%): 2L/3R, 3L/2R                           - mystical hint
    // Stage 3 (40%): 3L/3R                                  - clear eclipse
    // Stage 4 (55%): 3L/4R, 4L/3R                           - strong eclipse
    // Stage 5 (70%): 4L/4R                                  - deepening
    // Stage 6 (85%): 4L/5R, 5L/4R                           - near total
    // Stage 7 (100%): 5L/5R                                 - full eclipse
    // ==========================================
    [Header("=== ECLIPSE - 7-STAGE GRADUAL ===")]
    [Tooltip("Stage 1 (15%) - 2L/2R - subtle mystical wash")]
    [SerializeField] private PostProcessPreset eclipse15Preset = new PostProcessPreset
    {
        stateName = "Eclipse 15% (2L/2R)",
        temperature = 8, tint = -3, saturation = 0, contrast = 3,
        bloomIntensity = 0.32f, bloomThreshold = 0.95f, bloomColor = new Color(0.92f, 0.88f, 1f),
        vignetteIntensity = 0.26f, vignetteColor = new Color(0.08f, 0.06f, 0.12f)
    };
    
    [FormerlySerializedAs("eclipse20Preset")]
    [Tooltip("Stage 2 (25%) - 2L/3R or 3L/2R")]
    [SerializeField] private PostProcessPreset eclipse25Preset = new PostProcessPreset
    {
        stateName = "Eclipse 25% (2L/3R, 3L/2R)",
        temperature = 5, tint = -5, saturation = -2, contrast = 5,
        bloomIntensity = 0.35f, bloomThreshold = 0.9f, bloomColor = new Color(0.9f, 0.85f, 1f),
        vignetteIntensity = 0.28f, vignetteColor = new Color(0.1f, 0.05f, 0.12f)
    };
    
    [Tooltip("Stage 3 (40%) - 3L/3R")]
    [SerializeField] private PostProcessPreset eclipse40Preset = new PostProcessPreset
    {
        stateName = "Eclipse 40% (3L/3R)",
        temperature = 3, tint = -8, saturation = -5, contrast = 8,
        bloomIntensity = 0.4f, bloomThreshold = 0.8f, bloomColor = new Color(0.85f, 0.75f, 1f),
        vignetteIntensity = 0.32f, vignetteColor = new Color(0.12f, 0.05f, 0.15f),
        chromaticEnabled = true, chromaticIntensity = 0.03f
    };
    
    [FormerlySerializedAs("eclipse50Preset")]
    [Tooltip("Stage 4 (55%) - 3L/4R or 4L/3R")]
    [SerializeField] private PostProcessPreset eclipse55Preset = new PostProcessPreset
    {
        stateName = "Eclipse 55% (3L/4R, 4L/3R)",
        temperature = 0, tint = -10, saturation = -8, contrast = 12,
        bloomIntensity = 0.45f, bloomThreshold = 0.75f, bloomColor = new Color(0.8f, 0.65f, 1f),
        vignetteIntensity = 0.36f, vignetteColor = new Color(0.12f, 0.04f, 0.16f),
        chromaticEnabled = true, chromaticIntensity = 0.05f
    };
    
    [FormerlySerializedAs("eclipse60Preset")]
    [Tooltip("Stage 5 (70%) - 4L/4R")]
    [SerializeField] private PostProcessPreset eclipse70Preset = new PostProcessPreset
    {
        stateName = "Eclipse 70% (4L/4R)",
        temperature = -2, tint = -12, saturation = -10, contrast = 15,
        bloomIntensity = 0.5f, bloomThreshold = 0.7f, bloomColor = new Color(0.75f, 0.55f, 1f),
        vignetteIntensity = 0.4f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.06f
    };
    
    [FormerlySerializedAs("eclipse75Preset")]
    [Tooltip("Stage 6 (85%) - 4L/5R or 5L/4R")]
    [SerializeField] private PostProcessPreset eclipse85Preset = new PostProcessPreset
    {
        stateName = "Eclipse 85% (4L/5R, 5L/4R)",
        temperature = 2, tint = -14, saturation = -10, contrast = 18,
        bloomIntensity = 0.55f, bloomThreshold = 0.65f, bloomColor = new Color(0.78f, 0.58f, 1f),
        vignetteIntensity = 0.42f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.07f
    };
    
    [Tooltip("Stage 7 (100%) - 5L/5R - full eclipse, Secret Realm portal")]
    [SerializeField] private PostProcessPreset eclipseFullPreset = new PostProcessPreset
    {
        stateName = "Eclipse FULL 100% (5L/5R)",
        temperature = 5, tint = -18, saturation = -12, contrast = 22,
        bloomIntensity = 0.6f, bloomThreshold = 0.6f, bloomColor = new Color(0.8f, 0.6f, 1f),
        vignetteIntensity = 0.45f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.08f
    };
    
    // ==========================================
    // LIGHT PATH (5 presets)
    // Warm, bright, hopeful progression
    // ==========================================
    [Header("=== LIGHT PATH ===")]
    [SerializeField] private PostProcessPreset light1Preset = new PostProcessPreset
    {
        stateName = "Light1 (0L/1R, 1L/2R)",
        temperature = 8, saturation = 5, contrast = 2,
        bloomIntensity = 0.35f, bloomThreshold = 0.95f,
        vignetteIntensity = 0.22f
    };
    
    [SerializeField] private PostProcessPreset light2Preset = new PostProcessPreset
    {
        stateName = "Light2 (0L/2R)",
        temperature = 12, saturation = 8, contrast = 3,
        bloomIntensity = 0.4f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.2f
    };
    
    [SerializeField] private PostProcessPreset light3Preset = new PostProcessPreset
    {
        stateName = "Light3 (0L/3R, 1L/4R, 2L/5R, 3L/6R)",
        temperature = 16, saturation = 10, contrast = 4,
        bloomIntensity = 0.45f, bloomThreshold = 0.85f,
        vignetteIntensity = 0.18f
    };
    
    [SerializeField] private PostProcessPreset light4Preset = new PostProcessPreset
    {
        stateName = "Light4 (0L/4R, 1L/5R, 2L/6R, 3L/7R)",
        temperature = 20, saturation = 12, contrast = 5,
        bloomIntensity = 0.5f, bloomThreshold = 0.8f,
        vignetteIntensity = 0.15f
    };
    
    [SerializeField] private PostProcessPreset light5Preset = new PostProcessPreset
    {
        stateName = "Light5 (0L/5R, 1L/6R, 2L/7R)",
        temperature = 25, tint = 2, saturation = 15, contrast = 6,
        bloomIntensity = 0.6f, bloomThreshold = 0.75f,
        vignetteIntensity = 0.12f, vignetteColor = new Color(1f, 0.9f, 0.7f, 1f)
    };
    
    // ==========================================
    // LIGHT ESCALATION (5 presets)
    // Divine, heavenly, overwhelming light
    // ==========================================
    [Header("=== LIGHT ESCALATION (Divine) ===")]
    [SerializeField] private PostProcessPreset lightStage1Preset = new PostProcessPreset
    {
        stateName = "Light+Stage1 (0L/6R, 1L/7R, 2L/8R)",
        temperature = 28, tint = 3, saturation = 16, contrast = 7,
        bloomIntensity = 0.7f, bloomThreshold = 0.7f,
        vignetteIntensity = 0.1f, vignetteColor = new Color(1f, 0.84f, 0f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage2Preset = new PostProcessPreset
    {
        stateName = "Light+Stage2 (0L/7R, 1L/8R)",
        temperature = 32, tint = 4, saturation = 18, contrast = 8,
        bloomIntensity = 0.8f, bloomThreshold = 0.65f,
        vignetteIntensity = 0.08f, vignetteColor = new Color(1f, 0.78f, 0.14f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage3Preset = new PostProcessPreset
    {
        stateName = "Light+Stage3 (0L/8R, 1L/9R)",
        temperature = 36, tint = 5, saturation = 20, contrast = 10,
        bloomIntensity = 0.9f, bloomThreshold = 0.6f,
        vignetteIntensity = 0.06f, vignetteColor = new Color(1f, 0.71f, 0.28f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage4Preset = new PostProcessPreset
    {
        stateName = "Light+Stage4 (0L/9R)",
        temperature = 42, tint = 6, saturation = 22, contrast = 12,
        bloomIntensity = 1.0f, bloomThreshold = 0.55f,
        vignetteIntensity = 0.04f, vignetteColor = new Color(1f, 0.66f, 0.42f, 1f)
    };
    
    [SerializeField] private PostProcessPreset lightStage5Preset = new PostProcessPreset
    {
        stateName = "Light+Stage5 (0L/10R) MAXIMUM DIVINE",
        temperature = 50, tint = 8, saturation = 25, contrast = 15,
        bloomIntensity = 1.2f, bloomThreshold = 0.5f, bloomColor = new Color(1f, 0.95f, 0.85f),
        vignetteIntensity = 0.02f, vignetteColor = new Color(1f, 0.6f, 0.56f, 1f)
    };
    
    // ==========================================
    // DARK PATH (5 presets)
    // Cool, desaturated, dramatic progression
    // ==========================================
    [Header("=== DARK PATH ===")]
    [SerializeField] private PostProcessPreset dark1Preset = new PostProcessPreset
    {
        stateName = "Dark1 (2L/1R)",
        temperature = -5, tint = 2, saturation = -3, contrast = 3,
        bloomIntensity = 0.28f, bloomThreshold = 1f,
        vignetteIntensity = 0.28f, vignetteColor = new Color(0.1f, 0.06f, 0.12f)
    };
    
    [SerializeField] private PostProcessPreset dark2Preset = new PostProcessPreset
    {
        stateName = "Dark2 (2L/0R)",
        temperature = -8, tint = 4, saturation = -6, contrast = 5,
        bloomIntensity = 0.25f, bloomThreshold = 1f,
        vignetteIntensity = 0.32f, vignetteColor = new Color(0.15f, 0.06f, 0.17f)
    };
    
    [SerializeField] private PostProcessPreset dark3Preset = new PostProcessPreset
    {
        stateName = "Dark3 (3L/0R, 4L/1R)",
        temperature = -12, tint = 6, saturation = -10, contrast = 8,
        bloomIntensity = 0.3f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.36f, vignetteColor = new Color(0.2f, 0.06f, 0.22f)
    };
    
    [SerializeField] private PostProcessPreset dark4Preset = new PostProcessPreset
    {
        stateName = "Dark4 (4L/0R, 5L/1R, 5L/2R, 6L/2R, 6L/3R)",
        temperature = -16, tint = 8, saturation = -15, contrast = 10,
        bloomIntensity = 0.35f, bloomThreshold = 0.85f,
        vignetteIntensity = 0.4f, vignetteColor = new Color(0.25f, 0.06f, 0.27f)
    };
    
    [SerializeField] private PostProcessPreset dark5Preset = new PostProcessPreset
    {
        stateName = "Dark5 (5L/0R, 6L/1R, 7L/2R, 7L/3R) - Midnight Visible",
        temperature = -20, tint = 10, saturation = -20, contrast = 15,
        bloomIntensity = 0.4f, bloomThreshold = 0.7f,
        vignetteIntensity = 0.35f, vignetteColor = new Color(0.05f, 0.03f, 0.09f)
    };
    
    // ==========================================
    // DARK ESCALATION (5 presets)
    // Stormy, ominous, overwhelming darkness
    // ==========================================
    [Header("=== DARK ESCALATION (Stormy) ===")]
    [SerializeField] private PostProcessPreset darkStage1Preset = new PostProcessPreset
    {
        stateName = "Dark+Stage1 (6L/0R, 7L/1R, 8L/2R) Partly Cloudy",
        temperature = -22, tint = 11, saturation = -22, contrast = 16,
        bloomIntensity = 0.35f, bloomThreshold = 0.75f,
        vignetteIntensity = 0.38f, vignetteColor = new Color(0.05f, 0.03f, 0.09f),
        chromaticEnabled = true, chromaticIntensity = 0.05f
    };
    
    [SerializeField] private PostProcessPreset darkStage2Preset = new PostProcessPreset
    {
        stateName = "Dark+Stage2 (7L/0R, 8L/1R, 9L/2R) Overcast",
        temperature = -25, tint = 12, saturation = -25, contrast = 18,
        bloomIntensity = 0.32f, bloomThreshold = 0.8f,
        vignetteIntensity = 0.42f, vignetteColor = new Color(0.04f, 0.02f, 0.08f),
        chromaticEnabled = true, chromaticIntensity = 0.1f
    };
    
    [SerializeField] private PostProcessPreset darkStage3Preset = new PostProcessPreset
    {
        stateName = "Dark+Stage3 (8L/0R, 9L/1R) Light Rain",
        temperature = -28, tint = 14, saturation = -28, contrast = 20,
        bloomIntensity = 0.28f, bloomThreshold = 0.85f,
        vignetteIntensity = 0.45f, vignetteColor = new Color(0.04f, 0.02f, 0.07f),
        chromaticEnabled = true, chromaticIntensity = 0.15f,
        grainEnabled = true, grainIntensity = 0.1f
    };
    
    [SerializeField] private PostProcessPreset darkStage4Preset = new PostProcessPreset
    {
        stateName = "Dark+Stage4 (9L/0R) Heavy Rain",
        temperature = -32, tint = 16, saturation = -32, contrast = 24,
        bloomIntensity = 0.25f, bloomThreshold = 0.9f,
        vignetteIntensity = 0.5f, vignetteColor = new Color(0.03f, 0.02f, 0.06f),
        chromaticEnabled = true, chromaticIntensity = 0.25f,
        grainEnabled = true, grainIntensity = 0.2f
    };
    
    [SerializeField] private PostProcessPreset darkStage5Preset = new PostProcessPreset
    {
        stateName = "Dark+Stage5 (10L/0R) THUNDERSTORM",
        temperature = -40, tint = 20, saturation = -40, contrast = 30,
        bloomIntensity = 0.22f, bloomThreshold = 0.95f,
        vignetteIntensity = 0.55f, vignetteColor = new Color(0.02f, 0.01f, 0.05f),
        chromaticEnabled = true, chromaticIntensity = 0.4f,
        grainEnabled = true, grainIntensity = 0.3f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private int currentLeftRings;
    [SerializeField] private int currentRightRings;
    [SerializeField] private string currentPresetName;
    
    #endregion
    
    #region Private Fields
    
    private PostProcessPreset currentPreset;
    private PostProcessPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    
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
        
        if (!volume.profile.TryGetSettings(out colorGrading))
            colorGrading = volume.profile.AddSettings<ColorGrading>();
        colorGrading.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out bloom))
            bloom = volume.profile.AddSettings<Bloom>();
        bloom.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out vignette))
            vignette = volume.profile.AddSettings<Vignette>();
        vignette.enabled.Override(true);
        
        if (!volume.profile.TryGetSettings(out chromaticAberration))
            chromaticAberration = volume.profile.AddSettings<ChromaticAberration>();
        
        if (!volume.profile.TryGetSettings(out grain))
            grain = volume.profile.AddSettings<Grain>();
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
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            
            // Initialize with current state
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnRingsChanged(int leftRings, int rightRings)
    {
        currentLeftRings = leftRings;
        currentRightRings = rightRings;
        UpdateTargetPreset();
    }
    
    void UpdateTargetPreset()
    {
        targetPreset = GetPresetForRings(currentLeftRings, currentRightRings);
        currentPresetName = targetPreset.stateName;
        transitionProgress = 0f;
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[PostProcessController] {currentLeftRings}L/{currentRightRings}R → {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution - THE BRAIN
    
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
    PostProcessPreset GetPresetForRings(int L, int R)
    {
        int diff = Mathf.Abs(L - R);
        int minRings = Mathf.Min(L, R);
        int maxRings = Mathf.Max(L, R);
        bool darkWinning = L > R;
        bool lightWinning = R > L;
        bool bothHaveRings = L > 0 && R > 0;
        
        // ========================================
        // PRIORITY 1: ECLIPSE (7 stages, see GDD §5)
        // Trigger: min(L,R) >= 2 AND diff <= 1
        // ========================================
        if (minRings >= 2 && diff <= 1)
        {
            if (L == 5 && R == 5)                       return eclipseFullPreset; // Stage 7 (100%)
            if (minRings == 4 && maxRings == 5)         return eclipse85Preset;   // Stage 6 (85%)
            if (L == 4 && R == 4)                       return eclipse70Preset;   // Stage 5 (70%)
            if (minRings == 3 && maxRings == 4)         return eclipse55Preset;   // Stage 4 (55%)
            if (L == 3 && R == 3)                       return eclipse40Preset;   // Stage 3 (40%)
            if (minRings == 2 && maxRings == 3)         return eclipse25Preset;   // Stage 2 (25%)
            if (L == 2 && R == 2)                       return eclipse15Preset;   // Stage 1 (15%)
        }
        
        // ========================================
        // PRIORITY 2: SUNSET
        // Trigger: (1L/0R) OR (diff=2, darkWinning, bothHaveRings, max<=4)
        // ========================================
        if ((L == 1 && R == 0) ||
            (diff == 2 && darkWinning && bothHaveRings && maxRings <= 4))
        {
            return sunsetPreset;
        }
        
        // ========================================
        // PRIORITY 3: SUNRISE
        // Trigger: (0L/1R) OR (diff=2, lightWinning, bothHaveRings, max<=4)
        // ========================================
        if ((L == 0 && R == 1) ||
            (diff == 2 && lightWinning && bothHaveRings && maxRings <= 4))
        {
            return sunrisePreset;
        }
        
        // ========================================
        // PRIORITY 4: ESCALATION (diff >= 6)
        // Stage = diff - 5, clamped 1-5
        // ========================================
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
        
        // ========================================
        // PRIORITY 5: PATH (diff 1-5)
        // State driven by diff alone. Right rings cancel left symmetrically.
        // ========================================
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
        
        // ========================================
        // PRIORITY 6: NEUTRAL (L == R, eclipse already handled above)
        // ========================================
        return neutralPreset;
    }
    
    #endregion
    
    #region Apply Preset
    
    void ApplyPreset(PostProcessPreset preset)
    {
        if (colorGrading != null)
        {
            colorGrading.enabled.Override(preset.colorGradingEnabled);
            colorGrading.temperature.Override(preset.temperature);
            colorGrading.tint.Override(preset.tint);
            colorGrading.saturation.Override(preset.saturation);
            colorGrading.contrast.Override(preset.contrast);
        }
        
        if (bloom != null)
        {
            bloom.enabled.Override(preset.bloomEnabled);
            bloom.intensity.Override(preset.bloomIntensity);
            bloom.threshold.Override(preset.bloomThreshold);
            bloom.color.Override(preset.bloomColor);
        }
        
        if (vignette != null)
        {
            vignette.enabled.Override(preset.vignetteEnabled);
            vignette.intensity.Override(preset.vignetteIntensity);
            vignette.smoothness.Override(preset.vignetteSmoothness);
            vignette.color.Override(preset.vignetteColor);
        }
        
        if (chromaticAberration != null)
        {
            chromaticAberration.enabled.Override(preset.chromaticEnabled);
            chromaticAberration.intensity.Override(preset.chromaticIntensity);
        }
        
        if (grain != null)
        {
            grain.enabled.Override(preset.grainEnabled);
            grain.intensity.Override(preset.grainIntensity);
        }
    }
    
    #endregion
    
    #region Context Menu (For Testing)
    
    [ContextMenu("Test: Neutral (0L/0R)")]
    void TestNeutral() { PreviewPreset(neutralPreset); }
    
    [ContextMenu("Test: Sunset")]
    void TestSunset() { PreviewPreset(sunsetPreset); }
    
    [ContextMenu("Test: Sunrise")]
    void TestSunrise() { PreviewPreset(sunrisePreset); }
    
    [ContextMenu("Test: Eclipse 15%")]
    void TestEclipse15() { PreviewPreset(eclipse15Preset); }
    
    [ContextMenu("Test: Eclipse 25%")]
    void TestEclipse25() { PreviewPreset(eclipse25Preset); }
    
    [ContextMenu("Test: Eclipse 40%")]
    void TestEclipse40() { PreviewPreset(eclipse40Preset); }
    
    [ContextMenu("Test: Eclipse 55%")]
    void TestEclipse55() { PreviewPreset(eclipse55Preset); }
    
    [ContextMenu("Test: Eclipse 70%")]
    void TestEclipse70() { PreviewPreset(eclipse70Preset); }
    
    [ContextMenu("Test: Eclipse 85%")]
    void TestEclipse85() { PreviewPreset(eclipse85Preset); }
    
    [ContextMenu("Test: Eclipse FULL")]
    void TestEclipseFull() { PreviewPreset(eclipseFullPreset); }
    
    [ContextMenu("Test: Dark5 (Midnight)")]
    void TestDark5() { PreviewPreset(dark5Preset); }
    
    [ContextMenu("Test: Dark+Stage5 (Thunderstorm)")]
    void TestDarkMax() { PreviewPreset(darkStage5Preset); }
    
    [ContextMenu("Test: Light5 (Heavenly)")]
    void TestLight5() { PreviewPreset(light5Preset); }
    
    [ContextMenu("Test: Light+Stage5 (Divine)")]
    void TestLightMax() { PreviewPreset(lightStage5Preset); }
    
    void PreviewPreset(PostProcessPreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        transitionProgress = 1f;
        isTransitioning = false;
        ApplyPreset(preset);
        currentPresetName = preset.stateName;
        Debug.Log($"[PostProcessController] Preview: {preset.stateName}");
    }
    
    [ContextMenu("Print All Presets")]
    void PrintAllPresets()
    {
        Debug.Log("=== ALL 28 POST-PROCESS PRESETS ===");
        Debug.Log($"1. {neutralPreset.stateName}");
        Debug.Log($"2. {sunsetPreset.stateName}");
        Debug.Log($"3. {sunrisePreset.stateName}");
        Debug.Log($"4. {eclipse15Preset.stateName}");
        Debug.Log($"5. {eclipse25Preset.stateName}");
        Debug.Log($"6. {eclipse40Preset.stateName}");
        Debug.Log($"7. {eclipse55Preset.stateName}");
        Debug.Log($"8. {eclipse70Preset.stateName}");
        Debug.Log($"9. {eclipse85Preset.stateName}");
        Debug.Log($"10. {eclipseFullPreset.stateName}");
        Debug.Log($"11. {light1Preset.stateName}");
        Debug.Log($"12. {light2Preset.stateName}");
        Debug.Log($"13. {light3Preset.stateName}");
        Debug.Log($"14. {light4Preset.stateName}");
        Debug.Log($"15. {light5Preset.stateName}");
        Debug.Log($"16. {lightStage1Preset.stateName}");
        Debug.Log($"17. {lightStage2Preset.stateName}");
        Debug.Log($"18. {lightStage3Preset.stateName}");
        Debug.Log($"19. {lightStage4Preset.stateName}");
        Debug.Log($"20. {lightStage5Preset.stateName}");
        Debug.Log($"21. {dark1Preset.stateName}");
        Debug.Log($"22. {dark2Preset.stateName}");
        Debug.Log($"23. {dark3Preset.stateName}");
        Debug.Log($"24. {dark4Preset.stateName}");
        Debug.Log($"25. {dark5Preset.stateName}");
        Debug.Log($"26. {darkStage1Preset.stateName}");
        Debug.Log($"27. {darkStage2Preset.stateName}");
        Debug.Log($"28. {darkStage3Preset.stateName}");
        Debug.Log($"29. {darkStage4Preset.stateName}");
        Debug.Log($"30. {darkStage5Preset.stateName}");
        Debug.Log("===================================");
    }
    
    #endregion
    
    #region Public API
    
    public void ForceUpdateState()
    {
        if (WorldStateManager.Instance != null)
        {
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    public PostProcessPreset GetCurrentPreset() => currentPreset;
    public string GetCurrentPresetName() => currentPresetName;
    
    #endregion
}