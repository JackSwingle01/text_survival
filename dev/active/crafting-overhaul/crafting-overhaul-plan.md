# Crafting overhaul: families, previews, and equipment investment

Status: planned; no gameplay implementation started.
Date: 2026-09-06

## Intent and scope

Make crafting easier to understand and make owned equipment worth investing in. The player should choose a useful outcome, see its actual benefit and cost, and decide whether to improvise, improve, maintain, or replace.

The user selected three proposals from the crafting review:

1. Organize crafting around recognizable object families.
5. Show the result, comparison, full commitment, and next step before crafting.
3. Develop improvisation, improvement, and maintenance into an equipment lifecycle.

Ship 1 and 5 first. They must work with the existing recipe catalog and costs. Implement 3 afterward as a cutting-tool slice, then extend proven behavior. The other proposals are recorded in [deferred.md](deferred.md).

## Current state

- `NeedCraftingSystem` defines 71 recipes across 12 categories. `CraftingOverlay` renders 11 category buttons; Fishing is omitted.
- Recipes mix object types, material variants, processing, repairs, and construction. Six cutting-tool entries cover crude edges and material-specific knives.
- Discovery checks hide actionable details until all ingredient types have been discovered, while the list still exposes locked recipe names grouped under the first missing resource.
- Detail views show requirements, base time, and durability. There is no consistent comparison with owned gear. Durability is labeled as uses even for gear whose wear follows different rules.
- `CraftingEffort.ForRecipe` calculates current-condition duration only after selection. Project recipes expose setup time separately from later work.
- `CraftOption.Prerequisite` is defined but has no callers in the inspected code. Availability must account for camp state as well as inventory.
- Gear already supports durability and repair. Mending recipes restore half of maximum durability to equipped slots. Improvements do not consume a specific existing item.
- `Gear` uses -1 for infinite durability, but crafting availability currently rejects values below 1. Preserve the actual gear semantics when unifying checks.

## Phase A: trustworthy evaluation and preview (proposal 5)

### One evaluation shared by UI and execution

Introduce a compact crafting evaluation owned by the crafting domain. Proposed shape: `Evaluate(context, selection)` returns blockers, selected inputs/tools/target, effective duration, tool wear, and an output preview. The selection initially identifies an existing recipe; Phase C adds an optional target item.

- Include material quantities, usable tools, and contextual prerequisites. Respect infinite durability.
- Use the same evaluation for list status, details, and committing work. Re-evaluate immediately before starting, before spending time or consuming anything.
- Resolve exact tool instances so the preview and execution agree about which tool wears out. Warn if this action uses its last durability.
- Resolve category inputs with the existing selection order and disclose actual materials. Choosing substitutions is deferred.
- Define consistent completion/input commitment behavior and validate it against time advancement. Do not consume a partial set of inputs if the operation cannot complete; do not grant partial or duplicate outputs.
- Make previews side-effect free. Do not build camp features or mutate inventory to calculate their benefits.

This phase enforces existing prerequisites. Newly adding fire, water, or tool requirements to recipes is a separate balance/rule-consistency follow-up, not a prerequisite for the UX release.

### Detail layout

Order the selected option's information as follows:

1. **Result:** concrete purpose or improvement, followed by resulting item name.
2. **Compared with yours:** relevant capability and stats against equipped gear or an explicitly named owned comparison target.
3. **Time:** effective duration in the current condition, with a short reason when increased.
4. **Costs:** consumed resources and exact required tools/wear, distinct from retained tools.
5. **Availability:** every blocker and an actionable next step.
6. **Action:** a specific verb such as Make knife, Start project, or Mend boots.

Comparison rules:

- Compare equipment with the item in its slot; compare tools with an owned item serving the same purpose, preferring the equipped applicable item. Name the comparison item and support choosing another if ambiguous.
- Show only relevant values: cutting capability and condition/lifespan for knives; warmth/coverage for clothing; capacity and weight for bags; ignition behavior for fire kits; protection or function for camp features.
- Separate current condition from the maximum capability of the design. An upgrade comparison must not hide that an existing tool merely needs repair.
- If nothing comparable is owned, describe the new capability. Avoid a synthetic zero-stat item.
- Use gameplay-owned values and formulas. Avoid copied balance numbers in UI metadata and avoid precise promises where outcomes depend on weather or other conditions.
- Describe consumable charges, tool wear, and clothing condition accurately rather than labeling every durability value as uses.

Projects retain their current execution model. Preview setup and remaining construction work separately, including known work-rate modifiers. Show a total estimate when calculable and clearly distinguish passive waits from active work. This disclosure does not require converting every long recipe into a resumable project.

