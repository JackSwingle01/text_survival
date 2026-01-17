using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.IO;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions;

public class TravelRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;


    /// <summary>
    /// Shows travel options and moves to selected destination.
    /// Loops until player chooses to stop traveling.
    /// </summary>
    public void DoTravel()
    {
        // Check for pending travel target from map click
        if (_ctx.PendingTravelTarget.HasValue)
        {
            var target = _ctx.PendingTravelTarget;
            _ctx.PendingTravelTarget = null; // Clear it

            // Validate movement capacity before allowing travel
            var capacities = _ctx.player.GetCapacities();
            double moving = capacities.Moving;

            if (moving <= 0.5)
            {
                string message;
                Dictionary<string, string> buttons;

                if (moving <= 0.1)
                {
                    // Completely blocked
                    message = "You can barely move at all. Your injuries prevent travel.";
                    buttons = new() { { "ok", "OK" } };
                    DesktopIO.PromptConfirm(_ctx, message, buttons);
                    return; // Don't allow travel
                }
                else if (moving <= 0.3)
                {
                    // Severe impairment
                    int slowdown = (int)(1.0 / moving);
                    message = $"You can barely stand. Travel will be extremely slow and dangerous. (approximately {slowdown}x slower)";
                    buttons = new() { { "proceed", "Proceed" }, { "cancel", "Cancel" } };
                }
                else // moving <= 0.5
                {
                    // Moderate impairment
                    int slowdown = (int)(1.0 / moving);
                    message = $"Moving is difficult. Travel will be noticeably slower. (approximately {slowdown}x slower)";
                    buttons = new() { { "proceed", "Proceed" }, { "cancel", "Cancel" } };
                }

                // If we get here, show warning and get confirmation
                string response = DesktopIO.PromptConfirm(_ctx, message, buttons);
                if (response != "proceed")
                {
                    return; // User cancelled
                }
            }

            var destination = _ctx.Map?.GetLocationAt(target.Value.X, target.Value.Y);
            if (destination != null && destination != _ctx.CurrentLocation)
            {
                TravelToLocation(destination);
            }
            // Return to main menu after traveling to clicked tile
            return;
        }

        while (true)
        {
            // Auto-save when at travel menu
            var (saved, saveError) = SaveManager.Save(_ctx);
            if (!saved)
                Console.WriteLine($"[TravelRunner] Save failed: {saveError}");

            var connections = _ctx.Map?.GetTravelOptions() ?? [];
            if (connections.Count == 0)
            {
                GameDisplay.AddNarrative(_ctx, "You don't know where to go from here. You need to explore first.");
                return;
            }

            // Show travel options
            var choice = new Choice<Location?>("Where do you go?");

            foreach (var con in connections)
            {
                string lbl;
                if (con.Explored)
                {
                    int minutes = TravelProcessor.GetTraversalMinutes(_ctx.CurrentLocation, con, _ctx.player, _ctx.Inventory);
                    // Show name with tags for explored locations
                    string tags = !string.IsNullOrEmpty(con.Tags) ? $" {con.Tags}" : "";
                    lbl = $"{con.Name}{tags} (~{minutes} min)";
                }
                else
                {
                    lbl = con.GetUnexploredHint(_ctx.CurrentLocation, _ctx.player);
                }

                if (con == _ctx.Camp)
                    lbl += " - Camp";

                // todo - add backtracking?
                // if (con == expedition.TravelHistory.ElementAtOrDefault(1))
                //     lbl += " (backtrack)";

                choice.AddOption(lbl, con);
            }

            choice.AddOption("Done", null);

            var destination = choice.GetPlayerChoice(_ctx);
            if (destination == null) break;

            bool success = TravelToLocation(destination);
            if (!success) return; // Player died, exit entirely
        }
    }

    /// <summary>
    /// Travels to the specified destination, handling hazardous terrain, edge events, and progress.
    /// Returns true if travel succeeded, false if player died.
    /// </summary>
    internal bool TravelToLocation(Location destination)
    {
        Location origin = _ctx.CurrentLocation;
        var originPos = _ctx.Map!.CurrentPosition;
        var destPos = _ctx.Map.GetPosition(destination);

        // Check for blocked edges

        var season = _ctx.Weather.CurrentSeason;
        if (_ctx.Map.IsEdgeBlocked(originPos, destPos, season))
        {
            GameDisplay.AddNarrative(_ctx, GetBlockedMessage(originPos, destPos));
            return true;  // Not dead, just can't go there
        }


        // Check for edge events BEFORE travel

        var edgeEvent = _ctx.Map.TryTriggerEdgeEvent(originPos, destPos, _ctx);
        if (edgeEvent != null)
        {
            var result = GameEventRegistry.HandleEvent(_ctx, edgeEvent);

            // Check if the chosen outcome aborted the travel
            if (result.AbortsAction)
            {
                GameDisplay.AddNarrative(_ctx, "You decide not to proceed.");
                return true;  // Didn't travel, but not dead
            }

            if (!_ctx.player.IsAlive) return false;
        }


        // Get edge time modifier
        int edgeModifier = _ctx.Map.GetEdgeTraversalModifier(originPos, destPos);

        // Calculate base segment times
        int exitTime = TravelProcessor.CalculateSegmentTime(origin, _ctx.player, _ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(destination, _ctx.player, _ctx.Inventory);

        // Track quick travel choices and injury risks for checks after travel
        bool originQuickTravel = false;
        bool destQuickTravel = false;
        double originInjuryRisk = 0;
        double destInjuryRisk = 0;

        // Check both locations for hazards upfront
        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(destination);

        // Single prompt for the entire journey if any hazards
        if (originHazardous || destHazardous)
        {
            // Calculate times and risks for hazardous segments
            int originCarefulTime = (int)Math.Ceiling(exitTime * TravelProcessor.CarefulTravelMultiplier);
            int destCarefulTime = (int)Math.Ceiling(entryTime * TravelProcessor.CarefulTravelMultiplier);
            originInjuryRisk = originHazardous ? TravelProcessor.GetInjuryRisk(origin, _ctx.player, _ctx.Weather) : 0;
            destInjuryRisk = destHazardous ? TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather) : 0;

            // Combined times for display (only hazardous segments count)
            int combinedQuickTime = (originHazardous ? exitTime : 0) + (destHazardous ? entryTime : 0);
            int combinedCarefulTime = (originHazardous ? originCarefulTime : 0) + (destHazardous ? destCarefulTime : 0);
            double maxRisk = Math.Max(originInjuryRisk, destInjuryRisk);

            // Use destination for position (where player is heading)
            var position = _ctx.Map!.GetPosition(destination);

            // Combine hazard descriptions if both are hazardous with different types
            string hazardType = GetCombinedHazardDescription(origin, destination, originHazardous, destHazardous);

            string? hazardChoice = DesktopIO.PromptHazardChoice(
                _ctx,
                destination,
                position.X,
                position.Y,
                combinedQuickTime,
                combinedCarefulTime,
                maxRisk
            );

            // Handle turn back
            if (hazardChoice == null)
            {
                return true;  // Travel aborted, but not a failure
            }

            bool quickTravel = hazardChoice == "quick";

            // Apply speed choice to hazardous segments
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

        // Single combined progress bar with synchronized camera pan
        // Edge modifier applies once to total crossing
        int totalTime = exitTime + entryTime + edgeModifier;
        totalTime = Math.Max(5, totalTime);  // Minimum 5 minutes

        // Capture first visit status BEFORE MoveTo marks the location explored
        bool firstVisit = !destination.Explored;

        // Set up non-blocking travel - GameRunner will process incrementally
        var (died, stayed, pending) = RunTravelWithProgress(
            totalTime,
            destination,
            origin,
            originPos,
            firstVisit,
            originQuickTravel,
            destQuickTravel,
            originInjuryRisk,
            destInjuryRisk);

        if (died) return false;
        if (stayed) return true;
        if (pending) return true;  // Travel in progress - GameRunner will call CompleteTravel

        // This code path is only for synchronous travel (if we ever need it)
        return true;
    }

    /// <summary>
    /// Determines the specific hazard type for a location.
    /// </summary>
    private static string GetHazardDescription(Location location)
    {
        // Check for ice hazard
        var water = location.GetFeature<Environments.Features.WaterFeature>();
        if (water != null && water.GetTerrainHazardContribution() > 0)
            return "ice";

        // Generic terrain hazard
        return "terrain";
    }

    /// <summary>
    /// Gets a combined hazard description for a journey crossing hazardous terrain.
    /// </summary>
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


    // --- Progress Bar Helpers ---

    /// <summary>
    /// Sets up non-blocking travel with animated camera and incremental simulation.
    /// Returns (died, stayed, pending) - pending=true means travel is in progress and will complete asynchronously.
    /// </summary>
    private (bool died, bool stayed, bool pending) RunTravelWithProgress(
        int totalTime,
        Location destination,
        Location origin,
        GridPosition originPos,
        bool firstVisit,
        bool originQuickTravel,
        bool destQuickTravel,
        double originInjuryRisk,
        double destInjuryRisk)
    {
        // Calculate animation duration based on travel time
        // Longer trips get slower animations: 0.5s base + 0.03s per minute, capped at 1.2s
        float animDuration = Math.Clamp(0.5f + (totalTime * 0.03f), 0.5f, 1.2f);

        // Start camera animation to destination - let Camera's built-in easing handle the smooth pan
        var camera = DesktopRuntime.WorldRenderer?.Camera;
        var destPos = _ctx.Map!.GetPosition(destination);
        camera?.SetCenter(destPos.X, destPos.Y, animate: true, durationSeconds: animDuration);

        // Set up active travel state for incremental processing
        _ctx.ActiveTravel = new GameContext.ActiveTravelState
        {
            TotalMinutes = totalTime,
            SimulatedMinutes = 0,
            Destination = destination,
            Origin = origin,
            OriginPosition = originPos,
            FirstVisit = firstVisit,
            EventInterrupted = false,
            AnimationProgress = 0f,
            AnimationDurationSeconds = animDuration,
            OriginQuickTravel = originQuickTravel,
            DestQuickTravel = destQuickTravel,
            OriginInjuryRisk = originInjuryRisk,
            DestInjuryRisk = destInjuryRisk
        };

        // Return with pending=true - travel will be processed by GameRunner
        return (false, false, true);
    }

    /// <summary>
    /// Completes travel after incremental simulation finishes.
    /// Called by GameRunner when ActiveTravel is done.
    /// Returns false if player died.
    /// </summary>
    public bool CompleteTravel()
    {
        var travel = _ctx.ActiveTravel;
        if (travel == null) return true;

        var destination = travel.Destination;
        var origin = travel.Origin;

        // Check if event interrupted travel - give player option to stay at origin
        if (travel.EventInterrupted)
        {
            if (!DesktopIO.Confirm(_ctx, $"Continue traveling to {destination.Name}?"))
            {
                // Player chose to stay at origin - don't move
                _ctx.ActiveTravel = null;
                // Clear player position override
                if (DesktopRuntime.WorldRenderer != null)
                    DesktopRuntime.WorldRenderer.PlayerPositionOverride = null;
                // Reset camera to origin
                var camera = DesktopRuntime.WorldRenderer?.Camera;
                camera?.SetCenter(travel.OriginPosition.X, travel.OriginPosition.Y, animate: false);
                return true;
            }
        }

        // Move to destination
        _ctx.Map!.MoveTo(destination, _ctx.player);

        // Record discovery for named locations
        if (!destination.IsTerrainOnly)
            _ctx.RecordLocationDiscovery(destination.Name);

        // Clear player position override
        if (DesktopRuntime.WorldRenderer != null)
            DesktopRuntime.WorldRenderer.PlayerPositionOverride = null;

        // Apply injury checks
        if (travel.OriginQuickTravel && travel.OriginInjuryRisk > 0 && Utils.RandDouble(0, 1) < travel.OriginInjuryRisk)
        {
            TravelHandler.ApplyTravelInjury(_ctx, origin);
            if (!_ctx.player.IsAlive)
            {
                _ctx.ActiveTravel = null;
                return false;
            }
        }

        if (travel.DestQuickTravel && travel.DestInjuryRisk > 0 && Utils.RandDouble(0, 1) < travel.DestInjuryRisk)
        {
            TravelHandler.ApplyTravelInjury(_ctx, destination);
            if (!_ctx.player.IsAlive)
            {
                _ctx.ActiveTravel = null;
                return false;
            }
        }

        // Trigger first-visit event if one exists
        if (travel.FirstVisit && destination.FirstVisitEvent != null)
        {
            var evt = destination.FirstVisitEvent(_ctx);
            if (evt != null)
            {
                GameEventRegistry.HandleEvent(_ctx, evt);
            }
        }

        destination.Explore();

        // Show discovery popup if this is first visit and has discovery text
        if (travel.FirstVisit && !string.IsNullOrEmpty(destination.DiscoveryText) && destination.FirstVisitEvent == null)
        {
            DesktopIO.ShowDiscovery(_ctx, destination.Name, destination.DiscoveryText);
        }
        else
        {
            // Standard arrival message
            GameDisplay.AddNarrative(_ctx, $"You arrive at {destination.Name}.");
        }

        _ctx.ActiveTravel = null;
        return _ctx.player.IsAlive;
    }

    private void HandleVictory()
    {
        // _ctx.TriggerVictory();

        GameDisplay.ClearNarrative(_ctx);
        GameDisplay.AddSuccess(_ctx, "You made it.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, "The pass is behind you now.");
        GameDisplay.AddNarrative(_ctx, "Below, the far valley stretches green and sheltered.");
        GameDisplay.AddNarrative(_ctx, "Smoke rises from distant fires. Your tribe is there.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, "You survived.");
        GameDisplay.AddNarrative(_ctx, "");
        GameDisplay.AddNarrative(_ctx, $"Days survived: {_ctx.DaysSurvived}");
        GameDisplay.AddNarrative(_ctx, $"Season: {_ctx.Weather.GetSeasonLabel()}");
        GameDisplay.Render(_ctx, statusText: "Victory!");
    }

}
