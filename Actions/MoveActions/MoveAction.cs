using text_survival.IO;

namespace text_survival.Actions;

public class Move() : GameActionBase("Go somewhere else")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var options = new List<IGameAction>();
        foreach (var location in ctx.currentLocation.GetNearbyLocations())
        {
            options.Add(new GoToLocation(location));
        }
        options.Add(new Travel());
        options.Add(new ReturnAction("Stay Here..."));
        return options;
    }

    protected override void OnExecute(GameContext ctx)
    {
        var locations = ctx.currentLocation.GetNearbyLocations().Where(l => l.IsFound).ToList();
        bool inside = ctx.player.CurrentLocation.GetFeature<ShelterFeature>() != null;
        if (inside)
        {
            Output.WriteLine($"You can leave the shelter and go outside.");
        }
        if (locations.Count == 0)
        {
            Output.WriteLine("You don't see anywhere noteworthy nearby; you can stay here or travel to a new area.");
            return;
        }
        else if (locations.Count == 1)
        {
            Output.WriteLine($"You can go to the {locations[0].Name} or pack up and leave the region.");
        }
        else
        {
            Output.WriteLine("You see several places that you can go to from here, or you can pack up and leave the region.");
        }
    }
}