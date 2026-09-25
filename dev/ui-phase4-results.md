# Phase 4 — Map inspection and combat keyboard controls

Implemented the agreed default bindings. Full in-game visual acceptance remains pending: the native screenshot runner failed at startup because GLFW could not find a monitor. No player saves or UI settings were loaded or changed by the preview.

## Map

| Key | Behavior |
| --- | --- |
| WASD | Existing immediate world travel |
| X | Enter inspection at the player's tile |
| WASD in inspection | Move the inspection cursor across explored tiles; no travel or time cost |
| Enter in inspection | Open destination confirmation, with current travel time and crossing risk |
| Esc | Close confirmation first; then leave inspection |
| Arrows / G / Ctrl or Cmd +/- | Existing camera pan, player recenter, and zoom |

Confirmation waits for the opening Enter key to be released, then focuses Cancel. Tab/Shift+Tab and Enter choose Travel, or the fixed Careful / Quick / Cancel options for hazardous crossings. The care/quick indicators use shield/warning art. Camera panning brings an inspection cursor back into view if it crosses the viewport boundary. Unexplored tiles and concealed caves retain their existing information restrictions.

## Combat and stalking

| Key | Behavior |
| --- | --- |
| WASD, then Enter | Preview a destination, then commit one movement action |
| Q / E | Cycle enemy targets in stable roster order |
| X | Inspect units/cells without spending a turn |
| F / R / C / V | Attack / block / dodge / shove |
| T | Open weapon/stone choices, with quantity and hit chance; Tab/Enter select |
| Space / Z / B | Wait / assess / intimidate |
| H / J | Call for help / call for retreat |
| Shift+F | Flee, when near an edge |
| Esc | Cancel inspection, movement preview, or a popup; never flee |
| Tab / Shift+Tab / F6 | Existing control navigation and HUD panel switching |

Attack, block, dodge and flee lead a fixed, scrollable action list. Other action slots retain their IDs and positions across range/awareness changes. Unavailable controls stay focusable with an explanation. Status, actions and target details occupy separate fixed-height regions so feedback and target information do not reposition the actions.

The gold circle marks the selected target; a blue square marks inspection or a movement preview; the existing player sprite identifies the player. Inspection does not change the attack target. Q/E and clicking an enemy explicitly change it. Attack and throwing commands now carry the selected enemy through to execution; stale/dead/departed targets are rejected. Older callers that omit a target retain nearest-enemy targeting. Defense and intimidation availability still consider the nearest threat, independently of the selected attack target.

UI and execution share the same action availability rules. Movement previews reject occupied cells, the current cell, and destinations beyond the player's allowance. Existing mouse movement remains available. Waiting during an engaged fight takes one normal combat action; stalking retains the existing longer watch-and-wait behavior.

Target injury bars now have separate body-part labels, readable height, dark fills behind text, and stable alphabetical ordering rather than severity sorting.

## Art

Ten new 16×16 PixelArtCli sprites: block, dodge, shove, throw, intimidate, flee, call-help, call-retreat, inspect and target. All pass CONNECTED checks. Reviewed the 6× contact sheet and 32px display-size sheet. Attack reuses the equipped weapon's icon; unarmed attack uses the hand symbol.

[Combat icon sheet](../assets/previews/ui-phase4-icons.png)

## Verification

Native ImGui input tests cover target cycling and attack recipient, movement preview/commit/cancel, inspection without actions, unavailable attack, dialog/release gating, Shift+F versus Esc, throwable popup navigation, and safe/hazardous travel confirmations defaulting to Cancel after key release. State/capture tests cover removed targets, occupied cells, fixed action identities, and exploration without time or movement.

The regression command includes Desktop, GameCli, and Combat tests:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --no-restore --filter 'FullyQualifiedName~Desktop|FullyQualifiedName~Cli.GameCliTests|FullyQualifiedName~Combat' -m:1 -p:UseSharedCompilation=false
```

The combined suite passed **110 tests**. A subsequent focused run passed all **12 Phase 4 tests** after the final targeting/availability changes. The production HUD preview project builds with zero warnings and errors; `git diff --check` is clean.

The production HUD preview includes combat at all three scales at 1280×720. Its executable builds, but rendering cannot proceed without an available monitor. Actual-size full-screen review and a sustained physical-keyboard play session remain Phase 6 acceptance work; isolated input tests do not establish reduced fatigue.
