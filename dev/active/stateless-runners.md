# Plan: Stateless Game Runners

## Goal
Refactor game runners to be stateless - pass in GameContext + choice, get back updated state + available choices. Enables proper web game architecture.

## Summary
**Difficulty: Medium-High** | **Scope: ~10 migration phases**

The runners don't hold field state, but they control loops and embed input handling. The fix: invert control so runners return menu data instead of blocking for input.

---

## Core Types

### GamePhase Enum
Flat enumeration of all game states (where player input is needed):
```
CampMenu, TendFire_SelectFuel, StartFire_SelectTool, StartFire_Working,
StartFire_RetryPrompt, Expedition_SelectDestination, Expedition_ActionMenu,
Work_SelectType, Work_InProgress, Work_ContinuePrompt, Hunt_*, Encounter_*, etc.
```

### GameMenuOption
```csharp
public record GameMenuOption(string Label, string ActionId, bool IsDisabled = false);
```

### GameResponse
```csharp
public record GameResponse(
    GameContext Context,
    GamePhase Phase,
    string? Prompt,
    List<GameMenuOption> Options,
    List<NarrativeEntry> Narrative,
    ProgressInfo? Progress
);
```

### PhaseState (stored on GameContext)
Carries state between multi-step flows:
```csharp
public class PhaseState {
    public Tool? SelectedFireTool;
    public int WorkTimeRemaining;
    public Location? SelectedDestination;
    public Animal? CurrentTarget;
    public GamePhase ReturnPhase; // for acknowledgment screens
}
```

---

## Architecture

### Before (blocking)
```csharp
// In GameRunner:
var choice = new Choice<Action>();
choice.AddOption("Wait", Wait);
choice.GetPlayerChoice(ctx).Invoke(); // BLOCKS
```

### After (stateless)
```csharp
// GameStateMachine.Process() returns data:
public GameResponse Process(GameContext ctx, GamePhase phase, string? choiceId)
{
    return phase switch {
        GamePhase.CampMenu => HandleCampMenu(ctx, choiceId),
        GamePhase.StartFire_SelectTool => HandleStartFireSelect(ctx, choiceId),
        // ...
    };
}

// Console/Web adapter loops:
while (alive) {
    var response = machine.Process(ctx, phase, lastChoice);
    RenderNarrative(response.Narrative);
    if (response.Options.Count > 0) {
        lastChoice = GetPlayerInput(response.Options);
    }
    phase = response.Phase;
}
```

---

## Migration Phases

### Phase 1: Infrastructure
- Add `GamePhase` enum, `GameMenuOption`, `GameResponse`, `PhaseState`
- Add `GameStateMachine` skeleton
- Add `PhaseState` property to `GameContext`
- **No behavior change** - existing game works

### Phase 2: Camp Menu
- Implement `CampMenu` handler in state machine
- Create `ConsoleGameInterface` that calls machine for camp menu
- Other actions still delegate to existing runners
- **Test:** Camp menu works through new system

### Phase 3: Simple Actions
Migrate in order (simplest first):
1. Wait (just advances time)
2. Sleep (duration selection + time passage)
3. Eat/Drink (item selection loop)
4. Cook/Melt (similar pattern)

### Phase 4: Fire System
1. TendFire (fuel selection loop with "Done" exit)
2. StartFire (tool select → progress → success/retry)
- **Complex test case** - validates multi-step flow pattern

### Phase 5: Inventory
- View, Store, Retrieve flows
- Weight limit handling

### Phase 6: Crafting
- Need selection → Option selection → Progress

### Phase 7: Work Flows
1. Forage (duration → progress → results)
2. Harvest (target → duration → progress)
3. Explore (duration → discovery)
4. Set/Check traps

### Phase 8: Expedition
Most complex - nested menus:
1. Destination selection + hazard choice
2. Travel progress
3. Action menu (work/travel/return)
4. Return flow

### Phase 9: Hunting & Encounters
1. Hunt overview → search → stalking → attack
2. Predator encounters → combat

### Phase 10: Web Integration & Cleanup
- Update `WebGameInterface` to use state machine
- Remove old runner loops
- Simplify `Input` class

---

## Key Files

| File | Changes |
|------|---------|
| `Actions/GameRunner.cs` | Extract handlers, remove loops |
| `Actions/GameContext.cs` | Add PhaseState property |
| `Actions/Expeditions/ExpeditionRunner.cs` | Extract to handlers |
| `Actions/Expeditions/WorkRunner.cs` | Extract to handlers |
| `Web/WebIO.cs` | Simplify to use GameResponse |
| **New:** `Actions/GameStateMachine.cs` | Central state processor |
| **New:** `Actions/GamePhase.cs` | Phase enum |
| **New:** `Actions/GameResponse.cs` | Response types |
| **New:** `Actions/Handlers/*.cs` | Phase handlers |

---

## Console/Web Compatibility

Both modes use the same state machine:

```csharp
// Console
while (alive) {
    var response = machine.Process(ctx, phase, choice);
    Display(response);
    choice = Input.Select(response.Options);
    phase = response.Phase;
}

// Web
while (alive) {
    var response = machine.Process(ctx, phase, choice);
    SendWebFrame(response);
    choice = WaitForWebResponse();
    phase = response.Phase;
}
```

The only difference is how input is gathered - the game logic is identical.

---

## Estimated Effort

- **Phase 1-2:** 1 session (infrastructure + camp menu proof of concept)
- **Phase 3-4:** 1-2 sessions (simple actions + fire)
- **Phase 5-7:** 2 sessions (inventory, crafting, work)
- **Phase 8-9:** 2-3 sessions (expedition + hunting - most complex)
- **Phase 10:** 1 session (cleanup)

Total: ~8-10 focused sessions, can be done incrementally with working game at each step.
