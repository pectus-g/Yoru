# ONI HANDOFF — COMBAT 7
**Project:** Yoru · Unity 6000.2.7f2 · Built-in RP · `/Users/asenahazal/Documents/Yoru`
**Covers:** rounds 53–70 (phase-2 entrance cinematic, sky show, weather, arena work)
**Written:** 2 Sep 2026 · **Previous:** `Handoff/ONI_HANDOFF_COMBAT_6.md`

---

## 0. HOW TO WORK WITH HAZEL — READ THIS FIRST

Her rules, in her words, and they are not negotiable:

- *"never NEVER assump alwasy ask me dont do thigns on you own"*
- *"tell me what did you understand first than fix when i understand that we are in ssame page"*
- *"i dont want any mistakes so dont do anythng before undersatnding or being sure"*
- *"i don twant you to anything until you understand how to fix and what i want"*

**Lanes.** She does scene / Animator / FBX / prefab / art work. You do `.cs` code only.

**Every round:**
1. Ask before building. Present understanding first, get a yes, then code.
2. Back up the files you will touch to `OniLogs/cs_backup_round<N>/` **before** shipping.
3. After she says "compiled", verify it *yourself*: DLL mtime newer than the sources in
   `Library/ScriptAssemblies/Assembly-CSharp.dll`, and zero `error CS` in `Library/Bee/tundra.log.json`.
4. After she tests, read the newest `OniLogs/oni_*.log` yourself. Never ask her to paste it.
5. Simple English. She is not a native speaker and says so.
6. Keep code fast — no per-frame allocations, no `Find`/`GetComponent` in Update.

**The serialized-value trap.** A field already saved in the scene ignores your new code default.
When you change a default that matters, **rename the field** so Unity treats it as new. This has
bitten us at least four times (Fov Start 90 survived three rounds and caused a whole bad week).

---

## 1. THE HARD LESSON OF ROUNDS 67–70 — DO NOT REPEAT IT

Rounds 67, 68, 69 and 70 were four camera rebuilds in a row. **She reverted all of them.**
Every one had her approval, but the approvals were for *plans I could not verify*. The camera had
no measurement, so each round fixed a symptom and created a new one. It cost about a week.

Her words: *"it has been a week that we are trying to fix this issue"*, and
*"doing things without my approval or without reading well rushing thoguth result just making us set back"*.

**Rules that come out of it:**
- **Measure before you change.** If you cannot see the result, add telemetry FIRST, ship only that,
  read it, and only then change behaviour.
- **One change per round** when the thing being tuned is visual.
- Ask her for a **screenshot**. It works — the two screenshots she sent late in the session solved
  in one message what four rounds of maths had not.
- Do not stack systems. The camera ended up with six subsystems fighting (orbit, ride height,
  push-in, FOV ramp, auto-widen, sky tilt). That is what "worse than before" meant.

---

## 2. CURRENT STATE — WHAT IS ON HER MAC RIGHT NOW

| File | Size | Version |
|---|---|---|
| `Assets/Scripts/Enemy/OniBoss.cs` | 225,695 | **round 67** (she reverted 68/69/70) |
| `Assets/Scripts/Combat/EnemyCombat.cs` | 146,286 | round 53 |
| `Assets/Scripts/Combat/PlayerCombat.cs` | 197,250 | round 49 |
| `Assets/Scripts/Enemy/SwingWaveProjectile.cs` | 12,930 | round 43 |
| `Assets/Scripts/Player/PlayerMovement.cs` | 36,284 | round 42 |
| `Assets/Scripts/Player/PlayerHealth.cs` | 7,826 | round 53 (cinematic guard) |
| `Assets/Scripts/Camera/CameraGameFeel.cs` | 23,592 | round 61 (letterbox + screen flash + cine pose) |
| `Assets/Scripts/Camera/ThirdPersonCamera.cs` | 12,926 | round 64 (hold-U sky peek) |
| `Assets/Scripts/Combat/CombatMusicManager.cs` | 12,904 | round 55 (boss track channel) |
| `Assets/Scripts/Combat/StormWeather.cs` | 17,881 | round 65 (COZY bridge + StrikeAt + ThunderNow) |
| `Assets/Scripts/Enemy/BossHealthBarUI.cs` | 26,054 | round 58 (HideInstant) |
| `Assets/Scripts/ArenaClearanceGizmo.cs` | 5,307 | editor tool, new |

