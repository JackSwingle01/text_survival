# Event System Enhancement - Implementation Plan

## Overview

This plan implements the tension system, extended event results, world mutation, and system integration as specified in `dev/active/event-improvements.md`.

**Approach:** Incremental implementation. Complete Phase 1, test with a sample tension arc (Stalker), then proceed to later phases.

**Design Decisions:**
- Tool damage: Integer uses lost (matches existing `int Durability` system)
- Equipment damage: Reduce `Insulation` directly (no new condition property)
- Implementation: Phase 1 first, validate with working events, then continue

---

## Phase 1: Core Infrastructure (IMPLEMENT FIRST)

### 1.1 ActiveTension Class

**Create:** `Actions/Tensions/ActiveTension.cs`

```csharp
public class ActiveTension
{
    public string Type { get; }              // "Stalked", "SmokeSpotted", etc.
    public double Severity { get; set; }     // 0.0-1.0
    public DateTime CreatedAt { get; }
    public Location? RelevantLocation { get; }  // null = global, set = location-specific
    public Location? SourceLocation { get; }    // For "return to investigate" scenarios

    // Explicit properties instead of Dictionary<string, object>
    public string? AnimalType { get; }       // For Stalked: "Wolf", "Bear"
    public string? Direction { get; }        // For discoveries: "Southwest ridge"
    public string? Description { get; }      // Flavor: "Left arm laceration"

    // Decay/escalation behavior (tension-type-specific)
    public double DecayPerHour { get; }      // How fast severity decreases
    public bool DecaysAtCamp { get; }        // Does it decay when at camp?
    public bool EscalatesPassively { get; }  // Does it escalate without events? (usually false)
}
```

**No Dictionary<string, object>** — explicit properties based on actual catalog usage.

### 1.2 TensionRegistry

**Create:** `Actions/Tensions/TensionRegistry.cs`

Following the EffectRegistry pattern:
- `List<ActiveTension> Tensions`
- `HasTension(string type)` - location-aware check
- `GetTension(string type)` - retrieve by type
- `AddTension(ActiveTension)` - add new tension
- `ResolveTension(string type)` - remove tension
- `EscalateTension(string type, double amount)` - increase severity
- `Update(int minutes, bool atCamp)` - time-based decay

### 1.2.1 Decay Rules

**No passive escalation** — escalation only happens through event outcomes (makes it player-visible and decision-relevant).

**Passive decay only** — tensions fade over time if nothing triggers.

**Decay behavior per tension type:**
| Tension | DecayPerHour | DecaysAtCamp | Notes |
|---------|--------------|--------------|-------|
| Stalked | 0.05 | Yes | Predator loses the scent while at camp |
| SmokeSpotted | 0.03 | No | Smoke is still out there |
| Infested | 0.0 | No | Doesn't decay until resolved (vermin won't leave) |
| WoundUntreated | 0.0 | No | Actually escalates via effects (infection) |
| ShelterWeakened | 0.0 | No | Structural, doesn't heal |
| FoodScentStrong | 0.10 | Yes | Dissipates quickly |

**Decay formula in Update():**
```csharp
if (tension.DecaysAtCamp || !atCamp)
{
    double decay = tension.DecayPerHour * (minutes / 60.0);
    tension.Severity = Math.Max(0, tension.Severity - decay);
    if (tension.Severity <= 0)
        Remove(tension);
}
```

### 1.3 GameContext Integration

**Modify:** `Actions/GameContext.cs`

- Add `TensionRegistry Tensions` property
- Extend `Check()` method with tension-based conditions:
  - `Stalked`, `SmokeSpotted`, `Infested`, `WoundUntreated`, `ShelterWeakened`, `FoodScentStrong`, `Hunted`
  - Severity threshold variants: `StalkedHigh`, `InfestedSevere`

### 1.4 Extended EventResult

**Modify:** `Actions/GameEvent.cs`

Add new fields to EventResult:

```csharp
// Tension fields
public TensionCreation? CreatesTension { get; set; }
public string? ResolvesTension { get; set; }
public (string type, double amount)? EscalateTension { get; set; }

// Chaining fields
public Func<GameContext, GameEvent>? ChainEvent { get; set; }
public EncounterConfig? SpawnEncounter { get; set; }

// World mutation fields
public FeatureCreation? AddFeature { get; set; }
public FeatureModification? ModifyFeature { get; set; }
public Type? RemoveFeature { get; set; }
public LocationTemplate? DiscoverLocation { get; set; }

// Equipment targeting
public (ToolType type, int usesLost)? DamageTool { get; set; }  // Integer uses
public ToolType? BreakTool { get; set; }
public (EquipSlot slot, double insulationLoss)? DamageClothing { get; set; }  // Direct reduction

// Positive effects
public Effect? GrantBuff { get; set; }
```

