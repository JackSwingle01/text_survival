using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

public static class InventoryTransferHelper
{
    public static void RunTransferMenu(GameContext ctx, Inventory storage, string storageName, bool viewStorageFirst = false)
    {
        // For web sessions, use the new side-by-side transfer UI
        if (ctx.SessionId != null)
        {
            Web.WebIO.RunTransferUI(ctx, storage, storageName);
            return;
        }

        // Console fallback
        bool viewingStorage = viewStorageFirst;

        while (true)
        {
            // Show current view
            if (viewingStorage)
                GameDisplay.RenderInventoryScreen(ctx, storage, storageName);
            else
                GameDisplay.RenderInventoryScreen(ctx);

            // Build menu options
            var options = new List<string>();

            if (viewingStorage)
                options.Add("View carried items");
            else
                options.Add($"View {storageName.ToLower()}");

            options.Add("Store items");
            options.Add("Retrieve items");
            options.Add("Back");

            string selected = Input.Select(ctx, "Choose:", options);

            if (selected == "Back")
                break;
            else if (selected.StartsWith("View") && selected.Contains(storageName.ToLower()))
                viewingStorage = true;
            else if (selected == "View carried items")
                viewingStorage = false;
            else if (selected == "Store items")
                StoreItems(ctx, storage, storageName);
            else if (selected == "Retrieve items")
                RetrieveItems(ctx, storage, storageName);
        }

        if (ctx.SessionId != null)
            Web.WebIO.ClearInventory(ctx);
    }

    public static void StoreItems(GameContext ctx, Inventory storage, string storageName)
    {
        var playerInv = ctx.Inventory;

        while (true)
        {
            var items = playerInv.GetTransferableItems(storage);

            if (items.Count == 0)
            {
                GameDisplay.AddNarrative(ctx, "Nothing to store.");
                GameDisplay.Render(ctx, statusText: "Organizing.");
                break;
            }

            GameDisplay.Render(ctx, addSeparator: false, statusText: "Organizing.");

            var options = items.Select(i => i.Description).ToList();
            options.Add("Done");

            string selected = Input.Select(ctx, "Store which item?", options);

            if (selected == "Done")
                break;

            int idx = options.IndexOf(selected);
            double itemWeight = items[idx].Weight;

            // Check storage capacity for limited caches
            if (storage.MaxWeightKg > 0 && storage.MaxWeightKg < 500 && !storage.CanCarry(itemWeight))
            {
                GameDisplay.AddWarning(ctx, $"{storageName} is full! ({storage.CurrentWeightKg:F1}/{storage.MaxWeightKg:F0} kg)");
                GameDisplay.Render(ctx, statusText: "Organizing.");
                continue;
            }

            items[idx].TransferTo();
            GameDisplay.AddNarrative(ctx, $"Stored {items[idx].Description}");
        }
    }

    public static void RetrieveItems(GameContext ctx, Inventory storage, string storageName)
    {
        var playerInv = ctx.Inventory;

        while (true)
        {
            var items = storage.GetTransferableItems(playerInv);

            if (items.Count == 0)
            {
                GameDisplay.AddNarrative(ctx, $"{storageName} is empty.");
                GameDisplay.Render(ctx, statusText: "Organizing.");
                break;
            }

            GameDisplay.Render(ctx, addSeparator: false, statusText: "Organizing.");

            var options = items.Select(i => i.Description).ToList();
            options.Add("Done");

            string selected = Input.Select(ctx, "Retrieve which item?", options);

            if (selected == "Done")
                break;

            int idx = options.IndexOf(selected);
            double itemWeight = items[idx].Weight;

            // Check weight limit
            if (!playerInv.CanCarry(itemWeight))
            {
                GameDisplay.AddWarning(ctx, $"You can't carry that much! ({playerInv.CurrentWeightKg:F1}/{playerInv.MaxWeightKg:F0} kg)");
                GameDisplay.Render(ctx, statusText: "Organizing.");
                continue;
            }

            items[idx].TransferTo();
            GameDisplay.AddNarrative(ctx, $"Retrieved {items[idx].Description}");
        }
    }
}
