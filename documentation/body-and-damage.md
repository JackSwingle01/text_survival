# Body and Damage System - Complete Guide

Body hierarchy (Region → Tissue → Organ), capacity system, body composition (fat/muscle), damage processing, and healing mechanics.

## Table of Contents

- [Body Hierarchy](#body-hierarchy)
- [Body Parts and Structure](#body-parts-and-structure)
- [Capacity System](#capacity-system)
- [Body Composition](#body-composition)
- [Damage System](#damage-system)
- [Healing Mechanics](#healing-mechanics)
- [Best Practices](#best-practices)

---

## Body Hierarchy

The body system uses hierarchical composition:

```
Body
└── BodyRegion (Head, Torso, Arms, Legs)
    └── Tissue (Skin, Muscle, Bone)
        └── Organ (Brain, Heart, Lungs, etc.)
```

### Example Structure

```csharp
Torso (BodyRegion)
├── Skin (Tissue)
├── Muscle (Tissue)
├── Bone (Ribs, Tissue)
├── Heart (Organ)
└── Lungs (Organ)
```

**Key Files:**
- `Bodies/Body.cs` - Main body class
- `Bodies/BodyPart.cs` - Individual body part
- `Bodies/DamageProcessor.cs` - Damage calculation and propagation
- `Bodies/Capacities.cs` - Capacity definitions

---

## Body Parts and Structure

### Body Part Properties

```csharp
public class BodyPart
{
    public string Name { get; set; }
    public double MaxHealth { get; set; }
    public double CurrentHealth { get; set; }
    public BodyPartType Type { get; set; }  // Region, Tissue, Organ
    public List<BodyPart> SubParts { get; set; }
    public Dictionary<string, double> CapacityContributions { get; set; }
}
```

### Body Part Types

- **BodyRegion**: Major body areas (Head, Torso, Arms, Legs)
- **Tissue**: Structural layers (Skin, Muscle, Bone, Fat)
- **Organ**: Vital organs (Brain, Heart, Lungs, Liver, etc.)

### Creating Body Structure

```csharp
public static Body CreateHumanBody()
{
    var body = new Body();

    // Create head
    var head = new BodyPart("Head", BodyPartType.Region);
    head.AddSubPart(new BodyPart("Skull", BodyPartType.Tissue));
    head.AddSubPart(new BodyPart("Brain", BodyPartType.Organ)
    {
        CapacityContributions = new()
        {
            { CapacityNames.Consciousness, 1.0 },
            { CapacityNames.Manipulation, 0.5 }
        }
    });

    body.AddPart(head);
    return body;
}
```

---

## Capacity System

Capacities represent functional abilities derived from body part health.

### Core Capacities

```csharp
public static class CapacityNames
{
    public const string Moving = "Moving";
    public const string Manipulation = "Manipulation";
    public const string Consciousness = "Consciousness";
    public const string Sight = "Sight";
    public const string Hearing = "Hearing";
    public const string BloodPumping = "BloodPumping";
    public const string Breathing = "Breathing";
}
```

### Capacity Calculation

Capacities are calculated from body part health:

```csharp
public double GetCapacityValue(string capacityName)
{
    double totalContribution = 0;
    double weightedHealth = 0;

    foreach (var part in GetAllParts())
    {
        if (part.CapacityContributions.TryGetValue(capacityName, out double contribution))
        {
            totalContribution += contribution;
            weightedHealth += (part.CurrentHealth / part.MaxHealth) * contribution;
        }
    }

    return totalContribution > 0 ? weightedHealth / totalContribution : 0;
}
```

### Using Capacities

```csharp
// Check if player can move
var movingCapacity = player.Body.GetCapacityValue(CapacityNames.Moving);
if (movingCapacity < 0.3)
{
    Output.WriteLine("You're too injured to move!");
    return;
}

// Calculate speed based on capacity
var baseSpeed = 5.0;
var actualSpeed = baseSpeed * movingCapacity;

// Check consciousness
if (player.Body.GetCapacityValue(CapacityNames.Consciousness) < 0.1)
{
    Output.WriteLine("You lose consciousness!");
    player.Die();
}
```

---

## Body Composition

Body tracks fat and muscle mass dynamically.

### Composition Properties

```csharp
public class Body
{
    public double FatKg { get; set; }
    public double MuscleKg { get; set; }
    public double TotalWeight => FatKg + MuscleKg + BoneWeight;

    public double FatPercentage => FatKg / TotalWeight;
    public double MusclePercentage => MuscleKg / TotalWeight;
}
```

### Composition Effects

```csharp
// Fat increases cold resistance
var coldResistance = BaseColdResistance + (body.FatPercentage * 0.3);

// Muscle increases strength capacity
var strength = BaseStrength * (1 + body.MusclePercentage * 0.5);

// Both affect vitality
var vitality = (body.FatPercentage * 0.4) + (body.MusclePercentage * 0.6);

// Weight affects speed
var speedPenalty = Math.Max(0, (body.TotalWeight - OptimalWeight) * 0.01);
var actualSpeed = BaseSpeed * (1 - speedPenalty);
```

### Changing Composition

```csharp
// Consume calories to gain fat/muscle
public void ConsumeCalories(double calories)
{
    // Distribute based on activity
    if (RecentExercise)
    {
        MuscleKg += calories * 0.0001;  // Muscle building
    }
    else
    {
        FatKg += calories * 0.0001;  // Fat storage
    }
}

// Burn calories reduces fat/muscle
public void BurnCalories(double calories)
{
    // Burn fat first, then muscle
    var fatBurn = Math.Min(FatKg * 0.1, calories * 0.00008);
    FatKg -= fatBurn;

    var remainingCalories = calories - (fatBurn / 0.00008);
    if (remainingCalories > 0 && FatKg < MinFatKg)
    {
        MuscleKg -= remainingCalories * 0.00005;  // Muscle catabolism
    }
}
```

---

## Damage System

### Critical Rule: Single Entry Point

**❗ ALWAYS use `Body.Damage(DamageInfo)` - NEVER modify body parts directly!**

```csharp
// ✅ CORRECT
player.Body.Damage(new DamageInfo
{
    Amount = 15,
    DamageType = DamageType.Slashing,
    TargetRegion = BodyRegion.Torso
});

// ❌ WRONG
player.Body.GetPart("Torso").Health -= 15;  // DON'T DO THIS!
```

### DamageInfo Structure

```csharp
public class DamageInfo
{
    public double Amount { get; set; }
    public DamageType DamageType { get; set; }      // Blunt, Slashing, Piercing, etc.
    public BodyRegion TargetRegion { get; set; }    // Head, Torso, Arms, Legs
    public string SpecificPart { get; set; }        // Optional: target specific part
    public bool IgnoresArmor { get; set; }
}

public enum DamageType
{
    Blunt,      // Clubs, fists, falls
    Slashing,   // Knives, claws
    Piercing,   // Spears, teeth, arrows
    Burn,       // Fire
    Freeze,     // Extreme cold
    Poison      // Toxins
}
```

### Damage Processing Flow

```csharp
Body.Damage(DamageInfo damageInfo)
    ↓
DamageProcessor.DamageBody(Body body, DamageInfo info)
    ↓
1. Select target body part (region → specific part)
    ↓
2. Apply armor reduction (if applicable)
    ↓
3. Calculate damage distribution
    ↓
4. Damage part and propagate to sub-parts
    ↓
5. Check for critical injuries
    ↓
6. Generate effects (bleeding, broken bones, etc.)
    ↓
7. Update capacities
```

### Damage Examples

```csharp
// Simple damage
player.Body.Damage(new DamageInfo
{
    Amount = 10,
    DamageType = DamageType.Blunt,
    TargetRegion = BodyRegion.Torso
});

// Targeted damage
player.Body.Damage(new DamageInfo
{
    Amount = 25,
    DamageType = DamageType.Slashing,
    TargetRegion = BodyRegion.Arms,
    SpecificPart = "Right Hand"
});

// Environmental damage
player.Body.Damage(new DamageInfo
{
    Amount = 5,
    DamageType = DamageType.Freeze,
    TargetRegion = BodyRegion.Legs,  // Frostbite on extremities
    IgnoresArmor = true
});

// Poison damage (affects whole body)
foreach (var region in AllRegions)
{
    player.Body.Damage(new DamageInfo
    {
        Amount = 2,
        DamageType = DamageType.Poison,
        TargetRegion = region
    });
}
```

### Damage Propagation

Damage flows through hierarchy:

```
Torso takes 20 damage (Slashing)
    ↓
Skin takes 8 damage (40% of total)
    ↓
Muscle takes 7 damage (35% of total)
    ↓
Ribs take 3 damage (15% of total)
    ↓
Heart takes 2 damage (10% of total)
```

### Critical Injuries

```csharp
// Check for critical damage
if (part.Type == BodyPartType.Organ && part.HealthPercentage < 0.3)
{
    if (part.Name == "Heart")
    {
        Output.WriteLine("Your heart is failing!");
        // Apply severe effects
        effectRegistry.AddEffect(Effects.HeartFailure());
    }
    else if (part.Name == "Brain")
    {
        Output.WriteLine("You've suffered severe head trauma!");
        effectRegistry.AddEffect(Effects.Concussion());
    }
}

// Destroyed parts
if (part.CurrentHealth <= 0)
{
    Output.WriteLine($"Your {part.Name} is destroyed!");
    part.IsDestroyed = true;

    // Remove capacity contributions
    foreach (var capacity in part.CapacityContributions.Keys)
    {
        // Recalculate capacities
    }
}
```

---

## Healing Mechanics

### Natural Healing

```csharp
public void Heal(double amount)
{
    // Heal from most critical to least
    var sortedParts = GetAllParts()
        .Where(p => !p.IsDestroyed && p.CurrentHealth < p.MaxHealth)
        .OrderBy(p => p.HealthPercentage)
        .ToList();

    double remainingHeal = amount;
    foreach (var part in sortedParts)
    {
        if (remainingHeal <= 0) break;

        double healAmount = Math.Min(remainingHeal, part.MaxHealth - part.CurrentHealth);
        part.CurrentHealth += healAmount;
        remainingHeal -= healAmount;
    }
}
```

### Rest and Recovery

```csharp
public void Rest(int minutes)
{
    // Natural healing during rest
    double healRate = 0.1 * (minutes / 60.0);  // 0.1 HP per hour

    // Bonus healing if well-fed and hydrated
    if (survivalData.CaloriesRemaining > 1000)
    {
        healRate *= 1.5;
    }

    // Reduced healing if starving
    if (survivalData.CaloriesRemaining < 200)
    {
        healRate *= 0.3;
    }

    Heal(healRate);

    // Reduce fatigue
    survivalData.Exhaustion = Math.Max(0, survivalData.Exhaustion - (minutes / 60.0) * 10);
}
```

### Healing Items

```csharp
public static Item CreateMedicinalHerb()
{
    return new ConsumableItem
    {
        Name = "Medicinal Herb",
        OnConsume = (player) =>
        {
            player.Body.Heal(15);
            Output.WriteLine("You feel better after applying the herbs.");
        }
    };
}

// Using healing item
player.Inventory.GetItem("Medicinal Herb").Consume(player);
```

---

## Best Practices

### ✅ DO: Use Single Damage Entry Point

```csharp
// Good
player.Body.Damage(damageInfo);
```

### ✅ DO: Check Capacities Before Actions

```csharp
// Good
if (player.Body.GetCapacityValue(CapacityNames.Manipulation) < 0.5)
{
    Output.WriteLine("Your hands are too injured to craft!");
    return;
}
```

### ✅ DO: Consider Body Composition

```csharp
// Good: Fat affects cold resistance
var coldResistance = 0.5 + (player.Body.FatPercentage * 0.4);
```

### ❌ DON'T: Modify Body Parts Directly

```csharp
// Bad
part.CurrentHealth -= damage;

// Good
body.Damage(damageInfo);
```

### ❌ DON'T: Forget Critical Checks

```csharp
// Bad
body.Damage(damageInfo);
// No check for death/unconsciousness

// Good
body.Damage(damageInfo);
if (body.GetCapacityValue(CapacityNames.Consciousness) < 0.1)
{
    player.Die();
}
```

### ❌ DON'T: Ignore Body Composition

```csharp
// Bad: Fixed values
var speed = 5.0;

// Good: Account for weight
var speed = 5.0 * (1 - Math.Max(0, (body.TotalWeight - 70) * 0.01));
```

---

**Related Files:**
- [SKILL.md](../SKILL.md) - Main guidelines
- [survival-processing.md](survival-processing.md) - Survival mechanics
- [effect-system.md](effect-system.md) - Effects and injuries

**Last Updated**: 2025-11-01
