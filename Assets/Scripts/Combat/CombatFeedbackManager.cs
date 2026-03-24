using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat Feedback — Phase 3B
/// Singleton manager for all combat "juice": hitstop, camera shake, VFX at contact, post-process pulse.
/// All timing uses WaitForSecondsRealtime / unscaledTime so it works during Flurry Rush (timeScale 0.3).
/// 
/// HITSTOP: Animator.speed = 0, NOT Time.timeScale = 0 (kills VFX, camera, audio).
/// CAMERA SHAKE: Perlin noise, not Random.insideUnitSphere (jittery). Accessibility slider ready.
/// VFX: Spawned at actual contact point, not attackPoint.
/// SFX: Delegated to CombatSFXManager (separate script).
/// 
/// Attach to a persistent GameObject in the scene (e.g. "CombatManagers").
/// </summary>
public class CombatFeedbackManager : MonoBehaviour
{
    #region Singleton
    public static CombatFeedbackManager Instance { get; private set; }

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
    [Header("Hitstop Durations")]
    [Tooltip("Light hit: Combo1, Combo2")]
    [SerializeField] private float lightHitStopDuration = 0.04f;
    [Tooltip("Heavy hit: Combo3, Heavy Attack, Pounce")]
    [SerializeField] private float heavyHitStopDuration = 0.08f;
    [Tooltip("Parry success — most dramatic freeze")]
    [SerializeField] private float parryHitStopDuration = 0.12f;
    [Tooltip("Guard block (not perfect parry) — subtle")]
    [SerializeField] private float guardHitStopDuration = 0.04f;

    [Header("Camera Shake Intensities")]
    [Tooltip("Combo1, Combo2 — subtle")]
    [SerializeField] private float lightShakeIntensity = 0.15f;
    [Tooltip("Combo3, Heavy Attack — noticeable")]
    [SerializeField] private float heavyShakeIntensity = 0.35f;
    [Tooltip("Parry success — strongest")]
    [SerializeField] private float parryShakeIntensity = 0.50f;
    [Tooltip("Camera shake duration in seconds")]
    [SerializeField] private float shakeDuration = 0.15f;
    [Tooltip("Perlin noise frequency — higher = faster shake")]
    [SerializeField] private float shakeFrequency = 25f;

    [Header("Camera Shake Accessibility")]
    [Tooltip("Global shake multiplier (0 = off, 1 = full). Hook to Settings slider.")]
    [Range(0f, 1f)]
    [SerializeField] private float shakeIntensityMultiplier = 1f;

    [Header("Post-Process Pulse (Heavy Hits Only)")]
    [Tooltip("Enable chromatic aberration / vignette pulse on heavy hits")]
    [SerializeField] private bool enablePostProcessPulse = true;
    [Tooltip("Pulse intensity — chromatic aberration amount")]
    [SerializeField] private float pulseIntensity = 0.4f;
    [Tooltip("Pulse duration in seconds")]
    [SerializeField] private float pulseDuration = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private Camera mainCamera;
    private Transform camTransform;
    private Vector3 originalCamLocalPos;
    private Coroutine shakeCoroutine;
    private Coroutine pulseCoroutine;
    private YoruVFXManager vfxManager;

    // Post-process references (assigned at runtime if available)
    // Using reflection-free approach: cache the post-process volume component if present
    private MonoBehaviour postProcessVolume;