### 1.5 Supporting Types

**Create:** `Actions/EventTypes.cs`

```csharp
// TensionCreation with explicit properties (no Dictionary)
public record TensionCreation(
    string Type,
    double Severity,
    Location? RelevantLocation = null,
    string? AnimalType = null,      // For Stalked
    string? Direction = null,       // For discoveries
    string? Description = null      // Flavor text
);

public record EncounterConfig(string AnimalType, double InitialDistance, double InitialBoldness, List<string>? Modifiers = null);
public record LocationTemplate(string NamePattern, int TravelTime, string? Direction = null);
public record FeatureCreation(Type FeatureType, object Config);
public record FeatureModification(Type FeatureType, double? DepleteAmount = null);
```

### 1.6 Extended HandleOutcome

**Modify:** `Actions/GameEventRegistry.cs` - `HandleOutcome()`

Process in order:
1. Time (existing)
2. Message (existing)
3. Resources - costs and rewards (existing)
4. Damage/Effects (existing)
5. **Equipment** - tool damage, clothing damage (NEW)
6. **Tensions** - create, resolve, escalate (NEW)
7. **World Mutation** - features, locations (NEW)
8. **Chaining** - encounters, chain events (NEW)
9. Expedition abort (existing, moved to end)

---

## Phase 2: World Mutation

### 2.1 Feature Modification Support

**Modify:** `Environments/Location.cs`

Add methods:
- `RemoveFeature(Type featureType)` or `RemoveFeature<T>()`
- Allow feature property modifications (currently immutable)

**Modify:** Feature classes to support modification:
- `ForageFeature.Deplete(amount)`, `ForageFeature.Restore(amount)`
- `ShelterFeature.Damage(amount)`
- `HeatSourceFeature` already has modification methods

### 2.2 Location Discovery Enhancement

**Modify:** `Environments/Zone.cs`

Current system: `RevealRandomLocation(connectFrom)` pulls from unrevealed pool.

Enhancement: Support event-driven location creation:
- `AddDiscoveredLocation(LocationTemplate template, Location connectFrom)`
- Create location from template with appropriate features
- Connect to specified location
- Mark as unexplored until visited

### 2.3 LocationTemplate Factory

**Create:** `Environments/Factories/LocationTemplateFactory.cs`

- Convert LocationTemplate to actual Location
- Set features based on template hints
- Handle dynamic naming (`{Details}` substitution)

---

## Phase 3: System Integration

### 3.1 Predator Encounter Bridge

**Modify:** `Actions/GameEventRegistry.cs`

Add method: `SpawnPredatorEncounter(GameContext ctx, EncounterConfig config)`
- Create Animal from config.AnimalType
- Initialize encounter with config.InitialDistance, config.InitialBoldness
- Apply config.Modifiers to animal
- Hand off to existing encounter system

**Dependencies:** Need to understand `PredatorEncounter` class location and initialization.

### 3.2 Tool/Equipment Targeting

**Modify:** `Actions/GameEventRegistry.cs` - `HandleOutcome()`

```csharp
if (outcome.DamageTool != null)
{
    var tool = ctx.Inventory.GetTool(outcome.DamageTool.Type);
    if (tool != null)
    {
        tool.Durability -= outcome.DamageTool.DurabilityLoss;
        // Display message
    }
}
```

**Review needed:** Current tool/durability model in Inventory.

### 3.3 Extended ResourceTypes and RewardPools

**Modify:** `Items/RewardGenerator.cs`

Add new pools:
- `CraftingMaterials`, `ScrapTool`, `WaterSource`, `Tinder`
- `Bones`, `SmallGame`, `Feathers`, `MedicinalPlants`
- `Charcoal`, `Relics`

---

## Phase 4: Content & Events

### 4.1 New Effects

**Modify:** `Effects/EffectFactory.cs` (or equivalent)

Physical effects:
- Sore, Exhausted, Nauseous, FrozenFingers, Coughing

