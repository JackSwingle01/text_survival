# Location System Overhaul - Implementation Plan

## Scope: Foundation Phase

Implement new location properties (1-5), authored location infrastructure, and replace LocationFactory entirely. The ~100 specific locations will be authored incrementally after foundation is solid.

---

## Phase 1: Add New Location Properties - DONE

**File: [Location.cs](Environments/Location.cs)**

Add three new properties:
```csharp
public double Visibility { get; set; } = 0.5;    // 0-1, how far you can see/be seen
public double FootingHazard { get; set; } = 0.0; // 0-1, injury risk per traverse
public bool IsDark { get; set; } = false;        // Requires light to work
public string? DiscoveryText { get; set; }       // First-visit flavor text
public List<string> Tags { get; set; } = [];     // Authored character tags
```

Note this has been completed, but we decided to change the exact properties slightly - see location.cs.

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


---

ORIGINAL DESIGN:

**Location System Overhaul**

---

**What a location is:**

A location is a *destination* - somewhere you'd say "I'm going to X." It represents an activity area, not a physical size. Frozen Creek isn't the whole creek - it's the stretch where you work. Travel time is distance between locations, not size of the area.

---

**Three distinct concepts:**

| Concept | What it is | Examples |
|---------|-----------|----------|
| **Location** | Node in graph, a destination | Frozen Creek, Rocky Ridge, Bear Cave |
| **Feature** | Capability at a location, enables actions | ForageFeature, WaterFeature, ClimbableFeature |
| **Structure** | Built feature, persists | Lean-to, cache, fire pit |

The tall pine is a feature (enables scouting) at Rocky Ridge, not a separate location. You don't travel to the tree - you climb it while at the ridge.

---

**Two types of shelter:**

| Type | What it is | When it applies | Examples |
|------|-----------|-----------------|----------|
| **Environmental** | The location itself provides protection | Always while at location | Cave walls, forest canopy, ravine, dense thicket |
| **Structural** | Something built/occupied at a location | Only during stationary activities | Lean-to, tent, overhang you're under |

Activity determines structural shelter:

| Activity | Environmental | Structural |
|----------|--------------|------------|
| Resting | ✓ | ✓ |
| Crafting | ✓ | ✓ |
| Foraging | ✓ | ✗ |
| Hunting | ✓ | ✗ |

A cave is environmental - you're inside it, so protection always applies. But caves have limited activities (can't forage inside, need light to work). Trade-off: great shelter, but every supply run is an expedition.

---

**New location properties:**

| Property | What it does | Gameplay impact |
|----------|-------------|-----------------|
| **Visibility** (0-1) | How far you can see / be seen | Scouting, threat detection, hunting, exposure to predators |
| **FootingHazard** (0-1) | Injury risk per traverse | Sprain chance, forces careful travel decisions |
| **Darkness** (bool) | Requires light to work | Caves cost torch/fire resources |

Visibility is the big one. It creates the ridge vs. thicket tradeoff - good sightlines mean you're also visible. High visibility: spot threats early, prey spots you, predators spot you. Low visibility: ambush prey, get ambushed, can't scout.

---

**Location families (~100 total, 40-50 per run):**

**Water**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Frozen Creek | Reliable, seasonal | Water (frozen state varies), moderate visibility |
| Spring Hollow | Never freezes, sheltered | Water (reliable), low visibility, environmental shelter |
| Beaver Pond | Abundant, exposed | Water, high visibility, waterfowl, wood from dam |
| Marsh Edge | Rich but treacherous | Water, high footing hazard, slow travel, reeds/cattails |
| Creek Falls | Loud, fish | Water, fish, sound covers approach, slippery |
| Meltwater Pool | Seasonal, temporary | Water (spring only), exposed |

**Forest**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Dense Thicket | Hidden, slow | Low visibility, environmental shelter, slow travel, small game |
| Open Pines | Fast, exposed | High visibility, fast travel, larger game sightlines |
| Birch Stand | Tinder heaven | Abundant tinder/bark, moderate visibility |
| Deadfall Grove | Fuel bonanza, dangerous | Abundant fuel, high footing hazard |
| Ancient Grove | Quiet, sheltered | Environmental shelter, sparse undergrowth, rare finds |
| Burnt Stand | Charcoal, exposed | Easy charcoal, high visibility, no canopy |
| Young Growth | Dense, limited | Very low visibility, little fuel, small game |

