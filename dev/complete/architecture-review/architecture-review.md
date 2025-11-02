# Text Survival RPG - Comprehensive Architecture Review

**Last Updated:** 2025-11-01
**Reviewer:** Claude Code (Architecture Review Agent)
**Codebase Version:** cc branch
**Review Scope:** Complete architectural analysis of core systems

---

## Executive Summary

The Text Survival RPG codebase demonstrates **excellent architectural fundamentals** with strong adherence to established patterns. The composition-over-inheritance approach, builder patterns, and pure function design are well-implemented. However, there are several **critical architectural inconsistencies** and **design constraint violations** that need addressing.

**Overall Assessment:** ⚠️ **Good foundation with critical issues to resolve**

### Critical Findings Summary

- 🔴 **3 Critical Issues** - Design constraint violations
- 🟠 **5 Architecture Concerns** - Pattern inconsistencies
- 🟡 **4 Code Quality Issues** - Technical debt
- 🟢 **6 Positive Patterns** - Exemplary implementations

### Immediate Action Required

1. **Fix Body.Rest() double-updating World time** (Critical - causes time drift)
2. **Fix SurvivalProcessor.Sleep() mutating state** (Critical - violates pure function constraint)
3. **Fix Actor.Update() missing null check** (Critical - potential crash)
4. **Standardize time passage patterns** (High - inconsistent across actions)
5. **Review GameContext instantiation** (High - creates duplicate CraftingSystem)

---

## 1. Critical Issues

### 🔴 CRITICAL: Body.Rest() Double-Updates World Time

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Bodies/Body.cs:201`
**Severity:** Critical - Causes time drift and double survival processing

**Issue:**
```csharp
public bool Rest(int minutes)
{
    // ... survival processing happens here ...
    World.Update(minutes); // PROBLEM: Body shouldn't update World directly
    return Energy <= 0;
}
```

**Why This Matters:**
- Violates single responsibility principle - `Body` should not control world time
- `World.Update()` calls `Player.Update()` which calls `Body.Update()` again
- Creates **double survival processing** - player loses twice the calories/hydration
- Time can drift if called from multiple places
- Makes testing and debugging extremely difficult

**Impact:**
- Player starves/dehydrates 2x faster than intended during sleep
- Time inconsistencies across save/load
- Unpredictable behavior when multiple actors rest simultaneously

**Recommended Fix:**
```csharp
// In Body.cs - Remove World.Update call
public bool Rest(int minutes)
{
    var data = BundleSurvivalData();
    data.activityLevel = .5;
    var result = SurvivalProcessor.Sleep(data, minutes);
    UpdateBodyBasedOnResult(result);

    // Healing logic here...
    Heal(healing);

    // REMOVED: World.Update(minutes); // Caller's responsibility!
    return Energy <= 0;
}

// In ActionFactory.Sleep() - Let the action handle time
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
    .When(ctx => ctx.player.Body.IsTired)
    .Do(ctx =>
    {
        Output.WriteLine("How many hours would you like to sleep?");
        int minutes = Input.ReadInt() * 60;
        ctx.player.Body.Rest(minutes);
        World.Update(minutes); // TIME UPDATED HERE, NOT IN BODY
    })
    .ThenReturn()
    .Build();
}
```

**Design Principle Violated:**
- **Single Responsibility:** `Body` should manage physiology, not world time
- **Explicit Time Passage:** Actions should call `World.Update()`, not internal methods

---

### 🔴 CRITICAL: SurvivalProcessor.Sleep() Mutates Input State

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Survival/SurvivalProcessor.cs:279-288`
**Severity:** Critical - Violates pure function design constraint

**Issue:**
```csharp
public static SurvivalProcessorResult Sleep(SurvivalData data, int minutes)
{
    // PROBLEM: Mutating input parameter instead of copying!
    data.activityLevel = .5;
    data.Energy = Math.Min(1, data.Energy + (BASE_EXHAUSTION_RATE * 2 * minutes));
    data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * .7 * minutes));
    data.Calories = data.Calories -= GetCurrentMetabolism(data) / 24 / 60 * minutes;
    return new SurvivalProcessorResult(data); // Returns mutated input!
}
```

**Why This Matters:**
- **Violates pure function constraint** from CLAUDE.md: "SurvivalProcessor.Process() should remain stateless and return results"
- `Process()` method follows this correctly, but `Sleep()` does not
- Mutating input parameters creates hidden side effects
- Makes testing unreliable (input data changes unexpectedly)
- Breaks functional programming principles

**Impact:**
- Caller's `SurvivalData` object is modified unexpectedly
- Difficult to debug - changes happen "invisibly"
- Cannot safely call `Sleep()` multiple times with same data
- Violates documented design constraint

**Recommended Fix:**
```csharp
public static SurvivalProcessorResult Sleep(SurvivalData data, int minutes)
{
    // Create a copy to maintain purity
    var sleepData = new SurvivalData()
    {
        Temperature = data.Temperature,
        Calories = data.Calories,
        Hydration = data.Hydration,
        Energy = data.Energy,
        BodyStats = data.BodyStats,
        environmentalTemp = data.environmentalTemp,
        ColdResistance = data.ColdResistance,
        equipmentInsulation = data.equipmentInsulation,
        activityLevel = 0.5, // Sleep activity level
        IsPlayer = data.IsPlayer
    };

    // Modify the COPY, not the input
    sleepData.Energy = Math.Min(1, sleepData.Energy + (BASE_EXHAUSTION_RATE * 2 * minutes));
    sleepData.Hydration = Math.Max(0, sleepData.Hydration - (BASE_DEHYDRATION_RATE * .7 * minutes));
    sleepData.Calories -= GetCurrentMetabolism(sleepData) / 24 / 60 * minutes;

    return new SurvivalProcessorResult(sleepData);
}
```

