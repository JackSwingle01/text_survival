# Survival Consequences System - Implementation Plan

**Created**: 2025-11-03
**Last Updated**: 2025-11-03 (Phases 1-3 Complete)
**Status**: 60% Complete (3/5 Phases Done)
**Estimated Effort**: 4-6 hours total (3 hours completed, 1-2 hours remaining)

---

## Overview

Implement realistic survival stat consequences using the existing Body system architecture. This plan leverages fat/muscle tracking, organ damage system, and capacity modifiers already in the codebase.

**Key Principle**: Use direct code (not Effects) as per architectural design rule in `/documentation/effects-vs-direct-architecture.md`.

---

## Implementation Phases

### Phase 1: Core Starvation System ✅ COMPLETE

**Status**: ✅ IMPLEMENTED
**Time Spent**: ~1 hour
**Build Status**: SUCCESS (0 errors)

#### 1.1 Add Body Composition Constants ✅

**File**: `Bodies/Body.cs`
**Location**: Top of class (after existing fields)

```csharp
// Body composition limits
private const double MIN_FAT = 0.03;      // 3% essential fat (survival minimum)
private const double MIN_MUSCLE = 0.15;   // 15% minimum muscle (critical weakness)
private const double MAX_FAT = 0.50;      // 50% maximum fat (obesity)

// Calorie conversion rates (realistic)
private const double CALORIES_PER_LB_FAT = 3500;     // Well-established
private const double CALORIES_PER_LB_MUSCLE = 600;   // Protein catabolism
private const double LB_TO_KG = 0.454;

// Starvation progression thresholds
private const double HUNGRY_THRESHOLD = 0.50;        // 50% calories - early warning
private const double VERY_HUNGRY_THRESHOLD = 0.20;   // 20% calories - serious
private const double STARVING_THRESHOLD = 0.01;      // 1% calories - critical
```

**Implementation Complete**:
- Added all constants to Body.cs
- Values chosen for realism and gameplay balance
- 3% fat = essential fat for organ protection (medical minimum)
- 15% muscle = can barely function (realistic weakness)
- 3500 cal/lb fat = well-established metabolic constant
- 600 cal/lb muscle = gluconeogenesis rate from protein

#### 1.2 Add Time Tracking Fields ✅

**File**: `Bodies/Body.cs`
**Location**: With other private fields

```csharp
// Track time at critical levels for progressive damage
private int _minutesStarving = 0;      // Time at 0% calories
private int _minutesDehydrated = 0;    // Time at 0% hydration
private int _minutesExhausted = 0;     // Time at 0% energy
```

**Implementation Complete**:
- Added three tracking fields to Body.cs
- Tracks time at critical levels to apply progressive organ damage over realistic timelines

#### 1.3 Implement Fat Consumption Method ✅

**File**: `Bodies/Body.cs`
**Location**: New private method

```csharp
/// <summary>
/// Consume body fat to meet calorie deficit. Called when Calories = 0.
/// </summary>
/// <param name="calorieDeficit">How many calories below 0 (negative calories)</param>
/// <param name="messages">List to add messages to</param>
/// <returns>Remaining calorie deficit after consuming available fat</returns>
private double ConsumeFat(double calorieDeficit, List<string> messages)
{
    if (BodyFat <= MIN_FAT)
    {
        return calorieDeficit; // No fat left to burn
    }

    // Calculate how much fat we can burn (don't go below minimum)
    double fatAvailable = BodyFat - MIN_FAT;
    double caloriesFromFat = fatAvailable * (CALORIES_PER_LB_FAT / LB_TO_KG); // Convert kg to lb, then to calories

    if (caloriesFromFat >= calorieDeficit)
    {
        // Enough fat to cover deficit
        double fatToBurn = calorieDeficit / CALORIES_PER_LB_FAT * LB_TO_KG;
        BodyFat -= fatToBurn;

        // Message based on severity
        double fatPercent = BodyFatPercentage;
        if (fatPercent < 0.08)
            messages.Add("Your body is consuming the last of your fat reserves... You're becoming dangerously thin.");
        else if (fatPercent < 0.12)
            messages.Add("Your body is burning fat reserves. You're noticeably thinner.");

        return 0; // Deficit covered
    }
    else
    {
        // Burn all available fat, still have deficit
        BodyFat = MIN_FAT;
        messages.Add("Your body has exhausted all available fat reserves!");
        return calorieDeficit - caloriesFromFat;
    }
}
```

