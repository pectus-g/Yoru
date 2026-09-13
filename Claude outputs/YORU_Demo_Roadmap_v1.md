# YORU: Eclipse of Tails. Demo roadmap v1, step by step

Pectus Games. Sept 13, 2026. Companion to YORU_Dates_v1.md (why the dates are what they are). This is the work list, in order, to reach a submitted Steam demo of the GDD first loop for the October 2027 Next Fest, with the fight-only loop shippable first as the safety floor.

How to read: each line has an owner (H = you, C = Claude in the dev or marketing session, A = animator, M = modeller, I = illustrator, S = composer and sound designer), your hours, and a done test. Hours are yours only. Do the phases in order; inside a phase the order is the listed order unless a dependency says otherwise.

## Phase 0. Sept 14 to Oct 31, 2026 (22 h per week, 128 build hours)

Goal: close the Oni fight, capture the ring clip, design what does not exist yet, get contractors moving. Nothing new is built before the design is written.

1. RingShot capture (H+C, 8 h). RINGSHOT_HANDOFF.md. Done: four takes in Documents/YoruMarketing/RingShot.
2. Oni hit reactions closed (H+C, 20 h). From YORU_HITREACT_DIAGNOSIS.md. Done: every tier reaction plays at runtime, no freeze, logged in OniLogs.
3. Oni phase 2 lightning over-spawn (H+C, 8 h). Done: SkyShowRoutine spawns under 10 objects per storm beat, no frame hitch.
4. Oni arena lighting bake and braziers (H, 16 h). Braziers placed, lights Mixed, bake. Done: fur reads side to side, no pool of light following Yoru.
5. Oni texture rework (M, external, 3 to 5 weeks). Brief already delivered (ONI_TEXTURE_BRIEF.md). H: 8 h to integrate on return. Done: new maps on the prefab, look approved in the cave grade you kept.
6. Ability system design (H+C, 16 h). Decide: does the tree offer a choice (your Sept 12 plan) or one fixed ability per path (GDD 05 locked rule). Write the demo ability list: 2 dark and 2 light from GDD 05 section 2b (suggest Shadow Step, Kitsune Fire, Calming Mist, Bone Shield), slot mapping to keys 1 to 4, cooldown rules, Grimoire panel layout. Done: one page in GDD_May, marked ✅.
7. Tree ritual design (H+C, 8 h). Lock the order from GDD 02 steps 11 to 14 or your Sept 12 order. Beats, camera, which delivered clips play where (15 absorbing, 16 freeing soul, 17 circle activation). Done: a shot list, 10 to 15 beats.
8. Memory clip 1 and Echo Walk fragments brief (H+C, 8 h). Roadmap section 7 approach. Done: a brief for 11 panels sent to an illustrator.
9. Hire composer and sound designer (H, 16 h). Paid-test-first. Post in Oct, tests mid Oct, contract by Nov 15. Done: signed scope from YORU_Audio_Brief with demo-first delivery order (Oni loops, tree ritual cue, menu theme, exploration bed, then SFX batches).
10. Commission capsule art (H, 4 h). Brief from the marketing session. Done: illustrator booked, delivery by Dec 15.
11. Steam: clear the tax and identity verification (H, 2 h). Done: Steamworks app fully editable.
12. Confirm the animator's open list (H, 2 h). Which of Granny 5, Kodama 11, Hitotsume 13, Komainu 17 batches exist, and book the Oni persuasion reaction clips (pause, roar step, kanabo lower, look away, horns recede) for Jan delivery. Done: dated delivery list.
13. Install Windows Build Support in Unity Hub and add the borrowed PC to the plan (H, 2 h). Done: a Windows build target selectable in Build Settings.
14. Marketing emails (H, 2 h): CMF and Ontario Creates ownership eligibility questions from the plan. Done: two replies filed in the marketing project.

Phase 0 total: about 120 h. Fits the 128 available.

## Phase 1. Nov 1 to Dec 31, 2026 (40 h per week, about 290 build hours)

Goal: the loop's missing spine exists: orb, tree, ring, socket, abilities. First build ever.

