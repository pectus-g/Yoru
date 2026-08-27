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

    [Header("Strike Moments (measured at RUNTIME — round 38)")]
    [Tooltip("Overrides for the melee attacks' Strike Moment, applied at Start over the values on the attack list. ROUND 38: re-measured from the [OniBoss:Strike] runtime logs of real play — the FBX-derived numbers were wrong in both directions. Club_Swing 0.42 (was 0.85: every mid-range sample showed damage landing 350-380ms AFTER the club had already swept past her). ClubSwing2 0.32 (was right, ±46ms). ClubSlam 0.85 (was 0.52: damage fired ~305ms BEFORE the club reached the floor, on every clean close-range hit). KanaboSweep 0.48 (unmeasured — its Animator state still plays the Idle clip). RENAMED from strikeMomentOverrides so the stale values saved in the scene are dropped; keep [OniBoss:Strike] on for one session to confirm, then tune here. Delete an entry to keep the attack list's own value.")]
    [SerializeField] private StrikeMomentOverride[] strikeMomentsTuned =
    {
        new StrikeMomentOverride("Club_Swing", 0.42f),
        new StrikeMomentOverride("ClubSwing2", 0.32f),
        new StrikeMomentOverride("ClubSlam", 0.85f),
        new StrikeMomentOverride("KanaboSweep", 0.48f),
    };

    [Header("Club touch — round 39")]
    [Tooltip("ROUND 39 — Hazel's rule: whichever touches first is the hit. The club's damage now lands the FRAME the club really touches Yoru — the weapon is tracked as a LINE from handle to tip, every frame of the swing, so it is correct at every distance and every attack. The Strike Moments above stop dealing damage; their only job left is deciding when a missed swing RELEASES its ground wave. Untick to fall back to the timed strikes.")]
    [SerializeField] private bool clubTouchEnabled = true;
    [Tooltip("ROUND 39. How close the club's shaft line must come to Yoru's body to count as touching, metres. The kanabo is thick and she has a body, so ~0.7 reads honestly. Raise it if visual grazes fail to register; lower it if 'air touches' land. The [OniBoss:Touch] log prints the measured distance of every touch AND every swing's closest miss — tune from those numbers, not from feel.")]
    [SerializeField] private float clubTouchRadius = 0.7f;
    [Tooltip("ROUND 41. The club only counts as touching while its tip is MOVING at least this fast, m/s. Measured over two full sessions: real blows sweep at 85-190 m/s, while the follow-through drift after a swing sits at 33-36 — and two of Hazel's 'reaction came when it finished' hits were exactly her stepping into that drifting club at clip 0.89-0.93. 50 splits the two cleanly: a finished swing can never hit, a real blow always can. The touch log prints tip speed on every touch and the swing's fastest tip on every miss.")]
    [SerializeField] private float clubTouchMinSpeed = 50f;
    [Tooltip("ROUND 39. The touch check arms only after this fraction of the attack clip, so the WINDUP (the club whipping up/back at full speed, sometimes through her space at point-blank) can never deliver the hit early. Every measured real contact sits at 0.29-0.94 of its clip; windups live below 0.2. This is NOT a strike moment — inside the armed part, only real contact decides.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float clubTouchArmFrom = 0.2f;
    [Tooltip("ROUND 40. Bottom of Yoru's body line, metres above her feet. The club is now compared against her WHOLE body (a vertical line from here to Body Top), not one chest point — a slam descending from above meets her head first, so with a single chest point the hit registered ~100-150ms late ('slam hits very late'), and a high horizontal sweep could pass through her shoulders without ever nearing the chest point.")]
    [SerializeField] private float clubTouchBodyBottom = 0.25f;
    [Tooltip("ROUND 40. Top of Yoru's body line, metres above her feet — about her head. See Body Bottom.")]
    [SerializeField] private float clubTouchBodyTop = 1.55f;

    [Header("Combo ramp & finisher wave — round 42")]
    [Tooltip("ROUND 42. Damage multiplier for the MIDDLE hits of a combo (2nd, 3rd... before the last). Hazel's pick, like other action games: fixed by position, resets every combo. 1 = off. Pushed into the shared engine at Start, so other enemies stay untouched.")]
    [SerializeField] private float comboMidDamageMult = 1.25f;
    [Tooltip("ROUND 42. Damage multiplier for the LAST hit of a combo — the finisher. Applies to the club touch AND carries into what the finisher wave derives from.")]
    [SerializeField] private float comboFinisherDamageMult = 1.5f;
    [Tooltip("ROUND 42. The combo finisher's wave is THIS many times bigger — visual size and hit width. Only the finisher: every other wave keeps the base size you tuned in the fields above.")]
    [SerializeField] private float finisherWaveScale = 2f;
    [Tooltip("ROUND 42. The combo finisher's wave travels THIS many times further than Ground Wave Travel.")]
    [SerializeField] private float finisherWaveTravelMult = 2f;
    [Tooltip("ROUND 44. Chance that a charge picked ALONE chains straight into a fast random melee follow-up — the charge stops being a blunt single hit. The follow-up is the combo finisher: x1.5 damage and the big finisher wave. 0 = off.")]
    [Range(0f, 1f)]
    [SerializeField] private float chargeFollowUpChance = 0.9f;
    [Tooltip("ROUND 47 — Hazel: the wait between attacks should not be one pose. Chance that a STANDING pause uses plain IDLE instead of the Watch stance. 0 = always Watch, 1 = always Idle, 0.5 = random mix.")]
    [Range(0f, 1f)]
    [SerializeField] private float waitIdleChance = 0.5f;
    [Tooltip("ROUND 49. Chance that a pause WALKS A RING around Yoru instead of standing (needs Circle Strafe ticked on EnemyCombat). Each ring rolls its own direction — sometimes left, sometimes right. The rest of the pauses stand, split by the chance above. 0 = never circle.")]
    [Range(0f, 1f)]
    [SerializeField] private float waitCircleChance = 0.5f;
    [Tooltip("ROUND 51 — Hazel's anti-kite, step 1: after this many seconds of chasing WITHOUT reaching her, he throws a swing from out of reach so its ground wave flies at the runner and clips her. Her number: 4. 0 = off.")]
    [SerializeField] private float chaseWaveAfter = 4f;
    [Tooltip("ROUND 51 — anti-kite, step 2: after this many seconds of STILL-failing chase, he charges her down instead. Her pick: 6. 0 = off.")]
    [SerializeField] private float chaseChargeAfter = 6f;

    [Header("Phase-2 Ground Pound — round 52")]
    [Tooltip("ROUND 52 — Hazel's phase-2 entrance. The transition roar chains STRAIGHT into this, no wait: he jumps high and pounds the floor. The landing fires a huge jumpable shockwave, her impact effect, and the biggest camera shake of the fight. Also repeats rarely during phase 2. Untick to disable.")]
    [SerializeField] private bool groundPoundEnabled = true;
    [Tooltip("Animator state of the jump-and-pound animation (confirmed from the controller).")]
    [SerializeField] private string groundPoundState = "Ground_Pound";
    [Tooltip("Playback speed of the pound clip. 1 = as authored.")]
    [SerializeField] private float poundAnimSpeed = 1f;
    [Tooltip("Normalized moment of the clip where he SLAMS the floor — shockwave, damage and shake fire here. Watch the [OniBoss:Pound] log line after the first test and correct this number from it.")]
    [Range(0f, 1f)]
    [SerializeField] private float poundStrikeMoment = 0.6f;
    [Tooltip("Damage the landing shockwave deals to a GROUNDED Yoru. Jumping over it = safe, as always. Her pick: 45.")]
    [SerializeField] private int poundDamage = 45;
    [Tooltip("How fast the shockwave ring expands, m/s. Fast = hard to escape by running.")]
    [SerializeField] private float poundRingSpeed = 12f;
    [Tooltip("How far the shockwave reaches, metres.")]
    [SerializeField] private float poundRingMaxRadius = 12f;
    [Tooltip("Thickness of the ring's hit band, metres.")]
    [SerializeField] private float poundRingWidth = 1.6f;
    [Tooltip("Camera shake at the slam — the 'cave is about to fall' one. The phase roar is 0.6 for 0.5s; this tops it.")]
    [SerializeField] private float poundShakeIntensity = 1.1f;
    [SerializeField] private float poundShakeDuration = 0.8f;
    [Tooltip("YOUR explosion/impact prefab, spawned at the landing point. Empty = only the code-built ring is drawn.")]
    [SerializeField] private GameObject poundImpactVFX;
    [Tooltip("Seconds before the impact effect is destroyed.")]
    [SerializeField] private float poundImpactVFXLifetime = 4f;
    [Tooltip("ROUND 52 — 'only at phase 2 sometimes': earliest seconds between pounds during phase 2. 0 = the entrance pound only, no repeats.")]
    [SerializeField] private float poundRepeatCooldown = 25f;
    [Tooltip("Once the cooldown is over, roughly this chance PER SECOND that a pound fires while he is chasing her in phase 2.")]
    [Range(0f, 1f)]
    [SerializeField] private float poundRepeatChancePerSecond = 0.15f;

    [Header("Phase-2 Entrance CINEMATIC — round 53")]
    [Tooltip("ROUND 53 — Hazel: the gameplay camera cannot see him at the sky, so the ENTRANCE (and only the entrance — repeats stay pure gameplay) plays as a short cinematic: time slows, a cinematic camera frames the roar, follows his leap up, and at the TOP of the jump everything snaps back — normal time, camera behind Yoru, control returned — so the slam itself is dodged in normal gameplay. Yoru is frozen and untouchable ONLY while the cinematic runs. Untick = round-52 behavior.")]
    [SerializeField] private bool cinematicEnabled = true;
    [Tooltip("World speed during the cinematic. 0.45 = just under half speed. Lower = slower and more dramatic, but the entrance takes longer in REAL seconds (the roar clip is ~2.4s: at 0.45 it fills ~5s).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float cineSlowMotion = 0.45f;
    [Tooltip("The cinematic camera stands this many metres in FRONT of the Oni.")]
    [SerializeField] private float cineCamDistance = 7f;
    [Tooltip("Camera height above his feet, metres.")]
    [SerializeField] private float cineCamHeight = 2.6f;
    [Tooltip("Degrees the camera steps around to the side, so the shot is a 3/4 view instead of dead-on. 0 = straight in front of his face.")]
    [SerializeField] private float cineCamSideAngle = 25f;
    [Tooltip("Height on his body the camera AIMS at during the roar, metres above his feet (during the leap it aims at his hips instead, wherever they fly).")]
    [SerializeField] private float cineLookHeight = 2.6f;
    [Tooltip("Seconds the camera takes to fly from the gameplay view INTO the cinematic shot.")]
    [SerializeField] private float cineBlendIn = 0.45f;
    [Tooltip("Seconds the camera takes to fly back behind Yoru at the top of the jump.")]
    [SerializeField] private float cineBlendOut = 0.35f;
    [Tooltip("Normalized moment of the POUND clip that counts as the top of the jump — normal time, the camera and control ALL return here. Must be BEFORE Pound Strike Moment (0.6) or the slam would land inside the cinematic.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float cineApexMoment = 0.4f;
    [Tooltip("ROUND 53 — Hazel: after the cinematic the Oni waits AT LEAST this many seconds before his first attack, so the entrance never turns into an instant cheap hit. 0 = no grace.")]
    [SerializeField] private float cineFirstAttackGrace = 2f;

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
        [Tooltip("SLASH. Spawned once at the strike moment, at the club's position and angle, and left hanging in the air while the club carries on past it — the mark the blow leaves. Empty = use the fallback below.")]
        public GameObject vfx;
        [Tooltip("TRAIL. Spawned at the START of the swing and parented to the club, so it draws the whole arc the weapon travels, then fades when the swing ends. A different job from the slash: use one, the other, or both. Empty = use the trail fallback below.")]
        public GameObject trailVFX;
        [Tooltip("Effect spawned ON YORU where the club actually meets her, and only when the hit lands. Empty = use the Hit Land VFX fallback below.")]
        public GameObject hitVFX;
        [Tooltip("ROUND 30. Extra metres in front of him for THIS attack's wave, on top of Swing Wave Spawn Distance. 0 = use the shared distance.")]
        public float nudgeForward;
        [Tooltip("ROUND 30. Extra height for THIS attack's wave, on top of Swing Wave Spawn Height. Negative pushes it toward the ground — useful for the slam. 0 = use the shared height.")]
        public float nudgeUp;
        [Tooltip("ROUND 30. Sideways offset for THIS attack's wave, along his right. 0 = dead centre, which is usually what you want since the wave travels straight at her.")]
        public float nudgeRight;
        [Tooltip("ROUND 33. Tilt in degrees around the wave's own X axis (pitch it up or down).")]
        public float tiltX;
        [Tooltip("ROUND 33. Tilt in degrees around the wave's own Y axis (swing it left or right).")]
        public float tiltY;
        [Tooltip("ROUND 33. ROLL in degrees. This is the one that makes a slash sit DIAGONALLY across the swing instead of lying flat. Try 30-45 and watch it in Play.")]
        public float tiltZ;
        [Tooltip("ROUND 38: NO LONGER USED. The ground wave aims flat along the floor and JUMPING is what clears it, so there is no aim height to move any more. Kept only so existing rows don't re-serialise; safe to ignore.")]
        public float burstHeightOffset;
        [Tooltip("ROUND 38: seconds the effect gets to FINISH FADING after the wave's travel ends (or after it hits her) — emission stops and the particles already alive play out for this long while the wave brakes to a halt. No longer an independent visual lifetime; nothing is detached or cut off any more.")]
        public float lifetime = 2f;
        public SwingVFXBinding() { }
        public SwingVFXBinding(string attack) { this.attack = attack; }
    }


    [Tooltip("ROUND 28. Trail used for any attack whose row has no Trail Vfx of its own. Empty = that attack has no trail.")]
    [SerializeField] private GameObject swingTrailVFX;
    [Tooltip("ROUND 28. Seconds the trail is left to fade after the swing ends. It is unparented first, so it stops following him and dies where it was rather than snapping out of existence mid-arc.")]
    [SerializeField] private float trailFadeOut = 0.4f;

    private GameObject activeTrail;
    // ROUND 38: no activeWave / one-wave-at-a-time guard any more (round 37's mechanism, deleted
    // rather than kept as a stacked safety). A wave is now armed for well under a second and only
    // exists at all when its club missed, so hits from long-finished swings are impossible.

    [Tooltip("ROUND 25. Used for any attack whose row has no Hit VFX of its own. Spawned at the point where the club actually meets Yoru's body, only on swings that connect.")]
    [SerializeField] private GameObject hitLandVFX;
    [Tooltip("Seconds before a hit effect is destroyed.")]
    [SerializeField] private float hitLandVFXLifetime = 2f;
    [Tooltip("Nudge the hit effect this far back along the line from Yoru toward the club, metres. A small positive value keeps a flat effect from being buried inside her body.")]
    [SerializeField] private float hitLandVFXOffset = 0.1f;
    [Tooltip("ROUND 26. Measurement only, changes nothing. Logs where his club actually is at each strike moment — height above his feet, distance in front, sideways offset, and how far its own facing has swung from his — next to where the swing effect is currently being spawned, so the gap between the two is a number rather than a guess. Turn off once the effect is placed.")]
    [SerializeField] private bool logClubPositionAtStrike = true;
    [Tooltip("ROUND 38. How fast the ground wave travels, m/s. It is a REAL hitbox for its whole travel, so this is her reaction window: at 12 it covers its full 6m in half a second — visible, jumpable, and physically incapable of the old seconds-late hit. RENAMED from swingWaveSpeed so the scene's saved 3 (a four-second crawl that delivered hits from swings long finished) is dropped.")]
    [SerializeField] private float groundWaveSpeed = 12f;
    [Tooltip("ROUND 38. Half-width of the wave, metres: she is hit when she is ON THE GROUND within this of the wave's centre line as it passes. Height is no longer part of the check — being AIRBORNE is what clears it. RENAMED from swingWaveHitRadius (same meaning sideways, new vertical rule).")]
    [SerializeField] private float groundWaveWidth = 1.2f;
    [Tooltip("ROUND 38. Metres in FRONT OF HIM the wave is born. His body is ~1.4m across, so anything smaller is born hidden INSIDE him — the scene had 0.3 saved, which is exactly why no wave was ever seen leaving him. Keep it under his 3.5m club reach or it starts beyond her. RENAMED so that 0.3 is dropped.")]
    [SerializeField] private float groundWaveStartDistance = 1.6f;
    [Tooltip("ROUND 38. Height above the floor the wave visual rides at. This is ground force that Yoru ESCAPES BY JUMPING, so it wants to read low — shin height, not chest. Purely visual: the hit check is flat + airborne-gated. RENAMED from swingWaveSpawnHeight (the scene's saved 0.8 is dropped; you had already been pushing it down yourself).")]
    [SerializeField] private float groundWaveHeight = 0.35f;
    [Tooltip("ROUND 38. Metres the wave travels before it dissolves — and it is ARMED the whole way, there is no cosmetic phase. Armed time = this ÷ speed (0.50s at the defaults). Total threat = Start Distance + this; 1.6 + 6 = 7.6m punishes backing straight off without owning the whole arena. RENAMED from swingWaveTravel so the scene's saved 12 (4 seconds of flight) is dropped.")]
    [SerializeField] private float groundWaveTravel = 6f;
    [Tooltip("ROUND 33. Playback speed of the wave's effect. 1 = as authored. 0.5 plays it at half speed so it takes TWICE as long to play out — this is the only real way to make a short burst last longer, because deleting it later does nothing for an effect that has already finished. 0.35-0.6 is the useful range if your prefabs are over too quickly.")]
    [SerializeField] private float swingWaveVisualPlaybackSpeed = 1f;

    private PlayerHealth playerHealthRef;
    private PlayerMovement playerMoveRef;   // ROUND 38: the wave's airborne check — jumping clears it
    private string waveAttackName = "";     // trail bookkeeping only (see UpdateSwingTrail)

    // ROUND 39 — club touch state (see UpdateClubTouch).
    private SwingWaveProjectile currentSwingWave;  // this swing's released wave, so a late club touch can cancel it
    private Transform clubRootBone;                // shallowest kanabo bone — the handle end of the shaft line
    private string clubTouchAttackName = "";       // which swing the touch bookkeeping belongs to
    private bool   swingHitDelivered;              // the ONE hit of this swing has landed (club or wave)
    private bool   clubPrevTipValid;               // tip speed needs last frame's tip position
    private Vector3 clubPrevTipPos;
    private Vector3 clubPrevRootPos;               // ROUND 42: the shaft root moves too — needed for the one-frame prediction
    private GameObject chargeTrailInstance;        // ROUND 47: the fire attached to him during the rush

    // ROUND 52 — ground pound state.
    private Coroutine poundRoutine;
    private bool poundActive;
    private float nextPoundAllowedTime;
    private bool poundRingActive;
    private bool poundRingSpent;
    private Vector3 poundRingCenter;
    private float poundRingRadius;
    private float poundRingPrevRadius;

    // ROUND 53 — phase-2 entrance cinematic state.
    private bool cineActive;                  // the cinematic is running NOW (roar → top of the jump)
    private bool cineTimeSlowed;              // we own Time.timeScale — restore to 1 on ANY exit
    private float cineWeight;                 // camera blend, 0..1
    private Vector3 cineCamPos;               // the anchored shot position (world), set once at cinematic start
    private Coroutine cineBlendOutRoutine;
    private float  clubTouchClosest = float.MaxValue;  // per-swing closest shaft-to-body distance, logged on swing end
    private float  clubTouchClosestClip = -1f;
    private float  clubTouchMaxSpeed;

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
    [Tooltip("ROUND 43 — YOUR fire for the charge. Drag a fire VFX prefab here: while the rush travels, one puff spawns at his feet every Charge Wave Interval seconds and lives Charge Trail Lifetime seconds — a burning line follows him along the ground. Empty = no fire trail. (This replaces the gray placeholder arcs; the old toggle below only matters when this slot is empty.)")]
    [SerializeField] private GameObject chargeGroundTrailVFX;
    [Tooltip("ROUND 47. Seconds the fire keeps burning/fading where the rush ended, after the charge is over.")]
    [SerializeField] private float chargeTrailLifetime = 1.5f;
    [Tooltip("ROUND 51. Height above the floor the fire rides at during the rush. Hazel: it must sit ON the ground — keep this near 0.")]
    [SerializeField] private float chargeTrailHeight = 0.03f;
    [Tooltip("ROUND 43. Effect spawned ON Yoru at the exact moment the charge's damage connects. Empty = the shared Hit Land VFX is used.")]
    [SerializeField] private GameObject chargeHitVFX;
    // ROUND 50: the round-49 "second standing-fire slot" was a misunderstanding and is deleted —
    // there is ONE fire (the trail prefab above). How long its line stays on the ground and how
    // gradually it fades are the PREFAB's own Trail Renderer settings: Time and the Color
    // gradient's end alpha. The code's only duty is never to cut it short (see ReleaseChargeTrail).

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
            playerMoveRef = playerT.GetComponent<PlayerMovement>();
            if (playerMoveRef == null) playerMoveRef = playerT.GetComponentInChildren<PlayerMovement>();
            DebugLog($"ground wave ON (round 38): half the club's damage on TOUCH, {groundWaveSpeed:F0}m/s x {groundWaveTravel:F1}m "
                   + $"from {groundWaveStartDistance:F1}m in front at {groundWaveHeight:F2}m height — a club hit cancels that swing's wave, "
                   + $"jumping clears it. Per-attack effects come from the Swing Wave VFX By Attack rows. "
                   + $"PlayerHealth {(playerHealthRef != null ? "found" : "NOT FOUND — wave damage disabled")}, "
                   + $"PlayerMovement {(playerMoveRef != null ? "found" : "NOT FOUND — she will count as grounded")}.");
        }

        // ROUND 15: locate the club's TIP — the deepest bone whose name matches — so the distance
        // being measured is the business end, not the handle in his fist.
        // ROUND 25: found whenever the measurement, the swing wave, or the touch hit needs it.
        // ROUND 39: ALSO the shallowest match — the handle end — so the club is a LINE from handle
        // to tip for the touch check. A tip-only point misses shaft hits: close-range Club_Swing
        // puts the tip 1-2m PAST her while the shaft crosses her body (measured, not guessed).
        if ((measureStrikeContact || swingWaveEnabled || clubTouchEnabled) && !string.IsNullOrEmpty(clubBoneNameContains))
        {
            string needle = clubBoneNameContains.ToLowerInvariant();
            int bestDepth = -1;
            int rootDepth = int.MaxValue;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.ToLowerInvariant().Contains(needle)) continue;
                int depth = 0;
                for (Transform w = t; w != null && w != transform; w = w.parent) depth++;
                if (depth > bestDepth) { bestDepth = depth; clubBone = t; }
                if (depth < rootDepth) { rootDepth = depth; clubRootBone = t; }
            }
            if (clubRootBone == null || clubRootBone == clubBone)
                clubRootBone = (clubBone != null && clubBone.parent != null) ? clubBone.parent : clubBone;
            if (clubBone != null)
                DebugLog($"club tracking ON — tip '{clubBone.name}' (depth {bestDepth}), shaft root '{(clubRootBone != null ? clubRootBone.name : "none")}'.");
            else
                Debug.LogWarning($"[OniBoss] club tracking: no bone containing '{clubBoneNameContains}' under him. Touch hits and measurement are off; nothing else is affected.");
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

        // ROUND 47/49 — the wait is not one pose: each pause rolls stand-Watch, stand-Idle, or a
        // slow ring around her that starts left or right at random.
        combat.SetHoldWatchIdleChance(waitIdleChance);
        combat.SetHoldWaitCircleChance(waitCircleChance);

        // ROUND 51 — Hazel's anti-kite: 4s of failed chase → a thrown swing whose wave clips the
        // runner (only inside the wave's real reach); 6s → the charge runs her down.
        combat.ConfigureChasePunish(chaseWaveAfter,
                                    groundWaveStartDistance + Mathf.Max(0.5f, groundWaveTravel),
                                    chaseChargeAfter, chargeStateName);
        DebugLog($"anti-kite ON: thrown swing after {chaseWaveAfter:F0}s of failed chase (within {groundWaveStartDistance + Mathf.Max(0.5f, groundWaveTravel):F1}m), charge after {chaseChargeAfter:F0}s.");

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

        // Strike moments measured at RUNTIME from [OniBoss:Strike] — round 38 re-timed them so the
        // club's damage fires when the club actually TOUCHES her, not before (slam) or after (swing).
        if (strikeMomentsTuned != null && strikeMomentsTuned.Length > 0)
        {
            var applied = new System.Text.StringBuilder();
            foreach (var o in strikeMomentsTuned)
            {
                if (o == null || string.IsNullOrEmpty(o.attack)) continue;
                if (combat.SetAttackStrikeMoment(o.attack, o.strikeMoment)) applied.Append($" {o.attack}={o.strikeMoment:F2}");
                else Debug.LogWarning($"[OniBoss] strike moment override: no attack named/animated '{o.attack}' on EnemyCombat.");
            }
            if (applied.Length > 0) DebugLog($"strike moments overridden:{applied}");
        }

        // ROUND 39 — her rule: whichever touches first is the hit. The engine hands these four
        // attacks' damage to the touch detector (UpdateClubTouch); the strike moments above keep
        // only one job — releasing the ground wave when a swing has not connected. The charge is
        // NOT included: it keeps its own timed impact. If the club bones were not found, nothing
        // is handed over and the timed strikes keep working exactly as before.
        if (clubTouchEnabled && clubBone != null)
        {
            var touched = new System.Text.StringBuilder();
            foreach (var name in new[] { "Club_Swing", "ClubSwing2", "ClubSlam", "KanaboSweep" })
            {
                if (combat.SetAttackTouchDriven(name, true)) touched.Append(' ').Append(name);
                else Debug.LogWarning($"[OniBoss] club touch: no attack named/animated '{name}' on EnemyCombat.");
            }
            DebugLog($"club TOUCH hits ON (radius {clubTouchRadius:F2}m, min tip speed {clubTouchMinSpeed:F0}m/s, armed from {clubTouchArmFrom:F2} of the clip):{touched}. "
                   + "First touch wins — club full damage, wave half, one hit per swing.");
        }
        else if (clubTouchEnabled)
        {
            Debug.LogWarning("[OniBoss] club touch is enabled but the club bone was not found — falling back to the timed strikes.");
        }

        // ROUND 42 — combo damage ramp, her pick ("like other games"): opener x1, middle hits
        // x1.25, finisher x1.5, fixed by position. Pushed from here so the shared engine keeps
        // x1 for every other enemy. The waves inherit it automatically through CurrentAttackDamage.
        combat.ConfigureComboDamageRamp(comboMidDamageMult, comboFinisherDamageMult);
        DebugLog($"combo damage ramp ON: mid x{comboMidDamageMult:F2}, finisher x{comboFinisherDamageMult:F2} — "
               + $"finisher wave carries FULL damage at x{finisherWaveScale:F1} size, x{finisherWaveTravelMult:F1} travel.");

        // ROUND 44 — her call ("yes"): the charge alone looks blunt, so ~90% of solo charges chain
        // into a fast melee follow-up through the normal combo machinery.
        if (chargeFollowUpChance > 0f)
        {
            combat.ConfigureChargeFollowUp(chargeStateName, chargeFollowUpChance);
            DebugLog($"charge follow-up ON: {chargeFollowUpChance:P0} of solo charges chain into a random melee finisher.");
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

        // ROUND 53: the entrance cinematic slows the world ON PURPOSE — that is not a leak.
        if (cineActive) { slowSinceRealTime = -1f; slowWarned = false; return; }

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

        // ROUND 38: the wave decision rides the engine's own strike resolution — subscribed here
        // (combat is cached in Awake) and released in OnDisable, so it survives enable cycles and
        // never double-subscribes.
        if (combat != null) combat.StrikeResolved += OnStrikeResolved;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
        if (combat != null) combat.StrikeResolved -= OnStrikeResolved;

        // ROUND 53: never leave the world slowed or Yoru locked if the boss object goes away
        // mid-cinematic. (The camera and Yoru's protection also expire by themselves — this is
        // the belt to their braces.)
        EndPhaseCinematic("boss disabled");
    }

    private void Update()
    {
        KeepAnimationSpeed();       // ROUND 16
        UpdateAttackStepIn();
        UpdateSwingTrail();         // ROUND 38: trails only — the wave itself spawns from EnemyCombat's StrikeResolved
        UpdatePhaseTransition();
        UpdatePoundRing();          // ROUND 52: the pound's expanding shockwave
        UpdatePoundRepeat();        // ROUND 52: rare phase-2 repeats
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
        if (strikeMomentsTuned != null)
        {
            foreach (var o in strikeMomentsTuned)
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
        if (phaseTransitionActive || poundActive) return;  // the roar / the pound own the speed
        if (animator.speed < 0.05f) return;                // hitstop is running, leave it frozen
        if (!Mathf.Approximately(animator.speed, oniAnimationSpeed))
            animator.speed = oniAnimationSpeed;
    }

    /// <summary>
    /// ROUND 38 (was UpdateSwingWave). Trail bookkeeping ONLY: starts an attack's club trail when
    /// its swing begins and ends it when the swing (or combo step) does. The WAVE no longer fires
    /// from here — it spawns in OnStrikeResolved, in the same instant the engine resolves the
    /// club's own hit, so the wave and the club can never disagree about a swing again.
    /// </summary>
    private void UpdateSwingTrail()
    {
        if (combat == null) return;

        bool inAttack = combat.GetCurrentState() == EnemyCombat.EnemyState.Attack;
        string atk = inAttack ? combat.CurrentAttackName() : "";
        bool isCharge = inAttack && (atk == chargeStateName || combat.CurrentAttackAnim() == chargeStateName);

        // ROUND 47: the moment the rush state is over, the attached fire is released — unparented,
        // emission stopped, left to burn out where the rush ended.
        if (chargeTrailInstance != null && !(inAttack && isCharge)) ReleaseChargeTrail();

        if (!swingWaveEnabled || animator == null) return;

        if (!inAttack || isCharge)
        {
            waveAttackName = "";
            EndSwingTrail();
            return;
        }

        if (atk != waveAttackName)
        {
            waveAttackName = atk;
            EndSwingTrail();          // a combo step replaces the previous step's trail
            StartSwingTrail(atk);     // ROUND 28: the trail belongs to the WHOLE swing
        }
    }

    /// <summary>
    /// ROUND 38 — Hazel's redesigned wave, decided in the SAME instant the engine resolves the
    /// club (called synchronously from EnemyCombat.DealDamageToPlayer via its StrikeResolved event):
    ///
    ///   club CONNECTED → her hit effect plays and this swing's wave is cancelled OUTRIGHT — no
    ///                    wave, not even a cosmetic one. One hit per swing, always. Her words:
    ///                    "if club hits yoru that cancels the wave."
    ///   club MISSED    → the ground wave spawns, armed with HALF that attack's club damage for
    ///                    its whole short travel, and hurts her the frame it TOUCHES her on the
    ///                    ground. Airborne, it passes under her — jumping is the escape.
    ///
    /// Because this runs inside the engine's own strike, there is no clip-position poll here any
    /// more, no separately re-derived range check to drift out of agreement with the real hit
    /// (the old dist-vs-AttackRange guess could disagree with the engine's per-attack range),
    /// and no frame gap between the club's result and the wave's decision.
    /// </summary>
    private void OnStrikeResolved(bool clubConnected)
    {
        if (!swingWaveEnabled || combat == null || playerT == null) return;

        string atk  = combat.CurrentAttackName();
        string anim = combat.CurrentAttackAnim();
        if (atk == chargeStateName || anim == chargeStateName)
        {
            // The charge owns its own impact — ROUND 43: and it finally SHOWS one. Fired from the
            // engine's own resolution, so the effect appears the exact frame the charge damage lands.
            if (clubConnected && playerT != null)
            {
                GameObject fx = chargeHitVFX != null ? chargeHitVFX : hitLandVFX;
                if (fx != null)
                {
                    GameObject go = Instantiate(fx, playerT.position + Vector3.up * strikeContactBodyHeight,
                                                Quaternion.LookRotation(-transform.forward));
                    if (hitLandVFXLifetime > 0f) Destroy(go, hitLandVFXLifetime);
                }
            }
            return;
        }

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
                    + $"(the ground wave is born "
                    + $"{groundWaveStartDistance:F1}m in front of him at {groundWaveHeight:F2}m.)");
        }

        // ROUND 24: this attack's own effect if it has one, otherwise the fallback.
        GameObject prefab = null;          // ROUND 32: each attack brings its own, or has none
        GameObject hitPrefab = hitLandVFX;
        float life = 2f;
        Vector3 nudge = Vector3.zero;
        Vector3 tilt = Vector3.zero;
        if (swingWaveVFXByAttack != null)
        {
            foreach (var b in swingWaveVFXByAttack)
            {
                if (b == null || string.IsNullOrEmpty(b.attack)) continue;
                if (b.attack != atk && b.attack != anim) continue;
                if (b.vfx != null)
                {
                    prefab = b.vfx;
                    life = b.lifetime;
                    nudge = new Vector3(b.nudgeRight, b.nudgeUp, b.nudgeForward);
                    tilt = new Vector3(b.tiltX, b.tiltY, b.tiltZ);
                }
                if (b.hitVFX != null) hitPrefab = b.hitVFX;
                break;
            }
        }

        // ROUND 39: for a TOUCH-DRIVEN attack the hit and its effect were already delivered at the
        // touch. A timed attack (the fallback, or one not handed to the touch detector) still gets
        // its impact effect at its own connected strike, exactly as before.
        if (clubConnected && !combat.CurrentAttackTouchDriven()) SpawnHitLandVFX(hitPrefab);

        if (prefab == null) return;

        // ROUND 40 — her words: "i really want to see the wave in every attack. wave hits if the
        // club didn't hit, and if club did hit then wave won't hit but looks on the effect."
        // So the wave SPAWNS EVERY SWING; the club only decides whether it carries damage.
        bool armed = !clubConnected;

        // Born OUTSIDE his ~1.4m body (the scene's saved 0.3 had it born hidden inside him), low
        // to the floor, flying his forward — which is locked on where she was when the swing
        // committed, so backing straight away from a missed swing walks her down the wave's path.
        Vector3 origin = transform.position
                       + transform.forward * (groundWaveStartDistance + nudge.z)
                       + Vector3.up        * (groundWaveHeight        + nudge.y)
                       + transform.right   * nudge.x;

        // ROUND 42 — the combo FINISHER's wave is the show-piece: bigger, further, and FULL club
        // damage instead of half. Single attacks and combo openers keep the normal wave exactly
        // as tuned. (CurrentAttackDamage already carries the combo ramp, so a finisher wave is
        // full ramped damage.)
        bool finisher = combat.CurrentAttackIsComboFinisher();
        float travel = Mathf.Max(0.5f, groundWaveTravel) * (finisher ? Mathf.Max(1f, finisherWaveTravelMult) : 1f);
        float width  = groundWaveWidth * (finisher ? Mathf.Max(1f, finisherWaveScale) : 1f);
        int waveDamage = 0;
        if (armed)
            waveDamage = finisher ? Mathf.Max(1, combat.CurrentAttackDamage())
                                  : Mathf.Max(1, combat.CurrentAttackDamage() / 2);

        var wave = SwingWaveProjectile.Launch(
            prefab, origin, transform.forward,
            transform, playerT, playerHealthRef, playerMoveRef,
            groundWaveSpeed, travel, width,
            waveDamage,
            hitPrefab, hitLandVFXLifetime, hitLandVFXOffset,
            life, tilt, swingWaveVisualPlaybackSpeed,
            finisher ? Mathf.Max(1f, finisherWaveScale) : 1f);

        if (armed)
        {
            // ROUND 39: this swing owns its armed wave. If the wave touches her first, it spends
            // the swing and the club stands down; if the club touches her first, the wave is
            // DISARMED mid-flight and flies on as pure effect (see UpdateClubTouch). First touch
            // wins, one hit per swing.
            currentSwingWave = wave;
            wave.onConnected = OnSwingWaveConnected;

            DebugLog($"{atk}: club has not connected — ground wave released ARMED, {groundWaveStartDistance + nudge.z:F1}m in front, "
                   + $"{groundWaveSpeed:F0}m/s x {travel:F1}m = {travel / Mathf.Max(0.01f, groundWaveSpeed):F2}s, "
                   + (finisher
                        ? $"COMBO FINISHER: FULL {waveDamage} damage, x{finisherWaveScale:F1} size, x{finisherWaveTravelMult:F1} travel."
                        : $"carrying {waveDamage} (half of the club's {combat.CurrentAttackDamage()})."));
        }
        else
        {
            DebugLog($"{atk}: club already hit — ground wave released as VISUAL only (0 damage){(finisher ? $", finisher size x{finisherWaveScale:F1}" : "")}.");
        }
    }

    /// <summary>
    /// ROUND 39/40 — the club's hit, by TOUCH. Runs in LateUpdate so the skeleton is posed. The
    /// club is a line from handle to tip, and (ROUND 40) Yoru is a line from her feet to her head
    /// — the frame those two lines come within Club Touch Radius of each other, while the tip is
    /// genuinely swinging and the clip is past the windup, the engine delivers the full club hit
    /// (same pipeline as ever: shake, heavy/light, reaction, stun, that frame) and this swing is
    /// spent: its wave is DISARMED (it flies on as pure effect — round 40, the wave is always
    /// seen) or spawns unarmed. If the wave was released first AND touched her first, the wave's
    /// hit spent the swing and the club stands down. Whichever touches first — exactly as Hazel
    /// put it. Every swing logs either its touch (distance, clip position, tip speed) or its
    /// closest miss, so the tunables are tuned from numbers, never from feel.
    /// </summary>
    private void UpdateClubTouch()
    {
        if (!clubTouchEnabled || combat == null || clubBone == null || clubRootBone == null || playerT == null) return;

        bool inAttack = combat.GetCurrentState() == EnemyCombat.EnemyState.Attack;
        string atk = inAttack ? combat.CurrentAttackName() : "";
        bool isCharge = inAttack && (atk == chargeStateName || combat.CurrentAttackAnim() == chargeStateName);

        if (!inAttack || isCharge)
        {
            if (clubTouchAttackName.Length > 0) FlushClubTouchSwing();
            clubPrevTipValid = false;
            return;
        }

        if (atk != clubTouchAttackName)
        {
            if (clubTouchAttackName.Length > 0) FlushClubTouchSwing();
            clubTouchAttackName  = atk;
            swingHitDelivered    = false;
            currentSwingWave     = null;   // a new swing owns no wave yet; an older wave flies on under its own swing's account
            clubPrevTipValid     = false;
            clubTouchClosest     = float.MaxValue;
            clubTouchClosestClip = -1f;
            clubTouchMaxSpeed    = 0f;
        }

        // Tip speed from last frame's posed position — the "is it actually swinging" gate.
        Vector3 tip  = clubBone.position;
        Vector3 root = clubRootBone.position;
        float dt = Time.deltaTime;
        bool havePrev = clubPrevTipValid && dt > 0.0001f;
        float tipSpeed  = havePrev ? Vector3.Distance(tip, clubPrevTipPos) / dt : 0f;
        Vector3 tipVel  = havePrev ? (tip  - clubPrevTipPos)  / dt : Vector3.zero;
        Vector3 rootVel = havePrev ? (root - clubPrevRootPos) / dt : Vector3.zero;
        clubPrevTipPos   = tip;
        clubPrevRootPos  = root;
        clubPrevTipValid = true;

        // ROUND 42: skeleton SNAPS (blend pops, state jumps) read as impossible speeds — measured
        // spikes of 519-1078 m/s while real blows top out around 190. A frame like that is a
        // teleport, not a swing: no touch, no prediction, and it is not a real "fastest tip".
        if (tipSpeed > 300f) return;
        if (tipSpeed > clubTouchMaxSpeed) clubTouchMaxSpeed = tipSpeed;

        if (swingHitDelivered) return;
        if (animator == null || animator.IsInTransition(0)) return;

        float clip = Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
        if (clip < clubTouchArmFrom) return;

        // ROUND 40: her WHOLE body line, not one chest point — a slam descending from above meets
        // her head long before it meets a chest-height point, and that gap was the measured
        // ~100-150ms of "slam hits very late". A high sweep through her shoulders counts now too.
        Vector3 bodyBottom = playerT.position + Vector3.up * clubTouchBodyBottom;
        Vector3 bodyTop    = playerT.position + Vector3.up * clubTouchBodyTop;
        float shaftDist = DistanceSegmentToSegment(root, tip, bodyBottom, bodyTop);
        if (shaftDist < clubTouchClosest) { clubTouchClosest = shaftDist; clubTouchClosestClip = clip; }

        // ROUND 42 — one frame of foresight. At 85-190 m/s the club crosses 1.5-3m between two
        // frames, so waiting for "inside NOW" can be one frame behind what the eye already saw.
        // If NEXT frame's projected club line is already inside her, the touch is this instant.
        bool touching  = shaftDist <= clubTouchRadius;
        bool predicted = false;
        if (!touching && havePrev)
        {
            float nextDist = DistanceSegmentToSegment(root + rootVel * dt, tip + tipVel * dt, bodyBottom, bodyTop);
            if (nextDist <= clubTouchRadius) { touching = true; predicted = true; shaftDist = nextDist; }
        }

        if (!touching) return;
        if (tipSpeed < clubTouchMinSpeed) return;

        // Contact. The engine runs the full club hit this frame — or refuses if this attack's hit
        // was somehow already delivered engine-side; either way the swing is spent.
        bool delivered = combat.DeliverStrikeOnTouch();
        swingHitDelivered = true;
        if (!delivered) return;

        SpawnHitLandVFX(HitVFXForAttack(atk, combat.CurrentAttackAnim()));
        bool disarmedWave = currentSwingWave != null;
        if (disarmedWave) currentSwingWave.CancelledByClub();   // released before he reached her — the club got there first, so it flies on as pure effect

        Debug.Log($"[OniBoss:Touch] {atk}: club TOUCHED Yoru at clip {clip:F2} — shaft {shaftDist:F2}m from her body line, tip speed {tipSpeed:F0}m/s{(predicted ? ", ONE FRAME EARLY (predicted)" : "")}. Full club hit delivered this frame."
                + (disarmedWave ? " This swing's wave is disarmed and flies on as visual." : ""));
    }

    /// <summary>ROUND 39. End-of-swing bookkeeping + the tuning line: how close the club came when
    /// it did NOT touch. Prints only while the ClubPos diagnostic is on.</summary>
    private void FlushClubTouchSwing()
    {
        if (logClubPositionAtStrike && !swingHitDelivered && clubTouchClosestClip >= 0f && clubTouchClosest < float.MaxValue * 0.5f)
            Debug.Log($"[OniBoss:Touch] {clubTouchAttackName}: no touch this swing — closest {clubTouchClosest:F2}m at clip {clubTouchClosestClip:F2}, fastest tip {clubTouchMaxSpeed:F0}m/s (radius {clubTouchRadius:F2}, min speed {clubTouchMinSpeed:F0}).");
        clubTouchAttackName  = "";
        swingHitDelivered    = false;
        currentSwingWave     = null;
        clubTouchClosest     = float.MaxValue;
        clubTouchClosestClip = -1f;
        clubTouchMaxSpeed    = 0f;
    }

    /// <summary>ROUND 39. This swing's wave touched her first — the swing is spent and the club
    /// stands down. A wave connecting after its swing already ended is its own business (the
    /// current-swing reference was cleared), so it can never block the NEXT swing's club.</summary>
    private void OnSwingWaveConnected(SwingWaveProjectile w)
    {
        if (w != null && ReferenceEquals(w, currentSwingWave)) swingHitDelivered = true;
    }

    /// <summary>ROUND 39. The hit effect this attack's row wants (or the fallback) — shared by the
    /// touch delivery and the timed fallback.</summary>
    private GameObject HitVFXForAttack(string atk, string anim)
    {
        GameObject hitPrefab = hitLandVFX;
        if (swingWaveVFXByAttack != null)
        {
            foreach (var b in swingWaveVFXByAttack)
            {
                if (b == null || string.IsNullOrEmpty(b.attack)) continue;
                if (b.attack != atk && b.attack != anim) continue;
                if (b.hitVFX != null) hitPrefab = b.hitVFX;
                break;
            }
        }
        return hitPrefab;
    }

    /// <summary>ROUND 40. Shortest distance between two segments — the club's shaft line against
    /// Yoru's body line. Closed-form clamped solution (Ericson, Real-Time Collision Detection).
    /// 3D on purpose: jumping over a low sweep genuinely clears the shaft.</summary>
    private static float DistanceSegmentToSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1;   // club: root -> tip
        Vector3 d2 = q2 - p2;   // body: bottom -> top
        Vector3 r  = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        const float EPS = 0.00000001f;

        float s, t;
        if (a <= EPS && e <= EPS) return r.magnitude;
        if (a <= EPS)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= EPS)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > EPS ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f)      { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }

        Vector3 c1 = p1 + d1 * s;
        Vector3 c2 = p2 + d2 * t;
        return Vector3.Distance(c1, c2);
    }

    /// <summary>
    /// ROUND 28. Starts this attack's trail: spawned at the club and parented to it, so it draws
    /// the arc for the whole swing rather than appearing for one instant at the end.
    /// </summary>
    private void StartSwingTrail(string atk)
    {
        if (!swingWaveEnabled || clubBone == null) return;

        GameObject prefab = swingTrailVFX;
        if (swingWaveVFXByAttack != null)
        {
            string anim = combat != null ? combat.CurrentAttackAnim() : "";
            foreach (var b in swingWaveVFXByAttack)
            {
                if (b == null || string.IsNullOrEmpty(b.attack)) continue;
                if (b.attack != atk && b.attack != anim) continue;
                if (b.trailVFX != null) prefab = b.trailVFX;
                break;
            }
        }
        if (prefab == null) return;

        activeTrail = Instantiate(prefab, clubBone.position, clubBone.rotation);
        activeTrail.transform.SetParent(clubBone, true);   // keeps its authored size

        // ROUND 29: when the trail is born, against where the clip actually is. "Late" is a
        // feeling; this makes it a number, so the next fix is not another guess.
        if (logClubPositionAtStrike && animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[OniBoss:Trail] {atk} trail START at clip {Mathf.Clamp01(st.normalizedTime):F2}"
                    + $" (transition {(animator.IsInTransition(0) ? "still blending" : "done")}).");
        }
    }

    /// <summary>
    /// ROUND 47. Lets go of the charge fire: unparented so it stays where the rush ended, all
    /// emission stopped so it draws no further, destroyed once its fade time is up.
    /// </summary>
    private void ReleaseChargeTrail()
    {
        if (chargeTrailInstance == null) return;
        chargeTrailInstance.transform.SetParent(null, true);

        // ROUND 48: the destroy timer must never cut the effect shorter than the prefab's OWN
        // persistence — the ribbon's Time and the particles' lifetime are Hazel's tuning knobs,
        // so the linger stretches to whichever is longest.
        float linger = Mathf.Max(0.5f, chargeTrailLifetime);
        foreach (var ps in chargeTrailInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var main = ps.main;
            linger = Mathf.Max(linger, main.startLifetime.constantMax + 0.25f);
        }
        foreach (var tr in chargeTrailInstance.GetComponentsInChildren<TrailRenderer>(true))
        {
            tr.emitting = false;
            linger = Mathf.Max(linger, tr.time + 0.25f);
        }
        Destroy(chargeTrailInstance, linger);
        chargeTrailInstance = null;
        DebugLog($"charge fire trail: released — burns out over {linger:F1}s where the rush ended.");
    }

    /// <summary>
    /// ROUND 28. Ends the trail. Unparented first so it stops being carried around by him and
    /// fades where the swing left it, instead of vanishing mid-arc.
    /// </summary>
    private void EndSwingTrail()
    {
        if (activeTrail == null) return;
        if (logClubPositionAtStrike && animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[OniBoss:Trail] trail END at clip {Mathf.Clamp01(st.normalizedTime):F2}, "
                    + $"fading over {trailFadeOut:F2}s.");
        }
        activeTrail.transform.SetParent(null, true);
        Destroy(activeTrail, Mathf.Max(0f, trailFadeOut));
        activeTrail = null;
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
            : transform.position + transform.forward * 1.2f + Vector3.up * strikeContactBodyHeight;

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
        UpdateClubTouch();              // ROUND 39 — the club's real hit; needs the posed skeleton
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

                    if (chargeGroundTrailVFX != null)
                    {
                        // ROUND 47 — 'ToonFireTrail' is a TRAIL-type effect: it only draws while
                        // its transform MOVES, so the stationary puffs of rounds 43-46 spawned
                        // provably (the log said so) yet rendered nothing. Attached to HIM for
                        // the rush, it paints the burning line itself as he travels — exactly
                        // "fire follows the oni's foot". Released and left to fade at rush end.
                        if (chargeTrailInstance == null)
                        {
                            chargeTrailInstance = Instantiate(chargeGroundTrailVFX,
                                transform.position + Vector3.up * Mathf.Max(0f, chargeTrailHeight),
                                Quaternion.LookRotation(transform.forward), transform);
                            foreach (var ps in chargeTrailInstance.GetComponentsInChildren<ParticleSystem>(true))
                                if (!ps.isPlaying) ps.Play(true);
                            foreach (var tr in chargeTrailInstance.GetComponentsInChildren<TrailRenderer>(true))
                            {
                                tr.Clear();
                                tr.emitting = true;
                            }
                            DebugLog($"charge fire trail: '{chargeGroundTrailVFX.name}' attached to his feet for the rush.");
                        }
                    }
                    else if (chargeTrailVFX)
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

        // ROUND 53 — the entrance cinematic starts with the roar: world slowed, Yoru frozen and
        // untouchable, the cinematic camera flying in. It ends at the TOP of the pound jump (or
        // right after this routine, if no pound follows) — never later.
        BeginPhaseCinematic();

        float speed = Mathf.Clamp(phaseTransitionAnimSpeed, 0.2f, 1.5f);
        float hold = Mathf.Max(0f, phaseTransitionHold);
        float startReal = Time.unscaledTime;
        float clock = 0f;   // ROUND 53: clip-seconds — runs at Cine Slow Motion speed while the cinematic is on
        float clipLen = -1f;
        bool roared = false, driving = false;

        DebugLog($"PHASE 2: transition '{phaseTransitionState}' driven at x{speed:F2} + {hold:F1}s hold — "
               + $"{(phaseTransitionInvulnerable ? "untouchable" : "vulnerable")}, roar at {phaseRoarNormalizedTime:F2}");

        while (true)
        {
            yield return null;
            if (combat == null || animator == null) break;

            // ROUND 53: the clip clock. Real seconds normally; while the cinematic runs it
            // advances at Cine Slow Motion speed — THAT is what makes the roar itself play in
            // slow motion (this drive writes the clip's time by hand, so Time.timeScale alone
            // could never slow it — the same property that makes it immune to hitstop).
            clock += Time.unscaledDeltaTime * (cineActive ? Mathf.Clamp(cineSlowMotion, 0.1f, 1f) : 1f);
            float elapsed = clock;
            DriveCinematicFrame(false);   // roar framing; a no-op when the cinematic is off

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

            if (elapsed > 10f) { DebugLog("PHASE 2: transition hard-capped at 10 clip-seconds"); break; }
            if (Time.unscaledTime - startReal > 30f) { DebugLog("PHASE 2: transition hard-capped at 30s REAL"); break; }
            if (combat.GetCurrentState() == EnemyCombat.EnemyState.Dead) break;
        }

        if (phaseTransitionInvulnerable && health != null) health.SetInvulnerable(wasInvulnerable);
        phaseRoarReached = true;
        phaseTransitionActive = false;
        phaseTransitionRoutine = null;

        DebugLog($"PHASE 2: transition over after {Time.unscaledTime - startReal:F2}s real — he is back, and angrier");

        // ROUND 52 — Hazel: the entrance chains STRAIGHT into the Ground Pound, like a combo, no
        // wait. The pound routine parks the engine itself and releases it when the slam is done.
        if (groundPoundEnabled && HasState(groundPoundState) && combat != null
            && combat.GetCurrentState() != EnemyCombat.EnemyState.Dead)
        {
            DebugLog("PHASE 2: chaining into the GROUND POUND — no pause. The cinematic rides along until the top of the jump.");
            if (poundRoutine != null) StopCoroutine(poundRoutine);
            poundRoutine = StartCoroutine(GroundPoundRoutine("phase-2 entrance"));
        }
        else
        {
            // ROUND 53: no pound to follow (state missing, disabled, or he died mid-roar) — the
            // cinematic ends HERE instead of at the jump apex. Same snap-back either way.
            EndPhaseCinematic("transition over, no pound to follow");
            if (combat != null && combat.GetCurrentState() == EnemyCombat.EnemyState.Stagger)
            {
                // End the window ourselves — do not wait for a game-time timer that Yoru's
                // slow-motion can stretch. Chase re-decides everything on the next frame.
                combat.SetState(EnemyCombat.EnemyState.Chase);
            }
        }
    }

    /// <summary>
    /// ROUND 52 — the Ground Pound, driven exactly like the phase transition (time written by
    /// hand, engine parked via the Stagger window): jump high, slam the floor at Pound Strike
    /// Moment, fire the shockwave + shake + impact effect, hand the engine back.
    /// </summary>
    private System.Collections.IEnumerator GroundPoundRoutine(string reason)
    {
        poundActive = true;
        nextPoundAllowedTime = Time.time + Mathf.Max(5f, poundRepeatCooldown);

        // ROUND 53: if the cinematic is running we arrived from the phase transition — it rides
        // along until the TOP of the jump. Repeat pounds arrive with it off and play as pure
        // gameplay, exactly like round 52.
        bool cineRide = cineActive;

        combat.TriggerStagger(3f);   // parks the agent and cancels attack/combo, same as the roar
        int hash = Animator.StringToHash(groundPoundState);
        animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);

        float speed = Mathf.Clamp(poundAnimSpeed, 0.2f, 2f);
        float startReal = Time.unscaledTime;
        float clock = 0f;   // clip-seconds — slowed while the cinematic rides (see the roar routine)
        float clipLen = -1f;
        bool slammed = false, driving = false;

        DebugLog($"GROUND POUND ({reason}): driving '{groundPoundState}' at x{speed:F2}, slam at {poundStrikeMoment:F2}"
               + (cineRide ? $", cinematic until the apex at {cineApexMoment:F2}" : ""));

        while (true)
        {
            yield return null;
            if (combat == null || animator == null) break;

            clock += Time.unscaledDeltaTime * (cineActive ? Mathf.Clamp(cineSlowMotion, 0.1f, 1f) : 1f);
            float elapsed = clock;
            DriveCinematicFrame(true);   // leap framing: the camera aims at his hips, wherever they fly

            if (clipLen < 0f)
            {
                clipLen = StateClipLength(hash);
                if (clipLen > 0f)
                {
                    driving = true;
                    DebugLog($"GROUND POUND: clip is {clipLen:F2}s → {clipLen / speed:F2}s on screen");
                }
            }

            if (driving)
            {
                float t = Mathf.Clamp01(elapsed * speed / clipLen);
                animator.Play(hash, 0, t);
                combat.SetStaggerTimer(2f);

                // ROUND 53 — the TOP of the jump: normal time, camera behind Yoru, control
                // returned. Everything after this line — the fall, the slam, the ring — is
                // normal gameplay she dodges like any other attack.
                if (cineActive && t >= cineApexMoment)
                    EndPhaseCinematic($"top of the jump at clip {t:F2}");

                if (!slammed && t >= poundStrikeMoment)
                {
                    slammed = true;
                    DoPoundImpact();
                    Debug.Log($"[OniBoss:Pound] SLAM at clip {t:F2} ({elapsed:F2} clip-s) — ring {poundRingSpeed:F0}m/s out to {poundRingMaxRadius:F0}m, {poundDamage} damage, jumpable.");
                }

                if (elapsed >= clipLen / speed) break;
            }

            if (elapsed > 8f) { DebugLog("GROUND POUND: hard-capped at 8 clip-seconds"); break; }
            if (Time.unscaledTime - startReal > 25f) { DebugLog("GROUND POUND: hard-capped at 25s REAL"); break; }
            if (combat.GetCurrentState() == EnemyCombat.EnemyState.Dead) break;
        }

        // ROUND 53: whatever ended the loop (apex is the normal path, death or a cap the rare
        // ones), the cinematic may not outlive the pound.
        if (cineActive) EndPhaseCinematic("pound ended");

        poundActive = false;
        poundRoutine = null;
        if (combat != null && combat.GetCurrentState() == EnemyCombat.EnemyState.Stagger)
            combat.SetState(EnemyCombat.EnemyState.Chase);

        // ROUND 53 — Hazel: after the cinematic entrance, the Oni gives Yoru a breath before his
        // first attack. Entering Stagger zeroed his cooldown clock, so without this he could
        // swing the instant he lands. Entrance only — repeats owe her nothing.
        if (cineRide && combat != null && cineFirstAttackGrace > 0f)
        {
            combat.RaiseAttackCooldown(cineFirstAttackGrace);
            Debug.Log($"[OniBoss:Cine] first-attack GRACE — at least {cineFirstAttackGrace:F1}s before he may swing.");
        }

        DebugLog("GROUND POUND: done — the engine is his again.");
    }

    /// <summary>
    /// ROUND 53 — the entrance cinematic, Hazel's cut: (1) phase 2 hits → this camera takes over,
    /// time slows, Yoru is frozen and untouchable; (2) the roar plays in slow motion, framed on
    /// him; (3) the camera follows his leap; (4) at the TOP of the jump everything snaps back —
    /// normal time, camera behind Yoru, control returned; (5) the slam and its ring are dodged in
    /// normal gameplay; (6) then a first-attack grace so the entrance is never a cheap hit.
    /// The shot is anchored ONCE, in front of him where the roar begins.
    /// </summary>
    private void BeginPhaseCinematic()
    {
        if (!cinematicEnabled || CameraGameFeel.Instance == null) return;
        if (cineActive) return;

        cineActive = true;
        cineTimeSlowed = true;
        cineWeight = 0f;
        if (cineBlendOutRoutine != null) { StopCoroutine(cineBlendOutRoutine); cineBlendOutRoutine = null; }

        Time.timeScale = Mathf.Clamp(cineSlowMotion, 0.1f, 1f);

        // Anchor the shot: step Cine Cam Side Angle degrees around his facing, walk out Cine Cam
        // Distance metres, rise Cine Cam Height. He faces Yoru when phase 2 triggers, so this is
        // a front three-quarter view of the roar.
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; else fwd.Normalize();
        Vector3 dir = Quaternion.AngleAxis(cineCamSideAngle, Vector3.up) * fwd;
        cineCamPos = transform.position + dir * Mathf.Max(2f, cineCamDistance) + Vector3.up * cineCamHeight;

        Debug.Log($"[OniBoss:Cine] CINEMATIC ON — world at x{Time.timeScale:F2}, Yoru frozen + untouchable, "
                + $"camera {cineCamDistance:F1}m in front of him, apex at pound clip {cineApexMoment:F2}.");
    }

    /// <summary>
    /// ROUND 53 — one cinematic frame, called from inside the roar and pound drives. Re-asserts
    /// the slow world clock EVERY frame (so nothing that also writes Time.timeScale can win for
    /// more than one frame), refreshes Yoru's freeze + protection (both expire by themselves
    /// moments after the last refresh — nothing can leak), and feeds the camera its pose:
    /// the anchored shot, aimed at his chest during the roar or at his hips during the leap.
    /// </summary>
    private void DriveCinematicFrame(bool followLeap)
    {
        if (!cineActive) return;

        Time.timeScale = Mathf.Clamp(cineSlowMotion, 0.1f, 1f);

        if (playerHealthRef != null)
        {
            playerHealthRef.ApplyStun(0.25f);          // the engine's own capture freeze — movement + attacks blocked
            playerHealthRef.SetCinematicGuard(0.3f);   // and NOTHING can hurt her (GATE 0.7 in PlayerHealth)
        }

        cineWeight = Mathf.MoveTowards(cineWeight, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, cineBlendIn));

        Vector3 look = (followLeap && travelBone != null)
            ? travelBone.position
            : transform.position + Vector3.up * cineLookHeight;
        Vector3 to = look - cineCamPos;
        if (CameraGameFeel.Instance != null && to.sqrMagnitude > 0.001f)
            CameraGameFeel.Instance.SetCinematicPose(cineCamPos, Quaternion.LookRotation(to.normalized, Vector3.up), cineWeight);
    }

    /// <summary>
    /// ROUND 53 — the snap-back, normally at the top of the jump: world clock to 1, Yoru's freeze
    /// and protection dropped THIS instant (the slam must be dodgeable gameplay), and the camera
    /// flying home over Cine Blend Out seconds while the fall already runs at full speed. Safe to
    /// call twice; also called from OnDisable so nothing can outlive the boss object.
    /// </summary>
    private void EndPhaseCinematic(string why)
    {
        if (cineTimeSlowed) Time.timeScale = 1f;
        cineTimeSlowed = false;
        if (!cineActive) return;
        cineActive = false;

        if (playerHealthRef != null)
        {
            playerHealthRef.ApplyStun(0f);          // control back NOW
            playerHealthRef.SetCinematicGuard(0f);  // and she can be hit again — the slam is honest
        }

        if (isActiveAndEnabled)
        {
            if (cineBlendOutRoutine != null) StopCoroutine(cineBlendOutRoutine);
            cineBlendOutRoutine = StartCoroutine(CineBlendOutRoutine());
        }
        else
        {
            cineWeight = 0f;
            if (CameraGameFeel.Instance != null) CameraGameFeel.Instance.ClearCinematicPose();
        }

        Debug.Log($"[OniBoss:Cine] CINEMATIC OFF ({why}) — normal time, control returned, camera home in {cineBlendOut:F2}s.");
    }

    /// <summary>ROUND 53 — walks the camera weight back to 0, still aiming at the falling body so
    /// the fly home has no snap. The gameplay camera under it is live the whole way.</summary>
    private System.Collections.IEnumerator CineBlendOutRoutine()
    {
        while (cineWeight > 0f && CameraGameFeel.Instance != null)
        {
            cineWeight = Mathf.MoveTowards(cineWeight, 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, cineBlendOut));
            Vector3 look = travelBone != null ? travelBone.position : transform.position + Vector3.up * cineLookHeight;
            Vector3 to = look - cineCamPos;
            if (to.sqrMagnitude > 0.001f)
                CameraGameFeel.Instance.SetCinematicPose(cineCamPos, Quaternion.LookRotation(to.normalized, Vector3.up), cineWeight);
            yield return null;
        }
        if (CameraGameFeel.Instance != null) CameraGameFeel.Instance.ClearCinematicPose();
        cineBlendOutRoutine = null;
    }

    /// <summary>ROUND 52. The landing itself: the biggest shake in the fight, the code-built ring,
    /// her impact prefab, and the expanding hit ring armed (checked per frame in UpdatePoundRing).</summary>
    private void DoPoundImpact()
    {
        poundRingCenter = transform.position;
        poundRingRadius = 0f;
        poundRingPrevRadius = 0f;
        poundRingSpent = false;
        poundRingActive = true;

        if (CombatFeedbackManager.Instance != null && poundShakeIntensity > 0f)
            CombatFeedbackManager.Instance.CameraShake(poundShakeIntensity, poundShakeDuration);
        ProceduralImpactFX.Shockwave(poundRingCenter, poundRingMaxRadius, 0.8f, new Color(1f, 0.45f, 0.1f));

        if (poundImpactVFX != null)
        {
            GameObject fx = Instantiate(poundImpactVFX, poundRingCenter + Vector3.up * 0.05f, Quaternion.identity);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                if (!ps.isPlaying) ps.Play(true);
            Destroy(fx, Mathf.Max(1f, poundImpactVFXLifetime));
        }
    }

    /// <summary>
    /// ROUND 52. The pound's shockwave: a ring expanding from the landing point. The sweep test
    /// (previous radius → current radius) means it cannot skip her between frames. Grounded =
    /// hit once for Pound Damage with a HEAVY reaction; airborne = it passes under her, the same
    /// jump rule as every wave.
    /// </summary>
    private void UpdatePoundRing()
    {
        if (!poundRingActive) return;

        poundRingPrevRadius = poundRingRadius;
        poundRingRadius += poundRingSpeed * Time.deltaTime;

        if (!poundRingSpent && playerT != null && playerHealthRef != null)
        {
            bool airborne = playerMoveRef != null && playerMoveRef.IsAirborne();
            if (!airborne)
            {
                Vector3 to = playerT.position - poundRingCenter;
                to.y = 0f;
                float d = to.magnitude;
                float halfW = Mathf.Max(0.2f, poundRingWidth) * 0.5f;
                if (d <= poundRingRadius + halfW && d >= poundRingPrevRadius - halfW)
                {
                    poundRingSpent = true;
                    playerHealthRef.TakeDamage(poundDamage, true, poundRingCenter, false);
                    SpawnHitLandVFX(hitLandVFX);
                    Debug.Log($"[OniBoss:Pound] shockwave CAUGHT Yoru (grounded) at {d:F1}m for {poundDamage}.");
                }
            }
        }

        if (poundRingRadius > poundRingMaxRadius) poundRingActive = false;
    }

    /// <summary>ROUND 52 — "only at phase 2 sometimes": once the cooldown since the last pound is
    /// over, a small per-second chance fires it again while he is chasing her in phase 2.</summary>
    private void UpdatePoundRepeat()
    {
        if (!groundPoundEnabled || poundActive || poundRepeatCooldown <= 0f) return;
        if (combat == null || animator == null || playerT == null) return;
        if (!combat.IsPhase2() || phaseTransitionActive) return;
        if (Time.time < nextPoundAllowedTime) return;
        if (combat.GetCurrentState() != EnemyCombat.EnemyState.Chase) return;
        if (Random.value >= poundRepeatChancePerSecond * Time.deltaTime) return;
        if (!HasState(groundPoundState)) return;

        if (poundRoutine != null) StopCoroutine(poundRoutine);
        poundRoutine = StartCoroutine(GroundPoundRoutine("phase-2 repeat"));
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
