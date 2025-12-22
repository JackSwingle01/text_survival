# Event System Developer Guide

*Created: 2025-12*
*Last Updated: 2025-12-20*

How to create events, use tensions, spawn encounters, and integrate with game systems.

---

## Quick Reference

### Creating a Basic Event

```csharp
private static GameEvent MyEvent(GameContext ctx)
{
    var evt = new GameEvent("Event Name", "Description of what's happening.");
    evt.BaseWeight = 1.0;
    evt.RequiredConditions.Add(EventCondition.Working);

    var choice = new EventChoice("Choice Label", "What happens when chosen.",
        [
            new EventResult("Outcome message.", weight: 0.6)
            { TimeAddedMinutes = 10 },
            new EventResult("Alternative outcome.", weight: 0.4)
            { TimeAddedMinutes = 5, RewardPool = RewardPool.BasicSupplies }
        ]);

    evt.AddChoice(choice);
    return evt;
}
```

### Registering Events

Add event factories to `GameEventRegistry.AllEventFactories`:

```csharp
public static List<Func<GameContext, GameEvent>> AllEventFactories { get; } =
[
    // ... existing events ...
    MyEvent,
    MyOtherEvent,
];
```

---

## Event Structure

### GameEvent

```csharp
public class GameEvent(string name, string description)
{
    public string Name;
    public string Description;
    public readonly List<EventCondition> RequiredConditions = [];
    public double BaseWeight = 1.0;
    public readonly Dictionary<EventCondition, double> WeightModifiers = [];
}
```

- **RequiredConditions** — all must be true for event to be eligible
- **BaseWeight** — selection weight when multiple events are eligible
- **WeightModifiers** — multiply weight when conditions are met

### EventChoice

```csharp
public EventChoice(string label, string description, List<EventResult> results,
    List<EventCondition>? conditions = null)
```

- **label** — shown in the choice menu
- **description** — displayed when choice is selected
- **results** — weighted outcomes (one is randomly selected)
- **conditions** — required conditions for this choice to appear

### EventResult

```csharp
public class EventResult(string message, double weight = 1)
{
    // Core
    public string Message;
    public double Weight;
    public int TimeAddedMinutes;
    public bool AbortsExpedition;

    // Effects and damage
    public Effect? NewEffect;
    public DamageInfo? NewDamage;
    public Effect? GrantBuff;

    // Resources
    public RewardPool RewardPool = RewardPool.None;
    public ResourceCost? Cost;

    // Tensions
    public TensionCreation? CreatesTension;
    public string? ResolvesTension;
    public (string type, double amount)? EscalateTension;

    // Equipment
    public ToolDamage? DamageTool;
    public ToolType? BreakTool;
    public ClothingDamage? DamageClothing;

    // Chaining
    public EncounterConfig? SpawnEncounter;
    public Func<GameContext, GameEvent>? ChainEvent;
}
```

---

## Working with Tensions

### Creating a Tension

Use `TensionCreation` in an event outcome:

```csharp
new EventResult("You sense something following you.", weight: 0.3)
{
    CreatesTension = new TensionCreation("Stalked", 0.3, AnimalType: "Wolf"),
    NewEffect = EffectFactory.Fear(0.2)
}
```

### Available Tension Types

| Type | Factory Method | Decay/Hour | At Camp |
|------|---------------|------------|---------|
| Stalked | `ActiveTension.Stalked(severity, animalType?, location?)` | 0.05 | Yes |
| SmokeSpotted | `ActiveTension.SmokeSpotted(severity, direction?, source?)` | 0.03 | No |
| Infested | `ActiveTension.Infested(severity, location?)` | 0.0 | No |
| WoundUntreated | `ActiveTension.WoundUntreated(severity, description?)` | 0.0 | No |
| ShelterWeakened | `ActiveTension.ShelterWeakened(severity, location?)` | 0.0 | No |
| FoodScentStrong | `ActiveTension.FoodScentStrong(severity)` | 0.10 | Yes |
| Hunted | `ActiveTension.Hunted(severity, animalType?)` | 0.02 | Yes |

### Escalating a Tension

```csharp
new EventResult("It's getting bolder.", weight: 0.3)
{
    TimeAddedMinutes = 8,
    EscalateTension = ("Stalked", 0.15)  // +0.15 severity
}
```

Negative values reduce tension:

```csharp
new EventResult("It backs off.", weight: 0.2)
{
    TimeAddedMinutes = 5,
    EscalateTension = ("Stalked", -0.1)  // -0.1 severity
}
```

### Resolving a Tension

