# Oni (Boss 1) — Handoff 1: Animation & Combat Setup

Project: `/Users/asenahazal/Documents/Yoru` · branch `Gamefeel` · 11 Aug 2026
Scene: `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`
Source docs: `YORU_Oni_Animation_Order_and_Roles.docx`, GDD Doc 07f, GDD Doc 04.

> Companion file: `ONI_HANDOFF_LIGHTING_WEATHER.md` covers lighting, weather and post-processing.

---

## 1. Where things stand

### Done

| | |
|---|---|
| **Clip set** | `Assets/Enemies/ONI/Oni Animations 2/` is canonical. Verified by FBX authoring dates: v2 clips are 16 Jul, the old `Oni Animations` folder is 13 Jul. The five 17-Jul revisions from `OniUpdates` (Charge, Club Slam, Club Swing 2, Hit react Heavy, Stagger) were copied in and overwrote the 16-Jul versions. All verified bone-compatible — 336 nodes, zero mismatches against the base. |
| **Avatars** | `ONI_Base_01` → Generic, Create From This Model, **Root node = Hips**. Confirmed correct: `demon_warior_8C` exists only in the base model and in no clip; it is the mesh container. All 17 clips → Copy From Other Avatar. Old folder's base set to No Avatar to kill the duplicate in the picker. |
| **Materials** | 14 external `.mat` remapped onto `ONI_Base_01` from `Assets/Enemies/ONI/texture/Materials/`. FBX slot names use underscores, the `.mat` files use spaces, so auto-search never matched — they were assigned by hand. Normal maps now active. |
| **Loop Time** | Set on Idle, Walk, Run, Watch. |
| **Animator** | `Assets/Enemies/ONI/ONICONTROLLER.controller` — 17 states, **zero transitions**, one Float parameter `AnimSpeed`. Matches the Nopperabo pattern exactly. |
| **Scene object** | Oni built in `CaveScene_Oni_Boss1` with CapsuleCollider (r 1.4, h 6.2, centre y 2.85), NavMeshAgent, Animator, EnemyCombat, EnemyHealth, EnemyFX. Not yet a prefab. |

### Verified Animator state names

Copy these **exactly** into EnemyCombat — `run` and `Club_swing2` really are lowercase:

```
Idle   Walk   run   Oni_Alert   Watch
Hit_react_light   HitReact_medium   HitReact_Heavy   Stagger
Club_Swing   Club_swing2   Club_Slam   Oni_Charge
Phase_Transition   Ground_Pound   KanaboSweep   Death
```

### Measured clip lengths

| Clip | Length | | Clip | Length |
|---|---|---|---|---|
| Idle | 2.08s | | Club Slam | 2.50s |
| Walk | 2.50s | | Charge | 2.08s |
| Run | 1.25s | | Ground Pound | 3.12s |
| Alert | 2.12s | | Kanabo sweep | 1.25s |
| Watch | 2.08s | | Phase transition | 2.42s |
| Hit react light | 1.46s | | Stagger | 3.75s |
| Hit react Heavy | 1.50s | | Death Kneel | 1.88s |
| Club Swing | 1.58s | | Club Swing 2 | 2.21s |
| Hit react lighter light | 0.79s | | | |

---

## 2. TO DO — fix EnemyCombat (blocking)

### 2a. The four attacks — critical

Unity fills new array elements with zeros and ignores the script's C# defaults. Every attack entry is wrong. On **all four** (`Club_Swing`, `ClubSwing2`, `ClubSlam`, `Oni_Charge`):

| Field | Currently | Set to | Why |
|---|---|---|---|
| Telegraph Speed | 0 | **1** | 0 = frozen animation |
| Attack Speed | 0 | **1** | **0 freezes him mid-swing permanently.** The Attack state waits for the clip to finish; at zero speed it never does |
| Skip Telegraph | off | **on** | `telegraphAnim` is blank, so without this he enters Telegraph with no clip to play |
| Interrupts Combo | off | **on** | Otherwise Yoru doesn't flinch when the Oni lands a hit |

### 2b. Single fields

