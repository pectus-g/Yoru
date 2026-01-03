using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System;

/// <summary>
/// YORU: Post-Process Controller V4 - COMPLETE 27-PRESET SYSTEM
/// 
/// Covers ALL 66 possible ring combinations with 27 presets.
/// 
/// CATEGORIES:
/// - Neutral (1): 0L/0R, 1L/1R, 2L/2R
/// - Sunset (1): 1L/0R, 3L/1R, 4L/2R, 5L/3R, 6L/4R
/// - Sunrise (1): 1L/3R, 2L/4R, 3L/5R, 4L/6R
/// - Dark Path (4): Dark1-2, Dark3-5
/// - Dark Escalation (5): DarkStage1-5
/// - Light Path (4): Light1-2, Light3-5
/// - Light Escalation (5): LightStage1-5
/// - Eclipse Gradual (6): Eclipse_20, 40, 50, 60, 75, Full
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
    // Combos: 0L/0R, 1L/1R, 2L/2R
    // ==========================================
    [Header("=== NEUTRAL ===")]
    [SerializeField] private PostProcessPreset neutralPreset = new PostProcessPreset
    {
        stateName = "Neutral (0L/0R, 1L/1R, 2L/2R)",
        temperature = 0, tint = 0, saturation = 0, contrast = 0,
        bloomIntensity = 0.3f, bloomThreshold = 1f,
        vignetteIntensity = 0.25f, vignetteColor = Color.black
    };
    
    // ==========================================
    // SUNSET (1 preset)
    // Combos: 1L/0R, 3L/1R, 4L/2R, 5L/3R, 6L/4R
    // Warm oranges, golden hour feel
    // ==========================================
    [Header("=== SUNSET ===")]
    [SerializeField] private PostProcessPreset sunsetPreset = new PostProcessPreset
    {
        stateName = "Sunset (1L/0R, 3L/1R, 4L/2R, 5L/3R, 6L/4R)",
        temperature = 35, tint = 15, saturation = 10, contrast = 8,
        bloomIntensity = 0.5f, bloomThreshold = 0.8f, bloomColor = new Color(1f, 0.85f, 0.6f),
        vignetteIntensity = 0.3f, vignetteColor = new Color(0.3f, 0.1f, 0.05f)
    };
    
    // ==========================================
    // SUNRISE (1 preset)
    // Combos: 1L/3R, 2L/4R, 3L/5R, 4L/6R
    // Soft pinks and golds, hopeful
    // ==========================================
    [Header("=== SUNRISE ===")]
    [SerializeField] private PostProcessPreset sunrisePreset = new PostProcessPreset
    {
        stateName = "Sunrise (1L/3R, 2L/4R, 3L/5R, 4L/6R)",
        temperature = 25, tint = -8, saturation = 12, contrast = 5,
        bloomIntensity = 0.45f, bloomThreshold = 0.85f, bloomColor = new Color(1f, 0.9f, 0.8f),
        vignetteIntensity = 0.22f, vignetteColor = new Color(0.2f, 0.1f, 0.15f)
    };
    
    // ==========================================
    // ECLIPSE - GRADUAL (6 presets)
    // Building mystical/supernatural atmosphere
    // ==========================================
    [Header("=== ECLIPSE - GRADUAL ===")]
    [SerializeField] private PostProcessPreset eclipse20Preset = new PostProcessPreset
    {
        stateName = "Eclipse 20% (2L/3R, 3L/2R)",
        temperature = 5, tint = -5, saturation = -2, contrast = 5,
        bloomIntensity = 0.35f, bloomThreshold = 0.9f, bloomColor = new Color(0.9f, 0.85f, 1f),
        vignetteIntensity = 0.28f, vignetteColor = new Color(0.1f, 0.05f, 0.12f)
    };
    
    [SerializeField] private PostProcessPreset eclipse40Preset = new PostProcessPreset
    {
        stateName = "Eclipse 40% (3L/3R)",
        temperature = 3, tint = -8, saturation = -5, contrast = 8,
        bloomIntensity = 0.4f, bloomThreshold = 0.8f, bloomColor = new Color(0.85f, 0.75f, 1f),
        vignetteIntensity = 0.32f, vignetteColor = new Color(0.12f, 0.05f, 0.15f),
        chromaticEnabled = true, chromaticIntensity = 0.03f
    };
    
    [SerializeField] private PostProcessPreset eclipse50Preset = new PostProcessPreset
    {
        stateName = "Eclipse 50% (3L/4R, 4L/3R)",
        temperature = 0, tint = -10, saturation = -8, contrast = 12,
        bloomIntensity = 0.45f, bloomThreshold = 0.75f, bloomColor = new Color(0.8f, 0.65f, 1f),
        vignetteIntensity = 0.36f, vignetteColor = new Color(0.12f, 0.04f, 0.16f),
        chromaticEnabled = true, chromaticIntensity = 0.05f
    };
    
    [SerializeField] private PostProcessPreset eclipse60Preset = new PostProcessPreset
    {
        stateName = "Eclipse 60% (4L/4R)",
        temperature = -2, tint = -12, saturation = -10, contrast = 15,
        bloomIntensity = 0.5f, bloomThreshold = 0.7f, bloomColor = new Color(0.75f, 0.55f, 1f),
        vignetteIntensity = 0.4f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.06f
    };
    
    [SerializeField] private PostProcessPreset eclipse75Preset = new PostProcessPreset
    {
        stateName = "Eclipse 75% (4L/5R, 5L/4R)",
        temperature = 2, tint = -14, saturation = -10, contrast = 18,
        bloomIntensity = 0.55f, bloomThreshold = 0.65f, bloomColor = new Color(0.78f, 0.58f, 1f),
        vignetteIntensity = 0.42f, vignetteColor = new Color(0.12f, 0.04f, 0.18f),
        chromaticEnabled = true, chromaticIntensity = 0.07f
    };
    
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
    
    PostProcessPreset GetPresetForRings(int L, int R)
    {
        int diff = Mathf.Abs(L - R);
        int minRings = Mathf.Min(L, R);
        int maxRings = Mathf.Max(L, R);
        bool darkWinning = L > R;
        bool lightWinning = R > L;
        bool bothHaveRings = L > 0 && R > 0;
        
        // ========================================
        // 1. ECLIPSE STATES (diff ≤ 1, both ≥ 2/3)
        // ========================================
        if (diff <= 1 && minRings >= 2 && maxRings >= 3)
        {
            // 5L/5R = Full
            if (minRings == 5 && maxRings == 5)
                return eclipseFullPreset;
            
            // 5L/4R or 4L/5R = 75%
            if (minRings == 4 && maxRings == 5)
                return eclipse75Preset;
            
            // 4L/4R = 60%
            if (minRings == 4 && maxRings == 4)
                return eclipse60Preset;
            
            // 4L/3R or 3L/4R = 50%
            if (minRings == 3 && maxRings == 4)
                return eclipse50Preset;
            
            // 3L/3R = 40%
            if (minRings == 3 && maxRings == 3)
                return eclipse40Preset;
            
            // 3L/2R or 2L/3R = 20%
            if (minRings == 2 && maxRings == 3)
                return eclipse20Preset;
        }
        
        // ========================================
        // 2. SUNSET (1L/0R OR diff=2 + dark winning + both have rings)
        // ========================================
        if (L == 1 && R == 0)
            return sunsetPreset;
        
        if (diff == 2 && darkWinning && bothHaveRings)
            return sunsetPreset;
        
        // ========================================
        // 3. SUNRISE (diff=2 + light winning + both have rings)
        // ========================================
        if (diff == 2 && lightWinning && bothHaveRings)
            return sunrisePreset;
        
        // ========================================
        // 4. DARK ESCALATION (L ≥ 6, committed to dark)
        // ========================================
        if (L >= 6 && darkWinning)
        {
            int stage = L - 5; // 6L=stage1, 7L=stage2, etc.
            switch (stage)
            {
                case 1: return darkStage1Preset;
                case 2: return darkStage2Preset;
                case 3: return darkStage3Preset;
                case 4: return darkStage4Preset;
                default: return darkStage5Preset;
            }
        }
        
        // ========================================
        // 5. LIGHT ESCALATION (R ≥ 6, committed to light)
        // ========================================
        if (R >= 6 && lightWinning)
        {
            int stage = R - 5; // 6R=stage1, 7R=stage2, etc.
            switch (stage)
            {
                case 1: return lightStage1Preset;
                case 2: return lightStage2Preset;
                case 3: return lightStage3Preset;
                case 4: return lightStage4Preset;
                default: return lightStage5Preset;
            }
        }
        
        // ========================================
        // 6. DARK PATH (diff > 2, dark winning) - NIGHT
        // ========================================
        if (darkWinning && diff > 2)
        {
            // Map to Dark3-5 based on how dark
            if (L >= 5) return dark5Preset;
            if (L >= 4) return dark4Preset;
            return dark3Preset;
        }
        
        // ========================================
        // 7. LIGHT PATH (diff > 2, light winning) - BRIGHT DAY
        // ========================================
        if (lightWinning && diff > 2)
        {
            // Map to Light3-5 based on how bright
            if (R >= 5) return light5Preset;
            if (R >= 4) return light4Preset;
            return light3Preset;
        }
        
        // ========================================
        // 8. MILD DARK (diff 1-2, dark winning, no eclipse)
        // ========================================
        if (darkWinning)
        {
            if (L >= 4) return dark4Preset;
            if (L >= 3) return dark3Preset;
            if (L >= 2) return dark2Preset;
            return dark1Preset;
        }
        
        // ========================================
        // 9. MILD LIGHT (diff 1-2, light winning, no eclipse)
        // ========================================
        if (lightWinning)
        {
            if (R >= 4) return light4Preset;
            if (R >= 3) return light3Preset;
            if (R >= 2) return light2Preset;
            return light1Preset;
        }
        
        // ========================================
        // 10. NEUTRAL (L == R, no eclipse triggered)
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
    
    [ContextMenu("Test: Eclipse 20%")]
    void TestEclipse20() { PreviewPreset(eclipse20Preset); }
    
    [ContextMenu("Test: Eclipse 40%")]
    void TestEclipse40() { PreviewPreset(eclipse40Preset); }
    
    [ContextMenu("Test: Eclipse 50%")]
    void TestEclipse50() { PreviewPreset(eclipse50Preset); }
    
    [ContextMenu("Test: Eclipse 60%")]
    void TestEclipse60() { PreviewPreset(eclipse60Preset); }
    
    [ContextMenu("Test: Eclipse 75%")]
    void TestEclipse75() { PreviewPreset(eclipse75Preset); }
    
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
        Debug.Log("=== ALL 27 POST-PROCESS PRESETS ===");
        Debug.Log($"1. {neutralPreset.stateName}");
        Debug.Log($"2. {sunsetPreset.stateName}");
        Debug.Log($"3. {sunrisePreset.stateName}");
        Debug.Log($"4. {eclipse20Preset.stateName}");
        Debug.Log($"5. {eclipse40Preset.stateName}");
        Debug.Log($"6. {eclipse50Preset.stateName}");
        Debug.Log($"7. {eclipse60Preset.stateName}");
        Debug.Log($"8. {eclipse75Preset.stateName}");
        Debug.Log($"9. {eclipseFullPreset.stateName}");
        Debug.Log($"10. {light1Preset.stateName}");
        Debug.Log($"11. {light2Preset.stateName}");
        Debug.Log($"12. {light3Preset.stateName}");
        Debug.Log($"13. {light4Preset.stateName}");
        Debug.Log($"14. {light5Preset.stateName}");
        Debug.Log($"15. {lightStage1Preset.stateName}");
        Debug.Log($"16. {lightStage2Preset.stateName}");
        Debug.Log($"17. {lightStage3Preset.stateName}");
        Debug.Log($"18. {lightStage4Preset.stateName}");
        Debug.Log($"19. {lightStage5Preset.stateName}");
        Debug.Log($"20. {dark1Preset.stateName}");
        Debug.Log($"21. {dark2Preset.stateName}");
        Debug.Log($"22. {dark3Preset.stateName}");
        Debug.Log($"23. {dark4Preset.stateName}");
        Debug.Log($"24. {dark5Preset.stateName}");
        Debug.Log($"25. {darkStage1Preset.stateName}");
        Debug.Log($"26. {darkStage2Preset.stateName}");
        Debug.Log($"27. {darkStage3Preset.stateName}");
        Debug.Log($"28. {darkStage4Preset.stateName}");
        Debug.Log($"29. {darkStage5Preset.stateName}");
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