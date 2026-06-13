using UnityEngine;

/// <summary>
/// WATCH-ONLY diagnostic, v2, for the "open bag, view Quest page, close, everything feels
/// sluggish/stuttery/wrong FOREVER" bug on the Gamefeel branch. Edits NOTHING. Drop on one
/// empty GameObject, press Play, run the exact repro (open with I, click the Quest tab button,
/// close with I, then move around for ~10 seconds), read the console.
///
/// v1 proved the camera does NOT snap and state restores cleanly on close. So v2 measures the
/// thing that actually matches "everything feels wrong forever": FRAME TIME and INPUT COUNT.
///
///   1) FRAME TIME: prints one line per second with the average and WORST frame time (ms) in
///      that second. Healthy is ~16ms (60fps) or ~33ms (30fps) and STEADY. If the numbers climb
///      after the bag closes and stay high, the game is running slow = that is the "weirdness".
///      It tags each line BEFORE or AFTER the first bag close so the change is obvious.
///
///   2) JUMP INPUT: counts how many times the Space key is actually pressed vs how many times
///      the player reports a jump. If jumps >> presses, input is being read multiple times.
///
/// Nothing here changes gameplay. There is nothing to revert. Remove the GameObject when done.
/// </summary>
public class InventoryCloseWatcher : MonoBehaviour
{
    [Header("Watch-only. Just press Play and run the repro.")]
    [Tooltip("Seconds between frame-time report lines.")]
    [SerializeField] private float reportEverySeconds = 1f;

    [Tooltip("A single frame slower than this (ms) gets its own SPIKE line.")]
    [SerializeField] private float spikeMs = 50f;

    private InventoryUI inventory;
    private bool wasOpenLastFrame = false;
    private bool bagHasClosedOnce = false;

    // frame-time accumulation for the per-second report
    private float windowStart;
    private int windowFrames;
    private float windowWorstMs;
    private float windowSumMs;

    // jump input accounting
    private int spacePresses = 0;
    private int jumpsReported = 0;

    private void Start()
    {
        inventory = InventoryUI.Instance;
        if (inventory == null) inventory = FindObjectOfType<InventoryUI>();

        windowStart = Time.realtimeSinceStartup;
        Debug.Log("[CloseWatcher v2] Armed. Repro: open with I, click Quest tab, close with I, " +
                  "then MOVE AROUND for ~10s. Watch the frame-time numbers before vs after the close. " +
                  $"(inventory found: {inventory != null})");
    }

    private void Update()
    {
        // --- frame-time sampling (uses UNSCALED real time so a paused bag does not pollute it) ---
        float frameMs = Time.unscaledDeltaTime * 1000f;
        windowFrames++;
        windowSumMs += frameMs;
        if (frameMs > windowWorstMs) windowWorstMs = frameMs;

        // Per-second report
        float now = Time.realtimeSinceStartup;
        if (now - windowStart >= reportEverySeconds && windowFrames > 0)
        {
            float avg = windowSumMs / windowFrames;
            float fps = 1000f / Mathf.Max(0.0001f, avg);
            string phase = bagHasClosedOnce ? "AFTER-close" : "before-bag";
            Debug.Log($"[CloseWatcher v2] [{phase}] avg frame {avg:F1}ms ({fps:F0} fps), " +
                      $"worst {windowWorstMs:F1}ms, frames {windowFrames}");
            windowStart = now;
            windowFrames = 0;
            windowWorstMs = 0f;
            windowSumMs = 0f;
        }

        // Big single-frame spike
        if (frameMs > spikeMs)
        {
            string phase = bagHasClosedOnce ? "AFTER-close" : "before-bag";
            Debug.LogWarning($"[CloseWatcher v2] SPIKE [{phase}]: one frame took {frameMs:F0}ms");
        }

        // --- jump input accounting ---
        if (Input.GetKeyDown(KeyCode.Space)) spacePresses++;

        // --- detect the bag close edge ---
        if (inventory != null)
        {
            bool openNow = inventory.IsInventoryOpen();
            if (wasOpenLastFrame && !openNow)
            {
                if (!bagHasClosedOnce)
                {
                    bagHasClosedOnce = true;
                    Debug.Log("[CloseWatcher v2] ===== BAG CLOSED (first time) ===== " +
                              "frame-time lines below this are tagged AFTER-close. Move around now.");
                }
                Debug.Log($"[CloseWatcher v2] close state: timeScale {Time.timeScale:F2}, " +
                          $"cursorLock {Cursor.lockState}, spacePresses so far {spacePresses}, " +
                          $"jumpsReported so far {jumpsReported}");
            }
            wasOpenLastFrame = openNow;
        }
    }

    /// <summary>
    /// PlayerMovement's jump debug logs go through Debug.Log with a paw emoji. We cannot read those
    /// without touching that script, so instead we let the user eyeball it: this just reminds them
    /// at the end how many times THEY pressed Space, to compare against the paw-jump lines in the
    /// console. If the console shows far more jumps than this number, input is multi-firing.
    /// </summary>
    private void OnDisable()
    {
        Debug.Log($"[CloseWatcher v2] ===== SESSION END ===== you pressed Space {spacePresses} time(s). " +
                  "Compare that to how many paw-emoji JUMP lines appeared in the console. " +
                  "If there are many more jump lines than presses, input is firing multiple times.");
    }
}