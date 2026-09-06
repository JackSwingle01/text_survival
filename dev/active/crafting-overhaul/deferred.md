# Deferred crafting proposals

Recorded 2026-09-06. These ideas were positively received but are outside the selected first overhaul. They are not implementation prerequisites or rejected designs.

## Original proposal 2: material roles and substitutions

Express recipes using physical roles such as edge, shaft, binding, covering, and padding. Let compatible materials fill those roles, with a few readable differences in effort, durability, or performance. Disclose exact consumption and allow the player to protect scarce materials by choosing alternatives.

Examples: fiber or sinew for appropriate bindings; stone or flint for appropriate edges; willow bark converted to binding at the cost of medicine.

Revisit after family grouping and equipment improvements are stable. The current plan groups existing variants and authors explicit upgrade transitions; it does not implement a material-property framework. Avoid a combinatorial component inventory or distinctions with no visible consequence.

## Original proposal 4: contextual crafting entry points

Expose repair on worn equipment, hide preparation on raw hides, rendering/tea preparation at a fire, insulation improvements on a shelter, and construction at camp. Retain a central planning catalog and surface a few opportunities with explicit reasons.

Revisit once the shared evaluation and selection model can support multiple entry points. The selected plan adds actions within crafting families only; it does not redesign inventory, camp, fire, or treatment menus or build a recommendation engine.

## Original proposal 6: unified long work and batching

Make substantial gear and construction follow one start/pause/resume model with visible progress, committed materials, cancellation consequences, and total active work. Keep passive processes such as curing distinct. Batch repetitive material processing such as making several lengths of rope.

Revisit after inventory identity and work commitment semantics are established. Current scope only discloses existing project costs accurately; it does not turn all recipes into persistent projects or add a job queue.

## Related rule-consistency follow-up

Audit whether recipe requirements and outcomes match their descriptions: hide scraping tools, heat/water for rendered products and teas, bone-working requirements, and prepared tinder behaving as an ignition tool. Correct or simplify misleading recipes with attention to early-game access and progression.

Enforcing already-authored contextual prerequisites and fixing inaccurate UI claims are included in the selected plan. Introducing new resource dependencies and broad historical/balance changes are deferred. Do not display known inaccurate benefits while waiting for this audit; describe actual behavior or omit unsupported claims.