Backups live in `OniLogs/cs_backup_round<N>/` — **backup N holds the code from BEFORE round N.**
`cs_backup_round68` therefore holds the round-67 code that is currently live.

**Nothing is broken.** Everything compiles and runs; the entrance plays end to end.

---

## 3. WHAT THE PHASE-2 ENTRANCE DOES TODAY

Sequence, all verified in logs:

1. **Phase 2 triggers** (boss at 50% HP) → HP snapped to exactly half (`phase2StartsAtHalf`).
2. **Hard cut** — cinematic camera takes over, letterbox bars slide in (instant), Cut SFX plays,
   boss bar hidden instantly, Yoru's combat force-reset (`ForceResetCombat`), world slows to 0.45.
3. **The roar** — driven clip at `cineRoarSpeed`, roar SFX at the scream, then `cineRoarLinger`.
4. **The jump** — `cineRiseSpeed` until the beat window.
5. **The lightning beat** (clip frames 27–37, world dives to 0.20):
   storm break released → COZY switches to the phase-2 weather → the **sky show** runs:
   9 converging bolts at sky height marching from 14m inward, twin bolts, sky-fill instances,
   streamers, 2 COZY thunders (flash + rumble), club prefab at the call AND at the climax,
   white screen flash, big shake.
6. **The hang** — 2s frozen at the peak, camera drifts.
7. **The drop** — full speed, camera rides him down.
8. **The slam** — control returns, bars out, protection off, ring fires (45 dmg, jumpable),
   phase-2 music drops, 2s first-attack grace.

**Yoru during the cinematic:** frozen (`ApplyStun` refreshed per frame) and untouchable
(`PlayerHealth.SetCinematicGuard`, GATE 0.7). Both expire by themselves — nothing can leak.

---

## 4. THE OPEN PROBLEM — AND THE AGREED PLAN

**The problem:** the cinematic camera clips into rocks and walls and cannot find a good angle.

**The root cause, measured:** the Oni's spawn point at world **(481.8, 1.0, 455.4)** has only
**~4.5m of clear space** (pivot-based measurement; the real figure may be better — see §6).
The camera needs ~14m to orbit. **No camera code can fix a boss standing in a 4.5m gap.**
This is the single most important fact in this document.

**The agreed plan (she chose it, not yet built):**

- She places `CineStageMark` — **already exists in the scene at (482, 1, 455)** with the
  `ArenaClearanceGizmo` on it.
- On the hard cut, the code **moves the Oni to the mark** (and possibly Yoru with him, keeping
  their relative positions), snapped to the NavMesh. A cut hides repositioning completely — this
  is standard practice in Souls/God of War.
- Then the orbit runs in open space and **cannot** hit a wall.

**Measured best open spot:** **(482, 1, 415)** — about **41.7m of clearance**, 40m south of his
spawn, right next to Yoru's spawn (474.5, 2.0, 427.2). Alternatives: (494, 1, 417) and (470, 1, 417),
both ~40m clear.

**Her constraint:** *"I CAN NOT MOVE THE STAUTES THEY WILL STAY THERE"* — statues stay. The
`PillarA` and `IvyChunk` pieces near the spawn could still move if she wants option B.

---

## 5. HER ARENA WORK IN PROGRESS

**Structure of the level:** everything is inside one prefab, `Assets/Scenes 1/Cave/Environment.prefab`,
with 436 nested prefab instances in six groups:

| Group | Contents |
|---|---|
| TempleAssets | PillarA ×20, FloorB ×15, WallA ×14, WallC ×5, ArchA ×5, Stairs ×4, WallDoor ×3 … (~85) — **the castle** |
| Rocks | MegaRockB ×13, MegaRockA ×12, MegaRockC ×10, RockA/B/C, clusters (~45) |
| Foliage | Fern ×18, weedsA ×11, weedsB ×5, grasspatchA ×4, Ivy chunks (~50) |
| Statues | Statue ×5, LionStatue ×3 |
| Rubble / Roots | RubbleA-C, RootA/C |

