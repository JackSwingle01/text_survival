# Camp-Centric Survival Refactor Plan
**Date**: 2025-12-09
**Status**: Planning Complete - Ready for Implementation

## Overview

Transform the game from a location-navigation model to a camp-centric expedition model:
- Player has a **camp** (fire, shelter, storage) as home base
- All other locations are **expedition destinations** with round-trip travel
- **Fire is the tether** - it burns while you're away
- Actions are **commitments** with time calculated upfront

---

## Phase 1: Camp Foundation

### 1.1 Create CampManager

**New File**: `Actors/Player/CampManager.cs`

```csharp
public class CampManager
{
    public Location? CampLocation { get; private set; }
    public HeatSourceFeature? Fire => CampLocation?.GetFeature<HeatSourceFeature>();
    public ShelterFeature? Shelter => CampLocation?.GetFeature<ShelterFeature>();

    public double GetFireMarginMinutes(int expeditionMinutes)
    {
        if (Fire == null || !Fire.IsActive && !Fire.HasEmbers)
            return double.NegativeInfinity;
        return Fire.GetMinutesRemaining() - expeditionMinutes;
    }

    public void SetCamp(Location location) { ... }
    public void AbandonCamp() { ... }
}
```

### 1.2 Modify Player and Context

**File**: `Actors/Player/Player.cs`
- Add `public readonly CampManager campManager;`

**File**: `Actions/GameContext.cs`
- Add `public CampManager CampManager => player.campManager;`
- Add `public Location? Camp => player.campManager.CampLocation;`
- Add `public bool IsAtCamp => player.locationManager.CurrentLocation == Camp;`

### 1.3 Update Program.cs

Set initial camp on game start:
```csharp
player.campManager.SetCamp(startingArea);
```

---

## Phase 2: Expedition System Core

### 2.1 Expedition Infrastructure

**New File**: `Expeditions/ExpeditionPhase.cs`
```csharp
public enum ExpeditionPhase { NotStarted, TravelOut, Working, TravelBack, Completed, Aborted }
```

**New File**: `Expeditions/ActivityType.cs`
```csharp
public enum ActivityType { Forage, Hunt, Explore, Gather, Scout }
```

**New File**: `Expeditions/Expedition.cs`
```csharp
public class Expedition
{
    public Location Destination { get; }
    public Location Camp { get; }
    public ActivityType Activity { get; }

    // Time calculations
    public int TravelTimeOutMinutes { get; }
    public int WorkTimeBaseMinutes { get; }
    public int WorkTimeVarianceMinutes { get; }
    public int TravelTimeBackMinutes { get; }
    public int TotalEstimatedMinutes => TravelTimeOutMinutes + WorkTimeBaseMinutes + TravelTimeBackMinutes;

    // Risk factors
    public double ExposureFactor { get; }
    public double DetectionRisk { get; }

    // State
    public ExpeditionPhase CurrentPhase { get; private set; }
    public int ElapsedInPhaseMinutes { get; private set; }
    public int TotalElapsedMinutes { get; private set; }

    // Results
    public List<Item> ItemsGathered { get; } = new();
    public List<string> EventLog { get; } = new();
}
```

**New File**: `Expeditions/ExpeditionFactory.cs`
- `CreateForageExpedition(destination, camp)`
- `CreateHuntExpedition(destination, camp, target)`
- `CreateExploreExpedition(camp)`
- Uses coordinate distance for travel time calculation

### 2.2 Fire Margin Calculator

**New File**: `Expeditions/FireMarginCalculator.cs`
```csharp
public enum MarginStatus { Comfortable, Tight, Risky, VeryRisky }

public static MarginStatus GetStatus(double marginMinutes)
{
    if (marginMinutes > 30) return Comfortable;
    if (marginMinutes > 0) return Tight;
    if (marginMinutes > -15) return Risky;
    return VeryRisky;
}
```

### 2.3 Expedition Runner

**New File**: `Expeditions/ExpeditionRunner.cs`

