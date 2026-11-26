# #271: An NPC never drops a Need without a reason, and never paces

Hypothesis:
- Changes: `rebuild/Sim/Npc.cs` (each pursuit answers a step, a lack, or an impossibility; Decide records each Need passed over), `rebuild/Sim/WorldEvent.cs` and `Memory.cs` (NeedDropped, NeedStuck, registered but never filed in journals), `rebuild/Sim.Tests/NpcBenchReport.cs` (stuck and dropped counts, reversals from a shared helper), a new `Reversals` helper and `NpcNeedTests` in `rebuild/Sim.Tests`.
- Affects: `Npc.Mind.Decide` walks the ranked needs and silently takes the first pursuit with a step; `ForWarmth` explores on any Missing, including the impossible "a fire starter"; water and food explore at Need when they have no source; `ForRest` gives no step when too cold to sleep. The bench counts reversals inline.
- Stays green: NpcTests, NpcFireTests, ArchitectureTests, WhereThingsGoTests, SaveTests, the fast suite.
- First red: the no-starter scene fails because ForWarmth explores instead of raising NeedDropped naming the fire starter; the property test finds NeedStuck states.
- Scout: done inline, not by a sub-agent (the mind is one 900-line file read whole; events from deciding go to Decide's events list, as a hunt's do).
Revised: the bench's reversal rule moved to Sim.Tests/Reversals.cs first, on its own commit → seeds 1, 7, 101 over 13 days give the same lines before and after (reversals 81, 109, 64).
