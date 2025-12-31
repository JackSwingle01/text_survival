using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

public static class TreatmentHandler
{
    private static readonly List<(Resource Resource, string EffectKind, string Description, double EffectReduction)> DirectTreatments =
    [
        // SphagnumMoss - absorbent dressing with mild antiseptic
        (Resource.SphagnumMoss, "Bleeding", "Press sphagnum moss against the wound. It absorbs blood and has mild antiseptic properties.", 0.4),
        (Resource.SphagnumMoss, "Inflamed", "Apply sphagnum moss as antiseptic dressing", 0.05),

        // BirchPolypore - styptic + mild fever
        (Resource.BirchPolypore, "Bleeding", "Press birch polypore to staunch bleeding", 0.4),
        (Resource.BirchPolypore, "Fever", "Chew birch polypore for infection", 0.2),

        // PineResin - wound seal (BEST inflamed raw)
        (Resource.PineResin, "Inflamed", "Seal wound with antiseptic pine resin", 0.45),
        (Resource.PineResin, "Bleeding", "Seal wound with sticky resin", 0.2),

        // Usnea - antimicrobial
        (Resource.Usnea, "Inflamed", "Pack wound with antimicrobial usnea", 0.4),
        (Resource.Usnea, "Coughing", "Chew usnea lichen for throat", 0.15),

        // Amadou - weak wound treatment (fire utility is main value)
        (Resource.Amadou, "Bleeding", "Press amadou felt into wound", 0.25),
        (Resource.Amadou, "Inflamed", "Apply amadou as dressing", 0.15),

        // Chaga - fever fighter (BEST fever raw)
        (Resource.Chaga, "Fever", "Chew raw chaga for immune boost", 0.35),
        (Resource.Chaga, "Coughing", "Chew chaga for respiratory relief", 0.2),

        // WillowBark - pain/fever (BEST pain raw)
        (Resource.WillowBark, "Pain", "Chew willow bark for pain relief", 0.45),
        (Resource.WillowBark, "Fever", "Chew willow bark to reduce fever", 0.15),

        // JuniperBerry - gut + mild pain (BEST nauseous)
        (Resource.JuniperBerry, "Nauseous", "Chew juniper berries to settle stomach", 0.5),
        (Resource.JuniperBerry, "Pain", "Chew juniper berries for mild relief", 0.1),

        // PineNeedles - respiratory (BEST coughing raw)
        (Resource.PineNeedles, "Coughing", "Chew pine needles for throat", 0.35),
        (Resource.PineNeedles, "Nauseous", "Chew pine needles to settle stomach", 0.1),

        // RoseHip - weak pain raw (main value is Nourished buff from tea)
        (Resource.RoseHip, "Pain", "Eat rose hips for mild relief", 0.15),
    ];

    private record TreatmentOption(
        string Label,
        string EffectKind,
        double EffectReduction,
        string? SuccessMessage,
        string? GrantsEffect,
        // Source tracking
        Resource? DirectResource,
        Gear? CraftedGear
    )
    {
        public bool IsCrafted => CraftedGear != null;
    }