**Implementation Complete**:
- Implemented full ConsumeFat() method as designed
- Messages scale with severity (thresholds at 8% and 12% fat)
- Returns remaining deficit for muscle consumption stage
- Timeline matches medical reality: ~35 days to deplete fat reserves

#### 1.4 Implement Muscle Catabolism Method ✅

**File**: `Bodies/Body.cs`
**Location**: After ConsumeFat

```csharp
/// <summary>
/// Catabolize muscle tissue when fat reserves depleted. Reduces strength/speed.
/// </summary>
/// <param name="calorieDeficit">Remaining calorie deficit after fat consumption</param>
/// <param name="messages">List to add messages to</param>
/// <returns>Remaining deficit after consuming available muscle</returns>
private double ConsumeMuscle(double calorieDeficit, List<string> messages)
{
    if (Muscle <= MIN_MUSCLE)
    {
        return calorieDeficit; // At critical weakness
    }

    // Muscle catabolism is less efficient than fat burning
    double muscleAvailable = Muscle - MIN_MUSCLE;
    double caloriesFromMuscle = muscleAvailable * (CALORIES_PER_LB_MUSCLE / LB_TO_KG);

    if (caloriesFromMuscle >= calorieDeficit)
    {
        double muscleToBurn = calorieDeficit / CALORIES_PER_LB_MUSCLE * LB_TO_KG;
        Muscle -= muscleToBurn;

        // Critical warnings - muscle loss is serious
        double musclePercent = MusclePercentage;
        if (musclePercent < 0.18)
            messages.Add("Your body is cannibalizing muscle tissue! You feel extremely weak.");
        else if (musclePercent < 0.25)
            messages.Add("Your muscles are wasting away. You're losing strength rapidly.");

        return 0;
    }
    else
    {
        Muscle = MIN_MUSCLE;
        messages.Add("Your body has consumed almost all muscle tissue. Organ damage imminent!");
        return calorieDeficit - caloriesFromMuscle;
    }
}
```

**Implementation Complete**:
- Implemented full ConsumeMuscle() method as designed
- Critical warnings when muscle drops below 25% and 18%
- Automatic stat reduction via AbilityCalculator (muscle loss → strength/speed drop)
- Timeline: ~7 days of muscle catabolism after fat depletion

#### 1.5 Implement Starvation Organ Damage ✅

**File**: `Bodies/Body.cs`
**Location**: After ConsumeMuscle

```csharp
/// <summary>
/// Apply organ damage from extreme starvation. Occurs when fat and muscle depleted.
/// </summary>
/// <param name="minutesElapsed">Minutes at critical starvation</param>
private void ApplyStarvationOrganDamage(int minutesElapsed)
{
    // Progressive damage over ~5-7 days (7200-10080 minutes)
    // Target: 0.1 HP per hour = death in ~10 hours of extreme starvation
    double damagePerMinute = 0.1 / 60.0;
    double totalDamage = damagePerMinute * minutesElapsed;

    // Target random vital organs
    var vitalOrgans = new[] { "Heart", "Liver", "Brain", "Lungs" };
    string targetOrgan = vitalOrgans[Random.Shared.Next(vitalOrgans.Length)];

    Damage(new DamageInfo
    {
        Amount = totalDamage,
        Type = DamageType.Internal, // Bypasses armor
        TargetPartName = targetOrgan,
        Source = "Starvation"
    });
}
```

**Implementation Complete**:
- Implemented ApplyStarvationOrganDamage() as designed
- Added DamageType.Internal to DamageInfo.cs (bypasses armor)
- Targets vital organs (Heart, Liver, Brain, Lungs) randomly
- Progressive damage: 0.1 HP/hr = death in ~10 hours of extreme starvation

