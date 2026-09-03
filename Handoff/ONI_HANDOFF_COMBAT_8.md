# ONI HANDOFF — COMBAT 8
**Project:** Yoru · Unity 6000.2.7f2 · Built-in RP · `/Users/asenahazal/Documents/Yoru`
**Covers:** rounds 71–72 and a long lighting investigation of the boss cave
**Written:** 3 Sep 2026 · **Previous:** `Handoff/ONI_HANDOFF_COMBAT_7.md`

---

## 0. HOW TO WORK WITH HAZEL — READ THIS FIRST

Her rules, unchanged from handoff 7, and still not negotiable:

- *"never NEVER assump alwasy ask me dont do thigns on you own"*
- *"tell me what did you understand first than fix when i understand that we are in ssame page"*
- *"i dont want any mistakes so dont do anythng before undersatnding or being sure"*

**Lanes.** She does scene / Animator / FBX / prefab / art. You do `.cs` only.

**Every round:** ask before building · back up to `OniLogs/cs_backup_round<N>/` · verify the compile yourself · read the newest `OniLogs/oni_*.log` yourself · simple English · no per-frame allocations.

**She is not a native English speaker and has said so twice.** Short sentences. One instruction at a time.

---

## 1. THE HARD LESSON OF THIS SESSION — DO NOT REPEAT IT

This session ended badly. She said, accurately, *"you are being lazy i dont think you read my project well"* and then *"you are very much fucked up in this session"*.

**What actually went wrong, precisely:** every fact I read out of her *files* was correct and useful. Every Unity *UI path* I gave her was invented from memory and three of them were wrong. She hit dead end after dead end while I kept producing more pages.

The four failures, in order:

1. Told her to find a scene light in the **Project** window. Scene objects are in the **Hierarchy**.
2. Gave the COZY package path as `com.distantlands.cozy.core`. Unity shows packages by **display name**: `COZY 3: Stylized Weather`.
3. Told her to edit COZY modules on the **Modules** child object. `CozyModuleEditor.OnInspectorGUI()` is an **empty method** — COZY modules deliberately draw nothing there. The real panel is on the **Cozy Weather Sphere** parent.
4. Told her to change "Ambient Light Zenith Color" without naming its section. She landed in **Skydome Settings** (which is the sky) instead of **Lighting Settings** (which is the ambient), and reasonably concluded I was wrong about the colours.

**The rule that comes out of it:**

> **Read the editor script before you name any Unity UI path.** File data and screen paths are two different things. `Assets/Foo/Bar.asset` on disk is not what the Project window shows. A serialized field called `_baselineHeight` is labelled `Base Height` on screen. If you have not read the `[CustomEditor]` for a component, do not tell her where to click.

Second rule: **she asks for one instruction and gets ten paragraphs.** When she says "just tell me what to do", answer in under five lines. She said *"there is so many gibberish intead of what am i gonna do"*. She was right.

---

## 2. CURRENT STATE — CODE

| File | Size | Version |
|---|---|---|
| `Assets/Scripts/Enemy/OniBoss.cs` | 225,695 | **round 67** (68/69/70 still reverted) |
| `Assets/Scripts/Combat/EnemyCombat.cs` | 146,286 | round 53 |
| `Assets/Scripts/Combat/PlayerCombat.cs` | 197,250 | round 49 |
| `Assets/Scripts/Enemy/SwingWaveProjectile.cs` | 12,930 | round 43 |
| `Assets/Scripts/Player/PlayerMovement.cs` | 36,284 | round 42 |
| `Assets/Scripts/Player/PlayerHealth.cs` | 7,826 | round 53 |
| `Assets/Scripts/Camera/CameraGameFeel.cs` | 23,592 | round 61 |
| `Assets/Scripts/Camera/ThirdPersonCamera.cs` | 12,926 | round 64 |
| `Assets/Scripts/Combat/CombatMusicManager.cs` | 12,904 | round 55 |
| `Assets/Scripts/Combat/StormWeather.cs` | 17,881 | round 65 |
| `Assets/Scripts/Enemy/BossHealthBarUI.cs` | 26,054 | round 58 |
| `Assets/Scripts/ArenaClearanceGizmo.cs` | 5,307 | editor gizmo |