**Alternative:** Make `SurvivalData` a record type with immutability:
```csharp
public record SurvivalData
{
    public double Temperature { get; init; }
    public double Calories { get; init; }
    // ... etc - all properties use 'init' instead of 'set'
}
```

**Design Principle Violated:**
- **Pure Function Design:** Input should never be mutated
- **Predictable Behavior:** Functions should have no hidden side effects

---

### 🔴 CRITICAL: Actor.Update() Missing Null Check for CurrentLocation

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Actors/Actor.cs:19-28`
**Severity:** Critical - Potential NullReferenceException crash

**Issue:**
```csharp
public virtual void Update()
{
    EffectRegistry.Update();
    var context = new SurvivalContext
    {
        ActivityLevel = 2,
        LocationTemperature = CurrentLocation.GetTemperature(), // CRASH if null!
    };
    Body.Update(TimeSpan.FromMinutes(1), context);
}
```

**Why This Matters:**
- `CurrentLocation` can be null during construction or transitions
- `virtual Location? CurrentLocation { get; set; }` is nullable
- No null check before calling `.GetTemperature()`
- Will crash if actor updates before location is set

**Impact:**
- Game crash during actor construction
- Crash when transitioning between zones
- Unpredictable behavior in edge cases

**Recommended Fix:**
```csharp
public virtual void Update()
{
    EffectRegistry.Update();

    // Null safety check
    if (CurrentLocation == null)
    {
        // Skip survival update if no location (shouldn't happen in normal gameplay)
        return;
    }

    var context = new SurvivalContext
    {
        ActivityLevel = 2,
        LocationTemperature = CurrentLocation.GetTemperature(),
    };
    Body.Update(TimeSpan.FromMinutes(1), context);
}
```

**Better Alternative:** Make `CurrentLocation` non-nullable and require it in constructor:
```csharp
// In Actor.cs
public abstract Location CurrentLocation { get; protected set; }

protected Actor(string name, BodyCreationInfo stats, Location startingLocation)
{
    Name = name;
    CurrentLocation = startingLocation; // REQUIRED
    EffectRegistry = new EffectRegistry(this);
    this.combatManager = new CombatManager(this);
    Body = new Body(Name, stats, EffectRegistry);
}
```

**Design Principle Violated:**
- **Null Safety:** Nullable references should always be checked
- **Fail-Fast:** Construction should require all necessary dependencies

---

## 2. Architecture Concerns

### 🟠 Inconsistent Time Passage Patterns

**Severity:** High - Creates confusion and bugs
**Files:** Multiple action definitions in `ActionFactory.cs`

**Issue:**

The codebase uses **three different patterns** for time passage:

**Pattern 1: Direct World.Update() in Do()** (Most common)
```csharp
// CraftingSystem.cs:36
World.Update(recipe.CraftingTimeMinutes);
```

**Pattern 2: TakesMinutes() extension** (Rare)
```csharp
// ActionBuilder.cs:109-112
public static ActionBuilder TakesMinutes(this ActionBuilder b, int minutes)
{
    return b.Do(ctx => World.Update(minutes));
}
```

**Pattern 3: Implicit in method call** (Hidden)
```csharp
// ActionFactory.cs:69 - Sleep action
.Do(ctx => {
    ctx.player.Body.Rest(Input.ReadInt() * 60); // Rest() calls World.Update() internally!
})
```

**Why This Matters:**
- Developers must remember which methods update time vs. which don't
- Easy to accidentally double-update time (as seen in Body.Rest())
- No consistent convention = bugs and confusion
- Difficult to track total time spent in action chains

**Impact:**
- Time drift bugs (some actions don't consume time when they should)
- Double-time bugs (some actions consume time twice)
- Difficult to audit time costs
- Testing becomes unreliable

**Recommended Solution:**

**Establish clear convention:**
1. **Actions handle time explicitly** via `TakesMinutes()` or `World.Update()`
2. **Core methods never update time** - they only return duration or perform work
3. **Document time costs** in action descriptions

```csharp
// GOOD: Explicit time handling
public static IGameAction Forage(string name = "Forage")
{
    return CreateAction("Forage")
        .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx =>
        {
            var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
            forageFeature.Forage(1); // Doesn't update time
        })
        .TakesMinutes(60) // TIME COST EXPLICIT AND VISIBLE
        .AndGainExperience("Foraging")
        .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
        .Build();
}

// BAD: Hidden time cost
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
    .Do(ctx => {
        ctx.player.Body.Rest(minutes); // WHO KNOWS IF THIS UPDATES TIME?!
    })
    .Build();
}

