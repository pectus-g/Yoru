# ONI HANDOFF - COMBAT 10

**Project:** Yoru, Unity 6000.2.7f2, Built-in RP, Forward rendering, Linear colour space, `/Users/asenahazal/Documents/Yoru` (public repo `pectus-g/Yoru`)
**Covers:** the open-zone lighting and post pass on `CaveScene_Oni_Boss1`, the weather rebuild, the probe bake, and the unresolved "Yoru is flat" problem
**Written:** 7 Sep 2026. **Previous:** `ONI_HANDOFF_COMBAT_9.md` (kept outside the repo).
**Design source of truth in the project:** `Handoff/CAVE_LIGHTING_PLAN.md`.
**This file lives outside the project on purpose. Do not copy it into the repo.**

---

## 0. HOW TO WORK WITH HAZEL. READ THIS FIRST.

Her rules, unchanged and all still in force:

- Never assume. Ask if you are not sure.
- Every Unity setting comes with its exact location, every time: window or Hierarchy object, then component, then section or foldout, then field label, then the value.
- No backup folders, no README files, no "save first" warnings. The project is on GitHub and she reverts there.
- No clutter in the project. No permanent menu items for one-off passes. Edit files directly when it is safe. When the editor API is unavoidable, one menu item that deletes its own script file after running.
- She places and tunes by hand. You own `.cs` files and measurement.
- One step at a time, complete scripts only, plain English, short sentences, no flattery.
- No em-dashes anywhere. Not in chat, not in files, not in code comments.
- `PlayerMovement.cs`: DO NOT TOUCH.

**Read before you speak.** Every wrong statement in this session came from not reading far enough. Section 1 lists mine so you do not repeat them.

**Verification you do yourself.** Compile: `Library/ScriptAssemblies/Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` mtimes newer than your edit, and zero `error CS` under `Library/Bee`. Runtime: newest `OniLogs/oni_*.log` (columns: frame, real seconds, time scale, KIND, message). Bake: mtimes under `Assets/Scenes 1/CaveScene_Oni_Boss1/`.

**Reading the scene file.** `Assets/Scenes 1/CaveScene_Oni_Boss1.unity` is text YAML, about 6.8 MB. Split on `--- !u!`. Class numbers: 1 GameObject, 4 Transform, 20 Camera, 104 RenderSettings, 108 Light, 114 MonoBehaviour, 137 SkinnedMeshRenderer, 157 LightmapSettings, 215 ReflectionProbe, 218 Terrain, 220 LightProbeGroup, 223 Canvas, 224 RectTransform, 1001 PrefabInstance. Colours are 0 to 1 floats, the inspector shows them times 255. Light `m_RenderMode`: 0 Auto, 1 Important, 2 Not Important. Light `m_Lightmapping`: 1 Mixed, 2 Baked, 4 Realtime.

---

## 1. MISTAKES I MADE THIS SESSION. DO NOT REPEAT THEM.

1. **I read `m_IsActive` on `model:Cat_BODY` and never walked up to its parent.** `bodyYoru` is `m_IsActive: 0`. The real rendering Yoru is `PlayerYoru_1.1 > Cat_All_10_Tails_v4 > model:Cat_BODY`. Handoff 9 warns about exactly this in writing and I did it anyway. Consequence: I had her tick Forward Add Compatibility on the dead body, and I edited `Assets/YORU/Fur_Mat.mat` which only the dead body uses.
2. **I called Depth of Field "on" from its `enabled: value: 1` line without checking `active: 0` above it.** DoF was never running. An entire diagnosis paragraph was wrong.
3. **I told her Lightmap Resolution was 2.** `m_Resolution` is Indirect Resolution (realtime GI, off). The baked one is `m_BakeResolution`, which was already 40.
4. **I told her to set the four fills to Baked, in the same round where the bake did not run.** Baked lights emit nothing at runtime, so that round deleted four lights from the scene and put nothing back. She got darker and flatter and I did not catch it until the round after.
5. **I said no point light had ever touched her fur.** Too strong. XFur's shell shader is a surface shader whose ForwardBase pass does read SH, light probes and up to four per-vertex lights. What it does not get without Forward Add Compatibility is per-pixel additional lights.
6. **I told her to change Ambient Intensity Multiplier in the Lighting window.** With Ambient Mode = Gradient that field does not exist in the UI. She found and changed Environment Reflections Intensity Multiplier instead. The stored `m_AmbientIntensity: 1.21` is inert in Gradient mode.

