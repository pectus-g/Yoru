using UnityEngine;

/// <summary>
/// ONI (Boss 1) — boss-specific behavior layered on top of the shared EnemyCombat/EnemyHealth
/// engine. Same pattern as KomainuBoss: the shared scripts stay generic, everything unique to
/// THIS boss lives here. No shared-script behavior is changed by this component.
///
/// Implements (all Oni-only):
///   1. TIERED HIT REACTIONS — small hit = quick flinch clip, medium = full react clip,
///      heavy/stagger untouched (engine owns those). Only acts when the engine entered HitReact.
///   2. WAKE ON HIT — any damage while dormant (LostSoul) calls BecomeHostile(), so attacks
///      from behind start the fight instead of being ignored by the vision cone.
///   3. PRE-COMBAT WATCH STANCE — while dormant/idle: player inside watchRange = Watch
///      animation (he senses her), farther = Idle. Piggybacks on the engine's animation
///      tracker without fighting it. The leash/return-to-spawn behavior itself is the engine's
///      existing Escape Range + Return To Spawn On Disengage (both already configured).
///   4. BOSS BAR DRIVER — shows BossHealthBarUI on any hostile state (not just Alert, which a
///      projectile opener skips), sends SetPhase2 (crimson) at the phase flip, hides it again
///      when the Oni disengages and returns to his watch position.
///
/// Planned to live here later (per ONI handoff §3): ground pound AoE + landing circle,
/// kanabo sweep jump-only avoidance, phase-2 roar cinematic, arena destruction.
/// </summary>
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyHealth))]
public class OniBoss : MonoBehaviour
{
    [Header("Tiered Hit Reactions")]
    [Tooltip("Master switch for the tiered reactions below.")]
    [SerializeField] private bool tieredReactionsEnabled = true;

    [Tooltip("Damage at or BELOW this plays the quick flinch. Above it (but below the stagger threshold) plays the full react. Oni tuning: paws 10 = quick, tail shots / double paw 20 = full.")]
    [SerializeField] private int lightHitMaxDamage = 14;

    [Tooltip("Animator STATE for the quick flinch. Oni: 'HitReact_medium' holds the short 0.79s clip (the 'lighter light' file).")]
    [SerializeField] private string lightReactState = "HitReact_medium";

    [Tooltip("Animator STATE for the full react. Oni: 'Hit_react_light' is the 1.46s clip — also the engine's default, so medium hits simply keep it.")]
    [SerializeField] private string mediumReactState = "Hit_react_light";

    [Tooltip("Crossfade time into the react clip, seconds.")]
    [SerializeField] private float reactCrossfade = 0.08f;

    [Header("Wake On Hit")]
    [Tooltip("Any damage taken while dormant (LostSoul) makes the Oni hostile immediately — attacks from behind wake him. Without this he only reacts when the player enters his vision cone.")]
    [SerializeField] private bool wakeOnAnyHit = true;

    [Header("Pre-Combat Watch Stance")]
    [Tooltip("Animator STATE for the alert watching stance used before combat. Leave empty to disable the stance swap.")]
    [SerializeField] private string watchState = "Watch";
    [Tooltip("While dormant/idle: player closer than this plays the Watch stance, farther plays Idle. 0 disables. Tune to taste; pairs with the engine's Escape Range (leash) which you tweak separately on EnemyCombat.")]
    [SerializeField] private float watchRange = 15f;
    [Tooltip("Extra meters the player must retreat past Watch Range before the Oni relaxes back to Idle, so the stance does not flicker at the boundary.")]
    [SerializeField] private float watchHysteresis = 1.5f;

    [Header("Boss Bar")]
    [Tooltip("Drive the screen-top BossHealthBarUI for this boss: show on any hostile state, crimson at phase 2, hide on disengage. Needs a BossHealthBar object (with BossHealthBarUI) on the HUD canvas.")]
    [SerializeField] private bool driveBossBar = true;
    [Tooltip("Name shown above the bar.")]
    [SerializeField] private string bossBarDisplayName = "Oni";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private EnemyCombat combat;
    private EnemyHealth health;
    private Animator animator;
    private Transform playerT;

    private bool inWatchStance;   // current pre-combat stance (false = Idle)
    private bool barShown;
    private bool phase2Sent;

