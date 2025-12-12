# Text Survival Architecture Diagram

## Target Folder Structure (Post-Restructure)

```
text_survival/
├── Core/                     # Game loop essentials
│   ├── Program.cs
│   ├── World.cs
│   └── Config.cs
│
├── Actions/                  # Player choices, menu flow
│
├── Actors/
│   ├── Actor.cs, IBuffable.cs, ICombatant.cs
│   ├── Player/               # Player + managers
│   └── NPCs/                 # NPC, Animal, NPCFactory
│
├── Bodies/                   # Physical form, damage, capacities
│
├── Combat/                   # CombatManager, Narrator, Utils
│
├── Crafting/                 # Recipes, crafting system
│
├── Effects/                  # Buffs/debuffs
│
├── Environments/
│   ├── Zone.cs, Location.cs, Weather.cs, etc.
│   ├── Features/             # Heat, Shelter, Forage, etc.
│   └── Factories/            # Location/Zone factories
│
├── Events/                   # Event system (expandable)
│
├── IO/                       # Input/Output abstraction
│
├── Items/                    # All item types
│
├── Magic/                    # Spells
│
├── Skills/                   # (renamed from Level/)
│
├── Survival/                 # SurvivalProcessor
│
├── UI/                       # Map, display
│
└── Utils/                    # Utilities
```

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              GAME LOOP (Program.cs)                             │
│                                                                                 │
│   while(alive) { action.Execute(context) → World.Update(minutes) → repeat }    │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           ACTION LAYER (Actions/)                               │
│  ┌──────────────────────────────────────────────────────────────────────────┐   │
│  │ ActionFactory.cs - Static factory organizing actions by category        │   │
│  │  ├── Common (MainMenu, Back, Return)                                    │   │
│  │  ├── Survival (Forage, Sleep, StartFire, AddFuel)                       │   │
│  │  ├── Movement (Move, Travel)                                            │   │
│  │  ├── Inventory (OpenInventory, UseItem, DropItem)                       │   │
│  │  ├── Combat (Attack, TargetedAttack, SpellCasting)                      │   │
│  │  ├── Crafting (OpenCraftingMenu, CraftItem)                             │   │
│  │  └── Describe (LookAround, CheckStats, Hunting)                         │   │
│  └──────────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────────┐   │
│  │ ActionBuilder.cs - Fluent builder for creating actions                  │   │
│  │  .Named() → .When() → .Do() → .ThenShow()/.ThenReturn() → .Build()      │   │
│  └──────────────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────────────┐   │
│  │ GameContext.cs - Passed to all action lambdas                           │   │
│  │  { player, currentLocation, CraftingManager, EngagedEnemy }             │   │
│  └──────────────────────────────────────────────────────────────────────────┘   │
│                                                                                 │
│  RESPONSIBILITY: Player choices, menu flow, triggering state changes           │
│  OWNS: Action availability logic, UI prompts, action chaining                  │
│  DOES NOT OWN: Survival calculations, damage, world state                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                    ┌───────────────────┼───────────────────┐
                    ▼                   ▼                   ▼
