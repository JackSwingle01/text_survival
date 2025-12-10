---
name: dev-guidelines
description: Comprehensive C# development guide for Text Survival RPG. Use when creating actions with builder pattern, implementing body/damage systems, survival mechanics, crafting recipes, effects/buffs, or working with composition-based architecture. Covers ActionBuilder fluent API, property-based crafting, pure function survival processing, hierarchical body parts, manager composition (Player/NPC), factory patterns, Ice Age thematic consistency, and common anti-patterns. Essential for understanding builder pattern philosophy, When/Do/ThenShow action flow, Body.Damage entry point, EffectRegistry, RecipeBuilder, and skill vs NPC distinction.
---

# C# Game Development Guidelines

## Purpose

Establish consistency and best practices for developing the Text Survival RPG, a C# .NET 9.0 console-based survival game with modular architecture, builder pattern philosophy, and Ice Age thematic focus.

## When to Use This Skill

Automatically activates when working on:
- Creating or modifying actions (ActionFactory, ActionBuilder)
- Implementing body systems, damage, or healing
- Building survival mechanics (temperature, hunger, thirst)
- Creating crafting recipes or item properties
- Implementing effects, buffs, or debuffs
- Working with Player/NPC composition and managers
- Adding new items, creatures, or game content
- Refactoring game systems for better architecture

---

## Quick Start

### New Game Feature Checklist

- [ ] **Action**: Use ActionBuilder with .When().Do().ThenShow()
- [ ] **Time**: Update World.Update(minutes) if action takes time
- [ ] **Factory**: Add to appropriate factory (Item, NPC, Spell, etc.)
- [ ] **Builder**: Use builder pattern for recipes, effects, or actions
- [ ] **Properties**: Use property-based checks (not item-specific)
- [ ] **Thematic**: Use Ice Age appropriate naming (flint, hide, bone)
- [ ] **Testing**: Verify in-game functionality

### New Game System Checklist

- [ ] Namespace organization (Actions/, Bodies/, Crafting/, etc.)
- [ ] Builder pattern for complex creation
- [ ] Factory for simple content creation
- [ ] Manager for Player-specific features (if applicable)
- [ ] Pure functions where possible (see SurvivalProcessor)
- [ ] Integration with existing systems
- [ ] Extension methods for common patterns

---

## Architecture Overview

### Composition-Based Design

```
Player (Actor subclass)
    ├── Body (hierarchical parts)
    ├── EffectRegistry (buffs/debuffs)
    ├── CombatManager (shared with NPCs)
    ├── InventoryManager (Player-only)
    ├── LocationManager (Player-only)
    └── SkillRegistry (Player-only)

NPC (Actor subclass)
    ├── Body (stats from body only)
    ├── EffectRegistry
    └── CombatManager
```

**Key Principle:** Player gets skills + managers, NPCs get body-based stats only.

---

## Directory Structure

```
text_survival/
├── Actions/            # ActionBuilder, ActionFactory, DynamicAction
├── Actors/             # Player, Actor (base), NPC
├── Bodies/             # Body, BodyPart, DamageProcessor, Capacities
├── Crafting/           # RecipeBuilder, ItemProperty, CraftingSystem
├── Effects/            # EffectBuilder, EffectRegistry, DynamicEffect
├── Environments/       # Zone, Location, Features
├── Items/              # Item hierarchy, ItemFactory
├── Level/              # SkillRegistry (Player-only)
├── Survival/           # SurvivalProcessor, SurvivalData
├── Magic/              # Spell system
├── PlayerComponents/   # Manager classes
├── IO/                 # Console I/O, GameContext
└── Utils/              # Helpers

```

**Naming Conventions:**
- Builders: `ActionBuilder`, `RecipeBuilder`, `EffectBuilder`
- Factories: `ItemFactory`, `NPCFactory`, `BodyPartFactory`
- Managers: `InventoryManager`, `CombatManager`, `LocationManager`
- Processors: `SurvivalProcessor`, `DamageProcessor`

---

## Core Principles (10 Key Rules)

### 1. Builder Pattern Everywhere

```csharp
// ✅ ALWAYS: Use fluent builder API
var action = ActionFactory.CreateAction("Hunt")
    .When(ctx => ctx.CurrentLocation.HasWildlife())
    .Do(ctx => { /* hunting logic */ })
    .ThenShow(ctx => [ContinueHunting(), ReturnToLocation()])
    .Build();

// ❌ NEVER: Direct instantiation
var action = new DynamicAction("Hunt", ...); // Don't do this
```

### 2. Composition Over Inheritance

```csharp
// ✅ ALWAYS: Use managers for features
public class Player : Actor
{
    public InventoryManager Inventory { get; }
    public LocationManager LocationManager { get; }
    public SkillRegistry Skills { get; } // Player-only!
}

// ❌ NEVER: Add Player features to Actor base
public class Actor
{
    public Skills { get; } // NO! Makes NPCs have skills
}
```

### 3. Single Damage Entry Point

