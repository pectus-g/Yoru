# ONI HANDOFF 11 - Yoru fur and Oni arena lighting

Scene: `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`
Unity 6000.2.7f2, Built-in Render Pipeline, Forward, Linear colour space.
Written after a long lighting and fur pass. The fur is NOT finished. Read section 6 first if you only read one thing.

---

## 1. WHAT I GOT WRONG IN THIS SESSION

Read this before trusting anything else in the document.

1. **I said the fur was "done". It is not.** There is visible dark speckling across the coat and the colour is wrong. I called it finished after fixing the lighting terms and stopped looking. That was the biggest error and it cost real time.

2. **I read `m_IsActive` on `model:Cat_BODY` and never walked up to its parent.** There are two `model:Cat_BODY` objects. `bodyYoru` (the parent of one of them) has `m_IsActive: 0`. The real rendering Yoru is `PlayerYoru_1.1 > Cat_All_10_Tails_v4 > model:Cat_BODY`. Several edits went to the dead branch and did nothing.

3. **I called Depth of Field "on"** from its `enabled: value: 1` line without checking the `active: 0` above it. A whole diagnosis paragraph was wrong.

4. **I said Lightmap Resolution was 2.** `m_Resolution` is Indirect Resolution for realtime GI, which is off. The baked one is `m_BakeResolution`, already 40.

5. **I told her to set four fill lights to Baked in a round where the bake did not run.** Baked lights emit nothing at runtime, so that round deleted four lights and put nothing back. She got darker and flatter and I did not catch it until the round after.

6. **I claimed the fur data map's green channel was near zero.** It averages 0.728 inside the UV islands. I over-read the magenta preview.

7. **I told her to change Ambient Intensity Multiplier in the Lighting window.** With Ambient Mode = Gradient that field does not exist.

8. **I nearly blamed the fur data map's green channel for the speckling.** I checked XFur's own shipped tiger demo map before saying it and the tiger has the same overlapping brush blobs with the same 4.4x spread. Not the cause. Check the shipped demo before blaming an asset.

9. **I recommended `Fur Occlusion / Shadowing` 0.6.** That value feeds three separate things in the shader at once and it turned her into a black silhouette. 0.3 is the landing spot.

**Rule for whoever picks this up: verify against `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Tiger/Tiger Demo.unity` before claiming any XFur value is wrong. It is a working reference inside the same project on the same pipeline.**

---

## 2. HOW XFUR ACTUALLY LIGHTS, AND THE TWO DEAD GLOBALS

This is the part that was genuinely broken and is now fixed. Keep it.

The fur is a shell shader, `Assets/PIDI/XFur Studio 4/Legacy RP/XF_XFShells.shader`:

```
#pragma surface XFShellSurfacePass Standard fullforwardshadows vertex:XFShellVert addshadow
```

Maths lives in `Assets/PIDI/XFur Studio 4/Legacy RP/CGIncludes/XFurStudio_StandardPasses.cginc`.
The function that runs is `XFShellSurfacePass`, starting line 260. `BasicShellSurfacePass` at line 419 is a different, unused variant. Do not read line numbers above 419 and think they apply.

**`builtinFwdCompatibilityMode` is 0 (Forward Add off) and must stay off. Turning it on breaks the fur visually.**

Consequence, and this drives everything: **Yoru's fur can only be lit by the main directional light and by light probes. No point light in the scene can ever light her fur per-pixel.** ForwardBase gives one per-pixel directional plus SH plus at best four per-vertex point lights.

### The two globals nobody was setting

**`_XFurMainStandardLightColor`.** Set by a component XFur ships called `XFurLightManager` (`Source Code/Utilities/XFurLightManager.cs`, guid `918957877063e9441a5c615e48f61c06`). **That component was not in any scene or prefab in the project.** Grep the guid to confirm. So the global sat at black, and line 392:

```
specFinal *= _XFurMainStandardLightColor * NdotL * occlusion;
```

meant the entire anisotropic strand specular was multiplied by black. It had never drawn once.

