using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// YORU Combat Music Manager — Game Feel
/// 
/// Triggers combat music when any enemy enters Alert/Chase.
/// Ends combat music when ALL nearby enemies are dead OR no combat activity for X seconds.
/// 
/// How it works:
///   - Enemies register via NotifyEnemyAggro(). Each call resets the idle timer.
///   - When combat starts: ducks MusicController volume, fades in combat AudioSource.
///   - When combat ends: fades out combat music, restores MusicController.
///   - Enemies register themselves on EnemyCombat.Start, unregister on Dead state.
/// 
/// SETUP: 
///   1. Create empty GameObject "CombatMusicManager" in scene
///   2. Add this component
///   3. Drag a combat music AudioClip into the Inspector slot when ready
///   4. MusicController is auto-found at Start
/// 
/// MINIMAL CHANGE TO EnemyCombat.SetState:
///   In the Alert case, add:
///     if (CombatMusicManager.Instance != null) CombatMusicManager.Instance.NotifyEnemyAggro(this);
/// </summary>
public class CombatMusicManager : MonoBehaviour
{
    #region Singleton
    public static CombatMusicManager Instance { get; private set; }

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
    [Header("Combat Music")]
    [Tooltip("Drag combat music clip here when ready. Leave empty to test the system without audio.")]
    [SerializeField] private AudioClip combatMusicClip;
    [Tooltip("Combat music volume (before master)")]
    [SerializeField] [Range(0f, 1f)] private float combatVolume = 0.8f;

    [Header("Fade Timing")]
    [Tooltip("Seconds to fade combat music in")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [Tooltip("Seconds to fade combat music out")]
    [SerializeField] private float fadeOutDuration = 3f;
    [Tooltip("How much to duck ambient music during combat (0 = mute, 1 = no duck)")]
    [SerializeField] [Range(0f, 1f)] private float ambientDuckLevel = 0.2f;
    [Tooltip("Seconds of ambient duck/restore fade")]
    [SerializeField] private float duckFadeDuration = 1.5f;

    [Header("Combat End Conditions")]
    [Tooltip("Seconds of no combat activity before music fades out")]
    [SerializeField] private float idleTimeout = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region Private Fields
    private AudioSource combatSource;
    private MusicController musicController;

    // State
    private bool inCombat;
    private float idleTimer;
    private float targetCombatVolume;
    private float currentDuckLevel = 1f; // 1 = no duck, 0 = muted
    private float targetDuckLevel = 1f;

    // Track living aggro'd enemies
    private HashSet<EnemyCombat> activeEnemies = new HashSet<EnemyCombat>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Create dedicated AudioSource for combat music
        combatSource = gameObject.AddComponent<AudioSource>();
        combatSource.loop = true;
        combatSource.playOnAwake = false;
        combatSource.volume = 0f;
        combatSource.spatialBlend = 0f; // 2D — always audible

        if (combatMusicClip != null)
            combatSource.clip = combatMusicClip;

        // Find MusicController for ducking
        musicController = FindObjectOfType<MusicController>();

        DebugLog("CombatMusicManager initialized" +
            (combatMusicClip != null ? $" (clip: {combatMusicClip.name})" : " (no clip assigned — system ready, add music later)"));
    }

