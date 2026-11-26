# #288: A crossing goes at the walker's speed each minute, and no walker that can move is slower than a tenth

Hypothesis:
- Changes: `Traversal` (Crossing holds `Covered`, not `TotalMinutes`; `Advance` adds each minute's share, priced
  then), `Trip` (`Terms`, `Pricer`, `StepMinutes` read a floored speed and take the walker's species),
  `SpeciesTable` (one walking-speed floor shared with running), the save fixture, the spec's walking line.
- Affects: a crossing's length is fixed at `Begin` today, so a walker wounded, loaded or mended on the way keeps
  the quoted time; a body just above speed zero is quoted up to `int.MaxValue / 16` minutes. Forecasts and the
  stepped crossing agree because both are priced once, at the start.
- Stays green: `ActionSequenceTests`, `TravelTests`, `ForecastTests` (StepMinutes and route agreement), `TripTests`,
  `SaveFixtureTests` after regeneration, `LameTests` except the >10x pin.
- First red: a new test where an animal with a leg at a hundredth mends to nine tenths after a minute fails on
  taking ~100x the fresh crossing instead of under 2x.

Revised: scouted inline rather than by a sub-agent (the change is two files, `Travel.cs` and `Trip.cs`); the design review's sub-agent swept the other readers → it found `Web/Host/ViewBuilder.cs`, `Web/Host/Observer.cs`, and three tests that build a `Crossing` by hand.
Revised (review): a lame careful walker pays care's half-again on ground only its bad legs make risky, so it can take ~15x a fresh walker at the same pace, against the criterion's ten → care's slowing and the offer of a careful pace (`Trip.PaceMatters`) both read the ground and sky alone, at a fresh walker's footing; the accident risk still counts bad legs. Reading both from one quantity keeps care from ever being free.
Revised (review): "its load counted" → the floor is a tenth of a fresh body's unloaded speed, held under the walker's loaded speed, as `SpeciesTable.RunningIfAble` does for running; both bounds of the property hold against a fresh body carrying the same share of its capacity, since both bodies get the floor.
Revised (review): float progress needs an epsilon → `Crossing.Covered` is a whole number of parts out of `Crossing.Whole` (2^40); each minute adds ceil(Whole / minutes), so a constant crossing of N minutes arrives at exactly minute N, and the save holds an integer.
Revised (review): a minute whose body updates left the walker lame (`LiveBodies` runs before `Advance`) used to count, and on the last minute the walker still arrived → it covers nothing, and the walk stops with the reason. A body at speed zero has no speed to price.
Revised (review): a crossing that becomes blocked part way (a thawing river) → it finishes, as now; `Advance` never asks `Blocked`.
Revised: the criterion's animal (one leg at a hundredth) is a quadruped on three sound legs, which sets off barely slowed (10 min against 8 fresh) and passes with the crossing priced once → the test is a Theory over one bad leg (the issue's case) and all four, where the caribou sets off at the floor and only per-minute pricing brings it in within twice the fresh time.
Revised: the committed fixture was stale before this change (old code regenerates it at minute 145, not 189) and held no crossing, though `save-format.md` lists one → `SaveTests.SampleWorld` gives its caribou a crossing a third done; the regenerated fixture differs from old code's only in that crossing.
Revised: changed pre-existing tests: `TravelTests.ACrossingsInjury_ComesFromThePoolsOfTheTileWalkedOnto`, `TroddenEdgesTests` (animal walk helper) and `TracksTests` built a one-minute crossing with `TotalMinutes = 1` → `Covered = Crossing.Whole - 1`; `TroddenEdgesTests.AnimalCrosses` read `Begin().TotalMinutes` → `Trip.StepMinutes`.
Revised: changed pre-existing test `LameTests.OneLegAtAHundredth_IsOfferedTheWalk_AndItsCrossingIsFarLonger` pinned a crossing over ten times a whole body's → more than twice and at most ten times (the whole body sits on the five-minute shortest crossing), the floor's rule.
Revised: changed pre-existing test `ForecastTests.Routes_OverTroddenEdges_AgreeWithTheWalkMade_ForAPersonAndAnAnimal` walked under heavy snow, which fills a trail a little each minute, so a caribou's crossing priced 10 at its start took 11 → `TroddenSnow` lays settled snow (0.4 m, compaction 0.9) under the dry fixed sky, so the ground barely changes on the way; the trail still pays and the per-step and route totals agree exactly.
