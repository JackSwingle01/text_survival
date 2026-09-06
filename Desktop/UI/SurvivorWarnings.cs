using text_survival.Actions;

namespace text_survival.Desktop.UI;

public static class SurvivorWarnings
{
    public static IReadOnlyList<string> Build(GameContext ctx)
    {
        var warnings = new List<string>();
        var body = ctx.player.Body;
        if (ctx.player.EffectRegistry.GetAll().Any(e => e.EffectKind == "Bleeding")) warnings.Add("Bleeding");
        if (body.BodyTemperature < 95) warnings.Add("Hypothermia");
        else if (body.BodyTemperature < 97) warnings.Add("Getting cold");
        if (body.HydratedPct < .2) warnings.Add("Dehydrated");
        if (body.FullPct < .1) warnings.Add("Starving");
        if (body.EnergyPct < .15) warnings.Add("Exhausted");
        if (ctx.Inventory.CurrentWeightKg > ctx.Inventory.MaxWeightKg) warnings.Add("Overloaded");
        return warnings;
    }
}
