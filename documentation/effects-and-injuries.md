# Effects and Injuries System

*Created: 2024-11*
*Last Updated: 2025-12-20*

How damage, healing, and status effects work together.

---

## Body Parts vs Effects

Two distinct systems handle harm to the player:

### Body Parts = Structural Damage

Physical tissue condition tracked at 0-1 per body region. See [body-and-damage.md](body-and-damage.md) for full details.

- **Entry point:** `Body.Damage(DamageInfo)`
- **What it tracks:** Skin, muscle, bone, organ condition
- **Affects:** Capacities (Moving, Manipulation, Consciousness, etc.)
- **Healing:** Automatic via `SurvivalProcessor.ProcessRegeneration()` when well-fed, hydrated, rested

**Example:** Wolf bite damages leg muscle to 60% condition, reducing Moving capacity.

### Effects = Ongoing Processes

Status conditions that tick over time. Not structural damage.

- **Entry point:** `EffectRegistry.AddEffect(effect)`
- **What it tracks:** Severity (0-1), duration, progression
- **Affects:** Capacities, survival stats, Blood condition
- **Resolution:** Natural decay or treatment

**Example:** Bleeding drains Blood until treated. Hypothermia reduces consciousness while cold persists.

### The Distinction

| Aspect | Body Parts | Effects |
|--------|-----------|---------|
| What | Tissue damage | Processes/conditions |
| Persists | Until healed | Until resolved |
| Example | "Leg at 60%" | "Bleeding" |
| Entry point | `Body.Damage()` | `EffectRegistry.AddEffect()` |

**Key insight:** A wolf bite causes BOTH:
1. Body damage (structural injury to leg)
2. Bleeding effect (ongoing blood loss process)

Stopping the bleeding doesn't heal the leg. Healing the leg doesn't stop the bleeding.

---

## Effect System

### EffectFactory

Static factory methods for creating effects. No fluent builders.

**Location:** `Effects/EffectFactory.cs`

**Available Effects:**

| Effect | Source | Description |
|--------|--------|-------------|
| `Cold(degreesPerHour, durationMinutes)` | environment | Temperature drop |
| `Hyperthermia(severity)` | temperature | Overheating, reduces consciousness |
| `Sweating(severity)` | temperature | Hydration drain |
| `Shivering(intensity)` | temperature | Generates warmth, reduces manipulation |
| `Hypothermia(severity)` | temperature | Dangerous cold, reduces most capacities |
| `Frostbite(severity)` | temperature | Extremity damage |
| `Frostbite(bodyPart, severity)` | temperature | Targeted frostbite |
| `SprainedAnkle(severity)` | injury | Joint condition, reduces moving |
| `Fear(severity)` | fear | Shaking hands, reduces manipulation |
| `Bleeding(severity)` | wound | Blood loss via DamageType.Bleed to Blood |

**Usage:**
```csharp
var effect = EffectFactory.Bleeding(0.5);
player.EffectRegistry.AddEffect(effect);
```

### Effect Properties

Each effect has:

- **EffectKind** — Type name ("Bleeding", "Hypothermia")
- **Source** — What caused it ("wound", "temperature")
- **Severity** — 0-1 scale, affects intensity
- **HourlySeverityChange** — Natural decay rate (negative = improving)
- **RequiresTreatment** — If true, severity decays to floor (0.05) instead of 0
- **CapacityModifiers** — Reduce Moving, Manipulation, etc.
- **StatsDelta** — Affect hydration, temperature per tick
- **ThresholdMessages** — Messages when crossing severity thresholds

### RequiresTreatment Behavior

Effects with `RequiresTreatment = true` decay naturally but stop at 0.05 severity:

```
1.0 → 0.5 → 0.2 → 0.1 → 0.05 (floor, requires treatment to clear)
```

Without treatment, the effect lingers at minimal severity. Treatment calls `RemoveEffectsByKind()` to fully clear.

---

## EffectRegistry

Per-actor container for active effects.

**Location:** `Effects/EffectRegistry.cs`

### Core Methods

```csharp
// Add effect (handles stacking)
player.EffectRegistry.AddEffect(effect);

// Remove specific effect
player.EffectRegistry.RemoveEffect(effect);

// Remove all of a kind
player.EffectRegistry.RemoveEffectsByKind("Bleeding");

// Query
List<Effect> all = player.EffectRegistry.GetAll();
List<Effect> bleeds = player.EffectRegistry.GetEffectsByKind("Bleeding");
```

