using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for fishing at water locations.
/// Requires open water or an ice hole in frozen water.
/// Time invested increases catch probability.
/// Spear provides bonus catch chance.
/// </summary>
public class FishingStrategy : IWorkStrategy
{
    private int _selectedMinutes;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<WaterFeature>();
        if (feature == null)
            return "There's no water here.";
        if (feature.IsFrozen && !feature.HasIceHole)
            return "The water is frozen. You need to cut an ice hole first.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>("How long do you want to fish?");
        choice.AddOption("15 minutes (quick try)", 15);
        choice.AddOption("30 minutes (patient)", 30);
        choice.AddOption("60 minutes (dedicated)", 60);
        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        _selectedMinutes = baseTime;

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Fishing impaired by cold hands (wetness), patience (consciousness)
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "fishing";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Catch probability: base 40% for 15min, +15% per 15min, cap 85%
        double catchChance = Math.Min(0.85, 0.40 + (actualTime / 15 - 1) * 0.15);

        // todo: add effects/tools that modify catch chance
        // Spear bonus: +15%
        var spear = ctx.Inventory.GetTool(ToolType.Spear);
        if (spear?.Works == true)
            catchChance = Math.Min(0.95, catchChance + 0.15);

        var collected = new List<string>();
        var loot = new Inventory();

        // todo flesh this out and add variety of fish types/sizes
        if (Random.Shared.NextDouble() < catchChance)
        {
            // Small fish: 0.3-0.8 kg
            double fishWeight = 0.3 + Random.Shared.NextDouble() * 0.5;
            loot.Add(Resource.RawMeat, fishWeight);
            loot.Add(Resource.Bone, fishWeight * 0.1);
            collected.Add($"Fish ({fishWeight:F1}kg)");

            // Chance for second fish on longer sessions
            if (actualTime >= 30 && Random.Shared.NextDouble() < 0.25)
            {
                fishWeight = 0.3 + Random.Shared.NextDouble() * 0.5;
                loot.Add(Resource.RawMeat, fishWeight);
                loot.Add(Resource.Bone, fishWeight * 0.1);
                collected.Add($"Fish ({fishWeight:F1}kg)");
            }
        }

        if (collected.Count > 0)
        {
            InventoryCapacityHelper.CombineAndReport(ctx, loot);
            DesktopIO.ShowWorkResult(ctx, "Fishing",
                "Your patience pays off.", collected);
        }
        else
        {
            DesktopIO.ShowWorkResult(ctx, "Fishing",
                "The fish aren't biting today.", []);
        }

        return new WorkResult(collected, null, actualTime, false);
    }
}
