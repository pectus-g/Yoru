# Yoru — Oni Combat Handoff #6
**Written:** 23 Aug 2026, after the round-37 session ran out of context.
**Project:** `Yoru` (Unity 6000.2.7f2, Built-in RP). Mounted for the agent at `$HOME/mnt/Yoru`.
**Read this top to bottom before touching anything. The first section is not optional.**

---

## 0. How to work with Hazel — the rules that actually matter

These are hers, in her words. Every round that ignored one of them went backwards.

- **"never NEVER assump alwasy ask me dont do thigns on you own"**
- **"tell me what did you understand first than fix when i understand that we are in ssame page"**
- **"i dont want any mistakes so dont do anythng before undersatnding or being sure"**
- **"i don twant you to anything until you understand how to fix and what i want"**

Operationally, that means:

- **Say your understanding back and wait for a yes.** Not a summary of the request — a statement of what the code will do. She will correct you, and she has been right every single time.
- **Lanes.** She does scene / Animator / FBX / prefab edits. You do `.cs` only. If a fix needs an Animator state Speed changed, that is a bullet point for her, not something you route around in code.
- **Never ask her how it felt. Read the numbers.** After every test, read the newest file in `OniLogs/` with `device_bash`. Do not ask her to paste logs.
- **Verify the compile yourself before she plays.** Copy the file into the agent container, `mcs --parse`, then after she recompiles check that the DLL mtime beats the source mtime and that `Library/Bee/tundra.log.json` has no `error CS`.
- **Back up before editing.** `OniLogs/cs_backup_round<N>/` — the convention is already there through round 37.
- **`device_bash` cannot delete.** `rm` fails on the mount. `mv` into a `_to_delete/` folder and tell her.
- **Performance rules for this project:** no per-frame allocations, no `Find`/`GetComponent` in `Update`, editor-only checks in `Start` only.

---

## 1. Where things stand

Solved and stable: the Zelda-style attack launch, the permanent freeze, air control, the mid-air dodge hang, the hitstop wiping animator speed, the Oni's reach and pace, and the hit-reaction pull + auto-facing (both **deleted**, not disabled).

**The one open problem is the wave.** It was built as a projectile — it flies, and it damages on arrival. That design guarantees the hit lands after the swing. Hazel has now rejected it outright. Her new spec is §2. Nothing has been implemented against it. **Do not start coding it until she confirms your understanding, including the open question in §2.3.**

---

## 2. THE CURRENT TASK — the redesigned wave

### 2.1 Her spec, verbatim

> "lets do like this and see, if the club hit to the yoru it will be huge hit, if wave hits to yoru half of the demage. lets ot do the waave as projectile it doesnt need to disappear but once that touches to the yoru yoru must show hit reaction with wave and club. if the club hits to the yoru wave can hit as well. can have 2 attacks. yoru can run by jumping from the wave. but at the moment i am not even seeing the wae and the hit that effects yoru immeadietly hit reaction are late in any conditon"

### 2.2 What that reads as (confirm this with her, line by line)

| Her words | Reading |
|---|---|
| "if the club hit to the yoru it will be huge hit" | Club damage stays large — it is the real hit. |
| "if wave hits to yoru half of the demage" | Wave damage = **half the club's**, derived, not a separate hand-tuned number. |
| "lets ot do the waave as projectile" | The wave stops being a travelling object whose *arrival* deals damage. |
| "it doesnt need to disappear but once that touches to the yoru yoru must show hit reaction" | The **visual** may keep going / persist. The **hit** must register on contact. |
| "if the club hits to the yoru wave can hit as well. can have 2 attacks" | Both can land on one swing. Club and wave are not exclusive. |
| "yoru can run by jumping from the wave" | Jumping is the counter — implies a **low, ground-height** wave she clears by being airborne. |
| "i am not even seeing the wae" | She still cannot see it. See §3 for why — it is her current settings as much as the code. |
| "hit reaction are late in any conditon" | Late in *every* condition, club included. **§4 has a club-side cause she has not been told about yet.** |

### 2.3 The open question — ask this before writing anything

Her two requirements pull against each other and only she can pick:

> If the wave is not a projectile, its damage has to resolve **at the swing's strike moment** — one instant area check, with the visual as pure decoration that keeps travelling afterwards. That is the only way the hit is never late.
>
> **But if it resolves in one instant, jumping only saves her if she happens to be airborne at exactly that instant.** "Yoru can run by jumping from the wave" implies a window she can be inside of.

The two candidate answers to put to her:

- **(A) Instant, at strike moment.** Wave damage lands with the club's damage, same frame. Zero lateness, guaranteed. Jump counter = she must already be off the ground when he strikes — a read-and-react on the windup, like a Zelda ground-pound. Visual spawns at the same instant and travels outward as decoration only.
- **(B) A short armed window.** The wave is a moving *hitbox* for ~0.3–0.4 s after the strike moment, low to the ground, and it checks her grounded state. Jumping any time inside that window clears it. Slight lateness by design (up to the window length), but the jump counter works the way she described.

