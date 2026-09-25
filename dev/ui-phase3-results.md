# Phase 3 — Everyday keyboard flows

Implemented 2026-09-07. Native keyboard regression tests and shared CLI screen tests
verify the routes below. A physical keyboard play session and full-screen visual
review remain outstanding: HudPreview's graphical backend failed before rendering
with `GLFW: Failed to determine Monitor to center Window` (exit 139).

## Controls

| Screen / action | Keys | Behavior |
| --- | --- | --- |
| Food & water | E | Open from exploration; unavailable reason stays on its HUD action |
| Rest / make camp | B | Open the existing action for the current location |
| Treatment | H | Open treatment when available; existing eligibility unchanged |
| Existing openings | I / C / R / T / F | Inventory / crafting / fire / storage / forage |
| All dialogs | Tab / Shift+Tab, arrows, Enter | Traverse and deliberately activate native controls |
| Confirm | Enter on initial No; Tab then Enter for Yes | Safe initial answer; Esc means No |
| Select / Choose | Tab / arrows, Enter | Stable choice IDs; unavailable choices cannot execute |
| Quantity | Type amount, Enter or Tab then Enter | Explicit whole-number field, visible range, invalid values cannot submit |
| Food | F2 items; F3 actions | Select item then eat/drink/wash/cook/melt |
| Inventory | F2 categories; F3 items; F4 actions | Equip / unequip / drop; selection follows item identity |
| Crafting | Ctrl/Cmd+F search; F2 categories; F3 recipes; F4 craft | Native traversal also reaches ready filter, links and comparison selector |
| Fire | F2 tabs; F3 tools or fuel; F4 tinder | Stable IDs across quantity changes; repeat refueling with Enter |
| Transfer | F2 inventory; F3 storage; F4 amount | Select row, type amount, Tab then Enter to execute; direction shown explicitly |
| Forage | F4 focus; 1/2/3 duration; F search; K Keep Walking | Keep Walking returns the existing domain sentinel, matching the button |
| Exits | Esc or icon button | Popup/text-edit cancellation takes precedence; required choices do not invent cancellation |

Function-key region jumps work from text entry; ordinary typed letters still do not
become game actions. The existing modal release gate prevents an opening or held key
from answering the next prompt. Generic Select only offers Escape for an explicit
cancel/back/no tuple, and Choose for those explicit IDs. It does not guess from prose.

## Behavior and art

- Transfer rows only select. A separate button executes a revalidated amount, using
  the existing one-item domain operations. Desktop and CLI share the same execution
  method. A depleted or invalid selection cannot move supplies; selected direction
  remains visible. Survival mechanics and capacity policies are unchanged.
- Food selection is refreshed from current consumables and preserved across reopening
  when the item still exists. Missing items choose the first remaining item. Food and
  inventory reserve feedback space; action results still use the existing game result
  flow rather than adding a second gameplay loop.
- Inventory identity uses resource kind or gear InstanceId, independent of stack size
  and equip state. Crafting preserves search and filters through results and restores
  them when backing out of a prerequisite recipe.
- Fire tool/tinder identities survive changing quantities. Feedback has a reserved
  line; unavailable fuel remains focusable with its explanation.
- Initial focus is requested once after input release, not repeatedly while using the
  mouse or returning from a nested prompt. Region jumps intentionally focus the first
  control in that region. Depleted selections fall back predictably; urgency never
  reorders controls.
- Added transfer, drop, equip and unequip pixel art through PixelArtCli. All four pass
  CONNECTED validation; the contact sheet was reviewed at 6× and at 32px display size.
  Shared back/continue/confirm/cancel and domain icons cover execution and exit buttons.

## Verification

Build: zero warnings/errors, including HudPreview. Combined regression run: **66 passed**
(48 Desktop and 18 shared CLI tests). After final focus/hint adjustments, the
**48 Desktop tests passed again**. `git diff --check` is clean.

Native ImGui tests inject key press/release events into the production controls. They
cover safe confirmation, food selection/action/reopen, transfer selection versus
execution, one-Tab quantity traversal, repeated transfer and depletion, repeated fire
activation, and Keep Walking. They also cover the existing release gate, focus and
input-context contract. Shared CLI tests cover inventory actions, crafting readiness
and execution, food/fire results, and explicit transfer amounts in both directions.
The older CLI injury assertion was updated for Phase 2's combined name/severity row;
it still checks healthy versus destroyed tissue and the corresponding damage values.

Measured test sequences (each press counts once; HUD opening is separate):

| Route | Key presses | Observation |
| --- | --- | --- |
| Confirm No / Yes | 1 / 2 | Starts safely on No |
| Food first item -> action | 4 | F2, Enter, F3, Enter; no pointer required |
| Transfer storage item -> one unit | 5 | F3, Enter, F4, Tab, Enter |
| Repeat transfer | 1 | Enter; disabled after depletion |
| First / repeated refuel | 2 / 1 | F3, Enter; then Enter again |
| Keep Walking | 1 | K; same result as mouse button |

These are isolated native test sequences, not a claim of a completed physical-keyboard
survival session. Treatment, sleep and nested gameplay outcomes still need that live
integrated check. Full-screen screenshots at 1280×720 Large and 1600×900 Comfortable
are configured in HudPreview but could not be produced in this session. Phases 4–6
remain planned.
