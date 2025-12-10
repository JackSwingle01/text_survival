# Effect System - Buffs, Debuffs, and Status Effects

EffectBuilder pattern, EffectRegistry management, severity system, capacity modifiers, and common effect extensions.

## Table of Contents

- [Effect System Overview](#effect-system-overview)
- [EffectBuilder Pattern](#effectbuilder-pattern)
- [Severity System](#severity-system)
- [Capacity Modifiers](#capacity-modifiers)
- [EffectRegistry Usage](#effectregistry-usage)
- [Effect Lifecycle Hooks](#effect-lifecycle-hooks)
- [Common Effect Extensions](#common-effect-extensions)
- [Complete Effect Examples](#complete-effect-examples)

---

## Effect System Overview

**Purpose**: Track temporary or permanent status effects (buffs/debuffs) on Actors (Player/NPCs).

**Core Classes**:
- `Effect` - Base abstract class (Effect.cs:1-133)
- `EffectBuilder` - Fluent builder for creating effects (EffectBuilder.cs:1-163)
- `EffectRegistry` - Per-Actor effect container (EffectRegistry.cs:1-60)
- `DynamicEffect` - Concrete implementation built by EffectBuilder

**Key Concepts**:
- **Severity**: 0.0-1.0 scale representing effect strength
- **Capacity Modifiers**: Reduce/modify body part capacities (Movement, Consciousness, etc.)
- **Survival Stats**: Affect temperature, hunger, thirst, exhaustion
- **Lifecycle Hooks**: OnApply, OnUpdate, OnSeverityChange, OnRemove
- **AllowMultiple**: Whether same effect type can stack

```csharp
// Every Actor has an EffectRegistry
public class Actor
{
    public EffectRegistry EffectRegistry { get; set; }
    // ...
}

// Add effects via registry
player.EffectRegistry.AddEffect(bleedingEffect);
```

---

## EffectBuilder Pattern

**Location**: `Effects/EffectBuilder.cs`

Effects are built using a fluent builder pattern:

```csharp
var effect = EffectBuilderExtensions.CreateEffect("Bleeding")
    .From("Wolf Bite")
    .Targeting("Left Arm")
    .WithSeverity(0.7)
    .WithHourlySeverityChange(-0.05)  // Natural clotting
    .AllowMultiple(true)
    .ReducesCapacity(CapacityNames.Manipulation, 0.3)
    .OnUpdate(target => {
        // Damage every minute
        target.Body.Damage(new DamageInfo {
            Amount = 0.5,
            Type = DamageType.Bleed
        });
    })
    .Build();
```

**Builder Methods**:

### .Named(string effectKind)
Sets the effect type name (REQUIRED).
```csharp
.Named("Bleeding")
.Named("Poisoned")
.Named("Well Rested")
```

### .From(string source)
Sets the source/cause of effect.
```csharp
.From("Wolf Bite")
.From("Poisonous Berries")
.From("Good Sleep")
```

### .Targeting(string? bodyPart)
Targets specific body part (optional, null = whole body).
```csharp
.Targeting("Left Arm")  // Specific part
.Targeting(null)  // Whole body (default)
```

### .WithSeverity(double severity)
Initial severity (0.0-1.0, clamped automatically).
```csharp
.WithSeverity(0.3)  // Minor
.WithSeverity(0.7)  // Severe
.WithSeverity(1.0)  // Critical
```

### .WithHourlySeverityChange(double rate)
How severity changes per hour (positive = worsens, negative = heals).
```csharp
.WithHourlySeverityChange(-0.05)  // Natural healing (improves)
.WithHourlySeverityChange(0.1)    // Worsening condition
.WithHourlySeverityChange(0)      // Permanent (no change)
```

### .AllowMultiple(bool allow)
Whether multiple instances of same effect can exist.
```csharp
.AllowMultiple(true)   // Can have multiple bleeds
.AllowMultiple(false)  // Only one instance (default)
```

### .RequiresTreatment(bool requires)
Whether effect requires active treatment to improve.
```csharp
.RequiresTreatment(true)  // Won't heal without treatment
.RequiresTreatment(false) // Natural recovery (default)
```

### .ReducesCapacity(string capacity, double reduction)
Reduces a body capacity by fixed amount.
```csharp
.ReducesCapacity(CapacityNames.Moving, 0.3)      // -30% movement
.ReducesCapacity(CapacityNames.Consciousness, 0.5) // -50% consciousness
```

### .ModifiesCapacity(string capacity, double multiplier)
Multiplies a body capacity (can increase or decrease).
```csharp
.ModifiesCapacity(CapacityNames.Moving, -0.2)  // -20%
.ModifiesCapacity(CapacityNames.Moving, 0.1)   // +10% (buff)
```

### Lifecycle Hook Methods

```csharp
.OnApply(Actor target => { /* When first applied */ })
.OnUpdate(Actor target => { /* Every minute */ })
.OnSeverityChange((Actor target, double old, double new_) => { /* Severity changed */ })
.OnRemove(Actor target => { /* When removed */ })
```

### .Build()
Creates the final `DynamicEffect` object.

---

## Severity System

**Scale**: 0.0 (no effect) to 1.0 (maximum effect)

**Severity Bands**:
```csharp
// From Effect.cs:118-124
if (Severity < 0.3) return "Minor";      // 0.0-0.29
if (Severity < 0.7) return "Moderate";   // 0.3-0.69
if (Severity < 0.9) return "Severe";     // 0.7-0.89
return "Critical";                        // 0.9-1.0
```

**Automatic Severity Changes**:
Effects update severity every minute based on `hourlySeverityChange`:
```csharp
// From Effect.cs:53-57
if (hourlySeverityChange > 0)
{
    double minuteChange = hourlySeverityChange / 60;
    UpdateSeverity(target, minuteChange);
}
```

**Auto-Removal**:
When severity reaches 0, effect is automatically removed:
```csharp
// From Effect.cs:59-63
if (Severity <= 0)
{
    Remove(target);
    return;
}
```

**Example - Natural Clotting**:
```csharp
var bleeding = CreateEffect("Bleeding")
    .WithSeverity(0.8)  // Start severe
    .WithHourlySeverityChange(-0.05)  // -3% per hour
    .Build();
// After 16 hours: severity drops to 0, effect auto-removes
```

**Example - Worsening Infection**:
```csharp
var infection = CreateEffect("Infection")
    .WithSeverity(0.2)  // Start minor
    .WithHourlySeverityChange(0.1)  // +6% per hour
    .RequiresTreatment(true)  // Won't improve without treatment
    .Build();
// Without treatment: reaches critical in ~13 hours
```

---

## Capacity Modifiers

Effects can reduce body part capacities (see [body-and-damage.md](body-and-damage.md) for capacity system).

**Common Capacities**:
- `CapacityNames.Moving` - Movement ability
- `CapacityNames.Manipulation` - Hand use
- `CapacityNames.Consciousness` - Awareness
- `CapacityNames.BloodPumping` - Circulation
- `CapacityNames.Sight` - Vision
- `CapacityNames.Hearing` - Hearing

**Reduction Semantics**:
```csharp
.ReducesCapacity(CapacityNames.Moving, 0.3)
// Reduces capacity by 0.3 (30% reduction)
// If capacity was 1.0, now 0.7
// If capacity was 0.8, now 0.5
```

**Multiple Modifiers Stack**:
```csharp
// Two effects reducing same capacity
effect1.ReducesCapacity(CapacityNames.Moving, 0.2);  // -20%
effect2.ReducesCapacity(CapacityNames.Moving, 0.3);  // -30%
// Total reduction: -50% (stacks additively)
```

**Severity Multiplier**:
Capacity modifiers scale with effect severity:
```csharp
.ReducesCapacity(CapacityNames.Consciousness, 0.5)
// At severity 1.0: reduces by 0.5
// At severity 0.5: reduces by 0.25
// At severity 0.2: reduces by 0.1
```

**Example - Hypothermia**:
```csharp
var hypothermia = CreateEffect("Hypothermia")
    .WithSeverity(0.8)
    .ReducesCapacity(CapacityNames.Moving, 0.3)         // Hard to move
    .ReducesCapacity(CapacityNames.Manipulation, 0.3)   // Numb hands
    .ReducesCapacity(CapacityNames.Consciousness, 0.5)  // Drowsy
    .Build();
// Player effectively loses 24% movement, 24% manipulation, 40% consciousness
```

---

## EffectRegistry Usage

**Location**: `Effects/EffectRegistry.cs`

Every Actor has an EffectRegistry to manage their active effects.

### Adding Effects

```csharp
// From EffectRegistry.cs:9-26
player.EffectRegistry.AddEffect(effect);
```

**AllowMultiple Logic**:
- If `AllowMultiple = true`: Effect added unconditionally
- If `AllowMultiple = false`:
  - Check for existing effect of same kind
  - If exists: Update severity to maximum (existing vs new)
  - If not exists: Add as new effect

```csharp
// Example: Bleeding allows multiple (different wounds)
var bleed1 = CreateEffect("Bleeding").From("Wolf").AllowMultiple(true).Build();
var bleed2 = CreateEffect("Bleeding").From("Bear").AllowMultiple(true).Build();
player.EffectRegistry.AddEffect(bleed1);
player.EffectRegistry.AddEffect(bleed2);
// Result: TWO active bleeding effects

// Example: Hypothermia doesn't allow multiple (one state)
var hypo1 = CreateEffect("Hypothermia").WithSeverity(0.5).Build();
var hypo2 = CreateEffect("Hypothermia").WithSeverity(0.8).Build();
player.EffectRegistry.AddEffect(hypo1);  // severity 0.5
player.EffectRegistry.AddEffect(hypo2);  // updates to severity 0.8 (max)
// Result: ONE hypothermia effect at severity 0.8
```

### Removing Effects

```csharp
// Remove specific effect
player.EffectRegistry.RemoveEffect(effect);

// Remove all effects of a type
player.EffectRegistry.RemoveEffectsByKind("Bleeding");
```

### Querying Effects

```csharp
// Get all active effects
List<Effect> all = player.EffectRegistry.GetAll();

// Get effects of specific kind
List<Effect> bleeds = player.EffectRegistry.GetEffectsByKind("Bleeding");

// Check if has effect (manually)
bool hasBleeding = player.EffectRegistry.GetAll()
    .Any(e => e.EffectKind == "Bleeding");
```

### Update Cycle

```csharp
// Called automatically by Actor.Update()
player.EffectRegistry.Update();
```

**What Update Does** (EffectRegistry.cs:39-44):
1. Calls `effect.Update(owner)` on each effect
2. Each effect runs its OnUpdate hook
3. Each effect updates severity if has change rate
4. Effects at severity 0 auto-remove
5. Inactive effects cleaned up

---

## Effect Lifecycle Hooks

Effects have four hook methods you can implement:

### OnApply
Called once when effect is first applied.

**Use For**:
- Initial messages ("You start bleeding!")
- One-time setup
- Immediate effects

```csharp
.OnApply(target => {
    Output.WriteLine($"{target.Name} is now bleeding!");
})
```

### OnUpdate
Called every minute while effect is active.

**Use For**:
- Periodic damage/healing
- Repeated messages
- Status checks

```csharp
.OnUpdate(target => {
    // Damage every minute
    target.Body.Damage(new DamageInfo {
        Amount = 0.5,
        Type = DamageType.Bleed
    });
})
```

### OnSeverityChange
Called whenever severity changes significantly (>0.01).

**Use For**:
- Threshold messages ("Your bleeding is slowing")
- Stage transitions
- Conditional logic based on severity

```csharp
.OnSeverityChange((target, oldSeverity, newSeverity) => {
    if (newSeverity < 0.3 && oldSeverity >= 0.3) {
        Output.WriteLine($"{target.Name}'s condition improves!");
    }
})
```

### OnRemove
Called once when effect is removed (manually or severity reaches 0).

**Use For**:
- Cleanup messages ("You stop bleeding")
- Remove related effects
- Apply follow-up effects

```csharp
.OnRemove(target => {
    Output.WriteLine($"{target.Name} is no longer bleeding.");
})
```

**Hook Combination**:
```csharp
var effect = CreateEffect("Infection")
    .OnApply(t => Output.WriteLine("Infection sets in..."))
    .OnUpdate(t => t.Body.Damage(damageInfo))  // Every minute
    .OnSeverityChange((t, old, new_) => {
        if (new_ > 0.7 && old <= 0.7) {
            Output.WriteLine("The infection worsens!");
        }
    })
    .OnRemove(t => Output.WriteLine("The infection clears."))
    .Build();
```

---

## Common Effect Extensions

**Location**: `Effects/EffectBuilder.cs` (EffectBuilderExtensions, lines 165-415)

Pre-built extension methods for common effect patterns.

### Bleeding(damagePerHour)

```csharp
var bleed = CreateEffect("Bleeding")
    .From("Wolf Bite")
    .Targeting("Left Arm")
    .Bleeding(damagePerHour: 5.0)
    .Build();
```

**What it does**:
- Natural clotting (-5% severity/hour)
- Allows multiple instances
- Reduces BloodPumping and Consciousness capacities
- Deals damage every minute scaled by severity
- Automatic messages (apply, periodic, improvement)

### Poisoned(damagePerHour)

```csharp
var poison = CreateEffect("Poison")
    .From("Toxic Berries")
    .Poisoned(damagePerHour: 3.0)
    .Build();
```

**What it does**:
- Slow detoxification (-2% severity/hour)
- Allows multiple instances
- Reduces Consciousness, Manipulation, Moving, BloodPumping
- Deals poison damage every minute scaled by severity

### Healing(healPerHour)

```csharp
var heal = CreateEffect("Healing")
    .From("Medicinal Herbs")
    .Targeting("Right Leg")
    .Healing(healPerHour: 10.0)
    .Build();
```

**What it does**:
- Duration: 1 hour (auto-expires)
- Heals targeted body part every minute
- Healing scaled by severity

### Temperature(TemperatureType)

```csharp
// Hypothermia
var hypo = CreateEffect("Hypothermia")
    .Temperature(TemperatureType.Hypothermia)
    .WithSeverity(0.7)
    .Build();

// Frostbite
var frostbite = CreateEffect("Frostbite")
    .Temperature(TemperatureType.Frostbite)
    .Targeting("Right Hand")
    .Build();
```

**Types**:
- `Hypothermia`: Reduces Moving, Manipulation, Consciousness, BloodPumping; Requires treatment
- `Hyperthermia`: Reduces Consciousness, Moving, BloodPumping; Requires treatment
- `Frostbite`: Reduces Manipulation, Moving, BloodPumping; Natural recovery

### Duration Helpers

```csharp
// Fixed duration
.WithDuration(minutes: 60)  // Expires in 1 hour

// Permanent (no auto-removal)
.Permanent()

// Natural healing
.NaturalHealing(rate: -0.05)  // -5% per hour
```

### Survival Stat Modifiers

```csharp
// Dehydration
.CausesDehydration(hydrationLossPerMinute: 0.5)

// Exhaustion
.CausesExhaustion(exhaustionPerMinute: 0.2)

// Temperature change
.AffectsTemperature(hourlyChange: -5.0)  // Cools by 5°C/hour
```

### Message Helpers

```csharp
.WithApplyMessage("You feel the poison coursing through your veins!")
.WithRemoveMessage("The poison's effects fade.")
.WithPeriodicMessage("The wound throbs painfully...", chance: 0.05)
```

### Advanced Extensions

```csharp
// Effect chains
.AppliesOnRemoval(nextEffect)  // Apply another effect when this ends

// Effect clearing
.ClearsEffectType("Bleeding")  // Removes all Bleeding effects on apply

// Threshold triggers
.WhenSeverityDropsBelow(0.3, target => {
    Output.WriteLine("Condition improving!");
})

.WhenSeverityRisesAbove(0.7, target => {
    Output.WriteLine("Condition worsening!");
})
```

---

## Complete Effect Examples

### Example 1: Simple Bleeding Effect

```csharp
var bleeding = CreateEffect("Bleeding")
    .From("Combat Wound")
    .Targeting("Torso")
    .WithSeverity(0.6)
    .WithHourlySeverityChange(-0.05)  // Natural clotting
    .AllowMultiple(true)
    .ReducesCapacity(CapacityNames.BloodPumping, 0.2)
    .OnUpdate(target => {
        double damage = 0.5 * effect.Severity;
        target.Body.Damage(new DamageInfo {
            Amount = damage,
            Type = DamageType.Bleed,
            TargetPartName = "Torso"
        });
    })
    .WithApplyMessage("{target} is bleeding!")
    .WithRemoveMessage("{target}'s bleeding stops.")
    .Build();

player.EffectRegistry.AddEffect(bleeding);
```

### Example 2: Food Poisoning (Multi-Effect)

```csharp
var foodPoisoning = CreateEffect("Food Poisoning")
    .From("Spoiled Meat")
    .WithSeverity(0.8)
    .WithHourlySeverityChange(-0.1)  // Recovers in ~8 hours
    .ReducesCapacity(CapacityNames.Consciousness, 0.3)
    .ReducesCapacity(CapacityNames.Moving, 0.2)
    .CausesDehydration(hydrationLossPerMinute: 1.0)  // Severe dehydration
    .OnUpdate(target => {
        // Random nausea messages
        if (Utils.DetermineSuccess(0.02)) {
            Output.WriteLine($"{target.Name} feels nauseous...");
        }

        // Periodic damage
        if (Utils.DetermineSuccess(0.1)) {
            target.Body.Damage(new DamageInfo {
                Amount = 2.0,
                Type = DamageType.Poison
            });
        }
    })
    .WithApplyMessage("{target} feels violently ill!")
    .WhenSeverityDropsBelowWithMessage(0.5, "{target} starts feeling better.")
    .WithRemoveMessage("{target} has recovered from food poisoning.")
    .Build();
```

### Example 3: Well Rested Buff

```csharp
var wellRested = CreateEffect("Well Rested")
    .From("Good Sleep")
    .WithSeverity(1.0)
    .WithDuration(480)  // 8 hours
    .ModifiesCapacity(CapacityNames.Consciousness, 0.1)  // +10% awareness
    .ModifiesCapacity(CapacityNames.Moving, 0.05)  // +5% movement
    .GrantsExperience("Survival", xpPerMinute: 1)  // Bonus XP
    .WithApplyMessage("You feel refreshed and energized!")
    .WithRemoveMessage("The feeling of being well-rested fades.")
    .Build();
```

### Example 4: Progressive Hypothermia

```csharp
var hypothermia = CreateEffect("Hypothermia")
    .From("Extreme Cold")
    .WithSeverity(0.3)  // Start minor
    .RequiresTreatment(true)  // Needs active warming
    .ReducesCapacity(CapacityNames.Moving, 0.4)
    .ReducesCapacity(CapacityNames.Manipulation, 0.4)
    .ReducesCapacity(CapacityNames.Consciousness, 0.6)
    .WhenSeverityRisesAbove(0.5, target => {
        Output.WriteWarning($"{target.Name} is shivering uncontrollably!");
    })
    .WhenSeverityRisesAbove(0.8, target => {
        Output.WriteDanger($"{target.Name} is in critical danger from hypothermia!");
    })
    .OnUpdate(target => {
        // Gets worse in cold
        var tempEffect = target.Body.Temperature < 35;
        if (tempEffect) {
            target.EffectRegistry.GetEffectsByKind("Hypothermia").ForEach(e => {
                e.UpdateSeverity(target, 0.01);  // Worsens
            });
        }
    })
    .Build();
```

### Example 5: Timed Buff (Adrenaline Rush)

```csharp
var adrenaline = CreateEffect("Adrenaline Rush")
    .From("Combat")
    .WithSeverity(1.0)
    .WithDuration(5)  // 5 minutes
    .ModifiesCapacity(CapacityNames.Moving, 0.2)  // +20% movement
    .ModifiesCapacity(CapacityNames.Manipulation, 0.15)  // +15% manipulation
    .CausesExhaustion(exhaustionPerMinute: 0.5)  // Tiring
    .AppliesOnRemoval(
        CreateEffect("Exhausted")
            .From("Adrenaline Crash")
            .WithSeverity(0.6)
            .WithDuration(30)
            .ReducesCapacity(CapacityNames.Moving, 0.3)
            .Build()
    )
    .WithApplyMessage("Adrenaline surges through {target}!")
    .WithRemoveMessage("{target}'s adrenaline rush fades, leaving exhaustion.")
    .Build();
```

### Example 6: Conditional Effect (Infection from Wound)

```csharp
var wound = CreateEffect("Open Wound")
    .From("Bear Claw")
    .Targeting("Left Leg")
    .WithSeverity(0.7)
    .NaturalHealing(rate: -0.02)  // Slow natural healing
    .AllowMultiple(true)
    .OnUpdate(target => {
        // Chance to develop infection if untreated
        if (Utils.DetermineSuccess(0.01)) {  // 1% per minute
            var infection = CreateEffect("Infection")
                .From("Untreated Wound")
                .Targeting("Left Leg")
                .WithSeverity(0.3)
                .WithHourlySeverityChange(0.05)  // Worsens
                .RequiresTreatment(true)
                .Poisoned(damagePerHour: 5.0)
                .Build();

            target.EffectRegistry.AddEffect(infection);
            Output.WriteWarning("The wound has become infected!");
        }
    })
    .Build();
```

---

## Anti-Patterns to Avoid

### ❌ Direct Capacity Modification
```csharp
// WRONG: Don't modify capacities directly
player.Body.GetCapacity("Moving").Value -= 0.3;
```

### ✅ Use Effects
```csharp
// CORRECT: Use effect system
var effect = CreateEffect("Injured")
    .ReducesCapacity(CapacityNames.Moving, 0.3)
    .Build();
player.EffectRegistry.AddEffect(effect);
```

### ❌ Manual Severity Management
```csharp
// WRONG: Don't manually track severity
double severity = 1.0;
// ... later ...
severity -= 0.1;
```

### ✅ Use Automatic Severity Changes
```csharp
// CORRECT: Let effect system manage it
.WithHourlySeverityChange(-0.1)
```

### ❌ Forgetting AllowMultiple
```csharp
// WRONG: Multiple bleeds overwrite each other
CreateEffect("Bleeding").Build();  // AllowMultiple defaults to false
```

### ✅ Enable Multiple for Stackable Effects
```csharp
// CORRECT: Multiple wounds can exist
CreateEffect("Bleeding").AllowMultiple(true).Build();
```

---

## Related Files

**Core System Files**:
- `Effects/Effect.cs` - Base effect class (Effect.cs:1-133)
- `Effects/EffectBuilder.cs` - Builder implementation (EffectBuilder.cs:1-415)
- `Effects/EffectRegistry.cs` - Effect container (EffectRegistry.cs:1-60)
- `Effects/DynamicEffect.cs` - Concrete effect implementation

**Integration Points**:
- `Actors/Actor.cs` - EffectRegistry ownership
- `Bodies/Body.cs` - Capacity system integration
- `Bodies/CapacityModifierContainer.cs` - Capacity modifier application
- `Survival/SurvivalProcessor.cs` - Survival stat effects

**Related Guides**:
- [body-and-damage.md](body-and-damage.md) - Capacity system details
- [survival-processing.md](survival-processing.md) - Survival stat integration
- [builder-patterns.md](builder-patterns.md) - Builder pattern philosophy
- [complete-examples.md](complete-examples.md) - Full effect feature examples

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
