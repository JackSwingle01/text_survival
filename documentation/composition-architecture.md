# Actor Architecture

*Created: 2024-11*
*Last Updated: 2025-12-20*

How actors (Player, Animals) are structured using composition over inheritance.

---

## Overview

The game uses a flat hierarchy with composition for flexibility:

```
Actor (abstract base)
├── Player : Actor (has managers for hunting/stealth, skills)
└── Animal : Actor (has behavior, traits, activity)
```

**Key Files:**
- `Actors/Actor.cs` — Base class
- `Actors/Player/Player.cs` — Player implementation
- `Actors/Animals/Animal.cs` — Animal implementation

---

## Actor Base Class

All living entities share core components:

```csharp
public abstract class Actor
{
    public string Name;
    public Body Body { get; init; }
    public EffectRegistry EffectRegistry { get; init; }
    protected CombatManager combatManager { get; init; }

    // Combat interface - subclasses implement
    public abstract double AttackDamage { get; }
    public abstract double BlockChance { get; }
    public abstract string AttackName { get; }
    public abstract DamageType AttackType { get; }

    // State
    public bool IsEngaged { get; set; }
    public bool IsAlive => Vitality > 0;

    // Derived abilities (from Body + Effects)
    public double Strength { get; }
    public double Speed { get; }
    public double Vitality { get; }
    public double Perception { get; }
    public double ColdResistance { get; }
}
```

### What Actor Provides

- **Body** — Physical form, survival stats, damage tracking
- **EffectRegistry** — Active effects (bleeding, hypothermia, buffs)
- **CombatManager** — Attack/defend logic
- **Abilities** — Calculated from body state and effects

### Update Flow

```csharp
public virtual void Update(int minutes, SurvivalContext context)
{
    // 1. Process survival (temp, calories, hydration)
    var result = SurvivalProcessor.Process(Body, context, minutes);

    // 2. Tick effects and get their modifiers
    EffectRegistry.Update(minutes);
    result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
    result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());

    // 3. Apply all changes to body
    Body.ApplyResult(result);
}
```

---

## Player

Extends Actor with player-specific systems:

```csharp
public class Player : Actor
{
    public readonly StealthManager stealthManager;
    public readonly HuntingManager huntingManager;
    public readonly SkillRegistry Skills;  // Vestigial

    // Unarmed combat defaults (actual weapon passed to Attack)
    public override double AttackDamage => 2;
    public override string AttackName => "fists";
    public override DamageType AttackType => DamageType.Blunt;
}
```

### Player-Only Components

- **StealthManager** — Handles stalking calculations during hunts
- **HuntingManager** — Tracks current hunt state
- **SkillRegistry** — Skills (vestigial, see [skill-check-system.md](skill-check-system.md))

### Player Update

Player overrides `Update()` to store survival delta for UI display:

```csharp
public override void Update(int minutes, SurvivalContext context)
{
    var result = SurvivalProcessor.Process(Body, context, minutes);
    // ... effect processing ...

    // Store for UI trend arrows
    LastSurvivalDelta = result.StatsDelta;
    LastUpdateMinutes = minutes;

    Body.ApplyResult(result);
}
```

---

## Animal

Extends Actor with animal-specific behavior:

```csharp
public class Animal : Actor
{
    // Behavior type: Prey, Predator, Scavenger, DangerousPrey
    public AnimalBehaviorType BehaviorType { get; set; }

    // Size: Small, Medium, Large (affects weapon effectiveness)
    public AnimalSize Size { get; set; }

    // Awareness: Idle → Alert → Detected
    public AnimalState State { get; set; }

    // Hunting mechanics
    public double DistanceFromPlayer { get; set; }
    public int FailedStealthChecks { get; set; }
    public double EncounterBoldness { get; set; }

    // Individual traits
    public double SizeModifier { get; set; }  // 0.7-1.3
    public double Condition { get; set; }     // 0.3-1.0
    public double Nervousness { get; set; }   // 0.0-1.0
    public AnimalActivity CurrentActivity { get; }
}
```

### Behavior Types

| Type | On Detected |
|------|-------------|
| Prey | Flees immediately |
| Predator | Attacks (based on boldness) |
| Scavenger | Flees if outmatched |
| DangerousPrey | Fights back when cornered |

### Animal Traits

Each animal generates individual traits:
- **SizeModifier** — Body size relative to species average
- **Condition** — Health/fitness level
- **Nervousness** — Detection sensitivity
- **DistinguishingMarks** — "scarred flank", "limp", etc.

```csharp
// Generate unique traits for this individual
animal.GenerateTraits();

// Get player-facing description
string desc = animal.GetTraitDescription();
// "a lean doe with a scarred flank"
```

### Animal Activity

Animals cycle through activities that affect detection:

| Activity | Detection Modifier |
|----------|-------------------|
| Grazing | 0.7 (easier) |
| Moving | 1.2 (harder) |
| Resting | 0.9 (slightly easier) |
| Alert | 2.0 (very hard) |

Activities cycle automatically over time.

---

## Composition Pattern

### Why Composition?

```csharp
// Inheritance hell (avoided):
Actor → LivingActor → MortalActor → InventoriedActor → ...

// Composition (used):
Actor has Body, EffectRegistry, CombatManager
Player adds StealthManager, HuntingManager, Skills
Animal adds Behavior, Traits, Activity
```

**Benefits:**
- Flat hierarchy, easy to trace
- Components can be tested independently
- Add capabilities without changing base class

### Key Components

| Component | Owner | Purpose |
|-----------|-------|---------|
| Body | Actor | Physical form, stats, damage |
| EffectRegistry | Actor | Active effects, modifiers |
| CombatManager | Actor | Attack/defense calculations |
| StealthManager | Player | Stalking calculations |
| HuntingManager | Player | Hunt state tracking |
| SkillRegistry | Player | Skills (vestigial) |

---

## Creating Actors

### Creating Player

```csharp
var player = new Player();
// Uses Body.BaselinePlayerStats for body creation
```

### Creating Animals

Animals are created via `NPCFactory`:

```csharp
var deer = NPCFactory.CreateDeer();
deer.GenerateTraits();  // Make it unique
```

Factory handles species-specific stats (weight, speed, behavior type, attack damage, etc.).

---

**Related Files:**
- [body-and-damage.md](body-and-damage.md) — Body system details
- [effects-and-injuries.md](effects-and-injuries.md) — Effect system
- [skill-check-system.md](skill-check-system.md) — Skills (vestigial)
