using UnityEngine;
using DistantLands.Cozy;

/// <summary>
/// Connects WorldStateManager to COZY Weather system.
/// Controls eclipse, lighting, and atmosphere based on karma ring balance.
/// 
/// Performance:
/// - All COZY references cached at Start
/// - Update() only runs during active transitions
/// - Calculations only when ring state changes
/// </summary>
public class YoruCozyIntegration : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.5f;
    
    [Header("Light Environment (More RIGHT rings)")]
    [SerializeField] private float maxLightAmbient = 1.5f;
    [SerializeField] private Color lightSkyTint = new Color(1f, 0.95f, 0.8f);
    
    [Header("Dark Environment (More LEFT rings)")]
    [SerializeField] private float maxDarkAmbient = 0.3f;
    [SerializeField] private Color darkSkyTint = new Color(0.3f, 0.3f, 0.5f);
    [SerializeField] private float maxStarIntensity = 1f;
    
    [Header("Neutral (Game Start)")]
    [SerializeField] private float neutralAmbient = 1f;
    
    [Header("Eclipse (Perfect Balance)")]
    [SerializeField] private Color eclipseSkyTint = new Color(0.6f, 0.5f, 0.7f);
    
    #endregion
    
    #region Cached References
    
    private CozyWeather cozy;
    private EclipseModule eclipse;
    
    #endregion
    
    #region Environment State
    
    private struct EnvironmentState
    {
        public float eclipseRatio;
        public float ambientMultiplier;
        public float starIntensity;
        public Color skyTint;
        
        public static EnvironmentState Lerp(EnvironmentState a, EnvironmentState b, float t)
        {
            return new EnvironmentState
            {
                eclipseRatio = Mathf.Lerp(a.eclipseRatio, b.eclipseRatio, t),
                ambientMultiplier = Mathf.Lerp(a.ambientMultiplier, b.ambientMultiplier, t),
                starIntensity = Mathf.Lerp(a.starIntensity, b.starIntensity, t),
                skyTint = Color.Lerp(a.skyTint, b.skyTint, t)
            };
        }
        
        public bool ApproximatelyEquals(EnvironmentState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(eclipseRatio - other.eclipseRatio) < tolerance &&
                   Mathf.Abs(ambientMultiplier - other.ambientMultiplier) < tolerance &&
                   Mathf.Abs(starIntensity - other.starIntensity) < tolerance &&
                   ColorApproximatelyEquals(skyTint, other.skyTint, tolerance);
        }
        
        private static bool ColorApproximatelyEquals(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
        }
    }
    
    private EnvironmentState currentState;
    private EnvironmentState targetState;
    private bool isTransitioning;
    
    // Original values for sky tint calculation
    private Color originalSkyZenith;
    private Color originalSkyHorizon;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (!CacheReferences())
        {
            enabled = false;
            return;
        }
        
        CacheOriginalValues();
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
        // Only run when actively transitioning
        if (!isTransitioning) return;
        
        // Interpolate toward target
        float t = transitionSpeed * Time.deltaTime;
        currentState = EnvironmentState.Lerp(currentState, targetState, t);
        
        // Apply to COZY
        ApplyState(currentState);
        
        // Check if transition complete
        if (currentState.ApproximatelyEquals(targetState))
        {
            currentState = targetState;
            ApplyState(currentState);
            isTransitioning = false;
        }
    }
    
    #endregion
    
    #region Initialization
    
    private bool CacheReferences()
    {
        cozy = CozyWeather.instance;
        if (cozy == null)
        {
            Debug.LogError("[YoruCozyIntegration] CozyWeather not found!");
            return false;
        }
        
        eclipse = cozy.GetModule<EclipseModule>();
        if (eclipse == null)
        {
            Debug.LogWarning("[YoruCozyIntegration] Eclipse module not found. Eclipse effects disabled.");
        }
        
        return true;
    }
    
    private void CacheOriginalValues()
    {
        originalSkyZenith = cozy.skyZenithColor;
        originalSkyHorizon = cozy.skyHorizonColor;
    }
    
    private void InitializeState()
    {
        currentState = new EnvironmentState
        {
            eclipseRatio = 0f,
            ambientMultiplier = neutralAmbient,
            starIntensity = 0f,
            skyTint = Color.white
        };
        targetState = currentState;
    }
    
    private void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
        else
        {
            Debug.LogError("[YoruCozyIntegration] WorldStateManager not found!");
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
    
    private EnvironmentState CalculateTargetState(int left, int right)
    {
        var state = new EnvironmentState();
        
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            state.eclipseRatio = 1f;
            state.ambientMultiplier = neutralAmbient * 0.7f;
            state.starIntensity = 0.8f;
            state.skyTint = eclipseSkyTint;
            return state;
        }
        
        // Calculate eclipse (only when both tails have rings and are balanced)
        state.eclipseRatio = CalculateEclipseRatio(left, right);
        
        // Calculate balance (-5 to +5, normalized to -1 to +1)
        float balance = (right - left) / 5f;
        
        // Ambient light
        if (balance >= 0)
            state.ambientMultiplier = Mathf.Lerp(neutralAmbient, maxLightAmbient, balance);
        else
            state.ambientMultiplier = Mathf.Lerp(neutralAmbient, maxDarkAmbient, -balance);
        
        // Stars (only in dark)
        state.starIntensity = balance < 0 ? Mathf.Lerp(0f, maxStarIntensity, -balance) : 0f;
        
        // Sky tint
        if (balance >= 0)
            state.skyTint = Color.Lerp(Color.white, lightSkyTint, balance);
        else
            state.skyTint = Color.Lerp(Color.white, darkSkyTint, -balance);
        
        return state;
    }
    
    private float CalculateEclipseRatio(int left, int right)
    {
        // Need both tails to have rings
        if (left == 0 || right == 0)
            return 0f;
        
        int smaller = Mathf.Min(left, right);
        int larger = Mathf.Max(left, right);
        
        float balanceFactor = (float)smaller / larger;
        float progressFactor = (left + right) / 10f;
        
        return Mathf.Pow(balanceFactor * progressFactor, 0.7f);
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(EnvironmentState state)
    {
        // Eclipse
        if (eclipse != null)
            eclipse.eclipseRatio = state.eclipseRatio;
        
        // Ambient
        cozy.ambientLightMultiplier = state.ambientMultiplier;
        
        // Stars
        cozy.galaxyIntensity = state.starIntensity;
        
        // Sky tint (multiply original colors)
        cozy.skyZenithColor = originalSkyZenith * state.skyTint;
        cozy.skyHorizonColor = originalSkyHorizon * state.skyTint;
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Instantly apply current ring state without transition.
    /// </summary>
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
    
    /// <summary>
    /// Reset environment to neutral state.
    /// </summary>
    public void ResetToNeutral()
    {
        targetState = new EnvironmentState
        {
            eclipseRatio = 0f,
            ambientMultiplier = neutralAmbient,
            starIntensity = 0f,
            skyTint = Color.white
        };
        isTransitioning = true;
    }
    
    #endregion
}