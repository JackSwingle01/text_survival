using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Api;

/// <summary>
/// Pure function game engine. Each action transforms state and returns a response.
/// This is the core of the stateless REST API - no blocking I/O, no session state.
/// </summary>
public static class GameEngine
{
 
    /// <summary>
    /// Create a new game and return initial state.
    /// </summary>
    public static GameContext CreateNewGame()
    {
        GameEventRegistry.ClearTriggerTimes();
        return GameContext.CreateNewGame();
    }

    /// <summary>
    /// Build a WebFrame response from current game state.
    /// Automatically includes overlays for any pending activities.
    /// </summary>
    public static WebFrame BuildFrame(GameContext ctx, FrameMode? mode = null, List<Overlay>? overlays = null)
    {
        var state = GameStateDto.FromContext(ctx);
        mode ??= BuildDefaultMode(ctx);
        overlays ??= new List<Overlay>();

        // Add pending activity overlays if not already provided
        if (ctx.PendingActivity != null && overlays.Count == 0)
        {
            AddPendingActivityOverlays(ctx, overlays);
        }

        return new WebFrame(state, mode, overlays, Input: null);
    }

    /// <summary>
    /// Add overlays for the current pending activity state.
    /// </summary>
    private static void AddPendingActivityOverlays(GameContext ctx, List<Overlay> overlays)
    {
        if (ctx.PendingActivity == null) return;

        switch (ctx.PendingActivity.Phase)
        {
            case ActivityPhase.EventPending:
                if (ctx.PendingActivity.Event != null)
                {
                    var eventDto = new EventDto(
                        Name: ctx.PendingActivity.Event.Id,
                        Description: ctx.PendingActivity.Event.Description,
                        Choices: ctx.PendingActivity.Event.Choices.Select(c =>
                            new EventChoiceDto(
                                Id: c.Id,
                                Label: c.Text,
                                Description: c.Text,
                                IsAvailable: true,
                                Cost: null
                            )
                        ).ToList()
                    );
                    overlays.Add(new EventOverlay(eventDto));
                }
                break;

            case ActivityPhase.EventOutcomeShown:
                if (ctx.PendingActivity.Outcome != null)
                {
                    var outcomeDto = new EventOutcomeDto(
                        Message: ctx.PendingActivity.Outcome.Description,
                        TimeAddedMinutes: 0,
                        EffectsApplied: new List<string>(),
                        DamageTaken: new List<string>(),
                        ItemsGained: new List<string>(),
                        ItemsLost: new List<string>(),
                        TensionsChanged: new List<string>()
                    );
                    overlays.Add(new EventOutcomeOverlay(outcomeDto));
                }
                break;

            case ActivityPhase.HuntActive:
            case ActivityPhase.HuntResult:
                if (ctx.PendingActivity.Hunt != null)
                {
                    var huntDto = BuildHuntDto(ctx.PendingActivity.Hunt);
                    overlays.Add(new HuntOverlay(huntDto));
                }
                break;

            case ActivityPhase.EncounterActive:
            case ActivityPhase.EncounterOutcome:
                if (ctx.PendingActivity.Encounter != null)
                {
                    var encounterDto = BuildEncounterDto(ctx, ctx.PendingActivity.Encounter);
                    overlays.Add(new EncounterOverlay(encounterDto));
                }
                break;

            case ActivityPhase.CombatIntro:
            case ActivityPhase.CombatPlayerTurn:
            case ActivityPhase.CombatAnimalTurn:
            case ActivityPhase.CombatResult:
                if (ctx.PendingActivity.Combat != null)
                {
                    var combatDto = BuildCombatDto(ctx, ctx.PendingActivity.Combat);
                    overlays.Add(new CombatOverlay(combatDto));
                }
                break;
        }
    }