    public static bool CanApplyTreatment(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        // Check direct treatments
        foreach (var (resource, effectKind, _, _) in DirectTreatments)
        {
            if (!effects.Any(e => e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (inv.Count(resource) > 0)
                return true;
        }

        // Check crafted treatments
        var craftedTreatments = inv.Tools
            .Where(t => t.ToolType == ToolType.Treatment && !string.IsNullOrEmpty(t.TreatsEffect))
            .ToList();

        foreach (var gear in craftedTreatments)
        {
            if (effects.Any(e => e.EffectKind.Equals(gear.TreatsEffect, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Check crafted treatments that grant buffs (can always be used if player has them)
        var buffTreatments = inv.Tools
            .Where(t => t.ToolType == ToolType.Treatment && !string.IsNullOrEmpty(t.GrantsEffect))
            .ToList();

        if (buffTreatments.Count > 0)
            return true;

        return false;
    }

    // Keep legacy name for backwards compatibility
    public static bool CanApplyDirectTreatment(GameContext ctx) => CanApplyTreatment(ctx);

    public static void ApplyTreatment(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        // Build unified list of available treatments
        var available = new List<TreatmentOption>();

        // Add direct treatments
        foreach (var (resource, effectKind, description, reduction) in DirectTreatments)
        {
            if (!effects.Any(e => e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (inv.Count(resource) <= 0)
                continue;

            string resourceName = GetResourceDisplayName(resource);
            available.Add(new TreatmentOption(
                Label: $"[Raw] {description} ({inv.Count(resource)} available)",
                EffectKind: effectKind,
                EffectReduction: reduction,
                SuccessMessage: $"You apply the {resourceName}.",
                GrantsEffect: null,
                DirectResource: resource,
                CraftedGear: null
            ));
        }

        // Add crafted treatments that treat effects
        var craftedTreatments = inv.Tools
            .Where(t => t.ToolType == ToolType.Treatment && !string.IsNullOrEmpty(t.TreatsEffect))
            .ToList();

        foreach (var gear in craftedTreatments)
        {
            if (!effects.Any(e => e.EffectKind.Equals(gear.TreatsEffect, StringComparison.OrdinalIgnoreCase)))
                continue;

            available.Add(new TreatmentOption(
                Label: $"[Crafted] {gear.Name}",
                EffectKind: gear.TreatsEffect!,
                EffectReduction: gear.EffectReduction,
                SuccessMessage: gear.TreatmentDescription,
                GrantsEffect: gear.GrantsEffect,
                DirectResource: null,
                CraftedGear: gear
            ));
        }

        // Add crafted treatments that only grant buffs (can always be used)
        var buffOnlyTreatments = inv.Tools
            .Where(t => t.ToolType == ToolType.Treatment
                && string.IsNullOrEmpty(t.TreatsEffect)
                && !string.IsNullOrEmpty(t.GrantsEffect))
            .ToList();

        foreach (var gear in buffOnlyTreatments)
        {
            available.Add(new TreatmentOption(
                Label: $"[Crafted] {gear.Name} (buff)",
                EffectKind: "",
                EffectReduction: 0,
                SuccessMessage: gear.TreatmentDescription,
                GrantsEffect: gear.GrantsEffect,
                DirectResource: null,
                CraftedGear: gear
            ));
        }

        if (available.Count == 0)
        {
            GameDisplay.AddNarrative(ctx, "You don't have the right materials to treat your conditions.");
            GameDisplay.Render(ctx, statusText: "Thinking.");
            return;
        }

        // Show current conditions
        var allTreatableEffects = DirectTreatments.Select(t => t.EffectKind)
            .Concat(craftedTreatments.Where(g => g.TreatsEffect != null).Select(g => g.TreatsEffect!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var treatable = effects.Where(e => allTreatableEffects.Contains(e.EffectKind)).ToList();

        if (treatable.Count > 0)
        {
            GameDisplay.AddNarrative(ctx, "Current conditions:");
            foreach (var effect in treatable)
            {
                string severity = effect.Severity switch
                {
                    < 0.33 => "mild",
                    < 0.66 => "moderate",
                    _ => "severe"
                };
                GameDisplay.AddNarrative(ctx, $"  {effect.EffectKind} ({severity})");
            }
        }

        // Build choice menu
        var choice = new Choice<TreatmentOption?>("Apply treatment:");

        foreach (var t in available)
        {
            choice.AddOption(t.Label, t);
        }
        choice.AddOption("Cancel", null);

        GameDisplay.Render(ctx, statusText: "Deciding.");
        var selected = choice.GetPlayerChoice(ctx);

        if (selected == null)
            return;

        // Consume the treatment
        if (selected.IsCrafted)
        {
            var gear = selected.CraftedGear!;
            gear.Use();
            if (gear.Durability <= 0)
                inv.Tools.Remove(gear);
        }
        else
        {
            inv.Pop(selected.DirectResource!.Value);
        }

        // Apply effect reduction if treating an effect
        if (!string.IsNullOrEmpty(selected.EffectKind))
        {
            var targetEffect = effects.FirstOrDefault(e =>
                e.EffectKind.Equals(selected.EffectKind, StringComparison.OrdinalIgnoreCase));

            if (targetEffect != null)
            {
                double oldSeverity = targetEffect.Severity;
                targetEffect.Severity = Math.Max(0, targetEffect.Severity - selected.EffectReduction);

                // Time cost for treatment
                ctx.Update(5, ActivityType.TendingFire);

                // Show success message
                if (!string.IsNullOrEmpty(selected.SuccessMessage))
                {
                    if (targetEffect.Severity <= 0)
                    {
                        ctx.player.AddLog(ctx.player.EffectRegistry.RemoveEffect(targetEffect));
                        GameDisplay.AddSuccess(ctx, $"{selected.SuccessMessage} The {selected.EffectKind.ToLower()} has stopped.");
                    }
                    else if (targetEffect.Severity < oldSeverity * 0.5)
                    {
                        GameDisplay.AddSuccess(ctx, $"{selected.SuccessMessage} The {selected.EffectKind.ToLower()} is much better.");
                    }
                    else
                    {
                        GameDisplay.AddNarrative(ctx, $"{selected.SuccessMessage} The {selected.EffectKind.ToLower()} is slightly better.");
                    }
                }
            }

            // Apply secondary effect reduction (crafted treatments only)
            if (selected.IsCrafted && !string.IsNullOrEmpty(selected.CraftedGear!.SecondaryTreatsEffect))
            {
                var secondaryEffect = effects.FirstOrDefault(e =>
                    e.EffectKind.Equals(selected.CraftedGear.SecondaryTreatsEffect, StringComparison.OrdinalIgnoreCase));

                if (secondaryEffect != null)
                {
                    double oldSecondary = secondaryEffect.Severity;
                    secondaryEffect.Severity = Math.Max(0, secondaryEffect.Severity - selected.CraftedGear.SecondaryEffectReduction);

                    if (secondaryEffect.Severity <= 0)
                    {
                        ctx.player.AddLog(ctx.player.EffectRegistry.RemoveEffect(secondaryEffect));
                        GameDisplay.AddNarrative(ctx, $"The {selected.CraftedGear.SecondaryTreatsEffect.ToLower()} has also stopped.");
                    }
                    else if (secondaryEffect.Severity < oldSecondary)
                    {
                        GameDisplay.AddNarrative(ctx, $"The {selected.CraftedGear.SecondaryTreatsEffect.ToLower()} has also improved.");
                    }
                }
            }
        }
        else
        {
            // Buff-only treatment - just time cost
            ctx.Update(5, ActivityType.TendingFire);
            if (!string.IsNullOrEmpty(selected.SuccessMessage))
            {
                GameDisplay.AddSuccess(ctx, selected.SuccessMessage);
            }
        }

        // Grant buff effect if applicable
        if (!string.IsNullOrEmpty(selected.GrantsEffect))
        {
            var buffEffect = EffectFactory.Create(selected.GrantsEffect);
            if (buffEffect != null)
            {
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(buffEffect));
                GameDisplay.AddSuccess(ctx, $"You feel {selected.GrantsEffect.ToLower()}.");
            }
        }

        GameDisplay.Render(ctx, statusText: "Treating.");
    }

    // Keep legacy name for backwards compatibility
    public static void ApplyDirectTreatment(GameContext ctx) => ApplyTreatment(ctx);

    private static string GetResourceDisplayName(Resource resource) => resource switch
    {
        Resource.Amadou => "amadou",
        Resource.SphagnumMoss => "sphagnum moss",
        Resource.PineResin => "pine resin",
        Resource.Usnea => "usnea lichen",
        Resource.BirchPolypore => "birch polypore",
        Resource.JuniperBerry => "juniper berries",
        Resource.WillowBark => "willow bark",
        _ => resource.ToString().ToLower()
    };
}
