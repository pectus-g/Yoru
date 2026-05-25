using UnityEngine;
using UnityEngine.Serialization;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using System;
using System.Collections.Generic;

/// <summary>
/// YORU: Ambience Controller - PARTICLE FX + AUDIO
/// 
/// Controls COZY Particle FX prefabs and Ambience audio profiles.
/// Listens to OnRingsChanged for 29-state system.
/// 
/// PARTICLE FX (from COZY):
/// - Fireflies, Wisps (dark nights)
/// - Butterflies, Day Bugs, Birds (light path)
/// - Autumn Leaves (sunset)
/// - Aurora, Meteor (eclipse)
/// - Dust Storm, Thunder, Rain (dark storms)
/// 
/// Multiple particles can be active simultaneously.
/// </summary>
public class AmbienceController : MonoBehaviour
{
    #region Preset Class
    
    [Serializable]
    public class AmbiencePreset
    {
        [Header("State Info")]
        public string stateName = "Unnamed";
        
        [Header("Particle FX (assign COZY prefabs)")]
        public List<GameObject> particlePrefabs = new List<GameObject>();
        
        [Header("Audio Profile")]
        public AmbienceProfile audioProfile;
        [Range(0, 1)] public float audioWeight = 1f;
        [Range(0, 2)] public float masterVolume = 1f;
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("=== COZY REFERENCE ===")]
    [SerializeField] private CozyWeather cozyWeather;
    [SerializeField] private bool autoFindCozy = true;
    