**Verified this session:** highest ROUND marker in `OniBoss.cs` is 67, `cineMaxDistance` is absent, so the round-70 camera fix is genuinely not there. Compile clean, zero `error CS`.

**`cs_backup_round68/69/70` are EMPTY folders.** The round 68–70 code does not exist on disk anywhere. If she ever wants the round-70 camera fix back it has to be rewritten, not copied.

### Rounds 71 and 72, shipped then reverted at her request

- `TorchLight.cs` (flicker + distance cull for fire lights)
- `LightRingTool.cs` (editor window, places a ring of props)

Both now sit in `_to_delete/round71_72_light_scripts/`. Backups in `cs_backup_round71/` (README only, nothing was overwritten) and `cs_backup_round72/` (round-71 LightRingTool).

**Loose end:** `Assets/Org_Prefabs/Lights/Brazier.prefab` still references `TorchLight`'s GUID, so it will show a **missing script** on its `Point Light` child until someone removes that component. `Assets/Scripts/FX/` is an empty folder that could not be deleted from the agent side.

---

## 3. THE CAMERA BUG IS STILL LIVE AND STILL MEASURED

Round 67's `StepBackFactor` returns `1.15` every frame for any point beside or behind the lens, and there is no distance ceiling. It compounds.

Measured in her own runs this session:

- `oni_2026-09-02_18-37-29.log`: *"the camera stepped back to 28.2m at the farthest"*
- `oni_2026-09-01_16-45-01.log`: 28.3m

The fix is known and small: crop points beside the lens instead of pushing, bound the step-back, add a hard `cineMaxDistance`. It was round 70, and round 70 is gone.

---

## 4. THE SCENE, MEASURED

### Where the fight actually happens

From **215 distinct player positions** parsed out of `oni_2026-09-02_18-37-29.log`:

| | |
|---|---|
| Fight area | 39.3m wide × 32.1m deep |
| Centre of mass | **(477.9, 432.9)** |
| 50% of time within | 14.0m |
| 80% within | 19.1m |
| 95% within | 22.3m |

**She fights next to the stone stairs, not at `CineStageMark`.** A 14m pool at (482, 415) covers her only 27% of the time. At the centre of mass it covers 50%.

### Positions

- `OniBoss` spawn: **(467.7, 1.0, 438.6)** — she moved him this session, from (481.8, 455.4)
- `PlayerYoru_1.1`: (474.5, 2.0, 427.2)
- `CineStageMark`: (482, 1, 415), with `ArenaClearanceGizmo` on it
- `Environment`: **Y raised from 15.852 to 29.56** this session. X and Z unchanged, so plan-view distances still hold, all Y figures in older handoffs are stale.

### Clearance (pivot based, so treat as indicative)

| Spot | Solids inside 14m | Nearest |
|---|---|---|
| Old Oni spawn (481.8, 455.4) | 8 | 4.5m |
| Fight centre (477.9, 432.9) | 1 | 12.6m (Stairs, pivot 3m below floor) |
| (478, 430) | 0 | 15.5m |
| CineStageMark (482, 415) | 0 | 31.0m |

**(478, 430) is the sweet spot:** 14m camera ring is clear, the stairs sit just outside to the north, and it still holds ~79% of the fight.

### What the cave is made of

226 pieces: 96 temple/castle, 52 foliage, 43 mega rocks, 18 rocks, 9 rubble/roots, 8 statues. There is a processional avenue running north up the line X 482: statues on pillars at Z 457, again at Z 472, stairs, then the castle wall at Z 490. South of the arena is empty for 130m.

**`Environment.prefab` is used only by `CaveScene_Oni_Boss1`** (plus two junk scenes in `_Recovery`). Marking it Static cannot affect any real scene. She cares about this; she said twice not to change things that touch other scenes.

---

## 5. LIGHTING — THE FINDINGS. THIS IS THE VALUABLE PART OF THIS HANDOFF.

Every one of these was read out of her files and is real. None of them depend on my bad UI paths.

### The big one: her fog is 50 metres above her head

`mainCamera (1)` → `VolumetricFogAndMist.VolumetricFog` (Volumetric Fog & Mist by Kronnect):

| Field (screen label) | Serialized | Value | Problem |
|---|---|---|---|
| **Base Height** | `_baselineHeight` | **50** | The fog starts at world Y 50. The floor is Y 0 and she fights at Y 2. **She has never walked through her own fog.** |
| Height | `_height` | 50 | so the fog band is Y 50 to Y 100 |
| **Sun** | `_sun` | **EMPTY** | light scattering has no light to scatter from, so god rays produce nothing |
| Color | `_color` | 0.91, 0.91, 0.91 | near-white, a daytime fog colour |
| Density | `_density` | 0.35 | |
| Light Scattering | `_lightScatteringEnabled` | **1 (on)** | Weight 1.9, Illumination 18, Samples 16. All ready, all doing nothing without a Sun. |

### Her own fog controller is not running in this scene

`VolumetricFogController` on `mainCamera (1)`: `volumetricFog` slot empty, `currentPresetName: None`, and the log prints `WorldStateManager not found!` every single run. It picks its preset from the world state and there is no world state in the boss scene, so it never applies one.

Its Neutral preset holds the **correct** values she wants: `baselineHeight: 0`, `height: 60`, `fogColor: 0.051, 0.071, 0.110` (dark blue), `enableLightScattering: 1`. They have never been applied.

### Post processing: `Assets/Scenes 1/Cave/Cave_Oni_Profile.asset`

| Effect | Value | Verdict |
|---|---|---|
| **Bloom colour** | **0.227, 0.475, 1.0 (blue)** | Every fire in the cave glows blue. Biggest thing fighting the warm look. |
| Bloom threshold | 0.7 | lower it at night so flames catch |
| **Depth of Field** | focusDistance **5m**, f5.6 | **The boss is blurry.** She fights him at 5 to 15m. |
| Post Exposure | 0.7 | free brightness dial, untouched |
| Colour grading | temp -5, sat +20, tonemapper 2 | good for night |
| Vignette | 0.35, dark blue | good for night |
| Ambient Occlusion | 0.5 | keep, does most of the shape work at night |
| Chromatic Aberration | 0.05 | fine |
| Motion Blur | shutter 90 | untested with the phase 2 camera move |

### Lights in the scene

| Light | Type | State |
|---|---|---|
| `Sun Light` | Directional, Mixed | COZY's. Path: `Cozy Weather Sphere > Sky > Sun Offset > Sun Light` |
| `Moon Light` | Directional, Mixed | COZY's. Path: `Cozy Weather Sphere > Sky > Moon Light` |
| `ColdLight_Rim` (child of Oni) | Point, local (0, 7, -6) | **Range 6.5 on a 5m boss.** Barely grazes him. Should be ~14. |
| `YoruRim` (child of Yoru) | Point, local (6.8, 3.5, 16.2) | **Sits 18m from her with Range 12. Lights nothing at all.** |
| `LightShaft` | Spot | **off.** Also carries `VolumetricLightBeamSD` and `VolumetricDustParticles`, both lost when she disabled the object. |
| `Directional Light` | Directional 0.3 | off (correct, it was a second sun) |
| `WarmLight_Rim` / `ColdLight_Rim` | Point, **Range 170** | both off (correct, they were in range of everything everywhere) |

**Quality → Ultra → Pixel Light Count is 4.** Moon + Oni key + Oni rim + Yoru rim already fills it. Should go to 6.

### Baking

