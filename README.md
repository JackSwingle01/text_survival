# Text Survival

A console-based survival game set in an Ice Age world, inspired by The Long Dark, Don't Starve, and RimWorld. The text-only format enables deep, interconnected survival systems without graphical overhead.

## Game Vision

A camp-centric survival experience where fire is the anchor. Every expedition is a commitment — leave camp, do the work, return before your fire dies. Player knowledge is the only progression; the game doesn't get easier, you get better.

### Inspirations
- **The Long Dark**: Harsh survival where smart play can still fail but careless play always does
- **Don't Starve**: Progressive dread as resources dwindle
- **RimWorld**: Deep simulation creating emergent stories
- **A Dark Room**: Progressive disclosure through play

### Design Philosophy

#### Technical Principles
- **Simplicity with Depth**: Simple systems combine for complex outcomes
- **Modularity**: Supports adding features without major refactoring
- **Realism**: Grounded mechanics (calorie-based fat dynamics, physics-based fire)

#### Gameplay Principles
- **Fire as Tether**: Fire defines your radius of action — every decision runs through "can I make it back?"
- **Expeditions, Not Navigation**: Commit to round trips with time variance, exposure, and event risk
- **Depletion Creates Pressure**: Areas exhaust over time, pushing you outward or forcing camp moves
- **Ice Age Authenticity**: Period-appropriate materials and challenges (flint, bone, hide, cold)
- **Survival Over Combat**: Environmental challenges take priority over traditional RPG combat
- **Emergent Storytelling**: Systems interact to create unique stories, not scripted narratives
- **No Meta-Progression**: Knowledge persists in the player, not in unlocks

## Technical Architecture

The game is built in C# with a modular architecture separating concerns into distinct systems:

- **Actions**: Expedition and camp action definitions with time variance and event integration
- **Actors**: Player and NPC entities (Humanoid, Animal) with body systems and survival stats
- **Bodies**: Hierarchical body parts affecting capacities (Moving, Manipulation, Breathing, etc.)
- **Survival**: Minute-by-minute simulation of calories, hydration, temperature, and energy
- **Effects**: Buff/debuff system with severity progression and capacity modifiers
- **Environments**: Zones, locations, and features (fire, shelter, forage, harvestables)
- **Items**: Tools, fuel, food, materials with property-based crafting
- **Crafting**: Recipe system based on material properties rather than specific items
- **Combat**: Body-part targeted combat; hunting as distinct stealth-based system
- **Magic**: Shamanistic spells requiring specific materials (herbs, bones)
- **IO**: Abstraction layer for all input/output — supports console, test mode, future GUI

## Core Systems

### Camp and Expeditions

Your camp is where your fire is. Locations are expedition destinations, not a navigation graph. Each expedition commits you to:
- Travel time out
- Work time (with variance)  
- Travel time back
- Exposure and event risk throughout

Fire margin is calculated automatically before committing.

### Body System

**Structure**: Hierarchical parts for humans (Torso → Heart, Arm → Hand → Fingers) and simplified for animals (Body, Head, Legs, Tail).

**Capacities**: Derived from body part health — Moving, Manipulation, Breathing, BloodPumping, Consciousness, Sight, Hearing, Digestion.

**Composition**: Fat and muscle mass in kilograms affecting temperature resistance, speed, and strength.

### Survival Simulation

Rate-based calculation per minute:
- **Energy**: Depletes with time and exertion
- **Hydration**: Constant drain, faster with heat/exertion
- **Calories**: BMR-based with activity multipliers (7700 kcal = 1 kg fat)
- **Temperature**: Heat transfer toward environment, modified by clothing, shelter, fire

### Fire System

Physics-based simulation:
- Fuel types with different burn rates, temperatures, ignition requirements
- Fire phases: Igniting → Building → Roaring → Steady → Dying → Embers
- Ember preservation for relight without fire-starting tools
- Heat output based on fuel mass and fire maturity

### Skills

Player-only abilities improving with use:
- **Hunting**: Tracking, accuracy, yields
- **Foraging**: Find rates, identification
- **Firecraft**: Ignition success, fuel efficiency
- **Survival**: Navigation, weather reading
- **Crafting**: Quality, efficiency

Skills reflect knowledge — they improve through practice and never decrease.

## Biomes

Each biome serves a distinct gameplay role:

| Biome | Role | Resources | Challenge |
|-------|------|-----------|-----------|
| Forest | Starting area | Wood, bark, berries, small game | Moderate shelter |
| Plains | Hunting grounds | Large game, grass | Full exposure |
| Riverbank | Water/stone access | Water, stones, clay, rushes | Limited fire materials |
| Hillside | Mid-game versatility | Mixed resources, vantage | Moderate exposure |
| Cave | Advanced destination | Rare minerals, excellent shelter | Bring supplies, expect danger |

## Project Structure
```
text_survival/
├── Actions/          # Action and expedition definitions
├── Actors/           # Player, NPC, Animal entities
├── Bodies/           # Body parts, damage, capacities
├── Combat/           # Combat and hunting systems
├── Crafting/         # Recipes and crafting logic
├── Effects/          # Buffs, debuffs, conditions
├── Environments/     # Zones, locations, features
├── IO/               # Input/output abstraction
├── Items/            # Items, containers, equipment
├── Magic/            # Spells and shamanistic rituals
├── Skills/           # Skill definitions and progression
├── Survival/         # Survival stat processing
└── UI/               # Display and menu rendering
```

## Running the Game
```bash
dotnet build
dotnet run
```
