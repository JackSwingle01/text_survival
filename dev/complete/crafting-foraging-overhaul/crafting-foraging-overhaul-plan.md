# Crafting & Foraging System Overhaul - Strategic Plan

**Last Updated: 2025-11-01**
**Plan Review: 2025-11-01** - Addressed critical issues from plan-reviewer agent

## Plan Review Updates

This plan was reviewed by the plan-reviewer agent. The following critical issues were identified and addressed:

### Critical Issues Resolved

1. **Fire-Making Success/Failure Mechanism** ✅
   - **Issue**: Current CraftingSystem always succeeds if ingredients are available
   - **Solution**: Extended CraftingRecipe with optional BaseSuccessChance and SkillCheckDC properties
   - **Implementation**: See "Fire-Making Technical Design" section below Phase 2

2. **Fire Result Type Clarification** ✅
   - **Issue**: Ambiguity whether fire produces Item or LocationFeature
   - **Solution**: All fire-making recipes use `.ResultingInLocationFeature()` to create HeatSourceFeature
   - **Implementation**: Updated Phase 3 tasks with explicit result types

3. **Obsidian Durability System** ✅
   - **Issue**: Plan mentioned "fragile" obsidian but no durability system exists
   - **Solution**: Removed fragility reference, obsidian is simply rare/powerful for MVP
   - **Future**: Durability system deferred to post-MVP enhancement

### Additional Improvements

- Added **Missing Considerations** section covering harvesting yields, recipe organization, skill progression
- Updated **Timeline** to 4-5 weeks (from 3-4) for adequate testing
- Added **Biome Viability** checks to Phase 9 testing
- Added **Recipe Organization** task to Phase 8
- Expanded **Risk Assessment** with save compatibility and action menu complexity

### Review Verdict

**Plan approved for implementation** with critical issues resolved. The phased approach provides good risk mitigation through incremental delivery.

---

## Executive Summary

This plan outlines a comprehensive overhaul of the crafting and foraging systems to create a realistic, progression-based survival experience set in the Ice Age. The project addresses three critical issues:

1. **Unrealistic item distribution**: Crafted items spawning in the world
2. **Broken progression**: Players finding better gear than they can craft
3. **Lack of balance**: Foraging vs crafting reward structure unclear

**Core Design Philosophy**: Prioritize realism over game-y progression. Items should reflect what's plausible in an Ice Age setting, with natural material progressions (stone → flint → bone → obsidian) rather than arbitrary stat tiers.

**Timeline Estimate**: 4-5 weeks (extended for thorough testing)
**Effort Level**: XL
**Risk Level**: Medium (touches many interconnected systems)

---

## Current State Analysis

### Existing Systems

**Foraging System** (`ForageFeature.cs`):
- ✅ Diminishing returns implemented
- ✅ Biome-specific resource tables
- ❌ Currently spawns crafted items (torches, spears, hand axes)
- ❌ No initial hiding of items (everything visible immediately)
- ❌ No ItemStack display in forage results

**Crafting System** (`CraftingSystem.cs`):
- ✅ Property-based recipe system with RecipeBuilder
- ✅ Skill requirements supported
- ✅ Time costs implemented
- ✅ Three result types (Item, LocationFeature, Shelter)
- ❌ Only placeholder recipes (stone knife, spear, campfire, lean-to)
- ❌ No fire-making recipes
- ❌ No tool progression tiers

**Item System** (`ItemFactory.cs`):
- ✅ 40+ items defined with Ice Age theming
- ✅ CraftingProperties assigned
- ❌ Many items shouldn't be findable (weapons, armor, tools)
- ❌ Missing key material types (tinder, plant fibers, bark)

**Starting Conditions** (`Program.cs`):
- ❌ Player starts with high-quality gear (knife, leather tunic, pants, moccasins)
- ❌ Breaks intended difficulty curve

### Critical Gaps

1. **No fire-making system**: Fire is critical for survival but has no crafting recipes
2. **Material gaps**: Missing tinder, plant fibers, bark, charcoal
3. **No tool progression**: Jump from nothing to flint knife
4. **Shelter imbalance**: Only 2 shelter types, huge gap between them
5. **Spawn contamination**: Crafted items appearing in world spawn tables

---

## Proposed Future State

### Design Pillars

