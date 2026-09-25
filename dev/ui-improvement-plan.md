# Phased UI improvement plan

Status: Phases 1–5 implemented; see [implementation and verification notes](/Users/jackswingle/Documents/GitHub/text_survival/dev/ui-phase1-results.md). Phase 2: [HUD implementation and verification](ui-phase2-results.md). Phase 3: [keyboard implementation and verification](ui-phase3-results.md). Phase 4: [map/combat controls and verification limits](ui-phase4-results.md). Phase 5: [remaining coverage and verification](ui-phase5-results.md). Phase 6 remains planned; Phase 5 native screenshots are reviewed, and integrated acceptance remains pending.

Based on the [UI audit](/Users/jackswingle/Documents/GitHub/text_survival/dev/ui-accessibility-audit.md). The goal is comfortable reading, immediate recognition, stable spatial memory, and complete keyboard operation. Each phase should leave the game usable and independently reviewable.

## Rules for every phase

- Keep core stats and actions in fixed positions. Emphasize urgency in place with a border, tint and symbol; never reorder by urgency.
- Give every action button and meaningful information category an icon plus a text label. Repeated numeric fields can share a labeled category icon rather than duplicating decorative symbols on every number.
- Keep ordinary information calm. Separate category identity, keyboard focus, selection, unavailability, and warning severity visually.
- Preserve existing shortcuts where practical. Bindings must describe actual behavior and operate only in their intended input context.
- Support the mouse as well as the keyboard. Neither input method should reset the other's selection unexpectedly.
- Use the existing PixelArtCli pipeline for new pixel art. Reuse the 35 existing icons first.
- Do not change survival mechanics or underlying thresholds to make the UI look consistent. Map real gameplay states to consistent presentation; retain domain-specific meanings.

## Phase overview

| Phase | Priority | Deliverable | Completion check |
|---|---|---|---|
| 1. Shared foundations | High | Consistent input contexts, control metadata, sizing and semantic styles | One representative screen proves keyboard focus, icon, key hint, disabled reason and scaling together. |
| 2. Comfortable, stable HUD | High | Larger readable HUD, existing icons wired into actions, fixed core slots and in-place warnings | State changes do not relocate core controls; small and large window layouts remain usable. |
| 3. Complete everyday keyboard flows | High | Food, fire, rest, treatment, inventory, crafting and storage usable end to end | Complete the common survival loop without touching the mouse. |
| 4. Keyboard map inspection and combat | High | Inspect/target/move/act with stable combat controls | Explore a tile without moving, then finish a combat encounter without the mouse. |
| 5. Remaining screens and icon coverage | Med | Companions, discoveries, logs, work variants and utility controls brought up to the same standard | Every audit family has an explicit keyboard/icon/color disposition. |
| 6. Usability verification and tuning | High | Validated layouts, contrast, focus and icon recognition; updated audit | All high-priority gaps closed and remaining intentional exceptions documented. |

Phases are sequential by default. Icon batches are created alongside the phase that uses them; essential symbols do not wait until Phase 5. Testing occurs during each phase, with Phase 6 focused on integrated play and final tuning.

## Phase 1 — Shared foundations

1. Verify current ImGui keyboard navigation behavior in the running application and its integration. Establish explicit input contexts for exploration, combat, dialogs, text entry and popups. Only the active context handles a key.
2. Define a keyboard contract: Tab/Shift+Tab for focus traversal, arrows within lists/groups, Enter to activate the focused choice, Escape to return/cancel where allowed, a discoverable method for switching panels, and a search-focus shortcut. Resolve forage's custom Tab cycling deliberately. Do not globally intercept typing or let held/opening keys trigger a second action in a new dialog.
3. Extend shared action/control metadata with icon identity, optional shortcut, enabled reason and semantic state. Use it to render controls and route commands so the hint and implementation cannot drift.
4. Define shared typography, spacing, bar heights, button sizes, key-hint placement and panel sizing. Add persisted UI scale presets with a comfortable default candidate around 20–25% larger than today's text. Scale layout as a whole; map zoom remains separate. Choose crisp whole-pixel icon sizes such as 16 and 32 rather than blindly stretching pixel art.
5. Define shared colors for normal text, supporting text, focus, selection, disabled controls, caution, danger and success. Category icons carry identity; severity treatment carries urgency. Keep disabled reasons readable and accessible on keyboard focus.
6. Draft the minimum shared control icons with PixelArtCli: warning, blocked, confirm, cancel, back and continue. Reuse shapes where the meaning is identical.
7. Correct the misleading People/Discoveries close hints to the actual Escape behavior, unless deliberate toggle-close support is added with input-event protection.

