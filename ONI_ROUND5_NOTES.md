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
