using UnityEngine;
using System;

/// <summary>
/// YORU: Lighting Controller V3 - COMPLETE 22-STATE SYSTEM
/// 
/// Handles ALL 22 unique atmosphere states:
/// - 11 base states (Eclipse, Dark5-1, Neutral, Light1-5)
/// - 5 dark escalation stages (Dark5 + Stage1-5)
/// - 5 light escalation stages (Light5 + Stage1-5)
/// 
/// Features:
/// - Sun control (intensity, color, angle)
/// - Moon control (auto-created, for dark path visibility)
/// - Divine Glow light (for light path escalation)
/// - Per-state tweakable presets in Inspector
/// - Smooth transitions between states
/// </summary>
public class LightingController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class LightingPreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Sun Settings")]
        public Color sunColor = Color.white;
        [Range(0, 3)] public float sunIntensity = 1f;
        [Range(0, 90)] public float sunAngle = 50f;
        
        [Header("Moon Settings (Dark Path)")]
        public bool moonEnabled = false;
        public Color moonColor = new Color(0.6f, 0.7f, 0.95f);
        [Range(0, 3)] public float moonIntensity = 0.3f;  // Increased range for visibility
        
        [Header("Dark Path Light (Point light for visibility)")]
        [Range(0, 10)] public float darkPathIntensity = 0f;
        
        [Header("Ambient Settings")]
        public Color ambientColor = new Color(0.25f, 0.25f, 0.28f);
        [Range(0, 2)] public float ambientIntensity = 1f;
        
        [Header("Shadow Settings")]
        [Range(0, 1)] public float shadowStrength = 0.7f;
        
        [Header("Divine Glow (Light Escalation Only)")]
        [Range(0, 3)] public float divineGlowIntensity = 0f;
        
        public static LightingPreset Lerp(LightingPreset a, LightingPreset b, float t)
        {
            return new LightingPreset
            {
                stateName = b.stateName,
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                sunAngle = Mathf.Lerp(a.sunAngle, b.sunAngle, t),
                moonEnabled = t > 0.5f ? b.moonEnabled : a.moonEnabled,
                moonColor = Color.Lerp(a.moonColor, b.moonColor, t),
                moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t),
                darkPathIntensity = Mathf.Lerp(a.darkPathIntensity, b.darkPathIntensity, t),
                ambientColor = Color.Lerp(a.ambientColor, b.ambientColor, t),
                ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
                shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t),
                divineGlowIntensity = Mathf.Lerp(a.divineGlowIntensity, b.divineGlowIntensity, t)
            };
        }
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [SerializeField] private Light darkPathLight;  // Point light for dark path visibility
    [SerializeField] private Light divineGlowLight;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool autoFindLights = true;
    [SerializeField] private bool autoCreateMoonAndGlow = true;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private LightingPreset neutralPreset = new LightingPreset
    {
        stateName = "Neutral (Balance 0)",
        sunColor = new Color(1f, 0.98f, 0.95f),
        sunIntensity = 1f,
        sunAngle = 50f,
        moonEnabled = false,
        moonIntensity = 0f,
        darkPathIntensity = 0f,
        ambientColor = new Color(0.25f, 0.25f, 0.28f),
        shadowStrength = 0.7f
    };
    
    [Header("=== LIGHT PATH (Balance +1 to +5) ===")]
    [SerializeField] private LightingPreset light1Preset = new LightingPreset
    {
        stateName = "Light1 (+1, Golden Hour)",
        sunColor = new Color(1f, 0.85f, 0.6f),
        sunIntensity = 1.0f,
        sunAngle = 30f,
        ambientColor = new Color(0.3f, 0.27f, 0.22f),
        shadowStrength = 0.6f
    };
    
    [SerializeField] private LightingPreset light2Preset = new LightingPreset
    {
        stateName = "Light2 (+2, Warm Afternoon)",
        sunColor = new Color(1f, 0.9f, 0.7f),
        sunIntensity = 1.1f,
        sunAngle = 38f,
        ambientColor = new Color(0.32f, 0.3f, 0.25f),
        shadowStrength = 0.55f
    };
    
    [SerializeField] private LightingPreset light3Preset = new LightingPreset
    {
        stateName = "Light3 (+3, Bright)",
        sunColor = new Color(1f, 0.95f, 0.8f),
        sunIntensity = 1.2f,
        sunAngle = 45f,
        ambientColor = new Color(0.35f, 0.34f, 0.3f),
        shadowStrength = 0.5f
    };
    
    [SerializeField] private LightingPreset light4Preset = new LightingPreset
    {
        stateName = "Light4 (+4, Brighter)",
        sunColor = new Color(1f, 0.98f, 0.9f),
        sunIntensity = 1.3f,
        sunAngle = 55f,
        ambientColor = new Color(0.4f, 0.38f, 0.35f),
        shadowStrength = 0.45f
    };
    
    [SerializeField] private LightingPreset light5Preset = new LightingPreset
    {
        stateName = "Light5 (+5, Heavenly)",
        sunColor = new Color(1f, 1f, 0.95f),
        sunIntensity = 1.4f,
        sunAngle = 65f,
        ambientColor = new Color(0.45f, 0.43f, 0.4f),
        shadowStrength = 0.35f
    };
    
    [Header("=== LIGHT PATH ESCALATION (Stage 1-5, Divine) ===")]
    [SerializeField] private LightingPreset lightStage1Preset = new LightingPreset
    {
        stateName = "Light5+Stage1 (+6, Divine Beginning)",
        sunColor = new Color(1f, 1f, 0.96f),
        sunIntensity = 1.45f,
        sunAngle = 68f,
        ambientColor = new Color(0.48f, 0.46f, 0.43f),
        shadowStrength = 0.32f,
        divineGlowIntensity = 0.1f
    };
    
    [SerializeField] private LightingPreset lightStage2Preset = new LightingPreset
    {
        stateName = "Light5+Stage2 (+7, Radiant)",
        sunColor = new Color(1f, 1f, 0.97f),
        sunIntensity = 1.5f,
        sunAngle = 70f,
        ambientColor = new Color(0.5f, 0.48f, 0.45f),
        shadowStrength = 0.3f,
        divineGlowIntensity = 0.2f
    };
    
    [SerializeField] private LightingPreset lightStage3Preset = new LightingPreset
    {
        stateName = "Light5+Stage3 (+8, Glorious)",
        sunColor = new Color(1f, 1f, 0.98f),
        sunIntensity = 1.55f,
        sunAngle = 72f,
        ambientColor = new Color(0.53f, 0.51f, 0.48f),
        shadowStrength = 0.28f,
        divineGlowIntensity = 0.35f
    };
    
    [SerializeField] private LightingPreset lightStage4Preset = new LightingPreset
    {
        stateName = "Light5+Stage4 (+9, Transcendent)",
        sunColor = new Color(1f, 1f, 0.99f),
        sunIntensity = 1.6f,
        sunAngle = 74f,
        ambientColor = new Color(0.56f, 0.54f, 0.5f),
        shadowStrength = 0.25f,
        divineGlowIntensity = 0.5f
    };
    
    [SerializeField] private LightingPreset lightStage5Preset = new LightingPreset
    {
        stateName = "Light5+Stage5 (+10, MAXIMUM DIVINE)",
        sunColor = new Color(1f, 1f, 1f),
        sunIntensity = 1.65f,
        sunAngle = 76f,
        ambientColor = new Color(0.6f, 0.57f, 0.53f),
        shadowStrength = 0.22f,
        divineGlowIntensity = 0.7f
    };
    
    [Header("=== DARK PATH (Balance -1 to -5) ===")]
    [SerializeField] private LightingPreset dark1Preset = new LightingPreset
    {
        stateName = "Dark1 (-1, Late Afternoon)",
        sunColor = new Color(1f, 0.9f, 0.7f),
        sunIntensity = 0.95f,
        sunAngle = 35f,
        moonEnabled = false,
        moonIntensity = 0f,
        darkPathIntensity = 0f,  // Daytime, no need
        ambientColor = new Color(0.4f, 0.38f, 0.4f),
        shadowStrength = 0.72f
    };
    
    [SerializeField] private LightingPreset dark2Preset = new LightingPreset
    {
        stateName = "Dark2 (-2, Sunset)",
        sunColor = new Color(1f, 0.7f, 0.4f),
        sunIntensity = 0.85f,
        sunAngle = 20f,
        moonEnabled = false,
        moonIntensity = 0f,
        darkPathIntensity = 0.3f,  // Starting to add some fill light
        ambientColor = new Color(0.38f, 0.34f, 0.38f),
        shadowStrength = 0.75f
    };
    
    [SerializeField] private LightingPreset dark3Preset = new LightingPreset
    {
        stateName = "Dark3 (-3, Dusk)",
        sunColor = new Color(0.8f, 0.7f, 0.9f),
        sunIntensity = 0.6f,
        sunAngle = 15f,
        moonEnabled = true,
        moonColor = new Color(0.7f, 0.8f, 1f),
        moonIntensity = 1.0f,
        darkPathIntensity = 0.8f,  // More fill light as sun sets
        ambientColor = new Color(0.35f, 0.33f, 0.4f),
        shadowStrength = 0.8f
    };
    
    [SerializeField] private LightingPreset dark4Preset = new LightingPreset
    {
        stateName = "Dark4 (-4, Night)",
        sunColor = new Color(0.6f, 0.7f, 0.95f),
        sunIntensity = 0.4f,
        sunAngle = 60f,
        moonEnabled = true,
        moonColor = new Color(0.7f, 0.8f, 1f),
        moonIntensity = 1.5f,
        darkPathIntensity = 1.2f,  // Night needs more
        ambientColor = new Color(0.3f, 0.28f, 0.35f),
        shadowStrength = 0.7f
    };
    
    [SerializeField] private LightingPreset dark5Preset = new LightingPreset
    {
        stateName = "Dark5 (-5, Midnight - VISIBLE!)",
        sunColor = new Color(0.5f, 0.6f, 0.9f),
        sunIntensity = 0.3f,
        sunAngle = 70f,
        moonEnabled = true,
        moonColor = new Color(0.7f, 0.85f, 1f),
        moonIntensity = 2.0f,
        darkPathIntensity = 1.5f,  // Clear midnight
        ambientColor = new Color(0.25f, 0.25f, 0.32f),
        shadowStrength = 0.6f
    };
    
    [Header("=== DARK PATH ESCALATION (Stage 1-5, Stormy but VISIBLE) ===")]
    [SerializeField] private LightingPreset darkStage1Preset = new LightingPreset
    {
        stateName = "Dark5+Stage1 (-6, Partly Cloudy)",
        sunColor = new Color(0.48f, 0.56f, 0.85f),
        sunIntensity = 0.25f,
        sunAngle = 72f,
        moonEnabled = true,
        moonColor = new Color(0.65f, 0.75f, 0.95f),
        moonIntensity = 1.8f,
        darkPathIntensity = 2.0f,  // Clouds reduce moon, need more fill
        ambientColor = new Color(0.22f, 0.22f, 0.3f),
        shadowStrength = 0.55f
    };
    
    [SerializeField] private LightingPreset darkStage2Preset = new LightingPreset
    {
        stateName = "Dark5+Stage2 (-7, Overcast)",
        sunColor = new Color(0.45f, 0.52f, 0.8f),
        sunIntensity = 0.2f,
        sunAngle = 74f,
        moonEnabled = true,
        moonColor = new Color(0.6f, 0.7f, 0.9f),
        moonIntensity = 1.5f,
        darkPathIntensity = 3.0f,  // Heavy clouds, much more fill needed
        ambientColor = new Color(0.2f, 0.2f, 0.28f),
        shadowStrength = 0.5f
    };
    
    [SerializeField] private LightingPreset darkStage3Preset = new LightingPreset
    {
        stateName = "Dark5+Stage3 (-8, Light Rain)",
        sunColor = new Color(0.42f, 0.48f, 0.75f),
        sunIntensity = 0.18f,
        sunAngle = 75f,
        moonEnabled = true,
        moonColor = new Color(0.55f, 0.65f, 0.85f),
        moonIntensity = 1.2f,
        darkPathIntensity = 4.0f,  // Rain + clouds = more fill light
        ambientColor = new Color(0.18f, 0.18f, 0.25f),
        shadowStrength = 0.45f
    };
    
    [SerializeField] private LightingPreset darkStage4Preset = new LightingPreset
    {
        stateName = "Dark5+Stage4 (-9, Heavy Rain)",
        sunColor = new Color(0.39f, 0.44f, 0.7f),
        sunIntensity = 0.15f,
        sunAngle = 76f,
        moonEnabled = true,
        moonColor = new Color(0.5f, 0.6f, 0.8f),
        moonIntensity = 1.0f,
        darkPathIntensity = 5.0f,  // Heavy storm, big fill light
        ambientColor = new Color(0.16f, 0.16f, 0.22f),
        shadowStrength = 0.4f
    };
    
    [SerializeField] private LightingPreset darkStage5Preset = new LightingPreset
    {
        stateName = "Dark5+Stage5 (-10, THUNDERSTORM)",
        sunColor = new Color(0.5f, 0.55f, 0.75f),
        sunIntensity = 0.12f,
        sunAngle = 78f,
        moonEnabled = true,
        moonColor = new Color(0.45f, 0.55f, 0.75f),
        moonIntensity = 0.8f,
        darkPathIntensity = 6.0f,  // MAXIMUM fill light for thunderstorm
        ambientColor = new Color(0.14f, 0.14f, 0.2f),
        shadowStrength = 0.35f
    };
    
    [Header("=== ECLIPSE ===")]
    [SerializeField] private LightingPreset eclipsePreset = new LightingPreset
    {
        stateName = "Eclipse (5L + 5R, Dramatic)",
        sunColor = new Color(1f, 0.4f, 0.2f),
        sunIntensity = 0.2f,
        sunAngle = 45f,
        moonEnabled = true,
        moonColor = new Color(0.5f, 0.38f, 0.8f),
        moonIntensity = 0.15f,
        ambientColor = new Color(0.1f, 0.08f, 0.15f),
        shadowStrength = 0.95f
    };
    
    [Header("=== DIVINE GLOW SETTINGS ===")]
    [SerializeField] private Color divineGlowColor = new Color(1f, 0.98f, 0.9f);
    [SerializeField] private float divineGlowRange = 15f;
    [SerializeField] private Vector3 divineGlowOffset = new Vector3(0, 3f, 0);
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private WorldStateManager.AtmosphereState currentAtmosphere;
    [SerializeField] private int currentWeatherStage;
    
    #endregion
    
    #region Private Fields
    
    private LightingPreset currentPreset;
    private LightingPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        SetupLights();
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
            else
            {
                currentPreset = LightingPreset.Lerp(currentPreset, targetPreset, transitionProgress);
            }
            ApplyPreset(currentPreset);
        }
        
        // Update point lights to follow player
        if (playerTransform != null)
        {
            // Dark path light follows player (for visibility in dark states)
            if (darkPathLight != null && darkPathLight.enabled)
            {
                darkPathLight.transform.position = playerTransform.position + Vector3.up * 5f;
            }
            
            // Divine glow follows player (for light escalation)
            if (divineGlowLight != null && divineGlowLight.enabled)
            {
                divineGlowLight.transform.position = playerTransform.position + divineGlowOffset;
            }
        }
    }
    
    #endregion
    
    #region Setup
    
    void SetupLights()
    {
        // Don't try to find/use COZY's sun - it controls its own sun
        // We only add supplementary lighting for visibility
        if (sunLight == null && autoFindLights)
        {
            Debug.Log("[LightingController] Note: Not controlling sun (COZY Weather manages it)");
        }
        
        // ALWAYS create our own moon light - don't use COZY's moon
        // Look for existing YORU_Moon first
        var existingMoon = transform.Find("YORU_Moon");
        if (existingMoon != null)
        {
            moonLight = existingMoon.GetComponent<Light>();
            if (logChanges) Debug.Log("[LightingController] ✓ Found existing YORU_Moon");
        }
        else if (autoCreateMoonAndGlow)
        {
            var moonObj = new GameObject("YORU_Moon");
            moonObj.transform.SetParent(transform);
            moonLight = moonObj.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.7f, 0.85f, 1f);  // Bright blue-white
            moonLight.intensity = 1.0f;  // START BRIGHT for visibility
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.2f;
            moonLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            moonLight.enabled = false;  // Will enable when dark
            if (logChanges) Debug.Log("[LightingController] ✓ Created YORU_Moon light");
        }
        
        // Create dark path visibility light (point light that follows player)
        var existingDarkLight = transform.Find("YORU_DarkPathLight");
        if (existingDarkLight != null)
        {
            darkPathLight = existingDarkLight.GetComponent<Light>();
            if (logChanges) Debug.Log("[LightingController] ✓ Found existing YORU_DarkPathLight");
        }
        else if (autoCreateMoonAndGlow)
        {
            var darkLightObj = new GameObject("YORU_DarkPathLight");
            darkLightObj.transform.SetParent(transform);
            darkPathLight = darkLightObj.AddComponent<Light>();  // ASSIGN TO REFERENCE
            darkPathLight.type = LightType.Point;
            darkPathLight.color = new Color(0.7f, 0.75f, 0.9f);  // Slightly brighter blue-white
            darkPathLight.intensity = 0f;  // Will be set by dark presets
            darkPathLight.range = 100f;  // LARGE range for area illumination
            darkPathLight.shadows = LightShadows.None;  // No shadows for performance
            if (logChanges) Debug.Log("[LightingController] ✓ Created YORU_DarkPathLight (range: 100)");
        }
        
        // Create divine glow for light path
        var existingGlow = transform.Find("YORU_DivineGlow");
        if (existingGlow != null)
        {
            divineGlowLight = existingGlow.GetComponent<Light>();
            if (logChanges) Debug.Log("[LightingController] ✓ Found existing YORU_DivineGlow");
        }
        else if (autoCreateMoonAndGlow)
        {
            var glowObj = new GameObject("YORU_DivineGlow");
            glowObj.transform.SetParent(transform);
            divineGlowLight = glowObj.AddComponent<Light>();
            divineGlowLight.type = LightType.Point;
            divineGlowLight.color = divineGlowColor;
            divineGlowLight.range = divineGlowRange;
            divineGlowLight.intensity = 0f;
            divineGlowLight.shadows = LightShadows.Soft;
            if (logChanges) Debug.Log("[LightingController] ✓ Created YORU_DivineGlow light");
        }
        
        // Find player
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) 
            {
                playerTransform = player.transform;
                if (logChanges) Debug.Log($"[LightingController] ✓ Found player: {player.name}");
            }
            else
            {
                Debug.LogWarning("[LightingController] ⚠ No player found! Tag your player as 'Player'");
            }
        }
        
        // Log current state
        if (logChanges)
        {
            Debug.Log($"[LightingController] Ambient mode: {RenderSettings.ambientMode}, color: {RenderSettings.ambientLight}");
        }
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
            WorldStateManager.Instance.OnStateChanged.AddListener(OnAtmosphereChanged);
            WorldStateManager.Instance.OnWeatherStageChanged.AddListener(OnWeatherStageChanged);
            
            // Initialize to current state
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
            Debug.Log($"[LightingController] Transitioning to: {targetPreset.stateName}");
    }
    
    #endregion
    
    #region State Resolution
    
    LightingPreset GetPresetForState(WorldStateManager.AtmosphereState atmosphere, int weatherStage)
    {
        // Eclipse is special
        if (atmosphere == WorldStateManager.AtmosphereState.Eclipse)
            return eclipsePreset;
        
        // Check for escalation stages
        if (weatherStage > 0)
        {
            // Dark path escalation
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
            // Light path escalation
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
        
        // Base atmosphere states
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
    
    void ApplyPreset(LightingPreset preset)
    {
        // Sun (only if found - COZY may control its own sun)
        if (sunLight != null)
        {
            sunLight.color = preset.sunColor;
            sunLight.intensity = preset.sunIntensity;
            sunLight.shadowStrength = preset.shadowStrength;
            sunLight.transform.rotation = Quaternion.Euler(preset.sunAngle, 170f, 0f);
        }
        
        // Moon - ALWAYS apply this for dark path visibility
        if (moonLight != null)
        {
            moonLight.enabled = preset.moonEnabled;
            if (preset.moonEnabled)
            {
                moonLight.color = preset.moonColor;
                moonLight.intensity = preset.moonIntensity;
            }
        }
        
        // CRITICAL: Set ambient mode to Color so our settings take effect
        // COZY might set this to Skybox which would override our values
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = preset.ambientColor * preset.ambientIntensity;
        
        // Also set ambient intensity for good measure
        RenderSettings.ambientIntensity = preset.ambientIntensity;
        
        // Dark Path Light - point light for visibility in dark states
        if (darkPathLight != null)
        {
            darkPathLight.intensity = preset.darkPathIntensity;
            darkPathLight.enabled = preset.darkPathIntensity > 0.01f;
            
            // Position at player if available
            if (playerTransform != null)
            {
                darkPathLight.transform.position = playerTransform.position + Vector3.up * 5f;
            }
        }
        
        // Divine Glow - for light path escalation
        if (divineGlowLight != null)
        {
            divineGlowLight.intensity = preset.divineGlowIntensity;
            divineGlowLight.enabled = preset.divineGlowIntensity > 0.01f;
            
            // Position at player if available
            if (playerTransform != null)
            {
                divineGlowLight.transform.position = playerTransform.position + divineGlowOffset;
            }
        }
    }
    
    #endregion
    
    #region Context Menu (For Testing)
    
    [ContextMenu("Preview: Neutral")]
    void PreviewNeutral() => PreviewPreset(neutralPreset);
    
    [ContextMenu("Preview: Light5")]
    void PreviewLight5() => PreviewPreset(light5Preset);
    
    [ContextMenu("Preview: Light5+Stage5 (Divine)")]
    void PreviewLightMax() => PreviewPreset(lightStage5Preset);
    
    [ContextMenu("Preview: Dark5")]
    void PreviewDark5() => PreviewPreset(dark5Preset);
    
    [ContextMenu("Preview: Dark5+Stage5 (Storm)")]
    void PreviewDarkMax() => PreviewPreset(darkStage5Preset);
    
    [ContextMenu("Preview: Eclipse")]
    void PreviewEclipse() => PreviewPreset(eclipsePreset);
    
    void PreviewPreset(LightingPreset preset)
    {
        currentPreset = preset;
        targetPreset = preset;
        transitionProgress = 1f;
        isTransitioning = false;
        ApplyPreset(preset);
        if (logChanges) Debug.Log($"[LightingController] Preview: {preset.stateName}");
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
    
    public void SetTransitionDuration(float duration)
    {
        transitionDuration = Mathf.Clamp(duration, 0.5f, 5f);
    }
    
    public LightingPreset GetCurrentPreset() => currentPreset;
    
    #endregion
}