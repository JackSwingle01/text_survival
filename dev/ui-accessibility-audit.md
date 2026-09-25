> Phase 5 update: companion/discovery/history keyboard routes, numbered events, contextual work art and information symbols are implemented. [Current coverage table, intentional exceptions and verification](ui-phase5-results.md). The original tables below are the historical baseline; Phase 6 will finalize individual-row acceptance.

> Phase 4 implementation update: keyboard map inspection, destination confirmation, selected combat targets, movement previews and fixed combat controls are implemented. See [bindings and verification limits](ui-phase4-results.md).

> Phase 3 implementation update: everyday opening keys, focus jumps, typed quantities, safe confirmations and explicit transfers are implemented. See [keyboard verification and limitations](ui-phase3-results.md).

> Phase 2 implementation update: core HUD slots, HUD action icons, in-place vital warnings, stable injury ordering and reserved feedback are now implemented. See [verification and remaining scope](ui-phase2-results.md). The inventory tables below remain the original audit baseline.

# UI inventory and gap audit

Reviewed 2026-09-07 against the current desktop source. This is a code audit, not a live visual or keyboard usability test. Colors below assess intended meaning and consistency, not measured contrast. Importance is a design judgment: **High** = survival, frequent action, or required interaction; **Med** = useful contextual information/action; **Low** = occasional reference or administration. Context can raise importance.

**Keyboard:** a listed key is explicitly handled; **None** means no dedicated binding found; **Read-only** means the display itself needs no action. Opening a screen does not imply keyboard access to its contents. The application does not explicitly enable ImGui keyboard navigation in its startup code; generic Tab/arrow/Enter navigation is therefore **unverified**, not credited below. Forage has its own explicit Tab handler. **Icon** describes what the current renderer draws, not merely available artwork. **Color:** Good = appropriate semantic treatment in code; Partial = meaningful but inconsistent/incomplete; Neutral = default styling, acceptable for ordinary controls but offers little recognition; Gap = misleading or insufficient emphasis.

Repeated data-driven items are grouped by their shared renderer (resources, recipes, injuries, event choices, work variants). This inventories UI elements and action families, not every possible generated item name or narrative sentence. Preserve stable positions; recommendations mean emphasis in place, never sorting by urgency.

## Persistent HUD and map

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Day and time | High | Read-only | No | Neutral | Always visible; clock/sun/moon art exists but is not used here. |
| Weather summary / temperature button | High | None | No | Neutral | Compact layout hides condition label; use weather icon and stable temperature placement. |
| Weather details: season, condition, wind, precipitation, front | Med | None to open | No | Neutral | Popup requires discovery; reuse weather icons. |
| Inventory button | High | I | No | Neutral | Direct access is good; backpack art already exists. |
| Crafting button | High | C | No | Neutral | Direct access is good; gear art already exists. |
| Discoveries button | Med | L | No | Neutral | Journal art exists. |
| More / Help | Low | None | No | Neutral | Add recognizable help/menu icon and keyboard access. |
| Mute / unmute music | Low | None | No | Neutral | Hidden in More; needs sound/mute symbol. |
| Follow player toggle | Med | G | No button icon | Neutral | Keyboard access exists; follow state is shown in menu. |
| New game | Low | None | No | Neutral | Keep visually secondary; add restart symbol if all buttons receive icons. |
| Controls reference | Med | None to open | No | Neutral | Helpful list, but behind a mouse-opened menu. |
| Map movement | High | WASD | Player/map art | Terrain styling | Good direct movement; keypress based. |
| Camera pan | Med | Arrow keys | No control icon | N/A | Also middle drag; independent of movement. |
| Map zoom / reset | Med | Ctrl/Cmd + plus/minus; modifier + 0 | No control icon | N/A | Good keyboard alternative; this changes map zoom, not UI scale. |
| Inspect a tile without traveling | High | None | Map selection graphics | Selection treatment | Click-only selection path; needs a keyboard inspection cursor. |
| Clear tile selection / current location | Med | Esc | No button icon | Neutral | Consistent escape route. |
| Terrain, fog, trails, tracks, caves, features, animals | High | Inspection requires click | Map sprites / drawn graphics | Map-specific palette | Already visual; recognition/contrast still need live review at normal zoom. |
| Debug map reveal | Low | F12 | No | Feedback text | Developer control; not a player-facing design priority. |
| Recent event text and timestamps | Med | Read-only; no history navigation binding | No | Good | Success/warning/danger/discovery/system tones; add category symbols for recognition. |
| History / Collapse | Med | None | No | Neutral | Requires pointer; changes map/log height when explicitly requested. |
| Latest / unread count | Med | None | No | Neutral | Preserves reading position; button appears conditionally. Needs keyboard jump-to-latest. |
| Blocked-action / movement feedback | High | Triggered by attempted action | No | Partial | Amber text; long explanation may require hover. |