**Rock/High Ground**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Rocky Ridge | Scout, exposed | Very high visibility, high exposure, wind |
| Granite Outcrop | Toolstone | Stone source, moderate climb |
| Flint Seam | Quality stone, hard reach | Best toolstone, steep (footing hazard), limited |
| Boulder Field | Cover, treacherous | Moderate visibility, high footing hazard, hiding spots |
| Scree Slope | Fast down, slow up | Directional travel time, high footing hazard |
| Cliff Face | Impassable, resources | Blocks travel, bird eggs, overlook |

**Shelter Locations**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Bear Cave | Best shelter, occupied? | Excellent environmental shelter, dark, tension (occupied?) |
| Shallow Cave | Good shelter, safe | Good environmental shelter, dark, limited depth |
| Rock Overhang | Fire-friendly | Moderate environmental shelter, not dark, good for camp |
| Root Hollow | Hidden, cramped | Good shelter, very low visibility, single-person |

**Hunting/Animals**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Deer Meadow | Grazing, exposed | High visibility (both ways), dawn/dusk activity |
| Game Trail | Convergence, risky | Predictable movement, also predator highway |
| Wolf Den | Extreme risk/reward | Bones, scraps, pups in season, triggers tension |
| Rabbit Warren | Small game, reliable | Consistent small game, low reward per catch |

**Special**
| Location | Character | Key Properties |
|----------|-----------|----------------|
| Old Campsite | Salvage, story | Harvestables (salvage), narrative hook |
| Bone Pile | Materials, ominous | Bone source, predator sign, tension |
| Frozen Waterfall | Landmark, ice | Climbable in some conditions, dangerous |
| Lookout Pine | Scout without full exposure | Climbable feature, fall risk, visibility boost |

---

**Per-run randomization:**

**Fixed (authored):**
- Name
- Core description
- Primary feature type (Frozen Creek always has water)
- Base terrain, visibility, footing hazard
- What makes it *this place*

**Randomized per run:**
- Position in graph / distance from start
- Which locations appear (subset of 100)
- Abundance levels
- Secondary features (berry bushes this season?)
- Animal presence (wolf territory this run?)
- State (creek frozen solid vs. flowing, cave occupied vs. empty)
- Narrative details (fresh tracks, old bones, collapsed shelter)

Same location, different story each run. Player recognizes Frozen Creek but discovers it's frozen solid, has wolf sign, and someone's old camp is half-buried nearby.

---

**How players experience this (not stat dumps):**

**Names carry meaning**
"Deadfall Grove" teaches before you see stats. Sprain ankle there, you think "yeah, that tracks."

**One-time discovery text**
> *Fallen trees lie tangled across the slope, victims of some old storm. Fuel everywhere - but one wrong step could cost you.*

**Travel UI shows tags, not numbers**
```
1. Rocky Ridge [exposed, scout] ~30 min
2. Dense Thicket [hidden] ~15 min
3. Frozen Creek [water] ~20 min
4. Deadfall Grove [fuel, treacherous] ~25 min
```

**Events teach mechanics**
> *You spot movement on the ridge below - but from up here, you're silhouetted against the sky.*

> *Something moves in the thicket ahead. By the time you see it clearly, it's already close.*

**Consequences as curriculum**
- Sprain at Deadfall → learn footing hazard
- Wolf spots you at Deer Meadow → learn exposure
- Successful ambush in Thicket → learn concealment
- Didn't see bear coming in Thicket → learn visibility cuts both ways

---

**Event location requirements:**

Events check context and self-select:

```
"Spot movement in distance"
  Requires: Visibility > 0.6

"Lose footing"
  Requires: FootingHazard > 0.5, traveling or working

"Predator emerges from den"
  Requires: At or adjacent to den location

"Find abandoned camp"  
  Requires: Forest OR Clearing, not player's camp
  
"Something watches from the dark"
  Requires: Darkness = true, no active light
```

---

**Implementation priority:**

1. **Visibility property** - Biggest gameplay impact, enables scouting, affects hunting and threat detection
2. **FootingHazard property** - Creates terrain risk beyond time cost
3. **Darkness property** - Makes caves play differently
4. **WaterFeature** - Water as destination, frozen state
5. **Environmental vs structural shelter distinction** - Activity-aware temperature calculation
6. **Location tags for UI** - One-word character summary
7. **Discovery text** - First-visit paragraph per location
8. **Authored location pool** - Build out the 100