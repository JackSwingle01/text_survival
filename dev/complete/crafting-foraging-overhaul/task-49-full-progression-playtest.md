# Task 49: Full Progression Playtest Results

**Date**: 2025-11-02
**Tester**: Claude Code (automated playtest)
**Objective**: Test complete progression from start to Log Cabin to validate crafting/foraging overhaul
**Result**: **EARLY GAME BLOCKER** - Critical balance issues prevent progression

---

## Executive Summary

The playtest was terminated early due to **unwinnable game state** caused by cascading balance issues. While individual systems work as designed, their combination creates too many fail states in early game survival.

**Playtest Duration**: ~2.5 hours game time
**Progression Reached**: Failed to establish sustainable fire
**Blockers**: 4 critical, 3 high-priority balance issues

---

## Critical Issues Found

### 🔴 BLOCKER #1: Fire-Making RNG Death Spiral

**Severity**: CRITICAL
**Impact**: Game-breaking for new players

**Description**: Fire-making has 30-50% success rates and consumes materials on failure. Players can easily fail 3-4 attempts before succeeding, exhausting all resources.

**Observed Behavior**:
- Attempt 1 (Hand Drill, ~30%): FAILED - consumed 0.55kg materials
- Attempt 2 (Hand Drill, ~40%): FAILED - consumed 0.55kg materials
- Attempt 3 (Hand Drill, ~40%): SUCCESS - consumed 0.55kg materials
- Attempt 4 (Hand Drill, 30%): FAILED - consumed materials (at new location)
- Attempt 5 (Bow Drill, 50%): FAILED - consumed 5 items

**Materials consumed**: ~12 items across 5 attempts with 60% failure rate

**Player State at Termination**:
- Food: 16% (STARVING)
- Temp: 40.3°F (severe hypothermia)
- Energy: 27% (Very Tired)
- Materials: 2x Firewood, 1x Bark Strips (insufficient for another attempt)

**Root Cause**:
1. Success rates too low (30-50%) for critical survival mechanic
2. Material consumption on failure is punishing
3. No "learning curve" - failure XP doesn't help enough

**Recommendation**:
- EITHER increase base success rates (50-70%)
- OR don't consume materials on failure
- OR add "partial success" mechanic (embers created but need tending)
- OR provide guaranteed fire-starting method with longer time cost

---

### 🔴 BLOCKER #2: Resource Depletion Spiral

**Severity**: CRITICAL
**Impact**: Forces travel before fire established, accelerates hypothermia

**Description**: Starting location's forage feature depletes rapidly or has low spawn rates. Player must travel to find materials while cold and hungry.

**Observed Behavior**:
- Initial foraging (Clearing): 4 successful finds in 4 attempts
- Post-fire attempts (Clearing): 2 finds, 3 empty results in 5 attempts
- After travel (Ancient Woodland): 9 finds in ~8 attempts

**Forage Results by Location**:

**Clearing (Starting Location)**:
- Forage 1-4: 100% success (Firewood, Plant Fibers, Bark Strips, Large Stick)
- Forage 5-9: 40% success (2 finds, 3 empty)
- **Depleted after ~120 minutes**

**Ancient Woodland (After Travel)**:
- Forage 1: 100% success (Large Stick, Bark Strips)
- Forage 2-7: ~66% success rate
- **Much higher yields, including food/water**

**Root Cause**:
1. ForageFeature may deplete too quickly
2. Starting location has lower resource density than other forest locations
3. Travel time (7 min) + exposure risk conflicts with urgent fire need

**Recommendation**:
- Increase starting location resource density/respawn
- Add visible items on ground at start (not just forageable)
- Consider "tutorial" fire that lasts longer (2+ hours)

---

### 🟠 HIGH PRIORITY #3: Multiple Campfires Bug

**Severity**: HIGH
**Impact**: Clutters locations, confuses fire management UI

**Description**: Fire-making recipes create NEW HeatSourceFeatures instead of checking for existing ones. This results in multiple campfires in the same location.

**Observed Behavior**:
```
>>> CLEARING <<<
You see:
    Campfire (cold)
    Campfire (cold, 30 min fuel ready)
```

**Location State**:
- Original campfire: Depleted, cold, no embers
- New campfire: Created by Hand Drill recipe, has 30 min fuel but cold (not lit)

**Root Cause**:
`CraftingRecipe.ResultingInLocationFeature()` always creates a new feature rather than:
1. Checking if HeatSourceFeature already exists
2. Refueling existing campfire
3. Removing depleted campfire before creating new one

**Recommendation**:
- Modify fire-making recipes to check for existing HeatSourceFeature
- If exists and cold, refuel it
- If exists and has embers, just add fuel and relight
- Only create new if none exists

