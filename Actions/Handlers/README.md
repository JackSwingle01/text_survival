# Handlers vs Runners

This document explains the architectural distinction between **Handlers** and **Runners** in the Text Survival codebase.

## When to Use Handlers

**Handlers** are for **single-action flows** with **stateless game logic calculations**.

### Characteristics:
- **Static classes** with static methods
- **Stateless** - no instance state, operate only on parameters
- **Single, focused action** - one clear responsibility
- **Direct state mutation** - takes `GameContext` and mutates it directly
- **No player interaction loops** - execute logic and return

### Examples:
- `FireHandler.StartFire()` - handles fire starting logic with one attempt/retry loop
- `FireHandler.TendFire()` - handles adding fuel to fire
- `TravelHandler.ApplyTravelInjury()` - calculates and applies injury from travel
- `ConsumptionHandler.Eat()` - handles food consumption
- `TreatmentHandler.TreatInjury()` - applies medical treatment

### Pattern:
```csharp
public static class SomeHandler
{
    public static void DoAction(GameContext ctx)
    {
        // 1. Read state from ctx
        // 2. Calculate outcomes
        // 3. Mutate ctx directly
        // 4. Update display
    }
}
```

## When to Use Runners

**Runners** are for **multi-step flows** with **state** and **player interaction loops**.

### Characteristics:
- **Instance classes** - created with dependencies
- **Stateful** - holds GameContext reference and may track flow state
- **Multi-step processes** - orchestrates multiple actions over time
- **Player interaction loops** - repeatedly prompts player for decisions
- **Calls Handlers** - delegates specific actions to handlers

### Examples:
- `GameRunner` - main camp loop, orchestrates all camp activities
- `ExpeditionRunner` - manages travel, working, and returning
- `WorkRunner` - handles foraging, hunting, exploring loops
- `CraftingRunner` - multi-step crafting interface
- `HuntRunner` - interactive hunting (creates CarcassFeature on kill)
- `EncounterRunner` - predator encounter with turn-by-turn decisions
- `CombatRunner` - reusable combat module, can be called from encounters, events, hunts (creates CarcassFeature on victory)

### Pattern:
```csharp
public class SomeRunner
{
    private readonly GameContext _ctx;

    public SomeRunner(GameContext ctx)
    {
        _ctx = ctx;
    }

    public void Run()
    {
        // Multi-step loop:
        while (someCondition)
        {
            // 1. Show options
            // 2. Get player choice
            // 3. Delegate to handlers or execute
            // 4. Update state
            // 5. Repeat
        }
    }
}
```

## Key Distinctions

| Aspect | Handler | Runner |
|--------|---------|--------|
| **State** | Stateless (static) | Stateful (instance) |
| **Scope** | Single action | Multi-step flow |
| **Interaction** | No loops / simple retry | Player interaction loops |
| **Composition** | Leaf operations | Orchestrates handlers |
| **Mutability** | Direct ctx mutation | Calls handlers to mutate |

## Decision Tree

Ask yourself:

1. **Does this involve multiple player decisions in sequence?**
   - Yes → Runner
   - No → Continue

2. **Does this orchestrate multiple different actions?**
   - Yes → Runner
   - No → Continue

3. **Does this need to maintain state across multiple operations?**
   - Yes → Runner
   - No → Handler

## Composition Example

Runners call Handlers for specific operations:

```csharp
// In ExpeditionRunner.cs (Runner)
public void TravelToLocation(Location destination)
{
    // Runner handles the flow
    expedition.State = ExpeditionState.Traveling;
    bool died = RunTravelWithProgress(expedition, destination, travelTime);

    // Delegate to Handler for specific injury calculation
    if (shouldApplyInjury)
    {
        TravelHandler.ApplyTravelInjury(_ctx, destination);
    }
}
```

## Migration Guidelines

If you find yourself:
- Adding instance state to a Handler → Consider making it a Runner
- Creating a Runner with only one method → Consider making it a Handler
- Writing a Handler with multiple player input loops → It should be a Runner

## Philosophy

This separation follows the **Single Responsibility Principle**:
- **Handlers** know HOW to do one thing well (domain logic)
- **Runners** know WHEN to do things and in what order (orchestration)

This creates testable, maintainable code where:
- Handlers can be tested in isolation with different game states
- Runners can be tested for correct flow without testing domain logic
- Changes to "how" something works only affect Handlers
- Changes to "when/what order" only affect Runners