Sources: [TopBar](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/TopBar.cs), [MapInputRouter](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/Input/MapInputRouter.cs), [WorldRenderer](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/Rendering/WorldRenderer.cs), [EventLogPanel](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/EventLogPanel.cs), [HotkeyRegistry](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/Input/HotkeyRegistry.cs).

## Survivor panel

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Warmth / body temperature | High | Read-only | Yes: temperature | Partial | Green/amber/cold blue; cold danger does not use the red severity language of other vitals. Add warning shape/border in place. |
| Energy | High | Read-only | Yes: energy | Good | Threshold color and trend arrow; 18px bar is cramped. |
| Food | High | Read-only | Yes: food | Good | Threshold color and trend arrow; no direct shortcut to remedy. |
| Water | High | Read-only | Yes: water | Partial | Severity colors useful; healthy water is green here and blue in food screen. |
| Vitality | High | Read-only | Yes: vitality | Partial | Severity color good; arrow uses frame-to-frame changes, so may disappear quickly. |
| Temperature trend / feels-like summary | High | Read-only | No separate icon | Gap | Important worsening-temperature information is styled as disabled text. |
| Carry weight / capacity | High | Read-only | Yes: backpack | Partial | Color thresholds useful; red above 90% while Overloaded warning begins above 100%. |
| Warning summary: bleeding, cold, dehydration, starvation, exhaustion, overload | High | Read-only; full summary on hover | No | Partial | Always red even for Getting cold; shows first warning plus count. Order/visibility changes as conditions change. |
| Full warning list | High | No dedicated detail-scroll binding | No | Partial | All amber here, unlike red summary; variable-height list shifts later details. |
| Injuries section | High | Read-only | Heading only: bandage | Good | Severity labels plus colored damage bars. |
| Injured body parts / organs | High | Read-only | No per-part icon | Good | Explicit minor-to-critical text; sorts injured parts/organs by condition, so rows can move. |
| Blood | High | Read-only | No | Partial | Capacity colors useful; tiny bar and conditional visibility. |
| Moving / Manipulate / Conscious capacities | High | Read-only | No | Partial | Only appear below 90%; shifts layout and requires text recognition. |
| Active effects and severity/trends | High | Read-only; hover for details | Heading only: medicine | Partial | Beneficial/harmful severity colors; mild harmful effects can look green. |
| Effect details: capacity modifiers, damage, temperature, treatment, contributions | Med | Hover; no dedicated binding | No | Partial | Useful signed values/colors, but detailed explanations depend on hover. |
| Threats / last-seen animal / tensions | High | Read-only | Heading only: spear | Partial | Warning/severity tones; individual threat types need recognition cues. |
| Temperature & clothing expansion | Med | None | No | Neutral | Good progressive disclosure; needs keyboard focus/access. |
| Expanded body temperature / feels-like / clothing warmth | Med | Read-only | Yes | Partial | Clothing bar stays amber regardless of state; same concept has multiple color treatments. |
| Wind and precipitation details | Med | Read-only | Yes | Neutral | Gray supporting detail is reasonable; retain readable contrast. |
| Weather front / base temperature / location modifier | Low | Read-only | No | Neutral | Supporting numbers; icon can sit on their group. |
| Wind chill / sun / precipitation cooling / shelter / fire contributions | Med | Read-only | Yes | Good | Blue cooling and orange warming with signed values; effective local convention. |