**Timeline**:
- Days 1-35: Fat consumption (capacity penalties, weakness)
- Days 35-42: Muscle consumption (severe weakness, strength loss)
- Days 42+: Organ damage begins (10 hours to death if not fed)
- Total: ~6-7 weeks realistic starvation timeline

#### 1.6 Main Processing Method - Starvation ✅

**File**: `Bodies/Body.cs`
**Location**: New private method

```csharp
/// <summary>
/// Process all survival stat consequences. Called from UpdateBodyBasedOnResult.
/// </summary>
/// <param name="result">Result from SurvivalProcessor containing updated stats</param>
/// <param name="minutesElapsed">Minutes that passed this update</param>
private void ProcessSurvivalConsequences(SurvivalProcessorResult result, int minutesElapsed)
{
    var data = result.Data;

    // ===== STARVATION PROGRESSION =====
    if (data.Calories <= 0)
    {
        _minutesStarving += minutesElapsed;

        // Calculate how many calories we needed but didn't have
        // This is stored in the negative calories that got clamped to 0
        // We need to recalculate based on metabolism
        double currentMetabolism = 370 + (21.6 * Muscle) + (6.17 * BodyFat);
        currentMetabolism *= 0.7 + (0.3 * Health); // Injured bodies need more
        currentMetabolism *= data.activityLevel;
        double calorieDeficit = (currentMetabolism / 24.0 / 60.0) * minutesElapsed;

        // Stage 1: Consume fat reserves
        double remainingDeficit = ConsumeFat(calorieDeficit, result.Messages);

        // Stage 2: Catabolize muscle (only if fat depleted)
        if (remainingDeficit > 0)
        {
            remainingDeficit = ConsumeMuscle(remainingDeficit, result.Messages);
        }

        // Stage 3: Organ damage (only if muscle at minimum)
        if (remainingDeficit > 0 && _minutesStarving > 60480) // 6 weeks
        {
            ApplyStarvationOrganDamage(minutesElapsed);

            if (_minutesStarving % 60 == 0) // Every hour
            {
                result.Messages.Add($"You are starving to death... ({(int)(_minutesStarving / 1440)} days without food)");
            }
        }
    }
    else
    {
        _minutesStarving = 0; // Reset timer when fed
    }

    // TODO: Dehydration (Phase 2) - COMPLETED
    // TODO: Exhaustion (Phase 2) - COMPLETED
    // TODO: Regeneration (Phase 4) - NOT STARTED (at line 472)
}
```

**Implementation Complete**:
- Implemented full ProcessSurvivalConsequences() coordinator method
- Integrates fat → muscle → organ damage progression
- Resets timers when calories restored
- Hourly messages during extreme starvation

#### 1.7 Hook into Update Flow ✅

**File**: `Bodies/Body.cs`
**Method**: `UpdateBodyBasedOnResult`
**Location**: After line 157 (after updating stats from result)

```csharp
private void UpdateBodyBasedOnResult(SurvivalProcessorResult result)
{
    // Existing code (lines 144-157)
    Temperature = result.Data.Temperature;
    Calories = result.Data.Calories;
    Hydration = result.Data.Hydration;
    Energy = result.Data.Energy;

    if (result.Effects.Count > 0)
    {
        foreach (var effect in result.Effects)
        {
            _effectRegistry.AddEffect(effect);
        }
    }

    // NEW: Process survival consequences
    int minutesElapsed = 1; // Body.Update always called with 1 minute intervals
    ProcessSurvivalConsequences(result, minutesElapsed);

    // Existing code continues...
    foreach (var message in result.Messages)
    {
        if (_isPlayer)
            Output.WriteLine(message);
    }
}
```

**Implementation Complete**:
- Added integration hook at line 176 in UpdateBodyBasedOnResult()
- Calls ProcessSurvivalConsequences() after stat updates
- Properly integrated into Body.Update() flow

---

### Phase 2: Dehydration & Exhaustion ✅ COMPLETE

