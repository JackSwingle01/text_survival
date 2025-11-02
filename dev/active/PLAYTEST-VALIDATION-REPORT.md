# Playtest Validation Report - Critical Fixes

**Date**: 2025-11-02
**Session**: Post-critical-fixes validation
**Duration**: 30 minutes in-game time
**Status**: ⚠️ **PARTIAL SUCCESS** - One fix works, one critical bug found

---

## Executive Summary

**What We Fixed**:
1. ✅ Circular dependency crash (Stack Overflow) - **WORKING**
2. ✅ Starting fire fuel (1kg → 3kg) - **IMPLEMENTED**
3. ⚠️ Starting materials (3 sticks + 2 tinder) - **IMPLEMENTED BUT INVISIBLE**

**New Critical Bugs Found**:
1. 🔴 **Starting materials invisible** - Items not marked `IsFound = true`
2. 🔴 **Fire burns out in 30 min** - Should last 3 hours, burns 6x too fast

---

## Test Results

### ✅ SUCCESS: Game Starts Without Crashing

**Previous State**: Game crashed on startup with Stack Overflow (circular dependency)

**Fix Applied**: Changed `ParentLocation.GetTemperature()` to `Parent.Weather.TemperatureInFahrenheit` in 2 locations

**Test Result**: ✅ **WORKING**
- Game starts successfully
- No stack overflow errors
- Main menu displays correctly
- All systems accessible

**Verdict**: Circular dependency fix is **PRODUCTION READY**

---

### 🔴 FAILURE: Starting Materials Invisible

**Expected**: 3 Large Sticks + 2 Dry Grass visible on ground at game start

**Actual**: Materials added to `Location.Items` but not visible in "Look Around"

**Root Cause**:
- "Look Around" only shows items where `IsFound == true` (line 1559 of ActionFactory.cs)
- Our code added items with default `IsFound = false`

**Code Issue**:
```csharp
// BEFORE (BROKEN):
for (int i = 0; i < 3; i++) startingArea.Items.Add(ItemFactory.MakeStick());

// AFTER (FIXED):
for (int i = 0; i < 3; i++) {
    var stick = ItemFactory.MakeStick();
    stick.IsFound = true;  // Make visible
    startingArea.Items.Add(stick);
}
```

**Fix Status**: ✅ **IMPLEMENTED**

**Test Evidence**:
- Forage found 1 Large Stick (from ForageFeature, not our starting materials)
- "Look Around" showed only: Campfire (cold), Exits
- **NO items displayed** despite 5 items added to Location.Items
- Inventory had only foraged items (1 stick, 1 mushroom)

**Impact**:
- Player can't see or pick up guaranteed starting materials
- Defeats entire purpose of Fix #2
- Player back in unwinnable state

**Validation**: Will test after rebuild

---

### 🔴 FAILURE: Fire Burns Out in 30 Minutes

**Expected**: 3kg fire lasts 3 hours (180 minutes)

**Actual**: Fire completely cold with 0kg fuel after ~30 minutes

**Evidence**:
```
Time elapsed: ~30 minutes
Fire status: Cold (32°F), Status: Cold fire (no fuel), Capacity: 0.00/12.0 kg
```

**Math Check**:
- Starting fuel: 3.0kg softwood
- Burn rate: 1.0 kg/hr (from FuelType.cs)
- Expected duration: 3.0kg ÷ 1.0 kg/hr = **3 hours (180 min)**
- **Actual duration: < 30 minutes**
- **Burn rate actual: 6x too fast!**

**Possible Causes**:

**Cause A: Burn Rate Calculation Error**
```csharp
// HeatSourceFeature.Update() might be using wrong time units
// If treating "1 minute" as "1 hour", would burn 60x too fast
// But we observed 6x too fast, not 60x
```

**Cause B: Update() Called Multiple Times Per Minute**
```csharp
// If Update(TimeSpan.FromMinutes(1)) called 6x per game minute
// Would burn fuel 6x too fast
// Need to check World.Update() frequency
```

**Cause C: Fuel Not Added Correctly**
```csharp
// If campfire.AddFuel(startingFuel, 3.0) actually adds < 0.5kg
// Would explain rapid burnout
// Need to verify FuelMassKg after AddFuel()
```

**Cause D: Burn Rate Formula Wrong**
```csharp
// If burn rate formula in Update() uses wrong divisor
// e.g., dividing by 10 instead of 60 for minute-to-hour conversion
```

**Investigation Needed**:
1. Check `HeatSourceFeature.Update(TimeSpan)` implementation
2. Verify `campfire.AddFuel(startingFuel, 3.0)` actually adds 3kg
3. Check how many times `Location.Update()` is called per game minute
4. Review burn rate calculation in fuel consumption code

**Impact**:
- Game still unplayable
- 30 minutes < time to gather materials and craft fire-making tools
- Combined with invisible materials = double failure

---

## Temperature Progression (30 Minutes)

| Time  | Body Temp | Change   | Status |
|-------|-----------|----------|---------|
| T=0   | 98.6°F    | -        | Normal  |
| T=15  | ~90°F     | -8.6°F   | Cool (estimated) |
| T=30  | 80.1°F    | -18.5°F  | Cold    |