Core execution loop that processes expeditions in 5-minute chunks:
```csharp
public ExpeditionChunkResult ProcessChunk(Expedition expedition, Player player)
{
    for (int i = 0; i < CHUNK_SIZE; i++)
    {
        player.Update();           // Tick survival
        World.Update(1, true);     // Tick world (fire burns)

        var evt = CheckForEvents(expedition, player);
        if (evt.HasEvent) return new ChunkResult { Event = evt };

        expedition.AddElapsedTime(1);
    }
    return new ChunkResult { MinutesProcessed = CHUNK_SIZE };
}
```

---

## Phase 3: Expedition Actions

### 3.1 Create ExpeditionActions

**New File**: `Actions/ExpeditionActions.cs`

Key actions:
- `SelectForageExpedition()` - Choose destination for foraging
- `SelectHuntExpedition()` - Choose destination for hunting
- `SelectExploreExpedition()` - Dedicated exploration action
- `ShowExpeditionSummary(expedition)` - Display time/margin, confirm
- `ExecuteExpeditionPhase(expedition)` - Run expedition loop
- `WorkPhaseMenu(expedition)` - Options during work phase
- `ExpeditionComplete(expedition)` - Show results, transfer items

### 3.2 Expedition Confirmation Flow

```
1. Player selects "Forage"
2. Shows destination list with travel times + margins
3. Player selects destination
4. Shows expedition summary:
   "Deep Forest — ~70 min round trip
    Fire will have ~15 min remaining
    Proceed?"
5. Player confirms → expedition begins
6. Chunks process with event checks
7. On completion → results + back to camp
```

### 3.3 Refactor Main Menu

**File**: `Actions/ActionFactory.cs`

```csharp
public static IGameAction MainMenu()
{
    return CreateAction("Main Menu")
        .Do(ctx => {
            ShowSurvivalStats(ctx);
            if (ctx.IsAtCamp) CampStatusDisplay.Show(ctx.CampManager);
        })
        .ThenShow(ctx => ctx.IsAtCamp ? GetCampActions(ctx) : GetExpeditionActions(ctx))
        .Build();
}

private static List<IGameAction> GetCampActions(GameContext ctx)
{
    return [
        // Camp actions (no travel)
        Survival.TendFire(),
        Survival.Rest(),
        Survival.EatDrink(),
        Crafting.OpenCraftingMenu(),

        // Expeditions (round-trip)
        ExpeditionActions.SelectForageExpedition(),
        ExpeditionActions.SelectHuntExpedition(),
        ExpeditionActions.SelectExploreExpedition(),

        // Major decision
        Movement.MoveCamp(),
    ];
}
```

---

## Phase 4: Adapt Existing Systems

### 4.1 Foraging → Expedition

Current `ForageFeature.Forage()` stays the same, called from expedition work phase:
```csharp
public static IGameAction DoForageWork(Expedition expedition)
{
    return CreateAction("Search for resources (15 min)")
        .Do(ctx => {
            var feature = expedition.Destination.GetFeature<ForageFeature>();
            feature.Forage(0.25);
            expedition.ItemsGathered.AddRange(GetFoundItems());
        })
        .ThenShow(_ => [WorkPhaseMenu(expedition)])
        .Build();
}
```

### 4.2 Hunting → Expedition

- Keep `StealthManager` for stealth state tracking
- Hunt expedition's work phase uses existing approach/shoot sequence
- Variable work time based on tracking success

### 4.3 Movement → Move Camp

**Remove**: `GoToLocation()`, `GoToLocationByMap()`, `Travel()`

**Replace with**: `MoveCamp()` action with two options:
1. **Within zone** (10-30 min): Move to nearby location, easy
2. **To new zone** (60-120 min): Big journey, more events

```csharp
public static IGameAction MoveCamp()
{
    return CreateAction("Move Camp")
        .When(ctx => ctx.IsAtCamp)
        .ThenShow(ctx => [
            MoveCampWithinZone(),
            MoveCampToNewZone(),
            Common.Return("Stay here")
        ])
        .Build();
}
```

---

## Phase 5: Event System (MVP)

### 5.1 Basic Event Types

