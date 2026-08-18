# Oni — Action Plan (Gamefeel)

Project: `/Users/asenahazal/Documents/Yoru` · branch `Gamefeel` · 17 Aug 2026
Companion to `ONI_HANDOFF_COMBAT_2.md`. This doc is the agreed plan, not a status report.

---

## 0. The feel contract (agreed)

> Yoru's air kit creates the openings. The ground trade is where the damage happens.
> The Oni is a **slow tank that commits**: it turns in steps, never mid-attack, and whiffing
> costs it dearly. But it flinches visibly on **every** hit so the ground trade reads clearly.

Reference: Zelda. The five rules everything below serves:

1. Every hit stops time for 2–5 frames (hitstop).
2. Every hit shows a flinch, even small ones.
3. Enemies commit — once an attack starts it cannot turn.
4. Enemies never slide. Turning is discrete and animated, between attacks.
5. Big hits knock the enemy back, creating an obvious free-damage window.

---

## 1. Yoru's hit tiers (reference card)

| Yoru attack | Damage | Oni tier | Result |
|---|---|---|---|
| Melee jab | 10 | **LIGHT** (≤14) | quick flinch |
| Melee strong | 20 | **MEDIUM** (15–24) | full react |
| Tail arrow, 2-leg air shot | 20 | **MEDIUM** | full react |
| Beyblade / swirl (per tick) | 35 | **HEAVY** (≥25) | knockback + stagger 2.5s + ×1.5 punish |
| Tail arrow, 4-leg air shot | 40 (20 × 2.0) | **HEAVY** | knockback + stagger 2.5s + ×1.5 punish |

The damage bands are **correct** and are not being redesigned. Three things are miswired:

- The two react clips are **swapped**: LIGHT plays a state called `HitReact_medium`, MEDIUM
  plays `Hit_react_light`. So a light hit looks bigger than a medium one.
- Two different "heavy" definitions run at once: `EnemyCombat.heavyHitThreshold = 18` vs the
  OniBoss stagger threshold of 25.
- The reaction is chosen *after* the state already switched, so one frame of the generic
  react shows before the tier clip. That is the "late, always light" complaint.

---

## 2. Root causes found (evidence, not guesses)

### 2.1 The freeze — SOLVED, cause confirmed

`TailAimController` and `TailAimController4Leg` set **`Time.timeScale = 0.1`** while Yoru aims
(`TailAimController4Leg.cs:595`, `TailAimController.cs:466`), for up to **3 real seconds**.

`EnemyCombat.Update()` line 465 ticks every state timer on the **scaled** clock:

```csharp
if (stateTimer > 0)
    stateTimer -= Time.deltaTime;      // scaled
```

So while Yoru aims:

| State | Configured | Real duration at timeScale 0.1 |
|---|---|---|
| HitReact | 0.8s | **8 seconds** |
| Stagger | 2.5s | **25 seconds** |

The Animator is also `updateMode = Normal` (scaled), so the clip crawls too. The Oni was never
stuck — it was running at a tenth speed because Yoru was aiming. Aiming is her main ability, so
this happens constantly.

**Fixed** in `OniBoss.cs`: real-time ceilings on HitReact (1.2s) and Stagger (3.2s), measured in
`Time.unscaledTime`. No-op at normal speed. Plus a watchdog that logs one loud error if
`timeScale` ever stays below 1 for more than 6 real seconds — that would be a genuine leak, and
the log line will tell us which of the two freezes we saw.

### 2.2 The whole hit-feedback stack is missing from this scene

`CombatFeedbackManager` already implements everything asked for — hitstop (`Animator.speed = 0`,
correctly *not* timeScale), camera shake, hit VFX at the real contact point, post-process pulse,
FOV punch. `PlayerCombat.cs:2943` calls it on every landed hit.

But in the pasted log, **no `[CameraGameFeel] Shake` line appears on any of Yoru's hits** — only
on the Oni's attacks. `CombatFeedbackManager.Instance` is null: the manager object is not in
`CaveScene_Oni_Boss1`. The same is true for `CombatSFXManager` and `CombatPostProcessPulse`.

That single missing object is why Yoru's hits feel like nothing. It is one drag-and-drop.

### 2.3 Sliding and orbiting

`stoppingDistance 2.2` vs `attackRange 3.5` leaves a band where the Oni is too close to run and
too far to attack, so it circles. The log shows `run → Walk → run → Walk` repeating. It also
commits to attacks from 6m and whiffs (`Attack missed, player out of range (6.5m > 3.5m)`).

### 2.4 Vibration

`OniBoss.UpdateChargePin()` writes to 74 skeleton nodes in `LateUpdate`, after the Animator has
written the pose. If it stays armed outside the charge, Animator and pin fight every frame.

