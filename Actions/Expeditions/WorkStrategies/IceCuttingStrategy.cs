using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for cutting ice holes in frozen water.
/// Creates access for fishing and water collection.
/// Time required scales with ice thickness.
/// </summary>
public class IceCuttingStrategy : IWorkStrategy
{
    public Task<string?> ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<WaterFeature>();
        if (feature == null)
            return Task.FromResult<string?>("There's no water here.");
        if (!feature.CanCutIceHole())
            return Task.FromResult<string?>(feature.HasIceHole
                ? "There's already an ice hole here."
                : "The ice cannot be cut.");
        return Task.FromResult<string?>(null);
    }

    public Task<Choice<int>?> GetTimeOptions(GameContext ctx, Location location)
    {
        // Fixed time based on ice thickness - no choices offered
        return Task.FromResult<Choice<int>?>(null);
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

    public async Task<WorkResult> Execute(GameContext ctx, Location location, int actualTime)
    {
        var feature = location.GetFeature<WaterFeature>()!;
        feature.CutIceHole();

        string resultMessage = feature.IceThicknessLevel < 0.4
            ? "You break through the thin ice. Water is now accessible."
            : "You cut through the ice. Water is now accessible.";

        await ctx.Ui.ShowWorkResult(new WorkResultView("Ice Cutting", resultMessage, []));

        return new WorkResult([], null, actualTime, false);
    }
}
