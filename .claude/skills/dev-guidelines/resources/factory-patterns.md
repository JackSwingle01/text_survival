# Factory Patterns - Content Creation

Factory pattern for creating items, NPCs, spells, and locations with static methods and future JSON migration.

## Table of Contents

- [Factory Pattern Overview](#factory-pattern-overview)
- [ItemFactory](#itemfactory)
- [NPCFactory](#npcfactory)
- [BodyPartFactory](#bodypartfactory)
- [SpellFactory](#spellfactory)
- [LocationFactory and ZoneFactory](#locationfactory-and-zonefactory)
- [When to Use Factory vs Builder](#when-to-use-factory-vs-builder)
- [Future: JSON Data-Driven Content](#future-json-data-driven-content)

---

## Factory Pattern Overview

**Purpose**: Create pre-configured game objects (items, NPCs) with fixed properties.

**Characteristics**:
- Static methods (no builder instance needed)
- Pre-defined configurations
- Simple, fast creation
- Content-focused (not behavior-focused)

**Common Pattern**:
```csharp
public static class SomeFactory
{
    public static SomeType CreateThing()
    {
        var thing = new SomeType("Name");
        thing.Property1 = value1;
        thing.Property2 = value2;
        return thing;
    }
}

// Usage
var thing = SomeFactory.CreateThing();
```

---

## ItemFactory

**Location**: `Items/ItemFactory.cs`

Creates items with fixed properties (weapons, food, materials, equipment).

### Simple Item Example

```csharp
public static Item MakeStick()
{
    Item stick = new Item("Large Stick")
    {
        Description = "A sturdy branch, useful as fuel or crafting material",
        Weight = 0.5
    };
    stick.Properties[ItemProperty.Wood] = 0.5;
    stick.Properties[ItemProperty.Flammable] = 0.3;
    return stick;
}

// Usage
var stick = ItemFactory.MakeStick();
player.TakeItem(stick);
```

### Food Item Example

```csharp
public static FoodItem MakeBerry()
{
    var item = new FoodItem("Wild Berries", calories: 120, hydration: 100);
    string color = Utils.GetRandomFromList(["red", "blue", "black"]);
    string season = Utils.GetRandomFromList(["autumn", "summer"]);
    item.Description = $"A handful of {color} {season} berries.";
    item.Weight = 0.1F;
    return item;
}

// Usage
var berries = ItemFactory.MakeBerry();
player.TakeItem(berries);
player.Consume(berries);  // Restores calories + hydration
```

### Weapon Example

```csharp
public static Weapon MakeFists()
{
    return new Weapon(WeaponType.Unarmed, WeaponMaterial.Organic, "Bare Hands");
}

public static Weapon MakeStoneKnife()
{
    var knife = new Weapon(WeaponType.Blade, WeaponMaterial.Stone, "Stone Knife")
    {
        Damage = 5,
        Accuracy = 0.9
    };
    return knife;
}
```

### Random Item Example (Mushrooms)

```csharp
public static FoodItem MakeMushroom()
{
    var mushroom = new FoodItem("Wild Mushroom", 25, 5)
    {
        Description = "A forest mushroom. Some varieties are nutritious, others deadly.",
        Weight = 0.1F
    };

    double strength = Utils.RandDouble(1, 15);
    string targetOrgan = Utils.GetRandomFromList(["Stomach", "Liver", "Kidney"]);

    // 50% chance: healing or poison
    if (Utils.FlipCoin())
    {
        mushroom.HealthEffect = new()
        {
            Amount = strength,
            Type = "herbal",
            TargetPart = targetOrgan,
            Quality = Utils.RandDouble(0, 1.5)
        };
    }
    else
    {
        mushroom.DamageEffect = new()
        {
            Amount = strength * 0.66,
            Type = DamageType.Poison,
            TargetPartName = targetOrgan
        };
    }

    return mushroom;
}
```

---

## NPCFactory

**Location**: `Actors/NPCFactory.cs`

Creates NPCs (animals, enemies) with body stats, weapons, and loot.

### Simple NPC Example (Rat)

```csharp
public static Animal MakeRat()
{
    var bodyStats = new BodyCreationInfo
    {
        type = BodyTypes.Quadruped,
        overallWeight = 2,      // Small animal
        fatPercent = 0.15,      // 15% fat
        musclePercent = 0.40    // 40% muscle
    };

    var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "teeth", 100)
    {
        Damage = 2,
        Accuracy = 1.2
    };

    Animal rat = new("Rat", weapon, bodyStats)
    {
        Description = "A rat with fleas."
    };
    rat.AddLoot(ItemFactory.MakeSmallMeat());
    return rat;
}

// Usage
var rat = NpcFactory.MakeRat();
location.NPCs.Add(rat);
```

### Strong NPC Example (Wolf)

```csharp
public static Animal MakeWolf()
{
    var bodyStats = new BodyCreationInfo
    {
        type = BodyTypes.Quadruped,
        overallWeight = 40,      // 40 kg
        fatPercent = 0.20,       // 20% fat
        musclePercent = 0.60     // 60% muscle - STRONG
    };

    var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "fangs", 100)
    {
        Damage = 10,    // High damage
        Accuracy = 1.1
    };

    Animal wolf = new("Wolf", weapon, bodyStats)
    {
        Description = "A wolf."
    };
    wolf.AddLoot(ItemFactory.MakeLargeMeat());
    return wolf;
}
```

**Key NPC Stat**: `musclePercent` determines combat strength:
- Low muscle (0.3-0.4): Weak (rats, rabbits)
- Medium muscle (0.5-0.6): Average (wolves, deer)
- High muscle (0.7-0.8): Strong (bears, mammoths)

---

## BodyPartFactory

**Location**: `Bodies/BodyPartFactory.cs`

Creates body hierarchies for different creature types.

**Pattern**:
```csharp
public static Body CreateBody(BodyTypes bodyType, BodyCreationInfo stats)
{
    return bodyType switch
    {
        BodyTypes.Humanoid => CreateHumanoidBody(stats),
        BodyTypes.Quadruped => CreateQuadrupedBody(stats),
        BodyTypes.Avian => CreateAvianBody(stats),
        _ => throw new ArgumentException($"Unknown body type: {bodyType}")
    };
}
```

**Humanoid Example**:
```csharp
private static Body CreateHumanoidBody(BodyCreationInfo stats)
{
    var torso = new BodyRegion("Torso");
    torso.AddTissue(new Skin("Torso Skin", 5));
    torso.AddTissue(new Muscle("Torso Muscle", 15));
    torso.AddOrgan(new Heart("Heart", 5));
    torso.AddOrgan(new Lungs("Lungs", 10));

    var leftArm = new BodyRegion("Left Arm");
    leftArm.AddTissue(new Skin("Left Arm Skin", 2));
    leftArm.AddTissue(new Muscle("Left Arm Muscle", 3));

    // ... more regions ...

    var body = new Body(stats.Name, stats);
    body.AddRegion(torso);
    body.AddRegion(leftArm);
    // ... add all regions ...

    return body;
}
```

**When to Use**: Rarely modified directly. Used internally by Actor constructors.

---

## SpellFactory

**Location**: `Magic/SpellFactory.cs`

Creates spells with effects and casting requirements.

**Pattern**:
```csharp
public static Spell Bleeding => new Spell("Bleeding", 10, 5)
{
    Description = "Causes target to bleed",
    Effect = EffectBuilderExtensions.CreateEffect("Bleeding")
        .Bleeding(damagePerHour: 10)
        .Build()
};

public static Spell MinorHeal => new Spell("Minor Heal", 15, 3)
{
    Description = "Heals minor wounds",
    Effect = EffectBuilderExtensions.CreateEffect("Healing")
        .Healing(healPerHour: 20)
        .Build()
};
```

**Spell Properties**:
- **Name**: Display name
- **ManaCost**: Mana consumed on cast
- **CooldownMinutes**: Time before re-castable
- **Effect**: Effect applied to target (see [effect-system.md](effect-system.md))

---

## LocationFactory and ZoneFactory

**Location**: `Environments/LocationFactory.cs`, `Environments/ZoneFactory.cs`

Creates locations and zones with features, NPCs, and descriptions.

### Location Example

```csharp
public static Location CreateCave(Zone parentZone)
{
    var cave = new Location("Dark Cave", parentZone)
    {
        Description = "A dark, damp cave. Water drips from stalactites.",
        Temperature = 8,  // Cold
        IsFound = false   // Must be discovered
    };

    // Add features
    cave.Features.Add(new ForageFeature(cave, ["Mushrooms"], 60));

    // Add NPCs
    cave.NPCs.Add(NpcFactory.MakeBear());

    return cave;
}
```

### Zone Example

```csharp
public static Zone CreateForestZone()
{
    var zone = new Zone("Ancient Forest")
    {
        Description = "A dense forest of towering pines and ancient oaks."
    };

    // Add locations
    zone.Locations.Add(LocationFactory.CreateForestClearing(zone));
    zone.Locations.Add(LocationFactory.CreateCave(zone));
    zone.Locations.Add(LocationFactory.CreateRiverbank(zone));

    return zone;
}
```

---

## When to Use Factory vs Builder

### Use Factory When:
✅ Creating pre-defined content (items, NPCs)
✅ Fixed configurations (no customization needed)
✅ Simple creation (few parameters)
✅ Content creation focus

**Example**:
```csharp
// Factory: Pre-defined item with fixed stats
var knife = ItemFactory.MakeStoneKnife();
```

### Use Builder When:
✅ Complex configuration (many optional parameters)
✅ Dynamic behavior (actions, effects, recipes)
✅ Conditional logic (`.When()`, `.OnUpdate()`)
✅ Behavior creation focus

**Example**:
```csharp
// Builder: Custom recipe with requirements
var recipe = new RecipeBuilder()
    .Named("Custom Tool")
    .WithPropertyRequirement(ItemProperty.Stone, 2)
    .RequiringSkill("Crafting", 3)
    .Build();
```

### Hybrid Approach

Factories can use builders internally:

```csharp
public static Effect StandardBleeding(string source)
{
    // Factory wraps builder for common case
    return EffectBuilderExtensions.CreateEffect("Bleeding")
        .From(source)
        .Bleeding(damagePerHour: 5.0)
        .Build();
}
```

---

## Future: JSON Data-Driven Content

**Current State**: Content is code-based (factories).

**Future Direction** (from README.md): Migrate to JSON-driven content.

**Vision**:
```json
// items.json (future)
{
  "StoneKnife": {
    "name": "Stone Knife",
    "weight": 0.3,
    "description": "A sharp cutting tool",
    "properties": {
      "Sharp": 1.0,
      "Stone": 0.3
    }
  }
}
```

**Benefits of JSON Migration**:
- **Mod-friendly**: Players can create content without code
- **Balance tweaks**: Adjust stats without recompiling
- **Localization**: Translate names/descriptions easily
- **Content volume**: Create many items quickly

**Current Factories Become Loaders**:
```csharp
// Future: ItemFactory loads from JSON
public static Item MakeStoneKnife()
{
    return JsonLoader.LoadItem("StoneKnife");
}
```

**When Adding Content Now**:
Follow existing factory patterns, but keep in mind future JSON migration:
- Use clear, consistent naming
- Separate data (stats, descriptions) from logic
- Document all properties and their meanings

---

## Factory Organization Best Practices

### Static Methods

All factory methods are static:
```csharp
public static class ItemFactory
{
    public static Item MakeStick() { ... }
    public static Item MakeRock() { ... }
}

// Usage (no instantiation needed)
var stick = ItemFactory.MakeStick();
```

### Naming Conventions

**Pattern**: `Make<ThingName>()`
- `MakeStick()` - Creates a stick
- `MakeWolf()` - Creates a wolf
- `MakeBerry()` - Creates berries

### Grouping by Type

Organize methods by category:
```csharp
public static class ItemFactory
{
    // Food
    public static FoodItem MakeBerry() { ... }
    public static FoodItem MakeMushroom() { ... }

    // Materials
    public static Item MakeStick() { ... }
    public static Item MakeRock() { ... }

    // Weapons
    public static Weapon MakeStoneKnife() { ... }
    public static Weapon MakeFists() { ... }
}
```

---

## Anti-Patterns to Avoid

### ❌ Complex Logic in Factories

```csharp
// WRONG: Complex business logic in factory
public static Item MakeStick()
{
    var stick = new Item("Stick");
    if (player.HasSkill("Woodworking", 5)) {
        stick.Quality = "High";  // Don't check player state here
    }
    return stick;
}
```

### ✅ Keep Factories Simple

```csharp
// CORRECT: Just create the item
public static Item MakeStick()
{
    var stick = new Item("Stick");
    stick.Properties[ItemProperty.Wood] = 0.5;
    return stick;
}
```

### ❌ Mutable Static State

```csharp
// WRONG: Static mutable state
public static class ItemFactory
{
    private static int itemCount = 0;  // DON'T

    public static Item MakeStick()
    {
        itemCount++;  // Mutable state is bad
        return new Item($"Stick #{itemCount}");
    }
}
```

### ❌ Factories Depending on Other Factories (Circular)

```csharp
// WRONG: Circular dependency
public static Item MakeA()
{
    return new Item { RelatedItem = MakeB() };
}

public static Item MakeB()
{
    return new Item { RelatedItem = MakeA() };  // Circular!
}
```

---

## Related Files

**Factory Implementations**:
- `Items/ItemFactory.cs` - Item creation (ItemFactory.cs:1-150)
- `Actors/NPCFactory.cs` - NPC creation (NPCFactory.cs:1-80)
- `Bodies/BodyPartFactory.cs` - Body structure creation
- `Magic/SpellFactory.cs` - Spell creation
- `Environments/LocationFactory.cs` - Location creation
- `Environments/ZoneFactory.cs` - Zone creation

**Related Guides**:
- [builder-patterns.md](builder-patterns.md) - When to use builders instead
- [crafting-system.md](crafting-system.md) - RecipeBuilder creates recipes
- [effect-system.md](effect-system.md) - EffectBuilder creates effects
- [composition-architecture.md](composition-architecture.md) - How NPCs use factories
- [complete-examples.md](complete-examples.md) - Full content creation examples

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