1. **Realism-First**: Every item/recipe reflects plausible Ice Age technology
2. **Natural Progression**: Materials dictate progression (stone → flint → bone → obsidian)
3. **Meaningful Tiers**: Each tier unlocks new capabilities, not just stat increases
4. **Foraging for Materials Only**: World spawns only natural, unprocessed materials
5. **Critical Path Clear**: Fire (day 1) → Shelter (days 1-3) → Tools → Hunting

### Target Player Experience

**Day 1**:
- Wake up nearly naked
- Forage for sticks, stones, tinder
- Craft fire (challenging, may fail)
- Build basic windbreak
- Craft sharp rock for basic cutting

**Days 2-3**:
- Improve shelter to lean-to
- Craft better fire-starting tools
- Craft flint knife for hunting
- Begin hunting for hide/bone/sinew

**Week 1+**:
- Build permanent shelter (debris hut/cabin)
- Craft advanced tools (bone, obsidian)
- Create proper clothing
- Establish sustainable survival loop

---

## Implementation Phases

### Phase 1: Cleanup & Foundation (Effort: M)
**Goal**: Remove crafted items from world spawning, establish clean baseline

#### Tasks
1. **Update starting inventory** (`Program.cs`)
   - Remove oldBag with knife/armor
   - Add 2 tattered clothing items (low warmth/protection)
   - **AC**: Player starts vulnerable but not naked
   - **Effort**: S

2. **Clean LocationFactory spawn tables**
   - Remove all crafted items from `GetRandom*Item()` methods
   - Keep only: natural materials, food, water
   - Remove: spears, torches, hand axes, all armor
   - **AC**: No crafted items spawn in world
   - **Effort**: M

3. **Clean ForageFeature resource tables**
   - Review all biome forage tables
   - Remove crafted items, keep natural materials only
   - **AC**: Foraging yields only raw materials
   - **Effort**: S

4. **Reduce initial visible items**
   - Set `itemCount = 0` in LocationFactory methods initially
   - Items only revealed through foraging
   - **AC**: New locations appear empty until searched
   - **Effort**: S

---

### Phase 2: Material System Enhancement (Effort: L)
**Goal**: Add missing materials needed for realistic crafting

#### Tasks

5. **Add new ItemProperty enums** (`ItemProperty.cs`)
   - Add: `Tinder`, `Fur`, `Antler`, `Leather`, `Charcoal`, `PlantFiber`
   - **AC**: All new properties compile successfully
   - **Effort**: S

6. **Create tinder/fire-starting items** (`ItemFactory.cs`)
   - `MakeTinderBundle()` - dry grass, small twigs (Tinder, Flammable)
   - `MakeBarkStrips()` - inner bark (Binding, Tinder)
   - `MakeDryGrass()` - (Tinder, Flammable)
   - `MakePineResin()` - (Flammable, Tinder, sticky)
   - **AC**: All items craftable and findable
   - **Effort**: M

7. **Create plant fiber items**
   - `MakePlantFibers()` - cordage material (PlantFiber, Binding)
   - `MakeRushes()` - wetland plants (PlantFiber, Binding, Insulation)
   - **AC**: Provide alternative to sinew for early game
   - **Effort**: S

8. **Create charcoal item**
   - `MakeCharcoal()` - from burned wood (Charcoal, Flammable)
   - Used for fire-hardening wood
   - **AC**: Craftable from fire + wood
   - **Effort**: S

9. **Create stone variety**
   - Rename `MakeStone()` to `MakeRiverStone()`
   - `MakeSharpStone()` - broken stone (Sharp, Stone)
   - `MakeHandstone()` - hammer stone (Stone, Heavy)
   - **AC**: Different stones for different purposes
   - **Effort**: M

10. **Update biome forage tables**
    - Add new materials to appropriate biomes
    - Forest: high plant fiber, bark, tinder
    - Plains: moderate tinder, low wood
    - Riverbank: rushes, clay, smooth stones
    - Cave: limited organic, high stone
    - **AC**: Each biome has unique material profile
    - **Effort**: M

---

## Fire-Making Technical Design

**Challenge**: The existing `CraftingSystem` assumes all recipes succeed if ingredients are available. Fire-making requires skill-based success/failure.

**Solution**: Extend the `CraftingRecipe` class with optional success chance properties:

