# Yoru hit reaction reads late: diagnosis

Measured from the project on 10 Sep 2026: `PlayerCombat.cs`, `PlayerHealth.cs`, `EnemyCombat.cs`,
`OniBoss.cs`, `CombatFeedbackManager.cs`, `Yoru_Animato_Controller`, the scene values on
`PlayerYoru`, the Sep 9 `OniLogs`, and the animation curves inside the five hit reaction FBX files.

## 1. The code is not late. The clips are.

The chain from the club touching Yoru to the reaction being requested runs in ONE call stack,
in the same frame (`OniBoss.UpdateClubTouch` in LateUpdate, then `DeliverStrikeOnTouch`,
`TakeDamage`, `PlayHitReaction`, `HoldHitReaction`, `CrossFadeInFixedTime` with a 0.02 s blend).
Her logs confirm it: every one of the 30 hits on Sep 9 shows the touch line and the
`Hit react:` line on the same frame number. Code side cost, in total:

| Step | Delay |
|---|---|
| Touch detection (predicted one frame early) | 0 frames |
| Damage to reaction request | 0 ms, same call stack |
| Animator applies the crossfade on the next animation update | 1 frame (about 6 ms at 180 FPS) |
| Blend into the reaction clip | 20 ms |
| Total | about 25 ms |

The reaction clips, measured from their FBX rotation and translation curves (delta from frame 0):

| Clip (state) | Length | Torso moves | Head turns 10 deg | Pelvis rotates 10 deg | Body moves 5 cm | Whole body clearly moving | Biggest pose |
|---|---|---|---|---|---|---|---|
| HitReact_Light_2Leg | 1.25 s | 0.17 s (subtle) | 0.47 s | 0.45 s | 0.40 s | 0.38 s | 0.58 s |
| HitReact_Heavy_2Leg | 1.25 s | 0.17 s (subtle) | 0.45 s | 0.47 s | 0.40 s | 0.38 s | 0.58 s |
| HitReact_Running_4Leg | 0.67 s | 0.13 s | 0.10 s | 0.08 s | 0.12 s | 0.10 s | 0.37 s |
| HitReact_Light_4Leg (file `Combat_HitReact_Light_4Leg_nouse.fbx`) | 2.00 s | never | 0.28 s | never | 0.38 s | 0.38 s | 0.43 s |
| Bhit_run_reaction_4 (`Big Hit reaction on 4.fbx`) | 2.53 s | 0.12 s | 0.63 s | 0.08 s | 0.08 s | 0.07 s | 1.57 s |

`PlayerCombat` starts every reaction at `Hit React Start Offset` = 0.10 s. So for the two 2-leg
clips, which are what she is hit with almost every time (29 of the 30 reactions on Sep 9),
the timeline after the club touches her is:

| Time after contact | What the clip shows (2-leg light and heavy) |
|---|---|
| 0.00 s | clip 0.10 s: one shoulder 23 deg, everything else under 6 deg. Invisible. |
| 0.10 s | clip 0.20 s: left shoulder 51 deg, left hip 18 deg, head 2 deg. A one sided shrug. |
| 0.20 s | clip 0.30 s: shoulders 32/8, hips 29/22, head 2 deg, spine 5 deg. Still no flinch. |
| 0.30 s | clip 0.40 s: legs bend (hips 38/58), head 2 deg. |
| 0.37 s | clip 0.47 s: head 13 deg, pelvis 12 deg, arms fold. First frame that reads as a hit. |
| 0.48 s | clip 0.58 s: head 31 deg, body pushed back 8 cm. Peak. |
| 0.50 s | light reaction ends here (`Light Hit React Duration` 0.5) and blends back to idle, at the peak. |

So the flinch becomes readable about 0.35 s after contact and peaks at 0.48 s, and the light
reaction is cut at exactly that peak, so it never lands. That is the "reaction comes late".

Two more content facts from the same curves:

- The light and heavy 2-leg clips are the same animation for their first 0.43 s (max
  difference 1.7 deg across all bones). Heavy only differs after that (bigger head and pelvis,
  16 cm vs 18 cm push). A heavy hit cannot read heavier at contact because it is the same clip.
- The 4-leg standing light reaction is wired to a file the animator named `_nouse`: nothing at all
  for 0.27 s, then a head turn only, torso and pelvis never move. It played once on Sep 9.

## 2. Why earlier rounds concluded the opposite

`TraceHitReactClip` (round 36) reads the animator immediately after the crossfade is requested,
before the animator has processed it, so `IsInTransition` is false, the loop exits at 0 ms and
it logs the PREVIOUS state. Her logs show it: "asked for HitReact_Light_2Leg, now playing a clip
1.23 s long at speed 1.60" (that is Combo1), "0.79 s at speed 0.80" (Combo3), "1.00 s at 1.00"
(Combat_Empty). It never once looked at a reaction clip. "0 ms, 21 times out of 21" measured the
code path, which is indeed instant, and said nothing about the clip.

Round 41 then added the 0.10 s start offset on the assumption that the wind up was 0.1 s. It is
0.38 to 0.45 s on the clips that matter.

## 3. Minor contributors, all small, listed so nothing is hidden

- Hitstop from HER OWN landed attack freezes her animator for 40 to 80 ms; if the club touches
  her inside that window the reaction waits for the freeze to end. Rare.
- ClubSlam: the club touch fires at clip 0.44 while the club reaches the floor at 0.85 (her
  `[OniBoss:Strike]` lines: "damage fires 79 to 84 ms BEFORE the club is closest", and the touch
  itself is 0.38 s before floor contact). The clip lag roughly cancels this, which is why the
  slam probably feels fine while the swings feel late. Once the clip lag is fixed, the slam
  reaction will start reading early and its touch (Club Touch Radius 0.7, Body Top 1.55) will
  need retuning from the `[OniBoss:Touch]` numbers.
- `Bhit_run_reaction_4` is a composite take: the running flinch (frames 0 to 16), a snap to the
  neutral pose at frame 17, the tumble (18 to 59), then the running flinch again (61 to 76).
  Not late, but the snap at frame 17 is a visible hitch on heavy 4-leg hits.

## 4. Fix options

A. Content (the real fix). Brief for the animator, per clip: frame 0 IS the impact pose (head
   already snapped, torso already recoiled, weight already shifted), then 20 to 30 frames of
   recovery. No anticipation on the victim; the anticipation belongs to the attacker. Deliver
   heavy as its own clip (bigger displacement, longer recovery). Replace the 4-leg standing light
   reaction. Trim the frame 17 snap out of the big 4-leg hit.

B. Code, immediate, keeps the current clips: give each reaction its own start offset instead of
   one shared `Hit React Start Offset`. Values from the curves: 2-leg light 0.40, 2-leg heavy
   0.40, 4-leg running 0.10, 4-leg standing 0.28, 4-leg heavy 0.10. Starting the 2-leg clips at
   0.40 puts a readable flinch 2 to 3 frames after contact and the peak at 0.18 s. Their hold
   windows then cover the peak AND the recovery (light: clip 0.40 to 0.90), so the reaction lands
   instead of being cut at its top. This is a small change in `PlayerCombat.cs`: five offset
   fields, `HoldHitReaction` takes the offset as a parameter. Quick test without any code: set
   `PlayerYoru` > Player Combat > Hit Reaction > Hit React Start Offset to 0.4 and take a few
   2-leg hits. It will hurt the 4-leg clips, so it is a test value, not a keeper.

C. Fix the trace so it stops lying: `TraceHitReactClip` must `yield return null` once before it
   reads the animator, and log the state name it actually resolved to.

Do A and B both. B makes the fight testable today, A is what ships.
