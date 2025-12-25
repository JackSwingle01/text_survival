using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles eating and drinking actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class ConsumptionHandler
{
    // Calorie values per kg
    private const double CookedMeatCaloriesPerKg = 2500;
    private const double RawMeatCaloriesPerKg = 1500;
    private const double BerriesCaloriesPerKg = 500;

    // Hydration values
    private const double BerriesHydrationPerKg = 200;
    private const double WaterHydrationPerLiter = 1000;

    // Hyperthermia relief from drinking
    private const double HyperthermiaCooldownPerQuarterLiter = 0.15;

    public static void EatDrink(GameContext ctx)
    {
        var inv = ctx.Inventory;
        var body = ctx.player.Body;

        while (true)
        {
            int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
            int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
            GameDisplay.AddNarrative(ctx, $"Food: {caloriesPercent}% | Water: {hydrationPercent}%");
            GameDisplay.Render(ctx, statusText: "Eating.");

            var options = new List<string>();
            var consumeActions = new Dictionary<string, Action>();

            // Add food options
            if (inv.Count(Resource.CookedMeat) > 0)
            {
                double w = inv.Peek(Resource.CookedMeat);
                int calories = (int)(w * CookedMeatCaloriesPerKg);
                string opt = $"Cooked meat ({w:F1}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.CookedMeat);
                    int cal = (int)(eaten * CookedMeatCaloriesPerKg);
                    body.AddCalories(cal);
                    GameDisplay.AddSuccess(ctx, $"You eat the cooked meat. (+{cal} cal)");
                };
            }

            if (inv.Count(Resource.RawMeat) > 0)
            {
                double w = inv.Peek(Resource.RawMeat);
                int calories = (int)(w * RawMeatCaloriesPerKg);
                string opt = $"Raw meat ({w:F1}kg) - ~{calories} cal [risk of illness]";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.RawMeat);
                    int cal = (int)(eaten * RawMeatCaloriesPerKg);
                    body.AddCalories(cal);
                    GameDisplay.AddWarning(ctx, $"You eat the raw meat. (+{cal} cal)");
                    // TODO: Add chance of food poisoning
                };
            }

            if (inv.Count(Resource.Berries) > 0)
            {
                double w = inv.Peek(Resource.Berries);
                int calories = (int)(w * BerriesCaloriesPerKg);
                string opt = $"Berries ({w:F2}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.Berries);
                    int cal = (int)(eaten * BerriesCaloriesPerKg);
                    body.AddCalories(cal);
                    body.AddHydration(eaten * BerriesHydrationPerKg);
                    GameDisplay.AddSuccess(ctx, $"You eat the berries. (+{cal} cal)");
                };
            }

            if (inv.HasWater)
            {
                // Drink up to 1L, but don't waste water over hydration max
                double hydrationRoom = (SurvivalProcessor.MAX_HYDRATION - body.Hydration) / WaterHydrationPerLiter;
                double toDrink = Math.Min(1.0, Math.Min(inv.WaterLiters, hydrationRoom));
                toDrink = Math.Round(toDrink, 2);

                if (toDrink >= 0.01)
                {
                    string opt = $"Drink water ({toDrink:F2}L)";
                    options.Add(opt);
                    consumeActions[opt] = () =>
                    {
                        inv.WaterLiters -= toDrink;
                        body.AddHydration(toDrink * WaterHydrationPerLiter);

                        // Drinking water helps cool down when overheating
                        var hyperthermia = ctx.player.EffectRegistry.GetEffectsByKind("Hyperthermia").FirstOrDefault();
                        if (hyperthermia != null)
                        {
                            double cooldown = HyperthermiaCooldownPerQuarterLiter * (toDrink / 0.25);
                            hyperthermia.Severity = Math.Max(0, hyperthermia.Severity - cooldown);
                            GameDisplay.AddSuccess(ctx, "You drink some water. The cool water helps you cool down.");
                        }
                        else
                        {
                            GameDisplay.AddSuccess(ctx, "You drink some water.");
                        }
                    };
                }
            }

            options.Add("Done");

            if (options.Count == 1)
            {
                GameDisplay.AddNarrative(ctx, "You have nothing to eat or drink.");
                GameDisplay.Render(ctx);
                break;
            }

            string choice = Input.Select(ctx, "What would you like to consume?", options);

            if (choice == "Done")
                break;

            consumeActions[choice]();
            ctx.Update(5, ActivityType.Eating, render: true);
        }
    }
}
