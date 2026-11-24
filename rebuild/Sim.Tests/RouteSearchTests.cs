using Xunit.Abstractions;

namespace Survival.Tests;

/// Trip.Routes on a generated world, against a plain Dijkstra over dictionaries priced by Trip.StepMinutes: the
/// search as it was before its buffers were pooled (#133), so the pooled one must give the same minutes, the same
/// ways and the same settle order, for people and animals, out and into a site.
public class RouteSearchTests(ITestOutputHelper output)
{
    /// A plain Dijkstra over dictionaries, the oracle: the same settle order, tie-break and cutoffs as Trip.Routes promises.
    static (List<(TileId Tile, int Minutes)> Settled, Dictionary<TileId, TileId> Previous) Reference(World world, AgentId who,
        int maxMinutes = int.MaxValue, Func<TileId, int, bool>? until = null, Func<TileId, bool>? refused = null, TileId? into = null)
    {
        var agent = world.Agent(who);
        var m = Map.Of(world);
        var origin = into ?? agent.Tile;
        var explored = agent.Mind is PersonMind person ? person.Memory.Explored ?? new GroundMemory(m.Width, m.Height) : null;
        bool Open(TileId t) => explored == null || t == agent.Tile || explored.Contains(t);
        var minutes = new Dictionary<TileId, int> { [origin] = 0 };
        var previous = new Dictionary<TileId, TileId>();
        var settled = new Dictionary<TileId, int>();
        var order = new List<(TileId, int)>();
        var queue = new PriorityQueue<TileId, (int Minutes, TileId Tile)>();
        queue.Enqueue(origin, (0, origin));
        while (queue.TryDequeue(out var t, out var key))
        {
            if (settled.ContainsKey(t)) continue;
            settled[t] = key.Minutes;
            order.Add((t, key.Minutes));
            if (until?.Invoke(t, key.Minutes) == true) break;
            foreach (var n in MapGen.Neighbors(t))
            {
                if (!m.InBounds(n) || settled.ContainsKey(n) || !Open(n) || refused?.Invoke(n) == true) continue;
                var (from, to) = into == null ? (t, n) : (n, t);
                if (Traversal.Blocked(world, agent, from, to) != null) continue;
                long reach = (long)key.Minutes + Trip.StepMinutes(world, who, from, to, Pace.Quick);
                if (reach > maxMinutes) continue;
                int nd = (int)reach;
                if (minutes.TryGetValue(n, out var old) && nd >= old) continue;
                minutes[n] = nd;
                previous[n] = t;
                queue.Enqueue(n, (nd, n));
            }
        }
        return (order, previous);
    }

    static List<TileId> Chain(Dictionary<TileId, TileId> previous, TileId origin, TileId t)
    {
        var chain = new List<TileId>();
        for (var at = t; at != origin; at = previous[at]) chain.Add(previous[at]);
        return chain;
    }

    static void Agrees(World w, AgentId who, int maxMinutes = int.MaxValue, Func<TileId, int, bool>? until = null,
        Func<TileId, bool>? refused = null, TileId? into = null)
    {
        var (settled, previous) = Reference(w, who, maxMinutes, until, refused, into);
        var routes = Trip.Routes(w, who, maxMinutes, until, refused, into);
        Assert.Equal(settled, routes.Minutes.Select(kv => (kv.Key, kv.Value)));
        var origin = into ?? w.Agent(who).Tile;
        foreach (var (t, _) in settled)
        {
            Assert.Equal(Chain(previous, origin, t), routes.RouteBack(t));
            if (into == null) Assert.Equal(Chain(previous, origin, t).Prepend(t).SkipLast(1).Reverse(), routes.RouteTo(t));
        }
        Assert.Equal(settled.Count, routes.Reached.Count());
        Assert.Equal(into != null, routes.Into);
    }

