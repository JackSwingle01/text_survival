## Authored-content repair — completed September 6, 2026

- [x] Restore original predator catalog/choices instead of consolidating them onto generic menus.
- [x] Replace retired predator severity dependencies with explicit scene kinds and actual source/situation gates.
- [x] Bind incidental predator outcomes to the stated species; reject missing/stale sources.
- [x] Wire retreat, fire, bait, and harvesting to physical actions; grab-and-run actually travels afterward.
- [x] Preserve separate map/rendering work; correct only two test assertion forms needed for compilation.
- [x] Reconcile the archived content inventory: 91 declarations verbatim, four wording adaptations.
- [x] Validate: 619 tests pass; production authored camp/carcass UI inspected; diff whitespace check clean.

The earlier checklist below records the original implementation. Its content-removal decisions are superseded by this repair and `content-coverage.md`.

# Predator state migration tasks

- [x] Inspect current behavior, event, combat, UI, and save integration.
- [x] Define scope, migration phases, and acceptance criteria.
- [x] Phase 1: inventory all direct/dynamic producers and consumers; capture baseline checks.
- [x] Phase 1: add source-bound events, physical proximity queries, and shared encounter validation.
- [x] Phase 1: verify source identity, distance units, stale sources, and multiple packs.
- [x] Phase 2: add herd target memory and following/searching with bounded simulation stepping.
- [x] Phase 2: implement accessible food transfer/consumption and species deterrence.
- [x] Phase 2: add player observations and one complete tension-free wolf scene.
- [x] Phase 2: verify feeding, lost contact, deterrence, combat, and elapsed-time behavior.
- [x] Phase 3: migrate pack/threat/location scenes and outcome helpers.
- [x] Phase 3: migrate remaining content, signs, environmental details, and indirect consumers.
- [x] Phase 3: support generic bear/hyena sources and prevent overlap with dedicated legacy arcs.
- [x] Phase 3: audit that no gameplay caller reads or mutates the three tensions.
- [x] Phase 4: persist herd memory/observations; implement idempotent legacy cleanup.
- [x] Phase 4: replace predator severity UI with observations; remove obsolete APIs and conditions.
- [x] Phase 4: verify save identity, invalid references, missing fields, and cooldowns.
- [x] Phase 5: run targeted tests, full suite, build, and desktop wolf scenario.
- [x] Phase 5: document any removed unsupported content and final verification results.

Acceptance gates and detailed scenarios are in `predator-state-migration-plan.md`. All implementation phases are complete. Final full suite: 571 passing tests. Production screens rendered and inspected; complete wolf and retreat flows exercised through scripted UI.
