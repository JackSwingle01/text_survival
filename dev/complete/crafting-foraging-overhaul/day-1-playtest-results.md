# Day-1 Survival Path Playtest Results

**Date:** 2025-11-02
**Test Duration:** 4 hours in-game time
**Outcome:** ❌ **FAILED** - Cannot complete day-1 survival path
**Status:** 🔴 **CRITICAL BALANCE ISSUES FOUND**

---

## Executive Summary

The day-1 survival path is **NOT VIABLE** due to three critical issues:

1. **🔴 BLOCKER:** Forest biome does not spawn River Stones, preventing Sharp Rock crafting
2. **🔴 CRITICAL:** Player reaches complete incapacitation (0% Strength/Speed) after only 4 hours
3. **🔴 BALANCE:** Temperature system too punishing even with improved starting conditions

**Verdict:** Game is currently unplayable for new players. Major design changes required.

---

## Phase 1: Starting Conditions ✅

**Objective:** Verify starting gear and conditions

**Results:**
- ✅ Starting gear present: Fur wraps providing 75% Cold Resistance
- ✅ Starting campfire visible: "Campfire (dying)" at location
- ✅ Starting location forageable: Clearing has ForageFeature
- ✅ Initial stats reasonable:
  - Body temp: 95.7°F
  - Feels-like: 38.7°F (campfire +15°F bonus)
  - Food: 49% (Peckish)
  - Water: 74% (Fine)
  - Energy: 83% (Alert)

**Initial Effects:**
- Shivering: 43% (Moderate) - Already present at start
- Hypothermia: 1% (Minor) - Cold pressure immediate

**Assessment:** Starting conditions are correct per design, but player is already cold from minute 1.

---

## Phase 2: Foraging (4 hours) ⚠️ PARTIAL FAILURE

**Objective:** Gather materials for Sharp Rock, fire-making, and Windbreak

### Foraging Results by Hour

**Hour 1:**
- Found: Dry Grass (1), Plant Fibers (1), Firewood (1)
- Leveled to Foraging 1
- Cold messages: 14 occurrences

**Hour 2:**
- Found: Nothing (diminishing returns)
- Cold messages: 12 occurrences

**Hour 3:**
- Found: Nothing
- Cold messages: 9 occurrences

**Hour 4:**
- Found: Dry Grass (1)
- Cold messages: 19 occurrences (increasing!)

### Total Materials Gathered

| Material | Quantity | Weight | Required For |
|----------|----------|--------|--------------|
| Dry Grass | 2 | 0.04kg | Fire-making (Tinder) |
| Plant Fibers | 1 | 0.02kg | Binding/Windbreak |
| Firewood | 1 | 1.5kg | Fire-making/Windbreak |
| **River Stone** | **0** | **0kg** | **Sharp Rock (BLOCKED)** |

### Critical Finding: NO RIVER STONES

**🔴 BLOCKER:** Forest biome (Clearing) does not spawn River Stones.

**Impact:**
- Cannot craft Sharp Rock (requires 2x River Stone)
- Blocks entire tool progression
- Player must travel to Riverbank biome
- But travel is impossible (see Phase 2 survival stats below)

**Biome Design Issue:**
- Forest biome: Abundant organics ✓
- Forest biome: Fire materials ✓
- Forest biome: **NO stones** ❌

**Problem:** Sharp Rock is essential for day-1 survival (cutting tool), but requires materials not available in starting biome.

---

## Phase 2: Survival Stats (After 4 Hours) 🔴 CRITICAL FAILURE

**Objective:** Monitor survival stats during foraging

### Stats Progression

| Metric | Start | After 4 Hours | Change |
|--------|-------|---------------|--------|
| Body Temp | 95.7°F | 50.9°F | -44.8°F ⬇️ |
| Feels-Like | 38.7°F | 39.5°F | +0.8°F |
| Food | 49% | 34% | -15% ⬇️ |
| Water | 74% | 57% | -17% ⬇️ |
| Energy | 83% | 57% | -26% ⬇️ |

### Capabilities (After 4 Hours)

| Stat | Start | After 4 Hours | Status |
|------|-------|---------------|--------|
| Strength | 59% | **0%** | 🔴 Critical |
| Speed | 59% | **0%** | 🔴 Critical |
| Vitality | 92% | 60% | Fair |
| Perception | 100% | 100% | Excellent |
| Cold Resistance | 75% | 75% | Good |

### Active Effects (After 4 Hours)

