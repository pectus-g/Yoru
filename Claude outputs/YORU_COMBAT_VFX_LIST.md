# COMBAT VFX SWAP LIST (11 Sep 2026)

Every particle slot that fires during the Oni fight, where its field is, what fires it, and what is
in it right now. Read from the scene and the scripts, nothing guessed. Drag a new prefab onto the
field to swap; the code does not care which prefab it is.

## A. Yoru - Hierarchy: PlayerYoru_1.1 → Yoru VFX Manager

| Field (Inspector) | Fires when | Prefab now |
|---|---|---|
| Dust Puff Prefab | footstep dust | VFX/DustMotesCalm |
| Jump Launch Prefab | jump 1 | VFX/LandingImpact |
| Landing Impact Prefab | landing | VFX/MagicDust |
| Run Trail Prefab | running | VFX/DustMotesLively |
| Paw Attack 1 Prefab | animation event VFX_AttackPaw | VFX/Pawattack2 |
| Paw Attack 2 Prefab | animation event VFX_TowPaw | VFX/Pawattack2 (same as above) |
| Left Tail Magic Prefab | animation event VFX_LeftTail | VFX/LeftTailMagic |
| Right Tail Magic Prefab | animation event VFX_RightTail | VFX/LeftTailMagic (same as left) |
| Combo 1 VFX | light combo hit 1 | VFX/Pawattack2 |
| Combo 2 VFX | light combo hit 2 | VFX/Pawattack2 (same as combo 1) |
| Combo 3 VFX | light combo hit 3 | VFX/Jumplunch |
| Heavy Attack Prefab | heavy release | VFX/DustDirtyPoofSoft |
| Heavy Charge Buildup Prefab | heavy charge held | VFX/MagicChargeBlue |
| Spin VFX | spin attack | Mirza Beig Ultimate VFX / oneshot turbulentImpact3 |
| **Light Hit Spark Prefab** | **Yoru's light attack connects** | **EMPTY** |
| **Heavy Hit Spark Prefab** | **Yoru's heavy attack connects** | **EMPTY** |
| Dodge Trail Prefab | dodge | Mirza Beig XP-ACTION / loop smoke2 |
| Dodge Dash Trail Prefab | dash | same smoke2 |
| Light Hit React VFX | Yoru takes a light hit | Mirza Beig / oneshot criticalHit2 |
| Heavy Hit React VFX | Yoru takes a heavy hit | Mirza Beig / oneshot turbulentImpact4 |
| Soul Freeing Prefab | animation event VFX_Soul | VFX/MagicAuraBlue |
| Circle Activation Prefab | animation event VFX_Circle | VFX/CirleActivate |
| Absorbing Prefab | animation event VFX_Absorb | VFX/absorbing |

The two empty hit-spark slots are the biggest hole in the fight: `CombatFeedbackManager` calls
`PlayHitSparkVFX` on every landed hit and nothing spawns, so hitting the Oni gives hitstop, shake and
sound but no picture at the contact point. Every reference fight (Sekiro, Nioh, God of War) puts its
loudest effect exactly there.

Spawn points, if a new effect sits wrong: Left Paw = LeftPawVFX, Right Paw = RightPawVFX,
Left Tail Tip = LeftTailVFX, Right Tail Tip = RightTailTip_Anchor, Center Body = CenterVFX.

## B. Oni - Hierarchy: OniBoss → Oni Boss

| Field (Inspector) | Fires when | Prefab now |
|---|---|---|
| Pound Impact VFX | ground pound lands | Epic Toon FX / MagicNovaExplosionBlue |
| Cine Lightning VFX | club tip at the phase-2 beat | Epic Toon FX / LightningOrbSoftYellow |
| Cine Sky Fill VFX | extra bolts during the build | EMPTY (falls back to Cine Lightning) |
| Charge Ground Trail VFX | fire puffs while he rushes | Epic Toon FX / ToonFireTrail |
| Charge Hit VFX | the charge connects | Epic Toon FX / ExplosionFireballFire |
| **Swing Trail VFX** | **fallback trail for attacks with no row trail** | **EMPTY** |
| **Hit Land VFX** | **fallback hit for attacks with no row hit** | **EMPTY** |

Both fallbacks being empty is fine only because all four attack rows fill their own (below).

## C. Per-attack rows - OniBoss → Oni Boss → Swing Wave VFX By Attack

| Attack | Wave (Vfx) | Trail VFX | Hit VFX |
|---|---|---|---|
| Club_Swing | ETFX SwordWaveYellow | ETFX LightningFloorYellowTrail | ETFX SwordHitMiniRed |
| ClubSwing2 | ETFX SwordWaveYellow | ETFX LightningFloorYellowTrail | ETFX SwordHitRed |
| ClubSlam | ETFX SwordWaveRed | ETFX LightningFloorRedTrail 1 | ETFX SwordHitRedCritical |
| KanaboSweep | ETFX SwordSlashThickRed | VFX/SoftFireTrail | ETFX SwordHitRedCritical |

Every row is tilted X 90 with zero nudge and 2 s lifetime, so a replacement that is authored upright
will need Tilt X back to 0.

## D. Tail projectiles

| Field | Where | Prefab now |
|---|---|---|
| Left Tail VFX Prefab | PlayerYoru_1.1 → Ringmeshcontroller | Org_Prefabs/VFX_LeftTail_Fire |
| Right Tail VFX Prefab | PlayerYoru_1.1 → Ringmeshcontroller | Org_Prefabs/VFX_RightTail_Light |
| Bolt Prefab | PlayerYoru_1.1 → Tail Aim Controller | Org_Prefabs/Combat/YoruLeftTailBolt |
| Bolt Prefab | PlayerYoru_1.1 → Tail Aim Controller 4 Leg | Org_Prefabs/Combat/Yoru4LegTailBolt |
| Aim VFX | Tail Aim Controller | EMPTY |
| Reticle Sprite / Lock Sprite | both tail aim controllers | EMPTY (no aim UI at all) |

## E. Not particles, so a prefab will not fix them

Drawn in code, change the script if you want them different: the ground-pound ring (OniBoss builds a
line ring), the storm bolts (LineRenderer with Legacy Shaders/Particles/Additive), and the charge
wave arcs when Charge Ground Trail VFX is empty.

Enemy FX component on OniBoss has an empty Entries list, so it contributes nothing today.

## F. What you can swap from, already in the project

| Library | Prefabs | Style |
|---|---|---|
| Epic Toon FX | 1282 | flat toon, bright, no HDR. What the Oni uses now. |
| GabrielAguiar Unique Projectiles Vol 1 to 5 + Toon Vol 1 | 917 | stylised projectiles, hits, muzzles; the best match for a bright Yoru vs dark cave read |
| Mirza Beig Ultimate VFX (+ XP ACTION) | 564 | soft realistic smoke, sparks, impacts. Yoru's own VFX prefabs are built from its materials. |
| KriptoFX Mesh Effect | 25 | mesh-based slashes and trails, good for the kanabo swing |
| Assets/VFX (yours, hand-built) | 24 | built on Mirza Beig materials, 1 to 5 emitters each |

Anything you drop in should read against a dark cave: bright core, short life, and colour that is not
the same blue as the health bar and the moon. Warm sparks on Yoru's hits would separate her damage
from his.
