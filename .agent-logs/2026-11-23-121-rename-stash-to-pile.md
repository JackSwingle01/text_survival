# #121: Rename stash to pile

Hypothesis:
- Changes: every rebuild module that names the tile's pile (Map, Items, LocationFeatures, Memory, Signs, Activity, WorldEvent, Work, Npc, Narration, Report, Verbs, Pools, content discoveries.json, Web Host and wwwroot, Cli), Save.
- Affects: nothing in behaviour; the save's field names, activity and event kinds, the pile's cause text ("stashed by" becomes "put down by"), and the words the player reads ("stash" in feature names, refusals, narration, map tooltip, CLI command).
- Stays green: the fast suite, with tests changed only by names and the strings they assert.
- First red: SaveFixtureTests.ASaveFromBeforeThePileRename_LoadsItsStashesAsPiles fails on a format 25 save being refused.

Revised: this log was started after the mechanical rename, not before the first line of code → kept the hypothesis as it stood when the rename began.
Revised: the decision record "Save compatibility from first release" says no migrations yet, but the ticket's acceptance criteria ask for a pre-rename save to load → wrote the first migration, Save.Migrate25, a JSON rewrite of format 25 to 26 before reading, as the ticket asks; the record is left as it is, and save-format.md names the exception.
Revised: names chosen for the verbs, since the ticket names no verbs: the activities Stash and Unstash become PutDown ("putdown") and PickUp ("pickup"), matching the player's "Put down X" and "Take X" labels; the events Stashed and Unstashed become PutOnPile ("putonpile") and PickedUp ("pickedup"); the UI verb kind "stash" becomes "putdown"; the CLI command "stash" becomes "putdown"; the hidden find StashFind and its "stash" content flag become PileFind and "pile", since a found cache is laid as a pile.
Revised: the private helpers Items.Pile and Npc's local Pile would have clashed with the new names → renamed them PileOrNew and WithPack.
Revised: MedicineStash and TrapperStash are salvage finds, not piles, and the glossary's Cache is a different thing → renamed them MedicineBundle ("Medicine Bundle") and TrapperBundle ("Trapper's Bundle"), like the existing Cordage Bundle, so "stash" leaves the content too.
Revised: refusal text "no X stashed here" → "no X in the pile here"; ItemsTests and SubstrateTests assert the new words.
Revised: pre-existing tests changed by names only: every test that built a Stash or Unstash activity, read TileState.Stash or StashAt, or asserted a "stash" word (NarrationTests, ReportTests, SubstrateTests, GameHostTests, ContentTests' MedicineStash path); slow-fast-tests.txt follows the renamed test AStash_OffersTakingEachThingInIt.
Revised: added RetiredWordsTests.Stash_IsNamedPileEverywhere, a scan of rebuild for "stash" outside the migration, its fixture and its tests, so the rename stays done.
Revised: the regenerated sample-save.json differs from the format 25 one only in the renamed names, the cause text and the version, which shows no behaviour moved; the migration test also requires the migrated old save to write the same bytes as the current sample.
Follow-up: documents outside rebuild/ that still say stash (feature-parity-checklist, npc-simulation-plan, emergent-world-design, authored-content-catalog, specs and decision records) were left alone as plans and history; only save-format.md was updated.
