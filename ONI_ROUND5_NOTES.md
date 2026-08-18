# Oni — Round 5: the charge, for real

Short version: I stopped guessing and read the actual FBX files and the scene. The charge bug is a
**clip import setting**, not a behaviour bug, and I found two wiring problems on the way. The code
now also writes its own log file, so you never have to paste a console again.

---

## What I found (facts, not theories)

**1. The Oni Charge clip carries an 18 m dash inside the Hips bone.**
Read straight from the FBX: the Hips bone travels 18 m forward over the clip while the NavMesh
transform stands still — the body flew past the camera. That is the "charges, vanishes, comes back
and hits". Round 5's first test proved the fix: the pin held the mesh on his feet through the whole
clip (log: `max raw drift 18.07m, max cancelled 18.07m — pin held it`, mesh offset 0.24 m every
frame, never culled). I also switched the clip's *Root Motion Node* import setting to None (it was
the only clip in the project set to RootNode) — that turned out NOT to change the pose, so the pin is
the mechanism, not a safety net. Fine either way.

The clip itself, frame by frame (2.08 s):
- 0.00–0.20 coil back, club comes forward
- 0.26–0.28 club dead level (lance), small hop
- 0.30–0.60 the dash: 15 m at ~25 m/s, airborne, club straight forward
- 0.58 feet land, 0.66–0.78 club sweeps down, **~0.76 impact** (club straight down)
- 0.80–1.00 stands back up

