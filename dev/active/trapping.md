# Snare Trapping System - Implementation Plan

## Overview

A passive hunting system where players craft and place snares at locations with animal territories. Snares accumulate catch probability over time, with optional bait to improve success. Full ecosystem risks: predators attracted to bait/catches, scavengers stealing catches, potential trap injuries.

## User Requirements Summary

- **Trap types**: Snares only (small game: rabbit, ptarmigan, fox)
- **Placement**: Only at locations with `AnimalTerritoryFeature`
- **Bait**: Optional enhancement (meat/berries improve catch rate, decay over time)
- **Risks**: Predators attracted, scavengers steal catches, trap injuries possible

---

## Data Model

### PlacedSnare (new class)
```
State: Empty | CatchReady | Stolen | Destroyed
Properties:
  - MinutesSet (time since placement)
  - Bait (Meat | Berries | None)
  - BaitFreshness (0-1, decays ~10%/hour)
  - CaughtAnimalType, CaughtAnimalWeight
  - MinutesSinceCatch (for scavenger timing)
  - DurabilityRemaining (uses left)
```

### SnareLineFeature (new LocationFeature)
```
Manages List<PlacedSnare> at a location
Update(minutes): processes catch chance, bait decay, scavenger checks
Methods: PlaceSnare(), CheckSnares(), RemoveSnare()
```

### Crafting Recipes (NeedCategory.Trapping)
| Recipe | Materials | Time | Uses |
|--------|-----------|------|------|
| Simple Snare | 2 Sticks + 2 PlantFiber | 10 min | 5 |
| Reinforced Snare | 2 Sticks + 1 Sinew + 1 PlantFiber | 15 min | 10 |

---

## Catch Mechanics

### Per-Hour Probability
```
BaseChance = 0.03/hour (3% base)
TimeBonus = min(1.0, hoursSet / 6.0)  // Peaks at 6 hours
DensityMult = gameDensity from AnimalTerritoryFeature
BaitMult = bait ? (1.5 + freshness * 1.0) : 1.0  // 1.5-2.5x with bait

FinalChance = BaseChance * TimeBonus * DensityMult * BaitMult
```

### Animal Selection
Filter AnimalTerritoryFeature spawn list to small game (<10kg):
- Rabbit (2kg), Ptarmigan (0.5kg), Fox (6kg)

### Bait Decay
- 10% freshness loss per hour
- At 0%: bait gone, snare reverts to unbaited

---

## Risk Ecosystem

### Predator Attraction
- Baited snares: 10% chance to create/escalate `Stalked` tension per hour
- Catches waiting: escalate `Stalked` by 0.05/hour
- New event: `PredatorInvestigatesSnare` (wolf/fox may steal catch or destroy trap)

### Scavenger Theft
- After catch sits 3+ hours: 15%/hour chance of theft
- Result: `CatchReady` → `Stolen` (bones/scraps remain, reduced yield)
- Creates narrative: "Something got here before you"

### Trap Injuries
- 5% base chance when setting/checking
- +10% per 0.1 manipulation impairment
- Outcomes: cut fingers, hand abrasion (minor injuries)

---

## Work Integration

Add to `WorkRunner`:
- `DoSetTrap(Location, Tool snare, BaitType?)` - place snare at location
- `DoCheckTraps(Location)` - check all snares, collect catches

Add to `GetWorkOptions`:
- "Set snare" (if player has snare in inventory, location has AnimalTerritoryFeature)
- "Check traps" (if location has SnareLineFeature with snares)

---

## Events

### New Tensions
- `TrapLineActive`: Decays 0.02/hr, doesn't decay at camp

### Event Ideas
| Event | Trigger | Description |
|-------|---------|-------------|
| SnareTampered | TrapLineActive + OnExpedition | Find snare disturbed, tracks around it |
| PredatorAtTrapLine | Stalked + TrapLineActive | Predator investigating your snares |
| GoodCatch | Check snare with catch | Narrative moment for successful trapping |
| TrapLinePlundered | Catch waiting >3hr | Scavengers got there first |
| TrappingAccident | Setting/Checking + low manipulation | Hand injury from mechanism |

---

## Implementation Phases

### Phase 1: Core Data Structures
1. Add `ToolType.Snare` to `Items/Tool.cs`
2. Create `Environments/Features/PlacedSnare.cs`
3. Create `Environments/Features/SnareLineFeature.cs`
4. Add `NeedCategory.Trapping` to `Crafting/NeedCategory.cs`
5. Add snare recipes to `Crafting/NeedCraftingSystem.cs`

### Phase 2: Placement & Checking
1. Add `DoSetTrap()` and `DoCheckTraps()` to `WorkRunner.cs`
2. Update `GetWorkOptions()` to show trap options
3. Integrate with AnimalTerritoryFeature for placement validation
4. Connect catches to butchering (small game → meat, bone, hide)

### Phase 3: Catch Mechanics
1. Implement probability calculations in `PlacedSnare.Update()`
2. Add small game filtering from territory
3. Implement bait system (placement, decay)
4. Handle durability consumption (catch or triggered-empty = 1 use)

### Phase 4: Risk System
1. Add EventConditions: `HasActiveSnares`, `SnareHasCatch`, `SnareBaited`
2. Create `TrapLineActive` tension
3. Implement scavenger theft mechanic in `PlacedSnare.Update()`
4. Add trap injury checks in work methods

### Phase 5: Events
1. Create `Actions/Events/GameEventRegistry.Trapping.cs`
2. Register snare events in `GameEventRegistry.cs`
3. Add predator attraction escalation
4. Balance probability numbers through testing

---

## Files to Create

| File | Purpose |
|------|---------|
| `Environments/Features/PlacedSnare.cs` | Snare state and per-minute update logic |
| `Environments/Features/SnareLineFeature.cs` | Manages snares at a location |
| `Actions/Events/GameEventRegistry.Trapping.cs` | Trap-related events |

## Files to Modify

| File | Changes |
|------|---------|
| `Items/Tool.cs` | Add `ToolType.Snare` |
| `Crafting/NeedCategory.cs` | Add `Trapping` category |
| `Crafting/NeedCraftingSystem.cs` | Add `InitializeTrappingOptions()` with recipes |
| `Actions/Expeditions/WorkRunner.cs` | Add `DoSetTrap()`, `DoCheckTraps()`, update `GetWorkOptions()` |
| `Actions/GameContext.cs` | Add EventConditions for snare states |
| `Actions/Tensions/ActiveTension.cs` | Add `TrapLineActive` factory method |
| `Actions/GameEventRegistry.cs` | Register trapping events |

---

## Design Rationale

### Why SnareLineFeature vs Player Inventory?
Snares persist at locations after placement. Following existing patterns (ForageFeature, HarvestableFeature), placed traps belong on the location graph.

### Why Time-Based Mechanics?
Design principles emphasize "time is the universal currency." Delayed outcomes create:
- Return trip pressure (compound with fire management)
- Risk windows (scavengers, predators)
- Planning decisions (when to check, how many locations)

### Why Use AnimalTerritoryFeature?
Extends existing animal spawning system rather than creating parallel mechanics. Reuses game density, spawn weights, and depletion. ("Extend before creating")
