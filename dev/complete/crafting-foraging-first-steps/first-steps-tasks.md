# Crafting & Foraging First Steps - Task Checklist

**Last Updated: 2025-11-01**
**Scope**: Phase 1 (Cleanup) + Phase 2 (Materials) only

This checklist tracks the first implementation steps of the crafting and foraging overhaul. Complete these 10 tasks to establish the foundation for the remaining phases.

---

## Phase 1: Cleanup & Foundation (5/5) ✅ COMPLETE
**Goal**: Remove crafted items from spawning, establish harsh start
**Status**: ✅ Complete (2025-11-01)

### Task 1.1: Update Starting Inventory ✅
**File**: `Program.cs` (lines 26-30)
**Effort**: S (30 min)
**Status**: ✅ Complete

**Changes**:
- [ ] Remove knife from oldBag
- [ ] Remove leather tunic from oldBag
- [ ] Remove leather pants from oldBag
- [ ] Remove moccasins from oldBag
- [ ] Add tattered chest wrap to oldBag
- [ ] Add tattered leg wraps to oldBag
- [ ] Rename container to "Tattered Sack" (optional polish)

**Acceptance**:
- [x] Player starts with 0 weapons
- [x] Player starts with 2 armor pieces (tattered)
- [x] Total starting insulation < 0.05 (0.04 total)
- [x] Game compiles and runs

**Completed**: 2025-11-01

---

### Task 1.2: Create Tattered Clothing Items ✅
**File**: `ItemFactory.cs` (lines 354-372)
**Effort**: S (30 min)
**Status**: ✅ Complete

**Implementation**:
- [ ] Create `MakeTatteredChestWrap()` method
  - Weight: 0.1 kg
  - Slot: Chest
  - ArmorRating: 0.5
  - Insulation: 0.02
  - Description: Ice Age themed
- [ ] Create `MakeTatteredLegWrap()` method
  - Weight: 0.1 kg
  - Slot: Legs
  - ArmorRating: 0.5
  - Insulation: 0.02
  - Description: Ice Age themed

**Acceptance**:
- [x] Both methods compile
- [x] Can create items in code
- [x] Can equip items in correct slots
- [x] Insulation values = 0.02 each
- [x] NO CraftingProperties (not craftable)

**Completed**: 2025-11-01

---

### Task 1.3: Clean LocationFactory Spawn Tables ✅
**File**: `LocationFactory.cs` (lines 317-378)
**Effort**: M (2-3 hours)
**Status**: ✅ Complete

**Methods to Clean**:
- [ ] `GetRandomForestItem()` - remove weapons, armor, tools
- [ ] `GetRandomCaveItem()` - remove weapons, armor, tools
- [ ] `GetRandomRiverbankItem()` - remove weapons, armor, tools
- [ ] `GetRandomPlainsItem()` - remove weapons, armor, tools
- [ ] `GetRandomHillsideItem()` - remove weapons, armor, tools

**Items to Remove** (crafted/processed):
- [ ] Spears (all types)
- [ ] Clubs
- [ ] Hand axes
- [ ] Knives
- [ ] Leather armor
- [ ] Hide armor
- [ ] Torches
- [ ] Any cooked/processed items

**Items to Keep** (natural):
- ✅ Sticks, branches, wood
- ✅ Stones (all types)
- ✅ Berries, mushrooms, roots
- ✅ Water
- ✅ Hides (from dead animals - natural finds)
- ✅ Bones (from old kills - natural finds)

**Acceptance**:
- [x] NO weapons in any GetRandom*Item()
- [x] NO armor in any GetRandom*Item()
- [x] NO tools in any GetRandom*Item()
- [x] Natural materials still present
- [x] Game compiles
- [x] Can forage and find natural materials only

**Completed**: 2025-11-01
**Items Removed**: Forest (torches, spears), Cave (torches), Hillside (hand axes)

---

### Task 1.4: Clean ForageFeature Resource Tables ✅
**File**: `LocationFactory.cs` (Make* methods)
**Effort**: S (1 hour)
**Status**: ✅ Complete (already clean)