### Stacking Behavior

Controlled by `CanHaveMultiple` property:

- **CanHaveMultiple = false (default):** Adding same effect kind updates severity to higher value
- **CanHaveMultiple = true:** Multiple instances can exist (e.g., multiple bleeding wounds)

### Update Cycle

Called automatically by `Actor.Update()`:

1. Each effect's severity advances based on `HourlySeverityChange`
2. Effects at severity 0 (or at floor if RequiresTreatment) are cleaned up
3. Threshold messages fire when crossing severity bands

---

## Auto-Triggered Effects

Some effects trigger automatically from damage.

### Bleeding from Sharp/Pierce Damage

When sharp or pierce damage breaks skin, bleeding triggers automatically.

**Location:** `Bodies/DamageCalculator.cs`

```csharp
// After damage applied
if ((damageInfo.Type == DamageType.Sharp || damageInfo.Type == DamageType.Pierce)
    && result.TissuesDamaged.Any(t => t.TissueName == "Skin" && t.DamageTaken > 0.05))
{
    double bleedSeverity = Math.Clamp(result.TotalDamageDealt * 0.05, 0.1, 0.6);
    result.TriggeredEffect = EffectFactory.Bleeding(bleedSeverity);
}
```

**Severity calculation:**
- 15 damage mauling → 0.6 severity → ~2 hours to reach floor
- 5 damage cut → 0.25 severity → ~45 min to reach floor

### Applying Triggered Effects

Event handlers must apply triggered effects from damage results:

```csharp
if (outcome.NewDamage is not null)
{
    var dmgResult = ctx.player.Body.Damage(outcome.NewDamage);
    if (dmgResult.TriggeredEffect != null)
        ctx.player.EffectRegistry.AddEffect(dmgResult.TriggeredEffect);
}
```

---

## Blood System

Blood is a systemic body component that extends `Tissue`. Unlike organs which contribute capacities additively, Blood multiplies BloodPumping.

**Location:** `Bodies/Blood.cs`

### Properties

- **TotalVolumeMl:** 5000ml (normal blood volume)
- **FatalThreshold:** 0.50 (death occurs at 50% blood loss)
- **Condition:** 0-1 scale (1.0 = full blood volume)

### How Blood Works

1. **Bleeding Effect** deals `DamageType.Bleed` damage to Blood at 3000ml/hour at full severity
2. **Blood.Condition** decreases as damage is taken
3. **BloodPumping capacity** is multiplied by Blood.Condition
4. **Cascade formula** kicks in: at BloodPumping < 1.0, Consciousness/Moving/Manipulation decay linearly toward zero at 50%
5. **Death** occurs when BloodPumping reaches 50% (Consciousness hits zero)

### Damage Math

| Severity | ml/hour | Time to death (50% loss) |
|----------|---------|--------------------------|
| 1.0 (arterial) | 3000 | ~50 min |
| 0.6 (major) | 1800 | ~83 min |
| 0.3 (moderate) | 900 | ~2.8 hours |
| 0.1 (minor) | 300 | ~8 hours |

Severity decays at -0.1/hour, so minor wounds stabilize before death.

### Blood Regeneration

Blood regenerates slowly when:
- Well-fed (calories > 10%)
- Hydrated (hydration > 10%)
- Rested (energy < 50%)

Regeneration rate is half of normal tissue healing.

### Protection

Blood has constant protection (not condition-scaled) so bleeding doesn't accelerate:
- `DamageType.Bleed` → Toughness (5000)
- `DamageType.Internal` → Toughness (5000)
- Physical damage types → Immune (double.MaxValue)

---

## Modeling Notes

**Sprained Ankle:** Kept as Effect (joint condition) rather than body damage because it's a ligament/joint issue, not tissue damage.

---

## Related Files

- `Effects/Effect.cs` — Base effect class
- `Effects/EffectFactory.cs` — Static factory methods
- `Effects/EffectRegistry.cs` — Per-actor effect container
- `Bodies/Body.cs` — Body and damage entry point
- `Bodies/Blood.cs` — Blood system (extends Tissue)
- `Bodies/DamageCalculator.cs` — Damage processing, bleeding trigger
- `Bodies/CapacityCalculator.cs` — Capacity calculation, Blood multiplier, cascade effects
- [body-and-damage.md](body-and-damage.md) — Body hierarchy and damage system details
