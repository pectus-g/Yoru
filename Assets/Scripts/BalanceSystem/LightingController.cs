using UnityEngine;

/// <summary>
/// YORU Lighting Controller V2 - Balance-Based
/// 
/// Works with the two-layer balance system.
/// Ensures you can SEE at night (dark path) while maintaining atmosphere.
/// 
/// KEY CHANGES from V1:
/// - Uses AtmosphereState instead of raw ring counts
/// - Better minimum ambient light for dark states (so you can see!)
/// - Cleaner organization
/// </summary>
public class LightingController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool autoFindLight = true;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.1f, 3f)] private float transitionSpeed = 1f;
    
    [Header("=== NEUTRAL (Balance 0, Noon) ===")]
    [SerializeField] private Color neutralLightColor = new Color(1f, 0.98f, 0.95f);
    [SerializeField, Range(0, 3)] private float neutralIntensity = 1f;
    [SerializeField, Range(0, 90)] private float neutralAngle = 50f;
    [SerializeField] private Color neutralAmbientColor = new Color(0.25f, 0.25f, 0.28f);
    [SerializeField, Range(0, 1)] private float neutralShadowStrength = 0.7f;
    
    [Header("=== DARK 1 (Balance -1, Late Afternoon) ===")]
    [SerializeField] private Color dark1LightColor = new Color(1f, 0.9f, 0.7f);
    [SerializeField, Range(0, 3)] private float dark1Intensity = 0.95f;
    [SerializeField, Range(0, 90)] private float dark1Angle = 35f;
    [SerializeField] private Color dark1AmbientColor = new Color(0.25f, 0.22f, 0.25f);
    [SerializeField, Range(0, 1)] private float dark1ShadowStrength = 0.72f;
    
    [Header("=== DARK 2 (Balance -2, Sunset) ===")]
    [SerializeField] private Color dark2LightColor = new Color(1f, 0.7f, 0.4f);
    [SerializeField, Range(0, 3)] private float dark2Intensity = 0.85f;
    [SerializeField, Range(0, 90)] private float dark2Angle = 20f;
    [SerializeField] private Color dark2AmbientColor = new Color(0.28f, 0.2f, 0.25f);
    [SerializeField, Range(0, 1)] private float dark2ShadowStrength = 0.75f;
    
    [Header("=== DARK 3 (Balance -3, Dusk) ===")]
    [SerializeField] private Color dark3LightColor = new Color(0.8f, 0.7f, 0.9f);
    [SerializeField, Range(0, 3)] private float dark3Intensity = 0.6f;
    [SerializeField, Range(0, 90)] private float dark3Angle = 15f;
    [SerializeField] private Color dark3AmbientColor = new Color(0.18f, 0.16f, 0.22f);
    [SerializeField, Range(0, 1)] private float dark3ShadowStrength = 0.8f;
    
    [Header("=== DARK 4 (Balance -4, Night) ===")]
    [SerializeField] private Color dark4LightColor = new Color(0.6f, 0.7f, 0.95f);
    [SerializeField, Range(0, 3)] private float dark4Intensity = 0.4f;
    [SerializeField, Range(0, 90)] private float dark4Angle = 60f;
    [SerializeField] private Color dark4AmbientColor = new Color(0.12f, 0.12f, 0.18f);
    [SerializeField, Range(0, 1)] private float dark4ShadowStrength = 0.85f;
    
    [Header("=== DARK 5 (Balance -5, Midnight) - VISIBLE! ===")]
    [Tooltip("Moonlight color - enough to see!")]
    [SerializeField] private Color dark5LightColor = new Color(0.5f, 0.6f, 0.9f);
    [Tooltip("Keep above 0.25 so player can see!")]
    [SerializeField, Range(0, 3)] private float dark5Intensity = 0.35f;
    [SerializeField, Range(0, 90)] private float dark5Angle = 70f;
    [Tooltip("Minimum ambient - CRITICAL for visibility!")]
    [SerializeField] private Color dark5AmbientColor = new Color(0.1f, 0.1f, 0.15f);
    [SerializeField, Range(0, 1)] private float dark5ShadowStrength = 0.9f;
    
    [Header("=== LIGHT 1 (Balance +1, Golden Hour) ===")]
    [SerializeField] private Color light1LightColor = new Color(1f, 0.85f, 0.6f);  // Warm golden
    [SerializeField, Range(0, 3)] private float light1Intensity = 1.0f;
    [SerializeField, Range(0, 90)] private float light1Angle = 30f;  // Low sun = golden
    [SerializeField] private Color light1AmbientColor = new Color(0.3f, 0.27f, 0.22f);  // Warm
    [SerializeField, Range(0, 1)] private float light1ShadowStrength = 0.6f;
    
    [Header("=== LIGHT 2 (Balance +2, Warm Afternoon) ===")]
    [SerializeField] private Color light2LightColor = new Color(1f, 0.9f, 0.7f);  // Less golden
    [SerializeField, Range(0, 3)] private float light2Intensity = 1.1f;
    [SerializeField, Range(0, 90)] private float light2Angle = 38f;
    [SerializeField] private Color light2AmbientColor = new Color(0.32f, 0.3f, 0.25f);
    [SerializeField, Range(0, 1)] private float light2ShadowStrength = 0.55f;
    
    [Header("=== LIGHT 3 (Balance +3, Getting Brighter) ===")]
    [SerializeField] private Color light3LightColor = new Color(1f, 0.95f, 0.8f);
    [SerializeField, Range(0, 3)] private float light3Intensity = 1.2f;
    [SerializeField, Range(0, 90)] private float light3Angle = 45f;
    [SerializeField] private Color light3AmbientColor = new Color(0.35f, 0.34f, 0.3f);
    [SerializeField, Range(0, 1)] private float light3ShadowStrength = 0.5f;
    
    [Header("=== LIGHT 4 (Balance +4, Bright) ===")]
    [SerializeField] private Color light4LightColor = new Color(1f, 0.98f, 0.9f);
    [SerializeField, Range(0, 3)] private float light4Intensity = 1.3f;
    [SerializeField, Range(0, 90)] private float light4Angle = 55f;
    [SerializeField] private Color light4AmbientColor = new Color(0.4f, 0.38f, 0.35f);
    [SerializeField, Range(0, 1)] private float light4ShadowStrength = 0.45f;
    
    [Header("=== LIGHT 5 (Balance +5, HEAVENLY - Maximum Brightness!) ===")]
    [SerializeField] private Color light5LightColor = new Color(1f, 1f, 0.95f);  // Pure bright
    [SerializeField, Range(0, 3)] private float light5Intensity = 1.4f;  // Maximum!
    [SerializeField, Range(0, 90)] private float light5Angle = 65f;  // High sun
    [SerializeField] private Color light5AmbientColor = new Color(0.45f, 0.43f, 0.4f);  // Bright ambient
    [SerializeField, Range(0, 1)] private float light5ShadowStrength = 0.35f;  // Soft shadows
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private Color eclipseLightColor = new Color(1f, 0.4f, 0.2f);
    [SerializeField, Range(0, 3)] private float eclipseIntensity = 0.2f;
    [SerializeField, Range(0, 90)] private float eclipseAngle = 45f;
    [SerializeField] private Color eclipseAmbientColor = new Color(0.1f, 0.08f, 0.15f);
    [SerializeField, Range(0, 1)] private float eclipseShadowStrength = 0.95f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    
    #endregion
    
    #region Private State
    
    private struct LightState
    {
        public Color lightColor;
        public float intensity;
        public float angle;
        public Color ambientColor;
        public float shadowStrength;
        
        public static LightState Lerp(LightState a, LightState b, float t)
        {
            return new LightState
            {
                lightColor = Color.Lerp(a.lightColor, b.lightColor, t),
                intensity = Mathf.Lerp(a.intensity, b.intensity, t),
                angle = Mathf.Lerp(a.angle, b.angle, t),
                ambientColor = Color.Lerp(a.ambientColor, b.ambientColor, t),
                shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t)
            };
        }
        
        public bool ApproximatelyEquals(LightState other)
        {
            return Mathf.Abs(intensity - other.intensity) < 0.001f &&
                   Mathf.Abs(angle - other.angle) < 0.5f;
        }
    }
    
    private LightState currentState;
    private LightState targetState;
    private bool isTransitioning;
    private Vector3 originalRotation;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        if (!SetupLight())
        {
            enabled = false;
            return;
        }
        
        originalRotation = directionalLight.transform.eulerAngles;
        
        // Initialize to neutral
        currentState = GetStateForAtmosphere(WorldStateManager.AtmosphereState.Neutral);
        targetState = currentState;
        ApplyState(currentState);
        
        // Subscribe to events
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.AddListener(OnStateChanged);
            OnStateChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
            WorldStateManager.Instance.OnStateChanged.RemoveListener(OnStateChanged);
    }
    
    void Update()
    {
        if (!isTransitioning) return;
        
        float t = transitionSpeed * Time.deltaTime;
        currentState = LightState.Lerp(currentState, targetState, t);
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
    
    bool SetupLight()
    {
        if (directionalLight == null && autoFindLight)
        {
            Light[] lights = FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLight = light;
                    break;
                }
            }
        }
        
        if (directionalLight == null)
        {
            Debug.LogError("[LightingController] No Directional Light found!");
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region Event Handler
    
    void OnStateChanged(WorldStateManager.AtmosphereState newState)
    {
        targetState = GetStateForAtmosphere(newState);
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[LightingController] → {newState}");
    }
    
    #endregion
    
    #region State Mapping
    
    LightState GetStateForAtmosphere(WorldStateManager.AtmosphereState state)
    {
        switch (state)
        {
            case WorldStateManager.AtmosphereState.Dark5:
                return new LightState { lightColor = dark5LightColor, intensity = dark5Intensity, angle = dark5Angle, ambientColor = dark5AmbientColor, shadowStrength = dark5ShadowStrength };
            case WorldStateManager.AtmosphereState.Dark4:
                return new LightState { lightColor = dark4LightColor, intensity = dark4Intensity, angle = dark4Angle, ambientColor = dark4AmbientColor, shadowStrength = dark4ShadowStrength };
            case WorldStateManager.AtmosphereState.Dark3:
                return new LightState { lightColor = dark3LightColor, intensity = dark3Intensity, angle = dark3Angle, ambientColor = dark3AmbientColor, shadowStrength = dark3ShadowStrength };
            case WorldStateManager.AtmosphereState.Dark2:
                return new LightState { lightColor = dark2LightColor, intensity = dark2Intensity, angle = dark2Angle, ambientColor = dark2AmbientColor, shadowStrength = dark2ShadowStrength };
            case WorldStateManager.AtmosphereState.Dark1:
                return new LightState { lightColor = dark1LightColor, intensity = dark1Intensity, angle = dark1Angle, ambientColor = dark1AmbientColor, shadowStrength = dark1ShadowStrength };
            
            case WorldStateManager.AtmosphereState.Neutral:
                return new LightState { lightColor = neutralLightColor, intensity = neutralIntensity, angle = neutralAngle, ambientColor = neutralAmbientColor, shadowStrength = neutralShadowStrength };
            
            case WorldStateManager.AtmosphereState.Light1:
                return new LightState { lightColor = light1LightColor, intensity = light1Intensity, angle = light1Angle, ambientColor = light1AmbientColor, shadowStrength = light1ShadowStrength };
            case WorldStateManager.AtmosphereState.Light2:
                return new LightState { lightColor = light2LightColor, intensity = light2Intensity, angle = light2Angle, ambientColor = light2AmbientColor, shadowStrength = light2ShadowStrength };
            case WorldStateManager.AtmosphereState.Light3:
                return new LightState { lightColor = light3LightColor, intensity = light3Intensity, angle = light3Angle, ambientColor = light3AmbientColor, shadowStrength = light3ShadowStrength };
            case WorldStateManager.AtmosphereState.Light4:
                return new LightState { lightColor = light4LightColor, intensity = light4Intensity, angle = light4Angle, ambientColor = light4AmbientColor, shadowStrength = light4ShadowStrength };
            case WorldStateManager.AtmosphereState.Light5:
                return new LightState { lightColor = light5LightColor, intensity = light5Intensity, angle = light5Angle, ambientColor = light5AmbientColor, shadowStrength = light5ShadowStrength };
            
            case WorldStateManager.AtmosphereState.Eclipse:
                return new LightState { lightColor = eclipseLightColor, intensity = eclipseIntensity, angle = eclipseAngle, ambientColor = eclipseAmbientColor, shadowStrength = eclipseShadowStrength };
            
            default:
                return new LightState { lightColor = neutralLightColor, intensity = neutralIntensity, angle = neutralAngle, ambientColor = neutralAmbientColor, shadowStrength = neutralShadowStrength };
        }
    }
    
    #endregion
    
    #region Apply State
    
    void ApplyState(LightState state)
    {
        if (directionalLight == null) return;
        
        directionalLight.color = state.lightColor;
        directionalLight.intensity = state.intensity;
        directionalLight.shadowStrength = state.shadowStrength;
        
        Vector3 rotation = originalRotation;
        rotation.x = state.angle;
        directionalLight.transform.eulerAngles = rotation;
        
        RenderSettings.ambientLight = state.ambientColor;
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Preview: Neutral")]
    public void PreviewNeutral()
    {
        currentState = GetStateForAtmosphere(WorldStateManager.AtmosphereState.Neutral);
        ApplyState(currentState);
    }
    
    [ContextMenu("Preview: Dark5 (Midnight)")]
    public void PreviewDark5()
    {
        currentState = GetStateForAtmosphere(WorldStateManager.AtmosphereState.Dark5);
        ApplyState(currentState);
    }
    
    [ContextMenu("Preview: Light5 (Heavenly)")]
    public void PreviewLight5()
    {
        currentState = GetStateForAtmosphere(WorldStateManager.AtmosphereState.Light5);
        ApplyState(currentState);
    }
    
    [ContextMenu("Preview: Eclipse")]
    public void PreviewEclipse()
    {
        currentState = GetStateForAtmosphere(WorldStateManager.AtmosphereState.Eclipse);
        ApplyState(currentState);
    }
    
    #endregion
}