Primary code: Core/Program, HotkeyRegistry, MapInputRouter, HudAction, HudWidgets, GameGui, UiIcons, OverlaySizes, DesktopUi. New settings/styles/input helpers should be small and shared rather than repeated per screen.

**Done when:** a representative dialog can be opened, navigated, activated and closed with the keyboard; typed letters never trigger game actions; focus is visible; disabled explanations work without hovering; scale changes resize both text and controls. Add focused tests for input precedence and command eligibility, not tests that merely repeat styling constants.

## Phase 2 — Comfortable, stable HUD

1. Apply shared sizing to the top bar, survivor panel, inspector and event log. Increase reading space around vitals and action rows. Size columns for labels, icons, values and shortcuts without forcing excessive wrapping.
2. Wire existing art into navigation, personal, fire, shelter, storage, wait and resource buttons. Give clock/weather/location headings visual identity. Add only missing person/location symbols needed by this phase.
3. Give core action families permanent slots: food/water, treatment, fire, rest/camp, storage, people and wait. Keep unavailable core actions visible with a concise reason. Start/tend and sleep/make-camp remain predictable family slots with clear labels.
4. Keep contextual work in a stable ordered section; do not fill the screen with every nonexistent world interaction. If rows become unavailable during an open interaction, retain selection by action identity and move focus predictably only when necessary.
5. Replace the shifting priority-summary behavior with warnings on their corresponding fixed vital rows. Give other conditions a stable section and stable identity/order, rather than sorting injuries by severity. Do not add a new urgency-ranked panel.
6. Make worsening temperature readable, unify warning presentation between summary/details, and distinguish imminent danger from ordinary fire/warmth colors. Use stable borders/badges first. Any later motion should be a brief transition cue, optional and disabled by a reduced-motion setting; avoid continuous flashing.
7. Reserve feedback space so temporary messages do not push controls around. Retain the event log's existing respect for the reader's scroll position.

**Done when:** fresh camp, developed camp, multiple injuries, low vitals, disabled actions and selected destinations are readable at 1280×720 and the user's normal window size. Core action positions do not change when warnings appear/disappear. Larger scaling offers usable scrolling where necessary without clipping controls or collapsing the map to an impractical width. Use HudPreview for these states and inspect its actual images.

## Phase 3 — Complete everyday keyboard flows

1. Upgrade shared Select, Choose, Confirm and ReadInt dialogs first: deterministic initial focus, visible shortcuts, keyboard selection, explicit quantity input, Enter activation and Escape cancellation only when supported. For consequential choices, focus a safe option and require deliberate activation; do not let the key that opened a prompt answer it.
2. Add unambiguous opening shortcuts for food/water, rest and treatment. Choose the exact keys against the complete context binding map from Phase 1, avoiding existing exploration shortcuts. Show them consistently in controls/help.
3. Food: keyboard item selection and Eat/Drink/Wash/Cook/Melt actions; preserve selection after consumption where possible. Show results in reserved space.
4. Fire: keyboard tab/tool/tinder selection, start, fuel addition, charcoal/torch/ember actions and exit. Support repeated refueling without losing focus.
5. Inventory: keyboard categories/items/details; equip, unequip and drop actions with stable verb icons. Keep selection tied to item identity when inventory changes.
6. Crafting: search focus, category and recipe selection, ready filter, prerequisite/back navigation, comparison selector and craft/repair/improve activation. Preserve search and sensible focus through results.
7. Transfer: keyboard source/destination switching, item selection, amount selection and transfer. Make direction explicit; selecting a row with navigation must not itself transfer an item.
8. Finish forage's keyboard flow, including Keep Walking. Preserve its efficient focus/time/search behavior while making it conform to shared navigation rules.
9. Add action icons to execution and exit buttons throughout these screens. Draft transfer/drop/equip symbols as needed; use existing food/fire/tool icons for domain actions.

**Done when:** starting at the HUD, a player can eat, drink, tend a fire, sleep, treat an injury, equip a weapon, craft an item and transfer supplies using only the keyboard, including all choices/results and returning to the HUD. Test empty lists, unavailable requirements, depleted stacks and nested dialogs. Record key counts and unexpected focus jumps; every flow must avoid mandatory pointer travel.

## Phase 4 — Keyboard map inspection and combat

