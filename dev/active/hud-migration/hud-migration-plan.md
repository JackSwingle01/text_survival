# HUD migration plan

Status: implemented, 2026-09-06. See verification notes below for completed checks and the remaining manual interaction pass.

## Intended result

Make the map readable throughout a complicated run. Give personal condition, location interactions, navigation, and event history predictable homes. Preserve the pixel-art presentation and existing gameplay rules.

The reference mock is `hud-mock.png` beside this document. It illustrates layout and hierarchy; its values are examples. Its dotted route is not a commitment to multi-tile pathfinding: the first release previews only legal adjacent crossings.

| Region | Responsibility |
| --- | --- |
| Top strip | Time, compact weather, Inventory, Crafting, Discoveries, Help; retain NPC access, music and camera-follow controls in compact utilities |
| Left rail | Pinned vitals, temperature/trend, burden and concise danger summary; scrollable injuries, effects, threats, clothing and thermal detail below |
| Center | Clipped map, persistent selection outline, adjacent crossing indicator |
| Right rail | Current-location information and grouped actions, or selected-destination information and travel controls; separate combat presentation |
| Below map | Two or three recent events, expandable retained history; bounded by the center column |

## Findings in the current implementation

- `StatsPanel.Render` creates a 280-wide auto-height window and invokes `JournalPanel.Render` itself. Weather, body state and camp state share one long stream. Active effects are limited with `Take(6)`.
- `JournalPanel` independently reserves a 280 by 170 rectangle at the bottom left, reads only eight entries, and calls `SetScrollHereY(1.0f)` every frame. `NarrativeLog` already retains up to 200 entries.
- `ActionPanel` mixes location actions, personal menus, combat, availability queries and presentation. It requires `WorldRenderer` for layout and combat hover state.
- `TilePopup` owns selected coordinates, a cached location/crossing preview, map-relative window positioning and feature rendering. `WorldRenderer` also owns selection state.
- `DesktopUi` owns the useful single frame/modal composition but also map input, hotkey action selection and popup string-to-action conversion. Button and hotkey eligibility differ: fire and forage are examples.
- `Camera.ConfigureForScreenSize` knows sidebar widths; `GetRightPanelX` makes panel placement depend on the square grid. Its 60-pixel minimum tile size can exceed small available rectangles. `WorldRenderer` refreshes layout only on window-size changes, which will miss history expansion.
- `GameDisplay` already sends routine narrative to both the persistent log and transient toast feed. Showing both in the new event strip would duplicate messages.
- HUD renders after stacked prompts today. Map keyboard input checks `isTop`, but map-panel rendering/returned clicks are not uniformly gated. New persistent clickable controls need explicit routing and modal ordering.

## Component boundaries

Use small concrete components; introduce interfaces only where an actual second implementation or testing boundary needs one.

| Component | Owns | Does not own |
| --- | --- | --- |
| `HudLayout` | Pure calculation of region rectangles from logical display size, measured UI scale, and history state | Raylib calls, game state |
| `HudState` | Selected tile identity, section expansion, history follow/unread state | Cached mutable locations, survival state, serialized game data |
| `HudController` | HUD composition, per-frame interaction permissions and conversion of HUD intents to existing action results | Simulation execution or scheduler pumping |
| `SurvivorPanel`, `TopBar`, `LocationInspector`, `EventLogPanel`, `CombatPanel` | Rendering their assigned rectangles | Screen positioning policy or executing gameplay |
| Small presentation builders | Survivor summary, location sections, travel preview and action descriptors | Reimplementing temperature, travel or work formulas |
| `MapInputRouter` | Raylib input translated into selection, camera or action intents, respecting capture and focus | Drawing controls |
| Shared HUD widgets/style | Stat rows, section headings, navigation/action buttons, warning colors, duration formatting | A general UI framework |

Presentation records should contain only fields shared or needed for testing; avoid cloning the entire `GameContext` into a parallel model tree. Build from authoritative state each frame initially. Cache only if profiling justifies it.

Keep `DesktopUi` as the `IGameUi` adapter and owner of prompt completion. Keep `FrameScheduler`, `PlayerAction`, `CombatInput`, the handlers and work strategies. The HUD returns intent; the existing game loop executes it. A selected tile is an identity resolved against current state, not a stored `Location` snapshot.

## Interaction contract

