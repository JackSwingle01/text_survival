# Known Issues - Text Survival RPG

**Last Updated:** 2025-11-04 (Hypothermia Death + Bug Fixes)
**Status:** ✅ All critical bugs resolved - Game is fully playable

---

## 🔴 Breaking Exceptions

*Critical errors that cause crashes or prevent gameplay*

**None currently! All breaking exceptions have been resolved.** ✅

---

## ✅ Recently Resolved

### Death System (IMPLEMENTED 2025-11-04)
**Severity**: BLOCKER - Game never ended
**Status**: ✅ **RESOLVED**

**What Was Fixed**:
- Added death check in main game loop (Program.cs)
- Implemented comprehensive death screen showing:
  - Cause of death analysis (which organ failed, or hypothermia/dehydration/starvation)
  - Final survival stats (health, calories, hydration, energy, temperature)
  - Body composition at death (weight, fat %, muscle %)
  - Time survived (days, hours, minutes)
- Game now exits cleanly when player dies

**Implementation**:
- Location: `Program.cs` lines 12-88 (DisplayDeathScreen method)
- Death check: After each action execution
- Causes tracked: Brain death, cardiac arrest, respiratory failure, liver failure, hypothermia, dehydration, starvation

---

## 🟠 Bugs

*Incorrect behavior that prevents intended functionality*

### 1.2 Survival Stat Consequences (RESOLVED ✅)

**Severity:** HIGH - Game Balance Issue
**Location:** `Bodies/Body.cs`, `Bodies/CapacityCalculator.cs`, `Survival/SurvivalProcessor.cs`
**Status:** ✅ **RESOLVED** (2025-11-04 - All phases complete)

**Original Issue:** Players can survive indefinitely at 0% food/water/energy/extreme cold with no consequences.

**Implementation Complete (2025-11-04)**:
- ✅ **Phase 1**: Starvation system (fat/muscle consumption, organ damage)
- ✅ **Phase 2**: Dehydration & exhaustion tracking
- ✅ **Phase 3**: Capacity penalties at low stats
- ✅ **Phase 4**: Organ regeneration when fed/hydrated/rested
- ✅ **Phase 5**: Warning messages at stat thresholds
- ✅ **NEW**: Hypothermia organ damage (<89.6°F body temp → death in 3-7 hours)

**Hypothermia Death System**:
- Below 89.6°F: Progressive organ damage to Heart/Brain/Lungs
- Damage scales with temperature (40°F = 0.3 HP/hr, 85°F = 0.15 HP/hr)
- 30-minute grace period before damage starts
- Death in 3-7 hours depending on severity
- Location: `Bodies/Body.cs` lines 477-520

**Files Modified**:
- `Bodies/Body.cs` (~300 lines added)
- `Bodies/CapacityCalculator.cs` (~90 lines added)
- `Survival/SurvivalProcessor.cs` (~35 lines added)
- `Bodies/DamageInfo.cs` (added Internal damage type)

**Build Status**: ✅ SUCCESS (0 errors)
**Testing Status**: ⚠️ Needs extended playtesting for balance validation

---

### Multiple Campfires Created in Same Location (ALREADY FIXED ✅)

**Severity:** HIGH - Clutters locations, confuses fire management
**Location:** `Crafting/CraftingSystem.cs` lines 60-73
**Status:** ✅ **ALREADY FIXED** (Verified 2025-11-04)

**Fix Implementation:**
CraftingSystem.Craft() now checks for existing HeatSourceFeature before creating a new one:
```csharp
if (feature is HeatSourceFeature newFire)
{
    var existingFire = _player.CurrentLocation.Features.OfType<HeatSourceFeature>().FirstOrDefault();
    if (existingFire != null)
    {
        existingFire.AddFuel(initialFuel, newFire.FuelMassKg);
        Output.WriteSuccess($"You add fuel to the existing {recipe.LocationFeatureResult.FeatureName}.");
        break;
    }
}
```

**Note:** StartFire action (ActionFactory.cs lines 330-331) also checks for existing fires and handles relighting correctly.

### Inspect Action Listed Twice and Drops Item (FIXED ✅)

