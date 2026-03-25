using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Camera Game Feel — Phase 1
/// 
/// FOV punch system: brief FOV shifts on combat events to add impact.
///   - Heavy hit:    FOV OUT (+4°) — explosion outward
///   - Light hit:    FOV OUT (+1.5°) — subtle snap
///   - Parry:        FOV IN (-3°) — zoom INTO the moment
///   - Player dmg:   FOV IN (-2° light, -3° heavy) — flinch inward
///   - Sprint:       FOV OUT (+2°) while running — speed feel
///   - Heavy charge: FOV IN (-2°) during hold — tension, snaps back on release
/// 
/// Runs at execution order 200 so it applies AFTER Cinemachine writes to Camera.main.
/// Each frame: Cinemachine sets base FOV → this script adds punch + sprint offsets.
/// Purely additive — no drift, no state corruption.
/// 
/// All timing uses unscaledDeltaTime — works during Flurry Rush (timeScale 0.3).
/// 
/// SETUP: Attach to the CinemachineCamera GameObject.
///        References auto-found at Start. No Inspector wiring needed.
/// 
/// DO NOT MODIFY: ThirdPersonCamera.cs (this is a separate addon).
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
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    // Punch state (coroutine-driven, single-active)
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

        // Find player scripts — they live on the same GameObject (PlayerYoru_Def)
        playerMovement = FindObjectOfType<PlayerMovement>();
        playerCombat = FindObjectOfType<PlayerCombat>();

        if (mainCamera == null)
            Debug.LogWarning("[CameraGameFeel] Camera.main not found!");

        DebugLog("CameraGameFeel initialized");
    }

    /// <summary>
    /// Runs AFTER Cinemachine (execution order 200).
    /// Cinemachine has already written its FOV to Camera.main this frame.
    /// We read it, add our offsets, write it back. No drift — purely additive each frame.
    /// </summary>
    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // --- Sprint FOV (smooth lerp) ---
        bool isSprinting = playerMovement != null && playerMovement.IsRunning();
        // Don't apply sprint FOV during combat actions
        bool inCombatAction = playerCombat != null &&
            (playerCombat.IsAttacking() || playerCombat.IsGuarding() ||
             playerCombat.IsDodging() || playerCombat.IsDashing() ||
             playerCombat.IsChargingHeavy());

        float sprintTarget = (isSprinting && !inCombatAction) ? sprintFOVOffset : 0f;
        currentSprintOffset = Mathf.MoveTowards(
            currentSprintOffset, sprintTarget,
            sprintFOVSpeed * Time.unscaledDeltaTime);

        // --- Heavy charge FOV (smooth lerp) ---
        bool isCharging = playerCombat != null && playerCombat.IsChargingHeavy();
        float chargeTarget = isCharging ? heavyChargeFOV : 0f;
        currentChargeOffset = Mathf.MoveTowards(
            currentChargeOffset, chargeTarget,
            heavyChargeFOVSpeed * Time.unscaledDeltaTime);

        // --- Apply total offset ---
        float totalOffset = currentPunchOffset + currentSprintOffset + currentChargeOffset;
        if (Mathf.Abs(totalOffset) > 0.01f)
            mainCamera.fieldOfView += totalOffset;
    }
    #endregion

    #region Public API — Called by CombatFeedbackManager

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
        StartPunch(parryPunch, punchDuration * 1.5f); // Parry holds slightly longer
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
            // Ease-out: snappy onset, smooth return
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