```csharp
new EventResult("The confrontation is now.", weight: 1.0)
{
    ResolvesTension = "Stalked",
    SpawnEncounter = new EncounterConfig("Wolf", 20, 0.6)
}
```

### Checking Tensions in Events

Use EventConditions to require or weight by tension:

```csharp
// Require tension
evt.RequiredConditions.Add(EventCondition.Stalked);

// Require high severity
evt.RequiredConditions.Add(EventCondition.StalkedHigh);      // > 0.5
evt.RequiredConditions.Add(EventCondition.StalkedCritical);  // > 0.7

// Weight modifier
evt.WeightModifiers.Add(EventCondition.Stalked, 2.0);  // 2x when stalked
```

### Reading Tension Details

```csharp
var stalkedTension = ctx.Tensions.GetTension("Stalked");
var predator = stalkedTension?.AnimalType ?? "predator";
```

---

## Spawning Encounters

Events can trigger predator encounters using `SpawnEncounter`:

```csharp
new EventResult("It attacks!", weight: 0.15)
{
    TimeAddedMinutes = 5,
    SpawnEncounter = new EncounterConfig(
        AnimalType: "Wolf",
        InitialDistance: 15,     // meters
        InitialBoldness: 0.7     // 0-1, higher = more aggressive
    )
}
```

### EncounterConfig Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| AnimalType | string | "Wolf", "Bear", "Boar", etc. |
| InitialDistance | double | Starting distance in meters |
| InitialBoldness | double | 0-1 aggression level |
| Modifiers | List<string>? | Optional: "Wounded", "Hungry", etc. |

### Context-Aware Encounter Spawning

Pull tension data into the encounter:

```csharp
private static GameEvent Ambush(GameContext ctx)
{
    var stalkedTension = ctx.Tensions.GetTension("Stalked");
    var predator = stalkedTension?.AnimalType ?? "Wolf";

    var evt = new GameEvent("Ambush", $"The {predator.ToLower()} attacks!");
    evt.RequiredConditions.Add(EventCondition.StalkedCritical);

    var brace = new EventChoice("Brace Yourself", "No time to run.",
        [
            new EventResult("The predator attacks!", weight: 1.0)
            {
                ResolvesTension = "Stalked",
                SpawnEncounter = new EncounterConfig(predator, 5, 0.9)  // Close, very aggressive
            }
        ]);

    evt.AddChoice(brace);
    return evt;
}
```

---

## Equipment Damage

### Damaging Tools

Reduce durability by uses:

```csharp
new EventResult("Your knife chips on the ice.", weight: 0.2)
{
    DamageTool = new ToolDamage(ToolType.Knife, UsesLost: 3)
}
```

### Breaking Tools

Destroy a tool entirely:

```csharp
new EventResult("Your spear snaps!", weight: 0.1)
{
    BreakTool = ToolType.Spear
}
```

### Damaging Clothing

Reduce insulation:

```csharp
new EventResult("Your coat is torn.", weight: 0.15)
{
    DamageClothing = new ClothingDamage(EquipSlot.Chest, InsulationLoss: 0.1)
}
```

---

## Effects and Buffs

### Applying Negative Effects

```csharp
new EventResult("You're caught in the storm.", weight: 0.4)
{
    TimeAddedMinutes = 25,
    NewEffect = EffectFactory.Cold(-15, 45)  // -15 degrees/hour for 45 min
}
```

### Available Effects

| Effect | Factory | Description |
|--------|---------|-------------|
| Cold | `Cold(degreesPerHour, durationMinutes)` | Temperature drop |
| Hypothermia | `Hypothermia(severity)` | Dangerous cold |
| Frostbite | `Frostbite(severity)` | Extremity damage |
| SprainedAnkle | `SprainedAnkle(severity)` | Movement penalty |
| Fear | `Fear(severity)` | Manipulation penalty |
| Shaken | `Shaken(severity)` | Milder fear, fades faster |
| Bleeding | `Bleeding(severity)` | Blood loss |
| Sore | `Sore(severity, duration)` | Mild movement penalty |
| Paranoid | `Paranoid(severity)` | Longer-lasting fear |
| Exhausted | `Exhausted(severity, duration)` | Movement/manipulation penalty |
| Nauseous | `Nauseous(severity, duration)` | Digestion/consciousness penalty |
| Coughing | `Coughing(severity, duration)` | Breathing penalty |

### Applying Positive Buffs

Use `GrantBuff` for positive effects:

```csharp
new EventResult("You feel invigorated.", weight: 0.15)
{
    GrantBuff = EffectFactory.Focused(0.5, 60)  // +0.5 severity, 60 min
}
```

### Available Buffs

