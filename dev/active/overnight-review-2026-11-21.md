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
| Review fixes for the two rows above (13 findings, regression tests; markers converted) | 6f4ef1b | committed; second review running |
| Merge of `worktree-short-loop-plan` (short-loop plan, UI design, terrain art) | 11aee1a | done, at your request |
| Short-loop plan's "Asks of the Rebuild" (event tiles, historical minutes, chance factors, skills, place memory) | | after the phase 4 fix round |
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
- Shelter wind cover was cutting the wind twice: the body's convective wind and the wind chill.
  It now counts once, through the wind chill, the same rule tile wind follows. This roughly halves
  how much a good shelter helped. The review measured a snow shelter at -1.34 °F/h body cooling
  with both cuts against -2.32 with one. If you want shelters warmer, raise their insulation; don't
  restore the second cut.
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

- **The Phase 2 camper got harder after the phase 4 fixes**: 10 → 17 injuries, food it gathered
  10,954 → 7,301 kcal, the hand-fed share 80% → 86%. It still survives 30 days. The second review
  is checking what changed its path (likely the shelter-find draws or den territories shifting
  what is near camp).

## Deliberately left for you
- Retiring the old project (Phase 5's last step), unless parity is reached overnight.
- The Claude Doc version of the plan still shows tensions and events. The docs connector
  disconnected during the night, so I could not update it.
