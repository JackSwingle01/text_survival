using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record MoveRequest(int X, int Y);
public record HazardChoiceRequest(bool QuickTravel);
public record TravelContinueRequest(bool Continue);
public record ImpairmentConfirmRequest(bool Proceed);

[ApiController]
[Route("api/game/{sessionId}/navigation")]
public class NavigationController : GameControllerBase
{
    [HttpPost("move")]
    public ActionResult<GameResponse> Move(string sessionId, [FromBody] MoveRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.Map == null)
            return BadRequest(new { error = "No map available" });

        var originPos = ctx.Map.CurrentPosition;
        var targetPos = new GridPosition(req.X, req.Y);
        var targetLocation = ctx.Map.GetLocationAt(targetPos);
        var origin = ctx.CurrentLocation;

        if (targetLocation == null)
            return BadRequest(new { error = "Invalid location" });

        // Check movement capacity - warn if impaired
        var capacities = ctx.player.GetCapacities();
        double moving = capacities.Moving;

        if (moving <= 0.1)
        {
            // Completely blocked
            GameDisplay.AddWarning(ctx, "You can barely move at all. Your injuries prevent travel.");
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }
        else if (moving <= 0.5)
        {
            // Set up impairment warning
            int slowdown = (int)(1.0 / moving);
            string message = moving <= 0.3
                ? $"You can barely stand. Travel will be extremely slow and dangerous. (approximately {slowdown}x slower)"
                : $"Moving is difficult. Travel will be noticeably slower. (approximately {slowdown}x slower)";

            ctx.PendingActivity = new PendingActivityState
            {
                Phase = ActivityPhase.TravelImpairmentWarning,
                Travel = new TravelSnapshot(
                    TargetX: targetPos.X,
                    TargetY: targetPos.Y,
                    OriginX: originPos.X,
                    OriginY: originPos.Y,
                    IsHazardous: false,
                    QuickTimeMinutes: 0,
                    CarefulTimeMinutes: 0,
                    InjuryRisk: 0,
                    HazardDescription: null,
                    QuickTravelChosen: null,
                    StatusMessage: message,
                    IsFirstVisit: !targetLocation.Explored
                )
            };

            var confirmOverlay = new ConfirmOverlay(message);
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.WithOverlay(ctx, confirmOverlay));
        }

        // Check for blocked edges
        var season = ctx.Weather.CurrentSeason;
        if (ctx.Map.IsEdgeBlocked(originPos, targetPos, season))
        {
            string blockedMsg = GetBlockedMessage(ctx.Map, originPos, targetPos);
            GameDisplay.AddNarrative(ctx, blockedMsg);
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        // Check for edge events BEFORE travel
        var edgeEvent = ctx.Map.TryTriggerEdgeEvent(originPos, targetPos, ctx);
        if (edgeEvent != null)
        {
            // Set up travel state to resume after event
            SetupTravelState(ctx, originPos, targetPos, origin, targetLocation);

            // Store event and show event overlay
            ctx.PendingActivity!.Phase = ActivityPhase.EventPending;
            ctx.PendingActivity.EventSource = edgeEvent;
            ctx.PendingActivity.Event = new EventSnapshot(
                edgeEvent.Name,
                edgeEvent.Description,
                edgeEvent.GetAvailableChoices(ctx).Select(c => new ChoiceSnapshot(c.Label, c.Label)).ToList()
            );

            SaveGameContext(sessionId, ctx);
            return Ok(BuildEventResponse(ctx));
        }

        // Calculate travel times
        int exitTime = TravelProcessor.CalculateSegmentTime(origin, ctx.player, ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(targetLocation, ctx.player, ctx.Inventory);
        int edgeModifier = ctx.Map.GetEdgeTraversalModifier(originPos, targetPos);

        // Check for hazardous terrain
        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(targetLocation);

        if (originHazardous || destHazardous)
        {
            // Calculate times and risks for hazard prompt
            int quickTime = exitTime + entryTime + edgeModifier;
            int carefulExitTime = originHazardous ? (int)Math.Ceiling(exitTime * TravelProcessor.CarefulTravelMultiplier) : exitTime;
            int carefulEntryTime = destHazardous ? (int)Math.Ceiling(entryTime * TravelProcessor.CarefulTravelMultiplier) : entryTime;
            int carefulTime = carefulExitTime + carefulEntryTime + edgeModifier;

            double originRisk = originHazardous ? TravelProcessor.GetInjuryRisk(origin, ctx.player, ctx.Weather) : 0;
            double destRisk = destHazardous ? TravelProcessor.GetInjuryRisk(targetLocation, ctx.player, ctx.Weather) : 0;
            double maxRisk = Math.Max(originRisk, destRisk);

            string hazardDesc = GetCombinedHazardDescription(origin, targetLocation, originHazardous, destHazardous);

            ctx.PendingActivity = new PendingActivityState
            {
                Phase = ActivityPhase.TravelHazardPending,
                Travel = new TravelSnapshot(
                    TargetX: targetPos.X,
                    TargetY: targetPos.Y,
                    OriginX: originPos.X,
                    OriginY: originPos.Y,
                    IsHazardous: true,
                    QuickTimeMinutes: quickTime,
                    CarefulTimeMinutes: carefulTime,
                    InjuryRisk: maxRisk,
                    HazardDescription: hazardDesc,
                    QuickTravelChosen: null,
                    StatusMessage: null,
                    IsFirstVisit: !targetLocation.Explored
                )
            };

            var hazardOverlay = new HazardOverlay(new HazardPromptDto(
                targetPos.X,
                targetPos.Y,
                hazardDesc,
                quickTime,
                carefulTime,
                maxRisk
            ));
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.WithOverlay(ctx, hazardOverlay));
        }

        // No hazards - execute travel directly
        var result = ExecuteTravel(ctx, originPos, targetPos, exitTime + entryTime + edgeModifier, false);
        SaveGameContext(sessionId, ctx);
        return Ok(result);
    }

    [HttpPost("travel/cancel")]
    public ActionResult<GameResponse> CancelTravel(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("travel/hazard-choice")]
    public ActionResult<GameResponse> HazardChoice(string sessionId, [FromBody] HazardChoiceRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelHazardPending || ctx.PendingActivity.Travel == null)
            return BadRequest(new { error = "No pending hazard choice" });

        var travel = ctx.PendingActivity.Travel;
        var targetPos = new GridPosition(travel.TargetX, travel.TargetY);
        var originPos = new GridPosition(travel.OriginX, travel.OriginY);

        // Calculate actual travel time based on choice
        int travelMinutes = req.QuickTravel ? travel.QuickTimeMinutes : travel.CarefulTimeMinutes;

        // Update travel state with choice
        ctx.PendingActivity.Travel = travel with { QuickTravelChosen = req.QuickTravel };

        var result = ExecuteTravel(ctx, originPos, targetPos, travelMinutes, req.QuickTravel);
        SaveGameContext(sessionId, ctx);
        return Ok(result);
    }

    [HttpPost("travel/continue")]
    public ActionResult<GameResponse> TravelContinue(string sessionId, [FromBody] TravelContinueRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelInterrupted || ctx.PendingActivity.Travel == null)
            return BadRequest(new { error = "No pending travel continuation" });

        var travel = ctx.PendingActivity.Travel;

        if (!req.Continue)
        {
            // Player chose to stay at origin
            ctx.PendingActivity = null;
            GameDisplay.AddNarrative(ctx, "You decide not to continue.");
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        // Continue travel to destination
        var targetPos = new GridPosition(travel.TargetX, travel.TargetY);
        var destination = ctx.Map!.GetLocationAt(targetPos);

        if (destination == null)
        {
            ctx.PendingActivity = null;
            SaveGameContext(sessionId, ctx);
            return BadRequest(new { error = "Travel destination no longer valid" });
        }

        // Move player to destination
        ctx.Map.MoveTo(destination, ctx.player);
        ctx.PendingActivity = null;

        // Record discovery for named locations
        if (!destination.IsTerrainOnly)
            ctx.RecordLocationDiscovery(destination.Name);

        // Handle first visit
        if (travel.IsFirstVisit)
        {
            // Trigger first-visit event if one exists
            if (destination.FirstVisitEvent != null)
            {
                var evt = destination.FirstVisitEvent(ctx);
                if (evt != null)
                {
                    ctx.PendingActivity = new PendingActivityState
                    {
                        Phase = ActivityPhase.EventPending,
                        EventSource = evt,
                        Event = new EventSnapshot(
                            evt.Name,
                            evt.Description,
                            evt.GetAvailableChoices(ctx).Select(c => new ChoiceSnapshot(c.Label, c.Label)).ToList()
                        )
                    };
                    destination.Explore();
                    SaveGameContext(sessionId, ctx);
                    return Ok(BuildEventResponse(ctx));
                }
            }

            destination.Explore();

            // Show discovery popup
            if (!string.IsNullOrEmpty(destination.DiscoveryText))
            {
                var discoveryOverlay = new DiscoveryOverlay(new DiscoveryDto(
                    destination.Name,
                    destination.DiscoveryText
                ));
                SaveGameContext(sessionId, ctx);
                return Ok(GameResponse.WithOverlay(ctx, discoveryOverlay));
            }
        }

        GameDisplay.AddNarrative(ctx, $"You arrive at {destination.Name}.");
        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("travel/impairment")]
    public ActionResult<GameResponse> ImpairmentConfirm(string sessionId, [FromBody] ImpairmentConfirmRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelImpairmentWarning || ctx.PendingActivity.Travel == null)
            return BadRequest(new { error = "No pending impairment confirmation" });

        var travel = ctx.PendingActivity.Travel;
        ctx.PendingActivity = null;

        if (!req.Proceed)
        {
            // Player chose not to travel
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        // Retry the move now that we have confirmation
        // We need to recursively call Move logic
        var originPos = ctx.Map!.CurrentPosition;
        var targetPos = new GridPosition(travel.TargetX, travel.TargetY);
        var targetLocation = ctx.Map.GetLocationAt(targetPos);
        var origin = ctx.CurrentLocation;

        if (targetLocation == null)
        {
            SaveGameContext(sessionId, ctx);
            return BadRequest(new { error = "Invalid location" });
        }

        // Check for blocked edges
        var season = ctx.Weather.CurrentSeason;
        if (ctx.Map.IsEdgeBlocked(originPos, targetPos, season))
        {
            string blockedMsg = GetBlockedMessage(ctx.Map, originPos, targetPos);
            GameDisplay.AddNarrative(ctx, blockedMsg);
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.Success(ctx));
        }

        // Check for edge events BEFORE travel
        var edgeEvent = ctx.Map.TryTriggerEdgeEvent(originPos, targetPos, ctx);
        if (edgeEvent != null)
        {
            SetupTravelState(ctx, originPos, targetPos, origin, targetLocation);
            ctx.PendingActivity!.Phase = ActivityPhase.EventPending;
            ctx.PendingActivity.EventSource = edgeEvent;
            ctx.PendingActivity.Event = new EventSnapshot(
                edgeEvent.Name,
                edgeEvent.Description,
                edgeEvent.GetAvailableChoices(ctx).Select(c => new ChoiceSnapshot(c.Label, c.Label)).ToList()
            );

            SaveGameContext(sessionId, ctx);
            return Ok(BuildEventResponse(ctx));
        }

        // Calculate travel times
        int exitTime = TravelProcessor.CalculateSegmentTime(origin, ctx.player, ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(targetLocation, ctx.player, ctx.Inventory);
        int edgeModifier = ctx.Map.GetEdgeTraversalModifier(originPos, targetPos);

        // Check for hazardous terrain
        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(targetLocation);

        if (originHazardous || destHazardous)
        {
            int quickTime = exitTime + entryTime + edgeModifier;
            int carefulExitTime = originHazardous ? (int)Math.Ceiling(exitTime * TravelProcessor.CarefulTravelMultiplier) : exitTime;
            int carefulEntryTime = destHazardous ? (int)Math.Ceiling(entryTime * TravelProcessor.CarefulTravelMultiplier) : entryTime;
            int carefulTime = carefulExitTime + carefulEntryTime + edgeModifier;

            double originRisk = originHazardous ? TravelProcessor.GetInjuryRisk(origin, ctx.player, ctx.Weather) : 0;
            double destRisk = destHazardous ? TravelProcessor.GetInjuryRisk(targetLocation, ctx.player, ctx.Weather) : 0;
            double maxRisk = Math.Max(originRisk, destRisk);

            string hazardDesc = GetCombinedHazardDescription(origin, targetLocation, originHazardous, destHazardous);

            ctx.PendingActivity = new PendingActivityState
            {
                Phase = ActivityPhase.TravelHazardPending,
                Travel = new TravelSnapshot(
                    TargetX: targetPos.X,
                    TargetY: targetPos.Y,
                    OriginX: originPos.X,
                    OriginY: originPos.Y,
                    IsHazardous: true,
                    QuickTimeMinutes: quickTime,
                    CarefulTimeMinutes: carefulTime,
                    InjuryRisk: maxRisk,
                    HazardDescription: hazardDesc,
                    QuickTravelChosen: null,
                    StatusMessage: null,
                    IsFirstVisit: !targetLocation.Explored
                )
            };

            var hazardOverlay = new HazardOverlay(new HazardPromptDto(
                targetPos.X,
                targetPos.Y,
                hazardDesc,
                quickTime,
                carefulTime,
                maxRisk
            ));
            SaveGameContext(sessionId, ctx);
            return Ok(GameResponse.WithOverlay(ctx, hazardOverlay));
        }

        // No hazards - execute travel directly
        var result = ExecuteTravel(ctx, originPos, targetPos, exitTime + entryTime + edgeModifier, false);
        SaveGameContext(sessionId, ctx);
        return Ok(result);
    }

    private static void SetupTravelState(GameContext ctx, GridPosition originPos, GridPosition targetPos, Location origin, Location destination)
    {
        int exitTime = TravelProcessor.CalculateSegmentTime(origin, ctx.player, ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(destination, ctx.player, ctx.Inventory);
        int edgeModifier = ctx.Map!.GetEdgeTraversalModifier(originPos, targetPos);

        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(destination);
        double originRisk = originHazardous ? TravelProcessor.GetInjuryRisk(origin, ctx.player, ctx.Weather) : 0;
        double destRisk = destHazardous ? TravelProcessor.GetInjuryRisk(destination, ctx.player, ctx.Weather) : 0;

        ctx.PendingActivity = new PendingActivityState
        {
            Phase = ActivityPhase.None,
            Travel = new TravelSnapshot(
                TargetX: targetPos.X,
                TargetY: targetPos.Y,
                OriginX: originPos.X,
                OriginY: originPos.Y,
                IsHazardous: originHazardous || destHazardous,
                QuickTimeMinutes: exitTime + entryTime + edgeModifier,
                CarefulTimeMinutes: (int)Math.Ceiling(exitTime * (originHazardous ? TravelProcessor.CarefulTravelMultiplier : 1))
                                  + (int)Math.Ceiling(entryTime * (destHazardous ? TravelProcessor.CarefulTravelMultiplier : 1))
                                  + edgeModifier,
                InjuryRisk: Math.Max(originRisk, destRisk),
                HazardDescription: GetCombinedHazardDescription(origin, destination, originHazardous, destHazardous),
                QuickTravelChosen: null,
                StatusMessage: null,
                IsFirstVisit: !destination.Explored
            )
        };
    }

    private static GameResponse ExecuteTravel(GameContext ctx, GridPosition originPos, GridPosition targetPos, int travelMinutes, bool quickTravel)
    {
        var destination = ctx.Map!.GetLocationAt(targetPos)!;
        var origin = ctx.Map.GetLocationAt(originPos)!;
        bool firstVisit = !destination.Explored;

        // Apply minimum travel time
        travelMinutes = Math.Max(5, travelMinutes);

        // Process time - this may trigger events
        ctx.Update(travelMinutes, ActivityType.Traveling);

        // Check if player died during travel
        if (!ctx.player.IsAlive)
        {
            ctx.PendingActivity = null;
            return GameResponse.Success(ctx);
        }

        // Check if event occurred during travel
        if (ctx.EventOccurredLastUpdate)
        {
            // Store travel state for continuation prompt
            ctx.PendingActivity = new PendingActivityState
            {
                Phase = ActivityPhase.TravelInterrupted,
                Travel = new TravelSnapshot(
                    TargetX: targetPos.X,
                    TargetY: targetPos.Y,
                    OriginX: originPos.X,
                    OriginY: originPos.Y,
                    IsHazardous: false,
                    QuickTimeMinutes: 0,
                    CarefulTimeMinutes: 0,
                    InjuryRisk: 0,
                    HazardDescription: null,
                    QuickTravelChosen: quickTravel,
                    StatusMessage: $"Continue traveling to {destination.Name}?",
                    IsFirstVisit: firstVisit
                )
            };

            var confirmOverlay = new ConfirmOverlay($"Continue traveling to {destination.Name}?");
            return GameResponse.WithOverlay(ctx, confirmOverlay);
        }

        // Move player to destination
        ctx.Map.MoveTo(destination, ctx.player);

        // Apply injury for quick travel through hazards
        if (quickTravel && ctx.PendingActivity?.Travel?.InjuryRisk > 0)
        {
            double risk = ctx.PendingActivity.Travel.InjuryRisk;
            if (Random.Shared.NextDouble() < risk)
            {
                TravelHandler.ApplyTravelInjury(ctx, destination);
                if (!ctx.player.IsAlive)
                {
                    ctx.PendingActivity = null;
                    return GameResponse.Success(ctx);
                }
            }
        }

        // Clear pending travel state
        ctx.PendingActivity = null;

        // Record discovery for named locations
        if (!destination.IsTerrainOnly)
            ctx.RecordLocationDiscovery(destination.Name);

        // Handle first visit
        if (firstVisit)
        {
            // Trigger first-visit event if one exists
            if (destination.FirstVisitEvent != null)
            {
                var evt = destination.FirstVisitEvent(ctx);
                if (evt != null)
                {
                    ctx.PendingActivity = new PendingActivityState
                    {
                        Phase = ActivityPhase.EventPending,
                        EventSource = evt,
                        Event = new EventSnapshot(
                            evt.Name,
                            evt.Description,
                            evt.GetAvailableChoices(ctx).Select(c => new ChoiceSnapshot(c.Label, c.Label)).ToList()
                        )
                    };
                    destination.Explore();
                    return BuildEventResponse(ctx);
                }
            }

            destination.Explore();

            // Show discovery popup for locations with discovery text (if no first-visit event)
            if (!string.IsNullOrEmpty(destination.DiscoveryText))
            {
                var discoveryOverlay = new DiscoveryOverlay(new DiscoveryDto(
                    destination.Name,
                    destination.DiscoveryText
                ));
                return GameResponse.WithOverlay(ctx, discoveryOverlay);
            }
        }

        // Standard arrival message
        GameDisplay.AddNarrative(ctx, $"You arrive at {destination.Name}.");
        return GameResponse.Success(ctx);
    }

    private static string GetBlockedMessage(GameMap map, GridPosition from, GridPosition to)
    {
        var edges = map.GetEdgesBetween(from, to);
        var blocking = edges.FirstOrDefault(e => e.IsBlockedIn(map.Weather?.CurrentSeason ?? Weather.Season.Winter));

        return blocking?.Type switch
        {
            EdgeType.Cliff => "Sheer cliff face. No way up.",
            EdgeType.River when blocking.BlockedSeason == Weather.Season.Spring =>
                "The river is in full flood. Impassable until the waters recede.",
            _ => "The way is blocked."
        };
    }

    private static string GetCombinedHazardDescription(Location origin, Location destination, bool originHazardous, bool destHazardous)
    {
        if (originHazardous && destHazardous)
        {
            string originType = GetHazardDescription(origin);
            string destType = GetHazardDescription(destination);
            if (originType == destType)
                return originType;
            return $"{originType} and {destType}";
        }
        else if (originHazardous)
        {
            return GetHazardDescription(origin);
        }
        else
        {
            return GetHazardDescription(destination);
        }
    }

    private static string GetHazardDescription(Location location)
    {
        // Check for ice hazard
        var water = location.GetFeature<WaterFeature>();
        if (water != null && water.GetTerrainHazardContribution() > 0)
            return "ice";

        // Generic terrain hazard
        return "terrain";
    }

    private static GameResponse BuildEventResponse(GameContext ctx)
    {
        if (ctx.PendingActivity == null)
            return GameResponse.Success(ctx);

        var overlays = new List<Overlay>();

        switch (ctx.PendingActivity.Phase)
        {
            case ActivityPhase.EventPending:
                if (ctx.PendingActivity.Event != null)
                {
                    // Build event overlay for choice phase
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
                            )).ToList()
                    );
                    overlays.Add(new EventOverlay(eventDto));
                }
                break;

            case ActivityPhase.EventOutcomeShown:
                if (ctx.PendingActivity.Outcome != null)
                {
                    // Build event outcome overlay
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
        }

        if (overlays.Count > 0)
        {
            return new GameResponse(GameEngine.BuildFrame(ctx, overlays: overlays));
        }

        return GameResponse.Success(ctx);
    }
}
