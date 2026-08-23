# ONI HANDOFF — COMBAT 5 (2026-08-22)

Replaces **ONI_HANDOFF_COMBAT_4** completely. COMBAT_4's headline problem (Yoru's attack launch not
moving her) was **solved in the first hour of this session** — do not re-litigate it.

Project `/Users/asenahazal/Documents/Yoru`, Unity 6000.2.7f2, branch `Gamefeel`,
scene `Assets/Scenes 1/CaveScene_Oni_Boss1.unity`.

---

## 0. HOW WE WORK — Hazel's rules, learned the hard way

- **State the plan before doing. Label [YOU] / [ME]. Minimise [YOU].** Simple English, concise.
- **Never assume. Ask.** When she says something explicitly, execute it. When a number or a
  behaviour is genuinely undecided, ask first. She will tell you when you have misread her — say
  your understanding back to her before writing code.
- **She does scene / animator / FBX edits. You do `.cs`.**
- **Read her logs yourself.** `OniLogs/oni_<date>.log`. Never ask her to paste anything, never ask
  how it felt. The numbers are on disk.
- **Verify the compile before she plays.** `/Applications` is outside the shared folder, so use
  `mcs --parse` in the agent container, then confirm Unity's own rebuild: DLL newer than the
  source, and no `error CS` in `Library/Bee/tundra.log.json`.
- **Facts, not theories.** Every claim traceable to a log line or a source file. Every fix
  verifiable in the next log.
- **Measure before changing.** See §2 — this is the single biggest lesson of this session.
- Performance: no per-frame allocations, no Find/GetComponent in Update, editor checks Start-only.
- Shared scripts (`PlayerCombat`, `EnemyCombat`, `PlayerMovement`) get opt-in switches that default
  to the old behaviour — unless she explicitly asks for a global change.

---

## 1. THE SERIALIZED-VALUE TRAP — it cost four separate rounds

**A `[SerializeField]` default in code cannot change a value Unity has already stored.** This bit us
in three different forms. Recognise all three:

| form | where it hides | how to spot it |
|---|---|---|
| Saved in the **prefab** | `Assets/Scenes 1/PlayerYoru_1.1.prefab` | grep the field name |
| Saved in the **scene** (prefab-instance override) | `CaveScene_Oni_Boss1.unity` | grep the field name |
| Held **only in the open editor's memory** | nowhere on disk | grep finds nothing, yet the running build still uses the old value |

That third one is vicious. `dodge4LegSkipWindup` was reverted to 0 in code, the DLL rebuilt, and
nothing was saved anywhere — but the log still reported `skipped 0.18s`, because Unity carries
serialized values across a recompile for objects already loaded in the scene.

**The reliable fix: RENAME the field.** A renamed field is a new field, so the code default applies
and the stale value is dropped. Used deliberately for `swingWaveStartDistance`, `nudgeForward`,
`nudgeUp`. Deleting the field entirely works too, and is better when the feature was a mistake.

**Fields that are pushed at Start and therefore need Stop→change→Play:** `strikeMomentOverrides`,
`oniChaseSpeed`, `clubBoneNameContains`. Everything in the Swing Wave section is read per spawn and
IS live in Play. Tell her to **Copy Component → Stop → Paste Component Values** to keep Play-mode
tuning.

---

## 2. MEASURE FIRST. THE THREE TIMES I DID NOT, I MADE THINGS WORSE

- **The flip.** Five rounds tuning a movement curve. The trace then showed every flip ends at
  **85% of its clip** via the designed early-exit, so I had been tuning the last 15% of a move that
  never plays. Then a code-side clip offset introduced a **pose-jump stutter** worse than the
  original complaint.
- **The air-dodge fall.** "Natural gravity" seeded from her existing fall speed produced **~12 m of
  drop** — nearly double the 6.7 m bug it replaced.
- **The swing wave.** Anchored to the club without checking where the club is. At the strike moment
  on `Club_Swing` the club is *behind him*, so the wave was born inside his own body.

Every one of those was solved the moment a measurement existed. **Add the trace, get one session of
data, then change one thing.**

---