1. Soul orb (H+C, 24 h). Red orb on Oni death, blue on persuasion, magnet pull, absorb animation (clip 15), soul inventory entry, HUD soul icon. Done: kill Oni, orb absorbed, inventory shows the Oni soul.
2. Ancient Tree states (H+C, 24 h). Bare, glowing toward the next gate, brighter with particles when a soul is ready, post-ritual. Done: tree state follows game state in DemoScene_Day.
3. Tree ritual sequence (H+C, 60 h). Timeline in engine: approach, freeing (clip 16), memory clip 1 plays, ring grant (clip 17 circle activation), WorldStateManager.AddRing(true or false), world shift, socket choice. Done: full ritual plays from walking up to the tree, no debug keys.
4. Socket choice UI (H+C, 24 h). +1 Health, +1 Energy, +1 Shine. Done: choice applies to PlayerHealth, climb energy and SoulManager and is saved.
5. Ability system v1 (H+C, 60 h). Unlock, slots 1 to 4, cooldowns, input, VFX hooks. Done: an unlocked ability fires from its slot in the cave and on the island.
6. Two abilities functional (H+C, 40 h). One dark, one light, from the Phase 0 list. Done: both usable in the Oni fight.
7. Grimoire and ability panel (H+C, 40 h). Locked and unlocked states, ring tracker circles, tree choice screen if the choice stays. Done: panel opens with F hold as locked in the control scheme, no overlap with Memory Parchments (J).
8. First Windows build (H, 24 h). Build Settings scene list, resolution and quality defaults, a build script. Test on the borrowed PC. Done: the cave and the island run on Windows; a list of build-only bugs exists.
9. Memory clip 1 integration (H+C, 24 h). Panels from the illustrator into a 2.5D parallax scene with Timeline, music cue when it exists. Done: 45 to 60 s clip plays inside the ritual.
10. Announce trailer footage list (C, 0 h of yours). Marketing session lists the shots; capture happens in Phase 2.

Phase 1 total: about 320 h. Slightly over the 290 available, which is why Dec 31 for items 1 to 7 is the milestone and item 9 may cross into January.

## Phase 2. Jan 1 to Mar 31, 2027 (about 440 build hours)

Goal: the island loop from waking at the tree to the gate, the game shell, the Steam page and trailer out.

1. Quest system finish (H+C, 60 h). Data layer done per the PERSUASION_QUEST_ORB draft; Kodama and Noppera-bo persuasion quests written and wired; tracked quest glow trail. Done: both quests completable, Parchments update.
2. Noppera-bo phase 2 (H+C, 40 h). Wire Stagger, Teleport_In, Teleport_Out, HairLash_Telegraph, Grab, Scream. Done: phase 2 plays every state at least once in a test run.
3. Kodama and Hitotsume known issues (H+C, 40 h). From GDD 07 logs. Done: no issue marked High remains.
4. Offerings and gate (H+C, 40 h). Omamori, Incense, Sake items; guardians drop them; KomainuGate takes all three, ritual visual, portal, auto-save, load into the cave. Done: full transition island to cave and back without the editor.
5. Tree direction and Haru (H+C, 24 h). Glow toward the gate, Haru's hint line, no quest log. Done: a new player finds the gate without help within 10 minutes.
6. Island layout pass (H, 40 h). Blockers, collision, out-of-bounds, camera clipping, pickups placed, the demo boundary. Done: no fall-outs in three full runs.
7. Save and load (H+C, 60 h). Save points, rings, souls, inventory, quests, socket choices, scene. Done: quit and resume from every save point.
8. Death state and save-point retry (H+C, 40 h). Yoru death, fade, reload at last save point, enemies reset. Done: die to the Oni and retry three times without a restart.
9. Main menu and pause (H+C, 24 h). New game, continue, settings, quit; pause with resume and quit to menu. Done: a full session from menu to end screen.
10. Steam page assets (H, 16 h). Capsule delivered (Dec), 8 to 10 screenshots at 4K with post on, page text from the marketing session, localized page text. Done: page approved by Valve.
11. Announcement trailer (H+C+editor, 24 h of yours). Capture from the island, the RingShot, the Oni fight once the texture rework is in. Cut by a freelance editor. Done: 60 to 90 s, approved.
12. Feb 26, 2027: page public, trailer live, press release, Evolve Announce beat or DIY. Kickstarter pre-launch page opens.
13. Animator: Oni persuasion reaction clips delivered (A, by Jan 31). Granny and tier 4 batches as confirmed in Phase 0.
14. Illustrator: Echo Walk fragment panels delivered (I, by Feb 28).
15. Composer and sound designer: first deliveries (S): Oni phase 1 and 2 loops and transition, tree ritual cue, menu theme (by Mar 31).

Phase 2 total: about 410 h.

## Phase 3. Apr 1 to June 30, 2027 (about 440 build hours)

Goal: Oni persuasion, all abilities, audio in, gamepad, settings. Scenario A content complete June 30.

