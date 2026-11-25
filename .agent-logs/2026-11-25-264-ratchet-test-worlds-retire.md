# 264 — Ratchet: test-worlds, retire

Hypothesis: `NotYetOnTestWorlds` is already empty (emptied by the MapTests move), so deleting it and
passing `MayCallDirectly` alone to `CountRatchet` changes no outcome: every direct World.New /
Save.FromJson / Map.Generate call in Sim.Tests is already in `MayCallDirectly` at its exact count.
- Affects: `TestWorldSourceTests.TestsGetTheirWorlds_FromTestWorlds`, `dev/ratchets.md`.
- Nothing else names the list or the `test-worlds` section (grep, outside `.agent-logs/`).

Revised: no scout or design-review sub-agents, and no TDD cycle: the change is two deletions the
issue spells out word for word, and the existing test is its check. A mutation stood in for red.
Revised: the class comment and `MayCallDirectly`'s "Not a ratchet" note named the gone list; reworded
to say only what holds now.

Evidence:
- `TestWorldSourceTests` passes (Release).
- Mutation: with `CoreTests.cs` dropped from `MayCallDirectly` it fails with "CoreTests.cs has 1 direct
  ... calls (allowed 0)"; restored.
- `python3 .github/scripts/ratchets.py list` runs: comments 37 remaining, formatting 10 remaining.
- Fast suite (`--filter "Category!=Slow"`, Release): 1967 passed, 2 skipped, 0 failed.
