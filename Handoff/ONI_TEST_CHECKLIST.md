# Oni — Test Checklist

Keep this open while you play. Console signatures are in `code font`.

---

## Before pressing Play

**Nothing.** No inspector work. Every new setting is applied from code at Start, and the five clip
import edits (ONI_ROUND5_NOTES.md) are already in your project — Unity reimports them by itself.

Just wait for Unity to finish compiling and reimporting. If the console shows compile errors, stop and send them —
do not test.

**Sanity line.** The moment you press Play you should see these lines from the Oni:

```
[OniLog] console + telemetry → .../Yoru/OniLogs/oni_2026-08-18_....log
[OniBoss:Layer] charge travel bone: 'Hips' at depth 1 under '...'
[OniBoss:Layer] charge drive ON (windup to 0.40 at x1.3, rush 14m/s stops at 2.6m, ... strike section from 0.58, strike moment 0.76 ...)
[OniBoss:Layer] hold-ground ON (backstep 1.5m @ 1.6m/s after own attacks, watch 'Watch', attack facing LOCKED)
[OniBoss:Layer] OniBoss layer ready ...
```

If any of those are missing, the script did not recompile — nothing else in this list will be true.
Round 5 also prints a few one-line facts here (charge clip import setting, KanaboSweep clip, react tiers) — see ONI_ROUND5_NOTES.md.

---

## 1. Ground trade (hit him with LMB, stay close)

| Watch for | Should be |
|---|---|
| After each of his attacks | He **walks backward** a couple of steps, away from you. He physically moves now. |
| While he waits | Holds the Watch stance and does **not** turn to follow you. He ignores you. |
| Just before he attacks | One deliberate turn to face you, then the swing. |
| While swinging | He cannot turn. If you move sideways he misses. |
| Between animations | Blended, not snapping. |

Bad signs: still circling, still sliding, or the reverse walk plays with no movement.

---

## 2. Hit reactions

| Yoru attack | Damage | Expected |
|---|---|---|
| Light paw | 10 | quick flinch |
| Strong paw / ground tail arrow | 20 | full react, small push back (~0.35m) |
| Swirl / beyblade | 35 | stumble back ~0.9m, then stagger |
| 4-leg air tail shot | 40 | same, heavy |

**The freeze test.** Jump and swirl into him repeatedly. Last time this froze him.
He should now flinch once and keep animating — **no stutter, no freeze**.

Log check: during a swirl you should see `react tier: LIGHT (10 dmg)` only **occasionally**,
not repeated back-to-back with nothing between them. Repeated ones = the fix did not take.

Also: **hitting him should show sparks** at the contact point, and **Yoru should flash red**
when he hits her.

---

## 3. Yoru's LMB combo

Click LMB three times, fairly fast. Punch → punch → swirl.

The **second punch should now play most of the way through** before the swirl takes over.

Log check:

```
[ComboTrace] CHAIN DEFERRED (clip at 0.31, need 0.60)
```

That line appearing is the fix working — it means the clip's own event tried to cut the swing early
and was held back.

Knob if the pacing feels wrong: **Combo Cancel Min Progress** on PlayerCombat.
Lower = snappier cancels. Higher = heavier, more committed. 0 = exactly the old behavior.

---

## 4. The charge (walk far away, 12m+, and let him come; also once from close)

Expected: short wind-up in place while he turns to you (club comes forward) → he freezes on the
lance pose and rushes across the gap → brakes next to you → lands and slams the club down as a heavy
hit. His body stays on his feet the whole time — no flying ahead, no vanishing. From close range:
wind-up, then straight into the slam.

Log check:

```
[OniBoss:Layer] charge RUSH: clip frozen at 0.40, 11.3m to go at 14m/s
[OniBoss:Layer] charge STRIKE: arrived 2.6m from the lock point after 0.79s real — clip 0.40 → 0.58 ...
[Oni] Hit player for 18 (Oni_Charge)
[OniBoss:Layer] charge end (...) max raw drift ... — clip is IN PLACE  /  travel WAS baked in, pin held it
```

Bad signs and what they mean:

| What you see | What it means |
|---|---|
| `charge STRIKE: timed out after 2.50s` | He could not reach you — blocked, or speed too low |
| `Attack missed, player out of range` right after a charge | He is braking too far away → lower **Charge Stop Distance** |
| Rush pose looks wrong | **Charge Hold Normalized Time**: 0.27 = grounded lance, 0.40 = airborne lunge (now) |
| The slam starts too early / too late after he stops | **Charge Strike Normalized Time** (0.58) / **Charge Strike Moment** (0.76) |
| Body still flies ahead of his feet | Tell me — the log will say whether the reimport took and what the pin measured |

---

## What to send back

Nothing to paste. The Oni writes `OniLogs/oni_<date>.log` in your project folder while you play — I
read it from there. A short screen recording in `Assets/Captures` still helps for anything that
*looks* wrong.

---

## Remaining work

### Mine

| # | Task | Notes |
|---|---|---|
| M8 | Ground Pound attack + shockwave ring | The FBX already exists. Needs a new attack entry — I can register it from code so you do no editor work. |
| M9 | Alert reaction when he first realises you are there | `Oni Alert` clip already wired on aggro; needs to read better |
| M10 | Phase 1 random singles / Phase 2 mostly combos, more aggressive | Data-gated on the existing weight system |
| M11 | Turn off the combo trace logging | Once the combo question is closed |
| M12 | Re-check the "vibrates weirdly" report | Was possibly the flash-stacking bug, now fixed. If it persists I need a recording. |

### Yours

| # | Task | When |
|---|---|---|
| Y1 | ONICONTROLLER: KanaboSweep state → Motion = `Oni Kanabo sweep` clip | Optional |
| Y2 | Test (charge from far + from close, paws / strong paw / swirl on him). No log to paste. | Now |
| Y3 | Combat sounds | Later, your call — the SFX manager is in the scene and waiting |
| Y4 | Real VFX prefabs to replace my code-built sparks/waves | Later — assign on YoruVFXManager, then turn off `Procedural Hit Spark` and `Charge Wave VFX` |

Not started, deferred by earlier decision: wall colliders + line-of-sight gating, boss bar restyle,
Yoru health/death/respawn, prefabbing the Oni.
