using text_survival.Effects;
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
    private const double HoneyCaloriesPerKg = 3000;
    private const double NutsCaloriesPerKg = 6000;
    private const double RootsCaloriesPerKg = 400;
    private const double DriedMeatCaloriesPerKg = 3000;
    private const double DriedBerriesCaloriesPerKg = 2500;

    // Hydration values
    private const double BerriesHydrationPerKg = 200;
    private const double RootsHydrationPerKg = 100;
    private const double DriedMeatHydrationPerKg = -50;  // Needs water to digest
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

            if (inv.Count(Resource.Honey) > 0)
            {
                double w = inv.Peek(Resource.Honey);
                int calories = (int)(w * HoneyCaloriesPerKg);
                string opt = $"Honey ({w:F2}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.Honey);
                    int cal = (int)(eaten * HoneyCaloriesPerKg);
                    body.AddCalories(cal);

                    // Honey gives a quick energy boost - severity scales with amount eaten
                    double energySeverity = Math.Min(0.5, eaten / 0.25 * 0.3);
                    ctx.player.EffectRegistry.AddEffect(EffectFactory.Energized(energySeverity));

                    GameDisplay.AddSuccess(ctx, $"You eat the honey. Sweet energy. (+{cal} cal)");
                };
            }

            if (inv.Count(Resource.Nuts) > 0)
            {
                double w = inv.Peek(Resource.Nuts);
                int calories = (int)(w * NutsCaloriesPerKg);
                string opt = $"Nuts ({w:F2}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.Nuts);
                    int cal = (int)(eaten * NutsCaloriesPerKg);
                    body.AddCalories(cal);
                    GameDisplay.AddSuccess(ctx, $"You eat the nuts. Dense calories. (+{cal} cal)");
                };
            }

            if (inv.Count(Resource.Roots) > 0)
            {
                double w = inv.Peek(Resource.Roots);
                int calories = (int)(w * RootsCaloriesPerKg);
                string opt = $"Roots ({w:F2}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.Roots);
                    int cal = (int)(eaten * RootsCaloriesPerKg);
                    body.AddCalories(cal);
                    body.AddHydration(eaten * RootsHydrationPerKg);
                    GameDisplay.AddSuccess(ctx, $"You eat the roots. Starchy and filling. (+{cal} cal)");
                };
            }

            if (inv.Count(Resource.DriedMeat) > 0)
            {
                double w = inv.Peek(Resource.DriedMeat);
                int calories = (int)(w * DriedMeatCaloriesPerKg);
                string opt = $"Dried meat ({w:F2}kg) - ~{calories} cal [makes you thirsty]";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.DriedMeat);
                    int cal = (int)(eaten * DriedMeatCaloriesPerKg);
                    body.AddCalories(cal);
                    body.AddHydration(eaten * DriedMeatHydrationPerKg);
                    GameDisplay.AddSuccess(ctx, $"You eat the dried meat. Salty and chewy. (+{cal} cal)");
                };
            }

            if (inv.Count(Resource.DriedBerries) > 0)
            {
                double w = inv.Peek(Resource.DriedBerries);
                int calories = (int)(w * DriedBerriesCaloriesPerKg);
                string opt = $"Dried berries ({w:F2}kg) - ~{calories} cal";
                options.Add(opt);
                consumeActions[opt] = () =>
                {
                    double eaten = inv.Pop(Resource.DriedBerries);
                    int cal = (int)(eaten * DriedBerriesCaloriesPerKg);
                    body.AddCalories(cal);
                    GameDisplay.AddSuccess(ctx, $"You eat the dried berries. Sweet and tangy. (+{cal} cal)");
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

                // Wash off blood if player has Bloody effect
                if (ctx.player.EffectRegistry.HasEffect("Bloody"))
                {
                    double waterNeeded = 0.5;
                    double toUse = Math.Min(waterNeeded, inv.WaterLiters);
                    string washOpt = $"Wash off blood ({toUse:F2}L)";
                    options.Add(washOpt);
                    consumeActions[washOpt] = () =>
                    {
                        inv.WaterLiters -= toUse;
                        ctx.player.EffectRegistry.RemoveEffectsByKind("Bloody");

                        // Washing adds wetness (+5% = 0.05 severity)
                        ctx.player.EffectRegistry.AddEffect(EffectFactory.Wet(0.05));

                        GameDisplay.AddSuccess(ctx, "You wash the blood from your hands and clothes. You're a bit damp now.");
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
            GameDisplay.UpdateAndRenderProgress(ctx, "Eating.", 5, ActivityType.Eating);
        }
    }
}
