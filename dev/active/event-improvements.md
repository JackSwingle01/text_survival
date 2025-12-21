I'll create both documents. Let me start with the technical specification.

---

# Document 1: Event System Enhancement Specification

## Overview

This specification extends the existing event system to support narrative arcs, world-building, and deeper integration with game systems. The core principle: events should connect to existing mechanics, build the world, and chain into each other — not create parallel systems or exist as isolated incidents.

### Design Goals

1. Tension System — Unresolved narrative threads that persist across events, modify event weights, and resolve through player action or escalation
2. World Mutation — Events can add features to locations, discover new locations, and modify existing world state
3. System Integration — Events connect to predator encounters, tool durability, shelter features, and survival stats rather than rolling damage in isolation
4. Chained Outcomes — Event outcomes can trigger follow-up events, spawn encounters, or create tensions that lead to future events
5. Resource Specificity — Target specific tools, clothing, and infrastructure rather than generic resource pools

---

## Part 1: Tension System

### 1.1 ActiveTension Class

A tension represents an unresolved narrative thread that modifies the event landscape until resolved.

```
ActiveTension
├── Type: string              // Identifier: "Stalked", "SmokeSpotted", "Infested", "WoundUntreated"
├── Severity: double          // 0.0-1.0, affects weight modifiers and resolution drama
├── CreatedAt: DateTime       // When tension began (for time-based escalation checks)
├── RelevantLocation: Location?   // Where it applies (null = global)
├── SourceLocation: Location?     // Where it originated (for "return to investigate" scenarios)
├── Details: string?          // Flavor text: "Wolf", "Southwest ridge", "Left arm laceration"
└── Data: Dictionary<string, object>?  // Flexible storage for tension-specific data
```

### 1.2 GameContext Additions

```
GameContext
├── Tensions: List<ActiveTension>
├── HasTension(string type): bool
├── GetTension(string type): ActiveTension?
├── AddTension(ActiveTension tension): void
├── ResolveTension(string type): void
├── EscalateTension(string type, double amount): void
```

Tension Location Scoping:
- If `RelevantLocation` is null, tension applies everywhere
- If `RelevantLocation` is set, tension only affects events when `CurrentLocation == RelevantLocation`
- Example: "Infested" tension only applies at camp; "Stalked" applies during current expedition

### 1.3 Tension-Based Event Conditions

New EventCondition entries:

```
// Tension presence
Stalked              // Something is following the player
SmokeSpotted         // Distant smoke has been observed
Infested             // Vermin in camp storage
WoundUntreated       // Injury needs cleaning
ShelterWeakened      // Structure is compromised
FoodScentStrong      // Cooking/butchering attracted attention
Hunted               // Active predator pursuit (high severity Stalked)
```

These check `ctx.HasTension(type)` and optionally severity thresholds.

### 1.4 Tension Lifecycle

Creation: Event outcomes can create tensions via `CreatesTension` field

Escalation: Event outcomes can increase severity via `EscalateTension` field. At certain thresholds, more dramatic events become eligible.

Resolution: Tensions resolve through:
- Event outcome with `ResolvesTension` field
- Player action (clean wound, return to camp, confront predator)
- Automatic conditions (fire deters stalker, food runs out so infestation ends)
- Time-based expiration (smoke dissipates after 3 days if not investigated)

Intersection: Multiple active tensions compound pressure. Stalked + WoundUntreated + LowFuel creates desperate decision space.

---

## Part 2: Extended EventResult

### 2.1 New EventResult Fields

```
EventResult
├── [Existing fields]
│   ├── Message: string
│   ├── Weight: double
│   ├── TimeAddedMinutes: int
│   ├── AbortsExpedition: bool
│   ├── NewEffect: Effect?
│   ├── NewDamage: DamageInfo?
│   ├── RewardPool: RewardPool
│   ├── Cost: ResourceCost?
│
├── [Tension fields]
│   ├── CreatesTension: ActiveTension?
│   ├── ResolvesTension: string?          // Tension type to remove
│   ├── EscalateTension: (string type, double amount)?
│
├── [Chaining fields]
│   ├── ChainEvent: Func<GameContext, GameEvent>?   // Immediately triggers follow-up event
│   ├── SpawnEncounter: EncounterConfig?            // Triggers predator/animal encounter
│
├── [World mutation fields]
│   ├── AddFeature: (FeatureType type, FeatureConfig config)?  // Add feature to current location
│   ├── ModifyFeature: (FeatureType type, FeatureModification mod)?  // Change existing feature
│   ├── DiscoverLocation: LocationTemplate?         // Add new location to zone graph
│   ├── RemoveFeature: FeatureType?                 // Destroy feature (shelter collapse, etc.)
│
├── [Equipment targeting fields]
│   ├── DamageTool: (ToolType type, double durabilityLoss)?    // Damage specific tool
│   ├── BreakTool: ToolType?                        // Destroy specific tool
│   ├── DamageClothing: (ClothingSlot slot, double insulationLoss)?
│
├── [Player state fields]
│   ├── GrantBuff: Effect?                          // Positive effect for surviving hardship
```

