# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## IMPORTANT: Consult the Documentation

**Always consult the comprehensive documentation** in `documentation/` when working on this codebase. This documentation provides detailed guidance on all game systems and architectural patterns.

**The documentation is a LIVING DOCUMENT**:
- If you encounter contradictions between this file and the documentation, **ask the user which should be updated**
- If implementation patterns have evolved beyond what's documented, **propose updating the documentation**
- If new systems are added that aren't documented, **suggest adding them to the documentation**
- The documentation should always reflect the current state and best practices of the codebase

**Available documentation files**:
- `action-system.md` - Action builder pattern and menu flow
- `crafting-system.md` - Property-based crafting and recipes
- `effect-system.md` - Buff/debuff system and EffectRegistry
- `body-and-damage.md` - Body part hierarchy and damage processing
- `survival-processing.md` - Temperature, hunger, thirst mechanics
- `fire-management-system.md` - Fire states, ember system, and heat source features
- `skill-check-system.md` - Skill check formulas, success rates, and XP rewards
- `builder-patterns.md` - Fluent builder patterns used throughout
- `composition-architecture.md` - Actor composition and manager patterns
- `factory-patterns.md` - ItemFactory, NPCFactory, etc.
- `error-handling-and-validation.md` - Error handling conventions
- `complete-examples.md` - End-to-end feature implementation examples

## Project Overview

Text Survival is a console-based survival RPG set in an Ice Age world with shamanistic elements. Built in C# (.NET 9.0), it emphasizes deep interconnected systems with a modular architecture inspired by RimWorld and The Long Dark. The game leverages its text-only format to create immersive survival gameplay focused on resource management, body simulation, and emergent storytelling.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the game
dotnet run

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore
```

## Testing with TEST_MODE

**IMPORTANT**: Claude Code can play the game interactively using the `play_game.sh` helper script for automated testing. See **TESTING.md** for full details and examples.

**CRITICAL**: ALWAYS use the `play_game.sh` script commands to interact with the game. The script provides proper commands like `./play_game.sh log`, `./play_game.sh tail`, `./play_game.sh send`, etc.

## Unit Testing

The project includes comprehensive unit tests for calculation systems using **xUnit**. Tests focus on pure functions and calculation logic, providing regression protection for complex formulas.

### Running Tests

```bash
# Run all unit tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SurvivalProcessorTests"

# Run tests for a specific namespace
dotnet test --filter "FullyQualifiedName~Bodies"
```

### Test Structure

Tests are organized in `text_survival.Tests/` mirroring the main project structure:

```
text_survival.Tests/
├── Bodies/
│   ├── AbilityCalculatorTests.cs       # Strength, speed, vitality calculations
│   ├── CapacityCalculatorTests.cs      # Body capacity and cascading effects
│   ├── DamageProcessorTests.cs         # Damage penetration and distribution
│   ├── TissueTests.cs                  # Tissue protection and absorption
│   └── BodyTests.cs                    # Body composition calculations
├── Survival/
│   └── SurvivalProcessorTests.cs       # Temperature, hunger, metabolism
├── Utils/
│   ├── SkillCheckCalculatorTests.cs    # Skill check formulas
│   └── UtilsTests.cs                   # Random number utilities
└── TestHelpers/
    ├── TestFixtures.cs                 # Factory methods for test objects
    └── TestConstants.cs                # Baseline values for assertions
