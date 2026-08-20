using UnityEngine;
using System.Collections;

/// <summary>
/// YORU Combat System — Phase 3C v27
/// v20: EndDash/EndDodge StopCoroutine fix (insufficient — coroutines silently crash)
/// v21: ACTUAL FIX — time-based flag clearing that doesn't depend on coroutines:
///   - Dodge/dash timeouts tightened: +0.3s grace (was +1.0s) plus 2s absolute hard cap
///   - Movement-stuck safety: 0.5s WASD held + any flag stuck → force reset (was 1.5s + required no combat keys)
///   - These catch ALL orphan scenarios regardless of how the coroutine dies
/// v22: Guard movement-stuck false positive fix.
///   - Movement-stuck safety previously included isGuarding in the blocking-flag set.
///   - Holding Q + WASD to guard-walk (legitimate 0.75x speed via GuardMovementController)
///     after 1s caused the safety to ForceResetCombat → EndGuard, killing guard even
///     though Q was still held.
///   - Guard is excluded from the blocking-flag set. Three other safeties remain in place:
///     Q-released stuck check (lines ~342), animator-orphan detection (lines ~365),
///     and PlayerMovement's guard-routing in FixedUpdate. No protection is lost.
///   - Also caught up the init-log version string (was stuck at "v17").
/// v23: Parry animation restructure — separate Parry_Start clip from Parry idle loop.
///   - New parryStartState SerializeField (default "Parry_Start") — plays once when Q is pressed.
///   - parryIdleState is now a clean loop (no baked intro).
///   - parryIntroLength field repurposed: was "intro length within combined clip", now means
///     "duration of Parry_Start clip in seconds". Same field — Inspector value preserved.
///   - parryIntroComplete now genuinely flips false→true when start clip finishes
///     (previously only ever set to false in StartGuard, never used).
///   - UpdateGuardAnimation gains a Phase 1 / Phase 2 split:
///     Phase 1 = wait for Parry_Start to complete; Phase 2 = normal idle/walk selection.
///   - REMOVED: ~35-line "Parry idle loop skip" block (no longer needed with clean idle loop).
///   - REMOVED: special introAlreadyPlayed CrossFade with normalizedTimeOffset arg
///     (plain CrossFadeInFixedTime is correct for the new design).
///   - New blend-time case for parryStartState → idle/walk: 0.1s short blend
///     (start clip ends in parry pose, idle/walk begin in parry pose, so the blend is tight).
///   - Inspector action required: verify parryStartState matches your Animator state name,
///     and update parryIntroLength to match the actual duration of your Parry_Start clip.
/// v24: Q-release grace window — fixes parry breaking when pressing A/D while Q is held.
///   - Root cause: keyboard ghosting / N-key rollover. On many keyboards, pressing Q + A
///     or Q + D simultaneously causes the hardware to drop Q for 1+ frames. Unity sees
///     this as Input.GetKeyUp(Q) firing and ends guard, even though Q is still held.
///   - Fix: replace Input.GetKeyUp(Q) with polling-based detection plus an 80ms grace.
///     If Q reports as released but comes back within 80ms, treat as continuous hold.
///     If Q stays released past 80ms, end guard normally.
///   - Two new fields: qReleaseGraceTime (Inspector tunable, default 0.08s) and
///     qReleaseStartTime (runtime, tracks when Q first appeared released).
///   - 80ms is below human key-tap perception (~100ms) so guard doesn't feel sticky.
///     Above any keyboard ghost blip (typically 17-50ms at 60fps).
///   - The 0.5s guardStuckTimer safety net is unchanged and still catches true stuck cases.
/// v25: Parry guard offset smooth ramp on BOTH directions — fixes visible level snap on
///      idle->walk and the abrupt body drop on guard exit.
///   - Root cause: v22 used instant snap-up plus a 10-units-per-second descent. With any
///     non-trivial guardModelYOffset value, the body Y change happens in a single frame
///     while the animator CrossFade blends the pose over 0.1-0.25s. Body and animation
///     were moving on different timelines: at walk start the body would pop up before the
///     pose finished blending in (visible level change), and at guard exit the body would
///     drop out under the model in functionally one frame (abrupt pop). Trying to
///     compensate by lowering the offset value also lowered the lift below what the walk
///     clip actually needed, leaving paw tips clipping the ground.
///   - Fix: ramp the offset smoothly on both directions at a duration that approximately
///     matches the parry CrossFade blend times. Body Y and animation pose now move on the
///     same timeline. Once the ramp is smooth, guardModelYOffset can be tuned up to match
///     the walk clip's actual baked paw depth without the level change being visible.
///   - New Inspector field: guardOffsetRampDuration (default 0.2s). Speed is computed
///     dynamically as guardModelYOffset / guardOffsetRampDuration per second so the
///     visible ramp time stays constant regardless of the offset value chosen.
///   - Tuning order after applying:
///       1. Leave guardOffsetRampDuration at 0.2s for first test.
///       2. Bump guardModelYOffset up until paw tips visibly clear the ground during walk.
///          Likely target range 0.10-0.15 based on screenshot evidence at offset=0.05.
///       3. If walk-start still feels like a pop, raise guardOffsetRampDuration to 0.25s.
///          If guard-exit feels floaty, lower it to 0.15s.
///       4. Watch the idle->walk and guard-end transitions: body and paws should look like
///          one continuous motion, not a body pop with a separate paw shift.
///   - Nothing else in v22, v23 or v24 logic is touched. Single-block edit in LateUpdate.
/// v26: Parry guard offset gating fix — apply the lift across the ENTIRE guard envelope,
///      not just during walk states. Fixes the visible body rise at idle->walk transition.
///   - Root cause: v22 (carried into v25) gated guardModelYOffset on the walk states only
///     (parryWalkForwardState / parryWalkBackwardState). That gating was correct for the OLD
///     combined parry clip design where the idle pose had paws planted at ground level and
///     only the walk pose had paws baked low. With the v23 clean-clip rebuild, BOTH the
///     Parry_Start and Parry Idle clips have paws baked below the body root at the same
///     depth as the walk clips. So while idle, body was at original Y and paws were buried.
///     When walking started, body lifted to original Y + 0.10-0.15. The lift was correct
///     for walk but wrong for idle, so the player saw idle-clip paws clipping through
///     terrain followed by a body rise the moment WASD was pressed. That body rise was
///     the "getting up too early" symptom — body was anticipating the walk a fraction
///     ahead of the pose blend.
///   - Fix: gate guardModelYOffset on isGuarding (the whole guard envelope) instead of
///     on isGuardWalking (walks only). Body now sits at the lifted Y from Q-press through
///     Q-release. No visible level change between Parry_Start, Parry Idle, and the walks.
///     The smooth ramp introduced in v25 is unchanged and now does the work it was
///     designed for: covering the entry transition (Q press, ramp from 0 to offset over
///     guardOffsetRampDuration) and the exit transition (Q release, ramp back to 0).
///   - The walk-state detection variable (isGuardWalking) is removed from the LateUpdate
///     block since it's no longer needed there. currentGuardAnim is still used elsewhere
///     in UpdateGuardAnimation for state selection, so that field stays.
///   - Tuning context for guardOffsetRampDuration after this change:
///       Guard entry CrossFade is 0.15s (PlayGuardAnim). Guard exit CrossFade is 0.1s
///       (ReturnToIdle). Setting guardOffsetRampDuration around 0.10-0.15s keeps body Y
///       and pose blend roughly synchronized in both directions. Lower than 0.1 = body
///       drops faster than pose on exit (Yoru rises out of parry early). Higher than
///       ~0.2 = body lingers up after pose has settled (Yoru stays floating briefly).
///   - guardModelYOffset must equal the actual depth the parry clips bake the paws below
///     the body root. Likely 0.10-0.15 based on the screenshot evidence. Tune until paw
///     tips visibly clear the terrain in idle, walk, and Parry_Start.
///   - Nothing else in v22, v23, v24, or v25 logic is touched. Single-block edit in
///     LateUpdate plus a tooltip update on guardModelYOffset.
/// v27: Configurable guard exit blend time — fixes the abrupt parry-to-standing pose
///      snap when releasing Q.
///   - Root cause: ReturnToIdle() (line 1648) crossfades to combatIdleStateName with a
///     hardcoded 0.1s blend. EndGuard() called ReturnToIdle() on Q release. 0.1s = 6
///     frames at 60fps, which is brutally fast for a quadruped rising out of a low
///     defensive stance. The body Y offset descent (v25-v26) couldn't smooth this
///     because the offset is a 10-15cm vertical adjustment ON TOP of a much larger
///     pose change (crouch -> standing) happening in those 6 frames. Hazel correctly
///     reported that no value of guardOffsetRampDuration fixed the abrupt exit.
///   - Fix: add guardExitBlendTime SerializeField (default 0.3s, tunable). EndGuard()
///     now bypasses ReturnToIdle() and calls CrossFadeInFixedTime directly with this
///     duration. The shared ReturnToIdle() is left untouched so attack/dodge/dash
///     exits keep their existing 0.1s snap behavior, which is correct for those
///     fast-paced action recoveries.
///   - Tooltip on guardOffsetRampDuration updated: recommend matching guardExitBlendTime
///     so body Y descent and pose blend finish at the same moment. Mismatch leaves
///     the body either floating (ramp longer than blend) or clipping (ramp shorter
///     than blend) for the difference window.
///   - Combat responsiveness note: the longer pose blend does NOT block input. Yoru
///     can be commanded to attack/dodge during the 0.3s blend; the new action's
///     CrossFade will simply override the in-progress parry-to-idle blend. So this
///     change is purely visual and has no gameplay-feel cost.
///   - Tuning order after applying:
///       1. Set Guard Offset Ramp Duration to 0.3s (matches new exit blend default).
///       2. Test guard exit. Yoru should rise from parry crouch to standing over ~0.3s,
///          smoothly, like a cat unfolding from a tense pose.
///       3. If the exit feels too lazy/slow, lower both fields to 0.25 or 0.2.
///       4. If the exit still feels abrupt at 0.3s, the issue is somewhere else and
///          we keep digging (could be the parry-idle clip ending at a non-rest pose,
///          or the GuardMovementController disable having a side-effect).
/// Confirmed via logs: isDashing and isDodging both get orphaned by rapid input.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackPoint;

    [Header("Animation State Names — Combo")]
    [SerializeField] private string combo1StateName = "Combo1";
    [SerializeField] private string combo2StateName = "Combo2";
    [SerializeField] private string combo3StateName = "Combo3";
    [SerializeField] private string combatIdleStateName = "Combat_Idle";

    [Header("Animation State Names — Heavy Charge")]
    [Tooltip("Pull-back animation. Plays once when LMB hold passes the 0.3s gate.")]
    [SerializeField] private string heavyChargeWindUpState = "HeavyCharge_WindUp";
    [Tooltip("Held tension idle. Code crossfades into this once chargePercent reaches 1.0 — same pattern as parryIntroComplete.")]
    [SerializeField] private string heavyChargeHoldState = "HeavyCharge_Hold";
    [Tooltip("Release/strike animation. Plays when LMB is released at any charge level.")]
    [SerializeField] private string heavyReleaseState = "HeavyCharge_Release";

    [Header("Animation State Names — Hit Reaction")]
    [SerializeField] private string hitReactLight2Leg = "HitReact_Light_2Leg";
    [SerializeField] private string hitReactLight4Leg = "HitReact_Running_4Leg";
    [SerializeField] private string hitReactHeavy2Leg = "HitReact_Heavy_2Leg";
    [SerializeField] private string hitReactHeavy4Leg = "Bhit_run_reaction_4";

    [Header("Grab Reaction (Nopperabo close attack)")]
    [Tooltip("Reaction state played when an enemy grab catches Yoru while he is on 2 legs.")]
    [SerializeField] private string grabReact2LegState = "HitReact_Heavy_2Leg";
    [Tooltip("Frame the 2-leg grab reaction freezes on while held, out of its total frame count.")]
    [SerializeField] private int grabReact2LegFreezeFrame = 11;
    [SerializeField] private int grabReact2LegTotalFrames = 28;
    [Tooltip("Reaction state played when an enemy grab catches Yoru while he is on 4 legs (running).")]
    [SerializeField] private string grabReact4LegState = "Bhit_run_reaction_4";
    [Tooltip("Frame the 4-leg grab reaction freezes on while held, out of its total frame count.")]
    [SerializeField] private int grabReact4LegFreezeFrame = 25;
    [SerializeField] private int grabReact4LegTotalFrames = 76;
    [Tooltip("Frame the 4-leg grab reaction is cut on release, out of total frames. The clip is a run-style hit react, so without a cut its locomotion tail plays out while Yoru is still held in place and reads as running without moving. Cut it just after the recoil beat so it blends to idle instead. Keep this above the freeze frame.")]
    [SerializeField] private int grabReact4LegCutFrame = 50;
    [Tooltip("Blend time into and out of the grab reaction so the transitions stay smooth.")]
    [SerializeField] private float grabReactBlendIn = 0.08f;
    [SerializeField] private float grabReactBlendOut = 0.2f;
    [Tooltip("Safety: if the grab never signals release, the hold lets go after this many seconds.")]
    [SerializeField] private float grabReactMaxHold = 5f;

    [Header("Animation State Names — Dodge (frontflip)")]
    [SerializeField] private string dodge2LegState = "Dodge_2Leg";
    [SerializeField] private string dodge4LegState = "Dodge_4Leg";

    [Header("Animation State Names — Dash (rush)")]
    [SerializeField] private string dash2LegState = "DodgeDash_2Leg";
    [SerializeField] private string dash4LegState = "DodgeDash_4Leg";

    [Header("Animation State Names — Guard/Parry")]
    [SerializeField] private string parryStartState = "Parry_Start";
    [SerializeField] private string parryIdleState = "Parry";
    [SerializeField] private string parryWalkForwardState = "Parry_WalkForward";
    [SerializeField] private string parryWalkBackwardState = "Parry_WalkBackward";

    [Header("Hit Reaction Timing")]
    [SerializeField] private float lightHitReactDuration = 0.3f;
    [SerializeField] private float heavyHitReactDuration = 0.5f;
    [Tooltip("Cut the 4-leg heavy hit reaction (Bhit_run_reaction_4) at this frame and blend back to idle, so the clips run-cycle tail does not read as Yoru jogging in place. 0 = play the full duration with no cut.")]
    [SerializeField] private int heavy4LegHitCutFrame = 60;
    [Tooltip("Total frame count of the 4-leg heavy reaction clip, used to convert the cut frame into a normalized time.")]
    [SerializeField] private int heavy4LegHitTotalFrames = 76;
    [Tooltip("Safety ceiling in seconds for the clip-driven 4-leg heavy reaction. When a cut frame is set, the reaction no longer ends on Heavy Hit React Duration; it plays until the clip reaches the cut frame, and this value is only the hard stop if the clip somehow never gets there. Must be longer than the time the clip needs to reach the cut frame (frame 64 of 76 at 30fps is about 2.1s).")]
    [SerializeField] private float heavy4LegHitMaxHold = 2.5f;
    [Tooltip("Reaction duration for light hits that land while the magic-mushroom hallucination is active (the Mushroom strike itself). The default 0.3s light reaction is unreadable under the screen distortion, so these hits hold the light reaction clip this long instead. The clip still blends out early if it finishes before this time.")]
    [SerializeField] private float hallucinationHitReactDuration = 0.7f;

    [Header("Knockback Pull")]
    [SerializeField] private float pullDistance = 0.5f;
    [SerializeField] private float pullDuration = 0.15f;

    [Header("Layer Settings")]
    [SerializeField] private int combatLayerIndex = 1;

    [Header("Combo Settings")]
    [SerializeField] private float comboWindowTime = 2.0f;
    [SerializeField] private float attackCooldown = 0.1f;

    [Header("Damage")]
    [SerializeField] private int combo1Damage = 10;
    [SerializeField] private int combo2Damage = 20;
    [SerializeField] private int combo3Damage = 35;
    [Tooltip("Max bonus damage added at 100% charge. Base damage IS combo1Damage (the regular punch). Final = combo1Damage + chargePercent × this value. Default 100 → uncharged release = 10 dmg, fully charged = 110 dmg.")]
    [SerializeField] private int heavyChargeBonusMax = 100;
    [Tooltip("Duration in seconds for charge to fill from 0% to 100%. Should match the HeavyCharge_WindUp clip length (currently 2.1s) for visual sync, or set higher if you want the ring to fill slower than the wind-up plays.")]
    [SerializeField] private float heavyChargeTimeMax = 2.1f;
    [Tooltip("Length of the HeavyCharge_Release clip in seconds. ReleaseHeavyAttack schedules OnAttackEnd via Invoke at (clipLength - 0.1s) so Yoru returns to idle smoothly even if the animation event is missing from the clip. Same self-healing pattern as parryIntroLength.")]
    [SerializeField] private float heavyReleaseClipLength = 0.8f;
    [SerializeField] private int aerialSpinDamage = 25;

    [Header("Dodge — Distances (frontflip)")]
    [Tooltip("Forward distance for 2-leg frontflip")]
    [SerializeField] private float dodge2LegDistance = 3.0f;
    [Tooltip("Forward distance for 4-leg frontflip")]
    [SerializeField] private float dodge4LegDistance = 2.5f;

    [Header("Dodge — Arc")]
    [Tooltip("Height of the frontflip arc. 0 = flat, 1.5 = noticeable hop, 3 = big leap")]
    [SerializeField] private float dodgeHeight = 1.5f;

    [Header("Dodge — Timing")]
    [SerializeField] private float dodgeFallbackDuration = 0.87f;
    [SerializeField] private float iFrameStart = 0.08f;
    [SerializeField] private float iFrameEnd = 0.35f;
    [SerializeField] private float dodgeEarlyExitThreshold = 0.75f;

    [Header("Dash — Distances (RMB rush)")]
    [Tooltip("Forward distance for 2-leg dash")]
    [SerializeField] private float dash2LegDistance = 4.0f;
    [Tooltip("Forward distance for 4-leg dash")]
    [SerializeField] private float dash4LegDistance = 5.0f;

    [Header("Dash — Damage")]
    [SerializeField] private int dashDamage = 20;
    [SerializeField] private float dashHitRange = 1.8f;

    [Header("Dash — Timing")]
    [SerializeField] private float dashFallbackDuration = 0.5f;
    [Tooltip("I-frame start for dash (normalized 0-1)")]
    [SerializeField] private float dashIFrameStart = 0.05f;
    [Tooltip("I-frame end for dash (normalized 0-1)")]
    [SerializeField] private float dashIFrameEnd = 0.40f;

    [Header("Guard/Parry")]
    [Tooltip("Time window after Q press where a hit triggers perfect parry")]
    [SerializeField] private float perfectParryWindow = 0.2f;
    [Tooltip("Fraction of damage blocked by regular guard (0.7 = 70% blocked, 30% gets through)")]
    [SerializeField] private float guardDamageReduction = 0.7f;
    [Tooltip("Damage dealt to enemy on perfect parry counter")]
    [SerializeField] private int parryCounterDamage = 15;
    [Tooltip("Duration enemy is staggered after perfect parry")]
    [SerializeField] private float parryStaggerDuration = 1.2f;
    [Tooltip("Range to find closest attacking enemy for parry counter")]
    [SerializeField] private float parryCounterRange = 5f;
    [Tooltip("Duration of Parry_Start clip in seconds. After this elapses, idle/walk anims take over based on current input. Set this to match the actual length of your Parry_Start animation clip.")]
    [SerializeField] private float parryIntroLength = 1.26f;
    [Tooltip("CrossFade duration in seconds when guard ends (Q release) and Yoru transitions from parry pose back to combatIdleStateName. v27 default 0.3s. The previous behavior used the shared ReturnToIdle() with its hardcoded 0.1s blend, which was visibly too fast for a quadruped rising out of a low defensive stance. Range 0.25-0.4s typically reads as smooth and cat-like; lower values feel snappy/abrupt; higher values feel lazy. Does NOT block input — Yoru can attack/dodge mid-blend and the new action will override the in-progress crossfade, so this is a purely visual setting with no gameplay-feel cost.")]
    [SerializeField] private float guardExitBlendTime = 0.3f;
    [Tooltip("Grace window for Q-release detection. If Q reports as released but is pressed back down within this time, treat as continuous hold. Mitigates keyboard ghosting (Q dropped for 1+ frames when pressing A/D simultaneously). 0.08s default — below human key-tap perception (~0.1s) so guard doesn't feel sticky, above any keyboard ghost blip (~0.02-0.05s). Bump to 0.12 if interruptions still occur.")]
    [SerializeField] private float qReleaseGraceTime = 0.08f;
    [Tooltip("Y offset applied to bodyYoru during the ENTIRE guard envelope (Parry_Start + Parry Idle + parry walks) to lift paw tips off the ground. The v23 parry clips bake paw tips slightly below the body root in all three states at the same depth, so this single value covers all of them. Set this to the actual depth the clips bake the paws below the body root. Likely range 0.10-0.15. Set to 0 to disable. Pair with guardOffsetRampDuration so body and animation pose move together on guard entry and exit. v26: was previously gated on walk states only, which produced a visible body rise at idle->walk.")]
    [SerializeField] private float guardModelYOffset = 0.15f;
    [Tooltip("Duration in seconds for guardModelYOffset to ramp up (Q press / guard entry) or ramp down (Q release / guard exit). v27 recommendation: match this to guardExitBlendTime so the body Y offset descent finishes at the same moment as the parry-to-standing pose blend. Mismatch produces either a brief float (ramp longer than blend) or brief paw clipping (ramp shorter than blend) for the difference window. Default 0.3s matches the v27 default for guardExitBlendTime.")]
    [SerializeField] private float guardOffsetRampDuration = 0.3f;
    [Tooltip("Y offset applied to bodyYoru during dash to lift paw tips off ground")]
    [SerializeField] private float dashModelYOffset = 0.1f;
    [Tooltip("Visual model root (auto-finds bodyYoru). Offset during guard/dash for paw clipping fix.")]
    [SerializeField] private Transform visualModelRoot;

    [Header("Aerial Spin")]
    [Tooltip("Seconds between damage ticks while the spin runs. The spin hurts everything close to her for its whole length, in the air and on the ground, so this is how fast that damage repeats. 0.15 is about five hits across the clip.")]
    [SerializeField] private float aerialSpinTickInterval = 0.15f;
    [Tooltip("Damage per tick to every enemy in range. Kept separate from Aerial Spin Damage (the old single hit value) so the spin's total can be tuned without touching anything else.")]
    [SerializeField] private int aerialSpinTickDamage = 10;
    [Tooltip("Hard safety cap in seconds. The spin normally ends when its clip finishes; this only exists so an interrupted or missing clip can never leave Yoru spinning forever.")]
    [SerializeField] private float aerialSpinMaxTime = 2f;
    [Tooltip("Let Yoru run around while she finishes the spin on the ground, the way the Tasmanian Devil keeps travelling while spinning. Off means her feet stay planted until the spin ends, like every other attack.")]
    [SerializeField] private bool moveWhileSpinningOnGround = true;
    [Tooltip("How far down to look for the ground when working out how long until she lands. Used to settle her body before touchdown, so nothing snaps at the landing.")]
    [SerializeField] private float airtimeProbeDistance = 40f;

    [Header("Air Pose Height Pin")]
    [Tooltip("Stops the body dropping or popping when a combat animation takes over in mid air. The jump clips hold the body high in the POSE (they are Generic clips with no root motion node, so the rise is baked into the skeleton, not into the transform). Every combat and ability clip is authored standing at floor height. So the instant the combat layer takes the body in mid air, the body snaps down to floor height, and it pops back up when the layer hands back, while the transform never moves. This pins the body at the height the jump pose had, using the same bodyYoru offset the guard and dash fixes use. Physics, jump force, the jump clips and ground combat are all untouched.")]
    [SerializeField] private bool pinBodyHeightInAir = true;
    [Tooltip("Pose driven bone used to measure the body height above the transform. Auto-finds the name below. This is only read, never written.")]
    [SerializeField] private Transform poseHeightBone;
    [Tooltip("Bone searched for when Pose Height Bone is empty. Root_M is the top of the animated chain (DeformationSystem/root/Root_M).")]
    [SerializeField] private string poseHeightBoneName = "Root_M";
    [Tooltip("Real seconds to ease the pin out when the combat layer hands the body back while she is still in the air.")]
    [SerializeField] private float airPinReleaseRamp = 0.12f;
    [Tooltip("Seconds before touchdown that her body starts settling to standing height. The correction is faded out across this window so it reaches zero exactly as her paws land, which is why nothing snaps at the landing. Longer is gentler but starts the settle higher up.")]
    [SerializeField] private float airPinSettleBeforeLanding = 0.18f;
    [Tooltip("Log the measured pose height difference each time the pin engages. Useful once, noisy forever.")]
    [SerializeField] private bool logAirPin = true;

    [Header("Hitbox")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Safety")]
    [SerializeField] private float maxAttackDuration = 2f;

    [Header("Combo Chaining")]
    [Tooltip("ON = when the next combo step is already bought (a click was queued), it starts in the SAME frame the previous clip's OnAttackEnd event fires. OFF = old behavior, which crossfades to Combat_Idle first and lets Update pick the queued click up a frame later — that inserted blend toward idle is the visible 'the second punch plays halfway and snaps back to idle' pop.")]
    [SerializeField] private bool chainCombosWithoutIdle = true;
    [Tooltip("Prints one line per combo event (click, queue, drop, clip start, clip end + how far through the clip the end event fired). Turn on for one test if a combo link still looks cut: the clipProgress number on the OnAttackEnd line says whether the animation event is placed too early in that clip, which is an animation-authoring fix, not a code one.")]
    [SerializeField] private bool logComboTrace = true;
    [Tooltip("How much of the current combo clip must have played before a queued click is allowed to start the next step. The clips carry an OnCanQueueNextAttack animation event; if that event sits early in the timeline the next attack replaces the current one mid-swing, which is why the second punch looks cut in half. 0 = exact old behavior (chain the instant the event fires). 0.6 lets most of the swing read before the next one takes over. Raise toward 0.85 for heavier, more committed combos; lower for snappier cancels.")]
    [Range(0f, 1f)]
    [SerializeField] private float comboCancelMinProgress = 0.6f;

    [Header("Damage Flash (Zelda-style)")]
    [Tooltip("Tint Yoru red for a moment when she takes damage, the way Link flashes on being hit. Pure feedback — no ability, timing or hitbox is touched.")]
    [SerializeField] private bool damageFlashEnabled = true;
    [Tooltip("Colour Yoru is tinted on being hit.")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("How long the tint lasts, in REAL seconds so it does not stretch during aim slow-motion.")]
    [SerializeField] private float damageFlashDuration = 0.14f;
    [Tooltip("How many times the tint blinks on and off across that duration. 1 = a single solid flash, 2-3 = the classic blinking damage tell.")]
    [SerializeField] private int damageFlashBlinks = 2;

    [Tooltip("Spawn a code-built impact burst at the exact contact point on every landed hit. Needs no prefab, so 'I can't see anything for the VFX' is fixed immediately; assign Light/Heavy Hit Spark Prefab on YoruVFXManager later and turn this off.")]
    [SerializeField] private bool proceduralHitSpark = true;

    [Header("Combat Engagement (Form Transform Lock)")]
    [Tooltip("Seconds the player is considered 'engaged in combat' after the last hit exchanged either way (Yoru hits enemy, or enemy hits Yoru). Form transform is blocked during this window per GDD Doc 04 §4a. Same flag is the foundation for the deferred 'enemies remember combat' anti-exploit rule (5-10s per GDD).")]
    [SerializeField] private float engagedInCombatDuration = 5f;

    [Header("Combat Targeting (Soft Lock-On)")]
    [Tooltip("How far Yoru looks for an enemy to home onto when an attack starts.")]
    [SerializeField] private float targetingRange = 8f;
    [Tooltip("Half-cone in degrees in front of Yoru that counts for acquisition. 90 = a forward cone. Set to 180 for full all-around acquisition (Yoru will turn to face an enemy behind him).")]
    [SerializeField] private float targetingAngle = 90f;

    [Header("Combat Magnet / Lunge")]
    [Tooltip("Max distance Yoru slides toward the target per attack. He never slides further than this even if the enemy is far (no flying across the arena). Start 2-3m and tune by feel.")]
    [SerializeField] private float lungeMaxDistance = 2.5f;
    [Tooltip("How long the slide takes in seconds. Very small = light-speed cat feel. The slide always finishes before the punch connects.")]
    [SerializeField] private float lungeDuration = 0.06f;
    [Tooltip("Yoru tries to stop this far from the target so the enemy ends up inside attack range and the hit lands. Keep below Attack Range (Hitbox section).")]
    [SerializeField] private float lungeStopGap = 1.0f;
    [Tooltip("Turn speed toward the target while attacking (Slerp factor). Higher = snappier. The initial face on attack start is instant.")]
    [SerializeField] private float lungeFaceSpeed = 25f;
    [Tooltip("Damage multiplier applied ONLY when the lunge was capped short of the enemy and Yoru had to reach for the hit (the 'not so forceful' case). 1 = full damage, no reduction. Set below 1 (e.g. 0.6) if you want reaching hits to be weaker.")]
    [Range(0f, 1f)]
    [SerializeField] private float reachHitDamageMultiplier = 1.0f;
    [Tooltip("With NO enemy in range, attacks 1 and 2 stay planted (zero slide). Only the 3-hit combo finisher slides forward by this small amount, to give it weight without flying into empty space. The ledge check still applies, so this never carries Yoru off a cliff. Set 0 to keep the finisher planted too.")]
    [SerializeField] private float noTargetFinisherNudge = 0.5f;

    [Header("Launch (Zelda / Spider-Man snap) — round 6")]
    [Tooltip("ON = the attack slide is speed-based and may cover up to Launch Max Distance, so Yoru launches at the enemy on every ground hit and re-launches for the next one. The enemy is measured to its collider SURFACE (big bodies work) and line of sight is checked to its body centre (a bumpy floor between Yoru and the enemy's feet no longer blocks it). OFF = the old hop: at most Lunge Max Distance in Lunge Duration, line of sight to the enemy root.")]
    [SerializeField] private bool launchEnabled = true;
    [Tooltip("How far a single hit may launch her (metres). Enemies farther than Targeting Range are never targeted at all.")]
    [SerializeField] private float launchMaxDistance = 6f;
    [Tooltip("Launch speed in metres per second. 20 = 6 m in 0.3 s. Faster reads as a teleport; slower risks the punch connecting before she arrives.")]
    [SerializeField] private float launchSpeed = 20f;
    [Tooltip("Longest a launch may take (seconds) so it always finishes before the strike frame; the distance is capped to fit.")]
    [SerializeField] private float launchMaxDuration = 0.32f;
    [Tooltip("Grounded within this many seconds still counts as grounded for the launch — the CharacterController's isGrounded flickers on uneven floors, which silently killed the slide.")]
    [SerializeField] private float launchGroundedGrace = 0.15f;
    [Tooltip("Every ground attack moves her forward AT LEAST this far, even when she is already touching the enemy, so a swing always steps into the target instead of being thrown from standing. A body she is already against simply blocks it — the CharacterController stops her, so this can never push her through anything.")]
    [SerializeField] private float launchMinDistance = 0.8f;
    [Tooltip("Shortest a launch may take, seconds. The old 0.06s slide was over before the eye could see it — this is what made the snap invisible even when it was working. 0.12-0.16 reads as a pounce without delaying the punch.")]
    [SerializeField] private float launchMinDuration = 0.13f;

    [Header("Launch model — round 8")]
    [Tooltip("ON = the agreed three-case model. (A) No enemy at all: EVERY attack and every combo step still steps forward by Launch No Target Distance. (B) Enemy inside Launch Engage Distance: launch to its collider surface, as now. (C) Enemy beyond Launch Engage Distance: step forward only, never a part-way slide toward it. OFF = the old behaviour, where hits 1 and 2 stay planted with no enemy and a far enemy produces a clamped 6m slide that lands short. Default OFF because this script is shared by every fight; the Oni switches it on for his scene.")]
    [SerializeField] private bool launchWithNoTarget = false;
    [Tooltip("The step forward when there is nothing to launch at, metres — cases A and C. 0.9 is derived from Breath of the Wild's CutAddSpeedMax 0.15 / CutAddSpeedDec 0.012, which works out to about 0.94m of slide per swing. Applies to every attack and every combo step, so a 3-hit air combo carries her about 2.7m.")]
    [SerializeField] private float launchNoTargetDistance = 0.9f;
    [Tooltip("How close the enemy's COLLIDER SURFACE must be before an attack launches at it, metres. Inside this she launches; outside she steps forward instead. 4.5 is THE FINALS' shipped melee lunge distance (Season 11 raised it from ~3m). Measured to the surface, not the centre, so a 1.4m-radius boss is engaged from 5.9m of centre distance.")]
    [SerializeField] private float launchEngageDistance = 4.5f;
    [Tooltip("How far short of the enemy's COLLIDER SURFACE the launch aims to stop, metres. 0 = launch all the way to him and let her own capsule collide with his body, which is what 'launch to the enemy' means. The old Lunge Stop Gap of 1.0 m is why she stopped a metre short of a boss she was already next to. Raise this only if she ends up visibly inside something.")]
    [SerializeField] private float launchStopGap = 0f;
    [Tooltip("ROUND 9. Downward speed (m/s) held during a launch that STARTED on the ground, so the CharacterController keeps touching the floor while she slides. Move() with a purely horizontal vector clears isGrounded, which made her skim off the ground for the whole slide: AirPosePin engaged within 5ms of every single launch, her body bobbed 3-13cm, and PlayerMovement began applying fall gravity mid-attack. 2 is enough to hold contact without reading as a drop. 0 disables it.")]
    [SerializeField] private float launchGroundStick = 2f;
    [Tooltip("ROUND 9, opt-in. ON = airborne attacks launch and step forward too, exactly like grounded ones. The launch is horizontal only - PlayerMovement keeps full ownership of the fall, so this cannot produce the height drop the old airborne path had. OFF = the old behaviour, where StartLunge returns early in the air and an air attack never moves her.")]
    [SerializeField] private bool launchInAir = false;

    [Header("Lunge Safety")]
    [Tooltip("Stop the slide at ledges so Yoru never lunges off a cliff.")]
    [SerializeField] private bool useEdgeSafety = true;
    [Tooltip("How far below Yoru's feet still counts as solid ground for the edge check. If the ground ahead drops more than this, the slide stops at the edge.")]
    [SerializeField] private float edgeProbeDepth = 1.2f;
    [Tooltip("Layers treated as ground for the edge check AND as blockers for line of sight (so Yoru will not target or lunge at an enemy behind a wall). Default is Everything; set this to your ground/terrain/wall layers for best results.")]
    [SerializeField] private LayerMask environmentMask = ~0;
    [Tooltip("ROUND 9. Layers the EDGE PROBE accepts as solid floor. Deliberately SEPARATE from Environment Mask, because Environment Mask is also the line-of-sight mask: widening that one so the probe can see the floor would make the floor block targeting. Everything (the default) is correct for almost every scene - the probe skips Yoru's own body by root, so it can never see itself. Leave at Everything unless you have floors you specifically want her to refuse to step on.")]
    [SerializeField] private LayerMask edgeGroundMask = ~0;

    [Header("Combo 3 Beyblade Finisher")]
    [Tooltip("Hard time cap in seconds for the spin against a crowd, so a big group can never trap the player in an endless beyblade. A single enemy ends much sooner (one strike plus Beyblade Single Wind Down).")]
    [SerializeField] private float beybladeMaxTime = 1.5f;
    [Tooltip("Seconds between each enemy getting struck during the beyblade. Small = fast one-by-one hits.")]
    [SerializeField] private float beybladeHitInterval = 0.12f;
    [Tooltip("Single enemy only: after the one strike, Yoru spins this many seconds before stopping, so the spin reads instead of snapping straight back to idle. A crowd ignores this and keeps spinning until Beyblade Max Time.")]
    [SerializeField] private float beybladeSingleWindDown = 0.3f;
    [Tooltip("ON = after the single-target strike, keep spinning until the Combo3 clip has actually finished (capped by Beyblade Max Time) instead of cutting to idle after the wind-down. OFF = old behavior: the swirl ended ~0.36s in, at 40% of its clip, and snapped to idle.")]
    [SerializeField] private bool beybladeSingleLetClipFinish = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showHitboxGizmo = true;
    #endregion

    #region Private Fields
    private CharacterController characterController;
    private Transform cachedTransform;
    private PlayerMovement playerMovement;
    private YoruVFXManager vfxManager;
    private GuardMovementController guardMovement;
    private Camera mainCamera;
    private FormController formController;
    private PlayerHealth playerHealth;

    // Combo
    private int currentComboStep;
    private float lastAttackTime;
    private bool isAttacking;
    private bool canQueueNextAttack;
    private int queuedClicks;

    // Aerial
    private bool isAerialAttack;
    private bool hasUsedAerialAttack;

    // Heavy
    private bool isChargingHeavy;
    private float heavyChargeStartTime;
    private float storedHeavyChargePercent;
    private bool chargeHoldStarted;        // true once the WindUp animation clip has finished and Hold loop has been crossfaded in (single-fire per charge)
    private bool chargeReadyAnnounced;     // true once chargePercent crossed 1.0 and the ready audio/UI cue fired (separate from animation transition)

    // Input
    private float attackButtonHoldTime;

    // Safety
    private float attackStartTime;

    // Combat engagement — see engagedInCombatDuration serialized field above
    private float engagedInCombatUntil;

    // Position lock
    private bool lockPosition;
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private bool wasGroundedWhenLocked;

    // Hit reaction
    private bool isInHitReaction;
    private float hitReactionEndTime;
    private Coroutine hitReactSafetyCoroutine; // Backup force-clear (independent of UpdateHitReaction)
    private Coroutine hitReactHoldCoroutine; // Holds the hit reaction state visible for its duration

    // Grab reaction (enemy close-attack capture)
    private bool isInGrabReaction;
    private bool grabReleaseRequested;
    private Coroutine grabReactionCoroutine;

    // Dodge (frontflip — C)
    private bool isDodging;
    private float dodgeStartTime;
    private float currentDodgeDuration;
    private Quaternion dodgeLockedRotation;
    private Coroutine dodgeCoroutine;
    private bool hasUsedAirDodge;
    private float dodgeEndTime;

    // Dash (rush — MMB)
    private bool isDashing;
    private float dashStartTime;
    private float currentDashDuration;
    private Quaternion dashLockedRotation;
    private Coroutine dashCoroutine;
    private bool hasUsedAirDash;

    // Guard/Parry (Q)
    private bool isGuarding;
    private float guardStartTime;
    private float guardEndTime;       // cooldown — prevents rapid Q tap from corrupting Animator
    private string currentGuardAnim;
    private float lastCombatCrossFadeTime; // tracks last CrossFade on combat layer for health check
    private bool parryIntroComplete;       // true once Parry anim has played past the intro frames
    private Vector3 originalModelLocalPos;  // cached to restore after guard/dash Y offset
    private float guardStuckTimer;          // tracks how long isGuarding is true while Q not held
    private float qReleaseStartTime = -1f;  // -1 = Q held; >=0 = timestamp when Q first appeared released (v24 grace window)
    private float heavyStuckTimer;          // tracks how long isChargingHeavy is true while LMB not held
    private float guardIdleDebounceTimer;   // prevents flicker during quick walk direction changes (W→S passes through 0)
    private bool modelOffsetActive;         // true when bodyYoru Y offset is applied
    private float currentModelYOffset;      // current interpolated offset for smooth transitions

    // Air pose height pin. airPinComp is the metres currently added to bodyYoru to cancel the
    // jump-pose-versus-combat-pose height difference; airPinHeldPoseY is the body height above
    // the transform captured on the frame the pin engaged (the height the jump pose was giving).
    private float airPinComp;
    private float airPinHeldPoseY;
    private bool airPinEngaged;
    private int combatIdleHash;

    // Fall acceleration measured live from the controller, so the remaining airtime estimate uses
    // whatever gravity PlayerMovement actually applies instead of assuming Physics.gravity.
    private float prevVelocityY;
    private float measuredFallAccel;
    private readonly RaycastHit[] groundProbeBuffer = new RaycastHit[8];

    // Aerial spin lifecycle. The routine owns the spin from the press until the clip finishes,
    // straight through the landing, and ticks damage the whole way.
    private Coroutine aerialSpinCoroutine;
    private bool aerialSpinTicking;
    private float combatIdleSettledTimer;   // tracks how long Animator combat layer has been in idle
    private float movementStuckTimer;       // tracks how long WASD held while movement blocked by combat flags

    // Pull
    private Coroutine pullCoroutine;

    // Combat magnet / lunge
    private Transform currentLungeTarget;   // enemy acquired for the current attack (null = no target, lunge straight forward)
    private Collider currentLungeCollider;  // its collider (surface distance for the launch)
    private float lastGroundedTime = -10f;  // grounded grace for the launch
    private Coroutine lungeCoroutine;       // active slide toward the target
    private bool lungeEndedShort;           // true when the slide was capped before fully closing (drives reachHitDamageMultiplier)

    // Beyblade finisher (Combo 3)
    private bool isBeyblading;              // true while the looping spin is ticking damage one enemy at a time
    private Coroutine beybladeCoroutine;
    private int beybladeRotationIndex;      // round-robins crowd targets so the spin works the room and circles back
    private int combo3StateHash;            // cached hash of combo3StateName for the spin loop check

    // Animation hashes
    private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int HashComboStep = Animator.StringToHash("ComboStep");
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        characterController = GetComponent<CharacterController>();
        cachedTransform = transform;
        playerMovement = GetComponent<PlayerMovement>();
        vfxManager = GetComponent<YoruVFXManager>();
        guardMovement = GetComponent<GuardMovementController>();
        mainCamera = Camera.main;
        formController = GetComponent<FormController>();
        playerHealth = GetComponent<PlayerHealth>();

        // Phase 2 diagnostic — print ONCE so we can verify the gate is connected.
        if (formController != null)
            Debug.Log("[PlayerCombat] Phase 2 form gate CONNECTED (FormController found on this GameObject).");
        else
            Debug.LogError("[PlayerCombat] Phase 2 form gate NOT CONNECTED — FormController is NULL on this GameObject. Combat input WILL fire in Granny form. Check that FormController is on the same GameObject as PlayerCombat.");

        if (attackPoint == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(cachedTransform);
            ap.transform.localPosition = new Vector3(0f, 1f, 1f);
            attackPoint = ap.transform;
            Debug.LogWarning("[Combat] WARNING: AttackPoint not assigned in Inspector! Auto-created at (0,1,1).");
        }

        if (animator != null)
            animator.SetLayerWeight(combatLayerIndex, 1f);

        combo3StateHash = Animator.StringToHash(combo3StateName);

        if (guardMovement == null)
            Debug.LogWarning("[Combat] WARNING: GuardMovementController not found! Add it to PlayerYoru_Def.");

        // Auto-find visual model root for guard/dash Y offset (paw clipping fix)
        if (visualModelRoot == null)
        {
            visualModelRoot = cachedTransform.Find("bodyYoru");
            if (visualModelRoot == null)
                Debug.LogWarning("[Combat] WARNING: visualModelRoot (bodyYoru) not found! Assign in Inspector.");
        }
        if (visualModelRoot != null)
            originalModelLocalPos = visualModelRoot.localPosition;

        // Air pose height pin setup. The bone is only ever READ, to measure how high the current
        // pose is holding the body above the transform.
        combatIdleHash = Animator.StringToHash(combatIdleStateName);
        if (poseHeightBone == null && !string.IsNullOrEmpty(poseHeightBoneName))
        {
            Transform searchRoot = visualModelRoot != null ? visualModelRoot : cachedTransform;
            foreach (Transform t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == poseHeightBoneName) { poseHeightBone = t; break; }
            }
        }
        if (pinBodyHeightInAir && poseHeightBone == null)
            Debug.LogWarning("[Combat] Air pose pin OFF: bone '" + poseHeightBoneName
                + "' not found. Assign Pose Height Bone on PlayerCombat, or the mid air body"
                + " snap cannot be cancelled.");

        DebugLog("PlayerCombat initialized — Phase 3C v27");
    }

    private void Update()
    {
        if (characterController != null && characterController.isGrounded) lastGroundedTime = Time.time;
        EnforcePositionLock();
        // Sampled before input, so the airtime window the aerial spin checks is this frame's value.
        TrackFallAcceleration();
        UpdateHitReaction();
        HandleInput();
        CheckGroundedStatus();

        if (isGuarding)
            UpdateGuardAnimation();

        // Continuous soft lock-on during attacks. Yoru fast-turns to keep facing the acquired enemy
        // (so a strafing enemy stays in front for the next hit). Skipped during the beyblade, where
        // the spin animation owns rotation.
        if (isAttacking && !isBeyblading && !isInHitReaction && !(playerHealth != null && playerHealth.IsStunned()))
            TrackTarget();

        // Deferred combo chain: the clip's OnCanQueueNextAttack event fired too early, so the next
        // step was held back until the current swing had actually read. Fire it the moment it has.
        if (pendingChain)
        {
            if (!isAttacking || isInHitReaction || isDodging || isDashing || isGuarding || queuedClicks <= 0)
            {
                pendingChain = false;
            }
            else if (CombatClipProgress() >= comboCancelMinProgress)
            {
                pendingChain = false;
                queuedClicks--;
                PerformGroundCombo();
            }
        }

        if (!isAttacking && !isInHitReaction && !isDodging && !isDashing && !isGuarding
            && queuedClicks > 0 && currentComboStep > 0 && currentComboStep < 3)
        {
            queuedClicks--;
            PerformGroundCombo();
        }

        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("Safety: attack timeout");
            ForceResetCombat();
        }

        // Dodge timeout — tightened from +1.0s to +0.3s, plus 2s absolute hard cap
        if (isDodging)
        {
            float dodgeElapsed = Time.time - dodgeStartTime;
            if (dodgeElapsed > currentDodgeDuration + 0.3f || dodgeElapsed > 2.0f)
            {
                DebugLog($"Safety: dodge timeout ({dodgeElapsed:F2}s, expected {currentDodgeDuration:F2}s)");
                EndDodge();
            }
        }

        // Dash timeout — same tightening
        if (isDashing)
        {
            float dashElapsed = Time.time - dashStartTime;
            if (dashElapsed > currentDashDuration + 0.3f || dashElapsed > 2.0f)
            {
                DebugLog($"Safety: dash timeout ({dashElapsed:F2}s, expected {currentDashDuration:F2}s)");
                EndDash();
            }
        }

        // Guard safety — if Q not held but isGuarding stuck, Q release was missed (rapid input)
        // Uses accumulator instead of timestamp: only counts continuous frames where Q is up
        if (isGuarding)
        {
            if (!Input.GetKey(KeyCode.Q))
            {
                guardStuckTimer += Time.deltaTime;
                if (guardStuckTimer > 0.5f)
                {
                    DebugLog("Safety: guard stuck (Q released but isGuarding true) — forcing EndGuard");
                    EndGuard();
                    guardStuckTimer = 0f;
                }
            }
            else
            {
                guardStuckTimer = 0f; // Q is held, not stuck
            }
        }
        else
        {
            guardStuckTimer = 0f;
        }

        // Heavy charge safety — if LMB not held but isChargingHeavy stuck
        if (isChargingHeavy)
        {
            // Animation transition: WindUp clip end → Hold loop.
            // Detected via animator state info (normalizedTime >= 1.0 on the WindUp state)
            // so the held pose plays for the entire wait regardless of heavyChargeTimeMax.
            // Decoupled from chargePercent — same single-fire pattern as parryIntroComplete.
            if (!chargeHoldStarted)
            {
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (currentState.IsName(heavyChargeWindUpState) && currentState.normalizedTime >= 1f)
                {
                    chargeHoldStarted = true;
                    PlayCombatAnimation(heavyChargeHoldState);
                    DebugLog("WindUp clip ended — crossfaded to Hold");
                }
            }

            // UI / audio cue: charge ready at 100%. Separate from animation transition above —
            // this fires whenever the percent crosses 1.0, regardless of animation state.
            if (!chargeReadyAnnounced && GetHeavyChargePercent() >= 1f)
            {
                chargeReadyAnnounced = true;
                if (CombatSFXManager.Instance != null)
                    CombatSFXManager.Instance.PlayHeavyChargeReady();
                DebugLog("Heavy charge ready (100%)");
            }

            if (!Input.GetMouseButton(0))
            {
                heavyStuckTimer += Time.deltaTime;
                if (heavyStuckTimer > 0.5f)
                {
                    DebugLog("Safety: heavy charge stuck (LMB released but isChargingHeavy true) — resetting");
                    CancelHeavyCharge();
                    ReturnToIdle();
                    heavyStuckTimer = 0f;
                }
            }
            else
            {
                heavyStuckTimer = 0f;
            }
        }
        else
        {
            heavyStuckTimer = 0f;
        }

        // === Animator-driven orphan flag detection ===
        // If the combat layer has settled in CombatIdle for 0.3s+ but any flag is stuck → reset.
        // Timer is reset to 0 at every action start (StartGuard, PerformDodge, etc.) so it
        // never false-positives during the CrossFade transition from idle into the action.
        if (animator != null)
        {
            AnimatorStateInfo combatState = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            bool animatorInIdle = combatState.IsName(combatIdleStateName)
                && !animator.IsInTransition(combatLayerIndex);

            if (animatorInIdle)
                combatIdleSettledTimer += Time.deltaTime;
            else
                combatIdleSettledTimer = 0f;

            bool anyFlagStuck = isAttacking || isDodging || isDashing || isGuarding
                || isChargingHeavy || isInHitReaction;

            if (combatIdleSettledTimer > 0.3f && anyFlagStuck)
            {
                DebugLog($"Safety: Animator idle but flags stuck (atk={isAttacking} dod={isDodging} dsh={isDashing} grd={isGuarding} hvy={isChargingHeavy} hit={isInHitReaction}) — forcing reset");
                ForceResetCombat();
                combatIdleSettledTimer = 0f;
            }
        }

        // === Movement-stuck safety net ===
        // If WASD held for 1.0s while any combat flag blocks movement → force reset.
        // Timer resets at every action start (PerformDodge, PerformDash, etc.) so it
        // only fires AFTER the expected action duration. 1.0s is longer than any single
        // action (max is dodge at 0.87s) but shorter than the old 1.5s.
        //
        // GUARD IS EXCLUDED. Guard allows legitimate movement at 0.75x via
        // GuardMovementController, so holding Q+WASD is normal play, not a stuck state.
        // Guard has its own safeties: the Q-released stuck check above (lines ~312)
        // and the animator-orphan detection (lines ~365) catch real stuck-guard cases.
        {
            bool anyFlagBlocking = isAttacking || isChargingHeavy || isDodging || isDashing;
            bool wasdHeld = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
                || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

            if (anyFlagBlocking && wasdHeld)
            {
                movementStuckTimer += Time.deltaTime;
                if (movementStuckTimer > 1.0f)
                {
                    DebugLog($"Safety: movement stuck 1.0s (atk={isAttacking} dod={isDodging} dsh={isDashing} hvy={isChargingHeavy} hit={isInHitReaction} animSpeed={animator?.speed}) — forcing reset");
                    if (animator != null) animator.speed = 1f;
                    ForceResetCombat();
                    movementStuckTimer = 0f;
                }
            }
            else
            {
                movementStuckTimer = 0f;
            }
        }
    }

    private void LateUpdate()
    {
        EnforcePositionLock();
        if (isDodging)
            cachedTransform.rotation = dodgeLockedRotation;
        if (isDashing)
            cachedTransform.rotation = dashLockedRotation;

        // Per-frame model Y offset — prevents paw tips from clipping underground.
        // Runs in LateUpdate so it applies AFTER animation poses are set.
        // Guard: offset applied for the entire isGuarding window (v26 — was walk-only in v22-v25).
        // Dash: offset applied for the isDashing window.
        // Both use the smooth ramp introduced in v25 (single MoveTowards on entry and exit).
        if (visualModelRoot != null)
        {
            // v26: offset applies during the ENTIRE guard envelope, not just walks.
            // The v23 clean parry clips have paws baked below the body root in Parry_Start,
            // Parry Idle, AND the walks at the same depth. v22's walk-only gating produced
            // a visible body rise at idle->walk because idle was at original Y and walk was
            // at original + offset. Body now sits at lifted Y from Q-press through Q-release,
            // so there's no level change within the guard envelope. The smooth ramp from v25
            // (the MoveTowards block below) handles the entry and exit transitions only.
            float targetOffset = 0f;
            if (isGuarding)
            {
                targetOffset = guardModelYOffset;
            }
            else if (isDashing)
            {
                targetOffset = dashModelYOffset;
            }

            // v25: Smooth ramp on BOTH directions (was: instant up, fast descent at 10 units/sec).
            // The old design tried to prevent a 1-2 frame paw dip at walk-start by snapping the body
            // up instantly, but with the v23 clean walk clips the snap itself became the visible
            // problem: body popped up while the animation pose was still blending in idle, producing
            // a visible level change. The 10-units/sec descent at walk-end was so fast it
            // functionally snapped down too, producing an abrupt body drop on guard exit.
            // Ramp duration matches the parry CrossFade blend times (0.1-0.25s) so body Y and the
            // animation pose move on the same timeline. Speed is computed as offset / duration so
            // the visible ramp time stays constant regardless of the offset value chosen.
            // Fallback to the old fast rate if either field is configured at zero, so the behavior
            // degrades gracefully rather than getting stuck mid-ramp.
            float rampSpeed = (guardModelYOffset > 0.001f && guardOffsetRampDuration > 0.001f)
                ? guardModelYOffset / guardOffsetRampDuration
                : 10f;
            currentModelYOffset = Mathf.MoveTowards(currentModelYOffset, targetOffset, Time.deltaTime * rampSpeed);

            // Air pose height pin, composed into the same single write below.
            UpdateAirPoseHeightPin();

            float totalOffset = currentModelYOffset + airPinComp;
            bool needsOffset = Mathf.Abs(totalOffset) > 0.001f;
            if (needsOffset)
            {
                Vector3 pos = originalModelLocalPos;
                pos.y += totalOffset;
                visualModelRoot.localPosition = pos;
                modelOffsetActive = true;
            }
            else if (modelOffsetActive)
            {
                visualModelRoot.localPosition = originalModelLocalPos;
                currentModelYOffset = 0f;
                modelOffsetActive = false;
            }
        }
    }

    /// <summary>
    /// Cancels the mid air body snap.
    ///
    /// Why it exists: every clip in this project is Generic with no root motion node, so each one
    /// carries its own absolute body height inside the POSE. The jump clips hold the body high;
    /// the combat and ability clips are authored standing at floor height. The combat layer is
    /// unmasked and full body at weight 1, so the moment any combat state takes the body in mid
    /// air the body drops to floor height, and it pops back up when the layer hands back. The
    /// transform never moves, which is why the world stays steady and only the body jumps.
    ///
    /// How it works: while airborne and the combat layer owns a real state (anything other than
    /// the empty idle), the body height above the transform is held at whatever the jump pose was
    /// giving on the frame the pin engaged. The compensation is recomputed every frame from the
    /// live pose, so it tracks crossfades exactly instead of guessing a constant, and it falls to
    /// zero by itself as the layer blends back to the jump pose. Landing eases it out, since the
    /// poses agree on the ground. Only bodyYoru's local Y is touched: no physics, no transform, no
    /// jump force, no clip, no importer setting, and nothing changes while grounded.
    /// </summary>
    private void UpdateAirPoseHeightPin()
    {
        if (!pinBodyHeightInAir || poseHeightBone == null || animator == null)
        {
            airPinComp = 0f;
            airPinEngaged = false;
            return;
        }

        // Body height the CURRENT pose is producing, with last frame's compensation removed so the
        // measurement stays raw.
        float rawPoseY = poseHeightBone.position.y - cachedTransform.position.y - airPinComp;

        bool airborne = characterController != null && !characterController.isGrounded;
        int combatState = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).shortNameHash;
        if (animator.IsInTransition(combatLayerIndex))
            combatState = animator.GetNextAnimatorStateInfo(combatLayerIndex).shortNameHash;
        // The tail air shot owns her body for its WHOLE length, not only while a cast clip happens
        // to be playing. Between shots it parks the combat layer back on the empty state, and
        // without this line the pin let go there and grabbed again on the next draw, so her body
        // sank and rose once per shot. That cycling is the up and down she is seeing. Counting the
        // ability itself as owning the body means one height is captured when she presses R and
        // held until she lands or lets go of R, so nothing moves between shots.
        // The 4 leg air shot (TailAimController4Leg) needs exactly the same treatment. It is a
        // separate script with its own flags, and leaving it out here would bring the sink and
        // rise back for every 4 leg shot.
        bool tailShotActive = TailAimController.IsAiming || TailAimController.IsShotRunning
            || TailAimController4Leg.IsAiming || TailAimController4Leg.IsShotRunning;
        bool combatOwnsBody = combatState != combatIdleHash || tailShotActive;

        if (airborne && combatOwnsBody)
        {
            if (!airPinEngaged)
            {
                // Engage on the frame the takeover starts, holding the height the jump pose had.
                airPinEngaged = true;
                airPinHeldPoseY = rawPoseY;
                if (logAirPin)
                    Debug.Log("[AirPosePin] engaged, holding body at " + airPinHeldPoseY.ToString("F3")
                        + "m above the transform");
            }
            float correction = airPinHeldPoseY - rawPoseY;

            // Settle before touchdown. Her body has to come from its in air height down to standing
            // height at some point; doing it at the landing frame is the snap. So the correction is
            // faded out across the last moments of the fall and reaches zero exactly as she lands,
            // leaving nothing to move at the landing itself.
            if (airPinSettleBeforeLanding > 0.001f)
            {
                float timeToLand = EstimateTimeToLand();
                if (timeToLand >= 0f)
                    correction *= Mathf.Clamp01(timeToLand / airPinSettleBeforeLanding);
            }

            airPinComp = correction;
            return;
        }

        if (airPinEngaged && logAirPin)
            Debug.Log("[AirPosePin] released, peak correction was " + airPinComp.ToString("F3") + "m");
        airPinEngaged = false;

        // On the ground the correction is already zero, because the settle above finished it during
        // the fall. Anything left is rounding, so clear it rather than ease it and risk a float.
        if (!airborne)
        {
            airPinComp = 0f;
            return;
        }

        // Still airborne, so the combat layer handed the body back mid air. The pose blends home on
        // its own, so unwind gently and let the two move together.
        float ease = airPinReleaseRamp > 0.001f ? Time.unscaledDeltaTime / airPinReleaseRamp : 1f;
        airPinComp = Mathf.MoveTowards(airPinComp, 0f, Mathf.Abs(airPinComp) * ease + 0.0005f);
    }

    private void EnforcePositionLock()
    {
        if (!lockPosition || isDodging || isDashing || characterController == null || !wasGroundedWhenLocked)
            return;
        characterController.enabled = false;
        cachedTransform.position = lockedPosition;
        // Rotation NOT locked. Facing is handled by TrackTarget during combos and heavy.
        characterController.enabled = true;
    }

    private void CheckGroundedStatus()
    {
        if (characterController != null && characterController.isGrounded)
        {
            // Force-end aerial attacks on landing.
            // OnAttackEnd() is an animation event on the Combo3 clip, but when Yoru lands
            // mid-clip the base layer crossfade can interrupt combat layer playback, causing
            // the event to never fire → isAttacking stuck permanently → movement frozen.
            // 0.15s grace prevents false trigger on the frame right after jump (isGrounded
            // can be stale for 1-2 frames before CharacterController.Move applies velocity).
            // The landing no longer ends the spin: it carries on and finishes on the ground, which
            // is the only way it can ever complete (the clip is about 0.8s, a 2 leg jump gives
            // about 0.45s of air). AerialSpinRoutine owns the ending and always closes out, so this
            // only fires for a spin with no routine running, which would be a stuck flag.
            if (isAerialAttack && isAttacking && !aerialSpinTicking && Time.time - attackStartTime > 0.15f)
            {
                DebugLog("Aerial spin: force-ending on landing (no spin routine running)");
                OnAttackEnd();
            }

            if (hasUsedAerialAttack && !isAttacking)
            {
                hasUsedAerialAttack = false;
                isAerialAttack = false;
            }
            hasUsedAirDodge = false;
            hasUsedAirDash = false;
        }
    }
    #endregion

    #region Input
    private void HandleInput()
    {
        // Menu lockout: while any menu is open (Memory Parchments, Inventory), all
        // combat input is ignored, so clicking UI buttons never swings claws. The
        // matching damage gate lives in PlayerHealth; MenuGuard owns the rule.
        if (MenuGuard.IsAnyMenuOpen) return;

        // Phase 2 lockout: in Tomoe (human) form, all combat input is disabled.
        // Per GDD Doc 04 §4b and Doc 09 §8c. Tomoe is the persuasion form — no attacks,
        // no dodge, no dash, no guard, no tail abilities. Only walking, running, and
        // form transform (T, handled by FormController separately) respond.
        if (formController != null && formController.IsHuman)
        {
            // Diagnostic — log ONLY on actual input frames, not every Update tick.
            // If you press C/MMB/LMB in Granny form and see THIS log, the gate is working
            // and any VFX you see is leftover particles from a prior Yoru action (not a new attack).
            // If you press these keys in Granny form and DON'T see this log, the gate is broken.
            if (Input.GetKeyDown(KeyCode.C)
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(2)
                || Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log("[PlayerCombat] Combat input BLOCKED in Granny form (Phase 2 gate active).");
            }
            return;
        }

        if (isInHitReaction) return;
        if (isDodging || isDashing) return;
        // Captured/stunned (e.g. enemy grab): no attacks, guard, dodge, dash, or tail abilities.
        // The freeze is time-boxed in PlayerHealth, so this self-releases.
        if (playerHealth != null && playerHealth.IsStunned()) return;

        // Tail air shot lockout: while the R slow motion ability is active, or its released
        // shot motion is still finishing after a landing, ALL combat input stands down. LMB
        // must not start the air swirl or the heavy charge during the slow, and nothing may
        // stomp the combat layer while the cast is still firing. TailAimController owns both
        // flags. Diagnostic logs ONLY on actual input frames, same pattern as the Granny gate:
        // if you click during the slow and do NOT see this log, this gate did not make it in.
        // TailAimController4Leg is the same ability on the 4 leg jump and needs the same lockout.
        // It also means the 4 leg shot can safely use the LEFT mouse button if you set its Fire
        // Mouse Button to 0: this gate stops LMB reaching the air swirl while R is held.
        if (TailAimController.IsAiming || TailAimController.IsShotRunning
            || TailAimController4Leg.IsAiming || TailAimController4Leg.IsShotRunning)
        {
            if (Input.GetKeyDown(KeyCode.C)
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(2)
                || Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log("[PlayerCombat] Combat input BLOCKED during tail air shot.");
            }
            return;
        }

        // === GUARD INPUT (Q key) ===
        // Cooldown prevents rapid Q tap from corrupting Animator (same principle as dodge cooldown)
        if (Input.GetKeyDown(KeyCode.Q) && !isGuarding && Time.time - guardEndTime > 0.2f)
        {
            if (characterController != null && characterController.isGrounded)
            {
                StartGuard();
                return;
            }
        }

        // During guard: Q overrides ALL combat actions. Only release Q ends guard.
        // Same as Sekiro — hold block = hold block, nothing interrupts it.
        //
        // v24: Q-release uses grace-period polling instead of GetKeyUp.
        // Reason: keyboard ghosting / N-key rollover on many keyboards drops Q for 1+
        // frames when A or D is pressed simultaneously. GetKeyUp fires on that single
        // dropped frame, ending guard even though Q is still physically held.
        // Polling with grace: only end guard if Q has been continuously released for
        // qReleaseGraceTime (default 0.08s). Brief ghost blips (typically <0.05s) are
        // ignored; intentional releases (>0.08s, well below ~0.1s human tap perception)
        // end guard normally.
        if (isGuarding)
        {
            if (Input.GetKey(KeyCode.Q))
            {
                qReleaseStartTime = -1f; // Q is held, clear any pending release
            }
            else
            {
                if (qReleaseStartTime < 0f)
                {
                    qReleaseStartTime = Time.time; // first frame Q reported released — start grace
                }
                else if (Time.time - qReleaseStartTime >= qReleaseGraceTime)
                {
                    EndGuard();
                    qReleaseStartTime = -1f;
                    return;
                }
            }
            return; // Q held (or pending release within grace) — guard overrides everything
        }

        // Dodge input (C key)
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (TryDodge()) return;
        }

        // Dash input (Middle Mouse)
        if (Input.GetMouseButtonDown(2))
        {
            if (TryDash()) return;
        }

        bool isGrounded = characterController != null && characterController.isGrounded;

        if (Input.GetMouseButtonDown(0))
            attackButtonHoldTime = 0f;

        if (Input.GetMouseButton(0))
        {
            attackButtonHoldTime += Time.deltaTime;
            if (attackButtonHoldTime >= 0.3f && !isChargingHeavy && !isAttacking && isGrounded)
                StartHeavyCharge();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isChargingHeavy)
            {
                ReleaseHeavyAttack();
            }
            else if (attackButtonHoldTime < 0.3f)
            {
                if (isGrounded)
                    TryGroundCombo();
                else
                    TryAerialSpin();
            }
            attackButtonHoldTime = 0f;
        }
    }
    #endregion

    #region Guard/Parry System (Q — Sekiro-style)
    private void StartGuard()
    {
        combatIdleSettledTimer = 0f; // prevent orphan detection from killing this action
        movementStuckTimer = 0f;
        // Can cancel combo 1-2 into guard
        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return;
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            if (vfxManager != null) vfxManager.PlaySpinStop();
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (isChargingHeavy) CancelHeavyCharge();

        UnlockPosition();

        isGuarding = true;
        guardStartTime = Time.time;
        currentGuardAnim = "";
        parryIntroComplete = false;
        guardIdleDebounceTimer = 0f;
        qReleaseStartTime = -1f; // v24: clear any stale grace state from prior guard

        PlayGuardAnim(parryStartState);

        // Lock guard facing to the direction player is currently moving
        // If pressing D → guard faces right. If no input → fall back to transform.forward.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 guardDir;
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
            guardDir = GetInputDirectionCameraRelative(h, v);
        else
            guardDir = cachedTransform.forward;

        if (guardMovement != null)
            guardMovement.EnableGuard(guardDir);

        DebugLog($"Guard START (perfect parry window: {perfectParryWindow}s)");
    }

    private void EndGuard()
    {
        if (!isGuarding) return;

        isGuarding = false;
        guardEndTime = Time.time;
        currentGuardAnim = "";
        qReleaseStartTime = -1f; // v24: clear grace state on exit

        if (guardMovement != null)
            guardMovement.DisableGuard();

        // v27: bypass ReturnToIdle()'s hardcoded 0.1s CrossFade and use the tunable
        // guardExitBlendTime instead. The shared ReturnToIdle() is correct for fast
        // action recoveries (attack/dodge/dash end) but too snappy for guard exit,
        // where Yoru is rising from a deep crouch and needs more frames to read as
        // smooth. Direct CrossFade matches the rest of ReturnToIdle's behavior
        // (single CrossFadeInFixedTime call to combatIdleStateName on combat layer).
        if (animator != null)
        {
            animator.CrossFadeInFixedTime(combatIdleStateName, guardExitBlendTime, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }

        DebugLog("Guard END");
    }

    private void UpdateGuardAnimation()
    {
        // === Phase 1: Start animation ===
        // While Parry_Start is still playing, leave it alone — don't override with idle/walk.
        // Once start clip completes, parryIntroComplete flips true and Phase 2 takes over.
        // Time-based detection: simpler than animator state polling, matches existing parryIntroLength field.
        // Note: hitstop (animator.speed=0) pauses animation playback but Time.time keeps ticking,
        // so a perfect-parry triggered during the start phase may slightly truncate the start clip.
        // That's an acceptable tradeoff — perfect parry is a dramatic moment, the start phase has
        // already played partially, and gameplay flow matters more than animation purity.
        if (!parryIntroComplete)
        {
            if (Time.time - guardStartTime >= parryIntroLength)
            {
                parryIntroComplete = true;
                // Fall through to Phase 2 — pick idle/walk based on current input
            }
            else
            {
                return; // Start clip still playing; do nothing
            }
        }

        // === Phase 2: Idle / walk selection ===
        // Projection onto locked guard direction — same threshold (0.3) as GuardMovementController
        // so animation and movement always agree. Below 0.3 = no movement AND no anim switch.
        float projection = 0f;
        if (guardMovement != null)
            projection = guardMovement.GetGuardInputProjection();

        bool anyDirectionHeld = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f
            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

        string targetAnim;

        if (projection > 0.3f)
        {
            targetAnim = parryWalkForwardState;
            guardIdleDebounceTimer = 0f;
        }
        else if (projection < -0.3f)
        {
            targetAnim = parryWalkBackwardState;
            guardIdleDebounceTimer = 0f;
        }
        else if (anyDirectionHeld)
        {
            // Keys held but perpendicular to guard axis (projection between -0.3 and 0.3).
            // Keep whatever anim is currently playing — don't disrupt.
            // If still on parryStartState (just finished this frame), default to idle.
            targetAnim = currentGuardAnim;
            if (targetAnim == "" || targetAnim == parryStartState) targetAnim = parryIdleState;
            guardIdleDebounceTimer = 0f;
        }
        else
        {
            // No keys held at all. If currently walking, debounce before switching to idle.
            // This prevents 1-2 frame flicker when switching W→S (projection passes through 0).
            bool currentlyWalking = currentGuardAnim == parryWalkForwardState
                || currentGuardAnim == parryWalkBackwardState;

            if (currentlyWalking)
            {
                guardIdleDebounceTimer += Time.deltaTime;
                if (guardIdleDebounceTimer > 0.1f)
                {
                    targetAnim = parryIdleState;
                    guardIdleDebounceTimer = 0f;
                }
                else
                {
                    targetAnim = currentGuardAnim; // hold walk a bit longer
                }
            }
            else
            {
                targetAnim = parryIdleState;
                guardIdleDebounceTimer = 0f;
            }
        }

        if (targetAnim != currentGuardAnim)
        {
            // Blend times tuned per transition type
            float blendTime;
            if (currentGuardAnim == parryStartState)
                blendTime = 0.1f; // start → idle/walk: short blend (start clip ends in parry pose, idle begins in parry pose)
            else if (targetAnim == parryIdleState)
                blendTime = 0.25f; // walk → idle: smooth return
            else if (currentGuardAnim == parryIdleState || currentGuardAnim == "")
                blendTime = 0.2f;  // idle → walk: slightly longer to cover foot transition
            else
                blendTime = 0.15f; // walk ↔ walk (forward ↔ backward): quick

            currentGuardAnim = targetAnim;
            if (animator != null)
            {
                // Plain CrossFade — no normalizedTimeOffset needed since the new parryIdleState
                // is a clean loop (no baked intro to skip past).
                animator.CrossFadeInFixedTime(targetAnim, blendTime, combatLayerIndex);
                lastCombatCrossFadeTime = Time.time;
            }
        }

        // NOTE: The old "Parry idle loop skip" block (~35 lines) has been removed in v23.
        // It was a workaround for the previous design where parryIdleState contained both
        // the intro and the looping idle, and we had to prevent the clip from wrapping back
        // to frame 0 (which would replay the intro). With the new Parry_Start as a separate
        // one-shot clip, parryIdleState is now a clean loop and needs no intervention.
    }

    private void PlayGuardAnim(string stateName)
    {
        currentGuardAnim = stateName;
        if (animator != null)
        {
            animator.CrossFadeInFixedTime(stateName, 0.15f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
    }

    public bool IsInPerfectParryWindow()
    {
        return isGuarding && (Time.time - guardStartTime) <= perfectParryWindow;
    }

    public void OnPerfectParry(Vector3 attackerPos)
    {
        DebugLog("PERFECT PARRY!");

        EnemyCombat closestEnemy = FindClosestAttackingEnemy();
        if (closestEnemy != null)
        {
            closestEnemy.TriggerStagger(parryStaggerDuration);
            DebugLog($"Parry stagger: {closestEnemy.name} for {parryStaggerDuration}s");

            EnemyHealth enemyHealth = closestEnemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(parryCounterDamage, true);
                DebugLog($"Parry counter damage: {parryCounterDamage}");
            }
        }

        // Feedback — pass both animators for hitstop
        if (CombatFeedbackManager.Instance != null)
        {
            Animator enemyAnimator = closestEnemy != null ? closestEnemy.GetComponent<Animator>() : null;
            if (enemyAnimator == null && closestEnemy != null)
                enemyAnimator = closestEnemy.GetComponentInChildren<Animator>();
            CombatFeedbackManager.Instance.PlayParryFeedback(cachedTransform.position, animator, enemyAnimator);
        }
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayParryClang();
    }

    public void OnGuardHit(bool isHeavy)
    {
        DebugLog($"Guard blocked ({guardDamageReduction * 100f:F0}% reduced)");

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.PlayGuardFeedback();
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayGuardBlock();
    }

    private EnemyCombat FindClosestAttackingEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(cachedTransform.position, parryCounterRange, enemyLayer);
        EnemyCombat closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider col in nearby)
        {
            EnemyCombat ec = col.GetComponent<EnemyCombat>();
            if (ec == null) continue;

            EnemyHealth eh = col.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead()) continue;

            float dist = Vector3.Distance(cachedTransform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = ec;
            }
        }

        return closest;
    }
    #endregion

    #region Dodge System (C — evasive frontflip with arc)
    private bool TryDodge()
    {
        if (characterController == null) return false;
        if (Time.time - dodgeEndTime < 0.15f) return false;

        bool isGrounded = characterController.isGrounded;

        if (!isGrounded)
        {
            if (hasUsedAirDodge) return false;
            if (playerMovement == null || playerMovement.GetJumpWindowTimer() <= 0f) return false;
        }

        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return false;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        // Neutral dodge (no directional input) flips along Yoru's current
        // facing instead of snapping to camera-forward, so a side-on idle
        // frontflip travels the way she is already looking.
        Vector3 dodgeDir;
        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
        {
            Vector3 facing = cachedTransform.forward;
            facing.y = 0f;
            dodgeDir = facing.sqrMagnitude > 0.0001f ? facing.normalized : cachedTransform.forward;
        }
        else
        {
            dodgeDir = GetInputDirectionCameraRelative(h, v);
        }

        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        if (!isGrounded) hasUsedAirDodge = true;

        PerformDodge(is4Leg, dodgeDir);
        return true;
    }

    private void PerformDodge(bool is4Leg, Vector3 moveDir)
    {
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        if (isAttacking)
        {
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            if (vfxManager != null) vfxManager.PlaySpinStop();
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (isChargingHeavy) CancelHeavyCharge();

        UnlockPosition();

        isDodging = true;
        dodgeStartTime = Time.time;
        currentDodgeDuration = dodgeFallbackDuration;

        dodgeLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dodgeLockedRotation;

        string animState = is4Leg ? dodge4LegState : dodge2LegState;
        float distance = is4Leg ? dodge4LegDistance : dodge2LegDistance;

        animator.CrossFadeInFixedTime(animState, 0.12f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;

        DebugLog($"Dodge: {animState} ({distance}m, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dodgeCoroutine != null) StopCoroutine(dodgeCoroutine);
        dodgeCoroutine = StartCoroutine(DodgeMovement(moveDir, animState, distance));
    }

    private IEnumerator DodgeMovement(Vector3 direction, string dodgeStateName, float distance)
    {
        // Movement is slaved to the dodge clip's own playback so it can never
        // outrun the animation. Previously the duration was read from
        // GetCurrentAnimatorStateInfo one frame after the entry CrossFade, which
        // returns the OUTGOING state (Combat_Empty / idle), not the dodge clip.
        // Its length latched in and the character kept sliding after the flip
        // had visually finished, which read as the frozen "moving while not
        // animated" beat before locomotion resumed.
        // dodgeFallbackDuration now only covers the brief entry-crossfade window
        // before the dodge state becomes current, plus the safety case where the
        // state name cannot be resolved at all.
        int dodgeHash = Animator.StringToHash(dodgeStateName);
        float duration = dodgeFallbackDuration;
        currentDodgeDuration = duration;

        float elapsed = 0f;
        float previousEased = 0f;
        float previousArc = 0f;
        bool clipResolved = false;

        while (true)
        {
            elapsed += Time.deltaTime;

            float t;
            if (animator != null)
            {
                // During the entry transition the dodge clip is the NEXT state,
                // not the current one, so check both.
                AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                bool haveState = false;
                AnimatorStateInfo dodgeState = cur;

                if (animator.IsInTransition(combatLayerIndex))
                {
                    AnimatorStateInfo nxt = animator.GetNextAnimatorStateInfo(combatLayerIndex);
                    if (nxt.shortNameHash == dodgeHash)
                    {
                        dodgeState = nxt;
                        haveState = true;
                    }
                }
                if (!haveState && cur.shortNameHash == dodgeHash)
                {
                    dodgeState = cur;
                    haveState = true;
                }

                if (haveState && dodgeState.length > 0.1f)
                {
                    duration = dodgeState.length;
                    currentDodgeDuration = duration;
                    clipResolved = true;
                    t = Mathf.Clamp01(dodgeState.normalizedTime);
                }
                else
                {
                    t = Mathf.Clamp01(elapsed / duration);
                }
            }
            else
            {
                t = Mathf.Clamp01(elapsed / duration);
            }

            float eased = t * t * (3f - 2f * t);
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);

                if (dodgeHeight > 0f)
                {
                    // Use eased t (not raw t) for zero-velocity arc endpoints:
                    // sin(smoothstep(t) * PI) has derivative=0 at t=0 and t=1,
                    // eliminating sudden Y jolts that cause camera overshoot.
                    float arc = Mathf.Sin(eased * Mathf.PI) * dodgeHeight;
                    float arcDelta = arc - previousArc;
                    previousArc = arc;
                    move.y += arcDelta;
                }
                else if (!characterController.isGrounded)
                {
                    move.y = Physics.gravity.y * Time.deltaTime;
                }

                characterController.Move(move);
            }

            if (t >= dodgeEarlyExitThreshold)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
                {
                    DebugLog($"Dodge early exit at {t * 100f:F0}% (movement input)");
                    break;
                }
            }

            // End the instant the clip completes so EndDodge blends to
            // locomotion with no frozen tail. Before the clip resolves, fall
            // back to the timer so a missing or renamed state can never hang
            // the coroutine.
            if (clipResolved)
            {
                if (t >= 0.999f) break;
            }
            else if (elapsed >= duration)
            {
                break;
            }

            yield return null;
        }

        EndDodge();
    }

    private void EndDodge()
    {
        isDodging = false;
        if (dodgeCoroutine != null)
        {
            StopCoroutine(dodgeCoroutine);
            dodgeCoroutine = null;
        }
        dodgeEndTime = Time.time;
        if (animator != null)
        {
            // 0.25s blend for smoother frontflip→sprint/idle transition
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.25f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
        DebugLog("Dodge ended");
    }

    public bool IsInDodgeIFrames()
    {
        if (!isDodging || animator == null) return false;
        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= iFrameStart && normalizedTime <= iFrameEnd;
    }
    #endregion

    #region Dash System (MMB — aggressive flat rush with damage)
    private bool TryDash()
    {
        if (characterController == null) return false;
        if (isDodging || isDashing) return false;

        bool isGrounded = characterController.isGrounded;

        if (!isGrounded)
        {
            if (hasUsedAirDash) return false;
            if (playerMovement == null || playerMovement.GetJumpWindowTimer() <= 0f) return false;
        }

        if (isAttacking)
        {
            if (currentComboStep != 1 && currentComboStep != 2)
                return false;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dashDir = GetInputDirectionCameraRelative(h, v);

        bool is4Leg = Input.GetKey(KeyCode.LeftShift) ||
                      (playerMovement != null && playerMovement.IsRunning());

        if (!isGrounded) hasUsedAirDash = true;

        PerformDash(is4Leg, dashDir);
        return true;
    }

    private void PerformDash(bool is4Leg, Vector3 moveDir)
    {
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        if (isAttacking)
        {
            isAttacking = false;
            canQueueNextAttack = false;
            queuedClicks = 0;
            currentComboStep = 0;
            if (vfxManager != null) vfxManager.PlaySpinStop();
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }

        if (isChargingHeavy) CancelHeavyCharge();

        UnlockPosition();

        isDashing = true;
        dashStartTime = Time.time;
        currentDashDuration = dashFallbackDuration;

        dashLockedRotation = Quaternion.LookRotation(moveDir);
        cachedTransform.rotation = dashLockedRotation;

        string animState = is4Leg ? dash4LegState : dash2LegState;
        float distance = is4Leg ? dash4LegDistance : dash2LegDistance;

        animator.CrossFadeInFixedTime(animState, 0.03f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;

        DebugLog($"Dash: {animState} ({distance}m, {dashDamage} dmg, {(is4Leg ? "4leg" : "2leg")})");

        if (vfxManager != null) vfxManager.PlayDodgeDashTrailVFX();
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayDodge();

        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
        dashCoroutine = StartCoroutine(DashMovement(moveDir, distance));
    }

    private IEnumerator DashMovement(Vector3 direction, float distance)
    {
        float duration = dashFallbackDuration;
        bool needsClipUpdate = true;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (stateInfo.length > 0.1f)
            {
                duration = stateInfo.length;
                needsClipUpdate = false;
            }
        }
        currentDashDuration = duration;

        float elapsed = 0f;
        float previousEased = 0f;
        var hitEnemyIDs = new System.Collections.Generic.HashSet<int>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (needsClipUpdate && animator != null)
            {
                AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (si.length > 0.1f)
                {
                    duration = si.length;
                    currentDashDuration = duration;
                }
                needsClipUpdate = false;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float frameDelta = eased - previousEased;
            previousEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 move = direction * (distance * frameDelta);
                if (!characterController.isGrounded)
                    move.y = Physics.gravity.y * Time.deltaTime;

                characterController.Move(move);
            }

            DealDashDamage(hitEnemyIDs);

            yield return null;
        }

        EndDash();
    }

    private void DealDashDamage(System.Collections.Generic.HashSet<int> hitEnemyIDs)
    {
        Collider[] enemies = Physics.OverlapSphere(attackPoint.position, dashHitRange, enemyLayer);

        foreach (Collider enemy in enemies)
        {
            int id = enemy.gameObject.GetInstanceID();
            if (hitEnemyIDs.Contains(id)) continue;

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                hitEnemyIDs.Add(id);
                enemyHealth.TakeDamage(dashDamage, false);
                DebugLog($"Dash hit {enemy.name} for {dashDamage}");

                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);
                if (CombatFeedbackManager.Instance != null)
                {
                    Animator enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator == null)
                        enemyAnimator = enemy.GetComponentInChildren<Animator>();
                    CombatFeedbackManager.Instance.PlayHitFeedback(contactPoint, false, animator, enemyAnimator);
                }
                if (CombatSFXManager.Instance != null)
                    CombatSFXManager.Instance.PlayImpact(false);
            }
        }
    }

    private void EndDash()
    {
        isDashing = false;
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        dodgeEndTime = Time.time;
        if (animator != null)
        {
            // 0.2s blend for smoother dash→sprint/idle transition
            animator.CrossFadeInFixedTime(combatIdleStateName, 0.2f, combatLayerIndex);
            lastCombatCrossFadeTime = Time.time;
        }
        DebugLog("Dash ended");
    }

    public bool IsInDashIFrames()
    {
        if (!isDashing || animator == null) return false;
        float normalizedTime = animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
        return normalizedTime >= dashIFrameStart && normalizedTime <= dashIFrameEnd;
    }
    #endregion

    #region Shared — Camera Direction
    private Vector3 GetInputDirectionCameraRelative(float h, float v)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        if (mainCamera != null)
        {
            camForward = mainCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            camRight = mainCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
            return camForward;

        Vector3 dir = camForward * v + camRight * h;
        return dir.normalized;
    }
    #endregion

    #region Hit Reaction
    public void PlayHitReaction(bool isHeavy)
    {
        PlayHitReaction(isHeavy, Vector3.zero);
    }

    /// <summary>
    /// Cancels any combat action in progress (attack, heavy charge, guard, dodge, dash) and clears
    /// its flags, coroutines and animator parameters. Shared by the hit reaction and the grab
    /// reaction so both begin from a clean combat state.
    /// </summary>
    private void EndActiveCombatActions()
    {
        isAttacking = false;
        isChargingHeavy = false;
        chargeHoldStarted = false;
        chargeReadyAnnounced = false;
        canQueueNextAttack = false;
        queuedClicks = 0;
        currentComboStep = 0;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        storedHeavyChargePercent = 0f;
        UnlockPosition();
        if (vfxManager != null)
        {
            vfxManager.PlaySpinStop();
            vfxManager.StopHeavyChargeBuildupVFX();
        }
        if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.StopHeavyChargeLoop();

        if (isGuarding) EndGuard();

        if (isDodging)
        {
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
        }

        if (isDashing)
        {
            isDashing = false;
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
                dashCoroutine = null;
            }
        }

        if (isBeyblading)
        {
            isBeyblading = false;
            if (beybladeCoroutine != null)
            {
                StopCoroutine(beybladeCoroutine);
                beybladeCoroutine = null;
            }
        }
        if (lungeCoroutine != null)
        {
            StopCoroutine(lungeCoroutine);
            lungeCoroutine = null;
        }
        currentLungeTarget = null;

        if (animator != null)
        {
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }
    }

    public void PlayHitReaction(bool isHeavy, Vector3 attackerPos, bool feedbackOnly = false)
    {
        combatIdleSettledTimer = 0f;
        if (!gameObject.activeInHierarchy) return;

        if (feedbackOnly)
        {
            // Grab path: HP is applied by the caller; here we fire ONLY the impact feedback
            // (hit VFX, screen feedback, hit sound). The normal hit reaction animation and the
            // pull are skipped on purpose, because the held grab reaction is the visible reaction.
            if (vfxManager != null) vfxManager.PlayHitReactVFX(isHeavy);
            if (CombatFeedbackManager.Instance != null) CombatFeedbackManager.Instance.PlayPlayerHitFeedback(isHeavy);
            if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayPlayerHit(isHeavy);
            FlashDamage();
            return;
        }

        bool is4Leg = playerMovement != null && playerMovement.IsRunning();

        // Fires first, before anything that could be interrupted or blended away. The tint is the
        // one piece of damage feedback that is guaranteed to be visible no matter which reaction
        // clip is chosen or whether the combat layer is being fought over.
        FlashDamage();

        EndActiveCombatActions();

        if (attackerPos != Vector3.zero && characterController != null)
        {
            Vector3 pullDir = attackerPos - cachedTransform.position;
            pullDir.y = 0f;
            if (pullDir.sqrMagnitude > 0.01f)
            {
                cachedTransform.rotation = Quaternion.LookRotation(pullDir.normalized);
                if (pullCoroutine != null) StopCoroutine(pullCoroutine);
                pullCoroutine = StartCoroutine(SmoothPull(pullDir.normalized, pullDistance, pullDuration));
            }
        }

        string animState;
        float duration;

        if (isHeavy)
        {
            animState = is4Leg ? hitReactHeavy4Leg : hitReactHeavy2Leg;
            duration = heavyHitReactDuration;

            // Clip-driven 4-leg heavy reaction: when a cut frame is configured, the reaction must
            // stay alive long enough for Bhit_run_reaction_4 to actually reach that frame (almost
            // 2s in), so the 0.5s timer is replaced by the max-hold ceiling. HoldHitReaction ends
            // the reaction the moment the clip crosses the cut frame, so the ceiling only matters
            // if the clip never gets there.
            if (animState == hitReactHeavy4Leg && heavy4LegHitCutFrame > 0 && heavy4LegHitTotalFrames > 0)
                duration = heavy4LegHitMaxHold;
        }
        else
        {
            animState = is4Leg ? hitReactLight4Leg : hitReactLight2Leg;
            duration = lightHitReactDuration;

            // Mushroom strike readability: a 0.3s light reaction is lost under the hallucination
            // screen distortion (and the 4-leg light clip reads like normal running at that length).
            // Hold the light reaction longer so the hit visibly registers.
            if (HallucinationEffect.IsActive)
                duration = Mathf.Max(duration, hallucinationHitReactDuration);
        }

        if (vfxManager != null) vfxManager.PlayHitReactVFX(isHeavy);

        if (animator != null)
        {
            // Drive the reaction the robust way the grab does: force the combat layer to full weight
            // and actively hold the reaction state for its duration so a single crossfade cannot be
            // silently lost. The hold yields once UpdateHitReaction clears the flag and runs ReturnToIdle.
            if (hitReactHoldCoroutine != null) StopCoroutine(hitReactHoldCoroutine);
            hitReactHoldCoroutine = StartCoroutine(HoldHitReaction(animState, duration));
            lastCombatCrossFadeTime = Time.time;
            DebugLog($"Hit react: {animState} ({duration}s)");
        }

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.PlayPlayerHitFeedback(isHeavy);
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayPlayerHit(isHeavy);

        isInHitReaction = true;
        hitReactionEndTime = Time.time + duration;

        // Backup force-clear coroutine — independent of UpdateHitReaction's Time.time check.
        // Mirrors the aerial-spin "force-ending on landing" pattern: when the primary clear
        // mechanism is unreliable (animation events for aerial; observed stuck-flag for hit
        // react in May 2026 logs), a second independent timer guarantees the flag clears.
        // 0.1s buffer past the expected duration lets UpdateHitReaction win in the normal case.
        if (hitReactSafetyCoroutine != null) StopCoroutine(hitReactSafetyCoroutine);
        hitReactSafetyCoroutine = StartCoroutine(HitReactSafetyTimer(duration + 0.1f));
    }

    #region Grab Reaction
    /// <summary>
    /// Plays the stance-appropriate grab reaction (2-leg or 4-leg) when an enemy grab catches Yoru,
    /// then freezes it on its configured frame and holds there until ResumeGrabReaction is called.
    /// The camera shake, camera roll, hit sound and hit VFX are untouched and still fire from their
    /// own paths; this method only drives the reaction body animation.
    /// </summary>
    public void PlayGrabReaction()
    {
        if (!gameObject.activeInHierarchy || animator == null) return;

        bool is4Leg = playerMovement != null && playerMovement.IsRunning();
        string state = is4Leg ? grabReact4LegState : grabReact2LegState;
        int freezeFrame = is4Leg ? grabReact4LegFreezeFrame : grabReact2LegFreezeFrame;
        int totalFrames = is4Leg ? grabReact4LegTotalFrames : grabReact2LegTotalFrames;
        float freezeNorm = totalFrames > 0 ? Mathf.Clamp01((float)freezeFrame / totalFrames) : 0.4f;

        // Start from a clean combat state, then make sure no leftover hit reaction flag lingers.
        EndActiveCombatActions();
        isInHitReaction = false;

        if (grabReactionCoroutine != null) StopCoroutine(grabReactionCoroutine);
        grabReactionCoroutine = StartCoroutine(GrabReactionRoutine(state, freezeNorm));
    }

    /// <summary>
    /// Releases the held grab reaction so it plays from the freeze frame through to the end of the
    /// clip and then blends back to the normal combat pose. Called when the grab's strike is over.
    /// </summary>
    public void ResumeGrabReaction()
    {
        grabReleaseRequested = true;
    }

    /// <summary>
    /// Hard cleanup if the grab is cut short (enemy staggered or killed mid grab). Stops the
    /// reaction, blends back to the normal combat pose and clears the flags.
    /// </summary>
    public void CancelGrabReaction()
    {
        if (grabReactionCoroutine != null)
        {
            StopCoroutine(grabReactionCoroutine);
            grabReactionCoroutine = null;
        }
        if (animator != null && isInGrabReaction)
            animator.CrossFadeInFixedTime(combatIdleStateName, grabReactBlendOut, combatLayerIndex);
        isInGrabReaction = false;
        grabReleaseRequested = false;
    }

    private IEnumerator GrabReactionRoutine(string state, float freezeNorm)
    {
        isInGrabReaction = true;
        grabReleaseRequested = false;

        int stateHash = Animator.StringToHash(state);
        animator.SetLayerWeight(combatLayerIndex, 1f);
        animator.CrossFadeInFixedTime(state, grabReactBlendIn, combatLayerIndex, 0f);
        yield return null;

        // Phase 1: play in until we reach the freeze frame.
        while (true)
        {
            AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            bool settled = !animator.IsInTransition(combatLayerIndex) && si.shortNameHash == stateHash;
            if (settled && si.normalizedTime >= freezeNorm) break;
            if (!isInGrabReaction) yield break;
            yield return null;
        }

        // Phase 2: hold on the freeze frame by re-pinning the state each frame. Animator speed is
        // left untouched so this never collides with hitstop or the movement-stuck safety net. The
        // hold ends when the strike signals release, or after the safety timeout as a last resort.
        float held = 0f;
        while (!grabReleaseRequested && held < grabReactMaxHold)
        {
            animator.Play(stateHash, combatLayerIndex, freezeNorm);
            held += Time.deltaTime;
            if (!isInGrabReaction) yield break;
            yield return null;
        }

        // Phase 3: released. Play from the freeze frame onward, but stop at the configured cut
        // frame for the 4-leg heavy clip exactly like HoldHitReaction does: the blend to idle
        // starts early enough that, by the time it completes, the clip has only just reached the
        // cut frame, so the locomotion tail past it is never shown. Yoru also stays frozen (the
        // stun is refreshed every frame, the same pattern the grab swoop uses) until the reaction
        // is visually over, so he can never run around while the body is still mid-reaction.
        const float cutBlendOut = 0.12f;
        float cutNorm = 1f;
        // Both the heavy hit clip and the 4-leg grab clip carry a run/locomotion tail past the
        // reaction beat. Cut the release at the configured frame so Yoru blends back to idle instead
        // of playing that tail in place (which reads as running without moving).
        bool hasHeavyCut = state == hitReactHeavy4Leg && heavy4LegHitTotalFrames > 0 && heavy4LegHitCutFrame > 0;
        bool hasGrabCut = state == grabReact4LegState && grabReact4LegTotalFrames > 0 && grabReact4LegCutFrame > 0;
        bool hasCut = hasHeavyCut || hasGrabCut;
        if (hasHeavyCut)
            cutNorm = Mathf.Clamp01((float)heavy4LegHitCutFrame / heavy4LegHitTotalFrames);
        else if (hasGrabCut)
            cutNorm = Mathf.Clamp01((float)grabReact4LegCutFrame / grabReact4LegTotalFrames);

        while (true)
        {
            if (playerHealth != null) playerHealth.ApplyStun(0.2f); // hold the freeze through the release

            AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            if (si.shortNameHash == stateHash)
            {
                float triggerNorm = hasCut ? cutNorm : 0.98f;
                if (hasCut && si.length > 0f)
                    triggerNorm = Mathf.Max(0.05f, cutNorm - (cutBlendOut / si.length));

                if (si.normalizedTime >= triggerNorm) break;
            }
            if (!isInGrabReaction) yield break;
            yield return null;
        }

        // Blend back to the normal combat pose. The freeze stays on just long enough to cover the
        // blend, so control returns the moment Yoru is back on his feet and not a frame before.
        float blendOut = hasCut ? cutBlendOut : grabReactBlendOut;
        if (playerHealth != null) playerHealth.ApplyStun(blendOut);
        animator.CrossFadeInFixedTime(combatIdleStateName, blendOut, combatLayerIndex);
        isInGrabReaction = false;
        grabReleaseRequested = false;
        grabReactionCoroutine = null;
    }
    #endregion

    private IEnumerator SmoothPull(Vector3 direction, float distance, float duration)
    {
        float elapsed = 0f;
        float moved = 0f;
        while (elapsed < duration && moved < distance)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float speedMultiplier = Mathf.Lerp(2f, 0.2f, t);
            float step = (distance / duration) * speedMultiplier * Time.deltaTime;
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(direction * step);
                moved += step;
            }
            yield return null;
        }
        pullCoroutine = null;
    }

    // ─────────────────────────────────────────────────────── Zelda-style damage flash ──

    private Coroutine damageFlashRoutine;
    private Renderer[] flashRenderers;
    private Material[] flashMaterials;
    private Color[] flashOriginalColors;
    private bool flashCached;

    /// <summary>
    /// Tints Yoru toward damageFlashColor for a moment, blinking a couple of times, then restores
    /// the exact colours captured on the very first hit.
    ///
    /// Renderers and materials are cached once: calling .material every hit instantiates a fresh
    /// material copy each time, which on a fur-shaded ten-tailed cat is not free. Restoring from a
    /// snapshot taken on the FIRST hit (rather than on every hit) is what stops two fast hits from
    /// leaving her permanently red — the same stacking bug the hitstop system already guards against.
    /// </summary>
    private void FlashDamage()
    {
        if (!damageFlashEnabled) return;

        if (!flashCached)
        {
            flashCached = true;
            flashRenderers = GetComponentsInChildren<Renderer>(true);
            flashMaterials = new Material[flashRenderers.Length];
            flashOriginalColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                flashMaterials[i] = flashRenderers[i].material;
                flashOriginalColors[i] = flashMaterials[i].color;
            }
        }

        if (flashMaterials == null || flashMaterials.Length == 0) return;

        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
            RestoreFlashColors();
        }
        damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private void RestoreFlashColors()
    {
        if (flashMaterials == null) return;
        for (int i = 0; i < flashMaterials.Length; i++)
            if (flashMaterials[i] != null)
                flashMaterials[i].color = flashOriginalColors[i];
    }

    private IEnumerator DamageFlashRoutine()
    {
        int blinks = Mathf.Max(1, damageFlashBlinks);
        float half = Mathf.Max(0.02f, damageFlashDuration) / (blinks * 2f);

        for (int b = 0; b < blinks; b++)
        {
            for (int i = 0; i < flashMaterials.Length; i++)
                if (flashMaterials[i] != null)
                    flashMaterials[i].color = damageFlashColor;

            // Real time throughout — the flash must read at normal speed even while Yoru's own
            // tail-aim slow-motion has the world clock at a tenth speed.
            yield return new WaitForSecondsRealtime(half);

            RestoreFlashColors();

            if (b < blinks - 1)
                yield return new WaitForSecondsRealtime(half);
        }

        damageFlashRoutine = null;
    }

    private void UpdateHitReaction()
    {
        if (isInHitReaction && Time.time >= hitReactionEndTime)
        {
            isInHitReaction = false;
            ReturnToIdle();
        }
    }

    // Backup safety: WaitForSeconds-based clear that runs independently of UpdateHitReaction.
    // If UpdateHitReaction's Time.time >= hitReactionEndTime check fires first (normal case),
    // this coroutine finds isInHitReaction already false and no-ops. If UpdateHitReaction
    // fails to clear for any reason (the May 2026 stuck-flag bug), this catches it.
    // ForceResetCombat stops this coroutine to avoid double-fire after a manual reset.
    private IEnumerator HitReactSafetyTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isInHitReaction)
        {
            DebugLog($"Hit react: safety coroutine force-clearing isInHitReaction (UpdateHitReaction timer failed)");
            isInHitReaction = false;
            ReturnToIdle();
        }
        hitReactSafetyCoroutine = null;
    }

    // Mirrors the grab reaction's proven approach for the normal hit reaction: set the combat layer
    // to full weight, crossfade into the reaction, then keep the state asserted each frame so nothing
    // can overwrite the one-shot crossfade before it is seen. Lets the clip play through (no freeze).
    // Exits the moment the duration elapses or UpdateHitReaction clears the flag and runs ReturnToIdle,
    // so it never fights the blend back to idle.
    private IEnumerator HoldHitReaction(string state, float duration)
    {
        int hash = Animator.StringToHash(state);
        animator.SetLayerWeight(combatLayerIndex, 1f);
        animator.CrossFadeInFixedTime(state, 0.02f, combatLayerIndex, 0f);
        yield return null;

        // Optional early cut: the 4-leg heavy clip (Bhit_run_reaction_4) runs into a locomotion cycle
        // at its tail, which reads as Yoru jogging in place. The cut frame is the LAST VISIBLE frame:
        // the blend to idle starts early enough that, by the time it completes, the clip has only just
        // reached the cut frame, so nothing past it is ever shown. cutNorm stays at 1 (no cut) for
        // every other reaction.
        const float cutBlendOut = 0.12f;
        float cutNorm = 1f;
        bool hasCut = state == hitReactHeavy4Leg && heavy4LegHitTotalFrames > 0 && heavy4LegHitCutFrame > 0;
        if (hasCut)
            cutNorm = Mathf.Clamp01((float)heavy4LegHitCutFrame / heavy4LegHitTotalFrames);

        float elapsed = 0f;
        bool settledOnce = false;
        bool missingStateWarned = false;
        while (elapsed < duration && isInHitReaction)
        {
            AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            bool settled = si.shortNameHash == hash && !animator.IsInTransition(combatLayerIndex);
            if (settled) settledOnce = true;
            if (!settled)
            {
                // Lost the reaction state (something overwrote the one-shot crossfade): re-assert it.
                if (!animator.IsInTransition(combatLayerIndex))
                    animator.CrossFadeInFixedTime(state, 0.02f, combatLayerIndex, 0f);

                // If the state has never been reached after repeated re-asserts, the state name
                // almost certainly does not exist on the combat layer (CrossFade to a missing
                // state fails silently in Unity, which looks like "no hit reaction at all").
                // Shout once so the broken Inspector field is identifiable from the console.
                if (!settledOnce && !missingStateWarned && elapsed > 0.4f)
                {
                    Debug.LogWarning($"[Combat] Hit react state '{state}' never settled on combat layer {combatLayerIndex}. The reaction is firing but nothing is visible. Check the Hit Reaction state-name fields on PlayerCombat in the Inspector (expected: {hitReactLight2Leg} / {hitReactLight4Leg} / {hitReactHeavy2Leg} / {hitReactHeavy4Leg}).");
                    missingStateWarned = true;
                }
            }
            else
            {
                // Start the blend early so the clip lands ON the cut frame as the blend completes;
                // frames past the cut frame are never visible. Non-cut reactions (triggerNorm 1)
                // simply blend out when their clip finishes.
                float triggerNorm = cutNorm;
                if (hasCut && si.length > 0f)
                    triggerNorm = Mathf.Max(0.05f, cutNorm - (cutBlendOut / si.length));

                if (si.normalizedTime >= triggerNorm)
                {
                    animator.CrossFadeInFixedTime(combatIdleStateName, cutBlendOut, combatLayerIndex);
                    isInHitReaction = false;
                    break;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        hitReactHoldCoroutine = null;
    }
    #endregion

    #region Combat Targeting + Magnet Lunge
    /// <summary>
    /// Find the best enemy to home onto: the nearest live enemy inside targetingRange, inside the
    /// targetingAngle cone (set targetingAngle to 180 for full all-around), with a clear line of
    /// sight. Returns null when there is no valid enemy (the attack then lunges straight forward).
    /// </summary>
    // Diagnostic only (written when Log Combo Trace is on): why the last acquisition did or did not find a target.
    private string lastTargetTrace = "";

    private Transform AcquireTarget()
    {
        Collider[] nearby = Physics.OverlapSphere(cachedTransform.position, targetingRange, enemyLayer);
        if (nearby.Length == 0)
        {
            if (logComboTrace) lastTargetTrace = $"none: no collider on enemyLayer within {targetingRange}m";
            return null;
        }

        Transform best = null;
        Collider bestCol = null;
        float bestScore = float.MaxValue;
        int rejDead = 0, rejAngle = 0, rejLos = 0;

        foreach (Collider col in nearby)
        {
            EnemyHealth eh = col.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead()) { rejDead++; continue; }

            Vector3 toEnemy = col.transform.position - cachedTransform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist < 0.05f) continue;

            float angle = Vector3.Angle(cachedTransform.forward, toEnemy);
            if (angle > targetingAngle) { rejAngle++; continue; }

            if (!HasLineOfSight(col)) { rejLos++; continue; }

            float score = dist + angle * 0.02f;
            if (score < bestScore)
            {
                bestScore = score;
                best = col.transform;
                bestCol = col;
            }
        }
        currentLungeCollider = bestCol;

        if (logComboTrace)
        {
            lastTargetTrace = best != null
                ? $"'{best.name}' at {bestScore:F1}m"
                : $"none: {nearby.Length} collider(s) in range, rejected dead={rejDead} angle={rejAngle} lineOfSight={rejLos} (LOS is a Linecast to the enemy ROOT + 0.6m against environmentMask — uneven ground between Yoru and a big enemy's feet blocks it)";
        }
        return best;
    }

    /// <summary>
    /// True when nothing in environmentMask blocks the straight line from Yoru to the target. With
    /// Launch on, the line is aimed at the collider's body centre (a 6 m boss has his root at his
    /// feet — aiming there let every bump of a cave floor "hide" him); off, the old root + 0.6 m.
    /// </summary>
    private bool HasLineOfSight(Collider col)
    {
        Transform target = col.transform;
        Vector3 eye = cachedTransform.position + Vector3.up * 0.6f;
        Vector3 targetEye = launchEnabled ? col.bounds.center : target.position + Vector3.up * 0.6f;
        if (Physics.Linecast(eye, targetEye, out RaycastHit hit, environmentMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitRoot = hit.collider.transform.root;
            if (hitRoot != target.root && hitRoot != cachedTransform.root)
                return false; // a wall or obstacle is in the way
        }
        return true;
    }

    /// <summary>Closest point on the enemy's collider surface to Yoru (bounds fallback for concave meshes).</summary>
    private static Vector3 SurfacePoint(Collider col, Vector3 from)
    {
        if (col is MeshCollider mc && !mc.convex) return col.bounds.ClosestPoint(from);
        return col.ClosestPoint(from);
    }

    /// <summary>Turn toward the target on the horizontal plane. instant=true snaps (used at attack start); false fast-slerps (used while tracking).</summary>
    private void FaceTargetFast(Transform target, bool instant)
    {
        if (target == null) return;
        Vector3 look = target.position - cachedTransform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(look);
        cachedTransform.rotation = instant
            ? want
            : Quaternion.Slerp(cachedTransform.rotation, want, lungeFaceSpeed * Time.deltaTime);
    }

    /// <summary>Per-frame fast face toward the current target while an attack is active. Drops a dead target.</summary>
    private void TrackTarget()
    {
        if (currentLungeTarget != null)
        {
            EnemyHealth eh = currentLungeTarget.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead()) { currentLungeTarget = null; return; }
        }
        FaceTargetFast(currentLungeTarget, false);
    }

    /// <summary>
    /// Slide toward the target (or straight forward when target is null), capped at lungeMaxDistance,
    /// stopping lungeStopGap short so the enemy ends inside attack range. Sets lungeEndedShort when the
    /// cap stops Yoru before he fully closes, which drives the weaker "reach" hit.
    /// </summary>
    /// <summary>
    /// Opt-in (round 8). Switches on the agreed three-case launch model and sets its numbers:
    ///   A. no enemy              -> step forward `nudgeDistance` on EVERY attack and combo step
    ///   B. enemy within `engageDistance` of its collider surface -> launch to it (unchanged)
    ///   C. enemy beyond that     -> step forward only, never a clamped part-way slide
    /// Pass a negative value for any number to leave that Inspector value untouched.
    /// OFF by default so every other fight keeps the old planted behaviour.
    /// </summary>
    public void ConfigureLaunch(bool noTargetLaunch, float nudgeDistance = -1f, float engageDistance = -1f,
                               float coneAngleDegrees = -1f, float minDistance = -1f, float stopGap = -1f,
                               float speed = -1f, int airLaunch = -1, int edgeGroundLayers = 0)
    {
        launchWithNoTarget = noTargetLaunch;
        if (airLaunch        >= 0) launchInAir            = airLaunch != 0;   // -1 = leave alone
        if (edgeGroundLayers != 0) edgeGroundMask         = edgeGroundLayers; //  0 = leave alone
        if (nudgeDistance    >= 0f) launchNoTargetDistance = nudgeDistance;
        if (engageDistance   >= 0f) launchEngageDistance   = engageDistance;
        if (coneAngleDegrees >  0f) targetingAngle         = Mathf.Clamp(coneAngleDegrees, 1f, 180f);
        if (minDistance      >= 0f) launchMinDistance      = minDistance;   // 0 = no minimum
        if (stopGap          >= 0f) launchStopGap          = stopGap;       // 0 = all the way in
        if (speed            >  0f) launchSpeed            = speed;
    }

    private void StartLunge(Transform target, bool allowNoTargetNudge)
    {
        // Airborne attacks never slide. The magnet is a grounded tool: running the slide in the
        // air fights PlayerMovement's fall, and its keep-down step applied gravity as a per frame
        // position jump, which reads as a sudden height drop. Heavy release is the path that could
        // reach here airborne (charge on the ground, jump, release in the air). The strike still
        // plays, only the slide is skipped. With Launch on, "grounded a moment ago" counts too:
        // isGrounded flickers on uneven floors and was silently killing the slide.
        // ROUND 9: with Launch In Air on (Hazel's call), that early-out is lifted. The height drop
        // it was protecting against came from LungeRoutine adding its own gravity on top of
        // PlayerMovement's; that line is gone, so the airborne slide is now purely horizontal.
        if (characterController == null) return;
        bool grounded = characterController.isGrounded
                        || (launchEnabled && Time.time - lastGroundedTime <= launchGroundedGrace);
        if (!grounded && !(launchEnabled && launchInAir)) return;

        Vector3 dir;
        float distance = 0f;
        bool nudgeOnly = false;                              // round 8: cases A and C both step forward
        float targetGap = -1f;                               // surface distance, for the trace line
        lungeEndedShort = false;
        float maxDist = launchEnabled ? launchMaxDistance : lungeMaxDistance;

        if (target != null)
        {
            // Launch: measure to the collider SURFACE, so a 1.4 m-radius boss is reached like a
            // small enemy instead of Yoru trying to stop 1 m from his centre (inside his body).
            Vector3 aim = (launchEnabled && currentLungeCollider != null)
                ? SurfacePoint(currentLungeCollider, cachedTransform.position)
                : target.position;
            Vector3 toTarget = aim - cachedTransform.position;
            toTarget.y = 0f;
            float gap = toTarget.magnitude;
            targetGap = gap;
            dir = gap > 0.01f ? toTarget.normalized : cachedTransform.forward;

            // ROUND 8, CASE C — the enemy is real but too far to be worth sliding at, so step
            // forward instead of committing. Every shipped implementation we compared against does
            // exactly this rather than clamping: HL2's npc_assassin returns TOO_FAR past 1.5x the
            // animation's own travel, NOLF2's lunge goal does not fire outside 300-500 units, and
            // the UE5 melee prototypes clear the warp target and play a non-warping attack. The old
            // Mathf.Clamp is what produced "she slides the full 6 m and still lands a metre short".
            if (launchEnabled && launchWithNoTarget && gap > launchEngageDistance)
            {
                nudgeOnly = true;
            }
            else
            {
                // ROUND 8, CASE B — launch AT him. gap is to his collider SURFACE, so with Launch
                // Stop Gap at 0 she travels the whole way and her own capsule stops on his body.
                // That is what "launch to the enemy" means. No floor, no minimum: if the real gap
                // is 0.3 m she moves 0.3 m, if it is 2 m she moves 2 m.
                float want = gap - (launchEnabled ? launchStopGap : lungeStopGap);
                distance = Mathf.Clamp(want, 0f, maxDist);
                if (want > maxDist) lungeEndedShort = true;  // capped short, reach hit is weaker
            }
        }
        else
        {
            // ROUND 8, CASE A — nothing to aim at. With the launch model on, EVERY attack and every
            // combo step still steps forward. With it off, the old behaviour: hits 1 and 2 planted,
            // only the finisher nudged.
            dir = cachedTransform.forward;
            if (launchEnabled && launchWithNoTarget) nudgeOnly = true;
            else distance = allowNoTargetNudge ? noTargetFinisherNudge : 0f;
        }

        if (nudgeOnly)
        {
            // A step is always straight forward — she is not sliding at anything. The edge probe in
            // LungeRoutine still runs, so this can never carry her off a ledge.
            dir = cachedTransform.forward;
            distance = Mathf.Max(0f, launchNoTargetDistance);
            lungeEndedShort = false;
        }

        // ROUND 8: there is NO minimum launch distance. The old 0.8 m floor overrode the real
        // gap on 21 of 30 attacks and was then absorbed by the boss's collider, which is exactly
        // why the launch was invisible. Launch Min Distance is 0 unless someone deliberately
        // raises it; at 0 this block does nothing.
        if (launchEnabled && launchMinDistance > 0f && target != null && !nudgeOnly && distance < launchMinDistance)
            distance = launchMinDistance;

        // Launch timing: speed-based, floored so the slide is long enough to SEE, capped so it is
        // always over before the strike frame.
        float duration = lungeDuration;
        if (launchEnabled && distance > 0.01f)
        {
            float speed = Mathf.Max(1f, launchSpeed);
            duration = Mathf.Clamp(distance / speed, Mathf.Max(0.01f, launchMinDuration), Mathf.Max(0.06f, launchMaxDuration));
            // Epsilon: at exactly the ceiling (3.20m at 10m/s in 0.32s) float noise flagged a
            // launch that reaches perfectly as "capped short", which also drives the weaker reach hit.
            if (distance / speed > launchMaxDuration + 0.0005f)
            {
                distance = speed * launchMaxDuration;         // she cannot cover more in the time she has
                lungeEndedShort = true;
            }
        }

        if (logComboTrace)
        {
            string what;
            if (nudgeOnly && target != null)
                what = $"STEP (too far: {target.name} at {targetGap:F1}m > engage {launchEngageDistance:F1}m)";
            else if (nudgeOnly)
                what = "STEP (no enemy)";
            else if (target != null)
                what = $"{target.name} at {targetGap:F1}m";
            else
                what = "no target, planted";
            string air = (characterController != null && !characterController.isGrounded) ? " [AIRBORNE]" : "";
            Debug.Log($"[ComboTrace] LAUNCH {what} dist={distance:F2}m in {duration:F2}s{(lungeEndedShort ? " (capped short)" : "")}{air}");
        }

        if (lungeCoroutine != null) StopCoroutine(lungeCoroutine);
        if (distance > 0.01f)
            lungeCoroutine = StartCoroutine(LungeRoutine(dir, distance, duration));
    }

    private IEnumerator LungeRoutine(Vector3 dir, float distance, float slideDuration)
    {
        float duration = Mathf.Max(0.01f, slideDuration);
        float elapsed = 0f;
        float prevEased = 0f;

        // ROUND 8 diagnostics: the trace above prints what we INTENDED to travel. This prints what
        // actually happened, so "the log says 2.98m but she did not move" can never be a mystery
        // again. Three things can silently eat a launch: another action taking over, the ledge
        // probe refusing the next step, or a collider blocking the Move.
        Vector3 lungeStartPos = cachedTransform.position;
        string lungeEndReason = "completed";

        // ROUND 9 - THE BUG THAT ATE EVERY LAUNCH. The ledge check is only meaningful if it can see
        // the floor Yoru is STANDING on. In CaveScene_Oni_Boss1 the terrain sits on layer Default
        // while the probe mask was Ground only, so the probe found nothing anywhere in the scene,
        // read that as "cliff", and cancelled the launch on its first frame: 11 of 11 attacks in
        // the 15:36 log reported "actually moved=0.00m - STOPPED BY LEDGE PROBE after 0.004s".
        // A probe that cannot see the ground under her feet is misconfigured, not a cliff, so it
        // now stands down (once, loudly) instead of silently eating the launch. Where the mask IS
        // right - DemoScene_Day's terrain is on Ground, which is why the Noppera fight always
        // launched correctly - nothing changes and cliff protection still works.
        bool airborneNow = characterController != null && !characterController.isGrounded;
        bool startedGrounded = characterController != null && characterController.isGrounded;
        bool edgeSafetyActive = false;
        if (useEdgeSafety && !airborneNow)
        {
            edgeSafetyActive = GroundAhead(cachedTransform.position);
            if (!edgeSafetyActive) WarnEdgeProbeBlind();
        }

        while (elapsed < duration)
        {
            // Bail the instant another action takes over.
            if (isDodging || isDashing || isInHitReaction || isGuarding)
            {
                lungeEndReason = $"INTERRUPTED (dodge={isDodging} dash={isDashing} hitReact={isInHitReaction} guard={isGuarding})";
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);          // ease-out: fast start, settles
            float frameDelta = eased - prevEased;
            prevEased = eased;

            if (characterController != null && characterController.enabled)
            {
                Vector3 step = dir * (distance * frameDelta);

                // Edge safety: do not slide off a ledge. Probe the spot we are about to enter.
                // Skipped when the probe is blind (see above) or when she is already airborne,
                // where "is there floor at the next step" has no meaning.
                if (edgeSafetyActive && !GroundAhead(cachedTransform.position + step))
                {
                    lungeEndReason = $"STOPPED BY LEDGE PROBE after {elapsed:F3}s";
                    break;
                }

                // ROUND 9: no second gravity. The old keep-down line added one on top of the one
                // PlayerMovement already applies every FixedUpdate, as a raw position jump of g*dt
                // (about 0.16 m in a single frame) - the height drop that made airborne launches
                // unusable. Gravity is PlayerMovement's job.
                //
                // ROUND 9b: a launch that STARTED grounded keeps a small downward stick instead of
                // a flat zero. CharacterController.Move() with a purely horizontal vector clears
                // isGrounded, so the 16:23 log shows AirPosePin engaging within 5ms of every single
                // ground launch - she was skimming off the floor for the whole slide, bobbing 3-13cm
                // and picking up fall gravity mid-attack. Airborne launches still get a flat zero:
                // nothing may touch her fall arc.
                step.y = startedGrounded ? -Mathf.Max(0f, launchGroundStick) * Time.deltaTime : 0f;

                // Wall safety: CharacterController.Move resolves wall collisions, so Yoru cannot pass through one.
                characterController.Move(step);
            }
            yield return null;
        }

        if (logComboTrace)
        {
            Vector3 moved = cachedTransform.position - lungeStartPos;
            moved.y = 0f;
            Debug.Log($"[ComboTrace] LAUNCH RESULT wanted={distance:F2}m actually moved={moved.magnitude:F2}m — {lungeEndReason}");
        }
        lungeCoroutine = null;
    }

    // Preallocated so the probe never allocates per frame (standing performance rule).
    private readonly RaycastHit[] edgeProbeHits = new RaycastHit[8];
    private bool edgeProbeBlindWarned;

    /// <summary>Edge probe: is there solid ground (in edgeGroundMask) just below the given world position?</summary>
    private bool GroundAhead(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 0.3f;
        float probe = 0.3f + edgeProbeDepth;
        // ROUND 9: its own mask (not the line-of-sight one), and ALL hits are examined rather than
        // just the nearest. The old single-hit form returned "no ground" whenever the first thing
        // the ray met happened to belong to Yoru, which is a second way the launch could die.
        int mask = edgeGroundMask.value != 0 ? edgeGroundMask.value : environmentMask.value;
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, edgeProbeHits, probe, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = edgeProbeHits[i].collider;
            if (c != null && c.transform.root != cachedTransform.root) return true;
        }
        return false;
    }

    /// <summary>
    /// One-time, loud: the edge probe cannot see the floor Yoru is standing on, so it has been
    /// stood down for this scene. Names the floor and the layer it is actually on, so the fix is
    /// one click instead of another week of "the launch does nothing".
    /// </summary>
    private void WarnEdgeProbeBlind()
    {
        if (edgeProbeBlindWarned) return;
        edgeProbeBlindWarned = true;

        string floor = "nothing at all within " + (0.3f + edgeProbeDepth).ToString("F2") + "m";
        Vector3 origin = cachedTransform.position + Vector3.up * 0.3f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.3f + edgeProbeDepth, ~0, QueryTriggerInteraction.Ignore))
        {
            int l = hit.collider.gameObject.layer;
            floor = $"'{hit.collider.name}' on layer {l} '{LayerMask.LayerToName(l)}'";
        }
        Debug.LogWarning($"[PlayerCombat] EDGE PROBE BLIND: nothing in Edge Ground Mask under Yoru's own feet, "
            + $"so the ledge check would cancel every launch in this scene. It has been stood down here. "
            + $"The floor under her is {floor}. Add that layer to Edge Ground Mask (or move the floor onto a "
            + $"layer that is in it) to get ledge protection back.");
    }
    #endregion

    #region Beyblade Finisher (Combo 3)
    /// <summary>
    /// Begin the beyblade. Combo 3 spins in place and strikes nearby enemies one at a time. With a
    /// crowd it keeps spinning and circles back to re-hit until beybladeMaxTime. Against a single
    /// enemy it strikes once, spins a brief beat (beybladeSingleWindDown), then stops.
    /// </summary>
    private void StartBeyblade()
    {
        isBeyblading = true;
        beybladeRotationIndex = 0;
        if (beybladeCoroutine != null) StopCoroutine(beybladeCoroutine);
        beybladeCoroutine = StartCoroutine(BeybladeRoutine());
    }

    private IEnumerator BeybladeRoutine()
    {
        float startTime = Time.time;

        // Let the lunge carry Yoru into the group before the first strike.
        yield return new WaitForSeconds(Mathf.Max(0.01f, lungeDuration));

        while (true)
        {
            if (!isAttacking || isInHitReaction || isDodging || isDashing || isGuarding) break;

            // Keep the spin visually looping (a clean rotational clip restarts seamlessly).
            if (animator != null)
            {
                AnimatorStateInfo si = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (si.shortNameHash == combo3StateHash && si.normalizedTime >= 1f)
                    animator.Play(combo3StateHash, combatLayerIndex, 0f);
            }

            System.Collections.Generic.List<EnemyHealth> targets = EnemiesInBeybladeRange();

            // Whiffed into empty space: let the spin clip play out to its natural end before
            // returning to idle. A flat short wait cuts the ~0.8s swirl off after a fraction of a
            // second, which reads as a fast, interrupted snap (especially now the finisher no
            // longer lunges far). Poll the clip and end once it is essentially complete, capped by
            // beybladeMaxTime so a missing or interrupted Combo3 state can never hang the spin.
            if (targets.Count == 0)
            {
                float whiffTimer = 0f;
                while (whiffTimer < beybladeMaxTime)
                {
                    if (!isAttacking || isInHitReaction || isDodging || isDashing || isGuarding) break;
                    AnimatorStateInfo spin = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                    if (spin.shortNameHash == combo3StateHash && spin.normalizedTime >= 0.95f) break;
                    whiffTimer += Time.deltaTime;
                    yield return null;
                }
                break;
            }

            // Round-robin so a crowd is worked one by one and then circled back onto (re-hit).
            if (beybladeRotationIndex >= targets.Count) beybladeRotationIndex = 0;
            EnemyHealth target = targets[beybladeRotationIndex];
            beybladeRotationIndex++;

            target.TakeDamage(combo3Damage, false);
            engagedInCombatUntil = Time.time + engagedInCombatDuration;

            Collider c = target.GetComponent<Collider>();
            Vector3 contact = c != null ? c.ClosestPoint(attackPoint.position) : target.transform.position;
            Animator enemyAnim = target.GetComponent<Animator>();
            if (enemyAnim == null) enemyAnim = target.GetComponentInChildren<Animator>();
            PlayStrikeFeedback(contact, false, true, enemyAnim);

            // Single enemy: one strike and done. Spin a brief beat so it reads, then stop.
            //
            // The beat used to be a flat beybladeSingleWindDown (0.3s) — which, against a lone boss,
            // ended the swirl at ~40% of its 0.79s clip and crossfaded to idle. That is the "swirl
            // plays halfway then snaps back to idle" complaint, and it only ever happened when the
            // finisher HIT (a whiff already let the clip finish). Now: wait at least the wind-down,
            // then let the spin clip actually complete, capped by beybladeMaxTime.
            if (targets.Count == 1)
            {
                float held = 0f;
                while (true)
                {
                    if (!isAttacking || isInHitReaction || isDodging || isDashing || isGuarding) break;
                    held += Time.deltaTime;
                    bool minDone = held >= Mathf.Max(0f, beybladeSingleWindDown);
                    if (minDone && !beybladeSingleLetClipFinish) break;
                    AnimatorStateInfo spin = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                    bool clipDone = spin.shortNameHash == combo3StateHash && spin.normalizedTime >= 0.95f;
                    if (minDone && (clipDone || Time.time - startTime >= beybladeMaxTime)) break;
                    yield return null;
                }
                break;
            }

            yield return new WaitForSeconds(beybladeHitInterval);

            // Crowd hard cap so a big group can never trap the player in an endless spin.
            if (Time.time - startTime >= beybladeMaxTime) break;
        }

        isBeyblading = false;
        beybladeCoroutine = null;
        OnAttackEnd();
    }

    /// <summary>Live enemies inside attackRange right now, sorted by instance id for a stable rotation order.</summary>
    private System.Collections.Generic.List<EnemyHealth> EnemiesInBeybladeRange()
    {
        System.Collections.Generic.List<EnemyHealth> list = new System.Collections.Generic.List<EnemyHealth>();
        Collider[] inRange = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider col in inRange)
        {
            EnemyHealth eh = col.GetComponent<EnemyHealth>();
            if (eh == null || eh.IsDead()) continue;
            if (!list.Contains(eh)) list.Add(eh);
        }
        list.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        return list;
    }
    #endregion

    #region Ground Combo
    private void TryGroundCombo()
    {
        if (Time.time - lastAttackTime < attackCooldown)
        {
            // Silently swallowed input. If three fast clicks only ever produce two attacks, this
            // is where the third one died.
            if (logComboTrace)
                Debug.Log($"[ComboTrace] CLICK DROPPED by attackCooldown ({attackCooldown:F2}s, "
                        + $"{Time.time - lastAttackTime:F2}s since last attack) step={currentComboStep}");
            return;
        }

        if (isAttacking)
        {
            if (currentComboStep < 3 && queuedClicks < 2)
            {
                queuedClicks++;
                DebugLog($"Queued click #{queuedClicks} (combo {currentComboStep})");
                if (logComboTrace) Debug.Log($"[ComboTrace] QUEUED #{queuedClicks} during step {currentComboStep}");
            }
            else if (logComboTrace)
            {
                Debug.Log($"[ComboTrace] CLICK IGNORED (step={currentComboStep} queued={queuedClicks})");
            }
            return;
        }
        PerformGroundCombo();
    }

    private bool pendingChain;

    private void PerformGroundCombo()
    {
        pendingChain = false;
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        attackStartTime = Time.time;

        // Magnet: grab the closest valid enemy (cone + range + line of sight) and face it instantly.
        currentLungeTarget = AcquireTarget();
        FaceTargetFast(currentLungeTarget, true);

        if (currentComboStep > 0 && Time.time - lastAttackTime > comboWindowTime)
        {
            currentComboStep = 0;
            DebugLog("Combo window expired");
        }

        currentComboStep++;
        if (currentComboStep > 3) currentComboStep = 1;

        DebugLog($"Combo {currentComboStep}: {GetComboDamage(currentComboStep)} dmg");
        if (logComboTrace)
            Debug.Log($"[ComboTrace] START step={currentComboStep} state='{GetComboStateName(currentComboStep)}' queued={queuedClicks} target={lastTargetTrace}");

        // Lunge toward the target, re-found every hit. With no target, hits 1-2 stay planted and
        // only the finisher (combo step 3) nudges forward — see StartLunge. No position freeze.
        StartLunge(currentLungeTarget, currentComboStep == 3);

        PlayCombatAnimation(GetComboStateName(currentComboStep));

        if (vfxManager != null) vfxManager.PlayComboVFX(currentComboStep);

        animator.SetInteger(HashComboStep, currentComboStep);
        animator.SetBool(HashIsAttacking, true);

        isAttacking = true;
        isAerialAttack = false;
        canQueueNextAttack = false;
        lastAttackTime = Time.time;

        // Combo 3 is the beyblade finisher: keep spinning and tick every nearby enemy once, then stop.
        if (currentComboStep == 3)
            StartBeyblade();
    }

    private string GetComboStateName(int step)
    {
        switch (step)
        {
            case 1: return combo1StateName;
            case 2: return combo2StateName;
            case 3: return combo3StateName;
            default: return combo1StateName;
        }
    }

    private int GetComboDamage(int step)
    {
        switch (step)
        {
            case 1: return combo1Damage;
            case 2: return combo2Damage;
            case 3: return combo3Damage;
            default: return combo1Damage;
        }
    }
    #endregion

    #region Aerial Spin
    private void TryAerialSpin()
    {
        if (hasUsedAerialAttack || isAttacking) return;

        // No airtime refusal any more. The spin used to be cut off by the landing, so it had to be
        // refused when there was no room left; now it carries through the landing and finishes on
        // the ground, so there is nothing to protect against and it may always start.
        PerformAerialSpin();
    }

    /// <summary>
    /// Seconds until Yoru reaches the ground below her, or -1 when nothing is within probe range
    /// (a long drop, which counts as plenty of time). Uses the live fall acceleration sampled from
    /// the controller, so it stays right whatever gravity PlayerMovement applies, and it accounts
    /// for still rising by adding the climb to the apex and the fall back down.
    /// </summary>
    private float EstimateTimeToLand()
    {
        if (characterController == null) return -1f;
        if (characterController.isGrounded) return 0f;

        const float originLift = 0.3f;
        Vector3 origin = cachedTransform.position + Vector3.up * originLift;

        // Nearest hit that is NOT Yoru herself. Environment Mask is Everything by default, so a
        // plain Raycast starts inside her own capsule and reports zero distance, which reads as
        // zero airtime and refuses every spin.
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundProbeBuffer,
            airtimeProbeDistance, environmentMask, QueryTriggerInteraction.Ignore);
        float nearest = -1f;
        for (int i = 0; i < count; i++)
        {
            if (groundProbeBuffer[i].transform == null) continue;
            if (groundProbeBuffer[i].transform.root == cachedTransform.root) continue; // self
            if (nearest < 0f || groundProbeBuffer[i].distance < nearest)
                nearest = groundProbeBuffer[i].distance;
        }
        if (nearest < 0f) return -1f;

        float distance = Mathf.Max(0f, nearest - originLift);
        float g = Mathf.Abs(measuredFallAccel) > 0.5f ? Mathf.Abs(measuredFallAccel) : Mathf.Abs(Physics.gravity.y);
        if (g < 0.5f) return -1f;

        float vy = characterController.velocity.y;
        if (vy > 0f)
        {
            // Still rising: time up to the apex, then the fall from that extra height.
            float apexRise = (vy * vy) / (2f * g);
            return (vy / g) + Mathf.Sqrt(2f * (distance + apexRise) / g);
        }

        float fallSpeed = -vy;
        return (Mathf.Sqrt(fallSpeed * fallSpeed + 2f * g * distance) - fallSpeed) / g;
    }

    /// <summary>Samples how hard Yoru is actually accelerating downward while airborne.</summary>
    private void TrackFallAcceleration()
    {
        if (characterController == null) return;

        float vy = characterController.velocity.y;
        if (!characterController.isGrounded && Time.deltaTime > 0.0001f)
        {
            float accel = (vy - prevVelocityY) / Time.deltaTime;
            // Only downward acceleration inside a sane range, so jump impulses and landing spikes
            // cannot poison the reading.
            if (accel < -0.5f && accel > -200f)
                measuredFallAccel = measuredFallAccel < -0.5f
                    ? Mathf.Lerp(measuredFallAccel, accel, 0.2f)
                    : accel;
        }
        prevVelocityY = vy;
    }

    private void PerformAerialSpin()
    {
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        attackStartTime = Time.time;
        currentLungeTarget = AcquireTarget();
        FaceTargetFast(currentLungeTarget, true);

        // ROUND 9, Hazel's call: the AIRBORNE attack launches too. This is the path a click in the
        // air actually takes (HandleInput sends it to TryAerialSpin, not to the ground combo), so
        // lifting StartLunge's grounded early-out alone would have changed nothing here - the spin
        // never asked for a launch at all. Guarded by Launch In Air, so every other fight keeps the
        // old planted spin. The slide is horizontal only; PlayerMovement still owns the fall.
        if (launchEnabled && launchInAir) StartLunge(currentLungeTarget, true);

        hasUsedAerialAttack = true;
        isAerialAttack = true;
        currentComboStep = 3;
        DebugLog($"Aerial spin — {aerialSpinDamage} dmg");
        UnlockPosition();
        PlayCombatAnimation(combo3StateName);
        animator.SetInteger(HashComboStep, 3);
        animator.SetBool(HashIsAttacking, true);
        isAttacking = true;
        canQueueNextAttack = false;
        queuedClicks = 0;
        lastAttackTime = Time.time;

        // The routine owns the spin from here: it keeps hurting everything close for the whole
        // clip and carries the spin through the landing so it finishes on the ground.
        if (aerialSpinCoroutine != null) StopCoroutine(aerialSpinCoroutine);
        aerialSpinCoroutine = StartCoroutine(AerialSpinRoutine());
    }

    /// <summary>
    /// Runs the spin for its whole clip, landing included, ticking damage onto every enemy in range
    /// as it goes. Ends when the clip is essentially complete, when another action takes over, or
    /// at the safety cap, whichever comes first. The landing never ends it.
    /// </summary>
    private IEnumerator AerialSpinRoutine()
    {
        aerialSpinTicking = true;
        float startTime = Time.time;
        float nextTick = 0f;

        while (isAttacking && isAerialAttack)
        {
            if (isInHitReaction || isDodging || isDashing || isGuarding) break;
            if (Time.time - startTime >= aerialSpinMaxTime) break;

            if (Time.time >= nextTick)
            {
                nextTick = Time.time + Mathf.Max(0.02f, aerialSpinTickInterval);
                TickAerialSpinDamage();
            }

            // End on the clip's own finish, never on the landing. Reaching the state can take a
            // frame or two through the crossfade, hence the state check before reading progress.
            if (animator != null)
            {
                AnimatorStateInfo spin = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
                if (spin.shortNameHash == combo3StateHash && spin.normalizedTime >= 0.95f) break;
            }

            yield return null;
        }

        aerialSpinTicking = false;
        aerialSpinCoroutine = null;

        // Only close the attack out if nothing else already did (the clip's own OnAttackEnd event,
        // a hit reaction, a dodge), so this can never end an action it does not own.
        if (isAttacking && isAerialAttack) OnAttackEnd();
    }

    /// <summary>One damage tick: every live enemy inside attack range takes the spin's tick damage.</summary>
    private void TickAerialSpinDamage()
    {
        if (attackPoint == null) return;

        System.Collections.Generic.List<EnemyHealth> targets = EnemiesInBeybladeRange();
        foreach (EnemyHealth target in targets)
        {
            if (target == null || target.IsDead()) continue;

            target.TakeDamage(aerialSpinTickDamage, false);
            engagedInCombatUntil = Time.time + engagedInCombatDuration;

            Collider c = target.GetComponent<Collider>();
            Vector3 contact = c != null ? c.ClosestPoint(attackPoint.position) : target.transform.position;
            Animator enemyAnim = target.GetComponent<Animator>();
            if (enemyAnim == null) enemyAnim = target.GetComponentInChildren<Animator>();
            PlayStrikeFeedback(contact, false, true, enemyAnim);
        }
    }
    #endregion

    #region Heavy Attack
    /// <summary>
    /// Begin charging the heavy punch. Plays the WindUp animation (pull-back), fires the
    /// charge-start audio cue, and starts the charge-loop SFX. The WindUp → Hold crossfade
    /// is handled by Update() when chargePercent crosses 1.0 (same pattern as parryIntroComplete).
    /// </summary>
    private void StartHeavyCharge()
    {
        combatIdleSettledTimer = 0f;
        isChargingHeavy = true;
        chargeHoldStarted = false;
        chargeReadyAnnounced = false;
        heavyChargeStartTime = Time.time;
        currentComboStep = 0;

        PlayCombatAnimation(heavyChargeWindUpState);

        if (vfxManager != null) vfxManager.PlayHeavyChargeBuildupVFX();

        if (CombatSFXManager.Instance != null)
        {
            CombatSFXManager.Instance.PlayHeavyChargeStart();
            CombatSFXManager.Instance.PlayHeavyChargeLoop();
        }

        DebugLog("Charging heavy...");
    }

    /// <summary>
    /// Release the held charge as a strike. Damage = combo1Damage + Mathf.RoundToInt(chargePercent × heavyChargeBonusMax).
    /// Uncharged release does combo1Damage (10) — same as a regular punch, by design.
    /// Fully charged release does combo1Damage + heavyChargeBonusMax (110 at defaults).
    /// </summary>
    private void ReleaseHeavyAttack()
    {
        attackStartTime = Time.time;
        // Re-acquire on release so the strike homes onto the enemy even if it moved while charging
        // and Yoru's facing drifted.
        currentLungeTarget = AcquireTarget();
        FaceTargetFast(currentLungeTarget, true);
        storedHeavyChargePercent = Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
        int damage = combo1Damage + Mathf.RoundToInt(storedHeavyChargePercent * heavyChargeBonusMax);
        DebugLog($"Heavy {storedHeavyChargePercent * 100f:F0}% = {damage} dmg");
        // The release lunges/slides to the enemy (capped) instead of freezing in place. With no
        // target it stays planted (no nudge) rather than sliding into empty space.
        StartLunge(currentLungeTarget, false);
        PlayCombatAnimation(heavyReleaseState);
        if (vfxManager != null)
        {
            vfxManager.StopHeavyChargeBuildupVFX();
            vfxManager.PlayHeavyAttackVFX();
        }
        if (CombatSFXManager.Instance != null)
        {
            CombatSFXManager.Instance.StopHeavyChargeLoop();
            CombatSFXManager.Instance.PlayHeavyChargeRelease();
        }
        animator.SetBool(HashIsAttacking, true);
        isChargingHeavy = false;
        chargeHoldStarted = false;
        chargeReadyAnnounced = false;
        isAttacking = true;
        lastAttackTime = Time.time;
        currentComboStep = 0;

        // Self-healing return-to-idle: schedule OnAttackEnd at clip length - 0.1s buffer
        // (small buffer lets the return-to-idle CrossFade start before the clip hits its last
        // frame). Works whether or not the OnAttackEnd animation event is set on the Release
        // clip — the event will fire OnAttackEnd directly if present, and the Invoke fires it
        // as a fallback. OnAttackEnd's "if (!isAttacking) return" guard prevents double-fire.
        float invokeDelay = Mathf.Max(0.1f, heavyReleaseClipLength - 0.1f);
        Invoke(nameof(OnAttackEnd), invokeDelay);
    }

    /// <summary>
    /// Cancel an in-progress charge without firing the strike. Used by guard, dodge, dash,
    /// and the LMB-released-but-flag-stuck safety. Resets all charge state and stops audio.
    /// Caller is responsible for any animation crossfade (e.g. ReturnToIdle) if needed —
    /// guard/dodge/dash play their own next-state animation immediately after.
    /// </summary>
    private void CancelHeavyCharge()
    {
        isChargingHeavy = false;
        chargeHoldStarted = false;
        chargeReadyAnnounced = false;
        attackButtonHoldTime = 0f;
        storedHeavyChargePercent = 0f;
        if (vfxManager != null) vfxManager.StopHeavyChargeBuildupVFX();
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.StopHeavyChargeLoop();
    }

    public float GetHeavyChargePercent()
    {
        if (!isChargingHeavy) return 0f;
        return Mathf.Clamp01((Time.time - heavyChargeStartTime) / heavyChargeTimeMax);
    }
    #endregion

    #region Animation Playback
    private void PlayCombatAnimation(string stateName)
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(stateName, 0.05f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;
    }

    private void ReturnToIdle()
    {
        if (animator == null) return;
        animator.CrossFadeInFixedTime(combatIdleStateName, 0.1f, combatLayerIndex);
        lastCombatCrossFadeTime = Time.time;
    }
    #endregion

    #region Position Lock
    private void LockPositionNow()
    {
        lockPosition = true;
        lockedPosition = cachedTransform.position;
        lockedRotation = cachedTransform.rotation;
        wasGroundedWhenLocked = characterController != null && characterController.isGrounded;
        if (characterController != null)
        {
            characterController.enabled = false;
            cachedTransform.position = lockedPosition;
            cachedTransform.rotation = lockedRotation;
            characterController.enabled = true;
        }
    }

    private void UnlockPosition()
    {
        if (!lockPosition) return;
        lockPosition = false;
        wasGroundedWhenLocked = false;
    }
    #endregion

    #region Hit Detection
    public void DealDamage()
    {
        // During the beyblade finisher, BeybladeRoutine deals the damage one enemy at a time, so the
        // Combo3 clip's own DealDamage animation event must not also fire (it would double-hit).
        if (isBeyblading) return;
        // Same for the aerial spin, where AerialSpinRoutine ticks the damage for the whole clip.
        if (aerialSpinTicking) return;
        int damage = isAerialAttack ? aerialSpinDamage : GetComboDamage(currentComboStep);
        bool isFinisher = !isAerialAttack && currentComboStep == 3;
        DealDamageInRange(damage, isFinisher);
    }

    public void DealHeavyDamage()
    {
        int damage = combo1Damage + Mathf.RoundToInt(storedHeavyChargePercent * heavyChargeBonusMax);
        DealDamageInRange(damage, true);
    }

    private void DealDamageInRange(int damage, bool isHeavy)
    {
        // "Not so forceful": if the lunge was capped short of the target and Yoru had to reach for the
        // hit, the connecting blow is weaker. Default multiplier 1.0 = no change.
        if (lungeEndedShort && reachHitDamageMultiplier < 1f)
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * reachHitDamageMultiplier));

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage, isHeavy);
                DebugLog($"Hit {enemy.name} for {damage}{(isHeavy ? " (heavy)" : "")}");

                // Mark combat engaged — Yoru-to-enemy half of "hit exchanged either way"
                // per GDD Doc 04 §4a. Locks form transform for engagedInCombatDuration seconds.
                engagedInCombatUntil = Time.time + engagedInCombatDuration;

                Vector3 contactPoint = enemy.ClosestPoint(attackPoint.position);
                Animator enemyAnimator = enemy.GetComponent<Animator>();
                if (enemyAnimator == null)
                    enemyAnimator = enemy.GetComponentInChildren<Animator>();
                bool isCombo3 = !isAerialAttack && currentComboStep == 3;
                PlayStrikeFeedback(contactPoint, isHeavy, isCombo3, enemyAnimator);
            }
        }
    }

    /// <summary>
    /// Fire on-hit feedback for one strike. While the hallucination is active the hit deals no real
    /// damage (EnemyHealth blocks it), so the impact juice (camera shake, FOV punch, hitstop, spark)
    /// is suppressed and a phantom sound plays instead, so the swing reads as passing through. Only the
    /// outgoing hit is muted here; received-damage shake (an enemy hitting Yoru) is separate and stays.
    /// </summary>
    private void PlayStrikeFeedback(Vector3 contactPoint, bool isHeavy, bool isCombo3, Animator enemyAnimator)
    {
        if (HallucinationEffect.IsActive)
        {
            if (CombatSFXManager.Instance != null) CombatSFXManager.Instance.PlayPhantomHit();
            return;
        }

        if (CombatFeedbackManager.Instance != null)
            CombatFeedbackManager.Instance.PlayHitFeedback(contactPoint, isHeavy, animator, enemyAnimator);
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlayImpact(isHeavy, isCombo3);

        // "Show me the hit landed." CombatFeedbackManager's spark path silently does nothing while
        // YoruVFXManager's Light/Heavy Hit Spark Prefab fields are empty, which is why no impact VFX
        // was visible. This burst is generated in code so it works with nothing assigned.
        if (proceduralHitSpark)
            ProceduralImpactFX.Spark(contactPoint, isHeavy);
    }
    #endregion

    #region VFX/SFX Animation Events
    public void VFX_SpinStart()
    {
        if (vfxManager != null) vfxManager.PlaySpinStart();
    }

    public void VFX_SpinStop()
    {
        if (vfxManager != null) vfxManager.PlaySpinStop();
    }

    public void SFX_Swing()
    {
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlaySwing(currentComboStep);
    }

    public void SFX_SwingHeavy()
    {
        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.PlaySwing(0);
    }
    #endregion

    #region Animation Events — Combat Flow
    public void OnCanQueueNextAttack()
    {
        if (isBeyblading) return; // no combo chaining during the finisher spin
        canQueueNextAttack = true;
        if (queuedClicks <= 0) return;

        // THIS is where a combo link gets cut short. OnCanQueueNextAttack is an animation EVENT
        // inside each combo clip, and it used to start the next step the instant it fired — so the
        // current clip is chopped off at whatever frame that event happens to sit on. If the event
        // is early in Combo2, Combo2 visibly plays about half way and is replaced. That is the
        // "second animation gets interrupted and snaps" complaint, and no amount of blending hides it.
        //
        // Rather than making you re-place events on every clip, the chain now waits until the
        // current clip has actually played Combo Cancel Min Progress of itself. Set to 0 to get the
        // exact old behavior back.
        if (comboCancelMinProgress > 0f && CombatClipProgress() < comboCancelMinProgress)
        {
            pendingChain = true;
            if (logComboTrace)
                Debug.Log($"[ComboTrace] CHAIN DEFERRED (clip at {CombatClipProgress():F2}, need {comboCancelMinProgress:F2})");
            return;
        }

        queuedClicks--;
        PerformGroundCombo();
    }

    /// <summary>Normalized progress (0-1) of the clip currently playing on the combat layer.</summary>
    private float CombatClipProgress()
    {
        if (animator == null) return 1f;
        if (animator.IsInTransition(combatLayerIndex)) return 0f;
        return animator.GetCurrentAnimatorStateInfo(combatLayerIndex).normalizedTime;
    }

    public void OnAttackEnd()
    {
        // The beyblade owns the end of its spin (BeybladeRoutine clears the flag, then calls this).
        // Ignore the Combo3 clip's own OnAttackEnd event while the spin is still running.
        if (isBeyblading) return;
        if (!isAttacking) return;
        isAttacking = false;
        canQueueNextAttack = false;
        lastAttackTime = Time.time;
        if (currentComboStep >= 3 || isAerialAttack || currentComboStep == 0)
            queuedClicks = 0;
        if (isAerialAttack) isAerialAttack = false;
        UnlockPosition();

        if (logComboTrace && animator != null)
        {
            var st = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            // clipProgress well under 1.00 means this clip's OnAttackEnd animation EVENT is placed
            // early in the timeline, so the clip is being cut by its own event, not by code.
            Debug.Log($"[ComboTrace] END step={currentComboStep} clipProgress={st.normalizedTime:F2} "
                    + $"clipLen={st.length:F2}s queued={queuedClicks} elapsed={Time.time - attackStartTime:F2}s");
        }

        // Seamless chain: if the next step is already bought, start it in THIS frame rather than
        // crossfading to Combat_Idle and letting Update catch the queued click a frame later. That
        // one-frame detour through idle is what reads as "the punch snaps back to idle mid-swing".
        if (chainCombosWithoutIdle && queuedClicks > 0 && currentComboStep > 0 && currentComboStep < 3
            && !isInHitReaction && !isDodging && !isDashing && !isGuarding)
        {
            queuedClicks--;
            animator.SetBool(HashIsAttacking, true);
            PerformGroundCombo();
            return;
        }

        ReturnToIdle();
        animator.SetBool(HashIsAttacking, false);
    }
    #endregion

    #region Reset
    public void ForceResetCombat()
    {
        pendingChain = false;
        isAttacking = false;
        isChargingHeavy = false;
        chargeHoldStarted = false;
        chargeReadyAnnounced = false;
        canQueueNextAttack = false;
        queuedClicks = 0;
        currentComboStep = 0;
        attackStartTime = 0f;
        attackButtonHoldTime = 0f;
        isAerialAttack = false;
        hasUsedAerialAttack = false;
        storedHeavyChargePercent = 0f;
        isInHitReaction = false;
        dodgeEndTime = 0f;
        guardStuckTimer = 0f;
        heavyStuckTimer = 0f;
        combatIdleSettledTimer = 0f;
        movementStuckTimer = 0f;
        guardIdleDebounceTimer = 0f;

        if (CombatSFXManager.Instance != null)
            CombatSFXManager.Instance.StopHeavyChargeLoop();

        if (isGuarding) EndGuard();

        if (isDodging)
        {
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
        }
        if (isDashing)
        {
            isDashing = false;
            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
                dashCoroutine = null;
            }
        }
        if (isBeyblading)
        {
            isBeyblading = false;
            if (beybladeCoroutine != null)
            {
                StopCoroutine(beybladeCoroutine);
                beybladeCoroutine = null;
            }
        }
        if (lungeCoroutine != null)
        {
            StopCoroutine(lungeCoroutine);
            lungeCoroutine = null;
        }
        currentLungeTarget = null;
        if (pullCoroutine != null)
        {
            StopCoroutine(pullCoroutine);
            pullCoroutine = null;
        }
        if (hitReactSafetyCoroutine != null)
        {
            StopCoroutine(hitReactSafetyCoroutine);
            hitReactSafetyCoroutine = null;
        }

        UnlockPosition();
        if (vfxManager != null) vfxManager.PlaySpinStop();
        ReturnToIdle();

        if (animator != null)
        {
            animator.speed = 1f; // Defense in depth: always restore speed on combat reset
            animator.SetBool(HashIsAttacking, false);
            animator.SetInteger(HashComboStep, 0);
        }
        DebugLog("Combat reset");
    }
    #endregion

    #region Public Getters
    public bool IsAttacking()
    {
        // Self-heal: if isAttacking stuck longer than maxAttackDuration, force-clear.
        // Covers aerial spin landing where OnAttackEnd animation event may not fire
        // (e.g. combat layer crossfade interrupts the clip before the event frame).
        if (isAttacking && Time.time - attackStartTime > maxAttackDuration)
        {
            DebugLog("[Self-heal] isAttacking force-cleared (exceeded maxAttackDuration)");
            ForceResetCombat();
            return false;
        }

        // Taz rule: while she is finishing the spin ON THE GROUND, this reports "not attacking" so
        // PlayerMovement lets her run around instead of planting her feet. PlayerMovement is not
        // touched; it simply gets a different answer for that short window. Internally the attack
        // is still live (the field, not this accessor), so nothing else about the spin changes.
        if (moveWhileSpinningOnGround && aerialSpinTicking && isAerialAttack
            && characterController != null && characterController.isGrounded)
            return false;

        return isAttacking;
    }
    public bool IsChargingHeavy() => isChargingHeavy;
    public int GetCurrentComboStep() => currentComboStep;
    public bool IsAerialAttack() => isAerialAttack;
    public bool IsPositionLocked() => lockPosition;
    public bool IsInHitReaction() => isInHitReaction;
    public bool IsDodging()
    {
        // Self-heal: if isDodging stuck past expected duration + 0.5s grace, force-clear.
        // Coroutines can die silently (rapid input, component disable, etc.) without
        // reaching EndDodge(). This getter is called every frame by PlayerMovement line 222,
        // so the flag self-corrects within one frame of exceeding the deadline.
        if (isDodging && Time.time - dodgeStartTime > currentDodgeDuration + 0.5f)
        {
            DebugLog($"[Self-heal] isDodging force-cleared ({Time.time - dodgeStartTime:F2}s, expected {currentDodgeDuration:F2}s)");
            EndDodge();
            return false;
        }
        return isDodging;
    }
    public bool IsDashing()
    {
        // Self-heal: same pattern as IsDodging.
        if (isDashing && Time.time - dashStartTime > currentDashDuration + 0.5f)
        {
            DebugLog($"[Self-heal] isDashing force-cleared ({Time.time - dashStartTime:F2}s, expected {currentDashDuration:F2}s)");
            EndDash();
            return false;
        }
        return isDashing;
    }
    public bool IsGuarding() => isGuarding;
    public float GetDodgeEndTime() => dodgeEndTime;
    public float GetGuardDamageReduction() => guardDamageReduction;
    public int GetParryCounterDamage() => parryCounterDamage;
    public Animator GetAnimator() => animator;
    public float GetAnimatorSpeed() => animator != null ? animator.speed : -1f;

    /// <summary>
    /// True if a hit has been exchanged (Yoru→enemy or enemy→Yoru) within engagedInCombatDuration
    /// seconds (default 5s). Used by FormController to block form transform during active combat
    /// per GDD Doc 04 §4a. Independent of action-flag state (isAttacking etc) — does not get
    /// masked by accessor self-heal logic. Also the intended foundation for the deferred
    /// "enemies remember combat for 5-10s" anti-exploit rule.
    /// </summary>
    public bool IsEngagedInCombat() => Time.time < engagedInCombatUntil;

    /// <summary>
    /// Called by PlayerHealth.TakeDamage when an enemy attack lands on Yoru's hitbox (the
    /// enemy→Yoru half of "hit exchanged either way"). Refreshes the engagement window.
    /// </summary>
    public void MarkCombatEngaged() => engagedInCombatUntil = Time.time + engagedInCombatDuration;
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[Combat] {message}");
    }
    private void OnDrawGizmosSelected()
    {
        if (!showHitboxGizmo || attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
    #endregion
}