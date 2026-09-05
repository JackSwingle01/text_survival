# Text Survival — Systems Overview

*A web-based survival game set in an Ice Age world. This document describes the core systems and how they interact.*

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

Can be lit from fire, another torch, or firestarter+tinder. Limited burn time. A guttering torch away from a fire chains onto a fresh one automatically.

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
- Available work types: Forage, Hunt, Harvest, Trap, Chop, Cache, CraftingProject, Salvage, Butcher
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

**Scale**: Each tile represents approximately **100 meters** of terrain. The grid is 96x96 tiles (~9.6km x 9.6km). Travel time across a tile is based on ~80m/min walking speed, giving a ~1-minute base traversal for flat terrain, scaled up for difficult terrain types.

Core operations:
- `CurrentPosition` — player's (X, Y) coordinates
- `CurrentLocation` — derives from `_locations[CurrentPosition.X, CurrentPosition.Y]`
- `GetTravelOptions()` — adjacent passable locations from current position
- `MoveTo(location)` — updates position and visibility

Visibility system:
- `TileVisibility`: Unexplored → Explored → Visible
- Sight range calculated from current location's visibility factor (0-20 tiles)
- Moving updates which tiles are visible vs merely explored

Travel uses cardinal directions (N/S/E/W). Locations connect implicitly by adjacency, not explicit edges.

Grid interacts with: locations (grid contains them), travel (adjacency determines options), UI (grid rendering in TravelMode).

**Files**: `Environments/Grid/GameMap.cs`, `Environments/Grid/GridPosition.cs`

---

## Tracks and Trails

The ground remembers who walked on it, on two clocks.

`GameMap.RecordMove(from, to, maker, individuals, individualDepth)` is the single seam for
a completed crossing, whoever made it — the player via `GameMap.MoveTo`, NPCs via
`NPCMove.Complete` and their flee path, herds via `Herd.UpdateTravel`. Both systems below
are fed from that one call, and `GameMap.AdvanceGround` ages both once per tick.

**Footprints** (`TrackRegistry`) — Three makers by *shape*, not identity: Human, Paw,
Hoof. The player's boots and an NPC's leave the same mark and the player is trusted to
remember where they have been; what a print tells you is whether the thing that passed
walks on two feet, on pads, or on hooves.

Each track carries `Traffic` — how many individuals came through, accumulated across
passages and faded by weather in between, so three passes by one person and one pass by
three people read alike, which is also true on the ground. Clicking a tile reports it:
`Paw prints x5 - fresh, heading north`.

Rather than ageing every track every minute, the world keeps **one monotonic erosion
accumulator** that advances faster in snow, wind, and thaw. A track stores the
accumulator's value when stamped, so its age is one subtraction — and a blizzard that blew
through while the player was elsewhere is already accounted for, because it advanced the
same counter. One addition per minute, whatever the size of the map. Roughly four days in
dead calm cold, six hours in a blizzard, for one person's prints. `Depth` is derived, not
stored: linear in the weight of the heaviest thing through, sublinear in how many came, so
a herd's churn outlasts a lone animal's without lasting twelve times as long.

Prints render only within a short radius that widens with Perception — sight range reaches
twenty tiles, but nobody reads paw prints from two kilometres away. They also feed
`TrailSignSelector`, so "fresh wolf scat, still warm" appears because a wolf herd really
did pass, and the sign's age matches what the prints say.

**Desire paths** (`TrailWear`) — Every crossing adds wear to that edge; tiers are *derived*
from the scalar, never stored: None → Trace (visible, no faster) → Path (−1 min) → Trail
(−2 min). One adult human adds 1, so thresholds (5 / 14 / 35) read roughly as net
crossings. Wear is shared by both directions and only accrues between adjacent tiles —
herd patrol can hop across its territory, and crediting one arbitrary edge for a multi-tile
move would wear in a route nothing walked.

**Wear is linear in head count**, which is what makes herds work: every set of feet treads
the ground. A lone bear never forms a trace; a dozen caribou lay a Path in eight days and a
Trail in nineteen; a wolf pack of five gets there in about six weeks. Head count matters
more than bulk — three mammoths are slower to beat a route in than twelve caribou.

Decay has two named terms, deliberately weighted toward the first: settling and regrowth
per minute (~0.5 wear/day, so an abandoned Path is gone in about a month), plus a smaller
contribution from erosion as snow fills the trench. A single term cannot do this job —
erosion swings 16x between calm and blizzard, so any one constant either lets storms erase
weeks of walking or leaves calm weather never reclaiming anything. Two terms decouple the
reclaim timescale from the weather response: weather buries a path without undoing it, and
five unbroken days of blizzard costs a Path its speed bonus but not its existence.