    private static HuntDto BuildHuntDto(HuntSnapshot hunt)
    {
        return new HuntDto(
            AnimalName: hunt.AnimalType,
            AnimalDescription: $"A {hunt.AnimalType.ToLower()}",
            AnimalActivity: "grazing",
            AnimalState: hunt.IsActive ? "alert" : "idle",
            CurrentDistanceMeters: hunt.Distance,
            PreviousDistanceMeters: null,
            IsAnimatingDistance: false,
            MinutesSpent: hunt.Approaches * 5,
            StatusMessage: null,
            Choices: hunt.IsActive
                ? new List<HuntChoiceDto>
                {
                    new("approach", "Approach", "Get closer to the prey", true, null),
                    new("throw", "Throw", "Throw your weapon", hunt.Distance < 30, hunt.Distance >= 30 ? "Too far" : null),
                    new("abandon", "Abandon", "Give up the hunt", true, null)
                }
                : new List<HuntChoiceDto>(),
            Outcome: hunt.IsActive ? null : new HuntOutcomeDto(
                Result: hunt.Phase,
                Message: $"The {hunt.AnimalType.ToLower()} {(hunt.Phase == "killed" ? "falls" : "escapes")}.",
                TotalMinutesSpent: hunt.Approaches * 5,
                ItemsGained: new List<string>(),
                EffectsApplied: new List<string>(),
                TransitionToCombat: false
            )
        );
    }

    private static EncounterDto BuildEncounterDto(GameContext ctx, EncounterSnapshot encounter)
    {
        // Calculate threat factors dynamically from current context
        var threatFactors = new List<ThreatFactorDto>();
        if (ctx.Inventory.GetWeight(ResourceCategory.Food) > 0.5)
            threatFactors.Add(new ThreatFactorDto("meat", "Carrying meat", "restaurant"));
        if (ctx.player.Vitality < 0.5)
            threatFactors.Add(new ThreatFactorDto("weakness", "Showing weakness", "personal_injury"));
        if (ctx.Inventory.Weapon == null)
            threatFactors.Add(new ThreatFactorDto("unarmed", "Unarmed", "shield"));

        return new EncounterDto(
            PredatorName: encounter.AnimalType,
            CurrentDistanceMeters: encounter.Distance,
            PreviousDistanceMeters: null,
            IsAnimatingDistance: false,
            BoldnessLevel: encounter.Boldness,
            BoldnessDescriptor: encounter.Boldness > 0.7 ? "aggressive" : encounter.Boldness > 0.4 ? "wary" : "hesitant",
            ThreatFactors: threatFactors,
            StatusMessage: null,
            Choices: encounter.AvailableActions.Select(a => new EncounterChoiceDto(
                Id: a,
                Label: a switch
                {
                    "stand" => "Stand Ground",
                    "back" => "Back Away",
                    "attack" => "Attack",
                    "run" => "Run",
                    _ => a
                },
                Description: null,
                IsAvailable: true,
                DisabledReason: null
            )).ToList(),
            Outcome: null
        );
    }

