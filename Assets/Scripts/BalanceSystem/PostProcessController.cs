using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Controls Post-Processing effects based on karma balance.
/// 
/// Atmosphere Scaling (0-10 rings per tail):
/// 
/// DARK PATH:
/// - 0L = Neutral
/// - 5L = Sunset mood (warm contrast, soft vignette)
/// - 10L = Maximum eerie (desaturated, high contrast, strong vignette)
/// 
/// LIGHT PATH:
/// - 0R = Neutral
/// - 5R = Sunrise mood (warm bloom, soft)
/// - 10R = Maximum heavenly (bright, ethereal bloom)
/// 
/// ECLIPSE (5L + 5R):
/// - Dramatic purple grading, intense vignette, corona bloom
/// 
/// Requires: Post Processing Stack v2 (Built-in Pipeline)
/// </summary>
[RequireComponent(typeof(PostProcessVolume))]
public class PostProcessController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.5f;
    
    [Header("=== NEUTRAL (0 rings, Game Start) ===")]
    [SerializeField] private Color neutralColorFilter = Color.white;
    [SerializeField, Range(-100, 100)] private float neutralSaturation = 0f;
    [SerializeField, Range(-100, 100)] private float neutralContrast = 0f;
    [SerializeField, Range(0, 1)] private float neutralVignette = 0.25f;
    [SerializeField, Range(0, 10)] private float neutralBloom = 1f;
    [SerializeField, Range(-3, 3)] private float neutralExposure = 0f;
    
    [Header("=== DARK PATH: 5 Left Rings (Sunset Mood) ===")]
    [SerializeField] private Color dark5ColorFilter = new Color(1f, 0.85f, 0.7f);  // Warm orange
    [SerializeField, Range(-100, 100)] private float dark5Saturation = 5f;
    [SerializeField, Range(-100, 100)] private float dark5Contrast = 10f;
    [SerializeField, Range(0, 1)] private float dark5Vignette = 0.3f;
    [SerializeField, Range(0, 10)] private float dark5Bloom = 1.5f;
    [SerializeField, Range(-3, 3)] private float dark5Exposure = -0.1f;
    
    [Header("=== DARK PATH: 10 Left Rings (Maximum Eerie) ===")]
    [SerializeField] private Color dark10ColorFilter = new Color(0.7f, 0.75f, 0.9f);  // Cold blue
    [SerializeField, Range(-100, 100)] private float dark10Saturation = -40f;
    [SerializeField, Range(-100, 100)] private float dark10Contrast = 30f;
    [SerializeField, Range(0, 1)] private float dark10Vignette = 0.5f;
    [SerializeField, Range(0, 10)] private float dark10Bloom = 0.5f;
    [SerializeField, Range(-3, 3)] private float dark10Exposure = -0.5f;
    
    [Header("=== LIGHT PATH: 5 Right Rings (Sunrise Mood) ===")]
    [SerializeField] private Color light5ColorFilter = new Color(1f, 0.95f, 0.85f);  // Warm golden
    [SerializeField, Range(-100, 100)] private float light5Saturation = 10f;
    [SerializeField, Range(-100, 100)] private float light5Contrast = -5f;
    [SerializeField, Range(0, 1)] private float light5Vignette = 0.2f;
    [SerializeField, Range(0, 10)] private float light5Bloom = 2f;
    [SerializeField, Range(-3, 3)] private float light5Exposure = 0.1f;
    
    [Header("=== LIGHT PATH: 10 Right Rings (Maximum Heavenly) ===")]
    [SerializeField] private Color light10ColorFilter = new Color(1f, 1f, 0.95f);  // Bright white-gold
    [SerializeField, Range(-100, 100)] private float light10Saturation = 15f;
    [SerializeField, Range(-100, 100)] private float light10Contrast = -10f;
    [SerializeField, Range(0, 1)] private float light10Vignette = 0.1f;
    [SerializeField, Range(0, 10)] private float light10Bloom = 3.5f;
    [SerializeField, Range(-3, 3)] private float light10Exposure = 0.3f;
    
    [Header("=== ECLIPSE (5L + 5R Perfect Balance) ===")]
    [SerializeField] private Color eclipseColorFilter = new Color(0.9f, 0.7f, 1f);  // Purple
    [SerializeField, Range(-100, 100)] private float eclipseSaturation = -15f;
    [SerializeField, Range(-100, 100)] private float eclipseContrast = 35f;
    [SerializeField, Range(0, 1)] private float eclipseVignette = 0.55f;
    [SerializeField, Range(0, 10)] private float eclipseBloom = 4f;
    [SerializeField, Range(-3, 3)] private float eclipseExposure = -0.4f;
    
    #endregion
    
    #region Private State
    
    private struct PostProcessState
    {
        public Color colorFilter;
        public float saturation;
        public float contrast;
        public float vignette;
        public float bloom;
        public float exposure;
        
        public static PostProcessState Lerp(PostProcessState a, PostProcessState b, float t)
        {
            return new PostProcessState
            {
                colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                vignette = Mathf.Lerp(a.vignette, b.vignette, t),
                bloom = Mathf.Lerp(a.bloom, b.bloom, t),
                exposure = Mathf.Lerp(a.exposure, b.exposure, t)
            };
        }
        
        public bool ApproximatelyEquals(PostProcessState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(saturation - other.saturation) < tolerance &&
                   Mathf.Abs(contrast - other.contrast) < tolerance &&
                   Mathf.Abs(vignette - other.vignette) < tolerance &&
                   Mathf.Abs(bloom - other.bloom) < tolerance &&
                   Mathf.Abs(exposure - other.exposure) < tolerance;
        }
    }
    
    private PostProcessState currentState;
    private PostProcessState targetState;
    private bool isTransitioning;
    
    private PostProcessVolume volume;
    private ColorGrading colorGrading;
    private Vignette vignette;
    private Bloom bloom;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (!SetupPostProcessing())
        {
            enabled = false;
            return;
        }
        
        InitializeState();
        SubscribeToEvents();
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
    }
    
    private void Update()
    {
        if (!isTransitioning) return;
        
        float t = transitionSpeed * Time.deltaTime;
        currentState = PostProcessState.Lerp(currentState, targetState, t);
        ApplyState(currentState);
        
        if (currentState.ApproximatelyEquals(targetState))
        {
            currentState = targetState;
            ApplyState(currentState);
            isTransitioning = false;
        }
    }
    
    #endregion
    
    #region Setup
    
    private bool SetupPostProcessing()
    {
        volume = GetComponent<PostProcessVolume>();
        if (volume == null || volume.profile == null)
        {
            Debug.LogError("[PostProcessController] PostProcessVolume with profile required!");
            return false;
        }
        
        var profile = volume.profile;
        
        if (!profile.TryGetSettings(out colorGrading))
            colorGrading = profile.AddSettings<ColorGrading>();
        
        if (!profile.TryGetSettings(out vignette))
            vignette = profile.AddSettings<Vignette>();
        
        if (!profile.TryGetSettings(out bloom))
            bloom = profile.AddSettings<Bloom>();
        
        colorGrading.enabled.Override(true);
        colorGrading.colorFilter.Override(Color.white);
        colorGrading.saturation.Override(0f);
        colorGrading.contrast.Override(0f);
        colorGrading.postExposure.Override(0f);
        
        vignette.enabled.Override(true);
        vignette.intensity.Override(0.25f);
        
        bloom.enabled.Override(true);
        bloom.intensity.Override(1f);
        
        return true;
    }
    
    private void InitializeState()
    {
        currentState = new PostProcessState
        {
            colorFilter = neutralColorFilter,
            saturation = neutralSaturation,
            contrast = neutralContrast,
            vignette = neutralVignette,
            bloom = neutralBloom,
            exposure = neutralExposure
        };
        targetState = currentState;
        ApplyState(currentState);
    }
    
    private void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    #endregion
    
    #region Event Handler
    
    private void OnRingsChanged(int left, int right)
    {
        targetState = CalculateTargetState(left, right);
        isTransitioning = true;
    }
    
    #endregion
    
    #region State Calculation
    
    private PostProcessState CalculateTargetState(int left, int right)
    {
        // Eclipse - Perfect Balance
        if (left == 5 && right == 5)
        {
            return new PostProcessState
            {
                colorFilter = eclipseColorFilter,
                saturation = eclipseSaturation,
                contrast = eclipseContrast,
                vignette = eclipseVignette,
                bloom = eclipseBloom,
                exposure = eclipseExposure
            };
        }
        
        // Calculate dark contribution
        PostProcessState darkState = CalculateDarkState(left);
        
        // Calculate light contribution
        PostProcessState lightState = CalculateLightState(right);
        
        // Blend based on ring counts
        int total = left + right;
        if (total == 0)
        {
            return new PostProcessState
            {
                colorFilter = neutralColorFilter,
                saturation = neutralSaturation,
                contrast = neutralContrast,
                vignette = neutralVignette,
                bloom = neutralBloom,
                exposure = neutralExposure
            };
        }
        
        float leftWeight = (float)left / total;
        return PostProcessState.Lerp(lightState, darkState, leftWeight);
    }
    
    private PostProcessState CalculateDarkState(int leftRings)
    {
        if (leftRings <= 0)
        {
            return new PostProcessState
            {
                colorFilter = neutralColorFilter,
                saturation = neutralSaturation,
                contrast = neutralContrast,
                vignette = neutralVignette,
                bloom = neutralBloom,
                exposure = neutralExposure
            };
        }
        
        if (leftRings <= 5)
        {
            // Phase 1: Neutral → Sunset (0-5)
            float t = leftRings / 5f;
            return new PostProcessState
            {
                colorFilter = Color.Lerp(neutralColorFilter, dark5ColorFilter, t),
                saturation = Mathf.Lerp(neutralSaturation, dark5Saturation, t),
                contrast = Mathf.Lerp(neutralContrast, dark5Contrast, t),
                vignette = Mathf.Lerp(neutralVignette, dark5Vignette, t),
                bloom = Mathf.Lerp(neutralBloom, dark5Bloom, t),
                exposure = Mathf.Lerp(neutralExposure, dark5Exposure, t)
            };
        }
        else
        {
            // Phase 2: Sunset → Maximum Eerie (5-10)
            float t = (leftRings - 5) / 5f;
            return new PostProcessState
            {
                colorFilter = Color.Lerp(dark5ColorFilter, dark10ColorFilter, t),
                saturation = Mathf.Lerp(dark5Saturation, dark10Saturation, t),
                contrast = Mathf.Lerp(dark5Contrast, dark10Contrast, t),
                vignette = Mathf.Lerp(dark5Vignette, dark10Vignette, t),
                bloom = Mathf.Lerp(dark5Bloom, dark10Bloom, t),
                exposure = Mathf.Lerp(dark5Exposure, dark10Exposure, t)
            };
        }
    }
    
    private PostProcessState CalculateLightState(int rightRings)
    {
        if (rightRings <= 0)
        {
            return new PostProcessState
            {
                colorFilter = neutralColorFilter,
                saturation = neutralSaturation,
                contrast = neutralContrast,
                vignette = neutralVignette,
                bloom = neutralBloom,
                exposure = neutralExposure
            };
        }
        
        if (rightRings <= 5)
        {
            // Phase 1: Neutral → Sunrise (0-5)
            float t = rightRings / 5f;
            return new PostProcessState
            {
                colorFilter = Color.Lerp(neutralColorFilter, light5ColorFilter, t),
                saturation = Mathf.Lerp(neutralSaturation, light5Saturation, t),
                contrast = Mathf.Lerp(neutralContrast, light5Contrast, t),
                vignette = Mathf.Lerp(neutralVignette, light5Vignette, t),
                bloom = Mathf.Lerp(neutralBloom, light5Bloom, t),
                exposure = Mathf.Lerp(neutralExposure, light5Exposure, t)
            };
        }
        else
        {
            // Phase 2: Sunrise → Maximum Heavenly (5-10)
            float t = (rightRings - 5) / 5f;
            return new PostProcessState
            {
                colorFilter = Color.Lerp(light5ColorFilter, light10ColorFilter, t),
                saturation = Mathf.Lerp(light5Saturation, light10Saturation, t),
                contrast = Mathf.Lerp(light5Contrast, light10Contrast, t),
                vignette = Mathf.Lerp(light5Vignette, light10Vignette, t),
                bloom = Mathf.Lerp(light5Bloom, light10Bloom, t),
                exposure = Mathf.Lerp(light5Exposure, light10Exposure, t)
            };
        }
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(PostProcessState state)
    {
        if (colorGrading != null)
        {
            colorGrading.colorFilter.value = state.colorFilter;
            colorGrading.saturation.value = state.saturation;
            colorGrading.contrast.value = state.contrast;
            colorGrading.postExposure.value = state.exposure;
        }
        
        if (vignette != null)
            vignette.intensity.value = state.vignette;
        
        if (bloom != null)
            bloom.intensity.value = state.bloom;
    }
    
    #endregion
    
    #region Public API
    
    public void SnapToCurrentState()
    {
        if (WorldStateManager.Instance == null) return;
        
        targetState = CalculateTargetState(
            WorldStateManager.Instance.LeftRings,
            WorldStateManager.Instance.RightRings
        );
        currentState = targetState;
        ApplyState(currentState);
        isTransitioning = false;
    }
    
    #endregion
}