For missing inputs, show the full shortage. Link to an existing recipe that produces a missing tool/material when available. Otherwise use known discovery/location information if reliable, or plain acquisition guidance; do not invent map locations. Optional intermediate crafting returns to the original selection and must not silently queue work.

### Acceptance

- Existing shelter, unsuitable snow-shelter weather, fully repaired clothing, missing tools, broken tools, and infinite tools produce correct, consistent statuses at display and execution.
- The displayed duration matches the planned execution duration for the evaluated state. Changed conditions cause re-evaluation.
- A project cannot appear to take only its 15-minute setup when hours of additional work remain.
- Benefits and inputs can be understood before committing. No misleading universal uses label remains.

## Phase B: object families and progressive disclosure (proposal 1)

### Catalog structure

Add stable recipe and family identifiers as catalog metadata. Keep existing concrete recipes as executable variants; grouping does not require generic material roles, substitutions, or component inventory.

Replace the eleven-button strip with a small navigation list: **Tools, Fire & light, Food gathering, Clothing & carrying, Camp, Supplies**. Repairs appear with their relevant family and in a Repair filter. Names can be adjusted after fitting the actual overlay, but Fishing must be reachable and every existing recipe must have a home.

Initial family mapping:

| Section | Families / operations |
|---|---|
| Tools | Cutting tool, axe, shovel, knapping stone, needle |
| Fire & light | Friction fire kit, spark fire kit, prepared tinder, torch, ember carrier |
| Food gathering | Spear, snare, fishing rod, fishing net |
| Clothing & carrying | Gloves, headwear, body covering, leggings, footwear, belt, pouch, pack |
| Camp | Shelter frame, portable tent, bedding, curing rack, fire pit |
| Supplies | Hide preparation, fat rendering, cordage preparation, teas, bandages/poultices |

Within a family, reveal the actual choices only after selection. Do not merge recipes that enable different activities simply because their tool enum matches. In particular, the current Tinder Bundle/FireStriker modeling inconsistency must not determine its family.

Cutting-tool example:

```text
Tools > Cutting tool
  Make
    Quick edge       Crude Edge / Sharp Rock
    Handled knife    Stone / Bone / Shale / Flint
  Improve            Added in Phase C when an eligible tool is owned
  Maintain           Added in Phase C when an eligible tool is worn
```

Show a compact comparison of available methods/variants: time, primary benefit, and shortage. Pick a deterministic default using available inputs; do not label it universally best. Keep the player's selection stable while inventory changes.

### Visibility

- Default lists show families, not all variant rows. Ready, Missing inputs, and Needs conditions reflect the shared evaluation.
- Keep ordinary family purposes and a curated set of major aspirations visible before every ingredient is found: warmer clothing, portable shelter, substantial carrying capacity.
- Recipe knowledge is distinct from possession. Basic plans can be inspected when unavailable; discovery highlights newly available possibilities rather than making the player decode `???` entries.
- Keep undiscovered material-specific variants collapsed behind an explicit browse action. Preserve resource discovery elsewhere and do not expose unknown map locations.
- Provide search across recipe names and purposes, plus Ready and All filters. A search result selects the appropriate family and variant.
- Use one source of navigation names, family membership, and statuses. Remove duplicated display-name dictionaries.

### Acceptance

- Each existing recipe is reachable through its family or a relevant operation; none are silently dropped. Fishing is included.
- Opening Tools shows one cutting-tool family instead of six peer knife/edge recipes.
- A player can choose a cutting tool, inspect its tradeoffs, and craft without visiting unrelated sections.
- Missing resources do not bury the selected plan or imply that one ingredient is the only blocker.
- The layout remains usable at the supported minimum window size, with scrolling and keyboard navigation consistent with other overlays.

## Phase C: improvise, improve, maintain (proposal 3)

### First playable slice: cutting tools

Keep one concrete lifecycle rather than introducing a universal component simulator:

| Action | Input | Outcome and decision |
|---|---|---|
| Improvise | Existing quick-edge recipe inputs | Obtain a short-lived cutting capability quickly |
| Make | Existing handled-knife recipe inputs | Invest in a complete durable tool |
| Improve: add handle | An eligible owned edge, a stick, and the recipe's binding | Convert that tool into its defined handled counterpart without paying for another edge |
| Maintain: sharpen | A worn, serviceable eligible knife and suitable working tool | Restore a bounded amount of condition for time and working-tool wear |
| Refit: replace edge | An eligible handled knife and compatible replacement edge material | Preserve the handle; restore or change the cutting portion without crafting a second complete knife |

