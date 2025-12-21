using UnityEngine;

/// <summary>
/// Controls Unity's built-in fog based on karma balance.
/// 
/// Dark (more LEFT rings): Dense, cold blue/purple fog
/// Light (more RIGHT rings): Light, warm golden fog
/// Eclipse (5L + 5R): Medium density, mystical purple
/// 
/// Note: Works alongside COZY fog. Adjust COZY fog settings separately if needed.
/// </summary>
public class FogController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.5f;
    
    [Header("Fog Mode")]
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    
    [Header("Dark Settings (More LEFT rings)")]
    [SerializeField] private Color darkFogColor = new Color(0.15f, 0.15f, 0.25f);     // Cold dark blue
    [SerializeField, Range(0, 0.1f)] private float darkFogDensity = 0.035f;            // Dense
    [SerializeField] private float darkFogStart = 10f;                                  // Linear mode
    [SerializeField] private float darkFogEnd = 80f;
    
    [Header("Light Settings (More RIGHT rings)")]
    [SerializeField] private Color lightFogColor = new Color(0.9f, 0.85f, 0.7f);      // Warm golden
    [SerializeField, Range(0, 0.1f)] private float lightFogDensity = 0.008f;           // Light haze
    [SerializeField] private float lightFogStart = 30f;
    [SerializeField] private float lightFogEnd = 200f;
    
    [Header("Neutral Settings (Game Start)")]
    [SerializeField] private Color neutralFogColor = new Color(0.5f, 0.5f, 0.55f);    // Neutral grey
    [SerializeField, Range(0, 0.1f)] private float neutralFogDensity = 0.015f;
    [SerializeField] private float neutralFogStart = 20f;
    [SerializeField] private float neutralFogEnd = 150f;
    
    [Header("Eclipse Settings (Perfect Balance 5L + 5R)")]
    [SerializeField] private Color eclipseFogColor = new Color(0.3f, 0.2f, 0.35f);    // Mystical purple
    [SerializeField, Range(0, 0.1f)] private float eclipseFogDensity = 0.025f;
    [SerializeField] private float eclipseFogStart = 15f;
    [SerializeField] private float eclipseFogEnd = 100f;
    
    #endregion
    
    #region Private State
    
    private struct FogState
    {
        public Color color;
        public float density;
        public float start;
        public float end;
        
        public static FogState Lerp(FogState a, FogState b, float t)
        {
            return new FogState
            {
                color = Color.Lerp(a.color, b.color, t),
                density = Mathf.Lerp(a.density, b.density, t),
                start = Mathf.Lerp(a.start, b.start, t),
                end = Mathf.Lerp(a.end, b.end, t)
            };
        }
        
        public bool ApproximatelyEquals(FogState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(density - other.density) < tolerance &&
                   Mathf.Abs(start - other.start) < 0.5f &&
                   Mathf.Abs(end - other.end) < 0.5f &&
                   ColorApprox(color, other.color, tolerance);
        }
        
        private static bool ColorApprox(Color a, Color b, float t)
        {
            return Mathf.Abs(a.r - b.r) < t && Mathf.Abs(a.g - b.g) < t && Mathf.Abs(a.b - b.b) < t;
        }
    }
    
    private FogState currentState;
    private FogState targetState;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        // Enable fog if not already
        RenderSettings.fog = true;
        RenderSettings.fogMode = fogMode;
        
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
        currentState = FogState.Lerp(currentState, targetState, t);
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
    
    private void InitializeState()
    {
        currentState = new FogState
        {
            color = neutralFogColor,
            density = neutralFogDensity,
            start = neutralFogStart,
            end = neutralFogEnd
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
    
    private FogState CalculateTargetState(int left, int right)
    {
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            return new FogState
            {
                color = eclipseFogColor,
                density = eclipseFogDensity,
                start = eclipseFogStart,
                end = eclipseFogEnd
            };
        }
        
        // Calculate balance (-5 to +5, normalized to -1 to +1)
        float balance = (right - left) / 5f;
        
        FogState state;
        
        if (balance >= 0)
        {
            // Neutral to Light
            state = new FogState
            {
                color = Color.Lerp(neutralFogColor, lightFogColor, balance),
                density = Mathf.Lerp(neutralFogDensity, lightFogDensity, balance),
                start = Mathf.Lerp(neutralFogStart, lightFogStart, balance),
                end = Mathf.Lerp(neutralFogEnd, lightFogEnd, balance)
            };
        }
        else
        {
            // Neutral to Dark
            float darkAmount = -balance;
            state = new FogState
            {
                color = Color.Lerp(neutralFogColor, darkFogColor, darkAmount),
                density = Mathf.Lerp(neutralFogDensity, darkFogDensity, darkAmount),
                start = Mathf.Lerp(neutralFogStart, darkFogStart, darkAmount),
                end = Mathf.Lerp(neutralFogEnd, darkFogEnd, darkAmount)
            };
        }
        
        return state;
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(FogState state)
    {
        RenderSettings.fogColor = state.color;
        RenderSettings.fogDensity = state.density;
        RenderSettings.fogStartDistance = state.start;
        RenderSettings.fogEndDistance = state.end;
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
    
    public void SetFogMode(FogMode mode)
    {
        fogMode = mode;
        RenderSettings.fogMode = mode;
    }
    
    public void EnableFog(bool enable)
    {
        RenderSettings.fog = enable;
    }
    
    #endregion
}