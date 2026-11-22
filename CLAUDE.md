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

- Each red-green cycle: only the test class or file in hand,
  `dotnet test rebuild/Sim.Tests --filter "FullyQualifiedName~<ClassName>"`. Before each commit: the fast
  suite, `--filter "Category!=Slow"` (about 35 s). Before handback or PR: the full
  `dotnet test rebuild/Sim.Tests`. Stryker and long sims run in CI (`.github/workflows/mutation.yml`).
- Agents choosing which checks to run or add use the `mp:verification` skill.
- Tag any new test that takes 5 s or more with `[Trait("Category", TestCategories.Slow)]`.
- Run long simulations, sweeps and probes in Release (`-c Release`), and split independent seeds
  across parallel processes.
- Skip the old suite (`text_survival.Tests`) and the full solution build unless files outside
  `rebuild/` changed.
- Wait for long runs with `run_in_background` and its completion notice, not `until ...; do sleep`
  polling. A poll that greps for its own pattern (`pgrep -f "X"` from a shell whose command line
  contains "X") never ends, and leftover pollers outlive the agent that started them.
- For long-running agent work (e.g. overnight), schedule an hourly check for stalled agents and leftover processes.
## Domain Language and Decisions

- `GLOSSARY.md` defines the project's terms (stat tiers, effects, conditions, contest...). Use them.
- `documentation/decisions/` holds decision records (`YYYY-MM-DD-short-name.md`, a paragraph each)
  for hard-to-reverse, surprising choices. Read them before reopening a design question.
- Before adding any feature to the rebuild, read "Where things go" in
  `documentation/clean-room-architecture.md`: the home, rule and touch points for each kind.
