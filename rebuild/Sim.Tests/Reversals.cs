using Survival;

/// Pacing, counted one way for the NPC bench and the tests: a step back onto the tile just left, within
/// WindowMinutes of leaving it, having done nothing between (no fire lit, tree felled, meal eaten or forage
/// gathered). Feed it every event in order; Count is the reversals so far.
sealed class Reversals(AgentId who)
{
    public const int WindowMinutes = 30;

    (long Minute, TileId From)? lastMove;
    bool didSince;

    public int Count { get; private set; }

    public void Read(WorldEvent e)
    {
        switch (e)
        {
            case Moved m when m.Who == who:
                if (lastMove is { } l && m.To == l.From && m.Minute - l.Minute <= WindowMinutes && !didSince) Count++;
                (lastMove, didSince) = ((m.Minute, m.From), false);
                break;
            case FireLit f when f.By == who:
            case TreeFelled t when t.Who == who:
            case Ate a when a.Who == who:
            case Gathered g when g.Who == who:
                didSince = true;
                break;
        }
    }

    public static int In(IEnumerable<WorldEvent> log, AgentId who)
    {
        var count = new Reversals(who);
        foreach (var e in log) count.Read(e);
        return count.Count;
    }
}