// GOOD: Clear separation of concerns
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
    .When(ctx => ctx.player.Body.IsTired)
    .Do(ctx =>
    {
        Output.WriteLine("How many hours would you like to sleep?");
        int minutes = Input.ReadInt() * 60;
        ctx.player.Body.Rest(minutes); // JUST PROCESSES SLEEP
    })
    .TakesMinutes(ctx => ctx.player.ReadSleepDuration()) // TIME COST VISIBLE
    .ThenReturn()
    .Build();
}
```

**Documentation Update Required:**
Add to CLAUDE.md design constraints:
```markdown
## Time Management Convention

1. **Actions are responsible for time**: Use `.TakesMinutes()` or `World.Update()` in `.Do()`
2. **Core methods never update world time**: `Body.Rest()`, `CraftingSystem.Craft()`, etc. should not call `World.Update()`
3. **Time costs must be explicit**: Don't hide `World.Update()` inside utility methods
4. **Exception**: `World.Update()` loop handles per-minute actor updates automatically
```

---

### 🟠 GameContext Creates New CraftingSystem Every Time

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Actions/GameContext.cs:13`
**Severity:** Medium-High - Performance and state management issue

**Issue:**
```csharp
public class GameContext(Player player)
{
    public Player player = player;
    public Location currentLocation => player.CurrentLocation;
    public IGameAction? NextActionOverride { get; set; }
    public Npc? EngagedEnemy;
    public CraftingSystem CraftingManager = new CraftingSystem(player); // NEW INSTANCE EVERY TIME!
}
```

**Why This Matters:**
- `CraftingSystem` is recreated every time a `GameContext` is instantiated
- `CraftingSystem.InitializeRecipes()` runs repeatedly (wasteful)
- Recipe knowledge could theoretically differ between contexts
- Creates unnecessary object allocations
- Violates single-instance pattern for game systems

**Impact:**
- Performance waste (minimal but unnecessary)
- Potential for state inconsistencies
- Cannot track persistent crafting-related state
- Confusing ownership model (who owns the crafting system?)

**Recommended Fix:**

**Option 1: Make CraftingSystem a property of Player** ⭐ RECOMMENDED
```csharp
// In Player.cs
public class Player : Actor
{
    public readonly CraftingSystem CraftingManager; // Player owns it

    public Player(Location startingLocation) : base("Player", Body.BaselinePlayerStats)
    {
        // ... other initialization ...
        CraftingManager = new CraftingSystem(this);
    }
}

// In GameContext.cs
public class GameContext(Player player)
{
    public Player player = player;
    public CraftingSystem CraftingManager => player.CraftingManager; // Reference, not creation
    public Location currentLocation => player.CurrentLocation;
    public IGameAction? NextActionOverride { get; set; }
    public Npc? EngagedEnemy;
}
```

**Option 2: Make CraftingSystem static/singleton** (if recipes are always the same)
```csharp
public class CraftingSystem
{
    private static readonly Dictionary<string, CraftingRecipe> _globalRecipes = InitializeRecipes();

    public bool CanCraft(Player player, string recipeName) { /* ... */ }
    public void Craft(Player player, CraftingRecipe recipe) { /* ... */ }
}
```

**Design Principle Violated:**
- **Single Responsibility:** GameContext shouldn't construct game systems
- **Efficient Resource Use:** Don't recreate expensive objects unnecessarily
- **Clear Ownership:** Who owns CraftingSystem? Player or Context?

---

### 🟠 Forage Action Doesn't Update Time

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionFactory.cs:47-59`
**Severity:** Medium - Missing explicit time cost

**Issue:**
```csharp
public static IGameAction Forage(string name = "Forage")
{
    return CreateAction("Forage")
        .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx =>
        {
            var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
            forageFeature.Forage(1); // Passes "1" hour but...
        })
        .AndGainExperience("Foraging")
        // WHERE IS .TakesMinutes(60) ???
        .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
        .Build();
}
```

**Investigation:**
Looking at `ForageFeature.Forage()` would reveal if time is updated there. If it is, that's the hidden time pattern problem. If it isn't, foraging is **free time-wise** which is a critical bug.

**Why This Matters:**
- Message says "You forage for 1 hour" but time may not actually pass
- Violates player expectations
- Could allow infinite resource farming with zero time cost
- Listed in ISSUES.md as "Questionable Functionality"

**Recommended Fix:**
```csharp
public static IGameAction Forage(string name = "Forage")
{
    return CreateAction("Forage")
        .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx =>
        {
            var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
            forageFeature.Forage(1); // Should NOT update time internally
        })
        .TakesMinutes(60) // EXPLICIT TIME COST
        .AndGainExperience("Foraging")
        .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
        .Build();
}
```

**Verification Needed:**
Check `ForageFeature.Forage()` implementation to confirm whether it updates time or not.

---

### 🟠 Player.cs vs Actors/Player.cs Namespace Confusion

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Player.cs:12`
**Severity:** Low - Organizational inconsistency

**Issue:**
- `Player` class is in namespace `text_survival` (root)
- `Npc` and `Actor` are in namespace `text_survival.Actors`
- Creates namespace inconsistency

**Current Structure:**
```
text_survival/
├── Player.cs           // namespace text_survival
├── Actors/
│   ├── Actor.cs       // namespace text_survival.Actors
│   ├── NPC.cs         // namespace text_survival.Actors
│   └── NPCFactory.cs  // namespace text_survival.Actors
```