┌───────────────────────────┐ ┌─────────────────┐ ┌────────────────────────────┐
│    TIME SYSTEM (World.cs) │ │  IO LAYER (IO/) │ │   CRAFTING (Crafting/)     │
│                           │ │                 │ │                            │
│ Update(minutes):          │ │ Output.cs:      │ │ CraftingSystem.cs:         │
│  for each minute:         │ │  Write()        │ │  Craft(recipe)             │
│   Player.Update()         │ │  WriteLine()    │ │  CanCraft(recipe)          │
│   Zone.Update()           │ │  WriteColored() │ │                            │
│   Time += 1 minute        │ │                 │ │ RecipeBuilder.cs:          │
│                           │ │ Input.cs:       │ │  .WithPropertyReq()        │
│ OWNS: Game clock,         │ │  Read()         │ │  .RequiringSkill()         │
│ update orchestration      │ │  ReadInt()      │ │  .ResultingInItem()        │
│                           │ │  GetSelection() │ │                            │
│ DOES NOT OWN: What        │ │                 │ │ OWNS: Recipe validation,   │
│ happens during updates    │ │ OWNS: All I/O   │ │ material consumption       │
└───────────────────────────┘ └─────────────────┘ └────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          ACTOR LAYER (Actors/, Player.cs)                       │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                        Actor.cs (Abstract Base)                         │    │
│  │   { Name, Body, EffectRegistry, CombatManager, CurrentLocation }        │    │
│  │   Update() → EffectRegistry.Update() → Body.Update(1 min, context)      │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                          ▲                           ▲                          │
│            ┌─────────────┴─────────────┐   ┌────────┴────────┐                  │
│  ┌─────────┴─────────────────────────┐ │   │  NPC.cs         │                  │
│  │          Player.cs                │ │   │  { IsHostile,   │                  │
│  │  HAS Skills (SkillRegistry)       │ │   │    Loot }       │                  │
│  │  HAS Managers:                    │ │   │                 │                  │
│  │   - LocationManager               │ │   │  Animal.cs      │                  │
│  │   - InventoryManager              │ │   │  { BehaviorType │                  │
│  │   - StealthManager                │ │   │    State,       │                  │
│  │   - AmmunitionManager             │ │   │    Distance }   │                  │
│  │   - HuntingManager                │ │   │                 │                  │
│  └───────────────────────────────────┘ │   │  NO Skills      │                  │
│                                        │   │  Stats from Body│                  │
│                                        │   └─────────────────┘                  │
│  RESPONSIBILITY: Character state, inventory, skills, location tracking         │
│  OWNS: What a character HAS and CAN DO                                          │
│  DOES NOT OWN: How survival/damage is calculated (delegates to Body)            │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            BODY LAYER (Bodies/)                                 │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ Body.cs - Physical form and survival state                                │  │
│  │  Structure: List<BodyRegion> → Tissues (Skin/Muscle/Bone) → Organs        │  │
│  │  Composition: BodyFat (kg), Muscle (kg), Weight                           │  │
│  │  Survival: CalorieStore, Hydration, Energy, BodyTemperature               │  │
│  │                                                                           │  │
│  │  ENTRY POINTS (only ways to modify body):                                 │  │
│  │   - Damage(DamageInfo) → DamageProcessor                                  │  │
│  │   - Update(TimeSpan, SurvivalContext) → SurvivalProcessor                 │  │
│  │   - Consume(FoodItem) → direct stat modification                          │  │
│  │   - Heal(HealingInfo) → tissue regeneration                               │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌──────────────────────────────────┐  ┌──────────────────────────────────────┐ │
│  │ DamageProcessor.cs               │  │ CapacityCalculator.cs                │ │
│  │  PURE FUNCTION                   │  │  PURE FUNCTION                       │ │
│  │  DamageBody(body, info)          │  │  GetCapacities(body)                 │ │
│  │  → tissue penetration            │  │   1. Sum body part contributions     │ │
│  │  → organ damage                  │  │   2. Apply effect modifiers          │ │
│  │  → bleeding                      │  │   3. Apply survival stat penalties   │ │
│  └──────────────────────────────────┘  │   4. Apply cascading effects         │ │
│                                        └──────────────────────────────────────┘ │
│  ┌──────────────────────────────────┐  ┌──────────────────────────────────────┐ │
│  │ AbilityCalculator.cs             │  │ Capacities: Moving, Manipulation,   │ │
│  │  PURE FUNCTION                   │  │  Breathing, BloodPumping,            │ │
│  │  CalculateStrength(body)         │  │  Consciousness, Sight, Hearing,      │ │
│  │  CalculateSpeed(body)            │  │  Digestion                           │ │
│  │  CalculateVitality(body)         │  │                                      │ │
│  └──────────────────────────────────┘  └──────────────────────────────────────┘ │
│                                                                                 │
│  RESPONSIBILITY: Physical state, damage resolution, capacity calculation        │
│  OWNS: Body structure, tissue/organ health, body composition                    │
│  DOES NOT OWN: Survival rate calculations (delegates to SurvivalProcessor)      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         SURVIVAL LAYER (Survival/)                              │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ SurvivalProcessor.cs - PURE STATIC FUNCTION                               │  │
│  │                                                                           │  │
│  │ Process(SurvivalData, minutesElapsed, activeEffects)                      │  │
│  │   → SurvivalProcessorResult { Data, Effects, Messages }                   │  │
│  │                                                                           │  │
│  │ RATE CALCULATIONS:                                                        │  │
│  │  ┌─────────────────────────────────────────────────────────────────────┐  │  │
│  │  │ Energy:      -1 minute/minute (BASE_EXHAUSTION_RATE)                │  │  │
│  │  │ Hydration:   -2.78 mL/minute (BASE_DEHYDRATION_RATE)                │  │  │
│  │  │ Calories:    -BMR×activity/24/60 per minute                         │  │  │
│  │  │ Temperature: exponential heat transfer toward environment           │  │  │
│  │  └─────────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                           │  │
│  │ THRESHOLD TRIGGERS:                                                       │  │
│  │  ┌─────────────────────────────────────────────────────────────────────┐  │  │
│  │  │ temp < 97°F  → Shivering effect                                     │  │  │
│  │  │ temp < 95°F  → Hypothermia effect                                   │  │  │
│  │  │ temp < 89.6°F → Frostbite effects (extremities)                     │  │  │
│  │  │ temp > 99°F  → Sweating effect                                      │  │  │
│  │  │ temp > 100°F → Hyperthermia effect                                  │  │  │
│  │  └─────────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  RESPONSIBILITY: Calculate survival stat changes per time unit                  │
│  OWNS: Rate formulas, threshold definitions, effect generation                  │
│  DOES NOT OWN: Body state, effect storage (returns results to caller)           │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           EFFECT LAYER (Effects/)                               │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ EffectRegistry.cs - Per-actor effect management                           │  │
│  │  AddEffect(effect) - handles stacking/replacement                         │  │
│  │  Update() - severity changes, expiration, cleanup                         │  │
│  │  GetAll() - returns active effects for survival processing                │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ EffectBuilder.cs - Fluent API for effect creation                         │  │
│  │  .WithSeverity(0-1)                                                       │  │
│  │  .ReducesCapacity(capacity, amount)     → capacity modifier               │  │
│  │  .AffectsTemperature(hourlyChange)      → survival stat modifier          │  │
│  │  .CausesDehydration(mlPerMinute)        → survival stat modifier          │  │
│  │  .WithHourlySeverityChange(rate)        → self-resolving/worsening        │  │
│  │  .Targeting(bodyPartName)               → body-part specific              │  │
│  │  .AllowMultiple(bool)                   → stacking behavior               │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  RESPONSIBILITY: Buff/debuff state, modifier calculation                        │
│  OWNS: Active effect tracking, stacking rules, severity progression             │
│  DOES NOT OWN: When effects are created (SurvivalProcessor decides)             │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        ENVIRONMENT LAYER (Environments/)                        │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ Zone.cs - Geographic region                                               │  │
│  │  { Name, ZoneType, BaseTemperature, Weather, Locations[] }                │  │
│  │  Update() → cascades to all locations                                     │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                           │                                                     │
│                           ▼                                                     │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ Location.cs - Specific place within zone                                  │  │
│  │  { Name, Features[], Items[], Npcs[], Containers[] }                      │  │
│  │  GetTemperature() - composite calculation:                                │  │
│  │    zone weather → environment modifier → wind chill → sun warming →       │  │
│  │    precipitation → shelter insulation → heat source                       │  │
│  │  Update() → updates NPCs and HeatSourceFeature                            │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                           │                                                     │
│                           ▼                                                     │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │ LocationFeature Subclasses                                                │  │
│  │  EnvironmentFeature - terrain type (Forest, Cave, OpenPlain)              │  │
│  │  ShelterFeature - constructed protection (insulation, coverage)           │  │
│  │  HeatSourceFeature - FIRE SYSTEM                                          │  │
│  │    { IsActive, HasEmbers, FuelMassKg, FireAgeMinutes }                    │  │
│  │    GetEffectiveHeatOutput() - physics-based heat calculation              │  │
│  │    Update(TimeSpan) - fuel consumption, ember transition                  │  │
│  │  ForageFeature - probabilistic resource gathering                         │  │
│  │  HarvestableFeature - quantity-based visible resources                    │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  RESPONSIBILITY: World state, environmental conditions                          │
│  OWNS: Temperature context, resource availability, NPC placement                │
│  DOES NOT OWN: How actors respond to environment (actors decide via survival)   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Data Flow: Minute-by-Minute Update