| Field | Currently | Set to |
|---|---|---|
| `idleAnim` | `idle` | **`Idle`** — capital I, must match the state exactly |
| `pullRange` | 6 | **0** — that band is Nopperabo's hair-grab |
| `closeAttackName` | `CloseStrike` | **blank** — Nopperabo's swoop-grab, not his |
| `chaseSpeed` | 3 | **4.5** (GDD Phase 1) |
| `staggerDuration` | 1 | **1.2** (GDD) |
| `bossBarName` | blank | **`Oni`** — blank means no boss bar appears |
| `hasPhases` | off | **on**, threshold 0.5 — only when testing Phase 2 |
| Combo `ClubSwing1 → ClubSwing2 → ClubSlam` weight | 0 | **40** — weight 0 is never picked |

### 2c. NavMeshAgent

| Field | Currently | Set to |
|---|---|---|
| Radius | 0.5 | **~1.4** (matches his capsule) |
| Height | 2 | **~6** |
| Stopping Distance | 0 | **~2.8** (at 0 he walks into Yoru and overlaps) |

The scene's NavMesh bake uses agent radius 0.5. Widening past that will clip corners once the cave has real walls — fine for a flat terrain test.

### 2d. Then, in order

1. Confirm `Idle` is the **default (orange)** state in ONICONTROLLER.
2. Confirm the Oni **and all children** are on layer **Enemy (10)**. Yoru's attacks use an OverlapSphere against that layer; wrong layer = unhittable.
3. **Bake the NavMesh.** `CaveScene_Oni_Boss1` has none baked. He cannot take a single step without it.
4. Press Play. Read the Console.
5. Once it works, drag him into a Project folder to make him a prefab.

---

## 3. Architecture — settled decisions

**`EnemyCombat` is shared and stays shared.** `OniBoss.cs` will be a separate script layered on top, exactly the pattern `KomainuBoss.cs:28` already uses and which works in this project:

```csharp
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyHealth))]
public class KomainuBoss : MonoBehaviour
```

Komainu caches both components, sets `enemyCombat.enabled = false` during scripted beats, drives the Animator itself via its own `PlayState()`, then hands control back with `enabled = true` + `ResetCombatState()` + `BecomeHostile()`, and polls `GetCurrentState() == EnemyState.Returning` for disengage.

**Phase 1 combat needs no new code.** New code is only required for: the phase-2 roar cinematic, ground pound AoE + landing circle, kanabo sweep jump-only avoidance, arena destruction.

**Contract any new enemy must satisfy:**
- Layer `Enemy` (10) — `PlayerCombat` finds enemies with `Physics.OverlapSphere(..., enemyLayer)` at lines 1339, 1730, 2231, 2482, 2905
- `EnemyHealth` **and** `EnemyCombat` on the same GameObject as the collider — `col.GetComponent<EnemyHealth>()` is non-recursive
- An `Animator`
- Non-empty `bossBarName` for the boss health bar (shown on `EnemyState.Alert`, EnemyCombat:471)

**Do not add `EnemyDeathEffect`** — it hardcodes `animator.Play("die", 0, 0f)` at line 110, which clashes with the death state naming.

---

## 4. What the engine does and doesn't support

From `YORU_Oni_Animation_Order_and_Roles.docx`, the "(My side in Unity)" items:

| Your doc | Engine reality |
|---|---|
| Cut slam/charge/pound/sweep into Telegraph + Attack | **Supported** — separate states with separate clip fields, driven to clip end |
| "hitbox on the club only during the swing" | **Not how it works.** No hitboxes anywhere — damage is a distance check at the strike moment. No club collider exists |
| "on the landing frame, an animation event fires" | **Not used.** EnemyCombat never reads animation events; everything fires from state timing |
| Camera shake, damage, knockback, sounds | **Supported** — `cameraShakeOnHit`, `damage`, `EnemyFX` |
| Charge root motion travel | **Conflict** — enemies move by NavMeshAgent, root motion fights it |
| Ground Pound: reposition above player mid-jump | **Not present** |
| Ground Pound: red landing circle | **Not present** |
| Ground Pound: AoE shockwave | **Not present.** `isAoE` is declared at EnemyCombat:76 and read nowhere in the project |
| Kanabo Sweep: "cannot be rolled, must JUMP it" | **Not present** |
| Phase transition: horn glow, skin darkening, screen grey, music, push-back | **Not wired.** `UpdatePhase()` flips a bool and logs; fires no event |
| Boss bar turns crimson in Phase 2 | Bar works. `BossHealthBarUI.SetPhase2()` exists at line 277 and **has zero callers** |
| Arena pillars break | **Not present**, and the cave has no colliders yet |

