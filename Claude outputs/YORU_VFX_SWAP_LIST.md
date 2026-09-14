# YORU: Eclipse of Tails
# Every VFX slot on Yoru and the Oni, and what to swap it with

Read from the live scene `CaveScene_Oni_Boss1` on 14 Sep 2026.
Includes the four Vefects packs and the Lana Studio pack you just bought.

---

## 0. READ THIS FIRST: SIX OF YORU'S SLOTS ARE DEAD

Six fields on **Yoru VFX Manager** are typed `ParticleSystem`, not `GameObject`. The code calls
`.Play()` on whatever is in them. All six currently hold a **prefab asset** dragged from the
Project window. Playing a prefab asset plays it inside the asset file, not in the scene, so
**nothing renders**. Swapping the prefab will change nothing until this is fixed in code.

| Slot | What it is meant to be | Currently holds |
|---|---|---|
| Combo 1 VFX | her first combo hit | VFX/Pawattack2 |
| Combo 2 VFX | her second combo hit | VFX/Pawattack2 |
| Combo 3 VFX | her combo finisher | VFX/Jumplunch |
| **Spin VFX** | **the swirl on her spin attack. This is the one you could not spot.** | Mirza Beig oneshot turbulentImpact3 |
| Light Hit React VFX | she takes a light hit | Mirza Beig oneshot criticalHit2 |
| Heavy Hit React VFX | she takes a heavy hit | Mirza Beig oneshot turbulentImpact4 |

Fix is about 15 lines: change the six fields to `GameObject` and spawn them like every other
slot already does. Everything else on this list works today.

---

## 1. YORU, ON `PlayerYoru_1.1` -> **Yoru VFX Manager**

### Working slots (prefab in, effect out)

| Slot | Fires when | Currently holds |
|---|---|---|
| Dust Puff Prefab | footsteps | VFX/DustMotesCalm |
| Jump Launch Prefab | jump takeoff | VFX/LandingImpact |
| Landing Impact Prefab | landing | VFX/MagicDust |
| Run Trail Prefab | running | VFX/DustMotesLively |
| Paw Attack 1 Prefab | animation event VFX_AttackPaw | VFX/Pawattack2 |
| Paw Attack 2 Prefab | animation event VFX_TowPaw | VFX/Pawattack2 **(same as Paw 1)** |
| Left Tail Magic Prefab | animation event VFX_LeftTail | VFX/LeftTailMagic |
| Right Tail Magic Prefab | animation event VFX_RightTail | VFX/LeftTailMagic **(same as left)** |
| Heavy Attack Prefab | heavy release | VFX/DustDirtyPoofSoft |
| Heavy Charge Buildup Prefab | heavy charge held | VFX/MagicChargeBlue |
| Light Hit Spark Prefab | her light attack lands on him | Epic Toon FX SwordHitMiniBlue |
| Heavy Hit Spark Prefab | her heavy attack lands on him | Epic Toon FX SwordHitBlueCritical |
| Dodge Trail Prefab | dodge | Mirza Beig XP-ACTION loop smoke2 |
| Dodge Dash Trail Prefab | dash | same smoke2 **(same as dodge)** |
| Soul Freeing Prefab | animation event VFX_Soul | VFX/MagicAuraBlue |
| Circle Activation Prefab | animation event VFX_Circle | VFX/CirleActivate |
| Absorbing Prefab | animation event VFX_Absorb | VFX/absorbing |

Spawn points, same component: Left Paw, Right Paw, Left Tail Tip, Right Tail Tip, Center Body.

### Other Yoru VFX, different components, same object

| Slot | Component | Currently holds |
|---|---|---|
| Left Tail VFX Prefab | Ring Mesh Controller | Org_Prefabs/VFX_LeftTail_Fire |
| Right Tail VFX Prefab | Ring Mesh Controller | Org_Prefabs/VFX_RightTail_Light |
| Bolt Prefab | Tail Aim Controller | Org_Prefabs/Combat/YoruLeftTailBolt |
| Aim VFX | Tail Aim Controller | scene object vfx_Muzzle_Fireball01Blue |
| Bolt Prefab | Tail Aim Controller 4 Leg | Org_Prefabs/Combat/Yoru4LegTailBolt |
| Aim Vfx Left / Right | Tail Aim Controller 4 Leg | scene objects Glow_4Leg_L / Glow_4Leg_R |
| Cat To Granny VFX | Form Controller | VFX/MagicBuffBlue |
| Granny To Cat VFX | Form Controller | VFX/MagicBuffred |
| Reticle Sprite, Lock Sprite | both aim controllers | **EMPTY on both**, so aiming shows no crosshair at all. These take a Sprite, not a prefab. |

