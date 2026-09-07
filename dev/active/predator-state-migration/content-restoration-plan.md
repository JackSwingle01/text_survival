# Predator content restoration

Status: superseded by the content-preserving repair on September 6, 2026. The original catalog and choices are restored with explicit source rules; see content-coverage.md. The staged restoration proposal below is historical.

## Purpose

Migration 1 restored physical causality but did not preserve equivalent narrative coverage. The retirement inventory contains 95 outcome entries and names 12 staged scenes; these overlap and are not 107 independent events. Eight contextual callers now share a generic description/menu. This plan repairs that loss before extending the same consolidation to other tensions.

The acceptance standard is equivalent dramatic situations and meaningful decisions, not an equal number of branches. Keep the original text in `retired-outcomes.md` as the audit source. This document maps scene families; implementation must annotate every inventory entry with its replacement, explicit deferral, or retirement reason before claiming coverage complete.

## Authored scenes over shared mechanics

Retain the shared predator response implementation, but separate it from scene selection and prose. A scene supplies eligibility, a captured animal/feature source, contextual description, and the subset of appropriate actions. Ordinary handlers execute those actions. A post-action observation describes what actually happened, rather than selecting a claimed result before the simulation advances.

Facts used by a scene must be revalidated when presented and when a choice executes. Weather, terrain, light, activity, visible animals, accessible resources, and previously observed behavior can select prose variants. Flavor can embellish sensation without inventing consequential facts: a tense silence is fine; a second pack arriving requires that pack. Never describe hidden intention or exact numbers that the player's perception cannot establish.

Give specific eligible scenes priority over generic movement notices. Record recent scene/source/time only for repetition control. Do not make scene history increase threat or force the next dramatic beat. A newly visible predator should receive a contextual introduction once, not both the generic observation popup and a fishing/camp popup.

## First restoration slice: existing mechanics

| Scene family | Required facts | Restored decision or flavor | Boundary |
|---|---|---|---|
| Movement at camp edge / Footsteps Outside | Player at camp and awake; real observable predator; darkness/fire only when present | The interruption of supposed safety; watch, light/raise torch, leave real meat, retreat | Do not assert footsteps from sight alone or overnight tracks without evidence. Fever can color uncertainty without proving a predator exists. |
| Bear at fishing hole / Wolves by nets | Actual fishing/net context and visible matching species | Fishing interrupted by an animal; stop the task, watch, use available deterrence, leave | Current callers check only general field work. Tighten context before adding fishing prose. Fish-as-bait needs supported food consumption first. |
| Wolves at a kill | Accessible actual carcass plus observable wolves and relevant harvesting context | Unfinished work with an animal nearby; abandon work, watch, retreat, confront | First slice offers interruption and return through ordinary work controls. A new "cut one more portion" shortcut waits for safe work continuation support. |
| Predator at trap line | Local actual trap and observable predator; catch/bait only if actually present | Divided attention between the trap and the animal | Existing tension/field-work gates are insufficient. Do not claim bait theft, damage, or reset without feature mutation. |
| The Followers | Observable herd following the player | Recognition that the animal is still there; watch, deter, drop meat, travel | Refer to prior sightings only if observation memory establishes them. |
| Bait accepted / animal retreats / contact lost | Actual observed change after the response or later update | Authored payoff to a decision | "It lowers its head to the meat" needs feeding on that source. Loss of sight means uncertain whereabouts, not guaranteed escape. |

Example for an eligible carcass scene: "You stop cutting. Wolves are visible beyond the carcass. There is meat left to take." Further details such as treeline, snowfall, approach, or growing darkness are optional clauses selected only when supported. The decision is compelling because waiting and work consume time while the world continues.

## Named staged scenes