---

## 2. WHAT WAS FOUND AND FIXED THIS SESSION

All verified in the files after the change.

### 2.1 `VolumetricFogController` was overwriting her hand-tuned fog every Play

`Assets/Scripts/BalanceSystem/VolumetricFogController.cs` lives on `mainCamera (1)` and on `Start()` wrote its Neutral preset onto the Kronnect Volumetric Fog by reflection: density, height, baseline, **fog colour**, alpha, noise, sky haze, sky alpha, scattering. Its Neutral preset was Fog Color 13 18 28, Density 0.45, Sky Haze 5, Sky Alpha 0.6. Every inspector value she set on the Kronnect component was discarded at runtime.

**Fix shipped:** added a serialized `applyPresets` bool (header `=== SCENE OWNERSHIP ===`, label "Apply Presets", default true so other scenes are unaffected) plus guards in `Start`, `Update` and `OnRingsChanged`. When off the component disables itself at Start and logs one line. It is now **off** in this scene. Log confirms: `[VolumetricFogController] applyPresets is OFF in this scene.`

### 2.2 Auto Exposure was inverted and could only darken

`Cave_Oni_Profile` Auto Exposure was Filtering 64.98 / 95, Minimum +0.2, Maximum +0.6, Compensation 1.1. exposure = Compensation / clamp(avg luminance, 2^Min, 2^Max), so the multiplier could only sit between 0.73 and 0.96. Filtering at 64.98 also threw away the darkest 65 percent before measuring, so it only ever measured bright pixels and concluded the scene was bright.

**Now:** Filtering 40 / 85, Minimum -0.5, Maximum +0.2, Compensation 1.1, Progressive, Speed Up 1.5, Speed Down 0.6.

Note: Filtering is a MinMaxSlider with no numeric fields. It cannot be typed. It was written directly into the asset YAML.

### 2.3 The storm carried snow

Phase 2 used COZY's package `Thunder Storm`, whose precipitation is the Multi FX `Storm Precipitation`, which contains **both** rain and snow: Light Rain SFX 2, Heavy Rainfall, Thunderstorm Particle FX, Raining Event, **Thundersnow Particle FX, Heavy Snowfall, Snowing Event**. With no climate driving temperature the snow fired alongside the rain. Those were the white squares.

**Fix shipped:** two new weather profiles in `Assets/Scenes 1/Cave/`, hers, not COZY package assets:

- `Yoru_Oni_Phase1_Storm` (from Imminent Storm): High Nimbus Clouds, Imminent Storm Filter, Wet Weather, Blustery Wind, **Light Rainfall, Light Rain Particle FX, Light Rain Atmosphere SFX, Intermittent Thunder, Raining Event**. Imminent Storm had no precipitation at all, so phase 1 previously had zero rain.
- `Yoru_Oni_Phase2_Storm` (from Thunder Storm): Overcast Clouds, Heavy Storm Filter, Fog Light, **Tempestuous Wind, Torrential Thunder, Heavy Rainfall, Heavy Rain Particle FX, Thunderstorm Particle FX, Rain and Thunder SFX**, Raining Event. All snow removed.

Both are assigned on `StormWeather`. She likes the result.

Ruled out while investigating: the `Heavy Storm Filter` in the phase 2 profile has saturation -0.5 and a near-black sunFilter, but it only writes COZY's own sky shader globals and COZY's `Moon Light`. Sky Style, Fog Style and Handle Scene Lighting are all off and `Moon Light` is at intensity 0, so it does not touch the picture.

### 2.4 The floor was showing a lightmap from 2 September

`Terrain` is the only Contribute GI object in the scene and `LightingData.asset` was dated 2 Sep, baked under a completely different lighting setup. So for weeks the floor displayed baked light from an old scene while every dynamic object showed current realtime light. That is why the floor was the brightest thing in every screenshot and why the characters never sat right against it.

**Fixed by baking.** See section 3.

### 2.5 XFur fur profiles had light probes explicitly disabled

This is the big one and it is described in section 4.

---

## 3. CURRENT SCENE STATE, VERIFIED

Scene saved 7 Sep 17:48. Bake at 17:28.

**Lights**

