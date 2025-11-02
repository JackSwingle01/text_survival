# Day-1 Survival Path Playtest Plan

**Date:** 2025-11-02
**Objective:** Verify complete day-1 survival progression from spawn to basic establishment
**Duration:** Expected 2-3 hours of testing
**Game Time:** Target ~6-8 hours in-game

---

## Test Phases

### Phase 1: Starting Conditions Verification (5 min)
**Objectives:**
- ✓ Verify starting gear (Worn Fur Chest Wrap 0.08 + Fur Leg Wraps 0.07 = 0.15 insulation)
- ✓ Verify starting campfire present and visible (15 min fuel, +15°F)
- ✓ Verify starting location (Clearing) has ForageFeature
- ✓ Check initial stats (temperature, food, water, energy)

**Expected Results:**
- Body temp: 98.6°F
- Feels-like temp: ~38-40°F (with campfire bonus)
- Food: ~50%
- Water: ~75%
- Energy: ~85%

---

### Phase 2: Early Foraging (1-2 hours)
**Objectives:**
- Forage for 2-3 hours to gather essential materials
- Test material availability and spawn rates
- Monitor survival stats during foraging

**Target Materials:**
- River Stones: 2+ (for Sharp Rock)
- Dry Grass/Tinder: 0.1kg+ (for fire-making)
- Wood materials: 0.5kg+ (sticks, branches)
- Binding materials: 0.1kg+ (bark strips, plant fibers)

**Monitoring:**
- Body temperature progression
- Frostbite severity
- Food/water/energy depletion
- Message batching working correctly

---

### Phase 3: Craft Sharp Rock (10 min)
**Objectives:**
- Verify Sharp Rock recipe available
- Test crafting: 2x River Stone → Sharp Rock
- Confirm 100% success rate (no skill check)
- Verify Sharp Rock provides cutting capability

**Recipe Requirements:**
- 2x River Stone (0.4kg each = 0.8kg total)
- Crafting time: 5 minutes
- Success rate: 100%
- Result: Sharp Rock tool (cutting capability)

**Validation:**
- Recipe appears in crafting menu
- Preview shows correct materials
- Crafting succeeds on first attempt
- Sharp Rock appears in inventory

---

### Phase 4: Fire-Making Attempts (30-60 min)
**Objectives:**
- Test fire-making progression (Hand Drill → Bow Drill if materials available)
- Verify skill check system (30% base, +10% per level)
- Test failure mechanics (XP gain, material consumption)
- Achieve successful fire before hypothermia

**Hand Drill Fire:**
- Requirements: 0.5kg Wood + 0.1kg Tinder
- Success rate: 30% (Firecraft 0) → 40% (Firecraft 1) → 50% (Firecraft 2)
- Time: 20 minutes per attempt
- Expected attempts: 2-4 before success

**Monitoring:**
- Firecraft skill progression
- Material consumption accuracy
- Fire creation and warmth restoration
- Body temperature before/after fire

---

### Phase 5: Build Windbreak Shelter (40 min)
**Objectives:**
- Test Windbreak recipe (Tier 1 shelter)
- Verify LocationFeature creation at current location
- Test warmth bonus (+2°F)
- Confirm crafting time (30 minutes)

**Windbreak Recipe:**
- Requirements: 1.5kg Wood + 0.5kg Grass/Plant material
- Crafting time: 30 minutes
- Result: LocationFeature at current location (+2°F warmth)
- Success rate: 100%

**Validation:**
- Recipe appears after gathering materials
- Preview shows correct consumption
- Windbreak appears in location after crafting
- "Look around" shows windbreak
- Temperature bonus applies

---

## Success Criteria

**Minimum Viable Day-1 Path:**
1. ✅ Player survives 6+ hours without incapacitation
2. ✅ Can forage essential materials (stones, wood, tinder, binding)
3. ✅ Can craft Sharp Rock (basic tool)
4. ✅ Can successfully make fire within 4-5 attempts (realistic skill progression)
5. ✅ Can build Windbreak shelter for protection
6. ✅ Fire + Windbreak provide enough warmth to survive night

**Stretch Goals:**
- Survive 8+ hours
- Craft additional tools (spear, knife)
- Build Tier 2 shelter (Lean-to)
- Achieve Firecraft level 2-3

---

## Key Metrics to Track

### Survival Stats (hourly)
- Body temperature (°F)
- Frostbite severity (per extremity)
- Food % remaining
- Water % remaining
- Energy % remaining

### Crafting Success
- Materials gathered vs. needed
- Crafting attempts vs. successes
- Time spent crafting
- Skill levels achieved

### Balance Issues
- Material spawn rates (too scarce/abundant?)
- Fire-making difficulty (too hard/easy?)
- Survival time pressure (enough time to learn systems?)
- Warmth sources adequate (fire + windbreak sufficient?)

---

## Documentation

**Record for each phase:**
- Time elapsed (real and in-game)
- Stats before/after
- Materials gathered/consumed
- Successes and failures
- Player experience (frustration vs. engagement)
- Balance observations

**Final Report:**
- Complete timeline of events
- Balance recommendations
- Bug discoveries
- UX friction points
- Phase 9 Task 40 completion status

---

**Test Start:** Awaiting execution
**Test End:** TBD
**Result:** TBD
