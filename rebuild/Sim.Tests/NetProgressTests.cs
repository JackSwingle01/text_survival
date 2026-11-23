using Survival;
using Xunit;

/// NetProgress, the long runs' "nothing gets stuck" measure, read off a real world: a wolf is put on
/// the tiles a path names, one minute at a time, in the state it names.
public class NetProgressTests
{
    static (World W, Agent Wolf, TileId Start) OneWolf()
    {
        var w = World.New(1);
        foreach (var id in w.Agents.Keys.Where(id => id != AgentId.Player).ToList()) Agents.Remove(w, id);
        var m = Map.Of(w);
        var start = new TileId(m.Width / 2, m.Height / 2);
        return (w, w.Agent(Agents.Spawn(w, Species.Wolf, start)), start);
    }

    /// Walks the wolf where tileAt says each minute, wanting to move as patrolling does, and reads the
    /// measure every minute.
    static NetProgress Walk(Agent wolf, World w, int minutes, Func<int, TileId> tileAt, HerdState state = HerdState.Patrolling, int radiusTiles = 1)
    {
        var progress = new NetProgress(radiusTiles);
        for (int minute = 0; minute < minutes; minute++)
        {
            (wolf.Tile, Fauna.MindOf(wolf).State) = (tileAt(minute), state);
            progress.Sample(w);
        }
        return progress;
    }

    /// The pack that failed #94's long run: it turned back every ten minutes between two tiles, a cycle
    /// that fits the hour, so a look once an hour always saw it on the same tile, and a look for a new
    /// tile reset nothing either way. Net progress over the minutes sees it go nowhere for days.
    [Fact]
    public void ABounceBetweenTwoTiles_OnACycleThatFitsTheHour_IsStuck()
    {
        var (w, wolf, a) = OneWolf();
        var b = Map.Step(a, Direction.North);
        TileId Bounce(int minute) => minute / 10 % 2 == 0 ? a : b;
        Assert.All(Enumerable.Range(0, 3 * 24).Select(hour => Bounce(hour * 60)), t => Assert.Equal(a, t)); // what the hourly look saw
        var progress = Walk(wolf, w, 3 * 1440, Bounce);
        Assert.Equal(3 * 1440, progress.Worst.Minutes);
        Assert.Contains(wolf.Id.ToString(), progress.Worst.Who);
    }

    [Fact]
    public void AnAnimalWalkingOn_IsNotStuck()
    {
        var (w, wolf, start) = OneWolf();
        // A tile east every half hour, then back the same way: always getting somewhere new.
        TileId Line(int minute) { int leg = minute / 30 % 40; return new TileId(start.X + (leg < 20 ? leg : 40 - leg), start.Y); }
        Assert.True(Walk(wolf, w, 3 * 1440, Line).Worst.Minutes <= 60);
    }

    /// A wider radius catches a wider bounce: tiles two apart get nowhere within two tiles.
    [Fact]
    public void ABounceTwoTilesWide_IsStuckWithinTwoTiles_ButNotOne()
    {
        var (w, wolf, a) = OneWolf();
        var c = Map.Step(Map.Step(a, Direction.North), Direction.North);
        TileId Bounce(int minute) => minute / 10 % 2 == 0 ? a : c;
        Assert.Equal(1440, Walk(wolf, w, 1440, Bounce, radiusTiles: 2).Worst.Minutes);
        Assert.True(Walk(wolf, w, 1440, Bounce, radiusTiles: 1).Worst.Minutes <= 10);
    }

    /// Lying up is not wanting to move: a pack resting at a kill for a fortnight is not stuck.
    [Fact]
    public void AnAnimalResting_IsNotCounted()
    {
        var (w, wolf, a) = OneWolf();
        Assert.Equal(0, Walk(wolf, w, 1440, _ => a, HerdState.Resting).Worst.Minutes);
    }
}