**Critical numbers:**
- The kit is on a **5-unit grid** *inside Prefab Mode*.
- **The Environment instance is scaled ×3**, so **one floor tile = 15 metres in the game**.
- Transform chain: `world = (430.619, 15.852, 412.395) + 3 × ((42.317, −15.852, 54.079) + piecePos)`.
- She has already widened it: 211 → 226 pieces, footprint ~235m → **476m × 293m**.

**Workflow that was given to her (and works):** duplicate with Cmd+D, then type `+5` (or `+25` for a
row of five) into the Position field — never drag. Grid Size set to 5. Hide Foliage/Statues while
moving rocks. Never move on Y (exposes buried bottoms). Foliage last.

**NavMesh:**
- Agent Type 0 (default) is radius **0.74**, height **2** — but the Oni's agent is radius **1.4**
  and he is ~5m tall. **He can legally stand inside walls.** This contributes to the camera problem.
- Recommended (partly done): a `Boss` agent type, radius 1.5, height 5.5, on its own NavMeshSurface.
  She baked a second surface on 2 Sep (`NavMesh-NavMesh Surface 1.asset`, 4.4MB) — confirm it is the
  Boss one and that the Oni's NavMeshAgent uses it.
- To keep the boss out of the castle: a `NavMeshModifierVolume` over the castle interior,
  Area = Not Walkable, Affected Agents = Boss. **Yoru is a CharacterController and does not use
  NavMesh at all**, so this costs the player nothing.

---

## 6. THE MEASUREMENT TOOLS AVAILABLE TO YOU

**`ArenaClearanceGizmo.cs`** (editor-only, on `CineStageMark`): draws rings at 14m (red, must be
empty), 20m (yellow), 25m (green) plus a 15m vertical clear column; when selected it scans and draws
a red box + name + distance around every solid thing inside the red ring.

**Reading her scene from the container** — this works and is how most facts above were found:
parse `Assets/Scenes 1/CaveScene_Oni_Boss1.unity` and `Environment.prefab` as YAML with python.
Prefab instances store their name/position under `m_Modifications` → `propertyPath: m_Name` /
`m_LocalPosition.x|y|z`. **Remember the ×3 scale and the group offsets** or every number is wrong.

**Known limitation:** these measurements use each piece's **pivot**, not its true bounds. A rock
whose pivot is 20m away can still reach into the arena, and pieces on a rock ledge above the floor
look like blockers when they are not. Her screenshot suggested the real clearance is **better** than
the pivot maths said. **Trust the gizmo and her eyes over the pivot numbers.**

**She can send screenshots and they are worth more than any of this.** Ask for them.

---

## 7. LIGHTING / WEATHER STATE (as of 2 Sep)

- **COZY 3** (`com.distantlands.cozy.core` + eclipse add-on) is now in the cave scene: the
  `Cozy Weather Sphere` object exists, clock at **12:05 midday**.
- `StormWeather.driveCozy = 1`, **Phase 1 Weather = "Receeding Storm"**,
  **Phase 2 Weather = "Thunder Storm"**. It also retires `Rain_Cave` at start — COZY owns the rain
  (her decision).
- **Her complaint: play mode is too dark.** The cause is the storm profiles — midday under a
  thunderstorm is dark by design. Fixes offered, not yet applied:
  1. Phase 1 Weather → `Mostly Cloudy` / `Overcast` / `Approaching Storm` (keep Thunder Storm for phase 2).
  2. Cozy Weather Sphere → Atmosphere → **Ambient Light Multiplier** 1.095 → 1.6–2.0.
  3. Old `Directional Light` is at **0.3** while COZY's `Sun Light` is at 2 — set the old one to 0
     or raise it; two suns fight.
- **Do not chase "exposure: 0.12" in the scene file** — that belongs to the post-processing debug
  monitors, not COZY. I wasted her time on it once already.
- **COZY only simulates in Play mode.** The Scene view will look dark and that is normal. The
  Scene-view lighting toggle (or draw mode → Unlit) is for working, not for judging.

---

## 8. INSPECTOR GUIDE — EVERY CINEMATIC NUMBER (round-67 code)

