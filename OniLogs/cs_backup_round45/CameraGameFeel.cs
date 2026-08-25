using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Camera Game Feel — Phase 1 v2
/// 
/// All camera-side combat juice: position shake + FOV punch + sprint/charge FOV.
/// 
/// WHY THIS EXISTS:
/// CinemachineBrain writes Camera.main's position/rotation every LateUpdate.
/// Any direct camera.transform changes get overwritten immediately.
/// This script runs at execution order 200 (AFTER Cinemachine ~100) and applies
/// offsets ADDITIVELY — Cinemachine sets the base, we add on top. No fighting.
/// 
/// SHAKE:
///   Perlin noise position offset. Applied each frame in LateUpdate.
///   Intensity decays linearly over duration. Uses unscaledDeltaTime for Flurry Rush.
///   Accessibility: shakeMultiplier (0 = off, 1 = full) — pass through from Settings.
/// 
/// FOV PUNCH:
///   - Heavy hit:    FOV OUT (+4°) — explosion outward
///   - Light hit:    FOV OUT (+1.5°) — subtle snap
///   - Parry:        FOV IN (-3°) — zoom INTO the moment
///   - Player dmg:   FOV IN (-2° light, -3° heavy) — flinch inward
///   - Sprint:       FOV OUT (+2°) while running — speed feel
///   - Heavy charge: FOV IN (-2°) during hold — tension, snaps back on release
/// 
/// SETUP: Attach to the CinemachineCamera GameObject.
///        References auto-found at Start. No Inspector wiring needed.
/// 
/// DO NOT MODIFY: ThirdPersonCamera.cs, PlayerMovement.cs
/// </summary>
[DefaultExecutionOrder(200)] // After Cinemachine (~100) and ThirdPersonCamera
public class CameraGameFeel : MonoBehaviour
{
    #region Singleton
    public static CameraGameFeel Instance { get; private set; }

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
    [Header("Camera Shake")]
    [Tooltip("Perlin noise frequency — higher = faster shake")]
    [SerializeField] private float shakeFrequency = 25f;
    [Tooltip("Global shake multiplier (0 = off, 1 = full). Hook to Settings slider.")]
    [Range(0f, 1f)]
    [SerializeField] private float shakeMultiplier = 1f;

    [Header("Roll Shake — Close Attack")]
    [Tooltip("Peak roll angle in degrees (the camera rocks ±this about its forward axis, like a steering wheel turning side to side). Modest values read as a punchy impact; large values get seasick.")]
    [SerializeField] private float rollShakeAngle = 5f;
    [Tooltip("Left-right rocks per second. Higher = faster, more sudden wobble.")]
    [SerializeField] private float rollShakeFrequency = 7f;
    [Tooltip("Fraction of the duration (0-1) at which the roll starts fading out. 0.9 = hold full strength until 90% done, then ease to zero over the last 10%.")]
    [Range(0f, 1f)]
    [SerializeField] private float rollShakeFadeStart = 0.9f;

    [Header("FOV Punch — Combat")]
    [Tooltip("FOV degrees added on light hit (combo 1-2)")]
    [SerializeField] private float lightHitPunch = 1.5f;
    [Tooltip("FOV degrees added on heavy hit (combo 3, heavy attack)")]
    [SerializeField] private float heavyHitPunch = 4f;
    [Tooltip("FOV degrees subtracted on parry (zoom in = dramatic)")]
    [SerializeField] private float parryPunch = -3f;
    [Tooltip("FOV degrees on player damage (negative = zoom in)")]
    [SerializeField] private float playerDamageLightPunch = -2f;
    [SerializeField] private float playerDamageHeavyPunch = -3f;
    [Tooltip("Punch decay time in seconds")]
    [SerializeField] private float punchDuration = 0.2f;

    [Header("FOV — Sprint")]
    [Tooltip("Extra FOV while sprinting (positive = wider = faster feel)")]
    [SerializeField] private float sprintFOVOffset = 2f;
    [Tooltip("How fast sprint FOV transitions in/out")]
    [SerializeField] private float sprintFOVSpeed = 6f;