### 2.2 Supporting Types

EncounterConfig:
```
EncounterConfig
├── AnimalType: string          // "Wolf", "Bear", "Boar"
├── InitialDistance: double     // Starting distance in encounter
├── InitialBoldness: double     // Starting boldness (0-1)
├── Modifiers: List<string>     // "Wounded", "Hungry", "Protecting_Young"
```

LocationTemplate:
```
LocationTemplate
├── NamePattern: string         // "Smoke Source", "Frozen Creek", "{Details} Location"
├── TravelTimeFromCurrent: int  // Minutes to reach
├── Direction: string?          // Flavor: "Southwest", "Uphill"
├── PotentialFeatures: List<FeatureType>  // What might be there when explored
├── RevealedBy: string?         // Tension type that revealed it
```

FeatureConfig:
```
FeatureConfig
├── Type: FeatureType           // Shelter, Forage, Water, AnimalTerritory, etc.
├── Quality: double             // 0-1, affects feature effectiveness
├── InitialAbundance: double?   // For depletable features
├── Metadata: Dictionary<string, object>?
```

FeatureModification:
```
FeatureModification
├── DepleteAmount: double?      // Reduce abundance
├── RestoreAmount: double?      // Increase abundance
├── QualityChange: double?      // Modify quality
├── Destroy: bool               // Remove entirely
```

---

## Part 3: Extended HandleOutcome

### 3.1 Processing Order

HandleOutcome should process result fields in this order:

1. Time — Apply time cost, update game state
2. Resources — Deduct costs, apply rewards
3. Damage/Effects — Apply to player
4. Equipment — Damage/break tools, damage clothing
5. Tensions — Create, resolve, or escalate
6. World Mutation — Add/modify/remove features, discover locations
7. Chaining — Spawn encounters or chain events (these take over flow)

### 3.2 Pseudo-Implementation

```
HandleOutcome(ctx, outcome):
    
    // 1. Time
    if outcome.TimeAddedMinutes > 0:
        DisplayTimeMessage(outcome.TimeAddedMinutes)
        UpdateProgress(ctx, outcome.TimeAddedMinutes)
    
    // 2. Message
    Display(outcome.Message)
    
    // 3. Resources
    if outcome.Cost:
        DeductResources(ctx.Inventory, outcome.Cost)
    if outcome.RewardPool != None:
        resources = RewardGenerator.Generate(outcome.RewardPool)
        ctx.Inventory.Add(resources)
        DisplayRewards(resources)
    
    // 4. Damage/Effects
    if outcome.NewDamage:
        result = ctx.Player.Body.Damage(outcome.NewDamage)
        if result.TriggeredEffect:
            ctx.Player.EffectRegistry.AddEffect(result.TriggeredEffect)
    if outcome.NewEffect:
        ctx.Player.EffectRegistry.AddEffect(outcome.NewEffect)
    if outcome.GrantBuff:
        ctx.Player.EffectRegistry.AddEffect(outcome.GrantBuff)
    
    // 5. Equipment
    if outcome.DamageTool:
        tool = ctx.Inventory.GetTool(outcome.DamageTool.type)
        if tool:
            tool.Durability -= outcome.DamageTool.durabilityLoss
            Display($"Your {tool.Name} is damaged.")
    if outcome.BreakTool:
        tool = ctx.Inventory.RemoveTool(outcome.BreakTool)
        if tool:
            Display($"Your {tool.Name} breaks!")
            // Optionally add salvage materials
    if outcome.DamageClothing:
        clothing = ctx.Inventory.GetClothing(outcome.DamageClothing.slot)
        if clothing:
            clothing.Insulation -= outcome.DamageClothing.insulationLoss
            Display($"Your {clothing.Name} is damaged.")
    
    // 6. Tensions
    if outcome.CreatesTension:
        ctx.AddTension(outcome.CreatesTension)
        Display(GetTensionCreationMessage(outcome.CreatesTension))
    if outcome.ResolvesTension:
        ctx.ResolveTension(outcome.ResolvesTension)
        Display(GetTensionResolutionMessage(outcome.ResolvesTension))
    if outcome.EscalateTension:
        ctx.EscalateTension(outcome.EscalateTension.type, outcome.EscalateTension.amount)
    
    // 7. World Mutation
    if outcome.AddFeature:
        feature = FeatureFactory.Create(outcome.AddFeature.type, outcome.AddFeature.config)
        ctx.CurrentLocation.AddFeature(feature)
        Display($"This location now has {feature.Description}.")
    if outcome.ModifyFeature:
        feature = ctx.CurrentLocation.GetFeature(outcome.ModifyFeature.type)
        if feature:
            ApplyModification(feature, outcome.ModifyFeature.mod)
    if outcome.RemoveFeature:
        ctx.CurrentLocation.RemoveFeature(outcome.RemoveFeature)
        Display("The feature is destroyed.")
    if outcome.DiscoverLocation:
        newLocation = LocationFactory.CreateFromTemplate(outcome.DiscoverLocation, ctx.CurrentLocation)
        ctx.Zone.AddLocation(newLocation, connectedTo: ctx.CurrentLocation)
        Display($"You've discovered a new area: {newLocation.Name}")
    
    // 8. Chaining (takes over control flow)
    if outcome.SpawnEncounter:
        SpawnPredatorEncounter(ctx, outcome.SpawnEncounter)
        return  // Encounter handles its own flow
    if outcome.ChainEvent:
        chainedEvent = outcome.ChainEvent(ctx)
        HandleEvent(ctx, chainedEvent)  // Recursive
        return
    
    // 9. Expedition abort (checked last)
    if outcome.AbortsExpedition:
        ctx.Expedition?.Abort()
```

