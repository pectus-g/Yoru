# Oni Arena — Handoff 2: Lighting, Weather & Post-Processing

Project: `/Users/asenahazal/Documents/Yoru` · branch `Gamefeel` · 11 Aug 2026
Scene: `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`
Reference scene: `Assets/Scenes 1/DemoScene_BlueNight.unity`

> Companion file: `ONI_HANDOFF_COMBAT.md` covers the Oni's animation and combat setup.

**Arena shape:** enclosed cave with an **open roof**. Rain falls through the opening, lightning is visible through it, and light shafts come down through it. That drives every placement decision below.

---

## 1. Why everything looks washed out

Measured from the scene files, not guessed:

| | Cave now | DemoScene_BlueNight |
|---|---|---|
| Fog | **off** | on, Exponential Squared, blue-teal `0.169, 0.404, 0.577`, density 0.001 |
| Skybox material | **none assigned** | `Assets/Skyboxes MegaPack 2/3/3.mat` |
| Ambient source | **Skybox — but no skybox exists** | **Gradient**, intensity 1.21 |
| Ambient colours | set, but ignored in Skybox mode | Sky `0.162, 0.208, 0.366` · Equator `0.320, 0.540, 0.698` · Ground `0.180, 0.180, 0.306` |
| Directional light | **intensity 2.0**, warm white `1, 0.835, 0.712`, soft shadows | intensity **0.3**, pale blue `0.694, 0.876, 0.969`, soft shadows |
| Other lights | **none** | 2 point lights — cold blue intensity 2.06, warm orange intensity 1.0 |
| Post-processing | **none in the scene** | PPv2 volume |
| Sun assigned | no | yes |

One bright flat directional, no ambient shaping, no fog depth, no grading. **It is not the Oni's materials** — those were fixed and their normal maps are working.

---

## 2. Lighting fix — step by step

### 2a. Environment

**Window → Rendering → Lighting → Environment**

