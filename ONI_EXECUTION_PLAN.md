# Oni (Boss 1) — Execution Plan

Project: `/Users/asenahazal/Documents/Yoru` · branch `Gamefeel` · 11 Aug 2026
Companion to `YORU_Oni_Animation_Order_and_Roles.docx` and GDD Doc 07f.

---

## Already done

- **Avatars** — `Oni Animations 2/ONI_Base_01` set to Generic + Create From This Model, **Root node = Hips**. All 17 clips set to Copy From Other Avatar. Old `Oni Animations/ONI_Base_01` set to No Avatar to remove the duplicate.
- **Clip set** — `Oni Animations 2` is canonical. The five 17-Jul revisions from `OniUpdates` (Charge, Club Slam, Club Swing 2, Hit react Heavy, Stagger) were copied in and are the versions in use. Verified bone-compatible: 336 nodes, zero mismatches.
- **Materials** — 14 external `.mat` remapped onto `ONI_Base_01` from `Assets/Enemies/ONI/texture/Materials/`. Normal maps now active.
- **Loop Time** — set on Idle, Walk, Run, Watch.
- **ONICONTROLLER** — 17 states, no transitions, one Float parameter `AnimSpeed`.
- **Oni in scene** — built in `CaveScene_Oni_Boss1` with CapsuleCollider (r 1.4, h 6.2), NavMeshAgent, Animator, EnemyCombat, EnemyHealth, EnemyFX.

---

## PART 1 — Fix EnemyCombat (blocking, do first)

### 1a. The four attack entries — critical

Unity fills new array elements with zeros and ignores the script's defaults, so these are all wrong right now. On **every one of the four attacks**:

| Field | Currently | Set to | Why |
|---|---|---|---|
| `telegraphSpeed` | 0 | **1** | 0 = animation frozen |
| `attackSpeed` | 0 | **1** | **0 freezes him mid-swing — he never recovers** |
| `skipTelegraph` | off | **on** | `telegraphAnim` is blank; without this he enters Telegraph with no clip |
| `interruptsCombo` | off | **on** | Otherwise Yoru doesn't flinch when hit |

### 1b. Other fields

| Field | Currently | Set to |
|---|---|---|
| `idleAnim` | `idle` | **`Idle`** (capital I — must match the Animator state exactly) |
| `pullRange` | 6 | **0** |
| `closeAttackName` | CloseStrike | **blank** |
| `chaseSpeed` | 3 | **4.5** |
| `staggerDuration` | 1 | **1.2** |
| `bossBarName` | blank | **Oni** |
| `hasPhases` | off | **on** (threshold 0.5) — only if testing Phase 2 |
| Combo "ClubSwing1 → ClubSwing2 → ClubSlam" weight | 0 | **40** (0 = never picked) |

### 1c. NavMeshAgent

| Field | Currently | Set to |
|---|---|---|
| Radius | 0.5 | **~1.4** (matches his capsule) |
| Height | 2 | **~6** |
| Stopping Distance | 0 | **~2.8** (at 0 he walks into Yoru) |

Note: the scene's NavMesh bake uses agent radius 0.5. Widening the agent past that will make him clip corners once the cave has real walls. Fine for a flat terrain test.

### 1d. Then

- Confirm `Idle` is the **default (orange)** state in ONICONTROLLER.
- Confirm the Oni GameObject and all children are on layer **Enemy (10)**.
- **Bake the NavMesh.** The cave has none — he cannot take a step without it.
- Press Play. Read the Console.

Verified state names (copy exactly — `run` and `Club_swing2` really are lowercase):
`Idle` `Walk` `run` `Oni_Alert` `Watch` `Hit_react_light` `HitReact_medium` `HitReact_Heavy` `Stagger` `Club_Swing` `Club_swing2` `Club_Slam` `Oni_Charge` `Phase_Transition` `Ground_Pound` `KanaboSweep` `Death`

---

## PART 2 — Fix the washed-out lighting

### Diagnosis

| | Cave now | DemoScene_BlueNight |
|---|---|---|
| Fog | **off** | on, exp², blue-teal, density 0.001 |
| Skybox | **none assigned** | `Assets/Skyboxes MegaPack 2/3/3.mat` |
| Ambient source | Skybox — but no skybox exists | **Gradient**, intensity 1.21 |
| Directional | **intensity 2.0**, warm white | intensity **0.3**, pale blue |
| Other lights | none | 2 points — cold blue 2.06, warm orange 1.0 |
| Post-processing | **none in scene** | PPv2 volume |

One bright flat directional with no ambient shaping, no fog and no grading. That is the washout — it is not the Oni's materials.

### Steps

1. **Window → Rendering → Lighting → Environment**
   - Source → **Gradient**
   - Sky `0.162, 0.208, 0.366` · Equator `0.12, 0.16, 0.22` · Ground `0.047, 0.043, 0.035`
   - Intensity **0.6**
2. **Directional Light** — intensity **2 → 0.25**, colour cool blue-white, keep soft shadows.
3. **Fog** — on. Exponential Squared, density **~0.03**, near-black with a blue tint.
4. **Add two lights for shape:**
   - warm point at arena level, `0.91, 0.59, 0.24`, intensity ~2.5, range 15
   - cold point behind the fight, `0.40, 0.45, 1.0`, intensity ~2, range 20
5. **Post-processing** (project uses Post Processing Stack v2, `com.unity.postprocessing 3.5.1`)
   - Duplicate `Assets/Scenes 1/DemoScene_Nightt_Profiles/Post-Processing Volume Profile.asset`, rename for the cave
   - Empty GameObject → **Post-process Volume**, Is Global ✓, assign the copy
   - Add **Post-process Layer** to `mainCamera (1)`