---

## 5. Open threads

- **Landing bug still open.** `PlayerMovement.OnLanded()` fails to fire after a Pull interrupts a 4-leg jump, leaving `PlayerState.Jumping` stuck. Diagnosed from logs, never confirmed live. Matters because Ground Pound and the jump-over sweep both use airborne state. `PlayerMovement.cs` is **do-not-touch without approval**.
- **Unbounded loops** in `PlayerCombat.GrabReactionRoutine` phases 1 (line 2039) and 3 (2079) have no timeout. Real bugs, separate from the freeze above.
- **Root motion vs NavMeshAgent.** Apply Root Motion is currently OFF, same as Nopperabo. Revisit for Charge alone after Phase 1 feels right. The precedent for handling it is KomainuBoss disabling the agent during scripted beats.
- **Telegraph splits not done.** All four two-part moves run whole-clip via `skipTelegraph`. Splitting means opening the FBX Animation tab, setting an end frame on the wind-up and adding a second clip range for the strike.
- **Clip length vs state length.** `hitReactDuration` is 0.5s but the hit-react clips are 1.46s and 1.50s; Stagger is 3.75s against a 1.2s state. Clips get cut short. Fix with clip speed, not re-animation.
- **`Oni Hit react lighter light.fbx`** — an 18th file, a copy of the 13-Jul Hit react light (0.79s vs 1.46s), wired in as `HitReact_medium`. Confirm intended.
- **`OniUpdates` folder** still holds duplicate copies of the five clips. Unity imports them twice. Move outside `Assets/` when convenient.
- **Cave has no colliders.** `Assets/Scenes 1/Cave/Environment.prefab` has zero. Walls are decoration; the fight happens on the terrain.
- **Texture optimisation pending.** 53 Oni textures, 4096 source importing at 2048, ~475 MB of source PNG. Suggested: body / horn / hair / kanabo stay 2048; arm shields, waist pads, both clothing sets, sake → 1024; eyes, teeth, tongue, eyebrows → 512. Duplicates safe to remove: `eye l_AlbedoTransparency 1` and ` 2`, `eye l_Emission 1` and ` 2`, `clothing uper leg_ambient_occlusion 1`, `kanabo1_Metallic 1`, `teeth_MetallicSmoothness 1`. Unused: `body_height_base.png`, `kanabo1_thickness.png`.
- **GDD Doc 07f is out of date.** It still lists Swing 3, Grab and Throw, and has no Kanabo Sweep. The animation doc supersedes it. Per that doc's own rule, 07f should be updated first.
- **Yoru prefab in the cave is old.** Scene uses `Assets/Org_Prefabs/yoru_f/Yoru/PlayerYoru_Def.prefab` (3 Aug). Newest is `Assets/Org_Prefabs/PlayerYoru_Def.prefab` (8 Aug). **Safe swap order:** drag the new one in alongside the old → copy the transform → re-point the vcam **Tracking Target** and `mainCamera (1)`'s **ThirdPersonCamera → Player Transform** → deactivate the old one, don't delete → Play-test → only then delete. Deleting first breaks the camera follow.

---

## 6. Reference values — Nopperabo, for comparison

The working enemy this setup is modelled on. `Assets/Enemies/japonese.fbm/Nopperabo_prefab.prefab`, everything on one root GameObject, whole hierarchy layer 10.

- **NavMeshAgent** — radius 0.5, speed 3, accel 8, angular 120, stopping distance 2.5, height 1.7
- **CapsuleCollider** — radius 0.7, height 4, centre y 1.87
- **Animator** — Apply Root Motion **off**, no Avatar assigned on the component
- **EnemyHealth** — maxHealth 100, staggerDamageThreshold 15, deathDelay 2, useAnimations on
- **EnemyCombat** — detectionRange 10, attackRange 3.5, pullRange 6, escapeRange 15, chaseSpeed 5, attackCooldown 2, hasPhases on / 0.5, bossBarName `Nappero-bo`, 3 attacks, 3 combos. All attacks use `telegraphAnim` **blank** with `skipTelegraph` on — she runs whole clips.
- **Controller** — 13 loose states, zero transitions, one Float `AnimSpeed`, one layer.
