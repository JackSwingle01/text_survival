using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Persistence;
using text_survival.UI;
using text_survival.Web;

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
            var target = _ctx.PendingTravelTarget.Value;
            _ctx.PendingTravelTarget = null; // Clear it

            var destination = _ctx.Map?.GetLocationAt(target.X, target.Y);
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
            _ = SaveManager.Save(_ctx);

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
    /// Travels to the specified destination, handling hazardous terrain and progress.
    /// Returns true if travel succeeded, false if player died.
    /// </summary>
    internal bool TravelToLocation(Location destination)
    {
        Location origin = _ctx.CurrentLocation;

        // Calculate base segment times
        int exitTime = TravelProcessor.CalculateSegmentTime(origin, _ctx.player, _ctx.Inventory);
        int entryTime = TravelProcessor.CalculateSegmentTime(destination, _ctx.player, _ctx.Inventory);

        // Track quick travel choices for injury checks
        bool originQuickTravel = false;
        bool destQuickTravel = false;

        // Handle hazard prompts upfront
        if (TravelProcessor.IsHazardousTerrain(origin))
        {
            var (segmentTime, quickTravel) = PromptForSpeed(origin, exitTime, isExiting: true);
            exitTime = segmentTime;
            originQuickTravel = quickTravel;
        }

        if (TravelProcessor.IsHazardousTerrain(destination))
        {
            var (segmentTime, quickTravel) = PromptForSpeed(destination, entryTime, isExiting: false);
            entryTime = segmentTime;
            destQuickTravel = quickTravel;
        }

        // Single combined progress bar
        int totalTime = exitTime + entryTime;
        bool died = RunTravelWithProgress(totalTime);
        if (died) return false;

        // Move to destination
        _ctx.Map!.MoveTo(destination);

        // Apply injury checks after travel completes
        if (originQuickTravel)
        {
            double injuryRisk = TravelProcessor.GetInjuryRisk(origin, _ctx.player, _ctx.Weather);
            if (injuryRisk > 0 && Utils.RandDouble(0, 1) < injuryRisk)
            {
                TravelHandler.ApplyTravelInjury(_ctx, origin);
                if (!_ctx.player.IsAlive) return false;
            }
        }

        if (destQuickTravel)
        {
            double injuryRisk = TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather);
            if (injuryRisk > 0 && Utils.RandDouble(0, 1) < injuryRisk)
            {
                TravelHandler.ApplyTravelInjury(_ctx, destination);
                if (!_ctx.player.IsAlive) return false;
            }
        }

        bool firstVisit = !destination.Explored;

        // Trigger first-visit event if one exists
        if (firstVisit && destination.FirstVisitEvent != null)
        {
            var evt = destination.FirstVisitEvent(_ctx);
            if (evt != null)
            {
                GameEventRegistry.HandleEvent(_ctx, evt);
            }
        }

        destination.Explore();

        // Check for victory
        if (_ctx.IsWinLocation(destination))
        {
            HandleVictory();
            return true;
        }

        GameDisplay.AddNarrative(_ctx, $"You arrive at {destination.Name}.");

        // Show discovery text on first visit
        if (firstVisit && !string.IsNullOrEmpty(destination.DiscoveryText))
        {
            GameDisplay.AddNarrative(_ctx, destination.DiscoveryText);
        }

        return true;
    }

    /// <summary>
    /// Prompts player for speed choice on hazardous terrain.
    /// Returns adjusted time and whether quick travel was chosen.
    /// </summary>
    private (int segmentTime, bool quickTravel) PromptForSpeed(Location location, int normalTime, bool isExiting)
    {
        int carefulTime = (int)Math.Ceiling(normalTime * TravelProcessor.CarefulTravelMultiplier);
        double injuryRisk = TravelProcessor.GetInjuryRisk(location, _ctx.player, _ctx.Weather);

        // Get location position for UI overlay
        var position = _ctx.Map!.GetPosition(location);
        if (position == null)
            throw new InvalidOperationException($"Location {location.Name} not found on map");

        // Determine hazard type for display
        string hazardType = GetHazardDescription(location);

        // Use specialized hazard prompt (already exists in WebIO)
        bool quickTravel = WebIO.PromptHazardChoice(
            _ctx,
            location,
            position.Value.X,
            position.Value.Y,
            hazardType,
            normalTime,
            carefulTime,
            injuryRisk
        );

        int segmentTime = quickTravel ? normalTime : carefulTime;
        return (segmentTime, quickTravel);
    }

    /// <summary>
    /// Determines the specific hazard type for a location.
    /// </summary>
    private static string GetHazardDescription(Location location)
    {
        // Check for climb risk
        if (location.ClimbRiskFactor > 0)
            return "climb";

        // Check for ice hazard
        var water = location.GetFeature<Environments.Features.WaterFeature>();
        if (water != null && water.GetTerrainHazardContribution() > 0)
            return "ice";

        // Generic terrain hazard
        return "terrain";
    }


    // --- Progress Bar Helpers ---

    /// <summary>
    /// Runs travel with a progress bar. Returns true if player died during travel.
    /// </summary>
    private bool RunTravelWithProgress(int totalTime)
    {
        // Use centralized progress method - handles web animation and processes all time at once
        var (elapsed, interrupted) = GameDisplay.UpdateAndRenderProgress(_ctx, "Traveling...", totalTime, ActivityType.Traveling);

        return PlayerDied;
    }

    private void HandleVictory()
    {
        _ctx.TriggerVictory();

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
