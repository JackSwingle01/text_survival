# Migration 3: Cold stories from thermal state

Status: planned, not implemented. September 6, 2026.

## Content preservation requirement

Apply the principles in the [predator content restoration plan](../predator-state-migration/content-restoration-plan.md). Preserve distinct exposure, interrupted work, desperate shelter/fire decisions, and gradual recovery scenes through authored prose selected from real conditions. A temperature label alone does not replace a scene. Map every migrated outcome to restored content, an explicit missing mechanic, or a justified retirement; retain atmosphere without claiming unsupported injury or recovery. Report content coverage separately from passing mechanical tests before declaring completion.

## Outcome

Remove `DeadlyCold` and derive cold warnings and responses from the player's actual temperature, cooling or warming, wetness, insulation, activity, and environment. The thermal simulation determines consequences; events make those consequences legible and offer real actions.

Example: crossing exposed terrain in wet clothes causes simulated cooling. A warning offers stopping work, lighting a fire with available materials, or traveling to known shelter. Reaching a weak fire does not clear danger: the player remains cold until their actual body warms. A successful shelter action changes a real feature, so later exposure also reflects it.

## Current seams

- `SurvivalProcessor` already computes heat loss/gain using body state and `SurvivalContext`, including activity, wetness, insulation, fire, and stored clothing heat. Existing game thresholds include 95°F hypothermia and 89.6°F severe hypothermia.
- `CalculateTemperatureChangePerHour` is a raw rate. `ProcessTemperature` also applies the clothing heat buffer, so raw cooling does not always mean immediate core cooling.
- `GameEventRegistry.ColdSnap.cs` separately escalates severity, adds temperature effects/injuries, and resolves danger through prose. Some choices claim to build shelter, burn equipment, or return to camp without the matching world changes.
- `GameContext` can resolve `DeadlyCold` merely near fire. `TensionEventFactory` creates separate cold announcements. Megafauna events also produce the tension.
- The survival model's existing frostbite effect is tied to low core temperature; it is not a detailed peripheral tissue simulation. Several authored scenes make stronger claims about fingers than the simulation supports.
- `ProjectTemperatureAwayFromFire` retains the original location's shelter-adjusted temperature. It must not be presented as an accurate journey forecast.

## Design decisions

### One derived cold assessment

Add a small read-only assessment shared by conditions, warning creation, and HUD explanations. It contains current body temperature, a clearly labeled effective short-term temperature trend, existing cold impairment, wetness, and relevant environmental contributors. Reuse the survival model's thresholds through shared constants/classification rather than duplicate them in event code.

Calculate the effective trend through the same buffer-aware thermal calculation using a fixed short interval and the current activity/context, without mutating the body. Label it as current-condition trend, not a travel prediction. Verify that evaluating it changes no heat buffer, effects, health, or random state. Separate raw environmental heat loss from core-temperature change if explaining why clothing is temporarily protecting the player.

Distinguish exposure, current impairment, and recovery. A warm body that is cooling may deserve an exposure cue; a dangerously cold body that is warming still deserves a danger indication. Do not classify recovery from the presence of a fire or a cleared wetness flag. Thresholds express the existing game model; presentation names do not add a second damage model.

### Warnings observe transitions

Evaluate after the survival update is applied, including during travel/work/waits. Queue warnings for meaningful transitions into existing cold impairment/danger, and a restrained exposure cue when currently cooling under relevant conditions. Deduplicate against `ThresholdEventFactory`, `WeatherEventFactory`, and current cold effect announcements; choose one owner for each physiological warning. Weather changes may still be described independently.

Store only notification memory, such as last announced category and time. It does not affect temperature, damage, or event odds. Use a cooldown/rearm rule to prevent threshold jitter from repeating interruptions, with serious worsening allowed to interrupt promptly. Revalidate a queued warning against current state before showing it. Do not queue a new cold popup recursively while its response is advancing time; retain materially worsened state for the normal event boundary.

The HUD should explain the measured problem and relevant current contributors, e.g. cold and warming, or wet and cooling. Avoid an unexplained danger percentage, certainty about unseen destinations, or an exact time-to-death estimate.

### Every response performs its stated action

