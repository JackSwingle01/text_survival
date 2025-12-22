# Location System Overhaul - Implementation Plan

## Scope: Foundation Phase

Implement new location properties (1-5), authored location infrastructure, and replace LocationFactory entirely. The ~100 specific locations will be authored incrementally after foundation is solid.

---

## Phase 1: Add New Location Properties

**File: [Location.cs](Environments/Location.cs)**

Add three new properties:
```csharp
public double Visibility { get; set; } = 0.5;    // 0-1, how far you can see/be seen
public double FootingHazard { get; set; } = 0.0; // 0-1, injury risk per traverse
public bool IsDark { get; set; } = false;        // Requires light to work
public string? DiscoveryText { get; set; }       // First-visit flavor text
public List<string> Tags { get; set; } = [];     // Authored character tags
```

---

## Phase 2: Add EventConditions

**File: [GameContext.cs](Actions/GameContext.cs)**

Add to EventCondition enum:
- `HighVisibility` — Visibility > 0.7
- `LowVisibility` — Visibility < 0.3
- `InDarkness` — IsDark && no active light
- `HasLightSource` — Active fire at location
- `NearWater` — Has WaterFeature

Add condition checks in `Check()` method.

---

## Phase 3: FootingHazard Integration

**File: [TravelProcessor.cs](Environments/TravelProcessor.cs)**

Add method:
```csharp
public static double GetInjuryRisk(Location location, Player player, Weather weather)
```
- Base risk from FootingHazard
- Weather modifiers (wet/snow increases risk)
- Player capacity modifiers (impaired movement increases risk)
- Cap at 50%

**File: [ExpeditionRunner.cs](Actions/Expeditions/ExpeditionRunner.cs)**

After each traverse, roll against injury risk. On failure, apply sprained ankle effect.

---

## Phase 4: Darkness Gating

**File: [WorkRunner.cs](Actions/Expeditions/WorkRunner.cs)**

Before starting work at a location:
- Check if `location.IsDark`
- If dark and no active fire, display message and prevent work
- Future: torch consumption when working in dark

---

## Phase 5: Environmental vs Structural Shelter

**File: [Location.cs](Environments/Location.cs) - GetTemperature()**

Modify to accept activity context:
```csharp
public double GetTemperature(ActivityType activity = ActivityType.Idle)
```

Shelter application rules:
| Activity | Environmental | Structural |
|----------|--------------|------------|
| Resting, Crafting, Idle | ✓ | ✓ |
| Foraging, Hunting, Traveling | ✓ | ✗ |

**Files requiring update to pass activity:**
- [GameContext.cs](Actions/GameContext.cs) - GetSurvivalContext()
- [WorkRunner.cs](Actions/Expeditions/WorkRunner.cs) - work activities
- [ExpeditionRunner.cs](Actions/Expeditions/ExpeditionRunner.cs) - travel

---

## Phase 6: WaterFeature Enhancement

**File: [WaterFeature.cs](Environments/Features/WaterFeature.cs)**

Add water-specific properties:
```csharp
public WaterSourceType SourceType { get; set; }  // Stream, Spring, Pond, Marsh
public bool IsFrozen { get; set; }               // Affects collection time
```

---

## Phase 7: Authored Location Infrastructure

### New Files

**[Environments/LocationTemplates/LocationTemplate.cs]**
```csharp
public record LocationTemplate
{
    // Fixed properties
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string DiscoveryText { get; init; }
    public required LocationFamily Family { get; init; }
    public required List<string> Tags { get; init; }

    // Environment
    public required EnvironmentFeature.LocationType Environment { get; init; }
    public required double Visibility { get; init; }
    public required double FootingHazard { get; init; }
    public required bool IsDark { get; init; }

    // Randomizable ranges
    public required (int Min, int Max) TraversalMinutes { get; init; }
    public required (double Min, double Max) Exposure { get; init; }

    // Features
    public required Func<double, ForageFeature> ForageFactory { get; init; }
    public List<Func<HarvestableFeature>>? OptionalHarvestables { get; init; }
    public List<string>? PossibleAnimals { get; init; }
    public ShelterTemplate? Shelter { get; init; }
    public WaterTemplate? Water { get; init; }
}

public enum LocationFamily { Water, Forest, Rock, Shelter, Hunting, Special }
```

