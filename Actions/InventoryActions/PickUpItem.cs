using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actions;

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