1. Add a keyboard inspection mode with a clearly marked tile cursor. Inspecting must not spend time or move the player. Escape returns predictably to normal exploration.
2. Add selected-destination confirmation and keyboard choice of careful/quick crossings with icon, time and risk indicators. Preserve ordinary WASD travel and camera pan/zoom.
3. Establish combat-specific bindings for tactical movement/selection, target cycling, attack, defense, throwing, intimidate, calls, assess, wait and flee. Show the active keys; do not imply exploration bindings also work in combat.
4. Keep core combat action slots fixed across distance/awareness changes. Show unavailability rather than moving Attack/Block/Dodge/Flee around. Put variable throwable choices in a navigable sublist with stable identities.
5. Separate inspected target, selected target and player position with clear visual markers. Provide keyboard access to information currently dependent on hovering units.
6. Draft a coherent PixelArtCli combat set: block, dodge, shove, throw, intimidate, flee, call-help and call-retreat. Reuse attack weapon icons. Add target/inspection symbols and improve tiny target-injury bars.

**Done when:** keyboard-only play covers inspection without travel, safe and hazardous crossings, stalking, multiple targets, melee, ranged actions, unavailable actions and fleeing. No command leaks between exploration, combat and prompts. Verify command routing and eligibility with focused tests and confirm the actual visual flow in-game.

## Phase 5 — Remaining coverage and icon completion

1. Companions: keyboard selection, invite/dismiss, memories, give/ask, inventory and closing; reuse vital/item icons and add person/follow symbols. Surface urgent needs with the same severity conventions as the player.
2. Discoveries: category navigation, scroll access, category icons and correct close hints. Reduce repetitive placeholder clutter without hiding discovery counts.
3. Event history: keyboard expand/collapse, scroll and latest/unread controls. Ensure keyboard reading does not accidentally resume auto-follow. Add event-category symbols alongside colors.
4. Events/results: icons for outcomes, costs and choices where meaningful; shared numbered/focused selection behavior; readable consequences.
5. Contextual work: map every action family from the audit to a recognizable icon and keyboard route, including nets, traps, ice cutting, salvage, burial, curing and projects. Direct global keys are not required for every rare action, but keyboard navigation is.
6. Remaining information categories: blood, capacities, effects, tracks, shelter/bedding, decay and weather. Use distinct symbols where category fallbacks are confusing. Add low-priority help/sound/restart/recenter symbols.
7. Update the icon mapping documentation to match actual rendering. Every new .pxa must render to PNG, have its enlarged preview inspected, and be reviewed in a contact sheet and at actual UI size.

**Done when:** every action has an icon plus label, every meaningful information group has an icon, every interactive element has a keyboard route, and the audit explicitly records any intentional exception. No missing texture silently turns a planned icon into an unnoticed text-only control.

## Phase 6 — Integrated verification and tuning

1. Run a representative session entirely with the keyboard; repeat selected tasks with the mouse to catch regressions. Include sustained ordinary play rather than only isolated dialogs.
2. Review actual-size captures at 1280×720, normal desktop size and each supported UI scale, including Retina output. Verify focus visibility, truncation, wrapping, scrolling and map usability.
3. Measure text/background contrast for the final palette, especially secondary/disabled text, text on progress bars and selected controls. Verify states remain distinguishable without color alone.
4. Review stable positioning during warnings, healing, inventory depletion, craft filtering, changing combat range and temporary feedback. Explicitly test log-reading behavior with new events.
5. Verify icon silhouettes at their actual display size, not just enlarged contact sheets. Replace ambiguous icons and remove unnecessary repeated decoration.
6. Ask for user feedback on comfort after a real play session and tune scale/spacing/emphasis. This is the final subjective check on whether the fatigue problem improved; source review cannot establish it.
7. Update each audit row to Closed, Intentional exception, or Remaining with an explicit reason and priority. Update controls documentation and the icon README. Run relevant existing UI/game tests and the build; broaden only if failures or changed behavior warrant it.

**Done when:** all high-importance audit gaps are closed, no ordinary flow requires a mouse, core spatial layout stays predictable, icons and colors communicate consistently, and any remaining limitation is explicit. No blanket claim of accessibility or fatigue elimination based only on automated checks.

## Suggested implementation slices

Keep reviewable changes small within each phase: shared input and controls; settings/sizing; stable HUD; shared dialogs; food/fire; inventory/transfer; crafting/forage; map inspection; combat; remaining screens; final tuning. Bundle each icon batch with the controls that use it.
