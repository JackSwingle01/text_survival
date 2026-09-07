# Migration 2: Remaining predator stories from world state

Status: planned, not implemented. September 6, 2026.

## Content preservation requirement

Apply the [predator content restoration plan](../predator-state-migration/content-restoration-plan.md). A generic response menu does not establish narrative equivalence. Inventory each migrated scene/outcome as restored with concrete eligibility and consequences, deferred with a named missing mechanic, or intentionally retired with a reason. Restore distinct saber stalking/ambush and multi-species carcass competition scenes alongside their behavior. Keep authored atmospheric variation, contextual actions, and post-action payoff; let physical facts determine which claims are available. Report coverage gaps explicitly before marking the migration complete.

## Outcome

Replace `SaberToothStalked` and `ScavengersWaiting` with behavior belonging to real animals, and retire `FoodScentStrong` after moving its consumers onto actual food and body cues. Build on migration 1's herd pursuit, observations, source-bound events, and encounter validation. The player is playtesting that foundation separately.

Example: butchering leaves an actual carcass; a nearby hyena detects it, approaches, waits while a stronger animal occupies it, and feeds when it can. Taking or protecting the food changes its behavior. A saber-tooth follows a detected actor and can lose contact; selecting a narrative option never creates an invisible cat or advances an attack meter.

## Current seams

- `PredatorInteractions.Update` deliberately excludes saber-tooths. Their underlying behavior type is currently `SolitaryPredator`, shared with bears. Simply deleting that exclusion would not preserve their intended identity.
- `AnimalSelector.GetVariant` has no explicit saber-tooth or hyena mapping; deterrence falls through to `Unknown`.
- `GameEventRegistry.SaberTooth.cs` creates and adjusts severity, uses territory presence for eligibility, and sometimes substitutes scripted damage for combat. Its intended game rules distinguish noise, fire, and confronting an ambush.
- `MegafaunaStrategy` uses saber tension severity to unlock scouting/tracking/approaching. Mammoth progression shares this strategy and must remain functional.
- `ScavengerBehavior` already finds and consumes real carcasses and reacts to competitors. Generic predator feeding must not bypass these decisions or consume a meal twice.
- `FoodScentStrong` still feeds `Situations` and event outcomes. Existing shared sensing already reads meat, bleeding, and bloody effects; exposed food queries currently cover raw/cooked meat, accessible caches, and carcasses.
- `AnimalPresence.Near` includes territory membership. It is not proof that an animal can see, hear, or reach the player.

## Design decisions

### One behavior owner per herd

Keep shared pursuit memory, sensory checks, player observations, and encounter delivery. Add explicit species policies/strategies for saber-tooth and scavenger decisions under the existing herd update. A single tick chooses movement, feeding, or an encounter. Do not introduce a second event-driven AI tick or a replacement suspicion number.

Saber-tooths use an ambush-oriented strategy instead of inheriting bear foraging. Approach requires an actual detected actor or remembered location; loss of contact uses the shared search expiry. An ambush requires a living source within reach, with opportunity derived from existing visibility, activity, and combat readiness. Combat remains the owner of injury. Facing or scanning can reveal an observable cat and establish readiness through existing encounter mechanics; it cannot reveal hidden coordinates or guarantee safety. If no suitable readiness field exists, explicitly add a short-lived actor action fact, not a cumulative advantage meter.

Preserve the authored species rules as game design: fire gives the saber-tooth no deterrence bonus; a shout can reveal the actor's current location to a nearby cat. Implement noise as a local, short-lived action stimulus using an explicit range and the normal detection/memory path. It must not globally summon cats or raise aggression. Do not promise an effective "move unpredictably" counter unless actual movement and loss of sensory contact support it; use real retreat choices instead.

Scavengers prioritize detectable, accessible food. Preserve existing carcass competition and hunger/boldness behavior. Waiting means an actual herd remains near a food target it cannot safely access. Target depletion, removal, protection, competitor departure, and satiation cause reevaluation. Counts and descriptions come from the actual living herd. Multiple herds may contend for one carcass; each update reads the remaining quantity.

### Shared food facts

Consolidate food discovery/access checks used by generic predators and scavengers. Return concrete sources and available quantity, and consume through the source inventory/carcass API. Respect covered/buried/protected storage using existing access rules. Keep carried food a sensory cue rather than an inventory predators can remotely consume. Audit fish and other edible resources against existing diets; include those actually supported rather than hard-coding all items as bait.

