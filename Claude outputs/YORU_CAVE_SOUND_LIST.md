# YORU - Oni Cave Scene, Sound Requirements

Built from the real project: `Yoru_Animato_Controller`, `ONICONTROLLER`, `CombatSFXManager`,
`OniBoss`, `ClimbFX`, `EnemyFX`, `FormController`, `TailProjectile`, `StormWeather`.
Every event below is something the scene can already fire, or fires today with no sound on it.

"Slot" means a serialized AudioClip field already exists in code and is empty.
"New" means the event happens but nothing is wired to it yet.

---

## 1. HOW TO READ THE COUNTS

A game needs variations or repetition becomes audible within seconds. The counts below are
minimum shipping counts for a fight this size. A designer will quote per delivered file.

- Footsteps and impacts need 4 to 6 variations each.
- One shot signature moves need 1 to 2.
- Loops need to be seamless and are quoted separately.

---

## 2. YORU, LOCOMOTION AND TRAVERSAL

Animator states: `Locomotion`, `Jump2Legs`, `Jump4Legs`, `JumpWith2Legs`, `JumpWith4Legs`,
`ClimbX`, `ClimbY`, `ClimbMove`, `ClimbMantle`, `Pounce`.

| Event | Files | Status | Notes |
|---|---|---|---|
| Footstep, dry rock | 6 | New | She is a small light cat, soft pads not boots |
| Footstep, wet rock | 6 | New | The cave floor soaks during the fight, needs its own set |
| Footstep, puddle | 6 | New | Splash, used once the floor is fully wet |
| Two leg vs four leg gait | reuse | New | Same files, the game picks rate by gait |
| Jump takeoff | 3 | New | Two leg and four leg |
| Land soft | 3 | New | Normal drop |
| Land hard | 2 | New | Long fall |
| Climb hand grip | 4 | Slot (ClimbFX) | ClimbFX already has named slots per climb event |
| Climb foot scuff | 4 | Slot (ClimbFX) | |
| Climb mantle over the top | 2 | Slot (ClimbFX) | |
| Pounce launch | 2 | New | |
| Fur and collar movement | 4 | New | Light cloth or fur rustle under fast moves |
| Collar bell | 3 | New | She wears a bell, it should ring on landings and hard turns |

Subtotal: about 45 files.

---

## 3. YORU, COMBAT

Animator states: `Attack`, `AttackPaw`, `TowPaw`, `Combo1`, `Combo2`, `Combo3`,
`HeavyAttack`, `HeavyCharge_WindUp`, `HeavyCharge_Hold`, `HeavyCharge_Release`,
`Dodge_2Leg`, `Dodge_4Leg`, `DodgeDash_2Leg`, `DodgeDash_4Leg`, `Dodge_Backflip_2Leg`,
`Dodge_Backflip_4Leg`, `Parry_Start`, `Parry_Idle`, `Parry_WalkForward`, `Parry_WalkBackward`,
`DiveAttack`, `InfernoSpiral`.

| Event | Files | Status | Notes |
|---|---|---|---|
| Swing light | 4 | Slot `swingLight` | |
| Swing heavy | 2 | Slot `swingHeavy` | |
| Swing combo finisher | 2 | Slot `swingCombo3` | |
| Impact light on the Oni | 4 | Slot `impactLight` | |
| Impact heavy | 3 | Slot `impactHeavy` | |
| Impact combo finisher | 2 | Slot `impactCombo3` | Carries the hitstop, needs weight |
| Phantom hit, miss or whiff | 2 | Slot `phantomHit` | |
| Heavy charge start | 1 | Slot `heavyChargeStart` | |
| Heavy charge loop | 1 loop | Slot `heavyChargeLoop` | Seamless, must build tension |
| Heavy charge ready | 1 | Slot `heavyChargeReady` | The player must hear the window open |
| Heavy charge release | 1 | Slot `heavyChargeRelease` | |
| Dodge whoosh | 3 | Slot `dodgeWhoosh` | Roll, dash and backflip should differ |
| Parry clang | 3 | Slot `parryClang` | The signature sound of the whole fight |
| Guard block | 3 | Slot `guardBlock` | |
| Parry window opens | 1 | New | Sekiro style: a tiny cue on the perfect window |
| Player hit, light | 3 | Slot `playerHitLight` | |
| Player hit, heavy | 2 | Slot `playerHitHeavy` | |
| Player effort breaths | 6 | New | Attack grunts, landing breaths, keeps her alive |
| Player death | 2 | New | `HR_Die_2legs`, `HR_Die_4legs`, `HR_big_die_2legs`, `HR_jumping_Die_4` |
| Dive attack | 2 | New | |
| Inferno spiral | 2 | New | One shot plus a short tail |

Subtotal: about 50 files.

---

## 4. YORU, TAIL PROJECTILE AND ABILITIES

Animator states: `LeftTailCast`, `RightTailCast`, `LeftTail_Fast`, `LeftTail_Slow`,
`RightTail_Fast`, `RightTail_Slow`, `CircleActivation`, `Absorbing`, `FreeingSoul`, `Heart`.

