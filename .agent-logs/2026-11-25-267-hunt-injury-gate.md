# #267: A predator ranks a hunt by its kill chance and gates it by the chance of injury, which hunger loosens

Hypothesis:
- Changes: species.json (injuryChances beside killChances), Content.Tables (load + shape check), Combat.InjuryChance, Fauna (Needs.cs: Desperation, InjuryTolerance, one gate read by HuntAt, QuarryHere and Disperse), KillOdds.Measure (kill and injury), KillChanceTableReport (prints both), ForecastTests.
- Affects: `HuntAt` drops a hunt under kill 0.5 (Needs.cs:180); `QuarryHere` picks the best kill chance on the tile with no gate; `Disperse` weighs with-the-pack against alone by kill chance with no gate. A lone sabre-tooth never hunts; three wolves take on four bison.
- Stays green: ForecastTests, PackTests (but for the 0.5-gate case), HerdBudgetTests, the fast suite.
- First red: HuntGateTests' kill 0.20 / injury 0.00 case fails to compile (no gate function), then fails as not a source under the 0.5 gate.

Revised: the re-measured kill table came out identical to the one in species.json (the measurement is seeded) → pasted only the new injuryChances section; killChances unchanged.
Revised: the new tests could not go red on behaviour first, as `Fauna.HuntPays`, `Fauna.Desperation` and `Fauna.HuntSource` did not exist → the first red was a compile failure; the rule cases were written from the issue's numbers before the gate was written.
Revised: the acceptance criteria need "lists the hunt as a food source" for a fed animal, whose food is not due, so no plan shows it → added the forecast `Fauna.HuntSource(world, animal, tile)` (HuntAt with its hunters as they are) as the seam, with sightings given directly.
Revised: a fed pack's side members are not food-due, so `Fauna.Hunters` counts only the lead → the fed-three-wolves case also checks the gate on the three-hunter chances directly.
Revised: the content check also refuses an injury table whose predators and quarries differ from killChances (the gate reads both) → InjuryTableProblems.
Revised: changed pre-existing test `PackTests.APairOfWolves_LeavesBisonAlone_AndAPackOfEightTakesThemOn` (asserted the 0.5 gate). On the strip a wolf at hunger 0.8 reaches starvingAt in about two hours, and a starving pair risks four bison (injury 0.74 ≤ 0.75), so the pair now hunts by minute 150 → renamed `APairOfWolves_LeavesBisonAlone_UntilStarving`: hungry (0.8) it leaves them alone for an hour; starving (0.9) it takes them on.
Revised: changed pre-existing test `ForecastTests.KillChance_AgreesWithHeadlessPackHunts` → `KillAndInjuryChances_AgreeWithHeadlessPackHunts`, checking both tables, with three more cases (three wolves on four bison, a lone hyena on megaloceros, a lone sabre-tooth on caribou).
Revised: `KillOdds.Measure` returns (Kill, Injury) → its one other caller, KillChanceTableReport, prints both sections.
Revised: the cohesion gate counts no hunts or predator deaths in fights → added the report `HuntValleyReport` (HUNT_VALLEY=1, HUNT_VALLEY_SEED) for the before-and-after numbers, run on a worktree of origin/main and on this branch.
Revised: no separate adversarial design-review agent was run before code; the issue fixed every rule and number → the code review after the build is the only review.
Revised (review): the gate overload checked HuntPays and every caller looked the kill chance up again → `GatedKillChance` (the kill chance, or 0 when the hunt fails the gate) is the one read in HuntAt, QuarryHere and Disperse.
Revised (review): the PackTests rewrite dropped the pack-of-eight case → restored as a starving pack of eight that takes four bison on.
Revised (review): the test helper `ListsTheHunt` let an optional hunger override its desperation and skip an assert → it takes hunger alone and returns the hunters' count, which the three-wolves case asserts.
Revised (review): Desperation, KillChanceFloor and the tolerances carry no Pct/Level suffix (principles-code.md) → kept, matching KillChance and Hunger beside them.
Follow-up: a hyena's starvingAt is 1.1 and hunger stops at 1, so its desperation tops out at 0.67 (tolerance 0.55); whether hyenas should reach full desperation is Jack's call.
Follow-up: the starving pair of wolves on four bison passes the gate by 0.01 (injury 0.74 against 0.75); a re-measure after a combat change may flip it, and `PackTests.APairOfWolves_LeavesBisonAlone_UntilStarving` with it.
Follow-up: "desperation" and "injury chance" are not in GLOSSARY.md.
