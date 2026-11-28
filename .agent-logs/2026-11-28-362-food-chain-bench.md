# #362: Food chain bench: who eats what, per day and week, over N seeds

Hypothesis:
- Changes: Sim/Fauna.cs (an optional, unsaved intake sink on World, called where an animal's body takes in kcal: grazing and feeding), a new report in Sim.Tests (FoodBenchReport, [ReportFact("FOOD_BENCH")]) with its aggregation split from the stepping, nightly.yml (a food-bench job and artifact), publish_baselines.sh, CLAUDE.md and documentation/verification.md.
- Affects: nothing in the sim's behaviour: the sink is null unless a bench sets it. Save format unchanged (the sink is not saved), no new WorldEvent.
- Stays green: the fast suite, CommentHygieneTests, TestWorldSourceTests, WhereThingsGoTests, SaveTests.
- First red: FoodBenchTests (kills in the matrix equal the Killed events of the run) fails on a missing FoodBench type.
Revised (review): burn derived as intake minus the change in held store would make "a source equals the body's own accounting" true by construction → burn is reported as net-derived and labelled so; the intake check is a controlled run (a resting animal at a carcass, burn at its known per-minute rate); measuring travel and mending burn apart is a follow-up.
Revised (review): the sink classifying Fed into own kill, scavenged, remains and store put bench rules in Sim → the call sites pass only what was eaten (IntakeKind: Grazed, Carcass, Remains, Store) and the carcass's Cause; the bench classifies own kill by Cause.Who being the feeding herd.
Revised (review): per-body Body.Eat calls would count a herd's feeding once per member → the sink is called per herd and per source, from FeedMinute and GrazeMinute, before Nourish splits it.
Revised (review): the kill matrix could not sum to Killed with starvation (By null), person and unresolved killers → rows for none, Human and unknown, so every Killed event lands in one cell; species are read per minute from a live id map.
Revised (review): species named by literal would trip WhereThingsGoTests' SpeciesNamedInCode → the bench iterates the Species enum.
