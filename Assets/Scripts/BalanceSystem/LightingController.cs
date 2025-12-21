using UnityEngine;

/// <summary>
/// Controls Directional Light based on karma balance.
/// 
/// Atmosphere Scaling (0-10 rings per tail):
/// 
/// DARK PATH:
/// - 0L = Neutral daylight
/// - 5L = Sunset lighting (warm, low angle, long shadows)
/// - 10L = Night/eerie (moonlight blue, very dim)
/// 
/// LIGHT PATH:
/// - 0R = Neutral daylight
/// - 5R = Sunrise lighting (warm golden, low angle)
/// - 10R = Heavenly (bright white-gold, soft shadows)
/// 
/// ECLIPSE (5L + 5R):
/// - Dramatic rim lighting, orange corona glow
/// </summary>
public class LightingController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Light Reference")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool autoFindLight = true;
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.5f;
    
    [Header("=== NEUTRAL (0 rings, Game Start) ===")]
    [SerializeField] private Color neutralLightColor = new Color(1f, 0.98f, 0.95f);
    [SerializeField, Range(0, 3)] private float neutralIntensity = 1f;
    [SerializeField, Range(0, 90)] private float neutralAngle = 45f;
    [SerializeField] private Color neutralAmbientColor = new Color(0.22f, 0.22f, 0.25f);
    [SerializeField, Range(0, 1)] private float neutralShadowStrength = 0.75f;
    
    [Header("=== DARK PATH: 5 Left Rings (Sunset) ===")]
    [SerializeField] private Color dark5LightColor = new Color(1f, 0.6f, 0.3f);        // Orange sunset
    [SerializeField, Range(0, 3)] private float dark5Intensity = 0.9f;
    [SerializeField, Range(0, 90)] private float dark5Angle = 15f;                      // Very low (sunset)
    [SerializeField] private Color dark5AmbientColor = new Color(0.3f, 0.2f, 0.25f);   // Warm purple
    [SerializeField, Range(0, 1)] private float dark5ShadowStrength = 0.8f;
    
    [Header("=== DARK PATH: 10 Left Rings (Maximum Eerie Night) ===")]
    [SerializeField] private Color dark10LightColor = new Color(0.5f, 0.6f, 0.9f);     // Cold moonlight
    [SerializeField, Range(0, 3)] private float dark10Intensity = 0.3f;
    [SerializeField, Range(0, 90)] private float dark10Angle = 70f;                     // High moon
    [SerializeField] private Color dark10AmbientColor = new Color(0.08f, 0.08f, 0.15f); // Very dark blue
    [SerializeField, Range(0, 1)] private float dark10ShadowStrength = 0.95f;
    
    [Header("=== LIGHT PATH: 5 Right Rings (Sunrise) ===")]
    [SerializeField] private Color light5LightColor = new Color(1f, 0.9f, 0.7f);       // Golden sunrise
    [SerializeField, Range(0, 3)] private float light5Intensity = 1.1f;
    [SerializeField, Range(0, 90)] private float light5Angle = 20f;                     // Low golden hour
    [SerializeField] private Color light5AmbientColor = new Color(0.35f, 0.3f, 0.25f); // Warm
    [SerializeField, Range(0, 1)] private float light5ShadowStrength = 0.65f;
    
    [Header("=== LIGHT PATH: 10 Right Rings (Maximum Heavenly) ===")]
    [SerializeField] private Color light10LightColor = new Color(1f, 1f, 0.95f);       // Bright white
    [SerializeField, Range(0, 3)] private float light10Intensity = 1.5f;
    [SerializeField, Range(0, 90)] private float light10Angle = 50f;                    // High noon-ish
    [SerializeField] private Color light10AmbientColor = new Color(0.5f, 0.48f, 0.45f); // Bright ambient
    [SerializeField, Range(0, 1)] private float light10ShadowStrength = 0.4f;           // Soft shadows
    
    [Header("=== ECLIPSE (5L + 5R Perfect Balance) ===")]
    [SerializeField] private Color eclipseLightColor = new Color(1f, 0.4f, 0.2f);      // Corona orange
    [SerializeField, Range(0, 3)] private float eclipseIntensity = 0.25f;
    [SerializeField, Range(0, 90)] private float eclipseAngle = 45f;
    [SerializeField] private Color eclipseAmbientColor = new Color(0.12f, 0.08f, 0.18f); // Dark purple
    [SerializeField, Range(0, 1)] private float eclipseShadowStrength = 0.98f;
    
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
        
        public bool ApproximatelyEquals(LightState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(intensity - other.intensity) < tolerance &&
                   Mathf.Abs(angle - other.angle) < 0.5f &&
                   Mathf.Abs(shadowStrength - other.shadowStrength) < tolerance;
        }
    }
    
    private LightState currentState;
    private LightState targetState;
    private bool isTransitioning;
    private Vector3 originalRotation;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (!SetupLight())
        {
            enabled = false;
            return;
        }
        
        originalRotation = directionalLight.transform.eulerAngles;
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
    
    private bool SetupLight()
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
    
    private void InitializeState()
    {
        currentState = new LightState
        {
            lightColor = neutralLightColor,
            intensity = neutralIntensity,
            angle = neutralAngle,
            ambientColor = neutralAmbientColor,
            shadowStrength = neutralShadowStrength
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
    
    private LightState CalculateTargetState(int left, int right)
    {
        // Eclipse
        if (left == 5 && right == 5)
        {
            return new LightState
            {
                lightColor = eclipseLightColor,
                intensity = eclipseIntensity,
                angle = eclipseAngle,
                ambientColor = eclipseAmbientColor,
                shadowStrength = eclipseShadowStrength
            };
        }
        
        LightState darkState = CalculateDarkState(left);
        LightState lightState = CalculateLightState(right);
        
        int total = left + right;
        if (total == 0)
        {
            return new LightState
            {
                lightColor = neutralLightColor,
                intensity = neutralIntensity,
                angle = neutralAngle,
                ambientColor = neutralAmbientColor,
                shadowStrength = neutralShadowStrength
            };
        }
        
        float leftWeight = (float)left / total;
        return LightState.Lerp(lightState, darkState, leftWeight);
    }
    
    private LightState CalculateDarkState(int leftRings)
    {
        if (leftRings <= 0)
            return GetNeutralState();
        
        if (leftRings <= 5)
        {
            float t = leftRings / 5f;
            return LerpStates(GetNeutralState(), GetDark5State(), t);
        }
        else
        {
            float t = (leftRings - 5) / 5f;
            return LerpStates(GetDark5State(), GetDark10State(), t);
        }
    }
    
    private LightState CalculateLightState(int rightRings)
    {
        if (rightRings <= 0)
            return GetNeutralState();
        
        if (rightRings <= 5)
        {
            float t = rightRings / 5f;
            return LerpStates(GetNeutralState(), GetLight5State(), t);
        }
        else
        {
            float t = (rightRings - 5) / 5f;
            return LerpStates(GetLight5State(), GetLight10State(), t);
        }
    }
    
    private LightState GetNeutralState() => new LightState
    {
        lightColor = neutralLightColor, intensity = neutralIntensity, angle = neutralAngle,
        ambientColor = neutralAmbientColor, shadowStrength = neutralShadowStrength
    };
    
    private LightState GetDark5State() => new LightState
    {
        lightColor = dark5LightColor, intensity = dark5Intensity, angle = dark5Angle,
        ambientColor = dark5AmbientColor, shadowStrength = dark5ShadowStrength
    };
    
    private LightState GetDark10State() => new LightState
    {
        lightColor = dark10LightColor, intensity = dark10Intensity, angle = dark10Angle,
        ambientColor = dark10AmbientColor, shadowStrength = dark10ShadowStrength
    };
    
    private LightState GetLight5State() => new LightState
    {
        lightColor = light5LightColor, intensity = light5Intensity, angle = light5Angle,
        ambientColor = light5AmbientColor, shadowStrength = light5ShadowStrength
    };
    
    private LightState GetLight10State() => new LightState
    {
        lightColor = light10LightColor, intensity = light10Intensity, angle = light10Angle,
        ambientColor = light10AmbientColor, shadowStrength = light10ShadowStrength
    };
    
    private LightState LerpStates(LightState a, LightState b, float t) => LightState.Lerp(a, b, t);
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(LightState state)
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