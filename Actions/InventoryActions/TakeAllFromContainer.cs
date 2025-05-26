using text_survival.Items;

namespace text_survival.Actions;

public class TakeAllFromContainer(Container container) : GameActionBase("Take All")
{
    public override bool IsAvailable(GameContext ctx) => container.Count() > 1;

    protected override void OnExecute(GameContext ctx)
    {
        while (!container.IsEmpty)
        {
            var item = container.Items.First();
            container.Remove(item);
            ctx.player.TakeItem(item);
        }
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [];
    private readonly Container container = container;
}
