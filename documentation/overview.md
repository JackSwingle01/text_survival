# Text Survival — Systems Overview

*A console-based survival game set in an Ice Age world. This document describes the core systems and how they interact.*

---

## Fire

Physics-based simulation. Fire is infrastructure — a good fire with quality fuel extends your expedition radius. A dying fire while you're away means returning to cold camp.

Fire pit types — Open, Mound, Stone. Better pits provide wind protection and fuel efficiency.

Typed wood fuels with distinct burn characteristics — Pine (fast/hot), Birch (balanced), Oak (slow burn). Different fuels suit different needs. Specialized tinders (BirchBark, Amadou) improve ignition chances. Alternative fuels include bone and peat.

Two-mass fuel model — unburned fuel gradually ignites based on fire temperature. Hot fires catch new logs faster. Temperature-gated fuel addition prevents smothering with too much fuel at once.

Fire phases: Igniting → Building → Roaring → Steady → Dying → Embers. Ember preservation allows relight without fire-starting tools.

Charcoal production — burned fuel leaves charcoal behind, collectible after fire dies. Used for crafting.

Fire interacts with: survival simulation (heat source), expeditions (time pressure), locations (heat source feature), crafting (charcoal production).

**Files**: `Environments/Features/HeatSourceFeature.cs`

---

## Torches

Portable light and warmth for dark locations and night work.

Can be lit from fire, another torch, or firestarter+tinder. Limited burn time. Chaining prompt appears when running low.

Enable work in dark locations (caves, night) and provide portable warmth during expeditions. Consumable — cannot be relit once extinguished.

Torches interact with: expeditions (enables dark work), survival simulation (portable heat), crafting (craftable lighting).

**Files**: `Actions/Handlers/TorchHandler.cs`, `Items/Gear.cs`

---

## Expeditions

Simplified architecture using direct location tracking — no expedition state object. `GameContext.CurrentLocation` tracks where you are.

Travel system (`TravelRunner`):
- Movement between connected locations
- Hazardous terrain with quick/careful traversal options
- Injury risk calculations for dangerous terrain (climbing, thin ice)
- Event interruption with continue/stop prompts

Work system (`WorkRunner`):
- Strategy pattern: each work type has an `IWorkStrategy` implementation
- Available work types: Forage, Hunt, Harvest, Explore, Trap, Chop, Cache, CraftingProject, Salvage
- Darkness blocking — requires fire or torch for work in dark locations or at night
- Impairment calculations — manipulation and consciousness affect work speed
- Event interruption during work sessions

Expeditions interact with: fire (time pressure), torches (enables dark work), locations (destinations and features), events (triggers during travel and work), survival simulation (stats drain during travel).

---

## Locations

Locations are places in the world with travel times from camp. Each has:

- Name and character (Frozen Creek, Dense Birches, Rocky Overlook)
- Travel times between connected locations
- Features (what you can do there)
- Terrain properties (exposure, shelter potential, hazard level, darkness, climb risk, visibility, escape terrain, vantage points)

Locations connect as a graph. Camp is the anchor point.

Discovery states:
- Unexplored — can travel there but don't know what's there (shown as hints)
- Explored — full features revealed after visiting

Depletion: Areas run out over time. Pushes the player outward or forces camp relocation.

Temperature calculation — complex physics model accounting for wind chill, sun warming, precipitation, shelter effects (only when stationary), and heat sources.