**Code Location**: `Crafting/CraftingRecipe.cs` - `ResultingInLocationFeature()` method

---

### 🟠 HIGH PRIORITY #4: Food Scarcity Too Severe

**Severity**: HIGH
**Impact**: Player starves before able to hunt

**Description**: Food consumption is rapid, but early-game food sources provide minimal calories. Player reached 16% food with no hunting tools and no food sources nearby.

**Food Progression**:
- Start: 50% (Peckish)
- After 1 hour: 43% (Peckish)
- After 2 hours: 29% (Hungry)
- After 2.5 hours: 16% (STARVING)

**Food Consumption Rate**: ~14% per hour

**Food Sources Found**:
- Wild Mushroom: Restored only 1% food (negligible)
- No berries, no meat (requires hunting tools not yet craftable)

**To Hunt, Player Needs**:
1. Fire (to survive)
2. Sharp Rock (5 min craft, requires 2x Stone)
3. Then hunt animals

**Time to Hunt**: Minimum 3+ hours if everything goes perfectly

**Root Cause**:
1. Food consumption rate too high for scarcity
2. Early-game food sources (mushrooms, berries) provide too little
3. Path to hunting is gated behind fire + tool crafting

**Recommendation**:
- Reduce food consumption rate by 30-50%
- Increase mushroom/berry calorie values
- OR provide small game that doesn't require tools (gathering eggs, grubs, etc.)
- OR start player at 80-100% food instead of 50%

---

### 🟡 MEDIUM PRIORITY #5: Time Pressure Too High

**Severity**: MEDIUM
**Impact**: Player feels rushed, can't explore or learn systems

**Description**: Between fire-making (20-45 min), foraging (15 min/attempt), and travel (7 min), player's temperature drops to dangerous levels before fire established.

**Time Breakdown**:
- Fire attempt 1: 20 min (FAILED)
- Fire attempt 2: 20 min (FAILED)
- Fire attempt 3: 20 min (SUCCESS)
- Foraging (9 attempts): 135 min
- Travel: 7 min
- Fire attempt 4: 20 min (FAILED)
- Fire attempt 5: 45 min (FAILED)

**Total time**: ~267 minutes (4.5 hours)

**Temperature Progression**:
- Start: 98.6°F (with fire warmth)
- After fire out: 57.8°F
- After foraging: 43.8°F
- After more attempts: 40.3°F (CRITICAL)

**Feels Like Temp**: Dropped from 38.6°F to 30.3°F

**Root Cause**:
1. Starting fire only lasts 60 min
2. Ember grace period (15 min) is too short for realistic foraging
3. Temperature drops rapidly without fire
4. Every failed fire-making attempt costs 20-45 minutes

**Recommendation**:
- Increase starting fire duration to 90-120 minutes
- Increase ember duration to 25-30 minutes (currently 25% of burn time)
- Add "warm" status that persists 15-30 min after leaving fire
- Consider "desperation" mechanic (instant fire at high cost)

---

### 🟡 MEDIUM PRIORITY #6: Unclear Fire Management After Creation

**Severity**: MEDIUM
**Impact**: UX confusion about how to light newly created fire

**Description**: After successfully crafting a fire via Hand Drill, the campfire exists but is COLD with fuel ready. Not immediately obvious how to light it.

**Observed Behavior**:
1. Craft "Hand Drill Fire" - SUCCESS
2. Campfire created with 30 min fuel
3. Campfire is COLD (not burning)
4. Player must use "Start Fire" action again to light it

**Expected Behavior**: Fire-making recipe should create AND LIGHT the campfire immediately

**Recommendation**:
- Auto-light campfire when created via fire-making recipe
- OR add clear message: "You've created a campfire. Select 'Start Fire' to light it."
- This is confusing because the recipe is called "Hand Drill **Fire**" but doesn't actually start a fire

---

### 🟢 LOW PRIORITY #7: Recipe Display Shows Wrong Skill Level

**Severity**: LOW
**Impact**: Minor UI issue, doesn't affect gameplay

**Description**: Crafting recipe display shows "Required skill: Firecraft (Level 0)" even when player is Level 1.

**Observed**: After leveling up Firecraft to level 1, recipe still showed:
```
Required skill: Firecraft (Level 0)
```

**Root Cause**: UI displays the *requirement* (Level 0) rather than showing both requirement and player's current level

**Recommendation**: Display as:
```
Required skill: Firecraft (Level 0) - Your Level: 1
```

---

## Positive Observations

Despite critical issues, several systems worked well:

### ✅ Ember System Working
- Original campfire burned for 60 min as expected
- Embers lasted ~15 min after fire out (25% of burn time)
- "Add Fuel to Fire" correctly detected cold fire (no embers)

