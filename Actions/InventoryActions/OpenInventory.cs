using text_survival.Items;

namespace text_survival.Actions;

public class OpenInventory(bool back = false) : GameActionBase(back ? "Back" : "Open Inventory")
{
    public override bool IsAvailable(GameContext ctx)
    {
        return ctx.player.inventoryManager.Items.Count > 0;
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var options = new List<IGameAction>();
        foreach (ItemStack stack in ctx.player.inventoryManager.Items)
        {
            options.Add(new DecideInventoryAction(stack));
        }
        options.Add(new ReturnAction("Close Inventory"));
        return options;
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.inventoryManager.Describe();
    }
    protected override string userPrompt => "Select an item:";
}