**`_XFurSelfTransmission`.** Declared in `CGIncludes/XFurStudio_Core.cginc` line 141. **Nothing in the whole XFur package ever writes to it.** Used in exactly one place, line 411:

```
o.Emission = tex2D(_XFurEmissionMap, ...) * _XFurEmissionColor
           + saturate( pow(half4(0.75,0.6,0.4,1),2.5)
                     * pow(rim,5)
                     * max(pow(occlusion,2),0.05)
                     * saturate(dot(o.Normal,_XFurMainStandardLightDir))
                     * _XFurMainStandardLightColor
                     * _XFurSelfTransmission * 2 );
```

That is the warm backlight that reads as the Ori rim. Hardcoded warm tint, tight `pow(rim,5)` silhouette, gated to the side facing away from the key light. Multiplied by an unset uniform, so zero. XFur left it unfinished. Note line 523 has the same expression WITHOUT the transmission multiplier, in the unused `BasicShellSurfacePass`, which is how you can tell it was meant to work.

### The fix that is now in the project

`Assets/Scripts/FX/YoruFurLighting.cs`, on the `Directional Light` GameObject. It does XFurLightManager's job and sets the transmission value on top. `[ExecuteAlways]`, so it works in the Scene view without entering Play.

Current inspector state:

```
Key Light            (empty, resolves to RenderSettings.sun = Directional Light)
Fur Key Direction    FurKey        (empty GameObject, rotation 18 / -140 / 0)
Sheen Boost          1
Transmission         0.6
```

`Fur Key Direction` exists because `Directional Light` is at euler 55 / 20 / 0, a 55 degree top light, which gives a rounded shape almost no form. The Fur Key transform only aims the fur sheen and backlight. It carries no Light component and changes nothing else in the cave.

`SetSheen(float)` and `SetTransmission(float)` are public so the fight phases can drive them later.

### The rim that is not a light

Separately, line 406 to 408:

```
half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
half3 rimColor = lerp(_XFurSelfRimColor * saturate(...), saturate(o.Albedo * 2), 0.25)
               * _XFurSelfRimBoost * pow(rim, _XFurSelfRimPower) * (1 - length(vfxMap));
o.Albedo += rimColor;
o.Albedo = saturate(o.Albedo);
```

This adds to **albedo**, not emission. It is not a rim light, it is a view-dependent albedo lift. At `Rim Lighting Power` 1 it is a broad wash over most of the silhouette and it pushes a pale character past 1.0 into clipping. Power is now 4. The editor slider range is 0.1 to 10. `Rim Lighting Strength` has a minimum of **1.0** in the editor, so it cannot be lowered, only the power can be raised.

---

## 3. THE ALBEDO CLIPPING BUG THAT WAS FIXED

`Strands (R) Boost` and `Strands (G) Boost` in the Designer map to `_XFurSelfOverColorMod` and `_XFurSelfUnderColorMod` (`XFurStudioInstance.cs` lines 859 and 860). They land in line 353:

```
fColor = lerp( occlusionColor,
               furColor * lerp(_XFurSelfColorD * underMod, _XFurSelfColorC * overMod, underOver),
               saturate( occlusion + fur.b ) );
```

`_XFurSelfColorC` and `_XFurSelfColorD` are `noiseShadingTint2` and `noiseShadingTint3`, both pure white on this profile, so both boosts act as a flat multiplier.

Both were at **1.8**. Fur colour going in is Main Tint 0.765 / 0.843 / 0.922 times a noise blend between 0.86 and 1, so roughly 0.71 / 0.79 / 0.88. Times 1.8 gives **1.28 / 1.42 / 1.58**, and line 396 does `o.Albedo = saturate(o.Albedo + specFinal)`.

**Her albedo was clamped to pure white across the entire coat.** That destroyed the occlusion gradient, the strand noise and her actual fur colour in one clamp, leaving only the key light's NdotL to modulate the picture. Both are now **1.0**. This was a real fix and it should not be reverted.

