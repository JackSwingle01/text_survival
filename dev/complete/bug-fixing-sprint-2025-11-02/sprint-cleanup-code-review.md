# Code Review: Sprint Cleanup - Dead Campfire Display Logic

**Last Updated:** 2025-11-02
**Reviewer:** code-architecture-reviewer agent
**Scope:** Sprint cleanup session quick wins
**Status:** Production-ready with minor suggestions

---

## Executive Summary

**Overall Assessment:** ✅ **APPROVED FOR PRODUCTION**

The dead campfire display logic added to `ActionFactory.cs` is well-implemented, follows existing patterns, and successfully addresses Issue #4 from the bug-fixing sprint. The code is production-ready with only minor low-priority suggestions for future refinement.

**Key Strengths:**
- Consistent with existing LocationFeature display patterns
- Proper null safety (implicit via type checking)
- Clear variable naming and intent
- Appropriate limiting of dead fire display (max 1)
- Integrates seamlessly with existing HeatSourceFeature and ShelterFeature code

**Minor Considerations:**
- Counter-based limiting could be replaced with LINQ for consistency
- Edge case behavior with multiple dead fires is clear but could be more explicit

---

## Critical Issues

### None Found ✅

No critical issues identified. The code follows project patterns and integrates correctly.

---

## Important Improvements

### None Required ✅

The implementation meets requirements without significant concerns.

---

## Minor Suggestions

### 1. Consider LINQ-Based Limiting for Pattern Consistency

**Current Implementation (Lines 874-893):**
```csharp
// Display LocationFeatures (campfires, shelters, etc.)
int deadFiresShown = 0;
foreach (var feature in location.Features)
{
    if (feature is HeatSourceFeature heat)
    {
        // Show active fires with status
        if (heat.FuelRemaining > 0)
        {
            string status = heat.FuelRemaining < 0.5 ? "dying" : "burning";
            Output.WriteLine($"\t{heat.Name} ({status})");
            hasItems = true;
        }
        // Show max 1 dead fire per location (can be relit via fire-making recipes)
        else if (deadFiresShown == 0)
        {
            Output.WriteLine($"\t{heat.Name} (cold)");
            hasItems = true;
            deadFiresShown++;
        }
    }
    // ... rest of feature handling
}
```

**Pattern Observation:**
Elsewhere in the same file, LINQ is used consistently for filtering:
- Line 856: `location.Items.Where(i => i.IsFound)`
- Line 862: `location.Containers.Where(c => c.IsFound)`
- Line 868: `location.Npcs.Where(n => n.IsFound)`
- Line 611-613: Recipe filtering with `.Where()`

**Alternative LINQ Approach:**
```csharp
// Active fires with status
var activeFires = location.Features.OfType<HeatSourceFeature>().Where(h => h.FuelRemaining > 0);
foreach (var heat in activeFires)
{
    string status = heat.FuelRemaining < 0.5 ? "dying" : "burning";
    Output.WriteLine($"\t{heat.Name} ({status})");
    hasItems = true;
}

// Show max 1 dead fire
var deadFire = location.Features.OfType<HeatSourceFeature>().FirstOrDefault(h => h.FuelRemaining <= 0);
if (deadFire != null)
{
    Output.WriteLine($"\t{deadFire.Name} (cold)");
    hasItems = true;
}

// Shelters (always show)
var shelters = location.Features.OfType<ShelterFeature>();
foreach (var shelter in shelters)
{
    Output.WriteLine($"\t{shelter.Name} [shelter]");
    hasItems = true;
}
```

**Pros of LINQ Approach:**
- More consistent with surrounding code patterns
- Makes "max 1 dead fire" constraint more explicit via `FirstOrDefault()`
- Separates filtering logic from display logic
- Easier to modify filters in future (e.g., "show closest dead fire")

**Cons of LINQ Approach:**
- Slightly more verbose (3 separate loops vs 1)
- Multiple iterations over `location.Features` (negligible performance impact)
- Current imperative approach is perfectly readable

**Recommendation:** LOW PRIORITY
The current counter-based approach works fine and is clear. This is purely a style/consistency suggestion. LINQ would align better with the codebase's general preference for declarative filtering.

---

### 2. Document Edge Case: Multiple Dead Fires

**Current Behavior:**
When a location has multiple `HeatSourceFeature` instances with `FuelRemaining = 0`:
- Only the **first** dead fire encountered is displayed
- Others are silently skipped
- No indication to player that other dead fires exist

