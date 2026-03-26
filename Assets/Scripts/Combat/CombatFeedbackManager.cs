using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat Feedback — Phase 3B → Game Feel v3
/// Singleton manager for all combat "juice": hitstop, camera shake, VFX at contact, post-process pulse, FOV punch.
/// All timing uses WaitForSecondsRealtime / unscaledTime so it works during Flurry Rush (timeScale 0.3).
/// 
/// HITSTOP: Animator.speed = 0, NOT Time.timeScale = 0 (kills VFX, camera, audio).
/// CAMERA SHAKE: Perlin noise, not Random.insideUnitSphere (jittery). Accessibility slider ready.
/// VFX: Spawned at actual contact point, not attackPoint.
/// SFX: Delegated to CombatSFXManager (separate script).
/// POST-PROCESS: Delegated to CombatPostProcessPulse (separate volume, priority 100).
/// FOV PUNCH: Delegated to CameraGameFeel (additive, runs after Cinemachine).
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
    private YoruVFXManager vfxManager;

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
        vfxManager = FindObjectOfType<YoruVFXManager>();
        DebugLog("CombatFeedbackManager initialized (v3 — shake/pulse delegated)");
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

        // Game Feel v3: FOV punch on every hit (light = subtle, heavy = dramatic)
        if (CameraGameFeel.Instance != null)
            CameraGameFeel.Instance.PunchHit(isHeavy);

        // Keep combat music alive during active fighting
        if (CombatMusicManager.Instance != null)
            CombatMusicManager.Instance.NotifyCombatActivity();

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

        // Game Feel v3: FOV zoom in on parry (dramatic focus)
        if (CameraGameFeel.Instance != null)
            CameraGameFeel.Instance.PunchParry();

        // Keep combat music alive during active fighting
        if (CombatMusicManager.Instance != null)
            CombatMusicManager.Instance.NotifyCombatActivity();

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

        // Keep combat music alive during active fighting
        if (CombatMusicManager.Instance != null)
            CombatMusicManager.Instance.NotifyCombatActivity();

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

        // Game Feel v3: damage-specific post-process (red vignette) + FOV flinch
        // Replaces the old PostProcessPulse call — PulseDamage uses red vignette color
        if (enablePostProcessPulse && CombatPostProcessPulse.Instance != null)
            CombatPostProcessPulse.Instance.PulseDamage(isHeavy);
        if (CameraGameFeel.Instance != null)
            CameraGameFeel.Instance.PunchPlayerDamage(isHeavy);

        // Keep combat music alive during active fighting
        if (CombatMusicManager.Instance != null)
            CombatMusicManager.Instance.NotifyCombatActivity();

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

    #region Camera Shake — Delegated to CameraGameFeel

    /// <summary>
    /// Camera shake — delegated to CameraGameFeel which applies offsets AFTER Cinemachine.
    /// The old implementation wrote to camTransform.localPosition directly, but Cinemachine
    /// overwrote it every LateUpdate, so shake was never visible.
    /// </summary>
    public void CameraShake(float intensity, float duration)
    {
        if (CameraGameFeel.Instance != null)
        {
            CameraGameFeel.Instance.Shake(intensity, duration);
        }
        else
        {
            DebugLog("WARNING: CameraGameFeel not found — shake skipped");
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

    #region Post-Process Pulse — Delegated to CombatPostProcessPulse
    /// <summary>
    /// Brief chromatic aberration / vignette pulse on heavy hits and parry.
    /// Game Feel v3: delegates to CombatPostProcessPulse (separate volume, priority 100).
    /// The intensity/duration params are kept for backward compatibility but the actual
    /// values are tuned on CombatPostProcessPulse's Inspector.
    /// </summary>
    public void PostProcessPulse(float intensity, float duration)
    {
        if (!enablePostProcessPulse) return;

        // Delegate to the dedicated pulse volume
        if (CombatPostProcessPulse.Instance != null)
        {
            // Determine pulse type from context:
            // Called from PlayParryFeedback → intensity > pulseIntensity → parry
            // Called from PlayHitFeedback → intensity == pulseIntensity → hit
            // Called from PlayPlayerHitFeedback → intensity < pulseIntensity → damage (handled separately)
            if (intensity > pulseIntensity)
                CombatPostProcessPulse.Instance.PulseParry();
            else
                CombatPostProcessPulse.Instance.PulseHit();
        }

        DebugLog($"PostProcess pulse delegated (intensity={intensity:F2})");
    }
    #endregion

    #region Public Getters / Setters
    /// <summary>
    /// Set shake intensity multiplier from Settings UI (0 = off, 1 = full).
    /// Forwards to CameraGameFeel where the actual shake lives.
    /// </summary>
    public void SetShakeMultiplier(float multiplier)
    {
        shakeIntensityMultiplier = Mathf.Clamp01(multiplier);
        if (CameraGameFeel.Instance != null)
            CameraGameFeel.Instance.SetShakeMultiplier(multiplier);
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