> ⚠️ `mainCamera (1)` is a prefab instance shared with DemoScene_Day. Add the Post-process Layer as a **scene-only override — do NOT click "Apply to Prefab."** Same trap as the camera Tracking Target.

---

## PART 3 — Weather: rain, lightning, wet fur

Everything needed is already in the project.

### Wet fur on Yoru — confirmed working path

- **`XFurWeatherManager`** — `Assets/PIDI/XFur Studio 4/Source Code/Utilities/XFurWeatherManager.cs`
  Sliders: `RainIntensity` (0–1), `SnowIntensity` (0–1), `WindFrequency`, `WindStrength`, `RainDirection`, `SnowDirection`, wind influence.
- Yoru's prefab carries **24 `XFurStudioInstance`** components, so she will respond to it.
- Reference scenes that already do this: `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Tiger Demo.unity`, `Emissive Fur.unity`, `Basic Features - XF4.unity`
- Demo particle prefab: `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Rain.prefab`

**Steps:** open Tiger Demo first to see it working → add one empty GameObject to the cave scene with `XFurWeatherManager` → raise `RainIntensity` in Play mode and watch Yoru.

### Rain and lightning visuals — Mirza Beig Ultimate VFX

`Assets/Mirza Beig/Particle Systems/Ultimate VFX/Prefabs/`

- Rain: `Loop/pf_vfx-ult_demo_psys_loop_rainStorm.prefab`, `rainStorm2`, `rainSimple`
- Lightning / thunder: `Loop/pf_vfx-ult_demo_psys_loop_thundershock.prefab`, `Oneshot/pf_vfx-ult_demo_psys_oneshot_thundershock.prefab`, `lightningField`, `lightningAttack2`, `lightningAttack3`
- Snow (if wanted later): `snowstorm`, `snowstorm2`

**Order:** rain particles first, then XFurWeatherManager to match, then lightning as timed one-shots. Lightning should also punch the directional light intensity for a frame or two — that's what sells it.

> **Open question:** the fight is in a cave. Rain and lightning need sky. Does the arena have an open roof or a cave mouth, or does the fight move outdoors? This changes where the emitters go.

---

## Reference — architecture decisions

- **`EnemyCombat` is shared and stays shared.** `OniBoss.cs` will be a separate script layered on top, exactly like `KomainuBoss.cs:28`:
  ```csharp
  [RequireComponent(typeof(EnemyCombat))]
  [RequireComponent(typeof(EnemyHealth))]
  public class KomainuBoss : MonoBehaviour
  ```
  Komainu switches EnemyCombat off during scripted beats, drives the Animator itself, then hands back with `enabled = true` + `ResetCombatState()` + `BecomeHostile()`.
- **Phase 1 combat needs no new code.** Code is only required for: phase-2 cinematic, ground pound AoE + landing circle, kanabo sweep jump-only avoidance, arena destruction.
- **Contract a new enemy must satisfy:** layer `Enemy` (10); `EnemyHealth` and `EnemyCombat` on the same GameObject as the collider; an Animator; non-empty `bossBarName` for the boss bar.
- **Do not add `EnemyDeathEffect`** — it hardcodes `animator.Play("die")`, which clashes with the death state naming.

---

## Known gaps and open threads

- **Landing bug still open** — `PlayerMovement.OnLanded()` fails to fire after a Pull interrupts a 4-leg jump, leaving `PlayerState.Jumping` stuck. Unconfirmed. Relevant because Ground Pound and the jump-over sweep both use airborne state.
- **Root motion vs NavMeshAgent** — Charge and Ground Pound bake travel into the clip, but enemies move via NavMeshAgent. Apply Root Motion is currently OFF (same as Nopperabo). Revisit for Charge only, after Phase 1 feels right.
- **Telegraph splits not done** — Club Slam, Charge, Ground Pound and Kanabo Sweep are each one clip. Running whole-clip for now via `skipTelegraph`.
- **Cave has no colliders** — `Assets/Scenes 1/Cave/Environment.prefab` has zero. Walls are decoration; the fight happens on the terrain.
- **Texture optimisation pending** — 53 Oni textures, 4096 source importing at 2048, ~475 MB of source PNG. Suggested: body/horn/hair/kanabo stay 2048; arm shields, waist pads, both clothing sets, sake → 1024; eyes, teeth, tongue, eyebrows → 512. Duplicates to remove: `eye l_AlbedoTransparency 1` and ` 2`, `eye l_Emission 1` and ` 2`, `clothing uper leg_ambient_occlusion 1`, `kanabo1_Metallic 1`, `teeth_MetallicSmoothness 1`.
- **`OniUpdates` folder still holds duplicate copies** of the five clips — Unity imports them twice. Move outside `Assets/` when convenient.
- **GDD Doc 07f is out of date** — it still lists Swing 3, Grab and Throw, and has no Kanabo Sweep. The animation doc supersedes it; 07f should be updated to match.
- **`Oni Hit react lighter light.fbx`** — an 18th file, a copy of the 13-Jul Hit react light (0.79s vs 1.46s). Wired into the Animator as `HitReact_medium`. Confirm this is intended.
- **`hitReactDuration` is 0.5s** but the hit-react clips are 1.46s and 1.50s, and Stagger is 3.75s against a 1.2s state. Clips get cut short. Fix with clip speed, not re-animation.