| Retired scene | Replacement or disposition |
|---|---|
| Pack Signs | Observation of actual tracks/sign records; defer any claim of freshness/direction absent evidence. |
| Eyes in the Treeline | Visible predator introduction using actual terrain/light; first slice. |
| Circling | Describe following or current position now; retain literal circling only when movement history supports it. |
| The Pack Commits | Source-bound combat transition after the actual attack decision. |
| Stalker Circling | Same factual boundary as Circling; avoid duplicating the pack scene with a renamed menu. |
| The Predator Revealed | First actual sighting after prior uncertain evidence; otherwise ordinary first sighting. |
| Ambush | Valid real encounter plus supported awareness conditions; species-specific extension belongs to migration 2. |
| Shadow Movement | Contextual first/repeated sighting with appropriate light; no guaranteed pursuit claim. |
| Cut Off | Requires actual route obstruction; defer until an existing route rule can substantiate it. |
| Spotted in Open | Actual exposed terrain and detection state; narrator must distinguish what the player can know. |
| Mutual Visibility | Use actual reciprocal detection and observable animal behavior; no omniscient intent. |
| Escape into Thicket | Real travel into appropriate terrain followed by loss of contact; do not guarantee safety on selecting travel. |

## Remaining inventory families

- **Discovery, locations, first visits, expedition:** restore incidental sightings through actual observable animals; tracks, markings, and carcass discoveries need existing evidence/features. Preserve the pleasure of noticing wildlife without automatically starting pursuit.
- **Blood trail, threats, small game, den:** bind encounters and competing claims to the actual prey/carcass/den source. An animal arriving during a wait is a world update result. Prey theft and newly discovered kills require real resource/animal state; defer unsupported cases explicitly.
- **Scavenger competition:** preserve the three-way carcass scene as a migration 2 deliverable. Current `At the Carcass` only requires an observable predator and expedition, so it is not an equivalent. Require carcass and multiple real competitors, then narrate actual feeding/departure/access changes.
- **Camp and fever uncertainty:** keep subjective unease and atmospheric variation. A branch claiming that checking proved a real animal was present must depend on perception. Hallucination must not secretly spawn or move predators.
- **Herd and post-hunt pressure:** observable followers and competing predators support the framing. Hasty cuts, abandoned meat, and harvest timing need ordinary harvesting/resource mechanics; no weighted text-only theft or injury.

## Work sequence

- [x] Add scene selection/context on top of shared responses; retain generic fallback for otherwise uncovered sightings.
- [x] Restore camp, fishing, carcass, trap-line, and following introductions with exact physical eligibility and appropriate action subsets.
- [x] Add observed post-action payoff text for feeding, fleeing, continued following, and loss of sight, without advancing time twice.
- [x] Ensure specific scene selection and generic observation do not double-announce one animal.
- [x] Map each of the 95 archived entries to a restored scene, migration 2 dependency, explicit future mechanic, or intentional retirement. Never mark a generic menu as equivalent to a missing tactical situation.
- [ ] Restore incidental sightings and evidence-based scenes where current systems support them.
- [x] Record unsupported competition, theft, circling, and route-blocking cases in the relevant implementation plan.
- [ ] Verify contextual UI and meaningful action behavior; check cancellation, stale sources, real resource changes, and interrupted time.
- [ ] Review a coverage report with restored/deferred/retired counts and examples. Mark restoration complete only with explicit disposition of the inventory.

## Acceptance

The same real predator at a fishing site, occupied camp, and carcass produces meaningfully different framing and relevant choices. Different text alone does not count as a restored decision. No scene invents a resource, animal, route obstruction, or successful escape. Responses receive truthful narrative payoff. Some sightings remain quiet observations. Scene repetition is controlled without increasing danger or enforcing a staged arc.

First slice now implemented: contextual source-bound introductions, appropriate work interruption text, resting while watching, observed post-action payoff, and a single observation owner replacing seven random contextual registrations. Fever uncertainty remains separate and cannot duplicate a visible predator scene. The full archived outcome assessment is in [content-coverage.md](content-coverage.md): 34 partial and 61 deferred, with no claim that these are complete equivalents.

Five new regression tests cover activity/feature eligibility, depleted carcasses, local traps, duplicate observations, post-action truth, and five-minute resting behavior. The production camp response was rendered and visually inspected. Broader incidental evidence and specialized tactical decisions remain open.
