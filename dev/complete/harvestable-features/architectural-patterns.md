# Architectural Patterns - Harvestable Features

**Date**: 2025-11-02
**Context**: Patterns discovered during harvestable feature implementation

---

## Pattern 1: Lazy State Updates

### Problem
How to update respawn timers for potentially hundreds of harvestables without expensive update loops?

### Solution
**Lazy evaluation**: Only calculate respawn when player interacts with the feature.

```csharp
public List<Item> Harvest()
{
    UpdateRespawn();  // ← Calculate respawn ONLY when needed
    var items = new List<Item>();

    foreach (var resource in _resources.Values)
    {
        if (resource.CurrentQuantity > 0)
        {
            items.Add(resource.ItemFactory());
            resource.CurrentQuantity--;
            resource.LastHarvestTime = World.GameTime;
        }
    }

    return items;
}

private void UpdateRespawn()
{
    foreach (var resource in _resources.Values)
    {
        if (resource.CurrentQuantity >= resource.MaxQuantity ||
            resource.LastHarvestTime == DateTime.MinValue)
            continue;

        double hoursSinceHarvest = (World.GameTime - resource.LastHarvestTime).TotalHours;
        int unitsRespawned = (int)(hoursSinceHarvest / resource.RespawnHoursPerUnit);

        if (unitsRespawned > 0)
        {
            resource.CurrentQuantity = Math.Min(
                resource.MaxQuantity,
                resource.CurrentQuantity + unitsRespawned
            );
        }
    }
}
```

### Benefits
- **Performance**: No global update loop needed
- **Simplicity**: State updates only when accessed
- **Accuracy**: Uses real timestamps, not accumulated deltas
- **Scalability**: Supports thousands of harvestables without performance hit

### When to Use
- Infrequently accessed state (player visits location occasionally)
- State depends on elapsed time (respawn, decay, growth)
- Many objects exist but only few are active at once

---

## Pattern 2: Multi-Resource Composition

### Problem
How to represent features that provide multiple items with different respawn rates?

### Solution
**Resource composition**: Store multiple resources in a single feature.

```csharp
public class HarvestableFeature : LocationFeature
{
    private readonly Dictionary<Func<Item>, HarvestableResource> _resources = [];

    public void AddResource(Func<Item> itemFactory, int maxQuantity, double respawnHoursPerUnit)
    {
        _resources[itemFactory] = new HarvestableResource
        {
            ItemFactory = itemFactory,
            MaxQuantity = maxQuantity,
            CurrentQuantity = maxQuantity,
            RespawnHoursPerUnit = respawnHoursPerUnit,
            LastHarvestTime = DateTime.MinValue
        };
    }
}
```

**Usage**:
```csharp
// Berry bush provides food (berries) + crafting (sticks)
var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush", location);
berryBush.AddResource(ItemFactory.MakeBerry, maxQuantity: 5, respawnHoursPerUnit: 168.0);  // 1 week
berryBush.AddResource(ItemFactory.MakeStick, maxQuantity: 2, respawnHoursPerUnit: 72.0);   // 3 days
```

### Benefits
- **Realism**: Single feature can represent complex ecosystems
- **Flexibility**: Easy to add/remove resources without class changes
- **Independence**: Each resource respawns at its own rate
- **Richness**: Encourages thematic depth (berry bush = food + prunings)

### When to Use
- Features represent real-world objects with multiple yields
- Resources have different respawn/consumption rates
- Want to avoid class explosion (BerryBush, WaterSource, etc.)

---

## Pattern 3: Item Factory Functions

### Problem
How to create items on-demand without storing actual Item instances?

### Solution
**Factory pattern**: Store `Func<Item>` instead of `Item`, invoke when harvested.

```csharp
private readonly Dictionary<Func<Item>, HarvestableResource> _resources = [];

public List<Item> Harvest()
{
    var items = new List<Item>();

    foreach (var resource in _resources.Values)
    {
        if (resource.CurrentQuantity > 0)
        {
            var item = resource.ItemFactory();  // ← Create item on harvest
            items.Add(item);
        }
    }

    return items;
}
```

**Usage**:
```csharp
// Store factory function, not item
berryBush.AddResource(ItemFactory.MakeBerry, 5, 168.0);
//                     ^^^^^^^^^^^^^^^^^^^  ← Func<Item>, not Item
```

### Benefits
- **Memory**: Don't store pre-created items (5 berries × 1000 bushes = 5000 objects)
- **Freshness**: Items created with current game state
- **Flexibility**: ItemFactory can be modified without touching harvestables
- **Serialization**: Easier to save/load (store factory name, not object graph)

### When to Use
- Resources are created on-demand (not pre-existing)
- Items may need to reflect current game state
- Memory is a concern (many potential items)

---

## Pattern 4: Status Description Generation