- Environment Lighting **Source → Gradient** (currently Skybox, with no skybox — that's the flat grey fill)
- Sky `0.162, 0.208, 0.366`
- Equator `0.12, 0.16, 0.22` — deliberately darker than BlueNight's, since a cave shouldn't take open-sky bounce
- Ground `0.047, 0.043, 0.035` — keep the existing dark ground
- Intensity **0.6** (BlueNight uses 1.21 for open night)

Optional: assign `Assets/Skyboxes MegaPack 2/3/3.mat` as the Skybox Material so the open roof shows sky rather than flat colour. With Gradient ambient it won't affect lighting — only what you see through the hole.

### 2b. Directional light

- Intensity **2.0 → 0.25**
- Colour cool blue-white
- Keep soft shadows
- Angle it to come **through the roof opening** so it reads as moonlight falling into the cave, not a sun

### 2c. Fog

- Turn **on**
- Mode **Exponential Squared**
- Density **~0.03** (BlueNight uses 0.001 because it's outdoors — a cave wants far more)
- Colour near-black with a blue tint

### 2d. Two lights for shape

One directional will always look flat, whatever you do to its intensity. Add:

- **Warm point**, arena level, colour `0.91, 0.59, 0.24`, intensity ~2.5, range 15 — firelight/braziers, gives the Oni's red skin something to bounce
- **Cold point**, behind the fight, colour `0.40, 0.45, 1.0`, intensity ~2, range 20 — rim light so his silhouette separates from the cave wall

### 2e. Light shafts through the roof

The project already has **VolumetricLightBeam** (`Assets/VolumetricLightBeam/`, including `Resources/DustParticles.prefab`) and **VolumetricFog** (`FogVolume.prefab`, `FogBoxArea.prefab`, `DynamicFogVolume.prefab`).

A beam aimed down through the roof opening, plus dust particles, is the single highest-impact thing for this arena. It also gives the rain something to catch light on.

### 2f. Post-processing

Project uses **Post Processing Stack v2** (`com.unity.postprocessing 3.5.1`).

1. Duplicate `Assets/Scenes 1/DemoScene_Nightt_Profiles/Post-Processing Volume Profile.asset` — this is the profile BlueNight actually uses. Rename the copy for the cave.
2. Create an empty GameObject → add **Post-process Volume** → **Is Global** ✓ → assign the copy.
3. Add a **Post-process Layer** to `mainCamera (1)`.

> ⚠️ **`mainCamera (1)` is a prefab instance shared with DemoScene_Day.** Add the Post-process Layer as a **scene-only override — do NOT click "Apply to Prefab."** Otherwise you change the Day scene too. Same trap as the camera Tracking Target fix.

---

## 3. Weather — rain, wet fur, lightning

Everything needed is already in the project. Nothing to buy or import.

### 3a. Wet fur on Yoru — confirmed path

- **`XFurWeatherManager`** — `Assets/PIDI/XFur Studio 4/Source Code/Utilities/XFurWeatherManager.cs`
  Public sliders: `RainIntensity` (0–1), `SnowIntensity` (0–1), `WindFrequency` (0–32), `WindStrength` (0–2), `RainDirection`, `SnowDirection`, `RainWindInfluence`, `SnowWindInfluence`.
- **Yoru's prefab carries 24 `XFurStudioInstance` components**, so she responds to it. Verified on both `Org_Prefabs/PlayerYoru_Def.prefab` and `Org_Prefabs/yoru_f/Yoru/PlayerYoru_Def.prefab`.
- **Reference scenes that already do this** — open one first to see it working before wiring your own:
  - `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Tiger Demo.unity`
  - `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Basic Features - XF4.unity`
  - `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Emissive Fur.unity`
- Demo particle prefab: `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Rain.prefab`

**Steps:** open Tiger Demo → see how `XFurWeatherManager` is set up there → add one empty GameObject to the cave scene with the same component → raise `RainIntensity` while in Play mode and watch Yoru's fur darken and clump.

Note: the Oni does **not** use XFur — his hair is bone-driven mesh. Rain will wet Yoru but not him. If that reads oddly, a wetness tint on his body material is the cheap fix.

### 3b. Rain and lightning visuals — Mirza Beig Ultimate VFX

`Assets/Mirza Beig/Particle Systems/Ultimate VFX/Prefabs/`

| Effect | Prefab |
|---|---|
| Rain, heavy | `Loop/pf_vfx-ult_demo_psys_loop_rainStorm.prefab` |
| Rain, alt | `Loop/pf_vfx-ult_demo_psys_loop_rainStorm2.prefab` |
| Rain, light | `Loop/pf_vfx-ult_demo_psys_loop_rainSimple.prefab` |
| Thunder, looping | `Loop/pf_vfx-ult_demo_psys_loop_thundershock.prefab` |
| Thunder, one-shot | `Oneshot/pf_vfx-ult_demo_psys_oneshot_thundershock.prefab` |
| Lightning field | `Loop/pf_vfx-ult_demo_psys_loop_lightningField.prefab` |
| Lightning bolts | `Loop/pf_vfx-ult_demo_psys_loop_lightningAttack2.prefab`, `lightningAttack3` |
| Snow, if wanted later | `Loop/pf_vfx-ult_demo_psys_loop_snowstorm.prefab`, `snowstorm2` |

### 3c. Suggested build order

1. **Rain emitter** above the roof opening, sized to cover the arena, angled slightly so it isn't a dead-vertical curtain. Start with `rainSimple`, move to `rainStorm` once placement is right.
2. **`XFurWeatherManager`** — match `RainIntensity` to the particle density by eye, and point `RainDirection` the same way the particles fall.
3. **Wind** — `WindStrength` around 0.3–0.6 with `RainWindInfluence` up gives the rain and Yoru's fur the same drift. This is what makes it read as one weather system rather than two effects.
4. **Lightning** — one-shot `thundershock` fired on a randomised timer, positioned above the roof opening.
5. **The flash is the important part.** On each strike, spike the directional light intensity from 0.25 to roughly 2.5 for two or three frames and drop it back. A lightning particle without a light punch looks like a sticker. This needs a small script — trivial, but it is code.
6. **Ground wetness** — if the terrain material has smoothness, raising it during rain sells it more than more particles.

### 3d. Tie it to the fight

The GDD's phase-2 transition already calls for the screen going grey, heat haze and ground shake. Weather is a free amplifier: raise `RainIntensity`, increase wind, and shorten the gap between lightning strikes when Phase 2 begins. That would live in `OniBoss.cs` alongside the horn glow.

---

## 4. Existing systems worth knowing about

The project has a preset-driven environment layer under `Assets/Scripts/BalanceSystem/`, built for the light/dark world-state system:

- **`LightingController.cs`** — presets for sun colour, moon, ambient colour and intensity, ambient mode, shadows
- **`PostProcessController.cs`** — presets for colour grading, bloom, vignette, chromatic aberration
- **`VolumetricFogController.cs`** — fog geometry, colour, noise, sky haze, and **light scattering / god rays**
- **`AmbienceController.cs`** — spawns particle prefabs per state plus an audio profile
- `Worldstatemanager.cs`, `SkyboxDayNightSwitcher.cs`, `RotateSkybox.cs`, `CombatPostProcessPulse.cs`, `LightPathFXController.cs`

**The cave scene does not currently use any of them** — `YORU_LightingController.prefab` is not in it. So hand-set lighting will not be overridden at Play. That's convenient now, but it also means the cave is disconnected from the world-state lighting the rest of the game uses.

Two paths, pick one deliberately:

- **Hand-light the cave** — simple, isolated, no risk to other scenes. Right for getting the boss fight looking good fast.
- **Wire it into the BalanceSystem controllers** — consistent with the rest of the game and gets the light/dark path shifts for free, but more setup, and be aware those controllers **overwrite manual lighting at runtime**.

Recommendation: hand-light first, get the fight looking right, then decide whether the Oni arena should respond to world state at all. A boss arena arguably shouldn't.

---

## 5. Open questions

- Should the arena react to the **light/dark balance** system, or stay fixed?
- Is rain **always on** in this arena, or does it start at the phase-2 transition for drama?
- Does the roof opening need a **visible skybox**, or is a dark void above it enough?
- Terrain material — does it have a smoothness channel we can drive for wet ground?
