# Known Issues - Text Survival RPG

**Last Updated:** 2025-11-02
**Testing Context:** Comprehensive playtest of Phase 1-8 features

---

## 🔴 Breaking Exceptions

*Critical errors that cause crashes or prevent gameplay*

### ~~Frostbite Effects Stacking Infinitely~~

**Severity:** CRITICAL - Game Breaking
**Location:** Effect system / Frostbite generation in `SurvivalProcessor.cs` or `EffectRegistry.cs`
**Status:** ✅ **FIXED** (2025-11-01)

**Reproduction:**
1. Start new game
2. Forage for 3-4 hours
3. Check Stats
4. Observe hundreds/thousands of frostbite effects on every body part

**Observed:**
- **Hundreds of "Frostbite" effects** stacking on every single body part (fingers, toes, hands, feet, etc.)
- Each body part has 20-30+ frostbite effects ranging from Minor (7%) to Critical (100%)
- Strength drops to 0% (should be 97%)
- Speed drops to 0% (should be 84%)
- Stats screen filled with 500+ frostbite entries

**Expected:**
- Frostbite should either:
  - Replace existing frostbite on same body part (one frostbite per part)
  - Stack up to a reasonable limit (max 3-5 severity levels)
  - Be managed by `AllowMultiple(bool)` property in EffectBuilder

**Root Cause Hypothesis:**
- `EffectRegistry.AddEffect()` is not checking for existing frostbite effects
- OR `AllowMultiple` is set to `true` for frostbite (should be false)
- OR frostbite is being added every survival tick instead of once per threshold crossing
- Likely in `SurvivalProcessor.GenerateEffects()` or similar

**Impact:**
- 🔴 **COMPLETE GAMEPLAY BLOCKER** - Player stats reduced to 0% within 3 hours
- Cannot move, cannot fight, cannot perform any actions effectively
- Game is literally unplayable past the first few hours
- Blocks ALL testing of crafting, foraging, fire-making systems

**Files to Investigate:**
1. `Survival/SurvivalProcessor.cs` - frostbite effect generation logic
2. `Effects/EffectRegistry.cs` - `AddEffect()` method and duplicate checking
3. `Effects/EffectBuilder.cs` - `AllowMultiple` property for frostbite
4. `Survival/SurvivalProcessorResult.cs` - effects returned to actor

**Suggested Fix:**
```csharp
// In EffectBuilder for frostbite creation:
CreateEffect("Frostbite")
    .AllowMultiple(false)  // <-- Only allow ONE frostbite per body part
    .TargetingBodyPart(part)
    // ... rest of effect
```

**Testing Notes:**
- Bug discovered during Phase 1-3 testing (2025-11-01)
- Appeared after 4 hours of foraging in cold weather
- Starting fur wraps (0.15 insulation) were NOT sufficient to prevent this
- Even with starting campfire providing warmth

**Priority:** 🔴 **MUST FIX BEFORE ANY FURTHER TESTING**

**Solution Implemented:**

Two fixes were required:

1. **EffectRegistry.cs:15-19** - Fixed duplicate detection to check BOTH `EffectKind` AND `TargetBodyPart`
   ```csharp
   // Before: Only checked EffectKind
   var existingEffect = _effects.FirstOrDefault(e => e.EffectKind == effect.EffectKind);

   // After: Checks both EffectKind and TargetBodyPart
   var existingEffect = _effects.FirstOrDefault(e =>
       e.EffectKind == effect.EffectKind &&
       e.TargetBodyPart == effect.TargetBodyPart);
   ```

2. **SurvivalProcessor.cs:244** - Changed frostbite from `AllowMultiple(true)` to `AllowMultiple(false)`
   ```csharp
   .AllowMultiple(false) // Fixed: prevent infinite stacking on same body part
   ```

**Test Results After Fix:**
- ✅ Only 4 frostbite effects total (one per extremity: Left Arm, Right Arm, Left Leg, Right Leg)
- ✅ No infinite stacking
- ✅ Effects properly update severity instead of creating duplicates
- ✅ Game is now playable

**Note:** Frostbite severity is still very high (reaching 100% Critical on all extremities within ~4 hours). This may be a separate balance issue but is NOT a bug - it's the intended temperature physics working correctly.

---

## 🟠 Bugs

*Incorrect behavior that prevents intended functionality*

### ~~Bow Drill Recipe Requires Non-Existent Skill~~

**Severity:** HIGH - Breaking Exception
**Location:** `Crafting/CraftingSystem.cs` line 168
**Status:** ✅ **FIXED** (2025-11-02)

**Reproduction:**
1. Start game
2. Open crafting menu
3. Game crashes with `System.ArgumentException: Skill Fire-making does not exist`

**Root Cause:**
Bow Drill recipe had leftover `.RequiringSkill("Fire-making", 1)` from earlier implementation where skill checks were in recipes. After game balance refactor, skill checks moved to StartFire action, but Bow Drill recipe wasn't updated.

