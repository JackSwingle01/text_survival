# Crafting & Foraging Overhaul - First Steps Implementation Plan

**Last Updated: 2025-11-01**
**Parent Plan**: `dev/active/crafting-foraging-overhaul/crafting-foraging-overhaul-plan.md`

## Executive Summary

This plan focuses on implementing **Phase 1 (Cleanup) and Phase 2 (Materials)** of the crafting and foraging overhaul. These are the foundational steps that establish a clean baseline for the new system by:

1. Removing crafted items from world spawning
2. Making the starting experience harsh and challenging
3. Adding essential materials needed for realistic progression

**Timeline**: 5-7 days
**Effort Level**: L
**Risk Level**: Low (foundational work, easy to validate)

---

## Current State

According to the parent plan context:

**Starting Conditions** (`Program.cs` lines 26-32):
- Player starts with oldBag containing knife + leather armor
- This breaks the intended difficulty curve

**World Spawning** (`LocationFactory.cs`):
- Crafted items (spears, torches, hand axes, armor) spawn in world
- Items immediately visible without foraging
- Breaks "materials only" design philosophy

**Material System** (`ItemFactory.cs` & `ItemProperty.cs`):
- Missing critical materials for fire-making (tinder, plant fibers, bark)
- Missing processed materials (charcoal, leather)
- No stone variety (all stones are generic)

**Impact**: Players can skip crafting entirely by finding crafted items in the world.

---

## Implementation Phases

### Phase 1: Cleanup & Foundation (Days 1-2)

Remove all crafted items from spawning and establish harsh starting conditions.

#### Task 1.1: Update Starting Inventory ⏳
**File**: `Program.cs` (lines 26-32)
**Effort**: S (30 minutes)

**Current Code**:
```csharp
// Lines 26-32
var oldBag = new Container("Old Leather Bag", 10, 0.5);
oldBag.Add(ItemFactory.MakeKnife());
oldBag.Add(ItemFactory.MakeLeatherTunic());
oldBag.Add(ItemFactory.MakeLeatherPants());
oldBag.Add(ItemFactory.MakeMoccasins());
player.Inventory.Add(oldBag);
```

**Changes Required**:
1. Keep the Container (storage is needed)
2. Remove knife (breaks progression)
3. Replace leather armor with 2 tattered items
4. Create `MakeTatteredChestWrap()` and `MakeTatteredLegWrap()` in ItemFactory

**New Code Structure**:
```csharp
var oldBag = new Container("Tattered Sack", 10, 0.3); // Lighter, worse quality
oldBag.Add(ItemFactory.MakeTatteredChestWrap()); // Minimal warmth
oldBag.Add(ItemFactory.MakeTatteredLegWrap());   // Minimal warmth
player.Inventory.Add(oldBag);
```

**Acceptance Criteria**:
- [ ] Player starts with no weapons
- [ ] Player starts with minimal protection (tattered clothes)
- [ ] Starting insulation < 0.05 total
- [ ] Player immediately needs to forage/craft to survive

**Testing**:
- Start new game
- Check inventory contains only tattered items
- Verify no knife in starting equipment
- Check warmth values are minimal

---

#### Task 1.2: Create Tattered Clothing Items ⏳
**File**: `ItemFactory.cs`
**Effort**: S (30 minutes)

**Implementation**:

```csharp
public static Item MakeTatteredChestWrap()
{
    var item = new Item("Tattered Chest Wrap", ItemCategory.Armor, 0.1);
    item.Description = "Barely more than rags bound around your torso. Provides minimal warmth and protection.";
    item.Slot = EquipmentSlot.Chest;
    item.ArmorRating = 0.5; // Almost nothing
    item.Insulation = 0.02; // Minimal warmth
    return item;
}

public static Item MakeTatteredLegWrap()
{
    var item = new Item("Tattered Leg Wraps", ItemCategory.Armor, 0.1);
    item.Description = "Torn fabric wrapped crudely around your legs. Better than nothing, barely.";
    item.Slot = EquipmentSlot.Legs;
    item.ArmorRating = 0.5;
    item.Insulation = 0.02;
    return item;
}
```

