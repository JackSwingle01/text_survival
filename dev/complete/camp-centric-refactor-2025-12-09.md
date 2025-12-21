# Camp-Centric Survival Refactor Plan (Updated)
Date: 2025-12-10
Status: Architecture Refined - Ready for Implementation

 Overview

Transform the game from a location-navigation model to a camp-centric expedition model:
- Player has a camp (fire, shelter, storage) as home base
- All other locations are expedition destinations with round-trip travel
- Fire is the tether - it burns while you're away
- Actions are commitments with time calculated upfront

---

 Critical Architecture Decision: Non-Blocking Expeditions

Problem with original approach: The plan called for `ExpeditionRunner` to process expeditions in a `while(true)` loop inside a `.Do()` block. This blocks the game loop and prevents:
- Player choices during events
- UI updates during long expeditions
- Any interaction until expedition completes

Solution: Expeditions are state machines executed as chained actions, not blocking loops.

 The Pattern

Instead of:
```csharp
// BAD - Blocking
.Do(ctx => {
    while (!expedition.IsComplete) {
        runner.ProcessChunk(expedition, player);
    }
})
```

Use:
```csharp
// GOOD - Non-blocking, chainable
public static IGameAction ProcessExpeditionChunk(Expedition expedition)
{
    return CreateAction($"{expedition.CurrentPhase}...")
        .Do(ctx => {
            var result = ExpeditionRunner.ProcessChunk(expedition, ctx.player);
            // Process ONE chunk only
        })
        .ThenShow(ctx => GetNextExpeditionAction(expedition))
        .Build();
}

private static List<IGameAction> GetNextExpeditionAction(Expedition expedition)
{
    // Check for events first
    var evt = ExpeditionRunner.CheckForEvents(expedition);
    if (evt != null)
        return GetEventResponses(evt, expedition);

    // Check if complete
    if (expedition.IsComplete)
        return [ShowExpeditionResults(expedition)];

    // Continue to next chunk (recursive chain)
    return [ProcessExpeditionChunk(expedition)];
}
```

This makes each chunk a discrete action. The action system's `ThenShow` creates the loop, but player can make choices at event boundaries.

---

 Phase 1: Camp Foundation (Done)

 1.1 CampManager
File: `Actors/Player/CampManager.cs` - Already exists

 1.2 Player/Context Integration
- `Player.cs` has `Camp` property
- `GameContext.cs` has camp context helpers

 1.3 Program.cs
- Initial camp is set on game start

---

 Phase 2: Expedition Core (Partially Done)

 2.1 Expedition Infrastructure

Existing Files (in `Actions/Expeditions/`):
- `ExpeditionPhase.cs` - Phase enum
- `ExpeditionType.cs` - Type enum (Forage, Hunt, etc.)
- `Expedition.cs` - Data class

Needs Update: `Expedition.cs` should have:
```csharp
public class Expedition
{
    // Identity
    public Location startLocation { get; }
    public Location endLocation { get; }
    public ExpeditionType Type { get; }

    // Time calculations
    public int TravelTimeMinutes { get; }
    public int WorkTimeMinutes { get; }
    public int TimeVarianceMinutes { get; }
    public int TotalEstimatedTimeMinutes => (TravelTimeMinutes * 2) + WorkTimeMinutes;

    // Risk factors
    public double ExposureFactor { get; }
    public double DetectionRisk { get; }

    // State (mutable)
    public ExpeditionPhase CurrentPhase { get; private set; }
    public int MinutesElapsedPhase { get; private set; }
    public int MinutesElapsedTotal { get; private set; }

    // Results
    public List<Item> LootCollected { get; } = new();
    public List<string> EventsLog { get; } = new();

    // State transitions
    public void IncrementTime(int minutes);
    public bool IsPhaseComplete();
    public void AdvancePhase();
    public bool IsComplete => CurrentPhase == ExpeditionPhase.Completed;
}
```

 2.2 Fire Margin Calculator

