using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Turning a chosen recipe into the thing it makes: the time it takes, the materials it
/// eats, and where the result ends up.
/// </summary>
public static class CraftingHandler
{
    public static async Task Craft(GameContext ctx, CraftOption option)
    {
        var inv = ctx.Inventory;

        // Impaired hands, a dark shelter or wet fingers all cost time.
        var (craftMinutes, warnings) = CraftingEffort.ForRecipe(ctx, option);
        foreach (string warning in warnings)
            GameDisplay.AddWarning(ctx, warning);

        await RunCraftingProgress(ctx, option, craftMinutes);

        if (!ctx.player.IsAlive) return;

        if (option.ProducesFeature)
            BuildFeature(ctx, option);
        else if (option.RebuildShelter)
            RebuildShelter(ctx, option);
        else
            MakeItem(ctx, option, inv);
    }

    /// <summary>
    /// The work itself: materials come off the list as they are used up, and the result
    /// takes shape at the end. Crafting happens at camp, so nothing interrupts it.
    /// </summary>
    private static async Task RunCraftingProgress(GameContext ctx, CraftOption option, int craftMinutes)
    {
        var requirements = option.Requirements
            .Select(req => (Name: MaterialName(req.Material), Total: req.Count))
            .ToList();
        int totalMaterials = requirements.Sum(r => r.Total);

        using var view = ctx.Ui.BeginProgress(ProgressKind.Crafting, $"Working on {option.Name}...");
        var materials = view.Section("Materials");
        var result = view.Section("Result");

        var run = new TimedRun(craftMinutes, Pacing.ProgressSeconds(craftMinutes));
        view.TotalMinutes = craftMinutes;

        while (!run.Done && ctx.player.IsAlive)
        {
            float dt = await ctx.Ui.NextFrame();
            int due = run.Advance(dt);

            for (int i = 0; i < due; i++)
            {
                ctx.UpdateWithoutEvents(1, ActivityType.Crafting);
                run.MarkSimulated(1);
                if (!ctx.player.IsAlive) break;
            }

            view.Progress = run.Progress;
            view.SimulatedMinutes = run.SimulatedMinutes;

            // Materials are used up over the first four fifths of the work.
            int consumed = (int)(totalMaterials * Math.Min(run.Progress / 0.8f, 1f));
            RenderMaterials(materials, requirements, consumed);
            RenderResult(result, option, run.Done);
        }

        if (!ctx.player.IsAlive) return;

        RenderMaterials(materials, requirements, totalMaterials);
        RenderResult(result, option, complete: true);
        await view.WaitForContinue();
    }

    private static void RenderMaterials(
        ProgressSection section, List<(string Name, int Total)> requirements, int consumed)
    {
        section.Lines.Clear();
        int remaining = consumed;

        foreach (var (name, total) in requirements)
        {
            int used = Math.Min(remaining, total);
            remaining -= used;

            for (int i = 0; i < used; i++)
                section.Lines.Add(new ProgressLine($"[x] {name}", ProgressTone.Done));
            for (int i = used; i < total; i++)
                section.Lines.Add(new ProgressLine($"[ ] {name}"));
        }
    }

    private static void RenderResult(ProgressSection section, CraftOption option, bool complete)
    {
        section.Lines.Clear();
        var tone = complete ? ProgressTone.Normal : ProgressTone.Muted;
        section.Lines.Add(new ProgressLine(option.Name, tone));
        if (!string.IsNullOrEmpty(option.Description))
            section.Lines.Add(new ProgressLine(option.Description, tone));
    }

    private static void BuildFeature(GameContext ctx, CraftOption option)
    {
        var feature = option.CraftFeature(ctx.Inventory);
        if (feature == null) return;

        ctx.Camp.AddFeature(feature);

        // A project is started, not built - say so, and say what it will cost.
        if (feature is CraftingProjectFeature project)
        {
            GameDisplay.AddSuccess(ctx, $"Started construction project: {project.ProjectName}");
            GameDisplay.AddNarrative(ctx,
                $"Materials consumed. Work on it at camp to make progress - " +
                $"{project.TimeRequiredMinutes / 60:F1} hours of work in all.");

            if (project.BenefitsFromShovel)
            {
                GameDisplay.AddNarrative(ctx, ctx.Inventory.GetTool(ToolType.Shovel) != null
                    ? "Your shovel will double progress on this digging work."
                    : "A shovel would double your progress on this digging work.");
            }
        }
        else
        {
            GameDisplay.AddSuccess(ctx, $"You built a {option.Name}. It's at your camp now.");
        }

        ctx.RecordItemCrafted(option.Name);
    }

    private static void RebuildShelter(GameContext ctx, CraftOption option)
    {
        var rebuilt = option.CraftShelterRebuild(ctx.Camp, ctx.Inventory)
            ?? throw new InvalidOperationException($"Shelter rebuild '{option.Name}' produced nothing.");

        var (shelter, salvage) = rebuilt;
        GameDisplay.AddSuccess(ctx, "You rebuilt your shelter with a log frame!");
        GameDisplay.AddNarrative(ctx, shelter.GetStatusText());

        if (salvage.Count > 0)
        {
            string salvaged = string.Join(", ", salvage.Select(kvp => $"{kvp.Value} {kvp.Key.ToDisplayName()}"));
            GameDisplay.AddNarrative(ctx, $"Salvaged: {salvaged}");
        }

        ctx.RecordItemCrafted(option.Name);
    }

    private static void MakeItem(GameContext ctx, CraftOption option, Inventory inv)
    {
        var result = option.Craft(inv);

        if (result != null)
        {
            switch (result.Category)
            {
                case GearCategory.Equipment:
                    inv.Equip(result);
                    GameDisplay.AddSuccess(ctx, $"Equipped: {result.Name}");
                    break;

                case GearCategory.Accessory:
                    inv.Accessories.Add(result);
                    GameDisplay.AddSuccess(ctx, $"Crafted: {result.Name}");
                    break;

                case GearCategory.Tool:
                    if (result.IsWeapon)
                    {
                        inv.EquipWeapon(result);
                        GameDisplay.AddSuccess(ctx, $"Equipped: {result.Name}");
                    }
                    else
                    {
                        inv.Tools.Add(result);
                        GameDisplay.AddSuccess(ctx, $"Crafted: {result.Name}");
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Crafted gear '{result.Name}' has no known category.");
            }

            ctx.RecordItemCrafted(result.Name);
            return;
        }

        if (option.ProducesMaterials)
            GameDisplay.AddSuccess(ctx, $"Processed: {option.GetOutputDescription()}");
        else if (option.IsMendingRecipe)
            GameDisplay.AddSuccess(ctx, "Repaired equipment.");
        else
            GameDisplay.AddWarning(ctx, $"You couldn't finish the {option.Name}.");

        ctx.RecordItemCrafted(option.Name);
    }

    private static string MaterialName(MaterialSpecifier material) => material switch
    {
        MaterialSpecifier.Specific(var r) => r.ToDisplayName(),
        MaterialSpecifier.Category(var c) => c.ToString(),
        _ => throw new InvalidOperationException($"Unknown material specifier: {material}")
    };
}
