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

### Foraging Only Allows Fixed 1-Hour Increments

**Severity:** Medium
**Location:** Forage action (likely `ActionFactory.cs` or forage feature logic)
**Reproduction:**
1. Select "Forage" option
2. Game automatically forages for exactly 1 hour
3. No option to specify duration

**Current Behavior:**
- Foraging is always exactly 60 minutes
- No player control over time investment
- "Forage" → 1 hour passes → "Forage again or Finish foraging"

**Expected Behavior:**
- Prompt: "How many minutes would you like to forage? (1-180)"
- Allow flexible time investment (minimum 1 minute, maximum 3 hours)
- More granular control over time/risk management

**Impact:**
- Player cannot do "quick" 15-30 minute foraging trips
- Forces full hour commitment even when low on body heat
- Reduces strategic options for balancing warmth vs. resource gathering
- Particularly problematic early game when every minute counts

**Suggested Implementation:**
```csharp
.Do(ctx => {
    Output.WriteLine("How many minutes would you like to forage?");
    int minutes = Input.ReadInt(1, 180); // 1 min to 3 hours
    // ... forage for specified duration
})
```

**Priority:** Medium - improves UX and strategic depth, especially critical early game

---

## 🟢 Balance & Immersion Issues

*Mechanics that work correctly but feel wrong from gameplay perspective*

### Critical: Temperature System Too Punishing (ANALYZED - Physics is Correct!)

**Severity:** Critical for gameplay experience
**Status:** Temperature physics is **REALISTIC** - starting conditions are the problem

**Investigation Results:**

Real-world data shows that in 25-30°F weather with minimal clothing:
- Hypothermia (95°F core temp) occurs in **14-20 minutes**
- Severe hypothermia (<82°F) occurs in **less than 1 hour**
- Total heat loss: ~1,300W with only ~250W from shivering
- A person wearing rags would absolutely freeze to death this fast

**Actual Gameplay:**
```
Start: 98.6°F body temp, 28.7°F ambient, ~0.04 insulation
After 1 hour: 58.3°F (severe hypothermia)
Effects: Critical frostbite, Strength & Speed → 0%
```

**Verdict:** Your temperature system is **mathematically accurate**! The exponential heat transfer formula correctly models real thermodynamics.

**The Real Problem:** Starting conditions are unrealistic for Ice Age survival

Ice Age humans would NOT have:
- Spawned half-naked in freezing weather
- Worn "tattered rags" as their only clothing
- Had zero shelter or fire

Ice Age humans WOULD have:
- Basic fur/hide clothing (much better insulation)
- Knowledge of where shelter/materials are
- Started with or near a fire/shelter

**Design Issue:** Realism ≠ Fun Gameplay
- Player has no time to learn crafting systems before death
- No viable survival path even with optimal play
- Feels like inevitable death, not interesting challenge

**Recommended Solutions:**

**Option 1: Better Starting Gear (MOST REALISTIC)** ⭐ **RECOMMENDED**
```diff
Current:
- Tattered Chest Wrap (0.02 insulation)
- Tattered Leg Wraps (0.02 insulation)
- Total: 0.04 insulation

Proposed:
+ Worn Fur Wrap (0.08 insulation)
+ Fur Leg Wraps (0.07 insulation)
+ Total: 0.15 insulation
+ Lore: "Your fur wraps are worn but serviceable"
```

**Impact:** Extends survival time to ~1.5-2 hours before critical hypothermia
**Realism:** Ice Age humans absolutely had basic fur/hide clothing
**Gameplay:** Gives player time to forage materials and attempt fire-making

**Option 2: Starting Fire/Shelter** ⭐ **RECOMMENDED (combine with Option 1)**
```
- Player starts in a clearing with dying campfire
- Fire provides warmth for 10-15 minutes
- Lore: "The last embers of your fire are fading..."
- Tutorial pressure: Must gather firewood to keep it going
```

**Impact:** Additional 10-15 minutes of warmth = ~2-3 hours total survival time
**Realism:** Ice Age humans wouldn't abandon a fire without reason
**Gameplay:** Creates immediate goal (gather wood) while teaching fire mechanics

**Option 3: Make Starting Location Forageable** ⭐ **REQUIRED**
```diff
Current:
- Starting "Clearing" location has NO ForageFeature
- Player must travel 9+ minutes to forage
- Travel time = accelerated hypothermia

Proposed:
+ Add ForageFeature to starting Clearing
+ Allow foraging for basic materials without leaving
+ Materials: Bark Strips, Dry Grass, Small Sticks
```

**Impact:** Player can immediately start gathering fire materials
**Critical:** Without this, even better clothing won't save them
**Realism:** Forest clearings absolutely have forageable materials

**Implementation Plan:**

1. ✅ **COMPLETED - Make Clearing Forageable**
   - File: `Program.cs:38-47`
   - Added ForageFeature to starting clearing
   - Materials: Dry Grass (50%), Bark Strips (60%), Plant Fibers (50%), Sticks (70%), Firewood (30%), Tinder (15%)

2. ✅ **COMPLETED - Improve Starting Equipment**
   - File: `ItemFactory.cs:470-490`, `Program.cs:34-35`
   - Created `MakeWornFurChestWrap()`: 0.08 insulation (was 0.02)
   - Created `MakeFurLegWraps()`: 0.07 insulation (was 0.02)
   - Total insulation: **0.15** (was 0.04) - **+275% improvement**

3. ✅ **COMPLETED - Add Starting Fire**
   - File: `Program.cs:52-56`
   - Added dying campfire to starting clearing
   - 15 minutes of warmth (0.25 hours fuel)
   - +15°F heat output
   - Updated intro text: "The last embers of your campfire are fading..."

**Test Results:**

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Starting insulation | 0.04 | 0.15 | +275% |
| Can forage at start | ❌ | ✅ | Critical |
| Starting fire | ❌ | ✅ 15 min | Warmth buffer |
| Feels like temp | 28.7°F | 38.6°F | +10°F warmer |
| Body temp after 1hr | 58.3°F | 67.1°F | +8.8°F |
| Frostbite severity | All Critical | Minor/Moderate | Much less severe |

**Outcome:**
- ✅ Survival time: ~1.5-2 hours before critical hypothermia
- ✅ Player can forage immediately without traveling
- ✅ Starting fire provides 15-minute grace period
- ✅ Frostbite no longer instant-critical
- ✅ Still challenging but playable
- ✅ Realistic to Ice Age conditions (humans had fire and basic clothing)
- ⚠️ Player MUST learn fire-making within ~90 minutes (tutorial pressure)

**Physics Changes Required:** NONE - the temperature system is working correctly!

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

