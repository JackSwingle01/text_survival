# Text Survival — Design Overview

A console-based survival game set in an Ice Age world. The text-only format enables deep, interconnected survival systems without graphical overhead.

---

## Core Philosophy

### Fire as Tether

Every decision runs through one question: can I do this and get back before my fire dies?

The fire burns whether you're there or not. This single constraint makes everything matter — temperature, calories, time, distance. Systems don't run in parallel; they pressure each other through the bottleneck of fire.

Early game, you can barely leave camp. Your fire is small, your fuel is scarce. You forage nearby, stay close, survive minute to minute. As you learn the systems, your effective radius expands — not because numbers went up, but because you understand when risks are worth taking.

### Intersecting Pressures Create Decisions

A wolf isn't dangerous because it's a wolf. It's dangerous because you're bleeding, limping, low on fuel, and far from camp. Single systems create problems. Intersecting systems create meaningful choices.

The interesting decisions are time/risk tradeoffs:
- Do I search longer or take what I have?
- Do I go back to camp or push further?
- Can I make it back before the fire dies?

### Knowledge is Progression

No meta-progression, no unlocks, no character stats that grow. The player gets better, not the character. An experienced player survives because they understand the systems — when 10 minutes of margin is actually fine, when "abundant" doesn't mean safe, when to let a fire die on purpose.

### The Game Decides Low-Stakes, The Player Decides High-Stakes

The interface reads game state and surfaces what matters. "Tend fire" appears prominently when fuel is low. "Treat wound" surfaces when you're bleeding. The player chooses from 2-4 meaningful options, not exhaustive menus.

---

## Camp and Expeditions

### The Core Model

You have a camp. Everything else is an expedition.

Your camp is where your fire is. Camp is where you're safe — you can tend fire, craft, rest, eat, and manage inventory. When you leave camp, you're on an expedition with time cost and risk.

### Expedition Structure

Expeditions are flexible:
- **Leave camp** — choose a destination, travel there
- **Work or travel** — at each location, choose to work (forage, hunt, explore) or travel to another connected location
- **Return** — backtrack through your travel history to camp

The UI always shows fire status at the top of the screen. Fire margin is implicit — you see your fire remaining, you see travel times, and you develop intuition for how much you can push.

### Camp Actions vs Expeditions

**Camp actions** happen at camp with no travel — tend fire, craft, rest, eat, cook/melt snow, manage inventory. Time passes and fire burns, but you're present.

**Expeditions** leave camp. You can travel freely between locations, working as you go. The expedition ends when you return to camp.

**Work near camp** — forage and scout without leaving camp. Lets you gather nearby resources and discover new locations without expedition risk.

---

## Locations

Locations are expedition destinations with travel times from camp. Each has:
- Name and character (Frozen Creek, Dense Birches, Rocky Overlook)
- Travel time from camp
- Features (what you can do there)
- Terrain properties (exposure, shelter potential)

Locations connect to each other as a graph with distances. Camp is your anchor point; travel times to other locations are calculated from there.

### Location Discovery

Locations aren't all visible from the start. They reveal through exploration:
- **Unexplored** — you can travel there but don't know what's there (shown as hints like "rocky area to the north")
- **Explored** — full features revealed after visiting or scouting

Discovery happens dynamically through the exploration work type, which has a chance to generate new connected locations based on the zone's terrain.

### Depletion

Areas run out. The nearby grove is plentiful on day one, sparse by day three, picked over by day five. The game pushes you outward — range further (more risk, more time) or move camp (reset your radius).

---

## Features

Features are what make locations useful:

**ForageFeature** — Ambient scavenging. Search the area, find things based on location abundance and time invested. Yields diminish as you exhaust an area.

**HarvestableFeature** — Visible specific resources. A berry bush with 12 servings. A dead tree you can process. Quantity-based, not probability-based.

**EnvironmentFeature** — Terrain properties. A cave has wind protection. A ridge is exposed but offers visibility.

**ShelterFeature** — Built protection. Lean-to, snow wall, debris hut. Takes time to build, stays when you leave.

**HeatSourceFeature** — Fire. The anchor. Has fuel, burns down, produces heat. Can bank to embers, relight, die completely.

**AnimalTerritoryFeature** — Defines what game can be found at a location. Animals spawn dynamically when searching, not pre-placed. Game density depletes with successful hunts and respawns over time. Also provides helpers for the event system (random animal names, predator identification).

---

## Survival Simulation

Rate-based calculation per minute:
- **Energy** — depletes with time and exertion
- **Hydration** — constant drain, faster with heat/exertion  
- **Calories** — BMR-based with activity multipliers
- **Temperature** — heat transfer toward environment, modified by clothing, shelter, fire