- **Only the `Terrain` is Static.** All 226 environment pieces are `m_StaticEditorFlags: 0`.
- She ran a bake on 2 Sep at 23:51. It produced `LightingData.asset` + a **7.6 MB** `Lightmap-0_comp_light.exr` + dir map + shadowmask, about **15 MB total**, in `Assets/Scenes 1/CaveScene_Oni_Boss1/`. Because only the Terrain was static, **it baked the floor and ignored the entire cave.**
- `m_BakeResolution: 40` over a 476m footprint. That must come down to about 2 before anyone bakes the environment, or it will run for hours.
- Baking is viable later (Environment.prefab is scene-exclusive, and her COZY clock is paused so the sun angle never moves), but **not while she is still moving the arena.**

### What she owns (searched all 3,568 prefabs)

- **299 prefabs contain a Light.** Standouts: Ultimate VFX has a **ring of fire** in one prefab, **particle lights**, and three ready-made point light templates (no/soft/hard shadow, Range 10). Epic Toon FX has 8 torch flames, 6 candle lights, 5 tall fires, lightning explosions, and Soul missiles in crimson/orange/purple.
- **Volumetric Light Beam 2.0.0** (SaladGamer), Built-in RP supported, ships cookie textures.
- **Volumetric Fog & Mist** (Kronnect), ships `ShadowMapCopy` for light shafts through fog.
- Props in `gardenJapanese/.../Props/`: Brazier + Moss, Lamp_Ground 01–05, Lamp_Small/Medium/Tall, WoodenLamp, WoodenLantern, all with mossy twins. **None contain a light or a flame.** A working torch is always three parts: prop + fire VFX + light.
- Matching fire: `PF_FireSmall` (4 particle systems), `PF_FireMedium` (5), `PF_FireBig` (5).
- Lantern materials have `_EmissionColor`, so lantern heads can glow for free.
- `LightingController.cs` (988 lines, `Scripts/BalanceSystem/`) has a **dark path light** that follows the player and a **character rim light rig**. It is in `DemoScene_Day` and **not** in the boss scene.

---

## 6. COZY 3 — HOW IT ACTUALLY WORKS (the part I got wrong, now verified in code)

- COZY is an **embedded package** at `Packages/com.distantlands.cozy.core`. Unity's Project window labels it **`COZY 3: Stylized Weather`**, version 3.6.9.
- **Its profiles are shared by every scene and are wiped when COZY updates.** COZY prints this warning itself on the Atmosphere panel. **Always duplicate a profile into `Assets/` before editing it.**
- **Do not select the `Modules` child object.** `CozyModuleEditor.OnInspectorGUI()` is an empty method; modules draw nothing there. This is intended behaviour, not a bug.
- **The real panel is on the `Cozy Weather Sphere` parent**, built by `CozyWeatherEditor.CreateInspectorGUI()`. It shows a banner, a **search box**, +/styles/scene-tools/settings buttons, and a grid of module buttons. Each button shows its current setting underneath. Click a button to open that module.
- Also reachable via `Tools > Cozy: Stylized Weather 3 > Open Cozy Hub`.
- **Inside the Atmosphere panel the sections are separate:** `Skydome Settings` holds **Sky** Zenith/Horizon Color (the sky itself). `Lighting Settings`, further down, holds **Ambient Light** Zenith/Horizon Color and Ambient Light Multiplier. She hit this and I did not name the section.
- Clock: **18:50**, in the Time module. Time profile `Cozy Time Default` has `pauseTime: 1` and `resetTimeOnStart: 0`, so **the clock does not advance during play**.
- Atmosphere profile in use: **`Default Atmosphere`**. Live `ambientLightMultiplier` read 1.2375 off the sphere; the profile itself reads 1.5. **The profile number is the one to trust.**
- Moon: **`Stylized Moon`** satellite, `useLight: 1`.
- Ambient colours are **warm brown** by default: horizon `0.398, 0.258, 0.233`, zenith `0.541, 0.366, 0.335`. Raising the multiplier amplifies that warmth. For a cold night base they need to go blue.
- Weather profiles available include Mostly Cloudy, Overcast, Partly Cloudy, Approaching Storm, Distant Storm, Imminent Storm, Storm Eye, Dense Fog, Electric Fog.
- `StormWeather`: Phase 1 = `Receeding Storm`, Phase 2 = `Thunder Storm`, `driveCozy: 1`.

