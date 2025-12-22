# Fire Management System

*Created: 2024-11*
*Last Updated: 2025-12-20*

## Overview

The fire management system provides realistic fire behavior with three distinct states: **Active** (burning), **Embers** (dying), and **Cold** (extinguished). This system balances survival realism with playable UX, rewarding proactive fire management while avoiding tedious micromanagement.

---

## Core Concepts

### Fire States

The fire progresses through three states:

1. **Active (Burning)**
   - `IsActive = true`, `FuelRemaining > 0`
   - Provides full heat output (default 15°F)
   - Consumes fuel at 1 hour per hour
   - Visible status: "Campfire (burning)" or "Campfire (dying)" when < 15 min remaining

2. **Embers**
   - `HasEmbers = true`, `EmberTimeRemaining > 0`
   - Provides reduced heat output (35% of full heat = ~5.25°F)
   - Lasts 25% of the previous burn time
   - Can be relighted automatically by adding fuel
   - Visible status: "Campfire (glowing embers, X min)"

3. **Cold (Extinguished)**
   - `IsActive = false`, `HasEmbers = false`, `FuelRemaining = 0`
   - Provides no heat
   - Requires fire-making materials to relight
   - Visible status: "Campfire (cold)"

### Design Philosophy

**Why embers?**
- **Realism**: Real fires leave hot embers that can reignite
- **UX benefit**: Provides grace period for fire management without constant micromanagement
- **Proactive reward**: Players who tend fires before they die are rewarded with easier relighting

**Why 35% heat and 25% duration?**
- Tested values providing meaningful benefit without eliminating survival pressure
- 35% heat is noticeable (~5°F vs 15°F) but not sufficient for indefinite survival
- 25% duration provides reasonable window (e.g., 1-hour fire → 15-min ember window)

**Why auto-relight?**
- Manual relight tested and rejected as too punishing and tedious
- Matches reality (adding fuel to embers naturally relights the fire)
- Encourages proactive management vs reactive scrambling

---

## HeatSourceFeature API

### Class: `HeatSourceFeature : LocationFeature`

Location: `Environments/LocationFeatures.cs/HeatSourceFeature.cs`

#### Properties

```csharp
public bool IsActive { get; private set; }           // Fire is currently burning
public bool HasEmbers { get; private set; }          // Fire has transitioned to embers
public double HeatOutput { get; private set; }       // Base heat output in °F (default 15.0)
public double FuelRemaining { get; private set; }    // Hours of fuel remaining
public double EmberTimeRemaining { get; private set; } // Hours until embers go cold
public double FuelConsumptionRate { get; private set; } // Hours consumed per hour (default 1.0)
```

#### Constructor

```csharp
public HeatSourceFeature(Location location, double heatOutput = 15.0)
```

**Parameters:**
- `location`: Parent location for the heat source
- `heatOutput`: Base temperature increase in Fahrenheit (default 15°F)

**Example:**
```csharp
var campfire = new HeatSourceFeature(currentLocation, heatOutput: 15.0);
```

#### Methods

##### AddFuel(double hours)

Adds fuel to the fire with a maximum capacity of 8 hours.

**Behavior:**
- Adds specified hours to `FuelRemaining` (capped at 8.0 hours)
- **Auto-relight**: If fire has embers (`HasEmbers = true`), automatically relights the fire
- If fire is cold, fuel is added but fire remains inactive until explicitly started

**Parameters:**
- `hours`: Amount of fuel to add in hours

**Example:**
```csharp
campfire.AddFuel(2.0); // Add 2 hours of firewood

if (campfire.HasEmbers && campfire.FuelRemaining > 0)
{
    // Fire automatically relights!
    Output.WriteLine("The embers flare to life as you add fuel.");
}
```

**Design Note:** 8-hour maximum capacity allows realistic overnight fires without excessive micromanagement.

##### Update(TimeSpan elapsed)

Updates fire state based on time elapsed. Should be called from `Location.Update()`.

**Behavior:**
- **Active fire**: Consumes fuel at `FuelConsumptionRate` per hour
  - When fuel depletes, transitions to embers
  - Ember duration set to 25% of last fuel amount
