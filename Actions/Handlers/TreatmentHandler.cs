using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles treatment actions - both direct (raw resources) and crafted (teas, poultices).
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class TreatmentHandler
{
    /// <summary>
    /// Direct treatments: apply resources directly to wounds/conditions.
    /// Crafted treatments (teas/poultices) are handled via Gear.TreatsEffect property.
    /// </summary>
    private static readonly List<(Resource Resource, string EffectKind, string Description, double EffectReduction)> DirectTreatments =
    [
        // Bleeding treatments
        (Resource.Amadou, "Bleeding", "Press amadou into wound to stop bleeding", 0.5),
        (Resource.SphagnumMoss, "Bleeding", "Pack sphagnum moss as absorbent dressing", 0.4),
        (Resource.PineResin, "Bleeding", "Seal wound with pine resin", 0.3),
        // Fever treatments
        (Resource.Usnea, "Fever", "Apply usnea as antimicrobial dressing", 0.25),
        (Resource.BirchPolypore, "Fever", "Use birch polypore for infection", 0.2),
        // Wound infection prevention (Inflamed = precursor to Fever)
        (Resource.Usnea, "Inflamed", "Pack wound with usnea to fight infection", 0.5),
        (Resource.SphagnumMoss, "Inflamed", "Apply sphagnum moss antiseptic dressing", 0.4),
        (Resource.PineResin, "Inflamed", "Seal wound with antiseptic resin", 0.3),
        // Gut sickness treatment
        (Resource.JuniperBerry, "Nauseous", "Chew juniper berries to settle stomach", 0.4),
        // Pain treatment
        (Resource.WillowBark, "Pain", "Chew willow bark for pain relief", 0.35),
    ];

    /// <summary>
    /// Abstract representation of any treatment option (direct or crafted).
    /// </summary>
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
