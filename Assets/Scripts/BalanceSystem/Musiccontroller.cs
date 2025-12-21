using UnityEngine;

/// <summary>
/// Controls ambient music/audio based on karma balance.
/// 
/// Dark (more LEFT rings): Ominous, tense ambient
/// Light (more RIGHT rings): Peaceful, ethereal ambient
/// Eclipse (5L + 5R): Special eclipse theme
/// 
/// Crossfades between audio sources for smooth transitions.
/// </summary>
public class MusicController : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Transition")]
    [SerializeField, Range(0.1f, 2f)] private float transitionSpeed = 0.3f;
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource neutralMusic;
    [SerializeField] private AudioSource lightMusic;
    [SerializeField] private AudioSource darkMusic;
    [SerializeField] private AudioSource eclipseMusic;
    
    [Header("Volume Settings")]
    [SerializeField, Range(0, 1)] private float masterVolume = 0.7f;
    [SerializeField, Range(0, 1)] private float neutralBaseVolume = 0.5f;   // Always plays at some level
    
    #endregion
    
    #region Private State
    
    private struct MusicState
    {
        public float neutralVolume;
        public float lightVolume;
        public float darkVolume;
        public float eclipseVolume;
        
        public static MusicState Lerp(MusicState a, MusicState b, float t)
        {
            return new MusicState
            {
                neutralVolume = Mathf.Lerp(a.neutralVolume, b.neutralVolume, t),
                lightVolume = Mathf.Lerp(a.lightVolume, b.lightVolume, t),
                darkVolume = Mathf.Lerp(a.darkVolume, b.darkVolume, t),
                eclipseVolume = Mathf.Lerp(a.eclipseVolume, b.eclipseVolume, t)
            };
        }
        
        public bool ApproximatelyEquals(MusicState other, float tolerance = 0.01f)
        {
            return Mathf.Abs(neutralVolume - other.neutralVolume) < tolerance &&
                   Mathf.Abs(lightVolume - other.lightVolume) < tolerance &&
                   Mathf.Abs(darkVolume - other.darkVolume) < tolerance &&
                   Mathf.Abs(eclipseVolume - other.eclipseVolume) < tolerance;
        }
    }
    
    private MusicState currentState;
    private MusicState targetState;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Start()
    {
        InitializeAudioSources();
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
        currentState = MusicState.Lerp(currentState, targetState, t);
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
    
    private void InitializeAudioSources()
    {
        // Configure all audio sources
        ConfigureSource(neutralMusic);
        ConfigureSource(lightMusic);
        ConfigureSource(darkMusic);
        ConfigureSource(eclipseMusic);
        
        // Start playing (volumes will be set by state)
        PlaySource(neutralMusic);
        PlaySource(lightMusic);
        PlaySource(darkMusic);
        PlaySource(eclipseMusic);
    }
    
    private void ConfigureSource(AudioSource source)
    {
        if (source == null) return;
        
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
    }
    
    private void PlaySource(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying)
            source.Play();
    }
    
    private void InitializeState()
    {
        currentState = new MusicState
        {
            neutralVolume = neutralBaseVolume,
            lightVolume = 0f,
            darkVolume = 0f,
            eclipseVolume = 0f
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
    
    private MusicState CalculateTargetState(int left, int right)
    {
        var state = new MusicState();
        
        // Perfect Balance - Eclipse
        if (left == 5 && right == 5)
        {
            state.neutralVolume = neutralBaseVolume * 0.3f;  // Dim neutral
            state.lightVolume = 0f;
            state.darkVolume = 0f;
            state.eclipseVolume = 1f;                         // Full eclipse music
            return state;
        }
        
        // No eclipse music outside perfect balance
        state.eclipseVolume = 0f;
        
        // Calculate balance (-5 to +5, normalized to -1 to +1)
        float balance = (right - left) / 5f;
        
        if (balance >= 0)
        {
            // More light
            state.lightVolume = balance;                                      // Fade in light music
            state.darkVolume = 0f;
            state.neutralVolume = Mathf.Lerp(neutralBaseVolume, neutralBaseVolume * 0.5f, balance);
        }
        else
        {
            // More dark
            float darkAmount = -balance;
            state.lightVolume = 0f;
            state.darkVolume = darkAmount;                                    // Fade in dark music
            state.neutralVolume = Mathf.Lerp(neutralBaseVolume, neutralBaseVolume * 0.3f, darkAmount);
        }
        
        return state;
    }
    
    #endregion
    
    #region Apply State
    
    private void ApplyState(MusicState state)
    {
        SetVolume(neutralMusic, state.neutralVolume);
        SetVolume(lightMusic, state.lightVolume);
        SetVolume(darkMusic, state.darkVolume);
        SetVolume(eclipseMusic, state.eclipseVolume);
    }
    
    private void SetVolume(AudioSource source, float volume)
    {
        if (source != null)
            source.volume = volume * masterVolume;
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
    
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyState(currentState);
    }
    
    public void PauseAll()
    {
        if (neutralMusic != null) neutralMusic.Pause();
        if (lightMusic != null) lightMusic.Pause();
        if (darkMusic != null) darkMusic.Pause();
        if (eclipseMusic != null) eclipseMusic.Pause();
    }
    
    public void ResumeAll()
    {
        if (neutralMusic != null) neutralMusic.UnPause();
        if (lightMusic != null) lightMusic.UnPause();
        if (darkMusic != null) darkMusic.UnPause();
        if (eclipseMusic != null) eclipseMusic.UnPause();
    }
    
    #endregion
}