# Plan: Move Event Checking into GameContext.Update()

## Goal
Centralize event checking so ALL time-passing activities can trigger events, with activity-based frequency multipliers.

## Design Decisions
- **Sleep interruption**: Events wake the player immediately; they can resume sleeping after
- **Camp encounters**: If an event spawns a predator encounter, it happens immediately (even during cooking/crafting)
- **Event filtering**: Extend `RequiredConditions` with activity checks (`IsSleeping`, `IsCampWork`, etc.) so events can self-filter
- **Simplified API**: ActivityType carries default config (activity level, fire proximity, status text) so callsites are clean

## Current State
- Events only fire during Work (WorkRunner) and Travel (ExpeditionRunner)
- 11 other time-passing activities skip event checks entirely (sleeping, crafting, eating, tending fire, etc.)
- `GameEventRegistry.RunTicks()` is called separately from `GameContext.Update()`

---

## Implementation

### Step 1: Add ActivityType enum with config mapping
```csharp
public enum ActivityType
{
    Idle,       // Combat, encounters, menu - no events, no render
    Sleeping,   // Rare events, low activity, high fire proximity
    Resting,    // Occasional events, normal activity, high fire proximity
    CampWork,   // Moderate events, normal activity, moderate fire proximity
    Expedition  // Full event rate, high activity, no fire proximity
}

// Config per activity (eventMultiplier, activityLevel, fireProximity, statusText)
private static readonly Dictionary<ActivityType, (double Event, double Activity, double Fire, string Status)> ActivityConfig = new()
{
    [ActivityType.Idle]       = (0.0, 1.0, 0.0, ""),
    [ActivityType.Sleeping]   = (0.1, 0.5, 2.0, "Sleeping"),
    [ActivityType.Resting]    = (0.3, 1.0, 2.0, "Resting"),
    [ActivityType.CampWork]   = (0.5, 1.0, 0.5, "Working"),
    [ActivityType.Expedition] = (1.0, 1.5, 0.0, "Traveling"),
};
```

This lets callers just pass `ActivityType` and get appropriate defaults. Overloads can still allow custom values when needed.

### Step 2: Add UpdateResult record
```csharp
public record UpdateResult(int MinutesElapsed, GameEvent? TriggeredEvent);
```

### Step 3: Simplified Update() signature
```csharp
// Expose current activity for event condition checks
public ActivityType CurrentActivity { get; private set; }

// Main update - uses ActivityConfig defaults
public UpdateResult Update(int targetMinutes, ActivityType activity, bool render = false)
{
    CurrentActivity = activity;
    var config = ActivityConfig[activity];

    int elapsed = 0;
    GameEvent? evt = null;

    while (elapsed < targetMinutes && evt == null)
    {
        elapsed++;

        // Check for event (only if activity allows events)
        if (config.Event > 0)
            evt = GameEventRegistry.GetEventOnTick(this, config.Event);

        // Update survival/zone/tensions
        UpdateInternal(1, config.Activity, GetEffectiveFireProximity(config.Fire));

        // Optional render with status from config
        if (render && !string.IsNullOrEmpty(config.Status))
            GameDisplay.Render(this, statusText: config.Status);
    }

    return new UpdateResult(elapsed, evt);
}

// Overload for custom activity/fire values (expedition work has variable fire proximity)
public UpdateResult Update(int targetMinutes, ActivityType activity, double activityLevel, double fireProximity, bool render = false, string? statusText = null)
{
    // Same logic but with custom values instead of config defaults
}

// Fire proximity is 0 if no active fire, otherwise config value
private double GetEffectiveFireProximity(double configValue)
{
    var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
    if (fire == null || !fire.IsActive) return 0;
    return configValue;
}
```

**Benefits:**
- Most callsites become `ctx.Update(5, ActivityType.CampWork, render: true)`
- No more scattered magic numbers for activityLevel/fireProximity
- `CurrentActivity` available for event condition checks

### Step 4: Add activity-based EventConditions
```csharp
// In EventCondition enum
EventCondition.IsSleeping,    // ctx.CurrentActivity == ActivityType.Sleeping
EventCondition.IsResting,     // ctx.CurrentActivity == ActivityType.Resting
EventCondition.IsCampWork,    // ctx.CurrentActivity == ActivityType.CampWork
EventCondition.IsExpedition,  // ctx.CurrentActivity == ActivityType.Expedition

// In GameContext.Check()
EventCondition.IsSleeping => CurrentActivity == ActivityType.Sleeping,
EventCondition.IsResting => CurrentActivity == ActivityType.Resting,
EventCondition.IsCampWork => CurrentActivity == ActivityType.CampWork,
EventCondition.IsExpedition => CurrentActivity == ActivityType.Expedition,
```

This lets events self-filter:
- "Strange sound" requires nothing (can wake sleeper)
- "Tool slips" requires `IsCampWork`
- "Treacherous footing" requires `IsExpedition`

