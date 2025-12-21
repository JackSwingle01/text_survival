# Crafting System

*Created: 2024-11*
*Last Updated: 2025-12-20*

Need-based crafting where players select a need category, then see what they can make.

---

## Overview

The crafting system is **need-based**, not recipe-list based. Players express what they need (fire-starting tool, cutting tool, hunting weapon) and see available options based on their materials.

**Key Files:**
- `Crafting/NeedCraftingSystem.cs` — Manages craft options by need
- `Crafting/CraftOption.cs` — Single craftable item definition
- `Crafting/NeedCategory.cs` — Need category enum
- `Actions/CraftingRunner.cs` — UI for crafting

---

## How It Works

### Flow

1. Player selects "Crafting" from camp menu
2. System shows available need categories (only if materials exist)
3. Player picks a need (e.g., "Fire-starting supplies")
4. System shows craftable options + what's missing
5. Player crafts, time passes, tool is created

### Need Categories

```csharp
public enum NeedCategory
{
    FireStarting,    // Hand drill, bow drill
    CuttingTool,     // Knives
    HuntingWeapon    // Spears
}
```

Future categories: shelter improvements, clothing, containers.

---

## CraftOption

Defines a single craftable item:

```csharp
public class CraftOption
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required NeedCategory Category { get; init; }
    public required int CraftingTimeMinutes { get; init; }
    public required int Durability { get; init; }
    public required List<MaterialRequirement> Requirements { get; init; }
    public required Func<int, Tool> ToolFactory { get; init; }
}
```

### Example: Stone Knife

```csharp
new CraftOption
{
    Name = "Stone Knife",
    Description = "A proper knife with a handle. More durable and easier to use.",
    Category = NeedCategory.CuttingTool,
    CraftingTimeMinutes = 20,
    Durability = 10,
    Requirements = [
        new MaterialRequirement("Stone", 1),
        new MaterialRequirement("Sticks", 1),
        new MaterialRequirement("PlantFiber", 1)
    ],
    ToolFactory = durability => new Tool("Stone Knife", ToolType.Knife, 0.3)
    {
        Durability = durability,
        Damage = 6,
        BlockChance = 0.02,
        WeaponClass = WeaponClass.Blade
    }
}
```

---

## Material Requirements

Requirements use material names that map to Inventory aggregates:

| Material | Inventory Property |
|----------|-------------------|
| Sticks | `inv.StickCount` |
| Logs | `inv.LogCount` |
| Stone | `inv.StoneCount` |
| Bone | `inv.BoneCount` |
| Hide | `inv.HideCount` |
| PlantFiber | `inv.PlantFiberCount` |
| Sinew | `inv.SinewCount` |
| Tinder | `inv.TinderCount` |

### Checking & Consuming

```csharp
// Check if player can craft
bool canMake = option.CanCraft(inventory);

// Check what's missing
var (canCraft, missing) = option.CheckRequirements(inventory);
// missing = ["2 stone", "1 plant fiber"]

// Craft (consumes materials, returns tool)
Tool tool = option.Craft(inventory);
```

---

## Current Craftable Items

### Fire-Starting

| Item | Requirements | Time | Durability |
|------|-------------|------|------------|
| Hand Drill | 2 sticks | 15 min | 5 uses |
| Bow Drill | 3 sticks, 1 fiber | 25 min | 15 uses |

### Cutting Tools

| Item | Requirements | Time | Durability |
|------|-------------|------|------------|
| Sharp Rock | 2 stone | 5 min | 3 uses |
| Stone Knife | 1 stone, 1 stick, 1 fiber | 20 min | 10 uses |
| Bone Knife | 1 bone, 1 fiber | 15 min | 8 uses |

### Hunting Weapons

| Item | Requirements | Time | Durability | Damage |
|------|-------------|------|------------|--------|
| Wooden Spear | 3 sticks | 15 min | 5 uses | 7 |
| Heavy Spear | 1 log | 25 min | 8 uses | 9 |
| Stone-Tipped Spear | 1 log, 1 stone, 1 fiber | 35 min | 12 uses | 12 |

---

## NeedCraftingSystem

Manages all craft options:

```csharp
public class NeedCraftingSystem
{
    // Get options for a need (craftable first, then partial materials)
    public List<CraftOption> GetOptionsForNeed(NeedCategory need, Inventory inventory);

    // Get needs that have at least one option available
    public List<NeedCategory> GetAvailableNeeds(Inventory inventory);
}
```

Options are initialized in the constructor, organized by category.

---

## CraftingRunner

UI handler in `Actions/CraftingRunner.cs`:

```csharp
public void Run()
{
    // 1. Get available needs
    var needs = _crafting.GetAvailableNeeds(_ctx.Inventory);

    // 2. Player picks a need
    var selectedNeed = choice.GetPlayerChoice();

    // 3. Show options for that need
    ShowOptionsForNeed(selectedNeed);
}

private bool DoCraft(CraftOption option)
{
    // Time passes
    _ctx.Update(option.CraftingTimeMinutes);

    // Create tool
    var tool = option.Craft(_ctx.Inventory);

    // Add to inventory (weapons auto-equip)
    if (tool.IsWeapon)
        _ctx.Inventory.EquipWeapon(tool);
    else
        _ctx.Inventory.Tools.Add(tool);
}
```

---

## Design Philosophy

### Why Need-Based?

Traditional crafting shows a recipe list. Players scroll through items looking for what they want.

Need-based crafting inverts this:
- **"I need fire"** → see fire-starting options
- **"I need to cut"** → see cutting tool options
- **"I need to hunt"** → see hunting weapon options

This matches survival thinking: you identify the need, then find solutions.

### What's NOT Craftable

Some things are found, not crafted:
- Fire Striker / Flint & Steel (rare finds from foraging/events)
- Containers (future)
- Some clothing pieces (future)

### Tool Durability

Tools have durability (uses before breaking). Different materials affect durability:
- Sharp Rock: 3 uses (crude, breaks fast)
- Stone Knife: 10 uses (proper handle)
- Stone-Tipped Spear: 12 uses (well-made)

Durability is binary: tool works or it's broken. No quality tiers.

---

## Adding New Craftables

### 1. Add to NeedCraftingSystem

```csharp
private void InitializeNewOptions()
{
    _options.Add(new CraftOption
    {
        Name = "New Tool",
        Description = "Description here",
        Category = NeedCategory.CuttingTool,  // or new category
        CraftingTimeMinutes = 20,
        Durability = 10,
        Requirements = [
            new MaterialRequirement("Stone", 2),
            new MaterialRequirement("Sticks", 1)
        ],
        ToolFactory = durability => new Tool("New Tool", ToolType.Knife, 0.5)
        {
            Durability = durability
        }
    });
}
```

### 2. Add new category (if needed)

In `NeedCategory.cs`:
```csharp
public enum NeedCategory
{
    FireStarting,
    CuttingTool,
    HuntingWeapon,
    NewCategory  // Add here
}
```

Update `CraftingRunner.GetNeedLabel()` and `GetNeedDescription()` for display.

### 3. Add material (if needed)

If using new materials:
1. Add property to `Inventory` (aggregate list)
2. Add count/take methods
3. Add case to `CraftOption.GetMaterialCount()` and `ConsumeMaterial()`

---

**Related Files:**
- [action-system.md](action-system.md) — Runner pattern for CraftingRunner
- [overview.md](overview.md) — System overview
