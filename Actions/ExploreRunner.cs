using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.IO;

namespace text_survival.Actions;

public class ExploreState
{
    public Stack<Location> TravelHistory { get; } = [];
    public Location CurrentLocation => TravelHistory.Peek();
    public int MinutesElapsedTotal { get; private set; } = 0;
    public bool IsAtOrigin => TravelHistory.Count == 1;

    public void MoveTo(Location location, int travelTimeMinutes)
    {
        if (location == TravelHistory.Peek())
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
        ExploreState state = new();
        state.MoveTo(_ctx.CurrentLocation, 0);
        Output.Write("Begin Exploration...");

        while (!state.IsAtOrigin)
        {
            Choice<Location?> choice = new("Where do you go?");
            var connections = state.CurrentLocation.Connections;
            foreach (var con in connections)
            {
                string lbl = con.Explored ? con.Name : "???";
                if (con == state.TravelHistory.Peek())
                    lbl += " (backtrack)";
                choice.AddOption(lbl, con);
            }
            choice.AddOption("Return To Camp", null);
            var next = choice.GetPlayerChoice();
            if (next is null)
            {
                ReturnToCamp(state);
            }
            else
            {
                state.MoveTo(next, TravelProcessor.GetTraversalMinutes(next, _ctx.player));
                Output.WriteLine($"You have arrived at {state.CurrentLocation}: {state.CurrentLocation.Description}. {state.CurrentLocation.GetGatherSummary()}");
            }
        }
        Output.WriteLine("You made it back to camp.");
    }
    private void ReturnToCamp(ExploreState state)
    {
        while (!state.IsAtOrigin)
        {
            state.Backtrack();
        }
    }
    public bool HasUnexploredReachable(ExploreState state)
    {
        return state.CurrentLocation.GetUnexploredConnections().Any();
    }


    private void ExecuteMove(Location destination)
    {

    }
    private void ExecuteReturn()
    {

    }
}