### Step 5: Modify GameEventRegistry.GetEventOnTick() to accept multiplier
```csharp
public static GameEvent? GetEventOnTick(GameContext ctx, double activityMultiplier = 1.0)
{
    // Remove IsAtCamp check (now handled by ActivityType)
    double chance = BaseChancePerMinute * activityMultiplier;
    if (!Utils.DetermineSuccess(chance))
        return null;
    // ... rest unchanged
}
```

### Step 6: Update all callsites (simplified API)

**GameRunner.cs** (camp activities):
```csharp
// Before: ctx.Update(1); + ctx.Update(5, 1.0, 2.0); GameDisplay.Render(ctx, statusText: "Eating.");
// After:
var result = ctx.Update(5, ActivityType.CampWork, render: true);
if (result.TriggeredEvent != null) HandleEventAndEncounter(ctx, result.TriggeredEvent);
```
- `TendFire`: `Update(1, ActivityType.CampWork)`
- `Sleep`: Loop `Update(60, ActivityType.Sleeping, render: true)` - break on event
- `Wait`: `Update(1, ActivityType.Resting, render: true)`
- `EatDrink`: `Update(5, ActivityType.CampWork, render: true)`

**CraftingRunner.cs**:
- `DoCraft`: Loop `Update(5, ActivityType.CampWork, render: true)` with progress - break on event

**WorkRunner.cs** (uses custom fire proximity):
```csharp
// Expedition work needs variable fire proximity based on location
var result = ctx.Update(1, ActivityType.Expedition, activityLevel: 1.5, fireProximity: GetFireProximity(location), render: true, statusText: statusText);
```

**ExpeditionRunner.cs**:
- Travel: `Update(1, ActivityType.Expedition, render: true)`
- SpearRecovery: `Update(3, ActivityType.Expedition)`
- EncounterTurn: `Update(1, ActivityType.Idle)` (no events)
- CombatTurn: `Update(1, ActivityType.Idle)` (no events)
- Butchering: Loop `Update(1, ActivityType.Expedition)` - can be interrupted

### Step 7: Store PendingEncounter on GameContext
Instead of returning `EncounterConfig?` from `HandleEvent()`:

```csharp
// In GameContext
public EncounterConfig? PendingEncounter { get; set; }

// In GameEventRegistry.HandleOutcome()
if (outcome.SpawnEncounter is not null)
    ctx.PendingEncounter = outcome.SpawnEncounter;

// HandleEvent() becomes void
public static void HandleEvent(GameContext ctx, GameEvent evt) { ... }
```

Callers check `ctx.PendingEncounter` after event handling:
```csharp
var result = ctx.Update(1, ActivityType.Expedition);
if (result.TriggeredEvent is not null)
{
    GameEventRegistry.HandleEvent(ctx, result.TriggeredEvent);
    if (ctx.PendingEncounter is not null)
    {
        RunEncounter(ctx, ctx.PendingEncounter);
        ctx.PendingEncounter = null;
    }
}
```

This decouples event handling from encounter spawning and works uniformly at camp or on expedition.

---

## Files to Modify

1. **Actions/GameContext.cs**
   - Add `ActivityType` enum with `ActivityConfig` mapping
   - Add `UpdateResult` record
   - Add `CurrentActivity` and `PendingEncounter` properties
   - Add new `Update()` overloads with event checking and render option
   - Extract `UpdateInternal()` for the survival/zone/tension updates
   - Add `EventCondition` cases: `IsSleeping`, `IsResting`, `IsCampWork`, `IsExpedition`

2. **Actions/GameEventRegistry.cs**
   - Modify `GetEventOnTick()` to accept activity multiplier
   - Remove internal `IsAtCamp` check (now caller's responsibility via ActivityType)
   - Change `HandleEvent()` to return void, set `ctx.PendingEncounter` instead
   - Remove `RunTicks()` (now handled by GameContext.Update)

3. **Actions/GameRunner.cs**
   - Update 4 callsites (TendFire, Sleep, Wait, EatDrink)
   - Add event handling after each Update call
   - Add encounter spawning for camp if needed

4. **Actions/Crafting/CraftingRunner.cs**
   - Update DoCraft to loop with event checks
   - Handle event interruption (show progress, break on event)

5. **Actions/Expeditions/WorkRunner.cs**
   - Simplify `RunWorkWithProgress()` - remove `GameEventRegistry.RunTicks()` call
   - Use new `Update()` return value for event

6. **Actions/Expeditions/ExpeditionRunner.cs**
   - Simplify `RunTravelWithProgress()`
   - Update encounter/combat/butchering callsites
   - Ensure encounter spawning still works from events

---

## Event Frequency Summary

| Activity | Multiplier | ~Events/Hour |
|----------|------------|--------------|
| Idle (combat/encounter) | 0.0 | 0 |
| Sleeping | 0.1 | ~0.1 |
| Resting | 0.3 | ~0.3 |
| CampWork (craft/cook/tend) | 0.5 | ~0.5 |
| Expedition (work/travel) | 1.0 | ~1.0 |

---

## Testing Notes
- Verify sleeping can be interrupted by events
- Verify crafting can be interrupted
- Verify encounters spawn correctly at camp
- Verify existing expedition event flow unchanged
- Check that combat/encounters don't trigger events (Idle)