**(B) matches "yoru can run by jumping from the wave" better. (A) matches "hit reaction are late in any conditon" better.** Do not pick for her.

Also worth telling her: at **half** the club's damage the wave becomes a chip-damage threat, which argues for (B) — a small, dodgeable, frequent hit rather than a second big one.

---

## 3. Why she cannot see the wave — her current serialized values

**These live in the scene, not in code.** Changing the defaults in `.cs` will not move them. Read them from
`Assets/Scenes 1/CaveScene_Oni_Boss1.unity`, lines ~3427–3491.

```
swingWaveEnabled: 1
swingWaveRadius: 5.5        <-- DEAD FIELD, delete from OniBoss.cs
swingWaveHalfAngle: 60      <-- DEAD FIELD, delete from OniBoss.cs
swingWaveDamage: 8
swingWaveSpeed: 3           <-- too slow
swingWaveHitRadius: 1.2
swingWaveStartDistance: 0.3 <-- inside his ~1.4 m body; it is born hidden
swingWaveSpawnHeight: 0.8   <-- she lowered this from 1.2 herself
swingWaveTravel: 12
swingWaveVisualPlaybackSpeed: 1
```

Speed 3 × travel 12 = **4 seconds of flight**, and he swings roughly every 1.5 s. Measured travel-to-contact
distances from the last session were 1.5 / 3.0 / 1.6 / 2.7 / 1.7 / **11.4** / **7.5** / 1.6 m — the 11.4 m and
7.5 m hits are waves from swings that finished several seconds earlier. That is the "second time, very late"
she reported.

`swingWaveSpawnHeight: 0.8` is a useful signal: she is already trying to get it low. A jump-over-it wave wants
to be lower still (~0.3–0.5) and probably wider than `hitRadius 1.2` is tall.

**The serialized-value trap has cost ~4 rounds in this project, in three forms:**

1. Saved in the **prefab** (`environmentMask: m_Bits: 256`).
2. Saved in the **scene** as an override (`strikeMomentOverrides`, `swingWaveSpeed: 3`).
3. Held **only in the open editor's memory** — `dodge4LegSkipWindup` kept logging its old value after the
   revert compiled and nothing on disk held it.

**The fix is always the same: rename the field (new name → new default) or delete it.** Never assume a code
default is what is running.

---

## 4. Measured facts — including two she has NOT been told

Source: `OniLogs/oni_2026-08-22_20-48-51.log`, the newest run.

**Hit reactions are not late in code.** 21 of 21 `[HitReactTrace]` lines resolve in **0 ms** (one outlier at
42 ms). Layer weight 1.00, animator.speed 1.00. The reaction fires the instant it is asked for. The lateness
is entirely in *when it gets asked*.

**NEW — the club's own strike moments are wrong and inconsistent.** From `[OniBoss:Strike]`:

| Attack | strikeMoment set | Club actually closest at | Verdict |
|---|---|---|---|
| `Club_Swing` | 0.85 | clip 0.40–0.43 (when she is 1.5–3.1 m away) | damage fires **350–378 ms after** the club passed her |
| `Club_Swing` | 0.85 | clip 0.92 (when she is 2.55 m away) | 58 ms before — matched |
| `ClubSwing2` | 0.32 | clip 0.28–0.30 | matched, ±46 ms |
| `ClubSlam` | 0.52 | clip 0.85 (when she is 0.21–0.25 m away) | damage fires **302–307 ms before** the club lands |

Read that carefully: the "closest approach" measure is distance-based, so when she is far away it reports the
windup rather than the strike. But the two clean close-range samples are the ones that matter — **`ClubSlam`
damages her 300 ms before the club arrives**, and `Club_Swing` at 0.85 is landing after the club has passed on
every mid-range sample. This is a club-side source of "hit reaction feels wrong / late" that is independent of
the wave entirely. Worth raising with her as its own item.

**NEW — the swing trails start at the end of the swing.** From `[OniBoss:Trail]`:

```
ClubSwing2 trail START at clip 1.00 (transition still blending)
Club_Swing trail START at clip 0.67 (transition done)
ClubSlam   trail START at clip 1.00 (transition done)
```

The trail is being started when the clip is 67–100% done. That is exactly her complaint that "TRIALS ARE
GETTING LOST abit late". The `clip 1.00` readings are the *outgoing* state being sampled mid-transition, so
the start is gated on something that resolves too late. This has a trace on it but has never been diagnosed.

**Wave settings are the reason she cannot see it** — see §3.

---

## 5. File map