**Design Notes**:
- NOT craftable (starting-only items)
- No CraftingProperties needed
- Insulation values match context file specification (0.02 each)
- Ice Age appropriate descriptions

**Acceptance Criteria**:
- [ ] Both items compile and run
- [ ] Can be equipped in correct slots
- [ ] Armor rating ≤ 0.5
- [ ] Insulation = 0.02 each
- [ ] Descriptions fit Ice Age theme

---

#### Task 1.3: Clean LocationFactory Spawn Tables ⏳
**File**: `LocationFactory.cs`
**Effort**: M (2-3 hours)

**Methods to Modify**:
- `GetRandomForestItem()`
- `GetRandomCaveItem()`
- `GetRandomRiverbankItem()`
- `GetRandomPlainsItem()`
- `GetRandomHillsideItem()`

**Items to REMOVE** (crafted/processed):
- All weapons (spears, clubs, hand axes)
- All armor (leather tunic, hide armor, etc.)
- All tools (knives, axes)
- Torches (require fire to make)
- Any cooked/processed items

**Items to KEEP** (natural materials):
- Sticks / Branches / Wood
- Stones (all types)
- Berries / Mushrooms / Roots
- Water sources
- Hides (from dead animals - natural finds)
- Bones (from old kills - natural finds)

**Approach**:
1. Read each GetRandom*Item() method
2. Identify crafted items
3. Remove or comment them out
4. Verify only natural materials remain
5. Document removals

**Acceptance Criteria**:
- [ ] No weapons spawn in any biome
- [ ] No armor spawns in any biome
- [ ] No tools spawn in any biome
- [ ] No processed items spawn (torches, cooked food)
- [ ] Natural materials still spawn (sticks, stones, berries, water)
- [ ] Game still playable (enough materials to start crafting)

---

#### Task 1.4: Clean ForageFeature Resource Tables ⏳
**File**: `LocationFactory.cs` (ForageFeature creation in Make* methods)
**Effort**: S (1 hour)

**Methods to Review**:
- `MakeForest()` - ForageFeature setup
- `MakeCave()` - ForageFeature setup
- `MakeRiverbank()` - ForageFeature setup
- `MakePlain()` - ForageFeature setup
- `MakeHillside()` - ForageFeature setup

**Pattern**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeSomething(), abundance);
```

**Check Each Resource**:
- Is it a natural material? ✅ Keep
- Is it crafted/processed? ❌ Remove

**Common Violations**:
- Torches in forage tables
- Spears in forage tables
- Arrows in forage tables
- Cooked food in forage tables

**Acceptance Criteria**:
- [ ] Forest forage = natural only (sticks, berries, mushrooms, plant matter)
- [ ] Cave forage = natural only (stones, water, bones)
- [ ] Riverbank forage = natural only (stones, water, clay, fish)
- [ ] Plains forage = natural only (grass, stones, roots)
- [ ] Hillside forage = natural only (stones, roots, herbs)

---

#### Task 1.5: Hide Initial Visible Items ⏳
**File**: `LocationFactory.cs`
**Effort**: S (30 minutes)

**Pattern to Find**:
```csharp
for (int i = 0; i < itemCount; i++)
{
    location.AddItem(GetRandomForestItem());
}
```

**Change**:
```csharp
// No initial visible items - must forage to find resources
// for (int i = 0; i < itemCount; i++)
// {
//     location.AddItem(GetRandomForestItem());
// }
```

**Rationale**:
- Per design decision #3: "Nothing visible until foraging"
- Creates sense of exploration
- Locations feel empty until searched

**Alternative Approach** (if items still needed for some reason):
```csharp
int itemCount = 0; // Changed from 3-5 to 0
```

**Acceptance Criteria**:
- [ ] New locations have 0 visible items initially
- [ ] Player must use Forage action to find materials
- [ ] Foraged items appear after ForageFeature is used
- [ ] No immediate "loot" on arrival

**Testing**:
- Start new game
- Look around starting location
- Verify no items listed
- Use Forage action
- Verify items now appear

---

### Phase 2: Material System Enhancement (Days 3-5)

Add missing materials needed for realistic crafting progression.

#### Task 2.1: Extend ItemProperty Enum ⏳
**File**: `Crafting/ItemProperty.cs`
**Effort**: S (15 minutes)

**Current Enum** (18 properties):
```csharp
public enum ItemProperty
{
    Stone, Wood, Binding, Flammable, Sharp,
    Hide, Bone, Sinew, Firestarter, Insulation,
    Heavy, Food, Water, Clay, Medicinal,
    RawMeat, CookedMeat, Fat
}
```

**Add New Properties**:
```csharp
public enum ItemProperty
{
    // Existing...
    Stone, Wood, Binding, Flammable, Sharp,
    Hide, Bone, Sinew, Firestarter, Insulation,
    Heavy, Food, Water, Clay, Medicinal,
    RawMeat, CookedMeat, Fat,