**New File**: `Expeditions/ExpeditionEvent.cs`
```csharp
public enum ExpeditionEventType
{
    FireDanger,   // Fire margin low
    Encounter,    // Hostile animal
    Discovery,    // Found something
    Weather,      // Weather change
    Injury        // Minor injury
}
```

### 5.2 Event Checking

During expedition chunks, check for:
1. **Fire danger**: If margin drops below threshold, warn player
2. **Encounters**: Random hostile encounter based on DetectionRisk
3. **Discovery**: Random find during exploration

### 5.3 Event Responses

```csharp
public static List<IGameAction> GetEventResponses(ExpeditionEvent evt, Expedition exp)
{
    return evt.Type switch
    {
        FireDanger => [ContinueExpedition(exp), AbortExpedition(exp)],
        Encounter => [FightEncounter(exp, evt.Animal), FleeEncounter(exp)],
        _ => [ContinueExpedition(exp)]
    };
}
```

---

## Phase 6: Polish and Integration

### 6.1 Camp Status Display

**New File**: `UI/CampStatusDisplay.cs`
```
┌──────── CAMP STATUS ────────┐
  Fire: Steady (45 min)
  Temperature: 28°F
  Shelter: Lean-to (wind -40%)
└─────────────────────────────┘
```

### 6.2 Expedition Summary

On return, show:
- Time elapsed
- Items gathered (grouped)
- Fire status
- Any injuries/consequences

### 6.3 Testing

- Test forage expeditions with various fire margins
- Test hunt expeditions with existing stealth system
- Test move camp within zone and between zones
- Test event interrupts (fire warning, encounters)

---

## File Summary

### New Files (10)

| File | Purpose |
|------|---------|
| `Actors/Player/CampManager.cs` | Camp state, fire margin calculation |
| `Expeditions/ExpeditionPhase.cs` | Phase enum |
| `Expeditions/ActivityType.cs` | Activity enum |
| `Expeditions/Expedition.cs` | Expedition data class |
| `Expeditions/ExpeditionFactory.cs` | Create expedition instances |
| `Expeditions/ExpeditionRunner.cs` | Chunk-based execution loop |
| `Expeditions/ExpeditionEvent.cs` | Event types and data |
| `Expeditions/FireMarginCalculator.cs` | Margin status calculation |
| `Actions/ExpeditionActions.cs` | All expedition-related actions |
| `UI/CampStatusDisplay.cs` | Camp status rendering |

### Modified Files (5)

| File | Changes |
|------|---------|
| `Actors/Player/Player.cs` | Add CampManager |
| `Actions/GameContext.cs` | Add camp/expedition context |
| `Actions/ActionFactory.cs` | New main menu, remove old movement |
| `Actors/Player/LocationManager.cs` | Simplify (no more "move to" locations) |
| `Core/Program.cs` | Set initial camp |

### Unchanged Files (Reused)

- `Environments/Location.cs` - Already has camp features
- `Environments/Features/HeatSourceFeature.cs` - Fire physics unchanged
- `Environments/Features/ForageFeature.cs` - Called from expeditions
- `Actors/Player/StealthManager.cs` - Used for hunt expeditions
- `Environments/Zone.cs` - Used for move camp between zones

---

## Implementation Order

### Batch 1: Foundation
1. Create `CampManager.cs`
2. Modify `Player.cs` to use CampManager
3. Modify `GameContext.cs` with camp context
4. Update `Program.cs` to set initial camp
5. Build & test: Player has camp reference

### Batch 2: Expedition Core
1. Create `Expeditions/` folder with enums
2. Create `Expedition.cs` data class
3. Create `ExpeditionFactory.cs`
4. Create `FireMarginCalculator.cs`
5. Build & test: Can create expedition objects

### Batch 3: Expedition Runner
1. Create `ExpeditionRunner.cs`
2. Create `ExpeditionEvent.cs`
3. Build & test: Expeditions process in chunks

### Batch 4: Forage Expedition
1. Create `ExpeditionActions.cs` with forage actions
2. Refactor main menu to use expedition actions
3. Remove old standalone forage action
4. Build & test: Full forage expedition flow

