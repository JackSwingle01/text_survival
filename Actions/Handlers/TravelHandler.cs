using text_survival.Bodies;
using text_survival.Effects;
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
    /// Severity scales with terrain hazard level.
    /// </summary>
    public static void ApplyTravelInjury(GameContext ctx, Location location)
    {
        double hazard = location.TerrainHazardLevel;

        // Determine injury type based on hazard level
        if (hazard >= 0.7)
        {
            // Severe terrain - high chance of bad injury
            double severity = Utils.RandDouble(0.5, 0.8);
            ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(EffectFactory.SprainedAnkle(severity)));
            GameDisplay.AddNarrative(ctx, "You lose your footing on the treacherous ground and twist your ankle badly.");
        }
        else if (hazard >= 0.5)
        {
            // Moderate terrain - mix of injuries
            if (Utils.RandDouble(0, 1) < 0.6)
            {
                double severity = Utils.RandDouble(0.3, 0.6);
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(EffectFactory.SprainedAnkle(severity)));
                GameDisplay.AddNarrative(ctx, "You stumble and twist your ankle.");
            }
            else
            {
                // Minor cuts and bruises via body damage
                ctx.player.Body.Damage(new DamageInfo(15, DamageType.Blunt, "fall", "Leg"));
                GameDisplay.AddNarrative(ctx, "You slip and bruise yourself on the rocks.");
            }
        }
        else
        {
            // Lower hazard - mostly minor injuries
            if (Utils.RandDouble(0, 1) < 0.3)
            {
                double severity = Utils.RandDouble(0.2, 0.4);
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(EffectFactory.SprainedAnkle(severity)));
                GameDisplay.AddNarrative(ctx, "You misstep and tweak your ankle.");
            }
            else
            {
                ctx.player.Body.Damage(new DamageInfo(10, DamageType.Blunt, "stumble", "Leg"));
                GameDisplay.AddNarrative(ctx, "You stumble but catch yourself, scraping your leg.");
            }
        }

        Input.WaitForKey(ctx);
    }
}
