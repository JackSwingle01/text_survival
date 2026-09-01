using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.UI;
using text_survival.Desktop;

namespace text_survival.Actions;

/// <summary>
/// The moment between spotting an animal and stalking it: flavor, a behavioral hint, and the choice to approach.
/// The stalk, the fight, and everything the world remembers afterward belong to the combat system.
/// </summary>
public static class HuntRunner
{
    public static CombatResult Run(Animal target, GameContext ctx)
    {
        if (!PromptApproach(target, ctx))
            return CombatResult.Fled;

        ctx.RecordAnimalEncounter(target.AnimalType);
        ctx.Inventory.GetOrEquipWeapon(ctx, Items.ToolType.Spear);

        return CombatOrchestrator.RunHunt(ctx, target);
    }

    private static bool PromptApproach(Animal target, GameContext ctx)
    {
        var sighting = HuntingSightingSelector.SelectForAnimal(target, ctx);
        var behavior = HuntingSightingSelector.MapActivityToBehavior(target);
        string hint = HuntingSightingSelector.GetBehaviorHint(behavior);

        GameDisplay.AddNarrative(ctx, $"{sighting.Description}. {hint}");

        string traitDesc = target.GetTraitDescription();
        string message = string.IsNullOrEmpty(traitDesc)
            ? $"You spot a {target.Name.ToLower()}. Approach?"
            : $"You spot a {target.Name.ToLower()} ({traitDesc}). Approach?";

        var choices = new List<string> { "Approach", "Let it go" };
        string choice = DesktopIO.Select(ctx, message, choices, s => s);

        return choice == "Approach";
    }
}
