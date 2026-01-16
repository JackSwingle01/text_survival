using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for viewing and managing inventory.
/// </summary>
public class InventoryOverlay
{
    public bool IsOpen { get; set; }

    private string _selectedCategory = "All";
    private static readonly string[] Categories = { "All", "Fuel", "Food", "Medicine", "Material", "Tools", "Equipment" };

    /// <summary>
    /// Render the inventory overlay. Returns true if overlay should close.
    /// </summary>
    public bool Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return false;

        bool shouldClose = false;

        ImGui.SetNextWindowPos(new Vector2(200, 100), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin("Inventory", ref open, ImGuiWindowFlags.NoCollapse))
        {
            var inv = ctx.Inventory;

            // Header with weight info
            float weightPct = (float)(inv.CurrentWeightKg / inv.MaxWeightKg);
            Vector4 weightColor = weightPct > 0.9f
                ? new Vector4(1f, 0.3f, 0.3f, 1f)
                : weightPct > 0.7f
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(0.7f, 0.9f, 0.7f, 1f);

            ImGui.TextColored(weightColor, $"Weight: {inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F1} kg");
            ImGui.ProgressBar(weightPct, new Vector2(-1, 0), "");
            ImGui.Separator();

            // Category tabs
            if (ImGui.BeginTabBar("InventoryTabs"))
            {
                foreach (var category in Categories)
                {
                    if (ImGui.BeginTabItem(category))
                    {
                        _selectedCategory = category;
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            // Content based on selected category
            ImGui.BeginChild("InventoryContent", new Vector2(0, -30), ImGuiChildFlags.Borders);

            switch (_selectedCategory)
            {
                case "All":
                    RenderAllItems(inv);
                    break;
                case "Fuel":
                    RenderFuelItems(inv);
                    break;
                case "Food":
                    RenderFoodItems(inv);
                    break;
                case "Medicine":
                    RenderMedicineItems(inv);
                    break;
                case "Material":
                    RenderMaterialItems(inv);
                    break;
                case "Tools":
                    RenderTools(inv);
                    break;
                case "Equipment":
                    RenderEquipment(inv);
                    break;
            }

            ImGui.EndChild();

            // Close button
            if (ImGui.Button("Close [I]", new Vector2(-1, 0)))
            {
                shouldClose = true;
            }
        }
        ImGui.End();

        if (!open) shouldClose = true;
        if (shouldClose) IsOpen = false;

        return shouldClose;
    }

    private void RenderAllItems(Inventory inv)
    {
        // Resources
        RenderResourceSection(inv, "Fuel", ResourceCategory.Fuel);
        RenderResourceSection(inv, "Food", ResourceCategory.Food);
        RenderResourceSection(inv, "Medicine", ResourceCategory.Medicine);
        RenderResourceSection(inv, "Materials", ResourceCategory.Material);

        // Water
        if (inv.WaterLiters > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), "Water");
            ImGui.Text($"  {inv.WaterLiters:F1} liters");
        }

        // Tools summary
        if (inv.Tools.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.6f, 1f), $"Tools ({inv.Tools.Count})");
        }

        // Equipment summary
        var equippedCount = inv.Equipment.Values.Count(e => e != null);
        if (equippedCount > 0 || inv.Weapon != null)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.9f, 1f), "Equipment");
            if (inv.Weapon != null)
                ImGui.Text($"  Weapon: {inv.Weapon.Name}");
            ImGui.Text($"  {equippedCount}/5 slots equipped");
        }
    }

    private void RenderResourceSection(Inventory inv, string label, ResourceCategory category)
    {
        var resources = ResourceCategories.Items[category]
            .Where(r => inv.Count(r) > 0)
            .ToList();

        if (resources.Count == 0) return;

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), label);

        foreach (var resource in resources)
        {
            int count = inv.Count(resource);
            double weight = inv.Weight(resource);
            string name = resource.ToDisplayName();
            ImGui.Text($"  {name}: {count} ({weight:F2} kg)");
        }
    }

    private void RenderFuelItems(Inventory inv)
    {
        ImGui.Text("Fuel & Tinder");
        ImGui.Separator();

        foreach (var resource in ResourceCategories.Items[ResourceCategory.Fuel])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                double weight = inv.Weight(resource);
                string name = resource.ToDisplayName();
                ImGui.Text($"{name}: {count} ({weight:F2} kg)");
            }
        }

        // Tinder (separate category but related)
        ImGui.Separator();
        ImGui.Text("Tinder:");
        foreach (var resource in ResourceCategories.Items[ResourceCategory.Tinder])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                string name = resource.ToDisplayName();
                ImGui.Text($"  {name}: {count}");
            }
        }
    }

    private void RenderFoodItems(Inventory inv)
    {
        ImGui.Text("Food & Water");
        ImGui.Separator();

        // Water
        if (inv.WaterLiters > 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), $"Water: {inv.WaterLiters:F1} L");
        }
        else
        {
            ImGui.TextDisabled("No water");
        }

        ImGui.Separator();

        // Food items
        bool hasFood = false;
        foreach (var resource in ResourceCategories.Items[ResourceCategory.Food])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                hasFood = true;
                double weight = inv.Weight(resource);
                string name = resource.ToDisplayName();

                // Color code food types
                Vector4 color = resource switch
                {
                    Resource.RawMeat => new Vector4(0.9f, 0.6f, 0.6f, 1f),
                    Resource.CookedMeat => new Vector4(0.8f, 0.7f, 0.5f, 1f),
                    Resource.DriedMeat => new Vector4(0.7f, 0.6f, 0.5f, 1f),
                    Resource.Berries or Resource.DriedBerries => new Vector4(0.8f, 0.5f, 0.8f, 1f),
                    _ => new Vector4(0.8f, 0.8f, 0.7f, 1f)
                };

                ImGui.TextColored(color, $"{name}: {count} ({weight:F2} kg)");
            }
        }

        if (!hasFood)
        {
            ImGui.TextDisabled("No food");
        }
    }

    private void RenderMedicineItems(Inventory inv)
    {
        ImGui.Text("Medicine");
        ImGui.Separator();

        bool hasMedicine = false;
        foreach (var resource in ResourceCategories.Items[ResourceCategory.Medicine])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                hasMedicine = true;
                string name = resource.ToDisplayName();
                ImGui.Text($"{name}: {count}");
            }
        }

        if (!hasMedicine)
        {
            ImGui.TextDisabled("No medicine");
        }
    }

    private void RenderMaterialItems(Inventory inv)
    {
        ImGui.Text("Crafting Materials");
        ImGui.Separator();

        bool hasMaterials = false;
        foreach (var resource in ResourceCategories.Items[ResourceCategory.Material])
        {
            int count = inv.Count(resource);
            if (count > 0)
            {
                hasMaterials = true;
                double weight = inv.Weight(resource);
                string name = resource.ToDisplayName();
                ImGui.Text($"{name}: {count} ({weight:F2} kg)");
            }
        }

        if (!hasMaterials)
        {
            ImGui.TextDisabled("No materials");
        }
    }

    private void RenderTools(Inventory inv)
    {
        ImGui.Text("Tools");
        ImGui.Separator();

        if (inv.Tools.Count == 0)
        {
            ImGui.TextDisabled("No tools");
            return;
        }

        foreach (var tool in inv.Tools)
        {
            // Color based on condition
            Vector4 color = tool.Condition > 0.5
                ? new Vector4(0.8f, 0.8f, 0.7f, 1f)
                : tool.Condition > 0.25
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(1f, 0.4f, 0.4f, 1f);

            ImGui.TextColored(color, $"{tool.Name}");
            ImGui.SameLine();
            ImGui.TextDisabled($"({tool.Weight:F1} kg, {tool.Condition * 100:F0}%)");

            // Tool properties
            if (tool.IsWeapon)
            {
                ImGui.Text($"  Damage: {tool.Damage:F0}");
            }
        }

        // Active torch
        if (inv.ActiveTorch != null)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f), "Active Torch");
            ImGui.Text($"  {inv.TorchBurnTimeRemainingMinutes:F0} minutes remaining");
        }
    }

    private void RenderEquipment(Inventory inv)
    {
        ImGui.Text("Equipment");
        ImGui.Separator();

        // Weapon
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.7f, 1f), "Weapon:");
        if (inv.Weapon != null)
        {
            ImGui.SameLine();
            ImGui.Text($"{inv.Weapon.Name} (Dmg: {inv.Weapon.Damage:F0})");
        }
        else
        {
            ImGui.SameLine();
            ImGui.TextDisabled("None");
        }

        ImGui.Separator();
        ImGui.Text("Armor Slots:");

        // Equipment slots
        RenderEquipSlot(inv, EquipSlot.Head, "Head");
        RenderEquipSlot(inv, EquipSlot.Chest, "Chest");
        RenderEquipSlot(inv, EquipSlot.Legs, "Legs");
        RenderEquipSlot(inv, EquipSlot.Feet, "Feet");
        RenderEquipSlot(inv, EquipSlot.Hands, "Hands");

        // Totals
        ImGui.Separator();
        ImGui.Text($"Total Insulation: {inv.TotalInsulation:F1}");
        ImGui.Text($"Total Armor Weight: {inv.TotalEquipmentWeightKg:F1} kg");
        ImGui.Text($"Waterproofing: {inv.CalculateWaterproofingLevel() * 100:F0}%");

        // Accessories
        if (inv.Accessories.Count > 0)
        {
            ImGui.Separator();
            ImGui.Text("Accessories:");
            foreach (var acc in inv.Accessories)
            {
                ImGui.Text($"  {acc.Name} (+{acc.CapacityBonusKg} kg capacity)");
            }
        }
    }

    private void RenderEquipSlot(Inventory inv, EquipSlot slot, string label)
    {
        var gear = inv.GetEquipment(slot);
        ImGui.Text($"  {label}:");
        ImGui.SameLine();
        if (gear != null)
        {
            Vector4 color = gear.Condition > 0.5
                ? new Vector4(0.7f, 0.8f, 0.7f, 1f)
                : gear.Condition > 0.25
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(1f, 0.4f, 0.4f, 1f);
            ImGui.TextColored(color, $"{gear.Name} ({gear.Condition * 100:F0}%)");
        }
        else
        {
            ImGui.TextDisabled("Empty");
        }
    }
}