### Problem
How to communicate resource state to player without exposing internal quantities?

### Solution
**Descriptive bands**: Convert quantities to natural language.

```csharp
public string GetStatusDescription()
{
    UpdateRespawn();

    var descriptions = new List<string>();
    foreach (var resource in _resources.Values)
    {
        var sampleItem = resource.ItemFactory();

        string status = resource.CurrentQuantity switch
        {
            0 => "depleted",
            var q when q < resource.MaxQuantity / 3.0 => "sparse",
            var q when q < resource.MaxQuantity * 2.0 / 3.0 => "moderate",
            _ => "abundant"
        };

        descriptions.Add($"{sampleItem.Name}: {status}");
    }

    return $"{DisplayName} ({string.Join(", ", descriptions)})";
}
```

**Output**:
```
Wild Berry Bush (berries: abundant, sticks: moderate)
Forest Puddle (Water: sparse)
Arctic Willow Stand (Plant Fibers: abundant, Bark Strips: moderate, Healing Herbs: depleted)
```

### Benefits
- **Clarity**: Players understand state without seeing numbers
- **Immersion**: Natural language feels realistic
- **Flexibility**: Can adjust bands without changing UI
- **Gameplay**: Encourages checking features before harvesting

### When to Use
- Communicating state to players in natural language
- Want to hide exact numbers (preserve mystery)
- State has meaningful qualitative bands

---

## Pattern 5: Conditional Feature Spawning

### Problem
How to create varied locations without all features present?

### Solution
**Probabilistic spawning**: Use spawn chances for each feature type.

```csharp
// LocationFactory.MakeForest()

// Berry Bush - 30% spawn chance
if (Utils.DetermineSuccess(0.3))
{
    var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush", location);
    berryBush.AddResource(ItemFactory.MakeBerry, 5, 168.0);
    location.Features.Add(berryBush);
}

// Willow Stand - 20% spawn chance
if (Utils.DetermineSuccess(0.2))
{
    var willowStand = new HarvestableFeature("willow_stand", "Arctic Willow Stand", location);
    willowStand.AddResource(ItemFactory.MakePlantFibers, 8, 48.0);
    location.Features.Add(willowStand);
}
```

### Benefits
- **Variety**: Not all forests have berry bushes
- **Replayability**: Different resources each playthrough
- **Scarcity**: Rare resources remain special
- **Exploration**: Rewards scouting multiple locations

### Balancing Spawn Chances
- **Common** (30-50%): Basic resources (puddles, sticks)
- **Moderate** (20-30%): Useful but not essential (berry bushes, willow)
- **Rare** (10-20%): Strategic resources (pine sap, obsidian)
- **Very Rare** (5-10%): Unique features (hot springs, caves)

### When to Use
- Want location variety
- Resources should feel discovered, not guaranteed
- Balancing scarcity vs frustration

---

## Pattern 6: Action Availability via .When()

### Problem
How to show/hide actions based on feature state?

### Solution
**ActionBuilder .When()**: Condition controls action visibility.

```csharp
public static IGameAction HarvestResources()
{
    return CreateAction("Harvest Resources")
        .When(ctx => ctx.currentLocation.Features
            .OfType<HarvestableFeature>()
            .Any(f => f.IsDiscovered))  // ← Only show if harvestables exist
        .ThenShow(ctx =>
        {
            var harvestables = ctx.currentLocation.Features
                .OfType<HarvestableFeature>()
                .Where(f => f.IsDiscovered)
                .Select(f => InspectHarvestable(f))
                .ToList<IGameAction>();

            harvestables.Add(Common.Return("Back to Main Menu"));
            return harvestables;
        })
        .Build();
}
```

```csharp
public static IGameAction HarvestFromFeature(HarvestableFeature feature)
{
    return CreateAction($"Harvest from {feature.DisplayName}")
        .When(_ => feature.IsDiscovered && feature.HasAvailableResources())
        // ↑ Only show if discovered AND has resources
        .Do(ctx => { /* harvest logic */ })
        .Build();
}
```

### Benefits
- **Clean UI**: Players only see available actions
- **Clear feedback**: Disabled actions don't appear (not greyed out)
- **Type safety**: Actions reference specific features
- **Dynamic**: Menu updates as state changes

### When to Use
- Action availability depends on game state
- Want clean, context-sensitive menus
- Using ActionBuilder pattern

---

## Pattern 7: Feature Type Filtering in LookAround

### Problem
How to display different feature types with appropriate formatting?

### Solution
**Type checking with pattern matching**: Handle each LocationFeature subclass.

