# Bug-Fixing Sprint: Context & Key Information

**Last Updated:** 2025-11-01

---

## Sprint Context

This sprint fixes 3 critical UX bugs discovered during Phase 1-7 crafting/foraging testing:
1. Message spam during long actions (foraging, sleeping)
2. Starting campfire invisible in location display
3. Material consumption unclear when crafting

**Previous Work:**
- Phase 1-7 crafting/foraging overhaul (complete)
- Temperature balance fixes (complete)
- Frostbite infinite stacking bug (fixed)
- Testing infrastructure improvements (complete)

**Current State:**
- Game is playable and balanced
- All critical bugs fixed
- 6 active UX issues remain in ISSUES.md
- This sprint tackles top 3 issues

---

## Key Files & Locations

### Issue #1: Message Spam

**Primary Files:**
- `World.cs` - Main world update loop (lines 12-17)
  - Calls `Player.Update()` for each minute elapsed
  - This is where message deduplication should happen

- `Bodies/Body.cs` - Body update and survival processing
  - `Update(TimeSpan elapsed, GameContext ctx)` - line 75
  - Calls `SurvivalProcessor.Process()`

- `Survival/SurvivalProcessor.cs` - Pure function survival processing
  - `Process()` method generates status messages
  - Line 162-165: "You are still feeling cold" message
  - Uses `Config.NOTIFY_EXISTING_STATUS_CHANCE = 0.05` (5% probability)

- `Actions/ActionFactory.cs` - Long actions that trigger spam
  - Forage action (lines 47-61)
  - Sleep action (lines 63-77)

**Key Code Snippets:**

```csharp
// World.cs:12-17 - Where messages accumulate
public static void Update(int minutes)
{
    CurrentTime += TimeSpan.FromMinutes(minutes);
    for (int i = 0; i < minutes; i++)
    {
        Player.Update();  // Called 60 times for 1-hour forage
    }
}

// SurvivalProcessor.cs:162-165 - Message generation
else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
{
    result.Messages.Add("You are still feeling cold.");
}
```

**Architecture Notes:**
- SurvivalProcessor is pure function - should NOT modify this directly
- Message deduplication should happen at World.Update level
- Need way to tag messages as "critical" vs "repeatable"

---

### Issue #2: Campfire Visibility

**Primary Files:**
- `Actions/ActionFactory.cs` (lines 839-866) - LookAround action
  - Currently displays: Items, Containers, NPCs
  - Missing: LocationFeatures

- `Environments/LocationFeatures.cs` - Feature implementations
  - HeatSourceFeature (line 55+) - Campfire, torches
  - ShelterFeature (line 79+) - Windbreak, lean-to, etc.
  - ForageFeature (line 13+) - Foraging mechanic
  - EnvironmentFeature (line 142+) - Location type

- `Environments/Location.cs` - Location class
  - `Features` property - list of LocationFeatures

**Key Code Snippets:**

```csharp
// ActionFactory.cs:839-866 - Current LookAround implementation
bool hasItems = false;
foreach (var item in location.Items.Where(i => i.IsFound))
{
    Output.WriteLine("\t", item);
    hasItems = true;
}
foreach (var container in location.Containers.Where(c => c.IsFound))
{
    Output.WriteLine("\t", container, " [container]");
    hasItems = true;
}
foreach (var npc in location.Npcs.Where(n => n.IsFound))
{
    Output.WriteLine("\t", npc, " [creature]");
    hasItems = true;
}
// MISSING: LocationFeatures loop!

if (!hasItems)
{
    Output.WriteLine("Nothing...");
}
```

