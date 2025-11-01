# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

## Core Architecture

### Modular Design Philosophy

The codebase uses a **composition-over-inheritance** approach with systems organized into focused namespaces:

- **Bodies/** - Hierarchical body part system with injury tracking, capacities, and body composition (fat/muscle)
- **Actors/** - Base Actor class with Player and NPC subclasses; uses composition via managers (CombatManager, InventoryManager, etc.)
- **Actions/** - Action system using builder pattern (`ActionBuilder`) to create context-aware dynamic actions
- **Items/** - Item hierarchy with property-based crafting system (ItemProperty enum)
- **Environments/** - Zone → Location hierarchy with Features (ShelterFeature, HeatSourceFeature, ForageFeature)
- **Survival/** - SurvivalProcessor handles temperature, hunger, thirst, and exhaustion calculations
- **Crafting/** - Recipe-based crafting using property requirements and builder pattern
- **Effects/** - Buff/debuff system with EffectRegistry for tracking active effects
- **Magic/** - Spell system integrated with body part targeting
- **Level/** - Skill-based progression (player-only, not NPCs)

### Key Architectural Patterns

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

## Ice Age Thematic Direction

Per the README roadmap, the game is moving away from generic RPG elements toward Ice Age authenticity:
- Replace generic item names with period-appropriate equivalents (flint, bone, hide, etc.)
- Shamanistic magic requires physical components (herbs, bones, ritual items)
- Focus on survival mechanics over combat/leveling
- Emphasize environmental challenges (cold, scarcity, weather)

When adding content, prioritize thematic consistency with the Ice Age setting and survival focus over traditional RPG mechanics.
