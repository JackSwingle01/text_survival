# Crafting System - Property-Based Recipes

Property-based crafting with RecipeBuilder pattern, ItemProperty enum, recipe requirements, and result types.

## Table of Contents

- [Property-Based Philosophy](#property-based-philosophy)
- [ItemProperty Enum](#itemproperty-enum)
- [RecipeBuilder Pattern](#recipebuilder-pattern)
- [Property Requirements](#property-requirements)
- [Skill Requirements](#skill-requirements)
- [Result Types](#result-types)
- [Complete Recipe Examples](#complete-recipe-examples)

---

## Property-Based Philosophy

**Critical Principle**: Recipes use PROPERTIES, not specific items. This makes the system flexible and extensible.

```csharp
// ✅ CORRECT: Property-based
recipe.WithPropertyRequirement(ItemProperty.Stone, 2);
// Accepts ANY item with Stone property (rock, boulder, flint, etc.)

// ❌ WRONG: Item-specific check
if (player.HasItem("Rock")) { ... }
// Rigid, doesn't scale, violates the pattern
```

**Why Properties?**
- **Flexibility**: New items with same properties work automatically
- **Realistic**: "Need something sharp" vs "Need Flint Knife #47"
- **Extensibility**: Add new items without modifying recipes
- **Emergence**: Players discover creative solutions

**Property Definition in Items**:
```csharp
public class Item
{
    public Dictionary<ItemProperty, double> Properties { get; set; } = new();

    public bool HasProperty(ItemProperty property, double minQuantity = 0)
    {
        return Properties.TryGetValue(property, out double value)
            && value >= minQuantity;
    }
}
```

---

## ItemProperty Enum

**Location**: `Crafting/ItemProperty.cs`

```csharp
public enum ItemProperty : byte
{
    Stone,        // Rock, flint, boulder
    Wood,         // Branches, logs, sticks
    Binding,      // Sinew, plant fiber, rawhide strips
    Flammable,    // Dry wood, bark, fat
    Firestarter,  // Bow drill, flint & steel
    Insulation,   // Hide, fur, feathers
    RawMeat,      // Uncooked flesh
    CookedMeat,   // Cooked, safe to eat
    Bone,         // Structural, tool-making
    Hide,         // Leather, pelts
    Poison,       // Toxic substances
    Fat,          // Rendered fat, tallow
    Sharp,        // Cutting implements
    Clay          // Moldable earth
}
```

**Property Amounts**:
Properties use quantities (typically based on item weight):
```csharp
// Heavy rock has more Stone property
var boulder = new Item("Boulder", 5.0);
boulder.Properties[ItemProperty.Stone] = 5.0;

// Light rock has less Stone property
var pebble = new Item("Pebble", 0.5);
pebble.Properties[ItemProperty.Stone] = 0.5;
```

**Multiple Properties**:
Items can have multiple properties:
```csharp
var fattyMeat = new Item("Bear Fat", 1.0);
fattyMeat.Properties[ItemProperty.RawMeat] = 0.7;
fattyMeat.Properties[ItemProperty.Fat] = 0.3;
fattyMeat.Properties[ItemProperty.Flammable] = 0.2;  // Fat burns!
```

---

## RecipeBuilder Pattern

**Location**: `Crafting/RecipeBuilder.cs`

Recipes are built using a fluent builder pattern:

```csharp
var recipe = new RecipeBuilder()
    .Named("Stone Axe")
    .WithDescription("A crude but effective cutting tool")
    .WithPropertyRequirement(ItemProperty.Stone, 2)
    .WithPropertyRequirement(ItemProperty.Wood, 1)
    .WithPropertyRequirement(ItemProperty.Binding, 0.2)
    .ResultingInItem(() => ItemFactory.StoneAxe())
    .RequiringSkill("Crafting", 2)
    .RequiringCraftingTime(30)
    .Build();
```

**Builder Methods**:

### .Named(string name)
Sets the recipe name (REQUIRED).

### .WithDescription(string description)
Provides recipe description shown to player.

### .WithPropertyRequirement()
Adds a property requirement (see next section).

### .ResultingInItem()
Recipe produces an item (see Result Types section).

### .ResultingInLocationFeature()
Recipe produces a location feature (campfire, shelter, etc.).

### .ResultingInStructure()
Recipe produces a new location (shelter building).

### .RequiringSkill(string skillName, int level)
Sets skill name and minimum level required.
```csharp
.RequiringSkill("Crafting", 3)  // Need Crafting level 3
.RequiringSkill("Leatherworking", 5)  // Or specialized skill
```

### .RequiringCraftingTime(int minutes)
Time spent crafting (calls `World.Update(minutes)`).
```csharp
.RequiringCraftingTime(30)  // 30 minutes
.RequiringCraftingTime(120) // 2 hours for complex items
```

### .RequiringFire(bool requiresFire)
Whether recipe needs active fire source.
```csharp
.RequiringFire(true)  // Cooking, metalworking, etc.
```

### .Build()
Creates the final `CraftingRecipe` object.

---

## Property Requirements

**Class**: `CraftingPropertyRequirement`

```csharp
public class CraftingPropertyRequirement
{
    public ItemProperty Property { get; set; }
    public double MinQuantity { get; set; }
    public bool IsConsumed { get; set; }  // Consumed or just required?
}
```

**Creating Requirements**:

```csharp
// Simple form (consumed by default)
.WithPropertyRequirement(ItemProperty.Stone, 2)

// Explicit form
.WithPropertyRequirement(ItemProperty.Wood, 1, isConsumed: true)

// Tool requirement (not consumed)
.WithPropertyRequirement(ItemProperty.Sharp, 1, isConsumed: false)
```

**Consumption Behavior**:
- `isConsumed: true` - Materials are removed from inventory
- `isConsumed: false` - Items must be present but aren't consumed (tools)

**Example - Tool vs Material**:
```csharp
var recipe = new RecipeBuilder()
    .Named("Hide Strips")
    .WithPropertyRequirement(ItemProperty.Hide, 1, isConsumed: true)  // Material
    .WithPropertyRequirement(ItemProperty.Sharp, 1, isConsumed: false)  // Tool
    .ResultingInItem(() => ItemFactory.HideStrip())
    .Build();
// Player needs sharp tool + hide. Hide consumed, tool kept.
```

**Quantity Checking**:
The system aggregates property quantities across all inventory items:
```csharp
// Player has: 3 small rocks (0.5 Stone each), 1 large rock (2 Stone)
// Total Stone property: 3×0.5 + 2 = 3.5
// Can craft recipes requiring up to 3.5 Stone
```

---

## Skill Requirements

**Skills are PLAYER-ONLY** (never NPCs).

```csharp
.RequiringSkill("Crafting", 3)
// Player must have Crafting skill at level 3+
// Recipe still grants XP when crafted
```

**Common Skills**:
- `"Crafting"` - General crafting (default)
- `"Cooking"` - Food preparation
- `"Leatherworking"` - Hide processing
- `"Woodworking"` - Advanced wood items
- `"Foraging"` - Plant knowledge for recipes

**Skill Level Design**:
- Level 0-1: Basic items (torch, simple tools)
- Level 2-4: Intermediate items (weapons, clothing)
- Level 5-7: Advanced items (complex tools, structures)
- Level 8+: Expert items (specialized equipment)

**Experience Gain**:
```csharp
// From CraftingSystem.cs:77
int xpGain = recipe.RequiredSkillLevel + 2;
_player.Skills.GetSkill(recipe.RequiredSkill).GainExperience(xpGain);
```
Higher level recipes give more XP.

---

## Result Types

Recipes can produce three result types:

### 1. Item Result

Most common - produces item(s) added to inventory.

```csharp
.ResultingInItem(() => ItemFactory.StoneKnife())
```

**Multiple Items**:
```csharp
.ResultingInItem(() => ItemFactory.Arrow())  // Creates 5 arrows
.ResultingInItem(() => ItemFactory.Arrow())  // (Called multiple times)
```

**From CraftingSystem.cs**:
```csharp
case CraftingResultType.Item:
    var items = recipe.GenerateItemResults(_player);
    foreach (var item in items)
    {
        _player.TakeItem(item);
        Output.WriteSuccess($"You successfully crafted: {item.Name}");
    }
    break;
```

### 2. LocationFeature Result

Produces a feature added to current location (campfire, drying rack, etc.).

```csharp
.ResultingInLocationFeature(new LocationFeatureResult(
    "Campfire",
    loc => new HeatSourceFeature(loc, 40, 180)  // 40 temp, 180 min duration
))
```

**When to Use**:
- Temporary structures (campfire, lean-to)
- Location improvements (drying rack, storage)
- Heat sources, work stations

**From CraftingSystem.cs**:
```csharp
case CraftingResultType.LocationFeature:
    var feature = recipe.LocationFeatureResult.FeatureFactory(_player.CurrentLocation);
    _player.CurrentLocation.Features.Add(feature);
    Output.WriteSuccess($"You successfully built: {recipe.LocationFeatureResult.FeatureName}");
    break;
```

### 3. Structure Result (New Location)

Produces entirely new location in current zone (permanent shelter).

```csharp
.ResultingInStructure(
    "Small Shelter",
    zone => LocationFactory.CreateSmallShelter(zone)
)
```

**When to Use**:
- Permanent shelters
- Large structures
- Player-built locations

**From CraftingSystem.cs**:
```csharp
case CraftingResultType.Shelter:
    var newLocation = recipe.NewLocationResult.LocationFactory(_player.CurrentZone);
    _player.CurrentZone.Locations.Add(newLocation);
    newLocation.IsFound = true;
    Output.WriteSuccess($"You successfully built: {recipe.NewLocationResult.LocationName}");
    break;
```

---

## Complete Recipe Examples

### Example 1: Simple Tool (Stone Knife)

```csharp
var stoneKnifeRecipe = new RecipeBuilder()
    .Named("Stone Knife")
    .WithDescription("A sharp cutting tool made from flaked stone")
    .WithPropertyRequirement(ItemProperty.Stone, 0.5)
    .WithPropertyRequirement(ItemProperty.Binding, 0.1, isConsumed: true)
    .ResultingInItem(() => ItemFactory.StoneKnife())
    .RequiringSkill("Crafting", 1)
    .RequiringCraftingTime(20)
    .Build();
```

**Property Logic**:
- Small amount of stone (0.5 kg) - flint, chert, obsidian
- Minimal binding (0.1 kg) - plant fiber or sinew to wrap handle
- Both consumed in crafting
- Low skill requirement (level 1)
- 20 minutes to craft

### Example 2: Cooking Recipe (Cooked Meat)

```csharp
var cookedMeatRecipe = new RecipeBuilder()
    .Named("Cook Meat")
    .WithDescription("Cook raw meat over fire to make it safe to eat")
    .WithPropertyRequirement(ItemProperty.RawMeat, 0.5)
    .ResultingInItem(() => ItemFactory.CookedMeat())
    .RequiringSkill("Cooking", 0)
    .RequiringCraftingTime(15)
    .RequiringFire(true)
    .Build();
```

**Property Logic**:
- Requires raw meat (any source: rabbit, deer, bear)
- Must have active fire
- No skill requirement (basic survival)
- Transforms RawMeat property → CookedMeat property

### Example 3: Tool Using Tool (Hide Strips)

```csharp
var hideStripRecipe = new RecipeBuilder()
    .Named("Cut Hide Strips")
    .WithDescription("Cut raw hide into binding strips")
    .WithPropertyRequirement(ItemProperty.Hide, 1, isConsumed: true)
    .WithPropertyRequirement(ItemProperty.Sharp, 1, isConsumed: false)  // TOOL
    .ResultingInItem(() => ItemFactory.HideStrip())
    .RequiringSkill("Leatherworking", 1)
    .RequiringCraftingTime(25)
    .Build();
```

**Property Logic**:
- Hide is consumed (transformed into strips)
- Sharp tool REQUIRED but NOT consumed (knife, axe, sharp stone)
- Specialized skill (Leatherworking)
- Produces binding material for other recipes

### Example 4: Location Feature (Campfire)

```csharp
var campfireRecipe = new RecipeBuilder()
    .Named("Build Campfire")
    .WithDescription("Create a fire for warmth, light, and cooking")
    .WithPropertyRequirement(ItemProperty.Wood, 2)
    .WithPropertyRequirement(ItemProperty.Flammable, 0.5)  // Tinder
    .WithPropertyRequirement(ItemProperty.Firestarter, 1, isConsumed: false)
    .ResultingInLocationFeature(new LocationFeatureResult(
        "Campfire",
        loc => new HeatSourceFeature(loc, temperatureBonus: 40, durationMinutes: 180)
    ))
    .RequiringSkill("Survival", 1)
    .RequiringCraftingTime(30)
    .Build();
```

**Property Logic**:
- Wood (fuel) - consumed
- Flammable material (tinder) - consumed
- Firestarter (bow drill, flint) - NOT consumed (tool)
- Creates HeatSourceFeature at current location
- Feature lasts 180 minutes (3 hours)

### Example 5: Structure (Small Shelter)

```csharp
var shelterRecipe = new RecipeBuilder()
    .Named("Build Small Shelter")
    .WithDescription("Construct a basic shelter from branches and hides")
    .WithPropertyRequirement(ItemProperty.Wood, 10)  // Lots of branches
    .WithPropertyRequirement(ItemProperty.Binding, 2)
    .WithPropertyRequirement(ItemProperty.Hide, 3)  // Covering
    .ResultingInStructure(
        "Small Shelter",
        zone => LocationFactory.CreateSmallShelter(zone)
    )
    .RequiringSkill("Crafting", 4)
    .RequiringCraftingTime(240)  // 4 hours
    .Build();
```

**Property Logic**:
- Large material requirements (major construction)
- Creates entirely new Location in current Zone
- High skill requirement (level 4)
- Long crafting time (240 minutes = 4 hours)
- Permanent structure (not temporary like campfire)

### Example 6: Complex Multi-Output (Arrow Batch)

```csharp
var arrowRecipe = new RecipeBuilder()
    .Named("Craft Arrows")
    .WithDescription("Create a bundle of arrows for hunting")
    .WithPropertyRequirement(ItemProperty.Wood, 0.5)  // Shafts
    .WithPropertyRequirement(ItemProperty.Stone, 0.2) // Arrowheads
    .WithPropertyRequirement(ItemProperty.Binding, 0.1)
    .ResultingInItem(() => ItemFactory.Arrow())
    .ResultingInItem(() => ItemFactory.Arrow())
    .ResultingInItem(() => ItemFactory.Arrow())
    .ResultingInItem(() => ItemFactory.Arrow())
    .ResultingInItem(() => ItemFactory.Arrow())  // 5 arrows
    .RequiringSkill("Crafting", 3)
    .RequiringCraftingTime(60)
    .Build();
```

**Property Logic**:
- Batch crafting - multiple outputs from single recipe
- Each `.ResultingInItem()` call creates one arrow
- Efficient: 5 arrows for small material cost
- Skill requirement reflects precision work

### Example 7: Advanced Item with Tool Requirement

```csharp
var furCloakRecipe = new RecipeBuilder()
    .Named("Fur Cloak")
    .WithDescription("A warm cloak made from tanned hide and fur")
    .WithPropertyRequirement(ItemProperty.Hide, 3, isConsumed: true)
    .WithPropertyRequirement(ItemProperty.Insulation, 1, isConsumed: true)  // Fur
    .WithPropertyRequirement(ItemProperty.Sharp, 1, isConsumed: false)  // Cutting tool
    .WithPropertyRequirement(ItemProperty.Binding, 0.5, isConsumed: true)
    .ResultingInItem(() => ItemFactory.FurCloak())
    .RequiringSkill("Leatherworking", 5)
    .RequiringCraftingTime(180)  // 3 hours
    .Build();
```

**Property Logic**:
- Multiple material types (hide, fur, binding)
- Tool requirement (sharp knife for precise cutting)
- High skill level (advanced leatherworking)
- Long crafting time (complex construction)
- Result: High-value insulated clothing

---

## Anti-Patterns to Avoid

### ❌ Item-Specific Checks
```csharp
// WRONG: Checking for specific items
if (player.HasItem("Flint Rock") && player.HasItem("Oak Branch"))
{
    // Rigid, doesn't scale
}
```

### ✅ Property-Based Checks
```csharp
// CORRECT: Check properties
.WithPropertyRequirement(ItemProperty.Stone, 1)
.WithPropertyRequirement(ItemProperty.Wood, 1)
// Flexible, any stone + any wood works
```

### ❌ Manual Inventory Manipulation
```csharp
// WRONG: Direct inventory changes
player.Inventory.Remove(rock);
player.Inventory.Add(knife);
```

### ✅ Use RecipeBuilder
```csharp
// CORRECT: Let CraftingSystem handle it
recipe.ConsumeIngredients(player);
recipe.GenerateItemResults(player);
```

### ❌ Forgetting Time Passage
```csharp
// WRONG: No time consumption
recipe.Execute(player);  // Instant crafting?
```

### ✅ Require Crafting Time
```csharp
// CORRECT: Time passes
.RequiringCraftingTime(30)
// CraftingSystem calls World.Update(30)
```

---

## Related Files

**Core System Files**:
- `Crafting/RecipeBuilder.cs` - Builder implementation (RecipeBuilder.cs:1-124)
- `Crafting/CraftingSystem.cs` - Recipe execution and management (CraftingSystem.cs:1-150)
- `Crafting/ItemProperty.cs` - Property enum definition (ItemProperty.cs:1-26)
- `Crafting/CraftingRecipe.cs` - Recipe data structure

**Integration Points**:
- `Items/Item.cs` - Item property system
- `Items/ItemFactory.cs` - Item creation for results
- `Actions/ActionFactory.cs` - Crafting menu actions
- `Level/SkillRegistry.cs` - Skill requirement checking

**Related Guides**:
- [action-system.md](action-system.md) - Action integration for crafting menus
- [builder-patterns.md](builder-patterns.md) - Builder pattern philosophy
- [factory-patterns.md](factory-patterns.md) - ItemFactory for recipe results
- [complete-examples.md](complete-examples.md) - Full crafting feature examples

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
