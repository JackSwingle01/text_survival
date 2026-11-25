# Ratchet: formatting, batch of HudAction.cs ... CameraTests.cs (#223)

**Hypothesis.** The ten files differ from `dotnet format whitespace` only in whitespace: indentation,
line breaks inside one-line switch arms, and trailing spaces. Formatting them changes no token, so the
old game's build and suite stay green.

Revised: skipped the scout, design and adversarial-review sub-agents, and the code-review pass. The
change is mechanical, made by a tool, and checked by stripping all whitespace from each file before and
after (identical token streams in all ten). A sub-agent would only repeat that check.

- `dotnet format whitespace --folder --include <the ten>` then `--verify-no-changes`: passes.
- Whitespace-stripped content of every file is byte-identical to `HEAD`'s.
- `dotnet build`: 0 errors. `dotnet test text_survival.Tests`: 750 passed, 0 failed.
- The ratchet's remaining command lists ten files, none of this batch.
- No `rebuild/` files, so the rebuild suite was not run.
