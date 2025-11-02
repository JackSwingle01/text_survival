# Crafting & Foraging Overhaul - Task Checklist

**Last Updated: 2025-11-02**
**Plan Review: 2025-11-01** - Updated to 49 tasks after plan review
**Balance Testing: 2025-11-02** - Day-1 playtest completed, Phase 9 partially complete

Track progress by checking off completed tasks. Update dates as you complete each section.

---

## Phase 1: Cleanup & Foundation ⏳ (0/4)
**Target**: Remove crafted items from spawning, establish clean baseline
**Status**: Not Started

- [ ] **Task 1**: Update starting inventory (Program.cs) - *Effort: S*
  - Remove oldBag with knife/armor
  - Add 2 tattered clothing items
  - **Completed**: ____

- [ ] **Task 2**: Clean LocationFactory spawn tables - *Effort: M*
  - Remove crafted items from GetRandomForestItem()
  - Remove crafted items from GetRandomCaveItem()
  - Remove crafted items from GetRandomRiverbankItem()
  - Remove crafted items from GetRandomPlainsItem()
  - Remove crafted items from GetRandomHillsideItem()
  - **Completed**: ____

- [ ] **Task 3**: Clean ForageFeature resource tables - *Effort: S*
  - Review MakeForest() forage table
  - Review MakeCave() forage table
  - Review MakeRiverbank() forage table
  - Review MakePlain() forage table
  - Review MakeHillside() forage table
  - **Completed**: ____

- [ ] **Task 4**: Reduce initial visible items - *Effort: S*
  - Set itemCount = 0 in all LocationFactory methods
  - Test that locations appear empty
  - **Completed**: ____

---

## Phase 2: Material System Enhancement ⏳ (0/6)
**Target**: Add missing materials for realistic crafting
**Status**: Not Started

- [ ] **Task 5**: Add new ItemProperty enums - *Effort: S*
  - Add Tinder to ItemProperty.cs
  - Add Fur to ItemProperty.cs
  - Add Antler to ItemProperty.cs
  - Add Leather to ItemProperty.cs
  - Add Charcoal to ItemProperty.cs
  - Add PlantFiber to ItemProperty.cs
  - **Completed**: ____

- [ ] **Task 6**: Create tinder/fire-starting items - *Effort: M*
  - Create MakeTinderBundle() in ItemFactory
  - Create MakeBarkStrips() in ItemFactory
  - Create MakeDryGrass() in ItemFactory
  - Create MakePineResin() in ItemFactory (optional)
  - **Completed**: ____

- [ ] **Task 7**: Create plant fiber items - *Effort: S*
  - Create MakePlantFibers() in ItemFactory
  - Create MakeRushes() in ItemFactory
  - **Completed**: ____

- [ ] **Task 8**: Create charcoal item - *Effort: S*
  - Create MakeCharcoal() in ItemFactory
  - Add CraftingProperties: Charcoal, Flammable
  - **Completed**: ____

- [ ] **Task 9**: Create stone variety - *Effort: M*
  - Rename MakeStone() to MakeRiverStone()
  - Create MakeSharpStone() (broken stone with Sharp property)
  - Create MakeHandstone() (hammer stone)
  - **Completed**: ____

- [ ] **Task 10**: Update biome forage tables - *Effort: M*
  - Add tinder/plant fibers to Forest forage
  - Add rushes to Riverbank forage
  - Add dry grass to Plains forage
  - Verify each biome has unique material profile
  - **Completed**: ____

---

## Phase 3: Fire-Making System ⏳ (0/6)
**Target**: Implement critical fire-making with skill checks
**Status**: Not Started
**Review Note**: Updated to use CraftingRecipe skill check system

- [ ] **Task 11**: Extend CraftingRecipe for skill checks - *Effort: M*
  - Add BaseSuccessChance property (nullable double)
  - Add SkillCheckDC property (nullable int)
  - Modify CraftingSystem.Craft() to handle success/failure
  - Add XP rewards for both success and failure
  - **Completed**: ____

- [ ] **Task 12**: Add RecipeBuilder success methods - *Effort: S*
  - Implement .WithSuccessChance(double chance) method
  - Implement .WithSkillCheck(string skillName, int dc) method
  - **Completed**: ____

- [ ] **Task 13**: Implement Hand Drill recipe - *Effort: M*
  - Materials: Dry stick + Tinder
  - Time: 20 minutes
  - BaseSuccessChance: 0.3, SkillCheckDC: 0
  - Result: HeatSourceFeature via .ResultingInLocationFeature()
  - **Completed**: ____