```csharp
public class CraftingRecipe
{
    // Existing properties...

    // New properties for fire-making
    public double? BaseSuccessChance { get; set; }  // null = always succeeds
    public int? SkillCheckDC { get; set; }          // null = no skill check
}
```

**Success Calculation**:
```csharp
double successChance = recipe.BaseSuccessChance ?? 1.0;
if (recipe.SkillCheckDC != null) {
    var skill = player.Skills.Get("Firecraft");
    successChance += (skill.Level - recipe.SkillCheckDC) * 0.1;
}
successChance = Math.Clamp(successChance, 0.05, 0.95); // 5-95% range
bool success = Utils.DetermineSuccess(successChance);
```

**On Failure**:
- Materials consumed (represents realistic failed attempts)
- Player gains small Firecraft XP (1 point - learning from failure)
- Failure message displayed
- Time still passes

**On Success**:
- HeatSourceFeature created at location
- Player gains Firecraft XP (skill.Level + 3 points)
- Success message displayed

**Fire Recipe Progression**:
1. Hand Drill: BaseSuccessChance = 0.3, SkillCheckDC = 0 (30% base, +10% per Firecraft level)
2. Bow Drill: BaseSuccessChance = 0.5, SkillCheckDC = 1 (50% base at skill 0, 60% at skill 1)
3. Flint & Steel: BaseSuccessChance = 0.9, SkillCheckDC = null (90% always, no skill requirement)

**Integration Points**:
- Modify `CraftingSystem.Craft()` to check success before creating result
- All fire recipes use `.ResultingInLocationFeature(() => new HeatSourceFeature(...))`
- RecipeBuilder needs `.WithSuccessChance(double)` and `.WithSkillCheck(string skill, int dc)` methods

---

### Phase 3: Fire-Making System (Effort: L)
**Goal**: Implement critical fire-making recipes with skill-based success

#### Tasks

11. **Extend CraftingRecipe for skill checks**
    - Add `BaseSuccessChance` and `SkillCheckDC` properties
    - Modify `CraftingSystem.Craft()` to handle success/failure
    - Add XP rewards for success and failure
    - **AC**: Skill check system functional
    - **Effort**: M

12. **Add RecipeBuilder success methods**
    - `.WithSuccessChance(double chance)` method
    - `.WithSkillCheck(string skillName, int dc)` method
    - **AC**: Builder supports fire recipes
    - **Effort**: S

13. **Implement Hand Drill recipe** (`CraftingSystem.cs`)
    - Materials: Dry stick (Wood) + Tinder bundle
    - Time: 20 minutes
    - BaseSuccessChance: 0.3, SkillCheckDC: 0
    - Result: `.ResultingInLocationFeature(() => new HeatSourceFeature("Campfire", 10°F, 8 hours))`
    - **AC**: Can attempt fire day 1, 30% base success
    - **Effort**: M

14. **Implement Bow Drill recipe**
    - Materials: 2x Wood + PlantFiber/Sinew + Tinder
    - Time: 45 minutes
    - BaseSuccessChance: 0.5, SkillCheckDC: 1
    - Result: `.ResultingInLocationFeature(() => new HeatSourceFeature("Campfire", 10°F, 8 hours))`
    - **AC**: 50% base success, 60% at Firecraft 1
    - **Effort**: M

15. **Implement Flint & Steel recipe**
    - Materials: Flint + Stone + Tinder
    - Time: 5 minutes
    - BaseSuccessChance: 0.9, no skill check
    - Result: `.ResultingInLocationFeature(() => new HeatSourceFeature("Campfire", 10°F, 8 hours))`
    - **AC**: Reliable 90% fire once you have flint
    - **Effort**: S

16. **Test fire-making mechanics**
    - Verify success/failure messaging
    - Verify material consumption on failure
    - Verify XP gains
    - Verify HeatSourceFeature creation on success
    - **AC**: Fire-making works as designed
    - **Effort**: M

---

### Phase 4: Tool Progression System (Effort: XL)
**Goal**: Implement realistic tool tiers from crude to advanced

#### Tasks

17. **Implement Sharp Rock (Tier 1 cutting)**
    - Recipe: 2x River Stone (smash together)
    - Time: 5 minutes
    - Damage: 3, basic harvesting capability
    - **AC**: Immediate day-1 cutting tool
    - **Effort**: S

