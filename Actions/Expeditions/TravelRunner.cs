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
                    int minutes = TravelProcessor.GetTraversalMinutes(con, _ctx.player, _ctx.Inventory);
                    // Show name with tags for explored locations
                    string tags = !string.IsNullOrEmpty(con.Tags) ? $" {con.Tags}" : "";
                    lbl = $"{con.Name}{tags} (~{minutes} min)";
                }
                else
                {
                    lbl = con.GetUnexploredHint(_ctx.player);
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
        int travelTime;
        bool quickTravel = false;
        double injuryRisk = 0;

        if (TravelProcessor.IsHazardousTerrain(destination))
        {
            int quickTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
            int carefulTime = TravelProcessor.GetCarefulTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
            injuryRisk = TravelProcessor.GetInjuryRisk(destination, _ctx.player, _ctx.Weather);

            GameDisplay.AddNarrative(_ctx, "The terrain ahead looks treacherous.");

            var speedChoice = new Choice<bool>("How do you proceed?");
            speedChoice.AddOption($"Careful (~{carefulTime} min) - Safe passage", false);
            speedChoice.AddOption($"Quick (~{quickTime} min) - {injuryRisk:P0} injury risk", true);

            quickTravel = speedChoice.GetPlayerChoice(_ctx);
            travelTime = quickTravel ? quickTime : carefulTime;
        }
        else
        {
            travelTime = TravelProcessor.GetTraversalMinutes(destination, _ctx.player, _ctx.Inventory);
        }

        bool died = RunTravelWithProgress(travelTime);
        if (died) return false;

        // Check for injury if quick travel through hazardous terrain
        if (quickTravel && injuryRisk > 0)
        {
            if (Utils.RandDouble(0, 1) < injuryRisk)
            {
                TravelHandler.ApplyTravelInjury(_ctx, destination);
            }
        }

        bool firstVisit = !destination.Explored;
        _ctx.CurrentLocation = destination;
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


    // --- Progress Bar Helpers ---

    /// <summary>
    /// Runs travel with a progress bar. Returns true if player died during travel.
    /// </summary>
    private bool RunTravelWithProgress(int totalTime)
    {
        int elapsed = 0;
        bool died = false;
        string statusText = $"Traveling...";

        while (elapsed < totalTime && !died)
        {
            GameDisplay.Render(_ctx,
                addSeparator: false,
                statusText: statusText,
                progress: elapsed,
                progressTotal: totalTime);

            // Use the new activity-based Update with event checking
            elapsed += _ctx.Update(1, ActivityType.Traveling);

            if (PlayerDied)
            {
                died = true;
                break;
            }

            Thread.Sleep(100);
        }

        return died;
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
