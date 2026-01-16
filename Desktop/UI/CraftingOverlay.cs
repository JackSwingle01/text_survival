using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Crafting;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for crafting items.
/// </summary>
public class CraftingOverlay
{
    public bool IsOpen { get; set; }

    private NeedCategory _selectedCategory = NeedCategory.FireStarting;
    private CraftOption? _selectedOption;
    private string? _message;
    private float _messageTimer;

    private static readonly Dictionary<NeedCategory, string> CategoryNames = new()
    {
        [NeedCategory.FireStarting] = "Fire",
        [NeedCategory.CuttingTool] = "Cutting",
        [NeedCategory.HuntingWeapon] = "Weapons",
        [NeedCategory.Trapping] = "Traps",
        [NeedCategory.Processing] = "Process",
        [NeedCategory.Treatment] = "Medical",
        [NeedCategory.Equipment] = "Armor",
        [NeedCategory.Lighting] = "Light",
        [NeedCategory.Carrying] = "Bags",
        [NeedCategory.CampInfrastructure] = "Camp",
        [NeedCategory.Mending] = "Mend"
    };

    /// <summary>
    /// Render the crafting overlay.
    /// Returns the crafted item name if something was crafted, null otherwise.
    /// </summary>
    public string? Render(GameContext ctx, NeedCraftingSystem crafting, float deltaTime)
    {
        if (!IsOpen) return null;

        string? craftedItem = null;

        // Update message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= deltaTime;
            if (_messageTimer <= 0)
                _message = null;
        }

        ImGui.SetNextWindowPos(new Vector2(150, 80), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(600, 550), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin("Crafting", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Category buttons
            ImGui.Text("Category:");
            ImGui.SameLine();

            // First row of categories
            RenderCategoryButtons([
                NeedCategory.FireStarting, NeedCategory.CuttingTool, NeedCategory.HuntingWeapon,
                NeedCategory.Trapping, NeedCategory.Processing, NeedCategory.Treatment
            ], ctx, crafting);

            // Second row
            RenderCategoryButtons([
                NeedCategory.Equipment, NeedCategory.Lighting, NeedCategory.Carrying,
                NeedCategory.CampInfrastructure, NeedCategory.Mending
            ], ctx, crafting);

            ImGui.Separator();

            // Show message if any
            if (_message != null)
            {
                ImGui.TextColored(new Vector4(0.3f, 1f, 0.5f, 1f), _message);
                ImGui.Separator();
            }

            // Main content area - two columns
            float contentHeight = ImGui.GetContentRegionAvail().Y - 30;

            // Left: Recipe list
            ImGui.BeginChild("RecipeList", new Vector2(250, contentHeight), ImGuiChildFlags.Border);
            RenderRecipeList(ctx, crafting);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right: Selected recipe details
            ImGui.BeginChild("RecipeDetails", new Vector2(0, contentHeight), ImGuiChildFlags.Border);
            craftedItem = RenderRecipeDetails(ctx, crafting);
            ImGui.EndChild();

            // Close button
            if (ImGui.Button("Close [C]", new Vector2(-1, 0)))
            {
                IsOpen = false;
            }
        }
        ImGui.End();

        if (!open) IsOpen = false;

        return craftedItem;
    }

    private void RenderCategoryButtons(NeedCategory[] categories, GameContext ctx, NeedCraftingSystem crafting)
    {
        foreach (var category in categories)
        {
            bool selected = _selectedCategory == category;
            var options = crafting.GetOptionsForNeed(category, ctx.Inventory);
            int craftableCount = options.Count(o => o.CanCraft(ctx.Inventory));

            // Color based on availability
            Vector4 buttonColor;
            if (selected)
                buttonColor = new Vector4(0.3f, 0.5f, 0.8f, 1f);
            else if (craftableCount > 0)
                buttonColor = new Vector4(0.3f, 0.6f, 0.3f, 1f);
            else if (options.Count > 0)
                buttonColor = new Vector4(0.5f, 0.5f, 0.4f, 1f);
            else
                buttonColor = new Vector4(0.3f, 0.3f, 0.3f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);

            string label = CategoryNames.GetValueOrDefault(category, category.ToString());
            if (craftableCount > 0)
                label += $" ({craftableCount})";

            if (ImGui.Button(label))
            {
                _selectedCategory = category;
                _selectedOption = null;
            }

            ImGui.PopStyleColor();
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void RenderRecipeList(GameContext ctx, NeedCraftingSystem crafting)
    {
        var options = crafting.GetOptionsForNeed(_selectedCategory, ctx.Inventory, showAll: true);

        if (options.Count == 0)
        {
            ImGui.TextDisabled("No recipes in this category.");
            return;
        }

        ImGui.Text($"{CategoryNames.GetValueOrDefault(_selectedCategory, _selectedCategory.ToString())} Recipes:");
        ImGui.Separator();

        foreach (var option in options)
        {
            bool canCraft = option.CanCraft(ctx.Inventory);
            bool isSelected = _selectedOption == option;

            // Color based on craftability
            Vector4 textColor;
            if (isSelected)
                textColor = new Vector4(1f, 1f, 0.8f, 1f);
            else if (canCraft)
                textColor = new Vector4(0.5f, 1f, 0.5f, 1f);
            else
                textColor = new Vector4(0.6f, 0.6f, 0.6f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Text, textColor);

            string label = option.Name;
            if (canCraft)
                label = "[+] " + label;

            if (ImGui.Selectable(label, isSelected))
            {
                _selectedOption = option;
            }

            ImGui.PopStyleColor();

            // Show brief requirement on hover
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(option.Description);
                ImGui.EndTooltip();
            }
        }
    }

    private string? RenderRecipeDetails(GameContext ctx, NeedCraftingSystem crafting)
    {
        string? craftedItem = null;

        if (_selectedOption == null)
        {
            ImGui.TextDisabled("Select a recipe from the list.");
            return null;
        }

        var option = _selectedOption;
        var inv = ctx.Inventory;

        // Recipe name and description
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), option.Name);
        ImGui.Separator();
        ImGui.TextWrapped(option.Description);
        ImGui.Separator();