- **Continue / stop work:** use ordinary activity time and interruption handling. Continued exposure is processed once by survival; no extra `WithCold` penalty for the same elapsed minutes.
- **Start/tend a fire:** use the existing fire handler/UI path, actual tools/materials, success rules, time, and location feature. Cancellation or failure grants no warmth. A successful fire changes heat input; it does not directly restore core temperature or erase impairment.
- **Seek shelter / return to camp:** select a known real destination and use ordinary travel, including elapsed time and interruption. Do not imply an undiscovered overhang exists. Route forecasting is outside this migration.
- **Improve shelter:** use the existing improvement work at an existing shelter and consume actual materials/time. Offer construction only through an existing supported crafting recipe that creates a real shelter feature. Otherwise omit the emergency-build choice in this pass.
- **Wait to warm:** ordinary waiting with current conditions; preserve the possibility of continued cooling. No scripted recovery or automatic safety after a fixed duration.

Retire "sacrifice fingers" and generic "burn something" choices in this pass: the current systems do not substantiate the promised tradeoffs. Do not add equipment-burning rules or a peripheral injury model just to preserve an old option. Rewrite `GoingNumb`/`FrostbiteSettingIn` around symptoms actually represented, or retire them when the shared warning covers their purpose.

## Implementation sequence

1. **Inventory and assessment.** Map all `DeadlyCold` producers/consumers and cold announcement owners. Expose shared thermal thresholds and a pure buffer-aware assessment. Acceptance: repeated reads do not mutate state; displayed trend agrees with a normal thermal step under unchanged conditions, including charged clothing.
2. **Observation and UI.** Replace cold tension announcements/conditions with post-update state transitions; align HUD trend and explanation. Acceptance: cooling, severe cold, warming while impaired, and actual recovery remain distinct; jitter does not spam; worsening interrupts long activities through existing control flow.
3. **Physical response migration.** Rewrite or retire cold-snap and megafauna cold outcomes. Route choices through real fire/travel/work actions. Audit temperature effects and frostbite helpers in these migrated scenes for duplicate exposure/damage and suspicious magnitude units. Acceptance: each retained promise changes the corresponding world state; resource/time effects occur once; cancelled/stale actions do nothing.
4. **Remove legacy state and preserve saves.** Delete `DeadlyCold` factories, thresholds, display, resolution-near-fire, conditions, and mutation helpers after callers migrate. Reject old creation and strip the legacy key on load. Preserve temperature, wetness, heat buffer, effects/injuries, fires, and shelters. Initialize notification memory without replaying the whole arc; current severe danger remains visible on load. Acceptance: old severity neither heals nor harms the loaded character.
5. **Verification and content review.** Run focused thermal/event/save tests and the full suite. Review the actual cold response UI for truthful choices and readable current state. Record limitations rather than silently expanding the physiological model.

## Acceptance scenarios

- Warm/dry, cold/dry, cold/wet, sheltered, and exposed states produce assessments consistent with the existing thermal processor. Weather alone does not assert core hypothermia.
- Charged clothing temporarily buffers core cooling; querying the assessment does not drain it. Longer work still advances the real buffer normally.
- A cold player beside inadequate fire remains impaired and may keep cooling. A cold player warming successfully stays flagged until physiological recovery.
- Fire-start success consumes the correct resources and creates real heat; failure/cancellation grants no temperature jump or free shelter.
- Returning to camp changes location through real travel and can be interrupted. Shelter improvement consumes time/materials and changes the actual shelter.
- Identical elapsed exposure with and without a narrative warning causes equivalent thermal change, apart from the player's selected physical action. No duplicated cold damage from migrated outcomes.
- Threshold jitter, queued warnings that recover before display, and worsening during a response do not produce stale or recursive popups. Long work/travel stops correctly when interrupted.
- Saving/loading a severely cold player preserves physiology and current danger; legacy tension severity is ignored. Existing survival, wetness, warmth, and companion tests remain valid.

## Scope and risk

Keep the existing physiological balance and frostbite model unless a directly exposed defect prevents the migration; document any necessary correction separately. Do not migrate every illness or all weather content. Do not fix companion route forecasting or invent local tissue temperatures here. The largest risks are duplicated thermal damage, calculating trends without the heat buffer, and advancing time twice through event/handler nesting. Test these at the action boundary as well as in isolated assessment tests.

Planning estimate: three to five focused implementation/review sessions. Migration 2 and migration 3 share event/save integration seams but no new gameplay dependency; prefer finishing migration 2 first to keep each change reviewable. User playtesting is separate and can inform warning frequency later.