---

## Part 4: Event Weight Modifiers from Tensions

### 4.1 Tension Weight Integration

Events should be able to reference tensions in their weight modifiers:

```csharp
// In event factory
evt.WeightModifiers.Add(EventCondition.Stalked, 3.0);        // 3x more likely when stalked
evt.WeightModifiers.Add(EventCondition.WoundUntreated, 2.0); // 2x more likely with open wound
```

For severity-scaled modifiers, add new condition variants:
```
EventCondition.StalkedHigh      // Stalked tension severity > 0.6
EventCondition.InfestedSevere   // Infested tension severity > 0.8
```

Or extend the weight modifier system to accept severity thresholds:
```csharp
evt.TensionWeightModifiers.Add(("Stalked", minSeverity: 0.5), 2.5);
```

### 4.2 Example: Stalked Tension Affecting Events

```
Base event pool when Stalked:

StalkerCircling      — RequiredCondition: Stalked — BaseWeight: 2.0
StalkerRevealed      — RequiredCondition: Stalked, severity > 0.5 — BaseWeight: 1.5
Ambush               — RequiredCondition: StalkedHigh (>0.7) — BaseWeight: 1.0
StrangeSoundNearby   — WeightModifier: Stalked → 0.3x (less likely, already established)
NormalForaging       — WeightModifier: Stalked → 0.5x (tension suppresses mundane events)
```

The tension shifts the entire event landscape, making resolution/escalation events more prominent.

---

## Part 5: Integration Points

### 5.1 Predator Encounter System

`SpawnEncounter` should use the existing predator encounter system:

```
SpawnEncounter triggers:
1. Create Animal from AnimalType
2. Initialize PredatorEncounter with:
   - Distance = config.InitialDistance
   - Boldness = config.InitialBoldness + modifiers from player state
   - Apply config.Modifiers to animal
3. Transfer control to PredatorEncounterRunner
4. On encounter resolution, return to event system
```

Events should spawn encounters, not simulate them.

### 5.2 Feature System

Events can add features to locations:

```
AddFeature examples:
- ShelterFeature (quality 0.6, partial windbreak from improved natural shelter)
- WaterFeature (from discovered stream)
- ForageFeature (revealed berry patch)
- AnimalTerritoryFeature (game trail discovered)
```

Events can modify features:

```
ModifyFeature examples:
- Deplete ForageFeature (vermin ate through it)
- Damage ShelterFeature (storm weakened it)
- Restore ForageFeature (seasonal regrowth noted)
```

Events can destroy features:

```
RemoveFeature examples:
- ShelterFeature (collapse from snow load)
- HeatSourceFeature (fire extinguished by rain)
```