1. Echo Walk mechanic (H+C, 60 h). Tomoe form, fragments in the arena, approach order, hold ground on the red fragment, 3 attempts with hints at 2, fail to forced combat, Memory Gift on success. Done: both outcomes reachable from one save.
2. Fragment presentation (H+C, 60 h). The five panels as ghost scrolls in the arena (GhostEffect3D plus the illustrated panels), Oni reactions per fragment from the animator's clips, Takeshi surfacing on the fifth. Done: full persuasion run plays start to end with no placeholder.
3. Persuasion dialogue content (H, 16 h). Lines for the five fragments and the three failure states. Done: in DialogueData assets.
4. Light orb and persuasion ending of the fight (H+C, 16 h). Done: blue orb, tree ritual grants a right ring, Sunrise state (0L/1R).
5. Remaining two abilities (H+C, 40 h). Done: four abilities usable, tree choice works for both paths.
6. Gamepad and rebinding (H+C, 60 h). Old Input Manager: axis and button map, UI navigation, glyphs, rebinding screen; keyboard scheme untouched. Done: full run on an Xbox pad without touching the keyboard.
7. Settings (H+C, 36 h). Resolution, fullscreen, vsync, quality preset, audio sliders, camera sensitivity, invert. Done: every setting persists.
8. Audio integration (H+C, 80 h). Music states (menu, island, cave phase 1 and 2, ritual, memory), about 200 SFX into CombatSFXManager, EnemyFX, YoruVFXManager, UI; AudioMixer with ducking. Done: no silent action in the loop.
9. Optimization pass 1 (H+C, 40 h). Profile on the Windows PC: XFur shell count and LOD, volumetric fog resolution, COZY settings, lightmaps, occlusion, texture sizes. Done: 60 fps at 1080p on a mid-range PC (GTX 1660 or RTX 3060 class) in both scenes.
10. Demo end screen (H+C, 8 h). Stats (playtime, ring, path), wishlist button opening the Steam overlay to the store page, quit to menu. Done: overlay opens on Windows.
11. June 30, 2027: Scenario A content complete.

Phase 3 total: about 416 h.

## Phase 4. July 1 to Sept 27, 2027 (about 420 build hours)

Goal: three playtest rounds, submission.

1. Playtest round 1 (H+C, 40 h). Steam Playtest, closed, 10 to 20 players, survey plus a session log. Done: ranked bug and confusion list.
2. Fix pass 1 (H+C, 60 h).
3. Playtest round 2 (H+C, 40 h). 20 to 40 players including gamepad-only players.
4. Fix pass 2 and optimization pass 2 (H+C, 60 h).
5. Aug 15: go or no-go. Test: fight-only loop end to end on Windows, 60 fps, no blocker. Go means register for October. No-go means Feb 2028 and the same list continues.
6. Aug 31: Next Fest registration. Demo trailer (H+C+editor, 16 h). Screenshots refreshed.
7. Steam demo app, depot, build upload, demo page (H+C, 16 h). Done: the demo installs from Steam on the Windows PC.
8. Playtest round 3 on the Steam build (H+C, 40 h). Done: crash-free across 20 sessions.
9. Sept 10: persuasion decision. If Echo Walk is not solid, ship the demo with forced combat and a note in the demo end screen that the persuasion path arrives in an update. Persuasion joins the demo in Nov or Dec 2027.
10. Sept 14: submit for press preview. Preview keys to press and creators (Evolve Demo beat). Sept 27: final submission.
11. Reserve (H, 100 h). This is the slip buffer inside the phase. Do not schedule it.

Phase 4 total: about 372 h plus the reserve.

## Phase 5. October 2027 and after

1. Oct 18 to 25 (est.): Next Fest. Two live streams from the Steam page. Reply to every comment for the week.
2. Nov 1: Kickstarter gate check. Go: campaign Nov 8 to Dec 8. No-go: skip.
3. Nov to Dec: demo update with persuasion if it shipped without it; first production planning for bosses 2 to 4 with the per-boss cost measured on the Oni.
4. Steam page release window: "Coming 2029" or blank until the production plan exists.

## 6. Cut list, in order, if a phase runs long

1. Necromancy basic summon: already cut from the demo above. Grimoire shows it locked.
2. Ability choice at the tree: fall back to the GDD rule, one fixed ability per path, saves about 60 h.
3. Echo Walk with 3 fragments instead of 5: saves about 60 h and two panels.
4. Noppera-bo out of the demo island (keep Kodama and Hitotsume): saves about 40 h.
5. Second persuasion quest (Noppera-bo) out: saves about 30 h.
6. Oni persuasion out of the October build entirely (Scenario B): saves 192 h; it returns as the Nov or Dec demo update.
7. Settings reduced to resolution, fullscreen, audio: saves about 20 h.
Never cut: save and load, gamepad, the end screen with the wishlist button, the first Windows build in December.