**Status**: ✅ IMPLEMENTED
**Time Spent**: ~30 minutes
**Build Status**: SUCCESS (0 errors)

#### 2.1 Dehydration Organ Damage ✅

**File**: `Bodies/Body.cs`
**Location**: In ProcessSurvivalConsequences, after starvation section

```csharp
// ===== DEHYDRATION PROGRESSION =====
if (data.Hydration <= 0)
{
    _minutesDehydrated += minutesElapsed;

    // Dehydration kills faster than starvation
    // Target: ~24 hours (1440 minutes) to death
    if (_minutesDehydrated > 60) // After 1 hour, start damage
    {
        double damagePerMinute = 0.2 / 60.0; // 0.2 HP per hour = death in 5 hours
        double totalDamage = damagePerMinute * minutesElapsed;

        // Dehydration affects kidneys, brain, heart
        var affectedOrgans = new[] { "Brain", "Heart", "Liver" };
        string target = affectedOrgans[Random.Shared.Next(affectedOrgans.Length)];

        Damage(new DamageInfo
        {
            Amount = totalDamage,
            Type = DamageType.Internal,
            TargetPartName = target,
            Source = "Dehydration"
        });

        if (_minutesDehydrated % 60 == 0)
        {
            result.Messages.Add($"Your organs are failing from dehydration... ({_minutesDehydrated / 60} hours)");
        }
    }
}
else
{
    _minutesDehydrated = 0;
}
```

**Implementation Complete**:
- Implemented full dehydration organ damage logic
- 1-hour grace period before damage begins (0.2 HP/hr)
- Targets Brain, Heart, Liver (realistic organ failure)
- Hourly warning messages during critical dehydration

**Timeline**:
- First hour: No damage (warning messages only)
- After 1 hour: Progressive organ damage begins
- Death in ~6 hours at 0% hydration (realistic and urgent)

#### 2.2 Exhaustion Tracking ✅

**File**: `Bodies/Body.cs`
**Location**: In ProcessSurvivalConsequences, after dehydration

```csharp
// ===== EXHAUSTION PROGRESSION =====
if (data.Energy <= 0)
{
    _minutesExhausted += minutesElapsed;

    // Exhaustion doesn't directly kill, but creates vulnerability
    // Track for future use (hallucinations, forced sleep, etc.)
    if (_minutesExhausted > 480 && _minutesExhausted % 120 == 0) // Every 2 hours after 8 hours
    {
        result.Messages.Add("You're so exhausted you can barely function...");
    }
}
else
{
    _minutesExhausted = 0;
}
```

**Implementation Complete**:
- Implemented exhaustion time tracking
- No direct damage (as designed)
- Periodic warning messages after 8 hours exhausted
- Timer resets when energy restored

**Note**: Exhaustion damage comes from capacity penalties (Phase 3), not direct HP loss.

---

### Phase 3: Capacity Penalties ✅ COMPLETE

**Status**: ✅ IMPLEMENTED
**Time Spent**: ~1 hour
**Build Status**: SUCCESS (0 errors)

#### 3.1 Add Survival Stat Modifiers to CapacityCalculator ✅

**File**: `Bodies/CapacityCalculator.cs`
**Method**: `GetCapacities`
**Location**: Lines 19-20 (after applying effect modifiers)

```csharp
public static CapacityContainer GetCapacities(Body body)
{
    // Existing code (lines 7-15)...
    total = total.ApplyModifier(effectModifiers);

    // NEW: Apply survival stat penalties
    var survivalModifiers = GetSurvivalStatModifiers(body);
    total = total.ApplyModifier(survivalModifiers);

    // Existing code continues (cascading effects, etc.)
    total = ApplyCascadingEffects(total);
    return total;
}
```

**Implementation Complete**:
- Added survival modifier integration at lines 19-20
- Follows same pattern as effect modifiers
- Applied before cascading effects for proper stacking

#### 3.2 Implement Survival Stat Modifier Calculation ✅

**File**: `Bodies/CapacityCalculator.cs`
**Location**: New private static method

