
using text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival;

public class GameContext(Player player)
{
    public Player player = player;
    public Location currentLocation => player.CurrentLocation;
}

public interface IGameAction
{
    public string Name { get; set; }
    public void Execute(GameContext ctx);
    public bool IsAvailable(GameContext ctx);
}

public abstract class GameActionBase(string name) : IGameAction
{
    public virtual string Name { get; set; } = name;
    public virtual bool IsAvailable(GameContext ctx) => true;
    public void Execute(GameContext ctx)
    {
        OnExecute(ctx);
        World.Update(1);
        SelectNextAction(ctx);
    }

    protected abstract void OnExecute(GameContext ctx);
    protected abstract List<IGameAction> GetNextActions(GameContext ctx);
    protected IGameAction? NextActionOverride = null;

    private void SelectNextAction(GameContext ctx)
    {
        if (NextActionOverride != null)
        {
            NextActionOverride.Execute(ctx);
            return;
        }

        var actions = GetNextActions(ctx).Where(a => a.IsAvailable(ctx)).ToList();
        if (actions.Count == 0)
        {
            return; // back to main game loop   
        }
        else if (actions.Count == 1)
        {
            actions[0].Execute(ctx);
            return;
        }
        Output.WriteLine("\nWhat would you like to do?");
        IGameAction action = Input.GetSelectionFromList(actions)!;
        action.Execute(ctx);
    }
    public override string ToString() => Name;
}

public class DefaultAction() : GameActionBase("Default")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var actions = new List<IGameAction>();
        actions.AddRange([
                new LookAround(ctx.currentLocation),
                new Forage(),
                new OpenInventory(),
                new CheckStats(),
                new Sleep(),
                new Move(),
            ]);
        return actions;
    }
    protected override void OnExecute(GameContext ctx)
    {
        // this is mainly just a router to select the next action 
        ctx.player.DescribeSurvivalStats();
    }
}

public class ReturnAction(string name = "Back") : GameActionBase(name)
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        return;
    }
}

public class LookAround(Location location) : GameActionBase($"Look around {location.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var actions = new List<IGameAction>();
        foreach (Npc npc in location.Npcs)
        {
            actions.Add(new FightNpc(npc));
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

public class Sleep() : GameActionBase("Sleep")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        Output.WriteLine("How many hours would you like to sleep?");
        ctx.player.Sleep(Input.ReadInt() * 60);
    }
    public override bool IsAvailable(GameContext ctx)
    {
        // return ctx.player.Body. // todo only let you sleep when tired
        return base.IsAvailable(ctx);
    }
}

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
        var locations = ctx.currentLocation.GetNearbyLocations().Where(l=>l.IsFound).ToList();
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
public class Travel() : GameActionBase("Travel to a different area")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.Travel();
    }
    public override bool IsAvailable(GameContext ctx)
    {
        if (ctx.player.CurrentLocation.GetFeature<ShelterFeature>() != null)
        {
            return false;
        }
        return base.IsAvailable(ctx);
    }
}

public class OpenInventory() : GameActionBase("Open Inventory")
{
    // public override bool IsAvailable(GameContext ctx)
    // {
    //     return ctx.player.
    // }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.OpenInventory();
    }
}

public class CheckStats() : GameActionBase("Check Stats")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.Body.Describe();
        Describe.DescribeSkills(ctx.player);
        Output.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }
}

public class Forage(string name = "Forage") : GameActionBase(name)
{
    public override bool IsAvailable(GameContext ctx)
    {
        var forageFeature = ctx.player.CurrentLocation.GetFeature<ForageFeature>();
        return forageFeature != null;
    }

    protected override void OnExecute(GameContext ctx)
    {
        var forageFeature = ctx.player.CurrentLocation.GetFeature<ForageFeature>();
        if (forageFeature == null)
        {
            Output.WriteLine("You can't forage here");
            return;
        }

        Output.WriteLine("You forage for 1 hour");
        forageFeature.Forage(1);
    }

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [
            new Forage("Keep Foraging"),
            new LookAround(ctx.currentLocation),
            new ReturnAction("Finish foraging...")
        ];
    }
}

public class GoToLocation(Location location) : GameActionBase($"Go to {location.Name}{(location.Visited ? " (Visited)" : "")}")
{
    public override bool IsAvailable(GameContext ctx) => location.IsFound;

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }
    protected override void OnExecute(GameContext ctx)
    {
        location.Interact(ctx.player);
    }
    private readonly Location location = location;
}

public class FightNpc(Npc npc) : GameActionBase($"Fight {npc.Name}")
{
    public override bool IsAvailable(GameContext ctx) => npc.IsAlive && npc.IsFound;

    protected override void OnExecute(GameContext ctx)
    {
        Combat.CombatLoop(ctx.player, npc);
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }
    private readonly Npc npc = npc;
}

public class LootNpc(Npc npc) : GameActionBase($"Loot {npc.Name}")
{
    public override bool IsAvailable(GameContext ctx) => npc.IsFound && !npc.IsAlive && !npc.Loot.IsEmpty;
    protected override void OnExecute(GameContext ctx)
    {
        if (npc.Loot.IsEmpty)
        {
            Output.WriteLine("There is nothing to loot.");
            return;
        }
        npc.Loot.Open(ctx.player);
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }
    private readonly Npc npc = npc;
}

public class OpenContainer(Container container) : GameActionBase($"Look in {container}{(container.IsEmpty ? " (Empty)" : "")}")
{
    public override bool IsAvailable(GameContext ctx) => container.IsFound && !container.IsEmpty;
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        Npc? npc = Combat.GetFastestHostileNpc(ctx.currentLocation);
        if (npc != null && Combat.SpeedCheck(ctx.player, npc))
        {
            Output.WriteLine("You couldn't get past the ", npc, "!");
            NextActionOverride = new FightNpc(npc);
            return;
        }

        Output.WriteLine("You open the ", this);
        container.Open(ctx.player);
    }
    private readonly Container container = container;
}

public class PickUpItem(Item item) : GameActionBase($"Pick up {item.Name}")
{
    public override bool IsAvailable(GameContext ctx) => item.IsFound;

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        Npc? npc = Combat.GetFastestHostileNpc(ctx.currentLocation);
        if (npc != null && Combat.SpeedCheck(ctx.player, npc))
        {
            Output.WriteLine("You couldn't get past the ", npc, "!");
            NextActionOverride = new FightNpc(npc);
            return;
        }
        ctx.player.TakeItem(item);
    }
    private readonly Item item = item;
}