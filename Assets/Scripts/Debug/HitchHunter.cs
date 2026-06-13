using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// YORU Hitch Hunter — June 2026
///
/// Purpose: settle the pickup freeze / camera shake bug with NUMBERS instead of theories.
/// It watches every frame and tells apart the two possible causes:
///
///   [HITCH]       = the FRAME took too long (a real freeze / performance spike).
///                   Logged with: how long, GC garbage collected or not, whether a
///                   one-shot sound was just played, and how long ago the last
///                   inventory pickup was.
///
///   [CAMERA-JUMP] = the camera MOVED a large distance in a frame that was NORMAL
///                   speed. That means camera logic moved it, not a freeze.
///
/// If the console fills with [HITCH] lines after pickup  -> performance problem.
/// If it fills with [CAMERA-JUMP] lines                  -> camera logic problem.
/// If both                                               -> two problems stacked.
///
/// Setup: empty GameObject in the scene, add this component. Nothing to wire.
/// Press F9 during play for a summary of everything recorded so far.
/// </summary>
public class HitchHunter : MonoBehaviour
{
    #region Inspector
    [Header("Thresholds")]
    [Tooltip("A frame longer than this many milliseconds is logged as a HITCH (a freeze). 60fps frame = 16ms, so 40ms = a clearly felt stutter")]
    [SerializeField] private float hitchThresholdMs = 40f;
    [Tooltip("If the camera moves more than this many meters in ONE normal-speed frame, it is logged as a CAMERA-JUMP. Raise if sprinting causes false alarms")]
    [SerializeField] private float cameraJumpMeters = 0.6f;

    [Header("Keys")]
    [Tooltip("Press during play to print a summary of everything recorded")]
    [SerializeField] private KeyCode summaryKey = KeyCode.F9;
    #endregion

    #region State
    private Transform cam;
    private Vector3 lastCamPos;
    private bool hasLastCamPos;

    private int gcCountLast;
    private long monoHeapLast;

    private float lastInventoryEventTime = -999f;
    private float lastHitchTime = -999f;

    private int hitchCount;
    private int cameraJumpCount;
    private float worstFrameMs;
    private float worstCamJump;
    private float playStartTime;

    private readonly List<string> eventLog = new List<string>(256);
    #endregion

    #region Lifecycle
    private void Start()
    {
        playStartTime = Time.realtimeSinceStartup;

        Camera main = Camera.main;
        if (main != null) cam = main.transform;

        gcCountLast = System.GC.CollectionCount(0);
        monoHeapLast = Profiler.GetMonoUsedSizeLong();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += OnInventoryChanged;

        Debug.Log($"[HitchHunter] Armed. Hitch threshold: {hitchThresholdMs}ms. Camera jump threshold: {cameraJumpMeters}m. Press {summaryKey} for summary.");
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged()
    {
        lastInventoryEventTime = Time.realtimeSinceStartup;
        string msg = $"[HitchHunter] ======== INVENTORY EVENT (pickup/drop/use) at t={SessionTime():F2}s ========";
        Debug.Log(msg);
        eventLog.Add(msg);
    }
    #endregion

    #region Per-frame watch
    private void LateUpdate() // LateUpdate so the camera has already moved this frame
    {
        float frameMs = Time.unscaledDeltaTime * 1000f;
        if (frameMs > worstFrameMs) worstFrameMs = frameMs;

        // ---- HITCH detection (the freeze) ----
        if (frameMs > hitchThresholdMs)
        {
            hitchCount++;
            lastHitchTime = Time.realtimeSinceStartup;

            int gcNow = System.GC.CollectionCount(0);
            long heapNow = Profiler.GetMonoUsedSizeLong();
            bool gcRan = gcNow != gcCountLast;
            float heapDeltaMb = (heapNow - monoHeapLast) / (1024f * 1024f);

            // PlayClipAtPoint creates an object literally named "One shot audio".
            // If one exists right now, a sound was played within the last few seconds.
            bool oneShotAudioAlive = GameObject.Find("One shot audio") != null;

            float sincePickup = Time.realtimeSinceStartup - lastInventoryEventTime;
            string pickupNote = sincePickup < 900f ? $"{sincePickup:F2}s after last inventory event" : "no inventory event yet";

            string msg = $"[HITCH] #{hitchCount}: frame took {frameMs:F0}ms at t={SessionTime():F2}s | " +
                         $"GC ran: {(gcRan ? "YES" : "no")} (heap change {heapDeltaMb:+0.0;-0.0}MB) | " +
                         $"sound just played: {(oneShotAudioAlive ? "YES" : "no")} | {pickupNote}";
            Debug.LogWarning(msg);
            eventLog.Add(msg);
        }

        gcCountLast = System.GC.CollectionCount(0);
        monoHeapLast = Profiler.GetMonoUsedSizeLong();

        // ---- CAMERA-JUMP detection (the shake) ----
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            Vector3 pos = cam.position;
            if (hasLastCamPos)
            {
                float jump = (pos - lastCamPos).magnitude;
                if (jump > worstCamJump) worstCamJump = jump;

                bool normalSpeedFrame = frameMs < hitchThresholdMs;
                bool playerRotatingCamera = Input.GetMouseButton(1); // right mouse held = manual rotation, big moves are legit

                if (normalSpeedFrame && !playerRotatingCamera && jump > cameraJumpMeters)
                {
                    cameraJumpCount++;
                    string msg = $"[CAMERA-JUMP] #{cameraJumpCount}: camera moved {jump:F2}m in ONE normal {frameMs:F0}ms frame at t={SessionTime():F2}s " +
                                 $"(from {lastCamPos} to {pos}). This is camera LOGIC moving it, not a freeze.";
                    Debug.LogWarning(msg);
                    eventLog.Add(msg);
                }
            }
            lastCamPos = pos;
            hasLastCamPos = true;
        }

        // ---- Summary on demand ----
        if (Input.GetKeyDown(summaryKey))
            PrintSummary();
    }
    #endregion

    #region Summary
    private float SessionTime() => Time.realtimeSinceStartup - playStartTime;

    private void PrintSummary()
    {
        Debug.Log("[HitchHunter] ================= SUMMARY =================");
        Debug.Log($"[HitchHunter] Session length: {SessionTime():F0}s");
        Debug.Log($"[HitchHunter] HITCHES (frozen frames): {hitchCount}, worst frame: {worstFrameMs:F0}ms");
        Debug.Log($"[HitchHunter] CAMERA-JUMPS (camera logic): {cameraJumpCount}, worst jump: {worstCamJump:F2}m");
        if (hitchCount > 0 && cameraJumpCount == 0)
            Debug.Log("[HitchHunter] VERDICT SO FAR: this is a PERFORMANCE problem (freezes). The shake is the camera catching up after frozen frames.");
        else if (cameraJumpCount > 0 && hitchCount == 0)
            Debug.Log("[HitchHunter] VERDICT SO FAR: this is a CAMERA LOGIC problem. Frames are smooth, the camera itself is teleporting.");
        else if (hitchCount > 0 && cameraJumpCount > 0)
            Debug.Log("[HitchHunter] VERDICT SO FAR: BOTH things are happening. Two stacked problems.");
        else
            Debug.Log("[HitchHunter] VERDICT SO FAR: nothing abnormal recorded yet.");
        Debug.Log("[HitchHunter] Full event list below:");
        foreach (string line in eventLog)
            Debug.Log("[HitchHunter]   " + line);
        Debug.Log("[HitchHunter] ===========================================");
    }
    #endregion
}