```csharp
/// <summary>
/// Calculate capacity modifiers from survival stats (hunger, thirst, exhaustion).
/// These penalties make the player vulnerable before death.
/// </summary>
private static CapacityModifierContainer GetSurvivalStatModifiers(Body body)
{
    var modifiers = new CapacityModifierContainer();

    // Get current survival stats (0-100%)
    double caloriePercent = body.Calories / SurvivalProcessor.MAX_CALORIES;
    double hydrationPercent = body.Hydration / SurvivalProcessor.MAX_HYDRATION;
    double energyPercent = body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES;

    // ===== HUNGER PENALTIES =====
    // Progressive weakness as calories drop
    if (caloriePercent < 0.50)  // Below 50%
    {
        double hungerSeverity = (0.50 - caloriePercent) / 0.50; // 0-1 scale

        if (caloriePercent < 0.01) // Starving (0-1%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.40);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.40);
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.20);
        }
        else if (caloriePercent < 0.20) // Very hungry (1-20%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.25);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.25);
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
        }
        else // Hungry (20-50%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.10);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.10);
        }
    }

    // ===== DEHYDRATION PENALTIES =====
    // Affects consciousness and movement
    if (hydrationPercent < 0.50)
    {
        if (hydrationPercent < 0.01) // Severely dehydrated (0-1%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.60);
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.50);
            modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.30);
        }
        else if (hydrationPercent < 0.20) // Very thirsty (1-20%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.30);
            modifiers.SetCapacityModifier(CapacityNames.Moving, -0.20);
        }
        else // Thirsty (20-50%)
        {
            modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
        }
    }

    // ===== EXHAUSTION PENALTIES =====
    // Severe penalties allowing indefinite wakefulness but with major debuffs
    if (energyPercent < 0.01) // Exhausted (near 0%)
    {
        modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.60);
        modifiers.SetCapacityModifier(CapacityNames.Moving, -0.60);
        modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.40);
    }
    else if (energyPercent < 0.20) // Very tired (1-20%)
    {
        modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.30);
        modifiers.SetCapacityModifier(CapacityNames.Moving, -0.30);
        modifiers.SetCapacityModifier(CapacityNames.Manipulation, -0.20);
    }
    else if (energyPercent < 0.50) // Tired (20-50%)
    {
        modifiers.SetCapacityModifier(CapacityNames.Consciousness, -0.10);
        modifiers.SetCapacityModifier(CapacityNames.Moving, -0.10);
    }

    return modifiers;
}
```

**Implementation Complete**:
- Implemented full GetSurvivalStatModifiers() method (lines 120-195)
- Three-tier penalty system (50%, 20%, 1% thresholds) for each stat
- Carefully balanced values chosen for gameplay feel
- Hunger penalties: -10% to -40% Moving/Manipulation
- Dehydration penalties: -10% to -60% Consciousness
- Exhaustion penalties: -10% to -60% Consciousness/Moving

**Impact**:
- At 0% food/water/energy: -40 to -60% capacity penalties
- Makes player extremely vulnerable to cold, predators, accidents
- Can still function but barely (realistic)
- Combined penalties stack (starving + dehydrated + exhausted = near-helpless)

---

### Phase 4: Organ Regeneration ⏳ NOT STARTED

**Status**: ⏳ READY FOR IMPLEMENTATION
**Estimated Time**: 30-60 minutes
**Code Location**: Body.cs line 472 (replace TODO comment)

#### 4.1 Implement Natural Regeneration

**File**: `Bodies/Body.cs`
**Location**: In ProcessSurvivalConsequences, at the end