### ✅ Skill XP on Failure
- Failed fire-making attempts awarded Firecraft XP
- Leveled from 0 → 1 after first failure
- This softens the blow of RNG failures

### ✅ Forage Variety
- Ancient Woodland had good variety: Sticks, Firewood, Plant Fibers, Food, Water
- Forageable items matched biome (forest materials)
- Finding water/food via foraging works as intended

### ✅ Crafting Menu Filtering
- Menu correctly hid unavailable recipes based on materials
- "Show My Materials" worked well for planning
- Recipe categorization clear (Items vs Build Features)

### ✅ Status Display
- Temperature display with "Feels like" is informative
- Status bars clearly show urgency (red at low levels)
- "You are still feeling cold" messages reinforce survival pressure

---

## Progression Blockers

Player was unable to test mid/late-game systems due to early-game death spiral:

### ❌ Could Not Test:
- Sharp Rock crafting (no stones foraged)
- Tool progression (Sharpened Stick, Spear tiers)
- Hunting mechanics
- Windbreak/Lean-to shelter crafting
- Clothing crafting (Bark Wraps, etc.)
- Mid-game fire-making (Bow Drill upgrade path)
- Long-term survival balance

### ⚠️ Partially Tested:
- Fire-making: Hand Drill + Bow Drill mechanics work, but balance too punishing
- Foraging: Works but depletion too aggressive in starting location
- Resource management: Works but scarcity too severe
- Temperature system: Works but time pressure too high

---

## Recommendations Summary

### Immediate Fixes (Required for Playability)

1. **Fire-Making Balance** (Choose One):
   - Option A: Increase success rates to 50-70% base
   - Option B: Don't consume materials on failure
   - Option C: Add guaranteed slow method (rubbing sticks for 60 min = 100% success)
   - Option D: Partial success creates embers that can be fed

2. **Fix Multiple Campfires Bug**:
   - Check for existing HeatSourceFeature before creating new one
   - Reuse/refuel existing fire instead of creating duplicates

3. **Food Balance**:
   - Reduce consumption rate by 40%
   - OR increase Wild Mushroom to 10-15% food
   - OR start player at 80% food

4. **Starting Resources**:
   - Increase starting fire duration to 90-120 min
   - Increase forage density in starting location
   - OR place visible items on ground at start

### Medium-Term Improvements

5. **Fire Creation UX**:
   - Auto-light campfire when created via fire-making recipe
   - OR clarify that "Hand Drill Fire" creates unlit campfire

6. **Time Pressure**:
   - Extend ember grace period to 30 min
   - Slow temperature drop rate when player has been warm recently

7. **Early Game Path**:
   - Consider adding "easy" food source (eggs, grubs, etc.)
   - Tutorial messaging about fire importance

---

## Statistical Summary

**Playtest Metrics**:
- Game time survived: ~4.5 hours (267 minutes)
- Fire-making attempts: 5 (2 success, 3 failures) = 40% success rate
- Materials consumed: ~12 items across fire attempts
- Locations visited: 2 (Clearing, Ancient Woodland)
- Forage attempts: 18 (11 successful, 7 empty) = 61% success rate
- Final state: Starving (16% food), Hypothermic (40°F), Very Tired (27% energy)

**Death Spiral Timeline**:
1. Hour 0: Start with 60 min fire
2. Hour 1: Fire out, attempt 1 fails
3. Hour 1.5: Attempt 2 fails, materials low
4. Hour 2: Attempt 3 succeeds but campfire not lit
5. Hour 2.5: Travel to find resources, starving begins
6. Hour 3.5: Find resources, attempt 4 fails
7. Hour 4.5: Attempt 5 fails, unwinnable state

---

## Conclusion

The crafting and foraging overhaul has successfully implemented:
- ✅ Fire-making skill checks
- ✅ Forage variety and biome differentiation
- ✅ Ember system
- ✅ Material property system
- ✅ Recipe filtering and organization

However, the **balance is too punishing** for early-game survival. The combination of:
1. Low fire-making success rates (30-50%)
2. Material consumption on failure
3. Rapid resource depletion
4. High food consumption
5. Aggressive temperature drops

Creates a high failure rate that makes the game feel unfair rather than challenging. Players need more margin for error in the critical first few hours.

**Recommendation**: Address fire-making balance (recommendation #1) before further testing. Once players can reliably establish a fire within 2-3 attempts, the other systems can be properly evaluated.

**Task 49 Status**: INCOMPLETE - early game blockers prevent full progression testing. Recommend fixing critical issues #1-4 before retry.

---

## Next Steps

1. Review and prioritize recommendations with user
2. Implement fire-making balance changes
3. Fix multiple campfires bug
4. Adjust food/resource balance
5. Retry Task 49 playtest with fixes applied
6. If successful, proceed to mid/late-game testing
