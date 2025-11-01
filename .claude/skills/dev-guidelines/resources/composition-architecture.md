# Composition Architecture - Actor, Player, and NPC

Composition-over-inheritance design, Actor base class, Player/NPC distinction, manager pattern, and separation of concerns.

## Table of Contents

- [Composition Over Inheritance](#composition-over-inheritance)
- [Actor Base Class](#actor-base-class)
- [Player vs NPC - Critical Distinction](#player-vs-npc---critical-distinction)
- [Manager Pattern](#manager-pattern)
- [Player Managers](#player-managers)
- [NPC Simplicity](#npc-simplicity)
- [Composition Examples](#composition-examples)

---

## Composition Over Inheritance

**Core Principle**: Favor composition (has-a) over inheritance (is-a) for flexibility and maintainability.

**Why Composition**:
- **Flexibility**: Mix and match capabilities without deep hierarchies
- **Reusability**: Managers can be used independently
- **Testability**: Test managers in isolation
- **Clarity**: Explicit dependencies, no hidden behavior
- **Maintenance**: Change one manager without affecting others

**Example - Inheritance Hell (AVOIDED)**:
```csharp
// ❌ BAD: Deep inheritance hierarchy
Actor
  ├─ LivingActor (Body)
  │   ├─ MortalActor (Health)
  │   │   ├─ MovingActor (Location)
  │   │   │   ├─ InventoriedActor (Inventory)
  │   │   │   │   └─ SkilledActor (Skills) ← Player is here
  │   │   │   └─ SimpleMovingActor ← NPC is here?
  // Rigid, confusing, hard to maintain
```

**Example - Composition (USED)**:
```csharp
// ✅ GOOD: Flat hierarchy + composition
Actor (base)
  ├─ Player: Actor + Managers (Inventory, Location, Skills)
  └─ NPC: Actor (minimal, no managers)

// Flexible, clear, easy to maintain
```

---

## Actor Base Class

**Location**: `Actors/Actor.cs`

Base class for all game entities (Player, NPCs).

**Core Components** (Actor.cs:9-42):
```csharp
public abstract class Actor
{
    // Identity
    public string Name;

    // Core Systems (ALL actors have these)
    public Body Body { get; init; }
    public EffectRegistry EffectRegistry { get; init; }
    protected CombatManager combatManager { get; init; }

    // Location (can be overridden)
    public virtual Location? CurrentLocation { get; set; }

    // Combat
    public abstract Weapon ActiveWeapon { get; protected set; }
    public virtual void Attack(Actor target, string? bodyPart = null)
        => combatManager.Attack(target, bodyPart);

    // State
    public bool IsEngaged { get; set; }
    public bool IsAlive => !Body.IsDestroyed;

    // Update Cycle
    public virtual void Update()
    {
        EffectRegistry.Update();
        var context = new SurvivalContext { /* ... */ };
        Body.Update(TimeSpan.FromMinutes(1), context);
    }
}
```

**What Actor Provides**:
1. **Body** - Physical structure, health, capacities
2. **EffectRegistry** - Status effects (bleeding, poison, buffs)
3. **CombatManager** - Attack logic
4. **Name** - Identity
5. **Update()** - Per-minute update cycle

**What Actor Does NOT Provide**:
- Inventory (Player-specific via InventoryManager)
- Skills (Player-specific via SkillRegistry)
- Complex location tracking (Player-specific via LocationManager)
- Spells (Player-specific)

---

## Player vs NPC - Critical Distinction

**CRITICAL RULE**: Skills, complex managers, and progression systems are PLAYER-ONLY.

### Player Capabilities

**Player Has** (Player.cs:14-58):
```csharp
public class Player : Actor
{
    // MANAGERS (composition)
    private readonly LocationManager locationManager;
    public readonly InventoryManager inventoryManager;
    public readonly SkillRegistry Skills;  // ← PLAYER-ONLY

    // SPELLS (player-only)
    public readonly List<Spell> _spells;

    // Override for manager integration
    public override Location CurrentLocation
    {
        get => locationManager.CurrentLocation;
        set => locationManager.CurrentLocation = value;
    }

    public override Weapon ActiveWeapon
    {
        get => inventoryManager.Weapon;
        protected set => inventoryManager.Weapon = value;
    }
}
```

**Player-Only Features**:
- **Skills** - Level-based progression (Crafting, Combat, Survival)
- **Inventory Management** - Complex inventory with clothing, equipment
- **Location Management** - Zone tracking, location discovery
- **Spells** - Magic system (casting, mana, spell list)
- **Crafting** - Recipe knowledge, skill requirements
- **Experience** - Gain XP, level up skills

### NPC Capabilities

**NPC Has** (NPC.cs:7-48):
```csharp
public class Npc : Actor
{
    // Simple properties (NO managers)
    public string Description { get; set; }
    public bool IsFound { get; set; }
    public bool IsHostile { get; private set; }
    public override Weapon ActiveWeapon { get; protected set; }

    // Simple loot (NOT full inventory)
    public Container Loot { get; }

    // Basic stats from Body ONLY
    public double Health => Body.Health;
    public double MaxHealth => Body.MaxHealth;
}
```

**NPC Limitations**:
- **NO Skills** - Combat ability from Body stats only
- **NO Inventory** - Just a loot container
- **NO Spells** - Can't cast magic
- **NO Progression** - Stats are fixed at creation
- **NO Crafting** - Can't create items

**How NPCs Get Strength**:
NPCs derive power from their Body configuration:
```csharp
// Wolf is strong because of Body stats
var wolf = new Npc("Wolf", weapon, stats: new BodyCreationInfo
{
    BodyType = BodyType.Wolf,
    FatKg = 5.0,
    MuscleKg = 25.0  // High muscle = high damage, speed
});
```

---

## Manager Pattern

**Definition**: Manager classes encapsulate specific domains of functionality.

**Benefits**:
- **Separation of Concerns**: Each manager handles one responsibility
- **Testability**: Test managers independently
- **Flexibility**: Swap or modify managers without affecting Actor
- **Clarity**: Explicit dependencies

**Manager Structure**:
```csharp
public class SomeManager
{
    // Internal state
    private Data _data;

    // Constructor (inject dependencies)
    public SomeManager(dependencies...)
    {
        _data = ...;
    }

    // Public methods
    public void DoSomething() { /* ... */ }
    public Data GetData() => _data;
}
```

---

## Player Managers

Player uses three main managers:

### InventoryManager

**Responsibility**: Manage player's inventory, equipment, and clothing.

**Location**: `PlayerComponents/InventoryManager.cs`

**Key Methods**:
```csharp
public class InventoryManager
{
    public List<ItemStack> Items { get; }
    public Dictionary<EquipSlot, IEquippable> Equipment { get; }
    public Weapon Weapon { get; set; }
    public double ClothingInsulation { get; }

    public void AddToInventory(Item item);
    public void RemoveFromInventory(Item item);
    public void EquipItem(IEquippable item);
    public void UnequipItem(EquipSlot slot);
}
```

**Usage**:
```csharp
player.inventoryManager.AddToInventory(item);
player.inventoryManager.EquipItem(furCloak);
double insulation = player.inventoryManager.ClothingInsulation;
```

### LocationManager

**Responsibility**: Track player's zone and location.

**Location**: `PlayerComponents/LocationManager.cs`

**Key Methods**:
```csharp
public class LocationManager
{
    public Location CurrentLocation { get; set; }
    public Zone CurrentZone { get; }

    public void MoveTo(Location location);
}
```

**Usage**:
```csharp
player.CurrentLocation  // Backed by locationManager
player.CurrentZone      // Provided by locationManager
```

### SkillRegistry

**Responsibility**: Track player's skills and experience.

**Location**: `Level/SkillRegistry.cs`

**Key Methods**:
```csharp
public class SkillRegistry
{
    public Skill GetSkill(string skillName);
    public void GainExperience(string skillName, int xp);
    public int GetSkillLevel(string skillName);
}
```

**Usage**:
```csharp
player.Skills.GetSkill("Crafting").GainExperience(5);
int level = player.Skills.GetSkillLevel("Combat");
```

---

## NPC Simplicity

**Philosophy**: NPCs are deliberately simple. Complexity comes from Body configuration, not features.

**NPC Constructor** (NPC.cs:30-36):
```csharp
public Npc(string name, Weapon weapon, BodyCreationInfo stats)
    : base(name, stats)
{
    Description = "";
    IsHostile = true;
    ActiveWeapon = weapon;
    Loot = new Container(name, 10);
}
```

**That's It**. No managers, no skills, no complexity.

**NPC Stats Come From Body**:
```csharp
// Wolf's combat effectiveness comes from Body
var wolfStats = new BodyCreationInfo
{
    BodyType = BodyType.Wolf,
    FatKg = 5.0,
    MuscleKg = 25.0,  // Affects speed, damage
    Height = 0.8      // Affects hitbox, capacity contributions
};

var wolf = new Npc("Wolf", weapon, wolfStats);

// Wolf is strong because of:
// - High muscle → high Speed capacity → high damage multiplier
// - Body parts configured for wolf anatomy
// - NOT because of "Combat" skill (doesn't have skills)
```

**Why This Works**:
- **Realistic**: Animals are strong due to physiology, not training
- **Simple**: No skill system to manage for NPCs
- **Balanced**: Player gains advantage through skills/equipment
- **Performance**: Fewer systems to update per NPC

---

## Composition Examples

### Example 1: Player Update Cycle

Player's update uses composed managers:

```csharp
public override void Update()
{
    // Update effects (from Actor base)
    EffectRegistry.Update();

    // Get survival context using composed managers
    var context = new SurvivalContext
    {
        ActivityLevel = 2,
        LocationTemperature = locationManager.CurrentLocation.GetTemperature(),
        ClothingInsulation = inventoryManager.ClothingInsulation,
        //                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        //                   Uses InventoryManager for clothing data
    };

    // Update body with context
    Body.Update(TimeSpan.FromMinutes(1), context);
}
```

### Example 2: Player Location Change

Location change uses LocationManager:

```csharp
// User action triggers location change
player.CurrentLocation = newLocation;
//     ^^^^^^^^^^^^^^^^
//     Property backed by locationManager

// Internally in Player.cs:
public override Location CurrentLocation
{
    get => locationManager.CurrentLocation;
    set => locationManager.CurrentLocation = value;
}
```

### Example 3: NPC Attack (No Managers)

NPC attack uses only base Actor capabilities:

```csharp
// NPC attacks player
wolf.Attack(player);
//   ^^^^^^
//   From Actor.combatManager (no additional managers needed)

// Inside Actor.cs:
public virtual void Attack(Actor target, string? bodyPart = null)
    => combatManager.Attack(target, bodyPart);
```

### Example 4: Player Skill Check (Player-Only)

```csharp
// Check if player can craft recipe
bool canCraft = recipe.RequiredSkillLevel <=
    player.Skills.GetSkillLevel(recipe.RequiredSkill);
//  ^^^^^^^^^^^^^
//  Player-only feature

// NPC can't do this:
// wolf.Skills  ← Doesn't exist!
```

### Example 5: Weapon Access Differences

**Player**: Weapon managed by InventoryManager
```csharp
public override Weapon ActiveWeapon
{
    get => inventoryManager.Weapon;  // Manager integration
    protected set => inventoryManager.Weapon = value;
}
```

**NPC**: Weapon is simple property
```csharp
public override Weapon ActiveWeapon { get; protected set; }
// Just a simple property, set in constructor
```

---

## Design Patterns Summary

### Dependency Injection

Managers are injected in Player constructor:

```csharp
public Player(Location startingLocation) : base("Player", Body.BaselinePlayerStats)
{
    Name = "Player";
    locationManager = new LocationManager(startingLocation);
    //                                    ^^^^^^^^^^^^^^^^
    //                                    Injected dependency
    inventoryManager = new(EffectRegistry);
    //                     ^^^^^^^^^^^^^^
    //                     Injected dependency
    Skills = new SkillRegistry();
}
```

### Single Responsibility

Each manager has one job:
- **InventoryManager**: Only inventory/equipment
- **LocationManager**: Only location/zone tracking
- **SkillRegistry**: Only skills/experience
- **CombatManager**: Only combat logic

### Open/Closed Principle

Easy to add new managers without modifying Actor:

```csharp
// Hypothetical: Add quest manager to Player
public class Player : Actor
{
    public readonly QuestManager quests;  // New manager

    public Player(Location start) : base(...)
    {
        // ...
        quests = new QuestManager();  // Initialize new manager
    }
}
// Actor unchanged, NPC unchanged
```

---

## Anti-Patterns to Avoid

### ❌ Giving NPCs Skills

```csharp
// WRONG: NPCs shouldn't have skills
public class Npc : Actor
{
    public SkillRegistry Skills { get; }  // ← DON'T
}
```

### ❌ Bloating Actor Base Class

```csharp
// WRONG: Don't add player-specific features to Actor
public abstract class Actor
{
    public SkillRegistry Skills { get; }  // ← DON'T (player-only)
    public List<Spell> Spells { get; }    // ← DON'T (player-only)
}
```

### ❌ Deep Inheritance Hierarchies

```csharp
// WRONG: Avoid deep hierarchies
Actor → LivingActor → MovingActor → Player  // ← Too deep
```

### ❌ Manager-Like Logic in Actor

```csharp
// WRONG: Don't implement manager logic in Actor
public class Actor
{
    public void EquipItem(Item item)  // ← Belongs in InventoryManager
    {
        // Complex equipment logic here
    }
}
```

### ✅ Correct Approach

```csharp
// CORRECT: Keep Actor thin, use managers
public class Actor
{
    // Just core systems
    public Body Body { get; init; }
    public EffectRegistry EffectRegistry { get; init; }
}

public class Player : Actor
{
    // Managers handle complexity
    public readonly InventoryManager inventoryManager;

    public void EquipItem(Item item)
        => inventoryManager.EquipItem(item);  // Delegate to manager
}
```

---

## Related Files

**Core Architecture**:
- `Actors/Actor.cs` - Base actor class (Actor.cs:9-42)
- `Player.cs` - Player implementation with managers (Player.cs:14-58)
- `Actors/NPC.cs` - Simple NPC implementation (NPC.cs:7-48)

**Manager Implementations**:
- `PlayerComponents/InventoryManager.cs` - Inventory management
- `PlayerComponents/LocationManager.cs` - Location tracking
- `PlayerComponents/CombatManager.cs` - Combat logic
- `Level/SkillRegistry.cs` - Skill progression

**Related Guides**:
- [body-and-damage.md](body-and-damage.md) - Body system used by all actors
- [effect-system.md](effect-system.md) - EffectRegistry used by all actors
- [factory-patterns.md](factory-patterns.md) - Creating NPCs with NPCFactory
- [complete-examples.md](complete-examples.md) - Full NPC creation examples

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
