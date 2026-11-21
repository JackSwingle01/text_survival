namespace Survival;

/// Which agents stand on which tile (emergent-world-design §3.6): an index, never saved, built on the
/// first question after the world's agents change and kept as they walk. A whole-map pass (the
/// report, every thing's tags, a settle) is mostly "who is here" and "who is within r tiles", which
/// without it walked every agent for every tile.
///
/// It is kept exactly: an agent indexed here reports each move (Agent.Tile), and any agent made or
/// removed changes the count or the next id (Fits), so the next question builds it again. Replacing
/// an agent in World.Agents by hand under the same id (tests setting a scene) is the one change it
/// cannot see.
internal sealed class NearbyIndex
{
    readonly Dictionary<TileId, List<AgentId>> byTile = [];
    internal readonly Agent[] All;
    readonly int nextAgentId;

    internal NearbyIndex(World world)
    {
        All = [.. world.Agents.Values];
        nextAgentId = world.NextAgentId;
        foreach (var a in All) // id order, so every list starts sorted
        {
            a.Index = this;
            (byTile.TryGetValue(a.Tile, out var list) ? list : byTile[a.Tile] = []).Add(a.Id);
        }
    }

    internal bool Fits(World world) => world.Agents.Count == All.Length && world.NextAgentId == nextAgentId;

    internal IReadOnlyList<AgentId> At(TileId tile) => byTile.TryGetValue(tile, out var list) ? list : [];

    /// An indexed agent stepped from one tile to another.
    internal void Moved(AgentId id, TileId from, TileId to)
    {
        if (byTile.TryGetValue(from, out var old))
        {
            int i = old.BinarySearch(id);
            if (i >= 0) old.RemoveAt(i);
            if (old.Count == 0) byTile.Remove(from);
        }
        var list = byTile.TryGetValue(to, out var l) ? l : byTile[to] = [];
        int at = list.BinarySearch(id);
        if (at < 0) list.Insert(~at, id);
    }
}

public static class Nearby
{
    static NearbyIndex Index(World world)
    {
        if (world.Nearby is { } index && index.Fits(world)) return index;
        return world.Nearby = new NearbyIndex(world);
    }

    /// The agents standing on the tile, living or dead, in id order.
    public static IReadOnlyList<AgentId> At(World world, TileId tile) => Index(world).At(tile);

    /// The agents within radius tiles (Manhattan) of the tile, living or dead, in id order.
    public static List<Agent> Within(World world, TileId tile, int radius)
    {
        var index = Index(world);
        var found = new List<Agent>();
        // The cheaper way round: the diamond's tiles, or every agent.
        if (2 * radius * (radius + 1) + 1 < index.All.Length)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = Math.Abs(dx) - radius; dy <= radius - Math.Abs(dx); dy++)
                    foreach (var id in index.At(new TileId(tile.X + dx, tile.Y + dy))) found.Add(world.Agents[id]);
            found.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        else
            foreach (var a in index.All)
                if (MapGen.Manhattan(a.Tile, tile) <= radius) found.Add(a);
        return found;
    }
}
