using text_survival.Environments.Grid;

namespace text_survival.Actors;

/// <summary>An agreement plus the follower's own evidence. Hidden target positions are never cached.</summary>
public sealed class FollowIntent
{
    public Actor Target { get; set; } = null!;
    public GridPosition? LastSeenPosition { get; set; }
    public GridPosition? LeadPosition { get; set; }
    public double LastEvidenceMinute { get; set; }
    public long LastPassage { get; set; }
    public int RouteFailures { get; set; }
    public double NextRouteAttemptMinute { get; set; }
    public TrackMaker TrackKind { get; set; }
    public List<GridPosition> Investigated { get; set; } = [];
    public FollowIntent() { }
    public FollowIntent(Actor target) => Target = target;
}
