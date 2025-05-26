using text_survival.Items;

namespace text_survival.Actions;

public class TakeStackFromContainer(Container container, ItemStack stack) : GameActionBase($"Take {stack.DisplayName}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [new OpenContainer(container)]; // will be auto selected until the container is empty then it will auto return
    }

    protected override void OnExecute(GameContext ctx)
    {
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            container.Remove(item);
            ctx.player.TakeItem(item);
        }
    }
    private readonly Container container = container;
    private readonly ItemStack stack = stack;
}