    // Warn about a missing animator state only once, same protection the engine uses.
    private readonly System.Collections.Generic.HashSet<string> missingStatesWarned =
        new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        // EnemyCombat finds/caches the Animator in its own Start; fetch it from there so both
        // scripts are guaranteed to drive the same one.
        animator = combat.GetAnimator();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerT = p.transform;
        if (playerT == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) playerT = pm.transform;
        }

        DebugLog($"OniBoss layer ready (reactions, wake-on-hit, watch stance, boss bar). player={(playerT != null ? "found" : "NOT FOUND")}");
    }

    private void OnEnable()
    {
        var h = health != null ? health : GetComponent<EnemyHealth>();
        if (h != null) h.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    private void Update()
    {
        UpdatePreCombatWatch();
        UpdateBossBar();
    }

    // ─────────────────────────────────────────────────────────────── reactions + wake ──

    /// <summary>
    /// Runs right after EnemyHealth applied a hit and the engine chose its generic reaction.
    /// First: waking — damage during LostSoul makes him hostile (back attacks count).
    /// Then: if the engine entered HitReact, swap the playing clip for the damage tier.
    /// Staggers, flash-only hits and the killing blow never sit in HitReact, so they're left alone.
    /// </summary>
    private void HandleDamaged(int damage, bool isHeavy)
    {
        if (combat == null) return;

        if (wakeOnAnyHit && combat.GetCurrentState() == EnemyCombat.EnemyState.LostSoul)
        {
            combat.BecomeHostile();
            DebugLog($"woken by damage ({damage}) — back attacks count.");
            return; // this hit spent itself waking him; reactions apply from the next hit
        }

        if (!tieredReactionsEnabled) return;
        if (isHeavy) return; // heavy = stagger territory, engine owns it
        if (animator == null) return;
        if (combat.GetCurrentState() != EnemyCombat.EnemyState.HitReact) return;

        string state = damage <= lightHitMaxDamage ? lightReactState : mediumReactState;
        if (string.IsNullOrEmpty(state)) return;
        if (!HasState(state)) return;

        animator.CrossFadeInFixedTime(state, reactCrossfade, 0);
        DebugLog($"react tier: {(damage <= lightHitMaxDamage ? "LIGHT" : "MEDIUM")} ({damage} dmg) → '{state}'");
    }

    // ─────────────────────────────────────────────────────────────── pre-combat watch ──

    /// <summary>
    /// While the Oni is dormant (LostSoul) or has returned to his post (Idle), swap between the
    /// Watch stance (player near) and Idle (player far). The engine's per-frame PlayAnimation
    /// early-outs on its own tracker, so a direct crossfade here is not overridden; the moment
    /// real combat animations play, their different names take over cleanly.
    /// </summary>
    private void UpdatePreCombatWatch()
    {
        if (watchRange <= 0f || string.IsNullOrEmpty(watchState)) return;
        if (combat == null || animator == null || playerT == null) return;

        var s = combat.GetCurrentState();
        bool preCombat = s == EnemyCombat.EnemyState.LostSoul || s == EnemyCombat.EnemyState.Idle;
        if (!preCombat)
        {
            inWatchStance = false; // combat/other states own the animator now
            return;
        }

        float dist = Vector3.Distance(transform.position, playerT.position);

        if (!inWatchStance && dist <= watchRange)
        {
            if (!HasState(watchState)) return;
            animator.CrossFadeInFixedTime(watchState, 0.25f, 0);
            inWatchStance = true;
            DebugLog($"watch stance ON (player {dist:F1}m)");
        }
        else if (inWatchStance && dist > watchRange + watchHysteresis)
        {
            // Relax back to the engine's idle clip.
            animator.CrossFadeInFixedTime("Idle", 0.25f, 0);
            inWatchStance = false;
            DebugLog($"watch stance OFF, back to Idle (player {dist:F1}m)");
        }
    }

    // ─────────────────────────────────────────────────────────────────────── boss bar ──

    /// <summary>
    /// Shows the screen-top bar the moment the Oni is genuinely hostile (any combat state — a
    /// projectile opener can skip Alert entirely), keeps the phase-2 crimson in sync, and hides
    /// the bar when he disengages back to his post. Death fade is handled inside BossHealthBarUI
    /// by its own HP tracking.
    /// </summary>
    private void UpdateBossBar()
    {
        if (!driveBossBar || combat == null || health == null) return;
        if (BossHealthBarUI.Instance == null) return;

        var s = combat.GetCurrentState();
        bool hostile =
            s == EnemyCombat.EnemyState.Alert ||
            s == EnemyCombat.EnemyState.Chase ||
            s == EnemyCombat.EnemyState.Telegraph ||
            s == EnemyCombat.EnemyState.Attack ||
            s == EnemyCombat.EnemyState.Recovery ||
            s == EnemyCombat.EnemyState.HitReact ||
            s == EnemyCombat.EnemyState.Stagger;

        if (!barShown && hostile)
        {
            BossHealthBarUI.Instance.Show(health, bossBarDisplayName);
            barShown = true;
            DebugLog("boss bar shown");
        }

        if (barShown && !phase2Sent && combat.IsPhase2())
        {
            BossHealthBarUI.Instance.SetPhase2();
            phase2Sent = true;
            DebugLog("boss bar → phase 2 crimson");
        }

        // Disengaged back to post (leash) — hide until the next engagement. Dead is excluded:
        // the bar handles its own death flash + fade.
        bool disengaged =
            s == EnemyCombat.EnemyState.Returning ||
            s == EnemyCombat.EnemyState.Idle ||
            s == EnemyCombat.EnemyState.LostSoul;

        if (barShown && disengaged)
        {
            BossHealthBarUI.Instance.Hide("Oni disengaged");
            barShown = false;
            phase2Sent = false; // re-sent on next show if still phase 2
            DebugLog("boss bar hidden (disengage)");
        }
    }

    // ───────────────────────────────────────────────────────────────────────── helpers ──

    private bool HasState(string state)
    {
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash)) return true;
        if (missingStatesWarned.Add(state))
            Debug.LogError($"[OniBoss] Animator state '{state}' not found — check the state name.");
        return false;
    }

    private void DebugLog(string msg)
    {
        if (showDebugLogs)
            Debug.Log($"[OniBoss:Layer] {msg}");
    }
}
