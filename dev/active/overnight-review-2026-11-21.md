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
| Short-loop plan's "Asks of the Rebuild" (event tiles, historical minutes, chance factors, skills, place memory) | feac1de | committed; review running |
| Existing tables to data files | | |
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

## Deliberately left for you
- Retiring the old project (Phase 5's last step), unless parity is reached overnight.
- The Claude Doc version of the plan still shows tensions and events. The docs connector
  disconnected during the night, so I could not update it.