- **Ember state**: Consumes ember time at `FuelConsumptionRate` per hour
  - When ember time depletes, transitions to cold
- **Cold state**: No changes

**Parameters:**
- `elapsed`: TimeSpan of game time elapsed

**Example:**
```csharp
// In Location.Update(TimeSpan elapsed)
foreach (var feature in Features.OfType<HeatSourceFeature>())
{
    feature.Update(elapsed);
}
```

**Important**: This method must be called for fire depletion to work. Ensure `Location.Update()` propagates to all `LocationFeature.Update()` calls.

##### SetActive(bool active)

Manually activates or deactivates the fire. Used by fire-making actions.

**Behavior:**
- Setting `active = true` requires `FuelRemaining > 0`
- Clears ember state when activating
- Does not consume fire-making materials (handled by action)

**Parameters:**
- `active`: True to light the fire, false to extinguish

**Example:**
```csharp
// After successful fire-making skill check
if (fireStarted)
{
    campfire.SetActive(true);
    Output.WriteLine("Smoke rises from the tinder. You have fire!");
}
```

##### GetEffectiveHeatOutput()

Returns the current heat output based on fire state.

**Returns:**
- `double`: Temperature bonus in Fahrenheit
  - Active: Full `HeatOutput` (e.g., 15°F)
  - Embers: 35% of `HeatOutput` (e.g., 5.25°F)
  - Cold: 0°F

**Example:**
```csharp
double warmth = campfire.GetEffectiveHeatOutput();
double totalTemp = baseTemp + shelterBonus + warmth;
```

**Integration**: Used by `Location.GetTemperature()` to calculate total environmental temperature.

---

## State Transitions

### Transition Diagram

```
   [COLD]
     ↓ (SetActive - requires fire-making)
  [ACTIVE]
     ↓ (fuel depletes)
  [EMBERS] ← (AddFuel auto-relights)
     ↓ (ember time depletes)
   [COLD]
```

### Transition Details

#### COLD → ACTIVE

**Trigger**: `SetActive(true)` after successful fire-making

**Requirements:**
- `FuelRemaining > 0`
- Fire-making materials consumed (handled by action)
- Successful skill check (handled by crafting system)

**Result:**
- `IsActive = true`
- `HasEmbers = false`
- Fire begins burning

#### ACTIVE → EMBERS

**Trigger**: `Update()` when `FuelRemaining` reaches 0

**Automatic behavior:**
- `IsActive = false`
- `HasEmbers = true`
- `EmberTimeRemaining = _lastFuelAmount * 0.25`

**Example**: 2-hour fire → 30-minute ember period

#### EMBERS → ACTIVE (Auto-Relight)

**Trigger**: `AddFuel()` while `HasEmbers = true`

**Automatic behavior:**
- `IsActive = true`
- `HasEmbers = false`
- `EmberTimeRemaining = 0`
- No fire-making materials required

**UX Impact**: Rewards proactive fire management

#### EMBERS → COLD

**Trigger**: `Update()` when `EmberTimeRemaining` reaches 0

**Automatic behavior:**
- `HasEmbers = false`
- Fire must be restarted with fire-making materials

---

## Integration with Game Systems

### Location Temperature Calculation

`Location.GetTemperature()` incorporates fire heat:

```csharp
public double GetTemperature()
{
    double baseTemp = Zone.BaseTemperature;
    double shelterBonus = /* shelter insulation */;

    // Add heat from active fires and embers
    foreach (var heat in Features.OfType<HeatSourceFeature>())
    {
        baseTemp += heat.GetEffectiveHeatOutput();
    }

    return baseTemp;
}
```

**Temperature contributions:**
- Active fire: +15°F (full heat)
- Embers: +5.25°F (35% heat)
- Cold fire: +0°F

### Crafting System Integration

Fire is required for certain recipes using `.RequiringFire()`:

```csharp
new RecipeBuilder()
    .Named("Bone Knife")
    .WithPropertyRequirement(ItemProperty.Bone, 1, 0.3)
    .RequiringFire()
    .ResultingInItem(() => ItemFactory.MakeBoneKnife())
    .TakingMinutes(45)
    .Build()
```

**Fire requirement checking:**
```csharp
bool hasActiveFire = location.Features
    .OfType<HeatSourceFeature>()
    .Any(f => f.IsActive);
```

**Note**: Embers do NOT satisfy fire requirements for crafting (requires active flame).

### Action System Integration

Fire management actions are in main menu (positions 2-3) as "fundamental features":

#### Add Fuel to Fire

Location: `ActionFactory.cs` (~line 120)

**Behavior:**
- Lists all fires in location with status
- Allows selecting fire and fuel source (sticks/firewood)
- Consumes fuel items from inventory
- Calls `AddFuel(hours)` with appropriate amount
- Displays ember relight message if applicable

**Example output:**
```
Which fire do you want to fuel?
1. Campfire (glowing embers, 12 min) - warming you by +5.2°F
2. Campfire (cold)

You added a Large Stick to the fire.
The embers flare to life as you add fuel.
```

#### Start Fire

Location: `ActionFactory.cs` (~line 180)

**Behavior:**
- Only shown when cold fires exist
- Lists available fire-making recipes (Hand Drill, Bow Drill, Flint & Steel)
- Consumes materials and time based on recipe
- Performs skill check (if applicable)
- Calls `SetActive(true)` on success
- Awards XP on success/failure

**Example output:**
```
Choose a fire-making method:
1. Hand Drill (30% base, +10% per Firecraft level) - 20 minutes
   Requires: 0.5kg Wood, 0.05kg Tinder

Success! Smoke rises from the tinder. You have fire!
+3 XP to Firecraft (now level 2)
```

### Location Update Integration

**Critical**: `Location.Update()` must call `Feature.Update()` for all features:

```csharp
// In Location.cs Update method
public void Update(TimeSpan elapsed)
{
    // Update all location features (fires, foraging, etc.)
    foreach (var feature in Features)
    {
        feature.Update(elapsed);
    }
}
```

Without this, fires never deplete fuel or transition states.

---

## UI Display Patterns

### Fire Status Display

Fires display their status dynamically in "Look Around" and fire management actions:

#### Active Fire
- **Burning**: "Campfire (burning) - warming you by +15°F"
- **Dying**: "Campfire (dying, 12 min) - warming you by +15°F" (when < 15 min fuel)

#### Embers
- "Campfire (glowing embers, 8 min) - warming you by +5.2°F"

#### Cold
- "Campfire (cold)"

### Status Calculation

```csharp
string GetFireStatus(HeatSourceFeature fire)
{
    if (fire.IsActive)
    {
        if (fire.FuelRemaining < 0.25) // Less than 15 minutes
        {
            int minutesLeft = (int)(fire.FuelRemaining * 60);
            return $"(dying, {minutesLeft} min)";
        }
        return "(burning)";
    }
    else if (fire.HasEmbers)
    {
        int minutesLeft = (int)(fire.EmberTimeRemaining * 60);
        return $"(glowing embers, {minutesLeft} min)";
    }
    else
    {
        return "(cold)";
    }
}
```

### Warmth Display

Always show fire warmth contribution when present:

```csharp
double warmth = fire.GetEffectiveHeatOutput();
if (warmth > 0)
{
    status += $" - warming you by +{warmth:F1}°F";
}
```

This gives players immediate feedback on fire effectiveness.

---

## Code Examples

Fire management is handled in `GameRunner` with condition checks and action methods.

### Fire Availability Checks

```csharp
// In GameRunner - check if "Tend fire" should show
private bool HasActiveFire()
{
    var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
    if (fire == null) return false;
    return (fire.IsActive || fire.HasEmbers) && ctx.Inventory.HasFuel;
}

// Check if "Start fire" should show
private bool CanStartFire()
{
    var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
    bool noFire = fire == null;
    bool coldFire = fire != null && !fire.IsActive && !fire.HasEmbers;

    if (!noFire && !coldFire) return false;

    bool hasTool = ctx.Inventory.Tools.Any(t =>
        t.Type == ToolType.FireStriker ||
        t.Type == ToolType.HandDrill ||
        t.Type == ToolType.BowDrill);
    return hasTool && ctx.Inventory.CanStartFire;
}
```

### Tend Fire Action

```csharp
private void TendFire()
{
    var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
    var inv = ctx.Inventory;

    // Build fuel options
    var choice = new Choice<string>("Add fuel:");

    if (inv.Logs.Count > 0 && fire.CanAddFuel(FuelType.Softwood))
        choice.AddOption($"Add log ({inv.Logs.Count})", "log");

    if (inv.Sticks.Count > 0 && fire.CanAddFuel(FuelType.Kindling))
        choice.AddOption($"Add stick ({inv.Sticks.Count})", "stick");

    choice.AddOption("Done", "done");

    string selected = choice.GetPlayerChoice();
    if (selected == "done") return;

    // Add the fuel
    double weight = selected == "log" ? inv.TakeSmallestLog() : inv.TakeSmallestStick();
    var fuelType = selected == "log" ? FuelType.Softwood : FuelType.Kindling;
    fire.AddFuel(weight, fuelType);

    GameDisplay.AddNarrative($"You add a {selected} to the fire.");
    ctx.Update(1);  // 1 minute passes
}
```

### Start Fire Action

```csharp
private void StartFire()
{
    var fireTools = ctx.Inventory.Tools.Where(t =>
        t.Type == ToolType.HandDrill ||
        t.Type == ToolType.BowDrill ||
        t.Type == ToolType.FireStriker).ToList();

    // Show tool options with success chances
    foreach (var tool in fireTools)
    {
        double baseChance = GetFireToolBaseChance(tool);
        var skill = ctx.player.Skills.GetSkill("Firecraft");
        double successChance = Math.Clamp(baseChance + skill.Level * 0.1, 0.05, 0.95);
        // Display: "Hand Drill - 30% success chance"
    }

    // Attempt fire
    bool success = Utils.DetermineSuccess(successChance);
    inv.TakeTinder();  // Always consume tinder

    if (success)
    {
        inv.TakeSmallestStick();  // Consume kindling
        var newFire = new HeatSourceFeature();
        newFire.AddFuel(tinderUsed, FuelType.Tinder);
        newFire.AddFuel(kindlingUsed, FuelType.Kindling);
        newFire.IgniteFuel(FuelType.Tinder, tinderUsed);
        ctx.CurrentLocation.Features.Add(newFire);

        GameDisplay.AddSuccess("Success! You start a fire!");
        skill.GainExperience(3);
    }
    else
    {
        GameDisplay.AddWarning("You failed to start the fire.");
        skill.GainExperience(1);
    }
}
```

---

## Balance Configuration

### Key Constants

```csharp
// In HeatSourceFeature
const double MAX_FUEL_CAPACITY = 8.0;      // Hours (overnight fire)
const double DEFAULT_HEAT_OUTPUT = 15.0;   // Fahrenheit
const double EMBER_HEAT_RATIO = 0.35;      // 35% of full heat
const double EMBER_DURATION_RATIO = 0.25;  // 25% of burn time
const double FUEL_CONSUMPTION_RATE = 1.0;  // Hours per hour
```

### Tested Balance Values

**Starting fire duration**: 60 minutes
- Provides grace period for initial foraging/crafting
- Reduces new player frustration
- Long enough to gather more fuel

**Maximum fire capacity**: 8 hours
- Allows realistic overnight fires
- Reduces micromanagement tedium
- Still requires some attention (can't ignore for days)

**Ember heat output**: 35% (5.25°F from 15°F base)
- Noticeable but not sufficient for indefinite survival
- Tested against 50% (too forgiving) and 10% (negligible)

**Ember duration**: 25% of burn time
- 1-hour fire → 15-minute ember window
- 8-hour fire → 2-hour ember window
- Provides meaningful grace period without eliminating time pressure

### Tuning Recommendations

To adjust difficulty:
- **Easier**: Increase `EMBER_DURATION_RATIO` to 0.30-0.35 or `EMBER_HEAT_RATIO` to 0.40-0.45
- **Harder**: Decrease `MAX_FUEL_CAPACITY` to 6.0 or reduce starting fire to 45 minutes
- **More realistic**: Adjust `FUEL_CONSUMPTION_RATE` based on wood type (not currently implemented)

---

## Common Patterns

### Checking for Active Fire

```csharp
bool hasActiveFire = location.Features
    .OfType<HeatSourceFeature>()
    .Any(f => f.IsActive);
```

### Getting Total Warmth

```csharp
double totalWarmth = location.Features
    .OfType<HeatSourceFeature>()
    .Sum(f => f.GetEffectiveHeatOutput());
```

### Finding Best Fire for Fuel

```csharp
// Prioritize fires with embers (can relight), then dying fires
var bestFire = location.Features
    .OfType<HeatSourceFeature>()
    .OrderByDescending(f => f.HasEmbers)
    .ThenBy(f => f.FuelRemaining)
    .FirstOrDefault();
```

---

## Testing Checklist

When modifying fire management system:

- [ ] Fire depletes fuel correctly over time
- [ ] Fire transitions to embers at 25% of burn time
- [ ] Embers provide 35% heat output
- [ ] Adding fuel to embers auto-relights
- [ ] Adding fuel to cold fire does NOT auto-relight
- [ ] Fire state displays correctly in UI
- [ ] Location temperature correctly includes fire warmth
- [ ] Fire requirement blocks crafting when fire is cold
- [ ] Fire requirement satisfied by active fire only (not embers)
- [ ] Maximum 8-hour fuel capacity enforced
- [ ] `Location.Update()` properly propagates to `HeatSourceFeature.Update()`

---

## Design Rationale

### Why Three States?

**Two-state (on/off) considered and rejected:**
- Too binary, unrealistic
- No grace period for player error
- Encourages tedious micromanagement

**Three-state (burning/embers/cold) selected:**
- Matches reality
- Provides meaningful grace period
- Rewards proactive play without eliminating challenge
- UX tested positively

### Why Auto-Relight?

**Manual relight tested:**
- Required returning to inventory
- Required finding fire-making materials again
- Felt punishing for minor timing mistakes
- Added tedium without adding meaningful challenge

**Auto-relight implemented:**
- Matches reality (adding fuel to embers naturally relights)
- Rewards attention (caught fire before embers died)
- Still requires gathering fuel (not free)
- Tested with positive player feedback

### Why 8-Hour Maximum?

**Larger capacities (12-24 hours) considered:**
- Too forgiving
- Fire becomes "set and forget"
- Reduces survival tension
- Unrealistic for simple campfire

**8-hour capacity selected:**
- Allows sleeping through night
- Still requires daily attention
- Realistic for well-maintained campfire
- Balances convenience with survival pressure

---

## Future Enhancements

Potential improvements not currently implemented:

### Weather Effects
- Rain reduces effectiveness or extinguishes fire
- Wind increases consumption rate
- Snow reduces heat output

### Fuel Types
- Different wood types burn at different rates
- Hardwood (slower, hotter) vs softwood (faster, cooler)
- Special fuels (coal, fat) with unique properties

### Smoke and Visibility
- Active fires generate smoke
- Smoke attracts/repels NPCs
- Smoke visibility depends on weather

### Fire Quality
- Fire size affects heat output
- Small fire (5°F), medium (15°F), large (25°F)
- Size affects fuel consumption rate

### Advanced Embers
- Banking fire (intentionally create long-lasting embers)
- Ember quality affects relight success
- Transfer embers between locations

---

**Related Files:**
- [crafting-system.md](crafting-system.md) — Fire-starting tools (Hand Drill, Bow Drill)
- [action-system.md](action-system.md) — Runner pattern for fire actions
- [skill-check-system.md](skill-check-system.md) — Firecraft skill (vestigial)
- [overview.md](overview.md) — System overview