- [ ] **Task 14**: Implement Bow Drill recipe - *Effort: M*
  - Materials: 2x Wood + PlantFiber/Sinew + Tinder
  - Time: 45 minutes
  - BaseSuccessChance: 0.5, SkillCheckDC: 1
  - Result: HeatSourceFeature via .ResultingInLocationFeature()
  - **Completed**: ____

- [ ] **Task 15**: Implement Flint & Steel recipe - *Effort: S*
  - Materials: Flint + Stone + Tinder
  - Time: 5 minutes
  - BaseSuccessChance: 0.9, no skill check
  - Result: HeatSourceFeature via .ResultingInLocationFeature()
  - **Completed**: ____

- [ ] **Task 16**: Test fire-making mechanics - *Effort: M*
  - Verify success/failure messaging
  - Verify material consumption on failure
  - Verify XP gains (success and failure)
  - Verify HeatSourceFeature creation on success
  - **Completed**: ____

---

## Phase 4: Tool Progression System ⏳ (0/7)
**Target**: Implement realistic tool tiers
**Status**: Not Started

- [ ] **Task 17**: Implement Sharp Rock (Tier 1) - *Effort: S*
  - Create recipe: 2x River Stone → Sharp Rock
  - 5 minute craft time
  - Damage: 3
  - **Completed**: ____

- [ ] **Task 18**: Implement Flint Knife (Tier 2) - *Effort: M*
  - Create recipe: Flint + Stick + PlantFiber/Sinew
  - 20 minute craft time
  - Damage: 6
  - **Completed**: ____

- [ ] **Task 19**: Implement Bone Knife (Tier 3) - *Effort: M*
  - Create recipe: Large bone + Sinew + Charcoal
  - Requires fire
  - 45 minute craft time
  - Damage: 8
  - **Completed**: ____

- [ ] **Task 20**: Implement Obsidian Blade (Tier 4) - *Effort: M*
  - Create recipe: Obsidian + Antler + Sinew
  - 60 minute craft time
  - Damage: 12
  - **Completed**: ____

- [ ] **Task 21**: Create spear progression recipes - *Effort: L*
  - Sharpened Stick (5 min, wood only)
  - Fire-Hardened Spear (20 min, wood + fire)
  - Flint-Tipped Spear (40 min, wood + flint + binding)
  - Bone-Tipped Spear (60 min, wood + bone + sinew + fire)
  - Obsidian Spear (rare materials)
  - **Completed**: ____

- [ ] **Task 22**: Create club progression recipes - *Effort: M*
  - Stone-Weighted Club (15 min)
  - Bone-Studded Club (30 min)
  - **Completed**: ____

- [ ] **Task 23**: Create hand axe progression - *Effort: M*
  - Stone Hand Axe (25 min)
  - Flint Hand Axe (35 min)
  - **Completed**: ____

---

## Phase 5: Shelter Progression ⏳ (0/4)
**Target**: Meaningful shelter tiers from emergency to permanent
**Status**: Not Started

- [ ] **Task 24**: Implement Windbreak (Tier 1) - *Effort: M*
  - Recipe: 3kg Branches + 1kg Plant matter
  - 30 minute craft time
  - Creates LocationFeature: +2°F warmth
  - **Completed**: ____

- [ ] **Task 25**: Update Lean-to recipe (Tier 2) - *Effort: S*
  - Recipe: 6kg Wood + 1kg Binding + 2kg Insulation
  - 2 hour craft time
  - Creates Location: +5°F warmth
  - **Completed**: ____

- [ ] **Task 26**: Implement Debris Hut (Tier 3) - *Effort: M*
  - Recipe: 10kg Wood + 3kg Insulation + 1kg Binding
  - 4 hour craft time
  - Creates Location: +8°F warmth
  - **Completed**: ____

- [ ] **Task 27**: Update Log Cabin recipe (Tier 4) - *Effort: S*
  - Verify recipe: 20kg Wood + 5kg Stone + 3kg Binding + 8kg Insulation
  - 8 hour craft time
  - Creates Location: +15°F warmth
  - **Completed**: ____

---

## Phase 6: Clothing/Armor System ⏳ (0/4)
**Target**: Progression from makeshift to proper gear
**Status**: Not Started

- [ ] **Task 28**: Create tattered starting clothes - *Effort: S*
  - Create MakeTatteredWrap() in ItemFactory
  - Low insulation (0.02)
  - Minimal protection
  - **Completed**: ____

