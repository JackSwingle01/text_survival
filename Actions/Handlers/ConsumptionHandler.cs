using text_survival.Actors;
using text_survival.Effects;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Pure game logic for eating and drinking.
/// No UI code - just data and state mutations.
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

    /// <summary>
    /// Describes a consumable item available in inventory.
    /// </summary>
    public record ConsumableInfo(
        string Id,           // e.g., "CookedMeat", "water", "wash_blood"
        string Name,
        double Amount,       // weight in kg or liters
        int? Calories,
        int? Hydration,
        string? Warning
    );

    /// <summary>
    /// Result of consuming an item.
    /// </summary>
    public record ConsumptionResult(string Message, bool IsWarning);

    /// <summary>
    /// Returns all available consumables based on current inventory state.
    /// </summary>
    public static List<ConsumableInfo> GetAvailableConsumables(GameContext ctx)
    {
        var inv = ctx.Inventory;
        var body = ctx.player.Body;
        var result = new List<ConsumableInfo>();

        // Food items
        AddIfAvailable(result, inv, Resource.CookedMeat, "Cooked meat", CookedMeatCaloriesPerKg, 0, null);
        AddIfAvailable(result, inv, Resource.RawMeat, "Raw meat", RawMeatCaloriesPerKg, 0, "[risk of illness]");
        AddIfAvailable(result, inv, Resource.Berries, "Berries", BerriesCaloriesPerKg, BerriesHydrationPerKg, null);
        AddIfAvailable(result, inv, Resource.Honey, "Honey", HoneyCaloriesPerKg, 0, null);
        AddIfAvailable(result, inv, Resource.Nuts, "Nuts", NutsCaloriesPerKg, 0, null);
        AddIfAvailable(result, inv, Resource.Roots, "Roots", RootsCaloriesPerKg, RootsHydrationPerKg, null);
        AddIfAvailable(result, inv, Resource.DriedMeat, "Dried meat", DriedMeatCaloriesPerKg, DriedMeatHydrationPerKg, "[makes you thirsty]");
        AddIfAvailable(result, inv, Resource.DriedBerries, "Dried berries", DriedBerriesCaloriesPerKg, 0, null);

        // Water (drink up to 1L, capped by hydration room)
        if (inv.HasWater)
        {
            double hydrationRoom = (SurvivalProcessor.MAX_HYDRATION - body.Hydration) / WaterHydrationPerLiter;
            double toDrink = Math.Min(1.0, Math.Min(inv.WaterLiters, hydrationRoom));
            toDrink = Math.Round(toDrink, 2);

            if (toDrink >= 0.01)
            {
                result.Add(new ConsumableInfo(
                    "water",
                    "Water",
                    toDrink,
                    null,
                    (int)(toDrink * WaterHydrationPerLiter),
                    null
                ));
            }
        }

        // Special action: wash off blood
        if (ctx.player.EffectRegistry.HasEffect("Bloody") && inv.WaterLiters >= 0.5)
        {
            double toUse = Math.Min(0.5, inv.WaterLiters);
            result.Add(new ConsumableInfo(
                "wash_blood",
                "Wash off blood",
                toUse,
                null,
                null,
                null
            ));
        }

        return result;
    }

    private static void AddIfAvailable(
        List<ConsumableInfo> result,
        Inventory inv,
        Resource resource,
        string name,
        double caloriesPerKg,
        double hydrationPerKg,
        string? warning)
    {
        if (inv.Count(resource) > 0)
        {
            double weight = inv.Peek(resource);
            int calories = (int)(weight * caloriesPerKg);
            int? hydration = hydrationPerKg != 0 ? (int)(weight * hydrationPerKg) : null;

            result.Add(new ConsumableInfo(
                resource.ToString(),
                name,
                weight,
                calories,
                hydration,
                warning
            ));
        }
    }

    /// <summary>
    /// Executes consumption of the specified item.
    /// Removes from inventory and applies effects.
    /// </summary>
    public static ConsumptionResult Consume(GameContext ctx, string consumableId)
    {
        var inv = ctx.Inventory;
        var body = ctx.player.Body;

        // Water
        if (consumableId == "water")
        {
            double hydrationRoom = (SurvivalProcessor.MAX_HYDRATION - body.Hydration) / WaterHydrationPerLiter;
            double toDrink = Math.Min(1.0, Math.Min(inv.WaterLiters, hydrationRoom));
            toDrink = Math.Round(toDrink, 2);

            inv.WaterLiters -= toDrink;
            body.AddHydration(toDrink * WaterHydrationPerLiter);

            // Drinking water helps cool down when overheating
            var hyperthermia = ctx.player.EffectRegistry.GetEffectsByKind("Hyperthermia").FirstOrDefault();
            if (hyperthermia != null)
            {
                double cooldown = HyperthermiaCooldownPerQuarterLiter * (toDrink / 0.25);
                hyperthermia.Severity = Math.Max(0, hyperthermia.Severity - cooldown);
                return new ConsumptionResult("You drink some water. The cool water helps you cool down.", false);
            }
            return new ConsumptionResult("You drink some water.", false);
        }

        // Wash blood
        if (consumableId == "wash_blood")
        {
            double toUse = Math.Min(0.5, inv.WaterLiters);
            inv.WaterLiters -= toUse;
            ctx.player.EffectRegistry.RemoveEffectsByKind("Bloody");
            ctx.player.EffectRegistry.AddEffect(EffectFactory.Wet(0.05));
            return new ConsumptionResult("You wash the blood from your hands and clothes. You're a bit damp now.", false);
        }

        // Food items
        if (!Enum.TryParse<Resource>(consumableId, out var resource))
            return new ConsumptionResult($"Unknown consumable: {consumableId}", true);

        double eaten = inv.Pop(resource);

        // Record food discovery
        ctx.RecordFoodEaten(resource.ToString());

        return resource switch
        {
            Resource.CookedMeat => Eat(body, eaten, CookedMeatCaloriesPerKg, 0, "You eat the cooked meat."),
            Resource.RawMeat => Eat(body, eaten, RawMeatCaloriesPerKg, 0, "You eat the raw meat.", isWarning: true),
            // TODO: Add chance of food poisoning for raw meat
            Resource.Berries => Eat(body, eaten, BerriesCaloriesPerKg, BerriesHydrationPerKg, "You eat the berries."),
            Resource.Honey => EatHoney(ctx, body, eaten),
            Resource.Nuts => Eat(body, eaten, NutsCaloriesPerKg, 0, "You eat the nuts. Dense calories."),
            Resource.Roots => Eat(body, eaten, RootsCaloriesPerKg, RootsHydrationPerKg, "You eat the roots. Starchy and filling."),
            Resource.DriedMeat => Eat(body, eaten, DriedMeatCaloriesPerKg, DriedMeatHydrationPerKg, "You eat the dried meat. Salty and chewy."),
            Resource.DriedBerries => Eat(body, eaten, DriedBerriesCaloriesPerKg, 0, "You eat the dried berries. Sweet and tangy."),
            _ => new ConsumptionResult($"Cannot eat {resource}", true)
        };
    }

    private static ConsumptionResult Eat(
        Bodies.Body body,
        double amount,
        double caloriesPerKg,
        double hydrationPerKg,
        string message,
        bool isWarning = false)
    {
        int cal = (int)(amount * caloriesPerKg);
        body.AddCalories(cal);

        if (hydrationPerKg != 0)
            body.AddHydration(amount * hydrationPerKg);

        return new ConsumptionResult($"{message} (+{cal} cal)", isWarning);
    }

    private static ConsumptionResult EatHoney(GameContext ctx, Bodies.Body body, double eaten)
    {
        int cal = (int)(eaten * HoneyCaloriesPerKg);
        body.AddCalories(cal);

        // Honey gives a quick energy boost - severity scales with amount eaten
        double energySeverity = Math.Min(0.5, eaten / 0.25 * 0.3);
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Energized(energySeverity));

        return new ConsumptionResult($"You eat the honey. Sweet energy. (+{cal} cal)", false);
    }

    /// <summary>
    /// NPC consumption - applies calories/hydration/effects to an Actor.
    /// </summary>
    public static void EatDrink(Actor actor, Resource food, double amount)
    {
        var body = actor.Body;

        switch (food)
        {
            case Resource.CookedMeat:
                body.AddCalories((int)(amount * CookedMeatCaloriesPerKg));
                break;
            case Resource.RawMeat:
                body.AddCalories((int)(amount * RawMeatCaloriesPerKg));
                // TODO: Add chance of food poisoning for raw meat
                break;
            case Resource.Berries:
                body.AddCalories((int)(amount * BerriesCaloriesPerKg));
                body.AddHydration(amount * BerriesHydrationPerKg);
                break;
            case Resource.Honey:
                body.AddCalories((int)(amount * HoneyCaloriesPerKg));
                double energySeverity = Math.Min(0.5, amount / 0.25 * 0.3);
                actor.EffectRegistry.AddEffect(EffectFactory.Energized(energySeverity));
                break;
            case Resource.Nuts:
                body.AddCalories((int)(amount * NutsCaloriesPerKg));
                break;
            case Resource.Roots:
                body.AddCalories((int)(amount * RootsCaloriesPerKg));
                body.AddHydration(amount * RootsHydrationPerKg);
                break;
            case Resource.DriedMeat:
                body.AddCalories((int)(amount * DriedMeatCaloriesPerKg));
                body.AddHydration(amount * DriedMeatHydrationPerKg);
                break;
            case Resource.DriedBerries:
                body.AddCalories((int)(amount * DriedBerriesCaloriesPerKg));
                break;
        }
    }
}