**[Environments/LocationTemplates/LocationRegistry.cs]**

Static class holding all ~100 authored locations. Start with 10-15 examples:
- 3 Water (Frozen Creek, Spring Hollow, Marsh Edge)
- 4 Forest (Dense Thicket, Open Pines, Birch Stand, Deadfall Grove)
- 2 Rock (Rocky Ridge, Boulder Field)
- 2 Shelter (Shallow Cave, Rock Overhang)
- 2 Hunting (Deer Meadow, Game Trail)
- 2 Special (Old Campsite, Bone Pile)

**[Environments/LocationTemplates/LocationBuilder.cs]**

Factory that creates Location from LocationTemplate:
```csharp
public static Location Build(LocationTemplate template, Zone zone)
```
- Copies fixed properties
- Randomizes within ranges
- Creates features from factories
- Rolls for optional features

### Replace LocationFactory

**Delete: [Environments/Factories/LocationFactory.cs]**

**Modify: [Environments/Factories/ZoneGenerator.cs]**
- Select 40-50 templates from LocationRegistry
- Use LocationBuilder to create locations
- Build graph connections

---

## Phase 8: UI Integration

**File: [GameDisplay.cs](UI/GameDisplay.cs) or equivalent**

Show tags in travel options:
```
1. Rocky Ridge [exposed, scout] ~30 min
2. Dense Thicket [hidden] ~15 min
3. Frozen Creek [water] ~20 min
```

Show discovery text on first visit.

---

## Implementation Order

1. Add Location properties (Visibility, FootingHazard, IsDark, DiscoveryText, Tags)
2. Add EventConditions + checks
3. Implement FootingHazard injury rolls
4. Implement Darkness work gating
5. Implement activity-aware shelter
6. Enhance WaterFeature
7. Create LocationTemplate infrastructure
8. Create LocationBuilder
9. Author initial 10-15 templates
10. Modify ZoneGenerator to use templates
11. Delete LocationFactory
12. Update UI to show tags and discovery text
13. Author remaining ~85 locations (can be done incrementally)

---

## Critical Files

| File | Changes |
|------|---------|
| [Environments/Location.cs](Environments/Location.cs) | Add properties, modify GetTemperature() |
| [Actions/GameContext.cs](Actions/GameContext.cs) | Add EventConditions, update Check() |
| [Environments/TravelProcessor.cs](Environments/TravelProcessor.cs) | Add GetInjuryRisk() |
| [Actions/Expeditions/ExpeditionRunner.cs](Actions/Expeditions/ExpeditionRunner.cs) | Injury rolls, activity context |
| [Actions/Expeditions/WorkRunner.cs](Actions/Expeditions/WorkRunner.cs) | Darkness gating, activity context |
| [Environments/Features/WaterFeature.cs](Environments/Features/WaterFeature.cs) | Add SourceType, IsFrozen |
| [Environments/Factories/ZoneGenerator.cs](Environments/Factories/ZoneGenerator.cs) | Use authored templates |
| [Environments/Factories/LocationFactory.cs](Environments/Factories/LocationFactory.cs) | DELETE |
| NEW: Environments/LocationTemplates/LocationTemplate.cs | Template record |
| NEW: Environments/LocationTemplates/LocationRegistry.cs | All authored locations |
| NEW: Environments/LocationTemplates/LocationBuilder.cs | Template → Location factory |

---

## Success Criteria

- [ ] Visibility affects event eligibility and exploration
- [ ] FootingHazard causes injuries during travel
- [ ] Darkness prevents work without light
- [ ] Structural shelter only applies during stationary activities
- [ ] Authored templates replace random generation
- [ ] Tags display in travel UI
- [ ] Discovery text shows on first visit
- [ ] Game builds and runs with new system
