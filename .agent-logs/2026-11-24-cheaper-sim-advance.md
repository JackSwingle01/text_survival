# Make Sim.Advance cheaper per simulated minute

**Hypothesis.** A populated world (`World.New(17)`, 96 agents) spends about 20 % of `Sim.Advance` in
`world.Agents` lookups (a sorted dictionary walked once per agent read) and about 20 % in
`Body.GetCapacities`, half of that in `TissueCapacities` building lists and tuples through LINQ for
every wounded body. Both can go without changing a result: fill capacities into spans on the stack
with the same arithmetic in the same order, and have the `Nearby` index (an unsaved cache that already
holds every agent) hand out agents instead of ids, so reads of who stands on a tile skip the dictionary.
`World.Agents` itself stays as it is: its type and order are the save.

- Reproduced the profile first (macOS `sample` on a console harness, perf map, tiered compilation off):
  `SortedSet.FindNode` + `get_Item` 10.7 % inclusive, VSD dispatch stubs 8.4 % self, `GetCapacities`
  20.2 %, `TissueCapacities` 8.1 %, `IsWhole` 5.8 % self, `Fauna.Load` 25.1 %, `SideCount` 12.9 %.
- `Body.TissueCapacities` and `RegionCapacities` fill a caller's span; `Effects.CapacityDeltas` has a
  span overload; `GetCapacities` takes a `ReadOnlySpan<double>`. `WeakestOf` reads the three layers as
  `Enumerable.Min` did (first of equals, NaN wins).
- `NearbyIndex` holds `List<Agent>` per tile; `Nearby.At` returns agents. Every caller dropped its
  `world.Agents[id]`.
- Checks beyond the suite: the save after 20,000 and 60,000 simulated minutes of seed 17 hashes the
  same before and after; 300,000 random wounded bodies with random effects give bit-identical
  capacities, deltas, vitality, speed and strength before and after.

Revised: an issue (#163) and a `163-` branch were opened first → Jack said an issue gets picked up by
the issue agent and to work in the session; issue closed, branch renamed `cheaper-sim-advance`.

Revised: timing runs on a machine under load (load average 23) swung 2x between repetitions →
report the best of five repetitions, alternating builds.

Follow-up: the remaining `world.Agent(id)` reads (Scheduler, Combat, `World.Rng(system, who)`) are
about 4 % of `Sim.Advance`. Removing them means an id-keyed cache beside the sorted dictionary, which
`World.Agents` being public and mutable cannot keep exact without changing its type: Jack's decision.

Follow-up: `Body.IsWhole` is 7 % self time: it walks every tissue for every capacity read, and
`Sim.IsAlive` reads capacities for every neighbour in `Fauna.LoadOn` and `IsKin`. Caching it needs a
tissue to tell its body it changed (a parent reference, against principles-code.md).

Follow-up: `Trip.Routes` (20 %) and `Planner.Choose` (15 %) are now the largest parts of a minute.