`EdgeType.GameTrail`, `TrailMarker`, and `CutTrail` remain separate authored and
player-built edges; desire paths are the emergent sibling of the trail you cut by hand with
an axe, and both reach travel time through `GameMap.GetEdgeTraversalModifier`.

Tracks and trails interact with: travel (edge modifiers reach players, NPCs and herds alike
through `TravelProcessor.GetTraversalMinutes`), weather (erosion), herds and NPCs (they mark
the ground too), events (trail signs read real prints), rendering (`TrackRenderer`,
`EdgeRenderer`), the tile popup (`TilePopup.RenderTracks`).

**Files**: `Environments/Grid/TrackRegistry.cs`, `Environments/Grid/TrailWear.cs`,
`Desktop/Rendering/TrackRenderer.cs`, `assets/pixelart/tracks/`

---

## Features

Features are what make locations useful. They live on locations and define available activities.

**ForageFeature** — Ambient scavenging. Search the area, find things based on abundance and time invested. Depletion system with respawn. Resource types include medicinal plants, fungi, tree products.

**HarvestableFeature** — Visible specific resources. A berry bush, a dead tree. Quantity-based, not probability-based. Tool tier gating — some resources require better tools. Individual resource respawn timers.

**SmallGameFeature** — Ambient small game (rabbit, ptarmigan, fox, fish) as a density, like forage. Animals spawn when searched for, density depletes with kills and respawns over weeks, peak hours raise encounter rates. Large animals are never here: they live in herds. `AnimalPresence` is the one place that answers "what animals are near the player" by combining herds and small game; events, conditions, and work options all ask it.

**WoodedAreaFeature** — Tree chopping mechanic. Work accumulates until tree fells. Progress persists across sessions. Can specify wood type (Pine/Birch/Oak) or mixed forest. Trees respawn slowly.

**SnareLineFeature** — Trapping system. Manages multiple placed snares at a location. Snare states: Active, CatchReady, Stolen, Destroyed. Durability system, can be reinforced with bait. Small game only. Passive catching over time.

**CacheFeature** — Remote storage for expeditions. Natural (found) or Built types. Capacity limits vary. Special properties: predator protection, weather protection, food preservation.

**CuringRackFeature** — Food and hide preservation. Process items over time. Transforms raw → preserved versions with weight loss during drying.

**WaterFeature** — Frozen water bodies. Ice thickness affects hazard level. Ice hole cutting for fishing/water access. Holes refreeze over time.

**BeddingFeature** — Sleep quality system. Quality affects sleep efficiency. Wind protection and ground insulation properties. Warmth bonus during sleep.

**CraftingProjectFeature** — Multi-session construction. Tracks time invested and materials consumed. Progress persists. Material reclaim if abandoned. Some projects benefit from tools (shovel for digging).

**ShelterFeature** — Built protection. Temperature insulation, overhead coverage, wind coverage. Snow shelters degrade in warm temps. Damage/repair system. A crafted tent is a portable shelter: pitch it anywhere from the action panel and pack it up again to carry on (`CampHandler.DeployTent`/`PackTent`).

**HeatSourceFeature** — Fire. See Fire section above.

**CarcassFeature** — Dead animal carcasses awaiting butchering. Created by successful hunts, combat victories, and some events (like following ravens). Carcasses decay over time (fresh → good → questionable → spoiled) affecting meat yield. Contains all butchering logic: yields based on animal size and composition, tool checks (no knife = reduced yield), manipulation impairment penalties. Butchered via ButcherStrategy work type. Creates time pressure to return and process before spoilage.

Features interact with: locations (features live on locations), expeditions (work types use features), events (features can trigger events and be modified by event outcomes), crafting (features provide materials), survival simulation (shelter and heat).

**Files**: `Environments/Features/` (various feature implementations)

---

## Discovery System

Hidden features are generated at location creation and revealed through foraging. Players don't know what's available until they search — exploration rewards time investment.

Locations store both visible `Features` and hidden `HiddenFeatures`. Foraging accumulates discovery progress (perception-weighted hours). When progress exceeds a feature's threshold, it moves from hidden to visible.

**Always visible:**
- `ForageFeature` — Base foraging mechanic
- `WoodedAreaFeature` — Trees are obviously visible

**Hidden until discovered:**
- `HarvestableFeature` — Berry bushes, deadfall, flint outcrops
- `ShelterFeature` — Rock overhangs, natural shelters
- `SmallGameFeature` — Game trails, hunting grounds
- `EnvironmentalDetail` — Fallen logs, animal tracks, stone piles
- `EventTriggerFeature` — One-time discovery events (abandoned camps, frozen travelers)