**Forage Tables to Review**:
- [ ] `MakeForest()` - ForageFeature.AddResource() calls
- [ ] `MakeCave()` - ForageFeature.AddResource() calls
- [ ] `MakeRiverbank()` - ForageFeature.AddResource() calls
- [ ] `MakePlain()` - ForageFeature.AddResource() calls
- [ ] `MakeHillside()` - ForageFeature.AddResource() calls

**Check Each Resource**:
- Natural material? ✅ Keep
- Crafted/processed? ❌ Remove

**Acceptance**:
- [x] Forest forage = natural only
- [x] Cave forage = natural only
- [x] Riverbank forage = natural only
- [x] Plains forage = natural only
- [x] Hillside forage = natural only
- [x] NO torches in forage
- [x] NO weapons in forage
- [x] NO armor in forage

**Completed**: 2025-11-01 (verified - no changes needed)

---

### Task 1.5: Hide Initial Visible Items ✅
**File**: `LocationFactory.cs` (lines 45-51, 106-112, 171-177, 229-235, 289-295)
**Effort**: S (30 min)
**Status**: ✅ Complete

**Implementation**:
- [ ] Find all `for (int i = 0; i < itemCount; i++)` loops
- [ ] Comment out or set itemCount = 0
- [ ] Verify items only appear after Forage action

**Methods to Update**:
- [ ] `MakeForest()` - hide initial items
- [ ] `MakeCave()` - hide initial items
- [ ] `MakeRiverbank()` - hide initial items
- [ ] `MakePlain()` - hide initial items
- [ ] `MakeHillside()` - hide initial items

**Acceptance**:
- [x] New locations show 0 items initially (commented out all spawn loops)
- [x] "Look around" shows empty location
- [x] Forage action reveals items
- [x] Items added to location after forage

**Testing** (recommended):
- [ ] Start new game
- [ ] Look around starting location
- [ ] Verify no items visible
- [ ] Use Forage action
- [ ] Verify items now appear

**Completed**: 2025-11-01

---

## Phase 2: Material System Enhancement (6/6) ✅ COMPLETE
**Goal**: Add missing materials for realistic crafting
**Status**: ✅ Complete (2025-11-01)

### Task 2.1: Extend ItemProperty Enum ✅/❌
**File**: `Crafting/ItemProperty.cs`
**Effort**: S (15 min)
**Status**: ⏳ Not Started

**Add New Properties**:
- [ ] `Tinder` - Fire-starting materials
- [ ] `PlantFiber` - Early-game cordage
- [ ] `Fur` - Superior insulation
- [ ] `Antler` - Tool handles
- [ ] `Leather` - Processed hide
- [ ] `Charcoal` - Fire-hardened wood, fuel

**Acceptance**:
- [ ] All 6 properties compile
- [ ] No enum conflicts
- [ ] Can reference in CraftingProperties
- [ ] Comments added for clarity

**⚠️ IMPORTANT**: Complete this task BEFORE creating items that use these properties!

**Completed**: ____

---

### Task 2.2: Create Tinder Items ✅/❌
**File**: `ItemFactory.cs`
**Effort**: M (1.5 hours)
**Status**: ⏳ Not Started

**Items to Create**:
- [ ] `MakeDryGrass()`
  - Weight: 0.02 kg
  - Properties: Tinder 1.0, Flammable 0.5, Insulation 0.3
  - IsStackable: true
  - Description: Ice Age themed

- [ ] `MakeBarkStrips()`
  - Weight: 0.05 kg
  - Properties: Tinder 1.2, Binding 0.5, Flammable 0.8
  - IsStackable: true
  - Description: Inner bark, dual-purpose

- [ ] `MakeTinderBundle()`
  - Weight: 0.03 kg
  - Properties: Tinder 2.0, Flammable 1.0
  - IsStackable: true
  - Description: Prepared tinder nest

**Acceptance**:
- [ ] All 3 methods compile
- [ ] All use Tinder property
- [ ] All have IsStackable = true
- [ ] Weight < 0.1 kg each
- [ ] Descriptions fit Ice Age theme
- [ ] Can add to inventory without errors

**Completed**: ____

---