- Click selects a known tile without advancing time; hover never replaces selection. Selection survives camera pan. Escape closes the top prompt first; on the map it clears selection and restores current-location inspection. G follows the player.
- Selecting the current tile restores current-location mode. After completed movement, reset inspection to the new current location. Opening/closing a personal menu preserves a still-valid selection; new/load resets transient HUD state.
- Destination mode labels its context explicitly. It displays only information allowed by visibility and offers adjacent legal travel. No remote camp actions, resource leakage from fog, or implied pathfinding.
- Use `TravelProcessor.PreviewCrossing` for time/risk. Refresh previews as state changes and revalidate when committing. Retain quick/careful choices and the existing WASD hazard-confirmation flow.
- Current-location groups: Fire, Shelter & Rest, Resources & Work, Storage & Processing, People when applicable. Assign every existing work strategy a group, with a visible Other Work fallback. Include tents, treatment access, cooking/food, racks, caches, tracks, carcasses, traps, fishing and new crafting projects; no action may disappear merely because it lacks a preferred category.
- Personal navigation stays fixed. Food & Water and treatment belong with survivor needs; camp cooking can link to the same existing food overlay. Avoid duplicating the implementation behind different entry points.
- Navigation uses subdued styling. Immediate world actions show known durations; configuration-opening actions use an ellipsis. Unknown or chosen-later durations must not be invented. Stable IDs are separate from labels, counts and hotkey text.
- Buttons and hotkeys resolve against the same action descriptors/availability source. Domain handlers retain final validation. Invalid hotkeys give a brief reason; relevant unavailable actions show a disabled reason where useful.
- During a blocking modal or active work/travel, visible HUD data stays live, but gameplay/navigation controls do not submit actions. Preserve existing cancel/progress semantics. Only the active input context can submit one result per frame.
- Scroll-wheel input over either rail or history never operates the map. Combat clicks also respect UI mouse capture. Render modal content above the HUD; avoid a persistent bar covering a dialog.

## Migration sequence

### 1. Establish shared layout and bounded regions

Add pure `HudLayout` calculation. Pass explicit rectangles to HUD panels and a map viewport to the camera/renderer. Replace sidebar offsets and camera-derived right-panel positions. Recompute when window size, font/UI scale or history height changes.

Keep the existing square seven-tile view initially, centered within the available map viewport; use the viewport for clipping and the actual grid rectangle for picking. Fit tile size to the available space rather than overflowing at the current minimum. A rectangular world camera is a separate future enhancement.

Move the journal beneath the map and bound both rails. Split fixed survivor summary from scrolling detail. This phase delivers overlap relief before deeper regrouping.

Acceptance: no panel/map intersection at 1280x720, 1600x900 and 1920x1080 at the current 1.25 font scale; map picking and camera follow remain correct after resize/history expansion. At compact sizes reduce padding and summary height, collapse history to one line, and move lower-priority top controls into a labeled More menu. Below the validated minimum, constrain window size rather than silently clip controls. Test increased font scale to establish its supported minimum too.

### 2. Extract action presentation and input routing

Introduce shared action descriptors containing stable ID, label, group, enabled/reason, shortcut, interaction kind and existing action payload. Reuse location work options and existing domain eligibility helpers; consolidate only the checks needed by this HUD.

Extract map input from `DesktopUi`; apply a shared interaction policy to mouse and keyboard routes. Remove stringly typed popup results at the desktop boundary by returning a typed travel intent mapped to existing `PlayerAction.Travel`.

Acceptance: mouse and keyboard agree about fire, forage, storage and unavailable actions. No actions leak through inventory, event or progress dialogs. One input produces at most one committed action.

### 3. Replace tile popup with the location inspector

Move tile features, tracks, NPC summaries and crossing presentation into `LocationInspector`. Put selection in `HudState` and pass it to map rendering; delete the second source of selection ownership. Build previews from current authoritative state.

Move local fire/shelter/storage information out of `StatsPanel`. Group current-location actions beside their information. Keep a small explicitly current-location Wait footer in destination mode, as in the mock. Extract combat UI from `ActionPanel` into `CombatPanel`, preserving tactical information and actions.

Acceptance: fresh camp, developed camp, adjacent safe/hazardous tile, blocked edge, impassable tile, distant known tile and remembered out-of-sight tile each have correct data and controls. Panning preserves selection; movement and new/load reset it appropriately. Every existing location action remains reachable.

### 4. Finish survivor rail and permanent navigation

Extract top-bar time/weather and global menus. Build reusable survivor rows, severity summaries and collapsible detail groups. Remove the six-effect display cap; use scrolling. Derive pinned warnings from live game state using existing severity semantics, not expired toast text. Include threats and capacity consequences without crowding the summary.

Move food/treatment entry points into the survivor rail and retain hotkeys/overlay behavior. Add compact help describing actual bindings through `HotkeyRegistry` plus movement/pan controls. Move music/follow toggles into the shared layout.