Each terrain type has discovery pools. Reveal thresholds use exponential distribution — 50% found before expected time, with variance creating unpredictability. Environmental details are quick finds (15-30 min). Significant features take longer (flint outcrops 2-2.5 hours, rock overhangs 3-4 hours).

**EnvironmentalDetail lifecycle** — Hidden → Discovered (via foraging) → Used (player interacts, detail removed). One-time finds like gathering sticks from a fallen log or examining animal tracks.

**EventTriggerFeature** — When revealed, immediately triggers a discovery event via `DiscoveryEventFactory` then removes itself. Creates significant moments: finding an abandoned campsite, a frozen traveler, a hidden cache, a bear's food store. These are authored events placed in the world and revealed through exploration.

Discovery progress is separate from resource depletion — foraging can yield both resources and discoveries. Perception ability speeds discovery. Seeded generation ensures consistent discoveries per location across saves.

Discovery system interacts with: locations (storage), foraging (revelation), events (discovery events), perception (discovery speed).

**Files**: `Environments/Features/HiddenFeature.cs`, `Environments/Factories/DiscoveryGenerator.cs`, `Actions/Events/DiscoveryEventFactory.cs`

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

### Abilities

Abilities provide context-aware performance calculations built on top of capacities. While capacities measure raw body function, abilities incorporate environmental factors:

- **Vitality** — Overall life force (min of breathing, blood pumping, consciousness)
- **Strength** — Power output (vitality, body composition)
- **Speed** — Movement rate (encumbrance, vitality, strength)
- **Perception** — Awareness (darkness, consciousness, vitality)
- **Dexterity** — Fine motor control (manipulation, darkness, wetness, vitality)
- **ColdResistance** — Temperature tolerance (body fat with diminishing returns)

Abilities are used for performance calculations throughout the game: foraging yield, fire-starting chance, travel time, combat effectiveness. The key distinction: **threshold checks use Capacities** (e.g., "Is consciousness < 0.3?"), while **performance calculations use Abilities** (e.g., `yield = baseYield * perception`).

See [abilities-system.md](abilities-system.md) for comprehensive documentation.

**Files**: `Bodies/AbilityCalculator.cs`, `Bodies/AbilityContext.cs`

---

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

Architecture: `GameEvent` contains `EventChoice` objects with `EventResult` outcomes. `GameEventRegistry` (partial class across ~19 files) builds events with context-aware descriptions.

Two triggering paths:
- **Random events** — Base roll per minute → weighted selection from eligible events. Context affects both frequency and which events are eligible.
- **Discovery events** — Triggered deterministically when `EventTriggerFeature` is revealed through foraging. Guaranteed significant moments (abandoned camps, frozen travelers, hidden caches) placed in the world via `DiscoveryEventFactory`.

**Modular building blocks** — Three abstractions enable extensible event authoring:
- **Situations** — Compound predicates for *when* events trigger
- **Variants** — Text bundles that match *descriptions* to mechanics
- **Outcome Templates** — Reusable patterns for *what happens*

Adding a new system (e.g., wetness) means updating Situations once — all events using that situation automatically respond. New injury types get one variant pool — all accident events can use it. Common outcome patterns get one template — consistent behavior across events.

Event organization — narrative arcs: Weather, Expedition, Camp, Threat, Herd, Trapping, Location-specific, and multi-stage arcs (Cold Snap, Wound/Infection, Disturbed, Den claim, Pack hunting, Fever).

### Situations

Compound predicates that encapsulate complex game state checks. Events use semantic predicates instead of raw condition checks.

```csharp
if (Situations.Vulnerable(ctx) && Situations.UnderThreat(ctx))  // Crisis
if (Situations.AttractiveToPredators(ctx))  // Meat, bleeding, scent
```

Categories:
- **Predator attraction** — `AttractiveToPredators`, `PredatorAttractionLevel` (meat, bleeding, bloody, scent)
- **Vulnerability** — `Vulnerable`, `VulnerabilityLevel` (injured, slow, impaired, no weapon, blood loss)
- **Resource pressure** — `SupplyPressure`, `ResourceScarcity` (low fuel/food/water, depleted locations)
- **Exposure** — `Exposed`, `HarshConditions`, `ExtremeColdCrisis` (weather + shelter state)
- **Danger** — `UnderThreat`, `InCrisis`, `PackThreat` (tension-based compound states)
- **Favorable** — `FavorableConditions`, `WellEquipped`, `HuntingAdvantage`, `GoodForStealth`