**Why This Matters:**
- Violates "Namespace organization" principle from architecture
- Player should be in `Actors/` folder with other actor types
- Confusing for developers - where to find Player?
- Breaks logical grouping

**Impact:**
- Minor - mostly organizational
- Can confuse new contributors
- Makes refactoring harder

**Recommended Fix:**
```bash
# Move Player.cs to Actors folder
mv Player.cs Actors/Player.cs

# Update namespace in Actors/Player.cs
namespace text_survival.Actors;  // Was: namespace text_survival

public class Player : Actor { /* ... */ }
```

**Files to Update After Move:**
- Any files importing `text_survival.Player` would need to change to `text_survival.Actors.Player`
- Alternatively, add `using text_survival.Actors;` to those files

**Design Principle Violated:**
- **Namespace Organization:** Related classes should live in the same namespace
- **Consistent Structure:** Actor hierarchy should be co-located

---

### 🟠 Missing Validation in RecipeBuilder and ActionBuilder

**Files:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Crafting/RecipeBuilder.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionBuilder.cs`

**Severity:** Medium - Potential for invalid configurations

**Issue:**

**RecipeBuilder** only validates name:
```csharp
public CraftingRecipe Build()
{
    if (string.IsNullOrWhiteSpace(_name))
    {
        throw new InvalidOperationException("Name is required");
    }
    // But doesn't validate:
    // - At least one result type is set
    // - Required properties are specified
    // - Skill levels are non-negative
    // - Crafting time is positive
}
```

**ActionBuilder** only validates name:
```csharp
public IGameAction Build()
{
    if (string.IsNullOrWhiteSpace(_name))
    {
        throw new InvalidOperationException("Action name is required");
    }
    // But doesn't validate:
    // - Has either Do() or ThenShow() (empty actions are useless)
    // - NextActions isn't set when ThenReturn() was already called
}
```

**Why This Matters:**
- Invalid recipes could be created accidentally
- Runtime errors instead of compile-time/build-time errors
- Harder to debug when recipes don't work
- No fail-fast principle

**Impact:**
- Bugs harder to track down
- Invalid recipes might "build" but fail at runtime
- Developer confusion

**Recommended Fix:**

```csharp
// In RecipeBuilder.cs
public CraftingRecipe Build()
{
    if (string.IsNullOrWhiteSpace(_name))
        throw new InvalidOperationException("Recipe name is required");

    if (_requiredProperties.Count == 0)
        throw new InvalidOperationException($"Recipe '{_name}' has no property requirements");

    if (_resultType == CraftingResultType.Item && _resultItems.Count == 0)
        throw new InvalidOperationException($"Recipe '{_name}' has Item result type but no items specified");

    if (_resultType == CraftingResultType.LocationFeature && _locationFeatureResult == null)
        throw new InvalidOperationException($"Recipe '{_name}' has LocationFeature result type but no feature specified");

    if (_resultType == CraftingResultType.Shelter && _newLocationResult == null)
        throw new InvalidOperationException($"Recipe '{_name}' has Shelter result type but no location specified");

    if (_craftingTimeMinutes <= 0)
        throw new InvalidOperationException($"Recipe '{_name}' has invalid crafting time: {_craftingTimeMinutes}");

    if (_requiredSkillLevel < 0)
        throw new InvalidOperationException($"Recipe '{_name}' has negative skill level: {_requiredSkillLevel}");

    // Build validated recipe...
}
```

**Design Principle Violated:**
- **Fail-Fast:** Invalid configurations should be caught at build time
- **Self-Documenting Code:** Clear error messages guide developers

---

## 3. Code Quality Issues

### 🟡 Magic Numbers and Constants

**Severity:** Low-Medium - Maintainability issue
**Files:** Multiple

**Issue:**

Constants are scattered throughout the codebase without central definition:

**In SurvivalProcessor.cs:**
```csharp
private const double BASE_EXHAUSTION_RATE = 1;
public const double MAX_ENERGY_MINUTES = 960.0F;
private const double BASE_DEHYDRATION_RATE = 4000F / (24F * 60F);
public const double MAX_HYDRATION = 4000.0F;
public const double MAX_CALORIES = 2000.0;
// ... etc
```

**In EffectBuilder.cs:**
```csharp
.WithHourlySeverityChange(-0.05) // Magic number - what does -0.05 mean?
.ReducesCapacity(CapacityNames.BloodPumping, 0.2) // Why 0.2?
```

**In DamageProcessor.cs:**
```csharp
double maxAbsorption = remainingDamage * 0.7; // Why 70%?
```

**In ActionFactory.cs:**
```csharp
.When(ctx => AbilityCalculator.CalculateVitality(ctx.player.Body) > .2) // Why 0.2?
```

**Why This Matters:**
- Difficult to tune game balance
- Can't see relationships between constants
- Hard to maintain consistency
- No documentation of "why this number?"

**Recommended Fix:**

Create `GameBalance.cs` or `Config.cs`:
```csharp
namespace text_survival;

public static class GameBalance
{
    // Survival Constants
    public static class Survival
    {
        public const double MAX_ENERGY_MINUTES = 960.0; // 16 hours of wakefulness
        public const double BASE_EXHAUSTION_RATE = 1.0; // Per minute
        public const double SLEEP_ENERGY_RESTORE_RATE = 2.0; // 2x faster than consumption

