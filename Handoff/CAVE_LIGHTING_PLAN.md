# CAVE LIGHTING PLAN - CaveScene_Oni_Boss1
**Project:** Yoru, Built-in RP, Unity 6000.2.7f2
**What this is:** the one place the lighting and post design for the Oni cave lives. Targets, owners, per-phase behaviour, and the current state read from the scene file. Chat memory is not the source of truth. This file is.
**How we work:** Hazel places and tunes by eye. Claude owns everything that changes over time (code) and measures the scene after each step. One step at a time: Hazel says "done", Claude re-reads the scene, confirms the values, next step. No value is proposed without reading the current one first.

---

## 1. THE LOOK, IN ONE PARAGRAPH

Cold from above, neutral from the side, warm from below and from fire. The moon and the aurora are the cold. The reddish rock keeps its own colour because the side ambient is neutral, not blue. Warmth comes from the floor bounce and from every flame. Rim light on both characters so they never sink into the dark. The covered pockets stay very dark with only lanterns in them. The open area is readable at all times. This is the Ori rule set: rim lights and light rays everywhere, warm orange inside cold grey, deep shadows and strong light so every object pops.

## 2. LIGHT LIST AND TARGETS

| Role | Light | Count | Where | Target values | Shadows | Render Mode | Slot | Owner |
|---|---|---|---|---|---|---|---|---|
| Key, the moon | `Directional Light` | 1 | rotation 65 / 180 / 0 | Intensity 1.0 pre-fight, colour 176 224 246, cookie MoonCloudCookie_Soft size 40, Strength 0.6 | soft | Auto | 1 | Hazel |
| Floor fill, moon pools | `MoonPool_1..3` (Point) | 3 | (478, 14, 433) (468, 14, 440) (488, 14, 426) | 170 200 255, Intensity 1.0, Range 34 (her choice, 6 Sep) | none | Not Important (still Auto, open) | 0 | Hazel |
| Aurora spill | `AuroraSpill` (Point) | 1 | (481, 16, 447), under the opening | 120 190 230, Intensity 0.8, Range 34 (her choice, 6 Sep) | none | Not Important (still Auto, open) | 0 | Hazel |
| Warm accents, north | `Brazier` (only one left, `Brazier (1)` was deleted on 6 Sep, ask before re-adding) | 1 | now at (477, 0.12, 447); plan was a pair flanking the stairs at Z 460 | their light: Range 9, Intensity 1.8, TorchLight on | none | Auto | 1 each when near | Hazel |
| Yoru rim | `YoruRim` | 1 | child of Yoru, behind and above (0, 2.5, -2.5) | 180 210 255, Intensity 1.6, Range 7 | none | Auto | 1 | Hazel |
| Oni key + rim | `ONI_KEY`, `ColdLight_Rim` | 2 | children of the Oni | keep as they are | none | keep | 2 | Hazel |
| Lightning | flash light (created by StormWeather) | 1 | above the opening | peak 2.5, in code | none | - | during strikes | Claude |
| Covered pockets | `JapeneseLantern` instances | as placed | closed parts only, one per pocket, 8 to 10 m apart | Point Light Intensity 2 to 3, Range 5 to 7, TorchLight cull 35 | none | Auto | 1 each when near | Hazel |
| COZY lights | `Moon Light`, `Sun Light` | 2 | inside Cozy Weather Sphere | Moon Light intensity 0 (COZY keeps it alive, never deactivate the object), Sun Light off | - | - | 0 | leave alone |

**Budget rule.** Pixel Light Count is 6. Near the player that is: moon 1 + Oni 2 + Yoru rim 1 + the two nearest fires 2 = 6. The four fill lights are Not Important, which means per-vertex, which means they cost no slot. On the dense terrain floor vertex lighting reads as a smooth gradient. Every new light that is not a fire or a character light should be Not Important.

## 3. WHAT CHANGES ACROSS THE FIGHT

| | Pre-fight | Phase 1 (he engages) | Phase 2 (lightning beat) | Owner |
|---|---|---|---|---|
| COZY weather | Clear, aurora visible | Imminent Storm | Thunder Storm | code, done (round 73) |
| Moon intensity | 1.0 | 0.8 | 0.6 (revised up on 6 Sep: phase 2 read near black) | code, round 78 |
| Aurora spill | 0.8 | fades to 0 | 0 | code, round 78 |
| Ambient (the 3 colours) | as set by hand | x0.85 | x0.65 and bluer | code, round 78 |
| Kronnect fog density | 0.35 (scene value) | 0.35 | 0.45 to 0.7 over 2.5 s (target: 0.5, `Storm Fog Density` on StormWeather) | code, done |
| Floor wetness | dry | soaks to 0.35 | fully wet | code, done |
| Lightning | none | every 14 to 26 s | every 3 to 8 s, flash light + screen flash | code, done |
| Post grade | Cave_Oni_Profile | + Cave_Oni_Storm at 0.4 | + Cave_Oni_Storm at 1.0 | code, done (round 77) |
| Fires, lanterns | on | on | on, the only warm thing left | Hazel |