### 5.3 Tool/Equipment System

Events should target specific equipped or carried items:

```csharp
// Finding which tool to damage
if outcome.DamageTool.type == ToolType.Knife:
    tool = ctx.Inventory.Tools.FirstOrDefault(t => t.Type == ToolType.Knife)
```

Events can also require specific tools for choices:

```csharp
var digCarefully = new EventChoice("Dig Carefully", 
    "Probe with your knife.",
    results,
    conditions: [EventCondition.HasCuttingTool]);
```

### 5.4 Location Discovery

Events that discover locations should:

1. Create location from template
2. Connect it to current location (or specified location) in zone graph
3. Set appropriate travel time
4. Location starts as Unexplored until visited
5. Visiting reveals actual features

```
DiscoverLocation flow:
1. Event outcome has DiscoverLocation template
2. HandleOutcome creates Location from template
3. Zone.AddLocation(newLoc, connectedTo: currentLocation, travelTime: template.TravelTime)
4. Player sees: "You've spotted something to the southwest. A new area is now reachable."
5. Expedition menu shows new destination
6. First visit triggers exploration resolution (what's actually there)
```

---

## Part 6: Choice Availability System

### 6.1 Conditional Choices

Choices should only appear when their conditions are met:

```csharp
public EventChoice GetChoice(GameContext ctx)
{
    var availableChoices = new Choice<EventChoice>("What do you do?");
    var unavailableReasons = new List<(string label, string reason)>();
    
    foreach (var option in Choices.Options)
    {
        var choice = option.Value;
        var unmetConditions = choice.RequiredConditions
            .Where(c => !ctx.Check(c))
            .ToList();
        
        if (unmetConditions.Count == 0)
        {
            string label = choice.Cost != null
                ? $"{option.Label} [{choice.Cost.Amount} {choice.Cost.Type}]"
                : option.Label;
            availableChoices.AddOption(label, choice);
        }
        else
        {
            var reason = GetConditionReason(unmetConditions.First());
            unavailableReasons.Add((option.Label, reason));
        }
    }
    
    // Display unavailable options greyed out
    foreach (var (label, reason) in unavailableReasons)
    {
        DisplayGreyedOption($"{label} ({reason})");
    }
    
    return availableChoices.GetPlayerChoice();
}
```

### 6.2 Condition Reasons for Display

```csharp
private static string GetConditionReason(EventCondition condition) => condition switch
{
    EventCondition.HasFuel => "no fuel",
    EventCondition.HasTinder => "no tinder",
    EventCondition.HasFood => "no food",
    EventCondition.HasMeat => "no meat",
    EventCondition.HasCuttingTool => "no knife",
    EventCondition.HasFuelPlenty => "not enough fuel",
    EventCondition.FireBurning => "need fire",
    EventCondition.HasWater => "no water",
    EventCondition.HasPlantFiber => "no fiber",
    _ => "unavailable"
};
```

---

## Part 7: Camp vs Expedition Events

### 7.1 Context Separation

Some events only make sense in certain contexts:

Camp-only events:
- Fire behavior events (FireTrouble, EmbersScatter, ChokingSmoke)
- Storage events (VerminRaid, FoodSpoilage)
- Shelter events (ShelterLeak, StructuralFailure)
- Camp infrastructure events

Expedition-only events:
- Travel hazards (TreacherousFooting, ExposedPosition)
- Wildlife encounters (Stalker events, Ambush)
- Discovery events (NaturalShelter, WaterSource, OldCampsite)

Always available:
- Body events (MuscleCramp, FrozenFingers, GutWrench)
- Equipment events (EquipmentTrouble)
- Weather events (when conditions met)

### 7.2 Implementation

Events declare their context via required conditions:

```csharp
// Camp-only
evt.RequiredConditions.Add(EventCondition.AtCamp);
evt.RequiredConditions.Add(EventCondition.FireBurning);

// Expedition-only
evt.RequiredConditions.Add(EventCondition.OnExpedition);
evt.RequiredConditions.Add(EventCondition.Traveling);

// Working anywhere
evt.RequiredConditions.Add(EventCondition.Working);
```

New conditions needed:
```
AtCamp              // ctx.IsAtCamp
OnExpedition        // ctx.Expedition != null
NearFire            // Current location has active HeatSourceFeature
HasShelter          // Current location has ShelterFeature
```

---

## Part 8: New Resource Types

### 8.1 Extending ResourceType