## 7. Cinematics: the approach I recommend

The question was studio animation (too expensive), AI video (looks cheap) or in-engine scenes (effort unknown). The answer is two pipelines, both cheap, both on theme, and they share assets.

- In-world moments stay in engine with Timeline: the Oni intro (30 s), the tree ritual, the ring grant, the phase 2 entrance you already built. You own the models, the animator makes short clips, the camera is one lens and one move per beat, the same rule you set for the Oni cinematic camera in round 67. Cost per cinematic: 30 to 60 h of your time plus 3 to 6 animator clips.
- Memories become illustrated scrolls, not 3D scenes: memory clips and the Echo Walk fragments are the same thing seen from two sides (Tomoe's past, the boss's past). Commission 5 to 6 painted panels per memory in one consistent style (sumi-e or ukiyo-e ink and wash suits the game and hides low detail), then build each clip in Unity as 2.5D parallax layers under Timeline with the balance system's fog and light over them, one or two lines of text, and a music cue. A 45 s clip is a week of your time once the panels exist. Panels cost roughly $100 to $300 each from a mid-level illustrator; 11 panels for the demo (6 memory, 5 Echo Walk) is about $1.5k to $3.5k CAD, and the same illustrator can carry all ten bosses later. Full 3D memory scenes (village, house, earthquake, festival) would be 100 to 200 h each plus new environments and Tomoe, Hana, Misa and Pera models and animations. Not for the demo, probably never.
- The Echo Walk fragments as scrolls also solve a design problem: the player walks up to a painting that hangs in the arena's fog, which reads instantly as "a memory", and the Oni reacts to it. No extra 3D content per boss.
- What to avoid: mixing AI video with in-engine footage (the seam shows), and any cinematic that needs Tomoe in 3D beyond the Granny form you already have.

## 8. Contractors and when they must land

| Who | What | Book by | Deliver by | Blocks |
|---|---|---|---|---|
| Modeller | Oni texture rework (brief delivered Sept 12) | done | Nov 30, 2026 | announce trailer, Steam screenshots |
| Illustrator 1 | Capsule and key art | Oct 15, 2026 | Dec 15, 2026 | Steam page |
| Illustrator 2 (can be the same) | 6 memory panels, 5 Echo Walk panels | Nov 15, 2026 | Feb 28, 2027 | memory clip, persuasion |
| Composer + sound designer | Demo-first order from YORU_Audio_Brief | Nov 15, 2026 (after paid tests) | music Mar 31, SFX batches Apr to May 2027 | audio integration in Phase 3 |
| Animator | Oni persuasion reactions (5 clips), remaining tier 4 and Granny batches | Oct 31, 2026 | Jan 31, 2027 | Echo Walk |
| Trailer editor (freelance) | Announce trailer Feb 2027, demo trailer Aug 2027 | Jan 15, 2027 | Feb 20 and Aug 25, 2027 | page, fest |
| Windows PC (borrowed) | Every build from Dec 15, 2026 | Dec 1, 2026 | ongoing | everything in Phase 4 |

## 9. Weekly rhythm from Nov 1

- 4 days build (the phase list, top to bottom), 1 day capture and marketing (clips, posts, dev log, contractor replies).
- Every Friday: one Windows build, even if nothing changed. Build breakage found in a week costs an hour; found in September 2027 it costs the fest.
- Every month end: a 10 minute run of the whole loop as it exists, recorded. That recording is the dev log and the trailer archive.
- Every session with Claude starts from the phase list, not from a new idea. New ideas go to a parking list at the bottom of this file and get judged at phase boundaries.

## 10. What Claude does in this plan

In the dev session: complete scripts for every system above, Timeline and Unity setup steps with exact locations, diagnosis from OniLogs and Editor.log, build scripts, the optimization pass checklists, playtest survey design and result triage. In the marketing session: every text asset (page, trailer script, press release, posts, Kickstarter), the festival and press trackers, the Monday brief, the clip cuts from your captures, and the calendar in YORU_Dates_v1.md kept current. What Claude cannot shorten: your art direction time, contractor lead times, and playtesting calendar time. Those set the floor of every date.

## Parking list (new ideas, judged at phase boundaries)

- Ability tree with free choice of any combat ability (your Sept 12 idea) versus the GDD's one ability per path: decide in Phase 0 item 6.
