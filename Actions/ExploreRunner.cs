using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions;

public class ExploreState(Location startLocation)
{
    public Stack<Location> TravelHistory { get; } = new Stack<Location>([startLocation]);
    public Location CurrentLocation => TravelHistory.Peek();
    public int MinutesElapsedTotal { get; private set; } = 0;
    public bool IsAtOrigin => TravelHistory.Count == 1;

    public void MoveTo(Location location, int travelTimeMinutes)
    {
        if (location == TravelHistory.ElementAtOrDefault(1))
        {
            Backtrack();
        }
        else
        {
            TravelHistory.Push(location);
        }
        MinutesElapsedTotal += travelTimeMinutes;
    }
    public Location? Backtrack()
    {
        return TravelHistory.Pop();
    }
    public int GetEstimatedReturnTime(Player player)
    {
        return TravelProcessor.GetPathMinutes(GetPathBack(), player);
    }
    private List<Location> GetPathBack() => TravelHistory.ToList();

}

public class ExploreRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;

    public void Run()
    {
        ExploreState state = new(_ctx.CurrentLocation);
        GameDisplay.AddNarrative("Begin Exploration...");

        do
        {
            GameDisplay.Render(_ctx);
            Choice<Location?> choice = new("Where do you go?");
            var connections = state.CurrentLocation.Connections;
            int unknownCount = 0;
            foreach (var con in connections)
            {
                string lbl;
                if (con.Explored)
                    lbl = con.Name;
                else
                {
                    unknownCount++;
                    lbl = $"??? ({unknownCount})";
                }
                if (con == state.TravelHistory.ElementAtOrDefault(1))
                    lbl += " (backtrack)";
                choice.AddOption(lbl, con);
            }
            var next = choice.GetPlayerChoice();

            // Tick-based travel with progress bar
            int travelTime = TravelProcessor.GetTraversalMinutes(next, _ctx.player);
            int timeRemaining = travelTime;
            int timeElapsed = 0;
            bool flavorShown = false;
            string? pendingFlavor = null;
            double flavorThreshold = Random.Shared.NextDouble() * 0.4 + 0.3; // 30-70%

            while (timeRemaining > 0)
            {
                GameEvent? triggeredEvent = null;
                int ticksThisSegment = timeRemaining;

                // Run progress bar - exits early if event or flavor triggers
                Output.Progress($"Traveling to {next.Name}...", travelTime, task =>
                {
                    // Start from current progress
                    task.Increment(timeElapsed);

                    for (int i = 0; i < ticksThisSegment && triggeredEvent == null && pendingFlavor == null; i++)
                    {
                        var tickResult = GameEventRegistry.RunTicks(_ctx, 1);
                        _ctx.Update(tickResult.MinutesElapsed);
                        timeElapsed += tickResult.MinutesElapsed;
                        task.Increment(tickResult.MinutesElapsed);

                        // Check for flavor interrupt
                        double progress = (double)timeElapsed / travelTime;
                        if (!flavorShown && progress >= flavorThreshold)
                        {
                            pendingFlavor = GameEventRegistry.GetRandomFlavorMessage();
                        }

                        if (tickResult.TriggeredEvent != null)
                        {
                            triggeredEvent = tickResult.TriggeredEvent;
                        }

                        Thread.Sleep(100); // Visual pacing
                    }
                });

                timeRemaining = travelTime - timeElapsed;

                // Handle flavor interrupt
                if (pendingFlavor != null)
                {
                    GameDisplay.AddNarrative(pendingFlavor);
                    GameDisplay.Render(_ctx);
                    Input.WaitForKey();
                    pendingFlavor = null;
                    flavorShown = true;
                }

                // Handle event outside progress context (prompts work here)
                if (triggeredEvent != null)
                {
                    GameDisplay.Render(_ctx);
                    HandleEvent(triggeredEvent);
                }
            }

            state.MoveTo(next, travelTime);
            next.Explore();
            GameDisplay.AddNarrative($"You have arrived at {state.CurrentLocation.Name}: {state.CurrentLocation.Description}. {state.CurrentLocation.GetGatherSummary()}");
        } while (!state.IsAtOrigin);
        GameDisplay.AddNarrative("You made it back to camp.");
        GameDisplay.Render(_ctx);
    }
    public bool HasUnexploredReachable(ExploreState state)
    {
        return state.CurrentLocation.GetUnexploredConnections().Any();
    }

    private void HandleEvent(GameEvent evt)
    {
        GameDisplay.AddNarrative("EVENT:");
        GameDisplay.AddNarrative($"** {evt.Name} **");
        GameDisplay.AddNarrative(evt.Description + "\n");
        GameDisplay.Render(_ctx);
        var choice = evt.Choices.GetPlayerChoice();
        GameDisplay.AddNarrative(choice.Description + "\n");
        Output.ProgressSimple("...", 10);
        var outcome = choice.DetermineResult();
        HandleOutcome(outcome);
        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    private void HandleOutcome(EventResult outcome)
    {
        GameDisplay.AddNarrative("OUTCOME:");
        GameDisplay.AddNarrative(outcome.Message);

        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative($"(+{outcome.TimeAddedMinutes} minutes)");
            _ctx.Update(outcome.TimeAddedMinutes);
        }

        if (outcome.NewEffect is not null)
        {
            _ctx.player.EffectRegistry.AddEffect(outcome.NewEffect);
        }

        if (outcome.NewItem is not null)
        {
            _ctx.player.TakeItem(outcome.NewItem);
            GameDisplay.AddNarrative($"You found: {outcome.NewItem.Name}");
        }
    }
}