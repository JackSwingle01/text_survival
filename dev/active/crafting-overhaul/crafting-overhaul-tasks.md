# Crafting overhaul implementation checklist

## Planning complete

- [x] Inspect the current catalog, UI, execution, gear, and project model.
- [x] Record selected proposals 1, 5, and 3 in dependency order.
- [x] Record proposals 2, 4, and 6 as deferred.

## A — Evaluation and truthful previews

- [ ] Define shared selection/evaluation and side-effect-free output preview.
- [ ] Include all existing prerequisites, shortages, exact input/tool choices, and infinite-durability semantics.
- [ ] Validate at commit before time/resources are spent; define consistent completion failure behavior.
- [ ] Display actual benefits and named owned-gear comparisons with appropriate units.
- [ ] Show current-condition time, exact material/tool costs, and tool-break warnings.
- [ ] Show project setup and subsequent work separately with known modifiers.
- [ ] Show complete blockers and useful next steps without inventing acquisition locations.
- [ ] Verify blocked actions, previews versus execution, and project-time disclosure.

## B — Families and discovery presentation

- [ ] Assign stable recipe/family IDs and map the entire catalog.
- [ ] Build six-section family navigation and include Fishing.
- [ ] Nest methods/material variants within families without changing ingredient rules.
- [ ] Add stable selection, search, Ready/All filters, and consolidated display names.
- [ ] Separate plan visibility from material possession; keep aspirations visible and variants collapsed.
- [ ] Verify every recipe is reachable and locked states remain understandable.
- [ ] Manually check minimum-window layout, navigation, and the cutting-tool selection flow.

## C — Cutting-tool lifecycle

- [ ] Audit gear use, breakage/removal, equipped references, and save compatibility.
- [ ] Establish supported design identity and exact target-instance selection.
- [ ] Implement explicit add-handle transitions with retained edge condition and reduced input cost.
- [ ] Implement sharpening with a below-new cap and clear costs/eligibility.
- [ ] Implement edge replacement retaining the handle and consuming one target exactly once.
- [ ] Preserve scoped broken handled tools as unusable repair targets.
- [ ] Preview Make/Improve/Maintain outcomes and preserve applicable equipment state.
- [ ] Measure early-game tool churn; tune lifespan and scoped task wear from that baseline.
- [ ] Verify two-identical-item targeting, no duplication/free loops, failed operations, and save round trips.
- [ ] Playtest survival contexts where improvising, maintaining, replacing, and postponing are each reasonable.

## D — Extensions and integration

- [ ] Move existing clothing mending onto explicit targets and accurate previews.
- [ ] Extend the proven approach to spear improvements retaining shafts.
- [ ] Run relevant action/inventory/persistence checks and integration tests for changed shared behavior.
- [ ] Update crafting documentation to describe shipped behavior.
- [ ] Update context/checklist and retain the deferred backlog for later work.

Acceptance details and design boundaries are in [the plan](crafting-overhaul-plan.md). All implementation tasks are pending.
