# Ratchet: formatting, last batch, TerrainRendererTests.cs ... TerrainPreview/Program.cs (#263)

**Hypothesis.** The ten files differ from `dotnet format whitespace` only in whitespace: indentation,
braces moved to their own line, one-line `case` lists split. Formatting them changes no token, so the
old game, its suite and the four tools still build and pass.

Revised: skipped the scout, design and adversarial-review sub-agents, and the code-review pass, as the
previous batch did (`2026-11-25-ratchet-formatting-batch-hud.md`). A tool makes the change, and a check
that strips all whitespace from each file before and after confirms it. A sub-agent would only run
that check again.

Revised: left the ratchet's "When empty" step (the CI `format` job checks every file) out of this PR.
The issue's acceptance criteria allow no changes beyond the ten files, and that step changes CI. It is
left to the retire issue that the daily ratchet run files once the remaining command prints nothing
(`retire_issue` in `.github/scripts/ratchets.py`).

- `dotnet format whitespace --folder --include <the ten>`, then `--verify-no-changes`: passes.
- Each file with all whitespace stripped is byte-identical to `HEAD`'s. None of them has a verbatim
  or raw string literal whose contents the formatter could change.
- `dotnet build` (solution): 0 errors, 0 warnings. `dotnet test text_survival.Tests`: 750 passed, 0 failed.
- `tools/GameCli`, `tools/HudPreview`, `tools/PixelArtCli` and `tools/TerrainPreview` are not in the
  solution. Each builds on its own: 0 errors, 0 warnings. They have no tests.
- The ratchet's remaining command now prints nothing.
- No `rebuild/` files changed, so the rebuild suite was not run.

Follow-up: none. The ratchet run files the retire issue (widen CI's `format` job, delete the section).