```
  ACTION EXECUTES
        │
        ▼
  World.Update(N minutes)
        │
        │  ┌──────────────── FOR EACH MINUTE ────────────────┐
        │  │                                                  │
        │  │  Player.Update()                                 │
        │  │       │                                          │
        │  │       ├──► EffectRegistry.Update()               │
        │  │       │         │                                │
        │  │       │         ├── Update severity (per hour)   │
        │  │       │         ├── Run OnUpdate hooks           │
        │  │       │         └── Remove expired effects       │
        │  │       │                                          │
        │  │       └──► Body.Update(1 min, context)           │
        │  │                  │                               │
        │  │                  ├── BundleSurvivalData()        │
        │  │                  │    (gather current stats)     │
        │  │                  │                               │
        │  │                  ├── SurvivalProcessor.Process() │
        │  │                  │    ├── Apply rates            │
        │  │                  │    ├── Check thresholds       │
        │  │                  │    ├── Generate effects       │
        │  │                  │    └── Return results         │
        │  │                  │                               │
        │  │                  └── UpdateBodyBasedOnResult()   │
        │  │                       ├── Apply new effects      │
        │  │                       ├── ProcessConsequences()  │
        │  │                       │    (starvation, etc.)    │
        │  │                       └── Output messages        │
        │  │                                                  │
        │  │  Zone.Update()                                   │
        │  │       │                                          │
        │  │       └──► Location.Update() (each location)     │
        │  │                  │                               │
        │  │                  ├── NPC.Update() (each NPC)     │
        │  │                  └── HeatSource.Update()         │
        │  │                       (fuel consumption)         │
        │  │                                                  │
        │  │  Time += 1 minute                                │
        │  │                                                  │
        │  └──────────────────────────────────────────────────┘
        │
        ▼
  Output.FlushMessages() (deduplicate if multi-minute)
        │
        ▼
  RETURN TO ACTION (ThenShow → next menu)
```

