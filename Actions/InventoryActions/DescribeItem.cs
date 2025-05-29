using text_survival.Items;

namespace text_survival.Actions;

public class DescribeItem(Item item) : GameActionBase($"Inspect {item.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new OpenInventory()];
    protected override void OnExecute(GameContext ctx)
    {
        item.Describe();
    }
    private readonly Item item = item;
}
