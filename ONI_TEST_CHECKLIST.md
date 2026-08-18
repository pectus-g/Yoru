# Oni — Test Checklist

Keep this open while you play. Console signatures are in `code font`.

---

## Before pressing Play

**Nothing.** No inspector work. Every new setting is applied from code at Start.

Just wait for Unity to finish compiling. If the console shows compile errors, stop and send them —
do not test.

**Sanity line.** The moment you press Play you should see three lines from the Oni:

```
[OniBoss:Layer] hold-ground ON (standoff 3m, watch 'Watch', backstep 'Walk' reversed, attack facing LOCKED)
[OniBoss:Layer] charge drive ON (hold frame 0.35, speed 14, stops at 2.6m, tracks for 0.35s then commits)
[OniBoss:Layer] OniBoss layer ready ...
```

If any of those are missing, the script did not recompile — nothing else in this list will be true.

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

## 4. The charge (walk far away, 12m+, and let him come)

This is the one most likely to still need tuning.

Expected: club held straight out in front, fast dash across the whole gap, brake next to you,
**then** the club strike lands as a heavy hit.

Log check:

```
[OniBoss:Layer] charge arrived, 2.4m from the lock point after 1.10s real — releasing the club strike
[OniBoss] Hit player for 18 (Oni_Charge)
```

Bad signs and what they mean:

| What you see | What it means |
|---|---|
| `charge timed out after 2.50s` | He could not reach you — blocked, or speed too low |
| `Attack missed, player out of range` right after a charge | He is braking too far away → lower **Charge Stop Distance** |
| Pose during the rush looks wrong (mid-windup, or already swinging) | Adjust **Charge Hold Normalized Time** (now 0.35) |
| He still overshoots and pops back | Tell me — the freeze approach did not hold the clip |

**This is the only number that may need your eye: `Charge Hold Normalized Time` on OniBoss.**
It is the frame of the clip held for the whole rush. It should be the pose where the club is out
straight. Scrub the Oni Charge clip in the Animation window, find that frame, divide its time by the
clip length, type that in. Or just nudge 0.35 up/down until it looks right.

---

## What to send back

1. The full console log.
2. If the charge still looks wrong: a short screen recording of just the charge (Cmd+Shift+5).

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
| Y1 | Test and send the log | Now |
| Y2 | Tune `Charge Hold Normalized Time` if the rush pose looks wrong | Only if needed |
| Y3 | Combat sounds | Later, your call — the SFX manager is in the scene and waiting |
| Y4 | Real VFX prefabs to replace my code-built sparks/waves | Later — assign on YoruVFXManager, then turn off `Procedural Hit Spark` and `Charge Wave VFX` |

Not started, deferred by earlier decision: wall colliders + line-of-sight gating, boss bar restyle,
Yoru health/death/respawn, prefabbing the Oni.
