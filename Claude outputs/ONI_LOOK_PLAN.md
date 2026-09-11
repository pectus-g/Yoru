# ONI LOOK PLAN (11 Sep 2026)

Scene: `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`. Profile: `Assets/Scenes 1/Cave/Cave_Oni_Profile.asset`.
Everything below was read from the project today, not guessed. Numbers are display values 0 to 255 unless marked "linear".

## 1. What is actually off (measured)

**The colour grading clips the Oni to black and pure red.** I ported the Post Processing v2 ACES pipeline
(exposure, log contrast, white balance, lift/gamma/gain, hue/sat, ACES) and ran the scene values through it.
With the current profile (Post Exposure 0.7, Contrast 14, Lift offset -0.55, Gamma 0.168, Gain 0.608,
Tint 10, Hue Shift 10, Saturation 20):

| what the renderer produces (linear) | shows on screen now | with the proposed grade |
|---|---|---|
| grey 0.005 to 0.03 | 0, 0, 0 (black) | 1 to 26 |
| grey 0.05 | 17 | 45 |
| grey 0.10 (neutral rock) | 93, 81, 121 (violet) | 79, 87, 94 (cool grey) |
| grey 0.25 | 184 | 164 |
| Oni skin under ONI_KEY, facing the light | 96, 0, 0 (pure red) | 91, 35, 28 (warm red) |
| Oni skin under ONI_KEY, 45 degrees | 50, 0, 0 | 67, 23, 19 |
| Oni skin under ONI_KEY, 66 degrees | 0, 0, 0 | 39, 10, 9 |
| Oni skin under the moon, 45 degrees | 106, 6, 70 (magenta) | 92, 54, 67 |
| Yoru fur, moon + her rim | 200, 207, 220 | 183, 199, 206 |

Why: in this pipeline Lift is a linear offset. Offset -0.55 becomes about -0.10 in ACEScg linear, and Gain 0.608
becomes x1.5. So the grade is "multiply by 1.5, subtract 0.10": every pixel under 0.04 linear is black, and the
red skin loses its green and blue channels first, which is why he reads as a flat pure-red sticker with no
muscle. The purple on neutral surfaces comes from the trackballs (green is the lowest channel in Lift and
Gamma) plus Tint 10 plus Hue Shift 10. Your own plan (CAVE_LIGHTING_PLAN.md section 4) says "Tint 0 and Hue
Shift 0 (no purple), blacks not crushed". The profile went back to the old grade on 7 Sep 20:09 (commit 5462692d)
and got Lift -0.55 at 20:18 (478e9a8a). That is the day the Oni started looking cheap.

His mesh (165,824 tris), his textures (albedo now 2.5:1, data maps linear) and his materials (Standard,
metallic map, normal map, occlusion) are fine. No light in the scene can fix a black clip that happens after
rendering; this is why every light change so far went in circles.

**Second: the rim light really does pollute Yoru, by the same amount as her own rim.** ColdLight_Rim is
0.8 intensity, range 14, colour 102 115 255 (saturated blue). At 5 m it puts 0.19 of blue on Yoru; her own
YoruRim (0.6, range 7, at 2.2 m) puts 0.17 on her. Two rims from two directions flatten her. On the Oni,
saturated blue on red skin makes a purple edge, which is the "artificial" look. Culling it to Enemy only
removed the light pool under him (sticker), so that is not the fix either.

**Third: his skin is matte.** Body smoothness map alpha is 0.41 to 0.62, times Smoothness 0.9 = 0.37 to 0.56.
It rains in the arena from phase 1 and the floor gets wet (StormWeather Wet Smoothness 0.75), but he stays dry.
Wet skin is where the highlights and the "expensive" look come from in rain fights.

## 2. What the references do (checked today)

- Level Design Book, "Lighting for darkness": make it feel dark, don't make it dark; "some shadows terminate into
  0% black" but not whole subjects; "don't rely on dark albedo textures to darken the scene"; rim is used to
  emphasise silhouettes while most of the subject stays in shadow.
