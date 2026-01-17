using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Items;

namespace text_survival.Desktop.UI;

public class TransferOverlay
{
    public bool IsOpen { get; set; }

    private Inventory? _storage;
    private string _storageName = "Storage";
    private string? _lastMessage;

    public void Open(Inventory storage, string storageName)
    {
        IsOpen = true;
        _storage = storage;
        _storageName = storageName;
        _lastMessage = null;
    }

    public TransferResult? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen || _storage == null) return null;

        TransferResult? result = null;
        var playerInv = ctx.Inventory;

        // Center the window
        OverlaySizes.SetupWide();

        bool open = IsOpen;
        if (ImGui.Begin("Transfer Items", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Two-column layout
            float columnWidth = (ImGui.GetContentRegionAvail().X - 20) / 2;

            // Left column: Player inventory
            ImGui.BeginChild("PlayerInv", new Vector2(columnWidth, -60), ImGuiChildFlags.Border);
            ImGui.Text("Your Inventory");

            float playerWeightPct = (float)(playerInv.CurrentWeightKg / playerInv.MaxWeightKg);
            Vector4 playerWeightColor = playerWeightPct > 0.9f
                ? new Vector4(1f, 0.3f, 0.3f, 1f)
                : playerWeightPct > 0.7f
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(0.7f, 0.9f, 0.7f, 1f);

            ImGui.TextColored(playerWeightColor, $"{playerInv.CurrentWeightKg:F1} / {playerInv.MaxWeightKg:F1} kg");
            ImGui.ProgressBar(playerWeightPct, new Vector2(-1, 0), "");
            ImGui.Separator();

            // Click items to move to storage
            RenderTransferableItems(playerInv, true, ref result);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right column: Storage
            ImGui.BeginChild("StorageInv", new Vector2(columnWidth, -60), ImGuiChildFlags.Border);
            ImGui.Text(_storageName);

            if (_storage.MaxWeightKg < double.MaxValue)
            {
                float storageWeightPct = (float)(_storage.CurrentWeightKg / _storage.MaxWeightKg);
                ImGui.Text($"{_storage.CurrentWeightKg:F1} / {_storage.MaxWeightKg:F1} kg");
                ImGui.ProgressBar(storageWeightPct, new Vector2(-1, 0), "");
            }
            else
            {
                ImGui.Text($"{_storage.CurrentWeightKg:F1} kg");
                ImGui.TextDisabled("Unlimited capacity");
            }
            ImGui.Separator();

            // Click items to move to player
            RenderTransferableItems(_storage, false, ref result);
            ImGui.EndChild();

            // Message area
            if (_lastMessage != null)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), _lastMessage);
            }

            ImGui.Spacing();

            // Close button
            if (ImGui.Button("Done", new Vector2(-1, 30)))
            {
                IsOpen = false;
            }
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return result;
    }

    private void RenderTransferableItems(Inventory inv, bool isPlayerInventory, ref TransferResult? result)
    {
        bool hasItems = false;

        // Resources
        foreach (var category in new[] { ResourceCategory.Fuel, ResourceCategory.Food, ResourceCategory.Medicine, ResourceCategory.Material })
        {
            // Get all resources in this category
            var categoryResources = ResourceCategories.Items[category];
            bool hasAny = false;

            foreach (Resource resource in categoryResources)
            {
                int count = inv.Count(resource);
                if (count == 0) continue;

                if (!hasAny)
                {
                    hasAny = true;
                    hasItems = true;
                    ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), category.ToString());
                }

                double weight = inv.Weight(resource);
                string label = $"  {GetResourceName(resource)} x{count} ({weight:F1}kg)";

                if (ImGui.Selectable(label))
                {
                    result = new TransferResult
                    {
                        Resource = resource,
                        FromPlayer = isPlayerInventory
                    };
                }

                if (ImGui.IsItemHovered())
                {
                    string direction = isPlayerInventory ? _storageName : "your inventory";
                    ImGui.SetTooltip($"Click to move to {direction}");
                }
            }
        }

        // Water
        if (inv.WaterLiters > 0)
        {
            hasItems = true;
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Water");
            string waterLabel = $"  Water: {inv.WaterLiters:F2}L";

            if (ImGui.Selectable(waterLabel))
            {
                result = new TransferResult
                {
                    IsWater = true,
                    WaterAmount = Math.Min(1.0, inv.WaterLiters),
                    FromPlayer = isPlayerInventory
                };
            }

            if (ImGui.IsItemHovered())
            {
                string direction = isPlayerInventory ? _storageName : "your inventory";
                ImGui.SetTooltip($"Click to transfer 1L to {direction}");
            }
        }

        // Tools
        if (inv.Tools.Count > 0)
        {
            hasItems = true;
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Tools");

            foreach (var tool in inv.Tools.ToList())
            {
                string conditionStr = tool.ConditionPct < 0.3f ? " [worn]" : "";
                string label = $"  {tool.Name}{conditionStr}";

                if (ImGui.Selectable(label))
                {
                    result = new TransferResult
                    {
                        Tool = tool,
                        FromPlayer = isPlayerInventory
                    };
                }
            }
        }

        // Equipment
        if (inv.Equipment.Count > 0)
        {
            hasItems = true;
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Equipment");

            foreach (var kvp in inv.Equipment.ToList())
            {
                if (kvp.Value == null) continue;
                var equip = kvp.Value;
                string conditionStr = equip.ConditionPct < 0.3f ? " [worn]" : "";
                string label = $"  {equip.Name}{conditionStr}";

                if (ImGui.Selectable(label))
                {
                    result = new TransferResult
                    {
                        Equipment = equip,
                        FromPlayer = isPlayerInventory
                    };
                }
            }
        }

        // Accessories
        if (inv.Accessories.Count > 0)
        {
            hasItems = true;
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Accessories");

            foreach (var acc in inv.Accessories.ToList())
            {
                string label = $"  {acc.Name}";

                if (ImGui.Selectable(label))
                {
                    result = new TransferResult
                    {
                        Accessory = acc,
                        FromPlayer = isPlayerInventory
                    };
                }
            }
        }

        if (!hasItems)
        {
            ImGui.TextDisabled("Empty");
        }
    }

    private static string GetResourceName(Resource r) => r switch
    {
        Resource.CookedMeat => "Cooked Meat",
        Resource.RawMeat => "Raw Meat",
        Resource.DriedMeat => "Dried Meat",
        Resource.DriedBerries => "Dried Berries",
        Resource.BirchBark => "Birch Bark",
        Resource.PlantFiber => "Plant Fiber",
        _ => r.ToString()
    };

    /// <summary>
    /// Set a status message for the transfer.
    /// </summary>
    public void SetMessage(string message)
    {
        _lastMessage = message;
    }
}

/// <summary>
/// Result from transfer overlay interaction.
/// </summary>
public class TransferResult
{
    public bool FromPlayer { get; set; }
    public Resource? Resource { get; set; }
    public Gear? Tool { get; set; }
    public Gear? Equipment { get; set; }
    public Gear? Accessory { get; set; }
    public bool IsWater { get; set; }
    public double WaterAmount { get; set; }
}