| Object | Intensity | Range | Mode | Render Mode | Notes |
|---|---|---|---|---|---|
| `Directional Light` (the moon) | 1.5 | - | **Realtime** | Auto | colour 176 224 246, soft shadows **Strength 0.85**, cookie `MoonCloudCookie_Soft`, Cookie Size 60 |
| `MoonPool_1` | 3 | 16 | **Baked** | Not Important | (501, 14, 430) |
| `MoonPool_2` | 3 | 16 | **Baked** | Important (moot, baked) | (468, 14, 430) |
| `MoonPool_3` | 3 | 16 | **Baked** | Not Important | (484, 14, 402) |
| `AuroraSpill` | 3 | 20 | **Baked** | Not Important | (481, 16, 447) |
| `YoruRim` | 1.6 | 7 | Realtime | Auto | child of PlayerYoru |
| `ONI_KEY` | 1.6 | 14 | Realtime | Important | child of OniBoss |
| `ColdLight_Rim` | 2.06 | 14 | Realtime | Auto | child of OniBoss |

**Ambient** (Lighting window, Environment, Source Gradient): Sky **62 80 122**, Equator **58 60 72**, Ground **28 19 13**. `m_AmbientIntensity: 1.21` is stored but inert in Gradient mode. Environment Reflections Intensity Multiplier 0.8.

**Kronnect Volumetric Fog** on `mainCamera (1)`, component 7 of 9 in the inspector:
Albedo 60 80 115, Deep Obscurance 0.4, Density 0.35, Start Distance 20, Height 60, Sky Haze Height 80, Sky Haze Alpha 0.35, **Sun Shadows on, Strength 0.7**, Light Scattering on with **Shafts Intensity 0.06, Start Illumination 5**, Downsampling 1 (full res), Compute Depth off.

`mainCamera (1)` component order, top to bottom: Transform, Camera, Cinemachine Brain, **Post Process Layer**, Third Person Camera, Flare Layer, **Volumetric Fog** (Kronnect), Volumetric Fog Pos T, **Volumetric Fog Controller** (ours).

**Post processing.** One scene volume `PostProcess_`, global, priority 0, profile `Cave_Oni_Profile`. Post Process Layer anti-aliasing = **SMAA**. Effects actually running in the base profile: Ambient Occlusion (intensity 0.55, radius 0.45, colour 15 12 40), Auto Exposure, Vignette (0.32), Bloom (**intensity 1.4, threshold 1.15, diffusion 6**, colour 255 235 214), Color Grading (temperature -8, saturation 12, post exposure 1.1, **contrast 25, lift W 0, gain W 0.10**), Chromatic Aberration 0.04, Motion Blur 60 / 6. **Depth of Field is `active: 0`, it is NOT running.**

`Cave_Oni_Storm` (phase 2 differences, blended in by code) still has post exposure 0.95, contrast 22, saturation -12, colour filter 214 227 255, vignette 0.48, bloom 2.6, aberration 0.14, grain 0.12. **This still darkens phase 2 and Hazel has explicitly said phase 2 must not be darker. Not yet rewritten.**

**StormWeather** on the `StormWeather` root object: Phase 1 Weather `Yoru_Oni_Phase1_Storm`, Phase 2 Weather `Yoru_Oni_Phase2_Storm`, Pre Fight Weather `Clear`, Storm Fog Density 0.45, Storm Strike Interval 1.5 to 4.

**Baking.**
- Lighting Settings Asset: `Assets/Scenes 1/Cave/CaveOni_lighting.lighting`. Baked Lightmaps on, Progressive GPU, Lightmap Resolution 40, Max Size 1024, Ambient Occlusion **on**, Bounces 2, Light Probe Sample Multiplier 4, Lighting Mode Shadowmask.
- `Environment` and its children: **Reflection Probe Static only**, 369 prefab entries plus 43 loose rocks. Deliberately **not** Contribute GI, to avoid lightmapping a 300 m cave.
- `Terrain`: static flags 2147483647, so it is the only Contribute GI object.
- `RP_Arena` reflection probe: position (481, 10, 425), Box Size (75, 40, 80), Baked, Resolution 128, Box Projection off.
- `ArenaLightProbes`: LightProbeGroup at (482, 0, 415), roughly 400 probes. Inner grid 64 x 72 m at 8 m spacing over three heights, outer grid 170 x 160 m at 20 m spacing over two heights, all raycast onto the real floor.
- Bake output at 17:28: `LightingData.asset` 290,185 bytes (was 17,985), contains `LightProbes`, `m_BakedCoefficients`, `ProbeSetTetrahedralization`. `Lightmap-0_comp_light.exr` 8.1 MB. `ReflectionProbe-0.exr` and `ReflectionProbe-1.exr` present.

