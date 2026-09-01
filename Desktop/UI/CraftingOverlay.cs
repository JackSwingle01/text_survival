using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Desktop.Input;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for crafting items.
/// </summary>
public class CraftingOverlay
{
    public bool IsOpen { get; set; }

    private NeedCategory _selectedCategory = NeedCategory.FireStarting;
    private CraftOption? _selectedOption;

    /// <summary>
    /// The recipe the player committed to. Crafting passes game time, so the caller runs
    /// it - the screen only reports the choice.
    /// </summary>
    public CraftOption? SelectedRecipe { get; private set; }

    public void ClearSelectedRecipe() => SelectedRecipe = null;

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

    /// <summary>Render the crafting overlay.</summary>
    public void Render(GameContext ctx, NeedCraftingSystem crafting, float deltaTime)
    {
        if (!IsOpen) return;

        OverlaySizes.SetupWide();

        bool open = IsOpen;
        if (ImGui.Begin("Crafting", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Category buttons - all in one row with smaller buttons
            ImGui.Text("Category:");
            ImGui.SameLine();

            // All categories in a single row
            RenderCategoryButtonsCompact([
                NeedCategory.FireStarting, NeedCategory.CuttingTool, NeedCategory.HuntingWeapon,
                NeedCategory.Trapping, NeedCategory.Processing, NeedCategory.Treatment,
                NeedCategory.Equipment, NeedCategory.Lighting, NeedCategory.Carrying,
                NeedCategory.CampInfrastructure, NeedCategory.Mending
            ], ctx, crafting);

            ImGui.Separator();

            // Main content area - two columns
            float contentHeight = ImGui.GetContentRegionAvail().Y - 30;

            // Left: Recipe list
            ImGui.BeginChild("RecipeList", new Vector2(250, contentHeight), ImGuiChildFlags.Borders);
            RenderRecipeList(ctx, crafting);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right: Selected recipe details
            ImGui.BeginChild("RecipeDetails", new Vector2(0, contentHeight), ImGuiChildFlags.Borders);
            RenderRecipeDetails(ctx, crafting);
            ImGui.EndChild();

            // Close button
            if (ImGui.Button($"Close {HotkeyRegistry.GetTip(HotkeyAction.Cancel)}", new Vector2(-1, 0)))
            {
                IsOpen = false;
            }
        }
        ImGui.End();

        if (!open) IsOpen = false;
    }

    private void RenderCategoryButtonsCompact(NeedCategory[] categories, GameContext ctx, NeedCraftingSystem crafting)
    {
        // Use smaller button sizing for compact single row
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2, 2));

        foreach (var category in categories)
        {
            bool selected = _selectedCategory == category;
            // Use discovery-filtered options
            var options = crafting.GetDiscoveredOptionsForNeed(category, ctx.Inventory, ctx.Discoveries);
            int craftableCount = options.Count(o => o.CanCraft(ctx.Inventory));
            int hiddenCount = crafting.GetHiddenRecipeCount(category, ctx.Discoveries);

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

            // Use short label only
            string label = CategoryNames.GetValueOrDefault(category, category.ToString());

            if (ImGui.Button(label))
            {
                _selectedCategory = category;
                _selectedOption = null;
            }

            // Show craftable count and hidden count in tooltip
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                if (craftableCount > 0)
                    ImGui.Text($"{craftableCount} craftable");
                if (hiddenCount > 0)
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), $"{hiddenCount} locked");
                ImGui.EndTooltip();
            }

            ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        ImGui.PopStyleVar(2);
        ImGui.NewLine();
    }

    private void RenderRecipeList(GameContext ctx, NeedCraftingSystem crafting)
    {
        // Get discovered (visible) recipes
        var discoveredOptions = crafting.GetDiscoveredOptionsForNeed(_selectedCategory, ctx.Inventory, ctx.Discoveries);

        // Get locked recipes grouped by missing resource
        var lockedByResource = crafting.GetLockedRecipesByMissingResource(_selectedCategory, ctx.Discoveries);

        if (discoveredOptions.Count == 0 && lockedByResource.Count == 0)
        {
            ImGui.TextDisabled("No recipes in this category.");
            return;
        }

        // Render discovered recipes
        if (discoveredOptions.Count > 0)
        {
            ImGui.Text($"{CategoryNames.GetValueOrDefault(_selectedCategory, _selectedCategory.ToString())} Recipes:");
            ImGui.Separator();

            foreach (var option in discoveredOptions)
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

        // Render locked recipes grouped by missing resource
        if (lockedByResource.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();

            foreach (var (missingResource, lockedRecipes) in lockedByResource.OrderBy(kvp => kvp.Key.ToDisplayName()))
            {
                // Section header for missing resource
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"Find {missingResource.ToDisplayName()} to unlock:");

                foreach (var recipe in lockedRecipes)
                {
                    ImGui.TextColored(new Vector4(0.4f, 0.4f, 0.4f, 1f), $"  ??? {recipe.Name}");

                    // Show what the recipe makes on hover
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(recipe.Description);
                        ImGui.EndTooltip();
                    }
                }

                ImGui.Spacing();
            }
        }
    }

    private void RenderRecipeDetails(GameContext ctx, NeedCraftingSystem crafting)
    {
        if (_selectedOption == null)
        {
            ImGui.TextDisabled("Select a recipe from the list.");
            return;
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
                SelectedRecipe = option;
                IsOpen = false;
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
}
