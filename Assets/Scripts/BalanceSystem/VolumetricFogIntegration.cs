using UnityEngine;
using VolumetricFogAndMist;

/// <summary>
/// YORU: Volumetric Fog Integration (Built-in Pipeline)
/// Controls Volumetric Fog & Mist based on WorldStateManager balance.
/// 
/// SETUP:
/// 1. Add this script to your Main Camera (same object as VolumetricFog component)
/// 2. It will auto-find the VolumetricFog component
/// 3. Tweak presets in Inspector for each state
/// </summary>
public class VolumetricFogIntegration : MonoBehaviour
{
    #region Preset Class
    
    [System.Serializable]
    public class FogPreset
    {
        [Header("=== FOG DENSITY & SHAPE ===")]
        [Range(0f, 1f)]
        [Tooltip("Fog density (0 = none, 1 = thick)")]
        public float density = 0.3f;
        
        [Tooltip("Fog height")]
        public float height = 6f;
        
        [Tooltip("Base height for fog (negative = ground fog)")]
        public float baselineHeight = 0f;
        
        [Range(0f, 1f)]
        [Tooltip("Fog transparency")]
        public float alpha = 1f;
        
        [Header("=== FOG COLOR ===")]
        [Tooltip("Main fog color")]
        public Color color = new Color(0.89f, 0.89f, 0.89f, 1f);
        
        [Tooltip("Specular highlight color")]
        public Color specularColor = new Color(1f, 1f, 0.8f, 1f);
        
        [Header("=== LIGHT SCATTERING (God Rays) ===")]
        [Tooltip("Enable god rays for this state")]
        public bool lightScatteringEnabled = true;
        
        [Range(0f, 1f)]
        [Tooltip("God ray exposure/intensity")]
        public float lightScatteringExposure = 0.03f;
        
        [Range(0f, 1f)]
        [Tooltip("Light diffusion amount")]
        public float lightScatteringDiffusion = 0.5f;
        
        [Range(0f, 1f)]
        [Tooltip("Light spread")]
        public float lightScatteringSpread = 0.686f;
        
        [Tooltip("Tint color for god rays")]
        public Color lightScatteringTint = Color.white;
        
        [Header("=== SKY HAZE ===")]
        [Tooltip("Sky haze distance")]
        public float skyHaze = 15f;
        
        [Tooltip("Sky haze color")]
        public Color skyColor = new Color(0.81f, 0.81f, 0.81f, 0.8f);
        
        [Range(0f, 1f)]
        [Tooltip("Sky haze transparency")]
        public float skyAlpha = 0.8f;
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== REFERENCES ===")]
    [Tooltip("Auto-found if on same GameObject")]
    [SerializeField] private VolumetricFog volumetricFog;
    
    [Header("=== TRANSITION ===")]
    [Range(0.5f, 5f)]
    [Tooltip("How long fog transitions take")]
    [SerializeField] private float transitionDuration = 2f;
    