Source: [SurvivorPanel](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/SurvivorPanel.cs), [SurvivorWarnings](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/SurvivorWarnings.cs).

## Location information and travel

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| You are here / selected destination / location name / terrain | High | Esc returns to current location | No | Partial | Heading separates context; missing location/selection symbol. |
| Ground/surface condition | Med | Read-only | No | Neutral | Blue-gray text only. |
| Fire phase / time remaining / embers / fuel bar | High | Read-only; R opens fire | No | Partial | Time-based warning colors and layered fuel bar; orange also represents normal fire. |
| Shelter insulation / wind block / bedding quality | High | Read-only | No | Neutral | Shelter icon exists; current values require reading. |
| Forage resources / depletion / exploration percentage | Med | Read-only; F opens forage where available | No | Partial | Depletion muted, fully explored green; no quick category recognition. |
| Small game description | Med | Read-only | No | Neutral | Add animal/trap category symbol. |
| Water source | High | Read-only | No | Neutral | Water icon exists but this line is plain text. |
| Traps active / catch ready | Med | Read-only | No | Good | Ready catch green; needs trap symbol and stable ready indicator. |
| Curing rack contents / ready state | Med | Read-only | No | Good | Ready state green; add rack identity. |
| Carcass / decay | Med | Read-only | No | Gap | Decay is only text; emphasize impending loss in place. |
| Cache weight / empty state | Med | Read-only | No | Neutral | Storage icon already available. |
| Tracks: maker, count, age, direction | Med | Read-only; detail on hover | No in inspector | Partial | Freshness controls brightness; oldest tracks become dim. |
| NPC name / action / needs / warnings | High | N opens People | No | Partial | Name accent and warning text; needs person/status icons. |
| Travel here | High | WASD is alternate direct travel; no selected-tile confirm key | No | Neutral | Click inspection and keyboard movement are separate flows. |
| Travel carefully / quickly, time and risk | High | No direct choice keys | No | Gap | Risk presented as text in otherwise similar buttons. |
| Blocked travel reason | High | Read-only | No | Neutral | Needs recognizable blocked/hazard symbol. |
| At your location footer | Med | Read-only | No | Neutral | Repeats current location; useful when inspecting elsewhere. |

Sources: [LocationInspector](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/LocationInspector.cs), [TravelInspection](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/TravelInspection.cs).

## Location action buttons

All these use the same text-only `HudWidgets.Action` renderer. Disabled controls dim and explain why on hover; a bound shortcut also surfaces the disabled reason in the log area. No action-specific icon or urgency highlight is drawn. Conditional actions and work availability can shift rows.

| Surfaced action | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Food & water | High | None | No | Neutral | Biggest everyday gap; existing food/water art can be reused. |
| Treat wounds | High | None | No | Neutral | Appears only with qualifying wounds; bandage art exists. |
| Start / Tend fire | High | R | No | Neutral | Shortcut good; label changes in same action family. Reuse fire. |
| Sleep / Make camp | High | None | No | Neutral | Frequent survival action; moon/shelter art exists. |
| Pitch tent / Pack tent | Med | None | No | Neutral | Reuse shelter; distinguish deploy/pack with a symbol. |
| Camp storage | High | T | No | Neutral | Reuse storage icon. |
| Curing rack | Med | None | No | Neutral | Ready state in label; needs visual ready indicator. |
| People | Med | N | No | Neutral | No dedicated person icon in UI set. |
| Wait 5 minutes | High | Space | No | Neutral | Fixed footer and direct key are good; clock art exists. |
| Forage | High | F | No | Neutral | Good opening key; basket art exists. |
| Harvest resources / wood work | High | None | No | Neutral | Reuse materials/gear/fuel; distinguish resource target. |
| Hunt | Med | None | No | Neutral | Spear reusable. |
| Megafauna scout / track / approach | Med | None | No | Neutral | Context can be dangerous; needs tracking/hazard distinction. |
| Fish | Med | None | No | Neutral | Fish art exists. |
| Set fishing net / Check net | Med | None | No | Neutral | Net art missing. |
| Cut ice | Med | None | No | Neutral | Ice-cutting art missing; water is a category fallback. |
| Trapping / snare work | Med | None | No | Neutral | Trap/snare art missing. |
| Examine environmental detail | Med | None | No | Neutral | Journal fallback; magnifier would distinguish inspection. |
| Improve shelter | Med | None | No | Neutral | Shelter art exists. |
| Cache / ground stash / dig up placed items | Med | None | No | Neutral | Storage fallback; dig/bury distinction missing. |
| Butcher carcass | Med | None | No | Neutral | Knife/meat art exists. |
| Continue crafting project | Med | None | No | Neutral | Gear fallback; progress/continue marker useful. |
| Salvage | Med | None | No | Neutral | Backpack/materials fallback. |
| Loot body / Bury body | Med | None | No | Neutral | Backpack fallback for loot; burial symbol missing. |

