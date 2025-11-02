# Builder Patterns - Fluent API Design

Cross-cutting guide to builder pattern philosophy, method chaining, context passing, and consistency across game systems.

## Table of Contents

- [Builder Pattern Philosophy](#builder-pattern-philosophy)
- [Fluent API Principles](#fluent-api-principles)
- [Method Chaining Best Practices](#method-chaining-best-practices)
- [Context Passing Patterns](#context-passing-patterns)
- [Builder Consistency Across Systems](#builder-consistency-across-systems)
- [Extension Methods for Builders](#extension-methods-for-builders)
- [When to Use Builder vs Factory](#when-to-use-builder-vs-factory)

---

## Builder Pattern Philosophy

**Core Principle**: Builders create complex objects with many optional parameters through a fluent, readable interface.

**Why Builders Dominate This Codebase**:
1. **Readability**: Code reads like natural language
2. **Flexibility**: Optional parameters without constructor explosion
3. **Discoverability**: IDE autocomplete reveals available options
4. **Safety**: Required parameters enforced via `.Build()`
5. **Extensibility**: Add new options without breaking existing code

**Three Primary Builders**:
- **ActionBuilder** - Creates dynamic game actions with conditional logic
- **RecipeBuilder** - Creates crafting recipes with property requirements
- **EffectBuilder** - Creates status effects with capacity modifiers

**Common Pattern**:
```csharp
var result = new Builder()
    .RequiredMethod("value")      // Set required fields
    .OptionalMethod(value)        // Set optional fields
    .AnotherOptionalMethod(value) // Chain as needed
    .Build();                     // Validate and construct
```

---

## Fluent API Principles

### Principle 1: Every Method Returns `this`

**Rule**: Builder methods return the builder instance for chaining.

```csharp
public class ActionBuilder
{
    public ActionBuilder When(Func<GameContext, bool> condition)
    {
        _conditions.Add(condition);
        return this;  // ← ALWAYS return this
    }

    public ActionBuilder Do(Action<GameContext> action)
    {
        _actions.Add(action);
        return this;  // ← ALWAYS return this
    }
}
```

**Why**: Enables fluent chaining:
```csharp
action.When(ctx => true).Do(ctx => { }).ThenShow(ctx => []);
// ^^^^^                 ^^^^         ^^^^^^^^^
//   All return 'this' to enable next call
```

### Principle 2: Build() Validates and Constructs

**Rule**: `.Build()` is the ONLY method that creates the final object.

```csharp
public CraftingRecipe Build()
{
    // Validate required fields
    if (string.IsNullOrWhiteSpace(_name))
    {
        throw new InvalidOperationException("Name is required");
    }

    // Construct and return final object
    return new CraftingRecipe(_name, _description)
    {
        RequiredProperties = _requiredProperties,
        ResultType = _resultType,
        // ... set all properties
    };
}
```

**Why**: Prevents partial/invalid objects from existing.

### Principle 3: Clear Method Names

**Rule**: Method names should be verb phrases describing what they configure.

```csharp
// ✅ GOOD: Clear, descriptive
.Named("Stone Axe")
.WithPropertyRequirement(ItemProperty.Stone, 2)
.RequiringSkill("Crafting", 3)
.ResultingInItem(() => ItemFactory.StoneAxe())

// ❌ BAD: Unclear, cryptic
.Name("Stone Axe")
.Props(ItemProperty.Stone, 2)
.Skill("Crafting", 3)
.Item(() => ItemFactory.StoneAxe())
```

**Naming Patterns**:
- `.Named(...)` - Set name
- `.With...` - Add/configure something
- `.Requiring...` - Add requirement
- `.ResultingIn...` - Set result
- `.On...` - Hook/callback methods

### Principle 4: Optional Parameters with Defaults

**Rule**: Use default parameters for common cases.

```csharp
public RecipeBuilder WithPropertyRequirement(
    ItemProperty property,
    double minQuantity = 1,      // Default: 1 unit
    bool isConsumed = true       // Default: consumed
)
{
    _requiredProperties.Add(new CraftingPropertyRequirement(
        property, minQuantity, isConsumed
    ));
    return this;
}

// Usage
.WithPropertyRequirement(ItemProperty.Stone)  // Uses defaults
.WithPropertyRequirement(ItemProperty.Sharp, 1, false)  // Explicit
```

---

## Method Chaining Best Practices

### Pattern 1: Logical Grouping

Group related methods together for readability:

```csharp
// ✅ GOOD: Grouped logically
var recipe = new RecipeBuilder()
    // Identity
    .Named("Stone Knife")
    .WithDescription("A sharp cutting tool")

    // Requirements
    .WithPropertyRequirement(ItemProperty.Stone, 0.5)
    .WithPropertyRequirement(ItemProperty.Binding, 0.1)
    .RequiringSkill("Crafting", 1)
    .RequiringCraftingTime(20)

    // Result
    .ResultingInItem(() => ItemFactory.StoneKnife())

    .Build();

// ❌ BAD: Random order, hard to read
var recipe = new RecipeBuilder()
    .RequiringSkill("Crafting", 1)
    .Named("Stone Knife")
    .ResultingInItem(() => ItemFactory.StoneKnife())
    .WithPropertyRequirement(ItemProperty.Stone, 0.5)
    .WithDescription("A sharp cutting tool")
    .Build();
```

### Pattern 2: One Method Per Line

**Rule**: Each builder method on its own line (except trivial cases).

```csharp
// ✅ GOOD: One per line, easy to read
var action = CreateAction("Hunt")
    .When(ctx => ctx.CurrentLocation.HasWildlife())
    .Do(ctx => HuntingLogic(ctx))
    .ThenShow(ctx => [ContinueHunting(), Return()])
    .Build();

// ❌ BAD: Multiple per line, hard to scan
var action = CreateAction("Hunt").When(ctx => ctx.CurrentLocation.HasWildlife()).Do(ctx => HuntingLogic(ctx)).ThenShow(ctx => [ContinueHunting(), Return()]).Build();
```

### Pattern 3: Indent Continuation

**Rule**: Indent chained methods to show they're part of builder.

```csharp
// ✅ GOOD: Clear indentation
var effect = CreateEffect("Bleeding")
    .From("Wolf Bite")
    .WithSeverity(0.7)
    .AllowMultiple(true)
    .Build();

// ❌ BAD: No indentation, unclear structure
var effect = CreateEffect("Bleeding")
.From("Wolf Bite")
.WithSeverity(0.7)
.AllowMultiple(true)
.Build();
```

---

## Context Passing Patterns

Many builders use **context objects** to pass state to callbacks:

### GameContext (ActionBuilder)

```csharp
public class GameContext
{
    public Player Player { get; set; }
    public Location CurrentLocation => Player.CurrentLocation;
    public Zone CurrentZone => Player.CurrentZone;
    public CraftingSystem CraftingManager { get; set; }
    public World World { get; set; }
}
```

**Usage in Actions**:
```csharp
.When(ctx => ctx.CurrentLocation.HasFeature<ForageFeature>())
//    ^^^
//    GameContext provides access to location

.Do(ctx => {
    ctx.Player.TakeItem(ItemFactory.Berries());
    //  ^^^^^
    //  GameContext provides access to player
})
```

### Actor Context (EffectBuilder)

```csharp
.OnUpdate(actor => {
    actor.Body.Damage(damageInfo);
    //    ^^^^
    //    Actor (Player or NPC) passed as context
})

.OnSeverityChange((actor, oldSeverity, newSeverity) => {
    if (newSeverity < 0.3) {
        Output.WriteLine($"{actor.Name} is improving!");
    }
})
```

### Context Design Principles

**Rule 1**: Context should provide EVERYTHING the callback needs.

```csharp
// ✅ GOOD: Context has all needed info
.Do(ctx => {
    ctx.Player.TakeItem(item);  // Player from context
    ctx.World.Update(30);       // World from context
})

// ❌ BAD: Callback needs external state
.Do(ctx => {
    globalPlayer.TakeItem(item);  // Uses global, brittle
})
```

**Rule 2**: Context should be immutable during callback.

```csharp
// ✅ GOOD: Read from context
.When(ctx => ctx.Player.HasItem("Map"))

// ❌ BAD: Modify context itself
.When(ctx => {
    ctx.Player = new Player();  // DON'T modify context
    return true;
})
```

---

## Builder Consistency Across Systems

All three builders follow consistent patterns:

### Common Structure

| Method Type | ActionBuilder | RecipeBuilder | EffectBuilder |
|-------------|---------------|---------------|---------------|
| **Identity** | `.Named()` | `.Named()` | `.Named()` |
| **Description** | (In factory) | `.WithDescription()` | (Via source) |
| **Requirements** | `.When()` | `.WithPropertyRequirement()`<br/>`.RequiringSkill()` | `.WithSeverity()`<br/>`.RequiresTreatment()` |
| **Behavior** | `.Do()` | (In Build) | `.OnApply()`<br/>`.OnUpdate()`<br/>`.OnRemove()` |
| **Result** | `.ThenShow()`<br/>`.ThenReturn()` | `.ResultingInItem()`<br/>`.ResultingInFeature()`<br/>`.ResultingInStructure()` | (Effect itself) |
| **Build** | `.Build()` | `.Build()` | `.Build()` |

### Consistent Hook Patterns

All builders use similar hook/callback patterns:

**ActionBuilder**:
```csharp
.When(ctx => condition)   // Pre-execution check
.Do(ctx => action)        // Execute logic
.ThenShow(ctx => actions) // Post-execution navigation
```

**EffectBuilder**:
```csharp
.OnApply(actor => { })           // On creation
.OnUpdate(actor => { })          // Every minute
.OnSeverityChange((a, o, n) => { })  // On severity change
.OnRemove(actor => { })          // On removal
```

**RecipeBuilder**:
(Validation happens in Build(), not hooks)

### Consistent Extension Methods

All builders have extension methods following similar patterns:

**ActionBuilder Extensions**:
```csharp
.ShowMessage(message)
.AndGainExperience(skillName)
.ConsumeTime(minutes)
```

**EffectBuilder Extensions**:
```csharp
.Bleeding(damagePerHour)
.Poisoned(damagePerHour)
.Healing(healPerHour)
```

**RecipeBuilder**:
(Fewer extensions, most logic in core builder)

---

## Extension Methods for Builders

Extension methods add common patterns without bloating builder classes.

### Extension Method Pattern

```csharp
public static class ActionBuilderExtensions
{
    public static ActionBuilder ShowMessage(
        this ActionBuilder builder,
        string message
    )
    {
        return builder.Do(ctx => Output.WriteLine(message));
    }
}

// Usage
action.ShowMessage("You gather berries")
//     ^^^^^^^^^^^
//     Extension method feels like builder method
```

### When to Create Extensions

**✅ Create extension when**:
- Pattern used 3+ times
- Combines multiple builder calls
- Represents domain concept
- Simplifies common case

**❌ Don't create extension when**:
- Used only once
- Doesn't combine anything
- Adds no clarity

### Extension Method Examples

**Simple Extension** (single operation):
```csharp
public static ActionBuilder ConsumeTime(
    this ActionBuilder builder,
    int minutes
)
{
    return builder.Do(ctx => ctx.World.Update(minutes));
}

// Usage
.ConsumeTime(30)
// vs
.Do(ctx => ctx.World.Update(30))
```

**Complex Extension** (multiple operations):
```csharp
public static EffectBuilder Bleeding(
    this EffectBuilder builder,
    double damagePerHour
)
{
    return builder
        .WithHourlySeverityChange(-0.05)
        .AllowMultiple(true)
        .ReducesCapacity(CapacityNames.BloodPumping, 0.2)
        .OnUpdate(actor => {
            double damage = (damagePerHour / 60.0) * builder.Build().Severity;
            actor.Body.Damage(new DamageInfo { Amount = damage, Type = DamageType.Bleed });
        });
}

// Usage
.Bleeding(damagePerHour: 5.0)
// vs (without extension)
.WithHourlySeverityChange(-0.05)
.AllowMultiple(true)
.ReducesCapacity(CapacityNames.BloodPumping, 0.2)
.OnUpdate(actor => { /* complex logic */ })
```

---

## When to Use Builder vs Factory

**Decision Guide**:

### Use Builder When:
✅ Object has 5+ optional parameters
✅ Configuration is complex and conditional
✅ Many valid combinations of parameters
✅ Parameters have semantic meaning
✅ Readability matters (Actions, Effects, Recipes)

**Example**:
```csharp
// Builder: Recipe has many optional configurations
var recipe = new RecipeBuilder()
    .Named("Stone Axe")
    .WithPropertyRequirement(ItemProperty.Stone, 2)
    .WithPropertyRequirement(ItemProperty.Wood, 1)
    .RequiringSkill("Crafting", 2)
    .RequiringCraftingTime(30)
    .ResultingInItem(() => ItemFactory.StoneAxe())
    .Build();
```

### Use Factory When:
✅ Object creation is simple (few parameters)
✅ Pre-defined configurations (items, NPCs, spells)
✅ No optional parameters
✅ Fast instantiation needed
✅ Content creation (see [factory-patterns.md](factory-patterns.md))

**Example**:
```csharp
// Factory: Item has fixed properties
public static Item StoneKnife()
{
    var knife = new Item("Stone Knife", 0.3);
    knife.Properties[ItemProperty.Sharp] = 1.0;
    knife.Properties[ItemProperty.Stone] = 0.3;
    return knife;
}
```

### Hybrid Approach

Factories can use builders internally:

```csharp
public static class EffectFactory
{
    public static Effect StandardBleeding(string source)
    {
        return CreateEffect("Bleeding")
            .From(source)
            .Bleeding(damagePerHour: 5.0)
            .Build();
    }
}

// Usage
var effect = EffectFactory.StandardBleeding("Wolf Bite");
// vs
var effect = CreateEffect("Bleeding").From("Wolf Bite").Bleeding(5.0).Build();
```

---

## Comparison Table

| Aspect | ActionBuilder | RecipeBuilder | EffectBuilder |
|--------|---------------|---------------|---------------|
| **Complexity** | High (conditional logic, menu flow) | Medium (requirements, results) | Medium (hooks, modifiers) |
| **Key Feature** | `.ThenShow()` menu chaining | Property-based requirements | Lifecycle hooks |
| **Context** | GameContext | CraftingRecipe (in Build) | Actor |
| **Validation** | Runtime (`.When()`) | Build-time (`.Build()`) | Build-time + Runtime |
| **Extensions** | Many (ShowMessage, ConsumeTime) | Few (mostly core methods) | Many (Bleeding, Poisoned) |
| **Typical Length** | 5-10 lines | 6-12 lines | 4-8 lines |

---

## Anti-Patterns to Avoid

### ❌ Not Returning `this`
```csharp
// WRONG: Breaks chaining
public ActionBuilder Do(Action<GameContext> action)
{
    _actions.Add(action);
    // Missing: return this;
}
```

### ❌ Mutable State After Build
```csharp
// WRONG: Object shouldn't change after Build()
var recipe = builder.Build();
recipe.Name = "Different Name";  // DON'T
```

### ❌ Building Multiple Times
```csharp
// WRONG: Build() should be called ONCE
var builder = new RecipeBuilder().Named("Test");
var recipe1 = builder.Build();
var recipe2 = builder.Build();  // Unexpected behavior
```

### ❌ Side Effects in Builder Methods
```csharp
// WRONG: Builder methods shouldn't execute logic
public ActionBuilder Do(Action<GameContext> action)
{
    action(new GameContext());  // DON'T execute here!
    _actions.Add(action);
    return this;
}
```

---

## Related Files

**Builder Implementations**:
- `Actions/ActionBuilder.cs` - Action builder (ActionBuilder.cs:1-80)
- `Crafting/RecipeBuilder.cs` - Recipe builder (RecipeBuilder.cs:1-124)
- `Effects/EffectBuilder.cs` - Effect builder (EffectBuilder.cs:1-415)

**Extension Methods**:
- `Actions/ActionBuilderExtensions.cs` - Action extensions
- `Effects/EffectBuilderExtensions.cs` - Effect extensions (EffectBuilder.cs:165-415)

**Related Guides**:
- [action-system.md](action-system.md) - ActionBuilder details
- [crafting-system.md](crafting-system.md) - RecipeBuilder details
- [effect-system.md](effect-system.md) - EffectBuilder details
- [factory-patterns.md](factory-patterns.md) - When to use factories instead
- [complete-examples.md](complete-examples.md) - Full builder usage examples

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
