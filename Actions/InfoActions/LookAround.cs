using text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actions;

public class LookAround(Location location) : GameActionBase($"Look around {location.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var actions = new List<IGameAction>();
        foreach (Npc npc in location.Npcs)
        {
            actions.Add(new StartCombatAction(npc));
            actions.Add(new LootNpc(npc));
        }
        foreach (Item item in location.Items)
        {
            actions.Add(new PickUpItem(item));
        }
        foreach (var container in location.Containers)
        {
            actions.Add(new OpenContainer(container));
        }
        actions.Add(new ReturnAction());
        return actions;
    }
    protected override void OnExecute(GameContext ctx)
    {
        Output.WriteLine("You look around the ", location);
        Output.WriteLine("You are in a ", location, " in a ", location.Parent);
        Output.WriteLine("Its ", World.GetTimeOfDay(), " and ", location.GetTemperature(), " degrees.");
        Output.WriteLine("You see:");
        foreach (var thing in location.Items)
        {
            Output.WriteLine(thing);
            thing.IsFound = true;
        }
        foreach (var thing in location.Containers)
        {
            Output.WriteLine(thing);
            thing.IsFound = true;
        }
        foreach (var thing in location.Npcs)
        {
            Output.WriteLine(thing);
            thing.IsFound = true;
        }

        var nearbyLocations = location.GetNearbyLocations();
        if (nearbyLocations.Count == 0)
            return;
        Output.WriteLine("Nearby, you see some other places: ");
        foreach (var location in nearbyLocations)
        {
            Output.WriteLine(location);
            location.IsFound = true;
        }
    }

}
