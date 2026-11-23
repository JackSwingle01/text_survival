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