**Cinematic Camera**
| Field | Now | What it does |
|---|---|---|
| Cine Fov | 65 | The one locked lens. Higher = wider. Do not ramp it. |
| Cine Subject Fill | 0.7 | Share of screen height he fills; **higher = camera closer** |
| Cine Subject Screen Y | 0.4 | Where he sits vertically in rise/hang (0.5 = centred, lower = more sky) |
| Cine Pull Screen Y | 0.33 | Same, during the lightning pull |
| Cine Cam Below | 3 | Metres the camera sits below his middle (bigger = steeper look-up) |
| Cine Cam Min Height | 1 | Floor safety |
| Cine Cam Side Angle | 25 | Where the shot starts around him |
| Cine Roar / Rise / Drop Orbit | 120 / 40 / 30 | Degrees of orbit per beat |
| Cine Framing Smoothing | 5 | How fast distance settles (lower = lazier) |
| Cine Frame Margin | 4 | Edge breathing room |
| Cine Exit Fade | 0.3 | Handback after landing |
| Cine Aim Smoothing | 10 | Aim follow speed |
| Cine Apex Moment | 0.4 | Clip point counted as the top of the jump |

**Timing**
Cine Slow Motion 0.45 (world) · Cine Roar Speed 0.8 · Cine Roar Linger 0.9 ·
**Cine Rise Speed 0.45** (she says the jump start feels too slow — raise toward 1.0) ·
Slow Frame Start/End 27/37 of Clip Total Frames 94 · Cine Beat Slow 0.2 ·
Cine Top Hold 2 · Cine Hang Drift 7 · Cine First Attack Grace 2

**Sky show** — Sky Show Enabled · Sky Bolts 9 · Sky Bolt Range 14 · Sky Bolt Scale 1.5 ·
Sky Fill VFX (empty = uses the club prefab) · Streamer Count 3 · Sky Show Intensity 1 (1.5 = more) ·
Cozy Thunders 2 · Cine Lightning VFX = `LightningOrbSoftYellow` (assigned)

**Ground pound** — Pound Anim Speed 1 · Pound Strike Moment 0.6 · Pound Damage 45 ·
Ring Speed 12 · Ring Max Radius 12 · Ring Width 1.6 · Shake 1.1/0.8 ·
Repeat Cooldown 25 · Repeat Chance 0.15

**SFX / music slots (all empty, hers to fill)** — Cut SFX, Climax SFX, Drop SFX, Pound Slam SFX;
Phase 1 Music, Transition Music, Phase 2 Music, Roar SFX.

---

## 9. KNOWN BUG IN THE LIVE CODE

Round 67's frame-safety can **run the camera backwards** when the club swings past close to the
lens: points beside the lens return a 1.15× step-back every frame, which compounds. In the round-69
log it reached **39.6m** before walls stopped it. Round 70 fixed it three ways (points beside the
lens crop instead of pushing, the step-back is bounded to 1.5× the composed distance, and a hard
`cineMaxDistance` ceiling) — but round 70 was reverted with the rest. **If she reports the camera
flying backwards into rock, this is it**, and the fix is small and known.

---

## 10. PARKED — NOT FORGOTTEN

- **Permanent lightning club** through phase 2 (agreed: after the entrance beat looks right).
  Later: whether it should change damage.
- **Arena shrink / cave collapse** in phase 2 (her original idea).
- **COZY audio duck** under the boss music if the mix gets crowded.
- Her verdicts never given: the **parry glass-slam** (round 45), the **charge fire oil-line trail**,
  and whether the **4s/6s anti-kite** ladder has ever been seen in play.
- **Turn off verbose logging** at the end (`OniDebugLogFile`, the `[OniBoss:*]` traces) — it is the
  heaviest thing in the frame.
- She asked once for **"the sky note"**: applying the sky-peek lens tilt idea to the cinematic at the
  jump peak. Partly done in the reverted round 65; bring it back if she asks.

---

## 11. IF YOU DO ONE THING NEXT

Do **not** touch the camera maths. The next step is the agreed one:

1. Confirm with her where the fight should be staged — likely `CineStageMark` moved to **(482, 1, 415)**.
2. Ask her to confirm the gizmo reads CLEAR there.
3. Then write the small piece of code that, on the hard cut, moves the Oni (and Yoru with him,
   if she agrees) to the mark, snapped to the NavMesh.

That removes the wall problem at its source instead of patching it, and it is the only camera work
she has approved that has not been built.
