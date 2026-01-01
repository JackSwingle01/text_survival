# NPC AI System Integration Analysis

## Summary

The NPC AI pseudocode integrates **remarkably well** with the existing Text Survival architecture. The codebase already has excellent precedents (Herd behavior system, Actor base class, Strategy patterns) that align closely with the NPC design.

---

## BLOCKERS: Issues Requiring Significant Work

### 1. **Survival Context is Player-Centric** (Major Refactor)

`GameContext.GetSurvivalContext()` at line 248 is tightly coupled to the player:

```csharp
// Current implementation references:
double clothingInsulation = Inventory.TotalInsulation;     // Player's inventory
var wetEffect = player.EffectRegistry.GetEffectsByKind();  // Player's effects
overheadCover = CurrentLocation.OverheadCoverLevel;        // Player's location
```

**Blocker**: NPCs need their own survival context based on:
- Their inventory (different equipment)
- Their location (may not be where player is)
- Their proximity to fire (may be different)

**Fix Required**: Refactor to `GetSurvivalContext(Actor actor, Inventory inventory, Location location)` or create parallel NPC version.

---

### 2. **Body Threshold Properties Missing** (Medium)

The pseudocode relies on:
```
npc.Body.Temperature.IsCritical
npc.Body.Hydration.IsLow
npc.Body.Energy.IsCritical
```

**Current State**:
- `Body.IsTired` exists (line 55)
- Temperature thresholds are in `SurvivalProcessor` as private constants
- No `IsCritical`/`IsLow` properties exposed on Body

**Fix Required**: Add threshold properties to Body or create a `BodyThresholds` helper class.

---

### 3. **Feature Interaction Conflicts** (Design Decision)

What happens when player and NPC both forage the same location?

- `ForageFeature` tracks `NumberOfHoursForaged` (depletion)
- If NPC forages while player is away, returns will be lower
- Herds already compete via `ForageFeature.Graze()` method

**Questions**:
- Do NPCs share depletion pool with player? (Realistic but frustrating)
- Do NPCs have separate depletion tracking? (Gamey but fair)
- Do NPCs only forage locations player isn't using?

---

### 4. **Time Synchronization** (Performance)

When player takes a 60-minute action, `GameContext.Update()` loops 60 times:
```csharp
for (int minute = 0; minute < targetMinutes; minute++)
{
    player.Update(1, context);
    Herds.Update(1, this);  // Already here
    // NPCs.Update(1, this); // Would add here
}
```

**Concern**: Each NPC runs full decision tree every minute. With 3+ NPCs, this could slow the game.

**Mitigation Options**:
- Batch NPC updates (every 5 minutes)
- Simplify in-progress action ticks (only full decision tree when action completes)
- Cache decision results

---

### 5. **UI/Display Integration** (Unknown Scope)

The pseudocode generates rejection text:
```
"{npc.Name} looks at you, then back at the fire. Too cold."
"{npc.Name} is busy gathering {action.Resource}."
```

