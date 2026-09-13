# YORU: Eclipse of Tails. Estimated dates v1

Pectus Games. Sept 13, 2026. Companion to YORU_Demo_Roadmap_v1.md (the step-by-step list). Built from the local project (Documents/Yoru, Gamefeel branch), the GDD_May folder, the Claude outputs handoffs, the git history, and your answers of Sept 13. Every number below says where it came from. Change an input, and I recompute.

## 0. The answer in five lines

- Demo (GDD first loop, full): ready to submit between Sept 15, 2027 (nothing slips) and Nov 23, 2027 (normal 20% slip).
- Steam Next Fest: target the October 2027 edition. Feb 2027 is impossible, June 2027 is not reachable with the full loop, and Next Fest is one shot per game.
- Steam page public with the announcement trailer: Feb 26, 2027 target, March 31, 2027 at the latest.
- Evolve: nothing before Feb 2027. Announce beat March 2027, Demo beat Sept to Oct 2027.
- Release: Q1 2028 is not reachable for the full game. Realistic window is 2029. Section 6 shows why and what changes it.

## 1. What I measured

Project facts (read on Sept 12 to 13, 2026):

- Local repo: 422 commits since Sept 17, 2025. Active days per month: 10 to 21. The GitHub master is 6 months stale, the local folder is the truth.
- Code: about 44,000 lines of C# in Assets/Scripts. Built and working: player movement, combat phases 3A to 3D (dodge, parry, flurry, game feel), tail projectiles, climbing, cat to Granny transform, balance system (all five controllers, 6,184 lines), inventory and item examine, quests and dialogue (Yuki guide, Kodama quest assets, Mujina reveal), Komainu boss and gate, Oni boss (3,774 lines, rounds 8 to 80+, phase 2 cinematic, boss bar), Memory Parchments UI.
- Not in code at all: soul orb drop and pickup, Ancient Tree ritual, ring grant flow (rings change only by debug keys), socket choice, ability system and Grimoire, memory clip playback, Oni persuasion (Echo Walk), main menu, pause and settings, save and load (only InventoryManager touches PlayerPrefs), gamepad support (old Input Manager, no gamepad code), demo end screen. No build has ever been made. No audio: CombatMusicManager not in scene, 19 SFX slots empty, no AudioMixer, no composer hired.
- Content state from GDD 07 and the handoffs: Kodama and Hitotsume built with known issues; Noppera-bo in DemoScene_Day with phase 2 states not wired; Komainu built; Oni fight plays but the look goes back to the modeller (texture brief delivered Sept 12), lighting bake and braziers pending, phase 2 lightning over-spawn open, hit reactions under diagnosis (Sept 10).
- Animation: Yoru clips delivered include the three the tree ritual needs (15 absorbing, 16 freeing soul, 17 circle activation). Oni skeleton motion is with the animator. Granny (5), Kodama (11), Hitotsume (13), Komainu (17) clip batches: hiring status not confirmed.

Velocity anchors (how long comparable work actually took you):

- Oni boss fight: Aug 17 to Sept 12, 2026, about 4 weeks at 20 to 25 h per week, roughly 100 hours, to reach "fight plays, look and audio unresolved". That is the cost of one boss-scale system before persuasion, cinematics and audio.
- Balance system overhaul: May to June 2026, about 6 weeks, five controllers.
- Combat phase 3 (dodge, parry, flurry, game feel): May to July 2026, about 10 weeks.
- Pattern: one boss-scale system per 4 to 6 weeks, one mid-size system per 2 to 3 weeks, at 20 to 25 h per week, working with Claude the whole time. The estimates below already include Claude, so there is no extra speed-up to add.

Capacity model (your answer): 22 h per week until Oct 31, 2026, then 40 h per week from Nov 1, 2026. I count 85% of those hours as build time; the rest goes to contractors, marketing, admin and life. That gives 128 hours before Nov 1 and 34 hours per week after.

## 2. Work remaining, in hours of your time

Scenario A = the demo you chose (GDD first loop, full). B = same loop with the Oni persuasion stubbed (forced combat) and fewer quests. C = Oni cave plus tree ritual only. Contractor hours are not in this table; they run in parallel (section 7 of the roadmap).

