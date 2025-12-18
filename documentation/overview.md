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

Your camp is where your fire is. When you forage, hunt, or explore, you leave camp, do the work, and return. You don't "move" to the forest and then decide what to do there. You decide "I'm going to forage in the forest" and that's a single commitment with a round trip baked in.

### Expedition Structure

Every expedition commits you to:
- **Travel out** — time to reach destination
- **Work phase** — the activity itself, with time variance
- **Travel back** — return to camp

Time has variance. "Forage in Deep Forest: approximately 25 minutes (could be 15-40)." You can't calculate perfectly. Fire margin becomes an intuition you develop.

### Fire Margin

Before committing to an expedition, the system calculates and displays:
- Total estimated time (with variance range)
- Current fire remaining
- Margin assessment (Comfortable / Tight / Risky / Very Risky)

The player sees the tradeoffs before committing: "Deep Forest — ~70 min round trip. Fire will have ~15 min remaining. Proceed?"

### Camp Actions vs Expeditions

**Camp actions** happen at camp with no travel — tend fire, craft, rest, eat. Time passes and fire burns, but you're present. No margin check needed.

**Expeditions** leave camp and return. Forage, hunt, explore. These have the three-phase structure and automatic fire margin warnings.

**Move camp** is a special case — one-way travel, abandon infrastructure, establish somewhere new. Major commitment triggered by depletion or opportunity.

---

## Locations

Locations are expedition destinations with travel times from camp. Each has:
- Name and character (Frozen Creek, Dense Birches, Rocky Overlook)
- Travel time from camp
- Features (what you can do there)
- Terrain properties (exposure, shelter potential)

Locations connect to each other as a graph with distances. Camp is your anchor point; travel times to other locations are calculated from there.

### Location Discovery (Planned)

Locations aren't all visible from the start. They reveal through play:
- **Unknown** — you don't know it exists
- **Hinted** — "a dark shape on the far slope, might be shelter"
- **Visible** — confirmed, can travel there
- **Explored** — full features revealed

Discovery happens through exploration expeditions, events, vantage points, or time spent in an area.

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

**AnimalTerritoryFeature** — Defines what game can be found at a location. Animals spawn dynamically when searching, not pre-placed. Game density depletes with successful hunts and respawns over time.

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

## Effects

Buffs and debuffs with:
- Severity (0-1)
- Capacity modifiers (Hypothermia reduces Moving)
- Survival stat modifiers (Fever increases dehydration rate)
- Progression (severity changes over time, can worsen or heal)
- Targeting (body-part specific or general)

Effects are generated by the survival processor when thresholds trigger, or by events. The actor owns an EffectRegistry that tracks active effects and handles stacking/expiration.

---

## Fire System

Physics-based simulation:
- Fuel types with different burn rates and heat output
- Fire phases: Igniting → Building → Roaring → Steady → Dying → Embers
- Ember preservation for relight without fire-starting tools
- Heat contribution to location temperature based on fuel mass and fire maturity

Fire is infrastructure. Building a good fire with quality fuel extends your expedition radius. Letting it die while you're away means returning to cold camp — survivable if you planned for it, dangerous if you didn't.

---

## Threats (Planned)

Threats are data-driven, not scripted encounters.

A threat has: type, awareness, boldness (0-1), and state (distant → following → stalking → closing → attacking → fled).

Threat types define behavior as data:
- Boldness modifiers for conditions (player injured, player holding fire, night, etc.)
- State transition thresholds
- Behavioral flags (persistent, pack hunter, ambusher)

The wolf encounter isn't scripted. It emerges from: player carrying meat + injury slowing movement + threat system running its boldness calculation. Each escalation stage gives the player a chance to respond with contextual options.

---

## Events (Planned — Top Priority)

Events trigger during expedition phases based on:
- Location (wolf territory)
- Player activity (hunting attracts predators)
- Player state (injured, carrying food, bleeding)
- Time and weather

Events aren't random encounters — they're contextual interrupts that create decisions. The same wolf sighting plays differently when healthy vs injured, when camp is close vs far, when carrying meat vs not.

Events can:
- Escalate (sighting → approaching → attacking)
- Chain (finding tracks → following → ambush)
- Offer opportunities (spot a deer while foraging — abandon forage to hunt?)
- Force tradeoffs (storm coming — push through or abort?)

---

## Items

Items are property bags with core numbers:
- Weight, Hardness, Flammability, Burn rate, Nutrition, Condition

Plus freeform tags: "wood", "tinder", "cordage-material", "striker"

Actions query properties. A "burn" action checks flammability and burn rate. Behavior emerges from properties, not item-specific code.

**Aggregation approach (TBD):** Most resources may aggregate (fuel supply with quantity + quality modifier) rather than tracking individual sticks. Discrete items only where identity matters — tools with condition, special finds, trophies.

---

## Crafting (Needs Rework)

Current system is recipe-based. Design direction is need-based:

Don't show craftable items. Show categories of need:
- Fire-starting supplies
- Cordage or bindings
- Tools or weapons
- Shelter improvement

Player picks a category, sees what's possible with current materials, decides whether to proceed. A walking stick isn't a recipe — it's a query for wood meeting certain criteria (shoulder-height, straight, solid).

---

## Architecture

### Hierarchy

**Runners** — Control flow, player decisions, display UI
- GameRunner: main loop, calls into specific runners
- ExpeditionRunner: expedition flow from selection to completion
- (Future: CombatRunner, CraftingRunner)

**Processors** — Stateless domain logic, returns results
- SurvivalProcessor: calculates stat changes per tick, returns delta + effects + messages
- ExpeditionProcessor: handles expedition phase progression, event checks

**Data Objects** — Hold state, minimal behavior
- Body: physical form, survival stats, owned by Actor
- Expedition: current expedition state (phase, elapsed time, destination)
- Location: place in the world with features
- Actor: owns Body and EffectRegistry, orchestrates updates

### Update Flow

```
Action executes
    → World.Update(N minutes)
        → For each minute:
            → Actor.Update()
                → EffectRegistry.Update() (severity changes, expiration)
                → Body passes data to SurvivalProcessor
                → SurvivalProcessor returns delta, effects, messages
                → Body applies delta, Actor applies effects
            → Location.Update()
                → Fire burns down
                → (Future: NPC updates)
```

### Features

Features live on locations. Currently a hybrid — data objects with some behavior (ForageFeature.Forage() returns items). May refactor to pure data with processors, but works for now.

---

## What's Working

- Basic expedition loop (forage type)
- Hunt expedition with territory-based game spawning (search -> stalk -> kill -> density depletes)
- Fire system with fuel consumption and heat output
- Survival stat ticking (energy, hydration, calories, temperature)
- Body system with parts and capacities
- Effects system with severity and modifiers
- Location graph with travel times
- Expedition phases with fire margin display

## What's Next

1. **Events during expeditions** — the layer that makes expeditions tense
2. **Threat encounters** — data-driven boldness system
3. **Location discovery** — unknown → hinted → visible → explored
4. **Crafting rework** — need-based instead of recipe lists

## Explicitly Dropped

- **Character skills/stats** — conflicts with knowledge-is-progression philosophy
- **Magic/shamanism** — removed until core is solid
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