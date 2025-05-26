using text_survival.Items;

namespace text_survival.Actions;

public class DropItem(Item item) : GameActionBase($"Drop {item.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [];

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.DropItem(item);
    }
    private readonly Item item = item;
}
