# Migration 2 context

Content requirement added September 6: follow the predator content restoration plan linked from the implementation plan. Narrative coverage is a separate acceptance criterion; authored species scenes must accompany the mechanics. No restoration implementation has happened yet.

## SESSION PROGRESS — September 6, 2026

Planning complete. No migration 2 gameplay changes made. User is playtesting migration 1 and requested plans for migrations 2 and 3. Implementation is pending a subsequent request.

Migration 1 is present as uncommitted work; preserve it. Its recorded validation is 571 passing tests. That is the previous migration's result, not a fresh run for this plan.

## Key files and constraints

- `Actors/Animals/PredatorInteraction.cs`: shared pursuit, observation, sensing, food, deterrence, restore. Explicit saber exclusion and generic food handling are the main seams.
- `Actors/Animals/Herd.cs`, `AnimalType.cs`, `Behaviors/ScavengerBehavior.cs`, `Behaviors/SolitaryPredatorBehavior.cs`: single herd update, species mapping, existing feeding and competition.
- `Actions/Events/Variants/AnimalTypeVariant.cs`: selector and deterrence; saber/hyena currently fall through to Unknown.
- `Actions/Events/GameEventRegistry.SaberTooth.cs`, `GameEventRegistry.Scavenger.cs`: legacy authored arcs. Preserve species identity, replace severity-driven outcomes.
- `Actions/Expeditions/WorkStrategies/MegafaunaStrategy.cs`: shared mammoth/saber progression; migrate saber only.
- `Actions/Events/PredatorEventFactory.cs`, `Actions/GameEvent.cs`, `Actions/GameEventRegistry.cs`, `Actions/GameContext.cs`: source validation, real actions, pending combat and interruption. Reuse these safeguards.
- `Actions/Events/Situations.cs`, `OutcomeTemplates.cs`, `Actions/Tensions/*`, `Actions/ConditionChecker.cs`, `Actions/EventCondition.cs`: remaining legacy state consumers/producers.
- `Persistence/SaveManager.cs`: migration restore hook. Preserve actual serialized state; drop retired keys.
- `text_survival.Tests/Animals/PredatorInteractionTests.cs`: baseline source/food/encounter invariants to extend.

## Decisions to preserve

Species decisions share memory and encounter infrastructure, with one feeding/movement owner. Observations are player knowledge, not omniscient animal position. Food is a concrete accessible source; noise is a local action fact. No new severity proxy. Saber fire/noise rules are authored game rules. Unsupported narrative counters are retired or replaced by physical actions. Mammoths are out of scope.

## Resume

Read the [plan](predator-species-migration-plan.md) and [tasks](predator-species-migration-tasks.md), then audit all three legacy keys before editing. Check current worktree and any new user playtest findings. Related plans: [migration 1](../predator-state-migration/predator-state-migration-plan.md), [migration 3](../cold-state-migration/cold-state-migration-plan.md).
