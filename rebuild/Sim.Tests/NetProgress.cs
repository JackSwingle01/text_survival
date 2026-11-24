using Survival;

/// How far the animals that want to move get, read off the world every minute (#96). An animal wants
/// to move while it patrols or flees, and while it grazes with a way planned ahead of it (grazing where it
/// stands, with water beside it, a herd lives on one patch for a week and is not stuck: seeds 5 and 17,
/// #178 and #181); for each one this holds where it last made progress and
/// the minutes it has wanted to move since without getting further than radiusTiles from there. A
/// stuck animal, or one bouncing between two tiles, piles those minutes up; one walking on resets them.
/// Read every minute, a twitch on any period counts: an hourly look at a 10-minute bounce sees one tile.
/// A minute it cannot walk starts it afresh where it stands; a minute a fight holds it is not counted.
public sealed class NetProgress(int radiusTiles)
{
    readonly Dictionary<AgentId, (TileId Anchor, long WantingMinutes, HerdState LastState)> _tracked = [];

    /// The longest an animal has wanted to move without getting further than radiusTiles: its minutes,
    /// who, where and the state it was last in.
    public (long Minutes, string Who) Worst { get; private set; } = (0, "nobody");

    /// The same per state, so a run can tell grazing on from a pack on the hunt.
    public Dictionary<HerdState, long> WorstByState { get; } = [];

    static bool WantsToMove(HerdMind mind) => mind.State is HerdState.Patrolling or HerdState.Fleeing
        || mind is { State: HerdState.Grazing, Plan.Route.Count: > 0 };

    /// One minute's look at every animal.
    public void Sample(World w)
    {
        foreach (var a in w.Agents.Values)
        {
            if (a.Mind is not HerdMind mind || !Sim.IsAlive(a)) continue;
            if (!_tracked.TryGetValue(a.Id, out var p) || MapGen.Manhattan(p.Anchor, a.Tile) > radiusTiles
                || Body.GetCapacities(a.Body).Moving <= 0.1)
                p = (a.Tile, 0, mind.State);
            if (WantsToMove(mind) && !Sim.Held(w, a.Id)) p = (p.Anchor, p.WantingMinutes + 1, mind.State);
            _tracked[a.Id] = p;
            if (p.WantingMinutes > WorstByState.GetValueOrDefault(p.LastState)) WorstByState[p.LastState] = p.WantingMinutes;
            if (p.WantingMinutes > Worst.Minutes) Worst = (p.WantingMinutes, $"{a.Id} {a.Species} near {p.Anchor} ({p.LastState})");
        }
    }
}