| Package | A full loop | B fight only | C cave only | Basis |
|---|---|---|---|---|
| 1. Oni fight closure (hit reactions, phase 2 lightning, intro cinematic, arena bake, orb drop, texture integration, audio hooks) | 124 | 124 | 124 | Handoff 11 open list, HITREACT diagnosis, GDD 07f |
| 2. Oni persuasion, Echo Walk (5 fragments, 3 attempts, Oni reactions, Takeshi surfacing, fail to combat, light orb) | 192 | 0 | 0 | GDD 09 section 5a, "hardest Echo Walk in the game" |
| 3. Soul orb, tree states, ritual sequence, ring grant, socket choice | 140 | 140 | 140 | GDD 02 steps 11 to 14, GDD 05 section 4a; clips delivered |
| 4. Ability system v1 with 4 abilities and Grimoire UI (A), 2 abilities (B, C) | 196 | 136 | 136 | GDD 05 section 2; your Sept 12 "choose any combat ability" plan |
| 5. Memory clip 1, illustrated 2.5D in Unity (your time only) | 40 | 40 | 40 | GDD 01 memory 1; approach in roadmap section 7 |
| 6. Day island content: quest system finish, Kodama and Noppera-bo quests, tier 4 fixes, offerings and gate flow, tree direction, layout pass | 260 | 200 | 0 | GDD 02 steps 2 to 5, GDD 07 known issues, PERSUASION_QUEST_ORB draft |
| 7. Yoru gaps: death state and save-point retry, hit reaction polish, transform polish (necromancy summon cut from demo) | 80 | 80 | 60 | overview open items, GDD 06 basic summon cut |
| 8. Game shell: main menu, pause and settings with rebinding, gamepad, save and load, end screen with wishlist link, HUD polish | 236 | 236 | 206 | nothing exists yet; GDD 13 save system, rebindable controls |
| 9. Audio: paid tests, contracting, integration of music and about 200 SFX, mixer | 112 | 112 | 112 | YORU_Audio_Brief, YORU_CAVE_SOUND_LIST |
| 10. Build, performance, QA, Steam: first Windows build, optimization, 3 playtest rounds, Steam demo depot | 256 | 256 | 196 | never built; XFur 40 shells, volumetric fog, COZY on one island |
| 11. Marketing capture support (RingShot, screenshot and trailer sessions, Steam page) | 40 | 40 | 40 | RINGSHOT_HANDOFF, plan v1 |
| Total | 1,676 | 1,364 | 1,054 | |

Sanity check: the last 12 months produced roughly this much work (combat, balance, three enemy types, two bosses, inventory, quests, climbing). Another 9 to 12 months for a demo of the same weight is consistent with your own history, not pessimism.

## 3. The math and the three finish dates

Finish = 128 hours before Nov 1, 2026, then 34 hours per week. "Ready" means submitted to Steam with QA done, because package 10 is inside the totals.

| Scenario | Hours | Ready, nothing slips | Ready, 20% slip (normal) |
|---|---|---|---|
| A full loop | 1,676 | Sept 15, 2027 | Nov 23, 2027 |
| B fight only | 1,364 | July 13, 2027 | Sept 7, 2027 |
| C cave only | 1,054 | May 10, 2027 | June 23, 2027 |

What 20% slip means: a modeller delivery two weeks late, a rebuild of one system, a month of 25 hour weeks instead of 40. Every solo project I have data on ate at least that. Plan on the right column, celebrate the left.

## 4. Steam Next Fest windows (real dates and the pattern)

- Feb 2027 edition: registration closed Jan 10, 2027, fest Feb 22 to Mar 1, 2027. Out of reach for any scenario.
- June 2027 edition: registration expected late April 2027, fest mid June. Only scenario C makes it, and only with no slip. Spending the one-shot fest on a cave-only demo is a bad trade.
- October 2027 edition, from the 2026 pattern: registration by about Aug 31, 2027, press preview demo by about Sept 14, final submission about Sept 27, press preview from about Oct 8, fest about Oct 18 to 25, 2027. Scenario B makes it with the full 20% slip. Scenario A makes it only if nothing slips.
- Feb 2028 edition (fallback): registration about Jan 10, 2028, fest late Feb 2028. Scenario A with slip lands here.

Rule: register for October 2027 at the end of August 2027 only if the B floor (fight-only loop) is playable end to end by Aug 15, 2027. Otherwise take Feb 2028 and lose nothing but time.

## 5. Recommended dates

Plan: build so the fight-only loop (B) is shippable first, then add the Oni persuasion (A) on top. If persuasion is not solid by Sept 10, 2027, the October demo ships as B and persuasion arrives as a demo update in Nov or Dec 2027 (the demo stays on Steam after the fest; updates are allowed).