| Event | Files | Status | Notes |
|---|---|---|---|
| Tail cast, charge | 2 | New | Slow and fast variants |
| Tail projectile launch | 2 | Slot `launchSfx` | |
| Tail projectile travel | 1 loop | New | Short seamless whoosh |
| Tail projectile impact | 3 | Slot `impactSfx` | |
| Bullet time enter | 1 | New | The aim slowdown needs a filter sweep or a drop |
| Bullet time exit | 1 | New | |
| Circle activation | 2 | New | |
| Absorbing a soul | 1 loop + 1 tail | New | |
| Freeing a soul | 2 | New | |
| Form change, cat to Granny | 1 | Slot `catToGrannySFX` | |
| Form change, Granny to cat | 1 | Slot `grannyToCatSFX` | |

Subtotal: about 17 files.

---

## 5. THE ONI

Animator states: `Idle`, `Walk`, `run`, `Watch`, `Oni_Alert`, `Oni_Charge`, `Club_Swing`,
`Club_swing2`, `Club_Slam`, `KanaboSweep`, `Ground_Pound`, `Phase_Transition`,
`Hit_react_light`, `HitReact_medium`, `HitReact_Heavy`, `Stagger`, `Death`.

| Event | Files | Status | Notes |
|---|---|---|---|
| Heavy footstep, walk | 4 | New | He must feel enormous, low end and a rock crunch |
| Heavy footstep, run | 3 | New | |
| Alert, he notices her | 1 | New | The fight starts here, the whole scene turns on this |
| Charge roar | 1 | Slot `roarSFX` | |
| Club swing 1 and 2 | 3 | Slot `enemyAttackVocal` plus new | Air displacement, heavy wood |
| Kanabo sweep | 1 | New | |
| Club slam | 1 | New | |
| Ground pound impact | 1 | Slot `poundSlamSFX` | Phase 2 lands on this beat |
| Phase transition roar | 1 | New | The single biggest sound in the fight |
| Hit react light, medium, heavy | 3 | Slot `enemyHitVocal` | |
| Stagger | 1 | New | The reward sound for a good parry chain |
| Death | 2 | Slot `enemyDeathVocal` | Plus the kanabo detaching and hitting the floor |
| Kanabo drop on death | 1 | New | The animator delivered a detached club on death |
| Cinematic cut, climax, drop | 3 | Slots `cineCutSFX`, `cineClimaxSFX`, `cineDropSFX` | |

Subtotal: about 26 files.

---

## 6. THE CAVE ITSELF

Driven by `StormWeather`, COZY and the scene.

| Event | Files | Status | Notes |
|---|---|---|---|
| Cave room tone | 1 loop | New | Quiet, before the fight |
| Rain, light, phase 1 | 1 loop | New | COZY may supply, verify |
| Rain, heavy, phase 2 | 1 loop | New | |
| Rain on rock, close detail | 1 loop | New | Layered over the main rain |
| Wind, breeze | 1 loop | New | Matches the pre fight wind on her fur |
| Wind, storm | 1 loop | New | Matches phase 2 wind |
| Thunder, distant | 3 | New | COZY has its own, verify before ordering |
| Thunder, close crack | 3 | New | Fires with every `Strike()` |
| Water drips | 4 | New | Sparse, the covered rock areas |
| Puddle ambience | 1 loop | New | Once the floor is soaked |
| Brazier crackle | 1 loop | New | For the warm lights you have parked |
| Fight start sting | 1 | New | Optional, sells the state change |
| Phase 2 sting | 1 | New | Lands with the lightning beat |
| Fight over, calm | 1 | New | The weather returns to normal, a release cue |

Subtotal: about 21 files.

---

## 7. MUSIC

`CombatMusicManager` and `OniBoss` already expect exactly these three, plus a menu track
elsewhere in the game.

| Piece | Length | Notes |
|---|---|---|
| Phase 1 combat loop | 2 to 3 min seamless | `phase1Music` |
| Transition | 8 to 15 s | `transitionMusic`, bridges into phase 2 |
| Phase 2 combat loop | 2 to 3 min seamless | `phase2Music`, faster and harder, not darker |

Ask the composer for **stems** (drums, bass, melody, ambience) as separate files. Stems let the
lights react to the drums only, let the mix duck under dialogue, and let phase 2 be built by
adding layers rather than swapping tracks.

---

## 8. TOTALS

- Sound effects: about **160 files** across **60 distinct events**.
- Music: **3 pieces** for this fight, ideally delivered as stems.
- Already wired and empty: **19** `CombatSFXManager` slots, **8** `OniBoss` slots, plus
  `ClimbFX`, `EnemyFX`, `TailProjectile` and `FormController` slots.

The earlier brief of about 37 sound effects covered the whole game at a sketch level. This one
fight alone needs roughly four times that to not sound cheap, which is the honest number.

---

## 9. WHAT IS MISSING IN CODE, NOT IN AUDIO

These fire today with no audio hook at all. They need a line of code before a sound can play.

- Footsteps: no footstep system exists. Needs animation events on the locomotion clips, or a
  distance based stepper, plus a surface check so wet rock and puddles sound different.
- Player effort breaths and death vocals: no slots.
- The Oni's footsteps, sweep, slam, stagger and phase transition roar: no slots.
- Bullet time enter and exit on the tail aim: no slots.
- `CombatMusicManager` is not in the scene. It has to be added to `CombatManagers` before any
  music plays at all.
- There is no AudioMixer asset. Master, Music, SFX and Ambience groups are needed before any
  of this can be balanced, and before music can duck under anything.