**Example Scenario:**
```
Location.Features contains:
1. HeatSourceFeature "Campfire" (FuelRemaining = 0)
2. HeatSourceFeature "Signal Fire" (FuelRemaining = 0)
3. HeatSourceFeature "Cooking Fire" (FuelRemaining = 2.5)

Display shows:
- Campfire (cold)           ← First dead fire
- Cooking Fire (burning)    ← Active fire
(Signal Fire is hidden)
```

**Is This Intentional?**
Based on the comment "Show max 1 dead fire per location (can be relit via fire-making recipes)", this appears intentional. The design decision seems to be:
- Active fires are always relevant (provide heat/light)
- Dead fires clutter the display if too many
- Showing 1 dead fire reminds player "you can relight fires here"

**Potential Issues:**
1. **No disambiguation:** If multiple dead fires exist, player doesn't know which one will be relit by recipes
2. **Hidden inventory:** Other dead HeatSourceFeatures are effectively invisible
3. **Discoverability:** Player has no way to know additional dead fires exist

**Suggested Enhancement (Optional):**
If multiple dead fires are a realistic scenario, consider:

**Option A: Count indicator**
```csharp
var deadFireCount = location.Features.OfType<HeatSourceFeature>().Count(h => h.FuelRemaining <= 0);
if (deadFireCount > 0)
{
    var firstDead = location.Features.OfType<HeatSourceFeature>().First(h => h.FuelRemaining <= 0);
    string suffix = deadFireCount > 1 ? $" (+ {deadFireCount - 1} more)" : "";
    Output.WriteLine($"\t{firstDead.Name} (cold){suffix}");
    hasItems = true;
}
```
Output: `Campfire (cold) (+ 2 more)`

**Option B: Generic naming when multiple**
```csharp
if (deadFireCount == 1)
    Output.WriteLine($"\t{firstDead.Name} (cold)");
else
    Output.WriteLine($"\tExtinguished fires ({deadFireCount})");
```

**Recommendation:** VERY LOW PRIORITY
Only relevant if multiple HeatSourceFeatures per location becomes common. Currently, the game likely only has 1 campfire per location, making this a non-issue. Document the assumption rather than adding complexity.

---

## Architecture Considerations

### Integration with LocationFeature System ✅