Graduated levels (0-1) enable weight multipliers: `PredatorAttractionLevel(ctx) * 0.5` for event weighting.

### Variants

Ensure event text matches mechanics. Three variant types bundle descriptions with their mechanical effects.

**Injury Variants** — Text + body target + damage type. "Your foot catches" only plays when damage targets legs.

Pools: TripStumble, SharpHazards, IceSlip, RockyTerrain, ClimbingFall, FallImpact, Sprains, DarknessStumble, DebrisCuts, VerminBites, CollapseInjuries, EmberBurns. `VariantSelector` weights by context.

**Discovery Variants** — Text + reward pool. Generic descriptions match generic pools.

Pools: SupplyFinds, TinderFinds, MaterialFinds, BoneFinds, CampFinds, CacheFinds, SmallGameFinds, HideFinds.

**Illness Variants** — Symptoms tied to causes for player learning. Onset pools by cause (WoundOnset, ExposureOnset, ContaminationOnset, ExhaustionOnset). Hallucination pools weight toward real threats — sometimes the fever dream is real.

### Outcome Templates

Extension methods on `EventResult` for fluent chaining. Encode common patterns once.

```csharp
new EventResult("description", 0.5, 10)
    .ModerateCold()           // -12°C for 45 min
    .Frightening()            // Fear 0.3
    .BecomeStalked(0.4)       // Creates tension
```

Categories:
- **Cold/Weather** — `MinorCold`, `SevereCold`, `StormExposure`, `SoakedAndCold`, `FellThroughIce`
- **Fear** — `Unsettling`, `Frightening`, `Terrifying`, `Panicking`, `Shaken`
- **Damage** — `MinorFall`, `MinorBite`, `AnimalAttack`, `Mauled`, `MinorFrostbite`
- **Scent** — `MinorBloody`, `ModerateBloody`, `HeavilyBloody`
- **Costs/Rewards** — `StartsFire`, `BurnsFuel`, `FindsSupplies`, `FindsMeat`, `FindsCache`
- **Tensions** — `BecomeStalked`, `EscalatesStalking`, `ResolvesStalking`, `MarksDiscovery`
- **Compound** — `EscapeToCamp`, `FireScaresPredator`, `ColdAndFear`, `InjuredRetreat`
- **Equipment** — `DamagesEquipment`, `MinorEquipmentWear`, `FieldRepair`

Events interact with: tensions (create/escalate/resolve), locations (triggers and discovery), effects (outcomes apply them), predator encounters (can spawn them), inventory (costs and rewards), features (can modify), discovery system (discovery events).

**Files**: `Actions/Events/Situations.cs`, `Actions/Events/Variants/`, `Actions/Events/OutcomeTemplates.cs`, `Actions/Events/DiscoveryEventFactory.cs`, `Actions/GameEvent.cs`

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

## Combat System

Grid-based tactical combat. Every fight in the game runs on it: the player's hunts and encounters, NPCs defending themselves, and packs pulling down prey.

**Architecture** — One scenario, one setup, one aftermath:
- `CombatScenario.Create` — The only way to build a fight: two teams, a location, an opening distance, and an awareness state per side. A hunt is an Engaged player against Unaware prey; an ambush is the reverse; a brawl is Engaged on both sides.
- `CombatScenario` — State, rules, action execution, AI turns. `ResolveHeadless` runs a fight with no player in it on the same grid and AI.
- `CombatOrchestrator` — The player's turn loop and UI. `RunHunt`, `RunEncounter`, and `ResolveHeadless` are the entry points.
- `CombatAftermath.Apply` — Everything the world remembers afterward: carcasses and bodies, herd losses, fear, feeding and flight, wounded prey that got away, small game depletion, relationship memory, hunting experience.

**Entry Points:**
- **Hunting** — `HuntRunner` prompts the approach, then `CombatOrchestrator.RunHunt`
- **Encounters** — Herd behaviors and event outcomes queue an `EncounterConfig`; `GameContext` runs it through `CombatOrchestrator.RunEncounter`. A herd sends one of its own members, so kills thin the pack.
- **NPC and herd fights** — `NPCFight` and `PackPredatorBehavior` call `CombatOrchestrator.ResolveHeadless`

**Combat Grid** — 25x25 meter tactical grid (1m per cell):
- Units positioned at grid coordinates
- Distance determines available actions
- Movement costs action, affects morale
- Map edges allow fleeing

**Distance Zones:**
- **Close (0-1m)** — Melee range, grappling distance
- **Near (1-3m)** — Primary combat zone, weapon strike range
- **Mid (3-15m)** — Throwing range, intimidation
- **Far (15-25m)** — Standoff distance, approach or disengage

