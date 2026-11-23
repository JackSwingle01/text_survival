# #113: Ratchet: fast-test-time — ReportTests.Curve_has_one_point_per_chunk, HerdTests.Gate_ThreePopulatedDays_AreIndependentOfChunkingAndSaves, ForecastTests.TheGatherForecast_AgreesWithAnHourOfForaging

Hypothesis:
- Changes: Sim.Tests only (ReportTests, HerdTests, Verification/ConservationWorlds, slow-fast-tests.txt); no rebuild/Sim code
- Affects: alone in Release, Curve 9 s (four populated days of World.New(17)), ThreePopulatedDays 15 s (two runs of three populated days), GatherForecast 10 s (180 bare worlds, each regenerating its map: Map.Of costs ~35 ms after Save.FromJson, the load itself 0.5 ms, the hour of work ~3 ms)
- Stays green: the fast suite; test_durations.py with the three lines removed
- First red: none, a speed ratchet: each test's alone time is the measure

Revised: the scout and the adversarial design review as sub-agents → done inline: three test files, the time measured by a scratch stopwatch test, no Sim code touched
Revised: Bare() said loading costs about a millisecond, but every load regenerates the seed's map (~35 ms) → Bare and Seeded hand the template's GeneratedMap (read-only after generation) to each world they load: speeds every Bare user, changes no check
Revised: Curve_has_one_point_per_chunk checks the chunking and the day numbers, not the animals → runs on the bare world (ConservationWorlds.Seeded)
Revised: ThreePopulatedDays is three populated days by nature → tagged Slow; quick sibling Gate_APlantedHuntsDay_IsIndependentOfChunkingAndSaves on the bare world with only the planted pack and prey, so the Advance-chunking-and-saves rule keeps a fast check
Follow-up: BodySurvivalPropertyTests.FixedPoint_TheCoreSettlesOrLeaves_ItNeverOscillates took 6.3 s in one fast-suite run (1 s alone, under 5 s on the rerun): a CsCheck property that loads no world, slowed by contention, so it can fail test_durations.py by chance. Check: its duration across a few CI runs' fast.trx
Revised (review): the sibling copied the three-day test's body line for line → both call one AssertIndependentOfChunkingAndSaves(planted, minutes)
Revised (review): the sibling looked arguable beside Gate_OneLongStep_ManyShortOnes_AndSaveLoad_GiveTheSameWorld → kept: that one steps the player through 300 minutes with Sim.Step, and only the sibling checks Sim.Advance over a day that includes the planted hunt's encounter
Revised (review): ConservationWorlds written out by full namespace → a using, as NpcTests has
Follow-up: HerdTests.ALameAnimal_StaysBehind_AndTheHerdMovesOn (World.New(17), untouched here) took 5.4 s in the post-review fast suite, 3 s alone: unlisted tests near the limit cross 5 s by chance on a 4-core runner, so test_durations.py passed on one of three runs here. Check: durations of the near-limit unlisted tests across CI runs, and whether the 3 s listing margin needs widening
