# Feature Suggestions - Text Survival RPG

**Last Updated:** 2025-11-02
**Testing Context:** Comprehensive playtest of Phase 1-8 features

---

## Quality of Life Improvements

### Add Hunting Tutorial Messages

**Current Behavior:**
- Hunt action appears in main menu with no explanation
- Players unfamiliar with stealth mechanics may not understand detection risks
- No guidance on optimal range or when to shoot

**Suggested Enhancement:**
- First time player selects "Hunt", show brief tutorial:
  ```
  💡 HUNTING TIP: Approach carefully to get within range.
  Animals can detect you more easily at close range.
  Optimal bow range is 30-50m for best accuracy.
  ```
- Add contextual hints during first hunt:
  - "The deer hasn't noticed you yet. Keep approaching carefully."
  - "You're getting close - detection risk is increasing!"
  - "Good range for a shot! (40m)"

**Priority:** Medium - Improves new player experience with hunting system

---

### Show Detection Chance in Approach Action

**Current Behavior:**
- "Assess Target" shows "Next approach detection risk: 33%"
- During actual approach, no warning shown
- Player doesn't know how risky each approach is

**Suggested Enhancement:**
- Show risk in the Approach action name:
  ```
  1. Approach (23% detection risk)
  2. Assess Target
  3. Stop Hunting
  ```
- Or show risk in action output:
  ```
  Approaching... (detection risk: 45%)
  You carefully move 22m closer...
  ```

**Priority:** Low - Nice to have for informed decision-making

---

### Show Success Chance in Crafting Menu (Before Selecting Recipe)

**Current Behavior:**
- Crafting menu shows: "6. Craft Hand Drill Fire"
- Success chance only shown after selecting the recipe
- Player must navigate into recipe to see if it's viable

**Suggested Enhancement:**
- Show success chance inline in menu:
  ```
  6. Craft Hand Drill Fire (40% success)
  7. Craft Bow Drill Fire (60% success) [LOCKED: Need Firecraft 1]
  ```
- Helps player make informed decisions without extra navigation
- Shows skill progression value immediately
- Show lock reason for unavailable recipes

**Priority:** High - greatly improves crafting UX

---

### Add Skill Level Display in Main Stats Bar

**Current Behavior:**
- Main stats bar shows: Food, Water, Energy, Temp
- Skills only visible in "Check Stats" submenu
- Player can't see skill progression at a glance

---

### Highlight New Recipes When Unlocked

**Current Behavior:**
- New recipes silently appear in crafting menu
- No notification when skill/material requirements met
- Player may not notice new options

**Suggested Enhancement:**
- When new recipe becomes available, show message after foraging/leveling:
  ```
  You leveled up Firecraft to level 1!
  🔧 New recipe unlocked: Bow Drill Fire
  ```
- Mark new recipes in menu with [NEW] tag for one session
- Creates moment of achievement/discovery

**Priority:** Low - polish feature that improves engagement

---

### Add "Take All" Option When Looking Around

**Current Behavior:**
- Look around shows items on ground: "Dry Grass", "Large Stick"
- Player must manually navigate to pick up items one by one
- Tedious when there are 8+ items on ground

**Suggested Enhancement:**
- Add option in "Look Around" submenu: "Take all items"
- Streamlines early game material gathering workflow
- Reduces menu navigation clicks

**Priority:** High - current item pickup is tedious (tested during playtest)

**Example Flow:**
```
>>> CLEARING <<<
You see:
  Dry Grass
  Large Stick
  Bark Strips
  (+ 5 more items)

1. Take all items
2. Take Dry Grass
3. Take Large Stick
4. Back
```

---

### Show Material Requirements in Crafting Menu

**Current Behavior:**
- Crafting menu likely shows recipes but not what player currently has
- Player must check "View Materials" separately to see inventory

**Suggested Enhancement:**
- Show required vs. available for each recipe inline:
  ```
  Hand Drill Fire [✓]
  - Wood: 0.5kg (you have: 1.2kg) ✓
  - Tinder: 0.05kg (you have: 0.1kg) ✓
  - Success: 30% + 10% per Firecraft level
  ```

**Priority:** High - improves crafting UX significantly

---

### Add Recipe Discovery Messages

**Current Behavior:**
- All recipes likely appear in crafting menu when conditions met
- No fanfare or notification when new recipe unlocks

**Suggested Enhancement:**
- When player first meets requirements for a recipe:
  ```
  🔧 New Recipe Discovered: Sharp Rock!
  Smash two river stones together to create a crude cutting tool.
  ```
