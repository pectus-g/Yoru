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
| Floor fill, moon pools | `MoonPool_1..3` (Point) | 3 | (478, 9, 433) (468, 9, 440) (488, 9, 426) | 170 200 255, Intensity 0.6, Range 22 | none | Not Important | 0 | Hazel |
| Aurora spill | `AuroraSpill` (Point) | 1 | (481, 16, 447), under the opening | 120 190 230, Intensity 0.5, Range 28 | none | Not Important | 0 | Hazel |
| Warm accents, north | `Brazier`, `Brazier (1)` | 2 | flank the stairs: (477, floor, 460) and (487, floor, 460) | their light: Range 9, Intensity 1.8, TorchLight on | none | Auto | 1 each when near | Hazel |
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
| Moon intensity | 1.0 | 0.6 | 0.35 | code, round 78 |
| Aurora spill | 0.5 | fades to 0 | 0 | code, round 78 |
| Ambient (the 3 colours) | as set by hand | x0.85 | x0.65 and bluer | code, round 78 |
| Kronnect fog density | 0.45 | 0.45 | 0.7 over 2.5 s | code, done |
| Floor wetness | dry | soaks to 0.35 | fully wet | code, done |
| Lightning | none | every 14 to 26 s | every 3 to 8 s, flash light + screen flash | code, done |
| Post grade | Cave_Oni_Profile | + Cave_Oni_Storm at 0.4 | + Cave_Oni_Storm at 1.0 | code, done (round 77) |
| Fires, lanterns | on | on | on, the only warm thing left | Hazel |

Round 78 adds three slots to `StormWeather`: Moon Light, Clear Sky Lights (list), and the ambient multipliers. It reads the hand-set values at Start and scales them, so hand tuning stays the source of truth.

## 4. POST PROCESSING

One scene volume (`PostProcess_`, global, priority 0) with one profile, `Assets/Scenes 1/Cave/Cave_Oni_Profile`. That is the look. Tweak it freely.

`Assets/Scenes 1/Cave/Cave_Oni_Storm` is not a second look. It holds only five phase-2 differences (temperature, saturation, exposure, contrast, colour filter, vignette, bloom, aberration, grain) and StormWeather blends it in on a runtime volume at priority 10. The hit pulse (priority 100) and the hallucination (101) still sit on top.

Base profile intent: Tint 0 and Hue Shift 0 (no purple), Lift W +0.04 (blacks not crushed), Contrast 15, Post Exposure 1.1 EV, Bloom intensity 2 threshold 0.9 diffusion 8.5 neutral colour (only fires, moon, aurora and eyes bloom), AO 0.3 radius 0.45 blue, Motion Blur 60 / 6.

**If the scene reads too dark:** untick Auto Exposure first. It reads the fires and the moon as the scene's brightness and pulls everything else down. Either leave it off or set Type Fixed, Exposure Compensation 1.15.

## 5. FOG (Kronnect Volumetric Fog on mainCamera (1))

Sun slot: `Directional Light`. Albedo 30 42 62. Compute Depth on, scope Tree Billboards And Transparent Objects, layer Default (so fur is not painted over). Sun Shadows on, strength 0.5 (the shaft through the opening). Deep Obscurance 0.5. Dithering on 0.4. COZY sky, fog and scene lighting are all off. Kronnect owns fog, the Lighting window owns ambient, the skybox is the aurora cubemap.

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
- 78 (next): moon, aurora spill and ambient change with the phases.

## 8. CURRENT STATE (generated from the scene file, 2026-09-06 18:27 UTC)

Regenerated by Claude after each step. If this disagrees with the plan above, the plan is the target and this is reality.

Ambient (Lighting window, Trilight): Sky 61 73 110 / Equator 92 100 121 / Ground 66 46 34. Pixel Light Count (Ultra): 6.

| Light | Type | State | Intensity | Range | Colour (RGB) | Shadows | Render Mode |
|---|---|---|---|---|---|---|---|
| `ColdLight_Rim` | Point | on | 2.06 | 14 | 102 115 255 | none | Auto |
| `Directional Light` | Directional | on | 0.7 | - | 176 224 246 | soft | Auto |
| `Moon Light` | Directional | on | 0 | - | 203 217 245 | soft | Auto |
| `ONI_KEY` | Point | on | 1.6 | 14 | 255 201 138 | none | Important |
| `Sun Light` | Directional | OFF | 2 | - | 73 77 78 | none | Auto |
| `YoruRim` | Point | on | 0.5 | 20 | 245 250 223 | none | Auto |

| Light-carrying prop | Position (x, y, z) |
|---|---|
| `Brazier (1)` | 490, 0.12, 447 |
| `Brazier` | 477, 0.12, 447 |
| `JapeneseLantern` | 474.49777, 0.18, 425.2276 |