---

## 2. THE ONI, ON `OniBoss` -> **Oni Boss**

| Slot | Fires when | Currently holds |
|---|---|---|
| Pound Impact VFX | ground pound lands | Epic Toon FX MagicNovaExplosionBlue |
| Cine Lightning VFX | club tip at the phase 2 beat | Epic Toon FX LightningOrbSoftYellow |
| Cine Sky Fill VFX | extra bolts during the build | EMPTY, falls back to Cine Lightning |
| Charge Ground Trail VFX | fire while he rushes | Epic Toon FX ToonFireTrail |
| Charge Hit VFX | the charge connects | Epic Toon FX ExplosionFireballFire |
| Swing Trail VFX | fallback trail, all four rows have their own | EMPTY |
| Hit Land VFX | fallback hit, all four rows have their own. **Also the ground pound's hit effect on Yoru.** | EMPTY |

### Per attack, `Oni Boss` -> **Swing Wave VFX By Attack**

| Attack | Vfx (the ground wave) | Trail VFX (rides the club) | Hit VFX (lands on Yoru) |
|---|---|---|---|
| Club_Swing | SwordWaveYellow | FloorRedTrail 2 | SwordHitRedCritical |
| ClubSwing2 | SwordWaveYellow **(same)** | FloorRedTrail 2 **(same)** | SwordHitRedCritical **(same)** |
| ClubSlam | SwordWaveRed | LightningFloorRedTrail 1 | SwordHitRedCritical **(same)** |
| KanaboSweep | SwordSlashThickRed | VFX/SoftFireTrail | SwordHitRedCritical **(same)** |

All four rows are Tilt X 90, so anything you drop in that was authored standing upright needs
Tilt X back to 0.

**All four attacks share one hit effect.** Four different moves currently land identically.
That is the cheapest readability win on this whole list.

---

## 3. DUPLICATES: WHERE THE SAME PREFAB IS USED TWICE

Each of these makes two different actions look the same. Cheapest wins first.

1. **All four Oni attacks share SwordHitRedCritical.** Swing, swing 2, slam and sweep land identically.
2. **Club_Swing and ClubSwing2 share both their wave and their trail.** Two of his moves are visually one move.
3. Paw Attack 1 and Paw Attack 2 both use Pawattack2.
4. Combo 1 and Combo 2 both use Pawattack2.
5. Left and Right Tail Magic both use LeftTailMagic.
6. Dodge and Dodge Dash both use smoke2.

---

## 4. WHAT YOU JUST BOUGHT

### Vefects, 4 packs

| Pack | Usable prefabs | What is in it |
|---|---|---|
| **Stylized VFX** | 58 | Per element (Dark, Earth, Electric, Fire, Ice, Nature, Sound, Void, Water, Generic): Magic Cast, Magic Hit, Magic Projectile, Slash, Slash Circle, Piercing |
| **Smoke Bombs VFX** | 33 | Smoke bursts and loops: Classic, Cursed, Darkness, Devil, Fire Storm, Thunder Storm, Snow Storm, Sand Storm, Golden Sand, Psychic, Toxic, Aqua Storm, Sound Storm |
| **Stylized AoE VFX** | 33 | Per element: Burst and Area, 16 elements including Magma, Blood, Crystal, Heal, Light |
| **Anime Stylized VFX** | 26 | Basic Attack, Dash, Buff Cast + Loop, Debuff Cast + Loop, Heal Cast + Loop, Explosion Floor, Explosion Omni, Fireball, Lightning, Bomb, Arrow Shot, Gunshot, Pickup |

### Lana Studio, Environment VFX pack, 53 prefabs