```csharp
// ActionFactory.cs - LookAround action
foreach (var feature in location.Features)
{
    if (feature is HeatSourceFeature heat)
    {
        // Show fires with status
        if (heat.IsActive)
            Output.WriteLine($"\t{heat.Name} (burning, {heat.FuelRemaining:F0} min)");
        else if (heat.HasEmbers)
            Output.WriteLine($"\t{heat.Name} (glowing embers)");
    }
    else if (feature is ShelterFeature shelter)
    {
        // Always show shelters
        Output.WriteLine($"\t{shelter.Name} [shelter]");
    }
    else if (feature is HarvestableFeature harvestable && harvestable.IsDiscovered)
    {
        // Show harvestables with status
        string status = harvestable.GetStatusDescription();
        Output.WriteLine($"\t{status}");
        hasItems = true;
    }
    // Don't display ForageFeature or EnvironmentFeature
}
```

### Benefits
- **Extensibility**: Easy to add new feature types
- **Clarity**: Each type displays appropriately
- **Encapsulation**: Status logic stays in feature class
- **Filtering**: Can hide abstract/internal features

### When to Use
- Displaying collections of mixed types
- Each type needs custom display logic
- Want to filter by type (show some, hide others)

---

## Anti-Patterns Avoided

### ❌ Pre-Creating Items
**Bad**:
```csharp
// DON'T store actual items
private readonly List<Item> _items = [
    ItemFactory.MakeBerry(),
    ItemFactory.MakeBerry(),
    /* ... */
];
```

**Why Bad**: Memory waste, serialization complexity, stale state

**Good**:
```csharp
// DO store factory + quantity
private readonly Dictionary<Func<Item>, HarvestableResource> _resources = [];
berryBush.AddResource(ItemFactory.MakeBerry, 5, 168.0);
```

---

### ❌ Update Loop for Respawn
**Bad**:
```csharp
// DON'T update every feature every tick
public void Update(double minutesElapsed)
{
    foreach (var feature in allHarvestables)
        feature.UpdateRespawn(minutesElapsed);  // Expensive!
}
```

**Why Bad**: Performance (O(n) every tick), accumulated rounding errors

**Good**:
```csharp
// DO calculate on-demand using timestamps
private void UpdateRespawn()
{
    double hoursSinceHarvest = (World.GameTime - LastHarvestTime).TotalHours;
    int unitsRespawned = (int)(hoursSinceHarvest / RespawnHoursPerUnit);
}
```

---

### ❌ Class Explosion
**Bad**:
```csharp
// DON'T create class per feature type
public class BerryBush : LocationFeature { }
public class WillowStand : LocationFeature { }
public class PineSapSeep : LocationFeature { }
public class Puddle : LocationFeature { }
/* ... 50+ classes ... */
```

**Why Bad**: Maintenance nightmare, code duplication, inflexibility

**Good**:
```csharp
// DO use composition with multi-resource support
var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush", location);
berryBush.AddResource(ItemFactory.MakeBerry, 5, 168.0);
berryBush.AddResource(ItemFactory.MakeStick, 2, 72.0);
```

---

### ❌ Exposing Internal State
**Bad**:
```csharp
// DON'T expose quantities directly
public int BerryQuantity { get; set; }
public int StickQuantity { get; set; }

// Player sees: "Berry Bush (3 berries, 1 stick)"
```

**Why Bad**: Breaks immersion, rigid coupling, players optimize instead of roleplay

**Good**:
```csharp
// DO use descriptive bands
public string GetStatusDescription()
{
    string status = quantity switch
    {
        0 => "depleted",
        < max/3 => "sparse",
        < max*2/3 => "moderate",
        _ => "abundant"
    };
    return $"{DisplayName} ({itemName}: {status})";
}

// Player sees: "Berry Bush (berries: abundant, sticks: moderate)"
```

---

## Summary

### Key Takeaways

1. **Lazy evaluation** saves performance for infrequently accessed state
2. **Multi-resource composition** beats class explosion
3. **Item factories** are more flexible than pre-created instances
4. **Natural language descriptions** improve immersion
5. **Probabilistic spawning** creates variety and replayability
6. **ActionBuilder .When()** keeps menus clean and dynamic
7. **Type filtering** enables mixed-type collections with custom display

### Design Principles

- **Composition over inheritance** (HarvestableFeature vs BerryBush/Puddle/etc.)
- **Lazy over eager** (UpdateRespawn on-demand vs update loop)
- **Functions over objects** (Func<Item> vs pre-created items)
- **Description over numbers** ("abundant" vs "4/5 remaining")
- **Probability over determinism** (30% spawn vs always present)

### When in Doubt

Ask:
1. Can this be lazy? (Don't update if not accessed)
2. Can this be composed? (Multi-resource vs single-purpose class)
3. Can this be generated? (Factory vs stored instance)
4. Can this be described? (Natural language vs raw numbers)
5. Can this vary? (Probabilistic vs guaranteed)

These patterns emerge from composition-based architecture and favor simplicity, performance, and player experience over perfect realism or comprehensive modeling.
