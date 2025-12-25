using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// YORU: Post-Process Controller - V2 (Balance System)
/// Controls Post-Processing effects based on karma BALANCE (not individual ring counts).
/// 
/// TWO-LAYER SYSTEM:
/// Layer 1 (Atmosphere): Balance-based, caps at ±5
/// Layer 2 (Weather): Handled by WeatherIntensityController
/// 
/// BALANCE STATES:
/// -5 or less = Dark5 (Midnight, maximum eerie)
/// -4 = Dark4
/// -3 = Dark3
/// -2 = Dark2
/// -1 = Dark1
/// 0 = Neutral
/// +1 = Light1
/// +2 = Light2
/// +3 = Light3
/// +4 = Light4
/// +5 or more = Light5 (Maximum heavenly)
/// Eclipse (5L+5R) = Special dramatic effect
/// 
/// Requires: Post Processing Stack v2 (Built-in Pipeline)
/// </summary>
[RequireComponent(typeof(PostProcessVolume))]
public class PostProcessController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private PostProcessVolume volume;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] 
    private float transitionDuration = 2f;
    
    [Header("=== CURRENT STATE (Debug) ===")]
    [SerializeField] private WorldStateManager.AtmosphereState currentState;
    [SerializeField] private float transitionProgress = 1f;
    
    [Header("=== DARK 5 (Midnight Eerie) ===")]
    [SerializeField] private PostProcessSettings dark5 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -20f,      // Less extreme cold
        tint = 10f,
        saturation = -20f,       // Not so desaturated
        contrast = 15f,          // Less contrast so shadows aren't black
        
        vignetteEnabled = true,
        vignetteIntensity = 0.35f, // Less heavy
        vignetteSmoothness = 0.5f,
        vignetteColor = new Color(0.1f, 0.05f, 0.15f),
        
        bloomEnabled = true,
        bloomIntensity = 0.4f,   // More bloom so lights visible
        bloomThreshold = 0.7f    // Lower threshold = more glow
    };
    
    [Header("=== DARK 4 ===")]
    [SerializeField] private PostProcessSettings dark4 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -18f,
        tint = 8f,
        saturation = -18f,
        contrast = 12f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.3f,
        vignetteSmoothness = 0.45f,
        vignetteColor = new Color(0.1f, 0.05f, 0.15f),
        
        bloomEnabled = true,
        bloomIntensity = 0.35f,
        bloomThreshold = 0.8f
    };
    
    [Header("=== DARK 3 ===")]
    [SerializeField] private PostProcessSettings dark3 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -15f,
        tint = 5f,
        saturation = -15f,
        contrast = 10f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.25f,
        vignetteSmoothness = 0.4f,
        vignetteColor = new Color(0.1f, 0.05f, 0.15f),
        
        bloomEnabled = true,
        bloomIntensity = 0.3f,
        bloomThreshold = 0.85f
    };
    
    [Header("=== DARK 2 ===")]
    [SerializeField] private PostProcessSettings dark2 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -12f,
        tint = 3f,
        saturation = -10f,
        contrast = 12f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.25f,
        vignetteSmoothness = 0.25f,
        vignetteColor = new Color(0.15f, 0.1f, 0.2f),
        
        bloomEnabled = true,
        bloomIntensity = 0.15f,
        bloomThreshold = 0.95f
    };
    
    [Header("=== DARK 1 ===")]
    [SerializeField] private PostProcessSettings dark1 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -5f,
        tint = 2f,
        saturation = -5f,
        contrast = 5f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.15f,
        vignetteSmoothness = 0.2f,
        vignetteColor = new Color(0.2f, 0.15f, 0.25f),
        
        bloomEnabled = true,
        bloomIntensity = 0.1f,
        bloomThreshold = 0.9f
    };
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private PostProcessSettings neutral = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 0f,
        tint = 0f,
        saturation = 0f,
        contrast = 0f,
        
        vignetteEnabled = false,
        vignetteIntensity = 0f,
        vignetteSmoothness = 0.2f,
        vignetteColor = Color.black,
        
        bloomEnabled = true,
        bloomIntensity = 0.1f,
        bloomThreshold = 0.9f
    };
    
    [Header("=== LIGHT 1 (Golden Hour - Warm!) ===")]
    [SerializeField] private PostProcessSettings light1 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 20f,      // WARM golden
        tint = -5f,
        saturation = 12f,
        contrast = 8f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.1f,
        vignetteSmoothness = 0.5f,
        vignetteColor = new Color(1f, 0.9f, 0.6f),  // Golden vignette
        
        bloomEnabled = true,
        bloomIntensity = 0.25f,
        bloomThreshold = 0.8f
    };
    
    [Header("=== LIGHT 2 (Warm Afternoon) ===")]
    [SerializeField] private PostProcessSettings light2 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 15f,      // Still warm but less golden
        tint = -3f,
        saturation = 10f,
        contrast = 6f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.08f,
        vignetteSmoothness = 0.5f,
        vignetteColor = new Color(1f, 0.95f, 0.7f),
        
        bloomEnabled = true,
        bloomIntensity = 0.3f,
        bloomThreshold = 0.75f
    };
    
    [Header("=== LIGHT 3 (Getting Brighter) ===")]
    [SerializeField] private PostProcessSettings light3 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 10f,      // Less warm, more neutral bright
        tint = -2f,
        saturation = 8f,
        contrast = 5f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.05f,
        vignetteSmoothness = 0.5f,
        vignetteColor = new Color(1f, 1f, 0.9f),
        
        bloomEnabled = true,
        bloomIntensity = 0.4f,
        bloomThreshold = 0.7f
    };
    
    [Header("=== LIGHT 4 (Bright) ===")]
    [SerializeField] private PostProcessSettings light4 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 5f,       // Nearly neutral, bright
        tint = 0f,
        saturation = 5f,
        contrast = 3f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.05f,
        vignetteSmoothness = 0.6f,
        vignetteColor = new Color(1f, 1f, 0.95f),
        
        bloomEnabled = true,
        bloomIntensity = 0.5f,
        bloomThreshold = 0.65f
    };
    
    [Header("=== LIGHT 5 (HEAVENLY - Maximum Brightness!) ===")]
    [SerializeField] private PostProcessSettings light5 = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = 5f,       // Neutral-warm, pure light
        tint = 0f,
        saturation = 3f,        // Slightly vivid
        contrast = 0f,          // Soft, no harsh shadows
        
        vignetteEnabled = true,
        vignetteIntensity = 0.1f,
        vignetteSmoothness = 0.8f,
        vignetteColor = new Color(1f, 1f, 0.95f),  // Bright white-gold
        
        bloomEnabled = true,
        bloomIntensity = 0.7f,  // HIGH bloom = heavenly glow!
        bloomThreshold = 0.5f   // More things glow
    };
    
    [Header("=== ECLIPSE (Special) ===")]
    [SerializeField] private PostProcessSettings eclipse = new PostProcessSettings
    {
        colorGradingEnabled = true,
        temperature = -10f,
        tint = 20f,
        saturation = -15f,
        contrast = 35f,
        
        vignetteEnabled = true,
        vignetteIntensity = 0.55f,
        vignetteSmoothness = 0.3f,
        vignetteColor = new Color(0.3f, 0.1f, 0.4f),
        
        bloomEnabled = true,
        bloomIntensity = 0.8f,
        bloomThreshold = 0.5f
    };
    
    #endregion
    
    #region Private Fields
    
    private ColorGrading colorGrading;
    private Vignette vignette;
    private Bloom bloom;
    
    private PostProcessSettings currentSettings;
    private PostProcessSettings targetSettings;
    private PostProcessSettings previousSettings;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (volume == null)
            volume = GetComponent<PostProcessVolume>();
        
        // Get post-process effects
        if (volume.profile != null)
        {
            volume.profile.TryGetSettings(out colorGrading);
            volume.profile.TryGetSettings(out vignette);
            volume.profile.TryGetSettings(out bloom);
        }
        
        currentSettings = neutral;
        targetSettings = neutral;
        previousSettings = neutral;
    }
    
    private void OnEnable()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.AddListener(OnStateChanged);
            // Apply initial state
            OnStateChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    private void OnDisable()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.RemoveListener(OnStateChanged);
        }
    }
    
    private void Update()
    {
        // Handle smooth transition
        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);
            
            // Lerp all settings
            currentSettings = LerpSettings(previousSettings, targetSettings, transitionProgress);
            ApplySettings(currentSettings);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnStateChanged(WorldStateManager.AtmosphereState newState)
    {
        currentState = newState;
        
        // Store previous for lerping
        previousSettings = currentSettings;
        
        // Get target settings for new state
        targetSettings = GetSettingsForState(newState);
        
        // Start transition
        transitionProgress = 0f;
        
        Debug.Log($"[PostProcessController] Transitioning to {newState}");
    }
    
    #endregion
    
    #region Settings Methods
    
    private PostProcessSettings GetSettingsForState(WorldStateManager.AtmosphereState state)
    {
        return state switch
        {
            WorldStateManager.AtmosphereState.Eclipse => eclipse,
            WorldStateManager.AtmosphereState.Dark5 => dark5,
            WorldStateManager.AtmosphereState.Dark4 => dark4,
            WorldStateManager.AtmosphereState.Dark3 => dark3,
            WorldStateManager.AtmosphereState.Dark2 => dark2,
            WorldStateManager.AtmosphereState.Dark1 => dark1,
            WorldStateManager.AtmosphereState.Neutral => neutral,
            WorldStateManager.AtmosphereState.Light1 => light1,
            WorldStateManager.AtmosphereState.Light2 => light2,
            WorldStateManager.AtmosphereState.Light3 => light3,
            WorldStateManager.AtmosphereState.Light4 => light4,
            WorldStateManager.AtmosphereState.Light5 => light5,
            _ => neutral
        };
    }
    
    private PostProcessSettings LerpSettings(PostProcessSettings from, PostProcessSettings to, float t)
    {
        return new PostProcessSettings
        {
            // Color Grading
            colorGradingEnabled = t < 0.5f ? from.colorGradingEnabled : to.colorGradingEnabled,
            temperature = Mathf.Lerp(from.temperature, to.temperature, t),
            tint = Mathf.Lerp(from.tint, to.tint, t),
            saturation = Mathf.Lerp(from.saturation, to.saturation, t),
            contrast = Mathf.Lerp(from.contrast, to.contrast, t),
            
            // Vignette
            vignetteEnabled = t < 0.5f ? from.vignetteEnabled : to.vignetteEnabled,
            vignetteIntensity = Mathf.Lerp(from.vignetteIntensity, to.vignetteIntensity, t),
            vignetteSmoothness = Mathf.Lerp(from.vignetteSmoothness, to.vignetteSmoothness, t),
            vignetteColor = Color.Lerp(from.vignetteColor, to.vignetteColor, t),
            
            // Bloom
            bloomEnabled = t < 0.5f ? from.bloomEnabled : to.bloomEnabled,
            bloomIntensity = Mathf.Lerp(from.bloomIntensity, to.bloomIntensity, t),
            bloomThreshold = Mathf.Lerp(from.bloomThreshold, to.bloomThreshold, t)
        };
    }
    
    private void ApplySettings(PostProcessSettings settings)
    {
        if (colorGrading != null)
        {
            colorGrading.active = settings.colorGradingEnabled;
            colorGrading.temperature.value = settings.temperature;
            colorGrading.tint.value = settings.tint;
            colorGrading.saturation.value = settings.saturation;
            colorGrading.contrast.value = settings.contrast;
        }
        
        if (vignette != null)
        {
            vignette.active = settings.vignetteEnabled;
            vignette.intensity.value = settings.vignetteIntensity;
            vignette.smoothness.value = settings.vignetteSmoothness;
            vignette.color.value = settings.vignetteColor;
        }
        
        if (bloom != null)
        {
            bloom.active = settings.bloomEnabled;
            bloom.intensity.value = settings.bloomIntensity;
            bloom.threshold.value = settings.bloomThreshold;
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Force immediate state change without transition.
    /// </summary>
    public void SetStateImmediate(WorldStateManager.AtmosphereState state)
    {
        currentState = state;
        currentSettings = GetSettingsForState(state);
        targetSettings = currentSettings;
        previousSettings = currentSettings;
        transitionProgress = 1f;
        ApplySettings(currentSettings);
    }
    
    /// <summary>
    /// Preview a state without WorldStateManager (for testing in editor).
    /// </summary>
    [ContextMenu("Preview Current State")]
    public void PreviewCurrentState()
    {
        SetStateImmediate(currentState);
    }
    
    #endregion
    
    #region Nested Types
    
    [System.Serializable]
    public struct PostProcessSettings
    {
        [Header("Color Grading")]
        public bool colorGradingEnabled;
        [Range(-100f, 100f)] public float temperature;
        [Range(-100f, 100f)] public float tint;
        [Range(-100f, 100f)] public float saturation;
        [Range(-100f, 100f)] public float contrast;
        
        [Header("Vignette")]
        public bool vignetteEnabled;
        [Range(0f, 1f)] public float vignetteIntensity;
        [Range(0f, 1f)] public float vignetteSmoothness;
        public Color vignetteColor;
        
        [Header("Bloom")]
        public bool bloomEnabled;
        [Range(0f, 10f)] public float bloomIntensity;
        [Range(0f, 2f)] public float bloomThreshold;
    }
    
    #endregion
}