Note the `saturate(occlusion + fur.b)` blend factor. At the root, occlusion equals `1 - strength`. So the term is `1 - strength + fur.b`, which saturates to 1 for any `fur.b` above the strength value, and the occlusion tint never mixes in at all. Occlusion strength has to exceed `fur.b` before roots darken. `fur.b` here is the strands pattern blue channel out of the strands asset, not the fur data map, and I never measured it.

---

## 4. CURRENT FUR PROFILE VS XFUR'S WORKING DEMO

The live objects are all under `PlayerYoru_1.1 > Cat_All_10_Tails_v4`:

- `model:Cat_BODY`
- `LeftTail_NoRings`
- `RightTail_NoRings`

Everything else with an XFur component in that scene is under the inactive `bodyYoru`, or is an inactive tail-ring variant. There are 24 XFur instances in the scene and only 3 of them render.

Profile 0 comparison. Tiger is `Assets/PIDI/XFur Studio 4/Demos/Legacy RP/Tiger/Tiger Demo.unity`, XFur's own demo, same pipeline, and it looks correct.

| field | TIGER (works) | YORU (current) | note |
|---|---|---|---|
| furThickness | **0.881** | **0.35** | biggest gap, see section 6 |
| furLength | 0.12 | 0.6 | |
| furThicknessCurve | 0.45 | 0.3 | |
| renderingSamples | 16 | 40 | |
| useCurlyFur | **0** | **1** | curl params 0.4 / 0.4 / 0.02 / 0.02 |
| doubleSided | 0 | 1 | |
| selfOcclusionStrength | 0.685 | 0.3 | |
| selfOcclusionCurve | 0.162 | 0.3 | |
| selfOcclusionTint | 0,0,0 | 60,55,70 (body) / **150,185,200 (tails)** | tails are inconsistent, see section 7 |
| roughness | 0.618 | 0.9 | `smoothness = 1 - roughness`, see below |
| specularTint | 0.217/0.108/0.042 warm | 0.15/0.15/0.15 grey | |
| mainTint | 1,1,1 | 0.765/0.843/0.922 | |
| mainFurStrandBoost | 1.14 | 1.0 | |
| secondaryFurStrandBoost | 1.65 | 1.0 | |
| rimLightingStrength | 1.73 | 1.0 | |
| rimLightingPower | 5.91 | 4 | |
| useProbes | 1 | 1 | fixed this session, was 0 on all 66 profiles |
| builtinFwdCompatibilityMode | 0 | 0 | must stay 0 |

**On roughness.** Line 368 is `smoothness = saturate( 1 - _XFurSelfSmoothness )` and `_XFurSelfSmoothness` is set from the profile's `roughness` (line 847). Then line 384:

```
specFinal = pow(aniso, 128 * pow(smoothness,5)) * 5 * smoothness * lerp(_XFurSelfSpecularTint, o.Albedo, 0.35);
```

At roughness 0.9, smoothness is 0.1, `pow(0.1,5)` is 1e-5, so the exponent is 0.00128 and `pow(aniso, 0.00128)` is about 1 everywhere aniso is positive. That is a broad uniform wash, not a strand highlight. The tiger's 0.618 gives smoothness 0.382, exponent about 1.0, an actual falloff. **Lower roughness gives a tighter, more fur-like sheen. This has not been tried yet.**

---

## 5. THE FUR DATA MAP, MEASURED

`Assets/YORU/FurData/Rigged_Cat_v14_V2_model_Cat_furDataMap.png`, 2048x2048.
Channels per XFur's own tooltip: **R = fur mask, G = length, B = occlusion, A = thickness.**

Measured inside the UV islands (R > 0.02, which is 51% of the map):

| channel | mean | std | p5 | p50 | p95 | spread p95/p5 |
|---|---|---|---|---|---|---|
| R mask | 0.993 | 0.045 | 1.000 | 1.000 | 1.000 | 1.0x |
| G length | 0.728 | 0.267 | 0.231 | 0.765 | 1.000 | 4.3x |
| B occl | 0.993 | 0.045 | 1.000 | 1.000 | 1.000 | 1.0x |
| A thick | 0.989 | 0.073 | 1.000 | 1.000 | 1.000 | 1.0x |

