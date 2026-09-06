# Predator state migration context

## Session progress — 2026-09-06

Planning complete. Implementation not started. User requested a plan for the first migration discussed: derive predator content from simulation instead of tension severity.

Read `predator-state-migration-plan.md` for decisions and acceptance gates; use the tasks file during implementation.

## Key files

- `Actors/Animals/Herd.cs`: shared animal state, movement, boldness, members.
- `Actors/Animals/AnimalPresence.cs`: territory-based presence; needs a distinct physical/detected presence query.
- `Actors/Animals/Behaviors/{PackPredatorBehavior,SolitaryPredatorBehavior,ScavengerBehavior,HerdUpdateResult}.cs`: behavior integration and existing source-bound encounter requests.
- `Actions/GameContext.cs`: simulation ordering, encounter queue, species-only animal fallback.
- `Actions/GameEvent.cs`: event result mutations and species-only encounters.
- `Actions/Events/{GameEventRegistry.Pack,GameEventRegistry.Threats,GameEventRegistry.Locations,OutcomeTemplates,Situations,TensionEventFactory,EventQueue}.cs`: main content and event integration.
- `Actions/ConditionChecker.cs`, `Actions/EventCondition.cs`: old tension predicates.
- `Actions/Events/Variants/TrailSignVariant.cs`, `Environments/Features/EnvironmentalDetail.cs`: inspection currently creates predator tensions.
- `Combat/CombatOrchestrator.cs`, `Combat/CombatAftermath.cs`: real herd participants and persistent consequences.
- `Environments/Features/{CacheFeature,CarcassFeature,PlacedNet}.cs`: food representations and indirect consumers.
- `Desktop/UI/StatsPanel.cs`, `Desktop/UI/EventOverlay.cs`: tension presentation.
- `Persistence/SaveManager.cs`, `Persistence/GameInitializer.cs`: reference-preserving save graph and load initialization.

## Constraints

Only planning was authorized in this turn. No gameplay implementation or test execution was done.

The workspace already contains user changes in `Actions/GameContext.cs`, `Bodies/SurvivalContext.cs`, `Environments/Grid/GameMap.cs`, `Environments/Grid/TerrainType.cs`, `Environments/Location.cs`, `Environments/TravelProcessor.cs`, `Survival/SurvivalProcessor.cs`, and untracked surface implementation/tests. Preserve these and reread current files before editing.

Do not interpret the historical tension arc plan as an instruction to retain severity stages. Keep dedicated saber-tooth/scavenger tensions outside this pass, while migrating every use of the three generic predator tensions. Avoid source overlap creating duplicate combat.

## Resume

Start Phase 1 with a fresh call-site inventory and baseline build/tests. Resolve detection distance units and typed event source plumbing before converting narrative. There are no unanswered user decisions blocking the proposed approach.
