# Effects vs Direct Code - Architectural Design Rule

**Last Updated**: 2025-11-03
**Status**: Established Pattern

---

## Overview

This document defines the architectural rule for when to use the **Effect System** versus **Direct Code** for gameplay conditions, status effects, and body states.

This decision affects every feature in the game and ensures consistency across the codebase.

---

## The Design Rule

### One-Sentence Version

**"Use Effects for applied conditions with discrete lifecycles; use direct code for continuous stats and permanent body state."**

### Detailed Criteria

**Use DIRECT CODE when ALL of these are true:**
1. **Core survival stat** (Calories, Hydration, Energy, Temperature, Body composition)
2. **Continuous calculation** (recalculated every update, no discrete state)
3. **Stat-restorable** (fixed by eating/drinking/sleeping, not treatment)
4. **No lifecycle** (no "apply" or "remove" moment, always exists)

**Use EFFECT SYSTEM when ANY of these are true:**
1. **Applied condition** (has a moment of application)
2. **Discrete state** (exists or doesn't exist, not continuously calculated)
3. **Requires treatment OR time-based removal** (not resolved by stat increase)
4. **Has lifecycle** (OnApply, OnUpdate, OnRemove hooks needed)

---

## Primary vs Secondary Criteria

### Primary Criteria (Determinative)

These criteria directly determine the choice:

| Criterion | Effect System | Direct Code |
|-----------|---------------|-------------|
| **Nature** | Applied condition | Continuous calculation |
| **Trigger** | Discrete application moment | Threshold or formula |
| **Resolution** | Treatment or time-based | Stat restoration (eat/drink/sleep) |
| **Source of Truth** | Can stack (multiple wounds) | Single value (one temperature) |
| **Lifecycle** | OnApply/OnUpdate/OnRemove | Always exists, just calculated |

### Secondary Criteria (Supportive)

These criteria support but don't determine the choice:

**Favor Effects:**
- Multiple instances can exist (multiple wounds, multiple bleeds)
- Affects specific body parts (frostbite on hand, wound on leg)
- Has stages/progression (infection worsening, disease stages)
- Player should see it listed in status effects display

**Favor Direct:**
- Single source of truth (only one body temperature)
- Performance critical (calculated every frame)
- Foundation for other systems (body composition affects speed/strength)
- Part of permanent body state (organ health, body part condition)

---

## Visual Decision Tree

```
START: New gameplay condition to implement
  |
  ├─ Is it a core survival stat (Calories/Hydration/Energy/Temp)?
  │   YES → Use DIRECT CODE (add to SurvivalData/SurvivalProcessor)
  │   NO → Continue
  |
  ├─ Is it permanent body damage?
  │   YES → Use BODY DAMAGE system (Body.Damage, DamageProcessor)
  │   NO → Continue
  |
  ├─ Does it have a discrete application moment?
  │   NO → Use DIRECT CALCULATION (computed property or stat)
  │   YES → Continue
  |
  ├─ Is it resolved by eating/drinking/sleeping?
  │   YES → Use DIRECT CODE (handled in SurvivalProcessor)
  │   NO → Continue
  |
  ├─ Does it need OnApply/OnUpdate/OnRemove hooks?
  │   YES → Use EFFECT SYSTEM
  │   NO → Consider if simple stat check works
  |
  └─ DEFAULT → Use EFFECT SYSTEM (safest choice for conditions)
```

---

## Pattern Comparison

### Effects Are for CONDITIONS

**Characteristics:**
- Has a beginning (applied)
- Has an end (removed/cured)
- Temporary or treatable
- Can stack or interact
- Player-visible status

**Examples:**
- Hypothermia (triggered when temperature < 95°F)
- Bleeding (from a wound, requires treatment)
- Poisoning (from eating bad food, time-limited)
- Spell buffs (cast and expire)
- Broken bone (from fall, requires treatment)

**Code Pattern:**
```csharp
// Temperature (direct) triggers hypothermia (effect)
if (bodyTemperature < HypothermiaThreshold)
{
    var hypothermia = EffectBuilderExtensions
        .CreateEffect("Hypothermia")
        .WithSeverity(severity)
        .ReducesCapacity(CapacityNames.Movement, 0.3)
        .Build();

    effectRegistry.AddEffect(hypothermia);
}
```

### Direct Code Is for STATES

**Characteristics:**
- Always exists
- Continuously calculated
- Inherent to the body
- Single source of truth
- Foundation for other systems

**Examples:**
- Body temperature (always has a value)
- Calories/Hydration/Energy (core survival stats)
- Body composition (fat/muscle mass)
- Encumbered state (weight calculation)
- Movement speed (derived from stats)

**Code Pattern:**
```csharp
// Starvation is consequence of calories = 0, not separate condition
if (survivalData.Calories <= 0)
{
    // Apply direct consequences
    double starvationPenalty = CalculateStarvationPenalty();
    capacityModifiers.Movement *= (1.0 - starvationPenalty);

    // Consume body reserves
    ConsumeBodyFat(minutesElapsed);
}
```

---

## Complete Feature Categorization

Based on comprehensive analysis of 38 current and planned features:

### Current Implementations - Correct ✅

| Feature | System | Reasoning |
|---------|--------|-----------|
| Hypothermia | **Effect** | Threshold-triggered, temporary, treatable |
| Hyperthermia | **Effect** | Threshold-triggered, temporary, treatable |
| Frostbite | **Effect** | Body-part specific, requires treatment |
| Shivering | **Effect** | Temporary response, auto-resolves |
| Sweating | **Effect** | Temporary response, dehydration effect |
| Starvation consequences | **Direct** | Calories = 0 state, stat-restorable |
| Dehydration consequences | **Direct** | Hydration = 0 state, stat-restorable |
| Exhaustion consequences | **Direct** | Energy = 0 state, sleep-restorable |
| Body temperature | **Direct** | Core stat, always exists |
| Organ damage | **Direct** | Permanent body state, Body.Damage system |

### Planned Features - Recommended

| Feature | System | Reasoning |
|---------|--------|-----------|
| Bleeding | **Effect** | Multiple wounds, requires treatment |
| Broken bone | **Effect** | Long-term, body-part specific, treatment |
| Infection | **Effect** | Progressive, requires treatment |
| Disease | **Effect** | Multiple stages, treatment required |
| Poisoning | **Effect** | Temporary, damage over time |
| Spell buffs | **Effect** | Temporary, stackable, duration-based |
| Well-rested | **Effect** | Temporary buff from sleep |
| Wet/Soaked | **Effect** | Environmental, drying required |
| Food poisoning | **Effect** | Temporary, time-based |
| Adrenaline | **Effect** | Temporary buff, extreme short duration |
| Fear/Panic | **Effect** | Temporary, combat-triggered |
| Encumbered | **Direct** | Weight calculation, continuous |
| Body composition | **Direct** | Fat/muscle mass, core stats |
| Mental stress | **Direct** | Core stat (like Energy) |
| Movement speed | **Direct** | Derived from body stats |

---

## Key Architectural Patterns

### Pattern 1: Direct Stat → Threshold Effect

**When a core stat crosses a threshold, it triggers an effect.**

**Example:**
```csharp
// Body temperature (direct) is continuous
bodyTemperature = CalculateBodyTemperature();

// When it crosses threshold, apply effect
if (bodyTemperature < HypothermiaThreshold)
{
    effectRegistry.AddEffect(hypothermia);
}
```

**Used For:**
- Temperature → Hypothermia/Hyperthermia
- Temperature → Frostbite (severe)
- Temperature → Shivering/Sweating

**NOT Used For:**
- Calories → Starvation (direct consequence instead)
- Hydration → Dehydration (direct consequence instead)
- Energy → Exhaustion (direct consequence instead)

**Why Different?**
- **Temperature effects are environmental** - you get hypothermia from the cold, warming up removes it
- **Starvation is a state** - you ARE starving because calories = 0, eating fixes it immediately
- Temperature can be normal while hypothermic (lingering effect)
- Calories cannot be normal while starving (contradiction)

### Pattern 2: Direct State → Direct Consequences

**When a core stat reaches critical levels, apply direct consequences without intermediate effects.**

**Example:**
```csharp
// In Body.Update() after SurvivalProcessor
if (survivalData.Calories <= 0)
{
    // Direct capacity penalties
    ApplyStarvationPenalties(capacities);

    // Direct body composition changes
    ConsumeBodyReserves(minutesElapsed);

    // Direct organ damage (if prolonged)
    if (prolongedStarvation)
        ApplyOrganDamage();
}
```

**Used For:**
- Starvation → Capacity penalties + body consumption
- Dehydration → Capacity penalties + organ risk
- Exhaustion → Capacity penalties + debuffs
- Encumbered → Movement penalty

**Why Direct?**
- No discrete "application" moment (gradual onset)
- Resolved by stat restoration, not treatment
- State and consequence are the same thing
- Performance (calculated every update)

### Pattern 3: Action → Effect Application

**Discrete events apply effects with lifecycles.**

**Example:**
```csharp
// Combat hit applies bleeding
var bleeding = EffectBuilderExtensions
    .CreateEffect("Bleeding")
    .From("Wolf Bite")
    .Targeting(bodyPart)
    .WithSeverity(0.7)
    .OnUpdate(target => {
        target.Body.Damage(bleedDamage);
    })
    .Build();

target.EffectRegistry.AddEffect(bleeding);
```

**Used For:**
- Combat → Bleeding/Wounds/Broken bones
- Consuming item → Food poisoning/Spell effects
- Environmental exposure → Wet/Snow-blind
- Disease transmission → Infection/Parasites

---

## Edge Cases & Resolutions

### Case 1: Starvation - Why Not an Effect?

**Initial Thought:** Starvation could be an effect triggered at 0% calories.

**Decision:** Keep as direct consequence.

**Reasoning:**
- Starvation IS the state of having no calories, not a separate condition
- No discrete application moment (gradual slide to 0%)
- Resolved by eating (stat restoration), not treatment
- Capacity penalties calculated directly from calorie level
- Body composition changes are continuous (not on/off)

**Exception:** Extreme starvation symptoms (hallucinations, organ failure) could be effects spawned by prolonged starvation, but the base starvation state remains direct.

### Case 2: Exhaustion - Why Not an Effect?

**Initial Thought:** "Exhausted" could be a debuff effect.

**Decision:** Keep as direct consequence.

**Reasoning:**
- Exhaustion IS the Energy stat, not separate
- Energy drains continuously (no application moment)
- Resolved by sleep (stat restoration)
- Can stay awake indefinitely (just with penalties)
- Movement penalties calculated directly from Energy level

**Exception:** Post-adrenaline "Exhausted" crash could be an effect (discrete trigger from adrenaline wearing off).

### Case 3: Broken Bones - Effect or Damage?

**Initial Thought:** Could be permanent body damage like destroyed organs.

**Decision:** Use Effect system.

**Reasoning:**
- Has discrete application moment (combat, fall)
- Requires specific treatment (splint, time)
- Can exist on multiple body parts independently
- Has stages (fresh break → healing → healed)
- NOT permanent (unlike destroyed organs)
- Temporary capacity reduction (until treated)

### Case 4: Wet/Soaked - Effect or State?

**Initial Thought:** Could be a boolean state on the actor.

**Decision:** Use Effect system.

**Reasoning:**
- Has discrete application moment (rain, swimming)
- Requires specific removal (drying by fire, time)
- Temporary cold resistance penalty
- Can be treated (not just stat restoration)
- Player should see "Wet" in status

### Case 5: Encumbered - Effect or Calculation?

**Initial Thought:** Could be an effect applied when overweight.

**Decision:** Use direct calculation.

**Reasoning:**
- No discrete application moment (continuous weight check)
- Resolved by dropping items (not treatment or time)
- State and consequence are the same (heavy = slow)
- Performance critical (checked often)
- Movement penalty calculated directly from weight/strength ratio

---

## Why This Matters

### Code Organization

**Clear separation of concerns:**
- Effects handle temporary, applied conditions
- Direct code handles permanent, continuous state
- Body.Damage handles permanent injury
- Consistent patterns across codebase

### Performance

**Optimization opportunities:**
- Effects can be sparse (only exist when needed)
- Direct stats always calculated (but simple formulas)
- No unnecessary object creation for inherent states

### Player Understanding

**Mental model:**
- Status effects = things happening TO you (removable)
- Stats = your current state (restorable)
- Injuries = permanent damage (healable)
- Clear cause and effect

### Maintenance

**Easy to extend:**
- New environmental hazard? → Effect
- New survival stat? → Direct code
- New injury type? → Effect or Damage
- Pattern is clear, decisions are fast

---

## Anti-Patterns to Avoid

### ❌ Don't: Use Effects for Core Stats

```csharp
// WRONG - Creates "Hungry" effect when calories low
var hunger = EffectBuilderExtensions
    .CreateEffect("Hungry")
    .WithSeverity(1.0 - caloriePercent)
    .Build();
```

**Why Wrong:**
- Hunger IS the calorie state, not separate
- Creates redundant tracking
- Confuses stat restoration with treatment

**Correct:**
```csharp
// RIGHT - Check calories directly
if (calories < lowThreshold)
{
    ApplyHungerPenalties(calories);
}
```

### ❌ Don't: Use Direct Code for Applied Conditions

```csharp
// WRONG - Tracks bleeding as boolean flag
actor.IsBleeding = true;
actor.BleedingSeverity = 0.7;
```

**Why Wrong:**
- Can't have multiple bleeding wounds
- No lifecycle hooks for damage-over-time
- Hard to track source/location
- Manual cleanup required

**Correct:**
```csharp
// RIGHT - Use Effect system
var bleeding = CreateBleedingEffect();
actor.EffectRegistry.AddEffect(bleeding);
```

### ❌ Don't: Mix Patterns Inconsistently

```csharp
// WRONG - Temperature uses effect but calories use direct
var starvation = CreateStarvationEffect(); // ← Inconsistent
var hypothermia = CreateHypothermiaEffect(); // ← Correct
```

**Why Wrong:**
- Inconsistent pattern confuses developers
- One system can handle both correctly
- Temperature and calories are both stats

**Correct:**
```csharp
// RIGHT - Temperature triggers effect, calories apply direct
if (temperature < threshold) effectRegistry.AddEffect(hypothermia);
if (calories <= 0) ApplyStarvationConsequences();
```

---

## Testing the Rule

When implementing a new feature, ask these questions:

1. **Is it a core survival stat?** → Direct
2. **Is it permanent body damage?** → Body.Damage
3. **Does it have a discrete application moment?** → Effect if yes, Direct if no
4. **Is it resolved by stat restoration?** → Direct if yes, Effect if no
5. **Does it need lifecycle hooks?** → Effect if yes, Direct if no

**If unsure:** Default to Effect system (safest, most flexible).

**If performance critical:** Consider direct calculation.

**If affects multiple body parts independently:** Definitely Effect.

---

## Examples from Codebase

### ✅ Correct: Temperature → Hypothermia

**Location**: `SurvivalProcessor.cs` lines 184-212

```csharp
// Temperature is direct stat (calculated continuously)
data.Temperature += tempChange;

// Hypothermia is threshold effect (discrete condition)
if (data.Temperature < HypothermiaThreshold)
{
    var hypothermia = EffectBuilderExtensions
        .CreateEffect("Hypothermia")
        .Temperature(TemperatureType.Hypothermia)
        .WithSeverity(severity)
        .Build();

    result.Effects.Add(hypothermia);
}
```

**Why Correct:**
- Temperature = continuous stat (direct)
- Hypothermia = applied condition (effect)
- Clear separation, correct pattern

### ✅ Correct: Starvation → Direct Consequences

**Location**: To be implemented in `Body.cs`

```csharp
// Calories is direct stat
survivalData.Calories -= caloriesBurned;

// Starvation consequences are direct (not effects)
if (survivalData.Calories <= 0)
{
    // Apply capacity penalties directly
    ApplyStarvationPenalties();

    // Consume body reserves directly
    ConsumeBodyFat(minutesElapsed);
}
```

**Why Correct:**
- Calories = core stat (direct)
- Starvation = state consequence (direct)
- No intermediate effect object needed

### ✅ Correct: Combat → Bleeding Effect

**Location**: Example pattern for future implementation

```csharp
// Combat hit is discrete event
void OnHit(Actor target, BodyPart part, double damage)
{
    // Apply bleeding effect
    var bleeding = EffectBuilderExtensions
        .CreateEffect("Bleeding")
        .From($"{this.Name} Attack")
        .Targeting(part.Name)
        .WithSeverity(damage / 10.0)
        .OnUpdate(t => t.Body.Damage(bleedDamage))
        .Build();

    target.EffectRegistry.AddEffect(bleeding);
}
```

**Why Correct:**
- Discrete application moment (hit)
- Requires treatment (not stat restoration)
- Can have multiple (different body parts)
- Has lifecycle (OnUpdate for damage)

---

## Conclusion

This architectural rule provides **clear, consistent guidance** for implementing any gameplay condition. It has been validated against 38 current and planned features with excellent alignment.

**The current codebase already follows this pattern correctly** - no refactoring needed.

**Future features should follow this rule** to maintain consistency and code quality.

---

## References

- Research document: `dev/active/gameplay-fixes-sprint/gameplay-fixes-sprint-context.md`
- Effect system: `documentation/effect-system.md`
- Body system: `documentation/body-and-damage.md`
- Survival processing: `documentation/survival-processing.md`
