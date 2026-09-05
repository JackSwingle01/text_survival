using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Walking from one tile to the next: the hazard decision, the walk itself, and
/// everything that happens on arrival.
/// </summary>
public class TravelRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;

    /// <summary>
    /// Travel to the tile at these grid coordinates, checking first that the player can
    /// still walk. <paramref name="hazardMode"/> carries a pace ("quick"/"careful")
    /// already chosen from the tile popup, so hazardous terrain doesn't ask again.
    /// </summary>
    public async Task TravelTo(int x, int y, string? hazardMode = null)
    {
        if (!await CanStillWalk()) return;

        var destination = _ctx.Map?.GetLocationAt(x, y);
        if (destination == null || destination == _ctx.CurrentLocation) return;

        await TravelToLocation(destination, hazardMode);
    }

    /// <summary>
    /// Injured legs make travel slow and dangerous. This is never a real decision - the
    /// player already chose to move here - so it's a warning, not a blocking prompt,
    /// and only fires again once the injury gets worse than what was last flagged.
    /// Still blocks outright when the player genuinely can't walk.
    /// </summary>
    private async Task<bool> CanStillWalk()
    {
        double moving = _ctx.player.GetCapacities().Moving;

        if (moving <= 0.1)
        {
            await _ctx.Ui.ShowMessage("Cannot travel", "You can barely move at all. Your injuries prevent travel.");
            return false;
        }

        if (moving > 0.5)
        {
            _ctx.WarnedSlowMoving = null;
            return true;
        }

        if (_ctx.WarnedSlowMoving.HasValue && moving >= _ctx.WarnedSlowMoving.Value)
            return true;

        int slowdown = (int)(1.0 / moving);
        string message = moving <= 0.3
            ? $"You can barely stand. Travel is extremely slow and dangerous. (approximately {slowdown}x slower)"
            : $"Moving is difficult. Travel is noticeably slower. (approximately {slowdown}x slower)";

        GameDisplay.AddWarning(_ctx, message);
        _ctx.WarnedSlowMoving = moving;
        return true;
    }

    /// <summary>
    /// Travels to the destination, handling hazardous terrain, edge events, the walk, and
    /// arrival. Returns false only if the player died.
    /// </summary>
    internal async Task<bool> TravelToLocation(Location destination, string? hazardMode = null)
    {
        Location origin = _ctx.CurrentLocation;
        var originPos = _ctx.Map!.CurrentPosition;
        var destPos = _ctx.Map.GetPosition(destination);

        if (_ctx.Map.IsEdgeBlocked(originPos, destPos, _ctx.Weather.CurrentSeason))
        {
            GameDisplay.AddNarrative(_ctx, GetBlockedMessage(originPos, destPos));
            return true;  // Not dead, just can't go there
        }

        // Edge events fire before the crossing, and can call it off.
        var edgeEvent = _ctx.Map.TryTriggerEdgeEvent(originPos, destPos, _ctx);
        if (edgeEvent != null)
        {
            _ctx.EventQueue.Enqueue(edgeEvent);
            var result = await _ctx.ProcessQueuedEvents();

            if (result?.AbortsAction == true)
            {
                GameDisplay.AddNarrative(_ctx, "You decide not to proceed.");
                return true;  // Didn't travel, but not dead
            }

            if (!_ctx.player.IsAlive) return false;
        }

        int edgeModifier = _ctx.Map.GetEdgeTraversalModifier(originPos, destPos);

        int exitTime = TravelProcessor.CalculateSegmentTime(origin, _ctx.player, _ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(destination, _ctx.player, _ctx.Inventory);

        bool originQuickTravel = false;
        bool destQuickTravel = false;
        double originInjuryRisk = 0;
        double destInjuryRisk = 0;

        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(destination);

        // A step through safe terrain ends the hazardous stretch - the next hazard is a
        // fresh decision, not a continuation of the last one.
        if (!originHazardous && !destHazardous)
            _ctx.LastHazardChoice = null;

        // One decision for the whole journey, however many hazardous segments it has.
        if (originHazardous || destHazardous)
        {
            int originCarefulTime = (int)Math.Ceiling(exitTime * TravelProcessor.CarefulTravelMultiplier);
            int destCarefulTime = (int)Math.Ceiling(entryTime * TravelProcessor.CarefulTravelMultiplier);
            originInjuryRisk = originHazardous ? TravelProcessor.GetInjuryRisk(origin, _ctx.player, _ctx.Weather) : 0;
            destInjuryRisk = destHazardous ? TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather) : 0;

            var preview = TravelProcessor.PreviewCrossing(origin, destination, _ctx.player, _ctx.Weather, _ctx.Inventory, _ctx.Map);

            // The tile popup already asked (hazardMode), or a WASD stretch already
            // settled on a pace no riskier than this segment - either way, don't ask
            // again. Otherwise this is a fresh decision and needs the prompt.
            string? hazardChoice = hazardMode
                ?? (_ctx.LastHazardChoice != null && preview.RiskLevel <= _ctx.LastHazardRiskAccepted
                    ? _ctx.LastHazardChoice
                    : await PromptHazardChoice(destination, preview));

            if (hazardChoice == null)
                return true;  // Turned back, but not a failure

            _ctx.LastHazardChoice = hazardChoice;
            _ctx.LastHazardRiskAccepted = Math.Max(_ctx.LastHazardRiskAccepted, preview.RiskLevel);

            bool quickTravel = hazardChoice == "quick";

            if (originHazardous)
            {
                exitTime = quickTravel ? exitTime : originCarefulTime;
                originQuickTravel = quickTravel;
            }
            if (destHazardous)
            {
                entryTime = quickTravel ? entryTime : destCarefulTime;
                destQuickTravel = quickTravel;
            }
        }

        // The edge modifier applies once to the whole crossing.
        int totalTime = Math.Max(5, exitTime + entryTime + edgeModifier);

        // Capture first visit status BEFORE MoveTo marks the location explored
        bool firstVisit = !destination.Explored;

        var travel = new GameContext.ActiveTravelState
        {
            Destination = destination,
            Origin = origin,
            OriginPosition = originPos,
            Run = new TimedRun(totalTime, Pacing.TravelSeconds(totalTime)),
            FirstVisit = firstVisit,
            OriginQuickTravel = originQuickTravel,
            DestQuickTravel = destQuickTravel,
            OriginInjuryRisk = originInjuryRisk,
            DestInjuryRisk = destInjuryRisk
        };

        _ctx.ActiveTravel = travel;
        try
        {
            // An event can stop the crossing partway. If the player pushes on, the walk
            // resumes from where it left off - the run keeps the elapsed time.
            while (await Walk(travel))
            {
                if (!_ctx.player.IsAlive) return false;

                if (!await _ctx.Ui.Confirm($"Continue traveling to {destination.Name}?"))
                    return true;  // Stayed at the origin
            }

            if (!_ctx.player.IsAlive) return false;

            return await Arrive(travel);
        }
        finally
        {
            _ctx.ActiveTravel = null;
        }
    }

    /// <summary>
    /// The walk. Animation and simulation both come off the travel run's clock, so the
    /// sprite, the camera and the game time arrive together.
    /// Returns true if an event cut the crossing short.
    /// </summary>
    private async Task<bool> Walk(GameContext.ActiveTravelState travel)
    {
        var run = travel.Run;

        while (!run.Done && _ctx.player.IsAlive)
        {
            float dt = await _ctx.Ui.NextFrame();
            int due = run.Advance(dt);

            for (int i = 0; i < due; i++)
            {
                await _ctx.Update(1, ActivityType.Traveling);
                run.MarkSimulated(1);

                if (_ctx.EventOccurredLastUpdate) return true;
                if (!_ctx.player.IsAlive) return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Arriving: the move itself, the terrain's toll, and whatever the place has waiting.
    /// Returns false only if the player died on the way in.
    /// </summary>
    private async Task<bool> Arrive(GameContext.ActiveTravelState travel)
    {
        var destination = travel.Destination;

        _ctx.Map!.MoveTo(destination, _ctx.player);

        if (!destination.IsTerrainOnly)
            _ctx.RecordLocationDiscovery(destination.Name);

        if (travel.OriginQuickTravel && travel.OriginInjuryRisk > 0 && Utils.RandDouble(0, 1) < travel.OriginInjuryRisk)
        {
            TravelHandler.ApplyTravelInjury(_ctx, travel.Origin);
            if (!_ctx.player.IsAlive) return false;
        }

        if (travel.DestQuickTravel && travel.DestInjuryRisk > 0 && Utils.RandDouble(0, 1) < travel.DestInjuryRisk)
        {
            TravelHandler.ApplyTravelInjury(_ctx, destination);
            if (!_ctx.player.IsAlive) return false;
        }

        if (travel.FirstVisit && destination.FirstVisitEvent != null)
            _ctx.EventQueue.Enqueue(destination.FirstVisitEvent(_ctx));

        destination.Explore();

        if (travel.FirstVisit && !string.IsNullOrEmpty(destination.DiscoveryText) && destination.FirstVisitEvent == null)
            await _ctx.Ui.ShowMessage("Discovery!", $"{destination.Name}\n\n{destination.DiscoveryText}");

        await _ctx.ShowNotices();

        return _ctx.player.IsAlive;
    }

    private async Task<string?> PromptHazardChoice(Location targetLocation, CrossingPreview preview)
    {
        int riskPercent = (int)(preview.RiskLevel * 100);
        string message = $"Hazardous terrain ahead: {targetLocation.Name}\n\n" +
            $"Risk of injury: {riskPercent}%\n\n" +
            $"Quick crossing: {preview.QuickMinutes} minutes (full risk)\n" +
            $"Careful crossing: {preview.CarefulMinutes} minutes (reduced risk)";

        string choice = await _ctx.Ui.Choose(message,
        [
            ("quick", $"Quick ({preview.QuickMinutes}min)"),
            ("careful", $"Careful ({preview.CarefulMinutes}min)"),
            ("cancel", "Turn back")
        ]);

        return choice == "cancel" ? null : choice;
    }

    /// <summary>
    /// Get message explaining why a path is blocked.
    /// </summary>
    private string GetBlockedMessage(GridPosition from, GridPosition to)
    {
        var edges = _ctx.Map!.GetEdgesBetween(from, to);
        var blocking = edges.FirstOrDefault(e => e.IsBlockedIn(_ctx.Weather.CurrentSeason));

        return blocking?.Type switch
        {
            EdgeType.Cliff => "Sheer cliff face. No way up.",
            EdgeType.River when blocking.BlockedSeason == Weather.Season.Spring =>
                "The river is in full flood. Impassable until the waters recede.",
            _ => "The way is blocked."
        };
    }
}
