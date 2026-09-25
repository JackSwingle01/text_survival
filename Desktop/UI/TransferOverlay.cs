using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Items;

namespace text_survival.Desktop.UI;

public class TransferOverlay
{
    public bool IsOpen { get; set; }

    private Inventory? _storage;
    private string _storageName = "Storage";
    private string? _lastMessage;
    private TransferResult? _selection;
    private int _amount = 1;
    private bool _focus = true;

    public void Open(Inventory storage, string storageName)
    {
        IsOpen = true;
        _storage = storage;
        _storageName = storageName;
        _lastMessage = null;
        _selection = null;
        _amount = 1;
        _focus = true;
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
            UiText.Wrapped("F2: inventory · F3: storage · F4: amount · Tab / Enter: select, then transfer");
            // Two-column layout
            float columnWidth = (ImGui.GetContentRegionAvail().X - 20) / 2;

            // Left column: Player inventory
            ImGui.BeginChild("PlayerInv", new Vector2(columnWidth, -190 * UiStyle.Scale), ImGuiChildFlags.Borders);
            UiText.Text("Your Inventory");

            float playerWeightPct = (float)(playerInv.CurrentWeightKg / playerInv.MaxWeightKg);
            Vector4 playerWeightColor = playerWeightPct > 0.9f
                ? new Vector4(1f, 0.3f, 0.3f, 1f)
                : playerWeightPct > 0.7f
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(0.7f, 0.9f, 0.7f, 1f);

            UiText.Colored(playerWeightColor, $"{playerInv.CurrentWeightKg:F1} / {playerInv.MaxWeightKg:F1} kg");
            ImGui.ProgressBar(playerWeightPct, new Vector2(-1, 0), "");
            ImGui.Separator();

            // Select a source item; only the explicit transfer button moves it.
            UiNavigation.Initial(ref _focus);
            UiNavigation.Region(KeyboardKey.F2);
            RenderTransferableItems(playerInv, true);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right column: Storage
            ImGui.BeginChild("StorageInv", new Vector2(columnWidth, -190 * UiStyle.Scale), ImGuiChildFlags.Borders);
            UiText.Text(_storageName);

            if (_storage.MaxWeightKg < double.MaxValue)
            {
                float storageWeightPct = (float)(_storage.CurrentWeightKg / _storage.MaxWeightKg);
                UiText.Text($"{_storage.CurrentWeightKg:F1} / {_storage.MaxWeightKg:F1} kg");
                ImGui.ProgressBar(storageWeightPct, new Vector2(-1, 0), "");
            }
            else
            {
                UiText.Text($"{_storage.CurrentWeightKg:F1} kg");
                UiText.Disabled("Unlimited capacity");
            }
            ImGui.Separator();

            // Select a storage item to prepare the reverse direction.
            UiNavigation.Region(KeyboardKey.F3);
            RenderTransferableItems(_storage, false);
            ImGui.EndChild();

            int available = _selection?.Available(playerInv, _storage) ?? 0;
            UiText.Text(_selection == null ? "Select an item above." :
                $"{_selection.Label}: {(_selection.FromPlayer ? "Inventory -> " + _storageName : _storageName + " -> Inventory")}");
            UiNavigation.Region(KeyboardKey.F4);
            ImGui.SetNextItemWidth(160 * UiStyle.Scale);
            ImGui.InputInt("Amount", ref _amount);
            string? reason = available == 0 ? "Select an available item." :
                _amount < 1 || _amount > available ? $"Choose 1–{available}." : null;
            if (UiControls.Button(new("transfer", "Transfer selected amount", "transfer", DisabledReason: reason), size: new Vector2(-1, 0)))
            {
                result = _selection! with { Amount = _amount };
            }
            UiText.Colored(UiStyle.Supporting, _lastMessage ?? " ");
            if (UiIcons.Button("back", "Done [Esc]", "done", new Vector2(-1, 0))) IsOpen = false;

        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return result;
    }

