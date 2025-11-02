# Active Development - Current Status

**Date**: 2025-11-02
**Last Updated**: Unit Testing & Critical Bug Fixes
**Status**: ✅ Build Successful - All 74 Tests Passing

---

## Recently Completed

### Unit Test Suite & Critical Bug Fixes ✅ **COMPLETED**
**Date**: 2025-11-02
**Status**: 74 tests passing, 2 critical bugs fixed
**Test Coverage**: Comprehensive tests for all calculation systems

**What Was Added**:
- Complete xUnit test suite covering all calculation systems
- Test helpers and fixtures for creating test objects
- Test constants for baseline assertions
- 9 test files with 74 total tests

**Test Structure**:
```
text_survival.Tests/
├── Bodies/
│   ├── AbilityCalculatorTests.cs       (13 tests)
│   ├── CapacityCalculatorTests.cs      (8 tests)
│   ├── DamageProcessorTests.cs         (10 tests)
│   ├── TissueTests.cs                  (9 tests)
│   └── BodyTests.cs                    (5 tests)
├── Survival/
│   └── SurvivalProcessorTests.cs       (15 tests)
├── Utils/
│   ├── SkillCheckCalculatorTests.cs    (9 tests)
│   └── UtilsTests.cs                   (6 tests)
└── TestHelpers/
    ├── TestFixtures.cs
    └── TestConstants.cs
```

**Critical Bugs Found & Fixed**:

1. **Sleep Energy Clamping Bug** 🔴 **HIGH SEVERITY**
   - **Issue**: `SurvivalProcessor.Sleep()` was clamping energy to 1.0 instead of MAX_ENERGY_MINUTES (960)
   - **Impact**: Players could never recover from exhaustion - sleep was completely broken
   - **Fix**: Changed `Math.Min(1, ...)` to `Math.Min(MAX_ENERGY_MINUTES, ...)`
   - **File**: `Survival/SurvivalProcessor.cs:298`
   - **Result**: Sleep now properly restores energy up to 16 hours (960 minutes)

2. **Organ Capacity Scaling Bug** 🔴 **HIGH SEVERITY**
   - **Issue**: Organs didn't scale their capacity contributions with their condition
   - **Impact**: Destroyed heart still provided 100% blood pumping, destroyed lung still provided 100% breathing
   - **Symptoms**: Organ damage had NO mechanical effect on character abilities
   - **Fix**:
     - Added `GetConditionMultipliers()` override to `Organ` class
     - Included organs in capacity averaging calculation in `CapacityCalculator`
   - **Files**:
     - `Bodies/Organ.cs:27-43`
     - `Bodies/CapacityCalculator.cs:46-49`
     - `text_survival.Tests/Bodies/CapacityCalculatorTests.cs:28-73`
   - **Result**: Organ injuries now have meaningful mechanical consequences
     - Destroyed heart: Blood pumping drops to ~83% (from 100%)
     - One destroyed lung: Breathing drops to ~60% (other lung still functions)
     - Partial damage scales proportionally (50% heart = 50% contribution)

**Code Quality Improvements**:
- Extracted `SkillCheckCalculator` from `ActionFactory` for testability
- Created reusable test fixtures and constants
- All 74 tests passing consistently

**Documentation Updated**:
- `CLAUDE.md` - Added unit testing section
- `documentation/body-and-damage.md` - Added organ capacity scaling details
- `ISSUES.md` - Documented both bugs and fixes

**Files Created**:
- `text_survival.Tests/text_survival.Tests.csproj`
- `text_survival.Tests/Usings.cs`
- `text_survival.Tests/TestHelpers/TestFixtures.cs`
- `text_survival.Tests/TestHelpers/TestConstants.cs`
- 9 test files (see structure above)
- `Utils/SkillCheckCalculator.cs` (extracted for testability)

**Files Modified**:
- `text_survival.csproj` - Excluded test directory from main compilation
- `Survival/SurvivalProcessor.cs` - Fixed sleep energy clamping
- `Bodies/Organ.cs` - Added GetConditionMultipliers()
- `Bodies/CapacityCalculator.cs` - Include organs in averaging
- `Actions/ActionFactory.cs` - Refactored to use SkillCheckCalculator

**Testing Status**:
- ✅ All 74 tests passing
- ✅ Build successful (0 errors, 2 pre-existing warnings)
- ✅ Both critical bugs fixed and verified
- ✅ Comprehensive coverage of calculation systems