Thresholds trigger effects. Temperature drops below 95°F, you start shivering. Below 89.6°F, frostbite risk. The survival processor returns stat changes, triggered effects, and messages — it doesn't own state, just calculates.

---

## Body System

Hierarchical body parts for humans (Torso → Heart, Arm → Hand → Fingers). Each part contributes to capacities:
- **Moving** — legs, feet, knees
- **Manipulation** — hands, fingers, arms
- **Breathing** — lungs, chest
- **Consciousness** — head, brain
- Plus: BloodPumping, Sight, Hearing, Digestion

Injuries attach to body parts with severity (0-1). Capacity calculation is generic — sum injuries, check which capacities they affect, apply modifiers. No special cases per body part.

Body composition (fat, muscle mass in kg) affects temperature resistance, speed, strength. Calories convert to fat at 7700 kcal per kg.

---

## Effects vs Body Damage

Two distinct systems handle harm:

**Body Damage = Structural**
- Entry point: `Body.Damage(DamageInfo)`
- Tracks tissue condition (skin, muscle, bone, organs) at 0-1
- Affects capacities based on which parts are damaged
- Heals automatically when well-fed, hydrated, rested

**Effects = Processes**
- Entry point: `EffectRegistry.AddEffect(effect)`
- Ongoing conditions that tick over time (bleeding, hypothermia, fear)
- Have severity (0-1), decay rates, capacity/stat modifiers
- Resolve through natural decay or treatment

**The distinction:** A wolf bite causes BOTH body damage (structural injury to leg) AND a bleeding effect (ongoing blood loss). Stopping bleeding doesn't heal the leg. Healing the leg doesn't stop bleeding.

**Available effects:** Cold, Hypothermia, Hyperthermia, Shivering, Sweating, Frostbite, Bleeding, Sprained Ankle, Fear/Shaken

**Auto-triggered:** Sharp/pierce damage that breaks skin automatically triggers Bleeding effect proportional to damage dealt.

See [effects-and-injuries.md](effects-and-injuries.md) for full details.

---

## Fire System

Physics-based simulation:
- Fuel types with different burn rates and heat output
- Fire phases: Igniting → Building → Roaring → Steady → Dying → Embers
- Ember preservation for relight without fire-starting tools
- Heat contribution to location temperature based on fuel mass and fire maturity

Fire is infrastructure. Building a good fire with quality fuel extends your expedition radius. Letting it die while you're away means returning to cold camp — survivable if you planned for it, dangerous if you didn't.

---

## Predator Encounters

Predator encounters emerge during hunting when prey turns aggressive or when stalking fails. The system uses boldness-based AI:

- **Boldness (0-1)** — calculated from context: player injured, carrying meat, low vitality
- **Distance tracking** — predator closes or backs off based on player actions
- **Player options** — stand ground (reduces boldness), back away (increases boldness but gains distance), run (speed check), fight, drop meat (escape)

The encounter isn't scripted. It emerges from: player carrying meat + injury slowing movement + predator's boldness calculation. Each turn gives the player a chance to respond, and boldness determines whether the predator commits to attack or retreats.

Combat is simple turn-based when it occurs: attack with equipped weapon, predator attacks back. Victory allows butchering the carcass.

---

## Events

Events trigger during expedition phases based on:
- Location (wolf territory, animal territory)
- Player activity (hunting attracts predators)
- Player state (injured, carrying food, bleeding)
- Time and weather

Events aren't random encounters — they're contextual interrupts that create decisions. The same wolf sighting plays differently when healthy vs injured, when camp is close vs far, when carrying meat vs not.

### Architecture

Events use a factory pattern — each event type is a function that takes `GameContext` and returns a `GameEvent`. This allows events to bake location-specific context into their descriptions at trigger time (e.g., "Fresh deer tracks" instead of generic "animal tracks").

### Event Categories

Events fall into categories based on triggers and context:

- **Weather events** — triggered by weather conditions (storms, cold snaps, fog)
- **Environmental events** — terrain hazards, accidents during travel or work
- **Opportunity events** — discoveries, useful finds
- **Animal territory events** — tracks, carcasses, predator awareness (require AnimalTerritoryFeature)

Events can:
- Add time to expeditions
- Apply effects (injuries, cold, etc.)
- Grant rewards via RewardPools
- Consume resources as costs

The event pool continues to expand through playtesting.

---

## Items and Inventory

The inventory uses a hybrid approach:

**Aggregate resources** — stored as lists of weights for common items:
- Fuel: Sticks, Logs, Tinder (each a `List<double>` of individual weights)
- Food: Raw meat, Cooked meat, Berries (same pattern)
- Materials: Stone, Bone, Hide, PlantFiber, Sinew
- Water (in liters)

