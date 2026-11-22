using System.Text.Json.Serialization;

namespace Survival;

/// Every kind of thing a sign, an access check or a verb can point at (emergent-world-design §3.1).
/// Switches over it have no default arm and CS8509 is an error in Sim, so a new kind fails the build
/// wherever it is not handled.
public enum ThingKind { Agent, Body, Carcass, Pool, Stash, Fire, Shelter, Track, Sign, Item }

/// One thing in the world. Index must survive save and load and never shift when another thing is
/// removed; per kind:
/// - Agent: the AgentId of an agent with at least one living member.
/// - Body: the AgentId of an agent whose members are all dead (only a person stays dead in the world;
///   a herd's dead become carcasses, Combat.Aftermath).
/// - Carcass: Carcass.Id, never reused (World.NextCarcassId).
/// - Pool: the index in Pools.At (generation order; pools are never removed).
/// - Stash: TileState.StashId, new for each stash begun on the tile, so a ref to an emptied stash
///   never names the next one. A stash set down by hand in a test has no id: NoId.
/// - Fire: the FireId of a pit. Carried torches are not on a tile and are not things.
/// - Shelter: ShelterState.Id, never reused; ShelterState.Unplaced for a find shown before it stands.
/// - Track: (int)TrackMaker for that maker's prints; TrailIndexBase + (int)Direction for the desire
///   path along an edge, named from its west or north end (Trail makes the canonical form).
/// - Sign, Item: reserved. Neither has an id yet, and a list position shifts on removal.
public readonly record struct ThingRef(ThingKind Kind, TileId Tile, int Index) : IComparable<ThingRef>
{
    public const int TrailIndexBase = 16;
    public const int NoId = -1;

    public int CompareTo(ThingRef o) =>
        Tile.CompareTo(o.Tile) is int c and not 0 ? c : Kind.CompareTo(o.Kind) is int k and not 0 ? k : Index.CompareTo(o.Index);

    /// Canonical form: both ends of an edge give the same ref.
    public static ThingRef Trail(TileId from, Direction dir) => dir is Direction.West or Direction.North
        ? new(ThingKind.Track, Map.Step(from, dir), TrailIndexBase + (int)Opposite(dir))
        : new(ThingKind.Track, from, TrailIndexBase + (int)dir);

    static Direction Opposite(Direction d) => d switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        Direction.West => Direction.East,
    };

    public Direction? TrailDirection => Kind == ThingKind.Track && Index >= TrailIndexBase ? (Direction)(Index - TrailIndexBase) : null;

    public override string ToString() => $"{Kind} {TileIdConverter.Format(Tile)}#{Index}";
}

/// One hop of cause (emergent-world-design §3.2), saved on things that persist. Who may name an agent
/// that has died or left the world: never dereference it as live.
public readonly record struct Cause(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Who,
    string What)
{
    public override string ToString() => Who is AgentId who ? $"{What} ({who})" : What;

    public const string Natural = "natural";

    public static string Describe(World world, AgentId who)
    {
        if (who == AgentId.Player) return "the player";
        if (!world.Agents.TryGetValue(who, out var a)) return "someone";
        if (a.Mind is PersonMind) return "a person";
        string species = a.Species.ToString().ToLowerInvariant();
        if (a.Members.Count <= 1) return $"a {species}";
        return Fauna.BehaviorOf(a.Species) == HerdBehavior.Prey ? $"a {species} herd" : $"a {species} pack";
    }

    public static Cause By(World world, AgentId who) => new(who, Describe(world, who));
}