## 3. WHAT IS FIXED AND PROVEN (do not undo)

### The launch (COMBAT_4's open problem) — SOLVED
`GroundAhead()` raycast against `environmentMask`, which was **layer 8 "Ground" only**, while the
cave's `Terrain` sits on **layer 0 "Default"**. Nothing in the scene was on layer 8, so the probe
found no floor anywhere, called it a cliff, and cancelled **11 of 11** launches at `0.00m` travelled.
`DemoScene_Day`'s terrain IS on Ground, which is why the Noppera-bō fight always worked — Hazel's
own comparison found this.
Fix: the edge probe has its **own** mask (`edgeGroundMask`, Everything), separate from the
line-of-sight mask; it scans all hits, not just the nearest; and it stands down with a loud warning
if it cannot see the floor under her feet.

### The permanent freeze — SOLVED
`PlayerState.Jumping` is set in `PerformJump` and cleared in **exactly one place**, `OnLanded()`.
Landing is deferred while a combat action runs (`pendingLanding`), and **a single frame of ground
flicker cancelled that retry**. `Jumping` then stuck true forever, and `ApplyMovement` matched
neither branch → no walking, no turning, no jumping, permanently. Attacks still worked, which is why
none of PlayerCombat's four safety nets ever fired.
**Repro that finds it every time: jump, then front-flip.**
**Log fingerprint: a `JUMP` line with no matching `✅ LANDED!`.**
Fix, three layers: the flicker no longer cancels the deferred landing (needs real airtime);
`ClearAirborneState()` restores control on touchdown by every path; and a `STUCK-JUMP RESCUE`
watchdog logs loudly and forces the landing if it ever happens again. **It has not fired since.**

### Also fixed
- **Hitstop was flattening animator speed to 1.** `CombatFeedbackManager` restored a hardcoded `1f`
  instead of the speed it froze, silently wiping the Oni's 1.35. Now restores the captured value.
- **Air control** — the project had none. Airborne movement replayed a takeoff snapshot and rotation
  existed only in the grounded branch. Now: live steering, air rotation, and a jump taken during an
  axis flip keeps its direction.
- **Air dash holds height; air flip falls gently** (`airDodgeFallMultiplier` 0.45).
- **Dash speed** — was pinned at exactly 1.000 s because the code sampled the animator one frame
  after the crossfade and read the state she was *leaving*. Now `dashMoveDuration` = 0.4 s, explicit.
- **The hit-reaction pull.** Every hit dragged her 0.5 m *toward* the attacker
  (`attackerPos - herPos`). Off. Auto-facing on hit also off — nothing turns Yoru but the player.
- **Oni pace** — `oniAnimationSpeed` 1.35, chase 4.5/5.5. This alone fixed his reach: his club
  reached her on **36% → 83%** of swings, `Attack missed` 10 → 2.

---

## 4. THE MEASUREMENT RIG (leave it in until the feel work is done)

| trace | what it answers |
|---|---|
| `[ComboTrace] LAUNCH RESULT wanted=Xm actually moved=Ym — reason` | did the launch move her, and what stopped it |
| `[OniBoss:Strike]` | where the club is closest to her vs where damage fires |
| `[OniBoss:ClubPos]` | the club's real position at the strike moment, in his local frame |
| `[OniBoss:Trail]` | when the trail starts/ends, as a clip position |
| `[DodgeTrace]` | flip clip length, run time, dead tail |
| `[OniBoss:Wave]` / `swing wave born …` | where the wave spawned, its flight time, whether it carried damage |
| `STUCK-JUMP RESCUE` | the freeze, if it ever returns |
| `✅ LANDED!` vs `JUMP` | the freeze fingerprint |

Switches: `logComboTrace`, `logDodgeTiming`, `logClubPositionAtStrike`, `showDebugLogs` (PlayerCombat),
`showDebugLogs` (OniBoss), `logAirPin`. **Item O6: turn these off when the feel work is finished.**

---

## 5. THE SWING WAVE — current design

At each swing's strike moment the Oni launches a **travelling wave** (`SwingWaveProjectile.cs`,
new this session) carrying the per-attack slash prefab. It flies along his forward and, if it
reaches her, deals `swingWaveDamage` (8) **with a full hit reaction**.

