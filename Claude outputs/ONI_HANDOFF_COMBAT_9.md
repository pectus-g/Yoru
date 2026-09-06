# ONI HANDOFF - COMBAT 9
**Project:** Yoru, Unity 6000.2.7f2, Built-in RP, `/Users/asenahazal/Documents/Yoru` (public repo `pectus-g/Yoru`)
**Covers:** the night and storm lighting pass on `CaveScene_Oni_Boss1` (rounds 73 to 77), the lantern pipeline, post processing, and the health bar incident
**Written:** 6 Sep 2026. **Previous:** `Handoff/ONI_HANDOFF_COMBAT_8.md` in the project. **Design source of truth in the project:** `Handoff/CAVE_LIGHTING_PLAN.md` (section 8 there is regenerated from the scene file after every step). This handoff lives outside the project on purpose. Do not copy it into the repo.

---

## 0. HOW TO WORK WITH HAZEL. READ THIS FIRST.

Her rules, in her words, all still in force:

- "never ever do assumptions always ask me if you are not sure"
- "you must tell me where these settings are exactly every time" (Hierarchy or Project, object, component, section, field name, value)
- "dont worry about saves i use github": no backup folders, no README files, no save warnings
- "lots of noise ... find a clearer way": no permanent menu items for one-off passes. Edit files directly when it is safe. If an editor action is unavoidable, one menu item that deletes itself after running.
- "from now on i am tweaking myself": she places and tunes by hand. You own `.cs` files and anything that changes over time.
- one step at a time, complete scripts only, simple English, short sentences, no flattery
- no em-dashes anywhere. Not in chat, not in files, not in code comments. Use a comma, a colon, or a full stop.
- nothing extra in the project: no handoff copies, no tool folders, no notes. She said so on 6 Sep after I put this file and a `Handoff/tools/` folder in the repo. Both were deleted. The plan file is the one document she wants there.

**Lanes.** She does scene, prefabs, FBX, art, and all inspector tuning. You do code and measurement. After each of her steps you re-read the scene file from disk and confirm the values before proposing the next step.

**Read before you speak.** Every wrong statement this session came from not reading far enough:
- "zero fire lights in the scene": wrong, the Braziers are prefab instances whose names are stored as overrides. Search `PrefabInstance` blocks too.
- "Yoru_Dim_light Point Light is live at intensity 20": wrong, its parent object is inactive. Walk the whole parent chain and check `m_IsActive` on every ancestor. She said "YOU ARE WRONG READ BETTER I DONT TRUST YOU READ AGAIN STOP BEING LAZY".
- "Wrap Mode in the cookie importer": wrong, that field is hidden for the Cookie texture type. Read the editor script before naming a field.
- "the lantern is solid": wrong, it has real openings.
Verify from the file, then answer.

**Verification you do yourself.** Compile: `Library/ScriptAssemblies/Assembly-CSharp.dll` mtime newer than your edit and zero `error CS` under `Library/Bee`. Runtime: newest `OniLogs/oni_*.log` (columns: frame, real seconds, time scale, KIND, message). Syntax before shipping: tree-sitter in the cloud, or at least a bracket count.

**Reading the scene file.** `Assets/Scenes 1/CaveScene_Oni_Boss1.unity` is text YAML. Split on `--- !u!`, the number after `!u!` is the class (1 GameObject, 4 Transform, 224 RectTransform, 108 Light, 114 MonoBehaviour, 223 Canvas, 1001 PrefabInstance, `stripped` blocks are prefab children). A Light's active state is `m_Enabled` on the Light AND `m_IsActive` on every GameObject up the `m_Father` chain. Prefab instances store their name and edits as `m_Modifications` (propertyPath / value). Colours are stored as 0 to 1 floats, the inspector shows them times 255 (the project is Linear colour space, the stored value is what the inspector shows). Light `m_RenderMode`: 0 Auto, 1 Important, 2 Not Important. Write a small Python reader for this on her machine with `device_bash`, keep it in your own scratch folder, never in the repo.

---

## 1. SETTLED DECISIONS (do not reopen)

