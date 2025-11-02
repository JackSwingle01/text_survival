# Known Issues - Text Survival RPG

**Last Updated:** 2025-11-01
**Testing Context:** Phase 1-3 implementation of crafting & foraging overhaul

---

## 🔴 Breaking Exceptions

*Critical errors that cause crashes or prevent gameplay*

None identified yet.

---

## 🟠 Bugs

*Incorrect behavior that prevents intended functionality*

### ~~Energy Depletes to 0% Instantly~~

**Status:** ❌ **FALSE ALARM** - Energy works correctly (gradual depletion over time)

**What happened:** Multiple background test processes caused file I/O conflicts that showed incorrect state. Energy actually depletes properly: 83% → 82% → 81% → 74% over 1 hour of gameplay.

**Lesson learned:** Kill all background processes before testing, only run one game instance at a time.

---

### Game State Inconsistencies During Navigation

**Severity:** Medium
**Location:** Action system / menu navigation
**Reproduction:**
- During testing, after selecting "Check Stats" (option 2), the game showed a different location with items on ground and wolves
- Menu options changed unexpectedly
- Attempting to interact with items showed "Invalid input. Please enter a number between 1 and 3" despite displaying 15 options

**Expected:** Menus should navigate predictably
**Actual:** Game state appears to jump or roll back unpredictably

**Note:** This may be related to TestModeIO file I/O timing issues rather than core game logic

---

## 🟡 Questionable Functionality

*Behaviors that work but may not be intended or optimal*

### "Press any key to continue..." Requires Actual Input

**Severity:** Low
**Location:** `Input.cs` / `Output.cs`
**Description:** The "Press any key to continue..." prompt requires user input (any character), but when sending "ENTER" or empty string "", the game interprets it as invalid input for the next menu prompt.

**Current Behavior:**
- Game displays "Press any key to continue..."
- User sends "x" or any character
- Game continues to next screen

**Issue:** This causes confusion in automated testing and may not match player expectations

**Potential Solutions:**
1. Change prompt to "Press any key to continue..." and accept any input including empty
2. Remove the pause entirely in TEST_MODE
3. Use a different Input method that doesn't validate as a number

---

### Sleep Option Disappears When Exhausted

**Severity:** Low
**Location:** Action menu generation
**Reproduction:**
1. Player reaches 0% energy
2. Main menu shows only: "1. Look around", "2. Check Stats", "3. Go somewhere else"
3. Sleep option (normally option 3) is missing

**Expected:** Player should be able to sleep when exhausted
**Actual:** Sleep option is hidden, forcing exhausted player to navigate elsewhere

**Impact:** Creates frustrating UX where exhausted player cannot immediately rest

---

## 🟢 Balance & Immersion Issues

*Mechanics that work correctly but feel wrong from gameplay perspective*

### Critical: Temperature System Too Punishing

**Severity:** Critical for gameplay experience
**Context:** Phase 1-3 implementation changed starting equipment to minimal clothing

**Problem:**
- Starting equipment: Tattered Chest Wrap + Tattered Leg Wraps (~0.04 total insulation)
- Ambient temperature: 28.7°F (cold but not extreme)
- Player body temperature drops from 98.6°F → 91.5°F in seconds
- Player gets frostbite and hypothermia almost immediately
- Strength and Speed drop to 0% within minutes

**Current Starting Conditions:**
```
Starting vitals: 50% food, 75% water, 83% energy, 98.6°F temp
Ambient temp: 28.7°F
Equipment: ~0.04 insulation (very minimal)
Time to critical cold effects: < 5 minutes of gameplay
```

**Balance Issues:**
1. **Player cannot survive long enough to gather fire materials** - Even rushing to forage and craft fire, hypothermia sets in before success
2. **No viable survival path** - Player needs fire immediately, but:
   - Hand Drill has 30% success rate at skill 0
   - Takes 20 minutes to attempt
   - Failure consumes materials (realistic but punishing)
   - Multiple attempts needed = multiple foraging trips
   - Each action costs time = more cold damage