**Arena geometry.** `CineStageMark` at (482, 1, 415) carries `ArenaClearanceGizmo`: clearRadius 14, cameraRadius 20, **arenaRadius 25**, clearHeight 15.

**Health bar.** The root object is still literally named `GameObject`. It has RectTransform, Canvas (Screen Space Overlay) and `BossHealthBarUI`. It works, logs `[BossBar] BossHealthBarUI initialized`. No Canvas Scaler, so the bar is fixed pixel size at any resolution.

---

## 4. THE UNRESOLVED PROBLEM: YORU READS AS A FLAT SILHOUETTE

This is the one thing that did not get fixed and it has eaten most of the session.

### 4.1 Which object is actually Yoru

**`PlayerYoru_1.1 > Cat_All_10_Tails_v4 > model:Cat_BODY`** is the real one. SkinnedMeshRenderer enabled, 7 material slots, mesh from `Assets/YORU/Cat_All_10_Tails_v4.fbx`, 461 bones, root bone `root`.

`PlayerYoru_1.1 > bodyYoru > model:Cat_BODY` is on a parent with **`m_IsActive: 0`**. It has 17 material slots including `Assets/YORU/Fur_Mat.mat`, `Nose.mat`, `Eye.mat`, `Hair.mat`, `Teeth.mat`.

**Hazel has said explicitly: do not delete `bodyYoru`.** Migrating animation events and references off it is risky and it costs nothing at runtime because it is disabled. Leave it alone.

Consequence for anyone reading old notes: **`Fur_Mat.mat` is not Yoru's material.** The active body's fur material is slot 0 of the FBX, `{fileID: 6330045795069212188, guid: 5c8e94834d7cbc14a85cb0d3b2e62d3f}`. `Assets/YORU/Fur_Texture.png` is `Fur_Mat`'s texture, so any conclusion drawn from it applies to the dead body.

### 4.2 How XFur lights the fur, from their source

`Assets/PIDI/XFur Studio 4/Legacy RP/XF_XFShells.shader` is a surface shader:

```
#pragma surface XFShellSurfacePass Standard fullforwardshadows vertex:XFShellVert addshadow
```

Unity generates ForwardBase from that, which reads the main directional light, SH ambient and light probes, and up to four per-vertex point lights.

XFur does **not** render the shells through the SkinnedMeshRenderer. It draws them itself with `Graphics.DrawMesh` / `Graphics.DrawMeshInstanced` / `RenderMeshInstanced`, and it passes its own probe flag. `XFurStudioInstance.cs` line 1233, and again at 1260 and 1265:

```csharp
rp.lightProbeUsage = FurDataProfiles[i].useProbes
    ? LightProbeUsage.BlendProbes
    : LightProbeUsage.Off;
```

Because XFur's normal instanced draw only issues the base pass, **additional per-pixel lights never reach the fur** unless `builtinFwdCompatibilityMode` ("Forward Add Compatibility") is on. Their own tooltip:

> "Enables the Forward Add setup (additional pixel lights, point lights support) to the XFShells method when using Forward Rendering. This disables GPU instancing, making the shader considerably slower. Consider using Deferred rendering or URP instead."

**Turning that on breaks the fur on this project.** Hazel tested it: the body fur stopped rendering entirely and came back when she turned it off. It is currently **off** on both bodies and must stay off until someone works out why it breaks.

### 4.3 What was found and changed

Every fur profile in the scene had **`useProbes: 0`**, so all fur was drawn with `LightProbeUsage.Off` and could not see a single baked probe. 7 profiles on the active body, 17 on the dead body, and 42 across the 22 tail-ring objects.

A one-shot editor script set `useProbes = true` on all 66 and deleted itself. Verified in the saved scene: **66 of 66 are now on.** Do not call `XFurStudioInstance.UpdateFurMaterials()` from an editor script: it indexes `_furBlocks`, which only exists after XFur initialises, and throws `IndexOutOfRangeException` in edit mode. `useProbes` is read at draw time straight from the profile, so no material refresh is needed.

`selfOcclusionStrength` on the active body is now 0.45 on profile 0 and 0.65 on the other six.

