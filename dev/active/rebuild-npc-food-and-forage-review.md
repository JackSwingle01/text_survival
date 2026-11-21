# NPC food, forage balance and fire keeping: open for review

**Status:** open, deferred (noted 2026-11-20, clean-room rebuild phase 3; "fine for now, review later")
**Where:** `rebuild/Sim/Work.cs` (`FocusGain`), `rebuild/Sim/Npc.cs`, `rebuild/Sim/Pools.cs`.
**Related:** `rebuild-npc-hunting.md` (hunting is the planned high-return food source).

## Where it stands

- Design goal: focused foraging lets you scrape by; casual foraging, poor terrain, a worked-out
  tile or splitting the day with firewood still runs a deficit; hunting is the big return.
- Lever used: `Work.FocusGain = 1.3`. Each find in the focused category has a 30% chance of a
  duplicate. Before this, focus only filtered: a food-focused search found the same food as a
  general one. Density, depletion (1/(1+2h)) and four-week regrowth are unchanged.
- NPC food priority runs on `NpcMind.TargetFatKg` (EMA of its own fat, 3-day half-life, clamped to
  10-15% of body weight); wood beats wanted food until fuel lasts to dawn + 2 h. Food past a full
  stomach now becomes fat at 7700 kcal/kg (old bug, fixed).

Scripted focused forager from camp, 14 days, fat change per day (fuel and water supplied):

| seed | before (gain 1.0) | after (gain 1.3) | terrain |
|---|---|---|---|
| 17 | -0.061 | -0.002 | forest |
| 1 | -0.108 | -0.045 | forest |
| 2024 | -0.094 | -0.062 | forest |
| 3 | -0.088 | -0.038 | forest |
| 42 | -0.196 | -0.184 | grassland, 1 forest tile within 8 |
| 777 | -0.183 | -0.174 | grassland |

Staying on one tile: -0.12 to -0.20 kg/day. A nomadic forager with free fuel: -0.11 to +0.10.

NPC results on the full tree: 14-day gate 6/6; 20-seed 14-day sweep 18/20 (seeds 5 and 10 freeze,
no timber nearby); 30 days on the six gate seeds 3/6 (1, 42, 777 live; 17 dies day 27.8 at 6.9 kg
fat; 2024 day 28.6; 3 day 15.2 at 9.3 kg fat, so cold or fire, not hunger).

**Update 2026-11-21 (asks review, second round):** items 4 and 6 below are fixed as competence bugs, not
balance: a camp with no timber within 15 tiles now explores up to 30 for a stand and moves camp to it (seed 5 lives
30 days), and a small fire is built up with kindling, the reserve included, until the logs it has will go on.
Also fixed: strike odds the NPC will accept fall as it gets colder, it sleeps short beside a fire it cannot bank,
and from camp it does not set off for wood it would come home freezing from. Forage numbers and the daily schedule
are unchanged. NPC 30-day survival on the ten review seeds: 9/10 (seed 123 dies in a mammoth hunt it started).

**Update 2026-11-21 (fire-keeping review, third round):** every NPC trip and every stint of work away from camp is
held to one budget (`Npc.Afford`: home warm enough by the same reckoning the warmth need uses, the clothes' stored
heat included; rested enough; home by `HomeMinuteOfDay`), a far-out NPC too spent or cold to walk home makes camp
where it is, bed reckons by the hours the fire will burn (`Fire.HoursItWillBurn`), and short of starving it leaves
prey it would run from alone. Forage numbers and the daily schedule are unchanged. 60 days on the 16 review seeds:
all still die; mean death day 33.4 -> 34.8, median 34.2 -> 36.2, worst 5.0 -> 26.5, best 52.0 -> 40.0. Most deaths
are still fat at or under the 10% floor, then cold.

**Update 2026-11-21 (review of the trip budget, fourth round):** competence fixes, no balance. A far-out NPC makes camp
where it stands only beyond two tiles, carrying a fire's makings, the walk home reckoned along the path, and never while
camp has a fire, embers or a shelter unless too spent to get there (`Npc.Stranded`); walks to makings are warming walks,
judged by the legs and a lethal core line rather than the trip budget, and from a camp with no fire the walk to the trees
is too (`Npc.WarmingWalk`, `Afford(warming)`); a full pack no longer stops it taking tinder off its own stash; a lone NPC
hunts by species role and never a mammoth (`Npc.WouldHunt`). Forage numbers and the daily schedule are unchanged. 60 days
on the 16 review seeds: mean death day 35.2 -> 42.0, median 36.8 -> 37.7, worst 26.5 -> 29.7, one survivor (2025). No NPC
hunts at all (no caribou herd comes within the 3 tiles it looks). Most deaths are now the deferred day: food critical
pre-empts wood, the camp runs out and a cold morning finds it groping a worked-out tile (seeds 7, 11).

## To review

1. **Focus gain size.** At 1.3 a focused forest forager ends about -0.04 kg/day. At 1.5 about
   -0.02, and a nomad clearly gains fat. A design call.
2. **The NPC forages only about 4 h a day.** The rest: sleep ~7.8 h, resting 5-6 h (much of it
   warming by the fire), travel 2-3 h, wood ~1.3 h. Its kcal per forage hour matches the scripted
   forager's, so the deficit is its day, not the food numbers. Needs: more daylight foraging (less
   fire-warming, napping and hauling) and moving camp when the ground nearby is worked out. Do it
   with the hunting work, since both are about how the NPC spends its day.
3. **Grassland camps can't forage their way through.** Seeds 42 and 777. Fine as "poor terrain"
   only if the NPC/player can tell and move; otherwise a map-generation question.
4. **Seeds 5 and 10: no timber near camp** (nearest 24 tiles on seed 5). The NPC freezes on day 2
   / ~8. Camp placement or "move camp to timber" reach.
5. **Seed 3 dies on day 15.2 with fat to spare.** Cold or fire keeping; not yet traced.
6. **Fire-keeping bug (seed 8, clear bug).** With only oak on hand, the NPC lights tinder, then
   forages for fuel for 15 minutes while the fire dies, and repeats until it freezes. It should
   gather kindling before lighting, or feed what will catch. Fix with a regression test.

Tried and rejected during the retune: dropping logs instead of walking home to unload (less camp
firewood, more cold deaths); foraging tiles only seen, not visited (walked too far, seed-1 NPC died
day 4); delaying bedtime (marginal).

Harnesses and data: `~/.claude/jobs/2c768bb5/tmp/p3forage/` (`live.sh`, `VARIANT=before|after`).
Job scratch, so copy anything worth keeping before the job is cleaned up.