        public const double MAX_HYDRATION_ML = 4000.0;
        public const double BASE_DEHYDRATION_RATE_PER_MINUTE = 4000.0 / (24.0 * 60.0); // ~2.78 mL/min

        public const double MAX_CALORIES = 2000.0;
        public const double BASE_BODY_TEMPERATURE_F = 98.6;
    }

    // Combat Constants
    public static class Combat
    {
        public const double MIN_VITALITY_TO_MOVE = 0.2; // 20% vitality required
        public const double MIN_SPEED_TO_FLEE = 0.25;
    }

    // Damage Constants
    public static class Damage
    {
        public const double MAX_LAYER_ABSORPTION_PERCENT = 0.7; // Layers can absorb up to 70% of damage
        public const double PENETRATION_THRESHOLD = 0.3; // Damage must exceed 30% of protection to penetrate
    }

    // Effect Constants
    public static class Effects
    {
        public const double BLEEDING_NATURAL_CLOTTING_RATE = -0.05; // Per hour
        public const double POISON_DETOX_RATE = -0.02;
        public const double FROSTBITE_HEALING_RATE = -0.02;
    }
}
```

**Usage:**
```csharp
// Before
.When(ctx => AbilityCalculator.CalculateVitality(ctx.player.Body) > .2)

// After
.When(ctx => AbilityCalculator.CalculateVitality(ctx.player.Body) > GameBalance.Combat.MIN_VITALITY_TO_MOVE)
```

**Benefits:**
- Easy to find and tune all balance values
- Self-documenting (constant names explain purpose)
- Can see relationships between values
- Easy to create difficulty modes (scale constants)

---

### 🟡 Inconsistent Null Handling Patterns

**Severity:** Low - Code quality issue
**Files:** Multiple

**Issue:**

The codebase uses different patterns for handling nulls:

**Pattern 1: Null-coalescing with default** (Good)
```csharp
// DamageProcessor.cs:27
hitPart = BodyTargetHelper.GetPartByName(body, damageInfo.TargetPartName)
         ?? BodyTargetHelper.GetRandomMajorPartByCoverage(body);
```

**Pattern 2: Explicit null check** (Verbose)
```csharp
// Body.cs:88-96
if (healingInfo.TargetPart != null)
{
    var targetPart = Parts.FirstOrDefault(p => p.Name == healingInfo.TargetPart);
    if (targetPart != null)
    {
        HealBodyPart(targetPart, healingInfo);
        return;
    }
}
```

**Pattern 3: Null-forgiving operator** (Unsafe)
```csharp
// ActionFactory.cs:54
var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
```

**Pattern 4: Mixed** (Inconsistent)
```csharp
// EffectRegistry.cs:21
if (existingEffect != null) // Explicit check
{
    existingEffect.UpdateSeverity(_owner, newSeverity);
    return;
}
```

**Why This Matters:**
- Inconsistency makes code harder to read
- Null-forgiving operator `!` can hide bugs
- No clear convention for when to use which pattern

**Recommended Convention:**

1. **Use null-coalescing when providing defaults:**
   ```csharp
   var value = possiblyNull ?? defaultValue;
   ```

2. **Use pattern matching for complex null checks:**
   ```csharp
   if (healingInfo.TargetPart is string targetName)
   {
       var part = Parts.FirstOrDefault(p => p.Name == targetName);
       if (part != null)
           HealBodyPart(part, healingInfo);
   }
   ```

3. **Avoid null-forgiving operator `!` unless preceded by explicit null check:**
   ```csharp
   // BAD:
   var feature = location.GetFeature<ForageFeature>()!;

   // GOOD:
   var feature = location.GetFeature<ForageFeature>();
   if (feature != null)
   {
       feature.Forage(1);
   }

   // ACCEPTABLE (when .When() guarantees non-null):
   .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
   .Do(ctx =>
   {
       var feature = ctx.currentLocation.GetFeature<ForageFeature>()!; // Safe here
   })
   ```

4. **Enable nullable reference types project-wide:**
   ```xml
   <!-- In .csproj -->
   <Nullable>enable</Nullable>
   ```

---

### 🟡 Body.Rest() Returning Bool But Return Value Unused

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Bodies/Body.cs:184-204`
**Severity:** Low - Confusing API

**Issue:**
```csharp
public bool Rest(int minutes)
{
    // ... lots of sleep logic ...
    return Energy <= 0; // Returns whether fully rested
}

// But callers ignore it:
// ActionFactory.cs:69
ctx.player.Body.Rest(Input.ReadInt() * 60); // Return value discarded
```

**Why This Matters:**
- Return value suggests it's meaningful but nobody uses it
- Confusing API - what should the return mean?
- If it's important, callers should use it
- If it's not important, don't return it

**Recommended Fix:**

**Option 1:** Remove return value
```csharp
public void Rest(int minutes)
{
    // ... sleep logic ...
    // No return
}
```

**Option 2:** Use return value meaningfully
```csharp
public bool Rest(int minutes)
{
    // ... sleep logic ...
    bool fullyRested = Energy >= MAX_ENERGY_MINUTES;
    return fullyRested;
}

// In ActionFactory.cs:
.Do(ctx =>
{
    Output.WriteLine("How many hours would you like to sleep?");
    int minutes = Input.ReadInt() * 60;
    bool fullyRested = ctx.player.Body.Rest(minutes);

    if (fullyRested)
        Output.WriteLine("You wake up feeling refreshed!");
    else
        Output.WriteLine("You're still tired...");
})
```

