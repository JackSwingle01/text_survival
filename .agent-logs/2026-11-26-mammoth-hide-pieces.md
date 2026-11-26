# A mammoth's hide comes off in pieces of up to 3 kg (#287)

**Hypothesis.** Today a `MammothHide` piece may weigh 1.5 to 15 kg, so `Carcasses.TakeReady` gives a
mammoth's 15 kg of hide as one piece, and every mammoth recipe (hood 1, coat 1, pack 1, tent 2) eats a
whole kill's hide. Narrowing the ranges in `items.json` to MammothHide [1.5, 3], ScrapedMammothHide
[1.2, 2.4] and CuredMammothHide [1.08, 2.16] makes `TakeReady` cut 15 kg into 5 × 3 kg (full pieces
while two pieces' worth waits, then the rest evenly), scraping gives 2.4 and curing 2.16, inside the new
ranges. The load check (`Content.Tables.cs` `WeightProblems`) reads the minimum piece weights, which do not
change, so no gear weight changes. The graveyard loot (1.5 to 2.5 kg, one piece) is already inside.
The save fixture has no mammoth items, so it should not change.

Scout (sub-agent): tests that place pieces above the new maximum: `RackTests.cs:404,433`,
`MakeTests.cs:611`; `TestKit` nominal weights 10/8/7.2 (clamped, but stale). Comments calling a bag
necessary for two mammoth hides: `CampBuildsTests.cs:257`, `MakeTests.cs:813`. `shelters.json`'s
MammothHide `pieceKg` 5.0 is a separate table (the amount shown when a shelter lacks it, and continuous
materials only); it no longer matches a real piece.

Revised: no adversarial design-review sub-agent. The change is three numbers in one content file plus
tests; the scout already read every reader of those numbers, and the issue decides every behavior.

Revised: shelters.json MammothHide `pieceKg` 5.0 is left alone. It is a nominal figure, the kilos named
when a shelter is short of a material, not a real piece (Hide's is 1.0 against pieces of 0.1 to 60 kg),
and the issue says nothing else changes.

Built: `items.json` ranges narrowed; `TestKit` nominal pieces 3 / 2.4 / 2.16; the tests that placed 10, 8
and 7.2 kg pieces (`RackTests`, `MakeTests`) now place 3 and 2.4 and expect 2.4 and 2.16. New:
`CarcassTests.AMammothsHide_ComesOffInFivePiecesOfThreeKilos`, `ItemsTests.AMammothHide_HeavierThanThreeKilos_IsRefused`,
`RackTests.OneMammothsHide_ScrapedAndCured_MakesATentACoatAndAHood_WithOnePieceLeft` (butchered with a blade,
five scraped by hand, cured four then one on the rack, then the tent, coat and hood; one cured piece left).

Revised: the end-to-end rack test carries a large bag. Five fresh 3 kg hides with the knife and fire kit are
more than the bare 15 kg pack holds, and `Items.Receive` drops what does not fit, so the first scraped hide
landed on the ground. That is the carrying rule working, not the thing under test.

Removed: the bag workaround in `MakeTests.EveryRecipe_CanBeMade_FromWhatTheLandAndAKillGive` (bags no longer
kept on the pack) and the large bag in `CampBuildsTests.MammothHideTent_IsSewnFromTwoCuredMammothHides`. Both
pass without them. Checked the harness without the workaround against the old ranges: it fails
(`cure_mammoth_hide: ScrapedMammothHide never reaches the pack`), so the workaround was for these hides.

Load check: unchanged weights. `WeightProblems` reads the minimum piece weights, which did not change;
`ContentTests` pass. The sample save holds no mammoth items.

Review (standards and spec sub-agents): spec found nothing missing. Fixed: `RackTests.Daylight` renamed
`WaitForDaylight` (it advances the world; `Phase2GateTests.Daylight` is a predicate) and `Rested` renamed
`Rest`, now also used by the older hide-to-strips rack test in the same file. Not taken: moving the rest
top-up into `TestKit` across MakeTests, CarcassTests and CampBuildsTests (outside this ticket; Follow-up
below), reading `TestKit`'s nominal weights from `items.json` (they are nominal by design, set per kind), and
`shelters.json`'s 5.0 (above).

Follow-up: one `TestKit` helper for the sleep, food and water top-up repeated across MakeTests, RackTests,
CarcassTests (`Refresh`) and CampBuildsTests.

Checks: fast suite (`Category!=Slow`, Release) 2056 passed, 2 skipped, before the review commit; RackTests
28 passed after it. The sample save fixture test passed unchanged, so no regeneration.
