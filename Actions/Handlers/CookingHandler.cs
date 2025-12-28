using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles cooking and melting actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class CookingHandler
{
    // Cooking times in minutes
    private const int CookMeatTimeMinutes = 15;
    private const int MeltSnowTimeMinutes = 10;

    // Yields
    private const double MeltSnowWaterLiters = 0.5;

    public static void CookMelt(GameContext ctx)
    {
        var inv = ctx.Inventory;

        while (true)
        {
            GameDisplay.Render(ctx, statusText: "Cooking.");

            var options = new List<string>();
            var actions = new Dictionary<string, Action>();

            // Cook raw meat
            if (inv.Count(Resource.RawMeat) > 0)
            {
                double w = inv.Peek(Resource.RawMeat);
                string opt = $"Cook raw meat ({w:F1}kg) - {CookMeatTimeMinutes} min";
                options.Add(opt);
                actions[opt] = () =>
                {
                    // ActivityType.Cooking has EventMultiplier=0, so no events can interrupt
                    GameDisplay.UpdateAndRenderProgress(ctx, "Cooking meat...", CookMeatTimeMinutes, ActivityType.Cooking);
                    double cooked = inv.Pop(Resource.RawMeat);
                    inv.Add(Resource.CookedMeat, cooked);
                    GameDisplay.AddSuccess(ctx, $"Cooked {cooked:F1}kg of meat.");
                };
            }

            // Melt snow (always available in Ice Age)
            string snowOpt = $"Melt snow for water - {MeltSnowTimeMinutes} min";
            options.Add(snowOpt);
            actions[snowOpt] = () =>
            {
                // ActivityType.Cooking has EventMultiplier=0, so no events can interrupt
                GameDisplay.UpdateAndRenderProgress(ctx, "Melting snow...", MeltSnowTimeMinutes, ActivityType.Cooking);
                inv.WaterLiters += MeltSnowWaterLiters;
                GameDisplay.AddSuccess(ctx, $"Melted snow into {MeltSnowWaterLiters}L of water.");
            };

            options.Add("Done");

            string choice = Input.Select(ctx, "What would you like to do?", options);

            if (choice == "Done")
                break;

            actions[choice]();
            GameDisplay.Render(ctx, statusText: "Cooking.");
        }
    }
}