**Severity:** HIGH - Breaking user experience
**Location:** `Actions/ActionFactory.cs` line 727
**Status:** ✅ **FIXED** (2025-11-04)

**Issues Found:**
1. **Duplicate "Inspect" label**: DropItem action had `CreateAction($"Inspect {item}")` instead of `CreateAction($"Drop {item}")`
2. **Message ordering**: "You eat the X" appeared before "You take the X from your Bag"

**Fixes Applied:**
1. Changed line 727: `"Inspect {item}"` → `"Drop {item}"`
2. Moved `inventoryManager.RemoveFromInventory(food)` to occur BEFORE eating message in Player.cs line 92
3. Added early return after food consumption to prevent double removal

**Files Modified:**
- `Actions/ActionFactory.cs` (line 727)
- `Player.cs` (lines 91-96)


---

### Time Handling Pattern Inconsistency

**Severity:** Medium - Architectural Pattern Inconsistency (Technical Debt)
**Location:** ForageFeature.cs, ActionFactory.cs (fire actions)
**Status:** 🟡 **TECHNICAL DEBT**

**Issue:**
Some actions handle time updates inconsistently - ForageFeature.Forage() and fire actions manually call World.Update() instead of using the standard `.TakesMinutes()` ActionBuilder pattern.

**Impact:**
- Code works correctly but violates architectural consistency
- Makes time-handling logic harder to track
- Future developers may not know which pattern to follow

**Recommended Fix:**
Refactor to use `.TakesMinutes()` consistently across all actions.

**Priority:** Medium - Works correctly but should be standardized

---

### Material Properties Display Inconsistency

**Severity:** Medium - UX Bug
**Location:** Crafting system material display
**Status:** 🔴 **ACTIVE**

**Reproduction:**
1. Pick up Dry Grass and Large Stick
2. Open crafting menu → "Show My Materials" displays: `Tinder: 0.0 total`
3. View Hand Drill Fire craft screen shows: `Tinder: 0.5/0.1` (✓ available)

**Expected:** Both screens should show identical material totals.

**Impact:**
- Players see conflicting information
- May think they can't craft recipes when they actually can

**Priority:** Medium - Causes confusion but doesn't block gameplay

---

### Location.Update() Doesn't Call Feature.Update()

**Severity:** Medium - Missing Feature Functionality
**Location:** `Environments/Location.cs:150-154`
**Status:** ⚠️ **NEEDS VERIFICATION** (May already be fixed)

**Issue:**
Location.Update() method doesn't call Update() on LocationFeatures (like HeatSourceFeature), which should cause campfire fuel to never decrease naturally.

**Current Code:**
```csharp
public void Update()
{
    _npcs.ForEach(n => n.Update());
    // Missing: Features.ForEach(f => f.Update());
}
```

**Note:** Recent validation testing showed fires DO burn down correctly (3.0 hr → 2.0 hr after 1 hour). Need to verify current implementation.

**Priority:** Medium - Verify if still an issue

---

### Game State Inconsistencies During Navigation

**Severity:** Medium
**Location:** Action system / menu navigation
**Status:** 🟡 **MONITORING**

**Description:**
During testing, game state appeared to jump or rollback unpredictably during menu navigation. May be related to TestModeIO file I/O timing rather than core game logic.

**Priority:** Medium - Monitor during future testing

---

## 🟡 Questionable Functionality

*Behaviors that work but may not be intended or optimal*

### "Press any key to continue..." Requires Actual Input

**Severity:** Low
**Location:** Input.cs / Output.cs
**Status:** 🟡 **ACTIVE**

**Description:**
The "Press any key to continue..." prompt requires user input, but sending empty string "" causes it to be interpreted as invalid input for next menu.

**Impact:** Causes confusion in automated testing

**Potential Solutions:**
1. Accept any input including empty in TEST_MODE
2. Remove the pause entirely in TEST_MODE
3. Use different Input method that doesn't validate as number

---

### Sleep Option Disappears When Exhausted (FIXED ✅)

**Severity:** Low
**Location:** `Bodies/Body.cs` line 38
**Status:** ✅ **FIXED** (2025-11-04)

**Root Cause:**
IsTired property had backwards logic: `Energy > 60` (only allow sleep if NOT tired)

