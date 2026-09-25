# Phase 1 — UI foundations

Implemented 2026-09-07. Remaining screen-specific keyboard actions and stable HUD slots belong to later phases in the plan.

## Available now

| Control | Behavior |
|---|---|
| F1, or More / Help → UI size & keyboard | Open presentation settings |
| Compact / Comfortable / Large, then Apply size | Persist UI size independently of the game save; default Comfortable is 20% larger than the previous text scale |
| Tab / Shift+Tab | Navigate controls while in UI mode or a dialog |
| F6 | Enter/switch HUD panels: top bar, survivor, inspector/combat, events |
| Escape | Leave HUD keyboard mode or close a supported dialog; input fields/popups get first chance to handle it |
| Enter | Activate the focused native control |
| Ctrl/Cmd+F in Crafting | Focus recipe search |
| F4 in Foraging | Cycle focus choice; replaces the previous custom Tab behavior so Tab has one navigation meaning |

Existing exploration keys remain. Text input, modifiers, popups and active dialogs prevent exploration command routing. Newly active prompts wait for held keys to be released. Background modal controls are disabled. This establishes shared navigation, not complete direct shortcuts for every action.

Shared control descriptors carry identity, label, icon, shortcut, disabled reason and semantic tone. The UI-size dialog uses this renderer. Unavailable shared controls remain keyboard-focusable and explain why beside the control, but cannot execute. The existing HUD action model exposes the same metadata for its later renderer migration.

Shared sizing covers font scale, padding, spacing, scrollbars, bar heights and icon-button layout. Pixel icons use crisp 16px/32px sizes. Inventory category buttons wrap and crafting category widths account for the larger art. UI-scale changes apply between frames, avoiding mixed metrics inside a frame. Legacy custom rows and the full HUD redesign remain later work.

Colors now have shared normal/supporting/focus/selection/caution/danger/success roles. Keyboard focus stays visible immediately. Gameplay thresholds were not changed. People and Discoveries now correctly display Escape as their close key.

Seven PixelArtCli assets were added: warning, blocked, confirm, cancel, back, continue and text-size. The source/PNG pairs and control contact sheet were rendered and visually inspected. Some symbols are foundations for later screen migrations rather than already drawn everywhere.

## Verification

- Main application and preview builds checked with warnings treated as errors.
- Desktop/CLI regression run: 58 passed, one confirmed pre-existing failure (details below). Final focused desktop run: 39 passed; native navigation rechecked after test-file isolation.
- Input-owner release, exploration command precedence, modifier isolation, disabled-action eligibility and settings persistence/recovery tested.
- Native ImGui test exercises real Tab/Enter navigation and rejects unavailable actions; this caught and verified the fix for initially invisible focus.
- Production HUD/settings rendering checked at the three UI sizes.
- A pre-existing CLI storage-transfer test expects an entire stack while the current handler transfers one unit. The same failure was reproduced from an isolated unchanged HEAD checkout; no inventory behavior was changed here.
- Desktop automation could capture the running preview but did not reliably deliver input to its native window (`noWindowsAvailable`). Physical end-to-end keyboard play remains a manual verification item; native navigation tests are not represented as that walkthrough.

Use `tools/UiPreview` for an isolated interactive check. It does not execute gameplay actions or load game saves. Settings and input logs go to its own OS-temporary directory.