New File: `Actions/Expeditions/FireMarginCalculator.cs`
```csharp
public enum MarginStatus { Comfortable, Tight, Risky, VeryRisky, NoFire }

public static class FireMarginCalculator
{
    public static MarginStatus GetStatus(double marginMinutes)
    {
        if (double.IsNegativeInfinity(marginMinutes)) return NoFire;
        if (marginMinutes > 30) return Comfortable;
        if (marginMinutes > 0) return Tight;
        if (marginMinutes > -15) return Risky;
        return VeryRisky;
    }

    public static string GetWarningMessage(MarginStatus status, double margin) { ... }
}
```

 2.3 Expedition Runner (Update Needed)

File: `Actions/Expeditions/ExpeditionRunner.cs`

Change from "runs entire expedition" to "processes single chunk":

```csharp
public class ExpeditionRunner
{
    private const int CHUNK_SIZE_MINUTES = 5;

    public class ChunkResult
    {
        public int MinutesProcessed { get; init; }
        public bool PhaseCompleted { get; init; }
        public ExpeditionEvent? Event { get; init; }
    }

    /// <summary>
    /// Process ONE chunk of expedition time. Does not loop.
    /// </summary>
    public static ChunkResult ProcessChunk(Expedition expedition, Player player)
    {
        int processed = 0;
        while (processed < CHUNK_SIZE_MINUTES)
        {
            World.Update(1, true);
            player.Update();
            expedition.IncrementTime(1);
            processed++;

            // Check for events
            var evt = CheckForEvents(expedition, player);
            if (evt != null)
            {
                return new ChunkResult
                {
                    MinutesProcessed = processed,
                    Event = evt
                };
            }

            // Check for phase completion
            if (expedition.IsPhaseComplete())
            {
                return new ChunkResult
                {
                    MinutesProcessed = processed,
                    PhaseCompleted = true
                };
            }
        }

        return new ChunkResult { MinutesProcessed = processed };
    }

    public static ExpeditionEvent? CheckForEvents(Expedition exp, Player player) { ... }
    public static void DisplayPreview(Expedition exp, double fireMinutes) { ... }
}
```

---

 Phase 3: Expedition Actions (Core Focus)

 3.1 Action Architecture

File: `Actions/Expeditions/ExpeditionActions.cs`

The expedition flow is a series of chained actions:

```
SelectForageExpedition()
    --> ShowDestinationPicker()
        --> ShowExpeditionPlan(expedition)
            --> ProcessExpeditionChunk(expedition)  <--+
                |-- [Event] --> HandleEvent(event)    |
                |               --> Continue/Abort ---+
                |-- [Phase Done] --> ProcessNext... --+
                --> [Complete] --> ShowResults(expedition)
                                    --> Return to MainMenu
```

 3.2 Key Actions

