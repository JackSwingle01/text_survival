# Active Development - Current Status

**Date**: 2025-11-02
**Last Updated**: 17:15 UTC
**Status**: ✅ Build Successful - Ready for Testing

---

## Recently Completed

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
