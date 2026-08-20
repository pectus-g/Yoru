# ONI HANDOFF — COMBAT 4 (2026-08-20)

Replaces COMBAT_2 and COMBAT_3. Read this whole file before touching anything.
Project `/Users/asenahazal/Documents/Yoru`, Unity 6000.2.7f2, branch `Gamefeel`,
scene `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`.

---

## 0. THE ONE UNSOLVED PROBLEM — read this first

Hazel has asked **four times** for the same thing and it is still not right:

> "in each attack yoru must launch forward" — a visible forward launch at the enemy on every attack,
> like Zelda / Spider-Man. Also the Oni's attacks must snap to her, not swing at air.

**Do not treat this as a movement bug. The movement is happening and it is measured.**
From her 2026-08-20 log:

```
[ComboTrace] LAUNCH OniBoss at 1.6m dist=1.60m in 0.16s
[ComboTrace] LAUNCH RESULT wanted=1.60m actually moved=0.90m — completed
[ComboTrace] LAUNCH OniBoss at 2.1m dist=2.08m in 0.21s [AIRBORNE]
[ComboTrace] LAUNCH RESULT wanted=2.08m actually moved=1.50m — completed
```

She really does slide forward 0.9–1.5 m on every attack. It is invisible because **there is nothing
to cross**:

| number | value | consequence |
|---|---|---|
| Oni capsule radius | **1.4 m** | she can never be nearer than 1.4 m to his centre |
| Yoru `attackRange` (hit sphere at `attackPoint`) | **1.5 m** | she must stand almost against him to connect |
| `lungeStopGap` | **1.0 m** | the launch aims to stop 1 m short of his *surface* |
| launch from 4 m away | 4 − 1.4 − 1.0 = **1.6 m** of travel | and his body blocks the last part → 0.9 m actually moves |

So in a normal fight she is glued to him, every launch has ~1 m of room, it is over in 0.16 s, and
there is **no animation on it** — nothing about it reads as a launch. Three honest ways out; the next
session should put these to her as a choice and not guess:

- **A — give the attacks reach.** Lower `lungeStopGap` to ~0.3 and raise her `attackRange` a little
  so she can start a swing from 3–4 m and cross real ground on the way in. Cheapest, no new art.
- **B — sell it with the animation.** Play the dodge-dash clip (`DodgeDash_2Leg` / `DodgeDash_4Leg`)
  during the crossing, only when the launch is longer than ~1.5 m, blending into the punch. She named
  this animation herself but is worried a wind-up before every punch will feel bad — so gate it on
  distance. Add trail VFX + a small FOV punch so even a 1 m step reads.