18. **Implement Flint Knife (Tier 2 cutting)**
    - Recipe: Flint + Stick + PlantFiber/Sinew
    - Time: 20 minutes
    - Damage: 6, enables proper skinning
    - **AC**: Mid-tier tool requiring exploration
    - **Effort**: M

19. **Implement Bone Knife (Tier 3 cutting)**
    - Recipe: Large bone + Sinew + Charcoal (fire-hardening)
    - Time: 45 minutes
    - Requires fire and hunting
    - Damage: 8, best yield multipliers
    - **AC**: Late-tier requiring multiple systems
    - **Effort**: M

20. **Implement Obsidian Blade (Tier 4 cutting)**
    - Recipe: Obsidian shard + Antler handle + Sinew
    - Time: 60 minutes
    - Rare materials required (obsidian uncommon)
    - Damage: 12, best performance
    - **AC**: Rare/optional endgame tool
    - **Effort**: M
    - **Note**: Durability mechanics deferred to future update

21. **Create spear progression recipes**
    - Sharpened Stick (5 min, wood only)
    - Fire-Hardened Spear (20 min, wood + fire)
    - Flint-Tipped Spear (40 min, wood + flint + binding)
    - Bone-Tipped Spear (60 min, wood + bone + sinew + fire)
    - Obsidian Spear (rare, highest damage)
    - **AC**: 5-tier weapon progression
    - **Effort**: L

22. **Create club progression recipes**
    - Heavy Stick (found naturally, no recipe)
    - Stone-Weighted Club (15 min, stick + stone + binding)
    - Bone-Studded Club (30 min, stick + bone + sinew)
    - **AC**: Alternative weapon path
    - **Effort**: M

23. **Create hand axe progression**
    - Stone Hand Axe (25 min, stone + stick + binding)
    - Flint Hand Axe (35 min, flint + stick + sinew)
    - **AC**: Utility tool for wood harvesting
    - **Effort**: M

---

### Phase 5: Shelter Progression (Effort: L)
**Goal**: Create meaningful shelter tiers from emergency to permanent

#### Tasks

24. **Implement Windbreak (Tier 1 shelter)**
    - Recipe: 3kg Branches + 1kg Plant matter/Grass
    - Time: 30 minutes
    - Result: LocationFeature, +2°F warmth, minimal weather protection
    - **AC**: Emergency day-1 shelter
    - **Effort**: M

25. **Update Lean-to recipe (Tier 2)**
    - Recipe: 6kg Wood + 1kg Binding + 2kg Insulation
    - Time: 2 hours
    - Result: Location with ShelterFeature, +5°F, moderate weather block
    - **AC**: Improved from current version
    - **Effort**: S

26. **Implement Debris Hut (Tier 3)**
    - Recipe: 10kg Wood + 3kg Insulation + 1kg Binding
    - Time: 4 hours
    - Result: Location with ShelterFeature, +8°F, good weather protection, dry
    - **AC**: Mid-tier permanent shelter
    - **Effort**: M

27. **Update Log Cabin recipe (Tier 4)**
    - Recipe: 20kg Wood + 5kg Stone + 3kg Binding + 8kg Insulation
    - Time: 8 hours
    - Result: Location with ShelterFeature, +15°F, excellent protection
    - **AC**: Endgame shelter (already exists, just tune)
    - **Effort**: S

---

### Phase 6: Clothing/Armor System (Effort: M)
**Goal**: Create progression from makeshift to proper gear

#### Tasks

28. **Create tattered starting clothes**
    - `MakeTatteredWrap()` - minimal warmth (0.02 insulation)
    - Replace starting leather tunic/pants
    - **AC**: Starting gear is barely functional
    - **Effort**: S

29. **Create early-game wrappings**
    - Bark Wrappings (chest/legs) - craftable day 1
    - Grass Foot Wraps - better than barefoot
    - Plant Fiber Bindings - hands protection
    - **AC**: Basic protection from natural materials
    - **Effort**: M

30. **Tune existing hide armor**
    - Ensure requires hunting (hide, sinew)
    - Add tanning step if not present
    - **AC**: Mid-tier gear gated by hunting
    - **Effort**: S

31. **Create fur-lined armor (Tier 3)**
    - Recipe: Multiple hides + Fur + Sinew
    - Best insulation and protection
    - **AC**: Late-game cold weather gear
    - **Effort**: M