| File | Size | Role |
|---|---|---|
| `Assets/Scripts/Enemy/OniBoss.cs` | 123 KB | Oni-specific layer. Wave launch site at **~line 1042**. VFX rows, strike overrides, trail, measurement rig. |
| `Assets/Scripts/Enemy/SwingWaveProjectile.cs` | 8 KB | The projectile. **This is the file the redesign rewrites or replaces.** |
| `Assets/Scripts/Combat/EnemyCombat.cs` | 128 KB | Shared enemy combat. Strike fires on `AttackClipProgress() >= strikeMoment`. `attackerPos` is the Oni's position. |
| `Assets/Scripts/Combat/PlayerCombat.cs` | 193 KB | Player combat. Launch, dodge/dash, hit reactions. |
| `Assets/Scripts/Player/PlayerMovement.cs` | 36 KB | Movement, air control, the freeze fix + stuck-jump rescue. |
| `Assets/Scripts/Combat/CombatFeedbackManager.cs` | 16 KB | Hitstop. Captures/restores animator speed. |
| `Assets/Scenes 1/CaveScene_Oni_Boss1.unity` | — | **Holds all the live tuning values.** |

### The two code sites the redesign touches

`SwingWaveProjectile.OnDestroy()` — this line is why waves hang frozen in the air after they hit. It was added
in round 31 to stop the effect being cut off, and it created the bug she reported:

```csharp
private void OnDestroy()
{
    if (visualGO != null) visualGO.transform.SetParent(null, true);
}
```

`OniBoss.cs` ~line 1040 — one wave at a time, added round 37:

```csharp
if (activeWave != null) Destroy(activeWave.gameObject);

activeWave = SwingWaveProjectile.Launch(
    prefab, origin, transform.forward,
    transform, playerT, playerHealthRef,
    swingWaveSpeed, travel, swingWaveHitRadius,
    Mathf.Max(0f, strikeContactBodyHeight + burstOffset),
    swingWaveDamage,
    hitPrefab, hitLandVFXLifetime, hitLandVFXOffset,
    life, tilt, swingWaveVisualPlaybackSpeed);
```

17 parameters, verified matching. If you change the signature, re-verify with a **paren-aware** argument
splitter — a naive comma split miscounts `Mathf.Max(0f, x)` as two arguments and will give you a false
mismatch.

---

## 6. Parked, agreed, not started

- **Round B** (she agreed to this): trail from his feet on the charge, a hit VFX for the charge, and a fast
  randomised follow-up attack after the charge ~90% of the time with sharp transitions.
- **The charge has landed 0 hits, ever.** Diagnosed in handoff #3. Needs her go-ahead.
- **Delete the dead `swingWaveRadius` / `swingWaveHalfAngle` fields** from `OniBoss.cs`.
- **`KanaboSweep` plays the Idle clip** — her Animator lane. He has never used it in a logged session.
- **The combo finisher cannot be queued into** — 4 gates in the shared combo state machine.
- **The 4-leg front flip's slow start** — needs `Dodge_4Leg` state Speed ≈ 1.3 in the Animator. Her lane. Five
  rounds were burned tuning code for this before measuring; do not restart it in code.
- **Turn the verbose logging off** once the feel work is done: `[ComboTrace]`, `[OniBoss:Strike]`,
  `[OniBoss:ClubPos]`, `[OniBoss:Trail]`, `[DodgeTrace]`, `[OniBoss:Wave]`, `[HitReactTrace]`.

---

## 7. Hazel's side — how to test whatever gets built next

- Open `CaveScene_Oni_Boss1` and press Play.
- Let the Oni land **each** of `Club_Swing`, `ClubSwing2` and `ClubSlam` on you at least twice — once standing
  right on top of him, once from 2–3 m out.
- Then do the same three attacks again but **jump** as he starts each swing.
- Stop Play. Do not clear the console — the log file writes itself to `OniLogs/`.
- Say "tested" in chat. The agent reads the file. Do not paste anything.

---

## 8. The traps that have already cost rounds — do not re-enter them

1. **Tuning before measuring.** Five rounds went into front-flip curves; the trace then showed every flip
   exits at 85% of its clip through a designed early-exit. Measure first, always.
2. **The serialized-value trap** (§3). Rename or delete — never trust a code default.
3. **Layer masks.** The launch was dead for 11 attempts because `environmentMask` was layer 8 "Ground" and the
   cave terrain is on layer 0 "Default". A separate `edgeGroundMask` fixed it, with a fail-open probe.
4. **Reading her too fast.** "in eey attack yoru turn back to the oni" meant Yoru turns her *back to* him —
   not "turns to face". A misread cost a whole round. Say it back before you build.
5. **Fixing a symptom into a new bug.** Round 31 detached the wave visual on destroy to stop it being cut off,
   which created "waves don't disappear". Round 37 then added one-wave-at-a-time on top. Prefer deleting the
   mechanism over stacking guards on it.