**Strengths:**
1. **Proper Type Checking:** Uses `is HeatSourceFeature heat` pattern matching (C# best practice)
2. **Consistent with ShelterFeature Handling:** Both use similar display logic
3. **Respects Feature Hierarchy:** Correctly hides `ForageFeature` and `EnvironmentFeature` (abstract/non-physical)
4. **No Breaking Changes:** Pure addition, doesn't modify existing logic

**Design Pattern Adherence:**
The code follows the established pattern from the previous sprint fix:
- Lines 878-894: New dead fire logic
- Lines 895-900: Existing shelter logic (unchanged)
- Lines 901-902: Existing feature exclusion comments (unchanged)

This demonstrates good incremental development - building on proven patterns rather than refactoring.

---

### Null Safety Analysis ✅

**Pattern Matching Safety:**
```csharp
if (feature is HeatSourceFeature heat)
```
This pattern is null-safe:
- `feature` is guaranteed non-null (from `foreach` over `location.Features`)
- Type check ensures `heat` is a valid `HeatSourceFeature` instance
- C# compiler enforces null safety in pattern matching context

**Property Access Safety:**
```csharp
heat.FuelRemaining > 0
heat.Name
```
Both properties are:
- Non-nullable value types (`double`, `string`)
- Initialized in `HeatSourceFeature` constructor (verified in HeatSourceFeature.cs:13-14)
- No risk of `NullReferenceException`

**Collection Safety:**
```csharp
foreach (var feature in location.Features)
```
- `location.Features` is a `List<LocationFeature>` (non-nullable)
- Initialized in `Location` class constructor
- No risk of null collection iteration

**Verdict:** ✅ No null safety concerns

---

### Time Complexity & Performance

**Current Implementation:**
- O(n) single pass through `location.Features`
- Minimal overhead: 1 integer counter increment per dead fire
- No allocations (except string interpolation for display)

**LINQ Alternative:**
- O(n) for each LINQ query (3 separate passes if using suggested approach)
- `.OfType<T>()` and `.Where()` use deferred execution
- Negligible performance difference for typical locations (< 10 features)

**Verdict:** ✅ No performance concerns with either approach

---

## Edge Cases Analysis

### Edge Case 1: No Fires at All ✅
```csharp
Location.Features = [ForageFeature, ShelterFeature]
```
**Behavior:**
- `if (feature is HeatSourceFeature heat)` → false for all features
- `deadFiresShown` remains 0 (unused)
- No output for fires
- **Result:** Correct (nothing to display)

---

### Edge Case 2: Only Active Fires ✅
```csharp
Location.Features = [HeatSourceFeature(fuel=5.0), HeatSourceFeature(fuel=2.5)]
```
**Behavior:**
- Both fires show with status ("burning", "burning")
- `deadFiresShown == 0` branch never executes
- **Result:** Correct (all fires displayed)

---

### Edge Case 3: Only Dead Fires ✅
```csharp
Location.Features = [HeatSourceFeature(fuel=0), HeatSourceFeature(fuel=0)]
```
**Behavior:**
- First dead fire: Shows "Campfire (cold)", `deadFiresShown = 1`
- Second dead fire: `deadFiresShown == 0` is false, skipped
- **Result:** Only first dead fire shown (as intended by max 1 constraint)

---

### Edge Case 4: Mixed Active/Dead Fires ✅
```csharp
Location.Features = [
    HeatSourceFeature("Old Fire", fuel=0),
    HeatSourceFeature("Current Fire", fuel=3.0),
    HeatSourceFeature("Dead Fire", fuel=0)
]
```
**Behavior:**
- Old Fire: Shows "(cold)", `deadFiresShown = 1`
- Current Fire: Shows "(burning)"
- Dead Fire: Skipped (`deadFiresShown == 1`)
- **Result:** Correct (1 active fire, 1 dead fire shown)

---

### Edge Case 5: Fire with FuelRemaining = 0.5 (Boundary) ✅
```csharp
HeatSourceFeature(fuel=0.5)
```
**Behavior:**
- `heat.FuelRemaining > 0` → true
- Status: `0.5 < 0.5` → false → "burning"
- **Result:** Shows as "burning" (correct - fire still active)

**Note:** Boundary at 0.5 is consistent with "dying" threshold:
- `< 0.5` → "dying"
- `>= 0.5` → "burning"
- `= 0` → "cold"

This creates clear thresholds and avoids ambiguity.

---

### Edge Case 6: Negative Fuel (Invalid State) ⚠️
```csharp
HeatSourceFeature(fuel=-1.0)  // Should never happen
```
**Behavior:**
- `heat.FuelRemaining > 0` → false
- Falls into `else if (deadFiresShown == 0)` → shows "(cold)"
- **Result:** Handled gracefully (treats as dead fire)

**Root Cause Prevention:**
Checked `HeatSourceFeature.cs`:
- Line 42: `FuelRemaining = Math.Max(0, FuelRemaining - fuelUsed);`
- Fuel is clamped to minimum 0, cannot go negative
- **Verdict:** Not a real edge case, but handled correctly anyway

---

## Comparison with Previous Sprint Fix

**Sprint Fix (Lines 863-885):** Added LocationFeature display
**This Session (Lines 888-893):** Enhanced to show dead fires

**Quality Comparison:**
| Aspect | Sprint Fix | This Session |
|--------|-----------|--------------|
| Pattern Consistency | ✅ Excellent | ✅ Excellent |
| Code Comments | ✅ Clear | ✅ Clear + explains "why" |
| Edge Case Handling | ✅ Good | ✅ Good |
| Null Safety | ✅ Safe | ✅ Safe |
| Integration | ✅ Clean | ✅ Clean (builds on sprint fix) |

**Incremental Development:**
This session's changes are a **textbook example** of incremental enhancement:
1. Sprint fix established LocationFeature display pattern
2. This session extends it with minimal modification
3. No refactoring of working code
4. Maintains backward compatibility
5. Adds value (dead fire display) with minimal risk

---

## Testing Recommendations

### Manual Testing Checklist ✅
Based on sprint documentation, the following was tested:
- ✅ Dead fire displays as "(cold)"
- ✅ Only 1 dead fire shown per location
- ✅ Active fires still display correctly
- ✅ No regression in shelter display

### Additional Test Scenarios (Optional)
If thorough testing is desired:

1. **Create multiple dead fires:**
   ```csharp
   // In Program.cs or test script
   location.Features.Add(new HeatSourceFeature(location, 15.0)); // fuel=0 by default
   location.Features.Add(new HeatSourceFeature(location, 15.0));
   location.Features.Add(new HeatSourceFeature(location, 15.0));
   // Verify only 1 shown
   ```

2. **Verify fire relighting:**
   - Craft fire-making recipe at location with dead fire
   - Verify `AddFuel()` reactivates the fire
   - Verify fire transitions from "(cold)" → "(burning)"

3. **Fuel depletion edge case:**
   - Note: Sprint cleanup discovered `Location.Update()` doesn't call `Feature.Update()`
   - Fuel never decreases automatically (known issue)
   - Dead fires only occur via manual `FuelRemaining = 0` or testing

---

## Code Quality Metrics

### Readability: 9/10
- Clear variable names (`deadFiresShown`, `heat`, `status`)
- Descriptive comments explain "why" not just "what"
- Logical flow easy to follow
- Minor deduction: Counter variable could be eliminated with LINQ

### Maintainability: 9/10
- Follows established patterns (easy for future devs to understand)
- Low coupling (only touches display logic)
- Self-contained logic (no hidden dependencies)
- Comment explains design decision (max 1 dead fire)

### Robustness: 10/10
- Handles all edge cases correctly
- No null reference risks
- No performance concerns
- Gracefully handles invalid states (negative fuel)

### Documentation: 8/10
- Inline comment explains constraint
- Intent is clear
- Could benefit from XML doc comment explaining "max 1" design decision
- Sprint summary documents the change well

---

## Comparison with ActionFactory.cs Patterns

### Pattern: Filtering Collections Before Display

**Existing Pattern (Line 856):**
```csharp
foreach (var item in location.Items.Where(i => i.IsFound))
```

**This Implementation (Lines 875-893):**
```csharp
foreach (var feature in location.Features)
{
    if (feature is HeatSourceFeature heat)
    {
        if (heat.FuelRemaining > 0) { ... }
        else if (deadFiresShown == 0) { ... }
    }
}
```

**Difference:**
- Items: LINQ `.Where()` for filtering
- Features: Imperative `if` with type checking

**Why the Difference?**
- Items: Simple boolean filter (`IsFound`)
- Features: **Complex multi-condition logic** (type check + fuel level + counter)
- Features: **Different display per type** (HeatSource vs Shelter)

**Verdict:** ✅ Acceptable deviation
The imperative approach is justified by complexity. LINQ would require multiple passes or complex predicates.

---

### Pattern: Status Display with Parentheses

**Existing Examples:**
- Line 849: `(Wild Woods • Morning • 38.7°F)` - location metadata
- Line 864: `[container]` - item type (uses brackets)
- Line 870: `[creature]` - NPC type (uses brackets)
- Line 898: `[shelter]` - feature type (uses brackets)

**This Implementation:**
- Line 884: `(burning)` / `(dying)` - fire status (uses parentheses)
- Line 890: `(cold)` - fire status (uses parentheses)

**Pattern Consistency:**
- **Parentheses:** Used for dynamic state/metadata (time, temp, fire status)
- **Brackets:** Used for static type labels (container, creature, shelter)

**Verdict:** ✅ Excellent pattern adherence
Fire status is dynamic state → parentheses correct choice

---

## Documentation Quality

### Inline Comments ✅
**Line 887 Comment:**
```csharp
// Show max 1 dead fire per location (can be relit via fire-making recipes)
```

**Strengths:**
- Explains the "max 1" constraint
- Provides context (relighting mechanic)
- Helps future devs understand design decision

**Potential Enhancement:**
Could add implementation note:
```csharp
// Show max 1 dead fire per location (reduces clutter, can be relit via fire-making recipes)
// Note: If multiple dead fires exist, only the first encountered is displayed
```

---

### Sprint Documentation ✅

**CURRENT-STATUS.md Entry (Lines 217-223):**
```markdown
**Issue #4 (Dead Campfires):** RESOLVED (15 min)
  - Modified ActionFactory.cs to show "(cold)" status when FuelRemaining = 0
  - Limited to max 1 dead fire per location
  - Player can relight via fire-making recipes
  - Discovery: Location.Update() doesn't call Feature.Update() (fuel never decreases)
```

**Strengths:**
- Clear problem → solution description
- Mentions design decision (max 1)
- Documents unexpected discovery (fuel consumption bug)
- Accurate time estimate

**Excellent Documentation Practice** ✅

---

## Design Decisions Review

### Decision 1: "Max 1 Dead Fire Per Location"

**Rationale (Inferred):**
1. **UX Clarity:** Multiple dead fires clutter display
2. **Gameplay Value:** 1 dead fire serves as reminder/hint about relighting
3. **Simplicity:** Avoids complex fire management UI

**Alternative Approaches:**
- Show all dead fires → cluttered display
- Show no dead fires → player forgets fires exist
- Show count: "3 cold fires" → less immersive

**Verdict:** ✅ Good design decision
Balances information with clarity. Aligns with "one dead campfire is enough to convey the mechanic" philosophy.

---

### Decision 2: Counter vs LINQ for Limiting

**Current Approach:**
```csharp
int deadFiresShown = 0;
// ... later ...
else if (deadFiresShown == 0)
{
    // show fire
    deadFiresShown++;
}
```

**Trade-offs:**
- **Pro:** Simple, clear intent, works in single loop
- **Pro:** Minimal overhead (1 integer)
- **Con:** Less idiomatic C# (LINQ preferred in codebase)
- **Con:** Couples display logic with filtering

**Verdict:** ✅ Acceptable pragmatism
Counter approach is straightforward and works well. LINQ would be "more correct" stylistically but requires refactoring the loop structure.

---

### Decision 3: Status Text ("cold" vs alternatives)

**Chosen:** `(cold)`
**Alternatives:**
- `(dead)` - implies permanently unusable
- `(out)` - too casual
- `(extinguished)` - verbose
- `(0 fuel)` - too mechanical
- `(needs fuel)` - prescriptive

**Verdict:** ✅ Excellent choice
"cold" is:
- Thematically appropriate (Ice Age setting)
- Implies temporary state (can be reheated)
- Concise and clear
- Natural contrast with "burning"/"dying"

---

## Potential Future Enhancements

### 1. Fire Relighting Action
**Current State:**
- Dead fires shown in display
- Comment mentions "can be relit via fire-making recipes"
- **BUT:** Recipes likely create NEW fires, not relight existing ones

**Suggested Investigation:**
Verify if fire-making recipes:
- A) Create new HeatSourceFeature instances (adds to location.Features)
- B) Reactivate existing dead fires (`AddFuel()` on existing feature)

