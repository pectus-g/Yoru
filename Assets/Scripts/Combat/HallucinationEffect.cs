using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections;

/// <summary>
/// YORU Magic-Mushroom Hallucination — Nopperabō "Mushroom" attack.
///
/// A trippy, full-screen post-process wash triggered when Nopperabō's Mushroom attack lands.
/// Uses a SEPARATE PostProcessVolume (priority 101, global, weight 0) so it sits ABOVE the
/// combat pulse (priority 100) and the 29-state atmosphere system (priority 0) and NEVER
/// touches either. Pattern mirrors CombatPostProcessPulse.cs — builds its own volume + profile
/// at runtime in Start(), so there is no Inspector setup beyond attaching this script to an
/// empty GameObject named "HallucinationEffect".
///
/// How it works:
///   - Weight sits at 0 (invisible) by default.
///   - On Trigger(duration): weight ramps IN, HOLDS for the bulk of the duration, ramps OUT.
///   - The profile overrides only LensDistortion + ChromaticAberration + Bloom + Vignette
///     + ColorGrading + Grain — all stacked for a woozy, disorienting wash.
///
/// CRITICAL — DAMAGE GATE:
///   While the effect is ramping in / holding / ramping out, <see cref="IsActive"/> is true.
///   EnemyHealth.TakeDamage(int, bool) reads this static flag and ignores ALL incoming damage
///   while it is true, so Yoru can still move and input during the hallucination but deals zero
///   damage to every enemy. The flag is the ONLY thing the combat code depends on — the visual
///   is purely cosmetic on top of it.
///
/// All timing uses unscaledDeltaTime so the effect is unaffected by hitstop (Animator.speed=0)
/// or Flurry Rush (timeScale 0.3).
///
/// SETUP: Attach to a new empty GameObject called "HallucinationEffect".
///        The scene MUST already have a PostProcessLayer on the camera (it does, since
///        PostProcessController and CombatPostProcessPulse both rely on one).
/// </summary>
public class HallucinationEffect : MonoBehaviour
{
    #region Singleton
    public static HallucinationEffect Instance { get; private set; }

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

    #region Damage Gate
    /// <summary>
    /// True while the hallucination is ramping in, holding, or ramping out. Read by
    /// EnemyHealth.TakeDamage to gate ALL of Yoru's outgoing damage to zero for the window.
    /// Static so the gate needs no reference lookup. Reset to false in OnDestroy for safety.
    /// </summary>
    public static bool IsActive { get; private set; }
    #endregion

    #region Serialized Fields
    [Header("Ramp Timing (fraction of total duration)")]
    [Tooltip("Portion of the total duration spent ramping the wash IN")]
    [SerializeField, Range(0.05f, 0.45f)] private float rampInFraction = 0.2f;
    [Tooltip("Portion of the total duration spent ramping the wash OUT")]
    [SerializeField, Range(0.05f, 0.45f)] private float rampOutFraction = 0.3f;

    [Header("Lens Distortion (the woozy bend)")]
    [Tooltip("Peak lens distortion intensity (negative = pinch, positive = bulge)")]
    [SerializeField] private float lensDistortionPeak = -35f;
    [Tooltip("Speed of the in/out distortion wobble while held")]
    [SerializeField] private float lensWobbleSpeed = 2.2f;
    [Tooltip("Wobble amount added on top of the peak while held")]
    [SerializeField] private float lensWobbleAmount = 12f;

    [Header("Chromatic Aberration (color fringing)")]
    [SerializeField] private float chromaticPeak = 0.9f;
    [SerializeField] private float chromaticPulseSpeed = 3.5f;

    [Header("Bloom (dreamy flare)")]
    [SerializeField] private float bloomPeak = 6f;
    [SerializeField] private float bloomThreshold = 0.7f;

    [Header("Vignette (tunnel closing in)")]
    [SerializeField] private float vignettePeak = 0.45f;
    [SerializeField] private Color vignetteColor = new Color(0.35f, 0.1f, 0.45f);

