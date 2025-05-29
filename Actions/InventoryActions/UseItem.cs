using text_survival.Items;

namespace text_survival.Actions;

public class UseItem(Item item) : GameActionBase($"Use {item.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new OpenInventory()];
    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.UseItem(item);
    }
    private readonly Item item = item;
}
