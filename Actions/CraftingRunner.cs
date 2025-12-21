using text_survival.Bodies;
using text_survival.Crafting;
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
            GameDisplay.AddNarrative("You don't have materials to make anything useful.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey();
            return;
        }

        var choice = new Choice<NeedCategory?>("What do you need?");
        foreach (var need in needs)
        {
            choice.AddOption(GetNeedLabel(need), need);
        }
        choice.AddOption("Never mind", null);

        GameDisplay.Render(_ctx, statusText: "Thinking.");
        var selectedNeed = choice.GetPlayerChoice();

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
        GameDisplay.AddNarrative($"You {context}.");

        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);
        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative($"You need {GetNeedDescription(need)}, but don't have materials to make one.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey();
            return false;
        }

        GameDisplay.AddNarrative($"You could make {GetNeedDescription(need)}. Craft one now?");
        GameDisplay.Render(_ctx, statusText: "Thinking.");

        var confirm = new Choice<bool>("Craft now?");
        confirm.AddOption("Yes", true);
        confirm.AddOption("No", false);

        if (!confirm.GetPlayerChoice())
            return false;

        return ShowOptionsForNeed(need);
    }

    private bool ShowOptionsForNeed(NeedCategory need)
    {
        var options = _crafting.GetOptionsForNeed(need, _ctx.Inventory);

        if (options.Count == 0)
        {
            GameDisplay.AddNarrative("You don't have any materials for this.");
            return false;
        }

        // Show what's available first
        var craftable = options.Where(o => o.CanCraft(_ctx.Inventory)).ToList();
        var uncraftable = options.Where(o => !o.CanCraft(_ctx.Inventory)).ToList();

        if (uncraftable.Any())
        {
            GameDisplay.AddNarrative("Not enough materials for:");
            foreach (var opt in uncraftable)
            {
                var (_, missing) = opt.CheckRequirements(_ctx.Inventory);
                GameDisplay.AddNarrative($"  {opt.Name} - need: {string.Join(", ", missing)}");
            }
        }

        if (craftable.Count == 0)
        {
            GameDisplay.AddNarrative("You can't make anything right now.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Input.WaitForKey();
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
        var selected = choice.GetPlayerChoice();

        if (selected == null)
            return false;

        return DoCraft(selected);
    }

    private bool DoCraft(CraftOption option)
    {
        GameDisplay.AddNarrative($"You begin working on a {option.Name}...");

        // Consciousness impairment slows crafting
        var consciousness = _ctx.player.GetCapacities().Consciousness;
        int totalTime = option.CraftingTimeMinutes;
        if (AbilityCalculator.IsConsciousnessImpaired(consciousness))
        {
            totalTime = (int)(totalTime * 1.25);
            GameDisplay.AddWarning("Your foggy mind slows the work.");
        }
        int elapsed = 0;
        bool interrupted = false;

        // Craft with event checking
        while (elapsed < totalTime && !interrupted && _ctx.player.IsAlive)
        {
            int chunk = Math.Min(5, totalTime - elapsed);
            GameDisplay.Render(_ctx, statusText: "Crafting.", progress: elapsed, progressTotal: totalTime);

            var result = _ctx.Update(chunk, ActivityType.Crafting);
            elapsed += result.MinutesElapsed;

            if (result.TriggeredEvent != null)
            {
                GameEventRegistry.HandleEvent(_ctx, result.TriggeredEvent);

                if (_ctx.PendingEncounter != null)
                {
                    // TODO: Handle camp encounter
                    _ctx.PendingEncounter = null;
                }

                // Continue crafting after event (events don't cancel craft, just interrupt briefly)
            }

            Thread.Sleep(100);
        }

        if (!_ctx.player.IsAlive)
            return false;

        // Create the tool
        var tool = option.Craft(_ctx.Inventory);

        // Auto-equip weapons, otherwise add to tools
        if (tool.IsWeapon)
        {
            var previous = _ctx.Inventory.EquipWeapon(tool);
            if (previous != null)
            {
                _ctx.Inventory.Tools.Add(previous);
                GameDisplay.AddSuccess($"You crafted a {tool.Name}!");
                GameDisplay.AddNarrative($"You swap your {previous.Name} for the {tool.Name}.");
            }
            else
            {
                GameDisplay.AddSuccess($"You crafted a {tool.Name}!");
                GameDisplay.AddNarrative($"You equip the {tool.Name}.");
            }
        }
        else
        {
            _ctx.Inventory.Tools.Add(tool);
            GameDisplay.AddSuccess($"You crafted a {tool.Name}!");
        }
        if (option.Durability > 0)
        {
            GameDisplay.AddNarrative($"It should last for about {option.Durability} uses.");
        }

        GameDisplay.Render(_ctx, statusText: "Satisfied.");
        Input.WaitForKey();

        return true;
    }

    private static string GetNeedLabel(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "Fire-starting supplies",
        NeedCategory.CuttingTool => "A cutting tool",
        NeedCategory.HuntingWeapon => "A hunting weapon",
        _ => need.ToString()
    };

    private static string GetNeedDescription(NeedCategory need) => need switch
    {
        NeedCategory.FireStarting => "something to start a fire with",
        NeedCategory.CuttingTool => "a cutting tool",
        NeedCategory.HuntingWeapon => "a hunting weapon",
        _ => need.ToString().ToLower()
    };
}