| Effect | Severity | Location |
|--------|----------|----------|
| Shivering | 100% Critical | Core |
| Hypothermia | 100% Critical | Core |
| Frostbite | 100% Critical | Left Arm |
| Frostbite | 100% Critical | Right Arm |
| Frostbite | 100% Critical | Left Leg |
| Frostbite | 100% Critical | Right Leg |

**🔴 COMPLETE INCAPACITATION:**
- Strength: 0% - Cannot fight, cannot carry heavy items
- Speed: 0% - Cannot move effectively, cannot flee
- Body temp: 50.9°F - Severe hypothermia (real-world: death territory)
- All extremities: 100% Critical frostbite

**Assessment:** Player is completely incapacitated after only 4 hours, despite:
- Starting with improved fur wraps (0.15 insulation)
- Starting campfire providing warmth
- Forageable starting location
- 75% Cold Resistance

---

## Phase 3: Craft Sharp Rock ❌ BLOCKED

**Objective:** Craft Sharp Rock as first tool

**Result:** ❌ **BLOCKED - Missing Materials**

**Required Materials:**
- River Stone: 2x (0.8kg total)

**Available Materials:**
- River Stone: 0x ❌

**Blockers:**
1. Forest biome does not spawn River Stones
2. Must travel to Riverbank biome
3. Player incapacitated (0% Strength/Speed) - cannot travel
4. Travel time would cause further temperature drop

**Status:** Cannot proceed with day-1 survival path.

---

## Phase 4-5: Fire-Making & Windbreak ❌ NOT TESTED

**Result:** ❌ **NOT TESTED - Player Incapacitated**

**Reason:** After 4 hours:
- 0% Strength/Speed = complete incapacitation
- Cannot craft (requires functional limbs)
- Cannot gather more materials
- Cannot build shelter

**Materials Available:**
- Fire-making: Have some Tinder (0.04kg) + Firewood (1.5kg) - PARTIAL
- Windbreak: Have some materials but insufficient

**Status:** Testing halted due to incapacitation.

---

## Critical Issues Found

### 🔴 Issue #1: Sharp Rock Materials Not in Starting Biome

**Severity:** CRITICAL BLOCKER
**Issue:** River Stones do not spawn in Forest biome (Clearing)
**Impact:** Cannot craft Sharp Rock, blocks entire tool progression

**Root Cause:**
- Biome design separates stone materials (Riverbank) from organic materials (Forest)
- Day-1 survival path assumes Sharp Rock is craftable
- But Sharp Rock requires stones not available in starting biome

**Possible Solutions:**

**Option A: Add Stones to Forest Biome** ⭐ RECOMMENDED
- Add River Stones to Forest ForageFeature (low spawn rate: 0.3)
- Maintains biome specialization (Riverbank has MORE stones)
- Allows day-1 Sharp Rock crafting without forced travel
- Quick fix, minimal disruption

**Option B: Alternative Day-1 Tool**
- Create "Sharp Stick" recipe: Wood + grinding → sharp point
- No stones required
- Less effective than Sharp Rock
- Preserves biome specialization

**Option C: Change Starting Biome to Riverbank**
- Player starts at Riverbank instead of Forest
- Has access to both organics and stones
- But Riverbank has limited wood/fire materials (opposite problem)

**Recommendation:** **Option A** - Add stones to Forest at low spawn rate

---

### 🔴 Issue #2: Temperature Balance Still Too Harsh

**Severity:** CRITICAL - Game Unplayable
**Issue:** Player reaches complete incapacitation in 4 hours even with improvements

**Current Balance:**
- Starting insulation: 0.15 (3.75x better than old)
- Starting campfire: +15°F for 15 minutes
- Starting cold resistance: 75%
- **Result:** 4 hours to 0% Strength/Speed

**Problem:** Insufficient time to:
1. Learn crafting system (new players need exploration time)
2. Gather materials (need 6-8 hours of foraging for full progression)
3. Attempt fire-making multiple times (30% base success = avg 3-4 attempts)
4. Build shelter
5. Explore game mechanics

**Comparison:**
- Real-world: Severe hypothermia in 30-60 min (realistic)
- Gameplay: Needs 6-8 hours for viable day-1 path
- **Gap:** 4 hours is middle ground but still insufficient

**Possible Solutions:**

**Option A: Longer-Lasting Starting Fire** ⭐ RECOMMENDED
- Increase starting campfire fuel: 15 min → 60-90 min
- Provides grace period for learning
- Still creates urgency (fire dies eventually)
- Realistic: Ice Age humans would maintain fires

