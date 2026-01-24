using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for cutting ice holes in frozen water.
/// Creates access for fishing and water collection.
/// Time required scales with ice thickness.
/// </summary>
public class IceCuttingStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<WaterFeature>();
        if (feature == null)
            return "There's no water here.";
        if (!feature.CanCutIceHole())
            return feature.HasIceHole
                ? "There's already an ice hole here."
                : "The ice cannot be cut.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Fixed time based on ice thickness - no choices offered
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // Get base time from ice thickness
        var feature = location.GetFeature<WaterFeature>()!;
        int workTime = feature.GetIceCuttingMinutes();

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Heavy work - requires mobility and arm strength
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Chopping;

    public string GetActivityName() => "cutting ice";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var feature = location.GetFeature<WaterFeature>()!;
        feature.CutIceHole();

        string resultMessage = feature.IceThicknessLevel < 0.4
            ? "You break through the thin ice. Water is now accessible."
            : "You cut through the ice. Water is now accessible.";

        DesktopIO.ShowWorkResult(ctx, "Ice Cutting", resultMessage, []);

        return new WorkResult([], null, actualTime, false);
    }
}
