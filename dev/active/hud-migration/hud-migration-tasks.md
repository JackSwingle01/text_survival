# HUD migration checklist

- [x] Inspect HUD, camera, selection, input, action results and feedback flow.
- [x] Record component boundaries, phased migration and acceptance cases.
- [x] Preserve approved reference mock with the plan.
- [x] 1. Add shared layout; fit/clamp camera viewport; bound rails and reserve event strip.
- [x] 2. Share action descriptors/eligibility; extract input routing and enforce modal permissions.
- [x] 3. Replace popup with inspector; unify selection; group local actions and extract combat panel.
- [x] 4. Complete survivor summary/details, permanent navigation, help and utility controls.
- [x] 5. Implement retained history with follow/unread behavior; consolidate transient feedback.
- [x] 6. Remove obsolete code, update architecture docs, run targeted and full validation.
- [x] Render and inspect fresh/developed camp, crowded survivor state, destination selection, expanded history, compact/font-scaled layouts, combat and blocking dialog.
- [x] Run full suite (542 passed) and final targeted checks (23 passed).
- [ ] Complete manual mouse/keyboard interaction pass; Computer Use clicks could not target the temporary native app (`noWindowsAvailable`).

Each numbered item corresponds to the acceptance gate in the plan. Implementation is complete; the remaining manual interaction check is explicitly listed above.