Sources: [HudAction](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/HudAction.cs), [HudWidgets](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/HudWidgets.cs), [Location](/Users/jackswingle/Documents/GitHub/text_survival/Environments/Location.cs), feature `GetWorkOptions` providers. TrailMarkingStrategy is recognized by grouping code; its availability is not established by that grouping alone.

## Inventory, food, storage, crafting, and fire screens

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Inventory category filters | High | No direct filter keys | Yes | Selection highlight | Good recognition; add predictable keyboard category selection. |
| Inventory item tiles, counts, weight, condition | High | No direct item navigation | Yes, family fallbacks | Good | Category backgrounds and condition bars; small 16px art, some truncated names need hover. |
| Inventory item details / stats / descriptions | Med | Read-only after selection | Not on each detail | Partial | Condition and food risks colored; many plain numeric lines. |
| Drop 1 / Drop All / Drop | Med | None | No | Neutral | Add drop symbol; distinguish quantities without shifting targets. |
| Equip as Weapon / Unequip Weapon | High | None | No | Neutral | Spear/gear art available; no keyboard completion path. |
| Unequip clothing / Remove accessory | Med | None | No | Neutral | Clothing/backpack art available. |
| Inventory Close | Med | Esc | No | Neutral | Label matches implemented escape behavior. |
| Food screen calories / hydration bars | High | Read-only | No | Partial | Threshold colors useful, but healthy hydration differs from HUD. |
| Food / water / fire-action groups and item selections | High | No direct selection keys | Yes, family fallbacks | Partial | Icons help; selection/detail/action still spans two columns. |
| Food item nutrition / raw risk / cooking information | High | Read-only after selection | No per-field icons | Partial | Warning text helps; add consistent risk markers. |
| Eat / Drink | High | None | No | Neutral | Core repeated actions lack both shortcuts and button icons. |
| Wash | Med | None | No | Neutral | Water fallback; needs distinction from drinking. |
| Cook / Melt snow | High | None | No on execute buttons | Neutral | Selection has art; final buttons do not. |
| Food action feedback | Med | Read-only | No | Good | Green success / orange warning; temporary message insertion shifts content. |
| Food Done | Med | Esc | No | Neutral | Good exit binding. |
| Transfer: player/storage headings and capacities | Med | Read-only | No | Partial | Player weight text warns; storage capacity treatment less expressive. |
| Transfer resource / tool / clothing / accessory rows | High | None | Yes, family fallbacks | Partial | Clicking performs transfer, rather than selecting for a separate explicit action. Need keyboard direction/quantity flow. |
| Transfer feedback / Done | Med | Esc closes | No | Neutral | Feedback is blue regardless of message; Done omits available Esc hint. |
| Crafting search | High | Typing after focus; no focus shortcut | No | Neutral | Good text search; needs keyboard focus on demand. |
| Crafting section buttons | High | None | Yes | Selection highlight | Existing icon pattern worth extending. |
| Ready only filter | Med | None | No | Neutral | Checkbox communicates state; keyboard access unverified. |
| Recipe list / compact recipe selector | High | None | Yes in recipe rows | Good | Ready green, icon by output/family; generic compact selector needs keyboard review. |
| Recipe details / materials / tools / blockers / warnings | High | Read-only after selection | Materials/tools/output icons | Partial | Clear missing/ready labels; Have count can be green when stock is positive but insufficient. |
| Craft / repair / improve execute button | High | None | No | Partial | Nearby readiness text and disabled state; button has no icon/key. |
| View prerequisite recipe / Back to your recipe | Med | None | No | Neutral | Useful dependency navigation but mouse-driven. |
| Compare with equipment selector / comparison detail | Med | None for selector | No per comparison control | Neutral | Reading-heavy before/after comparison. |
| Crafting Close | Med | Esc | No | Neutral | Correct hint. |
| Fire Start / Tend tabs | High | R opens screen; no direct tab key | No | Default tabs | Opening shortcut does not complete fire interaction. |
| Ember availability / Use Ember Carrier | High | None for action | Yes: ember | Partial | Availability orange and explicit 100% success; key still missing. |
| Fire tool and tinder selection | High | None | Section icons; no individual radio icons | Neutral | Clear groups but requires pointer selection. |
| Missing tool / tinder / kindling; kindling count | High | Read-only | Yes | Good warning treatment | Already combines text, symbol and warning color. |
| Fire success estimate / modifiers | High | Read-only | Not consistently | Partial | Percentage/details require reading; maintain clear risk explanation. |
| Attempt to Start Fire | High | None | Yes: fire | Button styling | Good icon; no direct confirmation shortcut. |
| Fire phase / temperature / remaining time / embers | High | Read-only | Yes | Partial | Strong domain identity; unify severity with main HUD. |
| Add each fuel type | High | None | Yes, distinct fuel icons | Button / disabled styling | Good icon coverage; repeated refueling still requires pointer. |
| Collect charcoal | Med | None | Yes: charcoal | Button styling | Art complete; keyboard missing. |
| Light torch | Med | None | Yes: torch | Button styling | Art complete; keyboard missing. |
| Collect ember | Med | None | Yes: ember | Button styling | Art complete; keyboard missing. |
| Close fire screen | Med | Esc | Window close control | Neutral | Binding implemented by parent screen. |

