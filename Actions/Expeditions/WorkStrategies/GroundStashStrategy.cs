using text_survival.Environments;
using text_survival.Environments.Features;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for picking up / dropping items on the ground.
/// Instant access via transfer UI. Removes feature when empty.
/// </summary>
public class GroundStashStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var stash = location.GetFeature<GroundItemsFeature>();
        if (stash == null || !stash.HasItems)
            return "There's nothing on the ground here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location) => null;

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        return (0, []);
    }

    public ActivityType GetActivityType() => ActivityType.Idle;

    public string GetActivityName() => "ground_stash";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var stash = location.GetFeature<GroundItemsFeature>()!;

        Desktop.DesktopIO.RunTransferUI(ctx, stash.Storage, "DROPPED ITEMS");

        location.CleanupGroundItems();

        return WorkResult.Empty(0);
    }
}