---

## 7. HER DESIGN DECISIONS THIS SESSION — TREAT AS SETTLED

- **The boss fight is at NIGHT.** Not the midday it was built at.
- **The cave is mixed:** one large open-sky area, plus smaller rock-covered areas. She wants the covered areas **kept very dark**, with only small fire and "twinkle" lights.
- **The player walks the whole area.**
- **The castle interior is parked for later.**
- The look she wants is **"cozy and stormy"**: warm fire close, cold storm sky far.
- **The Oni must never sit in darkness.**
- **The LightShaft is off and staying off.** She did not like the bright disc it put on the floor. She turned it off herself and that was the right instinct for the look she wants.
- She does not want a ring of many braziers. She found them heavy and said the pot reads as an object rather than as light.
- She is doing the scene lighting **herself**, by hand. She asked for guidance, not scripts.

---

## 8. WHERE SHE GOT TO, AND WHAT IS NEXT

She was working through a night pass. Completed:

1. Turned `Directional Light` off (second sun)
2. Turned both Range-170 rim lights off
3. Turned `LightShaft` off
4. Added `TorchLight` to her Brazier prefab, then reverted the whole script
5. Duplicated the atmosphere profile and raised the Ambient Light Multiplier from 1.5

**Her exact feedback after that:** *"it is brighter but maybe too warm maybe colder in open area but warmer on the closed areas"*

**The next move, and where I left her stranded:** COZY's ambient colours are warm brown, so raising the multiplier amplified warmth. They need to go cold blue. The fields are **`Ambient Light Zenith Color`** and **`Ambient Light Horizon Color`**, and they live under **`Lighting Settings`** in the Atmosphere panel, **not** under `Skydome Settings` where she looked. Suggested night values: zenith `#263657`, horizon `#2B3647`. Then the warmth comes only from fires, one per covered space.

Still untouched from the free list: bloom colour, post exposure, fog Base Height, fog Sun slot, phase 1 weather, Depth of Field, Pixel Light Count.

---

## 9. PARKED — CARRIED FORWARD FROM HANDOFF 7

- The agreed staging plan: on the hard cut, move the Oni to `CineStageMark`, snapped to NavMesh. Approved, never built. Note that she has since moved the Oni's spawn, so confirm the plan still stands before building it.
- Permanent lightning club through phase 2.
- Arena shrink / cave collapse in phase 2.
- The round-70 camera fix (backwards fly to 28m). Must be rewritten.
- No `Boss` NavMesh agent type exists. Project still has only `Humanoid`, radius 0.74. The Oni's agent is type 0, radius 1.4, height 6. The 4.4 MB `NavMesh-NavMesh Surface 1.asset` baked on 2 Sep is the default surface rebaked, not a Boss surface.
- Turn off verbose logging (`OniDebugLogFile`, the `[OniBoss:*]` traces) before ship.
- Known non-bugs, ignore in logs: `VolumetricFogController: WorldStateManager not found!`, COZY "Lightning Light" missing script.
- Leftover 15 MB of unreferenced baked lightmaps in `Assets/Scenes 1/CaveScene_Oni_Boss1/`.

---

## 10. IF YOU DO ONE THING NEXT

Fix the fog. It is one field, it costs nothing, and it is the single biggest visual change available in the scene:

**`mainCamera (1)` → Volumetric Fog → `Base Height`: 50 → 0.**

Then drag `Moon Light` into the empty `Sun` slot and set the fog colour to a dark blue. Those three changes turn on an atmosphere system she already owns, already configured, and has never once seen working.

And when you talk to her: **short answers, verified paths, one instruction at a time.**