    [Header("FOV — Heavy Charge")]
    [Tooltip("FOV during heavy charge hold (negative = zoom in = tension)")]
    [SerializeField] private float heavyChargeFOV = -2f;
    [Tooltip("How fast charge FOV ramps in")]
    [SerializeField] private float heavyChargeFOVSpeed = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Private Fields
    private Camera mainCamera;
    private Transform camTransform;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    // Shake state (driven per-frame in LateUpdate, not coroutine)
    private float shakeIntensity;
    private float shakeDurationTotal;
    private float shakeElapsed;
    private float shakeSeed;
    private bool shakeActive;

    // Roll-shake state (close-attack "steering wheel" rock about the camera's forward axis)
    private float rollDurationTotal;
    private float rollElapsed;
    private bool rollActive;

    // FOV Punch state (coroutine-driven, single-active)
    private float currentPunchOffset;
    private Coroutine activePunchCoroutine;

    // Sprint state (continuous, lerped)
    private float currentSprintOffset;

    // Heavy charge state (continuous, lerped)
    private float currentChargeOffset;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
            camTransform = mainCamera.transform;

        playerMovement = FindObjectOfType<PlayerMovement>();
        playerCombat = FindObjectOfType<PlayerCombat>();

        if (mainCamera == null)
            Debug.LogWarning("[CameraGameFeel] Camera.main not found!");