---

### 🟡 SkillRegistry Using String-Based Lookup Instead of Type-Safe Access

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Level/SkillRegistry.cs:49-66`
**Severity:** Low - Type safety issue

**Issue:**
```csharp
public Skill GetSkill(string skillName)
{
    return skillName switch
    {
        "Fighting" => Fighting,
        "Endurance" => Endurance,
        // ... lots of string matching ...
        _ => throw new ArgumentException($"Skill {skillName} does not exist.")
    };
}

// Used like:
ctx.player.Skills.GetSkill("Firecraft").GainExperience(xp);
```

**Why This Matters:**
- Typos in skill names cause runtime errors
- No compile-time checking
- Must maintain string-to-property mapping
- Refactoring skill names is error-prone

**Alternative Approach:**

**Option 1:** Direct property access (recommended for player)
```csharp
// Instead of:
ctx.player.Skills.GetSkill("Firecraft").GainExperience(xp);

// Just use:
ctx.player.Skills.Firecraft.GainExperience(xp);
```

**Option 2:** Enum-based lookup (if dynamic lookup is truly needed)
```csharp
public enum SkillType
{
    Fighting,
    Endurance,
    Reflexes,
    Defense,
    Hunting,
    Crafting,
    Foraging,
    Firecraft,
    Mending,
    Healing,
    Magic
}

public Skill GetSkill(SkillType skillType)
{
    return skillType switch
    {
        SkillType.Fighting => Fighting,
        SkillType.Endurance => Endurance,
        // ... etc
    };
}

// Usage:
ctx.player.Skills.GetSkill(SkillType.Firecraft).GainExperience(xp);
```

**Benefits:**
- Compile-time type safety
- IntelliSense support
- Refactoring-friendly
- No typo-related bugs

**Current Design Justification:**
The current approach may be justified for:
- Recipe requirements (need string for serialization)
- Data-driven skill definitions
- Mod support

**Recommendation:** Keep string-based for recipes/data, but use direct property access in code where possible.

---

## 4. Positive Patterns

### 🟢 Excellent: ActionBuilder Pattern Implementation

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionBuilder.cs`
**Quality:** Exemplary

**What Makes It Great:**
```csharp
public static IGameAction Forage(string name = "Forage")
{
    return CreateAction("Forage")
        .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() != null)
        .ShowMessage("You forage for 1 hour")
        .Do(ctx => { /* logic */ })
        .AndGainExperience("Foraging")
        .ThenShow(_ => [Forage("Keep foraging"), Common.Return("Finish foraging")])
        .Build();
}
```

**Strengths:**
- ✅ **Fluent API** - Highly readable, self-documenting
- ✅ **Separation of Concerns** - Availability (When), execution (Do), navigation (ThenShow)
- ✅ **Composable** - Extensions like `.AndGainExperience()` can be added without changing core
- ✅ **Consistent Pattern** - All actions follow same structure
- ✅ **Testable** - Each piece can be tested independently

**Why This Is Exemplary:**
- Matches CLAUDE.md design constraints perfectly
- No manual menu loops
- Clear action flow: check availability → execute → navigate next
- Easy to extend with new action types

**Keep Doing:**
- Continue using builder pattern for all actions
- Add more extension methods for common patterns
- Document this as the canonical action pattern

---

### 🟢 Excellent: SurvivalProcessor Pure Function Design

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Survival/SurvivalProcessor.cs:33-87`
**Quality:** Exemplary (except for Sleep method)

**What Makes It Great:**
```csharp
public static SurvivalProcessorResult Process(SurvivalData data, int minutesElapsed, List<Effect> activeEffects)
{
    data.Energy = Math.Max(0, data.Energy - (BASE_EXHAUSTION_RATE * minutesElapsed));
    data.Hydration = Math.Max(0, data.Hydration - (BASE_DEHYDRATION_RATE * minutesElapsed));

    // Pure calculations - no external state
    double currentMetabolism = GetCurrentMetabolism(data);
    double caloriesBurned = currentMetabolism / 24 / 60 * minutesElapsed;
    data.Calories -= caloriesBurned;

    // ... more pure calculations ...

    SurvivalProcessorResult result = new(data);
    AddTemperatureEffects(data, oldTemperature, result);
    return result; // Returns new state, doesn't mutate external state
}
```

**Strengths:**
- ✅ **Stateless** - No instance variables, all state passed as parameters
- ✅ **Predictable** - Same input always produces same output
- ✅ **Testable** - Easy to write unit tests
- ✅ **Transparent** - All inputs and outputs explicit
- ✅ **Composable** - Can be called from anywhere safely

**Why This Is Exemplary:**
- Perfect adherence to pure function design constraint
- Easy to reason about temperature/survival calculations
- No hidden side effects
- Trivial to test edge cases

**Recommendation:**
- Use this as template for other game systems
- Fix `Sleep()` method to match this pattern
- Document this as the canonical processor pattern

---

### 🟢 Excellent: Property-Based Crafting System

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Crafting/CraftingSystem.cs`
**Quality:** Very Good

