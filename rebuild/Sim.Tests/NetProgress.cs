using Survival;

/// How far the animals that want to move get, read off the world every minute (#96). An animal wants
/// to move while it flees, and while it has a way planned ahead of it (grazing where it
/// stands, with water beside it, a herd lives on one patch for a week and is not stuck: seeds 5 and 17,
/// #178 and #181); for each one this holds where it last made progress and
/// the minutes it has wanted to move since without getting further than radiusTiles from there. A
/// stuck animal, or one bouncing between two tiles, piles those minutes up; one walking on resets them.
/// Read every minute, a twitch on any period counts: an hourly look at a 10-minute bounce sees one tile.
/// A minute it cannot walk starts it afresh where it stands; a minute a fight holds it is not counted.
public sealed class NetProgress(int radiusTiles)
{
    readonly Dictionary<AgentId, (TileId Anchor, long WantingMinutes, string Why)> _tracked = [];

    /// The longest an animal has wanted to move without getting further than radiusTiles: its minutes,
    /// who, where and why it last wanted to.
    public (long Minutes, string Who) Worst { get; private set; } = (0, "nobody");

    /// The same per reason (fleeing, or the stat its plan sees to), so a run can tell grazing on from a pack on the hunt.
    public Dictionary<string, long> WorstByWhy { get; } = [];

    static string? WantsToMove(HerdMind mind) => Fauna.Fleeing(mind) ? "fleeing"
        : mind.Plan is { Route.Count: > 0 } plan ? plan.Stat.ToString() : null;

    /// One minute's look at every animal.
    public void Sample(World w)
    {
        foreach (var a in w.Agents.Values)
        {
            if (a.Mind is not HerdMind mind || !Sim.IsAlive(a)) continue;
            var why = WantsToMove(mind);
            if (!_tracked.TryGetValue(a.Id, out var p) || MapGen.Manhattan(p.Anchor, a.Tile) > radiusTiles
                || Body.Speed(a.Body) <= 0)
                p = (a.Tile, 0, why ?? "still");
            if (why != null && !Sim.Held(w, a.Id)) p = (p.Anchor, p.WantingMinutes + 1, why);
            _tracked[a.Id] = p;
            if (p.WantingMinutes > WorstByWhy.GetValueOrDefault(p.Why)) WorstByWhy[p.Why] = p.WantingMinutes;
            if (p.WantingMinutes > Worst.Minutes) Worst = (p.WantingMinutes, $"{a.Id} {a.Species} near {p.Anchor} ({p.Why})");
        }
    }
}