---

### Phase 7: Foraging UX Improvements (Effort: M)
**Goal**: Make foraging feel like searching, not looting

#### Tasks

32. **Hide items until foraged**
    - Modify Location display to not show items initially
    - Items only revealed after Forage action
    - **AC**: Locations feel empty until searched
    - **Effort**: M

33. **Update Forage action output**
    - Use ItemStack for display
    - Show: "You found: Stick (3), River Stone (2), Tinder (1)"
    - Group similar items
    - **AC**: Clean, stacked display
    - **Effort**: M

34. **Add forage time display**
    - Show time elapsed during forage
    - "You spent 1 hour searching and found..."
    - **AC**: Player aware of time cost
    - **Effort**: S

---

### Phase 8: Recipe Implementation (Effort: XL)
**Goal**: Implement all recipes in CraftingSystem

#### Tasks

35. **Implement all fire-making recipes**
    - Hand drill, bow drill, flint & steel
    - Add to `CreateFireRecipes()` method
    - **AC**: All fire methods available
    - **Effort**: M

36. **Implement all tool recipes**
    - Sharp rock, flint knife, bone knife, obsidian blade
    - All spear tiers, club tiers, hand axes
    - Add to `CreateToolRecipes()` method
    - **AC**: Complete tool progression
    - **Effort**: L

37. **Implement all shelter recipes**
    - Windbreak, lean-to, debris hut, log cabin
    - Add to `CreateShelterRecipes()` method
    - **AC**: Complete shelter progression
    - **Effort**: M

38. **Implement all clothing recipes**
    - Wrappings, hide armor, fur-lined gear
    - Add to `CreateClothingRecipes()` method
    - **AC**: Complete clothing progression
    - **Effort**: M

39. **Verify all materials have purpose**
    - Check every ItemProperty is used in recipes
    - Document material → recipe mapping
    - **AC**: No orphaned materials
    - **Effort**: S

39b. **Implement recipe categorization**
    - Add `Category` enum to CraftingRecipe (Fire, Tools, Weapons, Shelters, Clothing, Misc)
    - Update all recipes with appropriate category
    - Modify crafting menu to group by category with headers
    - Sort unavailable recipes to bottom within each category
    - **AC**: Crafting menu organized and navigable
    - **Effort**: M

---

### Phase 9: Balance & Testing (Effort: L)
**Goal**: Ensure day-1 survival is possible but challenging

#### Tasks

40. **Test day-1 survival path**
    - Start new game
    - Verify can forage materials
    - Verify can make fire (may fail, should be possible)
    - Verify can make windbreak
    - Verify can make sharp rock
    - **AC**: Day 1 survivable with skill
    - **Effort**: M

41. **Balance material spawn rates**
    - Adjust forage abundances per biome
    - Ensure common materials are actually common
    - Ensure rare materials feel special
    - **AC**: Material availability matches intended difficulty
    - **Effort**: M

42. **Balance crafting times**
    - Review all recipe times
    - Ensure realistic (5 min simple → 8 hours complex)
    - Test time pressure on survival
    - **AC**: Time costs feel appropriate
    - **Effort**: S

43. **Balance tool effectiveness**
    - Test damage values in combat
    - Test harvesting yields with different tool tiers
    - Ensure each tier is noticeably better
    - **AC**: Tool progression feels rewarding
    - **Effort**: M

44. **Test shelter warmth values**
    - Verify temperature bonuses are meaningful
    - Test survival in different weather with each shelter tier
    - **AC**: Better shelters = tangible survival benefit
    - **Effort**: S

45. **Test biome viability**
    - Test starting in each biome: Forest, Plains, Riverbank, Cave, Hillside
    - Verify each biome has materials for day-1 fire
    - Verify each biome has materials for day-1 shelter
    - Identify and fix any unplayable biomes (e.g., Cave lacking organics)
    - **AC**: All biomes are viable starting locations
    - **Effort**: M

---

### Phase 10: Polish & Documentation (Effort: S)
**Goal**: Clean up and document the new systems

#### Tasks

46. **Update item descriptions**
    - Review all new items for Ice Age theming
    - Ensure consistent voice/tone
    - **AC**: Immersive, period-appropriate descriptions
    - **Effort**: S

