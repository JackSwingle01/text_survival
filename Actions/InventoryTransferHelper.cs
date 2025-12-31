using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

public static class InventoryTransferHelper
{
    public static void RunTransferMenu(GameContext ctx, Inventory storage, string storageName, bool viewStorageFirst = false)
    {
        Web.WebIO.RunTransferUI(ctx, storage, storageName);
    }
}
