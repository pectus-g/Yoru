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

    [Header("Charge Travel Cancel")]
    [Tooltip("The charge clip is a Mixamo-style animation with the forward travel baked into the HIPS bone (the FBX's motion node is the unmoving RootNode, so Unity extracts no root motion). Left alone the hips carry the mesh metres ahead of the NavMesh transform: the body overshoots Yoru and, because a SkinnedMeshRenderer's bounds stay with the transform, the renderer gets culled — the mesh VANISHES, then reappears when the transform catches up. This cancels the horizontal drift every frame of the charge state, so the mesh always sits on the transform. Vertical bob and all rotations are untouched.")]
    [SerializeField] private bool chargePinEnabled = true;
    [Tooltip("Animator STATE name of the charge attack clip.")]
    [SerializeField] private string chargeStateName = "Oni_Charge";
    [Tooltip("Horizontal drift (metres, world space) a skeleton node may show before it is pulled back. Tiny on purpose: skeleton bones do not translate locally except the travel bone, so anything above sensor noise IS the baked travel.")]
    [SerializeField] private float chargeTravelCancelEpsilon = 0.01f;
    [Tooltip("Force every SkinnedMeshRenderer on the Oni to recompute its bounds from the bones each frame. Belt-and-braces against the vanish: even if some travel slipped through, the mesh can no longer be culled while its bones are on screen. Trivial cost for one boss.")]
    [SerializeField] private bool keepMeshVisibleWhenBonesTravel = true;
    [Tooltip("NavMeshAgent acceleration while the charge plays. The default agent accel (8) takes over a second to reach charge speed — this makes the rush hit top speed almost instantly and stop hard at the player. Restored to the original value when the charge ends.")]
    [SerializeField] private float chargeAcceleration = 45f;

    [Header("Reaction Freeze Guard")]
    [Tooltip("Hard REAL-TIME ceiling on the reaction states. Yoru's air-shot aim drops Time.timeScale to 0.1, and every EnemyCombat timer runs on scaled time — so a 0.8s flinch really lasts 8 real seconds and a 2.5s stagger lasts 25. That is the 'Oni froze in a hit reaction' bug: the Oni is not stuck, it is running at a tenth speed because Yoru is aiming. This guard forces him back to Chase after the real-second limits below, whatever the world clock is doing. OFF = old behavior.")]
    [SerializeField] private bool reactionFreezeGuard = true;
    [Tooltip("Max REAL seconds the Oni may spend in HitReact. Keep a little above EnemyCombat's Hit React Duration (0.8). 0 disables just this one.")]
    [SerializeField] private float maxHitReactRealSeconds = 1.2f;
    [Tooltip("Max REAL seconds the Oni may spend in Stagger. Keep a little above EnemyCombat's Stagger Duration (2.5). 0 disables just this one.")]
    [SerializeField] private float maxStaggerRealSeconds = 3.2f;

    [Header("Slow-Motion Watchdog (diagnostic only)")]
    [Tooltip("Changes NOTHING. Logs one loud error if Time.timeScale stays below 1 for longer than the limit below, which proves whether an aim ability leaked and never restored the world clock (a permanent freeze) as opposed to normal aim slow-motion (a temporary one). Leave on until the freeze is fully understood, then turn off.")]
    [SerializeField] private bool slowMotionWatchdog = true;
    [Tooltip("Real seconds of continuous slow-motion before the watchdog complains. Yoru's aim budget is 3s, so anything past ~6s means the clock was never restored.")]
    [SerializeField] private float slowMotionWarnAfterRealSeconds = 6f;

    [Header("Hold Ground / No Orbiting")]
    [Tooltip("Stops the Oni circling and sliding around Yoru. After each of his OWN attacks he walks a fixed couple of steps BACKWARD (walk clip played in reverse), holds the watch stance WITHOUT tracking her, squares up once just before his cooldown ends, then steps in if she is out of reach and swings. Being hit does not trigger the backstep. Switched on from code at Start.")]
    [SerializeField] private bool holdGroundEnabled = true;
    [Tooltip("How far he walks straight BACK after each of his own attacks — a fixed 'couple of steps' whatever the distance to Yoru. Last round this was a standoff distance instead, and when Yoru was already ~3m away it computed to zero steps, which is why you saw only the Watch. 1.5m at 1.6 m/s ≈ 1 second of visible reverse walking.")]
    [SerializeField] private float holdGroundBackstepDistance = 1.5f;
    [Tooltip("How fast the backward steps are. Slow reads heavy and deliberate.")]
    [SerializeField] private float holdGroundBackstepSpeed = 1.6f;
    [Tooltip("Walk speed for the step-in when his cooldown has run out but Yoru is a little past reach.")]
    [SerializeField] private float holdGroundApproachSpeed = 2.2f;
    [Tooltip("Animator STATE scrubbed in reverse for the backstep. 'Walk' played backwards is exactly the reverted walk animation.")]
    [SerializeField] private string holdGroundBackstepState = "Walk";
    [Tooltip("Animator STATE held while waiting out the cooldown at standoff distance.")]
    [SerializeField] private string holdGroundWatchState = "Watch";
    [Tooltip("Seconds before his cooldown ends that he turns to face Yoru. This single turn is the ONLY rotation between attacks — that is what kills the sliding-orbit look.")]
    [SerializeField] private float holdGroundReaimLead = 0.4f;
    [Tooltip("Once a swing starts, the Oni cannot turn — he hits where Yoru WAS. Makes dodging a skill instead of a coin flip. The charge is exempt (a rush that cannot steer misses by design).")]
    [SerializeField] private bool lockFacingDuringAttack = true;

    [Header("Animation Blending")]
    [Tooltip("Applied from code at Start so nothing needs setting in the inspector. Blend time for ordinary state changes (idle / walk / run / watch / recovery). The engine default was a hardcoded 0.1s, which on a body this heavy reads as the animation snapping between poses.")]
    [SerializeField] private float blendTimeNormal = 0.25f;
    [Tooltip("Blend time for FORCED restarts — the hit reaction. 0 would be the old instant hard cut. A small value keeps the restart but stops the pose teleporting in one frame.")]
    [SerializeField] private float blendTimeForced = 0.12f;

    [Header("Facing")]
    [Tooltip("Below this horizontal distance he does not turn toward Yoru at all. When she is directly above him (her aerial spin puts her there) the flat direction is centimetres long and flips sign every frame — turning toward it makes his whole body shudder in place. THAT was the 'vibrates when Yoru jumps and swirls'. 1.0 = if she is within a metre horizontally, hold still.")]
    [SerializeField] private float lookAtMinFlatDistance = 1.0f;
    [Tooltip("The engine snaps the enemy to face the player instantly at the start of every attack (scene value: ON). On a body this heavy that is a visible one-frame rotation pop before every swing. OFF: he swings where he is facing — the square-up before the cooldown ends, and the chase, already point him at her.")]
    [SerializeField] private bool snapToFaceOnAttack = false;
    [Tooltip("Keep turning toward Yoru during the flinch? OFF: a flinch is a flinch — he reacts where he stands.")]
    [SerializeField] private bool trackPlayerDuringHitReact = false;

    [Header("Knockback On Being Hit")]
    [Tooltip("Push the Oni backward when Yoru connects, scaled by the damage tier. Without this a heavy hit reads as the boss simply stopping; with it he stumbles, which is what sells the weight.")]
    [SerializeField] private bool knockbackEnabled = true;
    [Tooltip("Metres pushed back by a LIGHT hit (10 dmg paw). 0 = light hits do not move him at all, which keeps the ladder readable.")]
    [SerializeField] private float knockbackLight = 0f;
    [Tooltip("Metres pushed back by a MEDIUM hit (20 dmg strong paw / ground tail arrow).")]
    [SerializeField] private float knockbackMedium = 0.35f;
    [Tooltip("Metres pushed back by a HEAVY / staggering hit (35 beyblade, 40 four-leg air shot). This is the stumble before the stagger plays.")]
    [SerializeField] private float knockbackHeavy = 0.9f;
    [Tooltip("Seconds the push takes. Short and sharp reads as impact; long reads as being shoved.")]
    [SerializeField] private float knockbackDuration = 0.18f;
    [Tooltip("Minimum REAL seconds between two knockbacks. The beyblade and aerial spin tick damage several times a second — without a floor here the Oni gets shoved on every tick and slides away across the arena.")]
    [SerializeField] private float knockbackMinInterval = 0.3f;

    [Header("Charge Drive (lance rush)")]
    [Tooltip("The Oni owns the charge himself instead of the shared engine's generic lunge. He holds the club straight forward, crosses the whole gap, brakes next to Yoru, and only THEN does the clip finish into the club strike. Turning this off returns the charge to the old engine lunge.")]
    [SerializeField] private bool chargeDriveEnabled = true;
    [Tooltip("Normalized point in the charge clip held for the whole rush — this should be the frame where the club is out straight in front of him. Lower = earlier in the wind-up. Must be BEFORE the strike, or the hit resolves before he arrives.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float chargeHoldNormalizedTime = 0.35f;
    [Tooltip("Clip speed during the in-place wind-up (club coming forward). 1 = the attack's own speed; a little above 1 makes the telegraph snappier without changing the strike.")]
    [SerializeField] private float chargeWindupSpeed = 1.3f;
    [Tooltip("Rush speed in metres per second. This is the whole gap-closer, so it should feel alarming — 12-18 for a 20m arena.")]
    [SerializeField] private float chargeSpeed = 14f;
    [Tooltip("How close he gets before braking. Should be inside Attack Range (3.5) so the club strike actually lands.")]
    [SerializeField] private float chargeStopDistance = 2.6f;
    [Tooltip("Seconds at the start of the rush during which he still steers toward Yoru's live position. After this he COMMITS to wherever she was — dodging late is how you beat it. 0 = commits instantly at the start.")]
    [SerializeField] private float chargeTrackSeconds = 0.35f;
    [Tooltip("How fast he turns while tracking. After the commit he keeps only a quarter of this, just enough to hold the club aimed at the locked point.")]
    [SerializeField] private float chargeTurnSpeed = 6f;
    [Tooltip("Hard ceiling in REAL seconds for the rush. If he somehow cannot reach the lock point (blocked, off-mesh) he releases the strike anyway instead of holding the pose forever.")]
    [SerializeField] private float chargeMaxTravelSeconds = 2.5f;

    [Header("Charge Trail VFX (placeholder)")]
    [Tooltip("The code-built arcs that trailed the rush last round — the 'linear strips' you saw. OFF by default now: they are stretched primitives standing in for real VFX and they read as junk. Turn on only if you want them back until a proper wave effect is authored and wired into EnemyFX.")]
    [SerializeField] private bool chargeTrailVFX = false;
    [Tooltip("Seconds between wave puffs while the charge clip plays.")]
    [SerializeField] private float chargeWaveInterval = 0.09f;
    [Tooltip("Width of each wave arc in metres. Scale to the Oni's size.")]
    [SerializeField] private float chargeWaveScale = 2.6f;

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
    private UnityEngine.AI.NavMeshAgent navAgent;
    private float preChargeAcceleration = -1f;

    private bool inWatchStance;   // current pre-combat stance (false = Idle)
    private bool barShown;
    private bool phase2Sent;

    // Reaction freeze guard + slow-motion watchdog state. All measured in UNSCALED time on
    // purpose: the whole point is to survive a world clock that is running at a tenth speed.
    private EnemyCombat.EnemyState lastSeenState;
    private float stateEnteredRealTime;
    private float slowSinceRealTime = -1f;
    private bool slowWarned;

    // Charge drift pin state
    private bool chargePinActive;
    private float chargeWaveTimer;
    private int chargeStateHash;

    // Charge drive state
    private ChargePhase chargePhase;
    private bool chargeFrozeClip;
    private float chargeStartRealTime;
    private float chargeRushStartRealTime;
    private Vector3 chargeTarget;
    private readonly System.Collections.Generic.List<Transform> pinNodes =
        new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Vector3> pinStartLocal =
        new System.Collections.Generic.List<Vector3>();

    // Warn about a missing animator state only once, same protection the engine uses.
    private readonly System.Collections.Generic.HashSet<string> missingStatesWarned =
        new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
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

        lastSeenState = combat.GetCurrentState();
        stateEnteredRealTime = Time.unscaledTime;

        // Smoother state changes. Applied from code so the shared engine keeps its old 0.1s default
        // for every other enemy and nothing needs setting in the inspector.
        combat.ConfigureAnimationBlending(blendTimeNormal, blendTimeForced);

        // How he turns: no shudder when she is overhead, no one-frame face-snap at attack start,
        // no tracking during the flinch.
        combat.ConfigureFacing(lookAtMinFlatDistance, snapToFaceOnAttack, trackPlayerDuringHitReact);
        DebugLog($"facing: minFlat {lookAtMinFlatDistance}m, snapOnAttack {(snapToFaceOnAttack ? "ON" : "off")}, trackInHitReact {(trackPlayerDuringHitReact ? "ON" : "off")}");

        // The mesh must never be culled while its bones are on screen (see Charge Travel Cancel).
        if (keepMeshVisibleWhenBonesTravel)
        {
            int n = 0;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                smr.updateWhenOffscreen = true;
                n++;
            }
            DebugLog($"skinned mesh bounds: updateWhenOffscreen ON for {n} renderer(s)");
        }

        // Take the charge away from the engine's generic lunge so two systems are not pushing the
        // same agent in the same frame.
        // The charge is almost always the opener of Charge→Slam, and the engine's default rule says
        // only a combo's FINAL step reads heavy. Without this the lance rush would land on Yoru as a
        // light tap however much damage it does.
        combat.SetComboStepsUseDamageThreshold(true);

        if (chargeDriveEnabled)
        {
            combat.SetExternalLungeControl(true);
            DebugLog($"charge drive ON (windup to frame {chargeHoldNormalizedTime:F2} in place, rush {chargeSpeed}m/s, "
                   + $"stops at {chargeStopDistance}m, steers for {chargeTrackSeconds}s then commits, trail VFX {(chargeTrailVFX ? "on" : "off")})");
        }

        // Turn the Oni into a committing heavy instead of a circling one. Done from code on purpose:
        // the shared EnemyCombat keeps its old defaults for every other enemy and no inspector work
        // is needed on the scene instance.
        if (holdGroundEnabled)
        {
            combat.ConfigureMeleeHoldGround(
                enabled: true,
                backstepDistance: holdGroundBackstepDistance,
                backstepSpeed: holdGroundBackstepSpeed,
                backstepState: holdGroundBackstepState,
                backstepSpeed_Anim: -1f,          // walk clip, reversed
                holdState: holdGroundWatchState,
                reaimLead: holdGroundReaimLead,
                lockAttackFacing: lockFacingDuringAttack,
                approachSpeed: holdGroundApproachSpeed);
            DebugLog($"hold-ground ON (backstep {holdGroundBackstepDistance}m @ {holdGroundBackstepSpeed}m/s after own attacks, "
                   + $"watch '{holdGroundWatchState}', attack facing {(lockFacingDuringAttack ? "LOCKED" : "free")})");
        }

        DebugLog($"OniBoss layer ready (reactions, wake-on-hit, watch stance, boss bar). player={(playerT != null ? "found" : "NOT FOUND")}");
    }

    // ────────────────────────────────────────────────── freeze guard + slow-mo watchdog ──

    /// <summary>
    /// Yoru's tail-shot aim sets Time.timeScale to 0.1 for up to 3 REAL seconds, and every timer in
    /// EnemyCombat (stateTimer -= Time.deltaTime) is on the scaled clock. So while she aims, the Oni's
    /// 0.8s flinch takes 8 real seconds and his 2.5s stagger takes 25 — he looks frozen mid-reaction.
    /// This is a ceiling measured in real seconds: past the limit the reaction is over, no matter what
    /// the world clock says. It never shortens anything at normal speed (the limits sit above the
    /// configured durations), so it is a no-op outside slow-motion.
    /// </summary>
    private void UpdateReactionFreezeGuard()
    {
        if (combat == null) return;

        var s = combat.GetCurrentState();
        if (s != lastSeenState)
        {
            lastSeenState = s;
            stateEnteredRealTime = Time.unscaledTime;
            return;
        }

        if (!reactionFreezeGuard) return;

        float limit;
        if (s == EnemyCombat.EnemyState.HitReact)      limit = maxHitReactRealSeconds;
        else if (s == EnemyCombat.EnemyState.Stagger)  limit = maxStaggerRealSeconds;
        else return;

        if (limit <= 0f) return;

        float held = Time.unscaledTime - stateEnteredRealTime;
        if (held < limit) return;

        DebugLog($"freeze guard: {s} held {held:F2}s REAL (limit {limit:F2}, world clock {Time.timeScale:F2}) → forcing Chase");
        combat.SetState(EnemyCombat.EnemyState.Chase);
    }

    /// <summary>
    /// Pure diagnostic, changes nothing. Yoru's aim abilities cache and restore Time.timeScale
    /// correctly on every exit path I can see, but if one ever leaks the whole game stays at a tenth
    /// speed and the Oni appears permanently frozen. One loud error line tells us which of the two
    /// freezes we are looking at instead of guessing.
    /// </summary>
    private void UpdateSlowMotionWatchdog()
    {
        if (!slowMotionWatchdog) return;

        if (Time.timeScale < 0.9f)
        {
            if (slowSinceRealTime < 0f) slowSinceRealTime = Time.unscaledTime;

            if (!slowWarned && Time.unscaledTime - slowSinceRealTime > slowMotionWarnAfterRealSeconds)
            {
                slowWarned = true;
                Debug.LogError($"[OniBoss:Layer] SLOW-MOTION STUCK — Time.timeScale has been {Time.timeScale:F2} "
                    + $"for over {slowMotionWarnAfterRealSeconds}s real (fixedDeltaTime {Time.fixedDeltaTime:F4}). "
                    + "An aim ability did not restore the world clock. THIS is the permanent freeze — send me this line.");
            }
        }
        else
        {
            slowSinceRealTime = -1f;
            slowWarned = false;
        }
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
        UpdateReactionFreezeGuard();
        UpdateSlowMotionWatchdog();
        UpdatePreCombatWatch();
        UpdateBossBar();
    }

    /// <summary>
    /// Runs AFTER the Animator has written this frame's pose, so the pin sees (and can cancel)
    /// exactly what the charge clip did to the skeleton this frame. Same ordering trick the
    /// player's AirPosePin uses.
    /// </summary>
    private void LateUpdate()
    {
        UpdateChargePin();
    }

    // ─────────────────────────────────────────────────────────────── charge drift pin ──

    private bool ChargeClipPlaying()
    {
        if (animator == null || combat == null) return false;
        if (combat.GetCurrentState() != EnemyCombat.EnemyState.Attack) return false;

        if (chargeStateHash == 0) chargeStateHash = Animator.StringToHash(chargeStateName);

        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == chargeStateHash) return true;
        return animator.IsInTransition(0)
            && animator.GetNextAnimatorStateInfo(0).shortNameHash == chargeStateHash;
    }

    /// <summary>Normalized time of the charge clip, transition-aware (reads the incoming clip while blending in).</summary>
    private float ChargeNormalizedTime()
    {
        if (animator == null) return 0f;
        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash == chargeStateHash) return next.normalizedTime;
        }
        var cur = animator.GetCurrentAnimatorStateInfo(0);
        return cur.shortNameHash == chargeStateHash ? cur.normalizedTime : 0f;
    }

    private enum ChargePhase { None, Windup, Rush, Strike }

    /// <summary>
    /// Runs in LateUpdate — AFTER the Animator has written this frame's pose — for the whole time
    /// the charge clip is on the animator, transition-in included.
    ///
    /// Two jobs:
    ///
    /// 1. TRAVEL CANCEL. The charge is a Mixamo-style clip: the forward travel is baked into the
    ///    HIPS bone's translation, not root motion (the FBX's motion node is the unmoving RootNode,
    ///    so Unity extracts nothing). Left alone, the hips carry the whole mesh metres ahead of the
    ///    NavMesh transform: the body overshoots Yoru, and — because a SkinnedMeshRenderer's bounds
    ///    stay with the transform — the renderer gets frustum-culled while its bones are elsewhere.
    ///    That is the "charges to Yoru, then vanishes, then comes back and hits" you saw. Every
    ///    frame this cancels the HORIZONTAL drift of the top skeleton levels against their pose at
    ///    charge start, in world space, so the mesh always sits on the transform while vertical bob
    ///    and every rotation are left untouched. Only the hips ever drift, so only the hips are moved.
    ///
    /// 2. THE LANCE RUSH, in three phases:
    ///    WINDUP — clip plays normally, in place, turning toward Yoru: the club comes forward.
    ///    RUSH   — at the hold frame the clip is FROZEN (AnimSpeed = 0, the multiplier the attack
    ///             states are bound to — no per-frame scrubbing, so no hard cuts) and the boss
    ///             drives itself across the gap. Steers for a moment, then commits.
    ///    STRIKE — arrived: AnimSpeed back to the attack's speed, the rest of the clip is the club
    ///             strike, and the engine's Strike Moment resolves the damage on it.
    /// </summary>
    private void UpdateChargePin()
    {
        if (animator == null) return;

        bool playing = ChargeClipPlaying();

        // ── charge began ─────────────────────────────────────────────────────────────────
        if (playing && !chargePinActive)
        {
            pinNodes.Clear();
            pinStartLocal.Clear();
            CollectPinNodes(animator.transform, 0, 3); // top 3 levels — covers flat and nested rigs
            chargePinActive = true;

            chargePhase = chargeDriveEnabled ? ChargePhase.Windup : ChargePhase.None;
            chargeStartRealTime = Time.unscaledTime;
            chargeRushStartRealTime = -1f;
            chargeFrozeClip = false;
            if (chargeDriveEnabled && combat != null && chargeWindupSpeed > 0f)
            {
                combat.SetAttackAnimSpeed(chargeWindupSpeed);   // snappier telegraph; restored on release / exit
                chargeFrozeClip = true;                        // "we touched AnimSpeed" — cleanup restores it
            }
            chargeTarget = playerT != null ? playerT.position : transform.position + transform.forward * 5f;
            chargeWaveTimer = 0f;

            if (navAgent != null)
            {
                preChargeAcceleration = navAgent.acceleration;
                navAgent.acceleration = chargeAcceleration;
            }

            DebugLog($"charge begin: cancelling baked travel on {pinNodes.Count} nodes, drive={(chargeDriveEnabled ? "windup→rush→strike" : "engine lunge")}");
        }
        // ── charge ended (state left, or interrupted by a flinch/stagger) ─────────────────
        else if (!playing && chargePinActive)
        {
            chargePinActive = false;
            chargePhase = ChargePhase.None;
            pinNodes.Clear();
            pinStartLocal.Clear();

            if (chargeFrozeClip && combat != null) combat.SetAttackAnimSpeed(1f);   // never leave the clip frozen
            chargeFrozeClip = false;

            if (navAgent != null && preChargeAcceleration > 0f)
            {
                navAgent.acceleration = preChargeAcceleration;
                preChargeAcceleration = -1f;
            }
        }

        if (!chargePinActive) return;

        // ── the drive ────────────────────────────────────────────────────────────────────
        if (chargePhase != ChargePhase.None) DriveCharge();

        // ── travel cancel, every frame of the state ──────────────────────────────────────
        if (chargePinEnabled)
        {
            float epsSq = chargeTravelCancelEpsilon * chargeTravelCancelEpsilon;
            for (int i = 0; i < pinNodes.Count; i++)
            {
                Transform t = pinNodes[i];
                if (t == null || t.parent == null) continue;

                Vector3 worldStart = t.parent.TransformPoint(pinStartLocal[i]);
                Vector3 drift = t.position - worldStart;
                drift.y = 0f;                                   // keep the vertical bob
                if (drift.sqrMagnitude > epsSq)
                    t.position -= drift;                        // children follow; they see no drift of their own
            }
        }
    }

    private void DriveCharge()
    {
        float now = Time.unscaledTime;

        switch (chargePhase)
        {
            case ChargePhase.Windup:
            {
                // In place: the club comes forward. Face her while it does.
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                }
                TurnToward(playerT != null ? playerT.position : chargeTarget, chargeTurnSpeed);

                if (ChargeNormalizedTime() >= chargeHoldNormalizedTime)
                {
                    combat.SetAttackAnimSpeed(0f);          // freeze on the club-forward frame
                    chargeFrozeClip = true;
                    chargePhase = ChargePhase.Rush;
                    chargeRushStartRealTime = now;
                    chargeTarget = playerT != null ? playerT.position : chargeTarget;
                    DebugLog($"charge RUSH: clip frozen at {ChargeNormalizedTime():F2}, {Vector3.Distance(transform.position, chargeTarget):F1}m to go");
                }
                return;
            }

            case ChargePhase.Rush:
            {
                float t = now - chargeRushStartRealTime;

                // Follows a little, then commits: steer toward her live position for the first
                // moments, then lock the destination. After the lock she escapes by moving.
                bool tracking = t < chargeTrackSeconds;
                if (tracking && playerT != null) chargeTarget = playerT.position;

                Vector3 to = chargeTarget - transform.position;
                to.y = 0f;
                float d = to.magnitude;

                TurnToward(chargeTarget, tracking ? chargeTurnSpeed : chargeTurnSpeed * 0.25f);

                bool arrived = d <= chargeStopDistance;
                bool timedOut = t >= chargeMaxTravelSeconds;

                if (!arrived && !timedOut)
                {
                    if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && d > 0.001f)
                    {
                        navAgent.isStopped = true;                          // we drive it ourselves
                        navAgent.Move(to.normalized * chargeSpeed * Time.deltaTime);
                    }
                    combat.HoldAttackSafety();                              // a long rush is not a hung state

                    if (chargeTrailVFX)
                    {
                        chargeWaveTimer -= Time.unscaledDeltaTime;
                        if (chargeWaveTimer <= 0f)
                        {
                            chargeWaveTimer = Mathf.Max(0.02f, chargeWaveInterval);
                            ProceduralImpactFX.Wave(transform.position + Vector3.up * 0.25f, transform.forward, chargeWaveScale);
                        }
                    }
                    return;
                }

                // Arrived (or gave up): stop dead, release the clip — the strike plays out from here.
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                    navAgent.velocity = Vector3.zero;
                combat.SetAttackAnimSpeed(combat.CurrentAttackSpeed());
                chargeFrozeClip = false;
                chargePhase = ChargePhase.Strike;

                DebugLog(arrived
                    ? $"charge arrived, {d:F1}m from the lock point after {t:F2}s real — releasing the club strike"
                    : $"charge timed out after {t:F2}s at {d:F1}m — releasing the club strike anyway");
                return;
            }

            case ChargePhase.Strike:
                // Committed. No turning, no moving; the travel cancel keeps the mesh planted.
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.velocity = Vector3.zero;
                }
                return;
        }
    }

    private void TurnToward(Vector3 worldPoint, float turnSpeed)
    {
        Vector3 to = worldPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.25f) return;   // on top of the point — nothing meaningful to face
        Quaternion look = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
    }

    /// <summary>
    /// Snapshot the local positions of the skeleton's top levels (pre-order: parents before their
    /// children) so the travel cancel can measure each node's own drift after its parent is fixed.
    /// </summary>
    private void CollectPinNodes(Transform parent, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            pinNodes.Add(c);
            pinStartLocal.Add(c.localPosition);
            CollectPinNodes(c, depth + 1, maxDepth);
        }
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

        // KNOCKBACK — fires for every tier, including the staggering ones, and before the tiered
        // clip swap below. The tier is read from what the engine ACTUALLY did with the hit rather
        // than from a second copy of the damage thresholds, so the push can never disagree with
        // the animation the player is looking at.
        if (knockbackEnabled && Time.unscaledTime >= nextKnockbackAllowedTime)
        {
            var s = combat.GetCurrentState();
            float push =
                s == EnemyCombat.EnemyState.Stagger ? knockbackHeavy :
                damage <= lightHitMaxDamage ? knockbackLight :
                knockbackMedium;

            // Rate limited on purpose. The beyblade and the aerial spin tick damage several times a
            // second; without this the Oni would be shoved the full heavy distance on every tick and
            // slide across the arena instead of stumbling once.
            nextKnockbackAllowedTime = Time.unscaledTime + Mathf.Max(0f, knockbackMinInterval);
            ApplyKnockback(push);
        }

        if (!tieredReactionsEnabled) return;
        if (isHeavy) return; // heavy = stagger territory, engine owns it
        if (animator == null) return;
        if (combat.GetCurrentState() != EnemyCombat.EnemyState.HitReact) return;

        // THE STUTTER FIX. Only swap the clip when the engine has just ENTERED HitReact.
        //
        // During the swirl / beyblade the player ticks damage many times a second. EnemyCombat
        // rate-limits its OWN flinch (further hits only flash), but this layer was re-crossfading
        // the reaction clip on every single one of those ticks, restarting it from frame 0 each
        // time. The boss therefore never got past the first few frames of the flinch: he looked
        // frozen, and the reaction read as stuttering. lastSeenState is the state as of the last
        // Update, so it is still the pre-hit state on the frame the flinch begins.
        if (lastSeenState == EnemyCombat.EnemyState.HitReact)
            return;

        string state = damage <= lightHitMaxDamage ? lightReactState : mediumReactState;
        if (string.IsNullOrEmpty(state)) return;
        if (!HasState(state)) return;

        animator.CrossFadeInFixedTime(state, reactCrossfade, 0);
        DebugLog($"react tier: {(damage <= lightHitMaxDamage ? "LIGHT" : "MEDIUM")} ({damage} dmg) → '{state}'");
    }

    // ─────────────────────────────────────────────────────────────────────── knockback ──

    private Coroutine knockbackRoutine;
    private float nextKnockbackAllowedTime;

    /// <summary>
    /// Shoves the Oni straight back from Yoru over a short window. Runs on UNSCALED time on
    /// purpose: a stumble that stretches to two real seconds because Yoru happens to be aiming
    /// looks like the boss is being pushed through treacle.
    /// </summary>
    private void ApplyKnockback(float distance)
    {
        if (distance <= 0.001f || playerT == null) return;

        Vector3 dir = transform.position - playerT.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        dir.Normalize();

        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(dir, distance, Mathf.Max(0.05f, knockbackDuration)));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 dir, float distance, float duration)
    {
        float t = 0f;
        float moved = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            // Ease-out: almost all of the travel happens in the first few frames, then it settles.
            // That front-loading is what makes it read as an impact rather than a shove.
            float target = distance * (1f - Mathf.Pow(1f - k, 3f));
            float step = target - moved;
            moved = target;

            // Move() respects the NavMesh and works even while the agent is stopped (which it is,
            // during Stagger/HitReact), so the Oni cannot be knocked through geometry or off the mesh.
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                navAgent.Move(dir * step);
            else
                transform.position += dir * step;

            yield return null;
        }

        knockbackRoutine = null;
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