**Option B: Higher Insulation**
- Starting insulation: 0.15 → 0.25
- Extends survival time to ~6-8 hours
- May feel too easy mid-game

**Option C: Slower Frostbite Progression**
- Reduce frostbite severity accumulation rate
- Temperature physics unchanged
- Still harsh but more playable

**Option D: Combination Approach** ⭐ **MOST RECOMMENDED**
- Longer starting fire (60 min)
- Slightly higher insulation (0.20)
- Slower frostbite progression (50% slower)
- **Result:** ~8-10 hours survival time = viable day-1 path

**Recommendation:** **Option D** - Multi-pronged balance adjustment

---

### 🟠 Issue #3: Foraging Spawn Rates - Diminishing Returns Too Aggressive

**Severity:** Medium
**Issue:** Hours 2-3 yielded nothing, only Hour 4 found 1 item

**Observations:**
- Hour 1: 3 items + level up (good)
- Hour 2: Nothing (harsh)
- Hour 3: Nothing (harsh)
- Hour 4: 1 item (minimal)

**Problem:** Players may give up after 2-3 hours of nothing

**Suggestion:**
- Reduce diminishing returns slightly
- Ensure at least 1 item every 2 hours (even if common)
- Maintains scarcity without feeling futile

---

## Recommendations

### Immediate (Required for Playability)

1. **🔴 Add River Stones to Forest Biome**
   - Spawn rate: 0.3 (low but obtainable)
   - Weight: 0.4kg each
   - Allow day-1 Sharp Rock crafting

2. **🔴 Increase Starting Campfire Duration**
   - Current: 15 minutes (0.25 hours)
   - Proposed: 60 minutes (1.0 hours)
   - Provides grace period for learning

3. **🔴 Slow Frostbite Progression**
   - Reduce severity accumulation rate by 50%
   - Temperature physics unchanged
   - Extends playable time to ~8 hours

### Short-Term (Balance Improvements)

4. **🟠 Increase Starting Insulation Slightly**
   - Current: 0.15
   - Proposed: 0.20
   - Combined with other changes = ~8-10 hours survival

5. **🟠 Adjust Foraging Diminishing Returns**
   - Guarantee minimum 1 item per 2 hours
   - Prevents futile feeling

6. **🟠 Test Riverbank Starting Location**
   - Alternative starting biome
   - Has stones + some organics
   - Compare viability

### Long-Term (Design Iteration)

7. **🟡 Create Alternative Day-1 Tools**
   - Sharp Stick (no stones required)
   - Gives players options if no stones found

8. **🟡 Tutorial/Hint System**
   - Guide players to Riverbank for stones
   - Explain biome specializations
   - Reduce trial-and-error frustration

---

## Success Criteria Assessment

**Minimum Viable Day-1 Path:**
1. ❌ Player survives 6+ hours - FAILED (4 hours to incapacitation)
2. ⚠️ Can forage essential materials - PARTIAL (no stones)
3. ❌ Can craft Sharp Rock - BLOCKED (no River Stones)
4. ❌ Can make fire - NOT TESTED (player incapacitated)
5. ❌ Can build Windbreak - NOT TESTED (player incapacitated)
6. ❌ Fire + Windbreak provide enough warmth - NOT TESTED

**Result:** 0/6 criteria met. Day-1 path is not viable.

---

## Conclusion

The day-1 survival path is currently **UNPLAYABLE** due to:

1. Missing critical materials (River Stones) in starting biome
2. Temperature balance too harsh even after improvements
3. Insufficient time to learn systems before incapacitation

**Priority Actions:**
1. Add River Stones to Forest biome (BLOCKER)
2. Extend starting fire duration to 60 min (CRITICAL)
3. Slow frostbite progression by 50% (CRITICAL)

**Estimated Fix Time:** 1-2 hours implementation + re-test

**Re-Test Required:** Yes - Full day-1 playtest after balance changes

---

## Phase 9 Task 40 Status

**Task 40: Test day-1 survival path**
**Status:** ⚠️ **FAILED - Critical Issues Found**
**Result:** Cannot complete due to blockers

**Next Steps:**
1. Implement critical fixes (stones, fire duration, frostbite)
2. Re-run day-1 playtest
3. Verify full progression: forage → Sharp Rock → fire → windbreak
4. Document viable day-1 path

**Task Completion:** 0% (blocked by critical issues)

---

**Test Conducted By:** Claude Code (Architecture Review)
**Test Date:** 2025-11-02
**Saved To:** `dev/active/crafting-foraging-overhaul/day-1-playtest-results.md`