```csharp
// ===== NATURAL ORGAN REGENERATION =====
// Only heal when ALL systems above critical levels
bool wellFed = data.Calories > SurvivalProcessor.MAX_CALORIES * 0.10;      // >10% calories
bool hydrated = data.Hydration > SurvivalProcessor.MAX_HYDRATION * 0.10;   // >10% hydration
bool rested = data.Energy < SurvivalProcessor.MAX_ENERGY_MINUTES * 0.50;   // <50% exhaustion

if (wellFed && hydrated && rested)
{
    // Calculate healing based on nutrition quality
    double nutritionQuality = Math.Min(1.0, data.Calories / SurvivalProcessor.MAX_CALORIES);
    double baseHealingPerHour = 0.1; // 10% organ recovery per hour (10 hours to full heal)
    double healingThisUpdate = (baseHealingPerHour / 60.0) * minutesElapsed * nutritionQuality;

    // Use existing healing system
    HealingInfo healing = new HealingInfo
    {
        Amount = healingThisUpdate,
        Type = "natural regeneration",
        Quality = nutritionQuality
    };

    Heal(healing);

    // Occasional feedback (don't spam)
    if (Health < 1.0 && minutesElapsed % 60 == 0 && Random.Shared.NextDouble() < 0.2)
    {
        result.Messages.Add("Your body is slowly healing...");
    }
}
```

**Implementation Notes**:
- **EXACT CODE PROVIDED** in design document
- Simply replace `// TODO: Regeneration (Phase 4)` at line 472
- Copy code from implementation plan section 4.1
- No additional logic needed - leverages existing Heal() method

**Healing Rates**:
- Well-fed (100% calories): 0.1 HP/hour → 10 hours to full recovery
- Moderately fed (50% calories): 0.05 HP/hour → 20 hours to full recovery
- Requires hydration + rest + food (realistic recovery conditions)
- Uses existing Heal() method (prioritizes most damaged parts)

**Next Steps**:
1. Open Bodies/Body.cs
2. Navigate to line 472
3. Replace TODO comment with regeneration code from section 4.1
4. Build and test

---

### Phase 5: Warning Messages ⏳ NOT STARTED

**Status**: ⏳ READY FOR IMPLEMENTATION
**Estimated Time**: 30 minutes
**Code Location**: Survival/SurvivalProcessor.cs after line 86

#### 5.1 Add Threshold Warnings to SurvivalProcessor

**File**: `Survival/SurvivalProcessor.cs`
**Method**: `Process`
**Location**: After line 86, before return statement

```csharp
// Add warning messages for critical thresholds
if (data.IsPlayer)
{
    double caloriePercent = data.Calories / MAX_CALORIES;
    double hydrationPercent = data.Hydration / MAX_HYDRATION;
    double energyPercent = data.Energy / MAX_ENERGY_MINUTES;

    // Hunger warnings
    if (caloriePercent <= 0.01 && Utils.DetermineSuccess(0.1))
        result.Messages.Add("You are starving to death!");
    else if (caloriePercent <= 0.20 && Utils.DetermineSuccess(0.05))
        result.Messages.Add("You're desperately hungry.");
    else if (caloriePercent <= 0.50 && Utils.DetermineSuccess(0.02))
        result.Messages.Add("You're getting very hungry.");

    // Thirst warnings
    if (hydrationPercent <= 0.01 && Utils.DetermineSuccess(0.1))
        result.Messages.Add("You are dying of thirst!");
    else if (hydrationPercent <= 0.20 && Utils.DetermineSuccess(0.05))
        result.Messages.Add("You're desperately thirsty.");
    else if (hydrationPercent <= 0.50 && Utils.DetermineSuccess(0.02))
        result.Messages.Add("You're getting quite thirsty.");

    // Exhaustion warnings
    if (energyPercent <= 0.01 && Utils.DetermineSuccess(0.1))
        result.Messages.Add("You're so exhausted you can barely stay awake.");
    else if (energyPercent <= 0.20 && Utils.DetermineSuccess(0.05))
        result.Messages.Add("You're extremely tired.");
    else if (energyPercent <= 0.50 && Utils.DetermineSuccess(0.02))
        result.Messages.Add("You're getting tired.");
}
```

**Implementation Notes**:
- **EXACT CODE PROVIDED** in design document
- Add after line 86 in Process() method, before `return result;`
- Copy code from implementation plan section 5.1
- Simple threshold checks with probabilistic messages