**Fix:**
Changed to: `Energy < SurvivalProcessor.MAX_ENERGY_MINUTES` (allow sleep if not fully rested)

**Impact:** Players can now sleep whenever they want, especially when exhausted (0% energy)

---

### Foraging Only Allows Fixed 1-Hour Increments

**Severity:** Medium
**Location:** Forage action (ActionFactory.cs)
**Status:** 🟡 **ACTIVE**

**Current Behavior:** Foraging is always exactly 60 minutes with no player control

**Expected:** Allow flexible time investment (e.g., "How many minutes would you like to forage? (15-180)")

**Impact:**
- Cannot do "quick" 15-30 minute trips
- Reduces strategic options for balancing warmth vs. resource gathering
- Particularly problematic early game

**Priority:** Medium - Improves UX and strategic depth

---

## 🟢 Balance & Immersion Issues

*Mechanics that work correctly but may need tuning*

### Frostbite Severity May Be Too High

**Severity:** Low (balance/tuning)
**Status:** 🟢 **DEFER** - Monitor during extended testing

**Observation:**
After 4 hours in 38-40°F weather with fur wraps (0.15 insulation), all extremities reach 100% Critical frostbite severity.

**Note:** This is NOT the stacking bug (which was fixed). Temperature physics are realistic and working correctly. This may be intentionally punishing to encourage fire-making/shelter.

**Recommendation:** Monitor during future playtests. If players can't reasonably survive first few hours with optimal play, consider adjusting.

**Priority:** Low - Defer until more testing data

---

### Time Passage During Menu Navigation

**Severity:** Medium
**Status:** 🟢 **MONITORING**

**Description:** Time appears to pass during menu actions (looking around, checking inventory, etc.) - each action may cost 5-15 minutes.

**Balance Concern:**
- Menu actions should be "free" or very low cost
- Navigation shouldn't cause starvation/hypothermia
- Player punished for interacting with game systems

**Expected:**
- Looking around: instant or 1 minute
- Opening containers: instant or 30 seconds
- Taking items: 1-2 minutes per item
- Checking stats: instant

**Priority:** Medium - Monitor and adjust if confirmed problematic

---

### Food Scarcity - Day 1 Survival

**Severity:** Medium (balance)
**Status:** 🟢 **NEEDS VALIDATION**

**Previous Findings:**
- Food depletes ~14% per hour
- Wild Mushroom restored only 1% food
- Player reached 16% food (STARVING) after 2.5 hours

**Since Then:**
- Game balance changes implemented (75% starting calories, improved food variety)
- Critical fixes completed (starting fire, guaranteed materials)
- **Needs re-testing with current balance**

**Priority:** Medium - Validate with fresh playtest

---

## ✅ Resolved Issues

*Fixed bugs and completed improvements - newest first*

### New Animal Spawns Inherit Previous Hunt State (Fixed 2025-11-02)
- **Severity:** HIGH - Broke hunting gameplay loop
- **Issue:** After first animal fled, subsequent animals spawned at 30m with "Detected" state instead of 100m "Idle"
- **Root Cause:** When animals fled, `CurrentLocation` was set to null but animals remained in `location._npcs` list with stale state. Hunt menu listed all animals from `_npcs` regardless of CurrentLocation.
- **Solution:**
  1. Added `Location.RemoveNpc(npc)` method to properly remove NPCs from `_npcs` list
  2. Updated 3 flee locations to call `RemoveNpc()` before setting `CurrentLocation = null`:
     - StealthManager.cs line 198 (HandleAnimalDetection)
     - HuntingManager.cs line 94 (Shoot miss flee)
     - HuntingManager.cs line 180 (Wounded flee with blood trail)
  3. Added defensive filter in ActionFactory.cs hunt menu to check `CurrentLocation == ctx.currentLocation`
- **Files Modified:**
  - Environments/Location.cs (added RemoveNpc method)
  - PlayerComponents/StealthManager.cs (call RemoveNpc on flee)
  - PlayerComponents/HuntingManager.cs (call RemoveNpc on flee - 2 locations)
  - Actions/ActionFactory.cs (defensive filter - 2 locations)
- **Testing:** Confirmed animals now spawn fresh at 100m Idle state every hunt
- **Result:** ✅ Full hunting loop now functional - can hunt multiple animals sequentially

