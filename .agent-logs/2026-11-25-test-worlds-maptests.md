# MapTests onto TestWorlds (#224)

**Hypothesis.** `MapTests.cs` has 27 direct `World.New`/`Save.FromJson`/`Map.Generate` calls. The tests
that only read seed 17's map, or use a world as a stage for the agent model, can take the shared one from
`TestWorlds`; the tests whose subject is the generator (named `Generate_*`, comparing against the old
generator's output or against themselves) and the save tests that check a save's bytes or what a load
rebuilds keep their direct calls.

Revised: no scout or design-review sub-agent. The change is one test file and two list entries, every
call site is visible in one read, and the job text decides each case; a sub-agent would add cost and no
information.

- Moved to `TestWorlds.Map`: `ANamedSite_SetsItsTilesGroundAndAir`, `ASitesOwnWater_TakesThePlaceOfTheRiversIce`
  (five seeds, under the 20-seed search exemption), `TileGround_MatchesOldTerrainLocation`,
  `PassStagesAndCamp_UseTheirOwnGround`, and the camp to compare in `Save_HoldsNoCampTile...`.
- Moved to `TestWorlds.New`: the world every save test starts from (it saves the same as `World.New`).
- Moved to `TestWorlds.Load`: the stripped copy in `Save_HoldsNoTerrain...` (only its size is read) and the
  mind check in `Agent_IsMembersAndAMind...`.
- Left direct, 16: the eight `Generate_*` tests (12 calls, generation is the subject); `Save.FromJson` where
  the load itself is the subject: the null `GeneratedMap` and regenerated map after a load, the camp derived
  on load, clothing derived on load, and the byte-exact round trip (4 calls). Listed in `MayCallDirectly`.

Revised: `NotYetOnTestWorlds` is left empty, not deleted. `dev/ratchets.md` says the daily run files the
"When empty" step as its own last issue once `remaining` prints nothing, and this issue says to change
nothing beyond its items.

Checks: `TestWorldSourceTests` and `MapTests` pass; fast suite (`Category!=Slow`, Release) 1648 passed,
1 skipped. The `test-worlds` remaining command prints nothing and exits 0.

Follow-up: the "When empty" step for `test-worlds` (delete `NotYetOnTestWorlds`, drop the ratchet's
section), which the ratchet run files next.