Round 78 adds three slots to `StormWeather`: Moon Light, Clear Sky Lights (list), and the ambient multipliers. It reads the hand-set values at Start and scales them, so hand tuning stays the source of truth.

## 4. POST PROCESSING

One scene volume (`PostProcess_`, global, priority 0) with one profile, `Assets/Scenes 1/Cave/Cave_Oni_Profile`. That is the look. Tweak it freely.

`Assets/Scenes 1/Cave/Cave_Oni_Storm` is not a second look. It holds only five phase-2 differences (temperature, saturation, exposure, contrast, colour filter, vignette, bloom, aberration, grain) and StormWeather blends it in on a runtime volume at priority 10. The hit pulse (priority 100) and the hallucination (101) still sit on top.

Base profile intent: Tint 0 and Hue Shift 0 (no purple), Lift W +0.04 (blacks not crushed), Contrast 15, Post Exposure 1.1 EV, Bloom intensity 2 threshold 0.9 diffusion 8.5 neutral colour (only fires, moon, aurora and eyes bloom), AO 0.3 radius 0.45 blue, Motion Blur 60 / 6.

**Auto Exposure, how it really works** (read from `AutoExposure.compute`): exposure = Exposure Compensation / clamp(average luminance, 2^Minimum, 2^Maximum). Minimum (EV) is the cap on how much it may brighten: lift = Compensation / 2^Minimum (with 1.1: Minimum -0.5 gives 1.56x, -1.0 gives 2.2x, -1.5 gives 3.1x). Maximum (EV) is the cap on how much it may darken: with Maximum +0.2 it can only go down to 0.96x, so it never crushes a bright frame. Filtering 40 / 85 drops the darkest 40 percent and the brightest 15 percent of pixels (the fires) from the average. Her 6 Sep values (Minimum +0.2, Maximum +0.6) are inverted: that setup can only darken (0.73x to 0.96x). Target: Filtering 40 / 85, Minimum -1.0, Maximum +0.2, Compensation 1.1, Type Progressive, Speed Up 1.5, Speed Down 0.6.

**Phase 2 storm profile target** (`Cave_Oni_Storm`, Color Grading): Post Exposure 1.05 (not 0.95), Contrast 18 (not 22), Color Filter 225 235 255 (not 214 227 255). The storm comes from cold desaturation, vignette, grain, rain and lightning contrast, never from lowering exposure.

## 5. FOG (Kronnect Volumetric Fog on mainCamera (1))

Sun slot: `Directional Light`. **How the colour works** (read from `VolumetricFog.cs` and the shader): the rendered fog colour is Albedo times the sun slot's light colour (Copy Sun Color on) times the sun slot's intensity plus Light Intensity, and Deep Obscurance darkens it toward the floor. With Albedo 30 42 61 the fog is dark navy paint over everything past Start Distance (20 m), darkest where the fight is. Targets: Albedo 60 80 115, Deep Obscurance 0.4, Density 0.35 pre-fight, Start Distance 20. Compute Depth on, scope Tree Billboards And Transparent Objects, layer Default (so fur is not painted over). Sun Shadows on, strength 0.5 (the shaft through the opening). Dithering on 0.4. COZY sky, fog and scene lighting are all off. Kronnect owns fog, the Lighting window owns ambient, the skybox is the aurora cubemap.

## 6. PLACEMENT STEPS FOR THIS ROUND (Hazel)

1. `Directional Light` → Light → Intensity 0.7 → **1.0**.
2. Create the three moon pools. Hierarchy → right-click → Light → Point Light, name `MoonPool_1`. Transform → Position (478, 9, 433). Light → Color 170 200 255, Intensity 0.6, Range 22, Shadow Type No Shadows, Render Mode **Not Important**. Duplicate twice (Cmd+D), positions (468, 9, 440) and (488, 9, 426).
3. Create `AuroraSpill` the same way at (481, 16, 447): Color 120 190 230, Intensity 0.5, Range 28, No Shadows, Not Important.
4. Move `Brazier` to (477, floor, 460) and `Brazier (1)` to (487, floor, 460). Snap to the floor by eye. On each: expand → `Point Light` → Range 9, Intensity 1.8. `TorchLight` is on them already through the prefab.
5. `PlayerYoru_1.1` → `YoruRim` → Position (0, 2.5, -2.5), Color 180 210 255, Intensity 1.6, Range 7.
6. Lanterns in the covered pockets: one per pocket, 8 to 10 m apart, never on the open floor. `Point Light` Intensity 2 to 3, Range 5 to 7.
7. Save. Say "done". Claude re-reads the scene, updates section 8, then ships round 78.

## 7. ROUND LOG