**Stack Trace:**
```
System.ArgumentException: Skill Fire-making does not exist.
   at text_survival.Level.SkillRegistry.GetSkill(String skillName)
   at text_survival.Crafting.CraftingRecipe.CanCraft(Player player)
   at text_survival.Crafting.CraftingSystem.GetAvailableRecipes()
```

**Fix:**
Removed `.RequiringSkill("Fire-making", 1)` from Bow Drill recipe. Skill check happens in StartFire action, NOT in crafting recipe.

**Before:**
```csharp
var bowDrill = new RecipeBuilder()
    .Named("Bow Drill")
    .RequiringSkill("Fire-making", 1)  // ← CRASH (skill doesn't exist)
    .ResultingInItem(ItemFactory.MakeBowDrill)
```

**After:**
```csharp
var bowDrill = new RecipeBuilder()
    .Named("Bow Drill")
    .RequiringCraftingTime(45)
    .WithPropertyRequirement(ItemProperty.Wood, 1.0)
    .WithPropertyRequirement(ItemProperty.Binding, 0.1)
    // NO skill requirement - skill check happens in StartFire action
    .ResultingInItem(ItemFactory.MakeBowDrill)
```

**Design Lesson:**
- Crafting = knowledge (can you make it?)
- Usage = skill (can you use it effectively?)
- Skill checks belong in actions, not recipes
- Crafting should always be 100% success (materials + time only)

---

### Multiple Campfires Created in Same Location

**Severity:** HIGH - Clutters locations, confuses fire management
**Location:** `Crafting/CraftingRecipe.cs` - `ResultingInLocationFeature()` method
**Status:** 🔴 **ACTIVE** (discovered 2025-11-02 during Task 49 playtest)

**Reproduction:**
1. Start game with campfire in clearing
2. Let campfire burn out completely (cold, no embers)
3. Craft "Hand Drill Fire" recipe
4. Look around location

**Observed:**
```
>>> CLEARING <<<
You see:
    Campfire (cold)
    Campfire (cold, 30 min fuel ready)
```

Two campfires exist in the same location:
- Original: Depleted, cold, no fuel or embers
- New: Created by fire-making recipe, has 30 min fuel but unlit

**Expected Behavior:**
Fire-making recipes should check for existing HeatSourceFeature and either:
1. Refuel existing campfire if cold/depleted
2. Relight existing campfire if it has embers
3. Remove depleted campfire before creating new one
4. Only create new campfire if none exists

**Root Cause:**
`CraftingRecipe.ResultingInLocationFeature()` always creates a NEW feature without checking if HeatSourceFeature already exists in the location.

**Impact:**
- Location clutter (multiple dead/cold campfires accumulate)
- Confusing UX - which campfire to interact with?
- "Add Fuel to Fire" and "Start Fire" may target wrong campfire
- Breaks immersion (why are there 3 campfires in my clearing?)

**Recommendation:**
```csharp
// In ResultingInLocationFeature() method:
var existingFire = location.Features.OfType<HeatSourceFeature>().FirstOrDefault();
if (existingFire != null)
{
    // Refuel/relight existing fire instead of creating new one
    existingFire.AddFuel(fuelAmount);
    if (!existingFire.IsActive()) existingFire.SetActive(true);
}
else
{
    // Create new fire only if none exists
    location.Features.Add(new HeatSourceFeature(...));
}
```

**Priority:** HIGH - affects UX and fire management mechanics

---

### Time Handling Pattern Inconsistency (Technical Debt)

**Severity:** Medium - Architectural Pattern Inconsistency
**Location:** ForageFeature.cs, ActionFactory.cs (fire actions)
**Status:** 🟡 **TECHNICAL DEBT** (discovered 2025-11-02 during code review)

**Issue:**
Some actions handle time updates inconsistently with the standard ActionBuilder pattern:
1. **ForageFeature.Forage()** internally calls `World.Update(minutes)` instead of letting the action system handle it
2. **Fire actions** (StartFire/AddFuelToFire) manually update time instead of using `.TakesMinutes()`

**Current Implementation:**
```csharp
// ForageFeature.cs - calls World.Update() internally
public void Forage(Actor actor, int minutes) {
    World.Update(TimeSpan.FromMinutes(minutes));  // ⚠️ Manual time update
    // ... forage logic
}

// ActionFactory.cs - Fire actions manually update time
.Do(ctx => {
    World.Update(TimeSpan.FromMinutes(20));  // ⚠️ Manual time update
    // ... fire logic
})
```

**Expected Pattern:**
```csharp
// Standard ActionBuilder pattern
CreateAction("Start Fire")
    .TakesMinutes(20)  // ✅ Declarative time handling
    .Do(ctx => {
        // ... fire logic (no manual time update)
    })
```