### 4.4 The result, and the current hypothesis

**The fur now renders.** Visible fringe on her forearms and tail in the latest screenshots, which was not there before.

**She got darker, not lighter.** Near black.

That is the diagnostic. With `LightProbeUsage.Off` a renderer gets the flat ambient probe. With `BlendProbes` it gets the interpolated baked probe at its position. She went darker, so **the baked probes at floor level are darker than the ambient.**

**Current hypothesis, not yet proven:** the probes are close to empty because the only light in the bake is the gradient ambient plus four point lights at intensity 3 with range 16 (roughly 0.09 attenuation at the fight centre), minus the baked AO that was switched on for this bake. **The moon is Realtime and realtime lights contribute nothing to a bake, so the brightest light in the scene by a wide margin is absent from every probe.**

### 4.5 The next diagnostic step, and it is cheap

Select **`ArenaLightProbes`** in the Hierarchy. The Scene view draws each probe as a sphere shaded with its actual baked value.

- **If the spheres are near-black:** the hypothesis holds. Get real light into the bake. The obvious lever is setting `Directional Light` to **Mixed** instead of Realtime, so its direct contribution is baked into the probes while shadows stay realtime, then re-bake. Raising the four fills further and moving them closer to (482, 415) is the second lever.
- **If the spheres are bright:** the hypothesis is wrong and the break is between the probe and the shell shader. Next things to look at there: whether `Graphics.DrawMesh` with a MaterialPropertyBlock is receiving the probe SH at all in this XFur version, and whether `receiveShadows: true` on 40 shells is self-shadowing the fur into blackness.

### 4.6 Other XFur observations worth keeping

- **Rim lighting exists per profile and is already on.** Active body profile 0: Rim Lighting Tint white, Strength 1, Power 1. It is not visibly doing anything, which is itself a clue. Section `Rim Lighting` in the Designer's `Properties` panel.
- **Fur Main Tint differs between the two bodies:** 195 215 235 on the active one, 255 255 255 on the dead one. Only matters if `bodyYoru` is ever re-enabled.
- Active body profile 0: Rendering Samples 40, Fur Strands Tiling 13, Fur Length 0.4, Fur Thickness 0.2, roughness 0.9, metallic 0.05, Double Sided on, Curly Fur on, Emissive Fur off, `useNormalmap` off at instance level.
- **Where the settings actually live.** Select the object, Inspector, `XFur Studio Instance`, button row under the header: `Settings` / `Built-in Modules` / `Fur Designer`. Click `Fur Designer`, pick the material in the `Active Fur Material` dropdown, click `Enter Edit Mode`. A separate window opens titled `XFur Studio Designer`. In that window there is a **Tools** box of 48x48 icon buttons with captions. The `Settings` icon shows Main Fur Settings, Basic Fur Appearance, **Fur Lighting** (Use Light Probes, Cast Shadows, Receive Shadows), Additional Features. The `Properties` icon shows Common Properties (including **Fur Occlusion / Shadowing**), Color Variation, Emissive Fur, Curly Fur, **Rim Lighting**, Per Instance Wind Settings.
- XFur's own warning, shown in its UI: painting and styling changes must be exported via the Export/Load button in the Designer, because they write texture maps. Plain slider values are serialized on the component and a normal scene save is enough.

---

## 5. WET FUR IN THE RAIN. MECHANISM FOUND, NOT WIRED.

Hazel tried this before and it failed. Here is why, from the source.

`Assets/PIDI/XFur Studio 4/Source Code/Utilities/XFurWeatherManager.cs` is a `MonoBehaviour` with `[Range(0,1)] public float RainIntensity` and `SnowIntensity`, plus wind fields. Its `Start()` does:

```csharp
Core.XFurStudioInstance.WeatherManager = this;
```

The fur VFX module reads that static and applies its `RainFX` preset.

Three conditions must all hold and right now none do:

1. An `XFurWeatherManager` component must exist somewhere in the scene. **There is none.**
2. The **VFX & Weather** module must be enabled on Yoru's XFur instance. `_vfxModule._enabled` is **0** on both bodies. Turn it on via Inspector, `XFur Studio Instance`, `Built-in Modules`.
3. Something must drive `RainIntensity`. **Nothing does.**

