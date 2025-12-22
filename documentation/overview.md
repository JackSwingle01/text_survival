# Text Survival — Systems Overview

*A console-based survival game set in an Ice Age world. This document describes the core systems and how they interact.*

---

## Fire

Physics-based simulation. Fire is infrastructure — a good fire with quality fuel extends your expedition radius. A dying fire while you're away means returning to cold camp.

- Fuel types with different burn rates and heat output
- Fire phases: Igniting → Building → Roaring → Steady → Dying → Embers
- Ember preservation for relight without fire-starting tools
- Heat contribution to location temperature based on fuel mass and fire maturity

Fire interacts with: survival simulation (temperature), expeditions (time pressure), locations (heat source feature).

---

## Expeditions

Flexible travel structure:

- Leave camp — choose a destination, travel there
- Work or travel — at each location, choose to work (forage, hunt, explore, harvest) or travel to another connected location
- Return — backtrack through travel history to camp

Fire status always visible. Travel times calculated from location graph. The UI shows what you need to judge your margin.

Expeditions interact with: fire (time pressure), locations (destinations and features), events (triggers during travel and work), survival simulation (stats drain during travel).

---

## Locations

Locations are places in the world with travel times from camp. Each has:

- Name and character (Frozen Creek, Dense Birches, Rocky Overlook)
- Travel time from connected locations
- Features (what you can do there)
- Terrain properties (exposure, shelter potential)

Locations connect as a graph. Camp is the anchor point.

Discovery states:
- Unexplored — can travel there but don't know what's there (shown as hints like "rocky area to the north")
- Explored — full features revealed after visiting

Depletion: Areas run out. Nearby grove is plentiful on day one, sparse by day three, picked over by day five. Pushes the player outward or forces camp relocation.

