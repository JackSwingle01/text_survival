# Survival Processing - Pure Function Guide

Pure function survival processing, temperature regulation, calorie burn, hydration, effect generation, and body composition impacts.

## Table of Contents

- [Pure Function Design](#pure-function-design)
- [SurvivalProcessor.Process()](#survivalprocessorprocess)
- [Temperature Regulation](#temperature-regulation)
- [Metabolism and Calories](#metabolism-and-calories)
- [Hydration System](#hydration-system)
- [Effect Generation](#effect-generation)
- [Body Composition Impact](#body-composition-impact)

---

## Pure Function Design

**Critical Principle**: `SurvivalProcessor.Process()` is a PURE FUNCTION - no side effects, no state mutation.

```csharp
// ✅ CORRECT: Pure function
var result = SurvivalProcessor.Process(
    survivalData,      // Input (not mutated)
    minutesElapsed,    // Input
    activeEffects      // Input (not mutated)
);
// Returns new result, original inputs unchanged

// ❌ WRONG: Stateful processing
processor.UpdateSurvival(player);  // Don't store state in processor
```

### Why Pure Functions?

1. **Testable**: Easy to unit test with predictable inputs/outputs
2. **Composable**: Can chain and combine freely
3. **Debuggable**: No hidden state changes
4. **Thread-safe**: Can run in parallel safely
5. **Deterministic**: Same inputs always produce same outputs

---

## SurvivalProcessor.Process()

### Method Signature

```csharp
public static SurvivalProcessorResult Process(
    SurvivalData data,
    int minutesElapsed,
    List<Effect> effects
)
```

### Input: SurvivalData

```csharp
public class SurvivalData
{
    public double CaloriesRemaining { get; set; }
    public double HydrationLevel { get; set; }  // 0.0-1.0
    public double BodyTemperature { get; set; }  // Celsius
    public double Exhaustion { get; set; }       // 0-100
    public double FatKg { get; set; }
    public double MuscleKg { get; set; }
}
```

### Output: SurvivalProcessorResult

```csharp
public class SurvivalProcessorResult
{
    public SurvivalData UpdatedData { get; set; }
    public List<Effect> NewEffects { get; set; }
    public List<string> Messages { get; set; }
    public bool PlayerDied { get; set; }
}
```

### Processing Flow

```
1. Calculate calorie burn (base + activity + temperature)
2. Calculate hydration loss (base + temperature + activity)
3. Calculate body temperature change (environment + clothing + shelter)
4. Update exhaustion (time-based + activity)
5. Check thresholds and generate effects
6. Check for death conditions
7. Return result with updated data + effects + messages
```

---

## Temperature Regulation

### Exponential Heat Transfer Model

```csharp
// Temperature changes follow exponential curve
double temperatureDifference = environmentTemp - bodyTemp;
double heatTransferRate = 0.1; // Base rate

// Adjust for insulation
heatTransferRate *= (1 - clothingInsulation);
heatTransferRate *= (1 - shelterInsulation);

// Exponential change (approaches environment temp)
double tempChange = temperatureDifference * heatTransferRate * (minutesElapsed / 60.0);
bodyTemp += tempChange;
```

### Insulation Sources

```csharp
// Clothing insulation
double clothingInsulation = 0;
foreach (var item in equippedItems)
{
    if (item.Has(ItemProperty.Insulation))
    {
        clothingInsulation += item.GetInsulationValue();
    }
}

// Shelter insulation
var shelter = location.GetFeature<ShelterFeature>();
double shelterInsulation = shelter?.InsulationValue ?? 0;

// Body fat insulation (natural)
double fatInsulation = Math.Min(0.2, data.FatKg / 20.0 * 0.2);

// Total insulation (caps at 0.9)
double totalInsulation = Math.Min(0.9,
    clothingInsulation + shelterInsulation + fatInsulation);
```

### Temperature Effects

```csharp
// Hypothermia thresholds
if (bodyTemp < 35.0)  // Mild hypothermia
{
    effects.Add(CreateHypothermia(severity: 0.3));
    messages.Add("You're getting dangerously cold...");
}
if (bodyTemp < 32.0)  // Severe hypothermia
{
    effects.Add(CreateHypothermia(severity: 0.7));
    messages.Add("You're freezing!");
}
if (bodyTemp < 28.0)  // Critical
{
    playerDied = true;
    messages.Add("You freeze to death.");
}

// Hyperthermia thresholds
if (bodyTemp > 39.0)  // Heat exhaustion
{
    effects.Add(CreateHeatExhaustion(severity: 0.4));
    messages.Add("You're overheating...");
}
if (bodyTemp > 41.0)  // Heat stroke
{
    playerDied = true;
    messages.Add("You die from heat stroke.");
}
```

---

## Metabolism and Calories

### Calorie Burn Calculation

```csharp
// Base metabolic rate (BMR)
double bmr = 1.2;  // Cal/min at rest

// Activity multiplier
double activityMultiplier = 1.0;  // At rest
if (isMoving) activityMultiplier = 2.0;
if (isFighting) activityMultiplier = 3.5;
if (isCrafting) activityMultiplier = 1.3;

// Temperature regulation cost
double tempCost = 0;
if (bodyTemp < 36.0)
{
    // Shivering increases calorie burn
    tempCost = (36.0 - bodyTemp) * 0.5;
}
if (bodyTemp > 38.0)
{
    // Sweating increases calorie burn
    tempCost = (bodyTemp - 38.0) * 0.3;
}

// Total burn
double caloriesBurned = (bmr * activityMultiplier + tempCost) * minutesElapsed;
data.CaloriesRemaining -= caloriesBurned;
```

### Starvation Effects

```csharp
if (data.CaloriesRemaining < 0)
{
    // Negative calories = starvation
    double starvationSeverity = Math.Abs(data.CaloriesRemaining) / 5000.0;

    // Burn body fat first
    double fatBurn = Math.Min(data.FatKg, Math.Abs(data.CaloriesRemaining) * 0.00012);
    data.FatKg -= fatBurn;

    // Then muscle (muscle catabolism)
    if (data.FatKg < 2.0)  // Minimal fat left
    {
        double muscleBurn = Math.Abs(data.CaloriesRemaining) * 0.00008;
        data.MuscleKg -= muscleBurn;

        // Severe starvation effects
        effects.Add(CreateStarvation(starvationSeverity));
        messages.Add("Your body is consuming itself...");
    }
}

// Death from starvation
if (data.MuscleKg < 15.0 || data.FatKg < 1.0)
{
    playerDied = true;
    messages.Add("You starve to death.");
}
```

---

## Hydration System

### Hydration Loss

```csharp
// Base water loss (0.5% per hour)
double baseWaterLoss = 0.005 * (minutesElapsed / 60.0);

// Temperature effects
double tempMultiplier = 1.0;
if (bodyTemp > 37.0)
{
    // Sweating increases water loss
    tempMultiplier += (bodyTemp - 37.0) * 0.3;
}
if (environmentTemp > 30.0)
{
    // Hot environment increases loss
    tempMultiplier += (environmentTemp - 30.0) * 0.02;
}

// Activity effects
if (isActive) tempMultiplier *= 1.5;

// Apply water loss
data.HydrationLevel -= baseWaterLoss * tempMultiplier;
data.HydrationLevel = Math.Max(0, Math.Min(1.0, data.HydrationLevel));
```

### Dehydration Effects

```csharp
if (data.HydrationLevel < 0.3)
{
    double severity = 1.0 - data.HydrationLevel;
    effects.Add(CreateDehydration(severity));
    messages.Add("You're severely dehydrated!");
}

if (data.HydrationLevel <= 0)
{
    playerDied = true;
    messages.Add("You die of dehydration.");
}
```

---

## Effect Generation

### Dynamic Effect Creation

```csharp
public static Effect CreateHypothermia(double severity)
{
    return new EffectBuilder()
        .Named("Hypothermia")
        .WithSeverity(severity)
        .ReducesCapacity(CapacityNames.Moving, 0.2 * severity)
        .ReducesCapacity(CapacityNames.Manipulation, 0.15 * severity)
        .ReducesCapacity(CapacityNames.Consciousness, 0.1 * severity)
        .WithApplyMessage("The cold is affecting your body...")
        .Build();
}

public static Effect CreateStarvation(double severity)
{
    return new EffectBuilder()
        .Named("Starvation")
        .WithSeverity(severity)
        .ReducesCapacity(CapacityNames.Moving, 0.3 * severity)
        .ReducesCapacity(CapacityNames.Manipulation, 0.2 * severity)
        .WithApplyMessage("You're weak from hunger...")
        .Build();
}
```

### Threshold-Based Effects

```csharp
List<Effect> newEffects = new();

// Generate effects based on thresholds
if (bodyTemp < 35.0 && !HasEffect("Hypothermia"))
{
    newEffects.Add(CreateHypothermia((37.0 - bodyTemp) / 10.0));
}

if (data.CaloriesRemaining < 500 && !HasEffect("Hunger"))
{
    newEffects.Add(CreateHunger(0.5));
}

if (data.Exhaustion > 80 && !HasEffect("Exhausted"))
{
    newEffects.Add(CreateExhaustion(data.Exhaustion / 100.0));
}

return newEffects;
```

---

## Body Composition Impact

### Fat Effects

```csharp
// Cold resistance from fat
double coldResistance = 0.5 + (data.FatKg / data.TotalWeight) * 0.4;
heatLossRate *= (1 - coldResistance);

// Movement speed penalty from excess fat
if (data.FatKg > optimalFatKg)
{
    double fatPenalty = (data.FatKg - optimalFatKg) * 0.01;
    speedMultiplier *= (1 - fatPenalty);
}

// Calorie reserves
double calorieReserves = data.FatKg * 7700;  // ~7700 cal/kg fat
```

### Muscle Effects

```csharp
// Strength from muscle
double strengthMultiplier = 1.0 + (data.MuscleKg / optimalMuscleKg - 1) * 0.5;

// Base metabolic rate increases with muscle
double bmr = 1.0 + (data.MuscleKg * 0.02);

// Movement capacity
double movingCapacity = Math.Min(1.0, data.MuscleKg / minViableMuscleKg);
```

### Complete Example

```csharp
public static void UpdateBody(Player player, int minutes)
{
    var survivalData = player.Body.BundleSurvivalData();
    var activeEffects = player.EffectRegistry.GetAll();

    // Process survival needs
    var result = SurvivalProcessor.Process(
        survivalData,
        minutes,
        activeEffects
    );

    // Apply results
    player.Body.ApplySurvivalData(result.UpdatedData);

    // Add new effects
    foreach (var effect in result.NewEffects)
    {
        player.EffectRegistry.AddEffect(effect);
    }

    // Display messages
    foreach (var message in result.Messages)
    {
        Output.WriteLine(message);
    }

    // Handle death
    if (result.PlayerDied)
    {
        player.Die();
    }
}
```

---

**Related Files:**
- [SKILL.md](../SKILL.md) - Main guidelines
- [body-and-damage.md](body-and-damage.md) - Body composition details
- [effect-system.md](effect-system.md) - Effect creation

**Last Updated**: 2025-11-01