    // Hitstop stacking fix: track active coroutine + frozen animators
    // Without this, overlapping HitStop calls corrupt the saved speed:
    //   Hit1 saves speed=1, sets 0. Hit2 saves speed=0(!!), sets 0.
    //   Hit1 restores 1. Hit2 restores 0 → permanently stuck.
    private Coroutine activeHitStopCoroutine;
    private Animator frozenPlayerAnim;
    private Animator frozenEnemyAnim;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            camTransform = mainCamera.transform;
            originalCamLocalPos = camTransform.localPosition;
        }

        DebugLog("CombatFeedbackManager initialized");

        vfxManager = FindObjectOfType<YoruVFXManager>();
    }
    #endregion

    #region Public API — Called by PlayerCombat and PlayerHealth

    /// <summary>
    /// Full feedback burst for a landed player attack.
    /// Call from PlayerCombat.DealDamageInRange() when a hit is confirmed.
    /// </summary>
    /// <param name="contactPoint">World position where the hit connected (enemy collider closest point)</param>
    /// <param name="isHeavy">True for Combo3, Heavy Attack, Pounce</param>
    /// <param name="playerAnimator">Yoru's Animator — frozen during hitstop</param>
    /// <param name="enemyAnimator">Hit enemy's Animator — frozen during hitstop (can be null)</param>
    public void PlayHitFeedback(Vector3 contactPoint, bool isHeavy, Animator playerAnimator, Animator enemyAnimator)
    {
        float stopDuration = isHeavy ? heavyHitStopDuration : lightHitStopDuration;
        float shakeAmount = isHeavy ? heavyShakeIntensity : lightShakeIntensity;

        HitStop(stopDuration, playerAnimator, enemyAnimator);
        CameraShake(shakeAmount, shakeDuration);
        SpawnHitVFX(contactPoint, isHeavy);

        if (isHeavy && enablePostProcessPulse)
            PostProcessPulse(pulseIntensity, pulseDuration);

        DebugLog($"Hit feedback: {(isHeavy ? "HEAVY" : "light")} at {contactPoint}");
    }

    /// <summary>
    /// Parry-specific feedback — strongest freeze and shake in the game.
    /// Call from PlayerCombat.OnPerfectParry().
    /// </summary>
    public void PlayParryFeedback(Vector3 contactPoint, Animator playerAnimator, Animator enemyAnimator)
    {
        HitStop(parryHitStopDuration, playerAnimator, enemyAnimator);
        CameraShake(parryShakeIntensity, shakeDuration * 1.5f);
        SpawnHitVFX(contactPoint, true);
        PostProcessPulse(pulseIntensity * 1.2f, pulseDuration);

        DebugLog("Parry feedback triggered");
    }

    /// <summary>
    /// Guard block feedback — subtle hitstop only.
    /// Call from PlayerHealth.TakeDamage() when guarding but not perfect parry.
    /// </summary>
    public void PlayGuardFeedback()
    {
        // Guard uses global hitstop (no specific animator targets needed)
        HitStop(guardHitStopDuration, null, null);
        CameraShake(lightShakeIntensity * 0.5f, shakeDuration * 0.5f);

        DebugLog("Guard feedback triggered");
    }

    /// <summary>
    /// Player took damage — light feedback (screen shake only, no hitstop on player).
    /// Call from PlayerHealth.TakeDamage() after HP subtraction.
    /// </summary>
    public void PlayPlayerHitFeedback(bool isHeavy)
    {
        float intensity = isHeavy ? heavyShakeIntensity * 0.8f : lightShakeIntensity * 0.6f;
        CameraShake(intensity, shakeDuration);

        if (isHeavy && enablePostProcessPulse)
            PostProcessPulse(pulseIntensity * 0.7f, pulseDuration);

        DebugLog($"Player hit feedback: {(isHeavy ? "heavy" : "light")}");
    }
    #endregion

    #region Hitstop — Animator.speed = 0 (single-active, no stacking)

    /// <summary>
    /// Freeze animators for a brief moment. Uses WaitForSecondsRealtime so it works during Flurry Rush.
    /// CRITICAL: Only one hitstop can be active at a time. Starting a new one cancels the old one
    /// and restores animators before re-freezing. This prevents the stacking bug where overlapping
    /// coroutines corrupt the saved speed value (saving 0 instead of 1).
    /// </summary>
    public void HitStop(float duration, Animator playerAnimator, Animator enemyAnimator)
    {
        if (duration <= 0f) return;

        // Cancel previous hitstop and restore animators immediately
        if (activeHitStopCoroutine != null)
        {
            StopCoroutine(activeHitStopCoroutine);
            RestoreFrozenAnimators();
        }

        activeHitStopCoroutine = StartCoroutine(HitStopCoroutine(duration, playerAnimator, enemyAnimator));
    }

    private IEnumerator HitStopCoroutine(float duration, Animator playerAnim, Animator enemyAnim)
    {
        // Track which animators we froze so RestoreFrozenAnimators can clean up
        frozenPlayerAnim = playerAnim;
        frozenEnemyAnim = enemyAnim;

        // Freeze
        if (playerAnim != null)
            playerAnim.speed = 0f;
        if (enemyAnim != null)
            enemyAnim.speed = 0f;

        // Wait real time — not affected by timeScale
        yield return new WaitForSecondsRealtime(duration);

        // Restore — always to 1f, never to a captured value that could be 0
        RestoreFrozenAnimators();
        activeHitStopCoroutine = null;
    }

    /// <summary>
    /// Restore any currently-frozen animators to speed 1. Safe to call multiple times.
    /// </summary>
    private void RestoreFrozenAnimators()
    {
        if (frozenPlayerAnim != null)
        {
            frozenPlayerAnim.speed = 1f;
            frozenPlayerAnim = null;
        }
        if (frozenEnemyAnim != null)
        {
            frozenEnemyAnim.speed = 1f;
            frozenEnemyAnim = null;
        }
    }
    #endregion

    #region Camera Shake — Perlin Noise

    /// <summary>
    /// Perlin noise camera shake. Uses unscaledDeltaTime for Flurry Rush compatibility.
    /// Respects shakeIntensityMultiplier (accessibility slider).
    /// </summary>
    public void CameraShake(float intensity, float duration)
    {
        if (camTransform == null || shakeIntensityMultiplier <= 0f) return;

        float adjustedIntensity = intensity * shakeIntensityMultiplier;
        if (adjustedIntensity < 0.01f) return;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeCoroutine(adjustedIntensity, duration));
    }

    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        float seed = Time.unscaledTime * 100f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float decay = 1f - t; // Linear falloff

            float x = (Mathf.PerlinNoise(seed, Time.unscaledTime * shakeFrequency) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(seed + 100f, Time.unscaledTime * shakeFrequency) - 0.5f) * 2f;

            Vector3 offset = new Vector3(x, y, 0f) * intensity * decay;
            camTransform.localPosition = originalCamLocalPos + offset;

            yield return null;
        }

        camTransform.localPosition = originalCamLocalPos;
        shakeCoroutine = null;
    }

    /// <summary>
    /// Call if camera changes (e.g. Cinemachine blend). Resets the reference position.
    /// </summary>
    public void UpdateCameraReference()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            camTransform = mainCamera.transform;
            originalCamLocalPos = camTransform.localPosition;
        }
    }
    #endregion

    #region VFX Spawning — Delegated to YoruVFXManager

    private void SpawnHitVFX(Vector3 position, bool isHeavy)
    {
        if (vfxManager != null)
            vfxManager.PlayHitSparkVFX(position, isHeavy);
    }
    #endregion

    #region Post-Process Pulse
    /// <summary>
    /// Brief chromatic aberration / vignette pulse. 
    /// Requires a PostProcessVolume on the camera or scene.
    /// Currently logs the call — hook your post-process stack here.
    /// </summary>
    public void PostProcessPulse(float intensity, float duration)
    {
        if (!enablePostProcessPulse) return;

        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseCoroutine(intensity, duration));
    }

    private IEnumerator PulseCoroutine(float intensity, float duration)
    {
        // HOOK POINT: Apply post-process effect here
        // For BRP Post-Processing Stack v2:
        //   chromaticAberration.intensity.value = intensity;
        //   vignette.intensity.value = baseVignette + intensity * 0.3f;
        // 
        // For now, log the pulse so we know the system fires correctly.
        // Hazel: replace this with your PostProcessController reference when ready.
        DebugLog($"PostProcess pulse: intensity={intensity:F2} duration={duration:F3}s");

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float falloff = 1f - t; // Linear decay

            // HOOK: lerp post-process values back to baseline here
            // chromaticAberration.intensity.value = intensity * falloff;

            yield return null;
        }

        // HOOK: Reset post-process values to baseline
        // chromaticAberration.intensity.value = 0f;
        pulseCoroutine = null;
    }
    #endregion

    #region Public Getters / Setters
    /// <summary>
    /// Set shake intensity multiplier from Settings UI (0 = off, 1 = full).
    /// </summary>
    public void SetShakeMultiplier(float multiplier)
    {
        shakeIntensityMultiplier = Mathf.Clamp01(multiplier);
    }

    public float GetShakeMultiplier() => shakeIntensityMultiplier;

    // Expose durations for other systems (parry, guard) to read
    public float GetParryHitStopDuration() => parryHitStopDuration;
    public float GetGuardHitStopDuration() => guardHitStopDuration;
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Feedback] {message}");
    }
    #endregion
}