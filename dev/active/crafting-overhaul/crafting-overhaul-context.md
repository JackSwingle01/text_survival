# Crafting overhaul context

## Session progress — 2026-09-06

- Completed: inspected current recipe catalog, crafting overlay/handler, effort calculation, gear durability, projects, and repository design principles.
- Completed: recorded the user's selected scope (original proposals 1, 5, and 3) and deferred proposals (2, 4, and 6).
- Completed: strategic plan and implementation checklist.
- Not started: gameplay implementation, balancing, UI changes, or runtime testing. This session is planning only.

## Decisions

- Ship recipe families and trustworthy previews before equipment lifecycle changes.
- Family grouping wraps existing concrete recipes. Generic material substitution is deferred.
- Share evaluation between display and execution, including existing prerequisites and effective time.
- Keep basic purposes and major aspirations visible; progressively disclose variants.
- Prototype improvements and maintenance with cutting tools, explicit selected targets, and named transitions. Do not introduce a full component simulator.
- The proposed sharpening cap and durability changes are provisional tuning decisions, not measured balance conclusions.
- Preserve existing save compatibility and unrelated workspace edits.

## Key files (paths relative to repository root)

- `principles.md`: time pressure, readable cause/effect, tradeoffs, and complexity budget.
- `Crafting/NeedCraftingSystem.cs`: all 71 recipes, discovery filtering, existing prerequisites.
- `Crafting/CraftOption.cs`: material/tool checks, consumption, output, repair and rebuild behavior.
- `Crafting/CraftingEffort.cs`: current-condition work time and warnings.
- `Crafting/NeedCategory.cs`, `Crafting/NeedCategoryDisplay.cs`: current categories/labels.
- `Desktop/UI/CraftingOverlay.cs`: category strip, recipe list, detail panel, selection.
- `Actions/Handlers/CraftingHandler.cs`: progress, time advancement, output placement and equipment behavior.
- `Items/Gear.cs`: gear capabilities, immutable properties, durability/repair, infinite-durability convention.
- `Items/Inventory.cs`: exact tool selection, equipment slots, removal behavior.
- `Environments/Features/CraftingProjectFeature.cs`: ongoing project state.
- `Actions/Expeditions/WorkStrategies/CraftingProjectStrategy.cs`: project work and shovel modifiers.
- `Persistence/SaveManager.cs`: JSON serialization/load behavior.
- `text_survival.Tests/`: action, inventory, persistence, and smoke-test patterns.
- `documentation/crafting-system.md`: significantly stale in parts; update when implementation ships, not as if the plan were implemented.

## Resume

Read [plan](crafting-overhaul-plan.md), [tasks](crafting-overhaul-tasks.md), and [deferred scope](deferred.md). Check current git status and any applicable repository instructions. Begin with shared evaluation and preview; verify current execution and breakage paths before modifying them. Keep this context and checklist synchronized with actual progress.
