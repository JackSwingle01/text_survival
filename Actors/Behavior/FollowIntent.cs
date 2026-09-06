namespace text_survival.Actors;

/// <summary>
/// The minimal state contract for autonomous following. Establishing willingness and
/// pursuing the target belong to behavior; this object does not choose or execute actions.
/// </summary>
public sealed class FollowIntent
{
    public Actor Target { get; set; } = null!;

    public FollowIntent() { }

    public FollowIntent(Actor target)
    {
        Target = target;
    }
}
