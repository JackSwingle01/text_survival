using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Desktop.Input;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>
/// Represents a selectable item in the inventory.
/// </summary>
public abstract record InventoryItem(string Name, double WeightKg)
{
    public record ResourceItem(Resource Resource, int Count, double WeightKg)
        : InventoryItem(Resource.ToDisplayName(), WeightKg);

    public record WaterItem(double Liters)
        : InventoryItem("Water", Liters); // Water weighs ~1kg per liter

    public record ToolItem(Gear Tool)
        : InventoryItem(Tool.Name, Tool.Weight);

    public record EquipmentItem(Gear Equipment, EquipSlot Slot)
        : InventoryItem(Equipment.Name, Equipment.Weight);

    public record AccessoryItem(Gear Accessory)
        : InventoryItem(Accessory.Name, Accessory.Weight);

    public record WeaponItem(Gear Weapon)
        : InventoryItem(Weapon.Name, Weapon.Weight);
}

/// <summary>
/// ImGui overlay for viewing and managing inventory with tile-based display.
/// </summary>
public class InventoryOverlay
{
    public bool IsOpen { get; set; }

    private string _selectedCategory = "All";
    private InventoryItem? _selectedItem;
    private string? _message;
    private float _messageTimer;

    private static readonly string[] Categories = { "All", "Food", "Fuel", "Medicine", "Material", "Gear" };

    /// <summary>
    /// Render the inventory overlay. Returns true if overlay should close.
    /// </summary>
    public bool Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return false;

        bool shouldClose = false;