Locations interact with: expeditions (travel destinations), features (what's available), events (location-based triggers), exploration (discovery of new locations).

---

## Features

Features are what make locations useful. They live on locations and define available activities.

ForageFeature — Ambient scavenging. Search the area, find things based on abundance and time invested. Yields diminish as you exhaust an area.

HarvestableFeature — Visible specific resources. A berry bush with 12 servings. A dead tree you can process. Quantity-based, not probability-based.

EnvironmentFeature — Terrain properties. A cave has wind protection. A ridge is exposed but offers visibility.

ShelterFeature — Built protection. Lean-to, snow wall, debris hut. Takes time to build, stays when you leave.

HeatSourceFeature — Fire. Has fuel, burns down, produces heat. Can bank to embers, relight, die completely.

AnimalTerritoryFeature — What game can be found here. Animals spawn dynamically when searching, not pre-placed. Game density depletes with successful hunts and respawns over time.

Features interact with: locations (features live on locations), expeditions (work types use features), events (features can trigger events and be modified by event outcomes), crafting (features provide materials).

---

## Survival Simulation

Rate-based calculation per minute:

- Energy — depletes with time and exertion
- Hydration — constant drain, faster with heat/exertion
- Calories — BMR-based with activity multipliers
- Temperature — heat transfer toward environment, modified by clothing, shelter, fire

Thresholds trigger effects. Temperature drops below 95°F, you start shivering. Below 89.6°F, frostbite risk.

The survival processor returns stat changes, triggered effects, and messages — it doesn't own state, just calculates.

Survival simulation interacts with: body system (stats live on body), effects (thresholds trigger effects), fire (heat contribution), locations (environmental exposure), expeditions (activity multipliers).

---

## Body System

Hierarchical body parts for humans (Torso → Heart, Arm → Hand → Fingers). Each part contributes to capacities:

- Moving — legs, feet, knees
- Manipulation — hands, fingers, arms
- Breathing — lungs, chest
- Consciousness — head, brain
- Plus: BloodPumping, Sight, Hearing, Digestion

Injuries attach to body parts with severity (0-1). Capacity calculation is generic — sum injuries, check which capacities they affect, apply modifiers. No special cases per body part.

Body composition (fat, muscle mass in kg) affects temperature resistance, speed, strength. Calories convert to fat at 7700 kcal per kg.

Body system interacts with: survival simulation (stats live here), effects (injuries trigger effects), damage (structural harm to parts), abilities (capacities determine what player can do).

---

## Effects

Effects are ongoing processes that tick over time. Distinct from body damage.

Body Damage = Structural
- Entry point: `Body.Damage(DamageInfo)`
- Tracks tissue condition (skin, muscle, bone, organs) at 0-1
- Affects capacities based on which parts are damaged
- Heals automatically when well-fed, hydrated, rested

Effects = Processes
- Entry point: `EffectRegistry.AddEffect(effect)`
- Ongoing conditions that tick over time
- Have severity (0-1), decay rates, capacity/stat modifiers
- Resolve through natural decay or treatment

A wolf bite causes BOTH body damage (structural injury to leg) AND a bleeding effect (ongoing blood loss). Stopping bleeding doesn't heal the leg. Healing the leg doesn't stop bleeding.

Available effects: Cold, Hypothermia, Hyperthermia, Shivering, Sweating, Frostbite, Bleeding, Sprained Ankle, Fear/Shaken. Also positive buffs from event outcomes.

Effects interact with: body system (damage vs process distinction), survival simulation (thresholds trigger effects), events (outcomes apply effects), abilities (effects modify capacities).

---

## Events

Events trigger during expeditions based on context:

- Location (wolf territory, terrain type)
- Player activity (hunting, foraging, traveling)
- Player state (injured, carrying food, bleeding)
- Time and weather
- Active tensions

Events aren't random encounters — they're contextual interrupts that create decisions.

Architecture: Events use a factory pattern. Each event type is a function that takes `GameContext` and returns a `GameEvent`. Events bake location-specific context into descriptions at trigger time.

Outcomes can:
- Add time to expeditions
- Apply effects or injuries
- Grant rewards via RewardPools
- Consume resources as costs
- Create, escalate, or resolve tensions
- Spawn predator encounters
- Damage tools or clothing
- Discover new locations

Events interact with: tensions (create/escalate/resolve), locations (triggers and discovery), effects (outcomes apply them), predator encounters (can spawn them), inventory (costs and rewards).

---

## Tensions

Tensions represent unresolved narrative threads that persist across events.

Examples:
- Stalked — a predator is following the player
- Infested — vermin in camp storage
- WoundUntreated — injury needs treatment
- FoodScentStrong — cooking/butchering attracted attention
- Hunted — active predator pursuit (escalated Stalked)

Each tension has:
- Type — identifier
- Severity — 0-1 scale affecting event weights
- Decay rate — how fast it fades per hour
- Camp behavior — whether it decays at camp

Lifecycle: Events create tensions → subsequent events can escalate → tensions resolve through player action, event outcomes, or natural decay → multiple active tensions compound pressure.

Tensions interact with: events (tensions modify event weights, events modify tensions), expeditions (some tensions decay differently at camp vs field).

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

Aggregate resources — stored as lists of weights:
- Fuel: Sticks, Logs, Tinder
- Food: Raw meat, Cooked meat, Berries
- Materials: Stone, Bone, Hide, PlantFiber, Sinew
- Water (in liters)

Discrete items — tracked individually:
- Tools (with type and durability)
- Equipment (clothing with insulation values)

Dual inventory:
- Player inventory — weight-limited (15kg), carried during expeditions
- Camp storage — unlimited capacity, accessible only at camp

Inventory interacts with: crafting (materials consumed, items produced), expeditions (carry weight affects travel), survival simulation (food/water consumption), events (costs and rewards), predator encounters (meat attracts, weapons enable combat).

---

## Crafting

Need-based system. Player expresses a need, sees what's craftable from available materials.

Need Categories:
- Fire-starting supplies (Hand Drill, Bow Drill)
- Cutting tools (Sharp Rock, Stone Knife, Bone Knife)
- Hunting weapons (Wooden Spear, Heavy Spear, Stone-Tipped Spear)

Materials come from foraging (stone, plant fiber) and butchering (bone, hide, sinew).

Binary tools: Tools either work or they're broken. No quality tiers. Different materials affect durability, not effectiveness.

Crafting interacts with: inventory (materials consumed, items produced), features (butchering provides materials), locations (foraging provides materials).

---

## Architecture

Runners — Control flow, player decisions, display UI
- GameRunner: main camp loop
- ExpeditionRunner: leaving camp, traveling, working, returning
- WorkRunner: forage, hunt, explore, harvest activities
- CraftingRunner: need-based crafting UI

GameContext — Central hub holding game state
- Player, Camp, Inventory, Expedition (when active), Zone
- Condition checking for events
- Update methods that tick time forward

Processors — Stateless domain logic, returns results
- SurvivalProcessor: calculates stat changes per tick
- ButcheringProcessor: calculates yields from animals
- TravelProcessor: calculates travel times

Data Objects — Hold state, minimal behavior
- Body, Expedition, Location, Camp, Features

Update Flow:
```
Action executes
    → GameContext.Update(N minutes)
        → Player.Update()
            → EffectRegistry.Update()
            → SurvivalProcessor calculates
            → Body applies changes
        → Zone.Update()
            → Locations update (fire burns, features tick)
```

---

## Design Direction

Not yet implemented, but shaping future development:

The mountain crossing — Win condition. Requires serious preparation (warmth, supplies, condition). Multi-day expedition.

Megafauna hunts — Trophy hunts that provide materials for gear required for the crossing.

Exploration areas — Distant, dangerous locations with unique rewards.

Camp investment — Persistent improvements that make a camp worth defending.