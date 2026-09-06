using text_survival.Actors.Animals;
using text_survival.Environments.Grid;
using text_survival.Environments.Navigation;
using text_survival.Environments.Perception;

namespace text_survival.Actors;

/// <summary>Actor-neutral agreement and evidence policy; callers own action scheduling.</summary>
public static class Following
{
    public const int SearchMinutes = 90;

    public static bool TryBegin(Actor follower, Actor target)
    {
        if (follower == target || !follower.IsAlive || !target.IsAlive || !Sight.CanSeeActor(follower, target)) return false;
        var seen = new HashSet<Actor> { follower };
        for (Actor? actor = target; actor != null; actor = actor.Following?.Target)
            if (!seen.Add(actor)) return false;
        follower.Following = new FollowIntent(target);
        return true;
    }

    public static void Observe(Actor follower, double minute)
    {
        if (follower.Following is not { } intent) return;
        if (!Sight.CanSeeActor(follower, intent.Target)) return;
        if (!intent.Target.IsAlive) { follower.Following = null; return; }
        var observedPosition = follower.Map.GetPosition(intent.Target.CurrentLocation);
        if (intent.LastSeenPosition != observedPosition)
        {
            intent.RouteFailures = 0;
            intent.NextRouteAttemptMinute = 0;
        }
        intent.LastSeenPosition = observedPosition;
        intent.LeadPosition = intent.LastSeenPosition;
        intent.LastEvidenceMinute = minute;
        intent.LastPassage = follower.Map.Tracks.LatestPassage;
        intent.TrackKind = intent.Target is Animal animal ? animal.AnimalType.Tracks() : TrackMaker.Human;
        intent.Investigated.Clear();
    }

    public static PathResult? Pursue(Actor follower, double minute)
    {
        if (follower.Following is not { } intent) return null;
        if (minute - intent.LastEvidenceMinute >= SearchMinutes)
        {
            follower.Following = null;
            return null;
        }
        if (intent.LeadPosition is not { } lead) return null;
        var position = follower.Map.GetPosition(follower.CurrentLocation);
        if (position == lead)
        {
            // Physical prints are anonymous. A crossing can be a false lead; no actor ID is consulted.
            var passage = follower.Map.Tracks.ReadPassages(position)
                .Where(p => p.Sequence > intent.LastPassage && p.Maker == intent.TrackKind && !intent.Investigated.Contains(p.To))
                .OrderByDescending(p => p.Sequence).FirstOrDefault();
            if (passage == null) return new(PathStatus.AlreadyThere, []);
            intent.Investigated.Add(position);
            if (intent.Investigated.Count > 64) intent.Investigated.RemoveAt(0);
            intent.LastPassage = passage.Sequence;
            intent.LeadPosition = lead = passage.To;
            // Trail evidence advances the route, but does not indefinitely reset the search clock.
        }
        if (minute < intent.NextRouteAttemptMinute) return new(PathStatus.BudgetExceeded, []);
        var route = Navigation.FindRoute(follower.Map, position, lead, follower);
        if (route.Status is PathStatus.NoRoute or PathStatus.BudgetExceeded)
        {
            intent.NextRouteAttemptMinute = minute + 5;
            if (++intent.RouteFailures >= 3) follower.Following = null;
        }
        else intent.RouteFailures = 0;
        return route;
    }
}
