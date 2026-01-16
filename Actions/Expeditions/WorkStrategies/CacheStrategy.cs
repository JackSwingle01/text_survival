using text_survival.Actions;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for accessing cache storage at a location.
/// Requires CacheFeature. No time cost - instant transfer menu.
/// </summary>
public class CacheStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var cache = location.GetFeature<CacheFeature>();
        if (cache == null)
            return "There's no cache here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Cache access is instant - no time options
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // No impairments for cache access
        return (0, []);
    }

    public ActivityType GetActivityType() => ActivityType.Idle;

    public string GetActivityName() => "cache";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var cache = location.GetFeature<CacheFeature>()!;

        string name = cache.Name.ToUpper();
        Desktop.DesktopIO.RunTransferUI(ctx, cache.Storage, name);

        return WorkResult.Empty(0); // No time cost for cache management
    }
}