    static IEnumerable<AgentId> Animals(World w) => w.Agents.Values.Where(a => a.Mind is not PersonMind && Sim.IsAlive(a)).Select(a => a.Id);

    [Fact]
    public void Routes_OnAGeneratedWorld_MatchThePlainSearch_ForPeopleAndAnimals_OutAndIntoASite()
    {
        var w = TestWorlds.New(17);
        // One of each species, the player, and the player into a tile it has explored some way off.
        var animals = Animals(w).GroupBy(id => w.Agent(id).Species).Select(g => g.First()).ToList();
        Assert.True(animals.Count >= 3, $"only {animals.Count} species on the map");
        foreach (var id in animals)
        {
            Agrees(w, id, maxMinutes: 240);
            var tile = w.Agent(id).Tile;
            Agrees(w, id, maxMinutes: 240, into: new TileId(tile.X + 3, tile.Y + 2) is var site && Map.Of(w).InBounds(site) ? site : tile);
        }
        Agrees(w, AgentId.Player);
        var afield = Reference(w, AgentId.Player, maxMinutes: 120).Settled.Last().Tile;
        Agrees(w, AgentId.Player, into: afield);
        var namedSite = Map.Of(w).Sites.Keys.MinBy(t => MapGen.Manhattan(t, w.Agent(animals[0]).Tile));
        Agrees(w, animals[0], into: namedSite);
        Agrees(w, AgentId.Player, into: Map.Of(w).Camp);
        Agrees(w, animals[0], until: (_, minutes) => minutes >= 90);
        Agrees(w, animals[0], maxMinutes: 600, refused: t => (t.X + t.Y) % 7 == 0);
    }

    [Fact]
    public void RouteMaps_StayTrue_AfterLaterSearches()
    {
        var w = TestWorlds.New(17);
        var animal = Animals(w).First();
        var first = Trip.Routes(w, animal, maxMinutes: 240);
        var copy = first.Minutes.ToList();
        var routes = copy.Select(kv => first.RouteTo(kv.Key)).ToList();
        Trip.Routes(w, AgentId.Player);
        Trip.Routes(w, animal, maxMinutes: 60, into: w.Agent(animal).Tile);
        Assert.Equal(copy, first.Minutes.ToList());
        Assert.Equal(routes, copy.Select(kv => first.RouteTo(kv.Key)));
    }

    /// Nothing calls Routes from a search's callbacks today; one that did gets its own scratch, and neither search is spoiled.
    [Fact]
    public void ASearchBegunInsideAnother_SpoilsNeither()
    {
        var w = TestWorlds.New(17);
        var (outer, inner) = (Animals(w).First(), AgentId.Player);
        var innerExpected = Reference(w, inner, maxMinutes: 60).Settled;
        RouteMap? innerRoutes = null;
        Agrees(w, outer, maxMinutes: 120, refused: _ => { innerRoutes ??= Trip.Routes(w, inner, maxMinutes: 60); return false; });
        Assert.Equal(innerExpected, innerRoutes!.Minutes.Select(kv => (kv.Key, kv.Value)));
    }

    /// A report for #133's PR: bytes allocated by one short and one long search, warm (the minute's step prices
    /// and the scratch already there). Asserts nothing about the numbers.
    [Fact]
    public void RouteSearch_Allocation_Report()
    {
        var w = TestWorlds.New(17);
        var animal = Animals(w).First(id => w.Agent(id).Species == Species.Caribou);
        foreach (var (name, max) in new[] { ("short (60 min)", 60), ("long (whole reach)", int.MaxValue) })
        {
            var warm = Trip.Routes(w, animal, maxMinutes: max);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var routes = Trip.Routes(w, animal, maxMinutes: max);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            output.WriteLine($"{name}: {routes.Minutes.Count} tiles settled, {bytes:N0} bytes, {bytes / (double)routes.Minutes.Count:F0} bytes a tile");
            Assert.Equal(warm.Minutes.Count, routes.Minutes.Count);
        }
    }
}
