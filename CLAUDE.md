## Primary References

**Always consult these documentation files at the start of a conversation:**

- @principles.md
- @principles-code.md
- @./documentation/overview.md

The principles doc tells you *how to approach* the codebase. The overview doc tells you *what exists*.

## Build and Test

```bash
dotnet build
dotnet test
```

For the clean-room rebuild (`rebuild/`):

- While iterating: `dotnet test rebuild/Sim.Tests --filter "Category!=Slow"` (about 35 s). Run the
  full `dotnet test rebuild/Sim.Tests` once before handing work back.
- Tag any new test that takes 5 s or more with `[Trait("Category", TestCategories.Slow)]`.
- Run long simulations, sweeps and probes in Release (`-c Release`), and split independent seeds
  across parallel processes.
- Skip the old suite (`text_survival.Tests`) and the full solution build unless files outside
  `rebuild/` changed.
- Wait for long runs with `run_in_background` and its completion notice, not `until ...; do sleep`
  polling. A poll that greps for its own pattern (`pgrep -f "X"` from a shell whose command line
  contains "X") never ends, and leftover pollers outlive the agent that started them.
## Domain Language and Decisions

- `GLOSSARY.md` defines the project's terms (stat tiers, effects, conditions, contest...). Use them.
- `documentation/decisions/` holds decision records (`YYYY-MM-DD-short-name.md`, a paragraph each)
  for hard-to-reverse, surprising choices. Read them before reopening a design question.
