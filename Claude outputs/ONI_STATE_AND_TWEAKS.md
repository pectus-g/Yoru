# Where the project stands, and the dials that are yours

## 1. First, check this before trusting your last test

Unity is open and has been the whole session. Your last play session started at 23:17. I reverted the
Oni lights at 23:11, six minutes earlier. If you did not reload the scene in between, Unity was still
running the bright version from its own memory, and what you judged was the version I had already
undone.

**The check, one second:** Hierarchy, expand `OniBoss`, click `ONI_KEY`, look at Inspector, Light,
Intensity.

- It says **2.5** → your editor is current, your judgement stands.
- It says **8** → your editor is stale. Reload the scene: File, Open Scene, CaveScene_Oni_Boss1, and
  answer Don't Save. Then judge again.

While Unity holds a stale copy, any save from the editor writes that stale copy back over the file.

## 2. Everything I changed, and what is on disk right now

### Reverted, nothing of mine remains

| | state |
|---|---|
| `Cave_Oni_Profile` (post processing) | byte identical to your 11 Sep original. Lift -0.55, Contrast 14, Tint 10, Hue 10, Saturation 20, blue Bloom at threshold 0.7, AO 0.15. Verified by file comparison. |
| `ONI_KEY` intensity | back to 2.5 |
| `ColdLight_Rim` intensity | back to 0.8 |
| Oni body albedo, normal, metallic maps | untouched originals |
| Oni body smoothness | back on its map at 0.9 |

### Still in place, on the Oni

| change | where |
|---|---|
| `ColdLight_Rim` colour is moon 176 224 247, was 102 115 255 | OniBoss, ColdLight_Rim, Light, Color |
| `ColdLight_Rim` no longer touches Yoru | same, Culling Mask, Player unticked |
| Hair no longer casts shadows on his face | OniBoss, Oni Hair Shadows component |
| Eyes emit at 6 instead of 1 | eye l and eye r materials, Emission |
| Skin pore detail normal at 0.35, tiling 24 | body material, Secondary Maps |

### Still in place, HUD and combat

| change | where |
|---|---|
| Boss bar fully rewritten: ink stroke, kanji seal, brush name with halo, heartbeat, two stage fill, phase dots | `BossHealthBarUI.cs` and bossHealthBar |
| Canvas Scaler added so the bar is the same size on every screen | bossHealthBar, Canvas Scaler |
| Light and heavy hit sparks wired, blue | PlayerYoru_1.1, Yoru VFX Manager |

### New files

`OniSkinPores_Normal.png`, `OniSeal_Kanji.png`, `OniSeal_KanjiGlow.png`, `OniHairShadows.cs`, and three
documents in `Claude outputs/`.

## 3. You are right about the wall, and here is the arithmetic

What a pixel of his skin ends up as on screen is three numbers multiplied together, then graded:

**albedo x light = colour, then the grade decides what survives.**

Your grade sends anything below about **0.035 linear** to pure black. His skin right now:

| channel | albedo | light on it | result | survives? |
|---|---|---|---|---|
| red | 0.211 | 0.615 | 0.130 | yes |
| green | 0.081 | 0.485 | 0.039 | barely |
| blue | 0.083 | 0.333 | 0.028 | no |

That is the whole problem in one table. His green and blue land under the floor, so he renders as pure
red with no form. There are only three terms in that multiplication, so there are only three places to
fix it:

- **the light** - you tested it, it looked artificial and spilled onto Yoru
- **the grade** - yours, restored, off limits
- **the albedo** - which is why the brief went to your modeller

There is no fourth term. This is not me running out of ideas, it is the equation only having three parts.

The one thing that steps outside it is **emission**, which is added after lighting and before grading, so
it needs neither more light nor a different grade and cannot spill onto Yoru or the floor. That is the
shader rim and the self lit skin you passed on. It stays available if you change your mind. Nothing else
does.

## 4. The dials, if you want to drive this yourself

### Making him read better, in order of how much they move the needle

| dial | where | now | try | what it does |
|---|---|---|---|---|
| Intensity | OniBoss, ONI_KEY, Light | 2.5 | 3 to 4 | the smallest step that gets green out of his skin. Past 5 the floor pool appears. |
| Color | OniBoss, ONI_KEY, Light | 255 201 138 | 255 237 219 | a less orange key. Lifts green and blue without adding any brightness, so no new spill. Free improvement, try this first. |
| Range | OniBoss, ONI_KEY, Light | 14 | 10 | tightens the light to his body so raising Intensity spills less on the floor. |
| Intensity | OniBoss, ColdLight_Rim, Light | 0.8 | 0 to 2 | his back and shadow side. Already excluded from Yoru so it cannot affect her. |

### Making the whole cave darker again, if that is what you want back

| dial | where | now |
|---|---|---|
| Lift, the offset slider under the wheel | Cave_Oni_Profile, Color Grading, Trackballs | -0.55. Less negative is lighter, more negative is darker. This one field is your entire black point. |
| Contrast | same panel | 14. Raising it deepens shadows without clipping as hard as Lift does. |
| Intensity | Cave_Oni_Profile, Vignette | 0.35. Darkens the frame edges only, never the centre, so it cannot hurt the Oni. |

### The boss bar

All on bossHealthBar, Boss Health Bar UI. Name Glow Radius P1 and P2 for halo size, Heartbeat Max for
thump strength, Heartbeat Period Full and Low for its tempo, Under Glow P1 and P2 for the glow under
the ink, Bar Width and Bar Height for the stroke, Phase Count 0 to remove the dots.