**Display Rules to Implement:**
- HeatSourceFeature: Show if has fuel, show status (burning/dying)
- ShelterFeature: Always show
- ForageFeature: Do NOT show (abstract mechanic)
- EnvironmentFeature: Do NOT show (it's the location itself)

---

### Issue #3: Crafting Transparency

**Primary Files:**
- `Crafting/CraftingRecipe.cs` - Recipe class and consumption logic
  - `ConsumeIngredients(Player player)` - line 93
  - `ConsumeProperty(Player, PropertyRequirement)` - lines 101-132
  - This is where items are selected and consumed

- `Crafting/RecipeBuilder.cs` - Recipe builder pattern
  - `WithPropertyRequirement()` - adds property requirements
  - `Build()` - creates final recipe

- `Actions/ActionFactory.cs` - Crafting UI
  - `CraftItem()` action (lines 654-673) - Where user confirms craft
  - Currently shows requirements but not which specific items

**Key Code Snippets:**

```csharp
// CraftingRecipe.cs:101-132 - Item consumption algorithm
private void ConsumeProperty(Player player, PropertyRequirement requirement)
{
    double remainingNeeded = requirement.AmountNeeded;

    // Filter items that have the required property
    var eligibleStacks = player.inventoryManager.Items
        .Where(stack => stack.FirstItem.HasProperty(requirement.Property, 0))
        .ToList();

    // Consume items greedily in inventory order
    foreach (var stack in eligibleStacks)
    {
        while (stack.Count > 0 && remainingNeeded > 0)
        {
            var item = stack.Pop();
            if (item.Weight <= remainingNeeded)
            {
                remainingNeeded -= item.Weight;
                // Item fully consumed
            }
            else
            {
                // Partial consumption
                var property = item.GetProperty(requirement.Property);
                if (property.HasValue)
                {
                    item.Weight -= (float)remainingNeeded;
                    remainingNeeded = 0;
                    stack.Push(item);
                }
            }
        }
    }
}
```

**Algorithm Behavior:**
- Filters items by property
- Consumes in inventory order (first-found)
- No optimization or "smart" selection
- Greedy algorithm - may consume valuable items unnecessarily

**Transparency Solution:**
- Create `GetItemsToConsume(Player)` method
- Reuse ConsumeProperty logic but don't actually consume
- Return preview of items that WOULD be consumed
- Display before crafting confirmation

---

## Architecture Patterns

### Action Builder Pattern

All actions use fluent builder:
```csharp
CreateAction("Name")
    .When(ctx => condition)
    .Do(ctx => { /* logic */ })
    .ThenShow(_ => [nextActions])
    .ThenReturn()
    .Build();
```

**Important:** Changes to actions must follow this pattern.

---

### Pure Function Design (SurvivalProcessor)

SurvivalProcessor is pure function:
```csharp
public static SurvivalProcessorResult Process(
    SurvivalData data,
    double minutesElapsed,
    Dictionary<EffectKind, Effect> effects)
{
    // No state modification
    // Returns result object with messages
}
```

**Important:** Don't add state or side effects to SurvivalProcessor.

---

### Message Output

All messages use Output.WriteLine():
```csharp
Output.WriteLine("Message text");
Output.WriteLine("Prefix: ", object);
```

**Important:** This is centralized display - could add deduplication here.

---

## Configuration Values

```csharp
// Config.cs
public static readonly double NOTIFY_EXISTING_STATUS_CHANCE = 0.05;  // 5% per minute
```

**Note:** Expected ~3 messages per hour (5% of 60), but actual is 15-20. Why?

---

## Testing Strategy

### Manual Testing with TEST_MODE

```bash
# Start game
./play_game.sh start

# Test foraging (Issue #1)
./play_game.sh send 2  # Forage
./play_game.sh tail 50  # View output

# Test look around (Issue #2)
./play_game.sh send 1  # Look around
./play_game.sh tail 20

# Test crafting (Issue #3)
./play_game.sh send 4  # Craft
./play_game.sh send 1  # Select recipe
./play_game.sh tail 30
```

### Expected Outcomes After Fixes

**Issue #1:**
```
You forage for 1 hour
You spent 1 hour searching and found: Dry Grass (1), Large Stick (1)
During your search, you felt cold.
```

**Issue #2:**
```
>>> CLEARING <<<
You see:
  Dying Campfire (burning)
  Dry Grass
  Large Stick
```

**Issue #3:**
```
Crafting: Sharpened Stick
Materials needed:
- Wood: 0.5kg

This will consume:
- Large Stick (0.5kg Wood)

Do you want to craft? (y/n)
```

---

## Known Issues & Gotchas

### Issue #1 Research Question

**Why 15-20 messages instead of 3?**

Math check:
- 60 minutes of foraging
- 5% chance per minute (`Config.NOTIFY_EXISTING_STATUS_CHANCE = 0.05`)
- Expected: 60 × 0.05 = 3 messages
- Actual: 15-20 messages (reported in ISSUES.md)

**Possible explanations:**
1. Config value is wrong (higher than 0.05)
2. Message triggered multiple times per update
3. Multiple status conditions triggering messages
4. Bug in DetermineSuccess probability

**Action:** Test and document actual behavior before implementing fix.

---

### Issue #2 Display Rules

**ForageFeature should NOT be displayed:**
- It's a mechanic (ability to forage), not an object
- Player discovers foraging by trying the action
- Showing "Forage Area" would be meta-knowledge

**EnvironmentFeature should NOT be displayed:**
- It represents the location type (Forest, Cave, etc.)
- Already shown in location header: "(Ancient Pine Forest • Morning)"

**HeatSourceFeature edge cases:**
- Active fire (has fuel, burning): Show as "Campfire (burning)"
- Dying fire (low fuel): Show as "Dying Campfire"
- Dead fire (0 fuel): Show as "Cold Campfire" OR hide? (TBD - needs decision)

---

### Issue #3 Algorithm Behavior

**Current ConsumeProperty is greedy:**
- Processes items in inventory order
- No concept of "better" vs "worse" items to consume
- Example: If you have Torch (Wood + Flammable + Light) and Stick (Wood), it might consume Torch first

**This is NOT a bug - it's by design:**
- Property-based system treats all Wood sources equally
- Player manages inventory order if they care

**Transparency solves the UX issue:**
- Player sees "Will consume: Torch" and can decide whether to proceed
- Doesn't require complex "smart selection" algorithm
- Maintains simple, predictable behavior

---

## Dependencies

**No external dependencies**

**Internal dependencies:**
- All fixes are independent (can be done in any order)
- Recommended order: #2 → #1 → #3 (easiest to hardest)

---

## Rollback Strategy

```bash
# Each fix should be a separate commit
git checkout -b bugfix/phase1-ux-fixes

# After Issue #2
git add .
git commit -m "Fix Issue #2: Campfire visibility"

# After Issue #1
git add .
git commit -m "Fix Issue #1: Message deduplication"

# After Issue #3
git add .
git commit -m "Fix Issue #3: Crafting transparency"

# If any fix causes regressions
git revert HEAD  # Undo last commit
```

---

## User Questions for Morning Review

1. **Issue #1 (Message Deduplication):**
   - Is the summary format clear? ("During your search, you felt cold")
   - Should critical messages be highlighted differently?

2. **Issue #2 (Campfire Display):**
   - Should inactive fires (0 fuel) be shown or hidden?
   - Should fire status show time remaining? ("Campfire (5 min left)")

3. **Issue #3 (Crafting Transparency):**
   - Is the preview format helpful?
   - Should player be able to choose which items to use? (Future enhancement)

---

## Implementation Summary (2025-11-02)

### What Was Actually Built

**Issue #1 - Message Batching System:**
- Implemented in `IO/Output.cs` (not World.Update as originally planned)
- Added static fields: `IsBatching`, `MessageBuffer`
- `StartBatching()` / `FlushMessages()` methods
- Critical message detection via string contains checks:
  - "leveled up", "developing", "damage", "Frostbite", "Hypothermia", "freezing", "health"
- Chosen format: "Message text (occurred X times)" - per user decision
- World.Update() wraps multi-minute updates in batching calls

**Why This Approach:**
- Centralized at output layer (cleaner than World.Update logic)
- No changes to SurvivalProcessor (preserves pure function design)
- Simple string detection (avoids complex message type enum)
- Works for all long actions (forage, sleep, rest, etc.)

**Issue #2 - LocationFeature Display:**
- Added foreach loop in ActionFactory.cs LookAround (lines 863-885)
- Shows HeatSourceFeature with status: "burning" or "dying"
- Shows ShelterFeature with name
- Explicitly skips ForageFeature and EnvironmentFeature (not physical objects)
- Dead fires (FuelRemaining = 0) currently hidden - follow-up issue created

**Issue #3 - Crafting Preview:**
- `PreviewConsumption()` in CraftingRecipe.cs mirrors `ConsumeProperty()` logic
- Creates copy of stack.Items to iterate without modifying: `var stackCopy = new List<Item>(stack.Items)`
- Returns `List<(string ItemName, double Amount)>`
- Display added in ActionFactory.cs CraftItem action before confirmation
- Format: "This will consume:\n  - Item (weight kg)"

**Key Discovery:**
Preview revealed that crafting "Sharpened Stick" (0.5kg Wood requirement) consumes:
- Bark Strips (0.05kg)
- Dry Grass (0.02kg)
- Large Stick (0.43kg)

This suggests items have multiple properties or greedy algorithm has issues. User decided to investigate & fix.

---

## User Decisions Made (2025-11-02)

**Q1: Dead Campfires**
- Decision: Show as "Cold Campfire" when FuelRemaining = 0
- Limit: Max 1 dead fire per location (prevent clutter)
- Reasoning: Player can relight fires via fire-making recipes (AddFuel() method exists)

**Q2: Message Deduplication Format**
- Decision: Keep current format "(occurred X times)"
- Rejected: "During your search, you felt cold (10 times)" format
- Rejected: Suppress entirely

**Q3: Material Selection Algorithm**
- Decision: Investigate & fix algorithm (not just accept transparency)
- Created follow-up issue: "Crafting Material Selection Algorithm Investigation"
- Next steps: Check ItemFactory for which items have Wood property, understand greedy algorithm

---

## Lessons Learned

**Message Batching:**
- Centralized output layer is powerful intervention point
- String-based critical detection works but fragile (typo-sensitive)
- Future: Consider MessageType enum for robustness

**LocationFeature Display:**
- `IsFound` pattern doesn't apply to LocationFeatures (they're always "there")
- Need explicit logic for which features to show vs hide
- Status display ("burning"/"dying") requires reading feature properties

**Crafting Preview:**
- Mirroring existing logic (copy-paste) ensures accuracy
- Creating stack copies prevents iterator modification bugs
- Preview exposed existing algorithm behavior (good - transparency working as intended!)

**Testing:**
- TEST_MODE is essential for rapid iteration
- play_game.sh workflow very effective for automated checks
- User review caught dead fire edge case that testing missed

---

**Last Updated:** 2025-11-02