**Run Tests**:
```bash
dotnet test                                              # Run all tests
dotnet test --filter "FullyQualifiedName~Survival"       # Survival tests only
dotnet test --filter "FullyQualifiedName~Bodies"         # Body system tests only
```

---

### Playtest Results - Fire Temperature System ✅ **TESTED**
**Date**: 2025-11-02
**Status**: System works, critical balance issues found
**Report**: See `PLAYTEST-REPORT-2025-11-02.md` for full details

**Critical Bug Fixed During Playtest**:
- 🔴 **Stack Overflow** - Circular dependency in temperature calculations (FIXED)
- Files changed: `Environments/LocationFeatures.cs/HeatSourceFeature.cs` (lines 56, 183)
- Solution: Use `Parent.Weather.TemperatureInFahrenheit` instead of `GetTemperature()`

**Playtest Findings**:
- ✅ Fire temperature system compiles and runs (no crashes)
- ✅ Foraging system works correctly
- ✅ Temperature physics are mathematically accurate
- 🔴 **Game is unplayable** - Starting fire burns out too fast, hypothermia kills before player can restart
- 🔴 Body temp: 69°F after 45 min (fatal hypothermia)
- 🔴 Resource gathering too slow vs temperature drop rate

**Critical Fixes Required**:
1. Increase starting fire: 1kg → 3kg fuel (3 hours instead of 1 hour)
2. Add guaranteed materials on ground: 3 Sticks + 2 Tinder
3. These will create viable survival path for new players

### Realistic Fire Temperature System ✅ **ARCHIVED**
**Goal**: Replace binary fire states with physics-based temperature system for realistic fire behavior
**Status**: Implementation complete, moved to `/dev/complete/realistic-fire-temperature/`
**Documentation**: See archived folder for full implementation details, formulas, and testing checklist

**Implementation**: Complete mass-based fuel system with realistic combustion physics
- **6 Fuel Types**: Tinder (450°F), Kindling (600°F), Softwood (750°F), Hardwood (900°F), Bone (650°F), Peat (700°F)
- **Temperature Progression**: Igniting → Building → Roaring → Steady → Dying → Embers → Cold
- **Minimum Temperature Requirements**: Create strategic fire-building progression
- **Tinder Bonus**: +15% fire-starting success when tinder available
- **Proper Scaling**: 700-900°F fire temps → 10-20°F ambient contribution (preserves balance)

**Key Features**:
1. **Fuel Type Properties**:
   - Peak temperature (450-900°F)
   - Burn rate (0.5-3.0 kg/hr)
   - Minimum fire temperature requirement (0-600°F)
   - Ignition bonus (+15% for tinder)
   - Startup time (3-25 minutes)

2. **Physics-Based Temperature**:
   - Startup curve: Sigmoid function (fires build to peak over 5-20 min)
   - Fire size effects: Larger fires burn hotter (0.7x to 1.1x multiplier)
   - Decline curve: Temperature drops as fuel depletes below 30%
   - Ember decay: Square root decay (600°F → 300°F over 25% of burn time)

3. **Strategic Progression**:
   - Tinder/Kindling (0°F req) → Start fire
   - Softwood (400°F req) → Need established fire
   - Hardwood (500°F req) → Need hot fire
   - Bone (600°F req) → Need very hot fire

4. **Enhanced UI**:
   - Color-coded fire phases (Red/Yellow/DarkYellow/DarkGray)
   - Shows fire temp, heat contribution, fuel remaining
   - Fuel list shows type, mass, burn time, temperature requirements
   - Blocks adding fuel if fire too cool (with helpful message)

**Files Created**:
- `Items/FuelType.cs` (234 lines) - Fuel enum, properties, database

**Files Modified**:
- `Crafting/ItemProperty.cs` - Added 6 fuel type properties
- `Items/Item.cs` - Added FuelMassKg, GetFuelType(), IsFuel()
- `Items/ItemFactory.cs` - Updated fuel items with properties, added Hardwood/Peat
- `Environments/LocationFeatures.cs/HeatSourceFeature.cs` - Complete rewrite (377 lines)
- `Actions/ActionFactory.cs` - Updated fire-starting + management UI
- `Program.cs`, `Crafting/CraftingSystem.cs` - Updated fire initialization

**Testing Status**:
- ✅ Compilation: Successful (0 errors)
- ⏳ Runtime: Pending playtest
- ⏳ Balance: Needs validation

**Critical Formula** (DO NOT change without extensive testing):
```csharp
heatOutput = (fireTemp - ambientTemp) / 90.0 * sqrt(fuelMassKg)
```
This scales 800°F fire with 3kg fuel to ~15°F ambient (matching old system).

