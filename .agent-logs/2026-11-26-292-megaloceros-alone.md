# Megaloceros may be alone: one to three strong (#292)

**Hypothesis.** Three one-line changes and their tests. `Populate.cs` places megaloceros `2 + rng.NextInt(2)`
strong; `1 + rng.NextInt(3)` draws once from the same stream, so every other species' start is placed by the
same draws, and only the megaloceros counts (and so the agent ids after them) move. The megaloceros row in
`Fauna.RenewalOf` arrives `new(0.0014, 2, 3)`; `new(0.0014, 1, 3)` also lets an arrival come in when the
valley has room for only one (`members < MinMembers` skipped it before). The cohesion gate applies
`MaxAlonePct` (a band) and `BrokenAlonePct` (a failure) to every `OpenGroup` species; megaloceros stays in
`OpenGroup` (the blob and regroup bands still read it) and only the two alone checks skip it. Expected
breakage: tests pinned to seed 17's population and ids, and the sample save fixture if it is built from a
populated world. Baseline gate (baselines branch): megaloceros alone 2.2 / 2.3 / 9.3 / 0.2 % on seeds
123 / 17 / 23 / 42.

There is no existing test of an arrival's size bounds (`RenewalTests.ArrivalsRun` checks only room), so
"the existing arrivals test, with the new bounds" becomes a new test in `RenewalTests`.

Revised: no scout or design-review sub-agent. The issue names the three lines, each was read in full along
with the gate and the arrival code, and a sub-agent would add cost and no information.

Revised: the start test populates seed 17's map over 200 world seeds (a world built like `World.New` on a
shared map, then `Fauna.Populate`), not 200 `World.New`s: map generation is not the subject, and 200 maps
would make it a Slow test. The placement's draws come from the world seed's "populate" stream, so the
counts vary as they do across real seeds.

Revised: nothing needed re-pinning. The fast suite passed on the first run after the change (2055 passed,
2 skipped): no hash or long run in it reads seed 17's megaloceros counts. The Slow tests that name
megaloceros (`HerdGoldenTests`, `HuntGateTests`, `ForecastTests`) spawn their own groups.

Revised: the sample save's fixture is not regenerated. `SURVIVAL_UPDATE_SAVE_FIXTURE=1` rewrites it (193+/566-),
but it rewrites it identically with this branch's two Sim changes stashed, on main's code: the drift is
main's, not this change's (`SampleWorld` places no megaloceros).

Checks: `Megaloceros_StartOneToThreeStrong` and `Megaloceros_ArriveOneToThreeStrong` red ([2, 3]) then green.
Cohesion gate, four seeds side by side in Release, about 2 minutes each: all pass. Megaloceros alone 2.3 / 30.4 /
1.0 / 6.7 % on seeds 123 / 17 / 23 / 42 (baselines 2.2 / 2.3 / 9.3 / 0.2 %); seed 17's 30.4 % would have
failed `BrokenAlonePct` (25 %). Their regroup band is outside on 17, 23 and 42 (0.00 / 0.14 / 0.23), as on
the baseline (17, 42, 123); biggest-herd and moves bands are within on every seed, as before.

Follow-up: `rebuild/Sim.Tests/Fixtures/sample-save.json` no longer matches what `SaveFixtureTests.SampleWorld`
generates on main (the round-trip test still passes, since it loads and re-saves the file). Someone should
check whether that drift is expected.

Revised (review): the gate's two alone checks read one list, `AloneLimited` (`OpenGroup` without megaloceros),
in place of `MayBeAlone` tested twice; the arrival test reaches midnight as the file's other tests do. Not taken:
reading the start size off the arrival row (caribou and bison already repeat theirs; out of this ticket's scope).

Follow-up: Populate's start sizes for caribou, bison and megaloceros repeat their arrival rows' bounds by hand
(`5 + NextInt(11)` against 5-15, and so on); one source would keep them from drifting.