Positive effects (buffs):
- Warmed, Fed, Rested, Focused, IronGut, Hardened

Psychological effects:
- Shaken, Paranoid (Fear already exists)

### 4.2 Tension-Based Event Chains

Example: Stalker Arc
1. **Something Watching** - Creates `Stalked` tension (severity 0.3)
2. **Stalker Circling** - Requires Stalked, escalates severity
3. **Stalker Revealed** - Requires Stalked severity > 0.5, shows animal type
4. **Ambush** - Requires Stalked severity > 0.7, spawns encounter

### 4.3 Camp-Specific Events

New EventConditions:
- `AtCamp` - ctx.IsAtCamp
- `OnExpedition` - ctx.Expedition != null
- `NearFire` - current location has active HeatSourceFeature
- `HasShelter` - current location has ShelterFeature

Camp events:
- Fire behavior (FireTrouble, EmbersScatter, ChokingSmoke)
- Storage events (VerminRaid, FoodSpoilage)
- Shelter events (ShelterLeak, StructuralFailure)

---

## Implementation Sequence

### Batch 1: Foundation
1. Create `ActiveTension` class
2. Create `TensionRegistry` class
3. Add TensionRegistry to GameContext
4. Add tension-based EventConditions to Check()

### Batch 2: EventResult Extension
5. Add supporting types (TensionCreation, EncounterConfig, etc.)
6. Extend EventResult with new fields
7. Update HandleOutcome processing order

### Batch 3: World Mutation
8. Add feature removal to Location
9. Add feature modification support
10. Enhance Zone with template-based discovery

### Batch 4: System Integration
11. Implement SpawnEncounter bridge
12. Implement tool/clothing damage
13. Add new RewardPools

### Batch 5: Content
14. Add new effects
15. Create first tension arc (Stalker)
16. Create camp-specific events

---

## Files to Create
- `Actions/Tensions/ActiveTension.cs`
- `Actions/Tensions/TensionRegistry.cs`
- `Actions/EventTypes.cs`
- `Environments/Factories/LocationTemplateFactory.cs`

## Files to Modify
- `Actions/GameContext.cs` - Add TensionRegistry, extend Check()
- `Actions/GameEvent.cs` - Extend EventResult
- `Actions/GameEventRegistry.cs` - Extend HandleOutcome
- `Environments/Location.cs` - Add RemoveFeature
- `Environments/Zone.cs` - Template-based discovery
- `Items/RewardGenerator.cs` - New pools
- Various feature classes for modification support

---

## Verified System Details

### Tool Durability (Items/Tool.cs)
- Uses `int Durability` (-1 = infinite, counts down on Use())
- `IsBroken` property (Durability == 0)
- For DamageTool: spec says `double durabilityLoss` but system uses integer uses
- **Decision needed:** Convert to use-reduction (int) or add a separate condition system?

### Equipment/Clothing (Items/Equipment.cs)
- `Equipment` class with `EquipSlot` (Head, Chest, Legs, Feet, Hands)
- `double Insulation` property (0-1)
- **No condition/durability** on equipment currently
- DamageClothing could reduce Insulation directly (already mutable)

### Predator Encounters (Actions/Expeditions/ExpeditionRunner.cs:552-701)
- `HandlePredatorEncounter(Animal predator, Expedition? expedition)` already exists
- Takes Animal with `EncounterBoldness` and `DistanceFromPlayer` set
- Returns `EncounterOutcome` enum
- **SpawnEncounter integration:** Create Animal, configure boldness/distance, call existing method

---

## Resolved Design Decisions

1. **Tool damage:** Integer uses lost (matches existing system)
2. **Equipment damage:** Reduce Insulation directly (simple, mutable property)
3. **Implementation:** Incremental - Phase 1 first, test with Stalker arc, then continue
4. **No Dictionary<string, object>:** Use explicit properties (AnimalType, Direction, Description) for tension data
5. **Decay mechanics:** Passive decay only, no passive escalation. Escalation happens through event outcomes.
6. **Camp behavior:** Tension-type-specific (Stalked decays at camp, Infested persists, etc.)

---

## Phase 1 Implementation Steps (Detailed)