### 2.5 Still open from last session

`Attack/Telegraph safety transition fired — clip 'Club_Slam' may not have completed cleanly`
is still in this log. The **AnimSpeed multiplier binding is still missing** on at least the Slam
state.

---

## 3. Good news — assets that already exist

No new animation recording is needed for the next round. Already in the project:

- `Oni Ground Pound.fbx`
- `Oni Alert.fbx` (already wired, plays on aggro)
- `Oni Phase transition.fbx`
- `Oni Walk.fbx` (can be played in reverse for the step-back)
- `Oni Watch.fbx`, `Oni Stagger.fbx`, both hit-react clips

---

## 4. HAZEL'S PARTS — in order, minimised

### Step 1 — Animator, one pass (≈5 min) 🔴 blocks everything

Open `ONICONTROLLER`. For each of the **5 attack states**
(`Club_Swing`, `ClubSwing2`, `Club_Slam`, `Oni_Charge`, `KanaboSweep`):

- Select the state → in the Inspector, **Speed** row → tick **Multiplier / Parameter**
- Choose **`AnimSpeed`** from the dropdown

That's 5 ticks. Nothing else works properly until this is done.

### Step 2 — Swap the two hit-react clips (≈1 min)

In the same controller:

- State **`HitReact_medium`** → set its Motion to **`Oni Hit react lighter light`** (the short 0.79s clip)
- State **`Hit_react_light`** → set its Motion to **`Oni Hit react light`** (the 1.46s clip)

If they are already this way, tell me and I will flip the two state names in code instead.

### Step 3 — Put the feedback managers in the scene (≈2 min) 🔴 biggest feel win

The Oni scene is missing the combat juice managers. Either:

- Drag the `CombatManagers` object from another scene / the Project folder into
  `CaveScene_Oni_Boss1`, **or**
- Create an empty GameObject named `CombatManagers` and add these three components:
  `CombatFeedbackManager`, `CombatSFXManager`, `CombatPostProcessPulse`

Then confirm: hitting the Oni should produce a `[CameraGameFeel] Shake` line in the console.

### Step 4 — Test and send me the log (≈5 min)

Play, fight the Oni properly, quit, paste the whole console. Specifically check whether the line
`SLOW-MOTION STUCK` ever appears — that answers the last open question about the freeze.

**That is all.** Steps 5+ are mine. Anything later that needs you (dragging a VFX prefab into a
field I create) will be collected into **one** batch at the end, not spread out.

---

## 5. CLAUDE'S PARTS — in order

| # | Task | File | Status |
|---|---|---|---|
| M1 | Real-time freeze guard + slow-motion watchdog | `OniBoss.cs` | ✅ done |
| M2 | Single-call tiered reaction (choose tier *before* the state change) | `OniBoss.cs` | pending approval |
| M3 | Gate the charge pin to the charge state only (kills the vibration) | `OniBoss.cs` | pending approval |
| M4 | Stop the orbit: dead zone between stoppingDistance and attackRange; no rotation during Attack + Recovery; require real range before committing | `EnemyCombat.cs` (opt-in fields) | pending approval |
| M5 | Step-back-and-watch after a landed hit: no turning, reverse Walk 2 steps, hold Watch, then re-attack | `OniBoss.cs` | pending approval |
| M6 | Tiered knockback: light 0m, medium 0.3m, heavy 0.8m over 0.2s, then stagger plays where it lands | `OniBoss.cs` | pending approval |
| M7 | Yoru red flash on taking damage (Zelda-style), matching the enemy `FlashRed` already in `EnemyHealth` | player health script | pending approval |
| M8 | Ground Pound attack + radial shockwave ring VFX + heavy shake | `OniBoss.cs` + attack data | pending approval |
| M9 | Charge: big wave VFX on the rush | `OniBoss.cs` | pending approval |
| M10 | Phase 1 random single attacks / Phase 2 mostly combos, shorter gaps | `EnemyCombat.cs` (opt-in) | pending approval |

Scope rule holds throughout: shared scripts (`EnemyCombat`, `EnemyHealth`) only gain **opt-in
fields that default to the old behavior**. Everything unique to this boss goes in `OniBoss.cs`.
Yoru's combat abilities are not touched — M7 is feedback only, not an ability.

---

## 6. Order of play

1. **YOU:** steps 1–3 above (≈8 minutes total).
2. **ME:** M2 + M3 while you do that — both are `OniBoss.cs`, no conflict with your editor.
3. **YOU:** test, send log.
4. **ME:** M4 + M5 + M6 (the movement and weight pass) — the biggest feel change.
5. **YOU:** test, send log + a short screen recording.
6. **ME:** M7 → M10 (the new content pass).

Re-evaluate after step 4. Do not add new attacks before the trade feels right.