Use actual bleeding/bloody effects for body scent. Inventory change, cleanup, food consumption, and protection naturally remove cues. If a retained butchering scene needs residue after all meat is removed, it must create a finite local physical resource through the ordinary activity outcome; defer that scene if the resource is unsupported. No scent-strength state, wind plume model, or new global attraction radius in this pass. Preserve existing ranges unless a documented species rule requires a distinct value.

### Content and hunting progression

Convert useful saber and scavenger scenes into observations and responses to a captured source. Reuse `PredatorEventFactory`, real travel, inventory operations, deterrence, and validated encounters. Revalidate source, food target, reach, and choice prerequisites after input and elapsed time. Retire prose that promises teleportation, an invented predator, guaranteed shelter, or an injury outside combat.

Replace only the saber branch of `MegafaunaStrategy`: scouting is available where actual evidence can be sought; tracking needs an observed sign or last known location; approaching needs a currently located animal and a reachable route. Use existing discovery/observation records where possible. Failed scouting can find nothing. Neither map-wide animal knowledge nor territory membership alone unlocks a precise target. Keep mammoth tension-based stages unchanged.

## Implementation sequence

1. **Map and consolidate facts.** Inventory every producer/consumer of the three retired keys, including templates, conditions, display, saves, hunting, fishing, and carcass scenes. Consolidate food lookup/access/consumption; establish explicit species mappings. Acceptance: no duplicate consumption path, protected sources stay protected, non-predator consumers have a documented replacement or retirement.
2. **Complete species behavior.** Implement saber policy and local noise stimulus; integrate scavenger food competition with shared pursuit. Remove the saber exclusion only when its behavior is wired. Acceptance: each species can find, lose, abandon, and confront a real target without tensions; bear/wolf behavior remains intact.
3. **Migrate content and saber hunting.** Bind scenes to observed animals/food, replace scripted attacks with encounters, and move saber work options onto knowledge and position. Archive retired prose with the reason and replacement. Acceptance: scene claims and available actions match current world state; mammoth progression still works.
4. **Retire and migrate saves.** Remove factories, mutations, conditions, thresholds, templates, and display entries for all three keys. Extend legacy rejection/loading cleanup without spawning animals or inventing pursuit. Preserve actual animals, food, injuries, and valid observations. Recompute transient food targets after loading if their references cannot safely round-trip. Acceptance: legacy keys cannot re-enter active gameplay, including through old generic outcome paths.
5. **Validate and tune.** Run focused behavior/event/save tests, then the full suite. Use deterministic fixtures for causality and a short manual species pass for pacing. Tune physical policy inputs only when a documented scenario shows the need.

## Acceptance scenarios

- No living cat nearby: shouting and inspecting signs cannot spawn a saber encounter. A cat only in the wider territory cannot attack or appear as currently visible.
- A real cat detects a shout in range, follows the last detected location, and loses the player after contact expires. Fire does not accidentally use `Unknown` deterrence.
- Scanning while working changes actual observation/readiness; a queued ambush is discarded if the cat dies, moves away, or loses eligibility.
- Hyenas approach exposed food, wait for a stronger occupant, feed after it leaves, and stop when food is gone or hunger is satisfied. Two herds cannot each eat the original full quantity.
- Dropping food removes it from player inventory; protected storage is inaccessible; removing bait invalidates a pending bait-dependent response.
- Saber scouting/tracking/approach follow evidence and reach. Existing mammoth stages still appear as before.
- Save/load works during following, searching, and feeding; old severity creates neither a cat nor free resources. Wolf/bear pursuit and interrupted travel/work regressions pass.

## Scope and risk

Do not migrate mammoths, `FreshTrail`, or all wildlife narration; do not add ecosystem-wide smell propagation or tactical pathfinding. Species identity and double-owned feeding are the largest implementation risks. Land shared food ownership before species content, then remove legacy state once its consumers are accounted for. Prefer fewer truthful scenes over retaining unsupported outcomes.

Planning estimate: four to six focused implementation/review sessions, with behavior integration likely the largest part. This is a sizing estimate, not a delivery commitment. Migration 3 can follow independently; playtest feedback on migration 1 may change pacing but need not block the architecture.