**Discrete items** — tracked individually where identity matters:
- Tools (with type and durability)
- Equipment (clothing with insulation values)

**Dual inventory system:**
- Player inventory — weight-limited (15kg), carried during expeditions
- Camp storage — unlimited capacity, accessible only at camp
- Transfer items between them via the inventory menu

---

## Crafting

Need-based crafting system. Player expresses a need, sees what's craftable from available materials.

**Need Categories** (MVP):
- Fire-starting supplies (Hand Drill, Bow Drill)
- Cutting tools (Sharp Rock, Stone Knife, Bone Knife)
- Hunting weapons (Wooden Spear, Heavy Spear, Stone-Tipped Spear)

**Crafting Materials** (aggregate resources in Inventory):
- Stone — from foraging (riverbanks, rocky areas)
- Bone — from butchering animals (~15% body weight)
- Hide — from butchering animals (~10% body weight)
- PlantFiber — from foraging (forests, grasslands)
- Sinew — from butchering animals (~5% body weight)

**Binary Tools**: Tools either work or they're broken. No quality tiers. Different materials affect durability (uses before breaking), not effectiveness.

**Flow**: Camp menu → "Work on a project" → Select need category → See craftable options → Craft. Time passes, materials consumed, tool added to inventory.

**Future Categories** (not yet implemented):
- Shelter improvements (LocationFeatures)
- Clothing/warmth (Equipment from hides)
- Containers/carrying (capacity bonuses)

---

## Architecture

### Hierarchy

**Runners** — Control flow, player decisions, display UI
- GameRunner: main camp loop, handles camp actions (fire, eating, cooking, inventory, sleep)
- ExpeditionRunner: leaving camp, traveling, working at locations, returning
- WorkRunner: work activities (forage, hunt, explore, harvest)
- CraftingRunner: need-based crafting UI

**GameContext** — Central hub holding game state
- Player, Camp, Inventory, Expedition (when active), Zone
- Condition checking for events (`Check(EventCondition)`)
- Update methods that tick time forward

**Processors** — Stateless domain logic, returns results
- SurvivalProcessor: calculates stat changes per tick, returns delta + effects + messages
- ButcheringProcessor: calculates meat/hide/bone yields from animals
- TravelProcessor: calculates travel times between locations

**Data Objects** — Hold state, minimal behavior
- Body: physical form, survival stats, owned by Actor
- Expedition: travel history, elapsed time, collection logs
- Location: place in the world with features
- Camp: wrapper around location with fire/shelter accessors and storage

### Update Flow

```
Action executes
    → GameContext.Update(N minutes)
        → Player.Update() with SurvivalContext
            → EffectRegistry.Update() (severity changes, expiration)
            → SurvivalProcessor calculates delta, effects, messages
            → Body applies delta, Player applies effects
        → Zone.Update()
            → Location.Update() for each location
                → Fire burns down
                → Features update (respawn timers, etc.)
```

### Features

Features live on locations. Hybrid pattern — data objects with behavior (ForageFeature.Forage() returns items, AnimalTerritoryFeature.SearchForGame() spawns animals).

---

## What's Working

- Camp-centric game loop with contextual menu options
- Fire management (starting, tending, fuel types, phases, embers)
- Flexible expeditions (travel between locations, work, return)
- Hunting with stalking, ranged attacks (spear/stones), predator encounters
- Foraging with depletion and respawn
- Location discovery via exploration work type
- Survival stats (energy, hydration, calories, temperature)
- Body system with parts, capacities, and damage
- Effects system with severity and modifiers
- Weather system affecting events and temperature
- Event system with contextual triggers and weight modifiers
- Need-based crafting (fire-starting, cutting tools, hunting weapons)
- Dual inventory (player carry + camp storage)
- Butchering animals for meat, hide, bone, sinew

## What's Next

Current focus is balance tuning and content expansion:

1. **More events** — expand variety based on playtesting
2. **Threat encounters** — polish predator boldness system, add more encounter types
3. **More crafting categories** — shelter improvements, clothing, containers
4. **Balance** — survival rates, resource yields, travel times

## Explicitly Dropped

- **Magic/shamanism** — removed, not coming back
- **Meta-progression/unlocks** — the player learns, not the game
- **Complex nutrition** — calories suffice, protein/fat/carb tracking is unnecessary complexity

## Deferred (Not Core)

- NPC/tribal content
- Seasonal progression
- Migration patterns
- Mental health/morale systems

---

## Design Tests

The redesign works if:
- An experienced player can reach a meaningful decision in under 5 minutes
- Two experienced players would reasonably choose differently in the same situation
- Fire margin creates genuine tension without being tedious
- Depletion naturally pushes players to range further or move camp
- Events feel like interrupts that matter, not random annoyances
- Stories emerge from systems interacting, not from authored sequences