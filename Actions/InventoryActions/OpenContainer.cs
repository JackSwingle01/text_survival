using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actions;

public class OpenContainer(Container container) : GameActionBase($"Look in {container}{(container.IsEmpty ? " (Empty)" : "")}")
{
    public override bool IsAvailable(GameContext ctx) => container.IsFound && !container.IsEmpty;
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var options = new List<IGameAction>();
        var itemStacks = ItemStack.CreateStacksFromItems(container.Items);
        foreach (var stack in itemStacks)
        {
            options.Add(new TakeStackFromContainer(container, stack));
        }
        options.Add(new TakeAllFromContainer(container));
        options.Add(new ReturnAction($"Close {container.Name}"));
        return options;
    }

    protected override void OnExecute(GameContext ctx)
    {
        Npc? npc = Combat.GetFastestHostileNpc(ctx.currentLocation);
        if (npc != null && Combat.SpeedCheck(ctx.player, npc))
        {
            Output.WriteLine("You couldn't get past the ", npc, "!");
            NextActionOverride = new StartCombatAction(npc);
            return;
        }

        Output.WriteLine("You open the ", this);
    }
    private readonly Container container = container;
}
