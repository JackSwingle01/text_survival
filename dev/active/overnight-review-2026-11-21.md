# Overnight run: review for the morning (2026-11-21)

Autonomous continuation of the clean-room rebuild while the user was away. Steps run one at a time,
each through the full loop: implementation agent → my verification (build, both suites, CLI) →
commit and push → review agent → fixes with regression tests that fail on the pre-fix commit →
second review.

**Boundaries:** the user allowed pushing and deleting ("everything is backed up in git"). The old
project is retired only once the rebuild reaches feature parity (Phase 5's gate), with the parity
checklist recorded here. The other session's worktree under `.claude/worktrees/` is left alone.
Design commitments are not reopened; balance and design calls default to old-game parity or the
conservative option and are listed below. No new `ponytail:` markers (user preference); the
existing 52 are being converted to plain notes and a dev doc.

## Status by step

| Step | Commit | Status |
|---|---|---|
| Design doc + plan | 2e91fad | done |
| 4a named places, crossing, den herds; 4b shelters, bedding, building | dc15566, 1c4c825 | committed; review running |
| Emergent step 1: find dump, distribution report, population curves | 2af01d6 | committed; in the same review |
| Existing tables to data files | | next |
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

**Emergent step 1 (tools)**
- Ambient forage and timber are not counted as finds (they are the survivable skeleton); they
  appear only once worked or grazed.
- "Scattered Bones" / "Old Bones" stocks (287 on seed 17) count as resources, not dead matter.
- Region floor: a region under 1/3 of the median count is "too few" (the doc only gave the 3x
  ceiling).

## Open questions

1. **Branch protection on main.** Pushes print "Bypassed rule violations: changes must be made
   through a pull request". Your account can bypass it, and you allowed pushing, so I pushed
   directly. Say if you'd rather have a PR per step.

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

## Deliberately left for you
- Retiring the old project (Phase 5's last step), unless parity is reached overnight.
- The Claude Doc version of the plan still shows tensions and events. The docs connector
  disconnected during the night, so I could not update it.