- Collision is a **point-to-segment distance test**, not a physics query — no layer masks (see §3
  for why that matters here), and a fast wave cannot tunnel past her between frames.
- **It can never double-hit.** If the club already connected, the wave spawns and flies with
  damage 0.
- Geometry that must hold: born at `swingWaveStartDistance` (2 m) — **above ~1.4 m** or it is inside
  his body, **below 3.5 m** or it starts past her, since 3.5 m is his club's reach.
- `Swing Wave Radius` (5.5) and `Swing Wave Half Angle` (60) are **dead leftovers** from the old
  instant-radius version. Delete them.

Per attack (`Swing Wave VFX By Attack`): `Vfx` (the travelling wave), `Trail Vfx` (rides the club
for the whole swing), `Hit VFX` (bursts on her body), `Nudge Forward/Up/Right`, `Tilt X/Y/Z`
(**Tilt Z is the roll that makes a slash diagonal**), `Burst Height Offset` (**0 = mid body for the
swings, −0.8 = the floor for the slam**), `Lifetime`.

**Hazel does not want a procedurally generated effect** — one was built and rejected. She drags her
own prefabs.

---

## 6. OPEN, IN HER PRIORITY ORDER

| # | item | state |
|---|---|---|
| **A** | **Round B of the swing/charge split**: charge trail from his feet, charge hit effect, and a **fast randomised follow-up attack after the charge ~90% of the time**, with sharp transitions. Agreed, not started. | **next** |
| **B** | Reactions "come late". **Measured 21/21 at 0.0 ms**, wave hits included — it is not response time. Prime suspect: `Club_Swing` damage still fires before the club arrives. Two measurements put arrival at clip **0.82–0.91**; she has it at **0.75**. Try 0.85. | evidence gathered |
| C | The **4-leg flip's slow start**. Not fixable from code — any clip offset causes a pose jump. The lever is the `Dodge_4Leg` state's **Speed** in the Animator (~1.3). Parked by her. | her lane |
| D | Trail timing — trace added, no data read yet. | needs one session |
| E | The **combo finisher cannot be queued into**, so the chain always breaks after hit 3. Four gates in the shared combo state machine. | agreed, not started |
| F | **The charge has landed 0 hits, ever.** Diagnosed in COMBAT_3, never approved. | needs her call |
| G | `KanaboSweep` still plays the **Idle clip**. He has never used that attack in any logged session. | her lane, one drag |
| H | Delete `Swing Wave Radius` / `Half Angle`; turn the verbose logs off (O6). | cleanup |

---

## 7. RESEARCH ALREADY DONE — do not redo (≈10 min of agent time)

OoT `z_player.c`: the slash step is **root motion, no constant**; the scripted lunge is
`speedXZ 15.0` decaying `5.0`/frame on **two attacks only**. Targeting cone 60° unlocked / 90° locked.
TP `d_a_alink_HIO_data.inc`: per-swing forward speed — vertical cut **5.0**, side **3.0**, stabs
**10.0 / 8.0** units/frame, decel **2.2**; rotational homing bails past **67.5°**.
BotW: `CutAddSpeedMax 0.15` / `CutAddSpeedDec 0.012` → **~0.94 m of slide per swing**;
`SwordSearchAngle 90°`. THE FINALS S11: shipped melee lunge **~3 m raised to ~4.5 m**.
**Every implementation skips the lunge when the target is too far. None clamp it** (HL2
`npc_assassin`, NOLF2, UE5 melee prototypes). Facing gates in the wild: HL2 41°, OoT 60, TP 67.5,
BotW 90, UE5 100. **Yoru ships 180 — left alone at her request.**

---

## 8. RELATIONSHIP NOTE

Hazel is precise, tests everything, and will tell you plainly when you have made it worse — she has
been right every time she said so. She asks you to say your understanding back before you build,
and that habit has caught real misreadings. She tunes numbers herself: give her the knob name, the
current value, and what direction to move it.

Own mistakes in one line and move on. Do not pad. Do not ask how it felt — read `OniLogs/`.