/// What is on a tile, as ThingRefs. Reads only.
public static class Things
{
    /// Includes hidden pools: this is the world, not anyone's knowledge. Desire paths are those named
    /// from this tile (east and south, Trail's canonical form).
    public static IEnumerable<ThingRef> At(World world, TileId tile)
    {
        var found = new List<ThingRef>();
        foreach (var id in Nearby.At(world, tile)) found.Add(OfAgent(world, id));
        var state = world.Tiles.GetValueOrDefault(tile);
        foreach (var c in state?.Carcasses ?? []) found.Add(new(ThingKind.Carcass, tile, c.Id));
        var pools = Pools.At(world, tile);
        for (int i = 0; i < pools.Count; i++) found.Add(new(ThingKind.Pool, tile, i));
        if (state != null && StashOf(state, tile) is ThingRef stash) found.Add(stash);
        foreach (var (id, f) in world.Fires)
            if (f.CarriedBy == null && f.Tile == tile) found.Add(new(ThingKind.Fire, tile, id.Value));
        if (ShelterAt(world, tile) is ThingRef shelter) found.Add(shelter);
        if (state?.Tracks != null)
            foreach (var sign in Tracks.Read(world, tile)) found.Add(new(ThingKind.Track, tile, (int)sign.Maker));
        foreach (var dir in (ReadOnlySpan<Direction>)[Direction.East, Direction.South])
            if (TrailTo(world, tile, dir) != TrailTier.None) found.Add(ThingRef.Trail(tile, dir));
        found.Sort();
        return found;
    }

    public static ThingRef OfAgent(World world, AgentId id)
    {
        var a = world.Agent(id);
        return new(Sim.IsAlive(a) ? ThingKind.Agent : ThingKind.Body, a.Tile, id.Value);
    }

    public static bool Exists(World world, ThingRef t)
    {
        return t.Kind switch
        {
            ThingKind.Agent or ThingKind.Body => world.Agents.TryGetValue(new AgentId(t.Index), out var a) && a.Tile == t.Tile
                && Sim.IsAlive(a) == (t.Kind == ThingKind.Agent),
            ThingKind.Carcass => Carcasses.Find(world, t.Tile, t.Index) != null,
            ThingKind.Pool => t.Index >= 0 && t.Index < Pools.At(world, t.Tile).Count,
            ThingKind.Stash => StashAt(world, t.Tile) == t,
            ThingKind.Fire => world.Fires.TryGetValue(new FireId(t.Index), out var f) && f.CarriedBy == null && f.Tile == t.Tile,
            ThingKind.Shelter => ShelterAt(world, t.Tile) == t,
            ThingKind.Track => t.TrailDirection is Direction dir ? ThingRef.Trail(t.Tile, dir) == t && TrailTo(world, t.Tile, dir) != TrailTier.None
                : Enum.IsDefined((TrackMaker)t.Index) && Tracks.Read(world, t.Tile).Any(s => (int)s.Maker == t.Index),
            ThingKind.Sign => false,
            ThingKind.Item => false,
        };
    }

    /// Throws: callers resolve a thing (with their own refusal) before asking anything of it.
    internal static void Require(World world, ThingRef t)
    {
        if (!Exists(world, t)) throw new ArgumentException($"No {t} in the world", nameof(t));
    }

    /// An empty pile is no stash.
    public static ThingRef? StashAt(World world, TileId tile) =>
        world.Tiles.TryGetValue(tile, out var state) ? StashOf(state, tile) : null;

    static ThingRef? StashOf(TileState state, TileId tile) =>
        state.Stash is { } s && (s.Kg.Count > 0 || s.Gear.Count > 0) ? new(ThingKind.Stash, tile, state.StashId ?? ThingRef.NoId) : null;

    public static ThingRef? ShelterAt(World world, TileId tile) =>
        Shelters.Peek(world, tile) is { } s ? new(ThingKind.Shelter, tile, s.Id) : null;

    internal static TrailTier TrailTo(World world, TileId tile, Direction dir)
    {
        var to = Map.Step(tile, dir);
        // Most edges were never walked: no wear to read.
        return Map.Of(world).InBounds(to) && world.Trails.ContainsKey(new EdgeId(tile, to)) ? Tracks.TierOf(world, tile, to) : TrailTier.None;
    }
}