| Buff | Factory | Description |
|------|---------|-------------|
| Warmed | `Warmed(severity, duration)` | Temperature boost |
| Rested | `Rested(severity, duration)` | Capacity bonuses |
| Focused | `Focused(severity, duration)` | Manipulation/consciousness bonus |
| Hardened | `Hardened(severity, duration)` | General resilience |

---

## Resource Costs and Rewards

### Consuming Resources

```csharp
new EventResult("You use fuel to start an emergency fire.", weight: 0.5)
{
    TimeAddedMinutes = 15,
    Cost = new ResourceCost(ResourceType.Fuel, 2)
}
```

### Available Resource Types

- `ResourceType.Fuel` — sticks, logs
- `ResourceType.Tinder` — fire-starting material
- `ResourceType.Food` — any edible

### Granting Rewards

```csharp
new EventResult("You find supplies.", weight: 0.35)
{
    TimeAddedMinutes = 15,
    RewardPool = RewardPool.BasicSupplies
}
```

### Available Reward Pools

| Pool | Contents |
|------|----------|
| BasicSupplies | Sticks, tinder, berries, small logs |
| AbandonedCamp | Random tool + tinder/kindling |
| HiddenCache | Quality tool (possibly fire striker) + fuel |
| BasicMeat | Small amount of raw meat |
| LargeMeat | 2-3 meat portions |
| GameTrailDiscovery | Minor supplies |
| CraftingMaterials | Stone, bone, plant fiber |
| ScrapTool | Damaged but usable tool |
| WaterSource | Fresh water |
| TinderBundle | Just tinder |
| BoneHarvest | 1-3 bones |
| SmallGame | Small meat + possible bone |
| HideScrap | Piece of usable hide |

---

## Event Conditions

### Player State

| Condition | Check |
|-----------|-------|
| Injured | Any body part < 100% |
| Bleeding | Has bleeding effect |
| Slow | Moving capacity < 0.7 |
| HasFood | Inventory has any food |
| HasMeat | Has raw or cooked meat |
| HasFuel | Has any fuel |
| HasTinder | Has tinder |

### Activity State

| Condition | Check |
|-----------|-------|
| Working | On expedition, actively working |
| Traveling | On any expedition |
| AtCamp | At camp location |
| OnExpedition | Expedition active |
| Inside | In sheltered location |
| Outside | Not in shelter |

### Location

| Condition | Check |
|-----------|-------|
| InAnimalTerritory | Location has AnimalTerritoryFeature |
| HasPredators | Territory includes predators |
| NearFire | Active fire at location |
| HasShelter | Location has ShelterFeature |

### Weather

| Condition | Check |
|-----------|-------|
| IsSnowing | Light snow or blizzard |
| IsBlizzard | Blizzard specifically |
| IsRaining | Rainy weather |
| IsStormy | Storm weather |
| HighWind | Wind speed > 0.6 |
| IsClear | Clear weather |
| IsMisty | Misty/foggy |
| ExtremelyCold | Base temp < -25 |
| WeatherWorsening | Weather just got worse |

### Resource Thresholds

| Condition | Check |
|-----------|-------|
| HasFuelPlenty | Fuel weight >= 3kg |
| HasFoodPlenty | Food weight >= 1kg |
| LowOnFuel | Fuel weight <= 1kg |
| LowOnFood | Food weight <= 0.5kg |
| NoFuel | No fuel |
| NoFood | No food |

### Tension State

| Condition | Check |
|-----------|-------|
| Stalked | Has Stalked tension |
| StalkedHigh | Stalked severity > 0.5 |
| StalkedCritical | Stalked severity > 0.7 |
| SmokeSpotted | Has SmokeSpotted tension |
| Infested | Has Infested tension |
| WoundUntreated | Has WoundUntreated tension |
| ShelterWeakened | Has ShelterWeakened tension |
| FoodScentStrong | Has FoodScentStrong tension |
| Hunted | Has Hunted tension |

---

## Complete Example: Tension Arc Event