Acceptance: an injured, wet, overloaded character with many effects still exposes all vitals and critical warnings. Expand/collapse and scrolling do not move fixed controls. Changing button labels/counts preserves ImGui identity and focus.

### 5. Consolidate feedback and history

Render the log's retained entries in an expandable center-column history area. Auto-follow only when already at the bottom; otherwise preserve reading position and show a new-entry indicator/Jump to latest control. Handle wrapped messages and retention rollover without snapping the reader unexpectedly.

Audit `ToastFeed.Show` producers before removing the floating renderer. Routine `GameDisplay` messages already exist in `NarrativeLog`; do not append a second copy. Route toast-only/local feedback into the reserved feedback region or its relevant control. Persistent danger is derived from state and remains visible until resolved. Keep decision dialogs and work-result screens intact in this migration.

Acceptance: bursts of events do not cover the map or controls; routine messages appear once; warnings survive transient expiry when the condition persists. Opening history changes layout without resetting camera follow or selection. Timers still update exactly once per frame where retained.

### 6. Remove transitional code and validate the complete flow

Delete obsolete popup rendering, independent fixed panel coordinates, duplicate status formatting/selection state, and old toast presentation after callers have moved. Update frame-loop documentation to reflect new composition and input ownership. Keep commits in dependency order and the game buildable between phases.

Run targeted layout, input/selection, action-availability, log-follow and camera tests; retain scheduler/layering guard tests. Run the full existing suite once integration is complete and distinguish pre-existing failures from migration regressions. Visually verify actual ImGui output; pure tests cannot establish readability or clipping.

## Verification matrix

| Scenario | Must establish |
| --- | --- |
| Fresh game | All onboarding actions and help remain discoverable |
| Developed camp | Fire, shelter, tents, storage, racks and diverse work options remain reachable |
| Crowded survivor state | Full detail is scrollable, warnings and vitals remain pinned |
| Travel and visibility | Correct legal destinations, time/risk and fog restrictions; WASD parity |
| Modal/work/progress | No background action, live status still renders, cancellation is preserved |
| Combat/stalking | Target information/actions remain usable; HUD clicks never move a unit |
| Long log and event burst | Reader position persists; new entries are discoverable; no duplicate notification |
| Resize/font scale/history expansion | Valid layout and coordinate picking, no overlap or unreachable footer |
| Save/load/new run | Gameplay saves remain compatible; transient UI state is safely reset |

## Scope and integration risks

The worktree already contains ongoing crafting, food, inventory and rendering changes, including `DesktopUi` and `WorldRenderer`. Implement against those live changes and recheck their diffs before each extraction; do not revert or overwrite them. Include crafting-project work in the action inventory.

Keep this migration on Raylib/ImGui. Avoid a renderer replacement, general docking framework, application-wide event bus, broad inventory/crafting overlay rewrite, or scheduler rewrite. Revisit save persistence for HUD preferences only if requested; it is unnecessary for session-local expansion and selection.

The largest risks are coordinate-space consistency, modal input leakage, stale travel previews, and losing conditionally available actions. Each has an explicit acceptance gate above. The first release should establish these boundaries; aesthetic refinements can then be made one panel at a time.

User refinement: weather details open from the top weather summary; People has one visible entry in the location panel; Follow Player uses G (MacBook-friendly), defined in HotkeyRegistry.


## Implementation verification

- Main application and preview build cleanly with zero warnings/errors.
- Full suite: **542 passed** (530 existing plus 12 HUD cases).
- Final targeted HUD/camera/scheduler/layering pass after the terrain lifetime fix: **23 passed**.
- Production-renderer screenshots inspected: fresh camp, selected destination, eight active effects with expanded history, 1280x720 at 1.25 and 1.5 font scale, developed camp, combat, and a blocking confirmation dialog. Selected outputs are in `verification/`.
- The multi-view fixture caught shared terrain assets being unloaded with an individual world view. Their lifetime now ends at application shutdown, so a new DesktopUi instance can render safely.
- Repeatable fixture: `dotnet run --project tools/HudPreview/HudPreview.csproj -- /tmp/hud-preview`.
- Remaining manual verification: direct mouse/keyboard interactions for scrolling, modal input isolation and the G shortcut. Computer Use could capture the temporary native app but every click failed with `noWindowsAvailable`, including after access was restarted. Rendering and pure input-state tests passed; do not treat those as an end-to-end mouse test.
- No user save was loaded by the screenshot fixture. Temporary interactive checks used `/tmp/text-survival-hud-check`.
