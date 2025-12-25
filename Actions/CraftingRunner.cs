using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
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
    /// Run the crafting menu. Shows need categories, then options.
    /// </summary>
    public void Run()
    {
        var needs = _crafting.GetAvailableNeeds(_ctx.Inventory);

        if (needs.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You don't have materials to make anything useful.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey(_ctx);
            return;
        }

        var choice = new Choice<NeedCategory?>("What do you need?");
        foreach (var need in needs)
        {
            choice.AddOption(GetNeedLabel(need), need);
        }
        choice.AddOption("Never mind", null);

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        var selectedNeed = choice.GetPlayerChoice(_ctx);

        if (selectedNeed == null)
            return;

        ShowOptionsForNeed(selectedNeed.Value);
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

        return ShowOptionsForNeed(need);
    }

    private bool ShowOptionsForNeed(NeedCategory need)
    {
        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);

        // Filter out features that already exist at camp (can only build one)
        options = options.Where(o => !IsFeatureAlreadyBuilt(o)).ToList();

        if (options.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You don't have any materials for this.");
            return false;
        }

        // Show what's available first
        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();
        var uncraftable = options.Where(o => !o.CanCraft(_ctx.Inventory)).ToList();

        if (uncraftable.Any())
        {
            GameDisplay.AddNarrative(_ctx, "Not enough materials for:");
            foreach (var opt in uncraftable)
            {
                var (_, missing) = opt.CheckRequirements(_ctx.Inventory);
                GameDisplay.AddNarrative(_ctx, $"  {opt.Name} - need: {string.Join(", ", missing)}");
            }
        }

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
        else if (option.ProducesEquipment)
        {
            // Equipment recipe
            var equipment = option.CraftEquipment(_ctx.Inventory);
            if (equipment == null)
                return false;

            var previous = _ctx.Inventory.Equip(equipment);
            GameDisplay.AddSuccess(_ctx, $"You crafted {equipment.Name}!");
            if (previous != null)
            {
                GameDisplay.AddNarrative(_ctx, $"You replace your {previous.Name}.");
                // Note: previous item is lost - could add to camp storage in future
            }
            else
            {
                GameDisplay.AddNarrative(_ctx, $"You put on the {equipment.Name}.");
            }
        }
        else
        {
            // Create the tool (or process materials)
            var tool = option.Craft(_ctx.Inventory);

            if (tool == null)
            {
                // Processing recipe - materials were added to inventory
                GameDisplay.AddSuccess(_ctx, $"You produced {option.GetOutputDescription()}!");
            }
            else
            {
                // Auto-equip weapons, otherwise add to tools
                if (tool.IsWeapon)
                {
                    var previous = _ctx.Inventory.EquipWeapon(tool);
                    if (previous != null)
                    {
                        _ctx.Inventory.Tools.Add(previous);
                        GameDisplay.AddSuccess(_ctx, $"You crafted a {tool.Name}!");
                        GameDisplay.AddNarrative(_ctx, $"You swap your {previous.Name} for the {tool.Name}.");
                    }
                    else
                    {
                        GameDisplay.AddSuccess(_ctx, $"You crafted a {tool.Name}!");
                        GameDisplay.AddNarrative(_ctx, $"You equip the {tool.Name}.");
                    }
                }
                else
                {
                    _ctx.Inventory.Tools.Add(tool);
                    GameDisplay.AddSuccess(_ctx, $"You crafted a {tool.Name}!");
                }
                if (option.Durability > 0)
                {
                    GameDisplay.AddNarrative(_ctx, $"It should last for about {option.Durability} uses.");
                }
            }
        }

        GameDisplay.Render(_ctx, statusText: "Satisfied.");
        Input.WaitForKey(_ctx);

        return true;
    }

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