- COZY 3 (v3.6.9, embedded package `Packages/com.distantlands.cozy.core`, shown in Unity as `COZY 3: Stylized Weather`) is weather only. On `Cozy Weather Sphere`: Sky Style **off**, Fog Style **off**, Handle Scene Lighting **off**. COZY never writes ambient or sun now. `Moon Light` stays at intensity 0 and its object stays active (COZY needs it alive). `Sun Light` component off.
- Her own `Directional Light` is the moon: colour 176 224 246, intensity 1.0, soft shadows strength 0.6, cookie `Assets/Org_Prefabs/Lights/Cookies/MoonCloudCookie_Soft.png`, Cookie Size 60, rotation 55 / 20 / 0 (she set both on 6 Sep).
- Ambient comes from the Lighting window, Trilight: Sky 61 73 110, Equator 92 100 121, Ground 66 46 34. Reflections: Skybox. Skybox `Skyboxes MegaPack 2/3/3.mat` (the aurora, she likes it). Camera clear flags Skybox.
- Kronnect Volumetric Fog and Mist on `mainCamera (1)` owns the fog. Sun slot = `Directional Light`.
- Post Processing Stack v2: one scene volume `PostProcess_` (global, priority 0, `Cave_Oni_Profile`). Storm differences live in `Cave_Oni_Storm` and are blended in by code on a runtime volume (priority 10). Strike flash on a second runtime volume (priority 11). Post layer mask is layer 9 `Post_Processing`. The hit pulse volume (priority 100) and the hallucination (101) sit on top.
- Three fight states: pre-fight = normal quiet cave, Clear weather, aurora visible. Phase 1 = "exciting and eerie, anime fight music", storm rolls in when he engages. Phase 2 = "faster tempo, dark, storm chaotic", Thunder Storm, lightning.
- Music-driven lights: **beat-synced pulse only**, no colour cycling. BPM fields per phase because the Oni track does not exist yet (composer outsourced). Suggested 125 to 135 for phase 1 and 160 to 175 for phase 2, she has not confirmed.
- The look: cold from above (moon, aurora), neutral from the side (reddish rock keeps its colour), warm from below and from every fire. Covered pockets very dark with lanterns only. Open area readable at all times. Reference: Ori (rim light and light rays everywhere, warm orange inside cold grey, deep shadows against strong light).
- Escalation must read bright and colourful (phase 1) to dark and chaotic (phase 2). Both phases dark reads as dark to darker, which is nothing.

---

## 2. CODE SHIPPED THIS SESSION

**`Assets/Scripts/Combat/StormWeather.cs`** (rounds 73 and 77, compiled, log-verified)
- Slots: `preFightWeather` (Clear), `phase1Weather` (still **Receeding Storm**, should be Imminent Storm, her step), `phase2Weather` (Thunder Storm), `stormProfile` (`Cave_Oni_Storm`), `phase1PostWeight` 0.4, `postFadeSeconds` 0.8, `strikeFlashExposure` 0.5, `strikeFlashChromatic` 0.2, `strikeFlashSeconds` 0.12, `calmFogDensity` 0.45, `stormFogDensity` 0.7, `transitionDuration` 2.5, `calmMaxWetness` 0.35, strike origin (481, 45, 447) spread 10, flash peak 2.5.
- Flow: Start sets COZY to `preFightWeather` instantly. Update polls `oniCombat.GetCurrentState()`; Alert, Chase, Telegraph or Attack means the fight started (same rule as the OniBoss music). `OnFightStarted` snaps `phase1Weather`, starts the lightning loop, fades the storm post volume to 0.4, the floor starts to soak. Phase 2 (`IsPhase2()`, released on the lightning-on-the-club beat) sets `phase2Weather`, ramps fog 0.45 to 0.7 over 2.5 s, storm post to 1.0, strikes every 3 to 8 s, each strike fires a screen flash (`PostFlash`, ColorGrading post exposure 3 and chromatic aberration on the flash volume, weight shaped by the strike). Log lines to expect: "post volumes ready on layer 9 (storm priority 10, flash priority 11)", "COZY -> 'Clear' (pre-fight)", "FIGHT STARTED (he engaged)", "PHASE 2 - storm breaking".
- Wetness is gated: 0 before the fight, `calmMaxWetness` in phase 1, 1 in phase 2. OnDisable zeroes the runtime volume weights, OnDestroy destroys the runtime flash profile.

