# #205: The capacity read list goes on the ratchet

Hypothesis:
- Changes: `dev/ratchets.md` (a `capacity reads` section); `CapacityReadTests` (scans Cli, Tune, TuneViewer, Tune.Tests and Host.Tests by their built files, and counts a call of `Effects.CapacityDeltas` from outside `Effects` as a read); `Sim.Tests.csproj` (builds those projects first, without referencing them); the eight test members first on the list, rewritten as outcomes.
- Affects: no sim behaviour. The list gains the three `CapacityDeltas` reads in the tests (SurvivalTests, EffectsPropertyTests, DirtyTests) and loses eight entries.
- Stays green: the fast suite; `CapacityReadTests` with the list updated.
- First red: `OnlyTheBodyReadsACapacity` fails naming the three unlisted `CapacityDeltas` reads once the rule counts them.

Revised: the tracker's parser takes only lower-case letters, digits and hyphens in a section name → the section is `## capacity-reads`, so its issues read `Ratchet: capacity-reads`.
Revised (review): `batch_issue` titles an issue with every item joined, and eight entries of about 80 characters pass GitHub's 256-character title cap (the dry run never calls GitHub) → `ratchets.py` cuts the title's item names to fit, ending in "and N more".
Revised (review): `internal` does not hide the capacities, since Sim grants InternalsVisibleTo to Sim.Tests, Tune and Host.Tests, and the rule's breaching sample would not compile once they are hidden → "When empty" says to make them visible only to the body's own types and to delete CapacityReadTests' reads of them, its breaching sample included, since the compiler then holds the rule.
Revised (review): the long-run check builds Sim.Tests alone, so the new not-referenced projects lengthen it → measured below; kept, since a rule that reads a stale or missing build of Tune or Host.Tests would pass wrongly.
Revised: building Sim.Tests from clean (`--no-incremental`, Release, this runner) takes 21.5 s with the new not-referenced projects against 18.8 s without → kept; about 3 s on the long-run check.
Revised: ArchUnitNET 0.13.5's loader takes only loaded assemblies, not file paths → the rule loads the five builds into a collectible load context of its own, and `TheToolsAndTheOtherTests_AreRead` fails if any of them is missing from what it reads.
Revised: adding the three `CapacityDeltas` reads changed which entries come first in ordinal order → this ticket's batch is the first eight of the list after they joined: two of them (`EffectsPropertyTests.Readouts_AreSeverityScaledSumsOfTheTable`, `Phase2GateTests.S`) are not among the first eight of the list as it stood.
Revised: "never below a fifth" held for each capacity, but at full hunger, thirst and tiredness a whole human's dexterity share is 0.183 and its vision 0.192 → `Discomforts_AreMonotone_AndNeverLeaveAWholeBodyHelpless` holds every faculty share above a sixth.
Revised: `Readouts_AreSeverityScaledSumsOfTheTable` restated the deltas' formula, capacity by capacity, beside the core temperature and bleeding sums → the core and bleed sums stay as `CoreTempAndBleedReadouts_AreSeverityScaledSumsOfTheTable`; the formula restatement is deleted, and `AWorseEffect_NeverRaisesAFaculty` holds what a caller sees of it (every effect's table only takes from a body).
Revised: the job as the issue words it says "the list" without naming it → the section's job opens with one sentence naming `CapacityReadsToday` in `CapacityReadTests`; the issue's words follow unchanged.
