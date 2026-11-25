# #191: Hides cure on the rack

Hypothesis:
- Changes: `rebuild/Sim/Content/recipes.json` (add rack recipes `cure_hide`, `cure_mammoth_hide`; delete hand `dry_hide`, `dry_mammoth_hide`), `RackTests`, `MakeTests` (hide tests and the reachability walk).
- Affects: a scraped hide is today cured by "Dry a hide by the fire" (4 h of the person's work by a fire, 0.9 ×, 8% chance of wasting half); mammoth hide the same. The rack offers only exact recipe inputs, so a raw `Hide` is never offered once the input is `ScrapedHide`. NPCs never cure hides (Npc.cs uses only the fire's cook and melt recipes), so nothing changes for them. No save fixture holds either recipe.
- Stays green: `RackTests`, `MakeTests`, `ContentStrictTests`, `ConservationMakeTests`, `SaveFixtureTests`.
- First red: `RackTests.AScrapedHide_IsACuredHide_FortyEightHoursLater_WithNoOneActing` fails on no "Put on scraped hide" in the rack's menu.

Revised (review): a 48 h wait in `EveryRecipe_CanBeMade_FromWhatTheLandAndAKillGive` leaves the player and the camp fire unattended → the walk's rack step advances an hour at a time, keeping the fire up and the body topped up, as its Make steps do.
Revised (review): `CuredHide` comes from loot, so that walk would cure only the mammoth hide on the rack → `RackTests.AHide_IsScraped_ThenCuredOnTheRack_ThenCutIntoStrips` walks a raw hide → scraped → rack → strips.
Revised (review): the walk could build its rack through Make → it uses `Stations.Build`, since `curing_rack` is a placed recipe the walk leaves out and `RackTests` already builds it from branches, sticks and rope.
Revised (review): `dev/active/parity-audit-items.json` ITM-21 names `dry_hide` → left as is: it records the audit's "now" at the time of the audit.
Revised: `AHide_ScrapesAndDries_ToAShareOfItsWeight` and `AMammothHide_IsCuredBeforeItIsSewn` (pre-existing) drove the deleted hand recipes → their curing halves moved to `RackTests`; the first is renamed `AHide_Scrapes_ToAShareOfItsWeight`.
Revised: without `dry_hide` the reachability walk's scraped hides were never used up, the pack filled, and `large_bag` failed on "Rope never reaches the pack" → after each recipe the walk also returns what no carried recipe takes (a scraped hide), and a missing material is cured on a rack when a rack recipe makes it.
Revised: the issue said to decide the TDD seams without a person → used the existing seams: the rack's menu (`Verbs.At`), `Sim.Step`/`Sim.Advance`, and the recipe table.
Revised: `RackTests.Drying_IsNotByHand` (pre-existing) → it also names `cure_hide` and `cure_mammoth_hide`, and checks `cure_hide` is refused by hand.
Revised (review): `AMammothHide_IsCuredBeforeItIsSewn` no longer cures → renamed `AMammothHide_IsNotSewnRaw_AndScrapesToEightTenths`, and the parity checklist's references follow.
Revised (review): the spec's "a hide half cured" save check had no test → `RackTests.AHalfCuredHide_SurvivesSaveAndLoad_WithTheFinishMinuteUnchanged`.
Revised (review): the walk's last assert claimed more than it checked, and the rack comment read as one day → the assert checks only that a rack was used; the comment says "dry, still days".