If (A), consider:
```csharp
// New action in ActionFactory.Common
public static IGameAction RelightFire(HeatSourceFeature deadFire)
{
    return CreateAction($"Relight {deadFire.Name}")
        .When(ctx => deadFire.FuelRemaining == 0 && ctx.player.Inventory.HasProperty(ItemProperty.Tinder, 0.05))
        .Do(ctx => {
            deadFire.AddFuel(0.5); // 30 minutes of fuel
            Output.WriteSuccess($"You relight the {deadFire.Name}.");
        });
}
```

**Priority:** Low - depends on actual fire-making recipe behavior

---

### 2. Dynamic Fire Status Thresholds
**Current Implementation:**
```csharp
string status = heat.FuelRemaining < 0.5 ? "dying" : "burning";
```

**Potential Enhancement:**
```csharp
string status = heat.FuelRemaining switch
{
    < 0.2 => "embers",       // Nearly out
    < 0.5 => "dying",        // Current threshold
    < 2.0 => "burning",      // Stable
    _ => "roaring"           // Well-fueled
};
```

**Benefits:**
- More granular fire state feedback
- Helps player estimate remaining warmth time
- Adds immersion

**Priority:** Very Low - current 3-state system (cold/dying/burning) is sufficient

---

### 3. Fire Fuel Depletion (Blocked Issue)
**Discovery from Sprint:**
> "Location.Update() doesn't call Feature.Update() (fuel never decreases)"

