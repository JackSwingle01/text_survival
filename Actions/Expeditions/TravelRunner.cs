using text_survival.Actions.Handlers;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Persistence;
using text_survival.UI;

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
        while (true)
        {
            // Auto-save when at travel menu
            _ = SaveManager.Save(_ctx);

            var connections = _ctx.CurrentLocation.GetConnections(_ctx);
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

        // Check hazards for each segment
        bool originHazardous = TravelProcessor.IsHazardousTerrain(origin);
        bool destHazardous = TravelProcessor.IsHazardousTerrain(destination);

        // SEGMENT 1: Exit origin (CurrentLocation = origin)
        if (originHazardous)
        {
            var (segmentTime, quickTravel) = PromptForSpeed(origin, exitTime, isExiting: true);
            exitTime = segmentTime;

            bool died = RunTravelWithProgress(exitTime);
            if (died) return false;

            // Check for injury if quick travel
            if (quickTravel)
            {
                double injuryRisk = TravelProcessor.GetInjuryRisk(origin, _ctx.player, _ctx.Weather);
                if (injuryRisk > 0 && Utils.RandDouble(0, 1) < injuryRisk)
                {
                    TravelHandler.ApplyTravelInjury(_ctx, origin);
                    if (!_ctx.player.IsAlive) return false;
                }
            }
        }
        else
        {
            // Normal speed for non-hazardous origin
            bool died = RunTravelWithProgress(exitTime);
            if (died) return false;
        }

        // TRANSITION: Update CurrentLocation
        _ctx.CurrentLocation = destination;

        // SEGMENT 2: Enter destination (CurrentLocation = destination)
        if (destHazardous)
        {
            var (segmentTime, quickTravel) = PromptForSpeed(destination, entryTime, isExiting: false);
            entryTime = segmentTime;

            bool died = RunTravelWithProgress(entryTime);
            if (died) return false;

            // Check for injury if quick travel
            if (quickTravel)
            {
                double injuryRisk = TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather);
                if (injuryRisk > 0 && Utils.RandDouble(0, 1) < injuryRisk)
                {
                    TravelHandler.ApplyTravelInjury(_ctx, destination);
                    if (!_ctx.player.IsAlive) return false;
                }
            }
        }
        else
        {
            // Normal speed for non-hazardous destination
            bool died = RunTravelWithProgress(entryTime);
            if (died) return false;
        }

        bool firstVisit = !destination.Explored;
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

        // Determine hazard type and narrative
        string direction = isExiting ? "Exiting" : "Entering";
        string hazardType = GetHazardDescription(location);

        GameDisplay.AddNarrative(_ctx, $"{direction} {location.Name} — the {hazardType} looks treacherous.");

        var speedChoice = new Choice<bool>("How do you proceed?");
        speedChoice.AddOption($"Careful (~{carefulTime} min) - Safe passage", false);
        speedChoice.AddOption($"Quick (~{normalTime} min) - {injuryRisk:P0} injury risk", true);

        bool quickTravel = speedChoice.GetPlayerChoice(_ctx);
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

        Input.WaitForKey(_ctx);
    }

}
