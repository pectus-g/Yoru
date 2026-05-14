using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;

/// <summary>
/// YORU Freeze Diagnostic — v3 — May 10 2026
/// 
/// Flight-recorder style. Continuously samples state at fixed intervals into a
/// rolling buffer. When any trigger fires (auto or manual), prints the entire
/// buffer so we see the trajectory leading INTO the freeze, not just one frame.
/// 
/// Auto-triggers (each names the condition that caught it):
///   STUCK-FLAG    : any combat flag on for > flagStuckThreshold
///   WASD-NOMOVE   : WASD pressed + horizontal speed near zero for > wasdNoMovementThreshold
///   ANIM-PAUSED   : animator.speed == 0 for > animatorPausedThreshold
///   CC-DISABLED   : CharacterController.enabled == false for > controllerDisabledThreshold
///   CLIP-OVERRUN  : combat-layer normTime > 1.2 while isAttacking still true
/// 
/// Drop-in replacement for v1/v2. Auto-finds references. Tab for manual dump.
/// </summary>
public class FreezeDebugDumper : MonoBehaviour
{
    #region Inspector
    [Header("References (auto-found in Start if null)")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private int combatLayerIndex = 1;

    [Header("Manual Trigger")]
    [SerializeField] private KeyCode manualDumpKey = KeyCode.Tab;

    [Header("Flight Recorder")]
    [Tooltip("How often to snapshot state into the rolling buffer (seconds).")]
    [SerializeField] private float snapshotInterval = 0.1f;
    [Tooltip("How many snapshots to keep. At 10Hz default, 60 = 6 seconds of history.")]
    [SerializeField] private int snapshotBufferSize = 60;

    [Header("Auto-Trigger Thresholds (seconds)")]
    [Tooltip("STUCK-FLAG: any single combat flag stays on this long.")]
    [SerializeField] private float flagStuckThreshold = 2.5f;
    [Tooltip("WASD-NOMOVE: WASD recent + speed below stationary threshold for this long. Most reliable real-freeze indicator.")]
    [SerializeField] private float wasdNoMovementThreshold = 0.4f;
    [Tooltip("WASD-NOMOVE: rolling window for considering WASD recently pressed.")]
    [SerializeField] private float wasdRecentWindow = 0.5f;
    [Tooltip("ANIM-PAUSED: animator.speed == 0 for this long (legit hitstop tops at ~0.12s).")]
    [SerializeField] private float animatorPausedThreshold = 0.3f;
    [Tooltip("CC-DISABLED: CharacterController.enabled == false for this long. Smoking gun for total immobility.")]
    [SerializeField] private float controllerDisabledThreshold = 0.15f;
    [Tooltip("CLIP-OVERRUN: combat-layer normTime > 1.2 while isAttacking still true.")]
    [SerializeField] private float clipOverrunThreshold = 0.3f;
    [Tooltip("Horizontal speed below this counts as 'stationary' (m/s).")]
    [SerializeField] private float stationarySpeedThreshold = 0.4f;
    [Tooltip("Minimum gap between auto-dumps (seconds). Prevents log spam.")]
    [SerializeField] private float autoCooldown = 4.0f;

    [Header("Behavior")]
    [SerializeField] private bool autoTriggersEnabled = true;
    [Tooltip("Also append every dump to a file in persistentDataPath.")]
    [SerializeField] private bool writeToFile = false;
    [SerializeField] private string logFileName = "yoru_freeze_log.txt";
    #endregion

    #region Snapshot data
    private struct Snap
    {
        public float time;
        public bool atk, hvy, dod, dsh, grd, hit, aerial, locked;
        public int comboStep;
        public bool grounded, ccEnabled;
        public float animSpeed;
        public Vector3 position;
        public Vector3 velocity;
        public float combatNormTime;
        public int combatHash;
        public bool combatInTransition;
        public float combatWeight;
        public float h, v;
        public bool lmb, mmb, q, space, shift, c;
    }

    private readonly Queue<Snap> ring = new Queue<Snap>(80);
    private float lastSnapshotTime = -100f;
    private float lastAutoDumpTime = -100f;

    private readonly float[] flagStuckTimers = new float[8];
    private static readonly string[] flagLabels = { "atk", "hvy", "dod", "dsh", "grd", "hit", "aerial", "lockPos" };
    private float wasdNoMoveTimer;
    private float animPausedTimer;
    private float ccDisabledTimer;
    private float clipOverrunTimer;
    private float lastWasdTime = -100f;
    #endregion

    #region Lifecycle
    private void Start()
    {
        if (playerCombat == null) playerCombat = FindObjectOfType<PlayerCombat>();
        if (playerCombat != null)
        {
            if (playerMovement == null) playerMovement = playerCombat.GetComponent<PlayerMovement>();
            if (characterController == null) characterController = playerCombat.GetComponent<CharacterController>();
            if (animator == null) animator = playerCombat.GetComponentInChildren<Animator>();
        }

        if (playerCombat == null || characterController == null || animator == null)
        {
            Debug.LogError($"[FreezeDebug v3] Missing refs: pc={playerCombat} cc={characterController} an={animator}. Disabling.", this);
            enabled = false;
            return;
        }

        Debug.Log($"[FreezeDebug v3] Armed. Manual={manualDumpKey}. Auto={autoTriggersEnabled}. " +
                  $"Buffer={snapshotBufferSize} snaps @ {snapshotInterval}s = {snapshotBufferSize * snapshotInterval:F1}s history.");
    }

    private void Update()
    {
        bool wasd = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                 || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        if (wasd) lastWasdTime = Time.time;

        if (Time.time - lastSnapshotTime >= snapshotInterval)
        {
            ring.Enqueue(TakeSnap());
            while (ring.Count > snapshotBufferSize) ring.Dequeue();
            lastSnapshotTime = Time.time;
        }

        if (Input.GetKeyDown(manualDumpKey))
            Dump("MANUAL (" + manualDumpKey + ")", isAuto: false);

        if (autoTriggersEnabled)
            RunAutoTriggers();
    }
    #endregion

    #region Snapshot
    private Snap TakeSnap()
    {
        Snap s = new Snap
        {
            time = Time.time,
            atk = playerCombat.IsAttacking(),
            hvy = playerCombat.IsChargingHeavy(),
            dod = playerCombat.IsDodging(),
            dsh = playerCombat.IsDashing(),
            grd = playerCombat.IsGuarding(),
            hit = playerCombat.IsInHitReaction(),
            aerial = playerCombat.IsAerialAttack(),
            locked = playerCombat.IsPositionLocked(),
            comboStep = playerCombat.GetCurrentComboStep(),
            grounded = characterController.isGrounded,
            ccEnabled = characterController.enabled,
            animSpeed = playerCombat.GetAnimatorSpeed(),
            position = characterController.transform.position,
            velocity = characterController.velocity,
            h = Input.GetAxisRaw("Horizontal"),
            v = Input.GetAxisRaw("Vertical"),
            lmb = Input.GetMouseButton(0),
            mmb = Input.GetMouseButton(2),
            q = Input.GetKey(KeyCode.Q),
            space = Input.GetKey(KeyCode.Space),
            shift = Input.GetKey(KeyCode.LeftShift),
            c = Input.GetKey(KeyCode.C)
        };

        if (animator != null && animator.layerCount > combatLayerIndex)
        {
            var info = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            s.combatNormTime = info.normalizedTime;
            s.combatHash = info.fullPathHash;
            s.combatInTransition = animator.IsInTransition(combatLayerIndex);
            s.combatWeight = animator.GetLayerWeight(combatLayerIndex);
        }
        return s;
    }
    #endregion

    #region Auto-Triggers
    private void RunAutoTriggers()
    {
        float dt = Time.deltaTime;
        Snap s = TakeSnap();
        float hspeed = new Vector2(s.velocity.x, s.velocity.z).magnitude;

        bool[] flags = { s.atk, s.hvy, s.dod, s.dsh, s.grd, s.hit, s.aerial, s.locked };
        for (int i = 0; i < 8; i++)
        {
            if (flags[i]) flagStuckTimers[i] += dt;
            else flagStuckTimers[i] = 0f;
            if (flagStuckTimers[i] > flagStuckThreshold)
            {
                Dump($"STUCK-FLAG: {flagLabels[i]} on for {flagStuckTimers[i]:F2}s", isAuto: true);
                flagStuckTimers[i] = 0f;
            }
        }

        bool wasdRecent = (Time.time - lastWasdTime) < wasdRecentWindow;
        if (wasdRecent && hspeed < stationarySpeedThreshold) wasdNoMoveTimer += dt;
        else wasdNoMoveTimer = 0f;
        if (wasdNoMoveTimer > wasdNoMovementThreshold)
        {
            Dump($"WASD-NOMOVE: WASD recent + speed {hspeed:F2}m/s for {wasdNoMoveTimer:F2}s", isAuto: true);
            wasdNoMoveTimer = 0f;
        }

        if (s.animSpeed == 0f) animPausedTimer += dt; else animPausedTimer = 0f;
        if (animPausedTimer > animatorPausedThreshold)
        {
            Dump($"ANIM-PAUSED: animator.speed=0 for {animPausedTimer:F2}s", isAuto: true);
            animPausedTimer = 0f;
        }

        if (!s.ccEnabled) ccDisabledTimer += dt; else ccDisabledTimer = 0f;
        if (ccDisabledTimer > controllerDisabledThreshold)
        {
            Dump($"CC-DISABLED: CharacterController.enabled=false for {ccDisabledTimer:F2}s", isAuto: true);
            ccDisabledTimer = 0f;
        }

        if (s.atk && s.combatNormTime > 1.2f) clipOverrunTimer += dt;
        else clipOverrunTimer = 0f;
        if (clipOverrunTimer > clipOverrunThreshold)
        {
            Dump($"CLIP-OVERRUN: normTime={s.combatNormTime:F2} + atk for {clipOverrunTimer:F2}s", isAuto: true);
            clipOverrunTimer = 0f;
        }
    }
    #endregion

    #region Dump
    private void Dump(string reason, bool isAuto)
    {
        if (isAuto && Time.time - lastAutoDumpTime < autoCooldown) return;
        if (isAuto) lastAutoDumpTime = Time.time;

        Snap now = TakeSnap();
        StringBuilder sb = new StringBuilder(8192);

        sb.AppendLine();
        sb.AppendLine("==================== [FREEZE-DEBUG v3] ====================");
        sb.AppendLine($"  Reason : {reason}");
        sb.AppendLine($"  Time   : {Time.time:F3}s   Frame: {Time.frameCount}   Snaps: {ring.Count}");
        sb.AppendLine("===========================================================");

        sb.AppendLine();
        sb.AppendLine("--- CURRENT STATE ---");
        sb.AppendLine($"  Flags: atk={now.atk} hvy={now.hvy} dod={now.dod} dsh={now.dsh}");
        sb.AppendLine($"         grd={now.grd} hit={now.hit} aerial={now.aerial} lockPos={now.locked}");
        sb.AppendLine($"         comboStep={now.comboStep}");
        sb.AppendLine($"  Anim : speed={now.animSpeed:F2}  combatLayer hash={now.combatHash} normT={now.combatNormTime:F3} " +
                      $"weight={now.combatWeight:F2} inTransition={now.combatInTransition}");
        sb.AppendLine($"  CC   : grounded={now.grounded}  enabled={now.ccEnabled}  pos={Vec(now.position)}  vel={Vec(now.velocity)}");
        sb.AppendLine($"  Move : IsRunning={(playerMovement != null ? playerMovement.IsRunning().ToString() : "n/a")}");
        sb.AppendLine($"  Input: H={now.h:F2} V={now.v:F2}  LMB={now.lmb} MMB={now.mmb} Q={now.q} Sp={now.space} Sh={now.shift} C={now.c}");
        sb.AppendLine($"         WASD pressed in last {wasdRecentWindow:F1}s: {(Time.time - lastWasdTime) < wasdRecentWindow} " +
                      $"(last {Time.time - lastWasdTime:F2}s ago)");

        sb.AppendLine();
        sb.AppendLine($"--- FLIGHT RECORDER ({ring.Count} snapshots, oldest first, dt={snapshotInterval:F2}s) ---");
        sb.AppendLine("  Legend: flags = A(tk) H(vy) D(od) S(sh) G(rd) R(eact) Y(aerial) L(ock)   gnd/cc: Y/n");
        sb.AppendLine("  t-now    flags     step gnd cc anSp normT layW tr | posX     posZ     spd   | H    V    LMB MMB Q  Sp Sh C");
        sb.AppendLine("  ------   --------  ---- --- -- ---- ----- ---- -- | -----    -----    ----  | ---- ---- --- --- -- -- -- -");

        if (ring.Count == 0) sb.AppendLine("  (empty)");
        else
        {
            float now_t = Time.time;
            foreach (var snap in ring)
                sb.AppendLine(FormatRow(snap, now_t));
        }

        sb.AppendLine("=========================================================== END");

        string text = sb.ToString();
        Debug.Log(text, this);

        if (writeToFile)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, logFileName);
                File.AppendAllText(path, text + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FreezeDebug] File write failed: {e.Message}");
            }
        }
    }

    private string FormatRow(Snap s, float now_t)
    {
        string flags = (s.atk ? "A" : "-") + (s.hvy ? "H" : "-") + (s.dod ? "D" : "-") + (s.dsh ? "S" : "-")
                     + (s.grd ? "G" : "-") + (s.hit ? "R" : "-") + (s.aerial ? "Y" : "-") + (s.locked ? "L" : "-");
        float hspd = new Vector2(s.velocity.x, s.velocity.z).magnitude;
        string btns = (s.lmb ? "1" : "0") + "   " + (s.mmb ? "1" : "0") + "   " + (s.q ? "1" : "0") + "  "
                    + (s.space ? "1" : "0") + "  " + (s.shift ? "1" : "0") + "  " + (s.c ? "1" : "0");
        return string.Format("  -{0,5:F2}s  {1}  {2,3}  {3}   {4}  {5,4:F2} {6,5:F2} {7,4:F2} {8}  | {9,7:F1}  {10,7:F1}  {11,4:F2} | {12,4:F1} {13,4:F1} {14}",
            now_t - s.time,
            flags,
            s.comboStep,
            s.grounded ? "Y" : "n",
            s.ccEnabled ? "Y" : "n",
            s.animSpeed,
            s.combatNormTime,
            s.combatWeight,
            s.combatInTransition ? "T" : "-",
            s.position.x,
            s.position.z,
            hspd,
            s.h, s.v,
            btns);
    }

    private string Vec(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    #endregion
}