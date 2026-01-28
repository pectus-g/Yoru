# YORU: Eclipse of Tails — Combat System Design Document
## Version 3.0 — Complete Technical Handoff
## January 2026

---

# TABLE OF CONTENTS

1. [Game Overview](#1-game-overview)
2. [Control Scheme](#2-control-scheme)
3. [Combat States & Stances](#3-combat-states--stances)
4. [Health & Energy Systems](#4-health--energy-systems)
5. [Combo System](#5-combo-system)
6. [Defensive Mechanics](#6-defensive-mechanics)
7. [Default Abilities](#7-default-abilities-free-from-start)
8. [Soul → Tree → Ring Flow](#8-soul--tree--ring-flow)
9. [Ring & Ability System](#9-ring--ability-system)
10. [Skill Tree UI](#10-skill-tree-ui)
11. [Dark Path Abilities](#11-dark-path-abilities-left-tail)
12. [Light Path Abilities](#12-light-path-abilities-right-tail)
13. [Necromancy System](#13-necromancy-system)
14. [Spirit Judgment Ultimate](#14-spirit-judgment-ultimate)
15. [Karma & Dialogue Integration](#15-karma--dialogue-integration)
16. [Stone System](#16-stone-system)
17. [Magatama Collectible System](#17-magatama-collectible-system)
18. [Side Quest System](#18-side-quest-system-for-stones)
19. [Kill Point System](#19-kill-point-system)
20. [Enemy Tier System](#20-enemy-tier-system)
21. [Enemy Respawn System](#21-enemy-respawn-system)
22. [Enemy Design: Withered Kodama](#22-enemy-design-withered-kodama)
23. [Animation Inventory](#23-animation-inventory)
24. [Animation Reuse Strategy](#24-animation-reuse-strategy)
25. [Animations Still Needed](#25-animations-still-needed)
26. [VFX & Sound Requirements](#26-vfx--sound-requirements)
27. [Integration with Existing Systems](#27-integration-with-existing-systems)
28. [Implementation Roadmap](#28-implementation-roadmap)
29. [Testing Stages](#29-testing-stages)
30. [Key Decisions Log](#30-key-decisions-log)

---

# 1. GAME OVERVIEW

## Genre
**Narrative Action-Adventure** (similar to God of War 2018, Ghost of Tsushima, Okami)

## Game Context
YORU: Eclipse of Tails is a narrative-driven action-adventure featuring Yoru, a two-tailed Nekomata (cat spirit) who judges lost souls in purgatory. Combat is one of two paths for resolving encounters — the other being empathy/dialogue.

## Combat Philosophy
- **Zelda-inspired**: Smooth, satisfying, empowering (not punishing)
- **No stamina for combat**: Only climbing uses energy
- **Clear telegraphs**: Enemies signal attacks for parry/dodge timing
- **Rewarding skill**: Perfect dodge and parry are powerful but not required
- **Movement freedom**: Player maintains mobility during combat
- **Cat spirit theme**: Agile, graceful, feline movements
- **Nekomata identity**: Necromancy is core power (raising the dead)
- **Dual paths**: Every boss can be defeated via Combat OR Empathy

## Core Loop
```
Encounter Boss → Combat OR Dialogue → Soul Drops → Take to Secret Tree → 
Ring + Memory + Health/Energy Choice → Skill Tree Opens → World Transforms
```

---

# 2. CONTROL SCHEME

## PC Controls (Finalized)

| Action | Primary Key | Alternative | Notes |
|--------|-------------|-------------|-------|
| Move | WASD | — | Standard |
| Sprint (4-leg) | Hold Shift | — | Switches to quadruped |
| Jump | Spacebar | — | Triple jump (default) |
| Dodge | Left Alt | — | Direction-sensitive |
| Parry | Q | — | Scared cat pose |
| Light Attack / Combo | Left-click (tap) | — | 3-hit combo |
| Heavy Attack | Left-click (hold 0.5s) | — | Charged attack |
| Left Tail (Dark) | 1 | Mouse4 | Projectile abilities |
| Right Tail (Light) | 2 | Mouse5 | Protection abilities |
| Ability Wheel | Hold Tab | — | Select equipped ability |
| Spirit Judgment | Hold R | — | Ultimate (when charged) |
| Lock-on | F | Middle Mouse | Optional toggle |
| Interact | E | — | Dialogue, objects |

## Key Rebinding
- Available in Settings menu
- Defaults pre-set, player can change anytime
- All combat actions reachable without moving hand from WASD

---

# 3. COMBAT STATES & STANCES

## Player Combat States

```csharp
public enum CombatState
{
    Idle,           // Not in combat
    Combat,         // In combat, can attack
    Attacking,      // During attack animation
    Dodging,        // During dodge i-frames
    Parrying,       // During parry window
    Stunned,        // After taking hit
    UsingAbility,   // During ability cast
    Ultimate,       // During Spirit Judgment
    SpiritSurge     // During Spirit Surge (no cooldowns)
}
```

## Stance System (Automatic)

| Stance | Triggered By | Speed | Damage | Dodge Type |
|--------|--------------|-------|--------|------------|
| **2-Leg** | Walking / Standing | Normal | 100% | Backflip (more i-frames) |
| **4-Leg** | Sprinting (Shift) | +30% | 85% per hit | Roll (longer distance) |

Stance switches automatically based on movement input.

---

# 4. HEALTH & ENERGY SYSTEMS

## Health System

| Property | Details |
|----------|---------|
| **Health Unit** | Peaches |
| **1 Peach** | 4 HP |
| **Starting Health** | 3 Peaches (12 HP) |
| **Max Health** | Expandable via Health Sockets |
| **Healing** | Food items, Healing Whisper ability, Eclipse Mercy ultimate |

## Energy System

| Property | Details |
|----------|---------|
| **Energy Unit** | Energy bars |
| **Used For** | Climbing ONLY |
| **NOT Used For** | Abilities (cooldown only, no energy cost) |
| **Starting Energy** | Base amount |
| **Max Energy** | Expandable via Energy Sockets |

## Health Socket vs Energy Socket Choice

After each boss encounter, at the Secret Tree:
- Player receives Ring
- Player receives Memory Piece
- Player CHOOSES: +1 Health Socket OR +1 Energy Socket

This is similar to Zelda BOTW's Heart vs Stamina choice (mechanic is not copyrighted).

---

# 5. COMBO SYSTEM

## Basic Combo (3-Hit)

| Input | Animation | Damage | Target | Duration |
|-------|-----------|--------|--------|----------|
| Click 1 | Combat_Combo1_Paw | 10 | Single enemy | 0.4s |
| Click 2 | Combat_Combo2_TwoPaw | 20 | Single enemy | 0.5s |
| Click 3 | Combat_Combo3_Spin | 35 | ONE enemy (Tasmanian devil style forward spin) | 0.7s |

**Important:** Combo 3 Spin hits ONE targeted enemy only — it's a forward rushing spin attack, NOT area damage. This differentiates it from Inferno Spiral (ring ability) which hits ALL nearby enemies.

**Combo Window**: 1.0 second between clicks
**Total Combo Damage**: 65 damage in 1.6 seconds

## Heavy Attack

| Charge Time | Damage | Effect |
|-------------|--------|--------|
| 0.5s (minimum) | 50 | Stagger |
| 1.0s | 65 | Stagger + knockback |
| 1.5s (maximum) | 80 | Stagger + guard break |

**Animation**: Combat_Combo1_Paw (charged version)

## Movement Attacks

| Attack | Input | Animation | Damage | Effect |
|--------|-------|-----------|--------|--------|
| Pounce | Sprint + Attack | Combat_Pounce | 40 | Gap closer, knockdown small enemies |
| Dive | Airborne + Attack | Combat_DiveAttack | 45 | Plunge down (primarily for exploration after climbing) |

**Note**: Dive Attack has NO stone — it's mainly for descending from heights, with occasional combat use.

## 4-Leg Combo Variant

When sprinting (4-leg stance):
- Attacks are 15% weaker per hit
- Attack speed is 20% faster
- Uses Combat_Combo3_Spin_4legs for finisher
- Better DPS due to speed, worse per-hit damage

---

# 6. DEFENSIVE MECHANICS

## Dodge System

| Input | Stance | Animation | Distance | I-Frames |
|-------|--------|-----------|----------|----------|
| Alt + Back/None | 2-Leg | Combat_Dodge_2Leg | 2m | Frames 6-20 |
| Alt + Forward | 2-Leg | Combat_DodgeDash_2Leg | 3m | Frames 4-16 |
| Alt + Back/None | 4-Leg | Combat_Dodge_4Leg | 2.5m | Frames 5-18 |
| Alt + Forward | 4-Leg | Combat_DodgeDash_4Leg | 3.5m | Frames 4-14 |

**No Dodge Stone** — Dodge is skill-based, no upgrades

## Perfect Dodge → Flurry Rush

| Property | Details |
|----------|---------|
| **Trigger** | Dodge within ±0.1 seconds of enemy attack landing |
| **Effect 1** | Time slows to 0.3x for 2 seconds |
| **Effect 2** | Player moves at normal speed |
| **Effect 3** | "Flurry Rush" prompt appears |
| **Effect 4** | Mash attack for up to 6 hits (8 with Flurry Stone) |
| **Damage** | 15 per hit × 6 = 90 total (120 with stone) |
| **Animation** | Combat_Combo1_Paw played rapidly |
| **Targets** | ONE enemy (whoever you dodged) |

**Difference from Spirit Judgment**: Flurry Rush hits ONE enemy, no targeting, earned through skill (perfect dodge). Spirit Judgment marks MULTIPLE enemies, requires ultimate charge.

## Parry System

| Property | Details |
|----------|---------|
| **Input** | Press Q |
| **Animation** | Combat_Parry (scared cat pose) |
| **Parry Window** | 0.4 seconds (0.5 with Parry Stone) |
| **Success** | Enemy staggers 1.2 seconds, counter available |
| **Fail** | Yoru briefly vulnerable |
| **Counter Damage** | 60 (90 with Counter Stone) |

## Hit Reactions

| Attack Type | Animation | Duration |
|-------------|-----------|----------|
| Light hit | Combat_HitReact_2Leg_small | 0.3s |
| Heavy hit | Combat_HitReact_2Leg / 4Leg | 0.6s |
| Critical | Combat_HitReact_2Leg | 1.0s |

---

# 7. DEFAULT ABILITIES (Free From Start)

These abilities require NO rings — player has them immediately.

## Combat Abilities

| # | Ability | Input | Animation | Damage | Stone Upgrade |
|---|---------|-------|-----------|--------|---------------|
| 1 | Paw Attack (Combo 1) | Click | Combat_Combo1_Paw | 10 | Paw Stone +25% |
| 2 | TwoPaw Attack (Combo 2) | Click×2 | Combat_Combo2_TwoPaw | 20 | Paw Stone +25% |
| 3 | Spin Attack (Combo 3) | Click×3 | Combat_Combo3_Spin | 35 | Spin Stone +25% |
| 4 | Heavy Attack | Hold Click | Combat_Combo1_Paw (charged) | 50-80 | Heavy Stone +25% |
| 5 | Pounce Attack | Sprint+Click | Combat_Pounce | 40 | Movement Stone +25% |
| 6 | Dive Attack | Air+Click | Combat_DiveAttack | 45 | NO STONE |
| 7 | Parry Counter | Attack after parry | Combat_Combo1_Paw | 60 | Counter Stone +50% |
| 8 | Flurry Rush | After perfect dodge | Combat_Combo1_Paw (rapid) | 90 | Flurry Stone +2 hits |

## Defensive Abilities

| # | Ability | Input | Animation | Stone Upgrade |
|---|---------|-------|-----------|---------------|
| 9 | Dodge Back | Alt | Combat_Dodge_2Leg/4Leg | NO STONE |
| 10 | Dodge Forward | Alt+Forward | Combat_DodgeDash_2Leg/4Leg | NO STONE |
| 11 | Parry | Q | Combat_Parry | Parry Stone +0.1s window |

## Basic Tail Abilities

| # | Ability | Input | Animation | Default Effect | Stone Upgrade |
|---|---------|-------|-----------|----------------|---------------|
| 12 | Basic Left Tail | 1 or Mouse4 | Ability_LeftTail_Fast | 1 projectile, 5 damage | 5 projectiles, more damage |
| 13 | Basic Right Tail | 2 or Mouse5 | Ability_RightTail_Fast | Block 1 hit, 2 seconds | Block 3 hits, 5 seconds |

**Important**: 
- Left Tail = Offensive (projectile damage)
- Right Tail = Defensive (air barrier protection, NO DAMAGE)
- Both scale with kill points AND stones

## Movement Abilities (Default)

| Ability | Details |
|---------|---------|
| Triple Jump | Available from start, no upgrades needed |
| Sprint | Hold Shift, switches to 4-leg stance |
| Climb | Uses Energy, separate system |

---

# 8. SOUL → TREE → RING FLOW

## Boss Encounter Outcomes

### Path A: PERSUASION (Empathy/Dialogue)
```
1. Boss persuaded through dialogue
2. Boss becomes light, vanishes peacefully
3. LIGHT SOUL drops
4. Player collects Light Soul
```

### Path B: COMBAT (Fighting)
```
1. Boss defeated through combat
2. Boss burns, becomes ashes, vanishes
3. DARK SOUL drops
4. Player collects Dark Soul
```

## Secret Tree Rewards

```
Player brings Soul to Secret Tree

Tree provides:
├── 1. RING
│   ├── Light Soul → Right Ring (Light abilities)
│   └── Dark Soul → Left Ring (Dark abilities)
│
├── 2. MEMORY PIECE
│   ├── Cinematic plays (must watch)
│   └── Memory goes to Memory Book (viewable later)
│
└── 3. SOCKET CHOICE
    ├── +1 Health Socket (more Peaches)
    └── OR +1 Energy Socket (more climbing)

After Tree:
├── Skill Tree opens automatically
├── Player spends Ring on ability
└── World atmosphere transforms
```

## Total from 10 Bosses
- 10 Rings (distributed between Left/Right based on choices)
- 10 Memory Pieces
- 10 Socket upgrades (mix of Health/Energy)

---

# 9. RING & ABILITY SYSTEM

## How Rings Work

```
10 Boss Encounters in Game
Each encounter = Player chooses COMBAT or EMPATHY

COMBAT choice → Dark Soul → Left Ring (Dark abilities)
EMPATHY choice → Light Soul → Right Ring (Light abilities)

Total possible: 10 rings distributed between tails
```

## Ability Unlock System: OPTION B (Full Player Choice)

**Every ring = Player chooses:**
- Unlock a NEW ability (from available pool)
- OR Upgrade an existing ability (if already unlocked)

**Rules:**
- 5 Dark abilities available to unlock
- 5 Light abilities available to unlock
- Each ability upgradable ONCE only
- Player chooses freely in ANY order
- Can upgrade immediately after unlocking

**Example Dark Path Progression:**
```
Ring 1 (Dark): Player chooses → Unlocks Nekomata's Call (Necromancy)
Ring 2 (Dark): Player chooses → Upgrades Nekomata's Call
Ring 3 (Dark): Player chooses → Unlocks Soul Burn
Ring 4 (Dark): Player chooses → Unlocks Spirit Judgment (ultimate)
Ring 5 (Dark): Player chooses → Upgrades Spirit Judgment
```

## Why Option B?
- Maximum player expression
- More replayability
- Immediate reward after every boss
- Industry standard (Skyrim, God of War, Elden Ring)

---

# 10. SKILL TREE UI

## Access
- Player can VIEW anytime (pause menu)
- Can only SPEND rings at Secret Tree (after boss)

## Visual Layout

```
┌─────────────────────────────────────────────────────────────┐
│                        SKILL TREE                           │
├────────────────────────┬────────────────────────────────────┤
│      DARK PATH         │         LIGHT PATH                 │
│      (Left Tail)       │         (Right Tail)               │
├────────────────────────┼────────────────────────────────────┤
│                        │                                    │
│  ┌──────────┐ ┌──────┐ │  ┌────────────┐ ┌──────┐          │
│  │Soul Burn │→│ UP   │ │  │Purification│→│ UP   │          │
│  └──────────┘ └──────┘ │  │   Burst    │ │      │          │
│                        │  └────────────┘ └──────┘          │
│  ┌──────────┐ ┌──────┐ │                                    │
│  │ Spirit   │→│ UP   │ │  ┌────────────┐ ┌──────┐          │
│  │ Surge    │ │      │ │  │ Calming    │→│ UP   │          │
│  └──────────┘ └──────┘ │  │   Mist     │ │      │          │
│                        │  └────────────┘ └──────┘          │
│  ┌──────────┐ ┌──────┐ │                                    │
│  │Nekomata's│→│ UP   │ │  ┌────────────┐ ┌──────┐          │
│  │  Call    │ │      │ │  │  Healing   │→│ UP   │          │
│  └──────────┘ └──────┘ │  │  Whisper   │ │      │          │
│                        │  └────────────┘ └──────┘          │
│  ┌──────────┐ ┌──────┐ │                                    │
│  │ Inferno  │→│ UP   │ │  ┌────────────┐ ┌──────┐          │
│  │ Spiral   │ │      │ │  │ Soul Sight │→│ UP   │          │
│  └──────────┘ └──────┘ │  └────────────┘ └──────┘          │
│                        │                                    │
│  ┌──────────┐ ┌──────┐ │  ┌────────────┐ ┌──────┐          │
│  │ Spirit   │→│ UP   │ │  │  Eclipse   │→│ UP   │          │
│  │ Judgment │ │      │ │  │   Mercy    │ │      │          │
│  └──────────┘ └──────┘ │  └────────────┘ └──────┘          │
│                        │                                    │
├────────────────────────┴────────────────────────────────────┤
│  Left Rings: ●●●○○○○○○○     Right Rings: ●●○○○○○○○○        │
│                                                             │
│  ╔═══════════════════════════════════════════════════════╗ │
│  ║  🔔 RING AVAILABLE! Select ability to unlock/upgrade. ║ │
│  ╚═══════════════════════════════════════════════════════╝ │
└─────────────────────────────────────────────────────────────┘
```

---

# 11. DARK PATH ABILITIES (Left Tail)

## Ring Ability 1: Soul Burn

| Property | Base | Upgraded: Soul Inferno |
|----------|------|------------------------|
| **What it does** | Charged fireball that burns enemy | + AoE explosion |
| **Input** | Hold 1 to charge, release to fire | Same |
| **Animation** | Ability_LeftTail_Slow | Same |
| **Projectile damage** | 30 | 30 |
| **Burn effect** | 5 dmg/sec × 3 sec = 15 | Same |
| **Total damage** | 45 | 45 + AoE to nearby |
| **AoE radius** | None | 3 meters |
| **Cooldown** | 8 seconds | 8 seconds |
| **Cost** | None (cooldown only) | None |
| **Note** | Slow animation — use when enemy staggered | Same |

## Ring Ability 2: Spirit Surge

| Property | Base | Upgraded |
|----------|------|----------|
| **What it does** | Reset ALL ability cooldowns + NO cooldowns for duration | Longer duration |
| **Effect** | Spam any ability freely (like Mario Kart star) | Same |
| **Duration** | 4 seconds | 8 seconds |
| **Cooldown** | 90 seconds | 75 seconds |
| **Animation** | Yoru glows with dark energy | Same, more intense |
| **VFX** | Dark aura, speed lines | Same |
| **Best use** | Save for tough fights, boss phases | Same |

## Ring Ability 3: Nekomata's Call (Necromancy)

| Property | Base | Upgraded: Undying Legion |
|----------|------|--------------------------|
| **What it does** | Summon ghost of defeated enemy | Gems more efficient |
| **Effect** | Ghost ally follows and attacks | Same |
| **Gem efficiency** | 1 gem = 1 gem | 1 gem = 2 gems |
| **Cooldown** | 45 seconds | 45 seconds |
| **Animation** | Cinematic_FreeingSoul (1.5x speed) | Same |
| **VFX** | Purple ground circle, ghost rises | Same, more intense |

*See Section 13 for complete Necromancy System*

## Ring Ability 4: Inferno Spiral

| Property | Base | Upgraded: Hellfire Ring |
|----------|------|-------------------------|
| **What it does** | AoE fire spin, hits ALL nearby enemies | + Burning ground |
| **Damage** | 60 to all in range | 60 + ground fire |
| **AoE radius** | 4 meters | 4 meters |
| **Ground fire** | None | 5 sec, 10 dmg/sec |
| **Cooldown** | 15 seconds | 15 seconds |
| **Animation** | Ability_InfernoSpiral | Same |
| **Different from Combo 3** | Hits ALL enemies, has fire | Combo 3 = ONE enemy |

## Ring Ability 5: Spirit Judgment (Ultimate)

| Property | Base | Upgraded: Eclipse Wrath |
|----------|------|-------------------------|
| **What it does** | Slow time, mark enemies, teleport-execute | More marks |
| **Max marks** | 5 | 7 |
| **Damage per mark** | 40-50 (karma-based) | Same |
| **Total damage** | 200-250 | 280-350 |
| **Cost** | Lose 1 Peach (Dark path) | Same |
| **Charge required** | 100% (builds from combat) | Same |
| **Cooldown** | 60 seconds | 60 seconds |

*See Section 14 for complete Spirit Judgment details*

---

# 12. LIGHT PATH ABILITIES (Right Tail)

## Ring Ability 1: Purification Burst

| Property | Base | Upgraded: Cleansing Wave |
|----------|------|--------------------------|
| **What it does** | Stun enemies + open dialogue option | Larger, longer |
| **Damage** | 0 (NO damage — Light path) | 0 |
| **Stun duration** | 2 seconds | 3 seconds |
| **Radius** | 5 meters | 8 meters |
| **Cooldown** | 10 seconds | 10 seconds |
| **Animation** | Ability_RightTail_Fast | Same |
| **Special** | During stun, "Speak" prompt appears on Tier 3-4 enemies | Same |
| **Dialogue opener** | Press E to start conversation instead of attacking | Same |

## Ring Ability 2: Calming Mist (Kiri)

| Property | Base | Upgraded |
|----------|------|----------|
| **What it does** | Mist resets enemy memory — they forget the battle | Affects more enemies |
| **Effect** | Enemy returns to calm state | Same |
| **Use case** | Failed persuasion? Use Calming Mist → Try dialogue again from zero | Same |
| **Damage** | 0 (NO damage — Light path) | 0 |
| **Duration** | Instant effect, enemy stays calm until attacked | Same |
| **Radius** | 5 meters (1 enemy) | 10 meters (multiple) |
| **Cooldown** | 20 seconds | 20 seconds |
| **Animation** | Mist spreads from Yoru | Same, larger |
| **VFX** | Soft fog/mist particles | Same |
| **Japanese origin** | Kiri (霧) — sacred mist in Japanese mountains | — |

## Ring Ability 3: Healing Whisper

| Property | Base | Upgraded: Rejuvenation |
|----------|------|------------------------|
| **What it does** | Heal Yoru over time | More healing + cleanse |
| **Healing** | 2 HP over 3 seconds | 4 HP over 3 seconds |
| **Cleanse** | No | Removes burns, poison |
| **Cooldown** | 15 seconds | 15 seconds |
| **Animation** | Ability_RightTail_Slow | Same |
| **VFX** | Green/gold particles flowing in | Same |

## Ring Ability 4: Soul Sight

| Property | Base | Upgraded: True Vision |
|----------|------|----------------------|
| **What it does** | Reveal hidden things in the world | + See attack patterns |
| **Reveals** | Hidden paths, items, weak points | + Enemy telegraphs glow |
| **Duration** | 10 seconds | 20 seconds |
| **Cooldown** | 25 seconds | 25 seconds |
| **Animation** | Eyes glow | Same |
| **VFX** | World highlights, overlay effect | Same + telegraph indicators |

## Ring Ability 5: Eclipse Mercy (Ultimate)

| Property | Base | Upgraded: Divine Restoration |
|----------|------|------------------------------|
| **What it does** | Mark and HEAL YOURSELF (not enemies!) | More marks + invincibility |
| **Max marks** | 5 (on self and allies) | 7 |
| **Healing per mark** | 4 HP | 4 HP |
| **Total healing** | 20 HP (5 Peaches) | 28 HP (7 Peaches) |
| **Invincibility** | None | 3 seconds after |
| **Cost** | None (free!) | None |
| **Cooldown** | 60 seconds | 60 seconds |
| **Animation** | Same as Spirit Judgment, gold VFX | Same |

**Important**: Eclipse Mercy heals YORU (the player), NOT enemies! Mark yourself multiple times for big heal.

---

# 13. NECROMANCY SYSTEM

## Overview
Nekomata's signature power — raising the dead to fight alongside you.

## Core Rule: One Soul Per Enemy TYPE

```
Player kills throughout game:
- 12 Kodama (same type)
- 5 Kasa-obake (same type)
- 1 Hitotsume-kozō

Stored in Necromancy Book:
- 1 Kodama soul
- 1 Kasa-obake soul
- 1 Hitotsume-kozō soul

Total: 3 unique souls (NOT 18)
```

## Storable Enemy Tiers

| Tier | Description | Count | Storable? | Gem Cost |
|------|-------------|-------|-----------|----------|
| Tier 1 | Main Bosses | 10 | ❌ NO | — |
| Tier 2 | Offering Guardians | 20 | ✅ YES | 6 gems (3 upgraded) |
| Tier 3 | Mixed Enemies | 10 | ✅ YES | 4 gems (2 upgraded) |
| Tier 4 | Common Enemies | 15 | ✅ YES | 2 gems (1 upgraded) |
| **Total** | | **55** | **45 storable** | |

**Why Tier 1 (Bosses) not storable:**
- First boss = no rings yet = no necromancy ability
- Bosses too powerful, would break balance
- Keeps boss encounters special

## Soul Gems

| Property | Details |
|----------|---------|
| **Type** | Collectible resource |
| **Total in game** | 95 gems |
| **Found in** | World exploration (70) + Enemy drops (25) |
| **Sellable** | Yes (good price) |
| **Buyback** | Yes (15% markup) |

## Ghost Behavior

| Property | Value |
|----------|-------|
| Appearance | Semi-transparent (50% opacity) |
| HP | 50% of original enemy |
| Damage | 75% of original enemy |
| Follow Range | 15m from player |
| AI | Auto-attacks nearest enemy |
| Duration | 10 + (rings × 2) + (peaches × 1) seconds |

## Necromancy Book UI

```
┌─────────────────────────────────────────────────────────────┐
│                    NECROMANCY BOOK                          │
├─────────────────────────────────────────────────────────────┤
│  Soul Gems: ◆◆◆◆◆◆◆◆ (8 available)                         │
│  Ghost Duration: 25 seconds                                 │
│  Undying Legion: [ACTIVE] (gems count as 2)                 │
├─────────────────────────────────────────────────────────────┤
│  TIER 4 — COMMON (2 gems each, 1 with upgrade)             │
│  [ ] Kodama               [Collected ✓]                    │
│  [ ] Kasa-obake           [Collected ✓]                    │
│                                                             │
│  TIER 3 — MIXED (4 gems each, 2 with upgrade)              │
│  [ ] Rokurokubi           [Collected ✓]                    │
│                                                             │
│  TIER 2 — GUARDIANS (6 gems each, 3 with upgrade)          │
│  [ ] Torii Guardian A     [Not Found]                      │
├─────────────────────────────────────────────────────────────┤
│  Selected: 2 souls | Cost: 3 gems | [SUMMON] [CANCEL]      │
└─────────────────────────────────────────────────────────────┘
```

---

# 14. SPIRIT JUDGMENT ULTIMATE

## Charge System

| Action | Charge Gained |
|--------|---------------|
| Basic attack hit | +2% |
| Perfect dodge | +3% |
| Parry success | +5% |
| Enemy kill | +10% |

**Visual**: Tails pulse/glow when 100% charged

## Execution Flow

```
PHASE 1: ACTIVATION (Hold R)
─────────────────────────────
- Time slows to 0.1x
- Animation: Combat_Parry
- Screen desaturates
- Duration: 3 seconds max

PHASE 2: MARKING (While R held)
─────────────────────────────
- Move mouse over enemies
- Left-click to mark (kanji symbol)
- Max: 5 marks (7 upgraded)
- Can mark same enemy multiple times

PHASE 3: EXECUTION (Release R)
─────────────────────────────
- Time snaps back to normal
- For each mark (2x animation speed):
  → Combat_DiveAttack (teleport)
  → Combat_Combo1_Paw (strike)
- Total: ~1.5-2 seconds

PHASE 4: AFTERMATH
─────────────────────────────
- All damage applied at once
- Cost applied based on karma
- Cooldown begins (60 sec)
```

## Karma Variants

| Path | Mark Color | Damage/Mark | Cost |
|------|------------|-------------|------|
| Dark (more left rings) | Crimson | 50 | Lose 1 Peach |
| Light (more right rings) | Gold | 30 | None |
| Balanced | Purple | 40 | None |

---

# 15. KARMA & DIALOGUE INTEGRATION

## Balance Calculation

```
Karma Score = Right Rings - Left Rings
Range: -10 (pure dark) to +10 (pure light)
```

## Dialogue Difficulty Modifiers

| Karma Score | Path Status | Dialogue Effect |
|-------------|-------------|-----------------|
| 0 | Balanced | No modifier |
| ±1 | Slightly tilted | No modifier |
| ±2 | Leaning | ±5% success chance |
| ±3 | Committed | ±10% success |
| ±4 | Devoted | ±15% success |
| ±5+ | Extreme | ±20% success |

## Light Path Dialogue Bonuses

- Ring 3+: Heart Reading unlocked (see success %)
- More Right Rings = easier persuasion
- Calming Mist allows retry from zero

---

# 16. STONE SYSTEM

## Complete Stone List (10 Stones)

| # | Stone | Affects | Effect |
|---|-------|---------|--------|
| 1 | Paw Stone | Combo 1, 2, Flurry Rush | +25% damage |
| 2 | Spin Stone | Combo 3 | +25% damage |
| 3 | Heavy Stone | Heavy Attack | +25% damage |
| 4 | Movement Stone | Pounce | +25% damage |
| 5 | Left Tail Stone | Basic Left Tail | 5 projectiles instead of 1 |
| 6 | Right Tail Stone | Basic Right Tail | Blocks 3 hits, 5 seconds |
| 7 | Parry Stone | Parry window | +0.1 second window |
| 8 | Counter Stone | Parry Counter | +50% damage |
| 9 | Flurry Stone | Flurry Rush | +2 extra hits (8 total) |
| 10 | Speed Stone | Movement speed | +10% run speed |

## Stone Slots

| Base Slots | 3 |
|------------|---|
| Max Slots | 10 |
| Upgrade method | Magatama collectibles |

## Stone Properties

- **Equippable**: Must be in slot to gain bonus
- **Sellable**: Yes (gains Mon, loses bonus)
- **Buyback**: Yes (15% markup, regains bonus)
- **Earned via**: Side quests (unlock with each ring)

---

# 17. MAGATAMA COLLECTIBLE SYSTEM

## What is Magatama?

| Property | Details |
|----------|---------|
| **Name** | Magatama (勾玉) |
| **Description** | Ancient Japanese curved jewels with spiritual power |
| **History** | Part of Japanese Imperial Regalia |
| **In YORU** | Collectibles hidden throughout world |
| **Visual** | Comma-shaped glowing jewels |

## Slot Expansion

| Magatama Found | Stone Slots |
|----------------|-------------|
| 0 | 3 (starting) |
| 3 | 4 |
| 6 | 5 |
| 9 | 6 |
| 12 | 7 |
| 15 | 8 |
| 18 | 9 |
| 21 | 10 (maximum) |

**3 Magatama = 1 additional stone slot**
**Total Magatama in game: 21**

## Where to Find
- Hidden in shrines
- Reward for exploration
- Secret areas
- Optional puzzles

---

# 18. SIDE QUEST SYSTEM (For Stones)

## How It Works

```
1. Player earns Ring (from boss via Tree)
2. Side Quest drops to Quest Book
3. Quest = specific challenge (kill with combo, parry attacks, etc.)
4. Complete quest → Receive Stone
5. Equip stone → Ability upgraded
```

## Quest List

| Ring # | Quest Name | Stone Reward | Requirement |
|--------|------------|--------------|-------------|
| 1 | Paw Mastery | Paw Stone | 20 kill points with combo |
| 2 | Spin Mastery | Spin Stone | 15 kill points with spin |
| 3 | Heavy Mastery | Heavy Stone | 15 kill points with heavy |
| 4 | Movement Mastery | Movement Stone | 15 kill points with pounce |
| 5 | Left Tail Mastery | Left Tail Stone | 20 kill points with left tail |
| 6 | Right Tail Mastery | Right Tail Stone | Block 30 attacks |
| 7 | Parry Mastery | Parry Stone | Parry 30 attacks |
| 8 | Counter Mastery | Counter Stone | 15 kill points with counter |
| 9 | Flurry Mastery | Flurry Stone | Perform 15 Flurry Rushes |
| 10 | Speed Mastery | Speed Stone | Run 5000 meters total |

**Note**: Kill points vary by tier (see Section 19)

---

# 19. KILL POINT SYSTEM

## Points Per Enemy Tier

| Tier | Enemy Type | Points per Kill |
|------|------------|-----------------|
| Tier 1 | Main Bosses | 0 (don't count) |
| Tier 2 | Offering Guardians | 3 points |
| Tier 3 | Mixed Enemies | 2 points |
| Tier 4 | Common Enemies | 1 point |

## Why Bosses Don't Count
- Bosses give Rings (already rewarding)
- Respecting player choice (Empathy players not punished)
- Bosses don't respawn (can't "catch up")
- Cleaner separation of reward systems

## Total Points Available

```
Fixed points (one-time kills):
- 20 Guardians × 3 points = 60 points
- 10 Mixed × 2 points × 11 cycles = 220 points (with respawns)

Farmable points:
- 15 Common × 1 point × unlimited = ∞

Total from playing normally: ~280 points
```

---

# 20. ENEMY TIER SYSTEM

## Overview

| Tier | Name | Count | HP Range | Respawn | Storable |
|------|------|-------|----------|---------|----------|
| 1 | Main Bosses | 10 | 8,000-12,000 | ❌ No | ❌ No |
| 2 | Offering Guardians | 20 | 1,500-2,000 | ❌ No | ✅ Yes |
| 3 | Mixed Enemies | 10 | 500-800 | ✅ Yes (on ring) | ✅ Yes |
| 4 | Common Enemies | 15 | 100-300 | ✅ Yes (area) | ✅ Yes |

## Guardian Details
- 2 Guardians per Torii gate
- 10 Torii gates total = 20 Guardians
- Do NOT respawn (one-time encounters)

---

# 21. ENEMY RESPAWN SYSTEM

## Respawn Rules

| Tier | Respawns? | Trigger |
|------|-----------|---------|
| Tier 4 (Common) | ✅ Yes | Player 50m+ away AND 2 minutes pass |
| Tier 3 (Mixed) | ✅ Yes | When player earns a Ring (max 10 times) |
| Tier 2 (Guardian) | ❌ No | One-time kills |
| Tier 1 (Boss) | ❌ No | Story progression |

## God of War Style (Chosen)
- Natural feeling during exploration
- Player doesn't feel "punished" by sudden respawn
- Distance + time = predictable, adjustable

---

# 22. ENEMY DESIGN: WITHERED KODAMA

## Overview
Tier 4 common enemy — first enemy to build for testing combat.

## Stats

| Property | Value |
|----------|-------|
| HP | 150 |
| Damage | 8 per hit |
| Detection Range | 8m |
| Attack Range | 2m |
| Kill Points | 1 |
| Storable Soul | Yes (2 gems) |
| Respawns | Yes (distance + time) |

## AI State Machine

```
IDLE (Patrol)
    │
    ▼ (player within 8m)
ALERT (0.5s) ──── sound cue
    │
    ▼
CHASE ──────────── runs toward player
    │
    ▼ (within 2m)
TELEGRAPH (0.5s) ── PARRY WINDOW
    │
    ▼
ATTACK (0.3s)
    │
    ▼
RECOVERY (0.4s)
    │
    └───► back to CHASE

SPECIAL:
- Heavy hit/parried → STAGGER (1.2s)
- HP ≤ 0 → DEATH → corpse stays 60s
- Player > 15m → return to IDLE
```

## Required Animations (14)

| Priority | Animation | Duration |
|----------|-----------|----------|
| HIGH | Idle | 2-3s loop |
| HIGH | Walk/Patrol | 1s cycle |
| HIGH | Alert | 0.5s |
| HIGH | Chase/Run | 0.8s cycle |
| HIGH | Attack Telegraph | 0.4-0.6s |
| HIGH | Attack | 0.3-0.5s |
| HIGH | Attack Recovery | 0.3-0.5s |
| HIGH | Hit Reaction Light | 0.3s |
| HIGH | Hit Reaction Heavy | 0.6s |
| HIGH | Stagger | 1-2s loop |
| HIGH | Death | 1.5s |
| LOW | Attack Variant 2 | 0.5s |
| LOW | Knockback | 0.5s |
| LOW | Taunt | 1s |

---

# 23. ANIMATION INVENTORY

## Combat Folder (Assets/Animations_Yoru/Combat/)

| Animation | Status | Use |
|-----------|--------|-----|
| Combat_Combo1_Paw | ✅ Have | Combo 1, Counter, Flurry |
| Combat_Combo2_TwoPaw | ✅ Have | Combo 2 |
| Combat_Combo3_Spin | ✅ Have | Combo 3 (2-leg) |
| Combat_Combo3_Spin_4legs | ✅ Have | Combo 3 (4-leg), Inferno base |
| Combat_Death_2Leg | ✅ Have | Death (standing) |
| Combat_Dodge_2Leg | ✅ Have | Backflip dodge |
| Combat_Dodge_4Leg | ✅ Have | Roll dodge |
| Combat_DodgeDash_2Leg | ✅ Have | Forward dash |
| Combat_DodgeDash_4Leg | ✅ Have | Forward dash (4-leg) |
| Combat_HitReact_2Leg | ✅ Have | Big hit reaction |
| Combat_HitReact_2Leg_small | ✅ Have | Light hit |
| Combat_HitReact_4Leg | ✅ Have | Hit reaction (4-leg) |
| Combat_Parry | ✅ Have | Parry, Spirit Judgment activation |

## Ability Folder (Assets/Animations_Yoru/Ability/)

| Animation | Status | Use |
|-----------|--------|-----|
| Ability_InfernoSpiral | ✅ Have | Ring 4 Dark |
| Ability_LeftTail_Fast | ✅ Have | Basic left tail |
| Ability_LeftTail_Slow | ✅ Have | Soul Burn |
| Ability_RightTail_Fast | ✅ Have | Basic right tail, Purification |
| Ability_RightTail_Slow | ✅ Have | Healing Whisper |

## Movement Folder (Assets/Animations_Yoru/Movement/)

| Animation | Status | Use |
|-----------|--------|-----|
| Movement_Idle_2Leg | ✅ Have | Standing |
| Movement_Idle_2leg_2 | ✅ Have | Idle variant |
| Movement_Idle_4Leg | ✅ Have | 4-leg idle |
| Movement_Jump_2Leg | ✅ Have | Jump |
| Movement_Jump_4Leg | ✅ Have | Jump (4-leg) |
| Movement_Run_4Leg | ✅ Have | Sprint |
| Movement_Walk_2Leg | ✅ Have | Walk |

## Cinematic Folder (Assets/Animations_Yoru/Cinematic/)

| Animation | Status | Use |
|-----------|--------|-----|
| Cinematic_Absorbing | ✅ Have | Dark choice |
| Cinematic_CircleActivation | ✅ Have | Ring unlock |
| Cinematic_FreeingSoul | ✅ Have | Light choice, Necromancy |
| Cinematic_Heart | ✅ Have | Special moment |
| Cinematic_Sleep | ✅ Have | Rest |
| Cinematic_WakeUp | ✅ Have | Wake |

---

# 24. ANIMATION REUSE STRATEGY

| Feature | Animations Used | How |
|---------|-----------------|-----|
| Spirit Judgment | Combat_Parry + Combat_DiveAttack + Combat_Combo1_Paw | 2x speed, teleport between |
| Flurry Rush | Combat_Combo1_Paw | Rapid repeat |
| Necromancy | Cinematic_FreeingSoul | 1.5x speed, purple VFX |
| Inferno Spiral | Ability_InfernoSpiral OR Combat_Combo3_Spin_4legs | Add fire VFX |
| Spirit Surge | Existing combat animations | Dark aura VFX overlay |
| Calming Mist | Ability_RightTail_Slow OR new pose | Mist VFX |

---

# 25. ANIMATIONS STILL NEEDED

| Animation | Purpose | Priority |
|-----------|---------|----------|
| Combat_Death_4Leg | Death while sprinting | HIGH |
| Combat_Death_Jump | Death while airborne | HIGH |
| Combat_Pounce | Sprint attack | HIGH |
| Combat_DiveAttack | Aerial plunge, Spirit Judgment | HIGH |
| Calming_Mist_Cast | Mist ability pose (optional) | LOW |
| Spirit_Surge_Activate | Power-up pose (optional) | LOW |

## State-Based Death System

```csharp
// When player dies, check state
if (isAirborne)
{
    Play("Combat_Death_Jump");
}
else if (isRunning)
{
    Play("Combat_Death_4Leg");
}
else
{
    Play("Combat_Death_2Leg");
}
```

---

# 26. VFX & SOUND REQUIREMENTS

## Combat VFX

| Action | VFX |
|--------|-----|
| Combo hits | White/blue slash trails |
| Combo 3 spin | Forward rushing tornado effect |
| Parry success | Golden flash + ripple |
| Perfect dodge | Time distortion bubble |
| Flurry rush | Multiple rapid slash trails |
| Hit received | Red flash |

## Ability VFX

| Ability | VFX |
|---------|-----|
| Soul Burn | Blue/purple fireball + trail + burn |
| Spirit Surge | Dark aura, energy crackling, speed lines |
| Nekomata's Call | Purple ground circle, ghost rises |
| Inferno Spiral | Fire spiral, embers, scorch marks |
| Spirit Judgment | Time distortion, marks, slash trails |
| Purification | Golden pulse wave, sparkles |
| Calming Mist | Soft fog spreading, calming particles |
| Healing | Green/gold particles flowing in |
| Soul Sight | Eyes glow, world highlights |
| Eclipse Mercy | Gold marks, healing particles |

## Sound Design

| Action | Sound |
|--------|-------|
| Combo | Whoosh + impact |
| Parry | Clang + "whomp" |
| Perfect dodge | Slow-mo "whomp" |
| Soul Burn | Fire whoosh + crackle |
| Spirit Surge | Power-up hum + energy burst |
| Necromancy | Rumble + ghostly wail |
| Calming Mist | Soft wind + chimes |
| Spirit Judgment | Time stop + stamps + slashes |

---

# 27. INTEGRATION WITH EXISTING SYSTEMS

## WorldStateManager

```csharp
// After Tree grants ring
WorldStateManager.Instance.AddLeftRing(); // Dark
// OR
WorldStateManager.Instance.AddRightRing(); // Light

// Triggers atmosphere change (29-state system)
```

## PlayerHealth

```csharp
// 1 Peach = 4 HP
playerHealth.TakeDamage(amount);
playerHealth.Heal(amount);

// Spirit Judgment dark cost
playerHealth.TakeDamage(4); // 1 Peach
```

## Existing Scripts to Modify

| Script | Changes |
|--------|---------|
| PlayerMovement.cs | Add IsAirborne(), IsRunning() getters |
| PlayerHealth.cs | Add socket expansion, state-based death |
| EnemyCombat.cs | Add Telegraph, Stagger states |
| EnemyHealth.cs | Add soul drop, corpse system |

---

# 28. IMPLEMENTATION ROADMAP

## Phase 1: Core Combat (Weeks 1-2)
- Create PlayerCombat.cs
- Basic combo (3-hit)
- Hit detection
- Enemy takes damage

## Phase 2: Enemy AI — Kodama (Weeks 3-4)
- State machine (Idle → Alert → Chase → Attack)
- Telegraph system
- Stagger state

## Phase 3: Defensive Mechanics (Weeks 5-6)
- Dodge with i-frames
- Parry system
- Perfect dodge → Flurry Rush

## Phase 4: Kill Points & Stones (Week 7)
- KillPointManager.cs
- Side quest triggers
- Stone equip system

## Phase 5: Second Tier 4 Enemy (Week 8)
- New enemy type
- Different attack patterns

## Phase 6: Basic Tail Abilities (Week 9)
- Left Tail (projectile)
- Right Tail (barrier)
- Stone upgrades

## Phase 7: Tier 3 Enemy (Weeks 10-11)
- Harder AI
- Respawn on ring system

## Phase 8: Ring Abilities (Weeks 12-13)
- Soul Burn, Purification
- Ability Wheel UI
- Skill Tree UI

## Phase 9: Tier 2 Guardian (Weeks 14-15)
- Mini-boss AI
- No respawn

## Phase 10: Remaining Abilities (Weeks 16-18)
- Spirit Surge, Calming Mist
- Necromancy system
- Soul Sight, Healing

## Phase 11: Ultimates (Weeks 19-20)
- Spirit Judgment
- Eclipse Mercy
- Charge system

## Phase 12: Tier 1 Boss (Weeks 21-23)
- Full boss AI
- Combat/Empathy choice
- Soul → Tree flow

## Phase 13: Magatama & Polish (Weeks 24-26)
- Collectible placement
- Balance pass
- VFX/Sound pass

---

# 29. TESTING STAGES

| Stage | Test | Enemy |
|-------|------|-------|
| 1 | Basic combo deals damage | Kodama (static) |
| 2 | Enemy AI works | Kodama (AI on) |
| 3 | Dodge i-frames | Kodama |
| 4 | Parry → Stagger → Counter | Kodama |
| 5 | Perfect dodge → Flurry | Kodama |
| 6 | Kill points accumulate | Kodama |
| 7 | Stone bonuses apply | Kodama |
| 8 | Basic tails work | Kodama |
| 9 | Enemy variety | Second Tier 4 |
| 10 | Harder combat | Tier 3 |
| 11 | Ring abilities | Tier 3 |
| 12 | Mini-boss | Tier 2 Guardian |
| 13 | Full boss + choice | Tier 1 Boss |

---

# 30. KEY DECISIONS LOG

| Topic | Decision | Reason |
|-------|----------|--------|
| Genre | Narrative Action-Adventure | Combat + Story + Exploration |
| Ability cost | Cooldown only (no mana) | Player freedom, action game standard |
| Ability unlock | Option B (full choice) | Player expression |
| Necromancy souls | 1 per enemy TYPE | Prevent system overload |
| Boss souls | NOT storable | Balance, no rings at first boss |
| Respawn | God of War style (distance + time) | Natural, not punishing |
| Spirit Surge | 4s base, 8s upgraded, 90s/75s cooldown | Mario Kart star feel |
| Calming Mist | Reset enemy memory for dialogue retry | Light path utility |
| Right Tail | Protection only (NO damage) | Light path = non-violent |
| Combo 3 | Single target (Tasmanian spin) | Differentiate from Inferno Spiral |
| Dive Attack | No stone (exploration focused) | Not main combat tool |
| Health choice | After Tree, Health OR Energy socket | Player agency |
| Stone slots | 3 base, +1 per 3 Magatama | Exploration reward |
| Kill points | Tier-based (1/2/3), bosses = 0 | Fair progression |
| Death animation | State-based (standing/running/airborne) | Polish |

---

# APPENDIX A: ABILITY QUICK REFERENCE

## Default Abilities (Free)

| Ability | Damage | Stone |
|---------|--------|-------|
| Combo 1 | 10 | Paw +25% |
| Combo 2 | 20 | Paw +25% |
| Combo 3 | 35 | Spin +25% |
| Heavy | 50-80 | Heavy +25% |
| Pounce | 40 | Movement +25% |
| Dive | 45 | None |
| Parry Counter | 60 | Counter +50% |
| Flurry Rush | 90 | Flurry +2 hits |
| Left Tail | 5 | 5 projectiles |
| Right Tail | 0 (block) | 3 hits, 5 sec |

## Dark Ring Abilities

| # | Ability | Effect | Cooldown |
|---|---------|--------|----------|
| 1 | Soul Burn | 45 dmg + burn | 8s |
| 2 | Spirit Surge | No cooldowns 4-8s | 90-75s |
| 3 | Nekomata's Call | Summon ghost | 45s |
| 4 | Inferno Spiral | 60 AoE fire | 15s |
| 5 | Spirit Judgment | 200-350 dmg ultimate | 60s |

## Light Ring Abilities

| # | Ability | Effect | Cooldown |
|---|---------|--------|----------|
| 1 | Purification Burst | Stun + dialogue | 10s |
| 2 | Calming Mist | Reset enemy memory | 20s |
| 3 | Healing Whisper | Heal 2-4 HP | 15s |
| 4 | Soul Sight | Reveal hidden | 25s |
| 5 | Eclipse Mercy | Heal 20-28 HP ultimate | 60s |

---

# APPENDIX B: STONE QUICK REFERENCE

| # | Stone | Effect | Quest Requirement |
|---|-------|--------|-------------------|
| 1 | Paw | +25% combo 1,2 | 20 kill pts with combo |
| 2 | Spin | +25% combo 3 | 15 kill pts with spin |
| 3 | Heavy | +25% heavy | 15 kill pts with heavy |
| 4 | Movement | +25% pounce | 15 kill pts with pounce |
| 5 | Left Tail | 5 projectiles | 20 kill pts with tail |
| 6 | Right Tail | 3 hits, 5 sec | Block 30 attacks |
| 7 | Parry | +0.1s window | Parry 30 attacks |
| 8 | Counter | +50% counter | 15 kill pts with counter |
| 9 | Flurry | +2 hits | 15 Flurry Rushes |
| 10 | Speed | +10% speed | Run 5000m |

---

*Document created for Hazel / Pectus Games*
*YORU: Eclipse of Tails — Combat System Design v3.0*
*January 2026*

*This document contains ALL discussions from the design session.*
*Ready for implementation phase.*