**`Assets/Scripts/FX/TorchLight.cs`** (round 76, meta guid `5aa856393fba34c62b6e145dc7ed0a03`, which also repaired the Brazier prefab's missing script). Layered flicker: jitter 0.10 at 11 Hz, swell 0.16 at 1.4 Hz, gulp (depth 0.30, flare 0.22, 0.45 s, every 0.7 to 2.6 s, piecewise SmoothStep envelope: fast fall, climb past base, settle), colour rides brightness (dim goes ember, bright goes yellow, `colorShift` 0.6), limits 0.45 to 1.45, sway 0.03 at 2.2, distance cull (defaults 35 / 10, the lantern prefab overrides 100 / 25 so lanterns flicker from far away). `Gust()` is public, `BaseIntensity` property. Runtime only. The earlier "follow the fire particles" version was mathematically flat and was replaced.

**Profiles** (edited directly in the YAML, she has since tuned by hand)
- `Assets/Scenes 1/Cave/Cave_Oni_Profile.asset`: the look. Current: temperature -8, tint 0, hue 0, saturation 12, post exposure 1.1, contrast 15, lift W +0.04, gain W +0.25, bloom 2 / threshold 0.7 / soft knee 0.7 / diffusion 8.5 / colour 255 235 214, AO 0.3 radius 0.45 colour 15 12 40, vignette 0.32, aberration 0.04, motion blur 60 / 6, **Auto Exposure on** with inverted values (see section 4).
- `Assets/Scenes 1/Cave/Cave_Oni_Storm.asset` (guid `6599bf70161d4065960e884087553b62`): only the phase-2 differences. Temperature -18, saturation -12, post exposure 0.95, contrast 22, colour filter 214 227 255, vignette 0.48 smoothness 0.55, bloom 2.6, aberration 0.14, grain 0.12 size 1.2.
- PPv2 effect script guids if you ever edit profile YAML again: AutoExposure b3f6f3f7c722b4544b97e3c75840aa33, Grain d65e486e4de6e5448a8fbb43dc8756a0, Bloom 48a79b01ea5641d4aa6daa2e23605641, ColorGrading adb84e30e02715445aeb9959894e3b4d, Vignette 40b924e2dad56384a8df2a1e111bb675, ChromaticAberration 6050e2d5de785ce4d931e4dbdbf2d755, AmbientOcclusion c1cb7e9e120078f43bce4f0b1be547a7, MotionBlur b94fcd11afffcb142908bfcb1e261fba.

**Cookies**: `MoonCloudCookie.png` and `MoonCloudCookie_Soft.png` in `Assets/Org_Prefabs/Lights/Cookies/` (tileable FFT noise, mean about 0.7, Texture Type Cookie, Light Type Directional).

**Lantern**: `Assets/Org_Prefabs/Lights/Japanese_lantern/JapeneseLantern.prefab`. Root is empty (rotation 0, scale 1), mesh on the `LanternMesh` child (rotation X 90), `Point Light` (8.04, range 8, colour 255 110 0, TorchLight on), `Lantern Light` (2, range 7), nested `PF_FireSmall`. The Meshy FBX was decimated in place (1,905,549 to 19,999 tris, 15,337 verts, 2.44 MB, mesh named `mesh_node`), importer Scale Factor 0.01, Bake Axis Conversion on. `PF_FireSmall` child systems use Scaling Mode Local, so the parent scale does not shrink the fire (set Hierarchy or scale the children). Material `JAPLAN` Standard, no emission mask, GPU instancing off. No collider yet.

**Eyes**: `Assets/YORU/Eye.mat` emission about 1.2 (was 8). `eyeDarkrealm` and `EyeLightRealm` are the world-state variants.

**Rocks**: `The Naked Dev/Cave Temple - Modular Pack/Prefabs/URP/Rocks/MegaRockB.prefab` and `MegaRockC.prefab` are clean (about 10k tris, convex mesh collider). The `Environment` prefab instance carries rotation (26.6, -37.8, -9.2) and scale 3, so every child rock inherits that tilt; move rocks with Global handles, or reparent them to the scene root, or edit in Prefab Mode.

`ProjectSettings/QualitySettings.asset`: Ultra Pixel Light Count 6.

Deleted this session at her request: `Assets/Editor/YoruLightingPassTools.cs`, `YoruLanternTools.cs`, backup folders, READMEs. Her own `Assets/Editor/YoruCaveBossTools.cs` (items 1 to 3 under "Yoru Tools") stays.

---

## 3. THE HEALTH BAR INCIDENT (6 Sep) AND WHERE IT STANDS

**What happened.** The root object `BossHealthBar` (Canvas + Canvas Scaler + Graphic Raycaster + `BossHealthBarUI`, layer UI) was deleted from the scene in the same edit that added the `Rock*_big_low` and `MegaRockC (n)` objects. Proven from git: present in commit `100bf4f0` (17:14 local), gone in `29c2d289` (17:22). Logs agree: `[BossBar] BossHealthBarUI initialized` is in the 16:25 and 16:46 logs and missing from 17:21. The scene never referenced the `HUDCanvas` prefab (that lives only in `DemoScene_Day`), so nothing else provides the bar. All of the deleted object's values were the script defaults.

**Her first rebuild (17:49 local) is incomplete.** She added `Boss Health Bar UI` to an empty object named `GameObject` (root, layer Default, plain Transform, no Canvas). The script runs, the 17:50 log says `[BossBar] Showing bar for: Oni (480/500)` and `boss bar shown`, but UI images cannot draw without a Canvas above them, so nothing appears. Fix given to her: select that `GameObject` in the Hierarchy, Inspector, Add Component, `Canvas` (Unity swaps in a RectTransform), leave Render Mode Screen Space - Overlay, rename to `BossHealthBar`. Nothing else. Confirm in the next log that the bar is visible in her screenshot, not only in the log.

`EnemyCombat` shows the bar on Alert (`bossBarName`), `OniBoss` drives it (show on any hostile state, crimson at phase 2, `HideInstant("cinematic")` during the phase-2 cinematic, hide on disengage) through the `BossHealthBarUI.Instance` singleton.

**Same edit, also removed by her:** `Brazier (1)` (was at 490, 0.12, 447), the one `JapeneseLantern` inside the arena (was at 474.5, 0.18, 425.2), and the whole `Environment (BackUp)` duplicate (good for performance). Ask whether the brazier and lantern removals were intended before planning warm light in the arena.

---

## 4. WHY THE SCENE READS DARK (verified against the files, not guessed)

**Auto Exposure is inverted.** In `Cave_Oni_Profile`: Filtering 65 / 95, Minimum (EV) +0.2, Maximum (EV) +0.6, Exposure Compensation 1.1, Type Progressive, Speed Up 1.5, Speed Down 0.6. The maths (from `AutoExposure.compute` and `AutoExposure.cs`): exposure = Compensation / clamp(average luminance, 2^Minimum, 2^Maximum). With Minimum +0.2 and Maximum +0.6 the multiplier can only sit between 0.73 and 0.96. It can only darken. Fix: Filtering 40 / 85, Minimum -1.0, Maximum +0.2 (multiplier between 0.96 and 2.2, so it lifts dark frames and never crushes bright ones). Tuning rule for her: maximum lift = Compensation / 2^Minimum (Minimum -0.5 gives 1.56x, -1.0 gives 2.2x, -1.5 gives 3.1x). Inspector labels are exactly "Filtering (%)", "Minimum (EV)", "Maximum (EV)", "Exposure Compensation", "Type".

**The fog is dark paint.** Kronnect on `mainCamera (1)`: Albedo 30 42 61, Density 0.35, Start Distance 20, Height 60, Deep Obscurance 1, Light Intensity 0, Copy Sun Color on, Compute Depth off, Sun Shadows off, Dithering off, Light Scattering on (weight 1.2, exposure 0.03). Read from `VolumetricFog.cs` (`ComputeLightColor`, `UpdateMaterialFogColor`) and `VolumetricFog.cginc`: the rendered fog colour is Albedo times the sun slot's colour times (sun intensity + Light Intensity), and Deep Obscurance multiplies it down toward the floor. So it is a dark navy that covers everything past 20 m, darkest where the fight is. In phase 2 the code doubles the density to 0.7 and the far half of the arena goes navy. Fix: Fog Colors section, Albedo about 60 80 115, Deep Obscurance 0.4; and `Storm Fog Density` on `StormWeather` 0.5 instead of 0.7. Then it reads as moonlit mist instead of darkness. Inspector labels (from `VolumetricFogInspector.cs`): General Settings: Sun, Compute Depth (+ scope, layer mask), Render Before Transp. Fog Geometry: Density, Noise Strength, Sparse, Start Distance, Height, FallOff. Fog Colors: Alpha, Albedo, Deep Obscurance, Light Intensity, Light Color, Copy Sun Color. Optimization Settings: Downsampling, Dithering.

**Phase 2 stacks four darkeners.** Storm profile at weight 1 (post exposure 0.95, contrast 22, saturation -12, colour filter 214 227 255, vignette 0.48), plus the fog doubling, plus rain, plus the inverted auto exposure. Fix in `Cave_Oni_Storm`, Color Grading: post exposure 1.05, contrast 18, colour filter 225 235 255. The storm should come from cold desaturation, vignette, grain, rain and lightning contrast, not from lowering exposure.

**Still open from the earlier list** (her lane): the four fills (`MoonPool_1..3`, `AuroraSpill`) are Render Mode Auto, should be Not Important (they steal pixel light slots from the fires and the moon; Pixel Light Count is 6). `Phase 1 Weather` on `StormWeather` is Receeding Storm, should be Imminent Storm (guid `5f1173877e77b3b40af72776ce4ae64a`). Kronnect: Compute Depth on with scope Tree Billboards And Transparent Objects and layer Default (so XFur fur is not painted over), Sun Shadows on, Dithering 0.4.

Harmless log noise to expect at phase 2: one `NullReferenceException` in COZY `ParticleFX.InitializeEffect` line 53 (COZY bug: `PlayEffect` calls `InitializeEffect(weatherSphere)` with a null sphere on the first frame for an FX profile that was not in the weather module's start list; the base call sets `weatherSphere`, so it works from the second frame; fix in the next StormWeather round by calling `InitializeEffect(CozyWeather.instance)` on each FX of the phase profiles at Start), and "The referenced script on this Behaviour (Game Object 'Lightning Light') is missing!" (guid `474bcb49853aa07438625e644c072ee6`, URP's additional light data on COZY's `Thunder And Lightning.prefab`, irrelevant in Built-in RP).

---

## 5. CURRENT SCENE STATE (scene saved 6 Sep 21:49 UTC)

Full table in `Handoff/CAVE_LIGHTING_PLAN.md` section 8. Summary: 11 Light components, 16 `JapeneseLantern` instances under `Lanterns` (all 79 m or more from the fight centre, none in the arena), 1 `Brazier` at (477, 0.12, 447). Fight centre (477.9, 432.9), opening about (481, 447). `MoonPool_1..3` intensity 1.0 range 34 colour 170 200 255 at (478 / 468 / 488, 14, 433 / 440 / 426), Auto. `AuroraSpill` 0.8 range 34 colour 120 190 230 at (481, 16, 447), Auto. `YoruRim` 1.6 range 7 colour 180 209 255 at local (0, 2.2, 0). `ONI_KEY` 1.6 / 14 colour 255 201 138 Important, `ColdLight_Rim` 2.06 / 14 colour 102 115 255. `Yoru_Dim_light` is inactive (its Point Light at 20 does nothing). COZY `Moon Light` 0, `Sun Light` off. Camera HDR on, far 5000. Only Terrain is static; the 7.6 MB lightmap from 2 Sep is stale.

---

## 6. NEXT: ROUND 78 (your lane, after her fixes above are in and she has sent one screenshot per phase)

Add to `StormWeather`: a `Moon Light` slot (her `Directional Light`) with per-phase intensities, revised after her "phase 2 too dark" feedback: 1.0 pre-fight, 0.8 phase 1, 0.6 phase 2 (not 0.6 / 0.35 as the plan first said). `Clear Sky Lights` list (`AuroraSpill`) fading to 0 on engage. Ambient multipliers x0.85 phase 1, x0.65 and bluer phase 2, read from the hand-set Lighting values at Start so hand tuning stays the source of truth (note `OniBoss` writes `RenderSettings.ambientLight` during the phase-2 cinematic sky pulse, coordinate with it). A `Phase 1 Profile` slot with a new `Cave_Oni_Phase1` (brighter, more saturated, cyan and violet accents) so phase 1 is the most colourful state. A beat pulse script: BPM per phase, pulses the moon pools, aurora spill and the Oni rim, never the moon, amplitude small (about 15 percent), with a phase-2 double-time option. Pre-initialise COZY FX to remove the one-frame exception. Possibly phase-driven eye emission.

Ask her first: BPM per phase (or "you pick"), and whether the arena should get its warm lights back (a brazier pair flanking the stairs at Z about 460 was the plan).

---

## 7. PARKED (carried from handoff 8, untouched this session)

The camera bug described in handoff 8 section 3. The stale lightmap. The lantern still needs a collider, GPU instancing on `JAPLAN`, and a NavMeshObstacle if the Oni can path through it. Death behaviour of the storm (what the weather does when he dies) is undecided.
