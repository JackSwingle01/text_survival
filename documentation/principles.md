# Text Survival — Development Principles

## Project Philosophy

A console-based Ice Age survival game where fire is the anchor. Every expedition is a commitment — leave camp, do the work, return before your fire dies. Player knowledge is the only progression; the game doesn't get easier, you get better.

**Core design principle:** Intersecting pressures create meaningful decisions. A wolf isn't dangerous because it's a wolf — it's dangerous because you're bleeding, low on fuel, and far from camp. Single systems create problems. Intersecting systems create choices.

**Design tests** — the game works if:
- An experienced player reaches a meaningful decision in under 5 minutes
- Two players would reasonably choose differently in the same situation
- Fire margin creates genuine tension without tedium
- Stories emerge from systems interacting, not authored sequences

---

## Architecture

### Hierarchy

```
Runners    → Control flow, player decisions, display UI
Processors → Stateless domain logic, returns results
Data       → Hold state, minimal behavior
```

**Runners** own the loop: present choices, call processors, display results. Never calculate domain logic directly.

**Processors** are pure-ish functions: take data in, return results (deltas, effects, messages). Don't own state, don't display anything, don't mutate inputs.

**Data Objects** hold state. Owners orchestrate updates by passing data to processors and applying returned results.

### Key Patterns

- **Processors return, callers apply.** A processor calculates what should change and returns it. The caller decides whether and how to apply those changes. This keeps logic testable and data flow explicit.

- **Single entry points for mutation.** Major state changes go through defined entry points. `Body.Damage(DamageInfo)` is the only way to damage a body — never modify body parts directly. This keeps mutation traceable and logic centralized.

- **IO abstraction.** All input/output goes through the IO namespace (`Output.cs`, `Input.cs`). Never use `Console.Write`, `Console.ReadLine`, etc. directly. This keeps the codebase IO-agnostic for testing and future interfaces.

- **Data-driven behavior.** Threats, items, effects — define as data, process with generic systems. If you're writing `if (type == "wolf")`, that's data pretending to be code.

- **Properties over categories.** Items are property bags (weight, hardness, flammability). Actions query properties. Behavior emerges from data, not item-specific code.

- **Features live on locations.** A location's capabilities come from its features. Features can be data-heavy or have some behavior — pragmatism over purity.

---

## Design Direction

### What's Core

- **Fire as tether** — every decision runs through "can I make it back?"
- **Expeditions, not navigation** — commit to round trips with time variance and event risk
- **Depletion creates pressure** — areas exhaust, pushing outward or forcing camp moves
- **Knowledge is progression** — no character stats that grow, player learns the systems
- **Contextual events** — interrupts that create decisions based on player state, not random encounters

### What to Avoid

- **Character progression systems** — conflicts with knowledge-is-progression philosophy
- **Recipe lists** — show needs, not craftable items
- **Random encounters** — events should be contextual, tied to what the player is doing and carrying
- **Navigation then decision** — "go to forest, then decide what to do" breaks the expedition commitment model
- **Simulation without decision** — complexity should create player choices, not just fidelity

---

## Working With This Codebase

### Before Suggesting Changes

1. **Locate it in the hierarchy.** Is this Runner logic (flow/display), Processor logic (domain calculation), or Data (state)? If unclear, that's a design smell.

2. **Extend before creating.** The existing systems (body, effects, features, survival) are foundations. Can they handle this, or does it genuinely need something new?

3. **Trace to fire margin.** Does this feature create pressure on the core loop? If it doesn't interact with the fire-as-tether constraint, reconsider its priority.

4. **Work backward from player experience.** What decision does this enable? If the player never sees the distinction, don't model it.

### When Implementing

- **Explicit over clever.** If/else chains are fine. Avoid reflection, magic strings, patterns requiring multi-file tracing. Control flow should be obvious.

- **Fail early at boundaries.** Validate in Runners before calling Processors. Processors can assume valid input.

- **Wait for 3 examples before abstracting.** Concrete code is easier to refactor than wrong abstractions. Let patterns emerge from duplication.

- **Aggregate where identity doesn't matter.** Fuel can be mass + quality. Discrete items only where condition, history, or uniqueness matters.

- **Backwards-compatibility is a smell.** Before adding special code for "legacy" handling or backwards compatibility, stop and reconsider. Usually means something should be migrated or removed, not patched around.

### Common Decisions

**"Where does this go?"**
- Player choice, menu flow → Runner
- Calculation, simulation, rules → Processor  
- State that persists between ticks → Data object
- Location capability → Feature

**"Should I add a new system?"**
- Can an existing system handle it? (Effects model most temporary modifiers)
- Does it create pressure through the fire bottleneck?
- Does it require character stats? (Probably conflicts with philosophy)

**"How detailed should this be?"**
- Does the detail create a decision?
- Will the player perceive the distinction?
- Does it interact with other systems, or run in parallel?

---

## Anti-Patterns

| Pattern | Problem |
|---------|---------|
| Processors that own state | Breaks testability, hides data flow |
| Runners that calculate domain logic | Mixes concerns, duplicates rules |
| Character skills that improve | Conflicts with knowledge-is-progression |
| Events as random rolls | Should be contextual to player state |
| Type-specific branches (`if wolf`, `if knife`) | Data pretending to be code |
| Abstractions before 3 examples | Premature, usually wrong |
| Systems that don't touch fire margin | Probably not core priority |

---

## Summary

The game is about intersecting pressures through a single bottleneck (fire). Architecture separates flow (Runners), logic (Processors), and state (Data). Player knowledge is the only progression — no stats, no unlocks, no meta-game.

When evaluating a feature or fix, ask:
1. Does this create a meaningful decision?
2. Does it pressure the fire-margin loop?
3. Where does it sit in the hierarchy?
4. Can existing systems handle it?

When in doubt: explicit over clever, extend before creating, decision over simulation.