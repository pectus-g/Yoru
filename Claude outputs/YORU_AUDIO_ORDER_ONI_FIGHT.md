# YORU: Eclipse of Tails
# Audio order sheet, Oni boss fight (cave)

Pectus Games. Prepared 13 Sep 2026.

Scope: the Oni cave boss fight only. Everything below is an event the game already fires.
Music is ordered as stems.

Target quality: high end indie. Japanese folklore setting, night cave, storm outside.
Player character is a small cat (Yoru). Boss is a giant oni with a kanabo (iron club).

---

## HOW TO READ THIS

- **Files** is the number of separate audio files wanted for that event.
- Repeated events need variations or the repetition becomes audible within seconds.
  Footsteps and impacts: 4 to 6 variations. Signature one-offs: 1 to 2.
- **Loop** means seamless looping file, quoted separately from one-shots.
- Format: 48 kHz, 24 bit WAV, mono for positional sounds, stereo for ambience and music.
- No baked reverb or limiting on one-shots. The game applies its own space.

---

## 1. MUSIC (3 pieces, delivered as stems)

| Piece | Length | Notes |
|---|---|---|
| Phase 1 combat loop | 2 to 3 min, seamless | Taiko and shamisen base. Driving, not frantic. |
| Transition | 8 to 15 s | Bridges phase 1 into phase 2. Lands on the boss roar. |
| Phase 2 combat loop | 2 to 3 min, seamless | Faster and harder than phase 1. **Not darker.** Escalation reads as tempo and intensity. |

**Stems required per piece:** drums, bass, melody, ambience, as separate aligned files plus a
reference mixdown.

Why stems: the game's lights pulse to the music spectrum and need the drum stem alone;
phase 2 should be built by adding layers rather than cutting to a different track;
music must duck under dialogue and stings.

---

## 2. YORU, LOCOMOTION (about 45 files)

She is a small, light cat. Soft pads, not boots. The cave floor gets progressively wet
during the fight, so three surface sets are needed.

| Event | Files | Type |
|---|---|---|
| Footstep, dry rock | 6 | one-shot |
| Footstep, wet rock | 6 | one-shot |
| Footstep, puddle splash | 6 | one-shot |
| Jump takeoff | 3 | one-shot |
| Land soft | 3 | one-shot |
| Land hard (long fall) | 2 | one-shot |
| Climb hand grip | 4 | one-shot |
| Climb foot scuff | 4 | one-shot |
| Climb mantle over the top | 2 | one-shot |
| Pounce launch | 2 | one-shot |
| Fur and body rustle under fast moves | 4 | one-shot |
| Collar bell (she wears one) | 3 | one-shot |

Two-leg and four-leg gaits reuse the same footstep files at different rates.

---

## 3. YORU, COMBAT (about 52 files)

| Event | Files | Type |
|---|---|---|
| Swing, light | 4 | one-shot |
| Swing, heavy | 2 | one-shot |
| Swing, combo finisher | 2 | one-shot |
| Impact on the Oni, light | 4 | one-shot |
| Impact, heavy | 3 | one-shot |
| Impact, combo finisher | 2 | one-shot |
| Phantom hit (hit swallowed, no contact) | 2 | one-shot |
| Heavy charge start | 1 | one-shot |
| Heavy charge hold | 1 | **loop** |
| Heavy charge ready (window opens) | 1 | one-shot |
| Heavy charge release | 1 | one-shot |
| Dodge whoosh (roll, dash, backflip differ) | 3 | one-shot |
| Parry clang | 3 | one-shot |
| Guard block | 3 | one-shot |
| Parry window opens (tiny Sekiro-style cue) | 1 | one-shot |
| Player hit, light | 3 | one-shot |
| Player hit, heavy | 2 | one-shot |
| Player effort breaths (attack, landing) | 6 | one-shot |
| Player death | 2 | one-shot |
| Low health heartbeat: lub (strong first beat) and dub (softer second beat), as two separate files | 2 | one-shot |
| Dive attack | 2 | one-shot |
| Inferno spiral | 2 | one-shot |