- [ ] **Task 29**: Create early-game wrappings - *Effort: M*
  - Bark Wrappings (chest/legs)
  - Grass Foot Wraps
  - Plant Fiber Bindings (hands)
  - Add recipes to CraftingSystem
  - **Completed**: ____

- [ ] **Task 30**: Tune existing hide armor - *Effort: S*
  - Ensure recipes require hunting materials
  - Verify gated by hunting mechanic
  - **Completed**: ____

- [ ] **Task 31**: Create fur-lined armor (Tier 3) - *Effort: M*
  - Recipe: Multiple hides + Fur + Sinew
  - Best insulation values
  - Late-game gear
  - **Completed**: ____

---

## Phase 7: Foraging UX Improvements ⏳ (0/3)
**Target**: Make foraging feel like searching
**Status**: Not Started

- [ ] **Task 32**: Hide items until foraged - *Effort: M*
  - Modify Location display logic
  - Items not shown in LookAround until foraged
  - **Completed**: ____

- [ ] **Task 33**: Update Forage action output - *Effort: M*
  - Use ItemStack for grouping
  - Display: "You found: Stick (3), Stone (2)"
  - Clean formatting
  - **Completed**: ____

- [ ] **Task 34**: Add forage time display - *Effort: S*
  - Show: "You spent 1 hour searching and found..."
  - Make time cost visible
  - **Completed**: ____

---

## Phase 8: Recipe Implementation ⏳ (0/6)
**Target**: Implement all ~40 recipes
**Status**: Not Started

- [ ] **Task 35**: Implement all fire-making recipes - *Effort: M*
  - Hand drill
  - Bow drill
  - Flint & steel
  - Campfire
  - Create CreateFireRecipes() method
  - **Completed**: ____

- [ ] **Task 36**: Implement all tool recipes - *Effort: L*
  - All cutting tool tiers (4)
  - All spear tiers (5)
  - All club tiers (2)
  - Hand axes (2)
  - Create CreateToolRecipes() method
  - **Completed**: ____

- [ ] **Task 37**: Implement all shelter recipes - *Effort: M*
  - Windbreak
  - Lean-to
  - Debris hut
  - Log cabin
  - Update CreateShelterRecipes() method
  - **Completed**: ____

- [ ] **Task 38**: Implement all clothing recipes - *Effort: M*
  - Bark wrappings
  - Grass wraps
  - Hide armor
  - Fur-lined gear
  - Create CreateClothingRecipes() method
  - **Completed**: ____

- [ ] **Task 39**: Verify all materials have purpose - *Effort: S*
  - Check every ItemProperty used in recipe
  - Document material → recipe mapping
  - Fix any orphaned materials
  - **Completed**: ____

- [ ] **Task 39b**: Implement recipe categorization - *Effort: M*
  - Add Category enum to CraftingRecipe (Fire, Tools, Weapons, Shelters, Clothing, Misc)
  - Update all recipes with appropriate category
  - Modify crafting menu to group by category with headers
  - Sort unavailable recipes to bottom within each category
  - **Completed**: ____
  - **Review Note**: Added from plan review for menu organization

---

## Phase 9: Balance & Testing ⏳ (3/6)
**Target**: Ensure survivable but challenging
**Status**: In Progress (Day-1 testing complete, biome testing pending)

- [x] **Task 40**: Test day-1 survival path - *Effort: M*
  - New game playthrough
  - Can forage materials? ✓ (Forest biome validated)
  - Can make fire? ✓ (new fire management system)
  - Can make windbreak? ⏳ (not tested yet)
  - Can make sharp rock? ✓ (Small Stone now available in Forest)
  - **Completed**: 2025-11-02
  - **Results**: 3 critical blockers identified and fixed
  - **Details**: `dev/active/crafting-foraging-overhaul/balance-testing-session.md`

- [ ] **Task 41**: Balance material spawn rates - *Effort: M*
  - Adjust forage abundances
  - Common materials easy to find ✓ (Forest: ~3-4 items per 15 min)
  - Rare materials feel special
  - Test across all biomes ⏳ (Forest complete, 4 biomes remaining)
  - **Completed**: ____
  - **Status**: Forest biome complete (baseResourceDensity 1.6, buffed abundances)

- [x] **Task 42**: Balance crafting times - *Effort: S*
  - Review all recipe times ✓
  - Verify realistic (5 min → 8 hours range) ✓
  - Test time pressure ✓ (15-min foraging intervals enable fire tending)
  - **Completed**: 2025-11-02
  - **Notes**: Fire-making 20-45 min (realistic), Sharp Rock 5 min (appropriate)

