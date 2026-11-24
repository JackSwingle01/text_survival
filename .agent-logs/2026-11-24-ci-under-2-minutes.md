# CI under two minutes

**Hypothesis.** CI takes about 255 s, in one job: restore 9, build 34, tests 174, the fast-test-time
re-run 31. The tests are CPU-bound: four assemblies share four cores, and `Sim.Tests` is about three
quarters of the work, so nothing short of more machines gets under 120 s. Split `Sim.Tests` into shards
by a hash of each test's name, and give the other test projects a job each.

Revised: four, six and eight hash shards measured 113-120 s, with thirteen checks. Jack: at most
three test checks, named for what they test, because the suite keeps growing -> dropped the shards
(`TestShard.cs` never merged).

Revised: three checks by kind of test. `TestCategories.LongRun` marks the 63 tests that step a world
for hours or days; they get their own check, the rest (`rebuild quick`) run with the tools' suites, and
the old game keeps its own. On x64 that measured quick 102 s, old game 111 s, long-run 144 s.

Revised: runtime settings do not help the long-run tier (server GC, gen0 size, untiered, PGO off: all
inside the noise of two identical baseline jobs, 82 s and 111 s). The noise is the hardware: x64 jobs
land on anything from an EPYC 7763 to a Xeon 6975P. For the quick tier, `TC_CallCountingDelayMs=0`
with `TieredPGO=0` measured 63 s -> 46 s on a 7763.

Revised: the arm64 runners (four real cores, one model) measured quick 72 s and long-run 122 s -> both
rebuild checks moved there. The old game stays on x64: 21 UI tests need `cimgui`, which has no
linux-arm64 build.

Revised: on arm64 the long-run check was held up by one class: `KillChainTests` ran 45 s of its tests
one after another -> its renewal section became `RenewalTests`.

- `slow-fast-tests.txt` and the `fast-test-time` ratchet are gone: every test on that list is now
  LongRun, and the 5 s limit applies to the quick tier with no exceptions.
- The old game's suite is skipped on a PR that changes only `rebuild/` and notes.

Follow-up: a simulated minute costs about 0.5 ms with a populated map. A sampled profile puts roughly a
fifth of it in `world.Agents` lookups (a `SortedDictionary`), another fifth in `Body.GetCapacities`
(`TissueCapacities` allocates through LINQ for any body that is not whole). Making those cheaper
without changing a result would shorten the long-run check more than any CI change left.

Follow-up: `KillChainTests.Bare` empties a world, but herds arrive in an empty valley within days, so a
40-day test of a carcass or a body still pays for the animals that came. Tests whose subject is not
animals have no way to keep them out.

Follow-up: `dotnet-trace`'s sampler is biased to allocation sites on this code (it put 31 % in
`World.Rng`, which a stopwatch measured at 3 %). `sample` with `DOTNET_PerfMapEnabled=1` gave the
profile above.