    // NEW - Phase 2 additions
    Tinder,      // Dry grass, bark, tinder bundles - critical for fire
    PlantFiber,  // Cordage alternative to sinew (early game)
    Fur,         // Superior insulation from hunting
    Antler,      // Tool handles, specialized uses
    Leather,     // Processed hide (tanned)
    Charcoal     // Fire-hardened wood, fuel
}
```

**Design Rationale**:
- **Tinder**: Separate from Flammable - specifically for fire-starting
- **PlantFiber**: Early-game cordage before hunting provides sinew
- **Fur**: Better than hide for cold-weather gear
- **Antler**: Realistic tool component (better handles than wood)
- **Leather**: Processed hide (requires tanning step)
- **Charcoal**: Enables fire-hardening wood (realistic technology)

**Acceptance Criteria**:
- [ ] All 6 new properties compile
- [ ] No enum conflicts
- [ ] Properties follow naming convention
- [ ] Comments added for clarity

---

#### Task 2.2: Create Tinder Items ⏳
**File**: `ItemFactory.cs`
**Effort**: M (1.5 hours)

**Items to Create**:

##### 2.2a: Dry Grass
```csharp
public static Item MakeDryGrass()
{
    var item = new Item("Dry Grass", ItemCategory.Material, 0.02);
    item.Description = "Dried grass stems, brittle and sun-bleached. Catches fire easily when bundled.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Tinder, 1.0 },
        { ItemProperty.Flammable, 0.5 },
        { ItemProperty.Insulation, 0.3 } // Can be used as stuffing
    };
    item.IsStackable = true;
    return item;
}
```

##### 2.2b: Bark Strips
```csharp
public static Item MakeBarkStrips()
{
    var item = new Item("Bark Strips", ItemCategory.Material, 0.05);
    item.Description = "Papery inner bark peeled from birch or cedar. Burns well and can be twisted into cordage.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Tinder, 1.2 },
        { ItemProperty.Binding, 0.5 }, // Weak cordage
        { ItemProperty.Flammable, 0.8 }
    };
    item.IsStackable = true;
    return item;
}
```

##### 2.2c: Tinder Bundle
```csharp
public static Item MakeTinderBundle()
{
    var item = new Item("Tinder Bundle", ItemCategory.Material, 0.03);
    item.Description = "A carefully prepared nest of dry grass, bark shavings, and plant fluff. Ready to catch the smallest spark.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Tinder, 2.0 }, // Best tinder
        { ItemProperty.Flammable, 1.0 }
    };
    item.IsStackable = true;
    return item;
}
```

**Design Notes**:
- Weight values realistic (very light materials)
- IsStackable = true (player will need multiples)
- Property values reflect quality: Tinder Bundle > Bark > Dry Grass
- Dual-purpose where realistic (bark = tinder + binding)

**Acceptance Criteria**:
- [ ] All 3 items compile
- [ ] Tinder property present on all
- [ ] Weight < 0.1 kg each
- [ ] IsStackable = true
- [ ] Descriptions Ice Age appropriate
- [ ] Can be added to inventory

---

#### Task 2.3: Create Plant Fiber Items ⏳
**File**: `ItemFactory.cs`
**Effort**: S (1 hour)

##### 2.3a: Plant Fibers
```csharp
public static Item MakePlantFibers()
{
    var item = new Item("Plant Fibers", ItemCategory.Material, 0.04);
    item.Description = "Tough fibers stripped from plant stems and bark. Can be twisted into serviceable cordage.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.PlantFiber, 1.0 },
        { ItemProperty.Binding, 0.8 } // Slightly worse than sinew
    };
    item.IsStackable = true;
    return item;
}
```

##### 2.3b: Rushes
```csharp
public static Item MakeRushes()
{
    var item = new Item("Rushes", ItemCategory.Material, 0.06);
    item.Description = "Long, fibrous wetland plants. Useful for weaving, binding, and insulation when dried.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.PlantFiber, 0.8 },
        { ItemProperty.Binding, 0.6 },
        { ItemProperty.Insulation, 0.5 } // Can be woven into mats
    };
    item.IsStackable = true;
    return item;
}
```

**Design Rationale**:
- PlantFiber property enables early-game crafting without hunting
- Binding values lower than Sinew (progression incentive)
- Rushes unique to Riverbank biome (biome differentiation)

**Acceptance Criteria**:
- [ ] Both items compile
- [ ] PlantFiber property present
- [ ] Binding values < 1.0 (worse than sinew)
- [ ] IsStackable = true
- [ ] Rushes have Insulation property

---

#### Task 2.4: Create Charcoal Item ⏳
**File**: `ItemFactory.cs`
**Effort**: S (30 minutes)

```csharp
public static Item MakeCharcoal()
{
    var item = new Item("Charcoal", ItemCategory.Material, 0.05);
    item.Description = "Blackened wood from incomplete burning. Can be used to harden other wood in fire or as fuel.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Charcoal, 1.0 },
        { ItemProperty.Flammable, 1.2 } // Burns hot
    };
    item.IsStackable = true;
    return item;
}
```

**Future Use**:
- Required for fire-hardened spear recipe
- Used in bone knife recipe (fire-hardening)
- Realistic Ice Age technology

**Acceptance Criteria**:
- [ ] Item compiles
- [ ] Charcoal property present
- [ ] Flammable property present (fuel value)
- [ ] IsStackable = true
- [ ] Description explains use

---

#### Task 2.5: Create Stone Variety ⏳
**File**: `ItemFactory.cs`
**Effort**: M (1.5 hours)

**Current Issue**: Only one generic `MakeStone()` exists.

**Solution**: Create 3 stone types with different purposes.

##### 2.5a: Rename Existing Stone → River Stone
```csharp
// Rename MakeStone() to:
public static Item MakeRiverStone()
{
    var item = new Item("River Stone", ItemCategory.Material, 0.3);
    item.Description = "A smooth, rounded stone from the riverbed. Good for smashing or as a hammer.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Stone, 1.0 },
        { ItemProperty.Heavy, 0.5 }
    };
    item.IsStackable = true;
    return item;
}
```

##### 2.5b: Sharp Stone (New)
```csharp
public static Item MakeSharpStone()
{
    var item = new Item("Sharp Stone", ItemCategory.Material, 0.2);
    item.Description = "A jagged stone broken to reveal sharp edges. Can be used as a crude cutting tool.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Stone, 1.0 },
        { ItemProperty.Sharp, 0.5 } // Not as good as flint
    };
    item.IsStackable = true;
    return item;
}
```

**Note**: This can also become a craftable tool (Sharp Rock weapon) - recipe uses 2x River Stone smashed together.

##### 2.5c: Handstone (New)
```csharp
public static Item MakeHandstone()
{
    var item = new Item("Handstone", ItemCategory.Material, 0.4);
    item.Description = "A dense, fist-sized stone that fits comfortably in hand. Ideal for pounding and hammering.";
    item.CraftingProperties = new Dictionary<ItemProperty, double>
    {
        { ItemProperty.Stone, 1.0 },
        { ItemProperty.Heavy, 1.0 } // Heavier than river stone
    };
    item.IsStackable = true;
    return item;
}
```

**Migration Note**: Any existing code calling `MakeStone()` must be updated to `MakeRiverStone()`.

**Acceptance Criteria**:
- [ ] MakeStone() renamed to MakeRiverStone()
- [ ] MakeSharpStone() created with Sharp property
- [ ] MakeHandstone() created with Heavy property
- [ ] All LocationFactory references updated
- [ ] All ForageFeature references updated
- [ ] Game compiles after rename

**Testing**:
- Run game after rename
- Verify no "MakeStone not found" errors
- Forage in different biomes, verify stones appear

---

#### Task 2.6: Update Biome Forage Tables ⏳
**File**: `LocationFactory.cs` (ForageFeature setup in Make* methods)
**Effort**: M (2 hours)

**Goal**: Add new materials to appropriate biomes, creating unique biome profiles.

##### Forest Biome (`MakeForest()`)
**Add**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeDryGrass(), 0.5);      // Common
forageFeature.AddResource(() => ItemFactory.MakeBarkStrips(), 0.7);    // Very common (trees!)
forageFeature.AddResource(() => ItemFactory.MakePlantFibers(), 0.6);   // Common
forageFeature.AddResource(() => ItemFactory.MakeTinderBundle(), 0.2);  // Rare (takes effort)
```

