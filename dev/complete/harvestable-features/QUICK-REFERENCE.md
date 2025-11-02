# Quick Reference - Harvestable Features

**Last Updated**: 2025-11-02 17:15 UTC
**Status**: ✅ Build successful, ready for testing

---

## 🚀 Quick Start

```bash
# Build
dotnet build

# Run
dotnet run

# Test harvestables
# → Main Menu → Look Around (see harvestables)
# → Main Menu → Harvest Resources → Inspect Berry Bush → Harvest
```

---

## 📁 Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `Environments/LocationFeatures.cs/HarvestableFeature.cs` | 1-152 | Core harvestable class |
| `Actions/ActionFactory.cs` | 476-542 | Harvest actions |
| `Actions/ActionFactory.cs` | 1442-1448 | LookAround display |
| `Actions/ActionFactory.cs` | 28-45 | MainMenu integration |
| `Environments/LocationFactory.cs` | 55-102 | Forest harvestables |
| `Environments/LocationFactory.cs` | 289-298 | Plains harvestables |
| `Environments/LocationFactory.cs` | 228-252 | Riverbank harvestables |
| `Items/ItemFactory.cs` | MakePineSap() | New item |
| `Crafting/ItemProperty.cs` | Adhesive, Waterproofing | New enums |

---

## 🌳 Harvestables Quick Ref

### Forest (30/20/15/30% spawn)
- **Berry Bush**: 5 berries (7d), 2 sticks (3d)
- **Willow Stand**: 8 fibers (2d), 4 bark (3d), 2 herbs (4d)
- **Pine Sap Seep**: 4 sap (7d), 1 tinder (10d)
- **Puddle**: 2 water (12h)

### Plains (30% spawn)
- **Meltwater Puddle**: 2 water (12h)

### Riverbank (70% or 30% spawn)
- **River**: 100 water (0.1h), 8 fish (1d), 6 clay (2d)
- **Stream**: 10 water (1h), 3 fish (2d), 5 stones (3d)

---

## ⚙️ API Quick Ref

### Creating Harvestables
```csharp
var feature = new HarvestableFeature("berry_bush", "Wild Berry Bush", location)
{
    Description = "A frost-hardy shrub..."
};
feature.AddResource(ItemFactory.MakeBerry, maxQuantity: 5, respawnHoursPerUnit: 168.0);
location.Features.Add(feature);
```

### Using Harvestables
```csharp
// Check availability
bool canHarvest = feature.HasAvailableResources();

// Harvest
List<Item> items = feature.Harvest();

// Get status
string status = feature.GetStatusDescription();
// → "Wild Berry Bush (berries: abundant, sticks: moderate)"
```

### Actions
```csharp
// Menu action (shows if any harvestables)
Survival.HarvestResources()

// Inspect action
Survival.InspectHarvestable(feature)

// Harvest action (only if resources available)
Survival.HarvestFromFeature(feature)
```

---

## 🐛 Bug Fixed

**Bow Drill Skill Crash**
- **Was**: `.RequiringSkill("Fire-making", 1)` → crash
- **Now**: No skill requirement (skill check in StartFire action)
- **File**: `Crafting/CraftingSystem.cs` line 168

---

## ✅ Next Steps

1. **Playtest**: Verify harvestables spawn and work
2. **Validate**: Run 5 game balance playtests
3. **Document**: Update fire/crafting docs (optional)
4. **Commit**: Ready when user confirms

---

## 📊 Status Bands

| Quantity | Status |
|----------|--------|
| 0 | depleted |
| < 1/3 max | sparse |
| < 2/3 max | moderate |
| ≥ 2/3 max | abundant |

---

## 🔧 Testing Commands

```bash
# Build
dotnet build

# Interactive
dotnet run

# Background test
./play_game.sh
./play_game.sh send 1          # Look Around
./play_game.sh send 4          # Harvest Resources
./play_game.sh tail            # Output

# Kill background
pkill -f "TEST_MODE=1 dotnet"
```

---

## 📝 Commit Message

```
Implement game balance improvements and harvestable features

Game Balance:
- Fire tools: 100% crafting, skill-based usage (30/50/90%)
- Resources: 48h respawn, 1.75x density
- Food: 75% starting, better mushrooms, new items

Harvestable Features:
- Add HarvestableFeature class (multi-resource, lazy respawn)
- Implement 9 harvestables: berry bush, willow, sap seep, puddles, rivers, streams
- Add harvest menu and actions to MainMenu
- Display harvestables in LookAround

Bug Fixes:
- Remove invalid Fire-making skill from Bow Drill recipe

Files: 13 modified, 1 new class, ~320 lines
```

---

## ⚠️ Don't Break

- ForageFeature (complements harvestables)
- EnvironmentFeature (unrelated)
- StartFire skill checks (working)
- Item.NumUses pattern (fire tools use this)
