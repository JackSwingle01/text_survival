# Complete Examples - Full Feature Implementations

End-to-end examples showing complete feature implementations from factory to integration.

## Table of Contents

- [Example 1: New Crafting Recipe (Fur Cloak)](#example-1-new-crafting-recipe-fur-cloak)
- [Example 2: New Status Effect (Infection)](#example-2-new-status-effect-infection)
- [Example 3: New Action with Menu Flow (Fishing)](#example-3-new-action-with-menu-flow-fishing)
- [Example 4: New NPC with Loot (Mammoth)](#example-4-new-npc-with-loot-mammoth)
- [Example 5: New Item with Properties (Bone Needle)](#example-5-new-item-with-properties-bone-needle)

---

## Example 1: New Crafting Recipe (Fur Cloak)

**Goal**: Add a craftable fur cloak that provides high insulation.

### Step 1: Create Item in ItemFactory

```csharp
// In Items/ItemFactory.cs

public static ClothingItem MakeFurCloak()
{
    var cloak = new ClothingItem("Fur Cloak", EquipSlot.Torso)
    {
        Description = "A warm cloak made from tanned hide and thick fur",
        Weight = 2.0,
        Insulation = 0.8  // High insulation value
    };

    // Properties for crafting requirements
    cloak.Properties[ItemProperty.Insulation] = 0.8;
    cloak.Properties[ItemProperty.Hide] = 2.0;  // Made from hide

    return cloak;
}
```

### Step 2: Create Recipe in CraftingSystem

```csharp
// In Crafting/CraftingSystem.cs → InitializeRecipes()

private void InitializeRecipes()
{
    // ... existing recipes ...

    // Fur Cloak Recipe
    var furCloakRecipe = new RecipeBuilder()
        .Named("Fur Cloak")
        .WithDescription("Craft a warm fur cloak for cold weather survival")

        // Material requirements
        .WithPropertyRequirement(ItemProperty.Hide, 3, isConsumed: true)
        .WithPropertyRequirement(ItemProperty.Insulation, 1, isConsumed: true)  // Fur
        .WithPropertyRequirement(ItemProperty.Binding, 0.5, isConsumed: true)
        .WithPropertyRequirement(ItemProperty.Sharp, 1, isConsumed: false)  // Tool

        // Skill and time
        .RequiringSkill("Leatherworking", 5)  // Advanced leatherworking
        .RequiringCraftingTime(180)  // 3 hours

        // Result
        .ResultingInItem(() => ItemFactory.MakeFurCloak())

        .Build();

    _recipes.Add("Fur Cloak", furCloakRecipe);
}
```

---

## Example 2: New Status Effect (Infection)

**Goal**: Create an infection effect that worsens without treatment.

### Step 1: Create Effect Extension

```csharp
// In Effects/EffectBuilderExtensions.cs

public static EffectBuilder Infected(
    this EffectBuilder builder,
    string targetBodyPart,
    double damagePerHour = 3.0)
{
    return builder
        .Named("Infection")
        .Targeting(targetBodyPart)
        .WithSeverity(0.3)
        .WithHourlySeverityChange(0.05)
        .RequiresTreatment(true)
        .ReducesCapacity(CapacityNames.Consciousness, 0.3)
        .ReducesCapacity(CapacityNames.BloodPumping, 0.2)
        .WithApplyMessage("{target}'s wound has become infected!")
        .OnUpdate(actor => {
            double damage = (damagePerHour / 60.0) * builder.Build().Severity;
            actor.Body.Damage(new DamageInfo
            {
                Amount = damage,
                Type = DamageType.Poison,
                Source = "Infection",
                TargetPartName = targetBodyPart
            });
        });
}
```

---

## Example 3: New Action with Menu Flow (Fishing)

**Goal**: Add fishing action with success/failure outcomes.

### Step 1: Create Fish Item

```csharp
public static FoodItem MakeFish()
{
    var fish = new FoodItem("Fresh Fish", calories: 200, hydration: 50)
    {
        Description = "A freshly caught fish",
        Weight = 0.8
    };
    fish.Properties[ItemProperty.RawMeat] = 0.8;
    return fish;
}
```

### Step 2: Create Fishing Actions

```csharp
// In Actions/ActionFactory.cs

public static IGameAction Fish()
{
    return CreateAction("Fish")
        .When(ctx => ctx.CurrentLocation.GetFeature<FishingFeature>() != null)
        .ShowMessage("You cast your line...")
        .Do(ctx => {
            var spot = ctx.CurrentLocation.GetFeature<FishingFeature>();
            ctx.World.Update(30);
            
            if (spot.TryFish(ctx.Player, out Item fish))
            {
                ctx.Player.TakeItem(fish);
                Output.WriteSuccess($"Caught {fish.Name}!");
            }
            else
            {
                Output.WriteLine("Fish aren't biting...");
            }
        })
        .ThenShow(ctx => [Fish("Continue"), Return("Stop")])
        .Build();
}
```

---

## Related Files

- [action-system.md](action-system.md)
- [crafting-system.md](crafting-system.md)
- [effect-system.md](effect-system.md)

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress
