# Gameplay Fixes Sprint Plan

## Overview
This sprint addresses 46 critical issues identified during gameplay testing. Issues are organized into 4 phases by severity and dependency, with specific code changes, acceptance criteria, and effort estimates.

**Total Estimated Effort**: ~15-20 development days

---

## Phase 1: Critical System Fixes (Game-Breaking)
**Priority**: BLOCKER - These prevent core gameplay from functioning
**Estimated Effort**: 5-6 days

### Issue 1.1: Fire-Making Skill Missing (Crash)
**Severity**: 🔴 BLOCKER
**Location**: Line 1027 in gameplay.txt - `System.ArgumentException: Skill Fire-making does not exist`
**Root Cause**: ActionFactory.cs references "Fire-making" skill (lines 370, 411) but SkillRegistry only has "Firecraft"

**Code Changes**:
```csharp
// File: Actions/ActionFactory.cs (lines 370, 411)
// BEFORE:
var skill = ctx.player.Skills.GetSkill("Fire-making");
var playerSkill = ctx.player.Skills.GetSkill("Fire-making");

// AFTER:
var skill = ctx.player.Skills.GetSkill("Firecraft");
var playerSkill = ctx.player.Skills.GetSkill("Firecraft");
```

**Acceptance Criteria**:
- [ ] Fire-making action executes without crash
- [ ] Firecraft skill gains XP on success/failure
- [ ] Skill level affects success chance correctly

**Effort**: S (30 minutes)

---

### Issue 1.2: Survival Stat Consequences Not Working
**Severity**: 🔴 BLOCKER
**Location**: Lines 256-260, 1112-1116 in gameplay.txt - Player survives at 0% food/water/energy indefinitely
**Root Cause**: SurvivalProcessor.cs reduces stats to 0 but doesn't apply damage/effects when stats hit 0. Body.cs Update() method doesn't check for death conditions.

**Code Changes**:

```csharp
// File: Survival/SurvivalProcessor.cs
// Add after line 52 (in Process method):
if (data.Calories <= 0)
{
    double excessCalories = -data.Calories;
    data.Calories = 0;
    
    // NEW: Apply starvation damage
    if (data.IsPlayer)
    {
        result.Messages.Add("Your body is consuming itself from starvation!");
    }
    result.Effects.Add(EffectBuilderExtensions
        .CreateEffect("Starvation")
        .WithSeverity(Math.Min(Math.Abs(excessCalories) / 500.0, 1.0))
        .ReducesCapacity(CapacityNames.Moving, 0.3)
        .ReducesCapacity(CapacityNames.Manipulation, 0.2)
        .CausesHealthLoss(0.02 * Math.Min(Math.Abs(excessCalories) / 500.0, 1.0)) // Lose 2% health per hour at max starvation
        .AllowMultiple(false)
        .Build());
}

// Add after line 36 (hydration reduction):
if (data.Hydration <= 0)
{
    double deficit = Math.Abs(data.Hydration);
    data.Hydration = 0;
    
    // NEW: Apply dehydration damage
    if (data.IsPlayer)
    {
        result.Messages.Add("Your body is shutting down from dehydration!");
    }
    result.Effects.Add(EffectBuilderExtensions
        .CreateEffect("Severe Dehydration")
        .WithSeverity(Math.Min(deficit / 1000.0, 1.0))
        .ReducesCapacity(CapacityNames.Consciousness, 0.5)
        .ReducesCapacity(CapacityNames.Moving, 0.4)
        .CausesHealthLoss(0.05 * Math.Min(deficit / 1000.0, 1.0)) // Lose 5% health per hour at max dehydration
        .AllowMultiple(false)
        .Build());
}

// Add after line 35 (energy reduction):
if (data.Energy <= 0)
{
    double deficit = Math.Abs(data.Energy);
    data.Energy = 0;
    
    // NEW: Apply exhaustion penalties
    if (data.IsPlayer)
    {
        result.Messages.Add("You are utterly exhausted - you can barely move!");
    }
    result.Effects.Add(EffectBuilderExtensions
        .CreateEffect("Severe Exhaustion")
        .WithSeverity(Math.Min(deficit / 200.0, 1.0))
        .ReducesCapacity(CapacityNames.Consciousness, 0.3)
        .ReducesCapacity(CapacityNames.Moving, 0.5)
        .ReducesCapacity(CapacityNames.Manipulation, 0.4)
        .AllowMultiple(false)
        .Build());
}
```

```csharp
// File: Effects/EffectBuilder.cs
// Add new method to EffectBuilder class (after line 100):
public EffectBuilder CausesHealthLoss(double healthLossPerMinute)
{
    // Apply health loss as direct body damage
    _effectUpdate = _effectUpdate.Add(new SurvivalStatsUpdate
    {
        // Convert health loss to damage via effect system
        // Implementation will need DamageInfo integration
    });
    return this;
}
```

**Alternative Simpler Approach** (Recommended):
Instead of adding health loss effects, create direct damage when stats hit 0:

```csharp
// File: Bodies/Body.cs
// Modify Update method (around line 138):
public void Update(TimeSpan timePassed, SurvivalContext context)
{
    var data = BundleSurvivalData();
    data.environmentalTemp = context.LocationTemperature;
    data.ColdResistance = context.ClothingInsulation;
    data.activityLevel = context.ActivityLevel;

    var result = SurvivalProcessor.Process(data, (int)timePassed.TotalMinutes, EffectRegistry.GetAll());
    UpdateBodyBasedOnResult(result);
    
    // NEW: Check for critical survival failures
    if (data.Calories <= 0)
    {
        // Starvation damage: 0.1% of max health per minute at 0 calories
        Damage(new DamageInfo 
        { 
            Amount = 0.001 * MaxHealth, 
            Type = DamageType.Blunt, 
            Source = "Starvation" 
        });
    }
    
    if (data.Hydration <= 0)
    {
        // Dehydration damage: 0.2% of max health per minute (faster than starvation)
        Damage(new DamageInfo 
        { 
            Amount = 0.002 * MaxHealth, 
            Type = DamageType.Blunt, 
            Source = "Dehydration" 
        });
    }
    
    // Energy at 0 just reduces capacities (already handled by effects in SurvivalProcessor)
}
```

**Acceptance Criteria**:
- [ ] Player takes damage when food/water = 0%
- [ ] Death occurs after extended time at 0% food (realistic timeframe: ~2-3 weeks)
- [ ] Death occurs after shorter time at 0% water (realistic timeframe: ~3-5 days)
- [ ] Energy at 0% reduces movement/combat effectiveness but doesn't kill
- [ ] Warning messages appear when stats critical
- [ ] Health bar decreases visibly when starving/dehydrated

**Effort**: M (1 day)

---

### Issue 1.3: Hypothermia/Temperature Damage Not Working
**Severity**: 🔴 BLOCKER
**Location**: Lines 256-260, 1112-1116 - Player at 40.1°F (hypothermia range) indefinitely with no consequences
**Root Cause**: SurvivalProcessor.cs creates Hypothermia/Frostbite effects (lines 186-252) but effects don't apply actual damage to body

**Code Changes**:

```csharp
// File: Effects/EffectBuilder.cs
// Add to TemperatureType.Hypothermia case (line 241):
TemperatureType.Hypothermia => builder
    .Named("Hypothermia")
    .ReducesCapacity(CapacityNames.Consciousness, 0.2)
    .ReducesCapacity(CapacityNames.Manipulation, 0.3)
    .ReducesCapacity(CapacityNames.Moving, 0.2)
    .WithHourlySeverityChange(0.1) // Gets worse over time if not warmed
    .WithDamagePerHour(0.05), // NEW: 5% health loss per hour at severity 1.0

// Add to TemperatureType.Frostbite case (line 258):
TemperatureType.Frostbite => builder
    .Named("Frostbite")
    .ReducesCapacity(CapacityNames.Manipulation, 0.4) // when on arms/hands
    .ReducesCapacity(CapacityNames.Moving, 0.3) // when on legs/feet
    .WithHourlySeverityChange(0.2) // Progresses faster than hypothermia
    .WithDamagePerHour(0.1), // NEW: 10% health loss per hour at severity 1.0 (targeted damage)
```

```csharp
// File: Effects/EffectBuilder.cs
// Add new field and method to EffectBuilder class:
private double _damagePerHour = 0;

public EffectBuilder WithDamagePerHour(double damagePerHour)
{
    _damagePerHour = damagePerHour;
    return this;
}
```

```csharp
// File: Effects/EffectRegistry.cs
// Modify Update method (around line 30) to apply damage from effects:
public void Update()
{
    DateTime now = World.GameTime;
    TimeSpan elapsed = now - _lastUpdate;
    _lastUpdate = now;

    // Update severity over time
    foreach (Effect effect in _effects.ToList())
    {
        effect.Severity += effect.HourlySeverityChange * elapsed.TotalHours;
        
        // NEW: Apply damage from effects
        if (effect.DamagePerHour > 0)
        {
            double damageAmount = effect.DamagePerHour * effect.Severity * elapsed.TotalHours;
            _owner.Body.Damage(new DamageInfo
            {
                Amount = damageAmount,
                Type = DamageType.Blunt,
                TargetPartName = effect.TargetBodyPart,
                Source = effect.Kind
            });
        }
        
        if (effect.Severity <= 0)
        {
            RemoveEffect(effect);
        }
    }
}
```

**Acceptance Criteria**:
- [ ] Hypothermia effect applies when body temp < 95°F
- [ ] Hypothermia causes gradual health loss (5% per hour at max severity)
- [ ] Frostbite effect applies to extremities when temp < 89.6°F
- [ ] Frostbite causes faster health loss (10% per hour at max severity)
- [ ] Warming up removes/reduces effects
- [ ] Death occurs from prolonged cold exposure

**Effort**: M (1 day)

---

### Issue 1.4: Death System Missing/Not Triggering
**Severity**: 🔴 BLOCKER
**Location**: Throughout gameplay - player never dies despite critical conditions
**Root Cause**: No death check in main game loop. Body.IsDestroyed checks Health <= 0 but nothing stops gameplay when true.

**Code Changes**:

```csharp
// File: Program.cs
// Modify main loop (around line 84):
while (true)
{
    // NEW: Check if player is dead
    if (!player.IsAlive)
    {
        Output.WriteLine();
        Output.WriteDanger("═══════════════════════════════════════");
        Output.WriteDanger("         YOU HAVE DIED");
        Output.WriteDanger("═══════════════════════════════════════");
        Output.WriteLine();
        Output.WriteLine("Your journey has come to an end.");
        
        // Show death statistics
        TimeSpan survived = World.GameTime - World.StartTime;
        Output.WriteLine($"You survived for {survived.Days} days, {survived.Hours} hours, {survived.Minutes} minutes.");
        Output.WriteLine($"Final Stats:");
        player.Body.Describe(); // Shows final health, injuries, etc.
        
        Output.WriteLine();
        Output.WriteLine("Press any key to exit...");
        Input.ReadKey(true);
        Environment.Exit(0);
    }
    
    defaultAction.Execute(context);
}
```

```csharp
// File: World.cs
// Add StartTime field to track game start:
public static DateTime StartTime { get; set; } = DateTime.Now;

// Initialize in Program.cs after creating World
```

**Acceptance Criteria**:
- [ ] Game ends when player health reaches 0
- [ ] Death screen displays cause of death
- [ ] Death screen shows survival time statistics
- [ ] Player cannot take actions after death
- [ ] Game exits gracefully after death

**Effort**: S (2 hours)

---

## Phase 2: High-Priority Bugs
**Priority**: HIGH - Significantly degrades gameplay experience
**Estimated Effort**: 4-5 days

### Issue 2.1: Message Spam ("You are still feeling cold" x269914 times)
**Severity**: 🟠 CRITICAL UX BUG
**Location**: Line 1110 in gameplay.txt
**Root Cause**: Config.NOTIFY_EXISTING_STATUS_CHANCE = 0.05 (5% chance per minute) causes spam during long sleep. Sleep action processes each minute individually (30000 hours = 1.8M minutes).

**Code Changes**:

```csharp
// File: Survival/SurvivalProcessor.cs
// Modify GenerateColdEffects method (line 155):
private static void GenerateColdEffects(SurvivalData data, bool isNewTemperatureChange, SurvivalProcessorResult result)
{
    // Generate cold messages
    if (isNewTemperatureChange && data.IsPlayer)
    {
        result.Messages.Add("You are starting to feel cold.");
    }
    // REMOVE THIS BLOCK - no spam messages during normal processing
    // else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
    // {
    //     result.Messages.Add("You are still feeling cold.");
    // }
    
    // ... rest of method unchanged
}
```

```csharp
// File: Bodies/Body.cs
// Modify UpdateBodyBasedOnResult to batch messages during sleep/long updates:
private void UpdateBodyBasedOnResult(SurvivalProcessorResult result)
{
    var resultData = result.Data;
    BodyTemperature = resultData.Temperature;
    CalorieStore = resultData.Calories;
    Hydration = resultData.Hydration;
    Energy = resultData.Energy;

    result.Effects.ForEach(EffectRegistry.AddEffect);

    // NEW: Batch duplicate messages
    var messageCounts = result.Messages
        .GroupBy(m => m)
        .Select(g => new { Message = g.Key, Count = g.Count() });
    
    foreach (var msg in messageCounts)
    {
        string formattedMessage = msg.Message.Replace("{target}", OwnerName);
        if (msg.Count > 1)
        {
            Output.WriteLine($"{formattedMessage} (occurred {msg.Count} times)");
        }
        else
        {
            Output.WriteLine(formattedMessage);
        }
    }
}
```

**Better Solution** (Recommended):
Don't output messages during sleep at all, only show summary:

```csharp
// File: Survival/SurvivalProcessor.cs
// Add parameter to Process to suppress messages:
public static SurvivalProcessorResult Process(SurvivalData data, int minutesElapsed, List<Effect> activeEffects, bool suppressMessages = false)
{
    // ... existing logic ...
    
    if (!suppressMessages)
    {
        AddTemperatureEffects(data, oldTemperature, result);
    }
    else
    {
        // Still add effects, just not messages
        AddTemperatureEffectsQuiet(data, oldTemperature, result);
    }
    
    return result;
}

// File: Bodies/Body.cs
// Modify Rest method (line 184):
public bool Rest(int minutes)
{
    var data = BundleSurvivalData();
    data.activityLevel = .5;
    
    // NEW: Suppress messages during sleep
    var result = SurvivalProcessor.Sleep(data, minutes);
    
    // Don't output individual messages, just effects summary
    CalorieStore = result.Data.Calories;
    Hydration = result.Data.Hydration;
    Energy = result.Data.Energy;
    BodyTemperature = result.Data.Temperature;
    result.Effects.ForEach(EffectRegistry.AddEffect);
    
    // Heal at end
    HealingInfo healing = new HealingInfo()
    {
        Amount = minutes / 10,
        Type = "natural",
        Quality = Energy >= SurvivalProcessor.MAX_ENERGY_MINUTES * 0.9 ? 1 : .7,
    };
    Heal(healing);
    
    // Show sleep summary instead of spam
    if (IsPlayer)
    {
        Output.WriteLine($"You slept for {minutes / 60.0:F1} hours.");
        if (result.Data.Temperature < 95)
        {
            Output.WriteWarning("You were cold while sleeping.");
        }
        if (result.Data.Calories <= 0 || result.Data.Hydration <= 0)
        {
            Output.WriteDanger("Your body suffered from lack of sustenance during sleep.");
        }
    }

    return Energy >= SurvivalProcessor.MAX_ENERGY_MINUTES * 0.9;
}
```

**Acceptance Criteria**:
- [ ] No duplicate messages during sleep
- [ ] Sleep shows summary instead of per-minute updates
- [ ] Cold status messages appear max once per game update cycle
- [ ] Long sleep (100+ hours) completes in <1 second

**Effort**: M (4 hours)

---

### Issue 2.2: Duplicate Inspect/Drop Menu Bug
**Severity**: 🟠 BUG
**Location**: Lines 356-358, 379-382, 508-512 - "Inspect" appears twice in item menus
**Root Cause**: ActionFactory.Inventory.DecideInventoryAction (line 866) creates menu with DescribeItem listed twice

**Code Changes**:

```csharp
// File: Actions/ActionFactory.cs
// Fix DecideInventoryAction method (line 866):
public static IGameAction DecideInventoryAction(ItemStack stack)
{
    Item item = stack.Peek();
    return CreateAction(stack.DisplayName)
    .ThenShow(_ => [
        UseItem(item),
        DescribeItem(item),  // REMOVE DUPLICATE
        // DescribeItem(item),  <- DELETE THIS LINE
        DropItem(item),
        Common.BackTo("inventory", OpenInventory)
    ])
    .WithPrompt($"What would you like to do with the {item.Name}")
    .Build();
}
```

**Note**: Line 727 has wrong action name - DropItem is named "Inspect" instead of "Drop":

```csharp
// File: Actions/ActionFactory.cs (line 725)
public static IGameAction DropItem(Item item)
{
    return CreateAction($"Drop {item}")  // CHANGE FROM "Inspect {item}"
    .ShowMessage($"You drop the {item}")
    .Do(ctx => ctx.player.DropItem(item))
    .ThenShow(_ => [OpenInventory()])
    .Build();
}
```

**Acceptance Criteria**:
- [ ] Item menu shows: Use, Inspect, Drop, Back (no duplicates)
- [ ] Drop action is labeled "Drop" not "Inspect"
- [ ] All three actions function correctly

**Effort**: S (15 minutes)

---

### Issue 2.3: Sleep Exploit (30,000 Hour Sleep Allowed)
**Severity**: 🟠 BUG
**Location**: Line 1109 - Player sleeps 30,000 hours without validation
**Root Cause**: ActionFactory.Sleep (line 101) uses Input.ReadInt() with no bounds checking

**Code Changes**:

```csharp
// File: Actions/ActionFactory.cs
// Modify Sleep action (line 95):
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
    .When(ctx => ctx.player.Body.IsTired)
    .Do(ctx =>
    {
        Output.WriteLine("How many hours would you like to sleep?");
        int hours = Input.ReadInt(1, 24); // NEW: Add min/max bounds
        
        if (hours > 12 && !ctx.player.Body.Energy <= SurvivalProcessor.MAX_ENERGY_MINUTES * 0.2)
        {
            Output.WriteWarning("You're not tired enough to sleep that long.");
            hours = 12; // Cap at 12 hours if not extremely tired
        }
        
        int minutes = hours * 60;
        ctx.player.Body.Rest(minutes);
        World.Update(minutes);
    })
    .TakesMinutes(0) // Handles time manually
    .ThenReturn()
    .Build();
}
```

**Better Approach** - Calculate needed sleep dynamically:

```csharp
public static IGameAction Sleep()
{
    return CreateAction("Sleep")
    .When(ctx => ctx.player.Body.IsTired)
    .Do(ctx =>
    {
        double currentEnergy = ctx.player.Body.Energy;
        double maxEnergy = SurvivalProcessor.MAX_ENERGY_MINUTES;
        double energyDeficit = maxEnergy - currentEnergy;
        
        // Calculate hours needed for full rest (restore at 2x rate per SurvivalProcessor)
        int hoursNeeded = (int)Math.Ceiling(energyDeficit / (2.0 * 60.0));
        
        Output.WriteLine($"You need about {hoursNeeded} hours of sleep to feel fully rested.");
        Output.WriteLine("How many hours would you like to sleep? (1-24)");
        
        int hours = Input.ReadInt(1, 24);
        int minutes = hours * 60;
        
        ctx.player.Body.Rest(minutes);
        World.Update(minutes);
    })
    .TakesMinutes(0)
    .ThenReturn()
    .Build();
}
```

**Acceptance Criteria**:
- [ ] Sleep input accepts only 1-24 hours
- [ ] Invalid input shows error and re-prompts
- [ ] Game suggests realistic sleep duration based on tiredness
- [ ] Sleeping longer than needed just wastes time (not beneficial)

**Effort**: S (30 minutes)

---

### Issue 2.4: Water Harvesting Not Accessible
**Severity**: 🟠 BUG
**Location**: Line 699-706 - "Forest Puddle (harvestable)" visible but no menu option to harvest
**Root Cause**: ActionFactory doesn't generate "Harvest Resources" action for HarvestableFeature

**Code Changes**:

```csharp
// File: Actions/ActionFactory.cs
// Add new action in Common class (after ForageAction around line 60):
public static IGameAction HarvestResources()
{
    return CreateAction("Harvest Resources")
    .When(ctx => ctx.currentLocation.GetFeature<HarvestableFeature>() != null && 
                 ctx.currentLocation.GetFeature<HarvestableFeature>()!.HasAvailableResources())
    .Do(ctx =>
    {
        var harvestable = ctx.currentLocation.GetFeature<HarvestableFeature>()!;
        Output.WriteLine($"You harvest resources from the {harvestable.DisplayName}...");
        
        var items = harvestable.Harvest();
        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                ctx.player.TakeItem(item);
            }
            
            var itemNames = items.Select(i => i.Name).Distinct();
            Output.WriteSuccess($"You harvested: {string.Join(", ", itemNames)}");
        }
        else
        {
            Output.WriteLine("The resource is currently depleted.");
        }
    })
    .TakesMinutes(5) // Takes 5 minutes to harvest
    .ThenReturn()
    .Build();
}
```

```csharp
// File: Actions/ActionFactory.cs
// Modify MainMenu to include HarvestResources (around line 28):
public static IGameAction MainMenu()
{
    return CreateAction("Main Menu")
        .Do(ctx =>
        {
            ctx.player.inventoryManager.ShowSurvivalStatus();
        })
        .ThenShow(ctx =>
        {
            var options = new List<IGameAction>
            {
                Location.LookAround(),
                Survival.AddFuelToFire(),
                Survival.StartFire(),
                Survival.HarvestResources(), // NEW
                Survival.Forage(),
                // ... rest of menu
            };
            // ... filtering logic
        })
        // ... rest of method
}
```

**Acceptance Criteria**:
- [ ] "Harvest Resources" appears in main menu when harvestable feature present
- [ ] Harvesting water from puddle gives water item
- [ ] Harvesting updates resource quantity
- [ ] Depleted resources show "depleted" status
- [ ] Resources respawn over time as designed

**Effort**: M (3 hours)

---

### Issue 2.5: Hunting Mechanics Broken
**Severity**: 🟠 BUG
**Location**: Lines 909-936 - Ptarmigan spotted and fled immediately on first approach
**Root Cause**: Hunting detection mechanics too sensitive, no stealth skill check

**Investigation Needed**: Read HuntingManager.cs and stealth mechanics to diagnose

**Preliminary Code Changes**:

```csharp
// File: PlayerComponents/HuntingManager.cs or StealthManager.cs
// Likely need to modify approach mechanics to include stealth check
// Need to read full hunting implementation first
```

**Acceptance Criteria**:
- [ ] Player can approach prey without instant detection
- [ ] Stealth/Hunting skill affects detection chance
- [ ] Distance affects detection chance
- [ ] Concealment (terrain) affects detection
- [ ] Wounded animals flee realistically

**Effort**: M (4 hours) - Needs investigation first

---

### Issue 2.6: Foraging Logic Error ("found nothing" then shows items)
**Severity**: 🟠 BUG
**Location**: Lines 1067-1082 - "found nothing" message appears, then items listed
**Root Cause**: ForageFeature.cs (line 82) outputs "found nothing" BEFORE items are added to location. Item collection menu appears after message.

**Code Changes**:

```csharp
// File: Environments/LocationFeatures.cs/ForageFeature.cs
// Modify Forage method (lines 37-84):
public void Forage(double hours)
{
    List<Item> itemsFound = [];

    // Run foraging checks
    foreach (Func<Item> factory in resourceAbundance.Keys)
    {
        double baseChance = ResourceDensity * resourceAbundance[factory];
        double scaledChance = baseChance * hours;

        if (Utils.DetermineSuccess(scaledChance))
        {
            var item = factory();
            item.IsFound = true;
            ParentLocation.Items.Add(item);
            itemsFound.Add(item);
        }
    }

    // Only deplete if items found
    if (itemsFound.Count > 0)
    {
        numberOfHoursForaged += hours;
    }

    lastForageTime = World.GameTime;

    // NEW: Don't output message here - let action handle it
    // REMOVE lines 66-83 (all Output.WriteLine calls)
    
    // Store found items for action to display
    // OR: Return itemsFound list and let action display results
}
```

```csharp
// File: Actions/ActionFactory.cs
// Modify Forage action to display results (around line 55):
public static IGameAction Forage(string actionName = "Forage")
{
    return CreateAction(actionName)
    .When(ctx => ctx.currentLocation.GetFeature<ForageFeature>() is not null)
    .Do(ctx =>
    {
        var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>()!;
        
        // Forage without immediate output
        int itemsBefore = ctx.currentLocation.Items.Count(i => i.IsFound);
        forageFeature.Forage(0.25); // 15 minutes
        int itemsAfter = ctx.currentLocation.Items.Count(i => i.IsFound);
        
        // Display results after foraging completes
        int itemsFound = itemsAfter - itemsBefore;
        if (itemsFound > 0)
        {
            var foundItems = ctx.currentLocation.Items
                .Where(i => i.IsFound)
                .TakeLast(itemsFound)
                .GroupBy(i => i.Name)
                .Select(g => $"{g.Key} ({g.Count()})");
            
            Output.WriteLine($"You spent 15 minutes searching and found: {string.Join(", ", foundItems)}");
        }
        else
        {
            Output.WriteLine("You spent 15 minutes searching but found nothing.");
        }
    })
    .ThenShow(ctx =>
    {
        // Show collection menu if items found
        var foundItems = ctx.currentLocation.Items.Where(i => i.IsFound).ToList();
        if (foundItems.Any())
        {
            return new List<IGameAction>
            {
                Inventory.TakeAllFoundItems(),
                Inventory.SelectFoundItems(),
                Forage("Keep foraging"),
                Common.Return("Leave items and finish foraging")
            };
        }
        else
        {
            return new List<IGameAction>
            {
                Forage("Keep foraging"),
                Common.Return("Finish foraging")
            };
        }
    })
    .Build();
}
```

**Acceptance Criteria**:
- [ ] "Found nothing" message only appears when truly nothing found
- [ ] Item collection menu only appears after foraging message
- [ ] Message accurately describes what was found
- [ ] No confusing message ordering

**Effort**: S (1 hour)

---

## Phase 3: Medium-Priority UX/Balance
**Priority**: MEDIUM - Quality of life and balance issues
**Estimated Effort**: 3-4 days

### Issue 3.1: Equipment Auto-Equip Without Choice
**Severity**: 🟡 UX ISSUE
**Location**: Lines 807-808 - Bark Chest Wrap auto-equipped after crafting without player choice
**Root Cause**: Player.TakeItem (line 77-80) auto-equips gear if CanAutoEquip returns true

**Code Changes**:

```csharp
// File: Player.cs
// Modify TakeItem to ask first (line 72):
public void TakeItem(Item item)
{
    locationManager.RemoveItemFromLocation(item);

    // NEW: Ask before auto-equipping
    if (item is IEquippable equipment && inventoryManager.CanAutoEquip(equipment))
    {
        Output.WriteLine($"Equip {item.Name}? (y/n)");
        if (Input.ReadYesNo())
        {
            inventoryManager.Equip(equipment);
            return;
        }
    }

    inventoryManager.AddToInventory(item);
}
```

**Alternative** - Add config option for auto-equip behavior:

```csharp
// File: Config.cs
public static bool AUTO_EQUIP_GEAR = false; // Default to manual

// File: Player.cs
public void TakeItem(Item item)
{
    locationManager.RemoveItemFromLocation(item);

    if (Config.AUTO_EQUIP_GEAR && 
        item is IEquippable equipment && 
        inventoryManager.CanAutoEquip(equipment))
    {
        inventoryManager.Equip(equipment);
        Output.WriteLine($"You equipped {item.Name}.");
        return;
    }

    inventoryManager.AddToInventory(item);
}
```

**Acceptance Criteria**:
- [ ] Player chooses whether to equip new gear
- [ ] Auto-equip can be disabled in config
- [ ] Equipped items show clear confirmation
- [ ] Unequipped items go to inventory

**Effort**: S (30 minutes)

---

### Issue 3.2: Cold Status Processing Performance
**Severity**: 🟡 PERFORMANCE
**Location**: Line 1110 - 269,914 cold status checks during 30K hour sleep
**Root Cause**: SurvivalProcessor processes minute-by-minute even during sleep

**Code Changes**:

Already addressed in Issue 2.1 by batching messages. Performance optimization:

```csharp
// File: Bodies/Body.cs
// Optimize Rest to process in hourly chunks instead of per-minute:
public bool Rest(int minutes)
{
    var data = BundleSurvivalData();
    data.activityLevel = .5;
    
    // Process in hourly chunks for performance
    int hoursToSleep = minutes / 60;
    int remainingMinutes = minutes % 60;
    
    for (int hour = 0; hour < hoursToSleep; hour++)
    {
        var hourResult = SurvivalProcessor.Sleep(data, 60);
        data = hourResult.Data;
        hourResult.Effects.ForEach(EffectRegistry.AddEffect);
    }
    
    if (remainingMinutes > 0)
    {
        var finalResult = SurvivalProcessor.Sleep(data, remainingMinutes);
        data = finalResult.Data;
        finalResult.Effects.ForEach(EffectRegistry.AddEffect);
    }
    
    // Update body with final state
    CalorieStore = data.Calories;
    Hydration = data.Hydration;
    Energy = data.Energy;
    BodyTemperature = data.Temperature;
    
    // ... healing and return
}
```

**Acceptance Criteria**:
- [ ] Long sleep completes in <1 second
- [ ] Survival processing remains accurate
- [ ] No performance degradation for 24+ hour sleeps

**Effort**: S (1 hour)

---

### Issue 3.3: Missing Features (Cooking, Containers, Equipment Screen)
**Severity**: 🟡 FEATURE GAP
**Location**: Various - Features exist in code but not accessible in menus

#### 3.3a: Cooking System Missing
**Investigation**: Check if CookingFeature exists and just needs menu integration

**Code Changes**: TBD after investigation

**Effort**: M (4-6 hours)

#### 3.3b: Equipment Screen Missing
**Root Cause**: No action to view equipped gear

**Code Changes**:

```csharp
// File: Actions/ActionFactory.cs
// Add to Inventory class:
public static IGameAction ViewEquipment()
{
    return CreateAction("View Equipment")
    .Do(ctx =>
    {
        Output.WriteLine("\n=== EQUIPMENT ===");
        ctx.player.inventoryManager.DescribeEquipment();
    })
    .ThenReturn()
    .Build();
}

// Add to MainMenu options (around line 40):
Inventory.ViewEquipment(),
```

**Acceptance Criteria**:
- [ ] Equipment screen shows all equipped items
- [ ] Shows stats (armor rating, warmth, damage)
- [ ] Accessible from main menu

**Effort**: S (1 hour)

---

### Issue 3.4: Balance Issues
**Severity**: 🟡 BALANCE

#### 3.4a: Stat Drain Too Harsh
**Location**: Lines 1090-1116 - Stats drop from 75% to 0% after sleeping ~30K hours
**Analysis**: 
- Food: 75% → 0% over 30,000 hours
- Water: 75% → 0% over 30,000 hours
- Energy: Restored by sleep (working correctly)

This is actually CORRECT behavior (realistic):
- 30,000 hours = 1,250 days = 3.4 years
- Anyone would die without food/water in 3.4 years

**Conclusion**: Not a bug, working as intended. Issue is sleep exploit (fixed in 2.3).

#### 3.4b: Forage Success Too Low
**Location**: Lines 98-229 - Many "found nothing" results
**Analysis**: Need to check ForageFeature resource density and abundance values

**Investigation Needed**: Check starting location forage rates in Program.cs

From Program.cs lines 36-49:
- Dry Grass: 0.5 abundance
- Bark Strips: 0.6 abundance  
- Plant Fibers: 0.5 abundance
- Stick: 0.7 abundance
- Berries: 0.4 abundance
- etc.

With 1.75x density multiplier, these should give reasonable success rates. "Found nothing" is normal/expected for realism.

**Recommendation**: No change needed, working as designed. Document in README that foraging has realistic failure rate.

**Effort**: None (not a bug)

---

## Phase 4: Low-Priority Polish
**Priority**: LOW - Nice to have, doesn't block gameplay
**Estimated Effort**: 2-3 days

### Issue 4.1: Input Validation Consistency
**Severity**: 🟢 POLISH
**Location**: Lines 73-74, 315-316, 457-458, 637-639 - Inconsistent input validation messages

**Examples**:
- Line 73: "Invalid input. Please enter a number between 1 and 2."
- Line 315: "Invalid input. Please enter a number."
- Line 637: "Invalid input. Enter N, E, S, W, or Q."

**Code Changes**:

```csharp
// File: IO/Input.cs
// Standardize all validation messages:

public static int ReadInt(int low, int high)
{
    while (true)
    {
        string? input = Read();
        if (int.TryParse(input, out int result) && result >= low && result <= high)
        {
            return result;
        }
        Output.WriteWarning($"Invalid input. Please enter a number between {low} and {high}.");
    }
}

public static int ReadInt()
{
    while (true)
    {
        string? input = Read();
        if (int.TryParse(input, out int result))
        {
            return result;
        }
        Output.WriteWarning("Invalid input. Please enter a number.");
    }
}
```

**Acceptance Criteria**:
- [ ] All input validation uses consistent format
- [ ] Error messages are clear and helpful
- [ ] Case-insensitive where appropriate (Y/n, N/E/S/W)

**Effort**: S (1 hour)

---

### Issue 4.2: Menu Navigation Improvements
**Severity**: 🟢 UX POLISH
**Location**: Throughout - Menu depth sometimes confusing

**Improvements**:
- [ ] Show breadcrumb trail (Main Menu > Inventory > Item)
- [ ] "Back" vs "Return" vs "Cancel" naming consistency
- [ ] ESC key support for backing out of menus

