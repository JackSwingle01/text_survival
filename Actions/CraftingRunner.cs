using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// Handles need-based crafting. Player picks a need category, sees options, and crafts.
/// </summary>
public class CraftingRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private readonly NeedCraftingSystem _crafting = new();

    /// <summary>
    /// Run the crafting menu. Shows all categories and recipes on one screen.
    /// </summary>
    public void Run()
    {
        // Render the crafting screen
        GameDisplay.RenderCraftingScreen(_ctx, _crafting);

        // Get all craftable options across all categories
        var allCraftable = new List<(NeedCategory category, CraftOption option)>();

        foreach (var category in Enum.GetValues<NeedCategory>())
        {
            var options = _crafting.GetOptionsForNeed(category, _ctx.Inventory);
            options = options.Where(o => !IsFeatureAlreadyBuilt(o)).ToList();
            var craftable = options.Where(o => o.CanCraft(_ctx.Inventory));

            foreach (var opt in craftable)
                allCraftable.Add((category, opt));
        }

        if (allCraftable.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You don't have materials to make anything.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey(_ctx);
            Web.WebIO.ClearCrafting(_ctx);
            return;
        }

        // Let player select a recipe
        var choice = new Choice<(NeedCategory, CraftOption)?>("Select a recipe to craft:");

        foreach (var (category, option) in allCraftable)
        {
            string categoryLabel = GetCategoryShortLabel(category);
            string label = $"[{categoryLabel}] {option.Name} - {option.GetRequirementsShort()} - {option.CraftingTimeMinutes} min";
            choice.AddOption(label, (category, option));
        }

        choice.AddOption("Cancel", null);

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var selected = choice.GetPlayerChoice(_ctx);

        Web.WebIO.ClearCrafting(_ctx);

        if (selected == null)
            return;

        DoCraft(selected.Value.Item2);
    }

    /// <summary>
    /// Contextual prompt when player needs something specific.
    /// Returns true if player crafted something.
    /// </summary>
    public bool PromptForNeed(NeedCategory need, string context)
    {
        GameDisplay.AddNarrative(_ctx, $"You {context}.");

        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);
        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, $"You need {GetNeedDescription(need)}, but don't have materials to make one.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey(_ctx);
            return false;
        }

        GameDisplay.AddNarrative(_ctx, $"You could make {GetNeedDescription(need)}. Craft one now?");
        GameDisplay.Render(_ctx, statusText: "Thinking.");

        var confirm = new Choice<bool>("Craft now?");
        confirm.AddOption("Yes", true);
        confirm.AddOption("No", false);

        if (!confirm.GetPlayerChoice(_ctx))
            return false;

        // Show crafting screen for this category
        GameDisplay.RenderCraftingScreen(_ctx, _crafting, $"CRAFT {GetNeedLabel(need).ToUpper()}");

        var result = ShowOptionsForNeed(need);

        Web.WebIO.ClearCrafting(_ctx);
        return result;
    }

    private bool ShowOptionsForNeed(NeedCategory need)
    {
        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);

        // Filter out features that already exist at camp (can only build one)
        options = options.Where(o => !IsFeatureAlreadyBuilt(o)).ToList();

        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You can't make anything right now.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey(_ctx);
            return false;
        }

        var choice = new Choice<CraftOption?>("What do you want to make?");

        foreach (var option in craftable)
        {
            string label = $"{option.Name} - {option.GetRequirementsShort()} - {option.CraftingTimeMinutes} min";
            choice.AddOption(label, option);
        }

        choice.AddOption("Cancel", null);

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var selected = choice.GetPlayerChoice(_ctx);

        if (selected == null)
            return false;

        return DoCraft(selected);
    }

    private bool DoCraft(CraftOption option)
    {
        GameDisplay.AddNarrative(_ctx, $"You begin working on a {option.Name}...");

        var capacities = _ctx.player.GetCapacities();
        int totalTime = option.CraftingTimeMinutes;

        // Consciousness impairment slows crafting (+25%)
        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
        {
            totalTime = (int)(totalTime * 1.25);
            GameDisplay.AddWarning(_ctx, "Your foggy mind slows the work.");
        }

        // Manipulation impairment slows crafting (+30%)
        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
        {
            totalTime = (int)(totalTime * 1.30);
            GameDisplay.AddWarning(_ctx, "Your unsteady hands slow the work.");
        }

        int elapsed = 0;
        bool interrupted = false;

        // Craft with event checking
        while (elapsed < totalTime && !interrupted && _ctx.player.IsAlive)
        {
            int chunk = Math.Min(5, totalTime - elapsed);
            GameDisplay.Render(_ctx, statusText: "Crafting.", progress: elapsed, progressTotal: totalTime);

            elapsed += _ctx.Update(chunk, ActivityType.Crafting);

            Thread.Sleep(100);
        }

        if (!_ctx.player.IsAlive)
            return false;

        // Handle different recipe outputs
        if (option.ProducesFeature)
        {
            // Feature recipe (e.g., curing rack) - adds to camp location
            var feature = option.CraftFeature(_ctx.Inventory);
            if (feature == null)
                return false;

            _ctx.Camp.AddFeature(feature);
            GameDisplay.AddSuccess(_ctx, $"You built a {option.Name}!");
            GameDisplay.AddNarrative(_ctx, "It's now available at your camp.");
        }
        else
        {
            // Create the gear (or process materials)
            var gear = option.Craft(_ctx.Inventory);

            if (gear == null)
            {
                // Processing recipe - materials were added to inventory
                GameDisplay.AddSuccess(_ctx, $"You produced {option.GetOutputDescription()}!");
            }
            else
            {
                // Handle different gear categories
                switch (gear.Category)
                {
                    case GearCategory.Equipment:
                        var previous = _ctx.Inventory.Equip(gear);
                        GameDisplay.AddSuccess(_ctx, $"You crafted {gear.Name}!");
                        if (previous != null)
                        {
                            GameDisplay.AddNarrative(_ctx, $"You replace your {previous.Name}.");
                            // Note: previous item is lost - could add to camp storage in future
                        }
                        else
                        {
                            GameDisplay.AddNarrative(_ctx, $"You put on the {gear.Name}.");
                        }
                        break;

                    case GearCategory.Accessory:
                        _ctx.Inventory.Accessories.Add(gear);
                        GameDisplay.AddSuccess(_ctx, $"You crafted {gear.Name}!");
                        GameDisplay.AddNarrative(_ctx, $"You can now carry {_ctx.Inventory.MaxWeightKg:F1} kg total.");
                        break;

                    case GearCategory.Tool:
                        // Auto-equip weapons, otherwise add to tools
                        if (gear.IsWeapon)
                        {
                            var previousWeapon = _ctx.Inventory.EquipWeapon(gear);
                            if (previousWeapon != null)
                            {
                                _ctx.Inventory.Tools.Add(previousWeapon);
                                GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                                GameDisplay.AddNarrative(_ctx, $"You swap your {previousWeapon.Name} for the {gear.Name}.");
                            }
                            else
                            {
                                GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                                GameDisplay.AddNarrative(_ctx, $"You equip the {gear.Name}.");
                            }
                        }
                        else
                        {
                            _ctx.Inventory.Tools.Add(gear);
                            GameDisplay.AddSuccess(_ctx, $"You crafted a {gear.Name}!");
                        }
                        if (option.Durability > 0)
                        {
                            GameDisplay.AddNarrative(_ctx, $"It should last for about {option.Durability} uses.");
                        }
                        break;
                }
            }
        }

        GameDisplay.Render(_ctx, statusText: "Satisfied.");
        Input.WaitForKey(_ctx);

        return true;
    }

    private static string GetCategoryShortLabel(NeedCategory category) => category switch
    {
        NeedCategory.FireStarting => "Fire",
        NeedCategory.CuttingTool => "Cutting",
        NeedCategory.HuntingWeapon => "Weapon",
        NeedCategory.Trapping => "Trap",
        NeedCategory.Processing => "Process",
        NeedCategory.Treatment => "Medical",
        NeedCategory.Equipment => "Gear",
        NeedCategory.Lighting => "Light",
        NeedCategory.Carrying => "Carry",
        _ => category.ToString()
    };

    private static string GetNeedLabel(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "Fire-starting supplies",
        NeedCategory.CuttingTool => "A cutting tool",
        NeedCategory.HuntingWeapon => "A hunting weapon",
        NeedCategory.Trapping => "Trapping equipment",
        NeedCategory.Processing => "Process materials",
        NeedCategory.Treatment => "Medical treatments",
        NeedCategory.Equipment => "Clothing and gear",
        NeedCategory.Lighting => "Light sources",
        NeedCategory.Carrying => "Carrying gear",
        _ => need.ToString()
    };

    private static string GetNeedDescription(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "something to start a fire with",
        NeedCategory.CuttingTool => "a cutting tool",
        NeedCategory.HuntingWeapon => "a hunting weapon",
        NeedCategory.Trapping => "trapping equipment",
        NeedCategory.Processing => "processing raw materials",
        NeedCategory.Treatment => "medical treatments",
        NeedCategory.Equipment => "clothing and gear",
        NeedCategory.Lighting => "a light source",
        NeedCategory.Carrying => "something to carry more",
        _ => need.ToString().ToLower()
    };

    /// <summary>
    /// Check if a feature-producing recipe has already been built at camp.
    /// Used to prevent building duplicate camp structures.
    /// </summary>
    private bool IsFeatureAlreadyBuilt(CraftOption option)
    {
        if (!option.ProducesFeature)
            return false;

        // Check specific feature types
        if (option.Name == "Curing Rack")
            return _ctx.Camp.GetFeature<CuringRackFeature>() != null;

        // Add other feature checks here as needed

        return false;
    }
}