Sources: [InventoryOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/InventoryOverlay.cs), [FoodOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/FoodOverlay.cs), [TransferOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/TransferOverlay.cs), [CraftingOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/CraftingOverlay.cs), [FireOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/FireOverlay.cs), [DesktopUi](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/DesktopUi.cs).

## Foraging, companions, discoveries, events, and shared dialogs

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Forage quality / exploration / clues / warnings | Med | Clue-to-focus is click; Tab offers focus alternative | No | Partial | Useful warning colors; clickable clue behavior needs keyboard equivalent. |
| Forage focus choices | High | Tab cycles | Yes | Selection highlight | Best existing example of icon + explicit keyboard flow. |
| Forage duration choices | High | 1 / 2 / 3 (15 / 30 / 60 min) | No | Selection highlight | Good shortcut visibility; clock art exists. |
| Search | High | F | No | Button styling | Good repeatable keyboard flow; use foraging icon. |
| Forage Cancel | Med | Esc | No | Button styling | Clear key hint. |
| Keep Walking 5 min | Med | None | No | Button styling | Keyboard gap in otherwise strong screen. |
| Companion identity / opinion / personality / memories | Med | N opens; no direct expansion keys | No | Partial | Lots of text; person/relationship identity absent. |
| Come with me / Go your own way | Med | None | No | Neutral | Add following/dismissal symbol and keyboard action. |
| Give / Ask for supplies | High | None | No, including resource rows | Neutral | Repeated exchange is mouse-heavy; reuse item art and transfer arrows. |
| Companion current action / need / decision reason | High | Read-only | No | Gap | Much of this uses disabled gray despite survival relevance. |
| Companion warmth / energy / food / water / clothing bars | High | Read-only | No | Partial | Colored but differs from player thresholds; reuse vital icons. |
| Companion effects / activity log | Med | Read-only | No | Partial | Effects orange regardless of kind; log neutral. |
| Companion inventory / resources / gear condition | Med | None to expand | No | Partial | Condition/weight color good; player inventory icons not reused. |
| Companion Close | Med | Esc; displayed N does not have close handler here | No | Gap | Fix misleading Close [N] label or implement N-to-close. |
| Discovery category tabs | Med | L opens; no direct tab keys | No | Default tabs | Land/Beasts/Provisions/Medicine/Works lack category icons. |
| Discovery entries / counts / undiscovered placeholders | Low | No dedicated navigation | No | Neutral | Readable hierarchy but many ??? lines add clutter. |
| Discovery Close | Med | Esc; displayed L does not have close handler here | No | Gap | Fix misleading Close [L] label or implement L-to-close. |
| Event narrative / choice descriptions | High | Read-only | No | Partial | Descriptions muted; consequences must remain readable. |
| Event choice buttons | High | None | No | Neutral | No numbered choices or direct action keys. |
| Event costs / unavailable choices | High | Read-only | No | Partial | Costs amber, disabled choices dim; no symbolic cost/risk cues. |
| Event outcomes: gains / losses / damage / time | High | Read-only | No | Good | Semantic text colors and signs; item/damage/time art missing. |
| Event/message Continue | High | Enter | No | Neutral | Strong keyboard baseline; standardize continue icon. |
| Generic Select / Choose choices | High | None | No | Neutral | Shared mouse-dependent renderer affects many gameplay flows. |
| Generic Yes / No confirmation | High | None | No | Neutral | No explicit Enter/Esc or Y/N; prioritize safe predictable focus. |
| Quantity/duration integer slider, OK, optional Cancel | High | No dedicated confirm/cancel bindings | No | Neutral | Generic slider keyboard behavior unverified; no direct input/confirm flow established. |
| Work result message | Med | Enter for message continuation | Warning glyph only; no item icons | Neutral | Structured result text is flattened into message text. |
| Work progress / elapsed and total minutes | High | Read-only | No main progress icon | Neutral | 20px bar; label and duration good, sizing inconsistent. |
| Progress section lines / gathered items | Med | Read-only | Optional, supplied by data | Partial | Category tones and optional icons; muted/done text can be very dim. |
| Progress Continue | High | Enter | No | Neutral | Good binding; use same continue symbol across screens. |