**Next Steps**:
1. ✅ Archive documentation (COMPLETE - moved to dev/complete)
2. ✅ Run comprehensive playtest (COMPLETE - see PLAYTEST-REPORT-2025-11-02.md)
3. ✅ Fixed critical circular dependency bug (Stack Overflow)
4. 🔴 **CRITICAL FIXES NEEDED**:
   - Increase starting fire fuel: 1kg → 3kg (3 hours warmth)
   - Add guaranteed materials on ground: 3 Sticks + 2 Tinder
   - These fixes will make game playable (currently unwinnable)

**Detailed Documentation**: See `/dev/complete/realistic-fire-temperature/` for complete implementation details, architectural decisions, and testing checklist.

---

## Previously Completed (18:30 UTC)

### 1. Fixed Crafting Preview Bug ✅
**Issue**: Crafting preview showed completely wrong items (Dry Grass, Nuts, Grubs) when crafting "Flint and Steel" which requires Flint+Stone properties.

**Root Cause**: `Item.GetProperty()` method used `FirstOrDefault()` on an enum list, which returns `ItemProperty.Stone` (value 0) when property not found instead of null. This caused `HasProperty()` to incorrectly match items without the required property.

**Fix**: Cast enum list to nullable before `FirstOrDefault()` in `Item.cs:77-82`:
```csharp
public ItemProperty? GetProperty(ItemProperty property)
{
    // Cast to nullable to ensure FirstOrDefault returns null when not found
    return CraftingProperties.Cast<ItemProperty?>().FirstOrDefault(p => p == property);
}
```

**Impact**: Crafting preview now correctly shows only items with required properties.

**Files Changed**: Items/Item.cs

---

### 2. Redesigned "Look Around" UI ✅
**Goal**: Create more streamlined, visually guided location display using colors and box format like stats display.

**Changes**:
- Replaced text-based format with box drawing characters (┌─┐│└┘├┤)
- Location name centered in cyan at top
- Zone/Time/Temperature subheader in gray
- Fire status prominently displayed with color coding:
  - Red: Burning fires
  - Yellow: Dying fires
  - DarkYellow: Embers
  - DarkGray: Cold fires
- Shelters in green
- Harvestable features in yellow
- Items in cyan
- NPCs in red
- Exits condensed on one line in dark cyan
- 54-character width matches stats display

**Before**:
```
>>> CLEARING <<<
(Shadowy Woodland • Morning • 38.7°F)

You see:
    Campfire (burning, 59 min) - warming you by +15°F

Nearby Places:
    → Cold Forest
```

**After**:
```
┌────────────────────────────────────────────────────┐
│                      CLEARING                      │
│ Shadowy Woodland • Morning • 38.7°F                │
├────────────────────────────────────────────────────┤
│ Campfire: Burning (59 min) +15°F                   │
│                                                    │
│ Exits: → Cold Forest  → Ancient Grove              │
└────────────────────────────────────────────────────┘
```

**Impact**: Information hierarchy guides player's eye to most important elements (fires, then shelters, then items, then exits).

**Files Changed**: Actions/ActionFactory.cs lines 1379-1595

---

## Previously Completed

### 1. Game Balance Implementation ✅
**Goal**: Increase day-1 survival rate from ~30% to 70-80%

**Changes**:
- Fire-making: Tool crafting 100% success, usage skill-based (30%/50%/90%)
- Resources: 48h respawn, 1.75x starting density
- Food: 75% starting calories, better mushrooms (120 cal), new items (nuts/eggs/grubs)
- Architecture: Removed crafting RNG, simplified fire tools to use Item.NumUses

**Files**: ItemFactory, Item, CraftingSystem, CraftingRecipe, RecipeBuilder, ActionFactory, ForageFeature, Program, Body, World, LocationFactory

### 2. Harvestable Feature System ✅
**Goal**: Add deterministic, quantity-based resource gathering

**Implementation**:
- **Core**: HarvestableFeature.cs - multi-resource support, per-resource respawn timers
- **Items**: Added ItemProperty.Adhesive/Waterproofing, ItemFactory.MakePineSap()
- **Forest Harvestables**:
  - Berry Bush (30%): 5 berries (168h), 2 sticks (72h)
  - Willow Stand (20%): 8 fibers (48h), 4 bark (72h), 2 herbs (96h)
  - Pine Sap Seep (15%): 4 sap (168h), 1 tinder (240h)
  - Forest Puddle (30%): 2 water (12h)