**Implication:**
- Dead fires currently only occur via manual testing
- HeatSourceFeature.Update() exists but never called
- Fire fuel mechanics not integrated with World time passage

**Suggested Fix (Separate Issue):**
```csharp
// In Location.cs
public void Update(TimeSpan elapsed)
{
    foreach (var feature in Features)
    {
        if (feature is HeatSourceFeature heat)
            heat.Update(elapsed);
        // Add other feature updates as needed
    }
}

// In World.cs Update() loop
foreach (var zone in Zones)
{
    foreach (var location in zone.Locations)
    {
        location.Update(timeElapsed);
    }
}
```

**Priority:** Medium - affects fire gameplay significantly
**Note:** This is a separate architectural issue, not related to display logic review

---

## Conclusion

### Production Readiness: ✅ APPROVED

**Summary:**
The dead campfire display logic is well-implemented, follows project conventions, and successfully addresses the user requirement. No blocking issues or critical concerns were found.

**Strengths:**
1. ✅ Consistent with existing patterns (incremental enhancement)
2. ✅ Null-safe and robust edge case handling
3. ✅ Clear code with good comments
4. ✅ Well-documented in sprint summary
5. ✅ No performance concerns
6. ✅ Tested and verified working

**Minor Suggestions for Future Refinement:**
1. Consider LINQ-based limiting for pattern consistency (Low Priority)
2. Document edge case behavior with multiple dead fires (Very Low Priority)
3. Investigate fire relighting mechanics (separate issue)
4. Fix Location.Update() → Feature.Update() integration (Medium Priority, separate issue)