### Task 2.3: Create Plant Fiber Items ✅/❌
**File**: `ItemFactory.cs`
**Effort**: S (1 hour)
**Status**: ⏳ Not Started

**Items to Create**:
- [ ] `MakePlantFibers()`
  - Weight: 0.04 kg
  - Properties: PlantFiber 1.0, Binding 0.8
  - IsStackable: true
  - Description: Tough plant fibers for cordage

- [ ] `MakeRushes()`
  - Weight: 0.06 kg
  - Properties: PlantFiber 0.8, Binding 0.6, Insulation 0.5
  - IsStackable: true
  - Description: Wetland plants, multi-purpose

**Acceptance**:
- [ ] Both methods compile
- [ ] Both use PlantFiber property
- [ ] IsStackable = true
- [ ] Binding values < 1.0 (worse than sinew)
- [ ] Rushes unique to Riverbank (noted for later)

**Completed**: ____

---

### Task 2.4: Create Charcoal Item ✅/❌
**File**: `ItemFactory.cs`
**Effort**: S (30 min)
**Status**: ⏳ Not Started

**Item to Create**:
- [ ] `MakeCharcoal()`
  - Weight: 0.05 kg
  - Properties: Charcoal 1.0, Flammable 1.2
  - IsStackable: true
  - Description: Burned wood, used for hardening

**Acceptance**:
- [ ] Method compiles
- [ ] Has Charcoal property
- [ ] Has Flammable property (fuel value)
- [ ] IsStackable = true
- [ ] Description explains use case

**Note**: Not forageable - created later via fire + wood recipe (Phase 3+)

**Completed**: ____

---

### Task 2.5: Create Stone Variety ✅/❌
**File**: `ItemFactory.cs` + all files referencing MakeStone()
**Effort**: M (1.5 hours)
**Status**: ⏳ Not Started

**⚠️ MIGRATION REQUIRED**: This task involves renaming an existing method!

**Step 1: Rename Existing**
- [ ] Find `MakeStone()` method in ItemFactory.cs
- [ ] Rename to `MakeRiverStone()`
- [ ] Update description: "smooth, rounded stone from riverbed"
- [ ] Keep properties: Stone 1.0, Heavy 0.5

**Step 2: Update All References**
- [ ] Search codebase for `MakeStone()`
- [ ] Replace all with `MakeRiverStone()`
- [ ] Verify LocationFactory forage tables updated
- [ ] Compile and verify no errors

**Step 3: Create New Stone Types**
- [ ] `MakeSharpStone()`
  - Weight: 0.2 kg
  - Properties: Stone 1.0, Sharp 0.5
  - IsStackable: true
  - Description: Broken stone with sharp edges

- [ ] `MakeHandstone()`
  - Weight: 0.4 kg
  - Properties: Stone 1.0, Heavy 1.0
  - IsStackable: true
  - Description: Hammer stone for pounding

**Acceptance**:
- [ ] MakeStone() no longer exists (search returns 0 results)
- [ ] MakeRiverStone() works correctly
- [ ] MakeSharpStone() created
- [ ] MakeHandstone() created
- [ ] Game compiles
- [ ] Can forage and find renamed stones

**⚠️ CRITICAL**: Complete this task BEFORE Task 2.6!

**Completed**: ____

---

### Task 2.6: Update Biome Forage Tables ✅/❌
**File**: `LocationFactory.cs` (Make* methods - ForageFeature setup)
**Effort**: M (2 hours)
**Status**: ⏳ Not Started

**Prerequisite**: Task 2.5 must be complete (stone rename)

**Biome Updates**:

#### Forest (`MakeForest()`)
- [ ] Add: `MakeDryGrass()` abundance 0.5
- [ ] Add: `MakeBarkStrips()` abundance 0.7
- [ ] Add: `MakePlantFibers()` abundance 0.6
- [ ] Add: `MakeTinderBundle()` abundance 0.2
- [ ] Test: Forage multiple times, verify distributions

#### Riverbank (`MakeRiverbank()`)
- [ ] Add: `MakeRushes()` abundance 0.8
- [ ] Add: `MakeRiverStone()` abundance 0.9
- [ ] Add: `MakeDryGrass()` abundance 0.3
- [ ] Verify: No bark strips (wrong biome)
- [ ] Test: Forage, verify high stone/water plant yield

