# #362: Food chain bench: who eats what, per day and week, over N seeds

Hypothesis:
- Changes: Sim/Fauna.cs (an optional, unsaved intake sink on World, called where an animal's body takes in kcal: grazing and feeding), a new report in Sim.Tests (FoodBenchReport, [ReportFact("FOOD_BENCH")]) with its aggregation split from the stepping, nightly.yml (a food-bench job and artifact), publish_baselines.sh, CLAUDE.md and documentation/verification.md.
- Affects: nothing in the sim's behaviour: the sink is null unless a bench sets it. Save format unchanged (the sink is not saved), no new WorldEvent.
- Stays green: the fast suite, CommentHygieneTests, TestWorldSourceTests, WhereThingsGoTests, SaveTests.
- First red: FoodBenchTests (kills in the matrix equal the Killed events of the run) fails on a missing FoodBench type.