```csharp
public enum ResourceType 
{ 
    Fuel,           // Sticks, logs
    Tinder,         // Fire-starting material
    Food,           // Any edible
    Meat,           // Specifically meat (for predator attraction)
    Water,          // Hydration
    PlantFiber,     // Cordage, binding
    Stone,          // Tools, weapons
    Bone,           // Tools, crafting
    Hide,           // Clothing, shelter
    Sinew           // Binding, bowstring
}
```

### 8.2 DeductResources Extension

```csharp
private static void DeductResources(Inventory inv, ResourceCost cost)
{
    for (int i = 0; i < cost.Amount; i++)
    {
        switch (cost.Type)
        {
            case ResourceType.Fuel:
                if (inv.Sticks.Count > 0) inv.TakeSmallestStick();
                else if (inv.Logs.Count > 0) inv.TakeSmallestLog();
                break;
            case ResourceType.PlantFiber:
                inv.TakePlantFiber(0.2); // ~200g per unit
                break;
            case ResourceType.Water:
                inv.Water = Math.Max(0, inv.Water - 0.25); // 250ml per unit
                break;
            // etc.
        }
    }
}
```

---

## Part 9: New Reward Pools

### 9.1 Suggested Pools

```csharp
public enum RewardPool
{
    None,
    
    // Existing
    BasicSupplies,      // Mixed small resources
    BasicMeat,          // Small meat yield
    LargeMeat,          // Substantial meat
    AbandonedCamp,      // Camp remnants (tools, fuel, materials)
    HiddenCache,        // Deliberately hidden supplies
    GameTrailDiscovery, // Information about hunting grounds
    
    // New
    CraftingMaterials,  // Stone, bone, fiber mix
    ScrapTool,          // Damaged but usable tool
    WaterSource,        // Water + location info
    Tinder,             // Fire-starting materials
    Bones,              // Crafting bones specifically
    SmallGame,          // Rabbit, bird — small calories + fur/feathers
    Feathers,           // Insulation crafting
    MedicinalPlants,    // Wound treatment materials
    Charcoal,           // Fire remnants, useful for various purposes
    Relics              // Rare finds — quality tools, unusual items
}
```

---

## Part 10: Effect Additions

### 10.1 New Effects for Events

Effects that events should be able to apply:

Physical:
```
Sore(severity, duration)        — Moving capacity reduced
Exhausted(severity, duration)   — Moving + Manipulation reduced, energy drain increased
Nauseous(severity, duration)    — Moving reduced, prevents eating, hydration drain
FrozenFingers(severity, duration) — Manipulation severely reduced
Coughing(severity, duration)    — Breathing capacity reduced
```

Positive (for surviving hardship):
```
Warmed(severity, duration)      — Cold resistance bonus
Fed(severity, duration)         — Reduced calorie drain, steady hands
Rested(severity, duration)      — Capacity bonuses
Focused(severity, duration)     — Manipulation bonus
IronGut(severity, duration)     — Raw food tolerance
Hardened(severity, duration)    — General resilience (cold, injury)
```

Psychological (with mechanical teeth):
```
Fear(severity)                  — Already exists, affects boldness calculations
Shaken(severity, duration)      — Minor capacity penalties, fades
Paranoid(severity, duration)    — Prevents rest, increases event vigilance
```

### 10.2 Effect Principle

Every effect must modify either:
- Survival stats (calorie drain, hydration drain, temperature)
- Capacities (Moving, Manipulation, Breathing, Consciousness)
- Specific mechanical interactions (can't eat, can't sleep, fire-starting penalty)

No pure flavor effects. If it doesn't change numbers, it's not an effect — it's narrative text.

---

## Summary: Implementation Priority

### Phase 1: Core Infrastructure
1. ActiveTension class and GameContext integration
2. Extended EventResult fields (tensions, chaining)
3. Extended HandleOutcome processing
4. New EventConditions for tensions

### Phase 2: World Mutation
5. AddFeature/ModifyFeature/RemoveFeature handling
6. DiscoverLocation handling and zone graph integration
7. Location templates and factory

### Phase 3: System Integration
8. SpawnEncounter → predator system bridge
9. Tool/clothing targeting
10. Extended ResourceTypes and RewardPools

### Phase 4: Content
11. New effects (physical, positive, psychological)
12. Tension-based event chains (Stalker arc, Infestation arc, Wound arc)
13. Camp-specific events
14. Full event library implementation

---

This specification provides the framework. The second document will catalog specific events using this system.