```csharp
// ✅ ALWAYS: Use Body.Damage()
player.Body.Damage(new DamageInfo
{
    Amount = 15,
    DamageType = DamageType.Slashing,
    TargetRegion = BodyRegion.Torso
});

// ❌ NEVER: Modify body parts directly
player.Body.GetPart("Torso").Health -= 15; // DON'T!
```

### 4. Pure Functions for Processing

```csharp
// ✅ ALWAYS: Stateless survival processing
var result = SurvivalProcessor.Process(
    survivalData,
    minutesElapsed,
    activeEffects
);
// Returns new result, doesn't mutate input

// ❌ NEVER: Stateful processing
processor.UpdateSurvival(player); // Don't store state
```

### 5. Property-Based Crafting

```csharp
// ✅ ALWAYS: Check item properties
recipe.WithPropertyRequirement(ItemProperty.Stone, 1)
       .WithPropertyRequirement(ItemProperty.Wood, 0.5);

// ❌ NEVER: Item-specific checks
if (inventory.Has("Sharp Rock") && inventory.Has("Stick")) // NO!
```

### 6. Action Menu Flow

```csharp
// ✅ ALWAYS: Use .ThenShow() for navigation
.ThenShow(ctx => [ContinueAction(), GoBack()])

// ✅ ALWAYS: Use .ThenReturn() to go back
.ThenReturn()

// ❌ NEVER: Manual menu loops
while (true) { ShowMenu(); } // Don't do this
```

### 7. Player vs NPC Distinction

```csharp
// ✅ CORRECT: Skills are Player-only
if (player.Skills.GetLevel("Hunting") >= 3)

// ❌ WRONG: NPCs don't have skills
if (npc.Skills.GetLevel("Hunting") >= 3) // Compile error!

// ✅ CORRECT: NPCs derive from body
var npcSpeed = npc.Body.GetCapacityValue(CapacityNames.Moving);
```

### 8. Explicit Time Passage

```csharp
// ✅ ALWAYS: Update time when action takes time
.Do(ctx => {
    // Crafting takes 30 minutes
    World.Update(30);
    ctx.Player.Inventory.AddItem(craftedItem);
})

// ❌ NEVER: Forget to update time
.Do(ctx => {
    // This should take time but doesn't!
    ctx.Player.Inventory.AddItem(craftedItem);
})
```

### 9. Ice Age Thematic Consistency

```csharp
// ✅ GOOD: Period-appropriate names
ItemFactory.CreateFlintKnife()
ItemFactory.CreateMammothHide()
ItemFactory.CreateBoneNeedle()

// ❌ BAD: Generic RPG names
ItemFactory.CreateSteelSword()
ItemFactory.CreateHealthPotion()
ItemFactory.CreateMagicStaff()
```

### 10. Factory for Content, Builder for Systems

```csharp
// ✅ FACTORY: Simple content creation
var knife = ItemFactory.CreateFlintKnife();
var wolf = NPCFactory.CreateWolf();

// ✅ BUILDER: Complex system creation
var recipe = new RecipeBuilder()
    .Named("Flint Knife")
    .RequiringSkill("Crafting", 1)
    .WithPropertyRequirement(ItemProperty.Stone, 1)
    .ResultingInItem(ItemFactory.CreateFlintKnife)
    .Build();
```

---

## Common Patterns

### ActionBuilder Fluent API

```csharp
return ActionFactory.CreateAction("Forage")
    .When(ctx => ctx.CurrentLocation.GetFeature<ForageFeature>() != null)
    .ShowMessage("You forage for an hour...")
    .Do(ctx => {
        var feature = ctx.CurrentLocation.GetFeature<ForageFeature>();
        var item = feature.Forage();
        ctx.Player.Inventory.AddItem(item);
        World.Update(60);
    })
    .AndGainExperience("Foraging", 10)
    .ThenShow(ctx => [Forage(), Common.Return()])
    .Build();
```

### EffectBuilder Pattern

```csharp
var hypothermia = EffectBuilderExtensions.CreateEffect("Hypothermia")
    .WithSeverity(0.3)
    .ReducesCapacity(CapacityNames.Moving, 0.2)
    .ReducesCapacity(CapacityNames.Consciousness, 0.15)
    .WithApplyMessage("You're getting dangerously cold!")
    .AllowMultiple(false)
    .Build();

player.EffectRegistry.AddEffect(hypothermia);
```

### RecipeBuilder Pattern

```csharp
return new RecipeBuilder()
    .Named("Stone Axe")
    .WithDescription("A crude but effective chopping tool")
    .RequiringSkill("Crafting", 2)
    .RequiringCraftingTime(45)
    .WithPropertyRequirement(ItemProperty.Stone, 2)
    .WithPropertyRequirement(ItemProperty.Wood, 1)
    .WithPropertyRequirement(ItemProperty.Binding, 0.5)
    .ResultingInItem(ItemFactory.CreateStoneAxe)
    .Build();
```

---

## Quick Reference