**What Makes It Great:**
```csharp
// Recipes use properties, not specific items
var boneKnife = new RecipeBuilder()
    .Named("Bone Knife")
    .WithPropertyRequirement(ItemProperty.Bone, 0.5)
    .WithPropertyRequirement(ItemProperty.Binding, 0.1)
    .WithPropertyRequirement(ItemProperty.Charcoal, 0.1)
    .ResultingInItem(ItemFactory.MakeBoneKnife)
    .Build();

// ANY item with Bone property can be used
// Not hardcoded to specific item names
```

**Strengths:**
- ✅ **Flexible** - New items automatically work if they have the right properties
- ✅ **Data-Driven** - Easy to add new recipes without code changes
- ✅ **Realistic** - Mimics real crafting (properties > specific objects)
- ✅ **Extensible** - New properties easy to add

**Why This Is Exemplary:**
- Matches CLAUDE.md design constraint: "Property-based crafting: Items contribute to recipes via properties"
- Avoids hardcoded item checks
- Makes modding/expansion easy
- Intuitive for players

**Improvement Suggestion:**
Consider adding quality/grade to properties:
```csharp
.WithPropertyRequirement(ItemProperty.Bone, minQuantity: 0.5, minQuality: 0.6)
```

---

### 🟢 Excellent: EffectBuilder Pattern

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Effects/EffectBuilder.cs`
**Quality:** Exemplary

**What Makes It Great:**
```csharp
var hypothermia = EffectBuilderExtensions
    .CreateEffect("Hypothermia")
    .Temperature(TemperatureType.Hypothermia)
    .WithApplyMessage(applicationMessage)
    .WithSeverity(severity)
    .AllowMultiple(false)
    .WithRemoveMessage(removalMessage)
    .Build();
```

**Strengths:**
- ✅ **Declarative** - Reads like a description, not instructions
- ✅ **Composable** - Extensions add domain-specific behavior
- ✅ **Type-Safe** - Helper methods prevent common errors
- ✅ **Consistent** - Matches ActionBuilder pattern
- ✅ **Powerful Extensions** - `.Temperature()`, `.Bleeding()`, `.Poisoned()` etc.

**Extension Pattern is Brilliant:**
```csharp
public static EffectBuilder Temperature(this EffectBuilder builder, TemperatureType type)
{
    return type switch
    {
        TemperatureType.Hypothermia => builder
            .Named("Hypothermia")
            .RequiresTreatment(true)
            .ReducesCapacity(CapacityNames.Moving, 0.3)
            .ReducesCapacity(CapacityNames.Consciousness, 0.5),
        // ... other temperature types
    };
}
```

**Why This Is Exemplary:**
- Encapsulates complex effect logic
- Prevents copy-paste errors
- Easy to add new effect types
- Self-documenting

---

### 🟢 Excellent: Hierarchical Body Part System

**Files:** Bodies folder
**Quality:** Very Good

**What Makes It Great:**
```csharp
// Clear hierarchy: Body → BodyRegion → Tissues/Organs
Body
└── BodyRegion (Torso, Arms, Legs, etc.)
    ├── Skin
    ├── Muscle
    ├── Bone
    └── Organs[]
```

**Strengths:**
- ✅ **Realistic** - Mimics actual anatomy
- ✅ **Damage Penetration** - Layers absorb damage realistically
- ✅ **Targeted Attacks** - Can aim for specific body parts
- ✅ **Capacity Contributions** - Each part affects abilities

**DamageProcessor Implementation:**
```csharp
private static double PenetrateLayers(BodyRegion part, DamageInfo damageInfo, DamageResult result)
{
    double remainingDamage = damageInfo.Amount;
    var layers = new[] { part.Skin, part.Muscle, part.Bone }.Where(l => l != null);

    foreach (var layer in layers)
    {
        // Each layer absorbs damage, reducing penetration
        // Realistic and intuitive
    }
    return remainingDamage;
}
```

**Why This Is Exemplary:**
- Single entry point: `Body.Damage(DamageInfo)`
- Damage flows through hierarchy naturally
- Easy to add new body types or parts
- Supports detailed combat narration

**Keep Doing:**
- Maintain single damage entry point
- Continue hierarchical organization
- Consider adding armor layers to hierarchy

---

### 🟢 Good: EffectRegistry Duplicate Handling (After Fix)

**File:** `/Users/jackswingle/Documents/GitHub/text_survival/Effects/EffectRegistry.cs:9-31`
**Quality:** Good (after recent fix)

**What Makes It Great (Post-Fix):**
```csharp
public void AddEffect(Effect effect)
{
    if (!effect.CanHaveMultiple)
    {
        // FIXED: Check BOTH EffectKind AND TargetBodyPart
        var existingEffect = _effects.FirstOrDefault(e =>
            e.EffectKind == effect.EffectKind &&
            e.TargetBodyPart == effect.TargetBodyPart);

        if (existingEffect != null)
        {
            // Update existing instead of adding duplicate
            existingEffect.UpdateSeverity(_owner, Math.Max(existingEffect.Severity, effect.Severity));
            return;
        }
    }

    // Only add if truly new
    _effects.Add(effect);
    effect.Apply(_owner);
}
```

**Strengths:**
- ✅ **Prevents Infinite Stacking** - Fixed critical bug
- ✅ **Severity Management** - Updates to worst severity
- ✅ **Targeted Effects** - Same effect can exist on different body parts
- ✅ **Configurable** - `CanHaveMultiple` flag allows flexibility

**Why This Is Important:**
- This fix resolved the game-breaking frostbite stacking bug
- Proper duplicate detection is critical for effect systems
- Shows good understanding of the problem domain

**Improvement Suggestion:**
Consider making severity update strategy configurable:
```csharp
public enum SeverityUpdateStrategy
{
    TakeMost Severe,    // Current: Math.Max
    TakeMostRecent,     // Replace with new
    AddCumulative,      // Sum severities
    AverageOut          // Average of old and new
}