### Step 1: Create ActiveTension class
**File:** `Actions/Tensions/ActiveTension.cs`
- Explicit properties: Type, Severity, CreatedAt, RelevantLocation, SourceLocation
- Content properties: AnimalType, Direction, Description (no Dictionary)
- Decay behavior: DecayPerHour, DecaysAtCamp, EscalatesPassively
- Factory methods encode decay table as source of truth:

```csharp
public static ActiveTension Stalked(double severity, string? animalType = null) => new(
    type: "Stalked",
    severity: severity,
    decayPerHour: 0.05,
    decaysAtCamp: true,
    animalType: animalType
);

public static ActiveTension Infested(double severity) => new(
    type: "Infested",
    severity: severity,
    decayPerHour: 0.0,
    decaysAtCamp: false
);

public static ActiveTension SmokeSpotted(double severity, string? direction = null) => new(
    type: "SmokeSpotted",
    severity: severity,
    decayPerHour: 0.03,
    decaysAtCamp: false,
    direction: direction
);

public static ActiveTension WoundUntreated(double severity, string? description = null) => new(
    type: "WoundUntreated",
    severity: severity,
    decayPerHour: 0.0,  // Escalates via effects, not decay
    decaysAtCamp: false,
    description: description
);
```

### Step 2: Create TensionRegistry
**File:** `Actions/Tensions/TensionRegistry.cs`
- List<ActiveTension> storage
- HasTension(type) with location-aware check
- GetTension(type), AddTension(), ResolveTension(), EscalateTension()
- Update(minutes, atCamp) with passive decay only (no passive escalation)
- Decay formula: severity -= decayPerHour * (minutes / 60.0)

### Step 3: Integrate TensionRegistry into GameContext
**Modify:** `Actions/GameContext.cs`
- Add `TensionRegistry Tensions` property
- Initialize in constructor
- Call `Tensions.Update()` in GameContext.Update()

### Step 4: Add tension-based EventConditions
**Modify:** `Actions/GameContext.cs`
- Add to EventCondition enum: Stalked, SmokeSpotted, Infested, WoundUntreated, etc.
- Add to Check() method: `case EventCondition.Stalked: return Tensions.HasTension("Stalked");`

### Step 5: Create supporting types
**File:** `Actions/EventTypes.cs`
- TensionCreation record
- EncounterConfig record (for future use)
- Keep minimal - only what Phase 1 needs

### Step 6: Extend EventResult
**Modify:** `Actions/GameEvent.cs`
- Add CreatesTension, ResolvesTension, EscalateTension fields
- Add SpawnEncounter field (for Stalker arc finale)
- Add GrantBuff field

### Step 7: Extend HandleOutcome
**Modify:** `Actions/GameEventRegistry.cs`
- Add tension processing after damage/effects
- Add SpawnEncounter processing (calls existing HandlePredatorEncounter)
- Add GrantBuff processing

### Step 8: Create Stalker Event Arc (Test Content)
**Modify:** `Actions/GameEventRegistry.cs`

Implement from `dev/active/event-catalog.md`:
- **Strange Sound Nearby** → Various outcomes, some create Stalked (0.3-0.4)
- **Stalker Circling** → Requires Stalked, outcomes escalate (+0.15 to +0.2) or reduce (-0.1)
- **The Predator Revealed** → Requires Stalked severity > 0.5, uses `{TensionDetails}` for animal name
- **Ambush** → Requires Stalked severity > 0.7, spawns encounter at close range with high boldness

Key patterns from catalog:
- `Creates Tension: Stalked (0.3)` → CreatesTension = new TensionCreation("Stalked", 0.3, animalType: "Wolf")
- `Escalates Stalked (+0.2)` → EscalateTension = ("Stalked", 0.2)
- `Resolves Stalked` → ResolvesTension = "Stalked"
- `Spawns Encounter (close range, high boldness)` → SpawnEncounter = new EncounterConfig("Wolf", 15, 0.7)

### Step 9: Test and Validate
- Build and run
- Verify tension creation from events
- Verify tension-based event filtering
- Verify tension escalation and resolution
- Verify SpawnEncounter triggers predator encounter

---

## Files Summary

### To Create
- `Actions/Tensions/ActiveTension.cs`
- `Actions/Tensions/TensionRegistry.cs`
- `Actions/EventTypes.cs`

### To Modify
- `Actions/GameContext.cs` - TensionRegistry, EventConditions
- `Actions/GameEvent.cs` - Extended EventResult
- `Actions/GameEventRegistry.cs` - HandleOutcome, new event factories
