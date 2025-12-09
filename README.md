# Text Survival

A console-based survival RPG set in an Ice Age world with shamanistic elements, inspired by games like RimWorld and The Long Dark. The game emphasizes deep, interconnected systems, leveraging its text-only format to create immersive survival gameplay without the overhead of graphics. Players must navigate procedurally generated environments, manage survival needs, engage in combat, craft tools, and use shamanistic magic to survive harsh conditions.

## Getting Started

```bash
# Build the project
dotnet build

# Run the game
dotnet run

# Run tests
dotnet test
```

## Game Vision and Core Concept

**Text Survival** is a focused survival experience where resource management, environmental challenges, and dynamic character interactions are paramount. Set in an Ice Age world where cold, scarcity, and rugged terrain shape gameplay, the player must adapt and overcome through skill development and strategic decision-making.

### Inspirations:
- **The Long Dark**: Emphasis on survival through managing hunger, thirst, and harsh weather conditions
- **RimWorld**: Detailed body part system and emergent storytelling through character interactions

### Design Philosophy

#### Technical Principles
- **Simplicity with Depth**: Simple systems combine for complex outcomes (e.g., a skilled but injured player vs. an unskilled healthy one)
- **Modularity**: Supports adding features like diseases or prosthetics without major refactoring
- **Realism**: Grounded mechanics (calorie-based fat dynamics, activity-driven muscle growth)

#### Gameplay Design
- **Ice Age Authenticity**: Period-appropriate materials and tools (flint, bone, hide)
- **Survival Over Combat**: Environmental challenges (cold, scarcity, weather) take priority over traditional RPG combat/leveling
- **Emergent Storytelling**: Systems interact to create unique player stories rather than scripted narratives
- **Resource Scarcity**: Every material matters; players must plan and adapt to survive

## Implemented Systems

### Survival System
- **Temperature Regulation**: Exponential heat transfer model with body temperature tracking
- **Hunger/Thirst**: Calorie-based metabolism with realistic fat/muscle dynamics (7700 kcal = 1 kg fat)
- **Exhaustion**: Activity-driven fatigue system
- **Dynamic Effects**: Hypothermia, frostbite, sweating, and other condition-based effects

### Body System
- **Hierarchical Structure**: BodyRegion -> Tissues (Skin, Muscle, Bone) -> Organs
- **Damage Processing**: Penetration-based damage through tissue layers
- **Capacities**: Body parts contribute to capacities (Moving, Manipulation, Breathing, etc.) which derive stats
- **Body Composition**: Tracks fat and muscle in kg with dynamic percentages affecting Speed, Vitality, and cold resistance

### Combat & Hunting
- **Turn-based Combat**: Driven by body-derived stats (Strength, Speed)
- **Hunting System**: Stealth-based approach with ranged weapons, tracking, and blood trails
- **Ammunition Management**: Bow/arrow system with different projectile types
- **Animal AI**: Behavior states (Grazing, Alert, Fleeing) based on player actions

### Crafting System
- **Property-Based**: Items tagged with properties (Stone, Wood, Flammable, Sharp, etc.)
- **Recipe Builder**: Fluent API for defining recipes with property requirements
- **Skill Requirements**: Recipes can require specific skill levels
- **Result Types**: Craft items, location features (fires, shelters), or structures

### Fire Management
- **Fire States**: Burning, Embers, Dead with realistic transitions
- **Ember System**: Fires leave embers allowing easier relight
- **Heat Distribution**: Exponential heat transfer affecting player temperature
- **Fuel System**: Different fuel types with varying burn times and heat output

### Location & Environment
- **Zone System**: Procedurally generated zones containing multiple locations
- **Biomes**: Forest, Plains, Riverbank, Hillside, Cave - each with distinct resources
- **Location Features**: Forage spots, harvestable resources, heat sources, shelters
- **Weather System**: Dynamic weather affecting visibility, temperature, and actions

### Skills & Progression
- **Player-Only Skills**: Hunting, FireMaking, Skinning, ToolCrafting, Shamanism
- **Skill Checks**: Base success rates with skill bonuses, clamped 5%-95%
- **XP System**: Both success and failure grant XP to encourage experimentation

### Action System
- **Builder Pattern**: Fluent API for creating context-aware actions
- **Menu Chaining**: Actions flow naturally via `.ThenShow()` and `.ThenReturn()`
- **Conditional Availability**: Actions filtered based on `.When()` conditions
- **Dynamic Context**: GameContext provides access to player, location, and game state

### Effects System
- **EffectRegistry**: Per-actor tracking of active buffs/debuffs
- **Effect Builder**: Create effects with severity, duration, capacity modifiers
- **Body Part Targeting**: Effects can target specific body parts or whole body

### IO Abstraction
- **Output.cs**: All text display with automatic type-based coloring
- **Input.cs**: All user input with validation helpers
- **Test Mode**: Routes I/O through TestModeIO for automated testing

## Biomes

Each biome serves a distinct gameplay role with intentional resource distributions:

- **Forest (Starting Biome)**: Abundant organic materials, good fire-making resources, balanced food availability. Forgiving starting area with all essentials for day-1 survival.

- **Plains**: Grassland materials, exposed to weather extremes, good hunting grounds. Risk/reward - great hunting but harsh conditions.

- **Riverbank**: Water access, river stones and clay, rushes and water plants. Specialized resources but limited wood/fire materials.

- **Hillside**: Balanced stone and organic mix, elevated vantage points. Versatile mid-game area for established players.

- **Cave (Advanced)**: NOT a starting biome - requires preparation. Excellent weather protection, rare materials (obsidian), but low food and dangerous wildlife.

## Testing

### Unit Tests
Comprehensive xUnit tests for calculation systems:
- Body capacity and damage calculations
- Survival processing (temperature, metabolism)
- Skill check formulas

```bash
dotnet test
```

### Integration Testing
Interactive test mode via `play_game.sh` for gameplay flow testing:

```bash
./play_game.sh start    # Start game in test mode
./play_game.sh send "1" # Send input
./play_game.sh log      # View game output
./play_game.sh stop     # Stop game
```

## Project Structure

```
text_survival/
├── Actions/           # Action system with builder pattern
├── Actors/            # Player, NPC, Animal classes
├── Bodies/            # Body part hierarchy and damage
├── Crafting/          # Recipe-based crafting system
├── Effects/           # Buff/debuff system
├── Environments/      # Zone, Location, Features
├── IO/                # Input/Output abstraction
├── Items/             # Item hierarchy and factories
├── Level/             # Skills and progression
├── Magic/             # Spell system
├── PlayerComponents/  # Player managers (Hunting, Stealth, etc.)
├── Survival/          # Survival processing
├── UI/                # Map rendering and UI components
├── Utils/             # Calculators and helpers
├── documentation/     # Detailed system documentation
└── dev/               # Development notes and plans
```

## Documentation

See the `documentation/` folder for detailed system guides:
- `action-system.md` - Action builder pattern and menu flow
- `crafting-system.md` - Property-based crafting and recipes
- `body-and-damage.md` - Body part hierarchy and damage processing
- `survival-processing.md` - Temperature, hunger, thirst mechanics
- `fire-management-system.md` - Fire states and heat sources
- `skill-check-system.md` - Skill check formulas and XP
- And more...

See `CLAUDE.md` for development guidelines when contributing.
