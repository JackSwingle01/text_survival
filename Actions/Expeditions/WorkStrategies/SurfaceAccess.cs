using text_survival.Environments;
using text_survival.Environments.Features;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// The second half of the access gate. <see cref="Location.GetWorkOptions"/> already hides a
/// buried target's actions, but a menu is not a rule: anything holding a reference to a
/// placed target - a strategy built before the snow fell, an event handing out a cache's
/// contents - has to ask again at the moment it acts.
/// </summary>
public static class SurfaceAccess
{
    /// <summary>Null if the target can be reached, or the reason it cannot.</summary>
    public static string? Check(Location location, LocationFeature? feature)
    {
        if (feature == null || !location.IsCovered(feature)) return null;

        int cm = (int)Math.Round(location.Surface.GetBlockingCoverM(feature.Placement) * 100);
        return $"{feature.AccessName} is under {cm} cm of snow. You'll have to dig it out first.";
    }
}
