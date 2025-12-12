# CLAUDE.md

Operational guide for Claude Code when working with this repository.

## Primary References

**Always consult these documentation files first:**

- @./documentation/principles.md — Design philosophy, architecture hierarchy, patterns, anti-patterns, decision frameworks
- @./documentation/overview.md — Current systems, what's working, what's planned, what's dropped

The principles doc tells you *how to approach* the codebase. The overview doc tells you *what exists*.

## Build and Run

```bash
dotnet build
dotnet run
dotnet test
```

Requires .NET 9.0.

## Testing

Use the `play_game.sh` helper script for interactive testing:

```bash
./play_game.sh start    # Start game in background
./play_game.sh send "1" # Send input
./play_game.sh log      # View output
./play_game.sh tail     # Follow output
./play_game.sh stop     # Stop game
```

See **TESTING.md** for full details.

## Project Structure

```
text_survival/
├── Core/           # Program.cs, World.cs, Config.cs
├── Actions/        # Expeditions/, camp actions
├── Actors/         # Actor.cs, Player/, NPCs/
├── Bodies/         # Body parts, DamageProcessor, capacities
├── Survival/       # SurvivalProcessor (stateless, returns results)
├── Effects/        # EffectRegistry, EffectBuilder
├── Environments/   # Zone, Location, Features/
├── Items/          # Property-based items
├── Crafting/       # Crafting system (being reworked)
├── IO/             # Output.cs, Input.cs (all I/O goes here)
└── UI/             # Display rendering
```

## Architecture Quick Reference

```
Runners    → Control flow, player decisions, display UI
Processors → Stateless domain logic, returns results
Data       → Hold state, minimal behavior
```

- **GameRunner** calls into specific runners (ExpeditionRunner, etc.)
- **Processors** (SurvivalProcessor, ExpeditionProcessor) take data, return deltas/effects/messages
- **Data objects** (Body, Expedition, Location) hold state; owners apply processor results
- **Features** live on locations, currently hybrid data+behavior

## Critical Boundaries

- **IO Namespace**: All input/output through `Output.cs` and `Input.cs`. Never use `Console.Write`, `Console.ReadLine`, etc. directly.

- **Single Mutation Entry Points**: Use `Body.Damage(DamageInfo)` for damage — never modify body parts directly. Processors return results, callers apply.

- **Backwards-Compatibility Smell**: Before adding special code for "legacy" handling, stop and consult. Usually means something should be migrated or removed.

## Issue Tracking

**ISSUES.md** is the central issue tracker. Update it when you:
- Find bugs during development or testing
- Discover unintended behavior
- Identify balance problems
- Encounter crashes
- Resolve an existing issue

## Development Workflow

- Active work lives in `dev/active/`
- Completed work moves to `dev/complete/`
- Update `dev/CURRENT-STATUS.md` with changes
- Keep status docs brief — what was done, files changed, next steps

## What's Dropped (Don't Implement)

- Character skills/stats that improve over time
- Magic/shamanism systems
- Meta-progression/unlocks
- Complex nutrition (protein/fat/carb tracking)
- ActionBuilder fluent pattern (replaced by explicit Runners)

See @./documentation/overview.md for full context on what's current vs. dropped.