**Message Strategy**:
- Probabilistic (not every update) to avoid spam
- Higher chance at critical levels (10% at death threshold)
- Escalating urgency (hungry → desperate → dying)
- Player-only (no NPC message spam)

**Next Steps**:
1. Open Survival/SurvivalProcessor.cs
2. Navigate to line 86 (before return statement)
3. Add warning message code from section 5.1
4. Build and test

---

## Testing Strategy

### Unit Tests (Optional but Recommended)

**File**: `text_survival.Tests/Bodies/StarvationSystemTests.cs`

```csharp
[Fact]
public void ConsumeFat_WithSufficientFat_CoversDeficit()
{
    // Arrange: 75kg person, 15% fat
    var body = CreateTestBody(fatPercent: 0.15);
    double initialFat = body.BodyFat; // 11.25kg

    // Act: 2000 calorie deficit (1 day starving)
    double deficit = body.ConsumeFat(2000, new List<string>());

    // Assert
    Assert.Equal(0, deficit); // Deficit fully covered
    Assert.True(body.BodyFat < initialFat); // Fat was consumed
    Assert.InRange(body.BodyFat, 10.9, 11.1); // ~0.26kg lost
}

[Fact]
public void StarvationProgression_ConsumeFatThenMuscleThenOrgans()
{
    // Test full starvation progression over weeks
}

[Fact]
public void DehydrationDamage_KillsIn24Hours()
{
    // Test dehydration timeline
}
```

### Integration Tests (TEST_MODE=1)

**Script**: `play_game.sh`

```bash
# Test starvation progression
./play_game.sh start
./play_game.sh send "7"  # Sleep
./play_game.sh send "168" # 7 days (10080 minutes)
./play_game.sh tail  # Should see fat consumption messages

# Test dehydration
./play_game.sh send "7"
./play_game.sh send "24"  # 24 hours
# Should be dead or nearly dead

# Test capacity penalties
# Check stats at 0% food - should see reduced movement/strength
```

### Manual Playtesting Checklist

- [ ] Player survives normally when fed/hydrated/rested
- [ ] Hunger warnings appear at 50%, 20%, 1% calories
- [ ] Fat consumption begins at 0% calories
- [ ] Muscle consumption begins when fat depleted
- [ ] Organ damage begins when muscle depleted
- [ ] Capacity penalties apply at low stats
- [ ] Player becomes noticeably weaker (reduced strength/speed)
- [ ] Player more vulnerable to cold when fat depleted
- [ ] Dehydration kills in ~24 hours
- [ ] Organs regenerate when well-fed/hydrated/rested
- [ ] Death occurs when organs fail (not arbitrary)
- [ ] Muscle/fat recovery works (eat to rebuild)

---

## Edge Cases & Error Handling

### 1. Negative Body Composition

**Issue**: Fat/Muscle could go negative
**Solution**: Clamp in consumption methods

```csharp
BodyFat = Math.Max(MIN_FAT, BodyFat - fatToBurn);
Muscle = Math.Max(MIN_MUSCLE, Muscle - muscleToBurn);
```

### 2. Rapid Stat Changes (Sleep Exploit)

**Issue**: Sleeping 30,000 hours processes 30,000 times
**Solution**: Already handled - each minute processes independently

### 3. Death During Consumption

**Issue**: Organ damage might kill mid-update
**Solution**: Check `IsAlive` before continuing processing

```csharp
if (!IsAlive) return; // Stop processing if dead
```

### 4. Division by Zero

**Issue**: Weight = 0 if fat/muscle both at 0
**Solution**: MIN_FAT + MIN_MUSCLE + baseWeight always > 0

### 5. Message Spam

**Issue**: Too many "starving" messages
**Solution**: Use probabilistic messages + time gating (% 60 == 0)

---

## Performance Considerations

### Computational Cost

**Per Update (1 minute)**:
1. Starvation check: O(1) - simple math
2. Fat/muscle consumption: O(1) - arithmetic
3. Organ damage: O(1) - single Body.Damage call
4. Capacity calculation: O(n) where n = number of body parts (~10-20)
5. Healing: O(n) - iterate damaged parts