47. **Update CLAUDE.md**
    - Document new crafting patterns (skill checks, success rates)
    - Document material system and property usage
    - Document progression philosophy
    - **AC**: Future devs understand the system
    - **Effort**: S

48. **Create crafting guide** (optional)
    - Could be in-game help or external doc
    - Material sources by biome
    - Recipe trees
    - **AC**: Players can learn system
    - **Effort**: S

49. **Playtest full progression**
    - Complete run from start to log cabin
    - Document any pain points
    - Iterate on balance
    - **AC**: Full progression feels good
    - **Effort**: M

---

## Missing Considerations & Future Enhancements

### Harvesting Yield System

**Current Gap**: Plan mentions tool tiers affecting "harvesting yields" but no multiplier system exists.

**Recommendation for MVP**:
- Add `HarvestMultiplier` property to cutting tools
  - Sharp Rock: 1.0x (base)
  - Flint Knife: 1.3x
  - Bone Knife: 1.5x
  - Obsidian Blade: 1.8x
- Apply multiplier when foraging with tool equipped
- Apply multiplier when skinning animals (future hunting system)

**Implementation**: Modify `ForageFeature.Forage()` to check equipped weapon and multiply yields.

### Recipe Organization

**Current Gap**: With 40+ recipes, crafting menu will become unwieldy.

**Recommendation for MVP**:
- Add recipe categories to `CraftingRecipe`: Fire, Tools, Weapons, Shelters, Clothing, Misc
- Group recipes in crafting menu by category
- Show category headers: "--- FIRE-MAKING ---", "--- TOOLS ---", etc.
- Filter unavailable recipes to bottom of each category (greyed out)

**Future Enhancement**: Recipe discovery system (learn recipes through experimentation or teaching)

### Skill Progression Rates

**Current Gap**: Firecraft heavily used but progression rates not defined.

**Recommendation for MVP**:
- Grant Firecraft XP on both success AND failure
  - Success: `player.Skills.AddXP("Firecraft", skill.Level + 3)`
  - Failure: `player.Skills.AddXP("Firecraft", 1)`
- Rationale: Learning from failure is realistic
- Balance testing needed to ensure reasonable progression (should reach level 2-3 within a few days)

### Storage Solutions

**Current Gap**: Many new materials require inventory management.

**Recommendation for MVP**:
- Ensure starting Container capacity is adequate (current: 10kg)
- Consider craftable storage:
  - Bark Basket (5kg, 15 min craft)
  - Hide Sack (10kg, requires hunting)
  - Storage Cache (LocationFeature, 50kg)

**Defer to Post-MVP**: Advanced storage structures, item sorting

### Weather/Temperature Verification

**Current Gap**: Shelters provide warmth bonuses but integration with SurvivalProcessor not verified.

**Testing Required**:
- Confirm `LocationTemperature` in `SurvivalContext` modified by shelter features
- Test hypothermia/frostbite thresholds with each shelter tier
- Verify warmth bonuses scale appropriately
- Consider if shelters should provide WeatherProtection percentage (rain/wind)

**If Issues Found**: May need to modify `SurvivalProcessor.Process()` to account for shelter features

---

## Risk Assessment & Mitigation

### High Risks

**Risk**: Breaking existing save compatibility
- **Mitigation**: This is early dev, acceptable to break saves
- **Fallback**: Add version number to save files, document breaking change
- **Impact**: High if saves matter, Low in current dev phase

**Risk**: Day-1 survival too hard (frustrating) or too easy (boring)
- **Mitigation**: Extensive playtesting in Phase 9
- **Fallback**: Tuning material spawn rates and success chances is easy
- **Impact**: High - core gameplay balance

**Risk**: Fire-making RNG feels unfair
- **Mitigation**: Provide multiple paths (hand drill vs bow drill vs flint)
- **Mitigation**: Grant XP on failure so attempts feel productive
- **Fallback**: Adjust success rates based on feedback (easy config change)
- **Impact**: Medium - affects player frustration

**Risk**: Biome balance after material redistribution
- **Mitigation**: Test each biome as starting location in Phase 9
- **Mitigation**: Ensure all biomes have path to day-1 survival (fire + shelter)
- **Fallback**: Add minimal organic materials to problematic biomes (e.g., Cave)
- **Impact**: High - could make some biomes unplayable

### Medium Risks

