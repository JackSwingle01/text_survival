using Survival;

/// How far the animals that want to move get, read off the world every minute (#96). An animal wants
/// to move while it patrols, grazes on or flees; for each one this holds where it last made progress and
/// the minutes it has wanted to move since without getting further than RadiusTiles from there. A
/// stuck animal, or one bouncing between two tiles, piles those minutes up; one walking on resets them.
/// Read every minute, a twitch on any period counts: an hourly look at a 10-minute bounce sees one tile.
/// Minutes it cannot walk, or a fight holds it, count as neither progress nor wanting.
public sealed class NetProgress(int radiusTiles)
{
    public int RadiusTiles { get; } = radiusTiles;

    readonly Dictionary<AgentId, (TileId Anchor, long WantingMinutes, HerdState Last)> _since = [];

    /// The longest an animal has wanted to move without getting further than RadiusTiles: its minutes,
    /// who, where and the state it was last in.
    public (long Minutes, string Who) Worst { get; private set; } = (0, "nobody");

    /// The same per state, so a run can tell grazing on from a pack on the hunt.
    public Dictionary<HerdState, long> WorstByState { get; } = [];

    public static bool WantsToMove(HerdState state) => state is HerdState.Patrolling or HerdState.Grazing or HerdState.Fleeing;

    /// One minute's look at every animal.
    public void Sample(World w)
    {
        foreach (var a in w.Agents.Values)
        {
            if (a.Mind is not HerdMind mind || !Sim.IsAlive(a)) continue;
            if (!_since.TryGetValue(a.Id, out var p) || MapGen.Manhattan(p.Anchor, a.Tile) > RadiusTiles
                || Body.GetCapacities(a.Body).Moving <= 0.1)
                p = (a.Tile, 0, mind.State);
            if (WantsToMove(mind.State) && !Sim.Held(w, a.Id)) p = (p.Anchor, p.WantingMinutes + 1, mind.State);
            _since[a.Id] = p;
            if (p.WantingMinutes > WorstByState.GetValueOrDefault(p.Last)) WorstByState[p.Last] = p.WantingMinutes;
            if (p.WantingMinutes > Worst.Minutes) Worst = (p.WantingMinutes, $"{a.Id} {a.Species} near {p.Anchor} ({p.Last})");
        }
    }
}