**Total**: ~O(n) where n ≈ 20 body parts
**Expected**: <1ms per actor per minute
**Impact**: Negligible even with 100+ NPCs

### Memory

**New fields**: 3 integers (12 bytes per actor)
**Impact**: Trivial

### Optimization Opportunities

1. **Cache capacities** if calculated multiple times per update
2. **Batch messages** to reduce list allocations
3. **Skip NPC processing** if far from player (already done)

---

## Migration & Backwards Compatibility

### Save Game Compatibility

**New fields** need defaults:
```csharp
private int _minutesStarving = 0;
private int _minutesDehydrated = 0;
private int _minutesExhausted = 0;
```

These will default to 0 on load, which is correct (not currently starving).

**No migration needed** - fields are private and have sensible defaults.

### Existing Saves

Players with existing saves will:
- Start with 0 minutes tracked (correct)
- Body composition from existing BodyFat/Muscle (already saved)
- No issues

---

## Success Criteria

### Minimum Viable (Phase 1-2) ✅ COMPLETE

- [x] Plan complete
- [x] Phase 1 implemented (starvation) ✅
- [x] Phase 2 implemented (dehydration/exhaustion) ✅
- [x] Player at 0% calories consumes fat ✅ (untested)
- [x] Player at 0% calories with no fat consumes muscle ✅ (untested)
- [x] Player dies from starvation after realistic timeline ✅ (untested)
- [x] Player dies from dehydration in ~6 hours ✅ (untested)
- [x] Build succeeds with no errors ✅
- [ ] Basic TEST_MODE=1 validation ⏳ (pending)

### Full System (Phase 3-5) ⏳ IN PROGRESS (3/5 Complete)

- [x] Phase 3 implemented (capacity penalties) ✅
- [ ] Phase 4 implemented (regeneration) ⏳ (ready for implementation)
- [ ] Phase 5 implemented (warnings) ⏳ (ready for implementation)
- [x] Capacity penalties apply at low stats ✅ (untested)
- [x] Player noticeably weaker when starving ✅ (untested - via capacity penalties)
- [ ] Organs regenerate when fed/hydrated/rested ⏳ (Phase 4)
- [ ] Warning messages appear at thresholds ⏳ (Phase 5)
- [ ] Full gameplay testing (30+ min session) ⏳ (pending)
- [ ] Balance verified (survival difficulty appropriate) ⏳ (pending)

---

## Rollback Plan

If issues arise:

1. **Quick Disable**: Comment out ProcessSurvivalConsequences() call
2. **Partial Rollback**: Comment out specific phases (keep fat consumption, remove organ damage)
3. **Full Rollback**: Git revert to commit before changes

**Safe Points**:
- After Phase 1: Starvation works standalone
- After Phase 2: Can disable regeneration if too fast
- After Phase 3: Can disable capacity penalties if too harsh

---

## Timeline Estimates

- **Phase 1** (Core Starvation): 2-3 hours
- **Phase 2** (Dehydration/Exhaustion): 1-2 hours
- **Phase 3** (Capacity Penalties): 1-2 hours
- **Phase 4** (Regeneration): 1 hour
- **Phase 5** (Warning Messages): 30 minutes
- **Testing & Balance**: 1 hour

**Total**: 6-10 hours (single work day)

**Can ship after Phase 1-2** for minimum viable fix (stop 0% exploit).

---

## Next Steps

1. **Review this plan** for completeness
2. **Start Phase 1** implementation
3. **Test incrementally** after each phase
4. **Gather feedback** from gameplay
5. **Iterate on balance** as needed

---

## References

- Design Document: `dev/active/survival-consequences-system/survival-consequences-design.md`
- Architecture Research: `dev/active/survival-consequences-system/architecture-research.md`
- Effects vs Direct: `documentation/effects-vs-direct-architecture.md`
- Body System Docs: `documentation/body-and-damage.md`
- Survival System Docs: `documentation/survival-processing.md`