**Player Actions by Zone:**
- **Close:** Attack, Block, Shove, Retreat
- **Near:** Attack, Dodge, Block, Advance, Retreat
- **Mid:** Throw (if weapon), Intimidate, Advance, Retreat
- **Far:** Intimidate, Advance, Retreat

**Combat Actions:**
- **Attack** — Melee strike, 90% base hit chance
- **Throw** — Ranged attack, weapon lost, hit chance decreases with distance
- **Dodge** — Set defensive stance, costs energy, pushes back 1m on success
- **Block** — Reduce incoming damage with weapon (damages weapon durability)
- **Shove** — Push enemy back based on strength/weight ratio
- **Intimidate** — Lower enemy boldness, may cause retreat
- **Advance/Retreat** — Move 3m toward/away from nearest enemy

**Morale & Boldness** — AI behavior driven by boldness (0-1):
- Whether a herd engages at all is `Herd.BoldnessToward(target)`: species `Temperament`, pack size, hunger, what it is defending, the target's vulnerability, the hour, and learned fear. The same number seeds the animals' morale when the fight opens.
- Combat events modify boldness (damage taken/dealt, allies killed, enemy retreat, etc.)
- Low boldness (<0.3) causes AI to flee
- High boldness (>0.7) makes AI aggressive
- Affects AI action selection via `CombatAI`

**Team Combat** — Supports multiple participants:
- Player can have NPC allies (NPCs decide to help via `DecideToHelpInCombat`)
- Animals can have pack members from their herd (0-3 random members)
- Allies/enemies tracked per unit
- Morale cascades affect team members

**AI Behavior** — `CombatAI` determines actions:
- Boldness-based decision making
- Distance-aware action selection
- Target selection (weakest enemy, or nearest if none wounded)
- Movement positioning (advance when bold, retreat when scared)