```csharp
public static class ExpeditionActions
{
    // Entry point: Select expedition type and destination
    public static IGameAction SelectForageExpedition()
    {
        return CreateAction("Forage")
            .ThenShow(ctx => GetForageDestinations(ctx))
            .Build();
    }

    private static List<IGameAction> GetForageDestinations(GameContext ctx)
    {
        var locations = ctx.Camp.GetNearbyForageLocations();
        var actions = locations.Select(loc =>
            CreateDestinationAction(loc, ExpeditionType.Forage, ctx)
        ).ToList();
        actions.Add(ActionFactory.Common.Return("Never mind"));
        return actions;
    }

    private static IGameAction CreateDestinationAction(
        Location loc, ExpeditionType type, GameContext ctx)
    {
        var expedition = ExpeditionFactory.Create(type, ctx.CampLocation, loc);
        var margin = ctx.Camp.GetFireMarginMinutes(expedition.TotalEstimatedTimeMinutes);
        var status = FireMarginCalculator.GetStatus(margin);

        string label = $"{loc.Name} (~{expedition.TotalEstimatedTimeMinutes} min)";
        if (status == MarginStatus.Risky || status == MarginStatus.VeryRisky)
            label += " ⚠";

        return CreateAction(label)
            .ThenShow(_ => [ShowExpeditionPlan(expedition)])
            .Build();
    }

    // Preview and confirm
    public static IGameAction ShowExpeditionPlan(Expedition expedition)
    {
        return CreateAction("Review Plan")
            .Do(ctx => ExpeditionRunner.DisplayPreview(
                expedition,
                ctx.Camp.GetFireMinutesRemaining()))
            .WithPrompt("Proceed with this expedition?")
            .ThenShow(_ => [
                StartExpedition(expedition),
                ActionFactory.Common.Return("Never mind")
            ])
            .Build();
    }

    // Begin expedition - transitions to first chunk
    public static IGameAction StartExpedition(Expedition expedition)
    {
        return CreateAction("Begin Expedition")
            .Do(ctx => {
                Output.WriteLine($"You set out for {expedition.endLocation.Name}...");
                expedition.AdvancePhase(); // NotStarted --> TravelingOut
            })
            .ThenShow(_ => [ProcessExpeditionChunk(expedition)])
            .Build();
    }

    // Core loop action - processes ONE chunk, chains to next
    public static IGameAction ProcessExpeditionChunk(Expedition expedition)
    {
        string phaseName = expedition.CurrentPhase switch
        {
            ExpeditionPhase.TravelingOut => "Traveling...",
            ExpeditionPhase.Working => "Working...",
            ExpeditionPhase.TravelingBack => "Returning...",
            _ => "..."
        };

        return CreateAction(phaseName)
            .Do(ctx => {
                var result = ExpeditionRunner.ProcessChunk(expedition, ctx.player);

                // Store result for ThenShow to use
                ctx.LastChunkResult = result;

                if (result.PhaseCompleted)
                    expedition.AdvancePhase();
            })
            .ThenShow(ctx => GetNextExpeditionActions(expedition, ctx))
            .Build();
    }

    private static List<IGameAction> GetNextExpeditionActions(
        Expedition expedition, GameContext ctx)
    {
        var result = ctx.LastChunkResult;

        // Event occurred - show event responses
        if (result?.Event != null)
            return GetEventResponses(result.Event, expedition);

        // Expedition complete - show results
        if (expedition.IsComplete)
            return [ShowExpeditionResults(expedition)];

        // Continue to next chunk (automatic, player just sees progress)
        return [ProcessExpeditionChunk(expedition)];
    }

    // Event handling
    private static List<IGameAction> GetEventResponses(
        ExpeditionEvent evt, Expedition expedition)
    {
        return evt.Type switch
        {
            ExpeditionEventType.FireWarning => [
                ContinueExpedition(expedition, "Press on"),
                AbortExpedition(expedition, "Head back now")
            ],
            ExpeditionEventType.Encounter => [
                FightEncounter(expedition, evt),
                FleeEncounter(expedition, evt)
            ],
            _ => [ContinueExpedition(expedition, "Continue")]
        };
    }

    // Results
    public static IGameAction ShowExpeditionResults(Expedition expedition)
    {
        return CreateAction("Expedition Complete")
            .Do(ctx => {
                Output.WriteLine($"You return to camp after {expedition.MinutesElapsedTotal} minutes.");
                // Display loot, fire status, etc.
            })
            .ThenReturn() // Back to main menu
            .Build();
    }
}
```

 3.3 GameContext Addition

File: `Actions/GameContext.cs`

Add field to pass chunk results between Do and ThenShow:
```csharp
public ExpeditionRunner.ChunkResult? LastChunkResult { get; set; }
```

---

 Phase 4: Adapt Existing Systems

 4.1 Foraging --> Expedition Work Phase

During `Working` phase for forage expeditions, call existing `ForageFeature`:

```csharp
// Inside ProcessChunk when phase is Working:
if (expedition.Type == ExpeditionType.Forage)
{
    var feature = expedition.endLocation.GetFeature<ForageFeature>();
    var items = feature?.ForageOnce(); // Get items for this work increment
    if (items != null)
        expedition.LootCollected.AddRange(items);
}
```

 4.2 Main Menu Refactor