    [Header("Color Grading (saturation surge + hue cycle)")]
    [Tooltip("Peak added saturation while hallucinating")]
    [SerializeField] private float saturationPeak = 60f;
    [Tooltip("Degrees-per-second the hue shifts while held — the 'colors breathing' feel")]
    [SerializeField] private float hueCycleSpeed = 80f;
    [Tooltip("Peak contrast added while hallucinating")]
    [SerializeField] private float contrastPeak = 20f;

    [Header("Grain (film fuzz)")]
    [SerializeField] private float grainPeak = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [Tooltip("Force weight to 1 permanently on Start for render testing. Disable after confirming visibility.")]
    [SerializeField] private bool forceVisibleForTesting = false;
    [Tooltip("Use only Vignette effect (like CombatPostProcessPulse which works). Enable this to test if PPv2 renders at all.")]
    [SerializeField] private bool useSimpleEffectsOnly = false;
    #endregion

    #region Private Fields
    private PostProcessVolume volume;
    private LensDistortion lensDistortion;
    private ChromaticAberration chromatic;
    private Bloom bloom;
    private Vignette vignette;
    private ColorGrading colorGrading;
    private Grain grain;
    private Coroutine activeHallucination;

    // Safety timer — releases IsActive if the coroutine somehow fails
    private float safetyReleaseTime;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        CreateHallucinationVolume();
        ValidatePostProcessSetup();
        DebugLog("HallucinationEffect ready");