**Code Changes**: TBD - needs design review

**Effort**: M (4 hours)

---

### Issue 4.3: Message Formatting Consistency
**Severity**: 🟢 POLISH
**Location**: Throughout - Inconsistent message capitalization and punctuation

**Examples to standardize**:
- "You are still feeling cold." vs "you are feeling cold"
- "You put the Large Stick in your Bag" vs "You put the large stick in your bag"
- Item names capitalized inconsistently

**Code Changes**: Create style guide, update Output methods to enforce

**Effort**: S (2 hours)

---

## Implementation Order & Dependencies

### Week 1: Critical Fixes
**Days 1-2**: Phase 1 (Blockers)
- Issue 1.1: Fire-making skill (30 min)
- Issue 1.2: Survival stat consequences (1 day)
- Issue 1.3: Temperature damage (1 day)
- Issue 1.4: Death system (2 hours)

### Week 2: High-Priority Bugs
**Days 3-5**: Phase 2 (High Priority)
- Issue 2.1: Message spam (4 hours)
- Issue 2.2: Duplicate menus (15 min)
- Issue 2.3: Sleep exploit (30 min)
- Issue 2.4: Water harvesting (3 hours)
- Issue 2.5: Hunting mechanics (4 hours + investigation)
- Issue 2.6: Foraging message logic (1 hour)

### Week 3: Medium-Priority UX
**Days 6-8**: Phase 3 (Medium Priority)
- Issue 3.1: Auto-equip prompt (30 min)
- Issue 3.2: Cold processing performance (1 hour)
- Issue 3.3: Missing features (6 hours total)
- Issue 3.4: Balance review (investigation only)

### Week 4: Polish
**Days 9-10**: Phase 4 (Low Priority)
- Issue 4.1: Input validation (1 hour)
- Issue 4.2: Menu navigation (4 hours)
- Issue 4.3: Message formatting (2 hours)

---

## Testing Strategy

### Unit Tests (Add to test_survival.Tests)
- [ ] SurvivalProcessor applies damage at 0 calories/hydration
- [ ] Temperature effects apply damage correctly
- [ ] Death triggers when health = 0
- [ ] Sleep bounds validation
- [ ] Foraging message logic

### Integration Tests (Using TEST_MODE=1)
- [ ] Play through survival scenario (0% food/water) → death
- [ ] Play through hypothermia → death
- [ ] Sleep for max hours (24) → wake up correctly
- [ ] Harvest water from puddle → get water item
- [ ] Hunt animal → stealth mechanics work
- [ ] Forage → messages appear in correct order

### Regression Tests
- [ ] Existing survival mechanics unchanged
- [ ] Fire-making still works
- [ ] Crafting system unaffected
- [ ] Combat system unaffected

---

## Risk Assessment

### High Risk Changes
1. **Issue 1.2 (Survival consequences)**: Could make game too punishing if damage rates wrong
   - Mitigation: Conservative damage rates, extensive testing
   - Fallback: Make damage rates configurable in Config.cs

2. **Issue 1.3 (Temperature damage)**: Could conflict with existing effect system
   - Mitigation: Use existing DamageInfo system, add tests
   - Fallback: Apply damage directly in Body.Update instead of via effects

3. **Issue 2.1 (Message spam)**: Changes to core survival processing
   - Mitigation: Add suppressMessages parameter to preserve existing behavior
   - Fallback: Just batch messages, don't change core logic

### Medium Risk Changes
1. **Issue 2.5 (Hunting)**: Complex system, needs investigation
   - Mitigation: Read full implementation before changes
   - Fallback: Document as "working as designed" if too complex

2. **Issue 3.3 (Missing features)**: May require new systems
   - Mitigation: Use existing patterns (ActionFactory, Features)
   - Fallback: Defer to future sprint if too large

### Low Risk Changes
All Phase 4 changes are cosmetic/polish with minimal risk.

---

## Success Metrics

### Must Have (Sprint Goal)
- [ ] All Phase 1 issues fixed (game not broken)
- [ ] All Phase 2 issues fixed (playable experience)
- [ ] Unit tests pass
- [ ] Integration tests pass

### Should Have
- [ ] 80%+ of Phase 3 issues fixed
- [ ] Performance acceptable (sleep <1 sec)
- [ ] No new bugs introduced

### Nice to Have
- [ ] Phase 4 polish completed
- [ ] Code coverage >60%
- [ ] Documentation updated

---

## Post-Sprint Activities

### Code Review
- [ ] Review all changes with user
- [ ] Verify acceptance criteria met
- [ ] Check for code quality issues

### Documentation Updates
- [ ] Update CURRENT-STATUS.md
- [ ] Update ISSUES.md (mark resolved)
- [ ] Update README if design philosophy changed
- [ ] Update relevant dev docs in documentation/

### Playtesting
- [ ] Full gameplay session (2+ hours)
- [ ] Record new issues found
- [ ] Validate all 46 issues actually fixed

---

**END OF SPRINT PLAN**
