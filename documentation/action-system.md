# Runner Architecture

*Created: 2024-12*
*Last Updated: 2025-12-20*

How player interaction flows through Runners and the Choice pattern.

## Table of Contents

- [Overview](#overview)
- [Runner Pattern](#runner-pattern)
- [Choice Pattern](#choice-pattern)
- [GameContext](#gamecontext)
- [Runner Hierarchy](#runner-hierarchy)
- [Common Patterns](#common-patterns)

---

## Overview

Player interaction is handled by **Runners** — classes that own a game loop and present choices to the player. Runners replaced the earlier ActionBuilder fluent pattern with explicit, procedural control flow.

**Key Files:**
- `Actions/GameRunner.cs` — Main camp loop
- `Actions/Expeditions/ExpeditionRunner.cs` — Travel and expedition logic
- `Actions/Expeditions/WorkRunner.cs` — Work activities (forage, hunt, explore)
- `Actions/CraftingRunner.cs` — Crafting menu

**Why Runners?**
- Explicit control flow (easy to trace)
- Clear ownership of game state changes
- No hidden behavior in builder chains
- Natural for sequential player decisions

---

## Runner Pattern

Each Runner follows the same structure:

```csharp
public class SomeRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;

    public void Run()
    {
        // Main loop - keep going until done
        while (ShouldContinue())
        {
            // 1. Render current state
            GameDisplay.Render(_ctx, statusText: "Current status");

            // 2. Build available choices
            var choice = new Choice<string>("What do you do?");

            if (CanDoThing())
                choice.AddOption("Do thing", "thing");

            choice.AddOption("Done", "done");

            // 3. Get player choice and execute
            string action = choice.GetPlayerChoice();

            switch (action)
            {
                case "thing":
                    DoThing();
                    break;
                case "done":
                    return;
            }
        }
    }

    private void DoThing()
    {
        // Update game state
        _ctx.Update(10); // 10 minutes pass

        // Show results
        GameDisplay.AddNarrative("You did the thing.");
    }
}
```

### Key Principles

1. **Runner owns its loop** — Each Runner controls when it exits
2. **GameContext is passed in** — Runners share state through GameContext
3. **Choices are built dynamically** — Available options depend on current state
4. **Actions update time** — Most actions call `_ctx.Update(minutes)`

---

## Choice Pattern

The `Choice<T>` class handles player selection:

```csharp
public class Choice<T>(string? prompt = null)
{
    public void AddOption(string label, T item);
    public T GetPlayerChoice();
}
```

### Usage Examples

**Action selection (using delegates):**
```csharp
var choice = new Choice<Action>();
choice.AddOption("Wait", Wait);           // Method reference
choice.AddOption("Tend fire", TendFire);
choice.GetPlayerChoice().Invoke();        // Execute selected action
```

**Value selection:**
```csharp
var choice = new Choice<Location?>("Where do you go?");
choice.AddOption("Frozen Creek (~12 min)", frozenCreek);
choice.AddOption("Dense Woods (~8 min)", denseWoods);
choice.AddOption("Cancel", null);

var destination = choice.GetPlayerChoice();
if (destination != null)
    TravelTo(destination);
```

**String-based branching:**
```csharp
var choice = new Choice<string>("What do you do?");
choice.AddOption("Work here", "work");
choice.AddOption("Travel", "travel");
choice.AddOption("Return to camp", "return");

string action = choice.GetPlayerChoice();
switch (action)
{
    case "work": DoWork(); break;
    case "travel": DoTravel(); break;
    case "return": ReturnToCamp(); break;
}
```

### Choice Best Practices

- **Conditional options** — Only add options when they're valid
- **Descriptive labels** — Include relevant info (travel time, resource quality)
- **Always have an exit** — Include "Cancel" or "Done" option
- **Use appropriate T** — `Action` for direct execution, `string` for branching, specific types for selection

---

## GameContext

`GameContext` is the central hub holding all game state:

```csharp
public class GameContext
{
    public Player player;
    public Camp Camp;
    public Inventory Inventory => player.Inventory;
    public Zone Zone;
    public Location CurrentLocation => player.CurrentLocation;
    public Expedition? Expedition;

    // Time update - ticks all systems
    public void Update(int minutes);

    // Condition checking for events
    public bool Check(EventCondition condition);
}
```

### Update Flow

When `ctx.Update(minutes)` is called:
1. Player survival stats tick (energy, hydration, temperature)
2. Effects tick (bleeding, hypothermia decay)
3. Zone updates (fire burns down, features respawn)
4. Events may trigger based on current state

### Passing Context

Runners receive GameContext in their constructor and share it with sub-runners:

```csharp
public class GameRunner(GameContext ctx)
{
    private void LeaveCamp()
    {
        var expeditionRunner = new ExpeditionRunner(ctx);  // Same context
        expeditionRunner.Run();
    }
}
```

---

## Runner Hierarchy

```
GameRunner (main loop)
├── ExpeditionRunner (leaving camp)
│   └── WorkRunner (activities at locations)
├── CraftingRunner (making items)
└── Direct methods (Wait, TendFire, EatDrink, etc.)
```

### GameRunner

Main camp loop. Runs while player is alive.

**Responsibilities:**
- Fire management (tend, start)
- Eating/drinking
- Cooking/melting snow
- Sleeping
- Inventory management
- Launching expeditions and crafting

**Key methods:**
- `Run()` — Main loop
- `RunCampMenu()` — Build and execute camp choices
- `TendFire()`, `StartFire()` — Fire management
- `EatDrink()`, `CookMelt()` — Consumption
- `Sleep()`, `Wait()` — Time passage

### ExpeditionRunner

Handles leaving camp, traveling, and returning.

**Responsibilities:**
- Travel between locations
- Work at locations (delegates to WorkRunner)
- Return to camp (backtracking)
- Expedition summary on return

**Key methods:**
- `Run()` — Main expedition loop
- `DoTravel()` — Location selection and movement
- `DoWork()` — Delegate to WorkRunner
- `ReturnToCamp()` — Backtrack to camp

### WorkRunner

Activities at a location (not at camp).

**Responsibilities:**
- Foraging
- Hunting (stalking, encounters)
- Exploring (discovering locations)
- Harvesting specific resources

**Key methods:**
- `DoForage()` — Search for resources
- `DoHunt()` — Find and stalk game
- `DoExplore()` — Discover new locations
- `DoHarvest()` — Collect from harvestable features

### CraftingRunner

Need-based crafting interface.

**Responsibilities:**
- Show craftable items by need category
- Check material requirements
- Execute crafting (time, materials, results)

---

## Common Patterns

### Conditional Menu Options

```csharp
var choice = new Choice<Action>();

// Only show if condition is met
if (HasActiveFire())
    choice.AddOption("Tend fire", TendFire);

if (ctx.Inventory.HasFood)
    choice.AddOption("Eat", Eat);

// Always have at least one option
choice.AddOption("Wait", Wait);
```

### Time-Consuming Actions

```csharp
private void DoForage()
{
    GameDisplay.AddNarrative("You search the area...");

    // Time passes
    _ctx.Update(30); // 30 minutes

    // Check if player died during activity
    if (!_ctx.player.IsAlive) return;

    // Do the thing
    var results = feature.Forage(30, _ctx.player);

    // Show results
    GameDisplay.AddNarrative($"You found {results.Count} items.");
}
```

### Sub-Runner Delegation

```csharp
private void LeaveCamp()
{
    // Create sub-runner with same context
    var runner = new ExpeditionRunner(_ctx);

    // Run it (blocks until done)
    runner.Run();

    // Back in GameRunner - player returned to camp
    GameDisplay.Render(_ctx, statusText: "Back at camp.");
}
```

### Static Availability Checks

```csharp
// WorkRunner exposes static check for other runners
public static bool HasWorkOptions(GameContext ctx, Location location)
{
    return location.HasFeature<ForageFeature>()
        || location.HasFeature<AnimalTerritoryFeature>()
        || ctx.Zone.HasUnrevealedLocations();
}

// Used by GameRunner
if (WorkRunner.HasWorkOptions(ctx, location))
    choice.AddOption("Work here", "work");
```

---

## Adding New Actions

### At Camp (GameRunner)

1. Add condition check method:
```csharp
private bool CanDoNewThing() => /* condition */;
```

2. Add to `RunCampMenu()`:
```csharp
if (CanDoNewThing())
    choice.AddOption("Do new thing", DoNewThing);
```

3. Implement the action:
```csharp
private void DoNewThing()
{
    // Time, state changes, display
}
```

### On Expedition (ExpeditionRunner/WorkRunner)

Same pattern, but in the appropriate runner. Work activities go in WorkRunner, travel-related in ExpeditionRunner.

### New Runner

For complex new features with their own loop:

```csharp
public class NewFeatureRunner(GameContext ctx)
{
    public void Run()
    {
        // Your loop here
    }
}

// Called from parent runner
var runner = new NewFeatureRunner(_ctx);
runner.Run();
```

---

**Related Files:**
- [overview.md](overview.md) — System overview
- [principles.md](principles.md) — Design philosophy