The parry clang is the signature sound of the whole fight. Worth extra passes.
The combo finisher impact carries a hitstop freeze, so it needs real weight.
The heartbeat (added 20 Sep 2026) plays while she is on her last peach. The game fires the lub and the dub itself,
in time with the pulsing peach on screen, so deliver them as two short separate thumps (not a loop, no tempo).
Low and round, a chest sound rather than a beep, but with enough body above 100 Hz to survive laptop speakers.

---

## 4. YORU, TAIL PROJECTILE AND ABILITIES (about 17 files)

She fires magic from her tails and has a slow-motion aim mode.

| Event | Files | Type |
|---|---|---|
| Tail cast, charge (slow and fast variants) | 2 | one-shot |
| Tail projectile launch | 2 | one-shot |
| Tail projectile travel | 1 | **loop**, short |
| Tail projectile impact | 3 | one-shot |
| Bullet time enter (filter sweep or drop) | 1 | one-shot |
| Bullet time exit | 1 | one-shot |
| Circle activation | 2 | one-shot |
| Absorbing a soul | 1 loop + 1 tail | loop + one-shot |
| Freeing a soul | 2 | one-shot |
| Form change, cat to Granny | 1 | one-shot |
| Form change, Granny to cat | 1 | one-shot |

---

## 5. THE ONI (about 26 files)

He must feel enormous. Low end, rock crunch, heavy wood and iron.

| Event | Files | Type |
|---|---|---|
| Heavy footstep, walk | 4 | one-shot |
| Heavy footstep, run | 3 | one-shot |
| Alert, he notices her (fight starts here) | 1 | one-shot |
| Charge roar | 1 | one-shot |
| Club swing 1 and 2 (air displacement) | 3 | one-shot |
| Kanabo sweep | 1 | one-shot |
| Club slam | 1 | one-shot |
| Ground pound impact | 1 | one-shot |
| Phase transition roar | 1 | one-shot |
| Hit reactions, light / medium / heavy | 3 | one-shot |
| Stagger | 1 | one-shot |
| Death vocal | 2 | one-shot |
| Kanabo detaching and hitting the floor on death | 1 | one-shot |
| Cinematic: cut, climax, drop | 3 | one-shot |

The phase transition roar is the single biggest sound in the fight. He raises the club,
lightning strikes it, he slams the floor. Everything lands on that beat.

The stagger is the reward sound for a good parry chain. It should feel earned.

---

## 6. THE CAVE (about 21 files)

The fight runs under a storm. Phase 1 is rain, phase 2 is a thunderstorm.
Part of the cave is open sky, part is covered rock.

| Event | Files | Type |
|---|---|---|
| Cave room tone, before the fight | 1 | **loop** |
| Rain, light (phase 1) | 1 | **loop** |
| Rain, heavy (phase 2) | 1 | **loop** |
| Rain on rock, close detail layer | 1 | **loop** |
| Wind, breeze | 1 | **loop** |
| Wind, storm | 1 | **loop** |
| Thunder, distant | 3 | one-shot |
| Thunder, close crack | 3 | one-shot |
| Water drips, sparse | 4 | one-shot |
| Puddle ambience, floor fully soaked | 1 | **loop** |
| Brazier crackle | 1 | **loop** |
| Fight start sting | 1 | one-shot |
| Phase 2 sting | 1 | one-shot |
| Fight over, calm returns | 1 | one-shot |

Note for the contractor: the project uses the COZY weather package, which ships some rain,
wind and thunder. Ask which of these six loops COZY already covers before quoting, so we do
not pay twice.

---

## 7. TOTALS

| | Count |
|---|---|
| Sound effects | about **162 files** across **61 events** |
| of which seamless loops | **12** |
| Music pieces | **3**, as stems (drums, bass, melody, ambience + reference mix) |

Section subtotals: Yoru locomotion 45, Yoru combat 52, Yoru tail and abilities 17,
the Oni 26, the cave 21.

---

## 8. DELIVERY NOTES FOR THE CONTRACTOR

- 48 kHz / 24 bit WAV. Mono for anything positional, stereo for ambience and music.
- Loops must be genuinely seamless, tested looping, no click at the join.
- No baked reverb, no limiting on one-shots.
- Name files by event, numbered for variations: `yoru_footstep_wetrock_01.wav`.
- Music stems must be sample-aligned and start at the same timestamp.
- Deliver a short paid test first (suggest: parry clang set, one Oni footstep set,
  8 bars of the phase 1 loop) before the full order.