File: `Actions/ActionFactory.cs`

```csharp
public static IGameAction MainMenu()
{
    return CreateAction("Main Menu")
        .Do(ctx => {
            ShowSurvivalStats(ctx);
            CampStatusDisplay.Show(ctx.Camp);
        })
        .ThenShow(ctx => GetCampActions(ctx))
        .Build();
}

private static List<IGameAction> GetCampActions(GameContext ctx)
{
    return [
        // Camp actions (instant, at camp)
        Survival.TendFire(),
        Survival.Rest(),
        Survival.EatDrink(),
        Crafting.OpenCraftingMenu(),

        // Expeditions (round-trip)
        ExpeditionActions.SelectForageExpedition(),
        ExpeditionActions.SelectHuntExpedition(),
        ExpeditionActions.SelectExploreExpedition(),

        // Major decision
        Movement.MoveCamp(),
    ];
}
```

---

 Phase 5: Events (MVP)

 5.1 Event Types

New File: `Actions/Expeditions/ExpeditionEvent.cs`
```csharp
public enum ExpeditionEventType
{
    FireWarning,    // Fire margin getting dangerous
    Encounter,      // Wildlife encounter
    Discovery,      // Found something interesting
    Weather,        // Weather change
    Injury          // Minor injury event
}

public class ExpeditionEvent
{
    public ExpeditionEventType Type { get; init; }
    public string Description { get; init; }
    public object? Data { get; init; } // Animal for encounters, Item for discoveries, etc.
}
```

 5.2 Event Checking (Simple MVP)

```csharp
public static ExpeditionEvent? CheckForEvents(Expedition exp, Player player)
{
    // Fire warning: check every chunk during travel back
    if (exp.CurrentPhase == ExpeditionPhase.TravelingBack)
    {
        var remainingTime = exp.GetRemainingTimeEstimate();
        var fireMargin = player.Camp.GetFireMarginMinutes(remainingTime);
        if (fireMargin < 10 && !exp.HasWarnedAboutFire)
        {
            exp.HasWarnedAboutFire = true;
            return new ExpeditionEvent
            {
                Type = ExpeditionEventType.FireWarning,
                Description = $"Your fire will have only ~{fireMargin:F0} minutes when you return."
            };
        }
    }

    // Random encounter during work phase
    if (exp.CurrentPhase == ExpeditionPhase.Working)
    {
        if (Random.Shared.NextDouble() < exp.DetectionRisk * 0.1)
        {
            // Create encounter event
        }
    }

    return null;
}
```

---

 Implementation Order (Updated)

 Batch 1: Foundation (Done)
- CampManager exists
- Player/Context have camp access
- Expedition data class exists

 Batch 2: Runner Refactor
1. Update `ExpeditionRunner.cs` to process single chunks (not loop)
2. Add `ChunkResult` class with event support
3. Add `CheckForEvents()` stub (returns null for now)
4. Test: Can process one chunk and return

 Batch 3: Action Chain Pattern
1. Update `ExpeditionActions.cs` with non-blocking pattern
2. Add `LastChunkResult` to `GameContext`
3. Implement: `SelectForageExpedition` --> `ShowExpeditionPlan` --> `ProcessExpeditionChunk` chain
4. Test: Can start and complete a forage expedition

 Batch 4: Fire Margin Integration
1. Create `FireMarginCalculator.cs`
2. Add margin display to destination picker
3. Add margin display to expedition preview
4. Test: Fire warnings show correctly

 Batch 5: Events MVP
1. Create `ExpeditionEvent.cs`
2. Implement fire warning event
3. Implement event response actions
4. Test: Fire warning interrupts expedition

 Batch 6: Main Menu Integration
1. Update `ActionFactory.MainMenu()` to use expedition actions
2. Remove old movement/forage actions
3. Add `CampStatusDisplay`
4. Full integration test

---

 Files to Modify/Create

 Modify