        // Update message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= deltaTime;
            if (_messageTimer <= 0)
                _message = null;
        }

        OverlaySizes.SetupWide();

        bool open = IsOpen;
        if (ImGui.Begin("Inventory", ref open, ImGuiWindowFlags.NoCollapse))
        {
            var inv = ctx.Inventory;

            // Header with weight info
            RenderWeightBar(inv);
            ImGui.Separator();

            // Category buttons in a row
            RenderCategoryButtons(inv);
            ImGui.Separator();

            // Show message if any
            if (_message != null)
            {
                ImGui.TextColored(new Vector4(0.3f, 1f, 0.5f, 1f), _message);
                ImGui.Separator();
            }

            // Main content area - two columns
            float contentHeight = ImGui.GetContentRegionAvail().Y - 30;

            // Left: Item tiles
            ImGui.BeginChild("ItemGrid", new Vector2(280, contentHeight), ImGuiChildFlags.Borders);
            RenderItemGrid(ctx, inv);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right: Selected item details
            ImGui.BeginChild("ItemDetails", new Vector2(0, contentHeight), ImGuiChildFlags.Borders);
            RenderItemDetails(ctx, inv);
            ImGui.EndChild();

            // Close button
            if (ImGui.Button($"Close {HotkeyRegistry.GetTip(HotkeyAction.Cancel)}", new Vector2(-1, 0)))
            {
                shouldClose = true;
            }
        }
        ImGui.End();

        if (!open) shouldClose = true;
        if (shouldClose) IsOpen = false;

        return shouldClose;
    }

    private void RenderWeightBar(Inventory inv)
    {
        float weightPct = (float)(inv.CurrentWeightKg / inv.MaxWeightKg);
        Vector4 weightColor = weightPct > 0.9f
            ? new Vector4(1f, 0.3f, 0.3f, 1f)
            : weightPct > 0.7f
                ? new Vector4(1f, 0.8f, 0.3f, 1f)
                : new Vector4(0.7f, 0.9f, 0.7f, 1f);

        ImGui.TextColored(weightColor, $"Carrying: {inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F1} kg");
        ImGui.ProgressBar(weightPct, new Vector2(-1, 0), "");
    }

    private void RenderCategoryButtons(Inventory inv)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));

        foreach (var category in Categories)
        {
            bool selected = _selectedCategory == category;
            int count = GetCategoryItemCount(inv, category);

            Vector4 buttonColor;
            if (selected)
                buttonColor = new Vector4(0.3f, 0.5f, 0.8f, 1f);
            else if (count > 0)
                buttonColor = new Vector4(0.3f, 0.5f, 0.3f, 1f);
            else
                buttonColor = new Vector4(0.3f, 0.3f, 0.3f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);

            if (ImGui.Button(category))
            {
                _selectedCategory = category;
                _selectedItem = null;
            }

            ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        ImGui.PopStyleVar(2);
        ImGui.NewLine();
    }

    private static int GetCategoryItemCount(Inventory inv, string category) => category switch
    {
        "All" => GetTotalItemCount(inv),
        "Food" => ResourceCategories.Items[ResourceCategory.Food].Count(r => inv.Count(r) > 0) + (inv.WaterLiters > 0 ? 1 : 0),
        "Fuel" => ResourceCategories.Items[ResourceCategory.Fuel].Count(r => inv.Count(r) > 0),
        "Medicine" => ResourceCategories.Items[ResourceCategory.Medicine].Count(r => inv.Count(r) > 0),
        "Material" => ResourceCategories.Items[ResourceCategory.Material].Count(r => inv.Count(r) > 0),
        "Gear" => inv.Tools.Count + inv.Equipment.Values.Count(e => e != null) + inv.Accessories.Count + (inv.Weapon != null ? 1 : 0),
        _ => 0
    };

    private static int GetTotalItemCount(Inventory inv)
    {
        int count = 0;
        foreach (var cat in ResourceCategories.Items.Values)
            count += cat.Count(r => inv.Count(r) > 0);
        if (inv.WaterLiters > 0) count++;
        count += inv.Tools.Count;
        count += inv.Equipment.Values.Count(e => e != null);
        count += inv.Accessories.Count;
        if (inv.Weapon != null) count++;
        return count;
    }

    private void RenderItemGrid(GameContext ctx, Inventory inv)
    {
        var items = GetItemsForCategory(inv, _selectedCategory);

        if (items.Count == 0)
        {
            ImGui.TextDisabled("No items in this category.");
            return;
        }

        // Single column list layout
        float windowWidth = ImGui.GetContentRegionAvail().X;
        float tileWidth = windowWidth - 8; // Leave small margin
        float tileHeight = 32;
        float spacing = 4;

        foreach (var item in items)
        {
            bool isSelected = _selectedItem == item;
            if (RenderItemTile(item, tileWidth, tileHeight, isSelected))
            {
                _selectedItem = item;
            }
            ImGui.Dummy(new Vector2(0, spacing));
        }
    }

    private static List<InventoryItem> GetItemsForCategory(Inventory inv, string category)
    {
        var items = new List<InventoryItem>();

        switch (category)
        {
            case "All":
                AddResourceItems(inv, ResourceCategory.Food, items);
                if (inv.WaterLiters > 0)
                    items.Add(new InventoryItem.WaterItem(inv.WaterLiters));
                AddResourceItems(inv, ResourceCategory.Fuel, items);
                AddResourceItems(inv, ResourceCategory.Medicine, items);
                AddResourceItems(inv, ResourceCategory.Material, items);
                AddGearItems(inv, items);
                break;

            case "Food":
                AddResourceItems(inv, ResourceCategory.Food, items);
                if (inv.WaterLiters > 0)
                    items.Add(new InventoryItem.WaterItem(inv.WaterLiters));
                break;

            case "Fuel":
                AddResourceItems(inv, ResourceCategory.Fuel, items);
                break;

            case "Medicine":
                AddResourceItems(inv, ResourceCategory.Medicine, items);
                break;

            case "Material":
                AddResourceItems(inv, ResourceCategory.Material, items);
                break;

            case "Gear":
                AddGearItems(inv, items);
                break;
        }

        return items;
    }

    private static void AddResourceItems(Inventory inv, ResourceCategory category, List<InventoryItem> items)
    {
        foreach (var resource in ResourceCategories.Items[category])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                double weight = inv.Weight(resource);
                items.Add(new InventoryItem.ResourceItem(resource, count, weight));
            }
        }
    }

    private static void AddGearItems(Inventory inv, List<InventoryItem> items)
    {
        // Weapon first
        if (inv.Weapon != null)
            items.Add(new InventoryItem.WeaponItem(inv.Weapon));

        // Equipment
        foreach (var (slot, gear) in inv.Equipment)
        {
            if (gear != null)
                items.Add(new InventoryItem.EquipmentItem(gear, slot));
        }

        // Tools
        foreach (var tool in inv.Tools)
            items.Add(new InventoryItem.ToolItem(tool));

        // Accessories
        foreach (var acc in inv.Accessories)
            items.Add(new InventoryItem.AccessoryItem(acc));
    }

    private bool RenderItemTile(InventoryItem item, float width, float height, bool isSelected)
    {
        bool clicked = false;
        Vector4 bgColor = GetItemColor(item);

        if (isSelected)
        {
            // Brighter for selected
            bgColor = new Vector4(
                Math.Min(1f, bgColor.X + 0.2f),
                Math.Min(1f, bgColor.Y + 0.2f),
                Math.Min(1f, bgColor.Z + 0.2f),
                1f
            );
        }

        ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgColor with { W = 1f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bgColor with { X = bgColor.X + 0.1f });

        // Use unique ID for each tile
        string id = GetItemId(item);
        if (ImGui.Button($"##{id}", new Vector2(width, height)))
        {
            clicked = true;
        }

        // Draw text overlays
        var rectMin = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();

        uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));
        uint subtextColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 0.8f));

        // Horizontal layout: Name on left, count/weight on right
        float verticalCenter = (height - 14) / 2; // Center text vertically (14 ~= text height)

        // Item name on left
        string displayName = TruncateName(item.Name, 20);
        drawList.AddText(rectMin + new Vector2(6, verticalCenter), textColor, displayName);

        // Count/quantity and weight on right
        string rightText = GetBottomText(item);
        float rightTextWidth = ImGui.CalcTextSize(rightText).X;
        drawList.AddText(rectMin + new Vector2(width - rightTextWidth - 6, verticalCenter), subtextColor, rightText);

        // Condition indicator for gear (small bar at bottom)
        if (item is InventoryItem.ToolItem t)
            RenderConditionBar(drawList, rectMin, width, height, t.Tool.ConditionPct);
        else if (item is InventoryItem.EquipmentItem e)
            RenderConditionBar(drawList, rectMin, width, height, e.Equipment.ConditionPct);
        else if (item is InventoryItem.WeaponItem w)
            RenderConditionBar(drawList, rectMin, width, height, w.Weapon.ConditionPct);

        ImGui.PopStyleColor(3);

        return clicked;
    }

    private static void RenderConditionBar(ImDrawListPtr drawList, Vector2 rectMin, float width, float height, double condition)
    {
        // Small bar at the bottom of the tile
        float barHeight = 3;
        float barWidth = (width - 8) * (float)condition;

        Vector4 barColor = condition > 0.5
            ? new Vector4(0.4f, 0.8f, 0.4f, 0.9f)
            : condition > 0.25
                ? new Vector4(0.9f, 0.7f, 0.2f, 0.9f)
                : new Vector4(0.9f, 0.3f, 0.3f, 0.9f);

        var barStart = rectMin + new Vector2(4, height - 5);
        var barEnd = barStart + new Vector2(barWidth, barHeight);

        drawList.AddRectFilled(barStart, barEnd, ImGui.ColorConvertFloat4ToU32(barColor));
    }

    private static string GetItemId(InventoryItem item) => item switch
    {
        InventoryItem.ResourceItem r => $"res_{r.Resource}",
        InventoryItem.WaterItem => "water",
        InventoryItem.ToolItem t => $"tool_{t.Tool.Name}_{t.Tool.GetHashCode()}",
        InventoryItem.EquipmentItem e => $"equip_{e.Slot}",
        InventoryItem.AccessoryItem a => $"acc_{a.Accessory.Name}_{a.Accessory.GetHashCode()}",
        InventoryItem.WeaponItem w => $"weapon_{w.Weapon.Name}",
        _ => "unknown"
    };

    private static string GetBottomText(InventoryItem item) => item switch
    {
        InventoryItem.ResourceItem r => r.Count > 1 ? $"x{r.Count} {r.WeightKg:F1}kg" : $"{r.WeightKg:F2}kg",
        InventoryItem.WaterItem w => $"{w.Liters:F1} L",
        InventoryItem.ToolItem t => $"{t.Tool.Weight:F1}kg",
        InventoryItem.EquipmentItem e => $"{e.Equipment.Weight:F1}kg",
        InventoryItem.AccessoryItem a => $"+{a.Accessory.CapacityBonusKg}kg cap",
        InventoryItem.WeaponItem w => $"{w.Weapon.Weight:F1}kg",
        _ => ""
    };

    private static Vector4 GetItemColor(InventoryItem item) => item switch
    {
        InventoryItem.ResourceItem r => GetResourceColor(r.Resource),
        InventoryItem.WaterItem => new Vector4(0.3f, 0.5f, 0.8f, 0.85f),      // Blue
        InventoryItem.ToolItem => new Vector4(0.6f, 0.55f, 0.4f, 0.85f),      // Tan
        InventoryItem.EquipmentItem => new Vector4(0.5f, 0.5f, 0.65f, 0.85f), // Blue-gray
        InventoryItem.AccessoryItem => new Vector4(0.55f, 0.45f, 0.5f, 0.85f),// Mauve
        InventoryItem.WeaponItem => new Vector4(0.65f, 0.4f, 0.4f, 0.85f),    // Red-brown
        _ => new Vector4(0.4f, 0.4f, 0.4f, 0.85f)
    };

    private static Vector4 GetResourceColor(Resource resource)
    {
        var category = resource.GetCategory();
        return category switch
        {
            ResourceCategory.Fuel => new Vector4(0.55f, 0.4f, 0.25f, 0.85f),     // Brown
            ResourceCategory.Food => new Vector4(0.35f, 0.55f, 0.35f, 0.85f),    // Green
            ResourceCategory.Medicine => new Vector4(0.5f, 0.35f, 0.55f, 0.85f), // Purple
            ResourceCategory.Material => new Vector4(0.45f, 0.5f, 0.55f, 0.85f), // Blue-gray
            ResourceCategory.Tinder => new Vector4(0.6f, 0.45f, 0.3f, 0.85f),    // Orange-brown
            _ => new Vector4(0.4f, 0.4f, 0.4f, 0.85f)
        };
    }

    private void RenderItemDetails(GameContext ctx, Inventory inv)
    {
        if (_selectedItem == null)
        {
            ImGui.TextDisabled("Select an item to see details.");
            return;
        }

        switch (_selectedItem)
        {
            case InventoryItem.ResourceItem r:
                RenderResourceDetails(ctx, inv, r);
                break;
            case InventoryItem.WaterItem w:
                RenderWaterDetails(ctx, inv, w);
                break;
            case InventoryItem.ToolItem t:
                RenderToolDetails(ctx, inv, t);
                break;
            case InventoryItem.EquipmentItem e:
                RenderEquipmentDetails(ctx, inv, e);
                break;
            case InventoryItem.AccessoryItem a:
                RenderAccessoryDetails(ctx, inv, a);
                break;
            case InventoryItem.WeaponItem w:
                RenderWeaponDetails(ctx, inv, w);
                break;
        }
    }

    private void RenderResourceDetails(GameContext ctx, Inventory inv, InventoryItem.ResourceItem item)
    {
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), item.Name);
        ImGui.Separator();

        ImGui.Text($"Quantity: {item.Count}");
        ImGui.Text($"Total Weight: {item.WeightKg:F2} kg");
        ImGui.Text($"Per item: {item.WeightKg / item.Count:F3} kg");

        var category = item.Resource.GetCategory();
        ImGui.Text($"Category: {category}");

        // Add description
        string description = ResourceDescriptions.GetDescription(item.Resource);
        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped(description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        // Resource-specific info
        if (category == ResourceCategory.Food)
        {
            double perUnitWeight = item.WeightKg / item.Count;
            RenderFoodInfo(item.Resource, perUnitWeight);
            ImGui.Separator();

            // Eat action
            if (ImGui.Button("Eat", new Vector2(-1, 28)))
            {
                var result = ConsumptionHandler.Consume(ctx, item.Resource.ToString());
                _message = result.Message;
                _messageTimer = 2f;

                // Update selection if depleted
                if (inv.Count(item.Resource) == 0)
                    _selectedItem = null;
            }
        }
        else if (category == ResourceCategory.Medicine)
        {
            ImGui.TextWrapped("Medical item. Use at camp to treat injuries.");
        }

        // Drop action
        ImGui.Spacing();
        if (ImGui.Button("Drop 1", new Vector2(ImGui.GetContentRegionAvail().X / 2 - 4, 28)))
        {
            inv.Remove(item.Resource, 1);
            _message = $"Dropped 1 {item.Resource.ToDisplayName()}";
            _messageTimer = 2f;
            if (inv.Count(item.Resource) == 0)
                _selectedItem = null;
        }
        ImGui.SameLine();
        if (ImGui.Button("Drop All", new Vector2(-1, 28)))
        {
            inv.Remove(item.Resource, item.Count);
            _message = $"Dropped all {item.Resource.ToDisplayName()}";
            _messageTimer = 2f;
            _selectedItem = null;
        }
    }

    private static void RenderFoodInfo(Resource resource, double perUnitWeight)
    {
        var (caloriesPerKg, hydrationPerKg) = ConsumptionHandler.GetNutritionInfo(resource);

        if (caloriesPerKg > 0)
        {
            int calories = (int)(perUnitWeight * caloriesPerKg);
            ImGui.Text($"Calories: ~{calories} per unit");
        }

        if (hydrationPerKg > 0)
            ImGui.Text($"Hydration: +{(int)(perUnitWeight * hydrationPerKg)}");
        else if (hydrationPerKg < 0)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), "Makes you thirsty");

        // Warnings for raw food
        if (resource == Resource.RawMeat)
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), "Raw - risk of illness");
    }

    private void RenderWaterDetails(GameContext ctx, Inventory inv, InventoryItem.WaterItem item)
    {
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), "Water");
        ImGui.Separator();

        ImGui.Text($"Amount: {item.Liters:F1} liters");
        ImGui.Text($"Weight: {item.Liters:F1} kg");

        ImGui.Separator();
        ImGui.TextWrapped("Essential for survival. Drink regularly to stay hydrated.");

        ImGui.Separator();

        // Drink action
        if (ImGui.Button("Drink", new Vector2(-1, 28)))
        {
            double amount = Math.Min(0.5, inv.WaterLiters);
            inv.WaterLiters -= amount;
            ctx.player.Body.Hydration += amount * 500; // Rough hydration value
            _message = $"Drank {amount:F1}L water";
            _messageTimer = 2f;

            if (inv.WaterLiters <= 0)
                _selectedItem = null;
        }
    }

    private void RenderToolDetails(GameContext ctx, Inventory inv, InventoryItem.ToolItem item)
    {
        var tool = item.Tool;

        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), tool.Name);
        ImGui.Separator();

        ImGui.Text($"Weight: {tool.Weight:F1} kg");

        // Condition bar
        ImGui.Text("Condition:");
        ImGui.SameLine();
        Vector4 condColor = tool.ConditionPct > 0.5
            ? new Vector4(0.4f, 0.8f, 0.4f, 1f)
            : tool.ConditionPct > 0.25
                ? new Vector4(0.9f, 0.7f, 0.2f, 1f)
                : new Vector4(0.9f, 0.3f, 0.3f, 1f);
        ImGui.TextColored(condColor, $"{tool.ConditionPct * 100:F0}%");
        ImGui.ProgressBar((float)tool.ConditionPct, new Vector2(-1, 0), "");

        if (tool.Durability > 0)
            ImGui.Text($"Uses remaining: {tool.Durability}");

        ImGui.Text($"Type: {tool.ToolType}");

        // Add description
        if (!string.IsNullOrWhiteSpace(tool.Description))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped(tool.Description);
            ImGui.PopTextWrapPos();
        }

        if (tool.IsWeapon)
        {
            ImGui.Separator();
            ImGui.Text($"Damage: {tool.Damage:F0}");

            // Equip as weapon option
            if (inv.Weapon != tool)
            {
                if (ImGui.Button("Equip as Weapon", new Vector2(-1, 28)))
                {
                    inv.Tools.Remove(tool);
                    var previous = inv.EquipWeapon(tool);
                    if (previous != null)
                        inv.Tools.Add(previous);
                    _message = $"Equipped {tool.Name}";
                    _messageTimer = 2f;
                }
            }
        }

        ImGui.Separator();

        // Drop action
        if (ImGui.Button("Drop", new Vector2(-1, 28)))
        {
            inv.Tools.Remove(tool);
            _message = $"Dropped {tool.Name}";
            _messageTimer = 2f;
            _selectedItem = null;
        }
    }

    private void RenderEquipmentDetails(GameContext ctx, Inventory inv, InventoryItem.EquipmentItem item)
    {
        var gear = item.Equipment;

        ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), gear.Name);
        ImGui.Separator();

        ImGui.Text($"Slot: {item.Slot}");
        ImGui.Text($"Weight: {gear.Weight:F1} kg");

        // Condition
        ImGui.Text("Condition:");
        ImGui.SameLine();
        Vector4 condColor = gear.ConditionPct > 0.5
            ? new Vector4(0.4f, 0.8f, 0.4f, 1f)
            : gear.ConditionPct > 0.25
                ? new Vector4(0.9f, 0.7f, 0.2f, 1f)
                : new Vector4(0.9f, 0.3f, 0.3f, 1f);
        ImGui.TextColored(condColor, $"{gear.ConditionPct * 100:F0}%");
        ImGui.ProgressBar((float)gear.ConditionPct, new Vector2(-1, 0), "");

        ImGui.Separator();
        ImGui.Text($"Base Insulation: {gear.BaseInsulation:F1}");
        ImGui.Text($"Current: {gear.Insulation:F1}");
        if (gear.TotalWaterproofLevel > 0)
            ImGui.Text($"Waterproofing: {gear.TotalWaterproofLevel * 100:F0}%");

        // Add description
        if (!string.IsNullOrWhiteSpace(gear.Description))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped(gear.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        // Unequip action
        if (ImGui.Button("Unequip", new Vector2(-1, 28)))
        {
            var unequipped = inv.Unequip(item.Slot);
            if (unequipped != null)
                inv.Tools.Add(unequipped);
            _message = $"Unequipped {gear.Name}";
            _messageTimer = 2f;
            _selectedItem = null;
        }
    }

    private void RenderAccessoryDetails(GameContext ctx, Inventory inv, InventoryItem.AccessoryItem item)
    {
        var acc = item.Accessory;

        ImGui.TextColored(new Vector4(0.8f, 0.7f, 0.8f, 1f), acc.Name);
        ImGui.Separator();

        ImGui.Text($"Weight: {acc.Weight:F1} kg");
        ImGui.Text($"Capacity Bonus: +{acc.CapacityBonusKg:F1} kg");

        // Replace generic text with description
        if (!string.IsNullOrWhiteSpace(acc.Description))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped(acc.Description);
            ImGui.PopTextWrapPos();
        }
        else
        {
            ImGui.Separator();
            ImGui.TextWrapped("Increases carrying capacity.");
        }

        ImGui.Separator();

        // Remove action
        if (ImGui.Button("Remove", new Vector2(-1, 28)))
        {
            inv.Accessories.Remove(acc);
            _message = $"Removed {acc.Name}";
            _messageTimer = 2f;
            _selectedItem = null;
        }
    }

    private void RenderWeaponDetails(GameContext ctx, Inventory inv, InventoryItem.WeaponItem item)
    {
        var weapon = item.Weapon;

        ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.6f, 1f), weapon.Name);
        ImGui.Text("(Equipped Weapon)");
        ImGui.Separator();

        ImGui.Text($"Weight: {weapon.Weight:F1} kg");
        ImGui.Text($"Damage: {weapon.Damage:F0}");

        // Condition
        ImGui.Text("Condition:");
        ImGui.SameLine();
        Vector4 condColor = weapon.ConditionPct > 0.5
            ? new Vector4(0.4f, 0.8f, 0.4f, 1f)
            : weapon.ConditionPct > 0.25
                ? new Vector4(0.9f, 0.7f, 0.2f, 1f)
                : new Vector4(0.9f, 0.3f, 0.3f, 1f);
        ImGui.TextColored(condColor, $"{weapon.ConditionPct * 100:F0}%");
        ImGui.ProgressBar((float)weapon.ConditionPct, new Vector2(-1, 0), "");

        if (weapon.Durability > 0)
            ImGui.Text($"Uses remaining: {weapon.Durability}");

        // Add description
        if (!string.IsNullOrWhiteSpace(weapon.Description))
        {
            ImGui.Spacing();
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextWrapped(weapon.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();

        // Unequip action
        if (ImGui.Button("Unequip Weapon", new Vector2(-1, 28)))
        {
            var unequipped = inv.UnequipWeapon();
            if (unequipped != null)
                inv.Tools.Add(unequipped);
            _message = $"Unequipped {weapon.Name}";
            _messageTimer = 2f;
            _selectedItem = null;
        }
    }

    private static string TruncateName(string name, int maxLen)
    {
        if (name.Length <= maxLen) return name;
        return name[..(maxLen - 2)] + "..";
    }
}
