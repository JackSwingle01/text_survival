# Phase 5 — Remaining keyboard and icon coverage

Implemented companion selection/actions, discovery navigation, deliberate keyboard history reading, numbered choices, structured results, contextual work icons and the remaining information symbols. This phase preserves spatial order; urgency changes styling in place.

## Controls and coverage

| Surface | Importance | Keyboard route | Icons / color | Notes |
| --- | --- | --- | --- | --- |
| Companion list | Medium | N opens; F2 list; Tab/Enter selects | Person; selection and focus outlines | Selection survives actions and reopening while the person is available |
| Invite / dismiss | Medium | F3, Enter | Follow / unfollow | Existing willingness and cooldown rules still apply |
| Memories / personality | Medium | Tab/Enter toggles | Journal / person | Long explanations wrap; personality starts collapsed |
| Give / ask | High | F4, Enter; Tab/Enter on resource actions | Give / ask plus resource art | Depleted rows remain available to focus, with an unavailable reason |
| Companion needs / condition | High | Read in the details region | Vital/effect icons; shared caution/danger styles | Warmth uses the player's 95°F/97°F thresholds; numeric vitals use the same 30%/50% boundaries |
| Companion inventory | Medium | F5, Enter | Backpack, category and gear icons | Native Tab navigation and scrolling remain available |
| Companion close | Medium | Esc | Back | Correct displayed key |
| Discovery categories | Medium | L opens; F2, Tab/Enter | Land, beasts, food, medicine, project | Selection persists; no automatic reordering |
| Discovery entries / counts | Low | F3; arrows, Page Up/Down, Home/End | Category heading | One remaining-count line replaces repeated question marks |
| Discovery close | Medium | Esc | Back | Correct displayed key |
| Event history | Medium | F6 to Events; F2 expand/collapse; F3 read; F4 latest | Journal / continue | Reading pauses following immediately; reaching the bottom does not resume following |
| History entries | Medium | Arrows, Page Up/Down, Home/End in reading region | Category symbols plus severity color | New entries increment unread while reading |
| Event choices | High | 1-9; Tab/arrows; Enter | Continue; disabled explanation | Unavailable choices and unreleased input cannot execute |
| Generic Select / Choose | High | 1-9; Tab/arrows; Enter; defined safe Esc exit | Continue / back | Existing safe initial focus remains |
| Event outcomes | High | Enter acknowledgment | Gains, loss, blood, effects, clock, warning | Consequences retain their descriptions and wrap |
| Work results | Medium | F3 reads long results; Enter continues | Work, warning, reward | Narrative, warnings and gains remain separate rows |
| Work progress | High | Existing acknowledgment / focus routes | Activity and section symbols | Bar scales with UI; supporting text has opaque readable tones |
| Contextual work | Medium | F6 to location; Tab/Enter | Explicit mapping for all 21 concrete strategy types | Nets, traps, salvage, burial, digging, ice cutting and projects no longer share a generic work icon |
| Curing / bedding / decay | Medium | Existing shared dialogs; read-only details | Curing rack, bedding, decay | No gameplay changes |
| Blood / capacities / effects | High | Read-only condition region | Separate category symbols | Detailed labels remain authoritative |
| Tracks and forage clues | Medium | Tab/Enter | Track / inspect | Track detail toggle and clue buttons replace hover/click-only detail paths |
| Help / sound / restart / recenter | Low | Tab/Enter in More / Help | Dedicated symbols | Existing command behavior preserved |

## Intentional exceptions

- Native text inputs, radio buttons, checkboxes, combo arrows, window-close marks and collapse triangles retain their standard ImGui control symbols. They do not get an extra decorative bitmap on every field.
- Prose, individual numbers, discovery names, personality values and each specific effect share their section/category icon. They retain text labels rather than assigning an ambiguous new pictogram to every sentence or condition.
- Data-driven choices use the shared continue/back symbol unless their screen has structured action metadata. Outcome strings use gain/loss/damage categories; no item identity is guessed from free-form text.
- Rare work actions have a complete Tab/Enter route; they do not each consume another global letter binding.
- Debug F12 remains a developer-only exception.

## Artwork and asset checks

Added 25 PixelArtCli sources and PNGs, bringing the UI set to 83. All were rendered and reviewed in the enlarged contact sheet and at 32px. Twenty-two use CONNECTED assertions. Sound waves, mute mark and recenter brackets intentionally contain disconnected components.

Every concrete work strategy has an explicit icon mapping. A test checks those mappings, resource icons, discovery category icons and literal shared icon references against existing PNGs. After textures are loaded, missing art draws a visible question-mark tile and logs the missing key once instead of silently becoming text-only.

[Icon sheet](../assets/previews/ui-phase5-icons.png)

## Verification

- Combined Desktop / GameCli regression suite: **85 passed**.
- Subsequent Desktop run after final clue/track changes: **67 passed**.
- Seven new native tests cover companion selection/invitation/dismissal, give/ask and depletion, discovery categories, event number eligibility/release gates, explicit history following, integer submission and asset coverage.
- Native preview builds with zero warnings/errors. Visual review covered production companion, discovery, event and result overlays at 1600×900 Comfortable and 1280×720 Large, with 2× Retina output, plus expanded history and the repaired quantity dialog. It caught and corrected a clipped companion explanation and an unsupported dash glyph.
- The first full preview run exposed an existing unsupported `EnterReturnsTrue` flag on ImGui integer input. The fix reads Enter after the normal integer control; a native regression test verifies submission without the assertion.
- Expanded asset-reference test: **1 passed**, including every resource and discovery category mapping. No missing-icon warnings appeared in the final native preview.
- `git diff --check` passes. Preview fixtures do not load or change player saves or persisted UI settings.

Representative captures: [companions](../assets/previews/ui-phase5-people.png), [discoveries](../assets/previews/ui-phase5-discoveries.png), [event](../assets/previews/ui-phase5-event.png), [result](../assets/previews/ui-phase5-result.png), [history](../assets/previews/ui-phase5-history.png).

Phase 6 still owns sustained physical-keyboard play, every scale/window combination, contrast measurements and subjective fatigue feedback. The baseline audit remains historical; the table above records Phase 5's implemented coverage and intentional exceptions rather than claiming that every original audit row has received final integrated acceptance.