    [Header("=== ECLIPSE PRESET ===")]
    [SerializeField] private FogPreset eclipsePreset = new FogPreset
    {
        density = 0.35f,
        height = 80f,
        baselineHeight = -5f,
        alpha = 1f,
        color = new Color(0.5f, 0.4f, 0.6f, 1f),
        specularColor = new Color(0.6f, 0.4f, 0.8f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.04f,
        lightScatteringDiffusion = 0.6f,
        lightScatteringSpread = 0.7f,
        lightScatteringTint = new Color(0.6f, 0.4f, 0.8f, 1f),
        skyHaze = 20f,
        skyColor = new Color(0.4f, 0.3f, 0.5f, 0.9f),
        skyAlpha = 0.9f
    };
    
    [Header("=== NEUTRAL PRESET ===")]
    [SerializeField] private FogPreset neutralPreset = new FogPreset
    {
        density = 0.3f,
        height = 6f,
        baselineHeight = 0f,
        alpha = 1f,
        color = new Color(0.89f, 0.89f, 0.89f, 1f),
        specularColor = new Color(1f, 1f, 0.8f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.03f,
        lightScatteringDiffusion = 0.5f,
        lightScatteringSpread = 0.686f,
        lightScatteringTint = Color.white,
        skyHaze = 15f,
        skyColor = new Color(0.81f, 0.81f, 0.81f, 0.8f),
        skyAlpha = 0.8f
    };
    
    [Header("=== LIGHT PATH PRESETS ===")]
    [Tooltip("Light +1 to +2")]
    [SerializeField] private FogPreset light1Preset = new FogPreset
    {
        density = 0.25f,
        height = 8f,
        baselineHeight = 2f,
        alpha = 0.95f,
        color = new Color(0.95f, 0.92f, 0.85f, 1f),
        specularColor = new Color(1f, 0.98f, 0.85f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.035f,
        lightScatteringDiffusion = 0.55f,
        lightScatteringSpread = 0.7f,
        lightScatteringTint = new Color(1f, 0.95f, 0.85f, 1f),
        skyHaze = 12f,
        skyColor = new Color(0.9f, 0.88f, 0.82f, 0.75f),
        skyAlpha = 0.75f
    };
    
    [Tooltip("Light +3 to +4")]
    [SerializeField] private FogPreset light3Preset = new FogPreset
    {
        density = 0.2f,
        height = 12f,
        baselineHeight = 5f,
        alpha = 0.9f,
        color = new Color(1f, 0.95f, 0.8f, 1f),
        specularColor = new Color(1f, 0.95f, 0.7f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.045f,
        lightScatteringDiffusion = 0.6f,
        lightScatteringSpread = 0.75f,
        lightScatteringTint = new Color(1f, 0.9f, 0.7f, 1f),
        skyHaze = 10f,
        skyColor = new Color(0.95f, 0.9f, 0.75f, 0.7f),
        skyAlpha = 0.7f
    };
    
    [Tooltip("Light +5")]
    [SerializeField] private FogPreset light5Preset = new FogPreset
    {
        density = 0.15f,
        height = 20f,
        baselineHeight = 10f,
        alpha = 0.85f,
        color = new Color(1f, 0.97f, 0.85f, 1f),
        specularColor = new Color(1f, 0.95f, 0.65f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.055f,
        lightScatteringDiffusion = 0.65f,
        lightScatteringSpread = 0.8f,
        lightScatteringTint = new Color(1f, 0.92f, 0.75f, 1f),
        skyHaze = 8f,
        skyColor = new Color(1f, 0.95f, 0.8f, 0.65f),
        skyAlpha = 0.65f
    };
    
    [Tooltip("Light +6 to +7 - Divine Escalation")]
    [SerializeField] private FogPreset light6Preset = new FogPreset
    {
        density = 0.2f,
        height = 30f,
        baselineHeight = 15f,
        alpha = 0.9f,
        color = new Color(1f, 0.95f, 0.75f, 1f),
        specularColor = new Color(1f, 0.9f, 0.6f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.07f,
        lightScatteringDiffusion = 0.7f,
        lightScatteringSpread = 0.85f,
        lightScatteringTint = new Color(1f, 0.88f, 0.65f, 1f),
        skyHaze = 12f,
        skyColor = new Color(1f, 0.93f, 0.7f, 0.75f),
        skyAlpha = 0.75f
    };
    
    [Tooltip("Light +8 to +10 - Maximum Divine")]
    [SerializeField] private FogPreset light8Preset = new FogPreset
    {
        density = 0.25f,
        height = 50f,
        baselineHeight = 20f,
        alpha = 0.95f,
        color = new Color(1f, 0.93f, 0.7f, 1f),
        specularColor = new Color(1f, 0.85f, 0.55f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.09f,
        lightScatteringDiffusion = 0.75f,
        lightScatteringSpread = 0.9f,
        lightScatteringTint = new Color(1f, 0.85f, 0.55f, 1f),
        skyHaze = 15f,
        skyColor = new Color(1f, 0.9f, 0.6f, 0.85f),
        skyAlpha = 0.85f
    };
    
    [Header("=== DARK PATH PRESETS ===")]
    [Tooltip("Dark -1 to -2")]
    [SerializeField] private FogPreset dark1Preset = new FogPreset
    {
        density = 0.35f,
        height = 5f,
        baselineHeight = -2f,
        alpha = 1f,
        color = new Color(0.75f, 0.78f, 0.85f, 1f),
        specularColor = new Color(0.8f, 0.85f, 0.95f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.02f,
        lightScatteringDiffusion = 0.45f,
        lightScatteringSpread = 0.6f,
        lightScatteringTint = new Color(0.85f, 0.85f, 0.9f, 1f),
        skyHaze = 18f,
        skyColor = new Color(0.7f, 0.72f, 0.8f, 0.85f),
        skyAlpha = 0.85f
    };
    
    [Tooltip("Dark -3 to -4")]
    [SerializeField] private FogPreset dark3Preset = new FogPreset
    {
        density = 0.45f,
        height = 4f,
        baselineHeight = -5f,
        alpha = 1f,
        color = new Color(0.6f, 0.55f, 0.7f, 1f),
        specularColor = new Color(0.7f, 0.65f, 0.8f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.015f,
        lightScatteringDiffusion = 0.4f,
        lightScatteringSpread = 0.5f,
        lightScatteringTint = new Color(0.7f, 0.65f, 0.8f, 1f),
        skyHaze = 22f,
        skyColor = new Color(0.5f, 0.5f, 0.6f, 0.9f),
        skyAlpha = 0.9f
    };
    
    [Tooltip("Dark -5")]
    [SerializeField] private FogPreset dark5Preset = new FogPreset
    {
        density = 0.55f,
        height = 3f,
        baselineHeight = -10f,
        alpha = 1f,
        color = new Color(0.5f, 0.45f, 0.6f, 1f),
        specularColor = new Color(0.6f, 0.5f, 0.7f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.01f,
        lightScatteringDiffusion = 0.35f,
        lightScatteringSpread = 0.4f,
        lightScatteringTint = new Color(0.6f, 0.5f, 0.7f, 1f),
        skyHaze = 25f,
        skyColor = new Color(0.4f, 0.4f, 0.5f, 0.95f),
        skyAlpha = 0.95f
    };
    
    [Tooltip("Dark -6 to -7 - Creeping Dread Escalation")]
    [SerializeField] private FogPreset dark6Preset = new FogPreset
    {
        density = 0.65f,
        height = 2.5f,
        baselineHeight = -15f,
        alpha = 1f,
        color = new Color(0.4f, 0.35f, 0.5f, 1f),
        specularColor = new Color(0.5f, 0.4f, 0.6f, 1f),
        lightScatteringEnabled = true,
        lightScatteringExposure = 0.005f,
        lightScatteringDiffusion = 0.3f,
        lightScatteringSpread = 0.3f,
        lightScatteringTint = new Color(0.5f, 0.4f, 0.6f, 1f),
        skyHaze = 30f,
        skyColor = new Color(0.3f, 0.3f, 0.4f, 1f),
        skyAlpha = 1f
    };
    
    [Tooltip("Dark -8 to -10 - Nightmare (NO god rays)")]
    [SerializeField] private FogPreset dark8Preset = new FogPreset
    {
        density = 0.75f,
        height = 2f,
        baselineHeight = -20f,
        alpha = 1f,
        color = new Color(0.3f, 0.25f, 0.4f, 1f),
        specularColor = new Color(0.4f, 0.3f, 0.5f, 1f),
        lightScatteringEnabled = false, // NO god rays in nightmare!
        lightScatteringExposure = 0f,
        lightScatteringDiffusion = 0.2f,
        lightScatteringSpread = 0.2f,
        lightScatteringTint = new Color(0.4f, 0.3f, 0.5f, 1f),
        skyHaze = 40f,
        skyColor = new Color(0.2f, 0.2f, 0.25f, 1f),
        skyAlpha = 1f
    };
    
    #endregion
    
    #region Private Fields
    
    private FogPreset currentPreset;
    private FogPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning = false;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (volumetricFog == null)
        {
            volumetricFog = GetComponent<VolumetricFog>();
        }
        
        if (volumetricFog == null)
        {
            Debug.LogError("[VolumetricFogIntegration] No VolumetricFog component found!");
            enabled = false;
            return;
        }
        
        currentPreset = ClonePreset(neutralPreset);
        targetPreset = ClonePreset(neutralPreset);
    }
    
    private void Start()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.AddListener(OnStateChanged);
            WorldStateManager.Instance.OnEclipseTriggered.AddListener(OnEclipseTriggered);
            
            OnStateChanged(WorldStateManager.Instance.CurrentState);
            Debug.Log("[VolumetricFogIntegration] Subscribed to WorldStateManager");
        }
        else
        {
            Debug.LogWarning("[VolumetricFogIntegration] WorldStateManager not found.");
        }
        
        ApplyPreset(currentPreset);
    }
    
    private void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnStateChanged.RemoveListener(OnStateChanged);
            WorldStateManager.Instance.OnEclipseTriggered.RemoveListener(OnEclipseTriggered);
        }
    }
    
    private void Update()
    {
        if (isTransitioning)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            
            if (transitionProgress >= 1f)
            {
                transitionProgress = 1f;
                isTransitioning = false;
                currentPreset = ClonePreset(targetPreset);
            }
            
            ApplyBlendedPreset(currentPreset, targetPreset, transitionProgress);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnStateChanged(WorldStateManager.AtmosphereState newState)
    {
        FogPreset newPreset = GetPresetForState(newState);
        StartTransition(newPreset);
        Debug.Log($"[VolumetricFogIntegration] State changed to {newState}");
    }
    
    private void OnEclipseTriggered()
    {
        StartTransition(eclipsePreset);
        Debug.Log("[VolumetricFogIntegration] Eclipse triggered!");
    }
    
    #endregion
    
    #region Preset Selection
    
    private FogPreset GetPresetForState(WorldStateManager.AtmosphereState state)
    {
        switch (state)
        {
            case WorldStateManager.AtmosphereState.Eclipse:
                return eclipsePreset;
                
            case WorldStateManager.AtmosphereState.Light1:
            case WorldStateManager.AtmosphereState.Light2:
                return light1Preset;
                
            case WorldStateManager.AtmosphereState.Light3:
            case WorldStateManager.AtmosphereState.Light4:
                return light3Preset;
                
            case WorldStateManager.AtmosphereState.Light5:
                return light5Preset;
                
            case WorldStateManager.AtmosphereState.Dark1:
            case WorldStateManager.AtmosphereState.Dark2:
                return dark1Preset;
                
            case WorldStateManager.AtmosphereState.Dark3:
            case WorldStateManager.AtmosphereState.Dark4:
                return dark3Preset;
                
            case WorldStateManager.AtmosphereState.Dark5:
                return dark5Preset;
                
            case WorldStateManager.AtmosphereState.Neutral:
            default:
                return neutralPreset;
        }
    }
    
    /// <summary>
    /// Get preset for escalation states (|balance| >= 6)
    /// Call this from WorldStateManager when balance exceeds ±5
    /// </summary>
    public FogPreset GetPresetForEscalation(int balance)
    {
        if (balance >= 8) return light8Preset;
        if (balance >= 6) return light6Preset;
        if (balance <= -8) return dark8Preset;
        if (balance <= -6) return dark6Preset;
        
        return GetPresetForState(WorldStateManager.Instance.CurrentState);
    }
    
    #endregion
    
    #region Transition System
    
    private void StartTransition(FogPreset newPreset)
    {
        if (!isTransitioning)
        {
            currentPreset = ClonePreset(currentPreset);
        }
        
        targetPreset = newPreset;
        transitionProgress = 0f;
        isTransitioning = true;
    }
    
    private void ApplyPreset(FogPreset preset)
    {
        if (volumetricFog == null) return;
        
        // Fog density & shape
        volumetricFog.density = preset.density;
        volumetricFog.height = preset.height;
        volumetricFog.baselineHeight = preset.baselineHeight;
        volumetricFog.alpha = preset.alpha;
        
        // Fog colors
        volumetricFog.color = preset.color;
        volumetricFog.specularColor = preset.specularColor;
        
        // Light Scattering (God Rays)
        volumetricFog.lightScatteringEnabled = preset.lightScatteringEnabled;
        volumetricFog.lightScatteringExposure = preset.lightScatteringExposure;
        volumetricFog.lightScatteringDiffusion = preset.lightScatteringDiffusion;
        volumetricFog.lightScatteringSpread = preset.lightScatteringSpread;
        volumetricFog.lightScatteringTint = preset.lightScatteringTint;
        
        // Sky Haze
        volumetricFog.skyHaze = preset.skyHaze;
        volumetricFog.skyColor = preset.skyColor;
        volumetricFog.skyAlpha = preset.skyAlpha;
    }
    
    private void ApplyBlendedPreset(FogPreset from, FogPreset to, float t)
    {
        if (volumetricFog == null) return;
        
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        
        // Fog density & shape
        volumetricFog.density = Mathf.Lerp(from.density, to.density, smoothT);
        volumetricFog.height = Mathf.Lerp(from.height, to.height, smoothT);
        volumetricFog.baselineHeight = Mathf.Lerp(from.baselineHeight, to.baselineHeight, smoothT);
        volumetricFog.alpha = Mathf.Lerp(from.alpha, to.alpha, smoothT);
        
        // Fog colors
        volumetricFog.color = Color.Lerp(from.color, to.color, smoothT);
        volumetricFog.specularColor = Color.Lerp(from.specularColor, to.specularColor, smoothT);
        
        // Light Scattering - handle enable/disable at midpoint
        bool shouldEnable = smoothT < 0.5f ? from.lightScatteringEnabled : to.lightScatteringEnabled;
        volumetricFog.lightScatteringEnabled = shouldEnable || from.lightScatteringEnabled || to.lightScatteringEnabled;
        
        float fromExposure = from.lightScatteringEnabled ? from.lightScatteringExposure : 0f;
        float toExposure = to.lightScatteringEnabled ? to.lightScatteringExposure : 0f;
        volumetricFog.lightScatteringExposure = Mathf.Lerp(fromExposure, toExposure, smoothT);
        
        volumetricFog.lightScatteringDiffusion = Mathf.Lerp(from.lightScatteringDiffusion, to.lightScatteringDiffusion, smoothT);
        volumetricFog.lightScatteringSpread = Mathf.Lerp(from.lightScatteringSpread, to.lightScatteringSpread, smoothT);
        volumetricFog.lightScatteringTint = Color.Lerp(from.lightScatteringTint, to.lightScatteringTint, smoothT);
        
        // Sky Haze
        volumetricFog.skyHaze = Mathf.Lerp(from.skyHaze, to.skyHaze, smoothT);
        volumetricFog.skyColor = Color.Lerp(from.skyColor, to.skyColor, smoothT);
        volumetricFog.skyAlpha = Mathf.Lerp(from.skyAlpha, to.skyAlpha, smoothT);
    }
    
    #endregion
    
    #region Utility
    
    private FogPreset ClonePreset(FogPreset original)
    {
        return new FogPreset
        {
            density = original.density,
            height = original.height,
            baselineHeight = original.baselineHeight,
            alpha = original.alpha,
            color = original.color,
            specularColor = original.specularColor,
            lightScatteringEnabled = original.lightScatteringEnabled,
            lightScatteringExposure = original.lightScatteringExposure,
            lightScatteringDiffusion = original.lightScatteringDiffusion,
            lightScatteringSpread = original.lightScatteringSpread,
            lightScatteringTint = original.lightScatteringTint,
            skyHaze = original.skyHaze,
            skyColor = original.skyColor,
            skyAlpha = original.skyAlpha
        };
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Apply escalation preset based on balance value
    /// Call this when balance exceeds ±5
    /// </summary>
    public void ApplyEscalation(int balance)
    {
        FogPreset escalationPreset = GetPresetForEscalation(balance);
        StartTransition(escalationPreset);
        Debug.Log($"[VolumetricFogIntegration] Escalation applied for balance {balance}");
    }
    
    /// <summary>
    /// Apply a state immediately without transition
    /// </summary>
    public void ApplyImmediate(WorldStateManager.AtmosphereState state)
    {
        FogPreset preset = GetPresetForState(state);
        currentPreset = ClonePreset(preset);
        targetPreset = ClonePreset(preset);
        transitionProgress = 1f;
        isTransitioning = false;
        ApplyPreset(preset);
    }
    
    /// <summary>
    /// Force a specific preset (for testing)
    /// </summary>
    public void ForcePreset(string presetName)
    {
        FogPreset preset = presetName.ToLower() switch
        {
            "eclipse" => eclipsePreset,
            "neutral" => neutralPreset,
            "light1" => light1Preset,
            "light3" => light3Preset,
            "light5" => light5Preset,
            "light6" => light6Preset,
            "light8" => light8Preset,
            "dark1" => dark1Preset,
            "dark3" => dark3Preset,
            "dark5" => dark5Preset,
            "dark6" => dark6Preset,
            "dark8" => dark8Preset,
            _ => neutralPreset
        };
        
        StartTransition(preset);
        Debug.Log($"[VolumetricFogIntegration] Forced preset: {presetName}");
    }
    
    #endregion
}