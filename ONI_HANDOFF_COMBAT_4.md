# ONI HANDOFF — COMBAT 4 (Yoru's attack launch, 2026-08-18 evening)

Read this first if you are a new session. It **replaces ONI_HANDOFF_COMBAT_3 for everything about
Yoru's attack launch**; COMBAT_3 is still correct for the Oni's own behaviour (charge, hold-ground,
reactions, phase 2). Project `/Users/asenahazal/Documents/Yoru`, Unity 6000.2.7f2, branch `Gamefeel`,
scene `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`.

## 0. STATUS AT HANDOFF — read this before you do anything

- `PlayerCombat.cs` and `OniBoss.cs` were last written **19:04:05 UTC**.
- `Library/ScriptAssemblies/Assembly-CSharp.dll` is from **18:59:19 UTC**.
- **=> Unity has NOT compiled the latest edit.** The running build does not contain: launch speed 10,
  engage distance 6 m, "do not touch the targeting cone", or the `LAUNCH RESULT` diagnostic.
- Both files pass a Mono syntax check with **0 errors** (references missing, so that is syntax only —
  Unity's compile has not been confirmed).
- **First action: get Unity to recompile, then check `Library/Bee/tundra.log.json` for `error CS`
  BEFORE Hazel plays.** She was burned once by testing an unverified build; do not repeat it.
- Backups of both files from before this round: `OniLogs/cs_backup_round8/`. One `cp` reverts.
- Nothing is committed. `git log -1` = `b8317075 aug18`. Both scripts are dirty (this round plus
  rounds 5b–7 which were already uncommitted).

## 1. How we work (Hazel's rules — keep them)

- **Manager mode.** State the plan BEFORE doing. Label every step **[YOU]** (Hazel) / **[ME]**.
  Minimise [YOU]. Simple English, concise. She tunes numbers herself — give her knob names.
- **Never assume. Ask her.** She has said this twice, emphatically. When her words are explicit,
  execute them; when a number or behaviour is genuinely undecided, ask before writing code.
- **Scope.** Shared scripts (`EnemyCombat`, `EnemyHealth`, `PlayerCombat`) only get **opt-in** APIs
  that default to the old behaviour. Everything Oni-specific lives in `OniBoss.cs`. Never change
  Yoru's combat abilities without her explicit OK (feedback and diagnostics are fine).
- **She does scene / animator / FBX edits. You do `.cs`.**
- **No pasting logs.** OniBoss writes `<project>/OniLogs/oni_<date>.log`. **Read it from her disk
  with device_bash after every test — she has told you to do this. Do not ask her how it felt;
  read the numbers.**
- **Facts, not theories.** Every claim must be traceable to a log line, a source file, or a
  measurement. Every fix must be verifiable in the next log.
- **Performance rule (standing):** no per-frame allocations, no Find/GetComponent in Update,
  editor checks Start-only.
- **Feel target:** Zelda. Yoru's air kit opens; the ground trade is where damage happens.

## 2. FOUR RULES LEARNED THE HARD WAY THIS ROUND — do not rediscover these

1. **A `[SerializeField]` default in code does NOT change a value Unity has already saved.**
   Changing `yoruTargetingCone = 120f` to `180f` in source did nothing — the scene kept 120, the
   log kept printing `Cone 120 deg`, and Hazel was told it was fixed when it was not. **Every
   number pushed at Start is now a `private const` in `OniBoss.cs`, not a SerializeField**, so this
   cannot happen again. If you add a tunable, either make her set it, or make it a const.
2. **Measure her actual play distances BEFORE proposing numbers.** An hour went into choosing a
   4.5 m engage threshold that applied to **3 of 51 attacks (6%)** and 0% of the session she then
   played. The distance distribution was already sitting in logs that had been read.
3. **A trace line that prints INTENT is not evidence of BEHAVIOUR.** `LAUNCH ... dist=2.98m` says
   what was requested, not what moved. The `LAUNCH RESULT` line added this round closes that gap.
4. **Verify the compile yourself before she plays.** `/Applications` is outside the shared folder
   so Unity's compiler is unreachable; use `mcs` in the agent container for a syntax check, then
   read `tundra.log.json` once Unity has rebuilt.

## 3. What Hazel asked for (verbatim intent, confirmed)

> Every attack and every combo step launches Yoru forward, like Zelda.
> - **No enemy** -> nudge forward, in the direction Yoru faces.
> - **Enemy close enough** -> the attack **must launch to the enemy**.
> - **Enemy too far** -> nudge forward only. There is a distance limit.
> - **There is NO minimum distance.** (She explicitly rejected the 0.8 m floor.)

This was checked against reference implementations (section 7). **Her ask is the industry-standard
pattern.** It is not wrong and should not be relitigated.

## 4. Code state after round 8

### `Assets/Scripts/Combat/PlayerCombat.cs`

New opt-in API (default OFF, so every other fight is byte-for-byte unchanged):

```csharp
public void ConfigureLaunch(bool noTargetLaunch, float nudgeDistance = -1f, float engageDistance = -1f,
                            float coneAngleDegrees = -1f, float minDistance = -1f, float stopGap = -1f,
                            float speed = -1f)
```
Negative = leave that Inspector value alone.

New fields: `launchWithNoTarget` (false), `launchNoTargetDistance` (0.9), `launchEngageDistance`
(4.5 in code, **overridden to 6 at Start by OniBoss**), `launchStopGap` (0).

`StartLunge()` now implements three cases:

| Case | Condition | Behaviour |
|---|---|---|
| **A** | no target found | step `launchNoTargetDistance` along `transform.forward` |
| **B** | target, surface gap <= `launchEngageDistance` | launch `gap - launchStopGap`, i.e. **all the way in** |
| **C** | target, surface gap > `launchEngageDistance` | step `launchNoTargetDistance` forward — **never a clamped part-way slide** |

The old `launchMinDistance` floor is gated behind `launchMinDistance > 0f` and is **pushed to 0** by
OniBoss. There is no minimum, as she required.

`LungeRoutine()` now records the start position and why the loop ended, and prints:
```
[ComboTrace] LAUNCH RESULT wanted=2.98m actually moved=2.98m — completed
                                                              — STOPPED BY LEDGE PROBE after 0.031s
                                                              — INTERRUPTED (dodge=True ...)
```

### `Assets/Scripts/Enemy/OniBoss.cs`

One `[SerializeField] bool configureYoruLaunch = true` (untick to A/B against the old behaviour),
plus **constants** pushed to PlayerCombat at Start:

```csharp
YORU_NUDGE_DISTANCE  = 0.9f    // BotW-derived, see section 7
YORU_ENGAGE_DISTANCE = 6.0f    // was 4.5 — her log showed 4.6m being rejected as "too far"
YORU_LAUNCH_MIN      = 0f      // NO minimum. She rejected the 0.8m floor.
YORU_LAUNCH_STOP_GAP = 0f      // all the way in; her capsule stops on his body
YORU_LAUNCH_SPEED    = 10f     // was 20 — 3m in 0.15s read as a teleport, not a launch
YORU_CONE            = -1f     // DO NOT TOUCH her targeting cone. 120 rejected him twice in 8 attacks.
```

## 5. Measured from the logs (facts, not impressions)

- **Attack distance to the Oni's collider SURFACE**, session 14:50 (27 targeted attacks):
  min 0.8 m, **median 1.3 m**, max 2.2 m. Centre distance: min 2.0, median 2.7, max 3.5.
- **21 of 30 launches moved exactly 0.80 m** — the old `launchMinDistance` floor — and at 0.8–1.3 m
  from a 1.4 m-radius boss that 0.80 m is absorbed by his collider. That is why the launch was
  invisible for weeks.
- Across two sessions, **3 of 51 attacks were beyond 4.5 m (6%)**.
- Session 15:00, after removing the floor and the stop gap: `LAUNCH OniBoss at 3.0m dist=2.98m`
  and `at 2.8m dist=2.78m` — gap and distance match, the launch is correct. Still at speed 20.
- Session 15:00 also shows `rejected dead=0 angle=1` **twice in 8 attacks** — the 120 deg cone
  throwing out an Oni who was standing next to her.
- Session 15:00: `STEP (too far: OniBoss at 4.6m > engage 4.5m)` — 10 cm over the line.
- **Input:** 31% (13:00 session) and 40% (10:52 session) of her clicks are discarded
  (`CLICK IGNORED (step=N queued=2)`), worst streak 6 consecutive. Parked at her call.

## 6. Open items

| # | Item | Owner | Notes |
|---|---|---|---|
| A1 | **Compile + verify, then test the launch** | ME then YOU | See section 0. Expect a visible ~3 m slide over ~0.3 s from 3–4 m out. |
| A2 | **Do airborne attacks launch?** | HAZEL — undecided | `StartLunge` returns early when not grounded. She said "each attack"; it has never been confirmed whether she means air attacks too. **Ask.** |
| A3 | Max launch travel is **3.2 m** (`speed 10 × launchMaxDuration 0.32`) | ME if she wants more | Between 3.2 m and 6 m she closes 3.2 m and stops short. Raising `Launch Max Duration` fixes it but the slide starts competing with the strike frame (~0.6 s into a 1.23 s combo clip). |
| O2 | **KanaboSweep animator state plays the Idle clip** | YOU | Drag `Oni Kanabo sweep` onto its Motion. Warned at Start in every log. He has **never used that attack in any session**. |
| C1 | **The charge has never landed. 0 hits, every session.** | HAZEL to approve | Diagnosed, untouched. He stops steering 0.35 s into a 1.2 s rush, so he commits ~15 m out and slams empty floor. Two windows total 1.18 s = 7.8 m of free movement; you need 2.3 m to escape. Proposed fix: commit by DISTANCE (~6 m) not time, and give the charge slam a larger hit radius than a standing swing. **Not approved. Do not implement without her.** |
| O6 | Turn off ComboTrace + verbose logs when closed | ME | `logComboTrace` on PlayerCombat, `showDebugLogs` on OniBoss/EnemyCombat. |
| I1 | 31–40% of clicks discarded | HAZEL parked | Queue depth 2; a press 0.02 s after the last is deleted rather than held. |
| F1 | **Unexplained freeze, 17:48 session** | open | Yoru stopped responding at t=7.39 s, immediately after `[TailAirShot4Leg] EXIT (landed, nothing drawn)` — she landed mid-aim on a 4-leg air shot. 10 s of silence at ~204 fps, so Unity was fine. No exception. `ComboTrace` count was **0**, so no attack ran and it was not the launch code. `FreezeDebugDumper.cs` and `showFreezeDebug` exist on PlayerMovement (currently **0** in the scene) — arm it if it recurs. |

## 7. Research reference (do not re-do this — it took ~10 minutes of agent time)

Primary sources, all verified in decompiled source or first-party dev posts.

- **Ocarina of Time** (`zeldaret/oot`, `z_player.c`): the step on a normal slash is **animation root
  motion, no constant exists**. The scripted lunge is `speedXZ = 15.0` decaying at `5.0`/frame,
  and it fires on **only two attacks** (forward stab while Z-targeting, spin-attack release).
  Targeting cone **60 deg** unlocked, 90 deg locked. Attention range 350 units acquire / 525 leash.
- **Twilight Princess** (`zeldaret/tp`, `d_a_alink_HIO_data.inc`): explicit per-swing forward speed —
  vertical cut **5.0**, side cuts **3.0**, stabs **10.0 / 8.0** units/frame, deceleration **2.2**.
  Rotational homing during slashes bails past **0x3000 = 67.5 deg**. Jump-attack homing lands
  **70 units short** of the target and clamps travel at **500 units**.
- **Breath of the Wild** (`zeldaret/botw` + zeldamods AIDef): `CutAddSpeedMax 0.15`,
  `CutAddSpeedDec 0.012` -> **~0.94 m of slide per swing** (derived; units not documented).
  `SwordSearchAngle 90 deg`, `SwordSearchFrame 6`. Dash attack `SearchAngle 60`.
- **THE FINALS S11 dev deep dive** — the only shipped melee lunge distance published by a developer:
  "lunge you up to around **3 m**" raised to "averages around **4.5 m**".
- **Every implementation skips the lunge when the target is too far. None clamp it.** HL2
  `npc_assassin` returns `COND_TOO_FAR_TO_ATTACK` past 1.5x the animation's travel; NOLF2's lunge
  goal does not fire outside 300–500 units; the UE5 melee prototypes clear the warp target and play
  a non-warping attack. **This is exactly what Hazel asked for.**
- **Compute the destination as a stand-off point on the line to the target**, not a travel distance —
  UE5 "Slash" `WarpTargetDistance 75 cm`, its fork 110 cm. Yoru already does better: she measures to
  the **collider surface** (`SurfacePoint`), which those projects do not, and which is what makes a
  6 m boss work.
- Facing gates in the wild: HL2 **41 deg**, OoT 60, TP 67.5, BotW 90, UE5 prototype 100.
  **Yoru's prefab ships 180**, which never rejects anything. Left alone at her request.
- Pitfalls already handled correctly in this codebase: `characterController.Move()` rather than a
  transform write (collision-safe), and a ledge probe before every step.

## 8. Log signatures to look for

```
[OniBoss:Layer] Yoru launch model ON: launches ALL THE WAY to an enemy within 6.0m of its surface
                at 10m/s (stop gap 0.00m, NO minimum distance), else steps 0.90m forward.
                Targeting cone left exactly as her prefab has it.
[ComboTrace] START step=1 state='Combo1' queued=0 target='OniBoss' at 4.3m
[ComboTrace] LAUNCH OniBoss at 3.0m dist=2.98m in 0.30s      <- gap and dist MATCHING = correct
[ComboTrace] LAUNCH RESULT wanted=2.98m actually moved=2.98m — completed     <- the proof
[ComboTrace] LAUNCH STEP (no enemy) dist=0.90m in 0.13s
[ComboTrace] LAUNCH STEP (too far: OniBoss at 7.1m > engage 6.0m) dist=0.90m in 0.13s
[ComboTrace] START ... target=none: 1 collider(s) in range, rejected ... angle=1   <- cone rejecting
```

Red flags: `actually moved` much smaller than `wanted`; `STOPPED BY LEDGE PROBE`; `rejected angle=`
with the Oni nearby; any `LAUNCH` where `dist` is a suspiciously round repeated number (that is a
floor or a cap, not real geometry).

## 9. Relationship note for whoever picks this up

Hazel has spent hours on this single feature and is at the end of her patience, for good reasons:
a shared-script edit was shipped without verifying the compile and she tested it and lost a session;
a cone change was reported as reverted when it was not; and an hour went into tuning numbers for a
situation that occurs in 6% of her attacks. **Read her logs before speaking. Show numbers, not
adjectives. Own mistakes in one line and move on. Do not ask her how it felt — the answer is in
`OniLogs/`.**
