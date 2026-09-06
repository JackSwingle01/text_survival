# Predator state migration

Status: proposed implementation plan, 2026-09-06. No gameplay changes made.

## Outcome and scope

Replace `Stalked`, `Hunted`, and `PackNearby` with situations derived from living predator herds. Preserve useful scenes and choices, but make their descriptions, consequences, and combat refer to the same animals in the world. There is no replacement aggregate threat meter.

First prove the complete loop with wolves, then migrate every producer and consumer of these three tensions, including generic encounters involving bears or hyenas. Species-specific `SaberToothStalked` and `ScavengersWaiting` arcs remain for later migration; prevent them from issuing duplicate encounters against a source already handled by this loop. Cold, shelter, wound, prey tracking, and the general tension registry remain outside scope. `FoodScentStrong` can remain for other content, but must not drive this new behavior path.

This plan supersedes the severity-driven pack/stalking portions of `../tension-arcs-plan.md` for this migration.

## Findings in the current implementation

- `Herd` already owns members, position, travel, hunger, fear, territory, and behavior. `BoldnessToward` derives engagement propensity from species, numbers, hunger, target vulnerability, and fear.
- `AnimalPresence.Near` means standing here **or territory contains this tile**. It is useful for territorial context but does not prove sensory proximity. `PickPredator` returns a species, losing source identity.
- Herd behavior requests already carry a herd. `GameContext` passes an existing member into `EncounterConfig`, and combat aftermath updates the participating herds.
- Event outcomes usually request an encounter by species. `HandlePendingEncounter` creates a fresh animal when no member is supplied. Migrated scenes must use the existing-member path.
- Predator tension creation is spread across pack/threat scenes, discoveries, camp, fishing, trapping, fever, terrain scenes, trail variants, and `EnvironmentalDetail`. Inspecting tracks can currently create stalking.
- `EscapeToCamp` clears tensions and aborts; it does not itself implement a physical pursuit or journey. Fire outcomes decrement tension. Both need consequences that match their wording.
- `EventQueue` stores events without source validation. Sources can die or move between queuing, display, and action resolution.
- Saves use `ReferenceHandler.Preserve`; herd references can use the existing object graph. Behavioral and observation persistence still need round-trip tests.

## Design decisions

### 1. Put predator memory on the herd

Add a small typed interaction record owned by the herd: target actor reference, last detected target location, last detection game minute, and current intent. Suggested intents are investigating, following, and searching; attacking is a validated encounter request, not another timed narrative stage. No target means ordinary species behavior applies.

Keep `HerdState` as the locomotion/activity state. Interaction intent provides the target and reason for its movement; one behavior update owns both, so two state machines never move the herd independently. Use actor references rather than a player-only boolean to avoid breaking existing NPC hunting. NPC behavior need not gain new content in this migration.

Following is not inevitable escalation. Hunger, food alternatives, learned fear, lost contact, and territory defense determine whether the herd approaches, feeds, searches, retreats, or attacks. Reuse `BoldnessToward` rather than introducing a second boldness formula. Store memory and intent only; derive danger, proximity, and descriptive categories when queried.

### 2. Separate proximity, detection, and player knowledge

Introduce a focused predator query/interaction service, using actual herd positions and existing species detection ranges. Territorial membership alone never permits a sighting or attack. Start with local detection using distance, existing visibility/terrain inputs where available, and concrete meat/blood/carcass cues. Do not build a wind-diffusion scent simulation for this pass.

Predators remember the last detected location and search it when contact is lost. They cannot read the player's position through the world indefinitely. Travel uses existing traversal time and passability; unreachable targets do not teleport predators or produce encounters.

Player observations are separate records: source herd, observed behavior, last seen location and time, and last announced change. They suppress repeat text and support uncertain wording after lost contact. Reading old tracks records evidence; it neither alerts a distant predator nor creates one. Only observable actions such as noise or approaching within detection range can change predator awareness.

