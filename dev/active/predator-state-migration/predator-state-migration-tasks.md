# Predator state migration tasks

- [x] Inspect current behavior, event, combat, UI, and save integration.
- [x] Define scope, migration phases, and acceptance criteria.
- [ ] Phase 1: inventory all direct/dynamic producers and consumers; capture baseline checks.
- [ ] Phase 1: add source-bound events, physical proximity queries, and shared encounter validation.
- [ ] Phase 1: verify source identity, distance units, stale sources, and multiple packs.
- [ ] Phase 2: add herd target memory and following/searching with bounded simulation stepping.
- [ ] Phase 2: implement accessible food transfer/consumption and species deterrence.
- [ ] Phase 2: add player observations and one complete tension-free wolf scene.
- [ ] Phase 2: verify feeding, lost contact, deterrence, combat, and elapsed-time behavior.
- [ ] Phase 3: migrate pack/threat/location scenes and outcome helpers.
- [ ] Phase 3: migrate remaining content, signs, environmental details, and indirect consumers.
- [ ] Phase 3: support generic bear/hyena sources and prevent overlap with dedicated legacy arcs.
- [ ] Phase 3: audit that no gameplay caller reads or mutates the three tensions.
- [ ] Phase 4: persist herd memory/observations; implement idempotent legacy cleanup.
- [ ] Phase 4: replace predator severity UI with observations; remove obsolete APIs and conditions.
- [ ] Phase 4: verify save identity, invalid references, missing fields, and cooldowns.
- [ ] Phase 5: run targeted tests, full suite, build, and desktop wolf scenario.
- [ ] Phase 5: document any removed unsupported content and final verification results.

Acceptance gates and detailed scenarios are in `predator-state-migration-plan.md`. No implementation tasks have been completed.
