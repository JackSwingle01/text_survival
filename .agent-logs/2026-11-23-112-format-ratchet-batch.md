# #112: formatting ratchet batch (predator, craft, render)

**Hypothesis:** `dotnet format whitespace --folder --include <10 files>` changes only whitespace in
these old-game files (outside `rebuild/`), so behaviour is untouched; the old suite and the build stay
green, and the ratchet's remaining command stops listing the 10 files.

- Revised: no scout, design review, TDD or code-review sub-agents. The change is a tool-generated
  whitespace pass with no design and no behaviour; it is checked mechanically instead: each file's
  content with all spaces, tabs and newlines stripped is identical to `HEAD`'s.
- Revised: some lines were split (object initialisers one member per line, e.g. `EncounterState.Capture`);
  that is the formatter's whitespace rule, and the stripped comparison confirms no token changed.
- Checks: `--verify-no-changes` on the 10 files passes; ratchet remaining command prints 20 files,
  none of the batch (`text_survival.Tests/Desktop/Rendering/TerrainRendererTests.cs` is a different
  file); `dotnet build` 0 errors; `dotnet test text_survival.Tests` 750/750 passed. The rebuild suite
  was not run: no file under `rebuild/` changed.