Yoru's `RainFX` preset is already sensible: colour white, specular 0.45 0.5 0.6, penetration 0.75, smoothness 0.75, intensity 1.25, fadeout 4 s, `ignoreWeatherComponent` false so it will listen.

**Planned work:** add a weather manager reference to `StormWeather` and ramp `RainIntensity` with the phases to match the existing floor wetness gate (`calmMaxWetness` 0.35 in phase 1, 1.0 in phase 2, 0 before the fight).

---

## 6. AUDIO. LARGELY DEAD CODE IN THIS SCENE.

- **`CombatMusicManager` is not in the scene.** `CombatManagers` has CombatFeedbackManager, CombatPostProcessPulse and CombatSFXManager only. `OniBoss` has a fully written music arc (`phase1Music`, `transitionMusic`, `phase2Music`, `roarSFX`, crossfades, phase 2 dropping exactly on the pound slam) and every call is guarded by `CombatMusicManager.Instance != null`. That is null here, so the whole arc is inert and does not even log.
- **There are no music tracks in the project.** 106 audio files, all SFX from Epic Toon FX, mixkit and freesound.
- **Every slot on `CombatSFXManager` is empty.** All 19: swings, impacts, dodge, parry, guard, player hits, enemy vocals, heavy charge.
- **There is no AudioMixer asset.** No groups, no ducking, no master bus.

---

## 7. WHAT HAZEL WANTS, IN HER WORDS

- Bright, readable, mysterious, Ori-like. Deep shadows against strong light, rim light everywhere, warm orange inside cold grey.
- **Three states.** No fight: quiet cave, open sky, aurora, no fight music, lights steady, no pulse. Phase 1: he engages, fight music starts, lights start pulsing on the beat. Phase 2: music faster, lights faster and harder.
- **Phase 2 must NOT be darker than phase 1.** Aggression reads as speed and movement. This overrides everything in earlier handoffs.
- **Disco lights.** Colour shift is approved for phase 2, overriding the old "beat-synced pulse only, no colour cycling" rule.
- **Beat source: spectrum reactive**, reading the actual playing track, not typed BPM fields. Chosen because the music does not exist yet.
- Two zones. The **open fight area first**, which is what this session covered. The fully closed rock areas later, and she will do those herself.
- Braziers and warm point lights are parked. She will place them once the global lighting and post are settled.
- Rain in both phases, no snow.
- Probe baking was parked as "the last thing" and has now been done.

---

## 8. NEXT WORK, IN ORDER

**A. Finish the Yoru diagnosis.** Section 4.5. Look at the probe spheres, then either get the moon into the bake or chase the shell shader. Nothing else should start until she can see form on her character.

**B. Round B, the three states.** A new `Cave_Oni_Phase1` profile as the brightest and most saturated state. Rewrite `Cave_Oni_Storm` so phase 2 raises post exposure instead of lowering it and gets its character from cold desaturation, vignette, grain, rain and lightning contrast. A spectrum-reactive beat script.

Design note for the beat script: Yoru's fur reads **ambient**, **light probes** and the **main directional**, not realtime point lights. So the pulse must drive `RenderSettings.ambientLight` and the moon to reach her, plus realtime accent lights to reach the environment. Coordinate with `OniBoss`, which already writes `RenderSettings.ambientLight` during the phase 2 cinematic sky pulse.

**C. Wet fur.** Section 5.

**D. Warm anchors.** Braziers back in the arena, her lane, her placement.

**E. Audio.** `CombatMusicManager` back onto `CombatManagers`, an AudioMixer with Master, Music, SFX and Ambient groups, a composer brief for the three-piece arc, and placeholder SFX into the 19 empty slots so the fight is not silent.

**F. Closed zone.** A local Post Process Volume with a blend distance, her lane, no code needed.

---

## 9. KNOWN NON-BUGS, IGNORE THEM

- XFur NullReference, COZY missing scripts, PathTool Material null, Komainu "No attacks defined", `PlayerYoru_Def` DEACTIVATED on Play stop.
- One `NullReferenceException` in COZY `ParticleFX.InitializeEffect` line 53 at phase 2, first frame only. Fix planned: call `InitializeEffect(CozyWeather.instance)` on each FX of the phase profiles at Start.
- `"The referenced script on this Behaviour (Game Object 'Lightning Light') is missing!"` (guid 474bcb49853aa07438625e644c072ee6). URP additional light data on COZY's `Thunder And Lightning.prefab`, irrelevant in Built-in RP.