### 3. Bind events to a source

Derived predicates should express facts such as observed predator nearby, predator following this actor, or predator within engagement range. Replace `High` and `Critical` requirements with scene-specific facts, not new numerical bands.

Select a source once when preparing an event. Pass a typed source context through descriptions, choices, and results. Never independently select a species for prose and a herd for consequences. Multiple packs remain separate sources; acting on one does not clear the other.

Revalidate source membership, living animals, target, location, and action eligibility before presentation and before resolution. If a scene is stale, discard it or provide a brief factual update. Do not consume costs for an already-invalid action. Time-consuming valid actions advance the world normally and may be interrupted by subsequent movement or attack.

Use one encounter admission path for behavior and migrated events: existing animal only, valid proximity, no duplicate pending encounter, existing combat cooldown honored. Revalidate pending combat when it actually starts. Scope removal of the species-only spawning fallback to migrated predator content; unrelated encounter systems can migrate separately.

### 4. Make responses concrete

| Response | World consequence |
|---|---|
| Keep working | Time passes; the herd continues sensing, traveling, or feeding |
| Raise a torch / make noise | Validate tools and fuel, then apply species-specific deterrence to this herd using existing fear/behavior mechanisms |
| Drop meat | Transfer actual inventory food to an accessible local food source; the herd can approach and consume it, reducing hunger |
| Retreat / return to camp | Use actual movement and travel time; the herd may follow while it retains contact |
| Stand ground / confront | Request combat against an existing eligible member; combat aftermath determines survivors and fear |
| Inspect signs | Update player evidence; change predator awareness only if the inspection actually makes detectable noise or approaches it |

Prefer existing ground storage/cache and carcass representations for food. If loose bait cannot be represented safely there, add a minimal typed ground-food feature with inventory transfer and consumption; do not misuse a carcass or create a bait-success flag. A safe cache is not edible bait. Dropped food is not guaranteed escape and cannot be consumed twice by different predators.

Camp itself offers no immunity. Actual deterrents and loss of contact matter. Solitary predators retain their species behavior, including den defense; do not force bears into a wolf escalation sequence.

### 5. Keep authored content as observation and decisions

Move useful creation/escalation/resolution text from `TensionEventFactory` to predator observation transitions. Generate important changes after herd simulation advances; use deduplication by source and observed change. Ambient event selection can still pace optional scenes, but it cannot summon threats, force pursuit progression, or pause actual danger.

Show factual observations in the stats panel rather than a predator severity percentage. After losing sight, say when/where the predator was last seen; never announce certain safety from the disappearance of a meter. Keep existing UI for unrelated tensions.

## Implementation sequence and acceptance gates

### Phase 1 — Source identity, queries, and encounter validation

Inventory direct strings, condition enums, helpers, dynamic trail tension names, and UI consumers. Add source-bearing event support, physical-proximity queries, and common encounter validation. Preserve existing NPC and combat behavior.

Gate: a scene and resulting combat refer to the same herd; a remote territory resident cannot attack; removing the source invalidates queued work; two packs remain distinct.

### Phase 2 — Wolf behavior and a complete playable loop

Implement awareness memory, following/searching, local food attraction/consumption, and observable changes. Route one pack scene through the full loop: hungry pack detects player butchering, approaches, becomes visible, responds to dropped food or retreat, and may engage. Add deterring and combat aftermath cleanup. Use game time everywhere, including search expiry and narrative cooldowns.

Gate: the sequence happens without any of the three tensions. It can end through feeding, deterrence, lost contact, or combat. Waiting and work use the same simulation. Long action updates must process movement and sensing at bounded intervals so predators cannot cross the player unnoticed because of a large time step; verify minute-by-minute versus batched advancement with controlled randomness.

### Phase 3 — Migrate content and other generic predators