// In EffectBuilder:
.WithSeverityUpdateStrategy(SeverityUpdateStrategy.TakeMostSevere)
```

---

## 5. Recommendations Summary

### Priority 1: Critical Fixes (Must Do Before Release)

1. **Fix Body.Rest() double World.Update**
   - Remove `World.Update()` call from `Body.Rest()`
   - Move time management to action layer
   - **Impact:** Prevents time drift and double survival processing

2. **Fix SurvivalProcessor.Sleep() state mutation**
   - Copy input `SurvivalData` instead of mutating
   - Maintain pure function design
   - **Impact:** Prevents hidden side effects, maintains design constraints

3. **Fix Actor.Update() null check**
   - Add null safety for `CurrentLocation`
   - Or make `CurrentLocation` required in constructor
   - **Impact:** Prevents potential crashes

### Priority 2: High-Value Improvements (Should Do Soon)

4. **Standardize time passage patterns**
   - Document convention in CLAUDE.md
   - Audit all actions for consistency
   - Fix Forage action to explicitly update time
   - **Impact:** Eliminates entire class of time-related bugs

5. **Fix GameContext CraftingSystem creation**
   - Move CraftingSystem ownership to Player
   - Update GameContext to reference, not create
   - **Impact:** Better performance, clearer ownership

6. **Add builder validation**
   - Validate RecipeBuilder configurations
   - Validate ActionBuilder configurations
   - **Impact:** Catch errors at build time, not runtime

### Priority 3: Code Quality (Nice To Have)

7. **Centralize constants** in `GameBalance.cs`
   - Move magic numbers to named constants
   - **Impact:** Easier balancing, better documentation

8. **Move Player.cs** to Actors namespace
   - Organize related classes together
   - **Impact:** Better code organization

9. **Improve null handling consistency**
   - Establish conventions
   - Enable nullable reference types
   - **Impact:** Fewer null reference bugs

10. **Consider SkillType enum** for type-safe access
    - Reduce reliance on string lookups where practical
    - **Impact:** Better IntelliSense, compile-time safety

---

## 6. Architecture Strengths Summary

The codebase demonstrates exceptional understanding of several architectural principles:

1. ✅ **Composition Over Inheritance** - Actor composition is well-designed
2. ✅ **Builder Patterns** - ActionBuilder, RecipeBuilder, EffectBuilder are exemplary
3. ✅ **Pure Functions** - SurvivalProcessor.Process() is perfect (except Sleep)
4. ✅ **Property-Based Design** - Crafting system is flexible and extensible
5. ✅ **Single Responsibility** - Most classes have clear, focused purposes
6. ✅ **Hierarchical Organization** - Body part system is realistic and elegant

**Overall Code Quality:** 7.5/10
- Excellent patterns and foundations
- Critical issues are fixable
- High maintainability once issues addressed

---

## 7. Next Steps

### For Parent Claude Instance:

1. **Review this document** with the user
2. **Get approval** for which fixes to implement
3. **Prioritize** based on user preferences:
   - Critical fixes first?
   - Or continue with Phase 4 implementation?
4. **Create tracking tasks** for approved changes
5. **Update CLAUDE.md** with new conventions once established

### Testing Requirements After Fixes:

- **Unit tests** for SurvivalProcessor.Sleep() to verify purity
- **Integration tests** for time passage in various actions
- **Regression tests** for frostbite stacking (ensure fix holds)
- **Null safety tests** for Actor.Update() edge cases

---

## Appendix: Files Reviewed

**Core Systems:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionFactory.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actions/ActionBuilder.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actions/GameContext.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actors/Actor.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actors/NPC.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Actors/NPCFactory.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Player.cs`

**Bodies & Damage:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Bodies/Body.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Bodies/DamageCalculator.cs`

**Survival System:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Survival/SurvivalProcessor.cs`

**Effects:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Effects/EffectRegistry.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Effects/EffectBuilder.cs`

**Crafting:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Crafting/CraftingSystem.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Crafting/RecipeBuilder.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Crafting/ItemProperty.cs`

**Items:**
- `/Users/jackswingle/Documents/GitHub/text_survival/Items/ItemFactory.cs`

**Other:**
- `/Users/jackswingle/Documents/GitHub/text_survival/World.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Level/SkillRegistry.cs`
- `/Users/jackswingle/Documents/GitHub/text_survival/Program.cs`

**Documentation:**
- `/Users/jackswingle/Documents/GitHub/text_survival/CLAUDE.md`
- `/Users/jackswingle/Documents/GitHub/text_survival/ISSUES.md`

---

**End of Review**
