using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles direct wound treatment actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class TreatmentHandler
{
    /// <summary>
    /// Direct treatments: apply resources directly to wounds/conditions.
    /// Different from crafted treatments (teas/poultices) which go through crafting.
    /// </summary>
    private static readonly List<(Resource Resource, string EffectKind, string Description, double EffectReduction)> DirectTreatments =
    [
        (Resource.Amadou, "Bleeding", "Press amadou into wound to stop bleeding", 0.5),
        (Resource.SphagnumMoss, "Bleeding", "Pack sphagnum moss as absorbent dressing", 0.4),
        (Resource.PineResin, "Bleeding", "Seal wound with pine resin", 0.3),
        (Resource.Usnea, "Fever", "Apply usnea as antimicrobial dressing", 0.25),
        (Resource.BirchPolypore, "Fever", "Use birch polypore for infection", 0.2),
    ];

    public static bool CanApplyDirectTreatment(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        foreach (var (resource, effectKind, _, _) in DirectTreatments)
        {
            // Check if player has this effect
            if (!effects.Any(e => e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check if player has the resource
            if (inv.Count(resource) > 0)
                return true;
        }
        return false;
    }

    public static void ApplyDirectTreatment(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll();
        var inv = ctx.Inventory;

        // Build list of available treatments
        var available = new List<(Resource Resource, string EffectKind, string Description, double EffectReduction)>();

        foreach (var treatment in DirectTreatments)
        {
            // Check if player has this effect
            if (!effects.Any(e => e.EffectKind.Equals(treatment.EffectKind, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Check if player has the resource
            if (inv.Count(treatment.Resource) > 0)
                available.Add(treatment);
        }

        if (available.Count == 0)
        {
            GameDisplay.AddNarrative(ctx, "You don't have the right materials to treat your wounds.");
            GameDisplay.Render(ctx, statusText: "Thinking.");
            Input.WaitForKey(ctx);
            return;
        }

        // Show current conditions
        var treatable = effects.Where(e => DirectTreatments.Any(t =>
            t.EffectKind.Equals(e.EffectKind, StringComparison.OrdinalIgnoreCase))).ToList();

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

        // Build choice menu
        var choice = new Choice<(Resource Resource, string EffectKind, string Description, double EffectReduction)?>("Apply treatment:");

        foreach (var t in available)
        {
            string label = $"{t.Description} ({inv.Count(t.Resource)} available)";
            choice.AddOption(label, t);
        }
        choice.AddOption("Cancel", null);

        GameDisplay.Render(ctx, statusText: "Deciding.");
        var selected = choice.GetPlayerChoice(ctx);

        if (selected == null)
            return;

        var (resource, effectKind, description, reduction) = selected.Value;

        // Consume resource
        inv.Pop(resource);

        // Find and reduce the effect
        var targetEffect = effects.FirstOrDefault(e =>
            e.EffectKind.Equals(effectKind, StringComparison.OrdinalIgnoreCase));

        if (targetEffect != null)
        {
            double oldSeverity = targetEffect.Severity;
            targetEffect.Severity = Math.Max(0, targetEffect.Severity - reduction);

            // Time cost for treatment
            ctx.Update(5, ActivityType.TendingFire); // Using TendingFire as closest match

            // Messages based on treatment effectiveness
            string resourceName = resource switch
            {
                Resource.Amadou => "amadou",
                Resource.SphagnumMoss => "sphagnum moss",
                Resource.PineResin => "pine resin",
                Resource.Usnea => "usnea lichen",
                Resource.BirchPolypore => "birch polypore",
                _ => resource.ToString().ToLower()
            };

            if (targetEffect.Severity <= 0)
            {
                ctx.player.AddLog(ctx.player.EffectRegistry.RemoveEffect(targetEffect));
                GameDisplay.AddSuccess(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} has stopped.");
            }
            else if (targetEffect.Severity < oldSeverity * 0.5)
            {
                GameDisplay.AddSuccess(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} is much better.");
            }
            else
            {
                GameDisplay.AddNarrative(ctx, $"You apply the {resourceName}. The {effectKind.ToLower()} is slightly better.");
            }
        }

        GameDisplay.Render(ctx, statusText: "Treating.");
        Input.WaitForKey(ctx);
    }
}