        DebugLog("CameraGameFeel initialized (shake + FOV)");
    }

    /// <summary>
    /// Runs AFTER Cinemachine (execution order 200).
    /// Cinemachine has already written position + FOV to Camera.main this frame.
    /// We add our offsets on top. No drift — purely additive each frame.
    /// </summary>
    private void LateUpdate()
    {
        if (mainCamera == null || camTransform == null) return;

        // === POSITION SHAKE ===
        // Cinemachine set the camera's world position this frame.
        // We add Perlin noise offset on top. Next frame Cinemachine resets position,
        // then we add a new offset — no accumulation, no drift.
        // Suppressed while a roll-shake is active so the close attack uses the roll alone.
        if (shakeActive && shakeMultiplier > 0f && !rollActive)
        {
            shakeElapsed += Time.unscaledDeltaTime;

            if (shakeElapsed >= shakeDurationTotal)
            {
                shakeActive = false;
            }
            else
            {
                float t = shakeElapsed / shakeDurationTotal;
                float decay = 1f - t; // Linear falloff

                float x = (Mathf.PerlinNoise(shakeSeed, Time.unscaledTime * shakeFrequency) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(shakeSeed + 100f, Time.unscaledTime * shakeFrequency) - 0.5f) * 2f;

                Vector3 offset = new Vector3(x, y, 0f) * shakeIntensity * shakeMultiplier * decay;

                // Apply in camera's local orientation so shake is screen-relative
                camTransform.position += camTransform.right * offset.x + camTransform.up * offset.y;
            }
        }

        // === ROLL SHAKE ===
        // Rocks the camera left/right about its own forward axis (Z) — the horizon tilts back and
        // forth like a steering wheel. Additive after Cinemachine (which resets rotation each frame),
        // so it never accumulates. Holds full strength until rollShakeFadeStart, then eases to zero.
        if (rollActive && shakeMultiplier > 0f)
        {
            rollElapsed += Time.unscaledDeltaTime;

            if (rollElapsed >= rollDurationTotal)
            {
                rollActive = false;
            }
            else
            {
                float t = rollElapsed / rollDurationTotal;
                float fade = (t < rollShakeFadeStart)
                    ? 1f
                    : 1f - (t - rollShakeFadeStart) / Mathf.Max(0.0001f, 1f - rollShakeFadeStart);
                fade = Mathf.Clamp01(fade);

                float angle = Mathf.Sin(rollElapsed * rollShakeFrequency * 2f * Mathf.PI)
                              * rollShakeAngle * shakeMultiplier * fade;

                camTransform.Rotate(0f, 0f, angle, Space.Self);
            }
        }

        // === FOV OFFSETS ===

        // Sprint FOV (smooth lerp)
        bool isSprinting = playerMovement != null && playerMovement.IsRunning();
        bool inCombatAction = playerCombat != null &&
            (playerCombat.IsAttacking() || playerCombat.IsGuarding() ||
             playerCombat.IsDodging() || playerCombat.IsDashing() ||
             playerCombat.IsChargingHeavy());

        float sprintTarget = (isSprinting && !inCombatAction) ? sprintFOVOffset : 0f;
        currentSprintOffset = Mathf.MoveTowards(
            currentSprintOffset, sprintTarget,
            sprintFOVSpeed * Time.unscaledDeltaTime);

        // Heavy charge FOV (smooth lerp)
        bool isCharging = playerCombat != null && playerCombat.IsChargingHeavy();
        float chargeTarget = isCharging ? heavyChargeFOV : 0f;
        currentChargeOffset = Mathf.MoveTowards(
            currentChargeOffset, chargeTarget,
            heavyChargeFOVSpeed * Time.unscaledDeltaTime);

        // Apply total FOV offset
        float totalFOVOffset = currentPunchOffset + currentSprintOffset + currentChargeOffset;
        if (Mathf.Abs(totalFOVOffset) > 0.01f)
            mainCamera.fieldOfView += totalFOVOffset;
    }
    #endregion

    #region Public API — Camera Shake

    /// <summary>
    /// Trigger Perlin noise camera shake. Applied additively after Cinemachine each frame.
    /// Replaces CombatFeedbackManager's old direct-write shake that Cinemachine was overriding.
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        if (shakeMultiplier <= 0f || intensity < 0.01f) return;

        // New shake overrides any in-progress shake (same behavior as old system)
        shakeIntensity = intensity;
        shakeDurationTotal = duration;
        shakeElapsed = 0f;
        shakeSeed = Time.unscaledTime * 100f;
        shakeActive = true;

        DebugLog($"Shake: intensity={intensity:F2} duration={duration:F3}s");
    }

    /// <summary>
    /// Trigger the roll-shake: the camera rocks left/right about its forward axis (steering-wheel
    /// tilt) for the given duration, using the serialized angle/frequency/fade. Cancels any active
    /// position shake so the effect stands alone (used for the enemy close attack). Angle and speed
    /// are tuned via the Roll Shake fields above.
    /// </summary>
    public void RollShake(float duration)
    {
        if (shakeMultiplier <= 0f || rollShakeAngle < 0.01f || duration <= 0f) return;

        shakeActive = false; // close attack rocks instead of position-shaking
        rollDurationTotal = duration;
        rollElapsed = 0f;
        rollActive = true;

        DebugLog($"RollShake: angle={rollShakeAngle:F1}° freq={rollShakeFrequency:F1}/s duration={duration:F3}s");
    }

    /// <summary>
    /// Set shake intensity multiplier from Settings UI (0 = off, 1 = full).
    /// </summary>
    public void SetShakeMultiplier(float multiplier)
    {
        shakeMultiplier = Mathf.Clamp01(multiplier);
    }

    public float GetShakeMultiplier() => shakeMultiplier;
    #endregion

    #region Public API — FOV Punch

    /// <summary>
    /// FOV punch outward on hit. Positive = wider = explosive feel.
    /// </summary>
    public void PunchHit(bool isHeavy)
    {
        float amount = isHeavy ? heavyHitPunch : lightHitPunch;
        StartPunch(amount, punchDuration);
        DebugLog($"FOV punch: hit {(isHeavy ? "heavy" : "light")} ({amount:+0.0;-0.0}°)");
    }

    /// <summary>
    /// FOV punch inward on parry. Negative = tighter = zoom into the moment.
    /// </summary>
    public void PunchParry()
    {
        StartPunch(parryPunch, punchDuration * 1.5f);
        DebugLog($"FOV punch: parry ({parryPunch:+0.0;-0.0}°)");
    }

    /// <summary>
    /// FOV punch inward on player damage. Negative = flinch.
    /// </summary>
    public void PunchPlayerDamage(bool isHeavy)
    {
        float amount = isHeavy ? playerDamageHeavyPunch : playerDamageLightPunch;
        StartPunch(amount, punchDuration);
        DebugLog($"FOV punch: player damage ({amount:+0.0;-0.0}°)");
    }
    #endregion

    #region Punch Coroutine (single-active)
    private void StartPunch(float amount, float duration)
    {
        if (activePunchCoroutine != null)
            StopCoroutine(activePunchCoroutine);
        activePunchCoroutine = StartCoroutine(PunchCoroutine(amount, duration));
    }

    private IEnumerator PunchCoroutine(float amount, float duration)
    {
        currentPunchOffset = amount;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentPunchOffset = amount * (1f - t * t);
            yield return null;
        }

        currentPunchOffset = 0f;
        activePunchCoroutine = null;
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[CameraGameFeel] {message}");
    }
    #endregion
}