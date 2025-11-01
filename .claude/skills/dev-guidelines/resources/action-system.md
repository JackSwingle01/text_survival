# Action System - Complete Guide

Complete guide to the Action system with ActionBuilder pattern, menu flow, GameContext, ActionFactory organization, and common extensions.

## Table of Contents

- [Overview](#overview)
- [ActionBuilder Pattern](#actionbuilder-pattern)
- [Builder Methods](#builder-methods)
- [GameContext](#gamecontext)
- [Action Flow Control](#action-flow-control)
- [ActionFactory Organization](#actionfactory-organization)
- [Extension Methods](#extension-methods)
- [Common Patterns](#common-patterns)
- [Best Practices](#best-practices)
- [Complete Examples](#complete-examples)

---

## Overview

The Action system is the core of player interaction in Text Survival. Every player choice is an action built using the fluent ActionBuilder API. Actions are:

- **Context-aware**: Access to player, location, inventory via GameContext
- **Conditionally available**: `.When()` conditions control visibility
- **Chainable**: `.ThenShow()` creates menu flow
- **Extensible**: Extension methods for common patterns

**Key Files:**
- `Actions/ActionBuilder.cs` - Builder implementation
- `Actions/ActionFactory.cs` - Organized action creation
- `Actions/DynamicAction.cs` - Action implementation
- `Actions/ActionBuilderExtensions.cs` - Extension methods

---

## ActionBuilder Pattern

### Basic Structure

```csharp
public static IGameAction MyAction()
{
    return ActionFactory.CreateAction("Action Name")
        .When(ctx => /* availability condition */)
        .Do(ctx => /* action logic */)
        .ThenShow(ctx => /* next menu */)
        .Build();
}
```

### Fluent API Design

ActionBuilder uses method chaining for readability:

```csharp
return CreateAction("Hunt Wildlife")
    .When(ctx => ctx.CurrentLocation.HasWildlife())         // Condition
    .ShowMessage("You spend an hour hunting...")             // Message
    .Do(ctx => {                                             // Logic
        var prey = ctx.CurrentLocation.GetRandomAnimal();
        if (ctx.Player.Skills.GetLevel("Hunting") >= prey.Difficulty)
        {
            ctx.Player.Inventory.AddItem(prey.Corpse);
            Output.WriteLine($"You successfully hunted a {prey.Name}!");
        }
        else
        {
            Output.WriteLine("The animal escaped.");
        }
        World.Update(60);
    })
    .AndGainExperience("Hunting", 15)                        // Extension
    .ThenShow(ctx => [Hunt(), Common.Return()])              // Next menu
    .Build();                                                 // Build action
```

---

## Builder Methods

### .Named(string name)

Sets the action name displayed to the player.

```csharp
.Named("Forage for food")
```

**Note**: Usually provided via `CreateAction(name)` instead.

### .When(Func<GameContext, bool> condition)

Adds availability condition. Multiple `.When()` calls are combined with AND logic.

```csharp
// Single condition
.When(ctx => ctx.CurrentLocation.GetFeature<ForageFeature>() != null)

// Multiple conditions (all must be true)
.When(ctx => ctx.Player.Inventory.Has(ItemProperty.Sharp))
.When(ctx => ctx.Player.Skills.GetLevel("Crafting") >= 2)
```

**Behavior**: Actions with false `.When()` conditions are automatically filtered from menus.

### .Do(Action<GameContext> action)

Executes action logic. Can have multiple `.Do()` calls that execute sequentially.

```csharp
.Do(ctx => {
    var item = ctx.Player.Inventory.GetRandomItem();
    ctx.Player.Inventory.RemoveItem(item);
    Output.WriteLine($"You dropped the {item.Name}");
})
```

### .ThenShow(Func<GameContext, List<IGameAction>> nextActions)

Defines next menu after action executes. Returns list of available actions.

```csharp
.ThenShow(ctx => [
    ContinueForaging(),
    ExamineFindings(),
    Common.Return("Stop foraging")
])
```

### .ThenReturn()

Returns to previous menu. Shorthand for empty next actions.

```csharp
.ThenReturn()

// Equivalent to:
.ThenShow(_ => [])
```

### .WithPrompt(string prompt)

Sets custom prompt message (rarely used; most use default).

```csharp
.WithPrompt("What would you like to craft?")
```

### .Build()

Builds and returns the `IGameAction` instance. Always the final call.

```csharp
.Build()
```

---

## GameContext

Every action receives a `GameContext` providing access to game state:

```csharp
public class GameContext
{
    public Player Player { get; }
    public Location CurrentLocation { get; }
    public CraftingManager CraftingManager { get; }
}
```

### Common Usage Patterns

```csharp
// Access player
.Do(ctx => ctx.Player.Inventory.AddItem(newItem))

// Access current location
.When(ctx => ctx.CurrentLocation.GetFeature<ShelterFeature>() != null)

// Access crafting
.Do(ctx => ctx.CraftingManager.Craft(recipe, ctx.Player))

// Access player skills
.When(ctx => ctx.Player.Skills.GetLevel("Hunting") >= 3)

// Access player body
.Do(ctx => ctx.Player.Body.Damage(damageInfo))
```

---

## Action Flow Control

### Menu Hierarchy

Actions create tree-like menu structures:

```
Main Menu
├── Look Around
├── Forage
│   ├── Keep Foraging
│   └── Stop Foraging (ThenReturn)
├── Inventory
│   ├── Inspect Item X
│   ├── Drop Item X
│   └── Back
└── Sleep
```

### ThenShow: Forward Navigation

Use `.ThenShow()` to create sub-menus:

```csharp
public static IGameAction MainMenu()
{
    return CreateAction("Main Menu")
        .ThenShow(ctx => [
            LookAround(),
            Forage(),
            OpenInventory(),
            Sleep()
        ])
        .Build();
}
```

### ThenReturn: Backward Navigation

Use `.ThenReturn()` to go back:

```csharp
public static IGameAction DescribeItem(Item item)
{
    return CreateAction($"Inspect {item}")
        .Do(_ => item.Describe())
        .ThenReturn()  // Returns to inventory menu
        .Build();
}
```

### Dynamic Menus

Generate menu options based on game state:

```csharp
.ThenShow(ctx => {
    var options = new List<IGameAction>();

    // Add dynamic options
    foreach (var item in ctx.Player.Inventory.GetItems())
    {
        options.Add(DescribeItem(item));
        options.Add(DropItem(item));
    }

    // Always add return option
    options.Add(Common.Return());

    return options;
})
```

---

## ActionFactory Organization

Actions are organized in static nested classes within `ActionFactory`:

```csharp
public static class ActionFactory
{
    public static class Common
    {
        public static IGameAction Return(string name = "Back") { }
        public static IGameAction MainMenu() { }
    }

    public static class Survival
    {
        public static IGameAction Forage() { }
        public static IGameAction Sleep() { }
    }

    public static class Movement
    {
        public static IGameAction Move() { }
        public static IGameAction Travel() { }
    }

    public static class Inventory
    {
        public static IGameAction OpenInventory() { }
        public static IGameAction DescribeItem(Item item) { }
    }

    public static class Crafting
    {
        public static IGameAction OpenCraftingMenu() { }
        public static IGameAction CraftItem(Recipe recipe) { }
    }

    public static class Combat
    {
        public static IGameAction Attack(NPC target) { }
        public static IGameAction Flee() { }
    }
}
```

### Usage Pattern

```csharp
// In code:
ActionFactory.Survival.Forage()
ActionFactory.Common.MainMenu()
ActionFactory.Combat.Attack(wolf)

// In ThenShow:
.ThenShow(ctx => [
    Survival.Forage(),
    Inventory.OpenInventory(),
    Common.Return()
])
```

---

## Extension Methods

Common patterns are implemented as extension methods in `ActionBuilderExtensions`:

### .ShowMessage(string message)

Displays message when action executes:

```csharp
.ShowMessage("You forage for 1 hour")
```

### .AndGainExperience(string skill, int amount = 10)

Grants skill experience:

```csharp
.AndGainExperience("Foraging")      // Default 10 XP
.AndGainExperience("Hunting", 25)   // Custom amount
```

### .AndPassTime(int minutes)

Updates world time:

```csharp
.AndPassTime(60)  // 1 hour
```

### Common Chaining

```csharp
return CreateAction("Craft Stone Knife")
    .ShowMessage("You spend 30 minutes crafting...")
    .Do(ctx => {
        ctx.CraftingManager.Craft(recipe, ctx.Player);
    })
    .AndGainExperience("Crafting", 20)
    .AndPassTime(30)
    .ThenReturn()
    .Build();
```

---

## Common Patterns

### Pattern 1: Simple Action (No Sub-Menu)

```csharp
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
        .When(ctx => ctx.Player.Body.IsTired)
        .Do(ctx => {
            Output.WriteLine("How many hours would you like to sleep?");
            ctx.Player.Body.Rest(Input.ReadInt() * 60);
        })
        .ThenReturn()
        .Build();
}
```

### Pattern 2: Repeatable Action

```csharp
public static IGameAction Forage()
{
    return CreateAction("Forage")
        .When(ctx => ctx.CurrentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx => {
            var feature = ctx.CurrentLocation.GetFeature<ForageFeature>()!;
            feature.Forage(1);
        })
        .AndGainExperience("Foraging")
        .ThenShow(_ => [
            Forage("Keep foraging"),
            Common.Return("Finish foraging")
        ])
        .Build();
}
```

### Pattern 3: Parameterized Actions

```csharp
public static IGameAction GoToLocation(Location location)
{
    return CreateAction($"Go to {location.Name}{(location.Visited ? " (Visited)" : "")}")
        .When(_ => location.IsFound)
        .Do(ctx => location.Interact(ctx.Player))
        .ThenReturn()
        .Build();
}

// Usage:
.ThenShow(ctx =>
    ctx.CurrentLocation
       .GetNearbyLocations()
       .Select(loc => GoToLocation(loc))
       .ToList()
)
```

### Pattern 4: Conditional Display

```csharp
public static IGameAction MainMenu()
{
    return CreateAction("Main Menu")
        .Do(ctx => BodyDescriber.DescribeSurvivalStats(ctx.Player.Body))
        .ThenShow(ctx => [
            Describe.LookAround(),
            Survival.Forage(),
            Inventory.OpenInventory(),
            Crafting.OpenCraftingMenu(),
            // Only show sleep if tired
            ctx.Player.Body.IsTired ? Survival.Sleep() : null,
            Movement.Move()
        ].Where(a => a != null).Cast<IGameAction>().ToList())
        .Build();
}
```

### Pattern 5: Time-Consuming Action

```csharp
public static IGameAction CraftItem(Recipe recipe)
{
    return CreateAction($"Craft {recipe.Name}")
        .When(ctx => recipe.CanCraft(ctx.Player))
        .ShowMessage($"You spend {recipe.CraftingTimeMinutes} minutes crafting...")
        .Do(ctx => {
            // Consume materials
            recipe.ConsumeMaterials(ctx.Player.Inventory);

            // Update time
            World.Update(recipe.CraftingTimeMinutes);

            // Create result
            var item = recipe.CreateResult();
            ctx.Player.Inventory.AddItem(item);

            Output.WriteLine($"You crafted a {item.Name}!");
        })
        .AndGainExperience("Crafting", recipe.ExperienceReward)
        .ThenReturn()
        .Build();
}
```

---

## Best Practices

### ✅ DO: Keep Actions Focused

```csharp
// Good: One clear purpose
public static IGameAction Sleep() { ... }
public static IGameAction Eat(Item food) { ... }
public static IGameAction Drink(Item water) { ... }
```

### ✅ DO: Use Extension Methods

```csharp
// Good: Readable, concise
.ShowMessage("Foraging...")
.AndGainExperience("Foraging")
.AndPassTime(60)
```

### ✅ DO: Filter with .When()

```csharp
// Good: Action only appears when valid
.When(ctx => ctx.Player.Inventory.GetWeight() < ctx.Player.MaxCarryWeight)
```

### ✅ DO: Organize by Category

```csharp
// Good: Clear organization
ActionFactory.Survival.Forage()
ActionFactory.Combat.Attack(target)
ActionFactory.Crafting.OpenMenu()
```

### ❌ DON'T: Create Manual Loops

```csharp
// Bad: Manual loop
while (true)
{
    ShowMenu();
    var choice = GetChoice();
    if (choice == "back") break;
}

// Good: Use .ThenShow() recursion
.ThenShow(ctx => [ContinueAction(), Common.Return()])
```

### ❌ DON'T: Forget Time Updates

```csharp
// Bad: Crafting with no time passage
.Do(ctx => {
    ctx.Player.Inventory.AddItem(craftedItem);
})

// Good: Update time appropriately
.Do(ctx => {
    World.Update(30);  // 30 minutes
    ctx.Player.Inventory.AddItem(craftedItem);
})
```

### ❌ DON'T: Hardcode Availability

```csharp
// Bad: Check in .Do()
.Do(ctx => {
    if (!ctx.Player.Inventory.Has(ItemProperty.Sharp))
    {
        Output.WriteLine("You need a sharp tool!");
        return;
    }
    // ... rest of logic
})

// Good: Check in .When()
.When(ctx => ctx.Player.Inventory.Has(ItemProperty.Sharp))
.Do(ctx => {
    // Logic only runs if condition met
})
```

---

## Complete Examples

### Example 1: Forage Action (From Codebase)

```csharp
public static IGameAction Forage(string name = "Forage")
{
    return CreateAction("Forage")
        .When(ctx => ctx.CurrentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx =>
        {
            var forageFeature = ctx.CurrentLocation.GetFeature<ForageFeature>()!;
            forageFeature.Forage(1);
        })
        .AndGainExperience("Foraging")
        .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
        .Build();
}
```

### Example 2: Move Action (From Codebase)

```csharp
public static IGameAction Move()
{
    return CreateAction("Go somewhere else")
        .When(ctx => AbilityCalculator.CalculateVitality(ctx.Player.Body) > .2)
        .Do(ctx =>
        {
            var locations = ctx.CurrentLocation.GetNearbyLocations()
                                               .Where(l => l.IsFound)
                                               .ToList();
            bool inside = ctx.Player.CurrentLocation.GetFeature<ShelterFeature>() != null;

            if (inside)
            {
                Output.WriteLine($"You can leave the shelter and go outside.");
            }
            if (locations.Count == 0)
            {
                Output.WriteLine("You don't see anywhere noteworthy nearby; you can stay here or travel to a new area.");
                return;
            }
            else if (locations.Count == 1)
            {
                Output.WriteLine($"You can go to the {locations[0].Name} or pack up and leave the region.");
            }
            else
            {
                Output.WriteLine("You see several places that you can go to from here, or you can pack up and leave the region.");
            }
        })
        .ThenShow(ctx =>
        {
            var options = new List<IGameAction>();
            foreach (var location in ctx.CurrentLocation.GetNearbyLocations())
            {
                options.Add(GoToLocation(location));
            }
            options.Add(Travel());
            options.Add(Common.Return("Stay Here..."));
            return options;
        })
        .Build();
}
```

### Example 3: Combat Action

```csharp
public static IGameAction Attack(NPC target)
{
    return CreateAction($"Attack {target.Name}")
        .When(ctx => ctx.Player.Body.GetCapacityValue(CapacityNames.Moving) > 0.3)
        .Do(ctx =>
        {
            // Calculate attack
            var damage = ctx.Player.CombatManager.CalculateDamage(target);
            var damageInfo = new DamageInfo
            {
                Amount = damage,
                DamageType = DamageType.Slashing,
                TargetRegion = target.Body.GetRandomVitalRegion()
            };

            // Apply damage
            target.Body.Damage(damageInfo);

            // Enemy counterattack
            if (target.IsAlive)
            {
                var counterDamage = target.CombatManager.CalculateDamage(ctx.Player);
                ctx.Player.Body.Damage(new DamageInfo
                {
                    Amount = counterDamage,
                    DamageType = DamageType.Piercing,
                    TargetRegion = ctx.Player.Body.GetRandomRegion()
                });
            }

            World.Update(5);  // Combat takes 5 minutes
        })
        .AndGainExperience("Combat", 20)
        .ThenShow(ctx => target.IsAlive
            ? [Attack(target), Flee(), UseItem()]
            : [LootBody(target), Common.Return()])
        .Build();
}
```

---

**Related Files:**
- [SKILL.md](../SKILL.md) - Main guidelines
- [builder-patterns.md](builder-patterns.md) - Builder pattern philosophy
- [complete-examples.md](complete-examples.md) - Full feature examples

**Last Updated**: 2025-11-01
