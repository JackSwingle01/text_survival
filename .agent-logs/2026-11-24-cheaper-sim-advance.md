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

Revised: Jack asked for the next low-risk ones after the PR opened → `SpeciesTable.Of` reads an array
by the species' number (was a read-only dictionary through its interface, 3.9 %), and `Fauna.Load`
asks `Shares` once per diet per call instead of once per neighbouring animal (`Shares` was 7.8 %; it
depends only on the two diets and the pool). 193-197 → 186 us per minute. Saves after 30,000-60,000
minutes of seeds 5, 17 and 99 hash the same as on `main`.

Revised: planned to make `World.Rng(system, who)` cheaper (5.4 %) → left. Its cost is the stream-name
string and two sorted-dictionary walks; skipping them needs a cache beside `World.Rngs`, which is
public, mutable and saved, the same question as `World.Agents`. Making the "body" stream lazy in
`Fauna.Live` would change which streams a save holds.

Revised: a comment on each of `Nearby.cs` and `Fauna.cs` tripped the comment-ratio ratchet → removed;
the names carry it (`sharesByDiet`).

Follow-up: the remaining `world.Agent(id)` reads (Scheduler, Combat, `World.Rng(system, who)`) are
about 4 % of `Sim.Advance`. Removing them means an id-keyed cache beside the sorted dictionary, which
`World.Agents` being public and mutable cannot keep exact without changing its type: Jack's decision.

Follow-up: `Body.IsWhole` is 7 % self time: it walks every tissue for every capacity read, and
`Sim.IsAlive` reads capacities for every neighbour in `Fauna.LoadOn` and `IsKin`. Caching it needs a
tissue to tell its body it changed (a parent reference, against principles-code.md).

Follow-up: `Trip.Routes` (20 %) and `Planner.Choose` (15 %) are now the largest parts of a minute.