**Rate**: -0.62°F per minute (similar to previous playtest)

**Comparison to Previous Playtest**:
- Previous (no fire): 98.6°F → 69.4°F in 45 min (-29.2°F)
- Current (fire burned out): 98.6°F → 80.1°F in 30 min (-18.5°F)
- **Fire provided minimal benefit** (burned out too fast)

---

## Systems Tested

### ✅ Working Systems
1. **Game startup** - No crashes
2. **Foraging** - Found items, XP gain works
3. **Dynamic menu** - "Craft Items" and "Open inventory" appear when appropriate
4. **Fire UI** - Shows fire status (Cold, fuel remaining, capacity)
5. **Temperature calculation** - No circular dependency errors

### 🔴 Broken Systems
1. **Starting materials display** - Items not visible
2. **Fire duration** - Burns 6x too fast
3. **Look Around UI** - Doesn't show items unless `IsFound = true`

### ⏳ Not Tested (Blocked)
1. **Fire temperature progression** - Fire burned out before testing
2. **Fuel type restrictions** - Couldn't test without active fire
3. **Fire phases** - Never saw Igniting/Building/Roaring phases
4. **Material sufficiency** - Couldn't pick up starting materials

---

## Critical Bugs Documented

### Bug #1: Starting Materials Not Visible
- **File**: Program.cs lines 68-79 (fixed)
- **Issue**: `IsFound = false` by default
- **Fix**: Set `IsFound = true` after creating items
- **Status**: ✅ Fixed, needs validation

### Bug #2: Fire Burns 6x Too Fast
- **Severity**: CRITICAL - Game-breaking
- **Expected**: 3kg fire = 3 hours
- **Actual**: 3kg fire = 30 minutes
- **Impact**: Makes all survival impossible
- **Status**: 🔴 **ACTIVE**, investigation needed

---

## Next Steps (Priority Order)

### IMMEDIATE (Before Anything Else)

**1. Investigate Fire Burn Rate Bug** 🔴 **CRITICAL**
   - Read `HeatSourceFeature.Update(TimeSpan)` implementation
   - Check fuel consumption calculation
   - Verify `AddFuel()` actually adds correct mass
   - Check if `Location.Update()` is being called too frequently

**2. Validate IsFound Fix**
   - Restart game with fixed code
   - Check if 5 items visible in "Look Around"
   - Verify can pick up all items
   - Confirm inventory shows correct materials

### SHORT TERM (This Session)

**3. Fix Fire Burn Rate**
   - Apply fix based on investigation
   - Rebuild and test
   - Verify 3kg fire lasts 3 hours

**4. Run Full Validation Playtest**
   - Test 2 hours in-game time
   - Gather materials
   - Craft fire-making tools
   - Restart fire before it burns out
   - Measure survival rate

### MEDIUM TERM (Next Session)

**5. Test Fire Temperature Features**
   - Fire phases (Igniting → Roaring → Dying)
   - Fuel type progression
   - Temperature requirements
   - Heat output calculations

---

## Playtest Metrics

**Time Spent**: 30 minutes real-time, ~30 minutes in-game
**Systems Tested**: 5 (startup, foraging, fire, UI, temperature)
**Critical Bugs Found**: 2
**Bugs Fixed During Session**: 1 (IsFound)
**Bugs Remaining**: 1 (fire burn rate)

---

## Updated Success Criteria

**Critical Fixes**:
- ✅ Circular dependency crash - **FIXED**
- ⚠️ Starting materials - **FIXED (needs validation)**
- 🔴 Fire duration - **BROKEN (needs investigation)**

**Game Playability**:
- ❌ Cannot survive without fire lasting 3 hours
- ❌ Starting materials not accessible yet
- ❌ Still in unwinnable state

**Recommendation**:
1. **Investigate fire burn rate immediately**
2. **Fix and rebuild**
3. **Run quick validation (15 min)**
4. **Then run full 2-hour playtest**

---

## Code Changes Made During Playtest

### Change #1: IsFound Fix
**File**: `Program.cs` lines 68-79

**Before**:
```csharp
for (int i = 0; i < 3; i++) startingArea.Items.Add(ItemFactory.MakeStick());
for (int i = 0; i < 2; i++) startingArea.Items.Add(ItemFactory.MakeDryGrass());
```

**After**:
```csharp
for (int i = 0; i < 3; i++) {
    var stick = ItemFactory.MakeStick();
    stick.IsFound = true;
    startingArea.Items.Add(stick);
}
for (int i = 0; i < 2; i++) {
    var tinder = ItemFactory.MakeDryGrass();
    tinder.IsFound = true;
    startingArea.Items.Add(tinder);
}
```

**Build**: ✅ Successful

---

## Conclusion

**Partial Success**:
- Circular dependency fix works perfectly
- IsFound fix implemented (needs validation)

**Critical Failure**:
- Fire burns 6x too fast (30 min vs 3 hours)
- Starting materials invisible (fixed, needs testing)

**Game State**: Still unplayable until fire burn rate fixed

**Confidence**: Medium - One fix validated, one fix likely works, one critical bug blocks everything

**Next Action**: **Investigate HeatSourceFeature.Update() to find burn rate bug**
