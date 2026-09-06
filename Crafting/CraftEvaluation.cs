using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Items;

namespace text_survival.Crafting;

/// <summary>A side-effect-free explanation of a particular action in the current state.</summary>
public sealed class CraftEvaluation
{
    public required CraftInputs Inputs { get; init; }
    public required List<string> Blockers { get; init; }
    public required List<string> Warnings { get; init; }
    public required int Minutes { get; init; }
    public required int LaterWorkMinutes { get; init; }
    public Gear? Output { get; init; }
    public bool Ready => Blockers.Count == 0;

    public static CraftEvaluation For(GameContext ctx, CraftOption option)
    {
        var inputs = CraftInputs.Resolve(option, ctx.Inventory);
        var blockers = inputs.Missing.ToList();
        if (option.Prerequisite?.Invoke(ctx) is { } reason) blockers.Add(reason);
        if (option.TargetGear != null && !CraftInputs.OwnedGear(ctx.Inventory).Contains(option.TargetGear))
            blockers.Add("The selected equipment is no longer in your inventory");
        if (option.TargetBlocker?.Invoke() is { } targetReason) blockers.Add(targetReason);
        var (minutes, warnings) = CraftingEffort.ForRecipe(ctx, option);
        foreach (var (tool, wear) in inputs.Tools)
            if (tool.Durability == wear && wear > 0) warnings.Add($"{tool.Name} will break after this work.");
        int later = option.ProjectWorkMinutes;
        if (option.ProjectBenefitsFromShovel && CraftInputs.OwnedGear(ctx.Inventory).Any(g => g.ToolType == ToolType.Shovel && g.Works))
            later = (int)Math.Ceiling(later / 2.0);
        return new CraftEvaluation
        {
            Inputs = inputs, Blockers = blockers, Warnings = warnings,
            Minutes = minutes, LaterWorkMinutes = later,
            Output = option.TargetResult?.Invoke() ?? option.GearFactory?.Invoke(option.Durability)
        };
    }

    public static IEnumerable<Gear> Comparisons(Inventory inv, Gear output) =>
        CraftInputs.OwnedGear(inv).Where(g => output.Slot != null ? g.Slot == output.Slot :
            output.ToolType != null ? g.ToolType == output.ToolType :
            output.CapacityBonusKg > 0 && g.CapacityBonusKg > 0).Distinct();

    public static List<string> Describe(Gear gear)
    {
        var lines = new List<string>();
        if (gear.Slot != null) lines.Add($"{gear.Slot}: warmth {gear.CloValue:F2} clo; waterproofing {gear.TotalWaterproofLevel:P0}");
        if (gear.CapacityBonusKg > 0) lines.Add($"Carrying capacity +{gear.CapacityBonusKg:F1} kg");
        if (gear.ToolType == ToolType.Knife) lines.Add("Cutting tool: prepare wood, make tools, and cut bindings");
        if (gear.Damage is { } damage) lines.Add($"Weapon damage {damage:0.#}");
        if (gear.ToolType is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill)
            lines.Add($"Base ignition chance {FireHandler.GetToolBaseChance(gear):P0}; conditions change the final chance");
        if (gear.EmberBurnHoursMax > 0) lines.Add($"Carries an ember for up to {gear.EmberBurnHoursMax:0.#} hours; starts unlit");
        if (gear.ToolType == ToolType.Tent)
            lines.Add($"Shelter: wind coverage {gear.ShelterWindCoverage:P0}, insulation {gear.ShelterTempInsulation:P0}");
        if (gear.TreatsEffect != null) lines.Add($"Treats {gear.TreatsEffect}: reduction {gear.EffectReduction:P0}");
        if (gear.SecondaryTreatsEffect != null) lines.Add($"Also treats {gear.SecondaryTreatsEffect}: reduction {gear.SecondaryEffectReduction:P0}");
        if (gear.GrantsEffect != null) lines.Add($"Grants {gear.GrantsEffect}");
        string condition = gear.Durability == -1 ? "Does not wear out" :
            gear.ToolType is ToolType.Treatment or ToolType.Torch or ToolType.EmberCarrier ? $"{gear.Durability} charge(s)" :
            $"Condition {gear.ConditionPct:P0} ({gear.Durability}/{gear.MaxDurability}); wear varies by activity";
        lines.Add(condition);
        lines.Add($"Weight {gear.Weight:F2} kg");
        return lines;
    }
}