| File | Changes |
|------|---------|
| `Actions/Expeditions/ExpeditionRunner.cs` | Single-chunk processing, no loop |
| `Actions/Expeditions/ExpeditionActions.cs` | Non-blocking chained actions |
| `Actions/GameContext.cs` | Add `LastChunkResult` field |
| `Actions/ActionFactory.cs` | New main menu with expedition actions |

 Create
| File | Purpose |
|------|---------|
| `Actions/Expeditions/FireMarginCalculator.cs` | Margin status and warnings |
| `Actions/Expeditions/ExpeditionEvent.cs` | Event types and data |
| `UI/CampStatusDisplay.cs` | Camp status rendering |

---

 Key Design Decisions

1. Non-blocking execution - Expeditions chain actions via `ThenShow`, not blocking loops
2. Chunk results in context - `GameContext.LastChunkResult` passes data between `Do` and `ThenShow`
3. Events interrupt naturally - Events are just different `ThenShow` results
4. ActionBuilder unchanged - No need to modify the builder pattern
5. ExpeditionRunner is stateless - It processes chunks, actions manage state flow

---

 Data Flow: Expedition

```
1. Player selects action type (Forage)
         |
2. Player selects location (Deep Forest)
         |
3. System calculates expedition shape:
   - Travel out: 25 min
   - Work time: 20 min (+/-10 variance)
   - Travel back: 25 min
   - Total estimate: 70 min (range: 60-80)
         |
4. System checks fire margin:
   - Fire remaining: 85 min
   - Margin after: 5-25 min
   - Verdict: Tight but possible
         |
5. Player sees summary and confirms:
   "Deep Forest - ~70 min round trip
    Fire will have ~15 min remaining
    Proceed?"
         |
6. Expedition begins
         |
7. Phase: TRAVEL OUT
   - Process in chunks (5 min each)
   - Each chunk: tick survival, tick world (fire burns)
   - Event check each chunk (lower probability)
   - Player not shown granular updates, just interrupts
         |
8. Phase: WORK
   - Roll actual duration (20 min +/- 10)
   - Process in chunks
   - Each chunk: tick survival, tick world
   - Event check each chunk (higher probability)
   - Events may offer abort: "Storm coming. Head back now?"
         |
9. Phase: TRAVEL BACK
   - Same as travel out
   - Events can still happen
   - Tension if fire margin is thin
         |
10. Arrival
    - Results presented (what you found/caught)
    - Fire status shown
    - Consequences resolved (if fire died, if injured, etc.)
         |
11. Back to camp menu
```

---

 Fire Margin Check Logic

```csharp
CheckFireMargin(expedition):

    fireRemaining = camp.fire.MinutesRemaining
    expeditionTime = expedition.TotalEstimatedTime

    margin = fireRemaining - expeditionTime

    if margin > 30:
        // Comfortable, no warning
        return true

    if margin > 0:
        // Tight
        Warn("Fire will have ~{margin} min when you return")
        return Confirm()

    if margin > -15:
        // Risky
        Warn("Fire may die before you return")
        return Confirm()

    // Very risky
    Warn("Fire will likely die. You'll return to cold camp.")
    return Confirm()
```

---

 Event Integration Points

Events belong to phases and contexts:

```
TravelOut/TravelBack:
    - Weather events (storm approaching)
    - Navigation events (get lost, find shortcut)
    - Wildlife sighting (distant, not engaged)
    - Injury events (slip, fall)

Work (Forage):
    - Discovery events (find cache, corpse, new location)
    - Wildlife encounters (closer, more likely)
    - Tool breaks
    - Bonus finds

Work (Hunt):
    - Prey behavior (spooks, moves, charges)
    - Other predators
    - Shot outcomes

Work (Explore):
    - Discovery events (primary purpose)
    - Danger events (stumble into predator den)
```

---

 Action Type Summary

| Type | Travel | Fire Margin Check | Examples |
|------|--------|-------------------|----------|
| Camp Action | None | No | Tend fire, craft, rest, eat |
| Expedition | Round-trip | Yes (automatic) | Forage, hunt, explore |
| Move Camp | One-way | N/A (leaving fire) | Relocate camp |