**2. Light and medium reactions were the wrong way round — my fault, fixed in code.**
In ONICONTROLLER, `HitReact_medium` plays the **1.46 s** stumble clip and `Hit_react_light` plays the
**0.79 s** tiny flinch (that is fine and stays). But the OniBoss defaults I shipped assumed the two
clips would be swapped (the plan's old "Step 2", which you rightly dropped), so a 10-dmg paw played the
big react and a 20-dmg strong paw the tiny one. That is a big part of "I can't tell which hit is
which". The two fields are now `Quick Flinch State` = `Hit_react_light` and `Full React State` =
`HitReact_medium`, applied from code — nothing for you to set.

**3. The KanaboSweep animator state plays the Idle clip.** (Already known — the sweep attack
literally idles.)

**4. The club is a child of the Hips with its own position animation.** My old travel-cancel
pinned three levels of bones, which would have frozen the club in his hand. Now only the Hips are
pinned.

---

## What I changed (my part, done)

| File | Change |
|---|---|
| `Enemy/OniBoss.cs` | Reaction tiers mapped the right way round (10 → quick flinch, 20 → full react). Charge rebuilt: WINDUP in place (turns to you) → RUSH with the clip frozen on the lance frame while he drives across → on arrival the clip **jumps to the strike section** (landing + club slam) instead of running the dash on the spot. If he is already next to you at the end of the wind-up, no rush — straight to the slam. Travel-cancel now pins **only the Hips**, lives as long as the clip is on the animator, and reports what it did. Charge strike moment moved to the real club impact (0.76). Log file + telemetry + editor sanity checks (below). |
| `Enemy/OniDebugLogFile.cs` (new) | Mirrors the whole console into `<project>/OniLogs/oni_<date>.log`, plus charge telemetry at 12 lines/s. Folder sits next to Assets; Unity ignores it. Keeps the newest 8. |
| `Combat/EnemyCombat.cs` | One opt-in call: `SetAttackStrikeMoment(name, value)`. Nothing else. |

New knobs on OniBoss (all pre-set, touch only if it looks wrong):
`Charge Hold Normalized Time` 0.40 (frozen rush pose; 0.27 = grounded lance, 0.40 = airborne lunge),
`Charge Strike Normalized Time` 0.58 (where the clip resumes on arrival),
`Charge Strike Moment` 0.76, `Charge Travel Bone Name` "Hips", `Write Log File` on.

At Start the Oni now prints plain facts to the console/log: which bone is pinned and at what depth,
whether the charge clip still has a Root Motion Node, whether the react states look swapped,
whether KanaboSweep shares Idle's clip.

---

## Who does what

**[ME, done with your OK] the real charge fix.** I rewrote `Oni Charge.fbx.meta` as text: Root Motion
Node → None, Bake Into Pose ✔ Rotation + ✔ Position (Y). The block is copied from your Idle clip's
meta; only the flags differ. Unity reimports the file the moment it gets focus. Originals are in
`OniLogs/meta_backup_round5/` (copy one back to undo).

**[ME, done with your OK] the reaction clips.** Same two checkboxes written into the metas of
`Oni Hit react light`, `Oni Hit react Heavy`, `Oni Stagger`, `Oni Ground Pound` — their crouch / sag
/ jump come back into the pose. XZ stays extracted (in place), knockback keeps moving him. If a
reaction suddenly looks wrong, say so — one file back from the backup folder undoes it.

**[YOU] Y1 — optional, 1 drag.** ONICONTROLLER → state `KanaboSweep` → Motion → drag the
`Oni Kanabo sweep` clip in.

**[YOU] Y2 — test.** Press Play. Let him charge you from far away, and once from close (stand next
to him after one of his swings). Hit him with paws (10), strong paw (20), and one swirl. Then stop
Play. **Nothing to paste** — I read `OniLogs/oni_….log` from your project folder myself. If a screen
recording of the charge is easy, drop it in `Assets/Captures` as before.

---

## First test — read from your OniLogs (3 play sessions, 23:59 / 00:02 / 00:03)

- Compile clean, no exceptions, no safety warnings.
- Charge from 20 m: wind-up 0.56 s → rush 1.25 s at 14 m/s → strike, hit for 18 on the club impact.
  Mesh planted the whole time. **One bug:** on arrival the clip did not jump to the slam — it played
  the dash on the spot for half a second first (I used a seconds-offset API where a normalized one
  was needed). Fixed and pushed; the next charge should land and slam immediately.
- Hold-ground: backstep 1.5 m → watch → step-in → swing, every time. No orbit lines.
- Reactions: 10 → quick flinch, 20 → full react, 35 → stagger. Correct now.
- Still open for your eyes only: how it *looks* — the rush pose (0.35 = his current live value; the
  knob is Charge Hold Normalized Time) and the reactions with the Y-bake.

## What I look for in the log

```
[OniBoss:Layer] charge travel bone: 'Hips' at depth 1 under 'Oni' ...
[OniBoss:Layer] charge begin: travel bone 'Hips' ... drive windup→rush→strike ...
[OniBoss:Layer] charge RUSH: clip frozen at 0.40, 11.3m to go at 14m/s
[OniBoss:Layer] charge STRIKE: arrived 2.6m from the lock point after 0.79s real — clip 0.40 → 0.58 ...
[Oni] Hit player for 18 (Oni_Charge)
[OniBoss:Layer] charge end (clip left the animator) ... max raw drift 0.00m — clip is IN PLACE     ← the reimport took
   or                                              ... max raw drift 7.4m, max cancelled 7.4m — travel WAS baked in, pin held it   ← reimport not yet done, pin did the job
```
Plus the round-4 items I still have no verdict on: backstep after his swings, no orbiting, hold
watch, no Slam safety warning. They are all in the same file.

---

## Why the reaction clips got the same edit

Every Oni clip imported with *Root Transform Position (Y) → Bake Into Pose* **off**. In Unity that
moves the hips' up/down motion out of the pose and into root motion — which the Oni does not apply —
so the heavy react's 1.2 m crouch, the stagger's 0.7 m sag and the ground pound's 10 m jump were
flattened. With your OK those four clips (and the charge) now bake it into the pose. Idle / Walk /
Run / Watch / Alert / the club swings are untouched.

---

## Round 5b — from your "done" message (all mine, already on your disk)

| What you said | What I did |
|---|---|
| Jump swirl gives no reaction | **Rapid-hit burst.** The swirl is 8 fast 10-dmg ticks; the engine flinched once and flashed the rest. Now: tick 2 upgrades the flinch to the full stumble, tick 3 (30 dmg inside 0.8 s) **staggers him with the heavy push** — once per burst, so no lock. Knobs: `Burst Stumble Damage` 20, `Burst Stagger Damage` 30, `Burst Window` 0.8 s. |
| Phase 2 transition does not run | **Phase-2 beat.** At 50% HP he drops what he is doing, plays `Phase_Transition` full length (2.4 s), takes **no damage** during it, camera shake + a red ground ring at the roar (0.6 of the clip), the boss bar turns crimson at the roar, then he comes back (faster chase, more combos — those were already the engine's phase-2 numbers). Log: `PHASE 2: transition ...`, `PHASE 2: roar`, `PHASE 2: transition over`. |
| Yoru's reaction is late | **Strike moments measured from the clips.** The scene had ClubSwing2 resolving its hit at 0.5 of the clip; the club actually passes at 0.32 — the hit landed ~0.3 s after the visual. Now applied from code: Club_Swing 0.55, ClubSwing2 0.32, ClubSlam 0.52, KanaboSweep 0.48 (charge 0.76 as before). Yoru's own reaction fires the same frame the hit resolves — the lateness was the hit itself. |
| "What happens when both attack at once?" | **Trades / armor** (your pick): during his swing, light and medium hits still hurt him and flash him white but do **not** stop the club — whoever connects first wins the trade. Heavy hits (35+) and a swirl burst still stop him. Yoru stays interruptible: his hit cancels her attack. That is how Zelda tanks behave — you do not stop a Moblin's swing with a light hit, you dodge it. No "clash" animation is needed for this. Log: `Hit during Attack — flash only` while he swings; `Attack → HitReact` should no longer appear for 10/20-dmg hits. |

Also fixed after reading your first log: on arrival the charge did not jump to the slam (it played the dash on the spot for half a second) — corrected, the next charge lands and slams immediately.

Test again exactly like before (charge from far and from close; jump-swirl him; get him below half HP; hit him during his swings). Nothing to paste — I read the log.

---

## Round 6 — from your second "done"

| What you said | What I did |
|---|---|
| Phase 2: scene gets too dark | Not the Oni — it is **StormWeather** in the scene (Phase 2: fog 0.45 → 0.7, floor darkens to 0.65, rain ×2). Those are your scene numbers: **[YOU]** select the StormWeather object → `Storm Fog Density` 0.7 → try 0.5, `Wet Darkening` 0.65 → 0.8 (higher = lighter floor), `Storm Rain Multiplier` 2 → 1.5 if you like. |
| Transition looks nice but too short / hard to see | His Animator plays the transition at **x0.6** (2.4 s → 4 s) and he **holds the last pose 0.6 s** before coming back. Only his animator is slowed, never the world clock (Yoru's aim owns that). Knobs on OniBoss: `Phase Transition Anim Speed`, `Phase Transition Hold`. |
| Yoru should launch at the enemy on every hit, even from far | **Launch** on PlayerCombat (new, ON): every ground hit slides her toward the target at 20 m/s, up to 6 m, stopping 1 m from the enemy's collider **surface** (a 6 m boss is now reached like a small enemy), and the line-of-sight check aims at the enemy's body centre (a bumpy floor no longer hides him). "Grounded a moment ago" counts as grounded (the controller flickers on uneven floors — that alone could kill the slide). Re-targets every hit, so: launch, hit, launch, hit. Airborne attacks unchanged. Knobs: `Launch Max Distance` 6, `Launch Speed` 20, `Launch Max Duration` 0.32, `Targeting Range` 8 (existing). Log: `[ComboTrace] START … target='OniBoss' …` then `[ComboTrace] LAUNCH OniBoss dist=… in …s`. Turn `Launch Enabled` off = exactly the old hop. |

Test: LMB him from 3-6 m away a few times; take him under half HP for the roar; look at the storm knobs.

---

## Round 7 — the phase-2 freeze, determined from your log (not guessed)

**What the log says.** Phase 2 fired at 66.596 s. Normal staggers in that same session lasted
exactly 2.5 s (27.556 → 30.057, 45.347 → 47.848). The phase-2 one lasted **66.596 → 74.064 = 7.47 s**,
while the transition animation itself finished in about 2.4 s. That gap — roughly five seconds
standing still in the last pose — is the freeze you saw. Two mistakes of mine, both provable from
the numbers:

1. **The clip never slowed.** I slowed it with `animator.speed = 0.6`. The hit that triggered phase 2
   was a heavy (57 dmg) and heavy hits run a hitstop, which sets the animator speed to 0 and then
   restores **the value it saw when it started** — 1. So my 0.6 was wiped a moment later. Proof: the
   roar fired 1.48 s in, which is 60 % of a 2.42 s clip at **full** speed.
2. **The window was sized wrong.** I measured the clip with `AnimatorStateInfo.length` and divided it
   by the slow factor. That property is *already* divided by every speed multiplier, so I divided
   twice: 2.42 / 0.6 / 0.6 ≈ 7.3 s of window around a 2.4 s animation. 7.3 s + a little ≈ the 7.47 s
   in the log.

**Fix.** The transition is now *driven*, not played: every frame I write the clip's time myself from
a real-time clock, and the routine ends the window itself instead of waiting on a game-time timer.
That is immune to the hitstop, to Yoru's slow-motion, and to the length trap. It also can't run long:
one hard cap at 10 s inside the routine, and the freeze guard now watches the transition too (it used
to be exempt — that exemption is why nothing shouted about the 7.5 s).

Knobs: `Phase Transition Anim Speed` 0.6 (2.4 s clip → 4 s on screen), `Phase Transition Hold` 0.6 s.
Want it slower still? 0.45 gives 5.4 s. New log lines: `PHASE 2: clip is 2.42s → 4.63s on screen`,
`PHASE 2: roar at 2.42s`, `PHASE 2: transition over after ~4.7s real`.

## Round 7 — the rest

| What you asked | What I did |
|---|---|
| The Oni's attacks must snap to Yoru too, not hit air — especially combos | **Attack step-in.** Every melee swing drives him forward during its wind-up (first 30 % of the clip, 5 m/s, up to 3.5 m) and stops just inside his attack range. It re-runs on **every combo step**, so a first swing that came up short is followed by a second that closes the gap. The charge keeps its own drive. He may still only turn slowly while stepping, so side-stepping late still beats him. Knobs: `Attack Step In Max Distance / Speed / Stop Margin / End Normalized Time / Turn Speed`. Log: `step-in: 'ClubSwing2' closed 1.8m, Yoru now 3.0m away`. |
| Swirl + 4-leg shot should give a heavy knock-back | **Heavy knock-back react.** Big hits (4-leg air shot, any hit at/above the 25-dmg stagger threshold, and swirl bursts) now play `HitReact_Heavy` — the 1.5 s clip with the body thrown 1.1 m back — instead of the long fall-down Stagger clip, and the down time is re-timed to that clip (~1.8 s instead of 2.5 s). That clip existed in your animator and **nothing had ever used it**. Log: `heavy knock-back react (40 dmg HEAVY) → 'HitReact_Heavy', down for 1.80s`. |
| Yoru must visibly launch on every attack | Two numbers were hiding the launch that already worked: it moved her only as far as the gap allowed (his body radius is 1.4 m, so from 3.9 m only ~1.5 m of it is real distance) and it did that in 0.06-0.08 s — too fast to see. Now: **minimum 0.8 m forward on every ground attack** (her capsule simply stops on his body when she is already touching him) and **minimum 0.13 s** so the eye can follow it. Knobs: `Launch Min Distance`, `Launch Min Duration`, `Launch Speed` 20, `Launch Max Distance` 6. |
| The Nopperabō launch — did you overwrite it? | **No.** Nothing about Nopperabō was touched; there is no Nopperabō-specific script — the launch/magnet is the one shared system on PlayerCombat that every fight uses. But it IS shared, so round 6/7 changes his fight too: she can now start a launch from up to 6 m (was 2.5 m) and the slide takes 0.13-0.32 s (was 0.06 s). If Nopperabō felt right before and feels different now, the honest fix is one of: turn `Launch Enabled` off (byte-for-byte the old behaviour), or lower `Launch Max Distance` back to 2.5. Worth one pass in his scene. |
| The forward-launch animation (dodge-dash) | Parked, as you said — we think about it together. My worry is the same as yours: playing a dash clip before every punch adds a wind-up to every hit. Middle road for later: play it only when the launch is longer than ~2 m, blend it into the punch. The Spider-Man big-leap option is in the notes too. |
| Swirl still shakes / vibrates | Parked at your call — after the bigger ones. |