Locations interact with: expeditions (travel destinations), features (what's available), events (location-based triggers), exploration (discovery), survival simulation (environmental temperature).

**Files**: `Environments/Location.cs`

---

## Grid

The world map is a 2D grid of locations. `GameMap` owns all spatial relationships — locations don't know their positions.

**Scale**: Each tile represents approximately **1 square mile** of terrain. Travel time across a tile (entering or exiting) is based on ~20 minutes per mile on flat terrain, giving a 10-minute radius for plain terrain, scaled up for difficult terrain types.

Core operations:
- `CurrentPosition` — player's (X, Y) coordinates
- `CurrentLocation` — derives from `_locations[CurrentPosition.X, CurrentPosition.Y]`
- `GetTravelOptions()` — adjacent passable locations from current position
- `MoveTo(location)` — updates position and visibility

Visibility system:
- `TileVisibility`: Hidden → Explored → Visible
- Sight range calculated from current location's visibility factor (0-4 tiles)
- Moving updates which tiles are visible vs merely explored

Travel uses cardinal directions (N/S/E/W). Locations connect implicitly by adjacency, not explicit edges.

Grid interacts with: locations (grid contains them), travel (adjacency determines options), UI (grid rendering in TravelMode).

**Files**: `Environments/Grid/GameMap.cs`, `Environments/Grid/GridPosition.cs`

---

## Features

Features are what make locations useful. They live on locations and define available activities.

**ForageFeature** — Ambient scavenging. Search the area, find things based on abundance and time invested. Depletion system with respawn. Resource types include medicinal plants, fungi, tree products.

**HarvestableFeature** — Visible specific resources. A berry bush, a dead tree. Quantity-based, not probability-based. Tool tier gating — some resources require better tools. Individual resource respawn timers.

**AnimalTerritoryFeature** — What game can be found here. Animals spawn dynamically when searching, not pre-placed. Game density depletes with successful hunts and respawns over time. Peak activity hours system affects encounter rates.

**WoodedAreaFeature** — Tree chopping mechanic. Work accumulates until tree fells. Progress persists across sessions. Can specify wood type (Pine/Birch/Oak) or mixed forest. Trees respawn slowly.

**SnareLineFeature** — Trapping system. Manages multiple placed snares at a location. Snare states: Active, CatchReady, Stolen, Destroyed. Durability system, can be reinforced with bait. Small game only. Passive catching over time.

**CacheFeature** — Remote storage for expeditions. Natural (found) or Built types. Capacity limits vary. Special properties: predator protection, weather protection, food preservation.

**CuringRackFeature** — Food and hide preservation. Process items over time. Transforms raw → preserved versions with weight loss during drying.

**WaterFeature** — Frozen water bodies. Ice thickness affects hazard level. Ice hole cutting for fishing/water access. Holes refreeze over time.

**BeddingFeature** — Sleep quality system. Quality affects sleep efficiency. Wind protection and ground insulation properties. Warmth bonus during sleep.

**CraftingProjectFeature** — Multi-session construction. Tracks time invested and materials consumed. Progress persists. Material reclaim if abandoned. Some projects benefit from tools (shovel for digging).

**ShelterFeature** — Built protection. Temperature insulation, overhead coverage, wind coverage. Snow shelters degrade in warm temps. Damage/repair system.

**HeatSourceFeature** — Fire. See Fire section above.

**CarcassFeature** — Dead animal carcasses awaiting butchering. Created by successful hunts, combat victories, and some events (like following ravens). Carcasses decay over time (fresh → good → questionable → spoiled) affecting meat yield. Contains all butchering logic: yields based on animal weight (40% meat, 15% bone, 10% hide, 5% sinew, 8% fat), tool checks (no knife = reduced yield), manipulation impairment penalties. Butchered via ButcherStrategy work type. Creates time pressure to return and process before spoilage.

**EnvironmentFeature** — Terrain properties affecting gameplay.

Features interact with: locations (features live on locations), expeditions (work types use features), events (features can trigger events and be modified by event outcomes), crafting (features provide materials), survival simulation (shelter and heat).

**Files**: `Environments/Features/` (various feature implementations)

---

## Survival Simulation

Rate-based calculation per minute:

- Energy — depletes with time and exertion. Sleep regenerates faster.
- Hydration — constant drain, faster with sweating in heat.
- Calories — BMR-based metabolism using body composition, activity multipliers applied. Organ condition affects efficiency.
- Temperature — physics-based heat transfer model. Heat capacity, heat loss, metabolism heat generation, fire proximity bonus.

Wetness system (major survival pressure):
- Wetness accumulates from precipitation exposure
- Drying rate based on fire proximity, temperature, wind
- Below freezing: clothes freeze wet (no drying)
- Wetness causes direct cooling and reduces clothing insulation effectiveness

Auto-generated survival effects trigger when stats drop low — Hungry, Thirsty, Tired. Severity scales with how depleted the stat is. Impair movement, manipulation, consciousness, sight, hearing.

Starvation mechanics: Burns fat first → muscle → organ damage → death.

Hypothermia: Severe cold triggers damage scaling with temperature drop. Targets vital organs.

Regeneration: Requires being well-fed, hydrated, and rested. Body parts heal over time. Nutrition quality and digestion capacity affect healing rate.

The survival processor returns stat changes, triggered effects, and messages — it doesn't own state, just calculates.

Survival simulation interacts with: body system (stats live on body), effects (thresholds trigger effects), fire (heat contribution), torches (portable heat), locations (environmental exposure), expeditions (activity multipliers), weather (wetness accumulation).

**Files**: `Survival/SurvivalProcessor.cs`

---

## Body System

Region-based architecture (not hierarchical tree). Body divided into regions: Head, Chest, Abdomen, Left/Right Arms/Legs.

Each region has:
- **Tissues**: Skin, Muscle, Bone (each with Condition 0-1 and Toughness values)
- **Organs**: Heart, Brain, Lungs, Liver, Stomach, Eyes, Ears (with Condition 0-1)
- **Coverage**: Percentage used for hit distribution during damage

Blood system — separate tissue with its own condition (0-1):
- Blood loss affects circulation (cascading effect in CapacityCalculator)
- Below 50% blood = consciousness/movement failure
- Blood regenerates slowly when well-fed, hydrated, rested

Capacities (calculated from tissue/organ condition):
- Moving — legs, feet condition
- Manipulation — arms, hands condition
- Breathing — lungs, chest condition
- Consciousness — brain, blood circulation
- Plus: BloodPumping, Sight, Hearing, Digestion

Multiple body types supported:
- Human (bipedal) — standard player body
- Quadruped (wolves, bears) — 4 legs
- Serpentine (snake-like) — no limbs
- Arachnid (8 legs, multiple eyes)
- Flying (birds) — wings + legs

Body composition (fat, muscle mass in kg) affects temperature resistance, speed, strength. Calories convert to fat at 7700 kcal per kg.

Capacity calculation is generic — average tissue multipliers across relevant regions. Blood condition affects all capacities via circulation.

Body system interacts with: survival simulation (stats live here), effects (damage triggers effects), damage (structural harm via DamageCalculator), abilities (capacities determine what player can do).

**Files**: `Bodies/Body.cs`, `Bodies/BodyPart.cs`, `Bodies/BodyPartFactory.cs`

---

## Effects

Effects are ongoing processes that tick over time. Distinct from body damage.

Body Damage = Structural
- Entry point: `Body.Damage(DamageInfo)`
- Tracks tissue condition (skin, muscle, bone, organs)
- Affects capacities based on which parts are damaged
- Heals automatically when well-fed, hydrated, rested

Effects = Processes
- Entry point: `EffectRegistry.AddEffect(effect)`
- Ongoing conditions that tick over time
- Have severity, decay rates, capacity/stat modifiers
- Resolve through natural decay or treatment

A wolf bite causes BOTH body damage (structural injury to leg) AND a bleeding effect (ongoing blood loss). Stopping bleeding doesn't heal the leg. Healing the leg doesn't stop bleeding.

Damage-triggered effects (auto-applied during combat/injury):
- **Bleeding** — Sharp/Pierce damage breaking skin. Rapid blood loss. Requires treatment.
- **Pain** — External damage. Impairs manipulation, consciousness, sight, hearing. Fades naturally.
- **Dazed** — Blunt head trauma. Impairs sight, hearing, consciousness. Fades slowly.

Temperature effects — Shivering, Hypothermia, Frostbite, Hyperthermia, Sweating. Affect temperature regulation, capacities, hydration.

Wetness effect — environmental exposure. Causes direct cooling and reduces clothing insulation.

Survival stat effects — Hungry, Thirsty, Tired (auto-generated when stats drop low).

Physical impairment — Exhausted, Sore, Stiff, Sprained Ankle, Clumsy. Affect movement and manipulation.

Illness — Nauseous, Coughing, Burn, Fever. Affect various capacities. Some worsen over time without treatment.

Psychological — Fear, Shaken, Paranoid. Affect manipulation and consciousness. Fade at different rates.

Positive effects (buffs from events/rest) — Warmed, Rested, Focused, Hardened. Temporary boosts to temperature regulation or capacities.

Effects interact with: body system (damage triggers effects), survival simulation (thresholds trigger effects), events (outcomes apply effects), abilities (effects modify capacities), damage (some effects like Bleeding target tissues directly).

**Files**: `Effects/EffectFactory.cs`, `Effects/EffectRegistry.cs`

---

## Events

Events trigger during expeditions based on context — location, player activity, player state, time, weather, and active tensions. Events aren't random encounters — they're contextual interrupts that create decisions.

Architecture: `GameEvent` is a class containing `EventChoice` objects, each with `EventResult` outcomes. `GameEventRegistry` (a partial class split across ~19 files) contains factory methods that build events with context-aware descriptions. Two-stage triggering: base roll per minute → weighted selection from eligible events.

Event organization — organized into narrative arcs:
- Weather events (storms, whiteouts, cold snaps)
- Expedition events (terrain hazards, discoveries, accidents)
- Camp events (vermin raids, shelter damage, smoke issues)
- Threat events (predator signs, stalking progression)
- Herd events (migration encounters)
- Multi-stage arcs (Cold Snap, Wound/Infection, Disturbed trauma, Den claim, Pack hunting, Fever progression)
- Trapping events
- Location-specific events

Outcomes can:
- Add time to expeditions
- Apply effects or injuries
- Grant rewards via RewardPools
- Consume resources as costs
- Create, escalate, or resolve tensions
- Spawn predator encounters
- Damage tools or clothing
- Discover new locations
- Add, modify, or remove features
- Chain into follow-up events

Events interact with: tensions (create/escalate/resolve), locations (triggers and discovery), effects (outcomes apply them), predator encounters (can spawn them), inventory (costs and rewards), features (can modify).

### Event Variants

Variants ensure event text matches mechanics. Three variant types handle different event categories: injuries (body targeting), discoveries (reward pools), and illness (cause-specific symptoms).

**Injury Variants** — The problem: "Your foot catches on something" shouldn't play when damage lands on the player's arm. `InjuryVariant` bundles description text with `BodyTarget`, damage type, and optional effects.

```csharp
var variant = VariantSelector.SelectAccidentVariant(ctx);
return new GameEvent("Minor Accident", variant.Description, 0.8)
    .Choice("Push On", ...,
        [new EventResult("You ignore it.", 0.5, 0).DamageWithVariant(variant)])
```

Terrain accident pools:
- **TripStumble**, **SharpHazards**, **IceSlip**, **RockyTerrain** — common terrain hazards
- **ClimbingFall**, **FallImpact**, **Sprains**, **DarknessStumble** — serious injuries with effects

Work mishap pools:
- **DebrisCuts** — searching ash piles, collapsed structures
- **VerminBites** — rodent/pest encounters
- **CollapseInjuries** — shelter/structure failure (with Dazed, SprainedAnkle)
- **EmberBurns** — fire-tending mishaps

`VariantSelector` weights pools by context — ice variants weight higher near frozen water, rocky variants on hazardous terrain.

**Discovery Variants** — Bundle find descriptions with `RewardPool`. Descriptions are generic to match generic pools — variety comes from *how* you find things, not what specific item appears.

```csharp
var discovery = DiscoverySelector.SelectGeneralDiscovery(ctx);
new EventResult(discovery.Description, 0.4, 8).WithDiscovery(discovery)
```

Pools: **SupplyFinds**, **TinderFinds**, **MaterialFinds**, **BoneFinds**, **CampFinds**, **CacheFinds**, **SmallGameFinds**, **HideFinds**

**Illness Variants** — Connect symptoms to underlying causes for player learning.

```csharp
var onset = IllnessSelector.SelectOnsetVariant(ctx);  // Picks based on wounds, temp, etc.
new EventResult(onset.Description, 1.0).WithIllnessOnset(onset)

var hallucination = IllnessSelector.SelectHallucinationVariant(ctx);
bool isReal = IllnessSelector.IsHallucinationReal(hallucination, ctx);  // 10-25% chance
```

Onset pools: **WoundOnset**, **ExposureOnset**, **ContaminationOnset**, **ExhaustionOnset**
Hallucination pools: **FireHallucinations**, **PredatorHallucinations**, **MovementHallucinations**, **IntruderHallucinations**

Hallucinations weight toward real threats (fire hallucinations more likely when fire is actually low) and have reality checks — sometimes the fever dream is real.

**Files**: `Actions/Events/Variants/` (InjuryVariant.cs, AccidentVariants.cs, DiscoveryVariant.cs, IllnessVariant.cs), `Actions/GameEvent.cs`

---

## Tensions

Tensions represent unresolved narrative threads that persist across events.

Each tension has:
- Type and severity (affecting event weights)
- Decay rate
- Camp behavior (whether it decays at camp or only in field)

Tension types:
- **Predator threats** — Stalked, Hunted (escalated), PackNearby
- **Camp threats** — SmokeSpotted (doesn't decay at camp!), Infested, ShelterWeakened, FoodScentStrong
- **Medical** — WoundUntreated, FeverRising (decays faster at camp with rest)
- **Environmental** — DeadlyCold (resolves when reaching fire)
- **Hunting/Prey** — WoundedPrey (trail goes cold), ClaimedTerritory, HerdNearby (window closes fast)
- **Psychological** — Disturbed, MarkedDiscovery
- **Trapping** — TrapLineActive

Camp vs field dynamics create interesting decisions:
- Some tensions decay only in field (camp is the source)
- Some decay faster at camp (safety allows recovery)
- Some decay slower at camp (immobility prevents resolution)

Lifecycle: Events create tensions → subsequent events can escalate → tensions resolve through player action, event outcomes, or natural decay → multiple active tensions compound pressure.

Tensions interact with: events (tensions modify event weights, events modify tensions), expeditions (decay behavior differs at camp vs field).

**Files**: `Actions/Tensions/ActiveTension.cs`, `Actions/Tensions/TensionRegistry.cs`

---

## Predator Encounters

Emerge during hunting or from event outcomes. Uses boldness-based AI:

- Boldness (0-1) — calculated from context: player injured, carrying meat, low vitality
- Distance tracking — predator closes or backs off based on player actions
- Player options: stand ground, back away, run, fight, drop meat

The encounter emerges from intersecting state: player carrying meat + injury slowing movement + predator's boldness calculation. Each turn gives the player a chance to respond.

Combat is simple turn-based when it occurs: attack with equipped weapon, predator attacks back.

Predator encounters interact with: events (can spawn encounters), body system (injuries affect options), inventory (carrying meat affects boldness, weapons affect combat), effects (fear, injuries from attacks).

---

## Items and Inventory

Hybrid approach:

Aggregate resources — stored as stacks tracking individual weights:
- Fuel: Sticks, Logs (Pine/Birch/Oak), Tinder, BirchBark, Charcoal
- Food: Raw meat, Cooked meat, Dried meat, Berries, Nuts, Roots
- Materials: Stone, Bone, Hide, PlantFiber, Sinew, Flint, Shale, Pyrite, Rope, Tallow
- Medicine: Various fungi, bark, resin, moss
- Water (in liters)

Discrete items — tracked individually using unified Gear system:

**Gear** — unified discrete item system with three categories:
- **Tools** — cutting tools, hunting weapons, fire starters, containers, shovels. Have durability. Some are dual-purpose weapons.
- **Equipment** — clothing worn in slots (Head, Chest, Legs, Feet, Hands). Provide insulation that degrades with condition. Equipment wears out over time.
- **Accessories** — carrying capacity boosters (pouches, belts, bags). Stack additively.

All gear shares unified durability system. Condition percentage affects equipment insulation. Creates pressure loop: clothing wears out → need to craft replacements.

Weapon system — weapons are tools with combat properties. Dual-purpose tools (axe, knife, spear) serve as both tools and weapons.

Dual inventory:
- Player inventory — weight-limited (base capacity + accessory bonuses), carried during expeditions
- Camp storage — unlimited capacity, accessible only at camp

Accessory system enables progression: start with limited capacity → craft bags to expand → longer expeditions possible.

Inventory interacts with: crafting (materials consumed, items produced), expeditions (carry weight affects travel), survival simulation (food/water consumption, insulation), events (costs and rewards), predator encounters (meat attracts, weapons enable combat).

**Files**: `Items/Gear.cs`, `Items/Inventory.cs`

---

## Crafting

Need-based system. Player expresses a need, sees what's craftable from available materials.

Need Categories:
- **Fire-starting** — Hand drills, bow drills, strikers. Different materials affect durability and ignition bonus.
- **Cutting tools** — Sharp rocks, knives (stone/bone/flint). Better materials last longer.
- **Hunting weapons** — Spears (wooden, heavy, stone-tipped). Progression through materials.
- **Trapping** — Snares (simple, reinforced). For passive small game hunting.
- **Processing** — Hide scraping, fat rendering, fiber processing, rope making. Transform raw materials.
- **Treatment** — Teas, poultices, dressings, seals. Medical items from foraged medicines.
- **Equipment** — Hide clothing (gloves, caps, wraps, leggings, boots). Insulation from cured hides.
- **Lighting** — Torches (simple, birch bark, resin). Portable light and warmth.
- **Carrying** — Pouches, belts, bags. Expand carrying capacity.

Materials come from foraging (stone, plant fiber, medicines), butchering (bone, hide, sinew, fat), and processing (rope, tallow, cured hide).

Can also craft features: curing racks, shelters, camp improvements. Multi-session projects via CraftingProjectFeature.

Tools have durability. Equipment provides insulation that degrades with condition. Different materials affect durability, not effectiveness.

Crafting interacts with: inventory (materials consumed, items produced), features (butchering provides materials, can craft features), locations (foraging provides materials), survival simulation (equipment insulation), effects (treatments).

**Files**: `Crafting/NeedCraftingSystem.cs`, `Crafting/NeedCategory.cs`

---

## Architecture

Runners — Control flow, player decisions, display UI
- GameRunner: main camp loop
- TravelRunner: movement between locations
- WorkRunner: all work activities (uses strategy pattern)
- CraftingRunner: need-based crafting UI
- HuntRunner: interactive hunt sequences (creates CarcassFeature on kill)
- EncounterRunner: predator encounters (creates CarcassFeature on victory)

Handlers — Activity-specific execution logic (static classes)
- FireHandler: fire starting, tending, fuel management
- TorchHandler: lighting, chaining, extinguishing
- CookingHandler: food preparation at fire
- ConsumptionHandler: eating, drinking
- TreatmentHandler: medical treatment application
- CampHandler: sleep, rest, camp improvements
- TravelHandler: movement between locations
- HuntHandler: hunting sequences
- CombatHandler: predator combat
- CuringRackHandler: hide/meat preservation

Handlers take `GameContext`, mutate state directly, handle player choices via `Input`. Runners orchestrate flow; handlers execute specific actions.

Work Strategies — `IWorkStrategy` implementations for each work type:
- ForageStrategy, HuntStrategy, HarvestStrategy, ExploreStrategy
- TrapStrategy (set/check modes), ChoppingStrategy, CacheStrategy
- CraftingProjectStrategy, SalvageStrategy, ButcherStrategy
- Each strategy provides: location validation, time options, impairment calculations, execution logic

GameContext — Central hub holding game state
- Player, Camp, Inventory, CurrentLocation (no expedition state object)
- Tensions, Weather, Locations
- Condition checking for events
- Update methods that tick time forward with event interruption

Activity Configuration — defines behavior for each activity type:
- Event multiplier (how often events trigger)
- Activity level (calorie burn multiplier)
- Fire proximity (heat bonus when near fire)
- Status text
- Creates tradeoffs: safe at camp vs. productive in field

Processors — Stateless domain logic, returns results
- SurvivalProcessor: calculates stat changes per tick
- ButcheringProcessor: calculates yields from animals
- TravelProcessor: calculates travel times
- DamageCalculator: calculates damage distribution

Data Objects — Hold state, minimal behavior
- Body, Location, Camp, Features, Gear

Update Flow:
```
Action executes
    → GameContext.Update(N minutes, activity type)
        → Per-minute loop
            → Calculate survival context (temp, wetness, insulation)
            → Player.Update()
                → EffectRegistry.Update()
                → SurvivalProcessor calculates
                → Body applies changes
            → Locations update (fire burns, features tick)
            → Tensions decay
            → Event check (can interrupt)
        → Handle triggered event
        → Spawn queued encounter
```

**Files**: `Actions/GameContext.cs`, `Config/ActivityConfig.cs`, `Actions/Expeditions/WorkStrategies/`

---

## Web UI

WebSocket-based communication between C# backend and browser frontend. Backend sends `WebFrame` DTOs; frontend renders them.

Mode + Overlay pattern separates UI states:
- **Modes** (mutually exclusive) — LocationMode (at camp), TravelMode (on expedition with grid), ProgressMode (animated activity in progress)
- **Overlays** (stackable) — InventoryOverlay, CraftingOverlay, EventOverlay. Multiple can be active simultaneously.

Frame structure:
- `State` — current game state (stats, weather, location, fire, etc.)
- `Mode` — which primary UI to show
- `Overlays` — which modal panels to display
- `Input` — action buttons and their handlers

FrameQueue handles rapid frame arrivals during travel. State machine: idle → processing → animating → idle. Progress animations block queue processing until complete.

JSON serialization uses `[JsonPolymorphic]` attributes for type discrimination. Frontend dispatches on `mode.type` and `overlay.type` via switch statements.

Web UI interacts with: all game systems (receives state updates), events (EventOverlay shows choices), inventory/crafting (overlay display).

**Files**: `Web/Dto/WebFrame.cs`, `Web/Dto/FrameMode.cs`, `Web/Dto/Overlay.cs`, `Web/WebIO.cs`, `wwwroot/app.js`, `wwwroot/modules/frameQueue.js`

---

## Design Direction

Not yet implemented, but shaping future development:

The mountain crossing — Win condition. Requires serious preparation (warmth, supplies, condition). Multi-day expedition.

Megafauna hunts — Trophy hunts that provide materials for gear required for the crossing.

Exploration areas — Distant, dangerous locations with unique rewards.

Camp investment — Persistent improvements that make a camp worth defending.