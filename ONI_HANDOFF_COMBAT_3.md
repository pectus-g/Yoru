# ONI HANDOFF — COMBAT 3 (state after round 5b, 2026-08-18)

Read this first if you are a new session (or the same session after a context reset). It replaces
ONI_HANDOFF_COMBAT_2 for everything about the Oni fight. Project: `/Users/asenahazal/Documents/Yoru`,
Unity 6000.2.7f2, branch `Gamefeel`, scene `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`.

## 1. How we work (Hazel's rules — keep them)

- Manager mode. Tell the plan BEFORE doing. Label every step **[YOU]** (Hazel) / **[ME]** (Claude).
  Minimise [YOU]. Simple English, concise. She tunes numbers herself — give knob names.
- Scope: shared scripts (`EnemyCombat`, `EnemyHealth`) only get **opt-in** fields/APIs that default to
  the old behaviour. Everything Oni-specific lives in `OniBoss.cs`. Never change Yoru's combat
  abilities (feedback and diagnostics are fine; targeting/lunge/i-frames/damage are abilities).
- Scene / animator / FBX edits are HERS. `.cs` files are mine. Exception granted in round 5: with her
  explicit OK I may write `.fbx.meta` import settings (text). Backups go to `OniLogs/meta_backup_*`.
- **No pasting logs.** OniBoss writes `<project>/OniLogs/oni_<date>.log` (whole console + charge
  telemetry). Read it from her disk with device_bash. Compile status: `Library/ScriptAssemblies/
  Assembly-CSharp.dll` mtime + `grep 'error CS' Library/Bee/tundra.log.json`. Reimports:
  `Library/Artifacts` mtimes. Recordings: `Assets/Captures/*.mov` (extract frames with ffmpeg).
- Facts, not theories: read the FBX (assimp → assxml → `fbxpose.py` pose evaluation), the scene YAML,
  the controller YAML, the metas. Every "fix" must be verifiable in the next log.
- **Performance rule** (hers, standing): this is a big game. No per-frame allocations, no Find/
  GetComponent in Update, telemetry file-only and only while a charge runs, editor checks Start-only,
  placeholder VFX (ProceduralImpactFX) is temporary → replace with pooled particles / her prefabs.
- Feel target: Zelda-like. Yoru's air kit opens; the ground trade is where damage happens; the Oni
  is a slow tank that commits (no orbiting, no sliding, armor on his swings, flinch ladder readable).

## 2. What is verified (from OniLogs, round 5)

- Charge: wind-up in place → clip frozen on the lance frame, NavMesh rush 14 m/s, brakes at 2.6 m →
  clip jumps to the strike section (0.58) → hit at 0.76. Mesh stays on the transform (pin cancels the
  18 m baked hips travel every frame; verified `meshOff=0.24m`, never culled). Close-range charge
  skips the rush.
- Hold-ground: after his own attacks: backstep 1.5 m (reversed Walk) → Watch (no tracking) → one
  turn → step-in if needed → attack. Verified in log every cycle.
- Reactions: 10 → `Hit_react_light` (0.79 s twitch), 20 → `HitReact_medium` (1.46 s stumble),
  ≥25 single hit → Stagger (EnemyHealth Stagger Damage Threshold 25), knockback light 0 / medium
  0.35 / heavy 0.9 m.
- Round 5b (compiled, awaiting her verdict): attack armor (light/medium hits during Attack/Telegraph
  flash only), rapid-hit burst (2 ticks → full react, 30 dmg in 0.8 s → stagger + push, once per
  burst), phase-2 beat (Phase_Transition clip 2.4 s, invulnerable, roar shake + red ring at 0.6, bar
  crimson at the roar), strike moments from the FBX (Club_Swing 0.55, ClubSwing2 0.32, ClubSlam 0.52,
  KanaboSweep 0.48, charge 0.76).

## 3. Open items