Convert `GameEventRegistry.Pack`, relevant `Threats` and `Locations` scenes, and shared outcome helpers. Then sweep all other producers/consumers, including `TrailSignVariant`, `EnvironmentalDetail`, `EscapeScenarioVariant`, `IllnessVariant`, net/fishing logic, and discovery factories.

Classify each old producer: observation only, interaction with an existing source, or unsupported fictional encounter. Rewrite unsupported outcomes into truthful alternatives or remove that branch. Do not retain a compatibility helper that silently creates an animal or sets an abstract pursuit level. New predators belong to world population systems, not inspection/choice side effects.

Generic bear/hyena scenes must use their existing herds and species responses. Dedicated legacy species arcs remain isolated. Remove duplicate migrated warnings and attacks when they overlap those arcs. Record any deferred unsupported scene explicitly rather than leaving a dormant old producer.

Gate: no gameplay reads, creates, escalates, or resolves the three old tensions. Every migrated choice has a physical source and consequence. Existing NPC hunting, den defense, and non-predator events still function.

### Phase 4 — Persistence, UI, and removal

Persist interaction memory and player observations through the current save graph. Default missing fields to no awareness. At load, validate references against registered living herds/actors and clear invalid targets.

For legacy saves, remove only the three obsolete tension records. Do not translate severity into pursuit or spawn a herd to explain an old warning. Retain all existing animals and let real conditions reestablish interactions. This deliberately drops unsupported legacy narrative state and must be documented. The migration is idempotent; all other tensions remain intact.

Delete the three factories, condition members, display entries, transition handlers, and old outcome APIs once callers are migrated. Update tests and stats/outcome presentation. Audit dynamic/custom tension creation so removed types cannot reappear; legacy names may remain only in migration code/tests/documentation.

Gate: old and new saves load; reference identity survives round trips; observed warnings do not replay on every reload; obsolete tensions cannot be recreated during play.

### Phase 5 — Regression and play validation

Run targeted new behavior/event/save tests, then `dotnet test text_survival.Tests/text_survival.Tests.csproj`. Build the game and play the wolf loop in the desktop UI. Capture baseline failures before implementation and distinguish them from migration regressions.

Required scenarios:

- No predators: inspecting signs, fishing, and fever scenes cannot invent a real follower.
- Territory resident far away: signs may exist, a nearby sighting/attack does not.
- A hungry nearby pack investigates concrete food; a feeding or frightened pack can decline pursuit.
- Lost contact: search last known location, then abandon; returning to camp alone does not clear pursuit.
- Bait: exact inventory transfer, only accessible food consumed, no duplicated food, hunger changes on consumption.
- Torch/noise: actual resource requirements and species response; no guaranteed global threat removal.
- Two packs: action and combat affect only their bound source; the other can still act.
- Source dies, flees, or moves during queued content: stale event/encounter safely cancels.
- Combat: same members participate; deaths/fear update the world and subsequent observations.
- Time: no repeat encounter roll per query; bounded simulation stepping prevents action-length exploits.
- Saves: active follow/search, observation cooldowns, missing fields, dangling references, and legacy tension cleanup.
- Regressions: NPC hunting, scavenger feeding, bear den defense, and unrelated tension arcs.

## Risks and completion criteria

The largest effort is the content sweep, not adding herd fields. Scope phases as dependent, reviewable changes; do not activate old severity progression alongside the new loop for the same content. Some legacy branches may be removed when they cannot be supported by physical state. Retain their useful writing where truthful.

Actual movement, detection range, and encounter distance may use different units; settle that mapping in Phase 1 and test edge distances explicitly. Avoid converting tiles to combat meters by an unexplained constant. Narrative pacing may become sparse or noisy; tune observations and real animal behavior, never compensate by manufacturing a tension.

Complete means: the three tensions are absent from live gameplay, migrated predator content has a traceable living source, player actions change that source or physical resources, knowledge is distinct from world truth, and the wolf scenario passes both deterministic tests and a playable UI check.
