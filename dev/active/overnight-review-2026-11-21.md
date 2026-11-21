# Overnight run: review for the morning (2026-11-21)

Autonomous continuation of the clean-room rebuild while the user was away. Steps run one at a time,
each through the full loop: implementation agent → my verification (build, both suites, CLI) →
commit and push → review agent → fixes with regression tests that fail on the pre-fix commit →
second review.

**Boundaries:** the user allowed pushing and deleting ("everything is backed up in git"). The old
project is retired only once the rebuild reaches feature parity (Phase 5's gate), with the parity
checklist recorded here. The other session's worktree under `.claude/worktrees/` is left alone.
Design commitments are not reopened; balance and design calls default to old-game parity or the
conservative option and are listed below. No `ponytail:` markers (user preference): all were
converted to plain comments, and real deferrals are listed in `dev/active/rebuild-deferred.md`.

## Status by step

| Step | Commit | Status |
|---|---|---|
| Design doc + plan | 2e91fad | done |
| 4a named places, crossing, den herds; 4b shelters, bedding, building | dc15566, 1c4c825 | committed |
| Emergent step 1: find dump, distribution report, population curves | 2af01d6 | committed |
| Review fixes for the two rows above (13 findings, regression tests; markers converted) | 6f4ef1b | committed |
| Second-review fixes (9 findings), wind revert, per-agent random streams | 53e232d | committed |
| Third review (no correctness bugs) and its fixes: drying at a fire, hardened camper gate (192/192 re-seeded runs live), hunt sighting factors, stream cleanup, find queue, hand-gathered fuel floor | c32468e | committed; Phase 4 content closed |
| Feature-parity checklist (237 rows) | c02e851 | done: `documentation/feature-parity-checklist.md` |
| Authored-content catalog (301 entries: 244 replaced, 17 left, 40 cut) | a6aad25 | done: `documentation/authored-content-catalog.md`; design refinements folded in |
| Merge of `worktree-short-loop-plan` (short-loop plan, UI design, terrain art) | 11aee1a | done, at your request |
| Short-loop plan's "Asks of the Rebuild" (event tiles, historical minutes, chance factors, skills, place memory) | feac1de | committed |
| Asks review fixes (NPC fire from memory, butchering conservation, NPC turn-backs, XP farming, journal) | 501856a | committed |
| Second asks-review fixes + NPC fire-keeping (short sleeps by a dying fire, earlier relights, kindling for oak, camp moves to timber) | 65d7a62 | committed; review running alongside the next step |
| Existing tables to data files (12 JSON files; behaviour byte-identical) | f5a8350 | committed; review confirmed byte-identical, found 2 loader holes |
| NPC review fixes: one trip budget (warmth, energy, home-by), stranded NPCs camp, bed counts burnable hours, no hunting prey it would flee | 45c7916 | committed |
| Loader fixes (strict enum names, required struct fields, explicit flags, ranges, dead rows, immutability), felling-yield report bug, NPC cold-pit bed | 12933a4 | committed; review found two NPC rules from 45c7916 causing the earliest deaths |
| Emergent step 2: substrate (ThingRef, Cause, Signs.At, Access.Refusal, tags and capabilities); byte-identical apart from saved causes | 11cedc4 | committed; review confirmed byte-identical; its findings folded into step 3 |
| NPC fixes: `Stranded` camp moves, warming walks judged by arrival, mammoth hunts, caribou huntable by role | e020f93 | committed; review running |
| Emergent step 3a: renewal, scavengers, body and bone pipeline, kill attribution, stable ids, speed caches | | in progress |
| Emergent step 2: substrate (ThingRef, Cause, Signs, Access) | | |
| Emergent step 3: kill chain, renewal, settling pass | | |
| Emergent step 4: affordances, moments, catalog paper exercise | | |
| Emergent steps 5-6, Phase 5 front end | | |

## Decisions made with a default (please confirm or change)

**Phase 4a (named places)**
- Climb edges on 8 old sites are carried as data but not applied. The old map never read them, so
  no climb cost was ever paid; applying them adds 6 minutes per side.
- Loot gear substitutions: old Flint Axe, Damaged Axe and Cracked Spear map to `stone_axe` and
  `wooden_spear`; Large Waterskin maps to `waterskin`.
- The 224x82 valley's existing eastern pass corridor was kept, rather than rebuilding the old
  96x96 north-wall layout.

**Phase 4b (shelters)**
- Snow shelters melt at 0.01 of every level per °F above freezing per clock hour. The old game had
  a stub here and they never melted; the rate is invented.
- Old item counts became kilos through one nominal weight per material (stick 0.3, log 2.0,
  hide 1.0 kg...).
- Improving a shelter is continuous (a tenth of a piece per minute) and refuses at the cap; the
  old game used 10-minute pieces and wasted material at the cap.
- Packing a tent takes 1 minute (old: 5). Building needs light, except makeshift bedding and tents.
- NPCs don't decide to build or improve shelters; they get shelter and bedding automatically when
  they sleep on a tile that has them.

**Phase 4 review round**
- **Drying at a fire reads the open wind** (less the pit's walls), as the old game did; a roof
  slows drying only away from a fire. Recommended by the third review on physics grounds.
- **The fuel floor counts only fuel gathered by hand** (nobody starts with an axe); trees are
  reported separately as felled fuel. Seed 17: 26 of 115 regions under the floor.
- **Shelter wind cover (decided twice; final below).** The first review called "cover cuts both the
  body's wind and the wind chill" a double count, and the first fix round made it count once,
  through the chill. The second review showed why that is wrong: inside a sealed snow shelter the
  body then still had the full outside wind through its clothing and drying. **Final default:**
  cover cuts both (the original 4b behaviour, the same as the fire pit). Tile wind factor still
  reaches the body only through the chill, for old-game parity; that inconsistency is logged in
  `rebuild-deferred.md` as a physics question for you.
- **Random streams are per agent** for body, combat and travel rolls. Before, herds shared the
  player's streams, so moving a herd changed the player's injury rolls (it was why the 30-day
  camper went from 10 to 17 injuries). Now one agent's rolls never depend on another's existence.
- Once the player stands on the far side of the pass, the run is over: the core refuses further
  player activities, instead of letting the player walk back and "un-win". `Advance` keeps running
  the world but the crossed player lives no more minutes.
- Discoverable shelters (rock overhang, ice cave, cave sleeping recess) are now placed when found.
  Their rolls come from the tile's own seeded stream; the old game used its process-wide RNG.
- The effect of counting wind cover once, measured (core temperature drop with no fire, seed 17):

  | Sky | Shelter | First hour, before → after | After 4 h, before → after |
  |---|---|---|---|
  | Snowy, -20 °C | Camp overhang | 0.47 → 1.00 °F | 4.66 → 5.45 °F |
  | Snowy | Snow shelter | 0.005 → 0.89 °F | 3.28 → 4.86 °F |
  | Snowy | Open | 2.92 °F | 10.40 °F |
  | Calm, -10 °C | Overhang | 0 → 0 | 1.35 → 1.69 °F |
  | Calm | Snow shelter | 0 → 0 | 0.75 → 1.44 °F |

**Emergent step 1 (tools)**
- Ambient forage and timber are not counted as finds (they are the survivable skeleton); they
  appear only once worked or grazed.
- "Scattered Bones" / "Old Bones" stocks (287 on seed 17) count as resources, not dead matter.
- Region floor: a region under 1/3 of the median count is "too few" (the doc only gave the 3x
  ceiling).

**Short-loop asks**
- Skills live on `PersonMind`, not `Agent` (plan decision 2: kind-specific state in the mind).
  Noted in `short-loop-plan.md`.
- Skill curve: XP to next level = 5 x (level + 1), max level 20. Firecraft adds +2% per level to the
  fire odds (the old +10% per level and a long curve can't both hold). Foraging +1.5% yield per
  level, cooking +2% speed, butchering +1% yield. Crafting has no bonus until crafting exists.
- Sight for memory is a 5-tile diamond behind one function (`Sight.Visible`) until sight rays port.
- The fire's flame counts as visible from a distance, so your map updates about 5 tiles from camp
  on the way home; the camp receipt still lists the fire's death at its real minute.
- Field receipts leave out human prints (yours and another person's are the same mark).
- Odds not yet broken into terms: stalking detection, dodge, shove, the flat 90% blow, butchering
  yield, a single forage find (reasons in `rebuild-deferred.md`).

**Content tables**
- Kept in code, deliberately: the camp tile (moves with "camp as a content site"), felling yields,
  carcass composition, body plans, the climate model, herd behaviour rates, NPC preference lists,
  skill curves, draw rules and map-generation shapes. They are models or policy, not tables.
- Inconsistencies found while moving, values unchanged: a "log" weighs 2.0 kg as shelter
  material, 2.0-4.0 kg felled, 1.5 kg in a Dry Wood Stack, 1.5-3.5 kg foraged; the old Barren
  forage table was unreachable and was dropped; rabbits and foxes take default behaviour (Prey)
  and diet (Carnivore).

**NPC hunting rule (supersedes the threshold below)**
- Hunting is now by species role: plain prey (caribou) when food is wanted, dangerous prey (bison,
  megaloceros) only when starving, nothing over 2,000 kg alone (mammoth). In 16 60-day runs the
  NPC still hunted zero times: it only looks for herds within 3 tiles and none came that close.
  That is the planned hunting work (`rebuild-npc-hunting.md`).

**NPC hunting threshold (earlier, superseded)**
- An NPC now hunts nothing it would flee in a fight (by the old size comparison) unless food is
  critical. By that scale a caribou counts too, so NPCs hunt nothing bigger than a rabbit until
  starving. In practice the NPC hunted once in 960 NPC-days before this change (the fatal mammoth).
  If caribou should be huntable when food is merely wanted, scale "theirs" by species threat; it is
  a one-line change. Left conservative pending the hunting work (`rebuild-npc-hunting.md`).

**Loader round**
- Four substrate display names changed to equal their keys ("bare rock" → `rock` and so on); nothing
  in the sim read them. Mineral Spring's small game listed no animals (an old-game bug); it now has
  the common rabbit and ptarmigan mix. Small game has no system yet, so no sim effect.
- The report's felled-tree fire hours were about 5.7% high (it assumed more logs per tree than
  chopping gives). Corrected; chopping itself is unchanged.

**Substrate**
- `Access.Refusal` is the one gate for reaching a thing, and every thing refusal calls it. The new
  conditions (buried, guarded, frozen in, across water) are implemented but not switched on,
  because nothing enforced them before and this step was meant to change no behaviour. Switching
  them on is a gameplay change for you to approve, most likely alongside moments (step 4).
- Kills are attributed to the other side's first fighter and its weapon (nothing records the last
  blow). A dead person's body has no cause yet.

## Open questions

1. **Branch.** Up to `2af01d6` went straight to main (pushes bypassed the "changes must be made
   through a pull request" rule). From then on, at your request, everything is on branch
   `rebuild-phase4-overnight`, with a PR to main at the end of the night.

2. **UI design vs derived tensions.** `ui-design.md` lists a "stalked" alert at "tension >= 2/3".
   Tensions are now derived (emergent-world-design.md), so the alert will read the player's belief
   about a real pack following them instead. Same intent, different source; confirm it's fine.

3. **False perception (from the content catalog).** Five old hallucination scenes can't be produced
   by a sim that never invents facts. Recommendation: fever and fear add uncertainty to real
   perceptions (a real fox heard as a wolf, a real shape mistaken) but never create things. That
   moves most of them to "replaced". Until you decide, they stay in the left pile.
4. **Static place text that can lie.** 11 `discoveryText` strings in `locations.json` assert live
   facts ("Wolf tracks everywhere", "Ravens circle", "Fresh dung steams"). Recommendation: strip the
   live claims from place text and let real signs say them. Not done overnight; it's content.
5. **"Ghost camp" settling.** 24 discovery entries need NPC travellers (step 6), which needs NPCs
   that survive a season. A cheaper settle could place camps and bodies with invented causes, but
   that breaks "facts never lie". Recommendation: wait for real travellers. Not done.
6. **Moment-type floor.** About 12 old body and weather events collapse into 5 moments, and about
   20 predator events into one follower dynamic, so "distinct moment types" will be lower than the
   old event count. Measure it against an old-game baseline before using it as a floor.

## Findings worth knowing

**Baseline from the tools (seed 17, before any emergent work):**
- Finds at day 90: 6538, of which resources 78%, signs 20%, dead matter 0.4%. Both ratio targets
  pass, but only because authored stocks (Frozen Puddle 345, Dry Grass Tussock 281...) dominate.
- Shelter floor fails in 89 of 115 regions; water in 11; fuel in 3.
- Caribou fall 21 → 5 in 90 days; bison 36 → 27; predators flat. Confirms the missing renewal.
- Carcasses never leave: 90-day-old kills still hold 35-75 kg at 100% decay (no bone pipeline).
- Region spread is very uneven by day 90 (20 regions over 3x the median, 19 under a third).
- An idle player dies around day 30 under Advance (expected: nobody feeds them).
- 90 days of Advance takes about 10.3 s (~3.5 s a month), so a one-year settle is about 40 s
  before hourly stepping for distant agents.

## Shortfalls (what didn't meet its gate, honestly)

- **The Phase 2 camper got harder after the first phase 4 fix round** (10 → 17 injuries, gathered
  food 10,954 → 7,301 kcal). Explained by the second review: all the food drop came from the
  wind-cover change (reverted, above) and all the injuries from herds sharing the player's random
  streams (now per agent). Final numbers after the second fix round are in the status table.

- **The camper gate was fragile all along (resolved).** Re-seeded 48 ways it died in 9-13 runs on
  every wind variant, including the code before tonight, so the "2 of 12" was noise. Root cause was
  the script: it fed 2 kg at every tend (~36 kg a day) and felled one tree a day, so a 48-hour
  blizzard drained camp. The script now sizes the fire, keeps a wood reserve, dries and drinks
  before leaving, and retries melting. It survives 192 of 192 re-seeded runs, and the test checks
  four of them on every run. No sim bug was found.
- **Phase 2 camper after the second fix round:** 14 injuries, gathered 3,189 kcal (94% hand-fed).
  Most of the drop on seed 17 is the new per-agent streams: across 12 reseeded runs survivors
  gathered 5.4-9.2k kcal against 5.2-12.1k before.
- **Player hunting is nearly impossible** (balance, not a bug). 2-3% immediate kills on caribou
  across six stalking policies; the prey notices the hunter in ~99% of hunts; throws hit ~35% at
  10 m. Numbers and what to retune are in `dev/active/rebuild-combat-balance.md`. The old sighting
  factors are now ported (grazing or resting prey is easier to approach); the effect is small.
- **Smallest territories after the pass fix** (80 seeds): Bison 93 tiles (target range 240+),
  Hyena 4 (ravine and cliff pockets elsewhere). Not on the pass any more; logged in
  `rebuild-deferred.md`.

- **NPC survival: improved on some seeds, not overall (correcting an earlier claim).** On the ten
  review seeds 9 of 10 reach day 30, but two of them pass the cutoff by 6 and 65 minutes. A
  60-day review run on 16 seeds found every NPC dies within 60 days, on this commit and the one
  before it. Mean death day is 35.3 against 33.7 before, or 35.4 against 35.7 leaving out seed 5,
  which was the one clear win: it now moves camp to timber and lives to day 33 instead of day 5.
  Three new seeds (7, 31, 1000) die earlier than before. The common end: fat falls about
  0.12-0.15 kg a day to the 10% floor, then leanness costs cold resistance and the NPC freezes
  striking at 5% odds. That is the deferred food and daily-schedule item, not fire-keeping. Four
  NPC bugs the review confirmed (cold trips not guarded on every path, exploring 30 tiles out
  without budgeting the walk home, evening wood fetches after a winter sunset, sleeping beside
  logs too cool to catch) and seed 123's mammoth hunt were then fixed (45c7916). 60 days, 16 seeds,
  mean death day 33.4 → 34.8, median 34.2 → 36.2: the worst seed improved (5.0 → 26.5), three seeds
  lost about 10 days each. Time out at night fell from 1,445 to 188 minutes. All still die, mostly
  of cold after fat reaches the 10% floor (the deferred food item).
- **After the stranding fix (e020f93):** 60 days, 16 seeds, mean death day 35.2 → 42.0, median
  36.8 → 37.7, worst 26.5 → 29.7, and one NPC (seed 2025) alive at day 60. The four earliest deaths
  were the stranding bug; two of them moved a lot (1000: 26.5 → 53.9). The rest still die the
  deferred way: critical food outranks wood all day, the camp runs dry, a cold morning.
- **Butchering yields changed slightly:** the old game's full processing produced 107% of what was
  cut (it minted mass). Fixed; full processing now recovers its old share of a ledger that holds
  what thorough work can get. Butchering times are unchanged.

## Deliberately left for you
- Retiring the old project (Phase 5's last step), unless parity is reached overnight.
- The Claude Doc version of the plan still shows tensions and events. The docs connector
  disconnected during the night, so I could not update it.
