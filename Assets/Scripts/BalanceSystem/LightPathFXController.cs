using UnityEngine;
using System;

/// <summary>
/// YORU: Light Path FX Controller V3
/// 
/// Controls particle effects for LIGHT PATH ESCALATION (Stage 1-5 only).
/// This controller ONLY activates when atmosphere is Light5 AND weatherStage > 0.
/// 
/// Effects include:
/// - Sparkles (floating light particles)
/// - Butterflies (ambient creatures)
/// - Flower petals (falling gently)
/// - Divine glow particles
/// 
/// Note: This controller is supplementary - the main divine glow light 
/// is handled by LightingController.
/// </summary>
public class LightPathFXController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class FXPreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Sparkles")]
        [Range(0, 100)] public int sparkleCount = 0;
        [Range(0, 2)] public float sparkleIntensity = 0.5f;
        
        [Header("Butterflies")]
        [Range(0, 50)] public int butterflyCount = 0;
        [Range(0, 2)] public float butterflySpeed = 1f;
        
        [Header("Petals")]
        [Range(0, 100)] public int petalCount = 0;
        [Range(0, 2)] public float petalFallSpeed = 1f;
        
        [Header("Divine Particles")]
        [Range(0, 100)] public int divineParticleCount = 0;
        [Range(0, 2)] public float divineIntensity = 0f;
        
        [Header("Overall")]
        [Range(0, 2)] public float masterIntensity = 1f;
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== PARTICLE SYSTEMS ===")]
    [SerializeField] private ParticleSystem sparkleParticles;
    [SerializeField] private ParticleSystem butterflyParticles;
    [SerializeField] private ParticleSystem petalParticles;
    [SerializeField] private ParticleSystem divineParticles;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 3f)] private float transitionDuration = 1.5f;
    
    [Header("=== BASE STATE (Light5, no escalation) ===")]
    [SerializeField] private FXPreset light5BasePreset = new FXPreset
    {
        stateName = "Light5 (Base - minimal FX)",
        sparkleCount = 0,
        butterflyCount = 5,
        petalCount = 0,
        divineParticleCount = 0,
        masterIntensity = 0.5f
    };
    
    [Header("=== ESCALATION STAGES ===")]
    [SerializeField] private FXPreset stage1Preset = new FXPreset
    {
        stateName = "Light5+Stage1 (Divine Beginning)",
        sparkleCount = 10,
        sparkleIntensity = 0.6f,
        butterflyCount = 10,
        petalCount = 0,
        divineParticleCount = 5,
        divineIntensity = 0.3f,
        masterIntensity = 0.7f
    };
    
    [SerializeField] private FXPreset stage2Preset = new FXPreset
    {
        stateName = "Light5+Stage2 (Radiant)",
        sparkleCount = 25,
        sparkleIntensity = 0.75f,
        butterflyCount = 15,
        petalCount = 5,
        divineParticleCount = 15,
        divineIntensity = 0.5f,
        masterIntensity = 0.8f
    };
    
    [SerializeField] private FXPreset stage3Preset = new FXPreset
    {
        stateName = "Light5+Stage3 (Glorious)",
        sparkleCount = 40,
        sparkleIntensity = 0.9f,
        butterflyCount = 20,
        butterflySpeed = 1.2f,
        petalCount = 15,
        divineParticleCount = 30,
        divineIntensity = 0.7f,
        masterIntensity = 0.9f
    };
    
    [SerializeField] private FXPreset stage4Preset = new FXPreset
    {
        stateName = "Light5+Stage4 (Transcendent)",
        sparkleCount = 55,
        sparkleIntensity = 1.1f,
        butterflyCount = 25,
        butterflySpeed = 1.3f,
        petalCount = 30,
        petalFallSpeed = 0.8f,
        divineParticleCount = 50,
        divineIntensity = 0.85f,
        masterIntensity = 0.95f
    };
    
    [SerializeField] private FXPreset stage5Preset = new FXPreset
    {
        stateName = "Light5+Stage5 (MAXIMUM DIVINE)",
        sparkleCount = 75,
        sparkleIntensity = 1.3f,
        butterflyCount = 35,
        butterflySpeed = 1.5f,
        petalCount = 50,
        petalFallSpeed = 0.6f,
        divineParticleCount = 75,
        divineIntensity = 1f,
        masterIntensity = 1f
    };
    
    [Header("=== OFF STATE ===")]
    [SerializeField] private FXPreset offPreset = new FXPreset
    {
        stateName = "OFF (not Light5)",
        sparkleCount = 0,
        butterflyCount = 0,
        petalCount = 0,
        divineParticleCount = 0,
        masterIntensity = 0f
    };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private bool isActive;
    [SerializeField] private int currentStage;
    
    #endregion
    
    #region Private Fields
    
    private FXPreset currentPreset;
    private FXPreset targetPreset;
    private float transitionProgress = 1f;
    private bool isTransitioning;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
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
            
            ApplyPreset(Lerp(currentPreset, targetPreset, transitionProgress));
        }
    }
    
    #endregion
    
    #region Setup
    
    void InitializeState()
    {
        currentPreset = offPreset;
        targetPreset = offPreset;
        ApplyPreset(offPreset);
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
        isActive = (state == WorldStateManager.AtmosphereState.Light5);
        UpdateTargetPreset();
    }
    
    void OnWeatherStageChanged(int stage)
    {
        currentStage = stage;
        UpdateTargetPreset();
    }
    
    void UpdateTargetPreset()
    {
        targetPreset = GetPresetForStage();
        transitionProgress = 0f;
        isTransitioning = true;
        
        if (logChanges)
            Debug.Log($"[LightPathFXController] Transitioning to: {targetPreset.stateName}");
    }
    
    FXPreset GetPresetForStage()
    {
        // Only active when Light5
        if (!isActive)
            return offPreset;
        
        // No escalation = minimal butterflies only
        if (currentStage <= 0)
            return light5BasePreset;
        
        // Escalation stages
        switch (currentStage)
        {
            case 1: return stage1Preset;
            case 2: return stage2Preset;
            case 3: return stage3Preset;
            case 4: return stage4Preset;
            default: return stage5Preset;
        }
    }
    
    #endregion
    
    #region Apply Preset
    
    FXPreset Lerp(FXPreset a, FXPreset b, float t)
    {
        return new FXPreset
        {
            stateName = b.stateName,
            sparkleCount = Mathf.RoundToInt(Mathf.Lerp(a.sparkleCount, b.sparkleCount, t)),
            sparkleIntensity = Mathf.Lerp(a.sparkleIntensity, b.sparkleIntensity, t),
            butterflyCount = Mathf.RoundToInt(Mathf.Lerp(a.butterflyCount, b.butterflyCount, t)),
            butterflySpeed = Mathf.Lerp(a.butterflySpeed, b.butterflySpeed, t),
            petalCount = Mathf.RoundToInt(Mathf.Lerp(a.petalCount, b.petalCount, t)),
            petalFallSpeed = Mathf.Lerp(a.petalFallSpeed, b.petalFallSpeed, t),
            divineParticleCount = Mathf.RoundToInt(Mathf.Lerp(a.divineParticleCount, b.divineParticleCount, t)),
            divineIntensity = Mathf.Lerp(a.divineIntensity, b.divineIntensity, t),
            masterIntensity = Mathf.Lerp(a.masterIntensity, b.masterIntensity, t)
        };
    }
    
    void ApplyPreset(FXPreset preset)
    {
        // Sparkles
        if (sparkleParticles != null)
        {
            var emission = sparkleParticles.emission;
            emission.rateOverTime = preset.sparkleCount * preset.masterIntensity;
            
            var main = sparkleParticles.main;
            main.startLifetimeMultiplier = preset.sparkleIntensity;
        }
        
        // Butterflies
        if (butterflyParticles != null)
        {
            var emission = butterflyParticles.emission;
            emission.rateOverTime = preset.butterflyCount * preset.masterIntensity;
            
            var main = butterflyParticles.main;
            main.simulationSpeed = preset.butterflySpeed;
        }
        
        // Petals
        if (petalParticles != null)
        {
            var emission = petalParticles.emission;
            emission.rateOverTime = preset.petalCount * preset.masterIntensity;
            
            var main = petalParticles.main;
            main.gravityModifier = preset.petalFallSpeed * 0.1f;
        }
        
        // Divine particles
        if (divineParticles != null)
        {
            var emission = divineParticles.emission;
            emission.rateOverTime = preset.divineParticleCount * preset.masterIntensity;
            
            var main = divineParticles.main;
            main.startLifetimeMultiplier = preset.divineIntensity;
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Preview: OFF")]
    void PreviewOff() { currentPreset = targetPreset = offPreset; ApplyPreset(offPreset); }
    
    [ContextMenu("Preview: Light5 Base")]
    void PreviewBase() { currentPreset = targetPreset = light5BasePreset; ApplyPreset(light5BasePreset); }
    
    [ContextMenu("Preview: Stage 3")]
    void PreviewStage3() { currentPreset = targetPreset = stage3Preset; ApplyPreset(stage3Preset); }
    
    [ContextMenu("Preview: Stage 5 (MAX)")]
    void PreviewStage5() { currentPreset = targetPreset = stage5Preset; ApplyPreset(stage5Preset); }
    
    #endregion
}