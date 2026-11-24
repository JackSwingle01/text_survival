# #138: RouteSearchTests gets its world from TestWorlds

Hypothesis:
- Changes: rebuild/Sim.Tests/RouteSearchTests.cs only: its shared `Lazy<World>` of `World.New(17)` goes; each test takes `TestWorlds.New(17)`, a load of seed 17's shared save on its shared map.
- Affects: the four RouteSearchTests and TestWorldSourceTests.TestsGetTheirWorlds_FromTestWorlds (red on main for this file).
- Stays green: every RouteSearchTests test, checking the same things on a world that saves the same as World.New(17).
- First red: TestWorldSourceTests.TestsGetTheirWorlds_FromTestWorlds, already red on main.
Revised: no scout or design-review sub-agents: the change is one file, one line's pattern, and the issue names the fix; the ratchet test is the check.
Revised: a fresh copy per test rather than one shared loaded world: Trip.Routes keeps scratch and step prices on the World, a copy costs ~6 ms, and it keeps each test independent of the others' order.
Revised: code review done inline, not through sub-agents: the diff is five lines in one test file; nothing found (each test's assertions and oracle unchanged, the allocation report still warms its own search first).
Validated: RouteSearchTests (4) and TestWorldSourceTests pass in Release; the fast suite passes, 1569 tests.