## Decision Tree: "Where Does X Belong?"

```
Is it a PLAYER CHOICE?
  └─► YES → ActionFactory (Actions/)
      - Menu item definitions
      - Availability conditions (.When)
      - Execution logic (.Do)
      - Menu chaining (.ThenShow)

Is it a CONTINUOUS RATE CHANGE?
  └─► YES → SurvivalProcessor (Survival/)
      - Add to rate calculations
      - Define threshold triggers
      - Generate appropriate effects

Is it a BUFF/DEBUFF with duration?
  └─► YES → EffectBuilder (Effects/)
      - Define severity curve
      - Capacity/survival modifiers
      - Stacking behavior

Is it a PHYSICAL STRUCTURE?
  └─► YES → Environment hierarchy
      - Zone: large geographic regions
      - Location: specific places
      - Feature: interactive elements at location

Is it CHARACTER STATE?
  └─► YES → Actor/Player composition
      - Body: physical form, survival stats
      - EffectRegistry: active effects
      - Managers: inventory, skills, location

Is it DAMAGE or HEALING?
  └─► YES → Body entry points ONLY
      - Damage(DamageInfo) → DamageProcessor
      - Heal(HealingInfo) → tissue regeneration
      - NEVER modify body parts directly
```

## Where New Features Would Go

```
1. FIRE MARGIN AWARENESS
   ├── Calculation: Survival/SurvivalMarginCalculator.cs (new pure function)
   ├── Display: ActionFactory.cs (MainMenu shows margin)
   └── Check: ActionBuilderExtensions.cs (.RequiresFireMargin(minutes))

2. ACTION TIME VARIANCE
   ├── Data: ActionBuilder.cs (add MinTime/MaxTime properties)
   ├── Roll: DynamicAction.cs (roll duration on execute)
   └── Display: ActionFactory.cs (show "15-30 min" instead of "15 min")

3. EVENT INTERRUPTS
   ├── Definition: Events/ (new namespace)
   │   ├── Event.cs (base class)
   │   ├── EventBuilder.cs (fluent API)
   │   └── EventRegistry.cs (probability weights, context filters)
   ├── Trigger: World.Update() (check for events each minute)
   └── Response: Events return IGameAction for player choice

4. ACTION TIERS
   └── ActionFactory.cs (multiple versions per activity)
       - Forage() → ForageSafe(), ForageNormal(), ForageRisky()
       - Each with different time/yield/event-probability

5. SURVIVAL MARGIN DISPLAY
   ├── Calculation: Survival/SurvivalMarginCalculator.cs
   │   - Time until each threat kills you
   │   - Minimum = overall margin
   └── Display: ActionFactory.MainMenu() (show margin prominently)
```

## Current Boundaries Summary

| Layer | Owns | Does NOT Own |
|-------|------|--------------|
| **Actions** | Player choices, menu flow, UI | Calculations, state |
| **World** | Time, update orchestration | What happens during updates |
| **Actor/Player** | Character state, managers | How stats are calculated |
| **Body** | Physical form, entry points | Rate formulas |
| **Survival** | Rate calculations, thresholds | Body state storage |
| **Effects** | Active buffs, modifiers | When to create effects |
| **Environment** | World structure, temperature | Actor responses |