        // Requirements
        ImGui.Text("Requirements:");
        var (canCraft, missing) = option.CheckRequirements(inv);

        foreach (var req in option.Requirements)
        {
            int have = GetMaterialCount(inv, req.Material);
            bool hasMaterial = have >= req.Count;

            Vector4 color = hasMaterial
                ? new Vector4(0.5f, 1f, 0.5f, 1f)
                : new Vector4(1f, 0.5f, 0.5f, 1f);

            string materialName = GetMaterialDisplayName(req.Material);
            ImGui.TextColored(color, $"  {materialName}: {have}/{req.Count}");
        }

        // Tool requirements
        if (option.RequiredTools.Count > 0)
        {
            ImGui.Text("Tools needed:");
            foreach (var toolType in option.RequiredTools)
            {
                var tool = inv.GetTool(toolType);
                Vector4 color;
                string status;

                if (tool == null)
                {
                    color = new Vector4(1f, 0.5f, 0.5f, 1f);
                    status = "missing";
                }
                else if (tool.Durability < 1)
                {
                    color = new Vector4(1f, 0.7f, 0.3f, 1f);
                    status = "broken";
                }
                else
                {
                    color = new Vector4(0.5f, 1f, 0.5f, 1f);
                    status = $"{tool.Durability} uses";
                }

                ImGui.TextColored(color, $"  {toolType}: {status}");
            }
        }

        ImGui.Separator();

        // Crafting info
        ImGui.Text($"Time: {option.CraftingTimeMinutes} minutes");
        if (option.Durability > 0)
            ImGui.Text($"Durability: {option.Durability} uses");

        // Output info
        if (option.ProducesMaterials)
        {
            ImGui.Text($"Produces: {option.GetOutputDescription()}");
        }

        ImGui.Separator();

        // Craft button
        if (canCraft)
        {
            if (ImGui.Button("Craft", new Vector2(-1, 30)))
            {
                // Perform crafting
                var result = option.Craft(inv);

                if (result != null)
                {
                    inv.Tools.Add(result);
                    _message = $"Crafted: {result.Name}";
                    craftedItem = result.Name;
                }
                else if (option.ProducesMaterials)
                {
                    _message = $"Processed: {option.GetOutputDescription()}";
                    craftedItem = option.Name;
                }
                else if (option.IsMendingRecipe)
                {
                    _message = $"Repaired equipment";
                    craftedItem = option.Name;
                }

                _messageTimer = 3.0f;

                // Advance game time
                ctx.Update(option.CraftingTimeMinutes, ActivityType.Crafting);
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Missing materials", new Vector2(-1, 30));
            ImGui.EndDisabled();

            // Show what's missing
            if (missing.Count > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.4f, 1f), "Need:");
                foreach (var item in missing)
                {
                    ImGui.Text($"  - {item}");
                }
            }
        }

        return craftedItem;
    }

    private static int GetMaterialCount(Inventory inv, MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var resource) => inv.Count(resource),
        MaterialSpecifier.Category(var category) => inv.GetCount(category),
        _ => 0
    };

    private static string GetMaterialDisplayName(MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var r) => r.ToDisplayName(),
        MaterialSpecifier.Category(var c) => c.ToString(),
        _ => "unknown"
    };

    public void ShowMessage(string message)
    {
        _message = message;
        _messageTimer = 3.0f;
    }
}