**Risk**: Too many recipes overwhelming (action menu complexity)
- **Mitigation**: Implement recipe categories in Phase 8
- **Mitigation**: Filter unavailable recipes to bottom of list
- **Fallback**: Hide advanced recipes until skill threshold (future enhancement)
- **Impact**: Medium - affects discoverability and UX

**Risk**: Material grinding tedious
- **Mitigation**: Generous forage yields, stackable items (ItemStack)
- **Mitigation**: Harvesting multipliers reward better tools
- **Fallback**: Adjust abundance values in config
- **Impact**: Medium - affects engagement

**Risk**: Progression too linear
- **Mitigation**: Multiple paths (clubs vs spears, different shelter types)
- **Mitigation**: Biome variety creates different starting conditions
- **Fallback**: Add more optional recipes
- **Impact**: Low - acceptable for MVP

### Low Risks

**Risk**: Performance issues with many items
- **Mitigation**: ItemStack already implemented
- **Fallback**: Optimize spawning algorithms

---

## Success Metrics

### Functional Metrics
- [ ] Player can survive day 1 starting with nearly nothing
- [ ] Fire craftable within first 2 hours of gameplay (with skill check system)
- [ ] Basic shelter achievable by end of day 1
- [ ] No crafted items spawn in world
- [ ] All 40+ recipes implemented and tested
- [ ] Complete tool progression (4-5 tiers per tool type)
- [ ] Complete shelter progression (4 tiers)
- [ ] All biomes viable as starting locations
- [ ] Recipe categorization implemented for menu organization

### Experience Metrics
- [ ] Early game feels challenging but not impossible
- [ ] Material gathering feels purposeful, not grindy
- [ ] Each tier upgrade feels meaningful
- [ ] Crafting system teaches itself through experimentation
- [ ] Progression takes 3-5 days to mid-tier, 1-2 weeks to high-tier

### Technical Metrics
- [ ] No crafted items in LocationFactory spawn tables
- [ ] All ItemProperties used in at least one recipe
- [ ] Recipe times realistic (verified against research)
- [ ] All recipes follow property-based pattern
- [ ] Code follows existing architectural patterns

---

## Dependencies & Resources

### Code Dependencies
- `ForageFeature.cs` - core foraging system
- `CraftingSystem.cs` - recipe implementation
- `ItemFactory.cs` - item creation
- `LocationFactory.cs` - world spawning
- `RecipeBuilder.cs` - recipe DSL
- `ItemProperty.cs` - material system
- `Program.cs` - starting conditions

### External Resources Needed
- Historical research on Ice Age tool-making (already incorporated in design)
- Playtester feedback (internal)
- Time estimates for crafting (use realistic approximations)

### Knowledge Dependencies
- Understanding of property-based crafting system ✓
- Understanding of ForageFeature mechanics ✓
- Understanding of LocationFeature system ✓
- Understanding of action system and GameContext ✓

---

## Timeline Estimate

### Week 1
- Phase 1: Cleanup (2 days)
- Phase 2: Materials (3 days)

### Week 2
- Phase 3: Fire-Making (4 days - extended for skill check system)
- Phase 4: Tool Progression (3 days)

### Week 3
- Phase 5: Shelters (2 days)
- Phase 6: Clothing (2 days)
- Phase 7: Foraging UX (2 days)

### Week 4
- Phase 8: Recipe Implementation (4 days - includes categorization)
- Phase 9: Balance & Testing (3 days)

### Week 5 (Buffer)
- Phase 9 continued: Additional testing iterations (2 days)
- Phase 10: Polish (2 days)
- Contingency for unforeseen issues

**Total: 4-5 weeks** (extended from original 3-4 weeks)
**Reason for extension**: Thorough testing of fire-making mechanics, biome viability, and recipe organization required for successful MVP

---

## Notes

- **Realism heuristic**: When in doubt, choose realistic over game-y
- **Every material must have purpose**: No decorative-only items in survival
- **Progression through access, not stats**: Better materials unlock new capabilities
- **Ice Age theme**: All items/recipes should fit the setting
- **Failure is okay**: Fire-making can fail, that's realistic and creates tension

---

## Approval & Sign-off

- [ ] Plan reviewed and approved
- [ ] Timeline acceptable
- [ ] Risks understood and accepted
- [ ] Ready to begin implementation

**Next Step**: Begin Phase 1 - Cleanup & Foundation