    [Header("=== PARTICLE SPAWN SETTINGS ===")]
    [Tooltip("Parent for spawned particles (leave empty to use this transform)")]
    [SerializeField] private Transform particleParent;
    [Tooltip("Offset from player for particle spawn")]
    [SerializeField] private Vector3 particleOffset = new Vector3(0, 5, 0);
    [Tooltip("Follow player?")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private Transform playerTransform;
    
    [Header("=== TRANSITION ===")]
    [SerializeField, Range(0.5f, 5f)] private float transitionDuration = 2f;
    
    [Header("=== AVAILABLE PARTICLE PREFABS ===")]
    [Tooltip("Drag COZY particle prefabs here from Packages/COZY 3/Content/Prefabs/Particle FX")]
    public GameObject firefliesPrefab;
    public GameObject wispsPrefab;
    public GameObject butterfliesPrefab;
    public GameObject dayBugsPrefab;
    public GameObject birdsPrefab;
    public GameObject autumnLeavesPrefab;
    public GameObject auroraPrefab;
    public GameObject auroraAltPrefab;
    public GameObject meteorPrefab;
    public GameObject dustStormPrefab;
    public GameObject swirlingPrefab;
    public GameObject blusteryPrefab;
    public GameObject lightRainPrefab;
    public GameObject heavyRainPrefab;
    public GameObject thunderPrefab;
    public GameObject thunderSnowPrefab;
    
    [Header("=== AVAILABLE AUDIO PROFILES ===")]
    [Tooltip("Drag COZY ambience profiles here")]
    public AmbienceProfile quietProfile;
    public AmbienceProfile lightWindProfile;
    public AmbienceProfile birdsongProfile;
    public AmbienceProfile dayBugsAudioProfile;
    public AmbienceProfile owlSoundsProfile;
    public AmbienceProfile blusteryAudioProfile;
    public AmbienceProfile swirlingAudioProfile;
    
    [Header("=== NEUTRAL ===")]
    [SerializeField] private AmbiencePreset neutralPreset = new AmbiencePreset
    {
        stateName = "Neutral",
        audioWeight = 0.7f,
        masterVolume = 0.5f
    };
    
    [Header("=== SUNSET ===")]
    [SerializeField] private AmbiencePreset sunsetPreset = new AmbiencePreset
    {
        stateName = "Sunset",
        audioWeight = 0.6f,
        masterVolume = 0.6f
    };
    
    [Header("=== SUNRISE ===")]
    [SerializeField] private AmbiencePreset sunrisePreset = new AmbiencePreset
    {
        stateName = "Sunrise",
        audioWeight = 0.7f,
        masterVolume = 0.55f
    };
    
    [Header("=== DARK PATH ===")]
    [SerializeField] private AmbiencePreset dark1Preset = new AmbiencePreset { stateName = "Dark1" };
    [SerializeField] private AmbiencePreset dark2Preset = new AmbiencePreset { stateName = "Dark2" };
    [SerializeField] private AmbiencePreset dark3Preset = new AmbiencePreset { stateName = "Dark3" };
    [SerializeField] private AmbiencePreset dark4Preset = new AmbiencePreset { stateName = "Dark4" };
    [SerializeField] private AmbiencePreset dark5Preset = new AmbiencePreset { stateName = "Dark5 (Midnight)" };
    
    [Header("=== DARK ESCALATION (Storms) ===")]
    [SerializeField] private AmbiencePreset darkStage1Preset = new AmbiencePreset { stateName = "Dark+Stage1" };
    [SerializeField] private AmbiencePreset darkStage2Preset = new AmbiencePreset { stateName = "Dark+Stage2" };
    [SerializeField] private AmbiencePreset darkStage3Preset = new AmbiencePreset { stateName = "Dark+Stage3" };
    [SerializeField] private AmbiencePreset darkStage4Preset = new AmbiencePreset { stateName = "Dark+Stage4" };
    [SerializeField] private AmbiencePreset darkStage5Preset = new AmbiencePreset { stateName = "Dark+Stage5 (THUNDERSTORM)" };
    
    [Header("=== LIGHT PATH ===")]
    [SerializeField] private AmbiencePreset light1Preset = new AmbiencePreset { stateName = "Light1" };
    [SerializeField] private AmbiencePreset light2Preset = new AmbiencePreset { stateName = "Light2" };
    [SerializeField] private AmbiencePreset light3Preset = new AmbiencePreset { stateName = "Light3" };
    [SerializeField] private AmbiencePreset light4Preset = new AmbiencePreset { stateName = "Light4" };
    [SerializeField] private AmbiencePreset light5Preset = new AmbiencePreset { stateName = "Light5 (Heavenly)" };
    
    [Header("=== LIGHT ESCALATION (Divine) ===")]
    [SerializeField] private AmbiencePreset lightStage1Preset = new AmbiencePreset { stateName = "Light+Stage1" };
    [SerializeField] private AmbiencePreset lightStage2Preset = new AmbiencePreset { stateName = "Light+Stage2" };
    [SerializeField] private AmbiencePreset lightStage3Preset = new AmbiencePreset { stateName = "Light+Stage3" };
    [SerializeField] private AmbiencePreset lightStage4Preset = new AmbiencePreset { stateName = "Light+Stage4" };
    [SerializeField] private AmbiencePreset lightStage5Preset = new AmbiencePreset { stateName = "Light+Stage5 (DIVINE)" };
    
    [Header("=== ECLIPSE STATES (7-stage gradient, see GDD §5) ===")]
    [Tooltip("Stage 1 (15%) - 2L/2R - subtle eclipse hint")]
    [SerializeField] private AmbiencePreset eclipse15Preset = new AmbiencePreset { stateName = "Eclipse 15%" };
    [FormerlySerializedAs("eclipse20Preset")]
    [Tooltip("Stage 2 (25%) - 2L/3R or 3L/2R")]
    [SerializeField] private AmbiencePreset eclipse25Preset = new AmbiencePreset { stateName = "Eclipse 25%" };
    [Tooltip("Stage 3 (40%) - 3L/3R")]
    [SerializeField] private AmbiencePreset eclipse40Preset = new AmbiencePreset { stateName = "Eclipse 40%" };
    [FormerlySerializedAs("eclipse50Preset")]
    [Tooltip("Stage 4 (55%) - 3L/4R or 4L/3R")]
    [SerializeField] private AmbiencePreset eclipse55Preset = new AmbiencePreset { stateName = "Eclipse 55%" };
    [FormerlySerializedAs("eclipse60Preset")]
    [Tooltip("Stage 5 (70%) - 4L/4R")]
    [SerializeField] private AmbiencePreset eclipse70Preset = new AmbiencePreset { stateName = "Eclipse 70%" };
    [FormerlySerializedAs("eclipse75Preset")]
    [Tooltip("Stage 6 (85%) - 4L/5R or 5L/4R")]
    [SerializeField] private AmbiencePreset eclipse85Preset = new AmbiencePreset { stateName = "Eclipse 85%" };
    [Tooltip("Stage 7 (100%) - 5L/5R - full eclipse")]
    [SerializeField] private AmbiencePreset eclipseFullPreset = new AmbiencePreset { stateName = "Eclipse FULL 100%" };
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool logChanges = true;
    [SerializeField] private string currentPresetName = "None";
    [SerializeField] private int debugLeftRings;
    [SerializeField] private int debugRightRings;
    [SerializeField] private int activeParticleCount;
    
    #endregion
    
    #region Private Fields
    
    private AmbiencePreset currentPreset;
    private List<GameObject> activeParticles = new List<GameObject>();
    private int currentLeftRings;
    private int currentRightRings;
    
    // COZY Ambience Module (via reflection)
    private object ambienceModule;
    private bool hasAmbienceModule;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Start()
    {
        FindCozy();
        FindPlayer();
        SetupDefaultPresets();
        
        if (particleParent == null)
            particleParent = transform;
        
        SubscribeToEvents();
        
        if (logChanges)
            Debug.Log($"[AmbienceController] Initialized. COZY: {cozyWeather != null}, Player: {playerTransform != null}");
    }
    
    void OnDestroy()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.RemoveListener(OnRingsChanged);
        }
        