| # | Item | Owner | Notes |
|---|---|---|---|
| O1 | Yoru does not "snap" to the Oni when attacking (she did on day-scene enemies) | ME after her test | Diagnostic added: `[ComboTrace] START ... target=...` says found / none + why. Likely: `HasLineOfSight` linecasts to the enemy ROOT + 0.6 m against Ground — the uneven cave floor blocks it for a 6 m enemy whose root is at his feet. Fix candidate (needs her OK — it is her ability): aim the LOS at the collider bounds centre and measure lunge/stop to the collider surface (ClosestPoint). Same behaviour for small enemies. |
| O2 | KanaboSweep animator state plays the Idle clip | YOU | Drag `Oni Kanabo sweep` onto the state's Motion. Warned at Start. |
| O3 | Ground Pound attack + shockwave (M8) | ME | Clip 3.12 s: jump 0–0.5, land 0.7 (hips 13 m up! Y now baked). Register attack from code (opt-in) or attack-list entry [YOU]. Use `ProceduralImpactFX.Shockwave` with the real AoE radius. |
| O4 | Alert reaction on noticing Yoru (M9) | ME | `Oni_Alert` already plays on aggro; needs to read better. |
| O5 | Phase 1 random singles / Phase 2 combo-heavy (M10) | ME | Engine has comboChanceP1 0.4 / P2 0.6, chaseSpeed 4.5 / 6, cooldown 3 / 2 already. |
| O6 | Turn off ComboTrace + verbose logs when closed (M11) | ME | `logComboTrace` on PlayerCombat; `showDebugLogs` on OniBoss/EnemyCombat. |
| O7 | Real VFX/SFX to replace ProceduralImpactFX and add sounds | YOU later | Then switch off `Procedural Hit Spark`, `Charge Trail VFX`, `Phase Roar Ring Radius` = 0. |
| O8 | Pool / replace ProceduralImpactFX allocations (perf) | ME | Only if it stays past prototype. |
| O9 | Yoru-side global changes to sanity-check in the day scene | YOU | Combo cancel min progress 0.6, beyblade single-target lets the clip finish, damage red flash, hit sparks, multi-hit shake reduction. All are on PlayerCombat / CombatFeedbackManager (shared by every fight). |

## 4. Facts about the assets (measured)

- Rig: `ONI_Base_01` root → `Hips` (depth 1) → Spine … Kanabo1 (club) is a CHILD OF HIPS with its
  own position animation (never pin it). 46 skinned renderers; body renderer 'Body', rootBone Hips.
  Animator: cullingMode CullUpdateTransforms, updateMode Normal, applyRootMotion off.
- Every Oni clip is "Take 001", 24 fps. Charge 2.08 s / 50 f (hips travel 18 m: dash 0.30–0.60,
  land 0.58, club sweep down 0.66–0.78, impact ~0.76). Club Swing 1.58 s (impact ~0.55). Club Swing 2
  2.21 s (impact ~0.32). Club Slam 2.50 s (impact ~0.52, speed 2 in scene). Kanabo sweep 1.25 s
  (impact ~0.48). Hit react light 1.46 s (hips 0.6 m back, 0.2 dip). Hit react lighter light 0.79 s
  (barely moves). Hit react Heavy 1.5 s (1.1 m back, 1.2 m dip — UNUSED). Stagger 3.75 s. Ground
  Pound 3.12 s (10 m jump). Phase transition 2.42 s (club raised 0.34–0.55, roar 0.55–0.75).
- Import: `Oni Charge` had Root Motion Node = RootNode; changed to None + Bake Into Pose Rotation/Y
  (round 5). Result: the pose did NOT change (travel still in the hips) — Unity did not extract it for
  this rig — so the OniBoss pin is the mechanism. Bake Y also on Hit react light / Heavy / Stagger /
  Ground Pound (their crouch/jump is now in the pose). Others untouched (Y not baked).
