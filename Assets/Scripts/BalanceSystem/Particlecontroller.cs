using UnityEngine;

/// <summary>
/// Controls atmospheric particle systems based on karma balance.
/// 
/// Dark (more LEFT rings): Crows, dark wisps, fewer butterflies
/// Light (more RIGHT rings): Butterflies, golden particles, fireflies
/// Eclipse (5L + 5R): Special mystical particles, both fireflies and wisps
/// 
/// Assign particle systems in Inspector, controller handles emission rates.
/// </summary>
public class ParticleController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.5f;
    
    [Header("Light Particles (More RIGHT rings)")]
    [Tooltip("Butterflies, golden dust, light orbs")]
    [SerializeField] private ParticleSystem[] lightParticles;
    [SerializeField, Range(0, 100)] private float maxLightEmission = 30f;
    
    [Header("Dark Particles (More LEFT rings)")]
    [Tooltip("Crows, dark wisps, shadowy mist")]
    [SerializeField] private ParticleSystem[] darkParticles;
    [SerializeField, Range(0, 100)] private float maxDarkEmission = 25f;
    
    [Header("Neutral Particles (Always present)")]
    [Tooltip("Fireflies, dust motes, ambient particles")]
    [SerializeField] private ParticleSystem[] neutralParticles;
    [SerializeField, Range(0, 100)] private float neutralEmission = 10f;
    
    [Header("Eclipse Particles (Perfect Balance only)")]
    [Tooltip("Special mystical particles for eclipse")]
    [SerializeField] private ParticleSystem[] eclipseParticles;
    [SerializeField, Range(0, 100)] private float eclipseEmission = 50f;
    
    #endregion
    
    #region Private State
    
    private struct ParticleState
    {
        public float lightEmission;
        public float darkEmission;
        public float neutralEmission;
        public float eclipseEmission;
        
        public static ParticleState Lerp(ParticleState a, ParticleState b, float t)
        {
            return new ParticleState
            {
                lightEmission = Mathf.Lerp(a.lightEmission, b.lightEmission, t),
                darkEmission = Mathf.Lerp(a.darkEmission, b.darkEmission, t),
                neutralEmission = Mathf.Lerp(a.neutralEmission, b.neutralEmission, t),
                eclipseEmission = Mathf.Lerp(a.eclipseEmission, b.eclipseEmission, t)
            };
        }
        
        public bool ApproximatelyEquals(ParticleState other, float tolerance = 0.1f)
        {
            return Mathf.Abs(lightEmission - other.lightEmission) < tolerance &&
                   Mathf.Abs(darkEmission - other.darkEmission) < tolerance &&
                   Mathf.Abs(neutralEmission - other.neutralEmission) < tolerance &&
                   Mathf.Abs(eclipseEmission - other.eclipseEmission) < tolerance;
        }
    }
    
    private ParticleState currentState;
    private ParticleState targetState;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        InitializeParticleSystems();
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
        currentState = ParticleState.Lerp(currentState, targetState, t);
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
    
    private void InitializeParticleSystems()
    {
        // Start all systems but with zero emission
        SetEmissionRate(lightParticles, 0f);
        SetEmissionRate(darkParticles, 0f);
        SetEmissionRate(neutralParticles, neutralEmission);
        SetEmissionRate(eclipseParticles, 0f);
    }
    
    private void InitializeState()
    {
        currentState = new ParticleState
        {
            lightEmission = 0f,
            darkEmission = 0f,
            neutralEmission = neutralEmission,
            eclipseEmission = 0f
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
    
    private ParticleState CalculateTargetState(int left, int right)
    {
        var state = new ParticleState();
        
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            state.lightEmission = maxLightEmission * 0.3f;   // Some light particles
            state.darkEmission = maxDarkEmission * 0.3f;     // Some dark particles
            state.neutralEmission = neutralEmission;
            state.eclipseEmission = eclipseEmission;          // Full eclipse particles
            return state;
        }
        
        // No eclipse particles outside perfect balance
        state.eclipseEmission = 0f;
        
        // Calculate balance (-5 to +5, normalized to -1 to +1)
        float balance = (right - left) / 5f;
        
        if (balance >= 0)
        {
            // More light = more light particles, fewer dark
            state.lightEmission = Mathf.Lerp(0f, maxLightEmission, balance);
            state.darkEmission = 0f;
            state.neutralEmission = neutralEmission;
        }
        else
        {
            // More dark = more dark particles, fewer light
            float darkAmount = -balance;
            state.lightEmission = 0f;
            state.darkEmission = Mathf.Lerp(0f, maxDarkEmission, darkAmount);
            state.neutralEmission = Mathf.Lerp(neutralEmission, neutralEmission * 0.5f, darkAmount); // Fewer fireflies in dark
        }
        
        return state;
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(ParticleState state)
    {
        SetEmissionRate(lightParticles, state.lightEmission);
        SetEmissionRate(darkParticles, state.darkEmission);
        SetEmissionRate(neutralParticles, state.neutralEmission);
        SetEmissionRate(eclipseParticles, state.eclipseEmission);
    }
    
    private void SetEmissionRate(ParticleSystem[] systems, float rate)
    {
        if (systems == null) return;
        
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            
            var emission = ps.emission;
            emission.rateOverTime = rate;
            
            // Enable/disable based on rate
            if (rate > 0.1f && !ps.isPlaying)
                ps.Play();
            else if (rate < 0.1f && ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
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
    
    /// <summary>
    /// Add a particle system to a category at runtime.
    /// </summary>
    public void AddParticleSystem(ParticleSystem ps, ParticleCategory category)
    {
        switch (category)
        {
            case ParticleCategory.Light:
                lightParticles = AddToArray(lightParticles, ps);
                break;
            case ParticleCategory.Dark:
                darkParticles = AddToArray(darkParticles, ps);
                break;
            case ParticleCategory.Neutral:
                neutralParticles = AddToArray(neutralParticles, ps);
                break;
            case ParticleCategory.Eclipse:
                eclipseParticles = AddToArray(eclipseParticles, ps);
                break;
        }
    }
    
    private ParticleSystem[] AddToArray(ParticleSystem[] array, ParticleSystem ps)
    {
        if (array == null)
            return new ParticleSystem[] { ps };
        
        var newArray = new ParticleSystem[array.Length + 1];
        array.CopyTo(newArray, 0);
        newArray[array.Length] = ps;
        return newArray;
    }
    
    public enum ParticleCategory { Light, Dark, Neutral, Eclipse }
    
    #endregion
}