- Adds sense of progression and discovery
- Teaches players about material requirements organically

**Priority:** Low - polish/immersion feature

---

### Group Inventory by Material Type

**Current Behavior:**
- Inventory likely shows items in order found
- Hard to see total materials available for crafting

**Suggested Enhancement:**
- Group inventory display by material type:
  ```
  === WOOD (1.7kg) ===
    Large Stick (1.2kg)
    Small Branch (0.5kg)

  === TINDER (0.15kg) ===
    Dry Grass (0.1kg)
    Bark Strips (0.05kg)

  === STONE (0.8kg) ===
    River Stone (0.4kg) x2
  ```

**Priority:** Medium - makes crafting material assessment much easier

---

## Gameplay Enhancements

### Add "Quick Craft" Option for Simple Recipes

**Current Behavior:**
- Every recipe requires navigating to crafting menu
- Confirmation prompts for each craft
- Slow for repeatedly crafting simple items

**Suggested Enhancement:**
- Once a recipe is crafted successfully 3+ times, add to "Quick Craft" submenu
- Skip confirmation for 100% success rate recipes
- Example: "Quick Craft: Sharp Rock (5 sec)" appears in main menu

**Priority:** Low - convenience feature for mid-late game

---

### Add Foraging "Hotspots" Based on Skill

**Current Behavior:**
- Foraging finds items randomly based on abundance rates
- No player agency in what to search for

**Suggested Enhancement:**
- At Foraging level 2+, allow player to focus search:
  ```
  What would you like to focus on finding?
  1. Wood materials (sticks, branches)
  2. Stone materials (river stones, flint)
  3. Fire materials (tinder, bark)
  4. Food (berries, roots, mushrooms)
  5. Search for everything (default)
  ```
- Higher success rate for focused category
- Teaches players about biome material profiles

**Priority:** Medium - adds strategic depth to foraging

---

### Add Material Weight Display in Forage Results

**Current Behavior:**
- Forage output shows: "Dry Grass (1), Large Stick (1)"
- No weight information shown

**Suggested Enhancement:**
- Show total weight collected and item weights:
  ```
  You spent 1 hour searching and found:
    Large Stick (1.2kg)
    Dry Grass (0.1kg)
  Total: 1.3kg of materials collected
  ```
- Helps players understand material requirements for recipes
- Shows progress toward crafting goals

**Priority:** Low - informational QoL

---

## Tutorial / New Player Experience

### Add Recipe Hints When Materials Found

**Current Behavior:**
- Player finds materials but may not know what they're used for
- Must open crafting menu to discover recipes

**Suggested Enhancement:**
- When player first finds a key material, show hint:
  ```
  You found: River Stone (0.4kg)

  💡 Tip: You can craft a Sharp Rock by smashing two river stones together.
  Find this recipe in the Craft menu.
  ```
- Only shows once per material type
- Teaches crafting system organically

**Priority:** Medium - improves new player onboarding

---

### Add "Survival Tips" During Sleep

**Current Behavior:**
- Sleep just advances time
- Missed opportunity for tutorial content

**Suggested Enhancement:**
- Randomly display survival tips during sleep:
  ```
  You sleep for 8 hours...

  💤 Survival Tip: Fur-lined clothing provides the best cold weather protection.
  Hunt animals to collect hides, then tan them into leather for crafting.
  ```
- Non-intrusive way to teach game mechanics
- Can be disabled with setting after first few days

**Priority:** Low - polish/tutorial feature

---

## Performance / Technical

### Optimize Forage Output Collection

**Current Behavior (from testing):**
- Items found during foraging are added to location immediately
- Then grouped at the end for display
- Possible performance concern with large collections

**Suggested Enhancement:**
- Collect items in memory during foraging loop
- Add to location only once at the end
- May reduce object allocation/GC pressure

**Priority:** Very Low - premature optimization, no observed issues

---

## Code Quality & Refactoring

*These suggestions come from the architecture review (2025-11-01). All critical and high-priority issues have been resolved. These are low-priority code quality improvements for future consideration.*

---

### Centralize Magic Numbers in GameBalance.cs

**Current Behavior:**
- Constants scattered throughout codebase (SurvivalProcessor.cs, EffectBuilder.cs, DamageProcessor.cs)
- Magic numbers like `0.2`, `0.7`, `-0.05` appear without documentation
- Difficult to tune game balance or see relationships between values