Tiger demo map for comparison: G spread **4.4x**, mean 0.596, and visually the same overlapping circular painter blobs. **So the green channel is normal. Do not blame it.**

R, B and A are effectively flat at 1.0 across the whole body. That matters because `_XFurSelfOcclusion * furData.b` means occlusion strength applies at full force uniformly with no per-area variation.

Import settings are `sRGBTexture: 1` and `textureCompression: 1`. **These match XFur's own shipped tiger data map exactly**, so they are XFur's intended setup, not a bug. I nearly reported them as one.

---

## 6. THE OPEN PROBLEM: DARK SPECKLING ON THE COAT

**Symptom.** Fine dark mottled speckles scattered across the whole lit coat, visible at gameplay distance, worse since Fur Length went 0.4 to 0.6. She describes it as "dots on it" and the fur colour reading wrong.

**This is unresolved. What follows is the leading hypothesis and it has not been tested.**

Look at lines 293 to 298:

```
half totalThickness = _XFurSelfThickness * furData.a * (1 + 0.15 * vfxMap.g * (1 - vfxMap.r));
half thicknessCurve = pow(furClip, lerp(8, 2, totalThickness) * pow(fPass/_XFurTotalPasses, 8 * _XFurSelfThicknessCurve));
furClip = furData.r * thicknessCurve - lerp(0.05,0.025,underOver) * _XFurSelfLength * (fPass/_XFurTotalPasses);
...
clip(furClip);
```

`furData.a` is 0.989, effectively 1. So `totalThickness` is basically the profile's `furThickness`.

- Yoru at 0.35: `lerp(8, 2, 0.35)` = **5.9**. The strand pattern is raised to a high power, crushing it toward zero, so `clip()` removes most of each shell and only sparse, isolated, thin strands survive.
- Tiger at 0.881: `lerp(8, 2, 0.881)` = **2.7**. Strands stay fat and connected into a coat.

Sparse thin strands, times 40 shells, times `furLength` 0.6 pushing them far off the surface, times `useCurlyFur: 1` (curl offsets the sample per shell and scatters them further, and the tiger has curl OFF) is a plausible mechanism for exactly this speckle pattern.

**Test order for whoever picks this up. One change at a time, screenshot each.**

1. `Fur Thickness` **0.35 to 0.7**. On `model:Cat_BODY` first, alone. This is the single most likely fix.
2. If that helps but is not enough, `Fur Thickness Curve` **0.3 to 0.45** to match the tiger.
3. `Use Curly Fur` **on to off**. Tiger has it off. Curl is scattering sparse strands.
4. `Fur Length` **0.6 back to 0.4**. She raised this herself and the speckle got worse after.
5. Only if all four fail, suspect shadow acne: 40 shells all have `castShadows: 1` and `receiveShadows: 1` with `addshadow` in the pragma, and the Directional Light is Soft shadows at strength 0.85. Turn `Receive Shadows` off on the profile as a diagnostic. If the speckle vanishes it is self-shadowing across the shell stack and the fix is shadow bias on the light, not the fur.

**Also unresolved: the fur colour.** Main Tint is 0.765 / 0.843 / 0.922, a pale blue-white, but she renders as a saturated royal blue-purple. That is the scene doing it, not the fur: ambient is a cool gradient, the key light is 0.69 / 0.88 / 0.97, and `Cave_Oni_Profile.asset` has saturation +20 and hueShift +10 on top. If the target is the softer look from `DemoScene_BlueNight`, the lever is the grade and the ambient, not Main Tint. **Nobody has confirmed with her which of the two she actually wants changed. Ask before touching it.**

---

## 7. EVERYTHING ELSE STILL OPEN

**Tail profiles are inconsistent with the body.** `LeftTail_NoRings` and `RightTail_NoRings` still have `selfOcclusionTint` at **150, 185, 200** (light blue) while the body is on **60, 55, 70**. So the body's roots darken toward near black while the tails' roots blend toward pale blue, which on a coat around 181/201/224 is almost no change. Tails also sit at occlusion 0.25 vs the body's 0.3. Fix both before judging her as a whole.

