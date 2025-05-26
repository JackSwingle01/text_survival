using text_survival.Items;

namespace text_survival.Actions;
public class DecideInventoryAction(ItemStack stack) : GameActionBase(stack.DisplayName)
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [new UseItem(item), new DescribeItem(item), new DropItem(item), new OpenInventory(back: true)];
    }

    protected override void OnExecute(GameContext ctx)
    {
        // pass
    }
    protected override string userPrompt => $"What would you like to do with the {item.Name}";
    private readonly Item item = stack.Peek();
}