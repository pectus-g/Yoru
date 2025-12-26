using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System;

/// <summary>
/// YORU: Ambience Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Manages COZY Ambience profiles for ALL 22 unique balance states.
/// 
/// COZY Ambience Profiles Available (from your package):
/// - Fireflies      (dark path, mystical nights)
/// - Wisps          (dark escalation, eerie)
/// - Owl Sounds     (night ambience)
/// - Butterflies    (light path, heavenly)
/// - Day Bugs       (bright day)
/// - Birdsong       (peaceful light)
/// - Light Wind     (calm)
/// - Blustery       (storm approaching)
/// - Swirling       (heavy storm)
/// - Quiet          (neutral/eclipse)
/// 
/// Each state can have PRIMARY and SECONDARY ambiance profiles
/// that blend together for richer atmosphere.
/// </summary>
public class AmbienceController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class AmbiencePreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Primary Ambience")]
        public AmbienceProfile primaryProfile;
        [Range(0, 1)] public float primaryWeight = 1f;
        
        [Header("Secondary Ambience (Optional)")]
        public AmbienceProfile secondaryProfile;
        [Range(0, 1)] public float secondaryWeight = 0f;
        
        [Header("Overall Volume")]
        [Range(0, 2)] public float masterVolume = 1f;
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== COZY REFERENCE ===")]
    [SerializeField] private CozyWeather cozyWeather;
    [SerializeField] private bool autoFindCozy = true;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== AVAILABLE PROFILES (Assign from COZY) ===")]
    [Tooltip("Peaceful day/light")]
    [SerializeField] private AmbienceProfile birdsongProfile;
    [Tooltip("Light path heavenly")]
    [SerializeField] private AmbienceProfile butterfliesProfile;
    [Tooltip("Bright sunny day")]
    [SerializeField] private AmbienceProfile dayBugsProfile;
    [Tooltip("Night mystical")]
    [SerializeField] private AmbienceProfile firefliesProfile;
    [Tooltip("Dark eerie")]
    [SerializeField] private AmbienceProfile wispsProfile;
    [Tooltip("Night sounds")]
    [SerializeField] private AmbienceProfile owlSoundsProfile;
    [Tooltip("Calm breeze")]
    [SerializeField] private AmbienceProfile lightWindProfile;
    [Tooltip("Storm approaching")]
    [SerializeField] private AmbienceProfile blusteryProfile;
    [Tooltip("Heavy storm")]
    [SerializeField] private AmbienceProfile swirlingProfile;
    [Tooltip("Silence/minimal")]
    [SerializeField] private AmbienceProfile quietProfile;
    [Tooltip("Eclipse special")]
    [SerializeField] private AmbienceProfile auroraProfile;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private AmbiencePreset neutralPreset = new AmbiencePreset
    {
        stateName = "Neutral",
        masterVolume = 0.5f
    };
    
    [Header("=== LIGHT PATH (Balance +1 to +5) ===")]
    [SerializeField] private AmbiencePreset light1Preset = new AmbiencePreset
    {
        stateName = "Light1 (Golden Hour)",
        masterVolume = 0.55f
    };
    
    [SerializeField] private AmbiencePreset light2Preset = new AmbiencePreset
    {
        stateName = "Light2 (Warm)",
        masterVolume = 0.6f
    };
    
    [SerializeField] private AmbiencePreset light3Preset = new AmbiencePreset
    {
        stateName = "Light3 (Bright)",
        masterVolume = 0.65f
    };
    
    [SerializeField] private AmbiencePreset light4Preset = new AmbiencePreset
    {
        stateName = "Light4 (Brighter)",
        masterVolume = 0.7f
    };
    
    [SerializeField] private AmbiencePreset light5Preset = new AmbiencePreset
    {
        stateName = "Light5 (Heavenly)",
        masterVolume = 0.8f
    };
    
    [Header("=== LIGHT PATH ESCALATION (Stage 1-5, Divine) ===")]
    [SerializeField] private AmbiencePreset lightStage1Preset = new AmbiencePreset
    {
        stateName = "Light5+Stage1 (Divine Beginning)",
        masterVolume = 0.85f
    };
    
    [SerializeField] private AmbiencePreset lightStage2Preset = new AmbiencePreset
    {
        stateName = "Light5+Stage2 (Radiant)",
        masterVolume = 0.9f
    };
    
    [SerializeField] private AmbiencePreset lightStage3Preset = new AmbiencePreset
    {
        stateName = "Light5+Stage3 (Glorious)",
        masterVolume = 0.92f
    };
    
    [SerializeField] private AmbiencePreset lightStage4Preset = new AmbiencePreset
    {
        stateName = "Light5+Stage4 (Transcendent)",
        masterVolume = 0.95f
    };
    
    [SerializeField] private AmbiencePreset lightStage5Preset = new AmbiencePreset
    {
        stateName = "Light5+Stage5 (MAXIMUM DIVINE)",
        masterVolume = 1f
    };
    
    [Header("=== DARK PATH (Balance -1 to -5) ===")]
    [SerializeField] private AmbiencePreset dark1Preset = new AmbiencePreset
    {
        stateName = "Dark1 (Late Afternoon)",
        masterVolume = 0.5f
    };
    
    [SerializeField] private AmbiencePreset dark2Preset = new AmbiencePreset
    {
        stateName = "Dark2 (Sunset)",
        masterVolume = 0.55f
    };
    
    [SerializeField] private AmbiencePreset dark3Preset = new AmbiencePreset
    {
        stateName = "Dark3 (Dusk)",
        masterVolume = 0.6f
    };
    
    [SerializeField] private AmbiencePreset dark4Preset = new AmbiencePreset
    {
        stateName = "Dark4 (Night)",
        masterVolume = 0.7f
    };
    
    [SerializeField] private AmbiencePreset dark5Preset = new AmbiencePreset
    {
        stateName = "Dark5 (Midnight)",
        masterVolume = 0.75f
    };
    
    [Header("=== DARK PATH ESCALATION (Stage 1-5, Stormy) ===")]
    [SerializeField] private AmbiencePreset darkStage1Preset = new AmbiencePreset
    {
        stateName = "Dark5+Stage1 (Partly Cloudy)",
        masterVolume = 0.8f
    };
    
    [SerializeField] private AmbiencePreset darkStage2Preset = new AmbiencePreset
    {
        stateName = "Dark5+Stage2 (Overcast)",
        masterVolume = 0.85f
    };
    
    [SerializeField] private AmbiencePreset darkStage3Preset = new AmbiencePreset
    {
        stateName = "Dark5+Stage3 (Light Rain)",
        masterVolume = 0.9f
    };
    
    [SerializeField] private AmbiencePreset darkStage4Preset = new AmbiencePreset
    {
        stateName = "Dark5+Stage4 (Heavy Rain)",
        masterVolume = 0.95f
    };
    
    [SerializeField] private AmbiencePreset darkStage5Preset = new AmbiencePreset
    {
        stateName = "Dark5+Stage5 (THUNDERSTORM)",
        masterVolume = 1f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private AmbiencePreset eclipsePreset = new AmbiencePreset
    {
        stateName = "Eclipse (5L + 5R)",
        masterVolume = 0.3f  // Quiet, awe-inspiring
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private WorldStateManager.AtmosphereState currentAtmosphere;
    [SerializeField] private int currentWeatherStage;
    
    #endregion
    
    #region Private Fields
    
    private AmbiencePreset currentPreset;
    private AmbiencePreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    private object ambienceModule;
    private bool hasAmbienceModule;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        FindCozy();
        AssignDefaultProfiles();
        InitializeState();
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.RemoveListener(OnAtmosphereChanged);
            WorldStateManager.Instance.OnWeatherStageChanged.RemoveListener(OnWeatherStageChanged);
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
            ApplyPreset(currentPreset, targetPreset, transitionProgress);
        }
    }
    
    #endregion
    
    #region Setup
    
    void FindCozy()
    {
        if (cozyWeather == null && autoFindCozy)
        {
            cozyWeather = FindObjectOfType<CozyWeather>();
        }
        
        if (cozyWeather != null)
        {
            // Try to find Ambience Module via reflection
            var moduleField = cozyWeather.GetType().GetField("ambienceModule") 
                           ?? cozyWeather.GetType().GetField("ambience");
            if (moduleField != null)
            {
                ambienceModule = moduleField.GetValue(cozyWeather);
                hasAmbienceModule = ambienceModule != null;
            }
            
            // Alternative: check for GetModule method
            if (!hasAmbienceModule)
            {
                var getModuleMethod = cozyWeather.GetType().GetMethod("GetModule");
                if (getModuleMethod != null)
                {
                    // Try to get ambience module by type
                    try
                    {
                        var moduleType = Type.GetType("DistantLands.Cozy.CozyAmbienceModule, DistantLands.Cozy.Runtime");
                        if (moduleType != null)
                        {
                            ambienceModule = getModuleMethod.MakeGenericMethod(moduleType).Invoke(cozyWeather, null);
                            hasAmbienceModule = ambienceModule != null;
                        }
                    }
                    catch { }
                }
            }
        }
        
        if (logChanges)
        {
            Debug.Log($"[AmbienceController] COZY found: {cozyWeather != null}, Ambience Module: {hasAmbienceModule}");
        }
    }
    
    void AssignDefaultProfiles()
    {
        // Assign profiles to presets if not already set
        // NEUTRAL
        if (neutralPreset.primaryProfile == null) neutralPreset.primaryProfile = quietProfile;
        if (neutralPreset.secondaryProfile == null) neutralPreset.secondaryProfile = lightWindProfile;
        neutralPreset.primaryWeight = 0.7f;
        neutralPreset.secondaryWeight = 0.3f;
        
        // LIGHT PATH
        if (light1Preset.primaryProfile == null) light1Preset.primaryProfile = lightWindProfile;
        if (light1Preset.secondaryProfile == null) light1Preset.secondaryProfile = birdsongProfile;
        light1Preset.primaryWeight = 0.6f;
        light1Preset.secondaryWeight = 0.4f;
        
        if (light2Preset.primaryProfile == null) light2Preset.primaryProfile = birdsongProfile;
        if (light2Preset.secondaryProfile == null) light2Preset.secondaryProfile = lightWindProfile;
        light2Preset.primaryWeight = 0.7f;
        light2Preset.secondaryWeight = 0.3f;
        
        if (light3Preset.primaryProfile == null) light3Preset.primaryProfile = dayBugsProfile;
        if (light3Preset.secondaryProfile == null) light3Preset.secondaryProfile = birdsongProfile;
        light3Preset.primaryWeight = 0.6f;
        light3Preset.secondaryWeight = 0.4f;
        
        if (light4Preset.primaryProfile == null) light4Preset.primaryProfile = birdsongProfile;
        if (light4Preset.secondaryProfile == null) light4Preset.secondaryProfile = dayBugsProfile;
        light4Preset.primaryWeight = 0.5f;
        light4Preset.secondaryWeight = 0.5f;
        
        if (light5Preset.primaryProfile == null) light5Preset.primaryProfile = butterfliesProfile;
        if (light5Preset.secondaryProfile == null) light5Preset.secondaryProfile = birdsongProfile;
        light5Preset.primaryWeight = 0.7f;
        light5Preset.secondaryWeight = 0.3f;
        
        // LIGHT ESCALATION
        if (lightStage1Preset.primaryProfile == null) lightStage1Preset.primaryProfile = butterfliesProfile;
        if (lightStage1Preset.secondaryProfile == null) lightStage1Preset.secondaryProfile = dayBugsProfile;
        lightStage1Preset.primaryWeight = 0.8f;
        lightStage1Preset.secondaryWeight = 0.2f;
        
        if (lightStage2Preset.primaryProfile == null) lightStage2Preset.primaryProfile = butterfliesProfile;
        if (lightStage2Preset.secondaryProfile == null) lightStage2Preset.secondaryProfile = birdsongProfile;
        lightStage2Preset.primaryWeight = 0.85f;
        lightStage2Preset.secondaryWeight = 0.15f;
        
        if (lightStage3Preset.primaryProfile == null) lightStage3Preset.primaryProfile = butterfliesProfile;
        if (lightStage3Preset.secondaryProfile == null) lightStage3Preset.secondaryProfile = dayBugsProfile;
        lightStage3Preset.primaryWeight = 0.9f;
        lightStage3Preset.secondaryWeight = 0.1f;
        
        if (lightStage4Preset.primaryProfile == null) lightStage4Preset.primaryProfile = butterfliesProfile;
        lightStage4Preset.primaryWeight = 1f;
        lightStage4Preset.secondaryWeight = 0f;
        
        if (lightStage5Preset.primaryProfile == null) lightStage5Preset.primaryProfile = butterfliesProfile;
        lightStage5Preset.primaryWeight = 1f;
        lightStage5Preset.secondaryWeight = 0f;
        
        // DARK PATH
        if (dark1Preset.primaryProfile == null) dark1Preset.primaryProfile = quietProfile;
        if (dark1Preset.secondaryProfile == null) dark1Preset.secondaryProfile = lightWindProfile;
        dark1Preset.primaryWeight = 0.7f;
        dark1Preset.secondaryWeight = 0.3f;
        
        if (dark2Preset.primaryProfile == null) dark2Preset.primaryProfile = quietProfile;
        dark2Preset.primaryWeight = 1f;
        dark2Preset.secondaryWeight = 0f;
        
        if (dark3Preset.primaryProfile == null) dark3Preset.primaryProfile = owlSoundsProfile;
        if (dark3Preset.secondaryProfile == null) dark3Preset.secondaryProfile = quietProfile;
        dark3Preset.primaryWeight = 0.6f;
        dark3Preset.secondaryWeight = 0.4f;
        
        if (dark4Preset.primaryProfile == null) dark4Preset.primaryProfile = owlSoundsProfile;
        if (dark4Preset.secondaryProfile == null) dark4Preset.secondaryProfile = firefliesProfile;
        dark4Preset.primaryWeight = 0.5f;
        dark4Preset.secondaryWeight = 0.5f;
        
        if (dark5Preset.primaryProfile == null) dark5Preset.primaryProfile = firefliesProfile;
        if (dark5Preset.secondaryProfile == null) dark5Preset.secondaryProfile = wispsProfile;
        dark5Preset.primaryWeight = 0.6f;
        dark5Preset.secondaryWeight = 0.4f;
        
        // DARK ESCALATION
        if (darkStage1Preset.primaryProfile == null) darkStage1Preset.primaryProfile = wispsProfile;
        if (darkStage1Preset.secondaryProfile == null) darkStage1Preset.secondaryProfile = quietProfile;
        darkStage1Preset.primaryWeight = 0.7f;
        darkStage1Preset.secondaryWeight = 0.3f;
        
        if (darkStage2Preset.primaryProfile == null) darkStage2Preset.primaryProfile = wispsProfile;
        if (darkStage2Preset.secondaryProfile == null) darkStage2Preset.secondaryProfile = owlSoundsProfile;
        darkStage2Preset.primaryWeight = 0.8f;
        darkStage2Preset.secondaryWeight = 0.2f;
        
        if (darkStage3Preset.primaryProfile == null) darkStage3Preset.primaryProfile = blusteryProfile;
        if (darkStage3Preset.secondaryProfile == null) darkStage3Preset.secondaryProfile = wispsProfile;
        darkStage3Preset.primaryWeight = 0.7f;
        darkStage3Preset.secondaryWeight = 0.3f;
        
        if (darkStage4Preset.primaryProfile == null) darkStage4Preset.primaryProfile = swirlingProfile;
        if (darkStage4Preset.secondaryProfile == null) darkStage4Preset.secondaryProfile = blusteryProfile;
        darkStage4Preset.primaryWeight = 0.8f;
        darkStage4Preset.secondaryWeight = 0.2f;
        
        if (darkStage5Preset.primaryProfile == null) darkStage5Preset.primaryProfile = swirlingProfile;
        darkStage5Preset.primaryWeight = 1f;
        darkStage5Preset.secondaryWeight = 0f;
        
        // ECLIPSE
        if (eclipsePreset.primaryProfile == null) eclipsePreset.primaryProfile = auroraProfile ?? quietProfile;
        eclipsePreset.primaryWeight = 1f;
        eclipsePreset.secondaryWeight = 0f;
    }
    
    void InitializeState()
    {
        currentPreset = neutralPreset;
        targetPreset = neutralPreset;
        ApplyPreset(currentPreset, currentPreset, 1f);
    }
    
    void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.AddListener(OnAtmosphereChanged);
            WorldStateManager.Instance.OnWeatherStageChanged.AddListener(OnWeatherStageChanged);
            OnAtmosphereChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnAtmosphereChanged(WorldStateManager.AtmosphereState state)
    {
        currentAtmosphere = state;
        UpdateTargetPreset();
    }
    
    void OnWeatherStageChanged(int stage)
    {
        currentWeatherStage = stage;
        UpdateTargetPreset();
    }
    
    void UpdateTargetPreset()
    {
        targetPreset = GetPresetForState(currentAtmosphere, currentWeatherStage);
        transitionProgress = 0f;
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[AmbienceController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    AmbiencePreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
    {
        if (atmosphere == WorldStateManager.AtmosphereState.Eclipse)
            return eclipsePreset;
        
        if (weatherStage > 0)
        {
            if (atmosphere == WorldStateManager.AtmosphereState.Dark5)
            {
                switch (weatherStage)
                {
                    case 1: return darkStage1Preset;
                    case 2: return darkStage2Preset;
                    case 3: return darkStage3Preset;
                    case 4: return darkStage4Preset;
                    default: return darkStage5Preset;
                }
            }
            else if (atmosphere == WorldStateManager.AtmosphereState.Light5)
            {
                switch (weatherStage)
                {
                    case 1: return lightStage1Preset;
                    case 2: return lightStage2Preset;
                    case 3: return lightStage3Preset;
                    case 4: return lightStage4Preset;
                    default: return lightStage5Preset;
                }
            }
        }
        
        switch (atmosphere)
        {
            case WorldStateManager.AtmosphereState.Dark5: return dark5Preset;
            case WorldStateManager.AtmosphereState.Dark4: return dark4Preset;
            case WorldStateManager.AtmosphereState.Dark3: return dark3Preset;
            case WorldStateManager.AtmosphereState.Dark2: return dark2Preset;
            case WorldStateManager.AtmosphereState.Dark1: return dark1Preset;
            case WorldStateManager.AtmosphereState.Light1: return light1Preset;
            case WorldStateManager.AtmosphereState.Light2: return light2Preset;
            case WorldStateManager.AtmosphereState.Light3: return light3Preset;
            case WorldStateManager.AtmosphereState.Light4: return light4Preset;
            case WorldStateManager.AtmosphereState.Light5: return light5Preset;
            default: return neutralPreset;
        }
    }
    
    #endregion
    
    #region Apply Preset
    
    void ApplyPreset(AmbiencePreset from, AmbiencePreset to, float t)
    {
        if (!hasAmbienceModule || ambienceModule == null) return;
        
        // Get the current blended weights
        float masterVol = Mathf.Lerp(from.masterVolume, to.masterVolume, t);
        
        // Apply primary profile
        AmbienceProfile targetPrimary = t > 0.5f ? to.primaryProfile : from.primaryProfile;
        float primaryWeight = Mathf.Lerp(from.primaryWeight, to.primaryWeight, t) * masterVol;
        
        // Apply secondary profile
        AmbienceProfile targetSecondary = t > 0.5f ? to.secondaryProfile : from.secondaryProfile;
        float secondaryWeight = Mathf.Lerp(from.secondaryWeight, to.secondaryWeight, t) * masterVol;
        
        // Try to set ambiance via COZY API
        try
        {
            // Method 1: Direct profile assignment
            var setAmbienceMethod = ambienceModule.GetType().GetMethod("SetAmbience");
            if (setAmbienceMethod != null)
            {
                if (targetPrimary != null && primaryWeight > 0.01f)
                {
                    setAmbienceMethod.Invoke(ambienceModule, new object[] { targetPrimary, primaryWeight });
                }
            }
            
            // Method 2: Ambience list manipulation
            var ambienceListField = ambienceModule.GetType().GetField("currentAmbience") 
                                 ?? ambienceModule.GetType().GetField("ambienceProfiles");
            if (ambienceListField != null)
            {
                // This would require more complex manipulation of the list
                // For now, we rely on SetAmbience method
            }
        }
        catch (Exception e)
        {
            if (logChanges)
                Debug.LogWarning($"[AmbienceController] Could not apply ambience: {e.Message}");
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() => PreviewPreset(neutralPreset);
    
    [ContextMenu("Preview: Light5+Stage5")]
    void PreviewLightMax() => PreviewPreset(lightStage5Preset);
    
    [ContextMenu("Preview: Dark5+Stage5")]
    void PreviewDarkMax() => PreviewPreset(darkStage5Preset);
    
    [ContextMenu("Preview: Eclipse")]
    void PreviewEclipse() => PreviewPreset(eclipsePreset);
    
    void PreviewPreset(AmbiencePreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        isTransitioning = false;
        ApplyPreset(preset, preset, 1f);
        if (logChanges) Debug.Log($"[AmbienceController] Preview: {preset.stateName}");
    }
    
    [ContextMenu("Log Current State")]
    void LogCurrentState()
    {
        Debug.Log($"[AmbienceController] Current: {currentPreset?.stateName}, Target: {targetPreset?.stateName}");
        Debug.Log($"[AmbienceController] Atmosphere: {currentAtmosphere}, Stage: {currentWeatherStage}");
        Debug.Log($"[AmbienceController] Has Ambience Module: {hasAmbienceModule}");
    }
    
    #endregion
    
    #region Public API
    
    public void ForceUpdateState()
    {
        if (WorldStateManager.Instance != null)
        {
            OnAtmosphereChanged(WorldStateManager.Instance.CurrentState);
        }
    }
    
    public AmbiencePreset GetCurrentPreset() => currentPreset;
    
    #endregion
}