# Migration 3 context

Content requirement added September 6: follow the predator content restoration principles linked from the implementation plan. Preserve contextual decisions and authored payoff; a physiological label alone is not equivalent content. No restoration implementation has happened yet.

## SESSION PROGRESS — September 6, 2026

Planning complete. No migration 3 gameplay changes made. User is playtesting migration 1 and requested plans for migrations 2 and 3. Implementation is pending a subsequent request.

The first predator migration remains uncommitted in this workspace; preserve it. Its recorded suite result is 571 passed, not a new validation of this plan.

## Key files and constraints

- `Survival/SurvivalProcessor.cs`: authoritative thermal processing and existing cold effects. Raw hourly rate differs from buffer-aware core change; effects currently inspect starting body temperature while hypothermia processing uses projected temperature. Check update ordering when adding observations.
- `Bodies/SurvivalContext.cs`: activity/location/fire/wetness/clothing inputs; use current context for current-condition assessment.
- `Actions/Events/GameEventRegistry.ColdSnap.cs`: severity-driven weather/numbness/frostbite arc and unsupported shelter/return promises.
- `Actions/Events/GameEventRegistry.Megafauna.cs`: additional cold tension producers during hunts.
- `Actions/Events/TensionEventFactory.cs`, `ThresholdEventFactory.cs`, `WeatherEventFactory.cs`: overlapping warning sources; assign a single owner for physiological transitions.
- `Actions/GameContext.cs`: update sequencing, event interruption, and legacy resolve-near-fire behavior.
- `Actions/Handlers/FireHandler.cs`, `CraftingHandler.cs`, `Actions/Expeditions/TravelRunner.cs`, `WorkRunner.cs`, `WorkStrategies/ShelterImprovementStrategy.cs`: actual response mechanics. Improvement requires an existing shelter; it is not emergency construction.
- `Actions/GameEvent.cs`, `Actions/GameEventRegistry.cs`: real action delegates, validation, and elapsed-time behavior from migration 1.
- `Desktop/UI/SurvivorPanel.cs`: body-temperature/rate presentation to align with assessment.
- `Persistence/SaveManager.cs`, `Actions/Tensions/*`, `Actions/ConditionChecker.cs`, `Actions/EventCondition.cs`: retirement and compatibility seams.
- `text_survival.Tests/Survival/SurvivalProcessorTests.cs`, wetness/warmth tests, companion survival tests, persistence tests: regressions to cover.

## Decisions to preserve

No replacement cold severity. Derive state from the existing model; notification memory only suppresses repeat messages. Warming is distinct from recovery. Fire/shelter/travel choices act through ordinary mechanics, with no automatic healing. Keep current frostbite abstraction and avoid unsupported tissue-specific prose. Do not reuse `ProjectTemperatureAwayFromFire` as an accurate route prediction because it retains the starting location's shelter temperature.

## Resume

Read the [plan](cold-state-migration-plan.md) and [tasks](cold-state-migration-tasks.md). Start with the legacy-key and warning-owner inventory, then implement the pure assessment before scene rewrites. Check new user playtest findings and current worktree. Related: [migration 1](../predator-state-migration/predator-state-migration-plan.md), [migration 2](../predator-species-migration/predator-species-migration-plan.md).