Author explicit transitions between supported designs. No free-form assembly or arbitrary cross-material transformation. Improvisation remains worthwhile when time is short; improvements should not require paying the entire from-scratch cost again.

Lifecycle decisions to implement:

- Add durable design identity to eligible gear; runtime operations also identify the exact owned target instance. Do not target by display name or simply the first knife in inventory.
- Add Make / Improve / Maintain actions within the family using the same evaluation and comparison UI.
- Adding a handle preserves the edge's remaining condition proportion instead of granting a free new edge. Preview the resulting durability and weight explicitly.
- Initial sharpening policy: restore condition up to a serviceable cap below new condition; it cannot resurrect a shattered edge. Prototype an 80% cap, marked as a tuning value. This keeps replacement relevant without tracking another wear bar.
- Replacing an edge restores that portion to new condition and costs new edge material plus labor; show which existing parts are retained. It is more expensive than routine sharpening but cheaper in materials than a complete replacement.
- Retain broken supported handled tools as unusable repair targets rather than losing the handle automatically. Keep consumed treatments and other disposable items out of this retention rule. Audit all breakage/removal paths before enabling it.
- Transform the selected item once, maintaining equipment references and relevant persistent state. If immutable gear properties require replacement objects, replace the selected slot/list entry deliberately and preserve instance identity.
- Do not turn a broken repair target into a usable required tool. Prevent self-maintenance from satisfying a separate working-tool requirement with the target itself unless explicitly allowed.

### Durability and pacing

Measure an early-game sequence first: make an edge, prepare a fire kit, make a spear, and perform ordinary resource processing. Record tool replacements and time spent maintaining.

Then tune cutting-tool lifespans so a handled knife covers several useful tasks and maintenance is occasional preparation. Replace the uniform one-use crafting charge for scoped tools with a small authored wear schedule based on task demands. Avoid a blanket duration multiplier across all tools: elapsed time alone does not describe abrasion.

Preview exact wear, including accumulated fractional wear if that is needed. Keep infinite durability and other existing item lifecycles working. Expand wear changes beyond the slice only when measured pacing supports them.

### Extend after the slice works

- Convert current clothing mending to explicit owned-item targeting and accurate before/after previews, keeping its existing effect initially.
- Extend named improvements/refits to spears where the retained shaft creates a clear saving.
- Add other maintenance operations only when the target offers a meaningful repair-versus-replacement decision. Do not add maintenance recipes to every item for completeness.

### Persistence and acceptance

- Existing saves load. Recognized legacy crafted designs can receive a safe mapped design ID; unknown/custom gear remains usable without speculative upgrades.
- Save/load preserves design identity, condition, fractional wear if introduced, and broken repair targets.
- With two knives, only the selected knife changes. Equipped upgrades remain equipped, and a failed action neither consumes the original nor duplicates an output.
- A player can carry an improvised edge forward into a handled tool, maintain it, and eventually refit it.
- Free repair loops, full-health sharpening, repeated-handle upgrades, and unlimited resource refunds are impossible.
- In playtesting, making a new tool, maintaining an old one, and deferring work each have plausible survival contexts. Maintenance should occur before or between expeditions, not after every ordinary action.

## Delivery and verification

1. Evaluation and truthful previews: focused prerequisite, duration, project-cost, and input-consistency tests.
2. Family interface: recipe reachability checks plus manual layout/navigation and discovery-state checks.
3. Cutting-tool lifecycle: exact-target transformation, failure behavior, breakage, and save-round-trip tests; opening-days playtest.
4. Proven extensions: clothing repair and spear improvements; update current crafting documentation.

Run relevant existing crafting/action, inventory, and persistence checks as their behavior changes. Do not write snapshot tests for every presentation row. Run broader checks after integration where cross-system wear or breakage behavior changed.

Each of the first two deliveries should be independently usable. Phase C must not delay the catalog and preview improvements. Relative effort: A is small-to-medium, B medium, C largest because it touches gear identity, breakage, balance, and persistence. Calendar estimates require the lifecycle audit and first slice.

## Risks and boundaries

- Family grouping can conceal useful options: retain search, explicit variants, and a catalog coverage check.
- Comparisons can mislead: use actual mechanics and name the comparison item; show uncertainty where outcomes are contextual.
- Maintenance can add chores: keep few operations, long enough lifespans, and measure repeated interactions.
- Gear is shared with combat, NPCs, rewards, and saves: scope lifecycle behavior by supported design rather than globally changing every item.
- Existing unrelated working-tree changes must be preserved. Implement this plan against the current state without reverting other work.

See [tasks](crafting-overhaul-tasks.md), [context](crafting-overhaul-context.md), and [deferred proposals](deferred.md).