**Arena lighting cannot reach her.** Two lights are parented to `PlayerYoru_1.1` and neither can touch the fur, because Forward Add is off:

- `YoruRim`, Realtime point, local (0, 2.2, 0), range 7, colour 180/210/255, intensity 1.6. Named to rim her. Does nothing to her.
- `Yoru_Dim_light > Point Light`, Realtime point, **intensity 20**, range 10, colour 230/245/255. Also does nothing to her, but it throws a pale circular pool on the ground that follows her everywhere and washes out the darkness. Recommended dropping it to 6. Not yet done.

Scene point lights: `AuroraSpill` (Baked), `MoonPool_1/2/3` (Baked), `ColdLight_Rim` (Realtime), `ONI_KEY` (Realtime).
Directionals: `Directional Light` (Mixed, 1.2, euler 55/20/0, Soft shadows 0.85, and it is `RenderSettings.sun`), `Moon Light` (Mixed, intensity 0), `Sun Light` (Mixed, disabled).

**It is baking time, and she asked to be told.** Ambient Mode is Gradient (Sky 41/53/93, Equator 82/138/178, Ground 46/46/78), which is top to bottom only and identical on her left and right. Structurally flat forever. The only way to get side-to-side light on the fur is **light probes**, and probes only carry light from Baked or Mixed lights. 414 probes are already placed on `ArenaLightProbes` at (482, 0, 415). Last bake was earlier the same day. `m_MixedBakeMode: 2` (Shadowmask), `m_BakeResolution: 40`.

Sequence: place the braziers, set `ColdLight_Rim` and `ONI_KEY` and every brazier light to **Mixed**, then bake. `MoonPool_1/2/3` and `AuroraSpill` are already Baked.

**Braziers not placed.** One exists and it is the best-looking thing in every frame. Her lane, her placement. `Assets/Scripts/FX/TorchLight.cs` (ROUND 76) drives the flicker.

**Phase 2 lightning over-spawn.** Codex found `SkyShowRoutine()` spawning about 45 objects in 2 seconds. Diagnostic: `OniBoss` > `Sky Show - round 61 (he pulls the storm)` > uncheck `Cine Sky Show Enabled`, then one run through phase 2.

**No `Cave_Oni_Phase1` profile yet.** Should be the brightest, most saturated state. Only `Cave_Oni_Profile.asset` (base) and `Cave_Oni_Storm.asset` (phase 2) exist.

**Spectrum-reactive beat lights not built.** Her spec: no fight is quiet with steady lights, phase 1 starts fight music with lights pulsing on the beat, phase 2 is faster music and faster harder lights with a colour shift. **Phase 2 must not be darker than phase 1. Aggression reads as speed and movement, not darkness.** Beat source must be spectrum reactive off the actual playing track, not a typed BPM. Design note: the pulse must drive `RenderSettings.ambientLight` and the main directional to reach her fur at all, plus realtime accents for the environment. Coordinate with `OniBoss`, which already writes `RenderSettings.ambientLight` during the phase 2 cinematic.

**Wet fur not wired.** Needs an `XFurWeatherManager` component in the scene (none exists anywhere), the VFX and Weather module enabled on the XFur instance (`_vfxModule._enabled` is 0 on all three live objects), and `RainIntensity` driven from `StormWeather` per phase. The Rain block is already configured on the profile: penetration 0.75, smoothness 0.75, intensity 1.25, fadeout 4.

**Audio.** `CombatMusicManager` is not in the scene. No music tracks. All 19 `CombatSFXManager` slots empty. No AudioMixer.

**Closed rock zone.** Her lane. Local Post Process Volume with blend distance. No code needed.

**Moon Shadow Type is Hard.** She will want Soft back for the final look.

---

## 8. THINGS THAT WERE FIXED AND SHOULD NOT BE RE-BROKEN

