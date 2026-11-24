# #154: Mammoths re-plan too often (69 moves an animal-day on seed 123)

Hypothesis:
- Changes: `Fauna.Interrupt` (Needs.cs): the kin-leaving interrupt.
- Affects: a grazing herd spread over a few tiles. Today any kin crossing off its tile re-plans every
  tilemate, even when it only steps to a neighbouring tile, where `KinAt` (#83, point 2) still counts it as
  with them. Mammoths, about 3 to a tile's capacity, spread over the most tiles and shuffle between them as
  tiles thin, so each shuffle re-plans the tile it left.
- Measured before (cohesion gate, seed 123, main at 5d2e9a9, Release, 60 days with the warm-up, a temporary
  counter in `Needs`), mammoth re-plans by reason: its kin are leaving 17,959 (food) + 1,177 (water) +
  1,140 (rest); the source has thinned 3,887; off its way 2,467 (food) + 11 (rest); a stat rose above the
  plan's 1,612 (food) + 1,353 (rest) + 620 (water); no plan 833; a threat on the way 146; the next step is
  refused 57; time to look again 24. Mammoths moved 56.5 tiles an animal-day, regrouped 27.1 times.
  Of the two candidates in the ticket it is the first, kin leaving; the shrinking capacity shows as
  "thinned", a fifth of it.
- Stays green: `IndividualsTests` (a flock follows a flight, a herd crossing follows), `HerdTests`, the fast suite.
- First red: a new `IndividualsTests` case: a grazing mammoth re-plans when its kin steps to a neighbouring
  tile to graze there.

Revised (review): design review pointed out re-plans are not moves: a kin-leaving re-plan mostly ends in staying, so silencing it for kin stepping one tile would cut few moves → counted each Travel chosen by the path that chose it before changing anything.
Revised: moves by cause, mammoths, seed 123, 60 days, before: alone with food due and walking to the nearest kin 8,964; a route after "the source has thinned" 7,120; kin following and other paths outside the planner 5,150; routes after kin leaving 4,568 (2,191 of them after kin stepping only to a neighbouring tile); after a stat rose 4,087; after off its way 2,458. The cause is "alone" read per tile while `KinAt` (#83, point 2) reads kin within one tile: a mammoth on its own tile beside its kin walks onto theirs, the tile tips over capacity (about 3 mammoths), it thins and they spread out again. That loop also drives most of the kin-leaving and thinned re-plans → "alone" for a grazing herd now means no kin within one tile (`Fauna.WithKin`), in both places that test it: a hungry animal going to kin first (`Needs`) and `Cohere`'s walk to kin. The kin-leaving interrupt is untouched, as #83 decided.
Revised: no number changed (capacity, graze rate, cohesion), so this is not the "stop and report" case; it makes "alone" agree with the rule Jack already chose for the cohesion cost.
Revised: the rule applied to all open and closed groups moved hyenas (seed 17: 6 starved; seed 42: a pack killed off) and ravens flew less on every seed → limited to prey herds (`IsPrey`); packs and flocks keep "alone" as no side on their own tile, since a pack hunts as one side.
Revised: one run per seed can't tell the change's effect from chaos → sampled main three ways (its own planner stream and two salted ones, "planner-salt", "planner-salt2", "planner-salt3") and the change three ways. Main's own salts move other species outside bands (seed 17: hyenas lose 8-9, wolves 6; seed 23 caribou regroups 0.47), so a single line outside a band isn't evidence of the change.
Revised: tried a narrower variant (only the hungry path, not `Cohere`): mammoths 24.9-32.0, other bands the same → kept both paths: more margin, and one meaning of "alone".
Revised: counts after, mammoths, seed 123, 60 days: re-plans: its kin are leaving 5,563 (food) + 676 (rest) + 636 (water), was 20,276; the source has thinned 2,089, was 3,887; off its way 211, was 2,478; a stat rose 2,571, was 3,585; no plan 1,377, was 833. Moves: alone and walking to kin 1,001, was 8,964; after thinned 3,343, was 7,120. Mammoths move 28.2 tiles an animal-day (25.4 and 25.6 with the salted streams), were 56.5 (55.5, 58.0, 55.4).
Follow-up: on main's rules with the planner stream salted to "planner-salt", seed 23's cohesion gate throws "Herd agent6 chose a refused Travel ... you can barely move": a latent path that chooses Travel for an animal that can't move. Reproduce by changing `world.Rng("planner", who)` to `world.Rng("planner-salt", who)` in `Planner.Choose` and running `CohesionGateSeed23Tests`.
Follow-up: caribou regroups sit near the 0.5 floor on main (seeds 17 and 123 below it in most samples) and a little lower with this change (seed 23: 0.32, 0.50, 0.43 against main's 0.69, 0.52, 0.47). Fewer walks onto kin's tiles mean fewer regroups. Whether that floor still measures lockstep is a band to tune.
Follow-up: the mammoths still starve (Jack's comment on #154: graze row 90 against a burn of 47, capacity about 3 a tile). Less churn doesn't change their intake against their burn; that's a number for Jack (#179).
Revised: first red was the hungry-alone path, not the kin-leaving interrupt: `IndividualsTests.AHungryGrazer_WithKinWithinOneTile_StaysPut_AndGoesToKinFartherOff` (kin one tile off) fails on main with the mammoth stepping onto its kin's tile at minute 14 and back at 19.
Revised (review): #83's decision lists "a lone animal looking for kin it can see (`Kin.Cohere`)" as unchanged, and the ticket says change only what's needed → dropped the `Cohere` half: only the hungry-alone path in `Needs` reads kin within one tile (the narrow variant, d). `Cohere` is as on main.
Revised (review): seed 123 with the narrow variant: mammoths move 24.9 tiles an animal-day (32.0 and 28.0 with the salted streams), regroup 10.1. Seed 42 allocated 460 MB a day, over the 406 band (main's own samples: 391, 372, 380, 451), reported, not failing. Other lines outside a band: 26 across the four seeds, against main's 21, 22 and 26.
Revised (review): standards: the test's distance is `apartTiles`; the comment at the call says why, and the spec says how.
Follow-up: seed 17's ravens move 0.5 tiles an animal-day with this change (main 0.94-1.43); the rule doesn't reach ravens and the change's salted samples give 1.27 and 4.50, so it reads as chaos, but nothing here proves it.