**Result**: Forest is BEST for fire-starting and cordage.

##### Riverbank Biome (`MakeRiverbank()`)
**Add**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeRushes(), 0.8);        // Very common
forageFeature.AddResource(() => ItemFactory.MakeRiverStone(), 0.9);    // Very common
forageFeature.AddResource(() => ItemFactory.MakeDryGrass(), 0.3);      // Less common (wet area)
```

**Result**: Riverbank is BEST for stones and water plants, POOR for tinder.

##### Plains Biome (`MakePlain()`)
**Add**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeDryGrass(), 0.8);      // Very common (grassland!)
forageFeature.AddResource(() => ItemFactory.MakePlantFibers(), 0.4);   // Moderate
forageFeature.AddResource(() => ItemFactory.MakeRiverStone(), 0.3);    // Uncommon
```

**Result**: Plains is BEST for grass/tinder, POOR for wood.

##### Cave Biome (`MakeCave()`)
**Add**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeRiverStone(), 0.6);    // Moderate
forageFeature.AddResource(() => ItemFactory.MakeHandstone(), 0.4);     // Moderate
forageFeature.AddResource(() => ItemFactory.MakeSharpStone(), 0.3);    // Uncommon (natural breaks)
// NO organic materials (no grass, bark, fibers)
```

**Result**: Cave is BEST for stones, TERRIBLE for organics (fire challenge!).

##### Hillside Biome (`MakeHillside()`)
**Add**:
```csharp
forageFeature.AddResource(() => ItemFactory.MakeRiverStone(), 0.7);    // Common
forageFeature.AddResource(() => ItemFactory.MakeHandstone(), 0.5);     // Common
forageFeature.AddResource(() => ItemFactory.MakeDryGrass(), 0.4);      // Moderate
forageFeature.AddResource(() => ItemFactory.MakePlantFibers(), 0.3);   // Uncommon
```

**Result**: Hillside is BALANCED stone/organic.

**Abundance Scale**:
- 0.1-0.2 = Rare
- 0.3-0.4 = Uncommon
- 0.5-0.6 = Common
- 0.7-0.8 = Very common
- 0.9-1.0 = Abundant

**Acceptance Criteria**:
- [ ] Forest has high tinder/fiber abundance
- [ ] Riverbank has high stone/water abundance
- [ ] Plains has high grass abundance
- [ ] Cave has NO organic materials
- [ ] Hillside has balanced stone/organic
- [ ] Each biome has unique material profile
- [ ] New materials integrated into existing AddResource() patterns

**Testing**:
- Forage in each biome multiple times
- Verify material distributions match design
- Verify Cave lacks organics (fire challenge confirmed)

---

## Success Metrics

### Phase 1 Success Criteria
- [ ] Player starts with no weapons
- [ ] Player starts with < 0.05 total insulation
- [ ] No crafted items spawn in any biome
- [ ] Locations appear empty until foraged
- [ ] Game is still playable (materials available)

### Phase 2 Success Criteria
- [ ] 6 new ItemProperty enums added
- [ ] 10+ new material items created
- [ ] All new items have Ice Age appropriate descriptions
- [ ] Each biome has unique material profile
- [ ] Cave biome lacks organics (creates fire challenge)
- [ ] All new materials are stackable

### Integration Testing
- [ ] Start new game, verify harsh start
- [ ] Forage in Forest, find tinder/fibers
- [ ] Forage in Riverbank, find rushes/stones
- [ ] Forage in Plains, find grass
- [ ] Forage in Cave, find NO organics
- [ ] Verify no crashes or null references
- [ ] Materials appear in inventory correctly

---

## Risk Assessment

### Low Risks

**Risk**: Breaking existing saves
- **Mitigation**: This is early dev, saves can be reset
- **Impact**: Acceptable

**Risk**: Missing a crafted item in spawn tables
- **Mitigation**: Systematic review of each GetRandom*Item() method
- **Impact**: Low - can fix in iteration

**Risk**: Typos in new ItemProperty references
- **Mitigation**: Compile after each enum addition
- **Impact**: Low - compiler will catch

### Medium Risks

**Risk**: Cave biome becomes unplayable (no fire materials)
- **Mitigation**: Addressed in Phase 9 with biome viability testing
- **Mitigation**: Can add minimal bark/grass if needed
- **Impact**: Medium - one biome potentially broken

**Risk**: Starting too harsh (frustrating for testing)
- **Mitigation**: Easy to adjust - add one low-tier weapon back temporarily
- **Fallback**: Create "debug start" mode
- **Impact**: Low - affects testing only

---

## Dependencies

### Phase 1 Dependencies
- **None** - Pure cleanup work

### Phase 2 Dependencies
- **Phase 1 must complete first** - Need clean baseline
- **ItemProperty.cs** - Must add enums before using them in items
- **Task 2.5 affects all biomes** - Stone rename cascades through forage tables

### Execution Order
1. Phase 1: Tasks 1.1 → 1.2 (starting conditions)
2. Phase 1: Tasks 1.3 → 1.4 → 1.5 (world cleanup)
3. Phase 2: Task 2.1 (enums FIRST)
4. Phase 2: Tasks 2.2 → 2.3 → 2.4 (new items)
5. Phase 2: Task 2.5 (stone variety - BEFORE 2.6)
6. Phase 2: Task 2.6 (update all forage tables)

---

## Timeline Estimate

**Phase 1** (4 tasks):
- Day 1: Tasks 1.1, 1.2 (starting conditions) - 1.5 hours
- Day 1: Task 1.3 (clean spawn tables) - 2.5 hours
- Day 2: Tasks 1.4, 1.5 (forage cleanup) - 1.5 hours

**Phase 2** (6 tasks):
- Day 3: Task 2.1 (enums) - 0.25 hours
- Day 3: Tasks 2.2, 2.3 (tinder + fibers) - 2.5 hours
- Day 4: Tasks 2.4, 2.5 (charcoal + stones) - 2 hours
- Day 5: Task 2.6 (biome forage updates) - 2 hours

**Total**: 5 days (12.25 hours actual work)

**Buffer**: 2 days for testing, iteration, bug fixes

**Final Estimate**: 5-7 days including testing

---

## Next Steps After First Steps

Once Phase 1 and Phase 2 are complete:

1. **Validate the foundation** - Extensive testing
2. **Begin Phase 3: Fire-Making System** - Skill checks and recipes
3. **Continue with parent plan** - Tool progression, shelters, etc.

**Parent Plan Reference**: `dev/active/crafting-foraging-overhaul/crafting-foraging-overhaul-plan.md`

---

## Notes

- This plan intentionally focuses on just Phase 1 & 2
- Establishes clean foundation for remaining phases
- Low risk, high validation potential
- Each task has clear acceptance criteria
- Biome differentiation emerges from material distribution
- Cave biome challenge (no organics) is INTENTIONAL design

**Implementation Philosophy**: Do the cleanup work first, then build on clean foundation.

---

## Approval

- [ ] Plan reviewed
- [ ] Timeline acceptable
- [ ] Ready to begin Task 1.1

**Next Action**: Update starting inventory in `Program.cs`