Rain (calm, average, heavy), Snow, Embers, Butterflies in 9 colours, Birds, Bubbles,
MagicField in 4 colours, Leaves and LeavesSpin, Moonlight, Orbs (lightning, sand, snow),
Rockfall, Sandstorm, SpeedBoost, Wind_Leaves_Tornado.

Environment, not combat. Good for the cave ambience and the Ancient Tree, not for hits.

---

## 5. SUGGESTED SWAPS

Keeping your colour rule: his damage on her is **red**, her damage on him is **blue**.

### The Oni, stop all four attacks looking the same

| Slot | Swap to | Why |
|---|---|---|
| Club_Swing -> Hit VFX | `VFX_Slash_Fire` or `VFX_Fire_Magic_Hit` | fast light hit |
| ClubSwing2 -> Hit VFX | `VFX_Slash_Circle_Fire` | reads as the second beat of the combo |
| ClubSlam -> Hit VFX | `VFX_Explosion_Floor` (Anime) | heavy downward slam |
| KanaboSweep -> Hit VFX | `VFX_Piercing_Fire` | wide sweep |
| Club_Swing -> Vfx (wave) | `VFX_Slash_Fire` | |
| ClubSwing2 -> Vfx (wave) | `VFX_Slash_Circle_Fire` | different shape, same family |
| Hit Land VFX (currently empty) | `VFX_Explosion_Omni` | gives the ground pound an impact on her, which it has never had |
| Cine Sky Fill VFX (currently empty) | `VFX_Lightning` (Anime) | more bolts during the phase 2 build |

### Yoru, keep her blue

| Slot | Swap to |
|---|---|
| Light Hit Spark Prefab | `VFX_Ice_Magic_Hit` or `VFX_Slash_Ice` |
| Heavy Hit Spark Prefab | `VFX_Slash_Circle_Ice` |
| Heavy Charge Buildup Prefab | `VFX_Buff_Loop` (Anime) |
| Heavy Attack Prefab | `VFX_Explosion_Omni_Ice` (Anime) |
| Dodge Trail Prefab | `VFX_Dash` (Anime) |
| Dodge Dash Trail Prefab | `VFX_Smoke_Bomb_Classic_L`, so dash and dodge finally differ |
| Left Tail Magic Prefab | `VFX_Void_Magic_Cast` |
| Right Tail Magic Prefab | `VFX_Sound_Magic_Cast`, so the two tails differ |
| Circle Activation Prefab | `VFX_Slash_Circle_Void` |
| Cat To Granny VFX | `VFX_Buff_Cast` (Anime) |
| Granny To Cat VFX | `VFX_Debuff_Cast` (Anime) |

### After the six dead slots are fixed

| Slot | Swap to |
|---|---|
| Combo 1 VFX | `VFX_Slash_Ice` |
| Combo 2 VFX | `VFX_Slash_Circle_Ice` |
| Combo 3 VFX | `VFX_Ice_Magic_Hit` |
| **Spin VFX** | `VFX_Slash_Circle_Generic` or `VFX_Smoke_Bomb_Aqua_Storm_L` |
| Light Hit React VFX | `VFX_Debuff_Cast` |
| Heavy Hit React VFX | `VFX_Explosion_Omni` |

### Cave ambience, from Lana Studio

| Use | Prefab |
|---|---|
| Phase 1 rain | `Rain_average` |
| Phase 2 rain | `Rain_heavy` |
| Pre-fight calm | `Rain_calm` |
| Embers near the braziers | `Embers_calm` or `Embers_average` |
| Moon shafts in the open-sky area | `Moonlight` |
| Falling debris during the pound | `Rockfall` |

---

## 6. ORDER OF WORK

1. Fix the six dead `ParticleSystem` slots, otherwise a third of Yoru's combat VFX stays invisible no matter what you drag in.
2. Give the Oni's four attacks four different hit effects. Biggest readability gain for the least work.
3. Split the six duplicate pairs listed in section 3.
4. Fill the two empty Oni slots (Hit Land VFX, Cine Sky Fill VFX).
5. Fill the two empty aim Sprites so tail aiming has a crosshair.
6. Cave ambience from Lana Studio last, it is polish not readability.