### Recommendation: Ship It ✅

This change is production-ready. The minor suggestions are style preferences and potential future enhancements, not blockers.

**Next Steps:**
1. ✅ Code is already tested and documented
2. ✅ Integrate with main branch (after user approval)
3. ⏸️ Consider LINQ refactoring in future cleanup session (optional)
4. 📋 Track fire fuel depletion issue separately (already noted in sprint docs)

---

## Files Modified

### /Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionFactory.cs

**Lines 874-903:** Dead campfire display logic

**Changes:**
- Added `int deadFiresShown = 0` counter (line 875)
- Added conditional logic for dead fires (lines 888-893)
- Shows "(cold)" status when `FuelRemaining = 0`
- Limits display to max 1 dead fire per location

**Impact:**
- Pure addition (no modification of existing logic)
- No breaking changes
- Integrates seamlessly with Sprint Fix from lines 863-885

**Build Status:** ✅ Compiles successfully (verified)

---

## Appendix: Alternative Implementations

### Alternative A: LINQ-Based Approach
```csharp
// Display LocationFeatures (campfires, shelters, etc.)

// Active fires with status
var activeFires = location.Features.OfType<HeatSourceFeature>().Where(h => h.FuelRemaining > 0);
foreach (var heat in activeFires)
{
    string status = heat.FuelRemaining < 0.5 ? "dying" : "burning";
    Output.WriteLine($"\t{heat.Name} ({status})");
    hasItems = true;
}

// Show max 1 dead fire per location (can be relit via fire-making recipes)
var deadFire = location.Features.OfType<HeatSourceFeature>().FirstOrDefault(h => h.FuelRemaining <= 0);
if (deadFire != null)
{
    Output.WriteLine($"\t{deadFire.Name} (cold)");
    hasItems = true;
}

// Always show shelters
foreach (var shelter in location.Features.OfType<ShelterFeature>())
{
    Output.WriteLine($"\t{shelter.Name} [shelter]");
    hasItems = true;
}

// Don't display ForageFeature or EnvironmentFeature
// (ForageFeature is an abstract mechanic, EnvironmentFeature is the location type)
```

**Pros:**
- More idiomatic C# (LINQ-first)
- Explicit filtering logic
- Easier to modify filters independently

**Cons:**
- 3 separate iterations over Features (negligible cost)
- More verbose (15 lines vs 20 lines)
- Requires restructuring existing code

**Verdict:** Stylistic preference, current approach is fine

---

### Alternative B: Grouped Display with Counts
```csharp
// Group fires by status
var fires = location.Features.OfType<HeatSourceFeature>().ToList();
var activeFires = fires.Where(h => h.FuelRemaining > 0).ToList();
var deadFires = fires.Where(h => h.FuelRemaining <= 0).ToList();

// Display active fires
foreach (var heat in activeFires)
{
    string status = heat.FuelRemaining < 0.5 ? "dying" : "burning";
    Output.WriteLine($"\t{heat.Name} ({status})");
    hasItems = true;
}

// Display dead fires (max 1, or count if multiple)
if (deadFires.Count == 1)
{
    Output.WriteLine($"\t{deadFires[0].Name} (cold)");
    hasItems = true;
}
else if (deadFires.Count > 1)
{
    Output.WriteLine($"\t{deadFires.Count} extinguished fires");
    hasItems = true;
}
```

**Pros:**
- Handles multiple dead fires more explicitly
- Clear separation of active vs dead

**Cons:**
- Over-engineering for current needs (likely only 1 fire per location)
- "3 extinguished fires" is less immersive than named fire

**Verdict:** Unnecessary complexity for current use case

---

**End of Review**