**Current UI**: `WebIO` and `WebFrame` are player-centric. NPC status would need:
- Narrative messages during play
- NPC status panel (what they're doing)
- Suggestion interface (command them)

**Unknown**: How intrusive would UI changes be?

---

### 6. **Suggestion/Command System** (New System)

Player giving commands to NPCs is entirely new:
- No existing pattern for player→NPC commands
- Need input handling (`[1] Come with me [2] Stay here [3] Go gather fuel`)
- Need response display
- Need trust-based acceptance/rejection

---

### 7. **Combat Coordination** (Medium)

`CombatActor.CreateAlly()` exists but:
- Current combat is 1v1 focused (player vs animal)
- Multiple combatants need targeting logic
- Who does the wolf attack - player or NPC?
- How do allies coordinate actions?

---

## Integration Categories

### Perfect Alignments (Use As-Is)

| System | File | How NPCs Use It |
|--------|------|-----------------|
| **Actor base class** | `Actors/Actor.cs` | NPCs extend Actor, get Body, EffectRegistry, combat stats for free |
| **Inventory** | `Items/Inventory.cs` | `HasFood`, `HasFuel`, `HasWeapon`, equipment slots work directly |
| **HerdRegistry pattern** | `Actors/Animals/HerdRegistry.cs` | Template for `NPCRegistry` - same update loop integration |
| **GameMap** | `Environments/Grid/GameMap.cs` | Grid movement, `GetTravelOptions()`, visibility system |
| **IWorkStrategy** | `Actions/Expeditions/WorkStrategies/` | `ForageStrategy`, `ChoppingStrategy` execute work for NPCs |
| **Location Features** | `Environments/Features/` | Fire, forage, cache, carcass - NPCs interact identically |
| **CombatActor.CreateAlly()** | `Combat/CombatActor.cs:91` | Placeholder for NPC allies already exists |

### Minor Adaptations Needed

| System | Change Required |
|--------|-----------------|
| **FireHandler** | Add non-interactive `NPCStartFire()` method (no UI prompts) |
| **NeedCraftingSystem** | Add `GetBestCraftableFor(need, inventory)` helper |
| **GameContext.Update()** | Add `NPCs.Update()` call after `Herds.Update()` (~line 469) |
| **CombatState** | Track ally actors, coordinate targeting |

### New Systems Required

| System | Description | Complexity |
|--------|-------------|------------|
| **NPC class** | Extends Actor with Personality, Memory, Relationships, CurrentAction | Medium |
| **NPCBehavior** | Priority-based decision engine (the pseudocode core) | High |
| **NPCMemory** | Bidirectional resource↔location lookup | Low |
| **RelationshipState** | Trust/Respect/Familiarity tracking | Low |
| **Suggestion system** | Player command interface with rejection text | Medium |
| **NPCCombatBehavior** | Ally combat AI beyond animal behavior | Medium |

---

## Pseudocode → Codebase Mapping

### Body Stats (Critical/Low Checks)

```
Pseudocode                     Existing Code
─────────────────────────────────────────────────────
npc.Body.Temperature.IsCritical → Body has temperature, needs IsCritical threshold
npc.Body.Hydration.IsCritical   → _hydration property exists, add threshold check
npc.Body.Energy.IsCritical      → _energy property exists, add threshold check
npc.Body.Calories.IsCritical    → _calorieStore exists, add threshold check
```

**Note**: Body tracks these stats but threshold checks (`IsCritical`, `IsLow`) need to be added or exposed. Currently survival effects trigger at 30% thresholds in `SurvivalProcessor`.

### Inventory Checks

```
Pseudocode                     Existing Code
─────────────────────────────────────────────────────
npc.Inventory.Has(Water)       → inventory.HasWater (exists)
npc.Inventory.Has(Fuel)        → inventory.HasFuel (exists)
npc.Inventory.Has(Food)        → inventory.HasFood (exists)
npc.Inventory.Has(Ignition)    → inventory.HasFirestarter (exists)
npc.HasEquipped(Weapon)        → inventory.Weapon != null (exists)
npc.HasEquipped(slot)          → inventory.Equipment[slot] != null (exists)
```

### Fire Interaction

```
Pseudocode                     Existing Code
─────────────────────────────────────────────────────
campFire.IsLit                 → HeatSourceFeature.IsActive
campFire.NeedsFuel             → fire.HoursRemaining < threshold
TEND_FIRE(npc, fire)           → fire.AddFuel(weight, type)
START_FIRE(npc, fire)          → FireHandler (needs non-UI variant)
```

### Location/Movement

```
Pseudocode                     Existing Code
─────────────────────────────────────────────────────
npc.Location                   → GridPosition tracked on NPC (like Herd)
location.HasResource(type)     → location.GetFeature<ForageFeature>() checks
context.Map.GetAdjacentTiles() → GameMap.GetTravelOptions() equivalent
MOVE_TO(npc, target)           → Update NPC.Position, time cost
```

### Work Execution

```
Pseudocode                     Existing Code
─────────────────────────────────────────────────────
WORK_FOR_RESOURCE(forage)      → ForageStrategy.Execute()
WORK_FOR_RESOURCE(chop)        → ChoppingStrategy.Execute()
WORK_FOR_RESOURCE(hunt)        → HuntStrategy.Execute()
```

---

## Proposed File Structure

```
Actors/
├── Actor.cs (existing - base class)
├── Player/
│   └── Player.cs (existing)
├── Animals/
│   ├── Animal.cs (existing)
│   ├── Herd.cs (existing - pattern to follow)
│   └── HerdRegistry.cs (existing - pattern to follow)
└── NPC/
    ├── NPC.cs                    # Extends Actor
    ├── NPCPersonality.cs         # Boldness, Selfishness, Sociability
    ├── NPCMemory.cs              # Resource↔Location bidirectional map
    ├── NPCRegistry.cs            # Manages all NPCs, called from GameContext
    ├── RelationshipState.cs      # Trust, Respect, Familiarity
    ├── Suggestion.cs             # Player command with expiry
    └── Behaviors/
        ├── INPCBehavior.cs       # Strategy interface
        ├── SurvivalBehavior.cs   # The main AI loop from pseudocode
        └── NPCActionExecutor.cs  # Executes actions (fire, forage, move)
```

---

## Game Loop Integration

Current flow in `GameContext.UpdateInternal()`:

```
1. GetSurvivalContext()
2. player.Update()           ← Player survival ticks
3. Weather.Update()
4. Locations.Update()        ← Features tick (fire burns, etc.)
5. Tensions.Update()
6. Herds.Update()            ← Animal AI runs here
7. [NEW] NPCs.Update()       ← NPC AI would run here
8. Event check
```

NPCs update **after** herds so they can react to animal movements.

---

## Key Design Decisions

### 1. Shared vs Separate Inventories
- **Recommendation**: NPCs have their own `Inventory` instance
- Camp storage (`CacheFeature`) is shared
- Transfer items via existing `CombineWithCapacity()` pattern

### 2. Time Passage
- NPCs tick every game minute (like herds)
- Long actions (foraging, crafting) consume NPC's action slot
- Time costs apply to NPC, not player

### 3. Combat
- Use existing `CombatActor.CreateAlly()`
- Create `NPCCombatBehavior` implementing `IAnimalCombatBehavior`
- NPCs can use weapons, target body parts, use defensive actions

### 4. Following Player
- When `npc.Following == player`, NPC moves toward player position
- Stays within 1-2 tiles unless commanded elsewhere
- Suggestion system overrides following behavior

---

## Effort Estimate

| Phase | Work Items | Scope |
|-------|------------|-------|
| **Phase 1: Foundation** | NPC class, NPCRegistry, GameContext integration | ~500 lines |
| **Phase 2: Basic AI** | Priority loop, warmth tree, GET_IT loop | ~800 lines |
| **Phase 3: Work/Craft** | Feature interaction, crafting decisions | ~400 lines |
| **Phase 4: Social** | Relationships, suggestions, rejection text | ~600 lines |
| **Phase 5: Combat** | NPCCombatBehavior, ally coordination | ~400 lines |
| **Phase 6: Polish** | Memory system, idle behaviors, following | ~300 lines |

**Total**: ~3000 lines of new code, minimal changes to existing systems.

---

## Open Questions

1. **NPC spawning**: Where/when do NPCs appear? Events? Fixed locations? Random encounters?

2. **Player control**: Can player give direct commands (go here, do this) or only suggestions?

3. **NPC death**: Permadeath? Respawn? Story implications?

4. **Multiple NPCs**: Can player have multiple companions? Camp population?

5. **Resource sharing**: Do NPCs eat from camp stores? Own supplies only?

6. **UI display**: How are NPC actions/status shown to player?

---

## Blocker Severity Summary

| Blocker | Severity | Effort | Can Defer? |
|---------|----------|--------|------------|
| 1. Survival Context refactor | **High** | Medium | No - NPCs can't survive without it |
| 2. Body threshold properties | Medium | Low | No - core to AI decision loop |
| 3. Feature conflicts | Medium | Low | Yes - can use same pool initially |
| 4. Performance | Low | Medium | Yes - optimize later |
| 5. UI integration | Medium | High | Partially - narrative works, panels later |
| 6. Command system | Medium | High | Yes - NPCs can be autonomous first |
| 7. Combat coordination | Medium | Medium | Yes - solo combat works first |

---

## Recommendation

**The system is feasible** but requires preparatory refactoring before the AI logic can be implemented.

### Must Fix First (Pre-requisites)
1. Refactor `GetSurvivalContext()` to accept Actor/Inventory/Location parameters
2. Add `IsCritical`/`IsLow` threshold properties to Body or create helper

### Can Build Incrementally
- Start with autonomous NPCs (no commands)
- Single NPC first, then scale
- Narrative messages only, defer status UI
- Same feature depletion pool (realistic)

### Defer Until Needed
- Performance optimization
- Full command interface
- Multi-combatant coordination


---

# ORIGINAL PLAN:

## NPC AI System - Complete Pseudocode

---

## Data Structures

```
Npc:
    // Identity
    Name: string
    
    // Fixed at creation
    Personality:
        Boldness: float 0-1        // risk tolerance, explore vs suffer
        Selfishness: float 0-1     // self vs group weighting
        Sociability: float 0-1     // desire for proximity/interaction
    
    // Dynamic (uses existing Body system)
    Body:
        Temperature, Hydration, Energy, Calories
        Health, Capacities
    
    // Inventory (uses existing system)
    Inventory: list<Item>
    
    // Position (uses existing system)
    Location: Location
    
    // Relationships
    Relationships: Dictionary<Actor, Relationship>
    
    // Memory
    ResourceMemory: ResourceMemory
    
    // Current state
    CurrentAction: NpcAction or null
    PendingSuggestion: Suggestion or null
    Following: Actor or null


Relationship:
    Familiarity: float 0-1      // have we interacted?
    Trust: float -1 to 1        // will they hurt me?
    Respect: float -1 to 1      // are they competent?


ResourceMemory:
    // Bidirectional lookup
    ResourceLocations: Dictionary<ResourceType, HashSet<Location>>
    LocationResources: Dictionary<Location, HashSet<ResourceType>>


Suggestion:
    From: Actor
    Target: object              // Location, Animal, Fire, Item, Actor
    TickCreated: int
```

---

## Constants

```
PRIORITY:
    CRITICAL = 1
    NEEDS = 2
    WORK = 3
    WANTS = 4
    IDLE = 5

TRUST_THRESHOLDS:
    HOSTILE = -0.3
    STRANGER = 0.0
    ACQUAINTANCE = 0.3
    TRUSTED = 0.5
    BONDED = 0.7

STOCKPILE:
    DAYS_PER_RESIDENT = 2
    FUEL_HOURS_PER_DAY = 24
    CALORIES_PER_DAY = 2000
    WATER_LITERS_PER_DAY = 3

SLEEP:
    CHUNK_DURATION_MINUTES = 60
    MIN_FIRE_RUNWAY_MINUTES = 120
    FIRE_TOPOFF_THRESHOLD_MINUTES = 60

SUGGESTION:
    EXPIRY_TICKS = 60           // 1 hour game time

RANDOMNESS:
    CRAFT_SKIP_CHANCE = 0.3
    STOCKPILE_SHUFFLE = true
```

---

## Main Loop

```
NPC_TICK(npc, context):
    
    // =====================
    // THREAT INTERRUPT
    // =====================
    
    threats = npc.Perception.GetHostilesInSight()
    if threats.Any(t => t.IsThreatening(npc)):
        RESPOND_TO_THREAT(npc, threats.MostDangerous())
        return
    
    // =====================
    // GET SUGGESTION PRIORITY
    // =====================
    
    followPriority = GET_FOLLOW_PRIORITY(npc, npc.PendingSuggestion?.From)
    
    // =====================
    // PRIORITY 1: CRITICAL NEEDS
    // =====================
    
    if npc.Body.Temperature.IsCritical:
        WARMTH_TREE(npc, context)
        return
    
    if npc.Body.Hydration.IsCritical:
        if npc.Inventory.Has(Water):
            DRINK(npc)
        else:
            GET_IT(npc, Water, context)
        return
    
    if npc.Body.Energy.IsCritical:
        EMERGENCY_REST(npc, context)
        return
    
    if npc.Body.Calories.IsCritical:
        if npc.Inventory.Has(Food):
            EAT(npc)
        else:
            GET_IT(npc, Food, context)
        return
    
    // =====================
    // CHECK SUGGESTION AT PRIORITY 2
    // =====================
    
    if followPriority >= PRIORITY.NEEDS:
        if TRY_EXECUTE_SUGGESTION(npc, context):
            return
    
    // =====================
    // PRIORITY 2: REGULAR NEEDS
    // =====================
    
    if npc.Body.Temperature.IsLow:
        WARMTH_TREE(npc, context)
        return
    
    if npc.Body.Hydration.IsLow:
        if npc.Inventory.Has(Water):
            DRINK(npc)
        else:
            GET_IT(npc, Water, context)
        return
    
    if npc.Body.Energy.IsLow and CAN_SLEEP(npc, context):
        SLEEP(npc, context)
        return
    
    if npc.Body.Calories.IsLow:
        if npc.Inventory.Has(Food):
            EAT(npc)
        else:
            GET_IT(npc, Food, context)
        return
    
    // =====================
    // CHECK SUGGESTION AT PRIORITY 3
    // =====================
    
    if followPriority >= PRIORITY.WORK:
        if TRY_EXECUTE_SUGGESTION(npc, context):
            return
    
    // =====================
    // PRIORITY 3: NECESSARY WORK
    // =====================
    
    if NECESSARY_WORK(npc, context):
        return
    
    // =====================
    // CHECK SUGGESTION AT PRIORITY 4
    // =====================
    
    if followPriority >= PRIORITY.WANTS:
        if TRY_EXECUTE_SUGGESTION(npc, context):
            return
    
    // =====================
    // PRIORITY 4: WANTS
    // =====================
    
    recipe = GET_CRAFT_DESIRE(npc, context)
    if recipe != null:
        // Random chance to skip crafting
        if Random.Float() > RANDOMNESS.CRAFT_SKIP_CHANCE:
            CRAFT_LOOP(npc, recipe, context)
            return
    
    // =====================
    // CHECK SUGGESTION AT PRIORITY 5
    // =====================
    
    if followPriority >= PRIORITY.IDLE:
        if TRY_EXECUTE_SUGGESTION(npc, context):
            return
    
    // =====================
    // PRIORITY 5: IDLE
    // =====================
    
    IDLE_BEHAVIOR(npc, context)
```

---

## Warmth Tree

```
WARMTH_TREE(npc, context):
    
    campFire = context.Camp.Fire
    
    // At camp with fire?
    if npc.Location == context.Camp.Location and campFire != null:
        
        if campFire.IsLit:
            if campFire.NeedsFuel and npc.Inventory.Has(Fuel):
                TEND_FIRE(npc, campFire)
                return
            else:
                // Warming - just wait
                REST(npc, context)
                return
        
        else:
            // Fire is out
            if npc.Inventory.Has(Fuel) and npc.Inventory.Has(Ignition):
                START_FIRE(npc, campFire)
                return
            else if not npc.Inventory.Has(Fuel):
                GET_IT(npc, Fuel, context)
                return
            else:
                // No ignition source - wait for someone else or suffer
                REST(npc, context)
                return
    
    // Not at camp - do we know where fire is?
    if campFire != null and npc.ResourceMemory.Knows(CampLocation):
        MOVE_TO(npc, context.Camp.Location)
        return
    
    // No known fire
    if npc.CanBuildFire():
        BUILD_FIRE(npc, npc.Location)
        return
    
    // Can't do anything - suffer
    REST(npc, context)  // Suffer in place
```

---

## Get It Loop

```
GET_IT(npc, resourceType, context):
    
    // Already have it?
    if npc.Inventory.Has(resourceType):
        return SUCCESS
    
    // Current location has it?
    if npc.Location.HasResource(resourceType):
        WORK_FOR_RESOURCE(npc, resourceType, npc.Location)
        return IN_PROGRESS
    
    // Adjacent tile has it?
    adjacent = context.Map.GetAdjacentTiles(npc.Location)
    adjacent = adjacent.Where(t => t.HasResource(resourceType))
    if adjacent.Any():
        target = adjacent.PickRandom()  // Randomize for variety
        MOVE_TO(npc, target)
        return IN_PROGRESS
    
    // Visible tile has it?
    visible = npc.Perception.GetVisibleTiles()
    visible = visible.Where(t => t.HasResource(resourceType))
    if visible.Any():
        target = visible.OrderBy(t => t.DistanceTo(npc.Location)).First()
        MOVE_TO(npc, target)
        return IN_PROGRESS
    
    // Memory knows location?
    known = npc.ResourceMemory.GetKnownLocations(resourceType)
    if known.Any():
        target = known.OrderBy(t => t.DistanceTo(npc.Location)).First()
        MOVE_TO(npc, target)
        return IN_PROGRESS
    
    // Nothing known - explore or suffer
    if npc.Personality.Boldness > 0.5 or IS_DESPERATE(npc):
        EXPLORE(npc, context)
        return IN_PROGRESS
    else:
        // Too timid to explore, wait and hope
        REST(npc, context)
        return FAILED


WORK_FOR_RESOURCE(npc, resourceType, location):
    
    workType = match resourceType:
        Food     => Forage
        Water    => GetWater
        Fuel     => Chop
        Material => Harvest
        _        => Forage
    
    npc.CurrentAction = new WorkAction(workType, location)
```

---

## Necessary Work

```
NECESSARY_WORK(npc, context):
    
    camp = context.Camp
    
    // Tend fire if needed (priority over gathering)
    if camp.Fire != null and camp.Fire.IsLit:
        if camp.Fire.NeedsFuel and npc.Inventory.Has(Fuel):
            if npc.Location == camp.Location:
                TEND_FIRE(npc, camp.Fire)
                return true
            else:
                MOVE_TO(npc, camp.Location)
                return true
    
    // Build stockpile needs list
    needs = []
    if STOCKPILE_LOW(camp, Fuel):  needs.Add(Fuel)
    if STOCKPILE_LOW(camp, Water): needs.Add(Water)
    if STOCKPILE_LOW(camp, Food):  needs.Add(Food)
    
    if needs.IsEmpty():
        return false
    
    // Shuffle for variety
    if RANDOMNESS.STOCKPILE_SHUFFLE:
        needs = needs.Shuffle()
    
    // Do first need
    resourceType = needs.First()
    
    // If we have some, stash it first
    if npc.Inventory.Has(resourceType):
        if npc.Location == camp.Location:
            STASH(npc, resourceType, camp.Storage)
        else:
            MOVE_TO(npc, camp.Location)
        return true
    
    // Go get it
    GET_IT(npc, resourceType, context)
    return true


STOCKPILE_LOW(camp, resourceType):
    
    residents = camp.Residents.Count
    days = STOCKPILE.DAYS_PER_RESIDENT
    
    target = match resourceType:
        Fuel  => residents * days * STOCKPILE.FUEL_HOURS_PER_DAY * camp.Fire.FuelPerHour
        Water => residents * days * STOCKPILE.WATER_LITERS_PER_DAY
        Food  => residents * days * STOCKPILE.CALORIES_PER_DAY
    
    current = camp.Storage.GetAmount(resourceType)
    
    return current < target
```

---

## Craft Loop

```
GET_CRAFT_DESIRE(npc, context):
    
    // P1: Weapon
    if not npc.HasEquipped(Weapon):
        recipe = GET_CRAFTABLE(npc, context, RecipeCategory.Weapon)
        if recipe != null:
            return recipe
    
    // P2: Gear - fill empty slots
    for slot in [Head, Chest, Legs, Feet, Hands]:
        if not npc.HasEquipped(slot):
            recipe = GET_CRAFTABLE(npc, context, RecipeCategory.Gear, slot)
            if recipe != null:
                return recipe
    
    // P3: Upgrades
    for slot in [Weapon, Head, Chest, Legs, Feet, Hands]:
        current = npc.GetEquipped(slot)
        recipe = GET_CRAFTABLE_UPGRADE(npc, context, slot, current)
        if recipe != null:
            return recipe
    
    // P4: Tools, medicine, misc
    recipe = GET_CRAFTABLE(npc, context, RecipeCategory.Tools)
    if recipe != null:
        return recipe
    
    return null


GET_CRAFTABLE(npc, context, category, slot = null):
    
    recipes = RecipeRegistry.GetRecipes(category, slot)
    recipes = recipes.OrderByDescending(r => r.Value)
    
    for recipe in recipes:
        if CAN_CRAFT_OR_GATHER(npc, recipe, context):
            return recipe
    
    return null


CRAFT_LOOP(npc, recipe, context):
    
    // Have all materials?
    if npc.Inventory.HasAll(recipe.Materials):
        CRAFT(npc, recipe)
        return
    
    // Find missing material
    for material in recipe.Materials:
        if not npc.Inventory.Has(material):
            
            if material.IsRaw:
                // Go gather it
                GET_IT(npc, material.ResourceType, context)
                return
            
            if material.IsCrafted:
                // Need to craft the component first
                componentRecipe = RecipeRegistry.GetRecipe(material)
                CRAFT_LOOP(npc, componentRecipe, context)
                return
```

---

## Sleep System

```
CAN_SLEEP(npc, context):
    
    // Must be at camp
    if npc.Location != context.Camp.Location:
        return false
    
    // Not freezing
    if npc.Body.Temperature.IsCritical:
        return false
    
    // Fire has runway
    fire = context.Camp.Fire
    if fire == null or not fire.IsLit:
        return false
    if fire.BurnTimeRemainingMinutes < SLEEP.MIN_FIRE_RUNWAY_MINUTES:
        return false
    
    // No threats
    if npc.Perception.HostilesInSight.Any():
        return false
    
    return true


SLEEP(npc, context):
    
    npc.CurrentAction = new SleepAction(SLEEP.CHUNK_DURATION_MINUTES)


ON_WAKE(npc, context):
    
    fire = context.Camp.Fire
    
    // Top off fire if getting low
    if fire != null and fire.IsLit:
        if fire.BurnTimeRemainingMinutes < SLEEP.FIRE_TOPOFF_THRESHOLD_MINUTES:
            if npc.Inventory.Has(Fuel):
                TEND_FIRE(npc, fire)
                return
    
    // Check if should sleep more
    if CAN_SLEEP(npc, context) and npc.Body.Energy.Percent < 0.8:
        SLEEP(npc, context)
        return
    
    // Done sleeping, return to main loop
    npc.CurrentAction = null
```

---

## Idle Behavior

```
IDLE_BEHAVIOR(npc, context):
    
    // Night sleep preference
    if context.Time.IsNight and CAN_SLEEP(npc, context):
        if npc.Body.Energy.Percent < 0.8:
            SLEEP(npc, context)
            return
    
    // Check if trusted leader is leaving
    trustedActors = GET_ACTORS_WITH_TRUST(npc, TRUST_THRESHOLDS.TRUSTED)
    for actor in trustedActors:
        if actor.IsLeaving(context.Camp) and not actor.IsInCriticalNeed:
            FOLLOW(npc, actor)
            return
    
    // Weighted random idle action
    options = WeightedList:
        { REST_NEAR_FIRE,   50 }
        { SIT_AND_WATCH,    20 }
        { TEND_FIRE_ANYWAY, 15 }
        { WANDER_NEARBY,    10 }
        { CHECK_ON_OTHERS,   5 }
    
    if context.Time.IsNight and CAN_SLEEP(npc, context):
        options.Add(SLEEP, 40)
    
    action = options.PickWeighted()
    action(npc, context)


REST_NEAR_FIRE(npc, context):
    if npc.Location != context.Camp.Location:
        MOVE_TO(npc, context.Camp.Location)
    else:
        npc.CurrentAction = new RestAction()


SIT_AND_WATCH(npc, context):
    npc.CurrentAction = new WatchAction()  // Scan surroundings


TEND_FIRE_ANYWAY(npc, context):
    fire = context.Camp.Fire
    if fire != null and fire.IsLit and npc.Inventory.Has(Fuel):
        if npc.Location == context.Camp.Location:
            TEND_FIRE(npc, fire)
        else:
            MOVE_TO(npc, context.Camp.Location)
    else:
        REST_NEAR_FIRE(npc, context)


WANDER_NEARBY(npc, context):
    adjacent = context.Map.GetAdjacentTiles(context.Camp.Location)
    safe = adjacent.Where(t => not t.HasThreats())
    if safe.Any():
        target = safe.PickRandom()
        MOVE_TO(npc, target)
    else:
        REST_NEAR_FIRE(npc, context)


CHECK_ON_OTHERS(npc, context):
    others = context.Camp.Residents.Where(r => r != npc)
    if others.Any():
        target = others.PickRandom()
        MOVE_TO(npc, target.Location)
    else:
        REST_NEAR_FIRE(npc, context)
```

---

## Suggestion System

```
GET_FOLLOW_PRIORITY(npc, fromActor):
    
    if fromActor == null:
        return -1
    
    if not npc.Relationships.ContainsKey(fromActor):
        return PRIORITY.IDLE  // Stranger
    
    trust = npc.Relationships[fromActor].Trust
    
    if trust < TRUST_THRESHOLDS.HOSTILE:
        return -1                       // Won't follow
    if trust < TRUST_THRESHOLDS.ACQUAINTANCE:
        return PRIORITY.IDLE            // Only if nothing else
    if trust < TRUST_THRESHOLDS.TRUSTED:
        return PRIORITY.WANTS           // Skip crafting
    if trust < TRUST_THRESHOLDS.BONDED:
        return PRIORITY.WORK            // Skip stockpiling
    
    return PRIORITY.NEEDS               // Skip comfort needs


RECEIVE_SUGGESTION(npc, suggestion):
    
    trust = GET_TRUST(npc, suggestion.From)
    
    // Hostile - reject outright
    if trust < TRUST_THRESHOLDS.HOSTILE:
        return GENERATE_HOSTILE_REJECTION(npc)
    
    followPriority = GET_FOLLOW_PRIORITY(npc, suggestion.From)
    currentPriority = GET_CURRENT_ACTION_PRIORITY(npc)
    
    // Current action more important than relationship allows
    if currentPriority < followPriority:
        return GENERATE_REJECTION(npc, currentPriority, npc.CurrentAction)
    
    // Accept
    npc.PendingSuggestion = suggestion
    return GENERATE_ACKNOWLEDGMENT(npc, trust)


TRY_EXECUTE_SUGGESTION(npc, context):
    
    suggestion = npc.PendingSuggestion
    
    // No suggestion or expired
    if suggestion == null:
        return false
    if (context.CurrentTick - suggestion.TickCreated) > SUGGESTION.EXPIRY_TICKS:
        npc.PendingSuggestion = null
        return false
    
    // Execute it
    EXECUTE_SUGGESTION(npc, suggestion, context)
    npc.PendingSuggestion = null
    return true


EXECUTE_SUGGESTION(npc, suggestion, context):
    
    // Interrupt current action cleanly
    if npc.CurrentAction != null:
        npc.CurrentAction.Interrupt()
    
    target = suggestion.Target
    
    match target:
        
        Location loc:
            if loc == npc.Location:
                npc.CurrentAction = new StayAction()
            else:
                npc.CurrentAction = new GoToAction(loc)
                // Will infer work from location features on arrival
        
        Animal animal:
            if animal.IsPrey:
                npc.CurrentAction = new HuntAction(animal)
            else if npc.Personality.Boldness > 0.5:
                npc.CurrentAction = new FightAction(animal)
            else:
                npc.CurrentAction = new FleeAction(animal)
        
        Fire fire:
            npc.CurrentAction = new TendFireAction(fire)
        
        Item item:
            npc.CurrentAction = new RetrieveAction(item)
        
        Actor actor:
            if actor == suggestion.From:
                npc.CurrentAction = new FollowAction(actor)
                npc.Following = actor
            else if actor.Body.IsInjured:
                npc.CurrentAction = new AssistAction(actor)
            else:
                npc.CurrentAction = new ApproachAction(actor)
```

---

## Rejection / Acknowledgment Text

```
GENERATE_REJECTION(npc, priority, action):
    
    match priority:
        
        PRIORITY.CRITICAL:
            match action.Type:
                Warming    => "{npc.Name} is shivering badly. Won't leave the fire."
                Drinking   => "{npc.Name} is desperately thirsty."
                Resting    => "{npc.Name} can barely stand."
                Eating     => "{npc.Name} is too weak from hunger."
                _          => "{npc.Name} can't. Survival first."
        
        PRIORITY.NEEDS:
            match action.Type:
                Warming    => "{npc.Name} looks at you, then back at the fire. Too cold."
                Drinking   => "{npc.Name} gestures at their throat. Water first."
                Resting    => "{npc.Name} is exhausted. Needs rest."
                Eating     => "{npc.Name} shakes their head. Hungry."
                _          => "{npc.Name} has other needs right now."
        
        PRIORITY.WORK:
            match action.Type:
                TendFire   => "{npc.Name} points at the fire. It needs tending."
                Gathering  => "{npc.Name} is busy gathering {action.Resource}."
                Stashing   => "{npc.Name} is storing supplies."
                _          => "{npc.Name} is busy with camp work."
        
        PRIORITY.WANTS:
            if action.Type == Crafting:
                "{npc.Name} holds up the half-finished {action.Recipe.Name}. Later."
            else:
                "{npc.Name} is focused on their work."
        
        _:
            "{npc.Name} shakes their head."


GENERATE_HOSTILE_REJECTION(npc):
    options = [
        "{npc.Name} ignores you.",
        "{npc.Name} turns away.",
        "{npc.Name} glares at you.",
    ]
    return options.PickRandom()


GENERATE_ACKNOWLEDGMENT(npc, trust):
    
    if trust > TRUST_THRESHOLDS.BONDED:
        options = [
            "{npc.Name} nods and stands.",
            "{npc.Name} is already moving.",
            "{npc.Name} grabs their gear.",
        ]
    
    else if trust > TRUST_THRESHOLDS.TRUSTED:
        options = [
            "{npc.Name} looks at you. Nods.",
            "{npc.Name} hesitates, then stands.",
            "{npc.Name} sets down their work.",
        ]
    
    else:
        options = [
            "{npc.Name} shrugs. Follows.",
            "{npc.Name} looks around. Nothing else to do. Comes along.",
            "{npc.Name} watches you for a moment. Decides to follow.",
        ]
    
    return options.PickRandom()
```

---

## Memory System

```
ResourceMemory:
    ResourceLocations: Dictionary<ResourceType, HashSet<Location>>
    LocationResources: Dictionary<Location, HashSet<ResourceType>>


ON_TILE_OBSERVED(memory, location, resources):
    
    // Clear old data for this location
    if memory.LocationResources.Has(location):
        oldResources = memory.LocationResources[location]
        for r in oldResources:
            memory.ResourceLocations[r].Remove(location)
    
    // Set new data
    memory.LocationResources[location] = resources.ToHashSet()
    
    for r in resources:
        if not memory.ResourceLocations.Has(r):
            memory.ResourceLocations[r] = new HashSet()
        memory.ResourceLocations[r].Add(location)


ON_RESOURCE_DEPLETED(memory, location, resourceType):
    
    if memory.ResourceLocations.Has(resourceType):
        memory.ResourceLocations[resourceType].Remove(location)
    
    if memory.LocationResources.Has(location):
        memory.LocationResources[location].Remove(resourceType)


GET_KNOWN_LOCATIONS(memory, resourceType):
    
    if memory.ResourceLocations.Has(resourceType):
        return memory.ResourceLocations[resourceType].ToList()
    
    return []
```

---

## Threat Response

```
RESPOND_TO_THREAT(npc, threat):
    
    // Fight or flight based on boldness and capability
    
    canFight = npc.HasEquipped(Weapon) and not npc.Body.IsInjured
    fightChance = npc.Personality.Boldness
    
    if canFight:
        fightChance += 0.2
    
    if threat.IsMuchStronger(npc):
        fightChance -= 0.3
    
    if threat.IsMuchWeaker(npc):
        fightChance += 0.3
    
    fightChance = Clamp(fightChance, 0.1, 0.9)
    
    if Random.Float() < fightChance:
        npc.CurrentAction = new FightAction(threat)
    else:
        npc.CurrentAction = new FleeAction(threat)


IS_DESPERATE(npc):
    
    return npc.Body.Temperature.IsCritical
        or npc.Body.Hydration.IsCritical
        or npc.Body.Calories.IsCritical


EXPLORE(npc, context):
    
    // Find unexplored adjacent tile
    adjacent = context.Map.GetAdjacentTiles(npc.Location)
    unexplored = adjacent.Where(t => not npc.ResourceMemory.LocationResources.Has(t))
    
    if unexplored.Any():
        target = unexplored.PickRandom()
        MOVE_TO(npc, target)
        return
    
    // All adjacent explored - pick random direction
    if adjacent.Any():
        target = adjacent.PickRandom()
        MOVE_TO(npc, target)
```

---

## Relationship Updates (One Hook)

```
ON_ACTOR_AFFECTED(source, action, affected, effect, witnesses):
    
    for witness in witnesses:
        INTERPRET_EVENT(witness, source, action, affected, effect)


INTERPRET_EVENT(witness, source, action, affected, effect):
    
    // How much does witness care about affected?
    stake = CALCULATE_STAKE(witness, affected)
    
    // Was effect positive or negative?
    valence = effect.ImpactValue  // -1 to +1
    
    // Delta to relationship
    delta = stake * valence * action.Weight
    
    if not witness.Relationships.Has(source):
        witness.Relationships[source] = new Relationship()
    
    rel = witness.Relationships[source]
    
    if action.RevealsTrustworthiness:
        rel.Trust = Clamp(rel.Trust + delta, -1, 1)
    
    if action.RevealsCompetence:
        rel.Respect = Clamp(rel.Respect + delta, -1, 1)
    
    // Familiarity always increases with interaction
    rel.Familiarity = Min(rel.Familiarity + 0.05, 1)


CALCULATE_STAKE(witness, affected):
    
    if witness == affected:
        return 1.0
    
    if not witness.Relationships.Has(affected):
        return 0.0
    
    trust = witness.Relationships[affected].Trust
    
    if trust > 0.5:
        return 0.7      // Care about allies
    if trust > 0:
        return 0.3      // Mild interest in acquaintances
    if trust < -0.3:
        return -0.3     // Glad when enemies suffer
    
    return 0.0          // Strangers
```

---

## Helper Functions

```
GET_CURRENT_ACTION_PRIORITY(npc):
    
    if npc.CurrentAction == null:
        return PRIORITY.IDLE
    
    return npc.CurrentAction.Priority


GET_TRUST(npc, actor):
    
    if actor == null:
        return 0
    
    if not npc.Relationships.Has(actor):
        return 0
    
    return npc.Relationships[actor].Trust


GET_ACTORS_WITH_TRUST(npc, minTrust):
    
    result = []
    
    for actor, rel in npc.Relationships:
        if rel.Trust >= minTrust:
            result.Add(actor)
    
    return result
```

---

## Summary

```
CORE LOOP:
    Threat Check → Critical Needs → [Suggestion?] → Regular Needs → 
    [Suggestion?] → Necessary Work → [Suggestion?] → Wants → 
    [Suggestion?] → Idle

GET_IT LOOP:
    Have It? → At Location? → Adjacent? → Visible? → Memory? → 
    Bold/Desperate? → Explore : Suffer

SUGGESTION PRIORITY BY TRUST:
    Hostile     → Rejected
    Stranger    → Idle only (5)
    Acquaintance → Skip wants (4)
    Trusted     → Skip work (3)
    Bonded      → Skip needs (2)
    (Never skip critical)

RANDOMNESS:
    - Stockpile order shuffled
    - 30% chance to skip crafting
    - Weighted random idle behaviors
    - Soft thresholds via probability curves
```