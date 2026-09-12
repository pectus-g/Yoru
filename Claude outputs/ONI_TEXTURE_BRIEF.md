# ONI: texture and material rework brief

**Character:** the Oni boss (General Takeshi), YORU: Eclipse of Tails.
**Scope:** textures and material authoring only. The mesh and the rig are fine and must not change.
**Source file:** `ONI_Base_01.fbx`, 91,755 verts / 165,824 triangles across 46 pieces. Silhouette, topology
and UVs are good. Nothing below asks for a remodel.

## 1. Where he is used, and why it matters

He fights in a cave at night, in a storm. The scene is deliberately dark: the render pipeline is Unity
Built-in deferred with an ACES tonemapper and a heavy grade whose black point sits at roughly **0.035 in
linear light**. Anything on screen whose colour channel falls below that value is clipped to pure zero.

That grade is intentional and is not going to change. It is what gives the game its look. So every surface
on this character has to carry its own detail within that constraint. Right now his skin does not, and the
result is that he reads as a flat red silhouette with no muscle, no form and no material identity.

Everything below is measured from the current texture set, not an opinion about the art.

## 2. The three defects

### 2.1 The body normal map is effectively flat

Mean surface tilt in each normal map on this character:

| map | mean tilt | 95th percentile |
|---|---|---|
| **body (skin)** | **3.1 deg** | **10.2 deg** |
| arm shields | 25.0 deg | 74.1 deg |
| kanabo | 24.5 deg | 58.8 deg |
| clothing upper leg | 22.1 deg | 65.7 deg |
| horn | 21.3 deg | 54.4 deg |
| lower leg clothing | 17.8 deg | 53.3 deg |
| waist pads | 14.2 deg | 41.6 deg |
| hair | 14.5 deg | 48.6 deg |

Every other surface on him carries real normal detail. His skin carries almost none. There are no pores,
no muscle striation, no scars, no skin folds. All of his muscle definition currently lives in the mesh
alone, so the moment he is lit from anything but a perfect angle, his body goes featureless.

**Needed:** a properly baked normal map from a high-poly sculpt of his torso, arms, legs, hands and face.
Target mean tilt in the 12 to 20 degree band, in line with his own armour maps. Pores and fine skin grain
at the high frequencies, muscle and scar forms at the low frequencies.

### 2.2 His skin albedo loses its green and blue channels in our light

Linear albedo means across his texture set:

| surface | R | G | B | R:G |
|---|---|---|---|---|
| **body (skin)** | **0.211** | **0.081** | **0.083** | **2.6** |
| arm shields | 0.212 | 0.126 | 0.097 | 1.7 |
| teeth | 0.179 | 0.105 | 0.084 | 1.7 |
| waist pads | 0.174 | 0.110 | 0.077 | 1.6 |
| horn | 0.202 | 0.143 | 0.088 | 1.4 |
| lower leg clothing | 0.140 | 0.110 | 0.074 | 1.3 |
| kanabo | 0.186 | 0.152 | 0.132 | 1.2 |
| clothing upper leg | 0.110 | 0.109 | 0.090 | 1.0 |

His skin is the only surface on the character with that much red dominance, and its green channel at 0.081
is the darkest of any non-cloth surface. Under a warm key light in our grade, the red channel survives and
the green and blue clip to zero, so his skin renders as pure saturated red with no tonal range. Measured on
screen today: 103, 0, 0 where the light hits him straight on, 60, 0, 0 at 45 degrees, and pure black past
66 degrees.

**Needed:** he should still read as a red demon. The constraint is that **the green channel must not fall
below about 0.13 in linear albedo** anywhere on the lit areas of his skin, or it disappears in our night
grade. That lands his red-to-green ratio around 1.5 to 1.7, which is where his own arm shields already sit
and still look convincingly red. How you get there is your call: more sub-surface warmth in the mid tones,
less pure crimson in the base, more value variation across the body.

### 2.3 There is no metal anywhere, and the skin roughness has no range

Metallic values, measured:

| surface | metallic mean | max |
|---|---|---|
| arm shields | 0.000 | 0.00 |
| waist pads | 0.000 | 0.00 |
| horn | 0.000 | 0.00 |
| kanabo | 0.023 | 0.98 |
| everything else | 0.000 | 0.00 |

His iron arm shields, his waist pad studs and his armour plates are all authored as pure dielectric. Only
the kanabo has any metal at all, and only in a small part of it. In a dark scene, metal is where nearly all
of the readable highlight comes from, so authoring it as non-metal removes his strongest source of shape.

Smoothness range, 10th to 90th percentile:

| surface | p10 | p50 | p90 | range |
|---|---|---|---|---|
| **body (skin)** | **0.41** | **0.50** | **0.62** | **0.20** |
| arm shields | 0.09 | 0.66 | 0.70 | 0.61 |
| kanabo | 0.09 | 0.47 | 0.70 | 0.61 |
| sake gourd | 0.33 | 0.79 | 0.91 | 0.58 |
| horn | 0.15 | 0.44 | 0.67 | 0.53 |
| waist pads | 0.24 | 0.37 | 0.70 | 0.47 |

His skin roughness barely varies. There is no difference between his oily brow, his dry callused knuckles,
his scars and his flat chest. A uniform roughness gives a uniform highlight, which is what makes a surface
read as plastic rather than as skin.

**Needed:** metallic 1 on the actual metal (arm shields, armour plates, waist pad studs, the kanabo bands),
with wear and pitting carried in the roughness rather than in the metallic. And a skin roughness map with
real range, target 0.4 or wider between the 10th and 90th percentile: shinier on the brow, nose, shoulders
and anywhere sweat would sit, much rougher on the hands, feet, elbows and scar tissue.

## 3. Deliverables

Per material set (body, arm shields, waist pads, horn, lower leg clothing, clothing upper leg, kanabo, hair):

- Albedo / base colour, 4096 x 4096, PNG, sRGB
- Normal map, 4096 x 4096, PNG, OpenGL convention (green up), linear
- Metallic + smoothness packed as Unity expects it: metallic in RGB, **smoothness in the alpha channel**,
  4096 x 4096, PNG, linear, not sRGB
- Ambient occlusion, 4096 x 4096, PNG, linear

The current set is already 4096 x 4096 across the board, so resolution is not the issue and does not need
to go up. This is an authoring pass, not a resolution pass.

## 4. What must not change

- The mesh, the UV layout and the skeleton. Animations are already built against this rig.
- The material and texture file names. They are wired into the scene and the prefab.
- His silhouette and his design. He is right; it is his surface that is not carrying.
- The eye and horn emission maps. Those work and are used for his glow.

## 5. How to check the work before sending it

Open the textures and confirm, on the skin specifically: the normal map has visible pore and muscle detail
rather than a flat blue field; the albedo green channel viewed on its own is not near black; the roughness
map viewed on its own has clear light and dark areas rather than one flat tone. Those three checks are the
whole brief.