- **Plains Harvestables**:
  - Meltwater Puddle (30%): 2 water (12h)
- **Riverbank Harvestables**:
  - River (70%): 100 water (0.1h), 8 fish (24h), 6 clay (48h)
  - Stream (30%): 10 water (1h), 3 fish (48h), 5 stones (72h)
- **Actions**: HarvestResources() menu, HarvestFromFeature(), InspectHarvestable()
- **Integration**: Added to MainMenu, displays in LookAround

**Files**:
- New: Environments/LocationFeatures.cs/HarvestableFeature.cs
- Modified: ItemProperty.cs, ItemFactory.cs, LocationFactory.cs, ActionFactory.cs, CraftingSystem.cs (fixed skill requirement bug)

**Bug Fixed**: Removed `.RequiringSkill("Fire-making", 1)` from Bow Drill recipe (skill doesn't exist, was causing crash)

---

## Build Status

✅ **Build successful** (0 errors, 2 pre-existing warnings)
- Player.cs(47,9): CS8765 nullability warning (pre-existing)
- SurvivalData.cs(16,19): CS8618 non-nullable field warning (pre-existing)

---

## Key Design Decisions This Session

### Harvestable Feature Philosophy
1. **Quantity-based vs RNG**: Predictable depletion/respawn vs probabilistic ForageFeature
2. **Multi-resource support**: Single feature provides multiple item types (e.g., berry bush = berries + sticks)
3. **Lazy respawn**: UpdateRespawn() called on-demand, not in update loop (performance)
4. **Water migration**: Puddles/streams/rivers use harvestables, replacing ForageFeature water RNG
5. **Respawn rates**: Berry bush 1 week (user-requested), puddles 12h, rivers effectively infinite

### Crafting Philosophy Reinforced
- **NO success chances in crafting** - User feedback: "not fun or realistic"
- Crafting is skill expression (recipe knowledge), RNG belongs in tool usage
- Fire-making skill check happens in StartFire action, NOT in recipe

### Item vs Custom Class
- Fire tools use Item.NumUses instead of custom FireMakingTool class
- Skill parameters encoded by item name in StartFire action
- Simpler, avoids unnecessary abstraction

---

## Next Steps

### Immediate (Ready to Execute)
1. **Playtest harvestable features**:
   - Verify features spawn correctly
   - Test harvest action integration
   - Confirm respawn mechanics work
   - Check LookAround display

2. **Validation playtests** (game-balance-implementation/):
   - Run 5 full playtests
   - Track survival rate, death causes, time-to-fire
   - Verify 70-80% survival target met

### Optional Enhancements
1. Add "Transplant Berry Bush" crafting recipe (long-term base building)
2. Update documentation:
   - fire-management-system.md (tool changes)
   - crafting-system.md (no success chances philosophy)
   - skill-check-system.md (fire-making skill checks)
3. Consider reducing ForageFeature water abundance (migration to harvestables)

### Cleanup
1. Move game-balance-implementation/ to dev/complete/ after validation
2. Create harvestable-features/ folder in dev/complete/ with implementation notes

---

## Important Context for Next Session

### File Locations
- Harvestable system: Environments/LocationFeatures.cs/HarvestableFeature.cs (150 lines)
- Harvest actions: ActionFactory.cs lines 476-542 (HarvestResources, HarvestFromFeature, InspectHarvestable)
- LookAround integration: ActionFactory.cs lines 1442-1448
- Water sources: LocationFactory.cs (MakeForest ~100, MakePlain ~297, MakeRiverbank ~229)
- New items: ItemFactory.cs (MakePineSap), ItemProperty.cs (Adhesive, Waterproofing)

### Testing Commands
```bash
dotnet build                    # Build (currently succeeds)
TEST_MODE=1 dotnet run          # Interactive test
./play_game.sh                  # Background test mode
./play_game.sh send 1           # Look Around
./play_game.sh send 4           # Harvest Resources (menu position)
./play_game.sh tail             # Check output
pkill -f "TEST_MODE=1 dotnet"   # Kill test session
```

### Known Issues
- None blocking - build is clean, system is ready for testing

### Uncommitted Changes
All changes are in working directory, ready to commit:
- Game balance implementation (fire/resources/food)
- Harvestable feature system
- Bow Drill skill requirement fix

### What NOT to Change
- Existing ForageFeature behavior (complements harvestables, don't break it)
- EnvironmentFeature (unrelated to harvestables, don't confuse)
- Fire-making action skill check logic (tested and working)