```

### What's Tested

**High Priority (Calculation Systems)**:
- `SurvivalProcessor` - Temperature regulation, metabolism, hunger/thirst
- `AbilityCalculator` - Strength, speed, vitality, cold resistance formulas
- `CapacityCalculator` - Body capacities with cascading effects (blood loss → consciousness → movement)
- `DamageProcessor` - Damage penetration through tissue layers
- `SkillCheckCalculator` - Skill check success rates and XP rewards

**Low Priority (Utilities)**:
- Body composition properties (weight, fat %, muscle %)
- Random number generation (statistical tests)

### Unit Tests vs Integration Tests

- **Unit Tests** (xUnit): Test calculation logic and pure functions in isolation
- **Integration Tests** (TestModeIO): Test full game flow, menu navigation, and gameplay loops

Use unit tests for catching calculation errors and regressions. Use integration tests (`play_game.sh`) for gameplay flow and UX testing.

## Issue Tracking

**CRITICAL**: When you find bugs, balance issues, questionable functionality, or breaking exceptions during development or testing, **YOU MUST UPDATE ISSUES.md**.

**ISSUES.md** is the central issue tracker organized by severity:
- 🔴 **Breaking Exceptions** - Critical crashes that prevent gameplay
- 🟠 **Bugs** - Incorrect behavior that prevents intended functionality
- 🟡 **Questionable Functionality** - Works but may not be intended
- 🟢 **Balance & Immersion Issues** - Gameplay feel problems

**When to update ISSUES.md**:
1. Finding a bug during development or testing
2. Discovering unintended behavior
3. Identifying balance problems (survival too hard/easy, skills too weak/strong, etc.)
4. Encountering crashes or exceptions
5. Noticing UX issues that break immersion
6. Resolving/fixing an existing issue (mark as resolved with solution)

**Format for new issues**:
- Clear title describing the problem
- Severity level (High/Medium/Low)
- Reproduction steps
- Expected vs. actual behavior
- Impact on gameplay
- Suggested solutions (if applicable)

This file helps track technical debt and prioritize fixes. Always check it before starting new work to avoid duplicating efforts or breaking known workarounds.

## Status Documentation

**Keep status documents brief and focused**:
- Status docs in `dev/active/` should be concise summaries, not comprehensive references
- Include: what was done, files changed, build status, next steps, key design notes
- Refer to detailed dev docs in `dev/active/[feature]/` for full context, formulas, and rationale
- Goal: Quick status check, not full documentation

## Core Architecture

### Modular Design Philosophy

The codebase uses a **composition-over-inheritance** approach with systems organized into focused namespaces:

- **Core/** - Game entry point, world state, and configuration (Program.cs, World.cs, Config.cs)
- **Actions/** - Action system using builder pattern (`ActionBuilder`) to create context-aware dynamic actions
- **Actors/** - Base Actor class in root, with subfolders:
  - **Actors/Player/** - Player class and manager components (InventoryManager, LocationManager, StealthManager, etc.)
  - **Actors/NPCs/** - NPC, Animal classes and NPCFactory
- **Bodies/** - Hierarchical body part system with injury tracking, capacities, and body composition (fat/muscle)
- **Combat/** - Combat system (CombatManager, CombatNarrator, CombatUtils)
- **Crafting/** - Recipe-based crafting using property requirements and builder pattern
- **Effects/** - Buff/debuff system with EffectRegistry for tracking active effects
- **Environments/** - Zone → Location hierarchy, with subfolders:
  - **Environments/Features/** - Location features (ShelterFeature, HeatSourceFeature, ForageFeature, etc.)
  - **Environments/Factories/** - Location and Zone factories
- **Items/** - Item hierarchy with property-based crafting system (ItemProperty enum)
- **IO/** - Input/Output abstraction layer enabling multiple IO types (console, test mode, future web/GUI)
- **Magic/** - Spell system integrated with body part targeting
- **Skills/** - Skill-based progression (player-only, not NPCs)
- **Survival/** - SurvivalProcessor handles temperature, hunger, thirst, and exhaustion calculations

### Key Architectural Patterns

#### 0. IO Abstraction Layer (IO/)
All console interaction goes through the IO namespace to enable future alternative interfaces:

**Output.cs** - All text output
- `Write(params object[])` - Automatic type-based coloring (NPCs=Red, Items=Cyan, etc.)
- `WriteLine(params object[])` - Write with newline
- `WriteColored(ConsoleColor, params object[])` - Write with specific color (auto save/restore)
- `WriteLineColored(ConsoleColor, params object[])` - Write colored text with newline
- `WriteWarning(string)` - Yellow text for warnings
- `WriteDanger(string)` - Red text for danger
- `WriteSuccess(string)` - Green text for success
- TestMode support: Routes to `TestModeIO.WriteOutput()` when `TEST_MODE=1`

**Input.cs** - All user input
- `Read()` - Read line of text
- `ReadInt()` - Read and validate integer
- `ReadInt(low, high)` - Read integer within range
- `ReadYesNo()` - Read yes/no response
- `ReadKey(bool intercept)` - Read single key press
- `GetSelectionFromList<T>(List<T>)` - Display numbered list and get selection
- TestMode support: Routes to `TestModeIO.ReadInput()` when `TEST_MODE=1`

**Critical**: Never use `Console.Write`, `Console.WriteLine`, `Console.ReadLine`, `Console.ReadKey`, or `Console.ForegroundColor` outside the IO namespace. This keeps the codebase IO-agnostic for future web/GUI support.

#### 1. Action System (ActionFactory.cs)
Actions are built using a fluent builder pattern and return `IGameAction`:
- `.Do(ctx => ...)` - Execute action logic
- `.When(ctx => condition)` - Conditional availability
- `.ThenShow(ctx => [actions])` - Chain to next menu
- `.ThenReturn()` - Return to previous menu
- Context (`GameContext`) provides player, location, crafting manager access

**Important**: The action system automatically filters unavailable actions based on `.When()` conditions and handles menu flow.

#### 2. Body Part System (Bodies/)
- **Hierarchical structure**: BodyRegion → Tissues (Skin, Muscle, Bone) → Organs
- **Capacities**: Body parts contribute to capacities (Moving, Manipulation, etc.) which calculate stats
- **Body composition**: Tracks fat/muscle in kg with dynamic percentages affecting stats (Speed, Vitality)
- **Damage flow**: `Body.Damage(DamageInfo)` is the ONLY entry point → calls `DamageProcessor.DamageBody()`

#### 3. Survival Processing (SurvivalProcessor.cs)
- **Pure function design**: `Process(SurvivalData, minutesElapsed, effects)` returns `SurvivalProcessorResult`
- Handles calorie burn, hydration loss, temperature regulation with exponential heat transfer
- Generates dynamic effects (hypothermia, frostbite, sweating) based on thresholds
- Body temperature affects cold/heat resistance: fat increases cold resistance, insulation from clothing/shelter

#### 4. Crafting System (Crafting/)
- **Property-based**: Items have `ItemProperty` tags (Stone, Wood, Flammable, etc.)
- **Builder pattern**: `RecipeBuilder` creates recipes with property requirements
- **Result types**: Item, LocationFeature, or Shelter (new Location)
- Recipes check `CanCraft(player)` based on inventory properties and skill levels

#### 5. Effect System (Effects/)
- **EffectRegistry** per actor tracks active effects
- **EffectBuilder** creates effects with severity, capacity modifiers, temperature impacts
- Effects can target specific body parts or whole body
- `AllowMultiple(bool)` controls stacking behavior

### Critical Implementation Notes

#### Player vs NPC Distinction
- **Player**: Has Skills (SkillRegistry), uses composition via managers (InventoryManager, LocationManager)
- **NPCs**: Stats derived ONLY from body parts, no skills. Differentiated by body traits (e.g., Wolf has high Speed)
- Both inherit from `Actor` which provides Body, EffectRegistry, and CombatManager

#### Time and World Update
- `World.Update(minutes)` advances global time
- Each Actor's `Update()` method processes survival needs via `SurvivalProcessor`
- Actions should update time appropriately (e.g., crafting consumes `CraftingTimeMinutes`)

#### Game Loop Structure
Program.cs shows the main loop:
1. Player starts at a Location in a Zone
2. `ActionFactory.Common.MainMenu()` is the root action
3. Action execution is recursive via `ThenShow()` chaining
4. Context (`GameContext`) carries state between actions

### Data-Driven Design Goals

The README indicates ongoing migration to JSON-driven content for items, NPCs, locations, and recipes. Current implementation is code-based via factory classes:
- `ItemFactory` - Hardcoded item creation
- `NPCFactory` - Hardcoded NPC creation
- `RecipeBuilder` - Code-based recipe definition
- `LocationFactory` / `ZoneFactory` - Procedural generation logic

When adding new content, follow existing factory patterns until JSON migration is complete.

## Development Patterns

### Adding a New Action
1. Create action in appropriate section of `ActionFactory` (Combat, Inventory, Crafting, etc.)
2. Use `CreateAction(name)` to start builder
3. Add `.When()` conditions for availability
4. Implement logic in `.Do(ctx => ...)`
5. Chain with `.ThenShow()` or `.ThenReturn()`

### Adding a New Item
1. Add to `ItemFactory` as static method
2. Define `CraftingProperties` if craftable
3. For equippable items, implement `IEquippable` interface
4. For consumables, use `ConsumableItem` with Effects

### Adding a New Crafting Recipe
1. Use `RecipeBuilder` in `CraftingSystem.InitializeRecipes()`
2. Define property requirements via `.WithPropertyRequirement()`
3. Set skill requirements with `.RequiringSkill(skillName, level)`
4. Choose result type: `.ResultingInItem()`, `.ResultingInLocationFeature()`, or `.ResultingInStructure()`

### Adding a New Body Part Type
1. Modify `BodyPartFactory.CreateBody()` for the body type
2. Define hierarchical structure (regions → tissues → organs)
3. Set capacity contributions for each part
4. Update `BodyTypes` enum if adding new creature type

### Working with Effects
1. Use `EffectBuilder` via `EffectBuilderExtensions.CreateEffect(name)`
2. Chain modifiers: `.ReducesCapacity()`, `.AffectsTemperature()`, etc.
3. Set severity and duration behavior
4. Add to `EffectRegistry` via `AddEffect(effect)`

## Design Constraints

1. **Skills are player-only**: Never add skills to NPCs. Their abilities come from body stats alone.
2. **Single damage entry point**: Always use `Body.Damage(DamageInfo)`, never modify body parts directly.
3. **Pure survival processing**: `SurvivalProcessor.Process()` should remain stateless and return results.
4. **Explicit time passage**: Actions that take time must call `World.Update(minutes)` or `Body.Update(timespan, context)`.
5. **Property-based crafting**: Items contribute to recipes via properties, not item-specific checks.
6. **Action menu flow**: Use `.ThenShow()` and `.ThenReturn()` - never manually create menu loops.
7. **IO abstraction layer**: All console I/O must go through `Output.cs` and `Input.cs`. Never use `Console.Write`, `Console.WriteLine`, `Console.ReadLine`, `Console.ReadKey`, or `Console.ForegroundColor` directly outside the IO namespace. This enables future support for alternative IO types (web, GUI) without codebase-wide refactoring.

## Design Philosophy

**For comprehensive design philosophy, see README.md "Design Philosophy" section** which covers:
- Technical Principles (simplicity with depth, modularity, realism)
- Gameplay Design (Ice Age authenticity, survival over combat, emergent storytelling)
- Biome Design Philosophy (detailed gameplay roles for all 5 biomes)

**Additional design decisions from recent development:**

### Skill Check Philosophy
- Base success rates (30-50%) balanced with skill progression (+10%/level)
- Clamped at 5%-95% to preserve player agency (no pure RNG gates)
- Failure grants XP (1 XP) to encourage experimentation and learning
- Used for realistic uncertainty (fire-making, hunting) not arbitrary difficulty

### Fire Management Philosophy
- **Ember system**: Matches reality (fires leave embers), reduces tedium, rewards proactive management
- **35% heat / 25% duration**: Tested values providing meaningful grace period without eliminating time pressure
- **Auto-relight from embers**: UX decision tested against manual relight (manual rejected as too punishing)
- **Main menu accessibility**: Fire is a "fundamental feature" not buried in submenus (survival-critical)

### Progression Philosophy
- **Material gating**: Stone → Flint → Bone → Obsidian (geographic scarcity increases with tier)
- **Skill gating**: Used sparingly, never blocking essential survival tools
- **Tool tiers**: 4-5 tiers per weapon type (progression feels meaningful, not overwhelming)

### Time Design Philosophy
- **15-minute foraging intervals**: Enables fire tending (4x per hour), granular time management
- **8-hour fire capacity**: Realistic overnight burn time, reduces micromanagement
- **Action pacing**: Crafting times balanced for realism vs fun (5 min simple, 60+ min complex)

### Balance Philosophy
- **Day-1 viability**: Forest biome must support survival path without perfect RNG
- **Scarcity with accessibility**: Every material matters, but not punishingly rare
- **Biome difficulty**: Forest (forgiving) → Plains/Hillside (challenging) → Cave (advanced/prepared)

When adding content, prioritize thematic consistency with Ice Age setting and survival focus over traditional RPG mechanics.

---

## Maintaining Documentation

**This file and the documentation folder must stay synchronized**:
- When architectural patterns change, update both
- When new systems are added, document them in `documentation/`
- When design constraints evolve, reflect changes in both places
- If something seems outdated or contradictory, ask the user to clarify and update accordingly

The `documentation/` folder contains **detailed reference guides**, while this CLAUDE.md is the **quick overview**.
- Make sure to consult the current plan in ./dev/active
- When you are done with a dev task, you can move the folder from the active directory to the complete dir
- When you find issues, document them in ISSUES.md and when you resolve them, remove them from the file
- Before adding special code for handling 'legacy' or 'backwards compatibility' stop and consult with the user how to proceed. Typically it's a code smell.
- Document all changes by updating the dev/CURRENT-STATUS.md
- For game design refer to the README especially the Design Philosophy section
- Whenever the user shares a new piece of game design philosophy make sure to add it to the readme
- After creating a plan, if it involves significant code changes, please invoke the plan-reviewer-agent