Sources: [ForageOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/ForageOverlay.cs), [NPCOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/NPCOverlay.cs), [DiscoveryLogOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/DiscoveryLogOverlay.cs), [EventOverlay](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/EventOverlay.cs), [DesktopUi](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/DesktopUi.cs). Shared dialogs cover data-driven treatment, sleep/camp, curing, work choices, and other handlers using Select/Choose/Confirm/ReadInt.

## Combat and stalking

| Surfaced element | Importance | Keyboard now | Icon now | Color now | Good / bad / gap |
|---|---|---|---|---|---|
| Combat / stalking mode and distance zone | High | Read-only | No panel icon | Partial | Orange header; distance zone varies with danger. Add clear mode identity. |
| Target identity / awareness / detection risk / hints | High | Hover target; no target-cycle key | Map unit art; no panel icons | Good semantic tones | Hover determines inspected target; keyboard target selection missing. |
| Target vitality / boldness / threat / aggression | High | Read-only | No | Partial | Separate colored bars; meanings still require text. |
| Target movement / strength / injuries | Med | Read-only | No | Partial | Supporting text dim; injury bars only 12px high. |
| Tactical cell movement / target click | High | None in combat routing | Map unit/cell graphics | Map-specific | Exploration WASD handler is not used during combat. |
| Wait / Assess | High | None; exploration Space is not routed here | No | Neutral | Dedicated combat bindings missing. |
| Attack | High | None | No | Neutral | Spear/knife art reusable; needs stable combat slot/key. |
| Block / Dodge / Shove | High | None | No | Neutral | Dedicated defensive-action icons missing. |
| Throw weapon / Throw stone, hit chance and quantity | High | None | No | Neutral | Conditional choices shift list; use stable slots and clear unavailable states. |
| Intimidate | High | None | No | Neutral | Needs distinct symbol and direct key. |
| Call for help / Call for retreat | High | None | No | Neutral | Needs distinct call/help/retreat symbols and keys. |
| Flee action | High | None | No | Neutral | Dynamic label/action; urgent escape option needs stable placement. |