### Batch 5: Hunt + Explore
1. Add hunt expedition to `ExpeditionActions.cs`
2. Integrate existing `StealthManager`
3. Add explore expedition
4. Build & test: Hunt and explore work

### Batch 6: Move Camp
1. Create `MoveCamp()` action
2. Remove old movement actions
3. Test within-zone and between-zone moves
4. Build & test: Camp relocation works

### Batch 7: Polish
1. Create `CampStatusDisplay.cs`
2. Improve expedition summary display
3. Add fire margin warnings to UI
4. Full integration testing

---

## Key Design Decisions

1. **Camp is just a Location** - No new Camp class, CampManager references a Location
2. **Expeditions are data objects** - Not actions themselves, processed by runner
3. **Chunk-based execution** - 5-minute chunks for granular event checking
4. **Fire margin is automatic** - Calculated and displayed on all expedition selections
5. **Zones stay for now** - Used for "big journey" move camp option
6. **Events are minimal MVP** - Fire warnings and basic encounters only
7. **Existing systems adapted** - ForageFeature, StealthManager reused

---

## Data Flow: Expedition

```
1. Player selects action type (Forage)
         ↓
2. Player selects location (Deep Forest)
         ↓
3. System calculates expedition shape:
   - Travel out: 25 min
   - Work time: 20 min (±10 variance)
   - Travel back: 25 min
   - Total estimate: 70 min (range: 60-80)
         ↓
4. System checks fire margin:
   - Fire remaining: 85 min
   - Margin after: 5-25 min
   - Verdict: Tight but possible
         ↓
5. Player sees summary and confirms:
   "Deep Forest — ~70 min round trip
    Fire will have ~15 min remaining
    Proceed?"
         ↓
6. Expedition begins
         ↓
7. Phase: TRAVEL OUT
   - Loop in chunks (5 min each)
   - Each chunk: tick survival, tick world (fire burns)
   - Event check each chunk (lower probability)
   - Player not shown granular updates, just interrupts
         ↓
8. Phase: WORK
   - Roll actual duration (20 min ± 10)
   - Loop in chunks
   - Each chunk: tick survival, tick world
   - Event check each chunk (higher probability)
   - Events may offer abort: "Storm coming. Head back now?"
         ↓
9. Phase: TRAVEL BACK
   - Same as travel out
   - Events can still happen
   - Tension if fire margin is thin
         ↓
10. Arrival
    - Results presented (what you found/caught)
    - Fire status shown
    - Consequences resolved (if fire died, if injured, etc.)
         ↓
11. Back to camp menu
```

---

## Fire Margin Check Logic

```csharp
CheckFireMargin(expedition):

    fireRemaining = camp.fire.MinutesRemaining
    expeditionTime = expedition.TotalEstimatedTime

    margin = fireRemaining - expeditionTime

    if margin > 30:
        // Comfortable, no warning
        return true

    if margin > 0:
        // Tight
        Warn("Fire will have ~{margin} min when you return")
        return Confirm()

    if margin > -15:
        // Risky
        Warn("Fire may die before you return")
        return Confirm()

    // Very risky
    Warn("Fire will likely die. You'll return to cold camp.")
    return Confirm()
```

---

## Event Integration Points

Events belong to phases and contexts:

```
TravelOut/TravelBack:
    - Weather events (storm approaching)
    - Navigation events (get lost, find shortcut)
    - Wildlife sighting (distant, not engaged)
    - Injury events (slip, fall)

Work (Forage):
    - Discovery events (find cache, corpse, new location)
    - Wildlife encounters (closer, more likely)
    - Tool breaks
    - Bonus finds

Work (Hunt):
    - Prey behavior (spooks, moves, charges)
    - Other predators
    - Shot outcomes

Work (Explore):
    - Discovery events (primary purpose)
    - Danger events (stumble into predator den)
```

---

## Action Type Summary

| Type | Travel | Fire Margin Check | Examples |
|------|--------|-------------------|----------|
| Camp Action | None | No | Tend fire, craft, rest, eat |
| Expedition | Round-trip | Yes (automatic) | Forage, hunt, explore |
| Move Camp | One-way | N/A (leaving fire) | Relocate camp |