| Date | Milestone | Depends on |
|---|---|---|
| Sept 14 to Oct 31, 2026 | RingShot captured; Oni fight closed except audio and texture; ability system designed; composer and sound designer paid tests; capsule art commissioned; Steam tax verification cleared | modeller brief out (done Sept 12) |
| Nov 1, 2026 | 40 h weeks begin | you |
| Dec 15, 2026 | First Windows build on the borrowed PC, whatever state it is in. Purpose: find build-only breakage early | Windows Build Support module |
| Dec 31, 2026 | Soul orb, tree ritual, ring grant, socket choice playable; ability v1 with 2 abilities | clips delivered (yes), ability design (Oct) |
| Jan 31, 2027 | Memory clip 1 integrated (panels commissioned Nov); announce trailer footage list captured | illustrator |
| Feb 26, 2027 | Steam page public + announcement trailer + press release. Evolve Announce beat starts (or DIY plus Terminals). Kickstarter pre-launch page opens the same day | capsule art (Dec), trailer cut (Feb), Oni look fixed (texture rework Oct to Nov) |
| Mar 31, 2027 | Latest acceptable date for the page. Past this, the wishlist runway before the fest is under 7 months | |
| Mar 31, 2027 | Day island loop playable start to gate (quests, offerings, Komainu); save and load; main menu and pause | |
| Apr 30, 2027 | Gamepad and rebinding; settings; audio integration begins as deliveries land | composer and sound designer (contracted Nov) |
| June 30, 2027 | Scenario A content complete: Oni persuasion in, all four abilities in | animator reaction clips (commission Jan) |
| July 2027 | Playtest round 1 (Steam Playtest, closed, 10 to 20 people) | first optimization pass done |
| Aug 15, 2027 | Go or no-go for October: B floor playable end to end on Windows at 60 fps on a mid-range PC | |
| Aug 31, 2027 | Next Fest October 2027 registration; demo trailer cut | |
| Sept 14, 2027 | Demo build submitted for press preview; Evolve Demo beat starts; preview keys to press and creators | |
| Sept 27, 2027 | Final submission | |
| Oct 18 to 25, 2027 (est.) | Steam Next Fest | |
| Nov 1, 2027 | Kickstarter gate check (8k wishlists, 1,500 list, live Discord). If go: campaign Nov 8 to Dec 8, 2027 | |
| Feb 2028 | Fallback Next Fest if A slipped past Sept 27, 2027 and you chose not to ship B | |

Evolve conversation, with these dates: ask for a per-beat quote for an Announce beat in March 2027 and a Demo beat Sept 14 to Oct 25, 2027. Do not restart anything in 2026. That is the whole message.

## 6. The release window

Q1 2028 does not survive contact with the numbers. The demo is one boss out of ten, one island, one memory clip, and it lands in late 2027 with you at 40 hours a week. What remains after the demo, per the GDD: 9 bosses each with fight, Echo Walk, arena, intro cinematic, memory clip and audio; the rest of the 55 enemies including Kasa-obake (not designed) and Tier 2 to 3 variants; the full purgatory map with Tori gates and fast travel; the Clan system; necromancy (six phases in GDD 06); seven endings and the Eclipse realm; localization; console ports.

Rough cost of the remaining bosses alone, using the Oni as the unit: the Oni will have consumed about 450 to 550 hours by the time fight, persuasion, cinematic, arena and audio are done. Nine more at half that cost (reuse, tools, experience) is 2,000 to 2,500 hours, or 60 to 75 weeks at 34 hours. Add the non-boss list above at a similar size and the full game is 2.5 to 3 years after the demo at the current team size. That is 2030. With a second developer from 2028 (Kickstarter or grant money) and a smaller v1 (6 to 7 bosses, no console at launch), 2029 is defensible. So: the Steam page says "Coming 2029" or nothing, never Q1 2028.

Levers, in order of effect: fewer bosses in v1; a second developer or a contract gameplay programmer after the demo; illustrated memory clips and Echo Walk fragments instead of 3D scenes (roadmap section 7); no console at launch; no necromancy at launch.

## 7. Assumptions that would change the dates

- Hours: if 40 h weeks start in October instead of November, A gains about 2 weeks. If they never start, A moves to spring 2028.
- Oni persuasion scope: if the Echo Walk ships in the demo with 3 fragments instead of 5, package 2 drops to about 130 hours.
- Ability choice: if the demo offers one fixed ability (the GDD rule) instead of a choice, package 4 drops by about 60 hours.
- Cinematics: the numbers assume illustrated 2.5D memory clips and an in-engine Oni intro (roadmap section 7). Full 3D memory scenes add 100 to 200 hours per clip.
- Contractors: every date after Feb 2027 assumes a composer and sound designer under contract by Nov 15, 2026, and the Oni texture rework back by Nov 30, 2026.
- Unknown until the first build (Dec 2026): performance. If the Day island runs under 40 fps on a mid-range PC, package 10 grows by 80 to 120 hours.

Sources for external dates: Steam Next Fest October 2026 page (registration Aug 31, final submission Sept 28, fest Oct 19 to 26), Feb 2027 page (registration Jan 10, fest Feb 22 to Mar 1), partner.steamgames.com. Everything else is from the project itself.
