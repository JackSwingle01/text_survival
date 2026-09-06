using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Move the snow off something the weather has covered - one target's own footprint, not the
/// tile. Nothing is unlocked permanently: the work is measured against the cover actually
/// there, so fresh snow can bury it again and a thaw can save you the trouble. Knowledge is
/// never affected; once found, a thing stays found.
/// </summary>
public class DigUpStrategy(string placementId) : IWorkStrategy
{
    private readonly string _placementId = placementId;

    /// <summary>Longest single session; a deep drift takes more than one.</summary>
    private const int MaxSessionMinutes = 45;


    public Task<string?> ValidateLocation(GameContext ctx, Location location)
    {
        var target = Find(location);
        if (target == null)
            return Task.FromResult<string?>("There's nothing here to dig out.");
        if (!location.IsCovered(target))
            return Task.FromResult<string?>($"{target.AccessName} is already clear.");
        return Task.FromResult<string?>(null);
    }

    public Task<Choice<int>?> GetTimeOptions(GameContext ctx, Location location)
    {
        var target = Find(location);
        if (target == null) return Task.FromResult<Choice<int>?>(null);

        double toolFactor = ShovelFactor(ctx, out string toolNote);
        double remaining = location.Surface.GetExcavationMinutes(target.Placement) / toolFactor;

        int session = Math.Clamp((int)Math.Ceiling(remaining), 1, MaxSessionMinutes);
        bool finishes = remaining <= MaxSessionMinutes;

        var choice = new Choice<int>(
            $"Dig out {target.AccessName}? {DepthNote(location, target)}{toolNote}");
        choice.AddOption(finishes ? $"Dig ({session} min)" : $"Dig for {session} min (won't finish)", session);
        choice.AddOption("Leave it", 0);
        return Task.FromResult<Choice<int>?>(choice);
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry);

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Crafting;

    public string GetActivityName() => "digging";

    // You are standing on the thing you are looking for.
    public bool AllowedInDarkness => true;

    public Task<WorkResult> Execute(GameContext ctx, Location location, int actualTime)
    {
        var target = Find(location);
        if (target == null || !location.IsCovered(target))
            return Task.FromResult(WorkResult.Empty(actualTime));

        // Re-checked rather than trusted from the prompt: a tool can break during the work.
        double toolFactor = ShovelFactor(ctx, out _);
        location.Surface.ApplyExcavation(target.Placement, actualTime, toolFactor);

        GameDisplay.AddNarrative(ctx, location.IsCovered(target)
            ? $"You keep digging. {target.AccessName} is still under the snow."
            : $"You clear the snow away. {target.AccessName} is reachable again.");

        return Task.FromResult(WorkResult.Empty(actualTime));
    }

    /// <summary>A working shovel doubles the digging. A broken one is a stick.</summary>
    private static double ShovelFactor(GameContext ctx, out string note)
    {
        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        if (shovel?.Works == true)
        {
            note = " Your shovel will halve the work.";
            return 2.0;
        }

        note = "";
        return 1.0;
    }

    private static string DepthNote(Location location, LocationFeature target)
    {
        int cm = (int)Math.Round(location.Surface.GetBlockingCoverM(target.Placement) * 100);
        return $"{cm} cm of snow over it.";
    }

    private LocationFeature? Find(Location location) =>
        location.Features.FirstOrDefault(f => f.PlacementId == _placementId);
}