3. **Death spiral is too fast** - Once cold effects start, they compound exponentially

**Immersion Impact:**
- Player feels helpless rather than challenged
- No time to explore the crafting system
- Forces optimal play path (rush fire) with no room for experimentation
- Doesn't feel like "survival challenge" but rather "inevitable death"

**Suggested Solutions (pick one or combine):**
1. **Increase starting insulation** - Give player slightly better wraps (0.08-0.10 insulation)
2. **Warmer starting weather** - Start at 35-40°F instead of 28°F
3. **Slower cold damage progression** - Reduce exponential cold damage multipliers
4. **Higher fire-making success rate** - Increase Hand Drill base success to 50%
5. **Starting fire source** - Player starts with dying campfire (10 minutes of warmth)
6. **Grace period on cold effects** - Delay severe hypothermia/frostbite for first 30 mins

**Recommended Fix:** Combination of #1 (better starting wraps), #3 (slower cold progression), and #6 (grace period) to allow player time to learn the systems before facing death.

---

### Time Passage During Menu Navigation

**Severity:** Medium
**Description:** Time appears to pass significantly during menu actions (looking around, checking inventory, etc.)

**Observed:**
- Looking around clearing
- Opening container
- Taking items
- Each action appears to cost 5-15 minutes of game time

**Balance Concern:**
- Menu actions should be "free" or very low cost
- Navigation shouldn't cause starvation/hypothermia
- Player is punished for interacting with game systems

**Expected Behavior:**
- Looking around: instant or 1 minute
- Opening containers: instant or 30 seconds
- Taking items from ground: 1-2 minutes per item
- Checking stats: instant

---

### Forage Success Rates Untested

**Severity:** Low (informational)
**Status:** Not yet tested due to energy/temperature blocking gameplay

**Needs Testing:**
- Forest biome material rates (bark 70%, tinder 20%, fibers 60%)
- Cave biome restrictions (stones only, NO organics)
- Riverbank, Plains, Hillside rates
- Whether rates feel balanced for gameplay loop

---

### Fire-Making Skill Checks Untested

**Severity:** Low (informational)
**Status:** Not yet tested due to inability to survive long enough to gather materials

**Needs Testing:**
- Hand Drill: 30% base + skill progression
- Material consumption on failure
- XP gain (1 XP on fail, skill+2 on success)
- Success chance display in crafting menu
- Whether 30% feels too punishing vs. engaging

---

## 📋 Testing Blockers

**Primary Blocker:** Temperature system too punishing - Strength & Speed drop to 0% within 1 hour, making game unplayable

**Impact:** Cannot meaningfully test crafting/foraging systems when player is crippled by hypothermia/frostbite before gathering materials

**Recommendation:** Adjust temperature balance first, then resume comprehensive testing of:
1. Foraging system (material rates, biome restrictions)
2. Fire-making recipes (success rates, material consumption, XP)
3. Crafting progression (tool recipes in Phase 4+)
4. Overall survival loop balance

---

## 🔧 Testing Notes

### Test Mode Script (`play_game.sh`)

**Status:** ✅ Fixed and working correctly

**Changes Made:**
- Clears output file before each command (prevents stale output)
- Clears input file before sending (prevents command queueing)
- Waits for READY state before sending commands
- No command queueing allowed

**Result:** Clean, predictable test interactions

---

## Priority Recommendations

1. **[CRITICAL]** Rebalance temperature system or starting conditions (see detailed suggestions in Balance section)
2. **[HIGH]** Test and document foraging success rates once playable
3. **[HIGH]** Test fire-making mechanics and skill progression once playable
4. **[MEDIUM]** Review game state navigation issues during TEST_MODE
5. **[LOW]** Improve "Press any key" handling for TEST_MODE
6. **[LOW]** Fix sleep option visibility when exhausted