**Combat Resolution** (`CombatResult`, from team A's point of view):
- **Victory** — All enemies dead
- **Defeat** — Player killed, or team A dead
- **Fled** — Team A left the field
- **Animal Fled** — Team B left the field
- **Animal Disengaged** — Stand-off or headless round cap

**Damage System** — Integrated with body system:
- Damage affects body parts, organs, blood
- Injuries reduce abilities (movement, manipulation, consciousness)
- Animal vitality shown descriptively (healthy, wounded, badly hurt, near death)

Combat interacts with: events (can spawn combat), body system (damage and abilities), inventory (weapons, meat affects boldness), NPCs (allies), herds (pack members), features (carcasses on victory), abilities (speed for dodge, strength for shove).

**Files:** `Combat/CombatOrchestrator.cs`, `Combat/CombatScenerio.cs`, `Combat/CombatAftermath.cs`, `Combat/CombatAI.cs`, `Combat/CombatFormulas.cs`

---

## Herds

Animal groups that move as unified entities within home territories. Creates a living world where animals graze, patrol, and hunt each other.

Three behavior types (strategy pattern):
- **Prey** (caribou, bison, megaloceros) — Graze when hungry, rest when satiated, flee from threats
- **Pack predators** (wolves) — Patrol territory, hunt NPC prey, engage player based on boldness
- **Solitary predators** (bears) — Forage as omnivores, highly territorial near den

Hunger drives behavior transitions: Resting → Grazing/Patrolling → Hunting/Feeding. Grazing depletes ForageFeature resources based on diet (browsers eat lichens, grazers eat grass, omnivores eat berries/fungi). Herds leave depleted areas faster — competing with player for forage.

Wounded animals split into trackable single-animal herds. Pack hunts run on the combat grid: `HerdVigilance` decides whether the prey noticed the pack, which sets how the fight opens, and `CombatAftermath` leaves the carcass the pack then defends.

Wolf dens and bear caves are authored locations; `HerdPopulator` anchors a real pack or bear on each one. Megafauna (mammoth, saber-tooth) are herds too, and their scout/track/approach work options come from the herd being near plus the hunt tension. A dead megafauna herd stays dead for the run.

Herds interact with: locations (territory spans tiles), features (grazing depletes ForageFeature), hunting (HuntStrategy searches herds), events (herd arc triggers), tensions (HerdNearby, WoundedPrey), encounters (predators engage player).

**Files**: `Actors/Animals/Herd.cs`, `Actors/Animals/HerdExtensions.cs`, `Actors/Animals/Behaviors/`

---

## NPC Allies

Autonomous survival agents with full needs-based decision making. NPCs pursue their own survival independently while sharing camp with the player.

**Autonomous Behavior:**
- **Need hierarchy** — Warmth > Water > Rest > Food > Work. NPCs interrupt current actions when critical needs arise.
- **Resource gathering** — Forage ambient resources, harvest from bushes/trees, chop wood with axes. Automatically craft missing tools.
- **Fire management** — Start fires with crafted tools, tend fires to maintain warmth, understand ember preservation.
- **Survival needs** — Eat, drink, sleep based on body state. Seek warmth when cold.
- **Inventory management** — Fill inventory during expeditions, stockpile resources at camp when supplies run low.
- **Movement** — Pathfinding across grid, travel to remembered resource locations, explore when resources unknown.

**Resource Memory:**
- NPCs remember where they've found resources (forage, harvestables, wood).
- Prefer known locations over exploration.
- Forget depleted locations after visiting and finding nothing.

**Work Types:**
- Forage (ambient searching), Harvest (bushes/features), Chop (trees with axe)
- Automatically handles tool requirements — crafts axes, fire-starters when needed
- Progress persists across sessions (chopping trees continues over multiple work periods)

**Death System:**
- NPCs can die from hypothermia, starvation, dehydration, blood loss, organ failure.
- Death creates `NPCBodyFeature` at death location with belongings.
- Discovery-based notification — player finds body when arriving at tile, not immediately announced.
- Temperature-aware decay (fresh → decomposing → skeletal) affects discovery text.
- Player can bury bodies or loot belongings via work options.

**Relationship Memory:**
- NPCs remember significant interactions with other actors.
- Memories accumulate over time and affect NPC opinion.
- Positive relationships make NPCs more likely to help in combat.
- `RelationshipEvents` routes game occurrences (combat, shared time, etc.) to NPCs who should remember them.

**Current Limitations:**
- No player influence (can't command or suggest actions)
- No NPC-to-player communication
- No threat response (NPCs ignore predators)
- Personality traits (Boldness, Selfishness, Sociability) exist but don't affect behavior yet
- No collaboration on tasks

NPCs interact with: survival simulation (same body/stats system), locations (same movement/features), inventory (own inventory + camp stockpile), crafting (autonomous tool creation), fire (start/tend), death (creates discoverable bodies), combat (relationship memory affects ally decisions).

**Files**: `Actors/NPC/NPC.cs`, `Actors/NPC/NPCFactory.cs`, `Actors/NPC/RelationshipMemory.cs`, `Actors/NPC/RelationshipEvents.cs`, `Environments/Features/NPCBodyFeature.cs`

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

Runners — Control flow and player decisions. They await the player; they never draw.
- GameRunner: the action loop
- CombatOrchestrator: grid-based tactical combat (hunts, encounters, and headless NPC/herd fights)
- TravelRunner: movement between locations
- WorkRunner: all work activities (uses strategy pattern)
- HuntRunner: the approach prompt before a hunt; the hunt itself is combat

Handlers — Activity-specific execution logic (static classes)
- FireHandler: fire starting, tending, fuel management
- TorchHandler: lighting, chaining, extinguishing
- CookingHandler: food preparation at fire
- ConsumptionHandler: eating, drinking
- TreatmentHandler: medical treatment application
- CampHandler: sleep, rest, camp improvements
- TravelHandler: movement between locations
- CuringRackHandler: hide/meat preservation
- CraftingHandler: runs a chosen recipe - the work, the materials, the result

Handlers take `GameContext`, mutate state directly, and await player choices through
`ctx.Ui`. Runners orchestrate flow; handlers execute specific actions. Everything from
`GameRunner.RunAsync` down is async - it awaits the player, never a thread.

Work Strategies — `IWorkStrategy` implementations for each work type:
- ForageStrategy, HuntStrategy, HarvestStrategy, TrapStrategy, ChoppingStrategy
- CacheStrategy, CraftingProjectStrategy, SalvageStrategy, ButcherStrategy
- Each strategy provides: location validation, time options, impairment calculations, execution logic

GameContext — Central hub holding game state
- Player, Camp, Inventory, CurrentLocation (no expedition state object)
- Tensions, Weather, Locations
- Condition checking for events
- `Update` ticks time forward and awaits the player when an event fires;
  `UpdateWithoutEvents` stays synchronous
- `Notices`, the queue the tick uses to reach the player, and `Ui`, the surface game
  logic reaches them through

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
Program frame loop
    → scheduler.Pump()            game logic advances until its next await
        → GameRunner.RunAsync
            → await ctx.Ui.WaitForPlayerAction()
            → the action runs: handler, work strategy, travel, combat
                → Pacing.PassTime(minutes, activity, progress view)
                    → await ctx.Ui.NextFrame()      one frame's dt
                    → TimedRun.Advance(dt)          how many minutes are now due
                    → await ctx.Update(1, activity) per due minute
                        → UpdateInternal(1)         synchronous, UI-free
                            → survival context, Player.Update, effects, body
                            → locations tick, weather, tensions, herds, NPCs
                            → queues events, encounters and notices
                        → if an event fired: await the player through ctx.Ui
            → drain ctx.Notices, run any pending encounter
    → ui.Frame(ctx, dt)           one frame: world, modals, HUD
```

**Files**: `Actions/GameContext.cs`, `Actions/GameRunner.cs`, `Config/ActivityConfig.cs`, `Actions/Expeditions/WorkStrategies/`

---

## Desktop UI

Native desktop application using Raylib-cs for graphics and ImGui.NET for panels. There
is **one frame loop**, **one frame composition**, and a one-way dependency: the UI
depends on the simulation and the simulation never depends on the UI. Game logic stays
ordinary sequential code, made possible by `async`/`await`.

See [frame-loop-architecture.md](frame-loop-architecture.md) for the full contract.

**Entry Point** — `Core/Program.cs` opens the window, installs the `FrameScheduler` as
the synchronization context, starts `GameRunner.RunAsync`, and then runs the only frame
loop there is:

```
while (!WindowShouldClose && !game.IsCompleted)
    dt = min(GetFrameTime(), 0.1)
    scheduler.Pump()       // game logic advances until it awaits again
    ui.Frame(ctx, dt)      // one frame; prompts may complete
```

A faulted game task is rethrown, never swallowed. Restart re-creates the context and the
UI and starts a new game task.

**FrameScheduler** (`Core/FrameScheduler.cs`) — a `SynchronizationContext` whose queue is
pumped once per frame. Every await in game logic resumes inside `Pump`, never inside
rendering: prompts complete a `TaskCompletionSource` created with
`RunContinuationsAsynchronously`, so the continuation is posted, not run inline. `Send`
throws and `Pump` is not reentrant.

**IGameUi** (`UI/IGameUi.cs`) — the complete surface game logic has on the player, reached
through `ctx.Ui`. `NextFrame`/`Wait` for time, prompts (`Select`, `Confirm`, `Choose`,
`ReadInt`, `ShowMessage`, `ShowWorkResult`, event choices, forage options, butcher mode),
screens (inventory, crafting, fire, food, transfer, discovery log, NPCs), the two base
screens (`WaitForPlayerAction`, `WaitForCombatAction`), and `BeginProgress`.

**DesktopUi** (`Desktop/DesktopUi.cs`) — the implementation, and the only
`Raylib.BeginDrawing` in the codebase. It owns the `WorldRenderer`, camera, HUD panels,
screen objects, and a **modal stack**:

- Base screens (`MapScreen`, `CombatScreen`) sit at the bottom and do not dim the world.
- Prompts and screens wrap a `TaskCompletionSource` and stack above them; nested prompts
  render underneath each other, which is the intended look.
- Progress views are modals that live until disposed.
- Only the top modal receives raw keyboard and mouse input; world hover always updates.
- The HUD (`StatsPanel`, `JournalPanel`, toasts) renders every frame in every state.

**Screens** keep their ImGui bodies but no longer own control flow: a screen *returns*
what the player chose and game logic acts on it. Instant actions (moving an item, adding
fuel) are applied inside the screen; anything that costs game time comes back as a result
so the caller can run it under a progress view.

**ProgressView** (`UI/ProgressView.cs`) — one display for every timed activity. Game logic
creates it, updates `Status`, `Progress` and `Sections` between frames, and disposes it
when done. Foraging finds and crafting materials are sections on this one view; there is
no bespoke foraging or crafting frame.

**Time** — `Pacing` decides how fast game time flows on screen; `TimedRun` turns real
seconds into whole due minutes with a fractional accumulator. `Pacing.PassTime` is the
canonical loop used by rest, sleep, work, camp setup, event time costs and incapacitation.
Anything that both animates and simulates derives both from one `TimedRun`, so they finish
together by construction.

**Notices** — the simulation tick never talks to the player. Witnessed deaths, discovered
bodies and newly unlocked recipes go onto `ctx.Notices`; game logic drains them between
actions and on arrival somewhere new.

**ScriptedUi** (`text_survival.Tests/Support/ScriptedUi.cs`) — answers from canned queues
and returns completed tasks, so the whole action loop runs in a test with no window.
An unanswered prompt throws with the prompt text.

**Rendering Layer**:
- `WorldRenderer` — grid tiles, the player sprite (interpolated during travel), hover
- `Camera` — a continuous world-space centre easing toward a target, one tile of overscan,
  drawn inside a scissor rect so nothing spills under the panels
- `TileRenderer`, `IconRenderer`, `AnimalRenderer`, `EdgeRenderer`, `EffectsRenderer`

**View models** live in `UI/` so game logic can name them without reaching into the
renderer: `OverlayData.cs` (EventDto, EventChoiceDto, EventOutcomeDto, DiscoveryLogDto),
`CombatInput.cs`, `PlayerAction.cs` (PlayerAction, Notice, WorkResultView, FireFeedback),
`ScreenResults.cs`, `ProgressView.cs`, `ToastFeed.cs`.

**Layering is enforced by a test** — `text_survival.Tests/Architecture/LayeringTests.cs`
scans the source and fails if game logic references the renderer, if the simulation
references `ctx.Ui`, if more than one file begins drawing, or if game logic blocks or
threads.

Desktop UI interacts with: all game systems (direct state access, read-only while
rendering), events (the event screen shows choices), inventory/crafting (screens).

**Files**: `Core/Program.cs`, `Core/FrameScheduler.cs`, `UI/IGameUi.cs`,
`Desktop/DesktopUi.cs`, `Desktop/Rendering/WorldRenderer.cs`, `Actions/Pacing.cs`,
`Actions/TimedRun.cs`

### Pixel Art Pipeline

Every world visual is a hand-authored 16x16 pixel-art PNG. There is no
procedural fallback: art is the only source, so an entity with no PNG is a
missing asset, and the loaders say so on stderr rather than quietly drawing
something else.

`tools/PixelArtCli` is a standalone CLI (zero dependencies, not part of the
main build) that renders a compact text format (`.pxa` — an ASCII grid plus a
hex-color palette legend) to PNG, so pixel art can be authored and reviewed as
plain text rather than drawn in an image editor. Source files live in
`assets/pixelart/`; `render-all` renders them into `assets/icons/`, where the
loaders pick them up by filename:

- `IconRenderer` — feature icons. A feature's `MapIcon` string is the PNG's
  basename, so a new icon is a new file and no code change.
- `TileRenderer` — terrain tiles (`<terrain>_tile*.png`, several variants per
  terrain), `player.png`, and `npc/{male,female}_{0..3}.png`.
- `AnimalRenderer` — `animals/<AnimalType>.png`, one per animal type.

`AssetPaths.Icons()` is the single place that resolves `assets/icons` (next to
the executable when published, next to the working directory in development)
and throws if it is missing. All loaded textures use `TextureFilter.Point` so
they stay crisp when scaled. See `tools/PixelArtCli/README.md` for the format
spec and per-category naming conventions.

Pixel art pipeline interacts with: tile/feature/animal/player rendering (the
only art source), the desktop rendering layer generally.

**Files**: `Desktop/Rendering/`, `tools/PixelArtCli/`, `assets/pixelart/`, `assets/icons/`

---

## The Mountain Crossing

The win condition, and the only ending other than death.

`GridWorldGenerator.GenerateMountainRange` walls off the north edge with 18 rows of
impassable mountain and carves a single-tile corridor of Rock through it at a random
column. `PlacePassLocations` names six stages along that corridor, south to north:

Pass Approach → Lower Pass → The Pass Proper → Upper Descent → Lower Descent → Far Side

Unnamed rock sits between them, so the crossing is an eighteen-tile trek, not a
doorway. Each stage is colder, more exposed and more hazardous than the last, peaking
at The Pass Proper (`terrainHazardLevel` 1.0, wind x2, -15°F, near-zero visibility).
`PlaceNamedLocations` only draws below the mountain rows, so nothing else lands there.

Far Side sets `Location.IsCrossingExit`. `GameRunner.Run` loops
`while (player.IsAlive && !CurrentLocation.IsCrossingExit)`, so reaching it ends the
run - there is no separate victory flag to keep in sync. `HandleVictory` closes out
the same way death does: a final screen with days survived and season, the save
deleted, and the choice to start again.

**Files**: `Environments/Factories/GridWorldGenerator.cs`, `Environments/Factories/LocationFactory.cs` (Mountain Pass Factories), `Actions/GameRunner.cs`

---

## Design Direction

Not yet implemented, but shaping future development:

Megafauna hunts — Trophy hunts that provide materials for gear required for the crossing.

Exploration areas — Distant, dangerous locations with unique rewards.

Camp investment — Persistent improvements that make a camp worth defending.