    private void Update()
    {
        if (inCombat)
        {
            // Check if all tracked enemies are dead
            CleanDeadEnemies();

            if (activeEnemies.Count == 0)
            {
                // No living enemies — start idle countdown
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleTimeout)
                {
                    EndCombat("all enemies dead + idle timeout");
                }
            }
            else
            {
                // Still have living aggro enemies — but check idle timer too
                // (in case player runs far away and enemies de-aggro)
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleTimeout)
                {
                    EndCombat("idle timeout (enemies alive but no activity)");
                }
            }
        }

        // Smooth fade combat volume
        float currentVol = combatSource.volume;
        float targetVol = targetCombatVolume;
        float fadeSpeed = inCombat ? (1f / fadeInDuration) : (1f / fadeOutDuration);
        combatSource.volume = Mathf.MoveTowards(currentVol, targetVol, fadeSpeed * Time.deltaTime);

        // Smooth duck ambient
        currentDuckLevel = Mathf.MoveTowards(currentDuckLevel, targetDuckLevel, (1f / duckFadeDuration) * Time.deltaTime);
        if (musicController != null)
        {
            // Duck by setting master volume. Store original and scale it.
            // MusicController.SetMasterVolume expects 0-1, we multiply its current target.
            musicController.SetMasterVolume(0.7f * currentDuckLevel);
        }

        // Stop playback when fully faded out
        if (!inCombat && combatSource.volume < 0.01f && combatSource.isPlaying)
        {
            combatSource.Stop();
        }
    }
    #endregion

    #region Public API — Called by EnemyCombat

    /// <summary>
    /// Call when an enemy enters Alert or Chase state.
    /// Starts combat music if not already playing. Resets idle timer.
    /// </summary>
    public void NotifyEnemyAggro(EnemyCombat enemy)
    {
        if (enemy == null) return;

        activeEnemies.Add(enemy);
        idleTimer = 0f; // Reset idle countdown

        if (!inCombat)
            StartCombat(enemy.name);
    }

    /// <summary>
    /// Call when any combat action happens (hit, dodge, parry, etc.)
    /// Resets the idle timer so music doesn't fade during active fighting.
    /// </summary>
    public void NotifyCombatActivity()
    {
        if (inCombat)
            idleTimer = 0f;
    }

    /// <summary>
    /// Call when an enemy dies. Removes from tracking.
    /// </summary>
    public void NotifyEnemyDead(EnemyCombat enemy)
    {
        if (enemy == null) return;
        activeEnemies.Remove(enemy);
        DebugLog($"Enemy removed: {enemy.name} ({activeEnemies.Count} remaining)");

        // If no enemies left, start the idle countdown (don't end instantly — 
        // gives a beat of tension after the last kill)
        if (activeEnemies.Count == 0 && inCombat)
        {
            idleTimer = idleTimeout - 3f; // 3 seconds of combat music after last kill
            if (idleTimer < 0f) idleTimer = 0f;
            DebugLog("Last enemy dead — combat music will fade in ~3s");
        }
    }
    #endregion

    #region Combat State
    private void StartCombat(string triggerEnemy)
    {
        inCombat = true;
        idleTimer = 0f;
        targetCombatVolume = combatVolume;
        targetDuckLevel = ambientDuckLevel;

        // Start playback if we have a clip
        if (combatSource.clip != null)
        {
            // Kick volume to 30% immediately — don't start from silence
            combatSource.volume = combatVolume * 0.3f;

            if (!combatSource.isPlaying)
                combatSource.Play();

            DebugLog($"COMBAT START (triggered by: {triggerEnemy}) — clip: {combatSource.clip.name}, playing: {combatSource.isPlaying}, vol: {combatSource.volume:F2}");
        }
        else
        {
            DebugLog($"COMBAT START (triggered by: {triggerEnemy}) — NO CLIP ASSIGNED");
        }
    }

    private void EndCombat(string reason)
    {
        inCombat = false;
        targetCombatVolume = 0f;
        targetDuckLevel = 1f; // Restore ambient to full
        activeEnemies.Clear();

        DebugLog($"COMBAT END ({reason})");
    }
    #endregion

    #region Helpers
    private void CleanDeadEnemies()
    {
        // Remove null (destroyed) or dead enemies from tracking
        activeEnemies.RemoveWhere(e =>
        {
            if (e == null) return true; // Destroyed
            var health = e.GetComponent<EnemyHealth>();
            return health != null && health.IsDead();
        });
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[CombatMusic] {message}");
    }
    #endregion
}