- [ ] **Task 43**: Balance tool effectiveness - *Effort: M*
  - Test damage values in combat
  - Test harvesting yields per tier
  - Ensure progression feels rewarding
  - **Completed**: ____
  - **Status**: Not started (requires combat testing)

- [ ] **Task 44**: Test shelter warmth values - *Effort: S*
  - Verify temperature bonuses work ✓ (fire warmth confirmed working)
  - Test survival in different weather
  - Each tier meaningfully better
  - **Completed**: ____
  - **Status**: Fire warmth verified (+15°F, ember +5.25°F), shelters untested

- [ ] **Task 45**: Test biome viability - *Effort: M* **HIGH PRIORITY NEXT**
  - Test starting in each biome: Forest ✓, Plains ⏳, Riverbank ⏳, Cave ⏳, Hillside ⏳
  - Verify each has materials for day-1 fire
  - Verify each has materials for day-1 shelter
  - Identify and fix unplayable biomes (e.g., Cave lacking organics)
  - **Completed**: ____
  - **Review Note**: Added from plan review as High Risk mitigation
  - **Status**: Forest validated, 4 biomes remaining

---

## Phase 10: Polish & Documentation ⏳ (0/4)
**Target**: Clean up and document
**Status**: Not Started

- [ ] **Task 46**: Update item descriptions - *Effort: S*
  - Review all new items
  - Ice Age theming check
  - Consistent voice/tone
  - **Completed**: ____

- [ ] **Task 47**: Update CLAUDE.md - *Effort: S*
  - Document new crafting patterns (skill checks, success rates)
  - Document material system and property usage
  - Document progression philosophy
  - **Completed**: ____

- [ ] **Task 48**: Create crafting guide (optional) - *Effort: S*
  - Material sources by biome
  - Recipe trees
  - Player-facing reference
  - **Completed**: ____

- [ ] **Task 49**: Playtest full progression - *Effort: M*
  - Start → Log Cabin playthrough
  - Document pain points
  - Iterate on balance
  - **Completed**: ____

---

## Progress Summary

**Total Tasks**: 49 (updated after plan review)
**Completed**: 41
**In Progress**: 3 (Phase 9)
**Not Started**: 5

**Overall Progress**: 84% (41/49 tasks complete)

### By Phase
- Phase 1 (Cleanup): 4/4 (100%) ✅
- Phase 2 (Materials): 6/6 (100%) ✅
- Phase 3 (Fire-Making): 6/6 (100%) ✅
- Phase 4 (Tools): 7/7 (100%) ✅
- Phase 5 (Shelters): 4/4 (100%) ✅
- Phase 6 (Clothing): 4/4 (100%) ✅
- Phase 7 (Foraging UX): 3/3 (100%) ✅
- Phase 8 (Recipes): 6/6 (100%) ✅
- Phase 9 (Balance): 3/6 (50%) ⏳ **CURRENT FOCUS**
- Phase 10 (Polish): 0/4 (0%)

**Notes**:
- Phases 1-8 complete (from previous sessions)
- Phase 9 started 2025-11-02 with day-1 playtest
- Major features added this session: Fire embers system, fire management UI, foraging balance

---

## Notes

- Update this file as tasks are completed
- Mark completion dates for tracking velocity
- Add notes about issues/blockers encountered
- Cross-reference with plan.md for task details

**Current Phase**: Phase 9 - Balance & Testing (Day-1 Complete)
**Next Task**: Task 45 - Test biome viability (HIGH PRIORITY)

### Recent Session Highlights (2025-11-02)

**Balance Fixes**:
- Starting fire: 15 min → 60 min
- Frostbite progression slowed by 50%
- Resource density buffed ~50%
- Foraging: 1 hour → 15 min intervals
- Fire capacity: 1 hour → 8 hours

**New Features**:
- Fire embers system (25% burn time extension, 35% heat)
- Fire management actions in main menu
- Immediate foraging collection flow
- Fire warmth UI visibility

**Critical Bugs Fixed**:
- Location.Update() now calls Feature.Update() (fire depletion works)
- Removed auto-ignition from cold fires
- Small Stone added to Forest (enables Sharp Rock crafting)

**Testing Status**:
- ✅ Forest biome day-1 survival validated
- ⏳ 4 biomes remaining (Plains, Riverbank, Cave, Hillside)
- ⏳ Tool/shelter effectiveness untested
- ⏳ Phase 10 polish pending