### Common Namespaces

| Namespace | Purpose |
|-----------|---------|
| text_survival.Actions | Action system, builders, factory |
| text_survival.Actors | Player, NPC, Actor base |
| text_survival.Bodies | Body parts, damage, capacities |
| text_survival.Crafting | Recipes, properties, crafting |
| text_survival.Effects | Effects, buffs, debuffs |
| text_survival.Environments | Zones, locations, features |
| text_survival.Items | Item hierarchy, properties |
| text_survival.Level | Skills (Player-only) |
| text_survival.Survival | Survival processing |

### Builder Method Chaining

| Builder | Key Methods |
|---------|-------------|
| ActionBuilder | .Named() .When() .Do() .ThenShow() .ThenReturn() |
| EffectBuilder | .WithSeverity() .ReducesCapacity() .AffectsTemperature() |
| RecipeBuilder | .Named() .RequiringSkill() .WithPropertyRequirement() |

### Extension Methods

```csharp
// Action extensions
.ShowMessage(string)
.AndGainExperience(skill, amount)
.AndPassTime(minutes)

// Effect extensions
.Bleeding()
.Poisoned()
.HealingEffect()
```

---

## Anti-Patterns to Avoid

❌ **Adding skills to NPCs** - Skills are Player-only, NPCs derive stats from body

❌ **Direct body part modification** - Always use Body.Damage(DamageInfo)

❌ **Stateful survival processing** - Keep SurvivalProcessor.Process() pure

❌ **Forgetting time passage** - Actions must call World.Update(minutes)

❌ **Item-specific crafting checks** - Use property-based requirements

❌ **Manual menu loops** - Use .ThenShow() and .ThenReturn()

❌ **Breaking composition** - Don't add Player features to Actor base

❌ **Generic naming** - Use Ice Age thematic terms (flint, hide, bone)

❌ **Direct instantiation** - Use builders and factories

❌ **Mutating processor inputs** - Return new results, don't modify parameters

---

## Navigation Guide

| Need to... | Read this |
|------------|-----------|
| Create actions with builder pattern | [action-system.md](documentation/action-system.md) |
| Understand body hierarchy and damage | [body-and-damage.md](documentation/body-and-damage.md) |
| Implement survival mechanics | [survival-processing.md](documentation/survival-processing.md) |
| Create crafting recipes | [crafting-system.md](documentation/crafting-system.md) |
| Add effects/buffs/debuffs | [effect-system.md](documentation/effect-system.md) |
| Learn builder pattern philosophy | [builder-patterns.md](documentation/builder-patterns.md) |
| Understand Player/NPC architecture | [composition-architecture.md](documentation/composition-architecture.md) |
| Create items, NPCs, content | [factory-patterns.md](documentation/factory-patterns.md) |
| Handle errors and validation | [error-handling-and-validation.md](documentation/error-handling-and-validation.md) |
| See complete examples | [complete-examples.md](documentation/complete-examples.md) |

---

## Resource Files

### [action-system.md](documentation/action-system.md)
Complete guide to ActionBuilder pattern, menu flow, GameContext, ActionFactory organization, and common extensions.

### [body-and-damage.md](documentation/body-and-damage.md)
Body hierarchy (Region → Tissue → Organ), capacity system, body composition (fat/muscle), damage processing, and healing mechanics.

### [survival-processing.md](documentation/survival-processing.md)
Pure function survival processing, temperature regulation, calorie burn, hydration, effect generation, and body composition impacts.

### [crafting-system.md](documentation/crafting-system.md)
Property-based crafting with RecipeBuilder, ItemProperty enum, skill requirements, and result types (Item/Feature/Structure).

### [effect-system.md](documentation/effect-system.md)
EffectBuilder pattern, EffectRegistry usage, severity system, capacity modifiers, temperature effects, and common patterns.

### [builder-patterns.md](documentation/builder-patterns.md)
Cross-cutting builder pattern philosophy, fluent API design, consistency across systems, and extension method patterns.

### [composition-architecture.md](documentation/composition-architecture.md)
Actor/Player/NPC hierarchy, manager composition, skills vs body-based stats, and separation of concerns.

### [factory-patterns.md](documentation/factory-patterns.md)
ItemFactory, NPCFactory, BodyPartFactory, SpellFactory patterns, static method organization, and future JSON migration notes.

### [error-handling-and-validation.md](documentation/error-handling-and-validation.md)
C# exception handling, guard clauses, input validation, error propagation, and async error patterns.

### [complete-examples.md](documentation/complete-examples.md)
Full feature implementations: new action, new item + recipe, new effect, new body part type, new NPC.

---

## Related Skills

- **skill-developer** - Meta-skill for creating and managing skills
- **brainstorming** - Design refinement using Socratic method
- **executing-plans** - Execute implementation plans in controlled batches

---

**Skill Status**: COMPLETE ✅
**Line Count**: < 500 ✅
**Progressive Disclosure**: 10 resource files ✅
**Last Updated**: 2025-11-01
