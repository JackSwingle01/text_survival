# #131: Share the warm-up worlds that several tests build alike

Hypothesis:
- Changes: rebuild/Sim.Tests TestKit (a SharedWorld helper: built once behind Lazy, handed out as save/load copies with the shared map), ReportTests, Phase3GateTests, SubstrateTests, slow-fast-tests.txt
- Affects: ReportTests' Lived() world (built 4 times by 3 tests) and Phase3GateTests.Run(160) (built by SubstrateTests.SignsAt_AgreesWithTheFindDump and LocationFeaturesSignsAndTags_NeverChangeTheWorld); no rebuild/Sim behaviour
- Stays green: ReportTests, SubstrateTests, Phase3GateTests, ArchitectureTests, the fast suite
- First red: none expected; a refactor of test setup, checked by the same assertions passing on copies
Revised (review): design review asked whether ArchitectureTests.ABusyRun_RaisesNoEventWithoutATile could share the busy day → no: it collects events from RunCollecting(200), a different run, and a copy holds no events; left alone.
Revised (review): the first test to touch a shared world pays its build, so which test carries it varies → measure each in the whole fast suite and take off the list only those under 5 s there.
Revised: CI gates on the flagged tests re-run alone, serially and untiered, so a test alone still pays its shared build → measured that way: Dump_covers_every_kind 3.7 s, Reports_change_nothing 3.9 s, SignsAt_AgreesWithTheFindDump 3.5 s, LocationFeaturesSignsAndTags_NeverChangeTheWorld 3.5 s came off slow-fast-tests.txt; Dump_is_deterministic is 5.7 s alone (still two builds) and stays on it.
Follow-up: ShelterTests.SnowShelter_MeltsByTheHour_HoweverOftenItIsLookedAt took 10.6 s in the parallel fast suite (2.2 s alone untiered, so CI passes it); untouched here.
