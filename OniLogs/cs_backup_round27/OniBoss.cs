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

    // Round 5: renamed on purpose (no FormerlySerializedAs). The old fields shipped with defaults that
    // assumed the two clips would be swapped in the controller; that step was dropped, so the scene
    // instance carried the mapping the wrong way round (light hits played the big react, medium hits
    // the tiny flinch). New names = fresh defaults from code, no inspector work.
    [Tooltip("Animator STATE for the quick flinch (light hits, 10 dmg paws). In ONICONTROLLER the state 'Hit_react_light' holds the 'Oni Hit react lighter light' clip: 0.79s, hips barely move — the small one.")]
    [SerializeField] private string quickFlinchState = "Hit_react_light";

    [Tooltip("Animator STATE for the full react (medium hits, 20 dmg strong paw / ground arrow). In ONICONTROLLER the state 'HitReact_medium' holds the 'Oni Hit react light' clip: 1.46s, hips 0.6m back and a 0.2m dip — the readable stumble. Heavy hits (35+) never come here: EnemyHealth's Stagger Damage Threshold (25) sends them to Stagger.")]
    [SerializeField] private string fullReactState = "HitReact_medium";

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

    [Header("Charge Travel Cancel (safety net)")]
    [Tooltip("The Oni Charge clip carries its 18m forward dash INSIDE the Hips bone (its import setting 'Root Motion Node' is set to the unmoving RootNode — the only clip in the project set that way — so Unity extracts no root motion and the hips travel is left in the pose). Left alone the hips carry the whole mesh 18m ahead of the NavMesh transform: that is the 'charges, vanishes, comes back' you saw. This pins the travel bone to the transform every frame the clip is on the animator (horizontal only; vertical bob and every rotation are untouched). The REAL fix is the import setting itself (see the round notes); with that applied this reads ~0 drift and does nothing.")]
    [SerializeField] private bool chargePinEnabled = true;
    [Tooltip("Animator STATE name of the charge attack clip.")]
    [SerializeField] private string chargeStateName = "Oni_Charge";
    [Tooltip("Name (or part of it) of the bone that carries the clip's travel. Only THIS bone is pinned — its children ride along, and bones with legitimate translation animation of their own (the club, Kanabo1, is a child of the hips and slides in the hand by animation) are left alone. Empty = find it automatically ('hips' / 'pelvis', then the body renderer's root bone).")]
    [SerializeField] private string chargeTravelBoneName = "Hips";
    [Tooltip("Horizontal drift (metres, world space) the travel bone may show before it is pulled back. Tiny on purpose: anything above sensor noise IS the baked travel.")]
    [SerializeField] private float chargeTravelCancelEpsilon = 0.01f;
    [Tooltip("Force every SkinnedMeshRenderer on the Oni to recompute its bounds from the bones each frame, so the mesh can never be culled while its bones are on screen. Trivial cost for one boss.")]
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

    [Header("Attack Armor (trades)")]
    [Tooltip("While he is swinging (Attack / Telegraph), light and medium hits deal their damage and flash him white but do NOT interrupt the swing — whoever connects first wins the trade, and a paw cannot stop a club. Heavy hits (EnemyHealth's Stagger Damage Threshold and above, and rapid-hit bursts) still stop him. Applied from code at Start; the shared engine keeps its old interruptible swings for every other enemy.")]
    [SerializeField] private bool armorDuringAttacks = true;

    [Header("Attack Step-In (his swings snap to Yoru)")]
    [Tooltip("Every melee swing drives him FORWARD into the swing while it winds up, so the club arrives where Yoru is instead of hitting air. Each combo step does it again — if the first swing came up short, the second one closes the gap. The charge has its own drive and is not affected.")]
    [SerializeField] private bool attackStepInEnabled = true;
    [Tooltip("Farthest he may travel during ONE swing, in metres. His attack range is 3.5m, so 3.5 here means a swing started at ~7m still lands.")]
    [SerializeField] private float attackStepInMaxDistance = 3.5f;
    [Tooltip("How fast he drives forward during the wind-up, m/s. Around 5 reads as a heavy committing step; above 8 starts to look like a second charge.")]
    [SerializeField] private float attackStepInSpeed = 5f;
    [Tooltip("He stops this far INSIDE attack range, so the strike is comfortably in reach rather than exactly on the edge.")]
    [SerializeField] private float attackStepInStopMargin = 0.5f;
    [Tooltip("The step only runs until this point of the swing clip — it must be over before the club connects (the strike moments are 0.32-0.55). 0.30 keeps the movement in the wind-up.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float attackStepInEndNormalizedTime = 0.30f;
    [Tooltip("How fast he may still turn toward Yoru WHILE stepping in (slow on purpose: he commits). 0 = no turning at all, he drives straight where he was already facing.")]
    [SerializeField] private float attackStepInTurnSpeed = 2.5f;

    [Header("Heavy Knock-Back React")]
    [Tooltip("Big hits (the 4-leg air shot, a swirl burst, anything the engine sends to Stagger) play the 'Oni Hit react Heavy' clip — body thrown 1.1m back with a deep dip — instead of the long fall-down Stagger clip. It reads as a real knock-back and he recovers sooner. The clip existed in the animator and nothing used it.")]
    [SerializeField] private bool heavyKnockbackReactEnabled = true;
    [Tooltip("Animator STATE for it.")]
    [SerializeField] private string heavyReactState = "HitReact_Heavy";
    [Tooltip("Crossfade into it, seconds.")]
    [SerializeField] private float heavyReactCrossfade = 0.06f;
    [Tooltip("Extra seconds he stays down after the clip ends (the punish window). The clip is 1.5s, so 0.3 gives ~1.8s total instead of the engine's 2.5s Stagger.")]
    [SerializeField] private float heavyReactExtraDownTime = 0.3f;

    [Header("Rapid-Hit Burst (jump swirl)")]
    [Tooltip("The aerial swirl lands many 10-dmg ticks in about a second. The engine flinches once and then only flashes for Hit React Cooldown, and the quick flinch clip is a head twitch — so the swirl reads as no reaction. This counts the ticks: past Burst Stumble Damage the flinch is upgraded to the full react clip; past Burst Stagger Damage he staggers and is pushed back — once per burst.")]
    [SerializeField] private bool burstEscalationEnabled = true;
    [Tooltip("REAL seconds of quiet that end a burst.")]
    [SerializeField] private float burstWindow = 0.8f;
    [Tooltip("Total burst damage (2+ hits) at which the quick flinch is upgraded to the full react clip.")]
    [SerializeField] private int burstStumbleDamage = 20;
    [Tooltip("Total burst damage (2+ hits) at which he staggers, with the heavy knockback. 0 = never stagger from a burst.")]
    [SerializeField] private int burstStaggerDamage = 30;

    [Header("Phase 2 Transition")]
    [Tooltip("At the phase flip (EnemyCombat: Has Phases / Phase Threshold) he drops whatever he is doing and plays the transition clip full length, untouchable, then comes back angrier. The engine only flips a flag; this is the visible part.")]
    [SerializeField] private bool phaseTransitionEnabled = true;
    [Tooltip("Animator STATE of the transition clip (Oni Phase transition: crouch, club raised, roar with the head thrown back, settle — 2.4s).")]
    [SerializeField] private string phaseTransitionState = "Phase_Transition";
    [Tooltip("Blend into the transition clip, seconds.")]
    [SerializeField] private float phaseTransitionBlend = 0.15f;
    [Tooltip("Playback speed of the transition clip (his Animator only, nothing else in the scene). The clip is 2.4s at 1; 0.6 stretches it to 4s so the roar can be seen. Restored afterwards.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float phaseTransitionAnimSpeed = 0.6f;
    [Tooltip("Seconds he holds the last pose after the clip has played out, before he comes back.")]
    [SerializeField] private float phaseTransitionHold = 0.6f;
    [Tooltip("No damage lands during the transition.")]
    [SerializeField] private bool phaseTransitionInvulnerable = true;
    [Tooltip("Normalized point of the clip where the roar peaks — camera shake and the ground ring fire here (measured from the FBX: club fully raised, head thrown back, 0.55-0.75).")]
    [Range(0f, 1f)]
    [SerializeField] private float phaseRoarNormalizedTime = 0.6f;
    [Tooltip("Camera shake at the roar (same scale as CombatFeedbackManager's hits; a heavy hit is ~0.5).")]
    [SerializeField] private float phaseRoarShakeIntensity = 0.6f;
    [SerializeField] private float phaseRoarShakeDuration = 0.5f;
    [Tooltip("Code-built ground ring at the roar (placeholder until real VFX exists), radius in metres. 0 = off.")]
    [SerializeField] private float phaseRoarRingRadius = 7f;

    [Header("Strike Moments (measured from the FBX files)")]
    [Tooltip("Overrides for the melee attacks' Strike Moment, applied at Start over the values on the attack list. Measured from the club's real travel in each clip: Club_Swing 0.55, ClubSwing2 0.32 (the scene's 0.5 resolved the hit ~0.3s AFTER the club had passed — that is the 'late reaction'), ClubSlam 0.52, KanaboSweep 0.48. Delete an entry to keep the attack list's own value.")]
    [SerializeField] private StrikeMomentOverride[] strikeMomentOverrides =
    {
        new StrikeMomentOverride("Club_Swing", 0.55f),
        new StrikeMomentOverride("ClubSwing2", 0.32f),
        new StrikeMomentOverride("ClubSlam", 0.52f),
        new StrikeMomentOverride("KanaboSweep", 0.48f),
    };

    [System.Serializable]
    public class StrikeMomentOverride
    {
        [Tooltip("Attack name (or attack animation) as on the EnemyCombat attack list.")]
        public string attack;
        [Range(0f, 1f)] public float strikeMoment = 0.5f;
        public StrikeMomentOverride() { }
        public StrikeMomentOverride(string attack, float strikeMoment) { this.attack = attack; this.strikeMoment = strikeMoment; }
    }

    [Header("Pace & Swing Wave — round 16")]
    [Tooltip("ROUND 16. Multiplies the Oni's animator speed, so every clip he plays runs faster. Safe for his damage timing: the engine fires each strike at a NORMALIZED point of the clip, so the hit stays at the same moment of the swing however fast it plays. 1 = untouched. 1.35 turns his 1.13s swing into 0.84s and his 1.58s swing into 1.17s.")]
    [SerializeField] private float oniAnimationSpeed = 1.35f;
    [Tooltip("ROUND 16. His chase speed, phase 1. Pushed at Start because the engine's value is a saved SerializeField. Yoru runs at 7, so he still cannot outrun her — he just stops falling so far behind. -1 leaves the engine value alone.")]
    [SerializeField] private float oniChaseSpeed = 4.5f;
    [Tooltip("ROUND 16. His chase speed in phase 2. -1 leaves the engine value alone.")]
    [SerializeField] private float oniChaseSpeedP2 = 5.5f;

    [Header("Swing Wave — round 16")]
    [Tooltip("ROUND 16. A shockwave released at the moment each swing strikes, so a swing that is just short of Yoru still reaches her. Measured over 28 of his swings, the club came within a metre of her on only 10 — the other 18 were guaranteed whiffs before they started, which is most of the dead air in the fight. This extends his threat in-world instead of making him magnetically follow her.")]
    [SerializeField] private bool swingWaveEnabled = true;
    [Tooltip("ROUND 24. One row per attack, so each swing can have its own effect. Give Club_Swing and ClubSwing2 the same prefab and they read as one move; give ClubSlam its own and it reads as a different one. Placement is per row too, because a slam wants its effect low and in front while a swing wants it at chest height. A row with no prefab falls back to Swing Wave VFX (Fallback) below. Match is by attack NAME or attack ANIMATION, whichever the row's text matches.")]
    [SerializeField] private SwingVFXBinding[] swingWaveVFXByAttack =
    {
        new SwingVFXBinding("Club_Swing"),
        new SwingVFXBinding("ClubSwing2"),
        new SwingVFXBinding("ClubSlam"),
        new SwingVFXBinding("KanaboSweep"),
    };

    [System.Serializable]
    public class SwingVFXBinding
    {
        [Tooltip("Attack name or attack animation, exactly as on the EnemyCombat attack list.")]
        public string attack;
        [Tooltip("Effect spawned at this attack's strike moment, in front of him — the swing itself. Empty = use the fallback below.")]
        public GameObject vfx;
        [Tooltip("Effect spawned ON YORU where the club actually meets her, and only when the hit lands. Empty = use the Hit Land VFX fallback below.")]
        public GameObject hitVFX;
        [Tooltip("Metres in front of him, along his facing.")]
        public float forward = 1.5f;
        [Tooltip("Height above his feet.")]
        public float height = 1.2f;
        [Tooltip("Seconds before it is destroyed.")]
        public float lifetime = 2f;
        public SwingVFXBinding() { }
        public SwingVFXBinding(string attack) { this.attack = attack; }
    }

    [Tooltip("Used for any attack with no row of its own above, or whose row has no prefab. Empty = that attack simply has no visual; the wave's damage is unaffected either way.")]
    [SerializeField] private GameObject swingWaveVFX;
    [Tooltip("Fallback: seconds before the spawned effect is destroyed.")]
    [SerializeField] private float swingWaveVFXLifetime = 2f;
    [Tooltip("Fallback: metres in front of him the effect is spawned, along his facing.")]
    [SerializeField] private float swingWaveVFXForward = 1.5f;
    [Tooltip("Fallback: height above his feet the effect is spawned.")]
    [SerializeField] private float swingWaveVFXHeight = 1.2f;

    [Tooltip("ROUND 25. Used for any attack whose row has no Hit VFX of its own. Spawned at the point where the club actually meets Yoru's body, only on swings that connect.")]
    [SerializeField] private GameObject hitLandVFX;
    [Tooltip("Seconds before a hit effect is destroyed.")]
    [SerializeField] private float hitLandVFXLifetime = 2f;
    [Tooltip("Nudge the hit effect this far back along the line from Yoru toward the club, metres. A small positive value keeps a flat effect from being buried inside her body.")]
    [SerializeField] private float hitLandVFXOffset = 0.1f;
    [Tooltip("ROUND 26. Measurement only, changes nothing. Logs where his club actually is at each strike moment — height above his feet, distance in front, sideways offset, and how far its own facing has swung from his — next to where the swing effect is currently being spawned, so the gap between the two is a number rather than a guess. Turn off once the effect is placed.")]
    [SerializeField] private bool logClubPositionAtStrike = true;
    [Tooltip("How far the wave reaches, metres, measured from him to Yoru. His club's own reach is 3.5m, so anything above that is the extra range the wave buys him. Only fires when the normal hit MISSED, so it can never double-dip.")]
    [SerializeField] private float swingWaveRadius = 5.5f;
    [Tooltip("Half-angle in front of him that the wave covers, degrees. 60 means a 120-degree arc; the wave never hits behind him.")]
    [SerializeField] private float swingWaveHalfAngle = 60f;
    [Tooltip("Damage the wave deals. Deliberately less than a clean club hit — being clipped by the wind of a swing should hurt less than being hit by the club.")]
    [SerializeField] private int swingWaveDamage = 8;

    private PlayerHealth playerHealthRef;
    private bool  waveFiredThisAttack;
    private string waveAttackName = "";

    [Header("Strike contact measurement — round 15 (diagnostic, temporary)")]
    [Tooltip("ROUND 15. Measures, at RUNTIME, the moment his club is physically closest to Yoru during each swing, and compares it to the Strike Moment the damage actually fires on. The existing moments were measured from the FBX clips, which shows where the club is in the animation but not where SHE was standing — so it cannot tell you whether the club reaches her at real play distance. Pure measurement: reads the skeleton, changes no behaviour. Untick when the numbers are in.")]
    [SerializeField] private bool measureStrikeContact = true;
    [Tooltip("Part of the club bone's name, case-insensitive. The DEEPEST bone matching this is used, so the measurement tracks the club's tip rather than its handle.")]
    [SerializeField] private string clubBoneNameContains = "kanabo";
    [Tooltip("Height above Yoru's feet treated as her body for the measurement, metres. 0.8 is roughly her chest on all fours.")]
    [SerializeField] private float strikeContactBodyHeight = 0.8f;

    private Transform clubBone;
    private bool   strikeMeasureActive;
    private string strikeMeasureAttack = "";
    private float  strikeMeasureMinDist = float.MaxValue;
    private float  strikeMeasureMinNorm = -1f;
    private float  strikeMeasureClipLen;

    [Header("Charge Drive (lance rush)")]
    [Tooltip("The Oni owns the charge himself instead of the shared engine's generic lunge. WINDUP: the clip plays in place while he turns to her (club comes forward). RUSH: the clip is frozen on the lance frame and he drives himself across the gap, steers briefly, commits, brakes next to Yoru. STRIKE: the clip JUMPS to its strike section (landing + club slam), so the baked dash is never played on the spot. If he is already next to her at the end of the wind-up the rush is skipped. Turning this off returns the charge to the old engine lunge.")]
    [SerializeField] private bool chargeDriveEnabled = true;
    [Tooltip("Normalized point in the Oni Charge clip frozen for the whole rush. Measured from the FBX: 0.20-0.22 = grounded, club forward-up; 0.26-0.28 = club dead level, small hop; 0.38-0.42 = airborne lunge, body leaning in, club level (default). Must be below Charge Strike Normalized Time.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float chargeHoldNormalizedTime = 0.40f;
    [Tooltip("Clip speed during the in-place wind-up (club coming forward). 1 = the attack's own speed; a little above 1 makes the telegraph snappier without changing the strike.")]
    [SerializeField] private float chargeWindupSpeed = 1.3f;
    [Tooltip("Normalized point the clip JUMPS to when he arrives (or when no rush was needed). Measured from the FBX: 0.58 = feet land, club at his side; 0.66-0.78 the club sweeps down; ~0.76 impact (club straight down); 0.80-1.00 he stands back up. Everything between the hold frame and this point (the baked dash) is skipped.")]
    [Range(0f, 0.98f)]
    [SerializeField] private float chargeStrikeNormalizedTime = 0.58f;
    [Tooltip("Blend seconds from the frozen lance pose into the strike section.")]
    [SerializeField] private float chargeStrikeBlend = 0.10f;
    [Tooltip("Strike Moment used for the charge INSTEAD of the value on the Oni_Charge attack entry, so the hit resolves on the club impact of the strike section (~0.76 in the FBX). -1 = leave the attack entry's own value (0.7 in the scene).")]
    [Range(-1f, 1f)]
    [SerializeField] private float chargeStrikeMoment = 0.76f;
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
    [Tooltip("Mirror the WHOLE console (every script's logs, warnings, errors, exceptions) plus charge telemetry into <project>/OniLogs/oni_<date>.log while playing. The folder sits next to Assets, so Unity ignores it. Keeps the newest 8 files. With this on there is nothing to copy or paste after a test — the file is read from disk.")]
    [SerializeField] private bool writeLogFile = true;
    [Tooltip("Telemetry lines per REAL second while a charge is active (file only, never the console).")]
    [SerializeField] private float chargeTelemetryHz = 12f;

    private EnemyCombat combat;
    private EnemyHealth health;
    private Animator animator;
    private Transform playerT;
    private UnityEngine.AI.NavMeshAgent navAgent;
    private float preChargeAcceleration = -1f;
    private SkinnedMeshRenderer bodyRenderer;   // biggest skinned mesh — visibility/bounds telemetry
    private static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

    private bool inWatchStance;   // current pre-combat stance (false = Idle)
    private bool barShown;
    private bool phase2Sent;

    // Rapid-hit burst state (unscaled clock)
    private float burstLastHitRealTime = -10f;
    private int burstDamage, burstTicks;
    private bool burstStaggerFired, burstUpgraded;

    // Phase-2 transition state
    private bool phaseTransitionDone, phaseTransitionActive, phaseRoarReached;
    private Coroutine phaseTransitionRoutine;

    // Attack step-in state
    private bool stepInActive;
    private float stepInTravelled;
    private string stepInAttack = "";


    // Reaction freeze guard + slow-motion watchdog state. All measured in UNSCALED time on
    // purpose: the whole point is to survive a world clock that is running at a tenth speed.
    private EnemyCombat.EnemyState lastSeenState;
    private float stateEnteredRealTime;
    private float slowSinceRealTime = -1f;
    private bool slowWarned;

    // Charge: pin (travel cancel) state
    private bool chargePinActive;          // the charge clip is on the animator (current or incoming state)
    private float chargeWaveTimer;
    private int chargeStateHash;
    private Transform travelBone;          // the bone that carries the clip's baked travel (Hips)
    private Vector3 travelBoneRestLocal;   // its local position in the rest pose (snapshotted at Start)
    private int travelBoneDepth = -1;
    private int pinFrames, pinCancelFrames;
    private float pinMaxRawDrift, pinMaxCancelled;
    private float telemetryNextRealTime;

    // Charge: drive state
    private ChargePhase chargePhase;
    private bool chargeFrozeClip;
    private float chargeStartRealTime;
    private float chargeRushStartRealTime;
    private Vector3 chargeTarget;

    // Warn about a missing animator state only once, same protection the engine uses.
    private readonly System.Collections.Generic.HashSet<string> missingStatesWarned =
        new System.Collections.Generic.HashSet<string>();

    [Header("Yoru — launch model (round 8, THIS SCENE ONLY)")]
    [Tooltip("Switches on PlayerCombat's round-8 launch model while this boss is in the scene. That script is SHARED by every fight, so it is flipped from here rather than defaulted on. Untick to compare against the old behaviour.")]
    [SerializeField] private bool configureYoruLaunch = true;

    // The numbers are deliberately CONSTANTS, not [SerializeField]s. A serialized field keeps the
    // value Unity saved the first time the script compiled, so changing a default in code silently
    // does nothing — that is exactly how the targeting cone stayed at 120 after it was "reverted".
    // Tune live on PlayerCombat during Play instead; these are only the values pushed at Start.
    private const float YORU_NUDGE_DISTANCE  = 0.9f;   // step forward with nothing to launch at
    // ROUND 9b. Engage only as far as she can actually ARRIVE. Max travel is
    // YORU_LAUNCH_SPEED * PlayerCombat's Launch Max Duration = 10 * 0.32 = 3.20m, and her strike
    // reaches attackRange 1.5m, so a launch still connects out to 3.20 + 1.5 = 4.70m of SURFACE
    // gap. Beyond that she cannot reach him even after a full launch: the 16:23 log has her
    // travelling the whole 3.20m from 5.3m and whiffing. Past 4.7m she steps forward instead.
    // Raise Launch Max Duration or Launch Speed and this number should move with them.
    private const float YORU_ENGAGE_DISTANCE = 4.7f;   // launch at anything within 4.7m of its SURFACE
    private const float YORU_LAUNCH_MIN      = 0f;     // NO minimum: the launch is the real gap
    private const float YORU_LAUNCH_STOP_GAP = 0f;     // all the way in; her capsule stops on his body
    private const float YORU_LAUNCH_SPEED    = 10f;    // 20 m/s covered 3m in 0.15s — too fast to see
    private const float YORU_CONE            = -1f;    // -1 = DO NOT TOUCH her targeting cone

    // ROUND 9.
    // AIR: Hazel's call — airborne attacks launch and step forward exactly like grounded ones.
    // EDGE LAYERS: the edge probe gets its own mask. Her prefab's Environment Mask is Ground only
    // (layer 8) and this cave's terrain is on Default (layer 0), so the probe found no floor
    // anywhere, called it a cliff, and killed 11 of 11 launches at 0.00m travelled. Environment
    // Mask is ALSO the line-of-sight mask, so it is left alone — widening it would let the cave
    // floor start blocking targeting. -1 = every layer, which is what a floor probe wants.
    private const bool YORU_LAUNCH_IN_AIR      = true;
    private const int  YORU_EDGE_GROUND_LAYERS = -1;   // ~0, every layer; 0 would mean "leave alone"

    private void Awake()
    {
        // First thing, before any other script logs: the on-disk mirror of the console.
        if (writeLogFile) OniDebugLogFile.Begin();

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

        // Round 8: the agreed launch model. It lives on PlayerCombat, which every fight shares, so
        // it is switched on HERE - this scene only - until it has been judged. Start-only lookup.
        if (configureYoruLaunch && playerT != null)
        {
            var yoru = playerT.GetComponent<PlayerCombat>();
            if (yoru == null) yoru = playerT.GetComponentInChildren<PlayerCombat>();
            if (yoru != null)
            {
                yoru.ConfigureLaunch(true, YORU_NUDGE_DISTANCE, YORU_ENGAGE_DISTANCE, YORU_CONE,
                                     YORU_LAUNCH_MIN, YORU_LAUNCH_STOP_GAP, YORU_LAUNCH_SPEED,
                                     YORU_LAUNCH_IN_AIR ? 1 : 0, YORU_EDGE_GROUND_LAYERS);
                DebugLog($"Yoru launch model ON: launches ALL THE WAY to an enemy within {YORU_ENGAGE_DISTANCE:F1}m of its surface at {YORU_LAUNCH_SPEED:F0}m/s (stop gap {YORU_LAUNCH_STOP_GAP:F2}m, NO minimum distance), else steps {YORU_NUDGE_DISTANCE:F2}m forward. Targeting cone left exactly as her prefab has it.");
                DebugLog($"Yoru launch ROUND 9: airborne attacks launch too ({(YORU_LAUNCH_IN_AIR ? "ON" : "off")}), edge probe now runs on its OWN layer mask (Everything) instead of the line-of-sight mask - the Ground-only mask found no floor in this cave and was cancelling 100% of launches at 0.00m travelled.");
            }
            else
            {
                Debug.LogWarning("[OniBoss] no PlayerCombat found on the player - Yoru's launch model stays OFF. Nothing else is affected.");
            }
        }

        // ROUND 16: pace. Animator speed is safe to scale because every strike fires on a
        // NORMALIZED clip position, so the hit keeps its place in the swing whatever the speed.
        if (oniAnimationSpeed > 0f && animator != null && !Mathf.Approximately(oniAnimationSpeed, 1f))
        {
            animator.speed = oniAnimationSpeed;
            DebugLog($"animator speed x{oniAnimationSpeed:F2} — his 1.13s swing now plays in {1.13f / oniAnimationSpeed:F2}s.");
        }
        if (combat != null && (oniChaseSpeed > 0f || oniChaseSpeedP2 > 0f))
        {
            combat.ConfigureChaseSpeed(oniChaseSpeed, oniChaseSpeedP2);
            DebugLog($"chase speed pushed: phase1 {oniChaseSpeed:F1}, phase2 {oniChaseSpeedP2:F1} (Yoru runs at 7).");
        }
        if (swingWaveEnabled && playerT != null)
        {
            playerHealthRef = playerT.GetComponent<PlayerHealth>();
            if (playerHealthRef == null) playerHealthRef = playerT.GetComponentInChildren<PlayerHealth>();
            DebugLog($"swing wave ON: {swingWaveDamage} dmg out to {swingWaveRadius:F1}m in a {swingWaveHalfAngle * 2f:F0}deg arc, "
                   + $"only when the club itself missed. VFX {(swingWaveVFX != null ? swingWaveVFX.name : "NOT SET — damage only")}. "
                   + $"PlayerHealth {(playerHealthRef != null ? "found" : "NOT FOUND — wave damage disabled")}.");
        }

        // ROUND 15: locate the club's TIP — the deepest bone whose name matches — so the distance
        // being measured is the business end, not the handle in his fist.
        // ROUND 25: found whenever EITHER the measurement or the swing wave needs it — the hit
        // effect is anchored to the club's tip, so it must not disappear when the diagnostic is
        // switched off at the end of the hunt.
        if ((measureStrikeContact || swingWaveEnabled) && !string.IsNullOrEmpty(clubBoneNameContains))
        {
            string needle = clubBoneNameContains.ToLowerInvariant();
            int bestDepth = -1;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.ToLowerInvariant().Contains(needle)) continue;
                int depth = 0;
                for (Transform w = t; w != null && w != transform; w = w.parent) depth++;
                if (depth > bestDepth) { bestDepth = depth; clubBone = t; }
            }
            if (clubBone != null)
                DebugLog($"strike contact measurement ON — tracking '{clubBone.name}' at depth {bestDepth}.");
            else
                Debug.LogWarning($"[OniBoss] strike contact measurement: no bone containing '{clubBoneNameContains}' under him. Measurement is off; nothing else is affected.");
        }

        // The mesh must never be culled while its bones are on screen (see Charge Travel Cancel).
        int smrCount = 0;
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            if (keepMeshVisibleWhenBonesTravel) smr.updateWhenOffscreen = true;
            if (bodyRenderer == null || (smr.bones != null && bodyRenderer.bones != null && smr.bones.Length > bodyRenderer.bones.Length))
                bodyRenderer = smr;
            smrCount++;
        }
        DebugLog($"skinned meshes: {smrCount} renderer(s), updateWhenOffscreen {(keepMeshVisibleWhenBonesTravel ? "ON" : "left alone")}, "
               + $"body renderer '{(bodyRenderer != null ? bodyRenderer.name : "none")}' rootBone '{(bodyRenderer != null && bodyRenderer.rootBone != null ? bodyRenderer.rootBone.name : "none")}'");

        // The travel bone (Hips) and its rest offset, read BEFORE the Animator has written a single
        // pose — this is the FBX bind pose, the cleanest possible reference for the pin.
        travelBone = FindTravelBone();
        if (travelBone != null)
        {
            Transform rigRoot = animator != null ? animator.transform : transform;
            travelBoneRestLocal = travelBone.localPosition;
            travelBoneDepth = 0;
            for (Transform t = travelBone; t != null && t != rigRoot; t = t.parent) travelBoneDepth++;
            DebugLog($"charge travel bone: '{travelBone.name}' at depth {travelBoneDepth} under '{rigRoot.name}', rest local {travelBoneRestLocal}");
        }
        else
        {
            Debug.LogWarning("[OniBoss] charge travel bone NOT found — the travel cancel is off. Set Charge Travel Bone Name to the hips bone's name.");
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
            if (chargeStrikeMoment >= 0f)
            {
                bool ok = combat.SetAttackStrikeMoment(chargeStateName, chargeStrikeMoment);
                if (!ok) Debug.LogWarning($"[OniBoss] no attack named / animated '{chargeStateName}' found on EnemyCombat — charge strike moment override not applied.");
            }
            DebugLog($"charge drive ON (windup to {chargeHoldNormalizedTime:F2} at x{chargeWindupSpeed}, rush {chargeSpeed}m/s stops at {chargeStopDistance}m, "
                   + $"steers {chargeTrackSeconds}s then commits, strike section from {chargeStrikeNormalizedTime:F2}, strike moment {(chargeStrikeMoment >= 0f ? chargeStrikeMoment.ToString("F2") : "engine")}, trail VFX {(chargeTrailVFX ? "on" : "off")})");
        }

        // Trades: light/medium hits during his swing flash and hurt but do not stop the club.
        if (armorDuringAttacks)
        {
            combat.SetAttackArmor(true);
            DebugLog("attack armor ON (his swings are not interrupted by light/medium hits; heavy hits and rapid-hit bursts still stop him)");
        }

        // Strike moments measured from the clips (see the tooltip) — the club connects when it looks like it does.
        if (strikeMomentOverrides != null && strikeMomentOverrides.Length > 0)
        {
            var applied = new System.Text.StringBuilder();
            foreach (var o in strikeMomentOverrides)
            {
                if (o == null || string.IsNullOrEmpty(o.attack)) continue;
                if (combat.SetAttackStrikeMoment(o.attack, o.strikeMoment)) applied.Append($" {o.attack}={o.strikeMoment:F2}");
                else Debug.LogWarning($"[OniBoss] strike moment override: no attack named/animated '{o.attack}' on EnemyCombat.");
            }
            if (applied.Length > 0) DebugLog($"strike moments overridden:{applied}");
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

#if UNITY_EDITOR
        EditorSanityChecks();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only, read-only checks of the animator wiring and the clip import settings, so the
    /// log states plain facts instead of guesses. Never changes an asset; each finding is one line.
    /// </summary>
    private void EditorSanityChecks()
    {
        try
        {
            var ac = animator != null ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
            if (ac == null || ac.layers.Length == 0) return;

            var clipByState = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var st in ac.layers[0].stateMachine.states)
                clipByState[st.state.name] = st.state.motion as AnimationClip;

            // 1) light/medium react states: the light one must be the shorter clip.
            if (clipByState.TryGetValue(quickFlinchState, out var lightClip) && clipByState.TryGetValue(fullReactState, out var medClip)
                && lightClip != null && medClip != null && lightClip.length > medClip.length + 0.05f)
            {
                Debug.LogWarning($"[OniBoss] react states look SWAPPED: Quick Flinch State '{quickFlinchState}' plays a {lightClip.length:F2}s clip "
                               + $"but Full React State '{fullReactState}' plays a {medClip.length:F2}s clip. Swap the two values on OniBoss so light hits get the short flinch.");
            }

            // 2) a state that plays another state's clip by mistake (KanaboSweep = Idle in the current controller).
            if (clipByState.TryGetValue("KanaboSweep", out var sweep) && clipByState.TryGetValue("Idle", out var idle) && sweep != null && sweep == idle)
                Debug.LogWarning("[OniBoss] animator state 'KanaboSweep' uses the SAME clip as 'Idle' — the sweep attack plays the idle pose. Drag the 'Oni Kanabo sweep' clip onto KanaboSweep's Motion field.");

            // 3) the charge clip's import: a Root Motion Node leaves the 18m dash inside the hips.
            if (clipByState.TryGetValue(chargeStateName, out var chargeClip) && chargeClip != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(chargeClip);
                var mi = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
                if (mi != null)
                {
                    if (!string.IsNullOrEmpty(mi.motionNodeName))
                        Debug.LogWarning($"[OniBoss] '{System.IO.Path.GetFileName(path)}': Root Motion Node = '{mi.motionNodeName}'. That setting keeps the clip's 18m dash inside the hips (the vanish). "
                                       + "Fix: select the file → Animation tab → Motion → Root Motion Node → None → Apply. The travel-cancel pin covers it until then.");
                    else
                        DebugLog($"charge clip '{System.IO.Path.GetFileName(path)}' imports with Root Motion Node = None (in place) — good.");
                }
            }

            // 4) vertical motion: with Bake Into Pose (Y) off, a clip's crouch/leap is removed and the feet float. Report only.
            var flat = new System.Collections.Generic.List<string>();
            foreach (var kv in clipByState)
            {
                if (kv.Value == null) continue;
                string path = UnityEditor.AssetDatabase.GetAssetPath(kv.Value);
                var mi = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
                if (mi == null) continue;
                var cas = mi.clipAnimations != null && mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
                if (cas == null || cas.Length == 0) continue;
                if (!cas[0].lockRootHeightY) flat.Add($"{kv.Key}({System.IO.Path.GetFileNameWithoutExtension(path)})");
            }
            if (flat.Count > 0)
                DebugLog($"clips importing with Root Transform Position (Y) NOT baked into pose ({flat.Count}): {string.Join(", ", flat)} — their hips' up/down motion is stripped (see round notes; optional).");
        }
        catch (System.Exception e)
        {
            DebugLog($"editor sanity checks skipped: {e.Message}");
        }
    }
#endif

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

        // The phase-2 roar borrows the Stagger window and ends it itself; give it room, but still
        // guard it — a blanket exemption is what let the 7.5s freeze go unnoticed.
        if (phaseTransitionActive) limit = 12f;

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
        KeepAnimationSpeed();       // ROUND 16
        UpdateAttackStepIn();
        UpdateSwingWave();          // ROUND 16
        UpdatePhaseTransition();
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
    /// <summary>
    /// ROUND 15. Per frame during a swing, how far the club's tip is from Yoru. Keeps the closest
    /// approach and the clip position it happened at; on the way out of the attack it prints that
    /// against the Strike Moment the damage fires on. If the club is closest at 0.41 and damage
    /// fires at 0.55, the hit lands after the club has already gone past — which is what "the
    /// reaction feels late" looks like from the outside, even though Yoru's own reaction is 0ms.
    /// Must run in LateUpdate: before the Animator writes the pose, the bone is a frame stale.
    /// </summary>
    private void UpdateStrikeContactMeasure()
    {
        if (!measureStrikeContact || clubBone == null || combat == null || playerT == null || animator == null) return;

        bool inAttack = combat.GetCurrentState() == EnemyCombat.EnemyState.Attack;
        string atk = inAttack ? combat.CurrentAttackName() : "";
        bool isCharge = inAttack && (atk == chargeStateName || combat.CurrentAttackAnim() == chargeStateName);

        if (!inAttack || isCharge) { FlushStrikeContactMeasure(); return; }

        if (!strikeMeasureActive || atk != strikeMeasureAttack)
        {
            FlushStrikeContactMeasure();
            strikeMeasureActive  = true;
            strikeMeasureAttack  = atk;
            strikeMeasureMinDist = float.MaxValue;
            strikeMeasureMinNorm = -1f;
        }

        if (animator.IsInTransition(0)) return;
        var cur = animator.GetCurrentAnimatorStateInfo(0);
        strikeMeasureClipLen = cur.length;

        Vector3 body = playerT.position + Vector3.up * strikeContactBodyHeight;
        float d = Vector3.Distance(clubBone.position, body);
        if (d < strikeMeasureMinDist)
        {
            strikeMeasureMinDist = d;
            strikeMeasureMinNorm = Mathf.Clamp01(cur.normalizedTime);
        }
    }

    private void FlushStrikeContactMeasure()
    {
        if (!strikeMeasureActive) return;
        strikeMeasureActive = false;
        if (strikeMeasureMinNorm < 0f || strikeMeasureMinDist >= float.MaxValue * 0.5f) return;

        float configured = -1f;
        if (strikeMomentOverrides != null)
        {
            foreach (var o in strikeMomentOverrides)
                if (o != null && o.attack == strikeMeasureAttack) { configured = o.strikeMoment; break; }
        }

        string verdict;
        if (configured < 0f)
        {
            verdict = "no override configured for this attack";
        }
        else
        {
            float deltaSec = (configured - strikeMeasureMinNorm) * strikeMeasureClipLen;
            verdict = Mathf.Abs(deltaSec) < 0.03f
                ? "MATCHED (within one or two frames)"
                : deltaSec > 0f
                    ? $"damage fires {deltaSec * 1000f:F0} ms AFTER the club is closest — it lands once the club has passed her"
                    : $"damage fires {-deltaSec * 1000f:F0} ms BEFORE the club is closest";
        }

        Debug.Log($"[OniBoss:Strike] {strikeMeasureAttack}: club tip closest to Yoru at {strikeMeasureMinDist:F2}m, "
                + $"clip position {strikeMeasureMinNorm:F2} | damage fires at "
                + $"{(configured >= 0f ? configured.ToString("F2") : "engine default")} | clip {strikeMeasureClipLen:F2}s "
                + $"=> {verdict}");
    }

    /// <summary>
    /// ROUND 16. Re-asserts his animator speed, because hitstop wipes it. CombatFeedbackManager
    /// freezes the enemy animator and then restores it with a hardcoded `speed = 1f` rather than
    /// the value it froze — so without this the pace increase would silently vanish the first time
    /// Yoru landed a hit, and never come back. Deliberately stands aside for the two cases that
    /// legitimately own the speed: an active hitstop (speed at or near 0) and the phase transition,
    /// which runs its roar at its own slower rate.
    /// </summary>
    private void KeepAnimationSpeed()
    {
        if (animator == null || oniAnimationSpeed <= 0f) return;
        if (phaseTransitionActive) return;                 // the roar owns the speed
        if (animator.speed < 0.05f) return;                // hitstop is running, leave it frozen
        if (!Mathf.Approximately(animator.speed, oniAnimationSpeed))
            animator.speed = oniAnimationSpeed;
    }

    /// <summary>
    /// ROUND 16 — swing wave. At the strike moment of a normal melee swing, release a shockwave.
    /// It only deals damage when the club itself did NOT reach her (she is beyond the engine's
    /// attack range), so it never stacks on top of a clean hit — it converts a guaranteed whiff
    /// into a glancing consequence. The charge is excluded; it owns its own impact.
    /// </summary>
    private void UpdateSwingWave()
    {
        if (!swingWaveEnabled || combat == null || playerT == null || animator == null) return;

        bool inAttack = combat.GetCurrentState() == EnemyCombat.EnemyState.Attack;
        string atk = inAttack ? combat.CurrentAttackName() : "";
        bool isCharge = inAttack && (atk == chargeStateName || combat.CurrentAttackAnim() == chargeStateName);

        if (!inAttack || isCharge) { waveFiredThisAttack = false; waveAttackName = ""; return; }

        if (atk != waveAttackName) { waveAttackName = atk; waveFiredThisAttack = false; }
        if (waveFiredThisAttack) return;
        if (animator.IsInTransition(0)) return;

        // Same moment the engine resolves the hit on.
        float strikeAt = 0.5f;
        if (strikeMomentOverrides != null)
        {
            foreach (var o in strikeMomentOverrides)
                if (o != null && o.attack == atk) { strikeAt = o.strikeMoment; break; }
        }
        if (Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime) < strikeAt) return;

        waveFiredThisAttack = true;

        // ROUND 26 — measurement only. The swing effect is currently spawned at a point measured
        // from his FEET (Swing Wave VFX Forward / Height), numbers that were guessed before anyone
        // knew his proportions. This prints where the club really is at the strike moment, in his
        // own local frame, so the correct anchor stops being a guess.
        if (logClubPositionAtStrike && clubBone != null)
        {
            Vector3 rel = clubBone.position - transform.position;
            float up      = rel.y;
            float forward = Vector3.Dot(rel, transform.forward);
            float right   = Vector3.Dot(rel, transform.right);
            Vector3 flat  = rel; flat.y = 0f;

            Vector3 clubFwdFlat = clubBone.forward; clubFwdFlat.y = 0f;
            float clubYaw = clubFwdFlat.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(transform.forward, clubFwdFlat.normalized, Vector3.up)
                : 0f;

            Debug.Log($"[OniBoss:ClubPos] {atk} at strike: club tip {up:F2}m up, {forward:F2}m in front, "
                    + $"{right:F2}m to his right ({flat.magnitude:F2}m out from his centre). "
                    + $"Club faces {clubYaw:F0}deg from his forward, pitch {clubBone.forward.y:F2}. "
                    + $"Effect currently spawns at {swingWaveVFXHeight:F2}m up / {swingWaveVFXForward:F2}m front "
                    + $"=> off by {up - swingWaveVFXHeight:F2}m up, {forward - swingWaveVFXForward:F2}m front.");
        }

        // ROUND 24: this attack's own effect if it has one, otherwise the fallback.
        GameObject prefab = swingWaveVFX;
        GameObject hitPrefab = hitLandVFX;
        float fwd = swingWaveVFXForward, hgt = swingWaveVFXHeight, life = swingWaveVFXLifetime;
        if (swingWaveVFXByAttack != null)
        {
            string anim = combat.CurrentAttackAnim();
            foreach (var b in swingWaveVFXByAttack)
            {
                if (b == null || string.IsNullOrEmpty(b.attack)) continue;
                if (b.attack != atk && b.attack != anim) continue;
                if (b.vfx != null) { prefab = b.vfx; fwd = b.forward; hgt = b.height; life = b.lifetime; }
                if (b.hitVFX != null) hitPrefab = b.hitVFX;
                break;
            }
        }

        if (prefab != null)
        {
            Vector3 at = transform.position + transform.forward * fwd + Vector3.up * hgt;
            GameObject fx = Instantiate(prefab, at, Quaternion.LookRotation(transform.forward));
            if (life > 0f) Destroy(fx, life);
        }

        if (playerHealthRef == null) return;

        Vector3 to = playerT.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        // The club got her — the engine already dealt its damage, so the wave stays visual only.
        if (dist <= combat.AttackRange())
        {
            SpawnHitLandVFX(hitPrefab);
            DebugLog($"swing wave: club reached her at {dist:F1}m, wave is visual only.");
            return;
        }

        if (dist > swingWaveRadius)
        {
            DebugLog($"swing wave: {dist:F1}m is beyond the wave's {swingWaveRadius:F1}m — nothing lands.");
            return;
        }

        float angle = Vector3.Angle(transform.forward, to);
        if (angle > swingWaveHalfAngle)
        {
            DebugLog($"swing wave: she is {angle:F0}deg off his facing, outside the {swingWaveHalfAngle:F0}deg arc.");
            return;
        }

        playerHealthRef.TakeDamage(swingWaveDamage, false, transform.position, false);
        DebugLog($"swing wave HIT for {swingWaveDamage} at {dist:F1}m ({angle:F0}deg) — the club fell short at {combat.AttackRange():F1}m.");
    }

    /// <summary>
    /// ROUND 25. Puts the hit effect where the club actually meets her, rather than at her centre
    /// or at some fixed offset. The club's tip bone is already tracked for the strike measurement,
    /// so at the strike moment its world position is known; asking her capsule for the closest
    /// point to it gives the exact spot on her body the club arrives at — which moves with where
    /// she is standing and which side the swing came in from. The effect is rotated to face back
    /// along the club's approach, so a directional prefab sprays outwards rather than into her.
    /// Falls back to a point between the two of them at chest height if the club bone is missing.
    /// </summary>
    private void SpawnHitLandVFX(GameObject prefab)
    {
        if (prefab == null || playerT == null) return;

        Vector3 from = clubBone != null
            ? clubBone.position
            : transform.position + transform.forward * 1.2f + Vector3.up * swingWaveVFXHeight;

        Vector3 contact;
        Collider body = playerT.GetComponent<Collider>();
        if (body != null && body.enabled)
        {
            contact = body.ClosestPoint(from);
            // ClosestPoint returns the query point itself when it is already inside the collider.
            if ((contact - from).sqrMagnitude < 0.0001f)
                contact = playerT.position + Vector3.up * strikeContactBodyHeight;
        }
        else
        {
            contact = playerT.position + Vector3.up * strikeContactBodyHeight;
        }

        Vector3 approach = from - contact;
        Quaternion rot = approach.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(approach.normalized)
            : Quaternion.LookRotation(-transform.forward);

        GameObject fx = Instantiate(prefab, contact + approach.normalized * hitLandVFXOffset, rot);
        if (hitLandVFXLifetime > 0f) Destroy(fx, hitLandVFXLifetime);
    }

    private void LateUpdate()
    {
        UpdateChargePin();
        UpdateStrikeContactMeasure();   // ROUND 15 diagnostic — needs the posed skeleton
    }

    // ───────────────────────────────────────────────────────── charge: pin + drive ──

    /// <summary>
    /// True while the charge clip is on layer 0 as the CURRENT or the INCOMING state. Read from the
    /// Animator only — not from the engine state — so the pin also covers the frames where the clip
    /// is blending out into Recovery or a flinch (the engine has already moved on, the clip has not).
    /// </summary>
    private bool ChargeClipOnAnimator()
    {
        if (animator == null) return false;
        if (chargeStateHash == 0) chargeStateHash = Animator.StringToHash(chargeStateName);

        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == chargeStateHash) return true;
        return animator.IsInTransition(0)
            && animator.GetNextAnimatorStateInfo(0).shortNameHash == chargeStateHash;
    }

    /// <summary>Normalized time of the charge clip, transition-aware (reads the incoming instance while blending in).</summary>
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
    /// the charge clip is on the animator.
    ///
    /// Two jobs:
    ///
    /// 1. TRAVEL CANCEL (safety net). The Oni Charge clip carries an 18m dash inside its Hips bone
    ///    (see the tooltip on Charge Pin Enabled). Every frame the travel bone's horizontal drift
    ///    from its rest offset is measured in world space and pulled back, so the mesh sits on the
    ///    NavMesh transform whatever the clip does. Only the travel bone is touched: its children
    ///    ride along, and the club (a child of the hips with translation animation of its own) is
    ///    left alone. If the clip's import setting is fixed this measures ~0 and does nothing.
    ///
    /// 2. THE LANCE RUSH, three phases, only while the engine is in its Attack state:
    ///    WINDUP — clip plays in place, turning toward Yoru: the club comes forward.
    ///    RUSH   — at the hold frame the clip is FROZEN (AnimSpeed 0 — the multiplier the attack
    ///             states are bound to) and the boss drives himself across the gap: steers briefly,
    ///             commits, brakes at Charge Stop Distance. Skipped if he is already that close.
    ///    STRIKE — the clip JUMPS to its strike section (landing + club slam), so the baked dash is
    ///             never played on the spot, and the engine's Strike Moment resolves the damage.
    ///
    /// Everything it sees goes to the log file (telemetry lines) so a test needs no guessing.
    /// </summary>
    private void UpdateChargePin()
    {
        if (animator == null) return;

        bool clipOn = ChargeClipOnAnimator();
        bool engineAttacking = combat != null && combat.GetCurrentState() == EnemyCombat.EnemyState.Attack;

        if (clipOn && !chargePinActive)       BeginCharge(engineAttacking);
        else if (!clipOn && chargePinActive)  EndCharge("clip left the animator");

        if (!chargePinActive) return;

        // ── the drive ────────────────────────────────────────────────────────────────────
        if (chargePhase != ChargePhase.None)
        {
            if (!engineAttacking) AbortDrive($"engine left Attack (now {(combat != null ? combat.GetCurrentState().ToString() : "no engine")}) during {chargePhase}");
            else DriveCharge();
        }

        // ── travel cancel ────────────────────────────────────────────────────────────────
        float rawDrift = 0f, cancelled = 0f;
        if (travelBone != null && travelBone.parent != null)
        {
            Vector3 worldRest = travelBone.parent.TransformPoint(travelBoneRestLocal);
            Vector3 drift = travelBone.position - worldRest;
            drift.y = 0f;                                   // keep the vertical bob / crouch / leap
            rawDrift = drift.magnitude;
            if (rawDrift > pinMaxRawDrift) pinMaxRawDrift = rawDrift;

            if (chargePinEnabled && rawDrift > chargeTravelCancelEpsilon)
            {
                travelBone.position -= drift;               // children follow
                cancelled = rawDrift;
                if (cancelled > pinMaxCancelled) pinMaxCancelled = cancelled;
                pinCancelFrames++;
            }
        }
        pinFrames++;

        ChargeTelemetry(rawDrift, cancelled);
    }

    private void BeginCharge(bool engineAttacking)
    {
        chargePinActive = true;
        chargeStartRealTime = Time.unscaledTime;
        chargeRushStartRealTime = -1f;
        chargeFrozeClip = false;
        chargeWaveTimer = 0f;
        pinFrames = 0; pinCancelFrames = 0; pinMaxRawDrift = 0f; pinMaxCancelled = 0f;
        telemetryNextRealTime = 0f;

        chargePhase = (chargeDriveEnabled && engineAttacking) ? ChargePhase.Windup : ChargePhase.None;
        if (chargePhase == ChargePhase.Windup)
        {
            if (combat != null && chargeWindupSpeed > 0f)
            {
                combat.SetAttackAnimSpeed(chargeWindupSpeed);   // snappier telegraph; restored on strike / exit
                chargeFrozeClip = true;                        // "we touched AnimSpeed" — cleanup restores it
            }
            chargeTarget = playerT != null ? playerT.position : transform.position + transform.forward * 5f;
            if (navAgent != null)
            {
                preChargeAcceleration = navAgent.acceleration;
                navAgent.acceleration = chargeAcceleration;
            }
        }

        OniDebugLogFile.Marker("CHARGE");
        string boneInfo = travelBone != null
            ? $"'{travelBone.name}' depth {travelBoneDepth}, local now {travelBone.localPosition} vs rest {travelBoneRestLocal}"
            : "NONE";
        DebugLog($"charge begin: travel bone {boneInfo}; pin {(chargePinEnabled ? "ON" : "off")}; "
               + $"drive {(chargePhase == ChargePhase.Windup ? "windup→rush→strike" : chargeDriveEnabled ? "off (engine not in Attack)" : "off")}; "
               + $"engine {(combat != null ? combat.GetCurrentState().ToString() : "?")}, AnimSpeed {animator.GetFloat(AnimSpeedHash):F2}, "
               + $"clip at {ChargeNormalizedTime():F2}, dist {(playerT != null ? FlatDistance(transform.position, playerT.position) : -1f):F1}m");
    }

    private void EndCharge(string reason)
    {
        if (chargePhase != ChargePhase.None) AbortDrive(reason);
        chargePinActive = false;

        // The whole verdict on the travel cancel in one line — this is what to look for after a test.
        DebugLog($"charge end ({reason}) after {Time.unscaledTime - chargeStartRealTime:F2}s real: pin measured {pinFrames} frames, "
               + $"cancelled on {pinCancelFrames}, max raw drift {pinMaxRawDrift:F2}m, max cancelled {pinMaxCancelled:F2}m"
               + (pinMaxRawDrift < 0.05f ? " — clip is IN PLACE (import fix active or no travel), pin idle" : pinCancelFrames > 0 ? " — travel WAS baked in, pin held it" : " — travel present but NOT cancelled (pin off?)"));
    }

    /// <summary>Stops the drive early (interrupt, clip gone) and undoes everything it touched.</summary>
    private void AbortDrive(string reason)
    {
        if (chargePhase == ChargePhase.None) return;
        // Leaving Attack during the strike section is the normal end (the engine went to Recovery);
        // anything earlier is an interrupt worth a clearer line.
        DebugLog(chargePhase == ChargePhase.Strike ? $"charge complete: {reason}" : $"charge INTERRUPTED: {reason}");
        chargePhase = ChargePhase.None;
        strikeJumpVerifyAtRealTime = -1f;

        if (chargeFrozeClip && combat != null) combat.SetAttackAnimSpeed(1f);   // never leave the clip frozen
        chargeFrozeClip = false;

        if (navAgent != null && preChargeAcceleration > 0f)
        {
            navAgent.acceleration = preChargeAcceleration;
            preChargeAcceleration = -1f;
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
                StopAgent();
                TurnToward(playerT != null ? playerT.position : chargeTarget, chargeTurnSpeed);

                if (ChargeNormalizedTime() >= chargeHoldNormalizedTime)
                {
                    chargeTarget = playerT != null ? playerT.position : chargeTarget;
                    float d0 = FlatDistance(transform.position, chargeTarget);

                    if (d0 <= chargeStopDistance)
                    {
                        // Already on top of her (a close-range opener): no rush, straight to the strike.
                        BeginStrike($"already {d0:F1}m from Yoru at the hold frame, rush skipped");
                    }
                    else
                    {
                        combat.SetAttackAnimSpeed(0f);          // freeze on the lance frame
                        chargeFrozeClip = true;
                        chargePhase = ChargePhase.Rush;
                        chargeRushStartRealTime = now;
                        DebugLog($"charge RUSH: clip frozen at {ChargeNormalizedTime():F2}, {d0:F1}m to go at {chargeSpeed}m/s");
                    }
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

                bool arrived = d <= chargeStopDistance + 0.05f;
                bool timedOut = t >= chargeMaxTravelSeconds;

                if (!arrived && !timedOut)
                {
                    if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && d > 0.001f)
                    {
                        navAgent.isStopped = true;                                          // we drive it ourselves
                        float step = Mathf.Min(chargeSpeed * Time.deltaTime, d - chargeStopDistance);   // never overshoot the brake point
                        if (step > 0f) navAgent.Move(to.normalized * step);
                    }
                    combat.HoldAttackSafety();                                              // a long rush is not a hung state

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

                StopAgent();
                BeginStrike(arrived
                    ? $"arrived {d:F1}m from the lock point after {t:F2}s real"
                    : $"timed out after {t:F2}s at {d:F1}m");
                return;
            }

            case ChargePhase.Strike:
                // Committed. No turning, no moving; the travel cancel keeps the mesh planted.
                StopAgent();
                VerifyStrikeJump();
                return;
        }
    }

    /// <summary>
    /// Arrived (or never needed to travel): release the clip at the attack's own speed and JUMP it
    /// to the strike section. The dash frames between the hold frame and the strike are skipped —
    /// they were only ever a running-on-the-spot with the mesh pinned.
    /// </summary>
    private void BeginStrike(string reason)
    {
        combat.SetAttackAnimSpeed(combat.CurrentAttackSpeed());
        chargeFrozeClip = false;
        chargePhase = ChargePhase.Strike;
        strikeJumpVerifyAtRealTime = -1f;

        float target = Mathf.Clamp01(chargeStrikeNormalizedTime);
        float here = ChargeNormalizedTime();
        if (here < target - 0.02f)
        {
            // Same state, later point. CrossFade takes a NORMALIZED offset. (CrossFadeInFixedTime's
            // offset is in SECONDS — the first round-5 test used it with 0.58 and landed at 0.28, so
            // the whole dash played on the spot before the slam.) The blend length is given relative
            // to the clip length; a check a moment later hard-sets the time if the jump did not take.
            float clipLen = ChargeClipLengthSeconds();
            float blendNorm = clipLen > 0.05f ? Mathf.Max(0.02f, chargeStrikeBlend) / clipLen : 0.05f;
            animator.CrossFade(chargeStateHash, blendNorm, 0, target);
            strikeJumpTarget = target;
            strikeJumpVerifyAtRealTime = Time.unscaledTime + 0.25f;
        }

        combat.HoldAttackSafety();   // fresh window for the strike section (its length is a fraction of the clip)

        DebugLog($"charge STRIKE: {reason} — clip {here:F2} → {target:F2} (blend {chargeStrikeBlend:F2}s), "
               + $"strike moment {(chargeStrikeMoment >= 0f ? chargeStrikeMoment.ToString("F2") : "engine value")}, "
               + $"Yoru {(playerT != null ? FlatDistance(transform.position, playerT.position) : -1f):F1}m away");
    }

    private float strikeJumpVerifyAtRealTime = -1f;
    private float strikeJumpTarget;

    /// <summary>Raw length in seconds of the clip on the charge state (independent of AnimSpeed).</summary>
    private float ChargeClipLengthSeconds()
    {
        if (animator == null) return 2.083f;
        var infos = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == chargeStateHash
            ? animator.GetNextAnimatorClipInfo(0)
            : animator.GetCurrentAnimatorClipInfo(0);
        for (int i = 0; i < infos.Length; i++)
            if (infos[i].clip != null && infos[i].clip.length > 0.05f) return infos[i].clip.length;
        return 2.083f;   // the Oni Charge clip's real length — only reached if the clip info is unreadable
    }

    /// <summary>A moment after the jump: if the clip is still before the strike section, hard-set it.</summary>
    private void VerifyStrikeJump()
    {
        if (strikeJumpVerifyAtRealTime < 0f || Time.unscaledTime < strikeJumpVerifyAtRealTime) return;
        strikeJumpVerifyAtRealTime = -1f;

        float n = ChargeNormalizedTime();
        if (n < strikeJumpTarget - 0.03f)
        {
            animator.Play(chargeStateHash, 0, strikeJumpTarget);
            DebugLog($"charge STRIKE: jump did not take (clip at {n:F2}, wanted {strikeJumpTarget:F2}) — hard-set");
        }
    }

    /// <summary>File-only telemetry at Charge Telemetry Hz while the clip is active.</summary>
    private void ChargeTelemetry(float rawDrift, float cancelled)
    {
        if (!OniDebugLogFile.IsOpen || chargeTelemetryHz <= 0f) return;
        if (Time.unscaledTime < telemetryNextRealTime) return;
        telemetryNextRealTime = Time.unscaledTime + 1f / chargeTelemetryHz;

        var cur = animator.GetCurrentAnimatorStateInfo(0);
        bool inTr = animator.IsInTransition(0);
        var nxt = inTr ? animator.GetNextAnimatorStateInfo(0) : cur;
        string curName = cur.shortNameHash == chargeStateHash ? "Charge" : $"h{cur.shortNameHash}";
        string nxtName = !inTr ? "-" : (nxt.shortNameHash == chargeStateHash ? "Charge" : $"h{nxt.shortNameHash}");

        Vector3 meshOffset = Vector3.zero;
        if (travelBone != null) { meshOffset = travelBone.position - transform.position; meshOffset.y = 0f; }

        string vis = "-";
        if (bodyRenderer != null)
        {
            Vector3 bc = bodyRenderer.bounds.center - transform.position; bc.y = 0f;
            vis = $"{(bodyRenderer.isVisible ? "vis" : "CULLED")} bounds+{bc.magnitude:F1}m";
        }

        float dist = playerT != null ? FlatDistance(transform.position, playerT.position) : -1f;
        float vel = navAgent != null ? navAgent.velocity.magnitude : 0f;

        OniDebugLogFile.Line(
            $"charge {chargePhase,-6} n={ChargeNormalizedTime():F2} spd={animator.GetFloat(AnimSpeedHash):F2} mult={cur.speedMultiplier:F2} "
          + $"st={(combat != null ? combat.GetCurrentState().ToString() : "?")} anim={curName}/{nxtName} "
          + $"drift={rawDrift:F2} cut={cancelled:F2} meshOff={meshOffset.magnitude:F2}m {vis} "
          + $"pos=({transform.position.x:F1},{transform.position.z:F1}) dist={dist:F1} vel={vel:F1}");
    }

    private void StopAgent()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
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
    /// The bone that carries the clip's baked travel. Exact name match first (inspector value, then
    /// 'hips' / 'pelvis'), then a contains-match, shallowest wins; then the biggest skinned mesh's
    /// root bone; then the top-level child with the most descendants (the skeleton).
    /// </summary>
    private Transform FindTravelBone()
    {
        Transform root = animator != null ? animator.transform : transform;

        var wanted = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(chargeTravelBoneName)) wanted.Add(chargeTravelBoneName.Trim().ToLowerInvariant());
        wanted.Add("hips");
        wanted.Add("pelvis");

        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (bool exact in new[] { true, false })
        {
            foreach (string w in wanted)
            {
                Transform best = null; int bestDepth = int.MaxValue;
                foreach (Transform t in all)
                {
                    if (t == root) continue;
                    string n = t.name.ToLowerInvariant();
                    bool hit = exact ? n == w : n.Contains(w);
                    if (!hit) continue;
                    int d = 0;
                    for (Transform p = t; p != null && p != root; p = p.parent) d++;
                    if (d < bestDepth) { best = t; bestDepth = d; }
                }
                if (best != null) return best;
            }
        }

        SkinnedMeshRenderer big = null;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.bones != null && (big == null || smr.bones.Length > big.bones.Length)) big = smr;
        if (big != null && big.rootBone != null && big.rootBone != root) return big.rootBone;

        Transform deep = null; int most = -1;
        for (int i = 0; i < root.childCount; i++)
        {
            int n = root.GetChild(i).GetComponentsInChildren<Transform>(true).Length;
            if (n > most) { most = n; deep = root.GetChild(i); }
        }
        return deep;
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

        // RAPID-HIT BURST — the jump swirl. May end in a stagger, in which case nothing below applies.
        if (burstEscalationEnabled && UpdateBurst(damage)) return;

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

        // Big single hits — the 4-leg air tail shot, a heavy paw, anything at or above EnemyHealth's
        // Stagger Damage Threshold — land the engine in Stagger. Give them the knock-back clip.
        if (combat.GetCurrentState() == EnemyCombat.EnemyState.Stagger)
        {
            PlayHeavyKnockback($"{damage} dmg{(isHeavy ? " HEAVY" : "")}");
            return;
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

        string state = damage <= lightHitMaxDamage ? quickFlinchState : fullReactState;
        if (string.IsNullOrEmpty(state)) return;
        if (!HasState(state)) return;

        animator.CrossFadeInFixedTime(state, reactCrossfade, 0);
        DebugLog($"react tier: {(damage <= lightHitMaxDamage ? "LIGHT" : "MEDIUM")} ({damage} dmg) → '{state}'");
    }

    // ─────────────────────────────────────────────────────── attack step-in (snap) ──

    /// <summary>
    /// Drives him forward into his own swing while it winds up, so a club that started a little too
    /// far away still arrives on Yoru instead of on air. Runs for every melee attack EXCEPT the
    /// charge (that one has its own drive), and re-runs on every combo step, which is what makes a
    /// combo close the gap when its first swing came up short.
    ///
    /// The movement stops well before the strike frame, and the turn allowed while it runs is slow,
    /// so the swing is still committed: side-stepping it in the last moments still beats him.
    /// </summary>
    private void UpdateAttackStepIn()
    {
        if (!attackStepInEnabled || combat == null || playerT == null || animator == null) return;

        if (combat.GetCurrentState() != EnemyCombat.EnemyState.Attack)
        {
            stepInActive = false;
            return;
        }

        // The charge owns its own movement.
        string atk = combat.CurrentAttackName();
        if (atk == chargeStateName || combat.CurrentAttackAnim() == chargeStateName) { stepInActive = false; return; }

        // New attack (or a new combo step) → fresh budget.
        if (!stepInActive || atk != stepInAttack)
        {
            stepInActive = true;
            stepInAttack = atk;
            stepInTravelled = 0f;
        }

        if (stepInTravelled >= attackStepInMaxDistance) return;

        // Only during the wind-up. AttackClipProgress lives in the engine, so read the animator
        // directly: the attack states are the ones playing right now.
        var cur = animator.GetCurrentAnimatorStateInfo(0);
        if (!animator.IsInTransition(0) && cur.normalizedTime > attackStepInEndNormalizedTime) return;

        Vector3 to = playerT.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        float stopAt = Mathf.Max(0.5f, combat.AttackRange() - attackStepInStopMargin);
        if (dist <= stopAt) return;

        if (attackStepInTurnSpeed > 0f) TurnToward(playerT.position, attackStepInTurnSpeed);

        float step = Mathf.Min(attackStepInSpeed * Time.deltaTime,
                               dist - stopAt,
                               attackStepInMaxDistance - stepInTravelled);
        if (step <= 0.0001f) return;

        Vector3 dir = to / Mathf.Max(0.0001f, dist);
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;          // the engine parks the agent during Attack; we push it
            navAgent.Move(dir * step);
        }
        else transform.position += dir * step;

        stepInTravelled += step;
        if (stepInTravelled >= attackStepInMaxDistance || dist - step <= stopAt)
            DebugLog($"step-in: '{atk}' closed {stepInTravelled:F2}m, Yoru now {Mathf.Max(0f, dist - step):F1}m away (range {combat.AttackRange():F1})");
    }

    // ─────────────────────────────────────────────────────── heavy knock-back react ──

    /// <summary>
    /// Swaps the engine's long fall-down Stagger clip for the heavy knock-back clip on the hits that
    /// deserve it, and re-times the window to that clip. Only on a FRESH entry into Stagger, so the
    /// punish hits that land while he is already down cannot restart it.
    /// </summary>
    private void PlayHeavyKnockback(string why)
    {
        if (!heavyKnockbackReactEnabled || animator == null || combat == null) return;
        if (phaseTransitionActive) return;                       // the roar owns this window
        if (combat.GetCurrentState() != EnemyCombat.EnemyState.Stagger) return;
        if (lastSeenState == EnemyCombat.EnemyState.Stagger) return;   // already down: no restart
        if (string.IsNullOrEmpty(heavyReactState) || !HasState(heavyReactState)) return;

        animator.CrossFadeInFixedTime(heavyReactState, heavyReactCrossfade, 0);

        float len = StateClipLength(Animator.StringToHash(heavyReactState));
        float window = (len > 0.05f ? len : 1.5f) + Mathf.Max(0f, heavyReactExtraDownTime);
        combat.SetStaggerTimer(window);

        DebugLog($"heavy knock-back react ({why}) → '{heavyReactState}', down for {window:F2}s");
    }

    // ──────────────────────────────────────────────────────────── rapid-hit burst ──

    /// <summary>
    /// Counts hits inside a short real-time window (the aerial swirl ticks 10 dmg many times a
    /// second). Two-plus hits past Burst Stumble Damage upgrade the quick flinch to the full react;
    /// past Burst Stagger Damage he staggers with the heavy knockback — once per burst.
    /// Returns true when it staggered him (the caller skips its own tier handling).
    /// </summary>
    private bool UpdateBurst(int damage)
    {
        float now = Time.unscaledTime;
        if (now - burstLastHitRealTime > burstWindow)
        {
            burstDamage = 0; burstTicks = 0;
            burstStaggerFired = false; burstUpgraded = false;
        }
        burstLastHitRealTime = now;
        burstDamage += damage;
        burstTicks++;
        if (burstTicks < 2) return false;                        // a single big hit is the engine's business

        var s = combat.GetCurrentState();
        if (s == EnemyCombat.EnemyState.Dead || phaseTransitionActive) return false;

        if (burstStaggerDamage > 0 && burstDamage >= burstStaggerDamage && !burstStaggerFired
            && s != EnemyCombat.EnemyState.Stagger)
        {
            burstStaggerFired = true;
            combat.TriggerStagger();
            PlayHeavyKnockback($"swirl burst, {burstTicks} hits / {burstDamage} dmg");
            nextKnockbackAllowedTime = 0f;                        // the knockdown push always goes through
            ApplyKnockback(knockbackHeavy);
            nextKnockbackAllowedTime = now + Mathf.Max(0f, knockbackMinInterval);
            DebugLog($"burst: {burstTicks} hits / {burstDamage} dmg inside {burstWindow:F1}s → knock-back + {knockbackHeavy:F2}m push");
            return true;
        }

        if (burstDamage >= burstStumbleDamage && !burstUpgraded && s == EnemyCombat.EnemyState.HitReact
            && animator != null && !string.IsNullOrEmpty(fullReactState) && HasState(fullReactState))
        {
            burstUpgraded = true;
            animator.CrossFadeInFixedTime(fullReactState, reactCrossfade, 0);
            DebugLog($"burst: {burstTicks} hits / {burstDamage} dmg → flinch upgraded to '{fullReactState}'");
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────── phase-2 transition ──

    /// <summary>
    /// The engine only flips isPhase2 at the threshold. This turns the flip into a beat: drop the
    /// current action (the engine's Stagger window is borrowed for it), play the transition clip
    /// full length inside that window, no damage lands, roar feedback at the peak, then the engine
    /// resumes on its own when the window ends. Fires once per fight.
    /// </summary>
    private void UpdatePhaseTransition()
    {
        if (!phaseTransitionEnabled || phaseTransitionDone || combat == null || animator == null) return;
        if (!combat.IsPhase2()) return;

        phaseTransitionDone = true;
        var s = combat.GetCurrentState();
        if (s == EnemyCombat.EnemyState.Dead) return;
        if (!HasState(phaseTransitionState)) { phaseRoarReached = true; return; }

        if (phaseTransitionRoutine != null) StopCoroutine(phaseTransitionRoutine);
        phaseTransitionRoutine = StartCoroutine(PhaseTransitionRoutine());
    }

    /// <summary>
    /// The transition is DRIVEN, not played: every frame the clip's time is written by hand from a
    /// real-time clock (animator.Play with a normalized time), and the routine ends the engine's
    /// window itself. Round 6 tried the obvious way — animator.speed 0.6 plus the engine's stagger
    /// timer — and the log showed why that cannot work here:
    ///   • the hitstop from the hit that triggered phase 2 restores animator.speed to what it saw
    ///     (1) a moment later, so the clip was never slowed at all (the roar landed at 1.48s = 60%
    ///     of the clip at full speed);
    ///   • AnimatorStateInfo.length ALREADY accounts for animator.speed, so sizing the window from
    ///     it divided by the slow factor a second time: a 7.3s window around a 2.4s animation. That
    ///     five-second gap, standing in the last pose, was the freeze.
    /// Writing the time directly is immune to both, and to Yoru's slow-motion.
    /// </summary>
    private System.Collections.IEnumerator PhaseTransitionRoutine()
    {
        phaseTransitionActive = true;
        phaseRoarReached = false;
        bool wasInvulnerable = health != null && health.IsInvulnerable;
        if (phaseTransitionInvulnerable && health != null) health.SetInvulnerable(true);

        // Borrow the engine's Stagger state: it stops the agent and cancels the attack/combo/grab.
        // Its timer is topped up every frame below and the state is ended here, so the window can
        // never outlive the animation.
        combat.TriggerStagger(3f);
        int hash = Animator.StringToHash(phaseTransitionState);
        animator.CrossFadeInFixedTime(hash, Mathf.Max(0.02f, phaseTransitionBlend), 0, 0f);

        float speed = Mathf.Clamp(phaseTransitionAnimSpeed, 0.2f, 1.5f);
        float hold = Mathf.Max(0f, phaseTransitionHold);
        float startReal = Time.unscaledTime;
        float clipLen = -1f;
        bool roared = false, driving = false;

        DebugLog($"PHASE 2: transition '{phaseTransitionState}' driven at x{speed:F2} + {hold:F1}s hold — "
               + $"{(phaseTransitionInvulnerable ? "untouchable" : "vulnerable")}, roar at {phaseRoarNormalizedTime:F2}");

        while (true)
        {
            yield return null;
            if (combat == null || animator == null) break;
            float elapsed = Time.unscaledTime - startReal;

            // Raw clip length — from the CLIP, not from AnimatorStateInfo.length (that one is
            // divided by the state's and the animator's speed, which is the trap described above).
            if (clipLen < 0f)
            {
                clipLen = StateClipLength(hash);
                if (clipLen > 0f)
                {
                    driving = true;
                    DebugLog($"PHASE 2: clip is {clipLen:F2}s → {clipLen / speed + hold:F2}s on screen");
                }
            }

            if (driving)
            {
                float t = Mathf.Clamp01(elapsed * speed / clipLen);
                animator.Play(hash, 0, t);                       // the time is ours, whatever speed says
                combat.SetStaggerTimer(2f);                      // keep the engine parked; we end it

                if (!roared && t >= phaseRoarNormalizedTime)
                {
                    roared = true;
                    phaseRoarReached = true;
                    if (CombatFeedbackManager.Instance != null && phaseRoarShakeIntensity > 0f)
                        CombatFeedbackManager.Instance.CameraShake(phaseRoarShakeIntensity, phaseRoarShakeDuration);
                    if (phaseRoarRingRadius > 0f)
                        ProceduralImpactFX.Shockwave(transform.position, phaseRoarRingRadius, 0.6f, new Color(1f, 0.25f, 0.2f));
                    DebugLog($"PHASE 2: roar at {elapsed:F2}s");
                }

                if (elapsed >= clipLen / speed + hold) break;    // the ONLY normal exit
            }

            if (elapsed > 10f) { DebugLog("PHASE 2: transition hard-capped at 10s real"); break; }
            if (combat.GetCurrentState() == EnemyCombat.EnemyState.Dead) break;
        }

        if (phaseTransitionInvulnerable && health != null) health.SetInvulnerable(wasInvulnerable);
        phaseRoarReached = true;
        phaseTransitionActive = false;
        phaseTransitionRoutine = null;

        // End the window ourselves — do not wait for a game-time timer that Yoru's slow-motion can
        // stretch. Chase re-decides everything (hold-ground, cooldown) on the next frame.
        if (combat != null && combat.GetCurrentState() == EnemyCombat.EnemyState.Stagger)
            combat.SetState(EnemyCombat.EnemyState.Chase);

        DebugLog($"PHASE 2: transition over after {Time.unscaledTime - startReal:F2}s real — he is back, and angrier");
    }

    /// <summary>Raw length of the clip on a given state (independent of every speed multiplier).</summary>
    private float StateClipLength(int stateHash)
    {
        if (animator == null) return -1f;
        bool inTr = animator.IsInTransition(0);
        var cur = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] infos =
            cur.shortNameHash == stateHash ? animator.GetCurrentAnimatorClipInfo(0)
            : inTr && animator.GetNextAnimatorStateInfo(0).shortNameHash == stateHash ? animator.GetNextAnimatorClipInfo(0)
            : null;
        if (infos == null) return -1f;
        for (int i = 0; i < infos.Length; i++)
            if (infos[i].clip != null && infos[i].clip.length > 0.05f) return infos[i].clip.length;
        return -1f;
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

        bool phaseBeatDone = !phaseTransitionEnabled || phaseRoarReached || phaseTransitionDone && !phaseTransitionActive;
        if (barShown && !phase2Sent && combat.IsPhase2() && phaseBeatDone)
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