- Scene: EnemyCombat attackRange 3.5, detectionRange 9, hitReactCooldown 1, staggerDuration 2.5,
  attackCooldown 3 / P2 2, hasPhases 1 threshold 0.5, comboChance 0.4/0.6, chase 4.5/6; attacks
  Club_Swing 16 spd1.4, ClubSwing2 16 spd1.4, ClubSlam 20 spd2, Oni_Charge 18 (lunge fields ignored:
  external drive), KanaboSweep 15 spd1.2. EnemyHealth 500 HP, staggerDamageThreshold 25,
  staggerDamageMultiplier 1.5, allowRestagger 0. Player: PlayerYoru prefabs, enemyLayer = Enemy (10),
  environmentMask = Ground (8), targetingRange 8, angle 180, lungeMaxDistance 2.5, lungeStopGap 1,
  attackRange 1.5. Oni collider: capsule r 1.4 h 6.2; NavMeshAgent r 1.4 h 6.

## 5. Code map (what lives where)

- `Assets/Scripts/Enemy/OniBoss.cs` — everything Oni: tiered reactions (`quickFlinchState`,
  `fullReactState`), wake-on-hit, watch stance, boss bar, freeze guard (real-time), slow-mo watchdog,
  knockback, hold-ground config → engine, blending/facing config → engine, charge (pin + drive:
  `chargeHoldNormalizedTime` 0.40 [live scene value 0.35], `chargeStrikeNormalizedTime` 0.58,
  `chargeStrikeMoment` 0.76, `chargeSpeed` 14, `chargeStopDistance` 2.6), attack armor, burst,
  phase-2 beat, strike-moment overrides, editor sanity checks, log file (`writeLogFile`).
- `Assets/Scripts/Enemy/OniDebugLogFile.cs` — console mirror + telemetry to `OniLogs/`.
- `Assets/Scripts/Combat/EnemyCombat.cs` — shared engine + opt-in APIs: ConfigureAnimationBlending,
  ConfigureFacing, ConfigureMeleeHoldGround, SetExternalLungeControl, HoldAttackSafety,
  SetAttackAnimSpeed/CurrentAttackSpeed, SetComboStepsUseDamageThreshold, SetAttackStrikeMoment,
  SetAttackArmor, SetStaggerTimer, TriggerStagger(duration), GetAnimator, GetCurrentState, IsPhase2.
- `Assets/Scripts/Enemy/EnemyHealth.cs` — SetInvulnerable/IsInvulnerable, OnDamaged event, flash.
- `Assets/Scripts/Combat/PlayerCombat.cs` — Yoru (do not change abilities): combo cancel gate,
  damage flash, procedural spark, beyblade wind-down, ComboTrace (now with target trace).
- `Assets/Scripts/Combat/CombatFeedbackManager.cs` — multi-hit feedback guard; `CameraShake` public.
- `Assets/Scripts/Combat/ProceduralImpactFX.cs` — Spark / Shockwave / Wave placeholders.
- Docs at project root: `ONI_ACTION_PLAN.md` (original plan), `ONI_TEST_CHECKLIST.md`,
  `ONI_ROUND5_NOTES.md` (+ 5b), this file. Analysis tool: `/home/claude/fbxpose.py` (session only —
  recreate: assimp export → assxml; nodes + channels; quaternions x y z w; collapse `$AssimpFbx$` chains
  for animated leaves).

## 6. Log signatures to look for

`[OniLog] console + telemetry → …` · `charge travel bone: 'Hips' at depth 1` · `charge begin/RUSH/
STRIKE/end … max raw drift … pin held it` · `Hit player for N (Attack)` · `hold-ground: BACKSTEP /
backstep done / WATCH / APPROACH` · `react tier: LIGHT/MEDIUM` · `burst: … → STAGGER` · `PHASE 2:
transition / roar / over` · `Hit during Attack — flash only` (armor) · `[ComboTrace] START … target=`
· warnings `react states look SWAPPED`, `KanaboSweep uses the SAME clip as Idle`, `Root Motion Node`.
