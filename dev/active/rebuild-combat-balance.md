# Combat balance needs retuning after the self-ally fix

**Status:** open, deferred (decided 2026-11-20: keep the fix, retune later)
**Where:** `rebuild/Sim/Combat.cs`, `rebuild/Sim/CombatAI.cs` (port of old `Combat/`).

## What changed

The old combat code counted every unit as its own ally: it got the ally boldness bonus from itself, and
its own wound fired AllyDamaged (+0.3 x cohesion, +0.6 for a wolf) on top of TookDamage (-0.1), so
wounded animals got bolder. The rebuild fixes this (regression test in `CombatTests`).

Headless outcomes, 1000 fights each (old numbers measured with the bug patched out of the harness):

| Scenario | With old bug | With fix |
|---|---|---|
| Spear-armed human vs lone wolf: human wins | ~76% | ~8% |
| Same: wounded wolf breaks off (AnimalFled) | low | ~27% |
| Same: human defeated | — | ~35% |

## Related tuning quirks kept for parity (decide together)

- Weapon damage is on a different scale from bites: a spear does 8 against animal bites around 0.45
  (0-1 scale), so one spear hit is near-fatal.
- Unarmed damage 0.15 for every human (old player value; old NPCs used 0.1).
- The old player's Fighting skill bonus on every blow is skipped (no skills yet).
- Every combat round uses activity factor 2.0 (old hunts used 1.5).

## What to do

Retune morale (boldness inputs, flee threshold) and/or the damage scales so the spear-vs-wolf fight
lands where the design wants it, then re-pin `CombatGoldenTests` statistics with a note. The design
target is a decision, not a port question: a lone wolf should be a dangerous but winnable fight for a
prepared player (principles.md: preparation should matter).