---

### Starting Materials Invisible (Fixed 2025-11-02)
- **Issue:** 5 guaranteed starting materials added to ground but not visible in "Look Around"
- **Root Cause:** Items had `IsFound = false` by default
- **Solution:** Set `IsFound = true` for all starting materials in Program.cs
- **Result:** All 5 items now visible (3 Large Sticks + 2 Dry Grass)

---

### Fire Burns Out Too Fast (Fixed 2025-11-02)
- **Issue:** Starting fire burned out in 30 minutes instead of 3 hours
- **Root Cause:** Tried to add softwood (requires 400°F fire) to cold fire (32°F) - AddFuel() failed silently
- **Solution:** Use kindling instead (0°F requirement), increased fuel to 4.5kg (accounts for 1.5 kg/hr burn rate)
- **Result:** Fire now lasts 3+ hours, shows "3.0 hr" → "2.0 hr" after 1 hour correctly

---

### Fire Temperature Circular Dependency Stack Overflow (Fixed 2025-11-02)
- **Severity:** CRITICAL - Game crashed on startup
- **Root Cause:** Circular dependency in temperature calculations:
  - GetCurrentFireTemperature() → Location.GetTemperature() → GetEffectiveHeatOutput() → GetCurrentFireTemperature() (infinite loop)
- **Solution:** Use `ParentLocation.Parent.Weather.TemperatureInFahrenheit` directly instead of calling Location.GetTemperature()
- **Files:** HeatSourceFeature.cs (lines 56, 183)
- **Result:** Game starts successfully, no crashes

---

### Fire-Making RNG Death Spiral (Fixed 2025-11-02)
- **Issue:** 30-50% success rates + material consumption on failure created unwinnable scenarios
- **Solution:** Implemented guaranteed starting conditions:
  - 4.5kg kindling fuel (3 hours warmth)
  - 3 Large Sticks + 2 Dry Grass on ground (visible)
  - Enough materials for 3-4 fire restart attempts
- **Result:** Game transformed from unplayable to viable survival path

---

### Resource Depletion Death Spiral (Fixed 2025-11-02)
- **Issue:** Starting location depleted rapidly, forcing dangerous travel before fire established
- **Solution:** Added guaranteed starting materials (3 sticks + 2 tinder) visible on ground
- **Result:** Player has sufficient materials for multiple fire attempts without traveling

---

### Temperature System Too Punishing (Improved 2025-11-01)
- **Issue:** Temperature physics mathematically correct but starting conditions unrealistic
- **Analysis:** Real-world data confirmed hypothermia in 14-20 minutes at 25-30°F with minimal clothing
- **Solutions Implemented:**
  1. Better starting gear: Worn Fur Wrap + Fur Leg Wraps (0.15 total insulation, was 0.04) - +275% improvement
  2. Starting fire: 15 minutes warmth, +15°F heat
  3. Forageable starting location: Can gather materials immediately
- **Result:** Survival time extended to ~1.5-2 hours, still challenging but playable

---

### Frostbite Effects Stacking Infinitely (Fixed 2025-11-01)
- **Severity:** CRITICAL - Game Breaking
- **Root Cause:** EffectRegistry only checked EffectKind, not TargetBodyPart. Frostbite set to AllowMultiple(true).
- **Symptoms:** Hundreds of frostbite effects on every body part, Strength/Speed → 0% within 3 hours
- **Solution:**
  1. Fixed EffectRegistry.cs to check BOTH EffectKind AND TargetBodyPart
  2. Changed frostbite to AllowMultiple(false)
- **Result:** Only 4 frostbite effects total (one per extremity), effects properly update severity

---

### Bow Drill Recipe Requires Non-Existent Skill (Fixed 2025-11-02)
- **Severity:** HIGH - Breaking Exception
- **Root Cause:** Bow Drill recipe had `.RequiringSkill("Fire-making", 1)` from earlier implementation
- **Error:** `System.ArgumentException: Skill Fire-making does not exist`
- **Solution:** Removed skill requirement from recipe - skill check happens in StartFire action
- **Files:** Crafting/CraftingSystem.cs line 168

---