- **C — stop her being glued to him.** After each combo she is pushed / walks back a little (the
  mirror of the Oni's hold-ground backstep), so the NEXT attack always has ground to cross. This is
  what actually makes Zelda and Spider-Man read: you are never standing inside the boss.

My honest read: **C + B** is what she is describing. A alone will not look like a launch.
Her fallback wish, in her words: "if this looks bad I want to try big leap spiderman method".

Also part of the same request: **the Oni's swings must snap to her.** That half is implemented
(`Attack Step-In`, below) and the log shows it working (`step-in: 'ClubSlam' closed 0.21m`), but it
only had small gaps to close in that session — it has not been tested from a real distance yet.

---

## 1. How to work with Hazel (rules — keep them)

- **Manager mode.** Say the plan BEFORE doing it. Label every step **[YOU]** (Hazel) / **[ME]**.
  Minimise her part. Simple English. Short. She tunes numbers herself — give her the knob NAME.
- **No assumptions.** She has said this twice. Determine things from her log / the FBX / the scene
  YAML and show the numbers. If you cannot determine it, ask a short question with concrete options.
- **Scope.** `EnemyCombat` / `EnemyHealth` are shared by every enemy: they may only gain **opt-in**
  fields and APIs that default to the old behaviour. Everything Oni-specific goes in `OniBoss.cs`.
  Yoru's combat *abilities* are hers — feedback, diagnostics and opt-in flags are fine, silent changes
  to her damage / i-frames / targeting are not.
- **Whose file is whose.** Scene, animator and FBX edits are HERS. `.cs` files are yours. Exception
  she granted once: with her explicit OK you may rewrite `.fbx.meta` import settings as text, keeping
  backups in `OniLogs/meta_backup_*`.
- **Never ask her to paste a log.** OniBoss writes the whole console to
  `<project>/OniLogs/oni_<date>.log`. Read it from her disk with `device_bash`. Compile check:
  `Library/ScriptAssemblies/Assembly-CSharp.dll` mtime + `grep 'error CS' Library/Bee/tundra.log.json`.
  Screen recordings land in `Assets/Captures/*.mov` (extract frames with ffmpeg).
- **Performance is a standing rule** (hers): big game, many systems coming. No per-frame allocations,
  no `Find` / `GetComponent` in Update, telemetry file-only and only while something is happening,
  editor checks at Start only. The procedural VFX (`ProceduralImpactFX`) is placeholder — pool it or
  delete it once real prefabs exist.
- **Feel target:** Zelda. Yoru's air kit opens; the ground trade is where damage happens; the Oni is
  a slow tank that commits — no orbiting, no sliding, armor on his swings, a readable flinch ladder.
- **Sessions get long.** Keep this handoff updated instead of relying on chat history.

---

## 2. Verified working (from her logs, with the numbers)

| Thing | Evidence |
|---|---|
| Charge | wind-up in place → clip frozen on the lance frame → NavMesh rush 14 m/s → brake at 2.6 m → clip jumps to the strike section (0.58) → hit at 0.76. `charge STRIKE: arrived 2.6m … clip 0.35 → 0.58` |
| Charge mesh pin | the clip's 18 m of hips travel is cancelled every frame: `max raw drift 18.07m, max cancelled 18.07m — pin held it`, mesh offset 0.24 m, never culled |
| Hold-ground | after each of his attacks: backstep 1.5 m (reversed Walk) → Watch (no tracking) → one turn → step-in → swing. No orbiting. |
| Reaction ladder | 10 dmg → `Hit_react_light` (0.79 s), 20 → `HitReact_medium` (1.46 s), ≥25 or heavy → knock-back, swirl burst (3×10 in 0.8 s) → knock-back + 0.9 m push |
| Heavy knock-back | `heavy knock-back react (40 dmg) → 'HitReact_Heavy', down for 1.80s` — that clip had never been used by anything before |
| Attack armor | light/medium hits during his swing flash only, the club keeps coming: `Hit during Attack — flash only` |
| Phase-2 beat | `PHASE 2: clip is 2.42s → 4.63s on screen`, `roar at 2.43s`, `transition over after 4.63s real`. The old 7.47 s freeze is gone. |
| Yoru launch | it moves her; see section 0 for why it does not read |

---

## 3. Open items

| # | Item | Owner | Notes |
|---|---|---|---|
| **O1** | **The launch (section 0)** | ME, after she picks A / B / C | The single most important thing. Four attempts so far. |
| O2 | Phase-2 transition "too short" | YOU, one number | It runs exactly 4.63 s now. `OniBoss → Phase Transition Anim Speed` 0.6 → **0.35** gives ~7 s; `Phase Transition Hold` 0.6 → 1.0 adds a beat at the end. No code needed. |
| O3 | Oni step-in from a real distance | ME to verify | Implemented; only ever had 0.2–1 m gaps to close in her tests. Needs a test where he starts 5–7 m away. |
| O4 | Swirl "shakes / vibrates weirdly" | ME | She parked it: "not the big issue". Old suspects were the overhead LookAt jitter and stacked hit feedback; both were addressed, so it needs a recording. |
| O5 | KanaboSweep plays the Idle clip | YOU, one drag | ONICONTROLLER → state `KanaboSweep` → Motion → `Oni Kanabo sweep`. Warned in the log at every start. |
| O6 | Ground Pound attack + shockwave | ME | Clip is 3.12 s, 10 m jump, lands at 0.70. `Ground_Pound` state exists, no attack entry yet. |
| O7 | Alert reaction when he first notices her | ME | `Oni_Alert` plays on aggro but does not read. |
| O8 | Phase 1 random singles / phase 2 combo-heavy | ME | Engine already has comboChance 0.4 / 0.6, chase 4.5 / 6, cooldown 3 / 2. |
| O9 | Turn the verbose logging off | ME, at the end | `logComboTrace` (PlayerCombat), `showDebugLogs` (OniBoss, EnemyCombat), `writeLogFile` (OniBoss). |
| O10 | Real VFX + SFX | YOU, later | Then switch off `Procedural Hit Spark`, `Charge Trail VFX`, `Phase Roar Ring Radius`. |
| O11 | **Nopperabō regression check** | YOU | Nothing of his was touched, but the launch/magnet is SHARED by every fight. Since round 6 she can start a launch from 6 m (was 2.5 m) and it takes 0.13–0.32 s (was 0.06 s). If his fight felt right before, check it. `Launch Enabled` off = byte-for-byte the old behaviour. |
| O12 | Scene is very dark in phase 2 | YOU | Not the Oni — `StormWeather` in the scene: `Storm Fog Density` 0.7 → 0.5, `Wet Darkening` 0.65 → 0.8, `Storm Rain Multiplier` 2 → 1.5. |

---

## 4. Measured asset facts (do not re-derive these)

**Rig.** `ONI_Base_01` root → `Hips` (depth 1) → Spine…; `Kanabo1` (the club) is a CHILD OF HIPS with
its own position animation — never pin it. 46 skinned renderers, body renderer `Body`, rootBone
`Hips`. Animator: cullingMode CullUpdateTransforms, updateMode Normal, applyRootMotion off.

**Clips** (all named "Take 001", 24 fps). Charge 2.08 s (hips carry 18 m of travel; dash 0.30–0.60,
land 0.58, club down 0.66–0.78, impact ~0.76). Club Swing 1.58 s (impact 0.55). Club Swing 2 2.21 s
(impact **0.32** — the scene said 0.5, which is why hits felt late). Club Slam 2.50 s (impact 0.52).
Kanabo sweep 1.25 s (impact 0.48). Hit react light 1.46 s (hips 0.6 m back). Hit react *lighter*
light 0.79 s. Hit react Heavy 1.5 s (1.1 m back, 1.2 m dip). Stagger 3.75 s. Ground Pound 3.12 s
(10 m jump). Phase transition 2.42 s (club raised 0.34–0.55, roar 0.55–0.75).

**Import settings.** `Oni Charge` Root Motion Node was RootNode → set to None (round 5); it did NOT
change the pose, so the OniBoss pin is what holds the mesh. Bake Into Pose Rotation + Y was also
written into Charge, Hit react light, Hit react Heavy, Stagger, Ground Pound. Backups in
`OniLogs/meta_backup_round5/`.

**Scene numbers.** EnemyCombat: attackRange 3.5, detectionRange 9, hitReactCooldown 1, staggerDuration
2.5, attackCooldown 3 / P2 2, hasPhases 1 @ 0.5, comboChance 0.4 / 0.6, chase 4.5 / 6; attacks
Club_Swing 16 (spd 1.4), ClubSwing2 16 (1.4), ClubSlam 20 (2), Oni_Charge 18 (its lunge fields are
ignored — OniBoss drives it), KanaboSweep 15 (1.2). EnemyHealth: 500 HP, staggerDamageThreshold 25,
staggerDamageMultiplier 1.5, allowRestagger 0. Oni collider capsule r 1.4 h 6.2; NavMeshAgent r 1.4
h 6. Player prefab: enemyLayer = Enemy(10), environmentMask = Ground(8), targetingRange 8, angle 180,
attackRange 1.5, lungeStopGap 1.

**Analysis tooling** (session-local, recreate if needed): `assimp export file.fbx out.assxml`, then a
small Python pose evaluator — parse `<Node>` matrices + `<NodeAnim>` channels, quaternions are
**x y z w**, and the `_$AssimpFbx$_` pivot chains must be collapsed into their animated leaf. That is
how every clip timing above was measured.

---

## 5. Code map

- `Assets/Scripts/Enemy/OniBoss.cs` — the whole boss layer: tiered reactions (`quickFlinchState` =
  `Hit_react_light`, `fullReactState` = `HitReact_medium`), heavy knock-back react, rapid-hit burst,
  wake-on-hit, watch stance, boss bar, real-time freeze guard, slow-motion watchdog, knockback,
  hold-ground config, blending/facing config, the charge (pin + windup/rush/strike drive), attack
  step-in, phase-2 beat, strike-moment overrides, editor sanity checks, log file.
- `Assets/Scripts/Enemy/OniDebugLogFile.cs` — mirrors the console + telemetry into `OniLogs/`.
- `Assets/Scripts/Combat/EnemyCombat.cs` — shared engine. Opt-in API added by this work:
  `ConfigureAnimationBlending`, `ConfigureFacing`, `ConfigureMeleeHoldGround`,
  `SetExternalLungeControl`, `HoldAttackSafety`, `SetAttackAnimSpeed` / `CurrentAttackSpeed`,
  `SetComboStepsUseDamageThreshold`, `SetAttackStrikeMoment`, `SetAttackArmor`, `SetStaggerTimer`,
  `AttackRange`, `CurrentAttackName`, `CurrentAttackAnim`, `ComboStepsRemaining`.
- `Assets/Scripts/Enemy/EnemyHealth.cs` — `SetInvulnerable` / `IsInvulnerable`, `OnDamaged` event.
- `Assets/Scripts/Combat/PlayerCombat.cs` — Yoru. Launch/magnet lives here (`launchEnabled`,
  `launchMaxDistance` 6, `launchSpeed` 20, `launchMaxDuration` 0.32, `launchMinDistance`,
  `launchMinDuration`, plus the older `lunge*` fields), target acquisition (line of sight aims at the
  collider centre, distance measured to the collider surface), ComboTrace logging.
- `Assets/Scripts/Combat/CombatFeedbackManager.cs` — hitstop, shake, multi-hit guard. **Careful: its
  hitstop sets `animator.speed = 0` and then restores the value it saw.** Anything that writes
  `animator.speed` will be silently overwritten by it — that caused the phase-2 freeze. Drive clip
  time with `animator.Play(hash, layer, normalizedTime)` instead.
- `Assets/Scripts/Combat/ProceduralImpactFX.cs` — placeholder Spark / Shockwave / Wave.
- Docs at the project root: `ONI_ACTION_PLAN.md`, `ONI_TEST_CHECKLIST.md`, `ONI_ROUND5_NOTES.md`
  (rounds 5, 5b, 6, 7 — includes the full freeze post-mortem), this file.

---

## 6. Traps already paid for — do not repeat them

1. `AnimatorStateInfo.length` is **already divided** by the state speed AND `animator.speed`. Sizing a
   timer from it and dividing again gave a 7.3 s window around a 2.4 s animation (the phase-2 freeze).
   Use `AnimatorClipInfo.clip.length` for the raw length.
2. `animator.speed` is not yours — the hitstop restores it. Drive time with `Play(..., normalizedTime)`.
3. `CrossFadeInFixedTime`'s offset argument is in **seconds**; `CrossFade`'s is **normalized**. Mixing
   them made the charge play its whole dash on the spot before the slam.
4. `CharacterController.isGrounded` flickers on uneven ground — a grounded-only gate silently killed
   the launch. There is a 0.15 s grace now.
5. A line-of-sight check aimed at a 6 m boss's ROOT (his feet) is blocked by every bump in the cave
   floor. Aim at the collider's bounds centre.
6. Rate-limit anything that reacts to damage: the swirl ticks many times a second and used to restart
   the flinch clip every tick (looked like a freeze) and fire 8 camera shakes.
7. Only act on a FRESH state entry (compare against `lastSeenState`), never on every damage event.
8. The engine parks the NavMeshAgent during Attack (`isStopped = true`); `navAgent.Move()` still works
   and is how the charge, the backstep and the step-in all move him.

---

## 7. Log signatures

`[OniLog] console + telemetry → …` · `charge travel bone: 'Hips' at depth 1` ·
`charge begin / RUSH / STRIKE / end … pin held it` · `step-in: 'X' closed Nm, Yoru now Nm away` ·
`hold-ground: BACKSTEP / backstep done / WATCH / APPROACH` · `react tier: LIGHT / MEDIUM` ·
`heavy knock-back react (…) → 'HitReact_Heavy'` · `burst: N hits / N dmg` ·
`PHASE 2: clip is … on screen` / `roar at …` / `transition over after …` ·
`Hit during Attack — flash only` (armor working) · `[ComboTrace] START … target='OniBoss' at Nm` ·
`[ComboTrace] LAUNCH … dist=Nm in Ns` + `LAUNCH RESULT wanted=Nm actually moved=Nm` ·
warnings: `KanaboSweep uses the SAME clip as Idle`, `react states look SWAPPED`, `Root Motion Node`.

---

## 8. First move for the next session

1. Read the newest `OniLogs/oni_*.log` before saying anything.
2. Put section 0's **A / B / C** to her as a short choice (one question, concrete options) — do not
   start coding the launch again from a guess.
3. Tell her O2 is one number she can change herself right now (`Phase Transition Anim Speed` → 0.35).