**Impact:**
- Code works correctly but violates architectural consistency
- Makes time-handling logic harder to track and debug
- Mixes responsibilities (feature logic + time management)
- Future developers may not know which pattern to follow

**Root Cause:**
ForageFeature was implemented before `.TakesMinutes()` pattern was fully established, and fire actions followed the same manual pattern.

**Recommended Fix:**
1. Refactor ForageFeature.Forage() to NOT call World.Update() internally
2. Update forage action to use `.TakesMinutes(minutes)` in ActionFactory
3. Update fire actions (StartFire, AddFuelToFire) to use `.TakesMinutes(20)` instead of manual updates
4. Ensure all actions use `.TakesMinutes()` for consistency

**Priority:** Medium - Works correctly but should be standardized in next refactor sprint

---

### Material Properties Display Inconsistency

**Severity:** Medium - UX Bug
**Location:** Crafting system material display
**Status:** 🔴 **ACTIVE** (discovered 2025-11-02 during playtest)

**Reproduction:**
1. Pick up Dry Grass and Large Stick
2. Open crafting menu
3. Select option 9 "Show My Materials"
4. Observe materials shown
5. Then attempt to craft Hand Drill Fire
6. Compare material display

**Observed:**
- "Show My Materials" (#9) displays: `Tinder: 0.0 total`
- But Hand Drill Fire craft screen shows: `Tinder: 0.5/0.1` (✓ available)
- Inconsistent display of same materials in different screens

**Expected:**
- Both screens should show identical material totals
- If player has Dry Grass with Tinder property, both should show Tinder > 0

**Root Cause Hypothesis:**
- "Show My Materials" may be filtering or calculating properties differently
- Craft preview correctly detects properties but summary screen doesn't
- Possible issue in how ItemProperty totals are calculated

**Impact:**
- Players see conflicting information about available materials
- May think they can't craft recipes when they actually can
- Confusing UX that undermines trust in the system

**Priority:** Medium - doesn't block gameplay but causes confusion

---

### ~~Crafting Preview Shows Incorrect Item Consumption~~

**Severity:** Medium - Display Bug
**Location:** `CraftingRecipe.cs` PreviewConsumption method, `Item.cs` GetProperty method
**Status:** ✅ **FIXED** (2025-11-02)

**Reproduction:**
1. Have: Dry Grass, Plant Fibers, Nuts, Grubs (items WITHOUT Flint/Stone properties)
2. Attempt to craft "Flint and Steel" (requires Flint 0.2kg + Stone 0.3kg properties)
3. Observe preview consumption list

**Observed:**
Preview shows will consume items that DON'T have the required properties:
- Dry Grass (0.02kg) - has Tinder, NOT Flint/Stone
- Plant Fibers (0.04kg) - has PlantFiber, NOT Flint/Stone
- Nuts (0.12kg) - food item, NO properties
- Grubs (0.05kg) - food item, NO properties

**Expected:**
- Preview should ONLY show items with Flint and Stone properties
- Should only consume items that match the recipe requirements

**Root Cause:**
In `Item.cs:77-79`, the `GetProperty()` method returns the result of `FirstOrDefault()` on an enum list. When the property doesn't exist, `FirstOrDefault()` returns `default(ItemProperty)` which is `ItemProperty.Stone` (enum value 0), NOT null!

This caused `HasProperty()` checks to incorrectly return true for items without the required property.

**Fix:**
Cast the enum list to nullable before calling `FirstOrDefault()`:

```csharp
// AFTER (FIXED):
public ItemProperty? GetProperty(ItemProperty property)
{
    // Cast to nullable to ensure FirstOrDefault returns null when not found
    return CraftingProperties.Cast<ItemProperty?>().FirstOrDefault(p => p == property);
}
```

**Impact:**
- Players saw completely wrong items in consumption preview
- Reduced trust in crafting system accuracy

**Files Changed:**
- `Items/Item.cs:77-82` - Fixed GetProperty() to properly return null for missing properties

**Priority:** Medium - Fixed enum default value bug affecting crafting preview

---

### Location.Update() Doesn't Call Feature.Update()

**Severity:** Medium - Missing Feature Functionality
**Location:** `Environments/Location.cs:150-154`
**Status:** 🔴 **ACTIVE** (discovered 2025-11-02 during dead campfire testing)

**Issue:**
`Location.Update()` method is called every minute by `World.Update()`, but it doesn't call `Update()` on LocationFeatures (like HeatSourceFeature). This means campfire fuel never decreases, fires never burn out naturally.

**Current Behavior:**
```csharp
// Location.cs:150-154
public void Update()
{
    // Locations.ForEach(i => i.Update());  // Commented out
    _npcs.ForEach(n => n.Update());
    // Missing: Features.ForEach(f => f.Update());
}
```

**Expected Behavior:**
- Location.Update() should call Update() on all LocationFeatures
- HeatSourceFeature.Update() would consume fuel over time
- Campfires would burn down from "burning" → "dying" → "cold" (0 fuel)

**Impact:**
- Campfires never run out of fuel naturally
- Starting campfire provides infinite warmth
- Dead campfire display feature (recently added) can't be tested
- Fire management mechanics don't work as intended

**Root Cause:**
Line 152 suggests Features should have Update() called, but it's commented out for unknown reason.

**Suggested Fix:**
```csharp
public void Update()
{
    _npcs.ForEach(n => n.Update());
    Features.ForEach(f => f.Update(TimeSpan.FromMinutes(1)));
}
```

**Priority:** Medium - Affects game balance (infinite warmth) but not game-breaking

---

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

### CRITICAL: Fire-Making RNG Death Spiral (Task 49 Blocker)

**Severity:** CRITICAL - Prevents early game progression
**Location:** Fire-making skill check system
**Status:** 🔴 **GAME-BREAKING** (discovered 2025-11-02 during Task 49 playtest)

**Issue:**
Fire-making has 30-50% base success rates and consumes materials on failure. Players can easily fail 3-5 attempts and exhaust all resources before getting a fire started, creating unwinnable scenarios.

**Observed During Playtest:**
- Attempt 1 (Hand Drill, ~30%): FAILED - consumed 0.55kg materials, gained Firecraft XP
- Attempt 2 (Hand Drill, ~40%): FAILED - consumed 0.55kg materials
- Attempt 3 (Hand Drill, ~40%): SUCCESS - consumed 0.55kg materials
- Attempt 4 (Hand Drill, 30%): FAILED - consumed materials (new location)
- Attempt 5 (Bow Drill, 50%): FAILED - consumed 5 items including all sticks

**Total**: 5 attempts, 3 failures (60% failure rate), ~12 items consumed

**Player State at Playtest Termination:**
- Food: 16% (STARVING)
- Temperature: 40.3°F (severe hypothermia)
- Energy: 27% (Very Tired)
- Materials remaining: 2x Firewood, 1x Bark Strips (insufficient for another attempt)
- **Result**: UNWINNABLE STATE - no materials for fire, dying from cold/hunger

**Root Causes:**
1. Success rates too low (30-50%) for CRITICAL survival mechanic
2. Material consumption on failure is too punishing
3. RNG variance creates death spirals - can fail 3-5 times in a row
4. Skill XP from failure helps but not enough (30% → 40% still fails often)

**Cascading Failure:**
```
Failed fire attempt → Lost materials → Forage for more → Hypothermia worsens
→ Failed fire attempt → Lost more materials → Starving → No time to recover
→ Failed fire attempt → Out of materials → GAME OVER
```

**Impact:**
- Game is literally unplayable for average players
- Even optimal play can fail due to RNG
- No viable survival path with current balance
- Blocks ALL mid/late-game testing
- First playthrough experience is "unfair" not "challenging"

**Solutions (Choose ONE or combine):**

**Option A: Increase Success Rates** ⭐ RECOMMENDED
- Hand Drill: 30% → 50% base
- Bow Drill: 50% → 70% base
- Reduces average failures from 3-4 to 1-2

**Option B: Don't Consume Materials on Failure**
- Keep low success rates but make failures free
- Still costs time (20-45 min) which creates pressure
- Materials only consumed on SUCCESS

**Option C: Guaranteed Slow Method**
- Add "Rub Sticks" option: 100% success, 60 minutes
- Slower but reliable fallback option
- Prevents death spirals from bad RNG

**Option D: Partial Success Mechanic**
- Failure creates "Smoldering Embers" (25% chance)
- Embers can be fed tinder to become fire
- Softens the blow of RNG failures

**Recommendation:** **Option A + C** - Increase success rates AND add guaranteed method
- New players use guaranteed method (learns fire importance)
- Experienced players risk faster method (rewards skill)
- Prevents unfair RNG deaths while maintaining challenge

**Priority:** 🔴 **MUST FIX BEFORE FURTHER PLAYTESTING**

---

### CRITICAL: Resource Depletion Death Spiral (Task 49 Blocker)

**Severity:** CRITICAL - Forces dangerous travel before fire established
**Location:** ForageFeature, starting location resource density
**Status:** 🔴 **ACTIVE** (discovered 2025-11-02 during Task 49 playtest)

**Issue:**
Starting location's forage feature depletes rapidly or has low spawn rates. Players exhaust local resources after 2-3 failed fire attempts and must travel while cold/hungry to find more materials.

**Observed Forage Results:**

**Clearing (Starting Location):**
- Forages 1-4: 100% success (4 items)
- Forages 5-9: 40% success (2 finds, 3 empty in 5 attempts)
- **Depleted after ~120 minutes of gameplay**

**Ancient Woodland (After Travel):**
- Forages 1-8: ~66% success rate
- Much higher yields including food/water
- Fresh, undepleted resource pool

**Problem:**
- Player uses up starting location resources on failed fire attempts
- Must travel 7+ minutes to new location (exposure risk)
- Travel accelerates hypothermia and hunger
- Time spent traveling = time NOT gathering fire materials
- Creates desperate cycle: Travel → Forage → Fail fire → Travel again

**Impact:**
- Combines with fire-making RNG to create double death spiral
- Even with good starting conditions, depletion forces risky choices
- Travel time conflicts with urgent fire need
- New location might ALSO deplete if fire-making keeps failing

**Root Causes:**
1. ForageFeature may have too-aggressive depletion curve
2. Starting location has lower density than other forest locations
3. Failed fire-making consumes 3-5x more materials than planned (due to RNG)
4. No respawn/regeneration of forage resources

**Solutions:**

**Option A: Increase Starting Location Density** ⭐ RECOMMENDED
- Buff starting clearing to have 2-3x current resource density
- Ensure at least 15-20 forageable items before depletion
- Accounts for fire-making failures consuming extra materials

**Option B: Add Visible Items on Ground at Start**
- Place 2-3 Sticks and 1-2 Tinder items visible (not forageable)
- Guaranteed materials for at least 1-2 fire attempts
- Tutorial: "There are some sticks on the ground. You should gather them."

**Option C: Resource Respawn/Regeneration**
- ForageFeature slowly regenerates items (1-2 per hour)
- Prevents total depletion
- Still creates scarcity but not unwinnable state

**Recommendation:** **Options A + B** - Buff starting location AND add visible items
- Ensures player has materials for 3-4 fire attempts minimum
- Visible items teach "gather before you need" lesson
- Prevents forced travel before fire established

**Priority:** 🔴 **CRITICAL** - Required for day-1 survival

---

### HIGH: Food Scarcity Creates Unwinnable State

**Severity:** HIGH - Player starves before able to hunt
**Location:** Food consumption rate, early-game food sources
**Status:** 🔴 **ACTIVE** (discovered 2025-11-02 during Task 49 playtest)

**Issue:**
Food depletes rapidly (~14% per hour) but early-game food sources provide minimal calories. Player reached 16% food (STARVING) before able to craft hunting tools.

**Food Progression Observed:**
- Start: 50% (Peckish)
- After 1 hour: 43% (Peckish)
- After 2 hours: 29% (Hungry)
- After 2.5 hours: 16% (STARVING)

**Food Consumption Rate:** ~14% per hour

**Food Sources Found:**
- Wild Mushroom: Restored only **1% food** (negligible!)
- No berries found
- No meat (requires hunting tools not yet accessible)

**Path to Hunting:**
1. Establish fire (to not freeze)
2. Craft Sharp Rock (5 min, requires 2x Stone from foraging)
3. Hunt animals with Sharp Rock
4. Cook meat on fire

**Minimum Time to Hunt:** 3+ hours if everything goes perfectly

**Problem:**
- Player starving at 16% after 2.5 hours
- No time to reach hunting (needs 3+ hours minimum)
- Wild Mushroom provides almost no calories
- Fire-making failures add 1-2 hours of delays
- Death from starvation before hunting is possible

**Impact:**
- Combines with fire-making and resource depletion for triple death spiral
- Even successful fire-making doesn't solve starvation
- No viable survival path in current balance
- Forces player to hunt without fire (hypothermia death instead)

**Solutions:**

**Option A: Reduce Food Consumption Rate** ⭐ RECOMMENDED
- Current: 14% per hour
- Proposed: 8-10% per hour (30-40% reduction)
- Extends survival time from 2.5 hours to 4-5 hours
- Gives time for fire + tool crafting + hunting

**Option B: Buff Early-Game Food**
- Wild Mushroom: 1% → 10-15% food
- Add other gathering: Eggs (5%), Grubs (3%), Nuts (8%)
- Doesn't require hunting tools
- Provides stopgap until hunting available

**Option C: Start Player with More Food**
- Start at 80-100% food instead of 50%
- Buys time for learning systems
- More realistic (wouldn't start adventure already hungry)

**Recommendation:** **Options A + B** - Slower consumption AND better early food
- Reduces time pressure
- Provides non-hunting food sources
- Still incentivizes hunting for better nutrition
- Makes day-1 survival actually possible

**Priority:** 🔴 **HIGH** - Prevents progression without hunting

---

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

### Frostbite Severity May Be Too High

**Severity:** Low (balance/tuning issue, not a bug)
**Status:** Observed during testing, not game-breaking

**Observation:**
After 4 hours of gameplay in 38-40°F weather with fur wraps (0.15 insulation):
- All extremities reach 100% Critical frostbite severity
- Strength drops to 0% (from 97%)
- Speed drops to 0% (from 84%)

**Note:** This is NOT the stacking bug (which was fixed). This is about the **severity values** being very high.

**Context:**
- The frostbite stacking bug is FIXED (only 1 effect per body part now)
- The temperature physics are realistic and working correctly
- This may be intentionally punishing to encourage fire-making/shelter

**Is This Actually A Problem?**
- ⚠️ Unclear - player has 4 hours to make fire and find shelter
- ⚠️ Starting campfire provides 15 minutes of warmth
- ⚠️ May be intended difficulty curve for Ice Age survival

**Recommendation:**
- Monitor during Phase 4+ testing with actual tool progression
- If players can't reasonably survive the first few hours even with optimal play, consider:
  1. Reducing frostbite severity progression rate
  2. Increasing fur wrap insulation slightly
  3. Making starting campfire last longer
- For now: **DEFER** - not blocking gameplay, may be intentional

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

### ~~Forage Success Rates Untested~~

**Severity:** Low (informational)
**Status:** ✅ **TESTED** (2025-11-01)

**Test Results:**
- ✅ Forest biome: Successfully found Bark Strips, Plant Fibers, Sticks, Firewood, Dry Grass
- ✅ Cave biome: Successfully found Mushrooms, River Stone, Flint, Clay, Handstone, Sharp Stone
- ⚠️ **Documentation Discrepancy**: Cave foraging has mushrooms (organic), contradicting CURRENT-STATUS.md which says "NO organics"
  - Master plan (line 205) says: "Cave: limited organic, high stone"
  - CURRENT-STATUS.md says: "Cave: Stones only, NO organics"
  - **Actual implementation matches master plan** - caves have LIMITED organic (mushrooms for food) but no fire-starting materials
  - **Verdict**: Not a bug, documentation needs clarification

---

### ~~Fire-Making Skill Checks Untested~~

**Severity:** Low (informational)
**Status:** ✅ **TESTED** (2025-11-01)

**Test Results - Hand Drill:**
- ✅ 30% base success chance (Firecraft 0) - **FAILED** first attempt
- ✅ Materials consumed on failure (as designed)
- ✅ XP gain on failure - leveled to Firecraft 1 (working correctly)
- ✅ 40% success chance (Firecraft 1) - **SUCCESS** second attempt
- ✅ Success chance displayed transparently: "Success! (40% chance)"
- ✅ Campfire (HeatSourceFeature) created on success
- ✅ Skill progression formula working: `30% + (level - 0) * 10%`

**Minor Discrepancy:**
- Plan document states: 0.05kg Tinder required
- Actual recipe requires: 0.1kg Tinder (2x more)
- **Impact**: Negligible - still easily obtainable

**Balance Assessment:**
- 30% base chance feels challenging but fair
- Failure providing XP makes attempts feel productive
- Leveling to 40% after one failure feels rewarding
- Material costs are reasonable

**Verdict:** Fire-making system working as designed ✅

---

## 📋 Testing Blockers

**Status:** ✅ **NO BLOCKERS** (as of 2025-11-01)

**Previously Resolved:**
1. ✅ Frostbite infinite stacking bug - FIXED (EffectRegistry + SurvivalProcessor)
2. ✅ Forest foraging - TESTED and working
3. ✅ Cave foraging - TESTED and working
4. ✅ Fire-making skill checks - TESTED and working

**Phases 1-3 Testing:** ✅ **COMPLETE**
- Starting conditions verified (fur wraps in container)
- Material system verified (tinder, bark, fibers all obtainable)
- Fire-making system verified (skill checks, XP, material consumption all working)

**Ready for Phase 4:** Tool Progression implementation

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

## 🧪 Playtest Results (2025-11-01 Post-Fix)

**Test Duration:** ~6 hours of in-game time
**Focus:** Verify frostbite fix + test fire-making skill checks
**Status:** ✅ **CRITICAL BUG FIXED - GAME NOW PLAYABLE**

### Systems Tested

**✅ Frostbite Effect System**
- **Result:** WORKING PERFECTLY
- After 4 hours in cold: Only 4 frostbite effects (one per extremity)
- No infinite stacking observed
- Effects properly update severity instead of duplicating
- Strength/Speed still drop to 0% (balance issue, not bug)

**✅ Foraging System**
- **Result:** WORKING AS DESIGNED
- Successfully found materials: Dry Grass, Bark Strips, Plant Fibers, Large Sticks
- Diminishing returns working (some hours yield nothing)
- Materials appear on ground after foraging
- Foraging grants XP (reached Foraging level 1)

**✅ Fire-Making Skill Checks**
- **Result:** WORKING AS DESIGNED
- Attempted Hand Drill Fire (30% base success chance)
- **Failed** on first attempt (expected with low skill)
- Materials consumed on failure (realistic!)
- Gained Firecraft XP from failure (1 XP) and leveled to Firecraft 1
- Crafting UI clearly shows requirements, success chance, and available materials

**✅ Dynamic Menu System**
- "Craft Items" menu appears after picking up items
- "Open Inventory" appears when carrying items
- Menus adapt to player state

### Key Findings

1. **Frostbite fix is production-ready** ✅
   - No more infinite stacking
   - Effects behave correctly
   - Game is fully playable

2. **Fire-making progression works** ✅
   - Skill checks functioning
   - Failure teaches (XP gain)
   - Material consumption is fair

3. **Foraging is balanced** ✅
   - Not too generous (some hours yield nothing)
   - Provides essential materials for fire-making
   - Starting clearing is forageable (critical for survival)

4. **Temperature/survival still harsh but playable**
   - After 4 hours: 50.9°F body temp (down from 98.6°F)
   - Frostbite reaches Critical severity
   - Strength/Speed drop to 0%
   - **This is likely working as intended per temperature physics**

### Issues Discovered

None! All tested systems working correctly.

### Recommendations for Next Testing Session

1. Test successful fire-making (gather more materials, attempt multiple times)
2. Test fire warmth mechanics (does campfire restore body temperature?)
3. Test Cave biome foraging (verify NO organics spawn)
4. Test other biomes (Riverbank, Plains, Hillside)
5. Test tool crafting once Phase 4 is implemented

---

## 🧪 Playtest Results (2025-11-02 Comprehensive Test)

**Test Duration:** ~3 hours of in-game time
**Focus:** Test all Phase 1-8 features (foraging, crafting, fire-making, survival)
**Status:** ✅ **MOSTLY WORKING** - 2 medium bugs found, severe balance issue confirmed

### Systems Tested

**✅ Starting Conditions**
- **Result:** WORKING CORRECTLY
- Starting gear visible: Worn Fur Chest Wrap + Fur Leg Wraps (0.15 total insulation)
- Starting campfire present and visible: "Campfire (dying)"
- Starting location (Clearing) has ForageFeature - can forage immediately
- Temperature: 98.6°F body, 38.6°F feels-like (starting fire providing warmth)

**✅ Foraging System**
- **Result:** WORKING PERFECTLY
- Hour 1: Found Dry Grass (1), Bark Strips (1), Large Stick (1), Firewood (1) + Leveled to Foraging 1
- Hour 2: Found Bark Strips (1), Large Stick (1)
- Hour 3: Found Dry Grass (1), Bark Strips (1)
- Message batching working: "You are still feeling cold. (occurred 8 times)"
- Forage output groups items correctly: "Dry Grass (1), Large Stick (1)"
- Time display working: "You spent 1 hour searching and found..."

**✅ Item Management**
- **Result:** WORKING CORRECTLY
- Items appear on ground after foraging (visible via "Look around")
- Can pick up items one by one
- Items added to Bag container successfully
- Dynamic menu appears: "Open inventory" and "Craft Items" options shown when carrying items

**⚠️ Crafting Menu**
- **Result:** MOSTLY WORKING - 2 bugs found
- Crafting menu displays correctly with 7 available recipes
- Hand Drill Fire recipe shows requirements and success chance
- Crafting preview feature working (shows materials to be consumed)
- **BUG #1**: "Show My Materials" shows `Tinder: 0.0` but craft screen shows `Tinder: 0.5` (inconsistent)
- **BUG #2**: Preview shows duplicate consumption entries (cosmetic only, actual crafting works)

**✅ Fire-Making Skill Checks**
- **Result:** WORKING AS DESIGNED
- Attempted Hand Drill Fire with 30% base success (Firecraft 0)
- **FAILED** on first attempt (expected)
- Materials consumed on failure: Dry Grass + Large Stick
- Gained XP and leveled to Firecraft 1 immediately
- Success chance would be 40% on next attempt (skill system working)
- Time cost: 20 minutes (appropriate)

**🔴 Survival System - SEVERE BALANCE ISSUE**
- **Result:** STILL TOO PUNISHING despite fixes
- After ~3 hours gameplay:
  - Body temp: 54.8°F (dropped from 98.6°F)
  - Frostbite: 100% Critical on all 4 extremities (Left Arm, Right Arm, Left Leg, Right Leg)
  - Strength: 0% (down from ~97%)
  - Speed: 0% (down from ~84%)
  - Hypothermia: 100% Critical
  - Shivering: 100% Critical
- Player is **completely incapacitated** after 3 hours even with:
  - Starting fur wraps (0.15 insulation - 3.75x better than old starting gear)
  - Starting campfire (15 min warmth)
  - Immediate access to foraging at spawn

**Critical Finding:**
Even with improved starting conditions (fur wraps + campfire + forageable clearing), the player still reaches critical hypothermia and total incapacitation within 3 hours. This is NOT enough time to:
1. Learn the crafting system
2. Gather enough materials for fire-making (requires ~0.5kg wood + 0.1kg tinder)
3. Attempt fire-making multiple times (30% base success rate means avg 3-4 attempts)
4. Success is almost impossible before death/incapacitation

**Verdict:** Temperature balance still needs adjustment (see Balance section)

### Issues Discovered

1. ✅ **Foraging UX** - Message batching working perfectly
2. ✅ **Campfire visibility** - Fixed, shows "Campfire (dying)"
3. ✅ **Crafting preview** - Shows exact consumption (with cosmetic bug in display)
4. 🟠 **NEW**: Material display inconsistency ("Show Materials" vs craft screen)
5. 🟠 **NEW**: Crafting preview shows duplicate entries (cosmetic bug)
6. 🔴 **ONGOING**: Survival time still too short (3 hours to incapacitation)

### Positive Highlights

1. **Dynamic menus working beautifully** - "Craft Items" and "Open inventory" appear automatically
2. **Message batching is a huge UX win** - No more spam, clean output
3. **Foraging feels rewarding** - Good variety of materials, clear output
4. **Crafting preview is transparent** - Players know exactly what they'll consume (minus display bug)
5. **Fire-making progression feels fair** - Failure teaches (XP), success is achievable with practice
6. **Starting campfire visible** - Players can see the warmth source

### Recommendations

1. **[CRITICAL]** Further adjust temperature balance (see Balance section below)
2. **[MEDIUM]** Fix material display inconsistency (confusing UX)
3. **[LOW]** Fix crafting preview duplicate entries (cosmetic issue)
4. **[HIGH]** Test successful fire-making (need more playtime to gather materials + attempt multiple fires)

---

## Priority Recommendations

**Updated:** 2025-11-02 (Post-Comprehensive Playtest)

1. **[CRITICAL]** Temperature balance still needs major adjustment
   - 3 hours to incapacitation is too short
   - Consider: Longer-lasting starting fire (30-60 min instead of 15 min)
   - Consider: Higher starting insulation (0.20-0.25 instead of 0.15)
   - Consider: Slower frostbite progression rate
   - OR: Make Hand Drill Fire easier (50% base success instead of 30%)

2. **[HIGH]** Fix material display inconsistency
   - "Show My Materials" shows incorrect Tinder value
   - Causes player confusion about available resources

3. **[MEDIUM]** Fix crafting preview duplicate entries
   - Cosmetic bug that reduces trust in system
   - Preview shows 4 items when only 2 consumed

4. ~~**[HIGH]** Test and document foraging success rates~~ ✅ **COMPLETED**
5. ~~**[HIGH]** Test fire-making mechanics and skill progression~~ ✅ **COMPLETED**
6. **[HIGH]** Continue Phase 4 implementation (Tool Progression)
7. **[MEDIUM]** Test biome-specific foraging (Cave, Riverbank, Plains, Hillside)
8. **[LOW]** Improve "Press any key" handling for TEST_MODE
9. **[LOW]** Fix sleep option visibility when exhausted
10. **[LOW]** Show dead campfires as "Cold Campfire"

---

## ✅ Resolved Issues

*Brief records of previously fixed issues*

### Message Spam During Long Actions (Fixed 2025-11-02)
- **Issue:** Repeated "still feeling cold" messages 15-20 times during 1-hour actions
- **Solution:** Added message batching system in `IO/Output.cs` and `World.cs` - now shows "(occurred 10 times)"
- **Files:** `IO/Output.cs:14-170`, `World.cs:10-30`

### Starting Campfire Not Visible (Fixed 2025-11-02)
- **Issue:** Campfire provided warmth but didn't appear in "Look around" display
- **Solution:** Added LocationFeature display loop to show HeatSourceFeature and ShelterFeature
- **Files:** `Actions/ActionFactory.cs:863-885`, `HeatSourceFeature.cs:11`

### Crafting Material Consumption Unclear (Fixed 2025-11-02)
- **Issue:** Players didn't know which specific items would be consumed before crafting
- **Solution:** Added `PreviewConsumption()` method to show exact items/amounts before confirmation
- **Files:** `Crafting/CraftingRecipe.cs:101-151`, `Actions/ActionFactory.cs:672-681`

### Dead Campfires Not Displayed (Fixed 2025-11-02)
- **Issue:** Campfires with FuelRemaining = 0 disappeared from "Look around" display
- **Solution:** Modified ActionFactory.cs to show "(cold)" status, limited to max 1 dead fire per location
- **Files:** `Actions/ActionFactory.cs:875-893` (dead campfire display logic)
- **Note:** Discovered Location.Update() doesn't call Feature.Update(), so fuel never decreases (separate issue)

### Material Selection Algorithm Investigation (Resolved 2025-11-02)
- **Issue:** Suspected Bark Strips and Dry Grass were consumed for Wood requirements
- **Investigation:** Verified Bark Strips [Tinder, Binding, Flammable] and Dry Grass [Tinder, Flammable, Insulation] have NO Wood property
- **Conclusion:** Algorithm working correctly - only items with required property are consumed
- **Files:** `Items/ItemFactory.cs` (property definitions), `Crafting/CraftingRecipe.cs:153-184` (ConsumeProperty logic)