### Crafting Preview Shows Incorrect Item Consumption (Fixed 2025-11-02)
- **Severity:** Medium - Display Bug
- **Root Cause:** Item.GetProperty() returned default(ItemProperty) = ItemProperty.Stone (enum value 0) instead of null when property not found
- **Symptoms:** Preview showed items without required properties (Dry Grass, Nuts, Grubs for Flint+Stone recipe)
- **Solution:** Cast enum list to nullable before FirstOrDefault()
- **Files:** Items/Item.cs:77-82

---

### Sleep Energy Clamping Bug (Fixed 2025-11-02)
- **Severity:** HIGH - Breaking bug
- **Root Cause:** SurvivalProcessor.Sleep() clamped energy to 1.0 instead of MAX_ENERGY_MINUTES (960)
- **Symptoms:** Energy would never restore above 1 minute, sleep mechanic completely broken
- **Solution:** Changed `Math.Min(1, ...)` to `Math.Min(MAX_ENERGY_MINUTES, ...)`
- **Files:** Survival/SurvivalProcessor.cs:298
- **Discovery:** Found during unit test creation

---

### Organ Capacity Scaling Bug (Fixed 2025-11-02)
- **Severity:** HIGH - Game logic bug
- **Root Cause:** Organs didn't implement GetConditionMultipliers(), weren't included in capacity averaging
- **Symptoms:** Destroyed heart/lung still provided 100% capacity, organ damage had no mechanical effect
- **Solution:**
  1. Added GetConditionMultipliers() override to Organ class
  2. Included organs in CapacityCalculator averaging
- **Files:** Bodies/Organ.cs:27-43, Bodies/CapacityCalculator.cs:46-49
- **Result:** Organ injuries now have meaningful consequences (destroyed heart → 83% blood pumping)

---

### Message Spam During Long Actions (Fixed 2025-11-02)
- **Issue:** Repeated "still feeling cold" messages 15-20 times during 1-hour actions
- **Solution:** Added message batching system - now shows "(occurred 10 times)"
- **Files:** IO/Output.cs:14-170, World.cs:10-30

---

### Starting Campfire Not Visible (Fixed 2025-11-02)
- **Issue:** Campfire provided warmth but didn't appear in "Look around"
- **Solution:** Added LocationFeature display loop to show HeatSourceFeature and ShelterFeature
- **Files:** Actions/ActionFactory.cs:863-885

---

### Crafting Material Consumption Unclear (Fixed 2025-11-02)
- **Issue:** Players didn't know which items would be consumed before crafting
- **Solution:** Added PreviewConsumption() method to show exact items/amounts before confirmation
- **Files:** Crafting/CraftingRecipe.cs:101-151

---

### Dead Campfires Not Displayed (Fixed 2025-11-02)
- **Issue:** Campfires with FuelRemaining = 0 disappeared from "Look around"
- **Solution:** Modified ActionFactory.cs to show "(cold)" status
- **Files:** Actions/ActionFactory.cs:875-893

---

### Energy Depletes to 0% Instantly (False Alarm)
- **Status:** ❌ **FALSE ALARM** - Energy works correctly
- **What happened:** Multiple background test processes caused file I/O conflicts showing incorrect state
- **Actual behavior:** Energy depletes properly: 83% → 82% → 81% → 74% over 1 hour
- **Lesson learned:** Kill all background processes before testing

---

### Forage Success Rates Untested (Tested 2025-11-01)
- **Status:** ✅ **TESTED AND WORKING**
- **Test Results:**
  - Forest: Successfully found Bark Strips, Plant Fibers, Sticks, Firewood, Dry Grass
  - Cave: Successfully found Mushrooms, River Stone, Flint, Clay, Handstone, Sharp Stone
  - All biome-specific foraging working correctly

---

### Fire-Making Skill Checks Untested (Tested 2025-11-01)
- **Status:** ✅ **TESTED AND WORKING**
- **Test Results:**
  - 30% base success (Firecraft 0) - FAILED first attempt (expected)
  - Materials consumed on failure (as designed)
  - XP gain on failure - leveled to Firecraft 1 (working)
  - 40% success (Firecraft 1) - SUCCESS second attempt
  - Skill progression formula working: `30% + (level * 10%)`
