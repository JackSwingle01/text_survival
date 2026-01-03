using text_survival.Actions.Variants;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles travel-related calculations and injury application.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class TravelHandler
{
    /// <summary>
    /// Apply injury from quick travel through hazardous terrain.
    /// Uses variant system for contextual, varied injuries.
    /// </summary>
    public static void ApplyTravelInjury(GameContext ctx, Location location)
    {
        // Select injury variant based on hazard type and severity
        var variant = VariantSelector.SelectTravelInjuryVariant(ctx, location);

        // Apply damage
        ctx.player.Body.Damage(new DamageInfo(
            variant.Amount,
            variant.Type,
            target: variant.Target
        ));

        // Apply any auto-triggered effects (sprains, dazed, etc.)
        if (variant.Effects != null)
        {
            foreach (var effect in variant.Effects)
            {
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
            }
        }

        // Show narrative
        GameDisplay.AddNarrative(ctx, variant.Description);
    }

    /// <summary>
    /// Apply travel injury to any actor (NPC-friendly, no UI).
    /// Returns description of what happened for caller to handle.
    /// </summary>
    public static string ApplyTravelInjury(Actor actor, Location location)
    {
        var variant = VariantSelector.SelectTravelInjuryVariant(location);

        actor.Body.Damage(new DamageInfo(
            variant.Amount,
            variant.Type,
            target: variant.Target
        ));

        if (variant.Effects != null)
        {
            foreach (var effect in variant.Effects)
            {
                actor.AddLog(actor.EffectRegistry.AddEffect(effect));
            }
        }

        return variant.Description;
    }
}