        // Debug mode: force the effect visible to verify PPv2 rendering works at all
        if (forceVisibleForTesting)
        {
            volume.weight = 1f;
            // Use VERY strong values for testing so it's unmissable
            if (vignette != null)
            {
                vignette.intensity.Override(0.7f); // Very strong vignette
                vignette.color.Override(Color.red); // Bright red so it's obvious
            }
            if (lensDistortion != null) lensDistortion.intensity.Override(lensDistortionPeak);
            if (chromatic != null) chromatic.intensity.Override(chromaticPeak);
            if (bloom != null) bloom.intensity.Override(bloomPeak);
            if (colorGrading != null)
            {
                colorGrading.saturation.Override(saturationPeak);
                colorGrading.contrast.Override(contrastPeak);
            }
            if (grain != null) grain.intensity.Override(grainPeak);
            Debug.LogError($"[Hallucination] *** FORCE VISIBLE MODE ACTIVE *** simpleOnly={useSimpleEffectsOnly} — you should see RED VIGNETTE! Disable after testing.");
        }
    }

    private void OnDestroy()
    {
        // Always clear the gate if this object is torn down mid-effect, or Yoru's damage
        // would stay disabled forever.
        IsActive = false;

        if (volume != null && volume.profile != null)
        {
            if (Application.isPlaying)
                Destroy(volume.profile);
        }
    }

    private void Update()
    {
        // Safety release — if IsActive is true but the timer has expired, force release.
        // This catches cases where the coroutine dies unexpectedly (scene unload, exception).
        if (IsActive && Time.unscaledTime > safetyReleaseTime)
        {
            Debug.LogWarning("[Hallucination] Safety release — IsActive was stuck, forcing off");
            IsActive = false;
            ResetAll();
            activeHallucination = null;
        }

        // Debug key: H = test hallucination effect (ALWAYS works, ignores showDebugLogs)
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.LogError("[Hallucination] ========== H KEY PRESSED ==========");
            Debug.LogError($"[Hallucination] volume={volume != null}, profile={volume?.profile != null}, vignette={vignette != null}");
            Debug.LogError($"[Hallucination] weight BEFORE={volume?.weight}, isGlobal={volume?.isGlobal}, priority={volume?.priority}");

            // Force immediate test - set weight to 1 and vignette intensity directly
            if (volume != null && vignette != null)
            {
                volume.weight = 1f;
                vignette.intensity.Override(0.8f);
                vignette.color.Override(Color.red);
                Debug.LogError($"[Hallucination] FORCED weight={volume.weight}, vignette.intensity should be 0.8 RED");
                Debug.LogError("[Hallucination] If you DON'T see red edges, PostProcessLayer is broken!");
            }

            // Also trigger normal effect
            Trigger(3f);
        }
    }
    #endregion

    #region Runtime Setup
    private void CreateHallucinationVolume()
    {
        volume = gameObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 101; // Above CombatPostProcessPulse (100) and atmosphere (0)
        volume.weight = 0f;    // Invisible by default

        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

        // Vignette is always created (known to work in CombatPostProcessPulse)
        vignette = ScriptableObject.CreateInstance<Vignette>();
        vignette.enabled.Override(true);
        vignette.mode.Override(VignetteMode.Classic);
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(0.6f);
        vignette.color.Override(vignetteColor);
        profile.AddSettings(vignette);

        if (useSimpleEffectsOnly)
        {
            // Simple mode: only vignette (matches CombatPostProcessPulse setup)
            volume.profile = profile;
            DebugLog("Hallucination volume built with SIMPLE EFFECTS ONLY (vignette)");
            return;
        }

        // Full effect suite
        lensDistortion = ScriptableObject.CreateInstance<LensDistortion>();
        lensDistortion.enabled.Override(true);
        lensDistortion.intensity.Override(0f);
        profile.AddSettings(lensDistortion);

        chromatic = ScriptableObject.CreateInstance<ChromaticAberration>();
        chromatic.enabled.Override(true);
        chromatic.intensity.Override(0f);
        profile.AddSettings(chromatic);

        bloom = ScriptableObject.CreateInstance<Bloom>();
        bloom.enabled.Override(true);
        bloom.intensity.Override(0f);
        bloom.threshold.Override(bloomThreshold);
        profile.AddSettings(bloom);

        colorGrading = ScriptableObject.CreateInstance<ColorGrading>();
        colorGrading.enabled.Override(true);
        colorGrading.gradingMode.Override(GradingMode.LowDefinitionRange);
        colorGrading.saturation.Override(0f);
        colorGrading.hueShift.Override(0f);
        colorGrading.contrast.Override(0f);
        profile.AddSettings(colorGrading);

        grain = ScriptableObject.CreateInstance<Grain>();
        grain.enabled.Override(true);
        grain.intensity.Override(0f);
        profile.AddSettings(grain);

        volume.profile = profile;

        DebugLog("Hallucination volume + 6 effects built (priority 101, weight 0)");
    }

    /// <summary>
    /// Validates PPv2 setup at runtime and logs diagnostics if something is misconfigured.
    /// </summary>
    private void ValidatePostProcessSetup()
    {
        // Find the main camera's PostProcessLayer
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[Hallucination] DIAGNOSTIC: No Camera.main found!");
            return;
        }

        var ppLayer = mainCam.GetComponent<PostProcessLayer>();
        if (ppLayer == null)
        {
            Debug.LogError("[Hallucination] DIAGNOSTIC: Camera.main has NO PostProcessLayer component! PPv2 effects will not render.");
            return;
        }

        if (!ppLayer.enabled)
        {
            Debug.LogError("[Hallucination] DIAGNOSTIC: PostProcessLayer on camera is DISABLED!");
            return;
        }

        // Check if our volume's layer is included in the PostProcessLayer's volumeLayer mask
        int ourLayer = gameObject.layer;
        LayerMask volumeMask = ppLayer.volumeLayer;
        bool layerIncluded = (volumeMask.value & (1 << ourLayer)) != 0;

        if (!layerIncluded)
        {
            Debug.LogWarning($"[Hallucination] DIAGNOSTIC: GameObject is on layer {ourLayer} ({LayerMask.LayerToName(ourLayer)}), " +
                $"but PostProcessLayer.volumeLayer mask ({volumeMask.value}) does NOT include it. " +
                "Global volumes should still work, but if not visible, try setting this GO to a layer in the mask.");
        }

        // Check if volume was created properly
        if (volume == null)
        {
            Debug.LogError("[Hallucination] DIAGNOSTIC: PostProcessVolume is NULL after creation!");
            return;
        }

        if (volume.profile == null)
        {
            Debug.LogError("[Hallucination] DIAGNOSTIC: PostProcessVolume.profile is NULL!");
            return;
        }

        DebugLog($"PPv2 setup validated — camera has PostProcessLayer, volume ready on layer {ourLayer}");
    }
    #endregion

    #region Public API — Called by EnemyCombat
    /// <summary>
    /// Triggers the hallucination wash for the given duration (seconds). Sets IsActive true
    /// for the whole window so Yoru's damage is gated. Restarts cleanly if called while already
    /// active. The pull-toward motion is NOT handled here — that is Yoru's own hit-reaction.
    /// </summary>
    public void Trigger(float durationInSeconds)
    {
        if (durationInSeconds <= 0f) return;

        if (activeHallucination != null)
            StopCoroutine(activeHallucination);

        // Safety timer — set to duration + 1s buffer so Update() can release if coroutine dies
        safetyReleaseTime = Time.unscaledTime + durationInSeconds + 1f;

        activeHallucination = StartCoroutine(HallucinationCoroutine(durationInSeconds));
        DebugLog($"Triggered ({durationInSeconds:F1}s)");
    }

    /// <summary>
    /// Immediately ends the hallucination, resets all effects to zero, and clears the gate.
    /// Use on staggers, scene changes, or any hard interrupt.
    /// </summary>
    public void StopImmediate()
    {
        if (activeHallucination != null)
        {
            StopCoroutine(activeHallucination);
            activeHallucination = null;
        }

        ResetAll();
        IsActive = false;
        DebugLog("Stopped immediately");
    }
    #endregion

    #region Hallucination Coroutine
    private IEnumerator HallucinationCoroutine(float duration)
    {
        IsActive = true;

        float rampIn = duration * rampInFraction;
        float rampOut = duration * rampOutFraction;
        float hold = Mathf.Max(0f, duration - rampIn - rampOut);

        // Ramp IN — weight 0 → 1
        float elapsed = 0f;
        while (elapsed < rampIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / rampIn);
            ApplyAtStrength(t);
            yield return null;
        }

        // HOLD — full strength with live wobble / hue cycle
        elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyAtStrength(1f);
            yield return null;
        }

        // Ramp OUT — weight 1 → 0
        elapsed = 0f;
        while (elapsed < rampOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / rampOut);
            ApplyAtStrength(t);
            yield return null;
        }

        ResetAll();
        IsActive = false;
        activeHallucination = null;
        DebugLog("Ended");
    }

    /// <summary>
    /// Applies every stacked effect scaled by strength (0..1), plus the time-based wobble,
    /// chromatic pulse, and hue cycle that make the wash feel alive while held.
    /// Wrapped in try/catch so a visual exception never leaves IsActive stuck.
    /// </summary>
    private void ApplyAtStrength(float strength)
    {
        try
        {
            if (volume == null) return;
            volume.weight = strength;

            float time = Time.unscaledTime;

            if (lensDistortion != null)
            {
                float wobble = Mathf.Sin(time * lensWobbleSpeed) * lensWobbleAmount * strength;
                lensDistortion.intensity.Override(lensDistortionPeak * strength + wobble);
            }

            if (chromatic != null)
            {
                float chromPulse = (0.7f + 0.3f * Mathf.Sin(time * chromaticPulseSpeed));
                chromatic.intensity.Override(chromaticPeak * strength * chromPulse);
            }

            if (bloom != null)
                bloom.intensity.Override(bloomPeak * strength);

            if (vignette != null)
                vignette.intensity.Override(vignettePeak * strength);

            if (colorGrading != null)
            {
                colorGrading.saturation.Override(saturationPeak * strength);
                colorGrading.contrast.Override(contrastPeak * strength);
                // Hue cycles continuously while active — wraps in [-180, 180]
                float hue = Mathf.Repeat(time * hueCycleSpeed * strength, 360f);
                if (hue > 180f) hue -= 360f;
                colorGrading.hueShift.Override(hue);
            }

            if (grain != null)
                grain.intensity.Override(grainPeak * strength);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Hallucination] visual apply failed: {e.Message}");
        }
    }

    private void ResetAll()
    {
        if (volume != null) volume.weight = 0f;
        if (lensDistortion != null) lensDistortion.intensity.Override(0f);
        if (chromatic != null) chromatic.intensity.Override(0f);
        if (bloom != null) bloom.intensity.Override(0f);
        if (vignette != null) vignette.intensity.Override(0f);
        if (colorGrading != null)
        {
            colorGrading.saturation.Override(0f);
            colorGrading.hueShift.Override(0f);
            colorGrading.contrast.Override(0f);
        }
        if (grain != null) grain.intensity.Override(0f);
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Hallucination] {message}");
    }
    #endregion
}