    private static CombatDto BuildCombatDto(GameContext ctx, CombatSnapshot combat)
    {
        // Build grid from CombatScenario if available
        CombatGridDto? grid = null;
        if (ctx.PendingActivity?.CombatScenario != null)
        {
            var scenario = ctx.PendingActivity.CombatScenario;
            var units = new List<CombatUnitDto>();

            // Add player units
            foreach (var unit in scenario.Team1)
            {
                units.Add(new CombatUnitDto(
                    Id: unit.actor.Name,
                    Name: unit.actor.Name,
                    Team: "player",
                    Position: new CombatGridPositionDto(unit.Position.X, unit.Position.Y),
                    Vitality: unit.actor.Vitality,
                    HealthDescription: GetHealthDescription(unit.actor.Vitality),
                    Threat: unit.Threat,
                    Boldness: unit.Boldness,
                    Aggression: unit.Aggression,
                    BoldnessDescriptor: GetBoldnessDescriptor(unit.Boldness),
                    Icon: "🧑"
                ));
            }

            // Add enemy units
            foreach (var unit in scenario.Team2)
            {
                units.Add(new CombatUnitDto(
                    Id: unit.actor.Name,
                    Name: unit.actor.Name,
                    Team: "enemy",
                    Position: new CombatGridPositionDto(unit.Position.X, unit.Position.Y),
                    Vitality: unit.actor.Vitality,
                    HealthDescription: GetHealthDescription(unit.actor.Vitality),
                    Threat: unit.Threat,
                    Boldness: unit.Boldness,
                    Aggression: unit.Aggression,
                    BoldnessDescriptor: GetBoldnessDescriptor(unit.Boldness),
                    Icon: "🐺"
                ));
            }

            grid = new CombatGridDto(
                GridSize: 25,
                CellSizeMeters: 1.0,
                Units: units
            );
        }

        return new CombatDto(
            DistanceZone: combat.Zone switch
            {
                0 => "melee",
                1 => "close",
                2 => "mid",
                _ => "far"
            },
            DistanceMeters: combat.Zone * 5 + 3,
            PreviousDistanceMeters: null,
            PlayerVitality: combat.PlayerHealth,
            PlayerEnergy: 1.0,
            PlayerBraced: false,
            Phase: combat.IsOver ? CombatPhase.Outcome : CombatPhase.PlayerChoice,
            NarrativeMessage: combat.Narrative,
            Actions: combat.IsOver
                ? new List<CombatActionDto>()
                : combat.AvailableActions.Select(a => new CombatActionDto(
                    Id: a,
                    Label: a switch
                    {
                        "thrust" => "Thrust",
                        "back_away" => "Back Away",
                        "dodge" => "Dodge",
                        "attack" => "Attack",
                        "advance" => "Advance",
                        "retreat" => "Retreat",
                        "intimidate" => "Intimidate",
                        _ => a
                    },
                    Description: null,
                    IsAvailable: true,
                    DisabledReason: null,
                    HitChance: null
                )).ToList(),
            ThreatFactors: new List<ThreatFactorDto>(),
            Outcome: combat.IsOver ? new CombatOutcomeDto(
                Result: combat.AnimalHealth <= 0 ? "victory" : "defeat",
                Message: combat.AnimalHealth <= 0 ? $"The {combat.AnimalType} falls!" : "You've been defeated.",
                Rewards: null
            ) : null,
            Grid: grid
        );
    }

    private static string GetHealthDescription(double vitality)
    {
        return vitality switch
        {
            > 0.9 => "healthy",
            > 0.7 => "lightly wounded",
            > 0.4 => "wounded",
            > 0.2 => "badly hurt",
            _ => "near death"
        };
    }

    private static string GetBoldnessDescriptor(double boldness)
    {
        return boldness switch
        {
            > 0.8 => "aggressive",
            > 0.6 => "bold",
            > 0.4 => "wary",
            _ => "cautious"
        };
    }

    private static FrameMode BuildDefaultMode(GameContext ctx)
    {
        if (ctx.Map == null)
            throw new InvalidOperationException("Map is null");

        var gridState = GridStateDto.FromContext(ctx);
        return new TravelMode(gridState);
    }
}

/// <summary>
/// Response from applying a game action. Contains the new state and any overlays to display.
/// </summary>
public record GameResponse(
    WebFrame Frame,
    bool IsError = false,
    string? ErrorMessage = null
)
{
    public static GameResponse Success(GameContext ctx)
    {
        return new GameResponse(GameEngine.BuildFrame(ctx));
    }

    public static GameResponse WithOverlay(GameContext ctx, Overlay overlay)
    {
        var frame = GameEngine.BuildFrame(ctx, overlays: new List<Overlay> { overlay });
        return new GameResponse(frame);
    }

    public static GameResponse Error(string message)
    {
        // Return error response - frame will be null for errors
        return new GameResponse(null!, IsError: true, ErrorMessage: message);
    }
}
