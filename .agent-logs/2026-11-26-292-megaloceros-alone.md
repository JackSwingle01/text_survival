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