Sources: [CombatPanel](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/CombatPanel.cs), [HudController](/Users/jackswingle/Documents/GitHub/text_survival/Desktop/UI/HudController.cs).

## Existing artwork versus missing artwork

There are **35 UI PNGs and matching .pxa sources**. Existing art: backpack, bandage, bone, charcoal, clock, clothing, ember, energy, fire, fish, food, foraging, fuel, gear, hide, journal, knife, materials, meat, medicine, moon, precipitation, roots, rope, shelter, spear, sticks, storage, sun, temperature, tinder, torch, vitality, water, wind.

| Icon work | Importance | Existing coverage | Actual gap / suggested draft |
|---|---|---|---|
| HUD survival and common actions | High | Most art already exists | Wire existing backpack, gear, journal, food/water, fire, bandage, moon, shelter, storage, clock, foraging into buttons. |
| Warning / blocked / success | High | No dedicated set | Warning triangle, blocked mark, check; pair with text and color. |
| Combat actions | High | Spear/knife; gear/material fallback | Block, dodge, shove, throw, intimidate, flee, help, retreat. |
| People / follow / dismiss | Med | No dedicated set | Person/group and follow/dismiss variants. |
| Transfer / drop / equip / unequip | High | Object icons exist | Direction/action symbols that distinguish verbs from item identity. |
| Confirm / cancel / back / continue | High | No dedicated set | Reusable check, cross, back, forward; consistent across dialogs. |
| Search / inspect / tracking | Med | Journal/foraging fallback | Magnifier, footprints/pawprints, inspection cursor. |
| Nets / snares / ice / burial / curing rack | Med | Fish/rope/water/storage/hide fallbacks | Dedicated silhouettes if fallback recognition is insufficient. |
| Blood / capacities / specific effects | High | Bandage/medicine only | Blood drop distinct from water; movement, hand, consciousness, effect badges. |
| Help / sound / restart / map follow | Low | No dedicated set | Question mark, speaker/mute, restart, recenter. |

Use the existing PixelArtCli .pxa workflow for any new art. Existing artwork is 16×16 with point filtering; 32×32 display preserves whole-pixel scaling. The icon README claims location action icons and work-strategy mappings that current HudWidgets.Action does not implement; treat it as stale documentation for those placements.

## Cross-cutting priorities

| Priority | Finding | Proposed correction |
|---|---|---|
| High | UI font is globally scaled to 1.25, but many icons, bars, paddings and control heights stay fixed. | Add coherent UI sizing; test 20–25% larger text with matching layout, and 32px pixel icons where appropriate. No user-facing UI scale control found. |
| High | Shortcuts open screens but usually cannot finish the action. | Define complete keyboard sequences for food, fire, sleep, treatment, inventory, crafting, transfer and combat. |
| High | Global keyboard navigation is not explicitly enabled by app code. | Verify dependency/runtime behavior, then add deliberate focus order, visible focus, panel switching, activation and escape rules. Avoid collisions with map keys and forage Tab. |
| High | HUD actions and combat controls are text-only despite available art. | Add icons to shared action renderer; retain labels and stable key hints. |
| High | Dynamic warning lists, injury sorting and conditional action rows move content. | Reserve stable locations for important stats/actions; use border/tint/badge emphasis in place. Do not promote/reorder by urgency. |
| High | Color conventions and severity thresholds differ between screens. | Shared semantic styles; separate category identity from severity; add non-color warning symbols. |
| Med | Some important information is only exposed on hover. | Focus-accessible detail/help and visible disabled reasons without requiring mouse travel. |
| Med | Displayed close shortcuts for People and Discoveries disagree with implementation. | Show Esc or implement the advertised toggle keys. |
| Med | Fixed 16–18px bars, narrow columns and variable text wrapping raise reading effort. | Scale spacing and panel widths with text; validate small-window layouts and avoid making the map unusably narrow. |

Validation still needed: live keyboard walkthroughs, normal-size screenshots at the user's display scale, measured text contrast, focus behavior, and icon recognition at actual rendering size. No gameplay or UI implementation changes were made for this audit.
