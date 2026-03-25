using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections;

/// <summary>
/// YORU Combat Post-Process Pulse — Game Feel Phase 1
/// 
/// Brief chromatic aberration + vignette pulses on heavy hits, parry, and player damage.
/// Uses a SEPARATE PostProcessVolume (priority 100, global, weight 0) so it NEVER
/// touches the 29-state atmosphere system on PostProcessController.
/// 
/// How it works:
///   - Weight sits at 0 (invisible) by default
///   - On pulse: weight ramps to 1, decays back to 0 over ~0.15s
///   - The profile overrides ONLY chromatic aberration and vignette
///   - Atmosphere volume (priority 0) is untouched underneath
/// 
/// All timing uses unscaledDeltaTime — works during Flurry Rush (timeScale 0.3).
/// 
/// SETUP: Attach to a new empty GameObject called "CombatPostProcessPulse".
///        No Inspector setup needed — volume + profile are created at runtime.
///        The scene MUST already have a PostProcessLayer on the camera (it does,
///        since PostProcessController uses one).
/// </summary>
public class CombatPostProcessPulse : MonoBehaviour
{
    #region Singleton
    public static CombatPostProcessPulse Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Serialized Fields
    [Header("Hit Pulse (Combo3, Heavy Attack)")]
    [Tooltip("Chromatic aberration intensity on heavy hit")]
    [SerializeField] private float hitChromatic = 0.4f;
    [Tooltip("Extra vignette intensity added on heavy hit")]
    [SerializeField] private float hitVignette = 0.15f;
    [Tooltip("Pulse decay time in seconds")]
    [SerializeField] private float hitDuration = 0.15f;

    [Header("Parry Pulse (strongest in the game)")]
    [SerializeField] private float parryChromatic = 0.55f;
    [SerializeField] private float parryVignette = 0.2f;
    [SerializeField] private float parryDuration = 0.2f;

    [Header("Player Damage Pulse")]
    [SerializeField] private float damageChromatic = 0.3f;
    [Tooltip("Vignette on player damage — heavier than hit pulse")]
    [SerializeField] private float damageVignette = 0.25f;
    [SerializeField] private float damageDuration = 0.2f;
    [Tooltip("Vignette color on player damage (red = danger)")]
    [SerializeField] private Color damageVignetteColor = new Color(0.4f, 0.05f, 0.05f);

    [Header("Default Vignette Color (non-damage pulses)")]
    [SerializeField] private Color defaultVignetteColor = new Color(0.1f, 0.06f, 0.12f);

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private PostProcessVolume pulseVolume;
    private ChromaticAberration chromatic;
    private Vignette vignette;
    private Coroutine activePulse;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        CreatePulseVolume();
        DebugLog("CombatPostProcessPulse initialized");
    }

    private void OnDestroy()
    {
        // Clean up runtime-created assets to prevent leaks in editor
        if (pulseVolume != null && pulseVolume.profile != null)
        {
            if (Application.isPlaying)
                Destroy(pulseVolume.profile);
        }
    }
    #endregion

    #region Runtime Setup
    private void CreatePulseVolume()
    {
        // Create PostProcessVolume on this GameObject
        pulseVolume = gameObject.AddComponent<PostProcessVolume>();
        pulseVolume.isGlobal = true;
        pulseVolume.priority = 100; // Above atmosphere volume (priority 0)
        pulseVolume.weight = 0f;    // Invisible by default

        // Create runtime profile — only overrides chromatic + vignette
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

        chromatic = ScriptableObject.CreateInstance<ChromaticAberration>();
        chromatic.enabled.Override(true);
        chromatic.intensity.Override(0f);
        profile.AddSettings(chromatic);

        vignette = ScriptableObject.CreateInstance<Vignette>();
        vignette.enabled.Override(true);
        vignette.mode.Override(VignetteMode.Classic);
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.6f);
        vignette.color.Override(defaultVignetteColor);
        profile.AddSettings(vignette);

        pulseVolume.profile = profile;

        DebugLog("Pulse volume created (priority 100, global, weight 0)");
    }
    #endregion

    #region Public API — Called by CombatFeedbackManager

    /// <summary>
    /// Heavy hit pulse — combo3, heavy attack. Moderate intensity.
    /// </summary>
    public void PulseHit()
    {
        vignette.color.Override(defaultVignetteColor);
        StartPulse(hitChromatic, hitVignette, hitDuration);
        DebugLog("Pulse: heavy hit");
    }

    /// <summary>
    /// Parry pulse — strongest in the game. Sharp and bright.
    /// </summary>
    public void PulseParry()
    {
        vignette.color.Override(defaultVignetteColor);
        StartPulse(parryChromatic, parryVignette, parryDuration);
        DebugLog("Pulse: parry");
    }

    /// <summary>
    /// Player damage pulse — red vignette for danger feel.
    /// </summary>
    public void PulseDamage(bool isHeavy)
    {
        float chromMult = isHeavy ? 1f : 0.6f;
        float vigMult = isHeavy ? 1f : 0.5f;
        vignette.color.Override(damageVignetteColor);
        StartPulse(damageChromatic * chromMult, damageVignette * vigMult, damageDuration);
        DebugLog($"Pulse: player damage ({(isHeavy ? "heavy" : "light")})");
    }
    #endregion

    #region Pulse Coroutine
    private void StartPulse(float chromIntensity, float vigIntensity, float duration)
    {
        if (activePulse != null)
            StopCoroutine(activePulse);
        activePulse = StartCoroutine(PulseCoroutine(chromIntensity, vigIntensity, duration));
    }

    private IEnumerator PulseCoroutine(float chromIntensity, float vigIntensity, float duration)
    {
        // Set peak values
        chromatic.intensity.Override(chromIntensity);
        vignette.intensity.Override(vigIntensity);
        pulseVolume.weight = 1f;

        // Decay weight from 1 → 0 over duration
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out: fast start, slow tail — feels snappy
            float weight = 1f - (t * t);
            pulseVolume.weight = weight;
            yield return null;
        }

        // Reset fully
        pulseVolume.weight = 0f;
        chromatic.intensity.Override(0f);
        vignette.intensity.Override(0f);
        activePulse = null;
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[PostProcessPulse] {message}");
    }
    #endregion
}