        // Clean up particles
        ClearAllParticles();
    }
    
    void Update()
    {
        // Follow player if enabled
        if (followPlayer && playerTransform != null && activeParticles.Count > 0)
        {
            Vector3 targetPos = playerTransform.position + particleOffset;
            particleParent.position = targetPos;
        }
    }
    
    #endregion
    
    #region Setup
    
    void FindCozy()
    {
        if (cozyWeather == null && autoFindCozy)
        {
            cozyWeather = CozyWeather.instance;
        }
        
        if (cozyWeather != null)
        {
            // Try to find Ambience Module
            var moduleField = cozyWeather.GetType().GetField("ambienceModule",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (moduleField != null)
            {
                ambienceModule = moduleField.GetValue(cozyWeather);
                hasAmbienceModule = ambienceModule != null;
            }
        }
    }
    
    void FindPlayer()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }
    
    void SetupDefaultPresets()
    {
        // NEUTRAL - Day bugs, light wind
        neutralPreset.particlePrefabs = CreateList(dayBugsPrefab);
        neutralPreset.audioProfile = lightWindProfile;
        
        // SUNSET - Autumn leaves
        sunsetPreset.particlePrefabs = CreateList(autumnLeavesPrefab, birdsPrefab);
        sunsetPreset.audioProfile = quietProfile;
        
        // SUNRISE - Birds, light wind
        sunrisePreset.particlePrefabs = CreateList(birdsPrefab, dayBugsPrefab);
        sunrisePreset.audioProfile = birdsongProfile;
        
        // DARK PATH (weather starts gathering at Dark4)
        dark1Preset.particlePrefabs = CreateList(dayBugsPrefab);
        dark1Preset.audioProfile = quietProfile;
        
        dark2Preset.particlePrefabs = CreateList();
        dark2Preset.audioProfile = quietProfile;
        
        dark3Preset.particlePrefabs = CreateList(firefliesPrefab);
        dark3Preset.audioProfile = owlSoundsProfile;
        
        // Dark4 - clouds start gathering slightly. Wind particles + fireflies for visibility.
        dark4Preset.particlePrefabs = CreateList(firefliesPrefab, blusteryPrefab);
        dark4Preset.audioProfile = owlSoundsProfile;
        
        // Dark5 - more clouds. Wind + dust hint.
        dark5Preset.particlePrefabs = CreateList(firefliesPrefab, blusteryPrefab, dustStormPrefab);
        dark5Preset.audioProfile = owlSoundsProfile;
        
        // DARK ESCALATION (shifted: clouds full -> light rain -> heavy rain -> snow -> snow+storm)
        // DarkStage1 - fully covered clouds. Wind + dust, no rain yet.
        darkStage1Preset.particlePrefabs = CreateList(blusteryPrefab, dustStormPrefab);
        darkStage1Preset.audioProfile = blusteryAudioProfile;
        
        // DarkStage2 - light rain begins.
        darkStage2Preset.particlePrefabs = CreateList(lightRainPrefab, swirlingPrefab);
        darkStage2Preset.audioProfile = swirlingAudioProfile;
        
        // DarkStage3 - heavy rain.
        darkStage3Preset.particlePrefabs = CreateList(heavyRainPrefab, swirlingPrefab, dustStormPrefab);
        darkStage3Preset.audioProfile = swirlingAudioProfile;
        
        // DarkStage4 - snow.
        darkStage4Preset.particlePrefabs = CreateList(thunderSnowPrefab, swirlingPrefab);
        darkStage4Preset.audioProfile = swirlingAudioProfile;
        
        // DarkStage5 - heavy snow + storm.
        darkStage5Preset.particlePrefabs = CreateList(thunderSnowPrefab, thunderPrefab, dustStormPrefab);
        darkStage5Preset.audioProfile = swirlingAudioProfile;
        darkStage5Preset.masterVolume = 1.2f;
        
        // LIGHT PATH
        light1Preset.particlePrefabs = CreateList(dayBugsPrefab, birdsPrefab);
        light1Preset.audioProfile = birdsongProfile;
        
        light2Preset.particlePrefabs = CreateList(dayBugsPrefab, birdsPrefab, butterfliesPrefab);
        light2Preset.audioProfile = birdsongProfile;
        
        light3Preset.particlePrefabs = CreateList(butterfliesPrefab, birdsPrefab);
        light3Preset.audioProfile = birdsongProfile;
        
        light4Preset.particlePrefabs = CreateList(butterfliesPrefab, birdsPrefab);
        light4Preset.audioProfile = birdsongProfile;
        
        light5Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab); // Double butterflies!
        light5Preset.audioProfile = birdsongProfile;
        
        // LIGHT ESCALATION (Divine)
        lightStage1Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab, birdsPrefab);
        lightStage1Preset.audioProfile = birdsongProfile;
        
        lightStage2Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab);
        lightStage2Preset.audioProfile = birdsongProfile;
        
        lightStage3Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab);
        lightStage3Preset.audioProfile = birdsongProfile;
        
        lightStage4Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab);
        lightStage4Preset.audioProfile = birdsongProfile;
        
        lightStage5Preset.particlePrefabs = CreateList(butterfliesPrefab, butterfliesPrefab, butterfliesPrefab);
        lightStage5Preset.audioProfile = birdsongProfile;
        lightStage5Preset.masterVolume = 1.2f;
        
        // ECLIPSE (7-stage)
        // Stage 1 (15%) - 2L/2R - subtlest hint
        eclipse15Preset.particlePrefabs = CreateList(firefliesPrefab);
        eclipse15Preset.audioProfile = quietProfile;
        eclipse15Preset.masterVolume = 0.45f;
        
        // Stage 2 (25%) - 2L/3R or 3L/2R
        eclipse25Preset.particlePrefabs = CreateList(wispsPrefab);
        eclipse25Preset.audioProfile = quietProfile;
        eclipse25Preset.masterVolume = 0.4f;
        
        // Stage 3 (40%) - 3L/3R
        eclipse40Preset.particlePrefabs = CreateList(wispsPrefab, firefliesPrefab);
        eclipse40Preset.audioProfile = quietProfile;
        eclipse40Preset.masterVolume = 0.35f;
        
        // Stage 4 (55%) - 3L/4R or 4L/3R
        eclipse55Preset.particlePrefabs = CreateList(wispsPrefab, auroraPrefab);
        eclipse55Preset.audioProfile = quietProfile;
        eclipse55Preset.masterVolume = 0.3f;
        
        // Stage 5 (70%) - 4L/4R
        eclipse70Preset.particlePrefabs = CreateList(auroraPrefab, wispsPrefab);
        eclipse70Preset.audioProfile = quietProfile;
        eclipse70Preset.masterVolume = 0.28f;
        
        // Stage 6 (85%) - 4L/5R or 5L/4R
        eclipse85Preset.particlePrefabs = CreateList(auroraPrefab, auroraAltPrefab, wispsPrefab);
        eclipse85Preset.audioProfile = quietProfile;
        eclipse85Preset.masterVolume = 0.25f;
        
        // Stage 7 (100%) - 5L/5R - full eclipse
        eclipseFullPreset.particlePrefabs = CreateList(auroraPrefab, auroraAltPrefab, meteorPrefab, wispsPrefab);
        eclipseFullPreset.audioProfile = quietProfile;
        eclipseFullPreset.masterVolume = 0.2f; // Near silence for awe
    }
    
    List<GameObject> CreateList(params GameObject[] prefabs)
    {
        var list = new List<GameObject>();
        foreach (var p in prefabs)
        {
            if (p != null)
                list.Add(p);
        }
        return list;
    }
    
    void SubscribeToEvents()
    {
        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.OnRingsChanged.AddListener(OnRingsChanged);
            
            currentLeftRings = WorldStateManager.Instance.LeftRings;
            currentRightRings = WorldStateManager.Instance.RightRings;
            OnRingsChanged(currentLeftRings, currentRightRings);
        }
        else
        {
            Debug.LogWarning("[AmbienceController] WorldStateManager not found!");
        }
    }
    
    #endregion
    
    #region Event Handler
    
    void OnRingsChanged(int leftRings, int rightRings)
    {
        currentLeftRings = leftRings;
        currentRightRings = rightRings;
        debugLeftRings = leftRings;
        debugRightRings = rightRings;
        
        AmbiencePreset newPreset = GetPresetForRings(leftRings, rightRings);
        
        if (newPreset != currentPreset)
        {
            currentPreset = newPreset;
            currentPresetName = newPreset.stateName;
            
            // Apply particles
            ApplyParticles(newPreset);
            
            // Apply audio
            ApplyAudio(newPreset);
            
            if (logChanges)
                Debug.Log($"[AmbienceController] {leftRings}L/{rightRings}R → {newPreset.stateName} ({newPreset.particlePrefabs.Count} particles)");
        }
    }
    
    #endregion
    
    #region State Resolution (same logic as PostProcessController)
    
    /// <summary>
    /// Resolve a preset from raw ring counts.
    /// Cascade matches WorldStateManager exactly (first match wins):
    /// 1. Eclipse (min>=2 AND diff<=1) -> stage 1-7
    /// 2. Sunset  ((1L/0R) OR (diff=2, dark wins, both, max<=4))
    /// 3. Sunrise ((0L/1R) OR (diff=2, light wins, both, max<=4))
    /// 4. Escalation (diff>=6) -> DarkStage/LightStage by (diff-5)
    /// 5. Path (diff 1-5) -> Dark/Light by diff
    /// 6. Neutral (L==R, not eclipse)
    /// </summary>
    AmbiencePreset GetPresetForRings(int L, int R)
    {
        int diff = Mathf.Abs(L - R);
        int minRings = Mathf.Min(L, R);
        int maxRings = Mathf.Max(L, R);
        bool darkWinning = L > R;
        bool lightWinning = R > L;
        bool bothHaveRings = L > 0 && R > 0;
        
        // === PRIORITY 1: ECLIPSE (7 stages, see GDD §5) ===
        if (minRings >= 2 && diff <= 1)
        {
            if (L == 5 && R == 5)                       return eclipseFullPreset; // Stage 7
            if (minRings == 4 && maxRings == 5)         return eclipse85Preset;   // Stage 6
            if (L == 4 && R == 4)                       return eclipse70Preset;   // Stage 5
            if (minRings == 3 && maxRings == 4)         return eclipse55Preset;   // Stage 4
            if (L == 3 && R == 3)                       return eclipse40Preset;   // Stage 3
            if (minRings == 2 && maxRings == 3)         return eclipse25Preset;   // Stage 2
            if (L == 2 && R == 2)                       return eclipse15Preset;   // Stage 1
        }
        
        // === PRIORITY 2: SUNSET ===
        if ((L == 1 && R == 0) ||
            (diff == 2 && darkWinning && bothHaveRings && maxRings <= 4))
        {
            return sunsetPreset;
        }
        
        // === PRIORITY 3: SUNRISE ===
        if ((L == 0 && R == 1) ||
            (diff == 2 && lightWinning && bothHaveRings && maxRings <= 4))
        {
            return sunrisePreset;
        }
        
        // === PRIORITY 4: ESCALATION (diff >= 6) ===
        if (diff >= 6)
        {
            int stage = Mathf.Clamp(diff - 5, 1, 5);
            if (darkWinning)
            {
                switch (stage)
                {
                    case 1: return darkStage1Preset;
                    case 2: return darkStage2Preset;
                    case 3: return darkStage3Preset;
                    case 4: return darkStage4Preset;
                    default: return darkStage5Preset;
                }
            }
            else // lightWinning (diff >= 6 guarantees L != R)
            {
                switch (stage)
                {
                    case 1: return lightStage1Preset;
                    case 2: return lightStage2Preset;
                    case 3: return lightStage3Preset;
                    case 4: return lightStage4Preset;
                    default: return lightStage5Preset;
                }
            }
        }
        
        // === PRIORITY 5: PATH (diff 1-5) ===
        if (diff >= 1)
        {
            if (darkWinning)
            {
                switch (diff)
                {
                    case 1: return dark1Preset;
                    case 2: return dark2Preset;
                    case 3: return dark3Preset;
                    case 4: return dark4Preset;
                    default: return dark5Preset; // diff == 5
                }
            }
            else // lightWinning
            {
                switch (diff)
                {
                    case 1: return light1Preset;
                    case 2: return light2Preset;
                    case 3: return light3Preset;
                    case 4: return light4Preset;
                    default: return light5Preset; // diff == 5
                }
            }
        }
        
        // === PRIORITY 6: NEUTRAL ===
        return neutralPreset;
    }
    
    #endregion
    
    #region Particle Management
    
    void ApplyParticles(AmbiencePreset preset)
    {
        // Clear existing particles
        ClearAllParticles();
        
        // Spawn new particles
        foreach (var prefab in preset.particlePrefabs)
        {
            if (prefab != null)
            {
                SpawnParticle(prefab);
            }
        }
        
        activeParticleCount = activeParticles.Count;
    }
    
    void SpawnParticle(GameObject prefab)
    {
        Vector3 spawnPos = particleParent.position;
        if (playerTransform != null)
            spawnPos = playerTransform.position + particleOffset;
        
        GameObject particle = Instantiate(prefab, spawnPos, Quaternion.identity, particleParent);
        particle.name = $"YORU_Particle_{prefab.name}";
        activeParticles.Add(particle);
    }
    
    void ClearAllParticles()
    {
        foreach (var particle in activeParticles)
        {
            if (particle != null)
            {
                // Try to stop particle systems gracefully
                var ps = particle.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(particle, 2f); // Delay to let particles fade
                }
                else
                {
                    Destroy(particle);
                }
            }
        }
        activeParticles.Clear();
    }
    
    #endregion
    
    #region Audio Management
    
    void ApplyAudio(AmbiencePreset preset)
    {
        if (!hasAmbienceModule || ambienceModule == null || preset.audioProfile == null)
            return;
        
        try
        {
            // Try to set ambience via COZY
            var setMethod = ambienceModule.GetType().GetMethod("SetAmbience");
            if (setMethod != null)
            {
                setMethod.Invoke(ambienceModule, new object[] { preset.audioProfile, preset.audioWeight });
            }
        }
        catch (Exception e)
        {
            if (logChanges)
                Debug.LogWarning($"[AmbienceController] Audio error: {e.Message}");
        }
    }
    
    #endregion
    
    #region Context Menu Tests
    
    [ContextMenu("Test: Neutral")]
    void TestNeutral() { OnRingsChanged(0, 0); }
    
    [ContextMenu("Test: Sunset")]
    void TestSunset() { OnRingsChanged(1, 0); }
    
    [ContextMenu("Test: Dark5 (Fireflies)")]
    void TestDark5() { OnRingsChanged(5, 0); }
    
    [ContextMenu("Test: Dark+Stage5 (Thunderstorm)")]
    void TestDarkStage5() { OnRingsChanged(10, 0); }
    
    [ContextMenu("Test: Light5 (Butterflies)")]
    void TestLight5() { OnRingsChanged(0, 5); }
    
    [ContextMenu("Test: Light+Stage5 (Divine)")]
    void TestLightStage5() { OnRingsChanged(0, 10); }
    
    [ContextMenu("Test: Eclipse FULL (Aurora + Meteor)")]
    void TestEclipseFull() { OnRingsChanged(5, 5); }
    
    [ContextMenu("Clear All Particles")]
    void TestClearParticles() { ClearAllParticles(); }
    
    #endregion
    
    #region Public API
    
    public void ForceRefresh()
    {
        if (WorldStateManager.Instance != null)
        {
            OnRingsChanged(WorldStateManager.Instance.LeftRings, WorldStateManager.Instance.RightRings);
        }
    }
    
    public AmbiencePreset GetCurrentPreset() => currentPreset;
    public int GetActiveParticleCount() => activeParticles.Count;
    
    #endregion
}