**Suggested Enhancement:**
- Create `GameBalance.cs` or `Config.cs` with organized constant categories:
  ```csharp
  public static class GameBalance
  {
      public static class Survival
      {
          public const double MAX_ENERGY_MINUTES = 960.0; // 16 hours
          public const double BASE_EXHAUSTION_RATE = 1.0;
          // ... etc
      }

      public static class Combat
      {
          public const double MIN_VITALITY_TO_MOVE = 0.2;
          // ... etc
      }
  }
  ```
- Makes balancing easier and values self-documenting
- Can see all related values in one place

**Priority:** Medium - Improves maintainability
**Estimated Time:** 3-4 hours

---

### Move Player.cs to Actors Namespace

**Current Behavior:**
- `Player.cs` is in root namespace `text_survival`
- `Actor.cs` and `NPC.cs` are in namespace `text_survival.Actors`
- Creates organizational inconsistency

**Suggested Enhancement:**
- Move `Player.cs` to `Actors/` folder
- Update namespace to `text_survival.Actors`
- Update imports in other files

**Priority:** Low - Organizational consistency
**Estimated Time:** 30 minutes

---

### Add Builder Validation to RecipeBuilder and ActionBuilder

**Current Behavior:**
- Builders only validate name is not empty
- Invalid recipes can be created:
  - Recipe with no property requirements
  - Recipe with Item result type but no items specified
  - Actions with neither Do() nor ThenShow() (useless actions)

**Suggested Enhancement:**
- Add comprehensive validation in `.Build()` methods:
  ```csharp
  if (_requiredProperties.Count == 0)
      throw new InvalidOperationException($"Recipe '{_name}' has no requirements");

  if (_resultType == CraftingResultType.Item && _resultItems.Count == 0)
      throw new InvalidOperationException($"Recipe '{_name}' has Item result but no items");
  ```
- Catches configuration errors at build time instead of runtime
- Makes debugging much easier

**Priority:** Medium - Prevents subtle bugs
**Estimated Time:** 1-2 hours

---

### Standardize Null Handling Patterns

**Current Behavior:**
- Codebase uses multiple patterns inconsistently:
  - Null-coalescing with default (`?? defaultValue`)
  - Explicit null checks (`if (value != null)`)
  - Null-forgiving operator (`!`)
  - Pattern matching

**Suggested Enhancement:**
- Establish and document conventions:
  1. Use null-coalescing for defaults
  2. Use pattern matching for complex checks
  3. Avoid `!` operator unless preceded by explicit null check
  4. Enable nullable reference types project-wide (`<Nullable>enable</Nullable>`)
- Add conventions to CLAUDE.md

**Priority:** Low - Code consistency
**Estimated Time:** 2-3 hours to audit and update

---

### Add Type-Safe Skill Access (Enum or Direct Properties)

**Current Behavior:**
- Skills accessed via string lookup: `GetSkill("Firecraft").GainExperience(xp)`
- Typos cause runtime errors
- No compile-time checking or IntelliSense

**Suggested Enhancement:**

**Option 1:** Direct property access (simplest)
```csharp
ctx.player.Skills.Firecraft.GainExperience(xp);
```

**Option 2:** Enum-based lookup (if dynamic needed)
```csharp
public enum SkillType { Fighting, Firecraft, Crafting, ... }
ctx.player.Skills.GetSkill(SkillType.Firecraft).GainExperience(xp);
```

**Note:** Current string-based approach may be justified for:
- Recipe requirements (serialization)
- Data-driven skill definitions
- Mod support

**Priority:** Low - Nice to have for IntelliSense
**Estimated Time:** 1-2 hours

---

### Remove Unused Return Value from Body.Rest()

**Current Behavior:**
- `Body.Rest()` returns `bool` (whether fully rested)
- Return value is never used by callers
- Confusing API

**Suggested Enhancement:**

**Option 1:** Remove return value
```csharp
public void Rest(int minutes) { /* ... */ }
```

**Option 2:** Use return value meaningfully
```csharp
bool fullyRested = ctx.player.Body.Rest(minutes);
if (fullyRested)
    Output.WriteLine("You wake up feeling refreshed!");
```

**Priority:** Very Low - Minor API cleanup
**Estimated Time:** 15 minutes

---

## Notes

- Most suggestions focus on UX improvements rather than new mechanics
- Priority based on impact to core gameplay loop vs. implementation effort
- Many suggestions build on the successful Phase 7 foraging UX improvements
- Should be considered AFTER fixing critical bugs in ISSUES.md
- Code quality suggestions are deferred from completed architecture review
