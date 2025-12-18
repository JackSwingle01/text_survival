using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.IO;

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
        Output.Write("Begin Exploration...");

        do
        {
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
            HashSet<int> shownThresholds = new();
            List<string> flavorToShow = new();

            while (timeRemaining > 0)
            {
                GameEvent? triggeredEvent = null;
                int ticksThisSegment = timeRemaining;

                // Run progress bar - exits early if event triggers
                Output.Progress($"Traveling to {next.Name}...", travelTime, task =>
                {
                    // Start from current progress
                    task.Increment(timeElapsed);

                    for (int i = 0; i < ticksThisSegment && triggeredEvent == null; i++)
                    {
                        var tickResult = GameEventRegistry.RunTicks(_ctx, 1);
                        _ctx.Update(tickResult.MinutesElapsed);
                        timeElapsed += tickResult.MinutesElapsed;
                        task.Increment(tickResult.MinutesElapsed);

                        // Collect flavor at progress thresholds (show after progress bar)
                        double progress = (double)timeElapsed / travelTime;
                        if (progress >= 0.25 && !shownThresholds.Contains(25))
                        {
                            flavorToShow.Add(GameEventRegistry.GetRandomFlavorMessage());
                            shownThresholds.Add(25);
                        }
                        else if (progress >= 0.50 && !shownThresholds.Contains(50))
                        {
                            flavorToShow.Add(GameEventRegistry.GetRandomFlavorMessage());
                            shownThresholds.Add(50);
                        }
                        else if (progress >= 0.75 && !shownThresholds.Contains(75))
                        {
                            flavorToShow.Add(GameEventRegistry.GetRandomFlavorMessage());
                            shownThresholds.Add(75);
                        }

                        if (tickResult.TriggeredEvent != null)
                        {
                            triggeredEvent = tickResult.TriggeredEvent;
                        }

                        Thread.Sleep(100); // Visual pacing
                    }
                });

                // Show collected flavor messages
                foreach (var flavor in flavorToShow)
                    Output.WriteLine(flavor);
                flavorToShow.Clear();

                timeRemaining = travelTime - timeElapsed;

                // Handle event outside progress context (prompts work here)
                if (triggeredEvent != null)
                {
                    HandleEvent(triggeredEvent);
                }
            }

            state.MoveTo(next, travelTime);
            next.Explore();
            Output.WriteLine($"You have arrived at {state.CurrentLocation.Name}: {state.CurrentLocation.Description}. {state.CurrentLocation.GetGatherSummary()}");
        } while (!state.IsAtOrigin);
        Output.WriteLine("You made it back to camp.");
    }
    public bool HasUnexploredReachable(ExploreState state)
    {
        return state.CurrentLocation.GetUnexploredConnections().Any();
    }

    private void HandleEvent(GameEvent evt)
    {
        Output.WriteLine("".PadRight(50, '-'));
        Output.WriteLine("EVENT:");
        Output.WriteLine($"** {evt.Name} **");
        Output.WriteLine(evt.Description + "\n");
        var choice = evt.Choices.GetPlayerChoice();
        Output.WriteLine(choice.Description + "\n");
        Output.WriteLine("".PadRight(50, '-'));

        var outcome = choice.DetermineResult();
        HandleOutcome(outcome);
    }

    private void HandleOutcome(EventResult outcome)
    {
        Output.WriteLine("OUTCOME:");
        Output.WriteLine(outcome.Message);

        if (outcome.TimeAddedMinutes != 0)
        {
            Output.WriteLine($"(+{outcome.TimeAddedMinutes} minutes)");
            _ctx.Update(outcome.TimeAddedMinutes);
        }

        if (outcome.NewEffect is not null)
        {
            _ctx.player.EffectRegistry.AddEffect(outcome.NewEffect);
        }

        if (outcome.NewItem is not null)
        {
            _ctx.player.TakeItem(outcome.NewItem);
            Output.WriteLine($"You found: {outcome.NewItem.Name}");
        }

        Output.WriteLine("".PadRight(50, '-'));
    }
}