- 73: StormWeather, open sky until the fight, storm snaps in on engage.
- 74: lantern FBX import fixed, mesh moved to its own child, Pixel Light Count 6.
- 75/76: TorchLight rewritten, layered flicker with the gulp, colour rides brightness.
- 77: post processing, base profile regraded, storm profile, runtime crossfade and strike screen flash.
- 6 Sep, her pass: fills raised (pools 1.0 / 34, spill 0.8 / 34), moon rotation 55 / 20 / 0, cookie size 60, `Environment (BackUp)` removed, rocks re-placed, `Brazier (1)` and the arena lantern removed, `BossHealthBar` deleted by accident (rebuild: Hierarchy, UI, Canvas, rename, Add Component Boss Health Bar UI).
- 78 (next): moon, aurora spill and ambient change with the phases, phase 1 profile, beat pulse, COZY FX pre-init. Details in the COMBAT 9 handoff document (Hazel keeps it outside the project).

## 8. CURRENT STATE (generated from the scene file, 2026-09-06 21:39 UTC)

Regenerated by Claude after each step. If this disagrees with the plan above, the plan is the target and this is reality. Positions are WORLD positions. Fight centre is (478, 433).

Ambient (Lighting window, Trilight): Sky 61 73 110 / Equator 92 100 121 / Ground 66 46 34. Pixel Light Count (Ultra): 6. Auto Exposure: on.

| Light | Under | Type | State | Intensity | Range | Colour (RGB) | Shadows | Render Mode | World pos |
|---|---|---|---|---|---|---|---|---|---|
| `AuroraSpill` | scene root | Point | on | 0.8 | 34 | 120 190 230 | none | Auto | 481, 16.0, 447 |
| `ColdLight_Rim` | OniBoss | Point | on | 2.06 | 14 | 102 115 255 | none | Auto | 468, 8.0, 433 |
| `Directional Light` | scene root | Directional | on | 1 | - | 176 224 246 | soft | Auto | 56, 26.3, 27 |
| `Moon Light` | Sky < Cozy Weather Sphere | Directional | on | 0 | - | 203 217 245 | soft | Auto | 267, 339.4, 573 |
| `MoonPool_1` | scene root | Point | on | 1 | 34 | 170 200 255 | none | Auto | 478, 14.0, 433 |
| `MoonPool_2` | scene root | Point | on | 1 | 34 | 170 200 255 | none | Auto | 468, 14.0, 440 |
| `MoonPool_3` | scene root | Point | on | 1 | 34 | 170 200 255 | none | Auto | 488, 14.0, 426 |
| `ONI_KEY` | OniBoss | Point | on | 1.6 | 14 | 255 201 138 | none | Important | 470, 7.0, 442 |
| `Point Light` | Yoru_Dim_light < PlayerYoru_1.1 | Point | OFF (parent inactive) | 20 | 10 | 230 244 255 | none | Important | 474, 5.0, 426 |
| `Sun Light` | Sun Offset < Sky < Cozy Weather Sphere | Directional | OFF | 2 | - | 73 77 78 | none | Auto | 267, 339.4, 573 |
| `YoruRim` | PlayerYoru_1.1 | Point | on | 1.6 | 7 | 180 209 255 | none | Auto | 474, 4.2, 427 |

| Light-carrying prop | World position (x, y, z) | From fight centre |
|---|---|---|
| `Brazier` | 477.0, 0.12, 447.0 | 14 m |
| `JapeneseLantern (1)` | 401.2, 0.90, 452.1 | 79 m |
| `JapeneseLantern (13)` | 551.9, 1.50, 467.8 | 82 m |
| `JapeneseLantern (11)` | 563.4, 1.13, 378.8 | 101 m |
| `JapeneseLantern (2)` | 363.5, 1.00, 464.3 | 119 m |
| `JapeneseLantern_herehiddengem` | 371.5, 0.06, 504.7 | 128 m |
| `JapeneseLantern (15)` | 476.5, 1.13, 299.1 | 134 m |
| `JapeneseLantern (12)` | 566.4, 1.02, 318.3 | 145 m |
| `JapeneseLantern (3)` | 333.9, 0.13, 469.8 | 149 m |
| `JapeneseLantern (10)` | 410.9, 1.20, 287.8 | 160 m |
| `JapeneseLantern (4)` | 320.4, 0.09, 495.5 | 170 m |
| `JapeneseLantern (14)` | 328.6, 1.07, 314.9 | 190 m |
| `JapeneseLantern (7)` | 282.8, 1.09, 436.8 | 195 m |
| `JapeneseLantern (9)` | 286.3, 0.98, 346.6 | 210 m |
| `JapeneseLantern (8)` | 265.2, 1.09, 398.1 | 216 m |
| `JapeneseLantern (6)` | 250.4, 1.04, 464.4 | 230 m |
| `JapeneseLantern (5)` | 244.7, 1.07, 477.8 | 238 m |