    private void RenderTransferableItems(Inventory inv, bool isPlayerInventory)
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
                    UiText.Colored(new Vector4(0.7f, 0.8f, 0.9f, 1f), category.ToString());
                }

                double weight = inv.Weight(resource);
                string label = $"  {GetResourceName(resource)} x{count} ({weight:F1}kg)";

                if (UiIcons.Selectable(UiIcons.ForResource(resource), label.TrimStart(), resource.ToString(), _selection?.FromPlayer == isPlayerInventory && _selection.Resource == resource))
                {
                    _amount = 1;
                    _selection = new TransferResult
                    {
                        Label = GetResourceName(resource),
                        Resource = resource,
                        FromPlayer = isPlayerInventory
                    };
                }

                if (ImGui.IsItemHovered())
                {
                    string direction = isPlayerInventory ? _storageName : "your inventory";
                    UiText.Tooltip($"Select, then transfer to {direction}");
                }
            }
        }

        // Tools
        if (inv.Tools.Count > 0)
        {
            hasItems = true;
            UiText.Colored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Tools");

            foreach (var tool in inv.Tools.ToList())
            {
                string conditionStr = tool.ConditionPct < 0.3f ? " [worn]" : "";
                string label = $"  {tool.Name}{conditionStr}";

                if (UiIcons.Selectable(UiIcons.ForGear(tool), label.TrimStart(), tool.InstanceId.ToString(), _selection?.FromPlayer == isPlayerInventory && _selection.Tool == tool))
                {
                    _amount = 1;
                    _selection = new TransferResult
                    {
                        Label = tool.Name,
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
            UiText.Colored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Equipment");

            foreach (var kvp in inv.Equipment.ToList())
            {
                if (kvp.Value == null) continue;
                var equip = kvp.Value;
                string conditionStr = equip.ConditionPct < 0.3f ? " [worn]" : "";
                string label = $"  {equip.Name}{conditionStr}";

                if (UiIcons.Selectable(UiIcons.ForGear(equip), label.TrimStart(), equip.InstanceId.ToString(), _selection?.FromPlayer == isPlayerInventory && _selection.Equipment == equip))
                {
                    _amount = 1;
                    _selection = new TransferResult
                    {
                        Label = equip.Name,
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
            UiText.Colored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "Accessories");

            foreach (var acc in inv.Accessories.ToList())
            {
                string label = $"  {acc.Name}";

                if (UiIcons.Selectable(UiIcons.ForGear(acc), label.TrimStart(), acc.InstanceId.ToString(), _selection?.FromPlayer == isPlayerInventory && _selection.Accessory == acc))
                {
                    _amount = 1;
                    _selection = new TransferResult
                    {
                        Label = acc.Name,
                        Accessory = acc,
                        FromPlayer = isPlayerInventory
                    };
                }
            }
        }

        if (!hasItems)
        {
            UiText.Disabled("Empty");
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
public record TransferResult
{
    public string Apply(Inventory player, Inventory storage)
    {
        var source = FromPlayer ? player : storage;
        var dest = FromPlayer ? storage : player;
        string direction = FromPlayer ? "to storage" : "to inventory";

        int available = Available(player, storage);
        if (Amount < 1 || Amount > available)
        { return "The selected amount is no longer available."; }
        int moved = 0;
        string message = "";
        for (int i = 0; i < Amount; i++)
        {
            TransferHandler.TransferResult result = this switch
            {
                { Resource: { } r } => TransferHandler.TransferResource(source, dest, r, direction),
                { Tool: { } t } => TransferHandler.TransferTool(source, dest, t, direction),
                { Equipment: { } e } => TransferHandler.TransferEquipment(source, dest, e, direction),
                { Accessory: { } a } => TransferHandler.TransferAccessory(source, dest, a, direction),
                _ => throw new InvalidOperationException("No selected item.")
            };
            message = result.Message;
            if (!result.Success) break;
            moved++;
        }
        return Amount == 1 ? message : $"Moved {moved}/{Amount} {Label} {direction}. " + (moved < Amount ? message : "");
    }

    public string Label { get; set; } = "Item";
    public int Amount { get; set; } = 1;
    public int Available(Inventory player, Inventory storage)
    {
        var source = FromPlayer ? player : storage;
        if (Resource is { } r) return source.Count(r);
        if (Tool is { } t) return source.Tools.Contains(t) ? 1 : 0;
        if (Equipment is { } e) return source.Equipment.Values.Contains(e) ? 1 : 0;
        if (Accessory is { } a) return source.Accessories.Contains(a) ? 1 : 0;
        return 0;
    }
    public bool FromPlayer { get; set; }
    public Resource? Resource { get; set; }
    public Gear? Tool { get; set; }
    public Gear? Equipment { get; set; }
    public Gear? Accessory { get; set; }
}