```csharp
private static GameEvent StalkerCircling(GameContext ctx)
{
    // Get tension details for context-aware text
    var stalkedTension = ctx.Tensions.GetTension("Stalked");
    var predator = stalkedTension?.AnimalType ?? "predator";

    var evt = new GameEvent("Stalker Circling",
        $"You catch movement in your peripheral vision. The {predator.ToLower()} is pacing you.");
    evt.BaseWeight = 1.5;
    evt.RequiredConditions.Add(EventCondition.Stalked);

    // Choice 1: Force confrontation now
    var confront = new EventChoice("Confront It Now",
        "Turn and face it. Better to fight on your terms.",
        [
            new EventResult("You spin to face it. The confrontation is now.", weight: 1.0)
            {
                TimeAddedMinutes = 5,
                ResolvesTension = "Stalked",
                SpawnEncounter = new EncounterConfig(predator, 20, stalkedTension?.Severity ?? 0.5)
            }
        ]);

    // Choice 2: Try to lose it
    var tryToLose = new EventChoice("Try to Lose It",
        "Double back, cross water, break your trail.",
        [
            new EventResult("You break your trail. It works.", weight: 0.35)
            { TimeAddedMinutes = 25, ResolvesTension = "Stalked" },
            new EventResult("It stays with you. You've wasted time.", weight: 0.35)
            { TimeAddedMinutes = 20, EscalateTension = ("Stalked", 0.2) },
            new EventResult("You get turned around trying to lose it.", weight: 0.20)
            { TimeAddedMinutes = 35, NewEffect = EffectFactory.Cold(-8, 30) },
            new EventResult("Your evasion leads you somewhere unexpected.", weight: 0.10)
            { TimeAddedMinutes = 30 }
        ]);

    // Choice 3: Keep moving
    var keepMoving = new EventChoice("Keep Moving, Stay Alert",
        "Maintain distance. Don't show weakness.",
        [
            new EventResult("You maintain distance. Exhausting but stable.", weight: 0.40)
            { TimeAddedMinutes = 10 },
            new EventResult("It's getting bolder.", weight: 0.30)
            { TimeAddedMinutes = 8, EscalateTension = ("Stalked", 0.15) },
            new EventResult("It backs off. Maybe lost interest.", weight: 0.20)
            { TimeAddedMinutes = 5, EscalateTension = ("Stalked", -0.1) },
            new EventResult("It commits.", weight: 0.10)
            {
                TimeAddedMinutes = 5,
                ResolvesTension = "Stalked",
                SpawnEncounter = new EncounterConfig(predator, 15, 0.6)
            }
        ]);

    // Choice 4: Return to camp
    var returnToCamp = new EventChoice("Return to Camp",
        "Head back now. Fire deters predators.",
        [
            new EventResult("You make it back. Fire deters it.", weight: 0.60)
            { ResolvesTension = "Stalked", AbortsExpedition = true },
            new EventResult("It follows to camp perimeter but won't approach fire.", weight: 0.25)
            { ResolvesTension = "Stalked", AbortsExpedition = true, NewEffect = EffectFactory.Fear(0.2) },
            new EventResult("It's bolder than you thought. Attacks before you reach safety.", weight: 0.15)
            {
                TimeAddedMinutes = 5,
                AbortsExpedition = true,
                ResolvesTension = "Stalked",
                SpawnEncounter = new EncounterConfig(predator, 10, 0.8)
            }
        ]);

    evt.AddChoice(confront);
    evt.AddChoice(tryToLose);
    evt.AddChoice(keepMoving);
    evt.AddChoice(returnToCamp);
    return evt;
}
```

---

## Related Files

- `Actions/GameEventRegistry.cs` — event factories and HandleOutcome
- `Actions/GameEvent.cs` — GameEvent, EventChoice, EventResult classes
- `Actions/EventTypes.cs` — TensionCreation, EncounterConfig, etc.
- `Actions/Tensions/ActiveTension.cs` — tension class with factory methods
- `Actions/Tensions/TensionRegistry.cs` — tension management
- `Actions/GameContext.cs` — EventCondition enum and Check() method
- `Effects/EffectFactory.cs` — effect creation methods
- `Items/RewardGenerator.cs` — reward pool generation

---

## Design Guidelines

### Events Should Create Decisions

Every event should present meaningful choices where reasonable players might choose differently. Avoid:
- Single obvious "correct" choice
- Choices with no meaningful difference
- Pure random punishment with no player agency

### Use Tensions for Narrative Arcs

Tensions allow events to build on each other. A single "wolf sighting" event is less interesting than a Stalker arc where tension escalates across multiple encounters until resolution.

### Connect to Existing Systems

Events should use existing systems rather than creating parallel mechanics:
- Use `SpawnEncounter` instead of simulating combat in event outcomes
- Use `Body.Damage()` patterns for injuries
- Use existing effects rather than inventing new stat modifiers

### Weight Modifiers Create Context

Use weight modifiers to make events more likely in appropriate contexts:

```csharp
evt.WeightModifiers.Add(EventCondition.HasMeat, 3.0);   // Predator events when carrying meat
evt.WeightModifiers.Add(EventCondition.Injured, 1.5);  // Accidents more likely when hurt
```

This makes the event pool feel responsive to player state without hard requirements.