- Exp-Points, "Asking the Legends of Lighting Art": Guillaume Deschamps-Michel (Ready at Dawn) avoids pitch
  black even when he likes strong contrast; Boon Cotter (Naughty Dog) motivates every light and turned specular
  down on rain-soaked scenes because full physical wetness looked worse; Andrew Prince (Call of Duty) uses a
  complementary warm colour against a cool scene to pull the eye.
- Game Developer, "Character rim lighting": studios do rim in the shader with a direction mask, an occlusion
  mask and a per-part texture mask, not with a real light behind the character.

Translation for this scene: fix the tone curve first, keep the cold cave in the lights (moon, ambient) and the
warmth on the Oni (ONI_KEY), never in the grade; separate him by value and hue, not by a blue line; give him
wet highlights; a rim only if still needed, and then in the shader (parked in `_to_delete/`).

## 3. Steps (one at a time, test between)

### Step 1. Grade (asset file, Unity can stay open, it reimports on focus)
Project → `Assets/Scenes 1/Cave/Cave_Oni_Profile` → Inspector → Color Grading
- White Balance → Tint: 10 → **0**  (Temperature stays -5)
- Tone → Hue Shift: 10 → **0**; Saturation: 20 → **12**; Contrast: 14 → **18**; Post-exposure stays 0.7
- Trackballs → Lift: colour **(0.97, 0.985, 1.00)**, offset **-0.03** (was 0.96/0.91/1.00, -0.55)
- Trackballs → Gamma: colour **(1, 1, 1)**, offset **0.05** (was 0.90/0.88/1.00, 0.168)
- Trackballs → Gain: colour **(1, 1, 1)**, offset **0** (was 0.86/0.98/1.00, 0.608)
Same profile → Bloom → Color: **white** (was 58 121 255, it put a blue halo on the warm key and fires); Threshold 0.7 → **0.9**
Same profile → Ambient Occlusion → Intensity 0.15 → **0.3**, Thickness Modifier 1.5 → **1.0** (muscle crevices)
Expected: blacks still exist (0.005 linear = 1), rock reads cool grey instead of violet, the Oni shows a red
gradient across the muscles, Yoru gets slightly darker (about -15) not brighter. If the frame feels too open,
the one dial is Lift offset: -0.03 → -0.06 (still no clip above 0.02 linear).

### Step 2. The rim (scene file, needs Unity closed or reload without saving)
Hierarchy → OniBoss → ColdLight_Rim → Light
- Color: 102 115 255 → **176 224 247** (the moon's colour, so the edge reads as moonlight, not a purple line)
- Intensity: stays 0.8. Range stays 14.
- Culling Mask: Everything → **Everything except Player** (untick Player). The ground pool under him stays, Yoru gets nothing from it.
ONI_KEY stays 2.5 / 255 201 138 / Soft. Re-check after Step 1; if he still needs more, 3.0, not a new light.

### Step 3. Wet skin (material file, Unity can stay open)
Project → `Assets/Enemies/ONI/texture/Materials/demon warior8_body_AlbedoTransparency` → Inspector → Main Maps
- Smoothness Source: Metallic Alpha → **Albedo Alpha** (the albedo has no alpha, so it counts as 1.0)
- Smoothness: 0.9 → **0.7** (this slider now sets the whole body; 0.7 = damp, restrained on purpose)
Reversible in one click (Source back to Metallic Alpha). If it works: arm shields and waist pads 0.6.

### Later, only if wanted
- Ambient (Lighting → Environment) is Sky 41 53 93 / Equator 82 138 178 / Ground 46 46 78 x1.21. Your plan says
  neutral side, warm floor: 61 73 110 / 92 100 121 / 66 46 34. The cyan side ambient is what greys out his red
  on the unlit sides.
- Eyes: `_EmissionColor` white → HDR x6 so they bloom softly (emission map is dim cyan, 0.16 linear).
- A brazier prop under ONI_KEY so the warm key is motivated.
- Shader Fresnel rim (parked), only after 1 to 3 are judged.
