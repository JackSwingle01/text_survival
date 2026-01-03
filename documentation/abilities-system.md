# Ability System

*Created: 2026-01-03*
*Last Updated: 2026-01-03*

## Table of Contents
- [Introduction](#introduction)
- [Core Concepts](#core-concepts)
- [The Six Core Abilities](#the-six-core-abilities)
- [AbilityContext](#abilitycontext)
- [Usage Patterns](#usage-patterns)
- [Decision Tree](#decision-tree-capacity-or-ability)
- [Integration Points](#integration-points)
- [Implementation Files](#implementation-files)

---

## Introduction

The Ability system provides a **two-tier architecture** for measuring character performance:

**Capacities** (Tier 1) - Raw body function (0-1)
- Internal to the body system
- Pure physiological measurements
- Used for threshold/availability checks
- Examples: "Can I move?" (Moving > 0.3), "Am I incapacitated?" (Consciousness < 0.3)

**Abilities** (Tier 2) - Context-aware performance
- Built on top of capacities
- Incorporate environmental factors (darkness, wetness, encumbrance)
- Used for performance calculations
- Examples: Speed (considers encumbrance), Perception (considers darkness), Dexterity (considers wetness)

### When to Use Each

| Use Case | Use This | Example |
|----------|----------|---------|
| **Threshold check** | Capacity | `if (capacities.Consciousness < 0.3)` // Incapacitated? |
| **Availability gate** | Capacity | `if (capacities.Moving < 0.3)` // Can move at all? |
| **Performance calculation** | Ability | `double travelTime = baseTime / actor.GetSpeed(context)` |
| **Effectiveness multiplier** | Ability | `double yield = baseYield * actor.GetPerception(context)` |

**Rule of thumb**: If you're checking a threshold for availability/impairment, use Capacity. If you're calculating how well something is performed, use Ability.

---

## Core Concepts

### Dependency Chain

Abilities influence each other and must be calculated in order:

```
Capacities (raw body function)
      ↓
  Vitality (foundation - "how alive are you")
      ↓
  Strength (power output, scaled by vitality)
      ↓
  Speed (vitality + encumbrance modulated by strength)

Perception (vitality + consciousness for alertness)
Dexterity (vitality for steadiness)
ColdResistance (body composition only)
```

### Context Factors

Abilities can be affected by environmental and situational factors:

- **Encumbrance** (0-1) - Inventory weight / max weight → affects Speed
- **Darkness** (0-1) - 0 = bright daylight, 1 = pitch black → affects Perception, Dexterity
- **Wetness** (0-1) - From weather/water exposure → affects Dexterity
- **Light Source** (bool) - Active fire or lit torch → negates darkness penalties

---

## The Six Core Abilities

### 1. Vitality - Foundation

**Purpose**: "How alive are you?" - Gates all other abilities

**Formula**:
```csharp
Vitality = min(Breathing, BloodPumping, Consciousness)
```

**Dependencies**: None - pure capacity measurement

**Interpretation**:
- **1.0** - Fully alive and functioning
- **0.7** - Diminished but stable
- **0.5** - Seriously compromised
- **0.3** - Near death
- **0.0** - Dead

**Used by**: Strength, Speed, Perception, Dexterity (as steadiness/alertness factor)

**Implementation**:
```csharp
// From Bodies/AbilityCalculator.cs
public static double CalculateVitality(Body body, CapacityModifierContainer effectModifiers)
{
    var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
    return Math.Min(
        capacities.Breathing,
        Math.Min(capacities.BloodPumping, capacities.Consciousness)
    );
}
```

---

### 2. Strength - Power Output

**Purpose**: Combat damage, encumbrance modulation. "Dying = weak, regardless of muscles."

**Formula**:
```csharp
baseStrength = muscleContribution + fatBonus + 0.30 (everyone has)
capacityFactor = (Moving * 0.5 + 0.5) * (Manipulation * 0.2 + 0.8)
Strength = baseStrength * capacityFactor * Vitality
```

**Dependencies**: Vitality

**Context Factors**: None

**Muscle Contribution** (diminishing returns):
- < 20% muscle: `musclePercent * 2.5`
- 20-40% muscle: `0.5 + (musclePercent - 0.2) * 1.0`
- \> 40% muscle: `0.7 + (musclePercent - 0.4) * 0.5`

**Key Insight**: A dying person (Vitality ~0) is weak regardless of muscle mass. A strong person with 40% muscle but 0.3 Vitality performs like someone with 12% muscle at full health.

**Used by**:
- Combat damage calculation
- Speed (modulates encumbrance effect)

**Implementation**:
```csharp
// From Bodies/AbilityCalculator.cs
public static double CalculateStrength(Body body, CapacityModifierContainer effectModifiers)
{
    var capacities = CapacityCalculator.GetCapacities(body, effectModifiers);
    double vitality = CalculateVitality(body, effectModifiers);

    // Body composition calculations...
    double bodyContribution = baseStrength + muscleContribution + fatBonus;
    double manipulationContribution = 0.80 + 0.20 * capacities.Manipulation;
    double movingContribution = 0.50 + 0.50 * capacities.Moving;

    return bodyContribution * movingContribution * manipulationContribution * vitality;
}
```

---

### 3. Speed - Movement Rate

**Purpose**: Travel time, dodge effectiveness, combat retreat speed

**Formula**:
```csharp
baseSpeed = f(Moving, bodyComposition)  // Complex body calculation
vitalityFactor = 0.5 + (Vitality * 0.5)  // At 0 vitality: 50% speed
effectiveEncumbrance = encumbrance / (0.5 + Strength * 0.5)  // Strong = load feels lighter
encumbrancePenalty = max(0, (effectiveEncumbrance - 0.5) * 0.8)
Speed = baseSpeed * vitalityFactor * (1 - encumbrancePenalty)
```

**Dependencies**: Vitality, Strength

**Context Factors**: Encumbrance (from inventory weight)

**Key Insight**: Strong person carrying 30kg feels it less than weak person. With Strength 1.0 and 60% encumbrance, effective encumbrance is 60%. With Strength 0.5 and 60% encumbrance, effective encumbrance is 80%.

**Used for**:
- Travel time calculation (`Environments/TravelProcessor.cs`)
- Dodge effectiveness (`Combat/DefensiveActions.cs`)
- Combat movement (`Actions/CombatRunner.cs`)

**Example Usage**:
```csharp
// From Environments/TravelProcessor.cs
var context = new AbilityContext
{
    EncumbrancePct = (inventory != null && inventory.MaxWeightKg > 0)
        ? inventory.CurrentWeightKg / inventory.MaxWeightKg : 0
};
double speed = actor.GetSpeed(context);
double speedMultiplier = 1.0 / Math.Max(0.1, speed);
int baseTime = (int)Math.Ceiling(location.BaseTraversalMinutes * (1 + multiplier) * speedMultiplier);
```

---

### 4. Perception - Awareness

**Purpose**: Foraging yield, hunting detection, spotting dangers

**Formula**:
```csharp
sightEffectiveness = HasLightSource ? 1.0 : (1.0 - DarknessLevel)
baseSight = Sight * sightEffectiveness

// In darkness, hearing becomes more important
if (DarknessLevel > 0 && !HasLightSource):
    sightWeight = 0.5 * (1 - DarknessLevel)
    hearingWeight = 1 - sightWeight
else:
    sightWeight = 0.5
    hearingWeight = 0.5

basePerception = (baseSight * sightWeight) + (Hearing * hearingWeight)
vitalityFactor = 0.7 + (Vitality * 0.3)
Perception = basePerception * Consciousness * vitalityFactor  // Consciousness double-dips!
```

**Dependencies**: Vitality, Consciousness (direct)

**Context Factors**: Darkness, HasLightSource

**Key Insight**: Consciousness "double-dips" - it affects Vitality AND is applied directly. Being dazed (low Consciousness) severely tanks Perception. In pitch darkness without light, perception is purely hearing-based.

**Used for**:
- Foraging yield (`Actions/Expeditions/WorkStrategies/ForageStrategy.cs`)
- Hunt detection chance (`Actions/HuntRunner.cs`)
- Spotting game/dangers

**Example Usage**:
```csharp
// From Actions/Expeditions/WorkStrategies/ForageStrategy.cs
var abilityContext = AbilityContext.FromFullContext(
    ctx.player, ctx.Inventory, location, ctx.GameTime.Hour);
double perception = ctx.player.GetPerception(abilityContext);

// Apply perception as a direct multiplier
if (perception < 1.0)
{
    found.ApplyMultiplier(perception);

    if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
        GameDisplay.AddWarning(ctx, "The darkness limits what you can find.");
    else if (perception < 0.7)
        GameDisplay.AddWarning(ctx, "Your foggy senses cause you to miss some resources.");
}
```

---

### 5. Dexterity - Fine Motor Control

**Purpose**: Fire-starting, crafting, butchering, traps

**Formula**:
```csharp
baseDexterity = Manipulation
darknessPenalty = HasLightSource ? 0 : DarknessLevel * 0.5  // Up to 50% penalty
wetnessPenalty = max(0, (WetnessPct - 0.3) * 0.3)  // Starts at 30% wetness, up to 21% penalty
steadiness = 0.7 + (Vitality * 0.3)  // Dying = shaky hands
Dexterity = baseDexterity * (1 - darknessPenalty) * (1 - wetnessPenalty) * steadiness
```

**Dependencies**: Vitality (for steadiness)

**Context Factors**: Darkness, Wetness, HasLightSource

**Key Insight**: Combines multiple factors that affect fine motor control:
- Can't see what you're doing (darkness)
- Slippery grip (wetness)
- Shaky hands (low vitality)

**Used for**:
- Fire-starting chance (`Actions/Handlers/FireHandler.cs`)
- Crafting time (`Actions/CraftingRunner.cs`)
- Butchering yield (`Actions/Expeditions/WorkStrategies/ButcherStrategy.cs`)
- Trap setting/checking (`Actions/Expeditions/WorkStrategies/TrapStrategy.cs`)

**Example Usage**:
```csharp
// From Actions/Handlers/FireHandler.cs
var abilityContext = AbilityContext.FromFullContext(
    ctx.player, ctx.Inventory, ctx.CurrentLocation, ctx.GameTime.Hour);
double dexterity = ctx.player.GetDexterity(abilityContext);

// Dexterity penalty (up to -50% at dexterity 0)
double dexterityPenalty = (1.0 - dexterity) * 0.5;
chance -= dexterityPenalty;
```

---

### 6. ColdResistance - Temperature Tolerance

**Purpose**: Survival simulation temperature calculations

**Formula**:
```csharp
ColdResistance = 0.35 + tanh(BodyFatKG / 6) * 0.65
```

**Dependencies**: None - pure body composition

**Context Factors**: None

**Key Insight**: Diminishing returns on fat. First 6kg provides most benefit, additional fat helps less. Minimum resistance is 0.35 even with no body fat.

**Used by**: `Survival/SurvivalProcessor.cs` for temperature calculations

---

## AbilityContext

The `AbilityContext` struct provides environmental and situational context for ability calculations.

### Properties

```csharp
public readonly struct AbilityContext
{
    public double EncumbrancePct { get; init; }  // 0-1, weight/maxWeight
    public double DarknessLevel { get; init; }   // 0-1, 0=daylight, 1=pitch black
    public double WetnessPct { get; init; }      // 0-1, from weather/water
    public bool HasLightSource { get; init; }    // Fire or torch negates darkness
}
```

### Factory Methods

**1. Default** - No context penalties
```csharp
var context = AbilityContext.Default;
// Use for: Backward compatibility, tests, or when context doesn't matter
```

**2. FromInventory** - Encumbrance only
```csharp
var context = AbilityContext.FromInventory(inventory);
// Use for: Speed calculations where only weight matters
// Provides: EncumbrancePct
// Example: Travel time calculations
```

**3. FromActorAndInventory** - Encumbrance + Wetness
```csharp
var context = AbilityContext.FromActorAndInventory(actor, inventory);
// Use for: Speed or Dexterity when darkness doesn't apply
// Provides: EncumbrancePct, WetnessPct
// Example: Combat dodge where you're outdoors in daylight but might be wet
```

**4. FromFullContext** - All context factors
```csharp
var context = AbilityContext.FromFullContext(actor, inventory, location, hourOfDay);
// Use for: Perception or Dexterity calculations where darkness matters
// Provides: EncumbrancePct, WetnessPct, DarknessLevel, HasLightSource
// Example: Fire-starting, foraging, crafting, butchering
```

### Darkness Calculation

Darkness is automatically calculated based on location and time:

| Condition | Darkness Level |
|-----------|---------------|
| Dark location (cave) | 1.0 (always) |
| Night (< 5 or >= 21 hours) | 1.0 |
| Dawn (5-6 hours) | 0.5 |
| Dusk (20-21 hours) | 0.6 |
| Evening (17-20 hours) | 0.2 |
| Daytime (6-17 hours) | 0.0 |

Light sources (active fire or lit torch) negate all darkness penalties.

---

## Usage Patterns

### Pattern 1: Context-Aware Ability Query

**Correct approach** for performance calculations:

```csharp
// Full context for Dexterity/Perception
var context = AbilityContext.FromFullContext(
    ctx.player, ctx.Inventory, ctx.CurrentLocation, ctx.GameTime.Hour);
double dexterity = ctx.player.GetDexterity(context);
double craftingTime = baseTime * (1.0 / dexterity);
```

### Pattern 2: Threshold Check (Use Capacity Instead)

**Correct approach** for availability/impairment checks:

```csharp
// Checking if player CAN do something
var capacities = ctx.player.GetCapacities();
if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
{
    // Player is incapacitated - can't perform action at all
    return;
}
```

### Pattern 3: Performance Calculation

**WRONG** - Using capacity for performance:
```csharp
var capacities = ctx.player.GetCapacities();
double yield = baseYield * capacities.Manipulation;  // Ignores wetness, darkness, vitality!
```

**RIGHT** - Using ability with context:
```csharp
var context = AbilityContext.FromFullContext(
    ctx.player, ctx.Inventory, location, ctx.GameTime.Hour);
double dexterity = ctx.player.GetDexterity(context);
double yield = baseYield * dexterity;  // Accounts for all factors
```

---

## Decision Tree: Capacity or Ability?

```
Question: What are you trying to do?
│
├─ Check if action is POSSIBLE?
│  │  (Can the player do this at all?)
│  └─ USE CAPACITY
│     Examples:
│     - Can player move? (Moving > 0.3)
│     - Is player unconscious? (Consciousness < 0.3)
│     - Can player manipulate objects? (Manipulation > 0.5)
│
└─ Calculate HOW WELL action is performed?
   │  (What's the effectiveness/speed/yield?)
   └─ USE ABILITY
      Examples:
      - How fast can player travel? → Speed
      - What's the foraging yield? → Perception
      - Fire-starting success chance? → Dexterity
      - Combat damage? → Strength
```

### Examples by System

| System | Capacity Use | Ability Use |
|--------|--------------|-------------|
| **Travel** | Can move at all? (`Moving > 0.3`) | How fast? (`Speed` with encumbrance) |
| **Combat** | Can dodge? (`Moving > 0.3`) | Dodge success? (`Speed` with encumbrance) |
| **Foraging** | Can search? (`Consciousness > 0.3`) | What yield? (`Perception` with darkness) |
| **Fire** | Can manipulate? (`Manipulation > 0.5`) | Success chance? (`Dexterity` with darkness/wetness) |
| **Crafting** | Can work? (`Consciousness > 0.3`) | How long? (`Dexterity` with darkness/wetness) |

---

## Integration Points

### Survival System
- **Speed** determines travel time between locations
- **ColdResistance** affects temperature balance

### Combat System
- **Strength** determines melee damage
- **Speed** determines dodge success and retreat effectiveness
- Capacities used for availability gates (can the player dodge at all?)

### Work Strategies
- **Perception** affects foraging yield, hunting detection
- **Dexterity** affects butchering yield, trap effectiveness
- Time impairments check capacities first (can work?), then apply ability multipliers

### Fire System
- **Dexterity** determines fire-starting success chance
- Darkness, wetness, and manipulation all contribute via Dexterity

### Crafting System
- **Dexterity** determines crafting time penalty
- Lower dexterity = longer crafting time

---

## Implementation Files

**Core Implementation**:
- `Bodies/AbilityCalculator.cs` (323 lines) - All ability calculation logic
- `Bodies/AbilityContext.cs` (129 lines) - Context struct and factory methods
- `Actors/Actor.cs` - Ability access methods (`GetSpeed`, `GetPerception`, `GetDexterity`)

**Tests**:
- `text_survival.Tests/Bodies/AbilityCalculatorTests.cs` (258 lines) - Comprehensive ability tests

**Usage Examples**:
- `Environments/TravelProcessor.cs` - Speed with encumbrance
- `Combat/DefensiveActions.cs` - Speed for dodge
- `Actions/Expeditions/WorkStrategies/ForageStrategy.cs` - Perception with darkness
- `Actions/Handlers/FireHandler.cs` - Dexterity with darkness and wetness
- `Actions/CraftingRunner.cs` - Dexterity for crafting time
- `Actions/Expeditions/WorkStrategies/ButcherStrategy.cs` - Dexterity for yield
- `Actions/Expeditions/WorkStrategies/TrapStrategy.cs` - Dexterity for trap work
