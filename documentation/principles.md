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

## Design Direction

### What's Core

- **Fire as tether** — every decision runs through "can I make it back?" Fire status is always visible so players can judge their margin.
- **Camp vs expedition** — camp is safe, everything else is an expedition with time cost and risk. Flexible travel between locations, but you're always away from camp.
- **Depletion creates pressure** — areas exhaust, pushing outward or forcing camp moves
- **Knowledge is progression** — player learns the systems across permadeath runs. Within a session, camp/gear improvements provide progression; across sessions, player skill is the only thing that carries over.
- **Contextual events** — interrupts that create decisions based on player state, not random encounters

### What to Avoid

- **Meta-progression systems** — no unlocks or persistent upgrades between runs
- **Recipe lists** — show needs, not craftable items
- **Random encounters** — events should be contextual, tied to what the player is doing and carrying
- **Simulation without decision** — complexity should create player choices, not just fidelity

---

## Working With This Codebase

- **Simple > Complex.** Write direct, obvious code. If/else chains are fine. Avoid patterns requiring multi-file tracing.

- **IO abstraction.** All input/output goes through the IO namespace (`Output.cs`, `Input.cs`). Never use Console directly.

- **Wait for 3 examples before abstracting.** Concrete code is easier to refactor than wrong abstractions.

- **Extend before creating.** Can existing systems handle this, or does it genuinely need something new?

- **Work backward from player experience.** What decision does this enable? If the player never sees the distinction, don't model it.

---

## Summary

The game is about intersecting pressures through the fire bottleneck. Player knowledge is the only progression.

When evaluating a feature or fix:
1. Does this create a meaningful decision?
2. Does it pressure the fire-margin loop?
3. Can existing systems handle it?

When in doubt: simple over complex, extend before creating.