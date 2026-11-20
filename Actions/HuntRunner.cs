using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// The moment between spotting an animal and stalking it: flavor, a behavioral hint, and the choice to approach.
/// The stalk, the fight, and everything the world remembers afterward belong to the combat system.
/// </summary>
public static class HuntRunner
{
    public static async Task<CombatResult> Run(Animal target, GameContext ctx)
    {
        var sighting = HuntingSightingSelector.SelectForAnimal(target, ctx);
        if (!await PromptApproach(target, sighting, ctx))
            return CombatResult.Fled;

        ctx.RecordAnimalEncounter(target.AnimalType);
        await ReadySpear(ctx);

        return await CombatOrchestrator.RunHunt(ctx, target, sighting.DetectionModifier);
    }

    /// <summary>
    /// Put a spear in hand before the stalk. If several will do, the player picks.
    /// </summary>
    private static async Task ReadySpear(GameContext ctx)
    {
        var inv = ctx.Inventory;
        if (inv.Weapon?.ToolType == ToolType.Spear) return;

        var available = inv.Tools.Where(t => t.IsWeapon && t.ToolType == ToolType.Spear).ToList();
        if (available.Count == 0) return;

        Gear chosen = available[0];
        if (available.Count > 1)
        {
            var choice = new Choice<Gear>("Which weapon?");
            foreach (var weapon in available)
                choice.AddOption($"{weapon.Name} ({weapon.Damage:F0} dmg)", weapon);
            chosen = await choice.GetPlayerChoice(ctx);
        }

        inv.Tools.Remove(chosen);
        var previous = inv.EquipWeapon(chosen);
        if (previous != null)
            inv.Tools.Add(previous);
    }

    private static async Task<bool> PromptApproach(Animal target, HuntingSighting sighting, GameContext ctx)
    {
        var behavior = HuntingSightingSelector.MapActivityToBehavior(target);
        string hint = HuntingSightingSelector.GetBehaviorHint(behavior);

        GameDisplay.AddNarrative(ctx, $"{sighting.Description.TrimEnd('.')}. {hint}");

        string message = $"You spot {target.GetTraitDescription()}. Approach?";

        var choices = new List<string> { "Approach", "Let it go" };
        string choice = await ctx.Ui.Select(message, choices, label => label);

        return choice == "Approach";
    }
}