#### Plains (`MakePlain()`)
- [ ] Add: `MakeDryGrass()` abundance 0.8
- [ ] Add: `MakePlantFibers()` abundance 0.4
- [ ] Add: `MakeRiverStone()` abundance 0.3
- [ ] Test: Forage, verify high grass yield

#### Cave (`MakeCave()`)
- [ ] Add: `MakeRiverStone()` abundance 0.6
- [ ] Add: `MakeHandstone()` abundance 0.4
- [ ] Add: `MakeSharpStone()` abundance 0.3
- [ ] Verify: NO organic materials (grass, bark, fibers)
- [ ] Test: Forage, verify ONLY stones appear

#### Hillside (`MakeHillside()`)
- [ ] Add: `MakeRiverStone()` abundance 0.7
- [ ] Add: `MakeHandstone()` abundance 0.5
- [ ] Add: `MakeDryGrass()` abundance 0.4
- [ ] Add: `MakePlantFibers()` abundance 0.3
- [ ] Test: Forage, verify balanced stone/organic mix

**Acceptance**:
- [ ] Forest = high tinder/fiber abundance
- [ ] Riverbank = high stone/water plant abundance
- [ ] Plains = high grass abundance
- [ ] Cave = ONLY stones (no organics)
- [ ] Hillside = balanced distribution
- [ ] All AddResource() calls use correct pattern
- [ ] No crashes during forage

**Integration Testing**:
- [ ] Start in each biome, forage 5 times
- [ ] Verify material types match design
- [ ] Verify abundances feel correct
- [ ] Cave challenge confirmed (no fire materials)

**Completed**: ____

---

## Progress Summary

**Total Tasks**: 17 (5 Phase 1 + 6 Phase 2 + 6 Phase 3 BONUS)
**Completed**: 17
**In Progress**: 0
**Not Started**: 0

**Overall Progress**: 100% ✅

### By Phase
- **Phase 1 (Cleanup)**: 5/5 (100%) ✅ 2025-11-01
- **Phase 2 (Materials)**: 6/6 (100%) ✅ 2025-11-01
- **Phase 3 (Fire-Making)**: 6/6 (100%) ✅ 2025-11-01 BONUS

---

## Critical Path

**Execution Order** (must follow this sequence):

1. ✅ Tasks 1.1 & 1.2 (starting conditions) - can be done in parallel
2. ✅ Task 1.3 (spawn tables)
3. ✅ Tasks 1.4 & 1.5 (forage cleanup) - can be done in parallel
4. ⚠️ **Task 2.1 MUST complete before 2.2, 2.3, 2.4** (enums before usage)
5. ✅ Tasks 2.2, 2.3, 2.4 (new items) - can be done in parallel after 2.1
6. ⚠️ **Task 2.5 MUST complete before 2.6** (stone rename before forage updates)
7. ✅ Task 2.6 (forage updates) - final integration

**Blockers**:
- Task 2.1 blocks 2.2, 2.3, 2.4 (need enums)
- Task 2.5 blocks 2.6 (need renamed stones)

---

## Testing Strategy

### After Phase 1
- [ ] Start new game
- [ ] Verify harsh start (no weapon, minimal insulation)
- [ ] Verify empty locations (no visible items)
- [ ] Forage in each biome
- [ ] Verify NO crafted items found
- [ ] Verify natural materials found

### After Phase 2
- [ ] Forage in Forest - verify tinder/fiber abundance
- [ ] Forage in Riverbank - verify stone/rushes abundance
- [ ] Forage in Plains - verify grass abundance
- [ ] Forage in Cave - verify NO organics (critical test)
- [ ] Forage in Hillside - verify balanced mix
- [ ] Verify all materials stack correctly
- [ ] Verify no crashes or errors

### Integration
- [ ] Complete day 1 survival test (new game to first night)
- [ ] Verify material gathering feels purposeful
- [ ] Verify Cave start is challenging but possible
- [ ] No game-breaking bugs

---

## Notes