- `VolumetricFogController.cs` had an `applyPresets` bool added and it is **off** in this scene. It was overwriting her hand-tuned Kronnect fog via reflection every Play.
- Auto Exposure was inverted and could only darken. `active: 0` now, matching `DemoScene_BlueNight`.
- Snow was firing inside the storm profile (white squares). Removed.
- Phase 1 had zero rain. `Imminent Storm` has no precipitation FX. `Yoru_Oni_Phase1_Storm.asset` (guid `7f3a82b967194102954db32760771d47`) now carries Light Rainfall, Light Rain Particle FX, Light Rain Atmosphere SFX, Intermittent Thunder, Raining Event.
- `Yoru_Oni_Phase2_Storm.asset` (guid `fa1bce6aa83a464784f0d798d9a2d2b7`) carries Tempestuous Wind, Torrential Thunder, Heavy Rainfall, Heavy Rain Particle FX, Thunderstorm Particle FX, Rain and Thunder SFX. All snow removed.
- A 2 September lightmap was lighting the floor from a different scene.
- Zero light probes and zero reflection probes. 414 probes now placed.
- All 66 XFur fur profiles had `useProbes` false, so the fur drew with `LightProbeUsage.Off`. All on now. **Do not call `UpdateFurMaterials()` from an editor script to apply this. It indexes `_furBlocks`, which only exists after XFur initialises, and throws IndexOutOfRange in edit mode. `useProbes` is read straight from the profile at draw time, `XFurStudioInstance.cs` lines 1233, 1260, 1265, so the profile edit alone is enough.**
- `StormWeather.cs` line 305, `ApplyFog(Mathf.Lerp(calmFogDensity, stormFogDensity, storm))`, had both fields at 0.45, so phase 2 slammed the Kronnect density back to 0.45. Set to 0.08 and 0.16.
- Phase 2 grade was going grey because storm saturation -20 fought base +20 and landed on exactly zero.

Post profiles, current: `Cave_Oni_Profile.asset` has temperature -5, tint +10, hueShift +10, saturation +20, postExposure 0.7, contrast 14, bloom intensity 1 / threshold 0.7 / diffusion 7 / blue tint, AO 0.15 radius 0.8, vignette 0.35, DoF and AutoExposure both `active: 0`. `Cave_Oni_Storm.asset` has temperature -22, saturation +4, postExposure 0.75, contrast 18, vignette 0.42, bloom 2.0, aberration 0.14, grain 0.12.

---

## 9. HER WORKING RULES, NON-NEGOTIABLE

- **Never assume. Ask if you are not sure.**
- **Every Unity setting comes with its exact location, every time.** Hierarchy object or window, then component, then section or foldout, then field label, then value.
- **No backup folders, no README files, no "save first" warnings.** The project is on GitHub and she reverts there.
- **No clutter in the project.** No permanent menu items for one-off passes. Edit files directly when safe. If an editor action is unavoidable, one menu item that deletes itself after running.
- **No em-dashes anywhere.** Not in chat, not in files, not in code comments.
- One step at a time. Complete scripts only, never snippets. Plain English, short sentences, no flattery.
- She tweaks by hand now. Scripts and measurement are yours, placement and tuning are hers.
- `Assets/Scripts/Player/PlayerMovement.cs`: **do not touch.**
- **Do not delete `bodyYoru`.** Migrating animation events off it is risky and she wants it checked thoroughly first.
- Handoff documents live outside the repo.
- Scripts follow a `ROUND N` comment header that explains why the approach was chosen and what earlier attempts missed. Highest used is ROUND 78 (`YoruFurLighting.cs`). TorchLight is 76.

---

## 10. THE ART TARGET

`Assets/Scenes 1/DemoScene_BlueNight.unity` is the reference she picked, in her own project. She wants that look "with fog a little and more chaotic". Its grade has already been transplanted into `Cave_Oni_Profile.asset` and deepened. BlueNight has no Kronnect fog at all, ambient Sky 41/53/93, Equator 82/138/178, Ground 46/46/78, directional intensity 0.3, and its profile is `Assets/Scenes 1/DemoScene_Nightt_Profiles/Post-Processing Volume Profile.asset`. **There is no fur in that scene**, so it is an environment reference only, not a fur reference.

The overall look she is after: deep shadows against strong light, rim light everywhere, warm orange inside cold grey. Her words: "artistically deep, cozy but cold, mysterious."