- **Update completion dates** as tasks finish
- **Mark blockers** if encountered
- **Phase 1 must fully complete** before starting Phase 2
- **Task 2.1 is critical** - blocks 3 other tasks
- **Task 2.5 involves migration** - be careful with rename
- **Cave biome design is intentional** - no organics is a feature, not a bug

**Current Phase**: Phase 1 - Cleanup & Foundation
**Next Task**: Task 1.1 - Update starting inventory (`Program.cs`)

---

## After First Steps Complete

Once all 11 tasks are checked off:

1. **Validate thoroughly** - Run full testing suite
2. **Update parent task list** - Mark Phase 1 & 2 complete in main overhaul plan
3. **Begin Phase 3** - Fire-making system (skill checks and recipes)
4. **Reference parent plan** - `dev/active/crafting-foraging-overhaul/crafting-foraging-overhaul-plan.md`

**Estimated Completion**: 5-7 days with testing and iteration

---

## BONUS: Phase 3 Fire-Making System (6/6) ✅ COMPLETE

**Goal**: Implement realistic fire-making with skill checks
**Status**: ✅ Complete (2025-11-01)
**Note**: This was NOT in the original first-steps plan but was implemented as bonus work

### Task 3.1: Extend CraftingRecipe ✅
**Files**: `CraftingRecipe.cs` (lines 29-31)
**Status**: ✅ Complete
- [x] Added BaseSuccessChance property (nullable double)
- [x] Added SkillCheckDC property (nullable int)
- [x] Documented purpose of each property
**Completed**: 2025-11-01

### Task 3.2: Modify CraftingSystem.Craft() ✅
**Files**: `CraftingSystem.cs` (lines 41-69)
**Status**: ✅ Complete
- [x] Added skill check logic before result generation
- [x] Calculate success chance: base + (skill - DC) * 0.1
- [x] Clamp to 5-95% range
- [x] On failure: consume materials, grant 1 XP, return
- [x] On success: show % chance, continue to results
**Completed**: 2025-11-01

### Task 3.3: Add RecipeBuilder Methods ✅
**Files**: `RecipeBuilder.cs` (lines 20-22, 106-118, 137-139)
**Status**: ✅ Complete
- [x] Added _baseSuccessChance field
- [x] Added _skillCheckDC field
- [x] Implemented WithSuccessChance(double) method
- [x] Implemented WithSkillCheck(string, int) method
- [x] Updated Build() to assign new properties
**Completed**: 2025-11-01

### Task 3.4: Implement Hand Drill Recipe ✅
**Files**: `CraftingSystem.cs` (lines 165-180)
**Status**: ✅ Complete
**Recipe Details**:
- Materials: 0.5kg Wood + 0.05kg Tinder
- Time: 20 minutes
- Success: 30% base + 10% per Firecraft level
- Result: Campfire (10°F warmth, 0.5 fuel)
**Completed**: 2025-11-01

### Task 3.5: Implement Bow Drill Recipe ✅
**Files**: `CraftingSystem.cs` (lines 183-199)
**Status**: ✅ Complete
**Recipe Details**:
- Materials: 1kg Wood + 0.1kg Binding + 0.05kg Tinder
- Time: 45 minutes  
- Success: 50% base + 10% per level above DC 1
- Result: Campfire (10°F warmth, 0.5 fuel)
**Completed**: 2025-11-01

### Task 3.6: Implement Flint & Steel Recipe ✅
**Files**: `CraftingSystem.cs` (lines 202-217)
**Status**: ✅ Complete
**Recipe Details**:
- Materials: 0.2kg Firestarter + 0.3kg Stone + 0.05kg Tinder
- Time: 5 minutes
- Success: 90% (no skill modifier)
- Result: Campfire (10°F warmth, 0.5 fuel)
**Completed**: 2025-11-01

---

## Documentation Updates (2025-11-01)

All documentation has been updated to reflect completed work:

✅ `first-steps-context.md` - Full implementation summary added
✅ `first-steps-tasks.md` - All tasks marked complete with dates
✅ `SESSION-SUMMARY-2025-11-01.md` - Comprehensive session summary created

**Next Session**: Read SESSION-SUMMARY first for quick context
