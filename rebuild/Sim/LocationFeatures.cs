using System.Text.Json.Serialization;

namespace Survival;

/// Every kind of location feature a sign, an access check or a verb can point at (emergent-world-design §3.1).
/// Switches over it have no default arm and CS8509 is an error in Sim, so a new kind fails the build
/// wherever it is not handled.
public enum LocationFeatureKind { Agent, Body, Carcass, Pool, Pile, Fire, Shelter, Track, Sign, Item, Trodden }

/// One location feature in the world. Index must survive save and load and never shift when another is
/// removed; per kind:
/// - Agent: the AgentId of an agent with at least one living member.
/// - Body: the AgentId of an agent whose members are all dead (only a person stays dead in the world;
///   a herd's dead become carcasses, Combat.Aftermath).
/// - Carcass: Carcass.Id, never reused (World.NextCarcassId).
/// - Pool: the index in Pools.At (generation order; pools are never removed).
/// - Pile: TileState.PileId, new for each pile begun on the tile, so a ref to an emptied pile
///   never names the next one. A pile set down by hand in a test has no id: NoId.
/// - Fire: the FireId of a pit. Carried torches are not on a tile and are not location features.
/// - Shelter: ShelterState.Id, never reused; ShelterState.Unplaced for a find shown before it stands.
/// - Track: (int)TrackMaker for that maker's prints; TrailIndexBase + (int)Direction for the desire
///   path along an edge, named from its west or north end (Trail makes the canonical form).
/// - Trodden: 0, the tile's snow packed by time spent on it (Tracks.TroddenAt), while it reads as trodden ground.
/// - Sign, Item: reserved. Neither has an id yet, and a list position shifts on removal.
public readonly record struct LocationFeature(LocationFeatureKind Kind, TileId Tile, int Index) : IComparable<LocationFeature>
{
    public const int TrailIndexBase = 16;
    public const int NoId = -1;

    public int CompareTo(LocationFeature o) =>
        Tile.CompareTo(o.Tile) is int c and not 0 ? c : Kind.CompareTo(o.Kind) is int k and not 0 ? k : Index.CompareTo(o.Index);

    /// Canonical form: both ends of an edge give the same ref.
    public static LocationFeature Trail(TileId from, Direction dir) => dir is Direction.West or Direction.North
        ? new(LocationFeatureKind.Track, Map.Step(from, dir), TrailIndexBase + (int)Opposite(dir))
        : new(LocationFeatureKind.Track, from, TrailIndexBase + (int)dir);

    static Direction Opposite(Direction d) => d switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        Direction.West => Direction.East,
    };

    public Direction? TrailDirection => Kind == LocationFeatureKind.Track && Index >= TrailIndexBase ? (Direction)(Index - TrailIndexBase) : null;

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
        if (Fauna.HerdOf(world, a).Count <= 1) return $"a {species}";
        return Fauna.BehaviorOf(a.Species) == HerdBehavior.Prey ? $"a {species} herd" : $"a {species} pack";
    }

    public static Cause By(World world, AgentId who) => new(who, Describe(world, who));
}

/// The location features on a tile. Reads only.
public static class LocationFeatures
{
    /// Includes hidden pools: this is the world, not anyone's memory. Desire paths are those named
    /// from this tile (east and south, Trail's canonical form).
    public static IEnumerable<LocationFeature> At(World world, TileId tile)
    {
        var found = new List<LocationFeature>();
        foreach (var group in Fauna.GroupsAt(world, tile)) found.Add(OfAgent(world, group[0].Id)); // a herd under its first
        var state = world.Tiles.GetValueOrDefault(tile);
        foreach (var c in state?.Carcasses ?? []) found.Add(new(LocationFeatureKind.Carcass, tile, c.Id));
        var pools = Pools.At(world, tile);
        for (int i = 0; i < pools.Count; i++) found.Add(new(LocationFeatureKind.Pool, tile, i));
        if (state != null && PileOf(state, tile) is LocationFeature pile) found.Add(pile);
        foreach (var (id, f) in world.Fires)
            if (f.CarriedBy == null && f.Tile == tile) found.Add(new(LocationFeatureKind.Fire, tile, id.Value));
        if (ShelterAt(world, tile) is LocationFeature shelter) found.Add(shelter);
        if (state?.Tracks != null)
            foreach (var sign in Tracks.Read(world, tile)) found.Add(new(LocationFeatureKind.Track, tile, (int)sign.Maker));
        foreach (var dir in (ReadOnlySpan<Direction>)[Direction.East, Direction.South])
            if (TrailTo(world, tile, dir) != TrailTier.None) found.Add(LocationFeature.Trail(tile, dir));
        if (state?.Trodden != null && IsTrodden(world, tile)) found.Add(new(LocationFeatureKind.Trodden, tile, 0));
        found.Sort();
        return found;
    }

    public static LocationFeature OfAgent(World world, AgentId id)
    {
        var a = world.Agent(id);
        return new(Sim.IsAlive(a) ? LocationFeatureKind.Agent : LocationFeatureKind.Body, a.Tile, id.Value);
    }

    public static bool Exists(World world, LocationFeature t)
    {
        return t.Kind switch
        {
            LocationFeatureKind.Agent or LocationFeatureKind.Body => world.Agents.TryGetValue(new AgentId(t.Index), out var a) && a.Tile == t.Tile
                && Sim.IsAlive(a) == (t.Kind == LocationFeatureKind.Agent),
            LocationFeatureKind.Carcass => Carcasses.Find(world, t.Tile, t.Index) != null,
            LocationFeatureKind.Pool => t.Index >= 0 && t.Index < Pools.At(world, t.Tile).Count,
            LocationFeatureKind.Pile => PileAt(world, t.Tile) == t,
            LocationFeatureKind.Fire => world.Fires.TryGetValue(new FireId(t.Index), out var f) && f.CarriedBy == null && f.Tile == t.Tile,
            LocationFeatureKind.Shelter => ShelterAt(world, t.Tile) == t,
            LocationFeatureKind.Track => t.TrailDirection is Direction dir ? LocationFeature.Trail(t.Tile, dir) == t && TrailTo(world, t.Tile, dir) != TrailTier.None
                : Enum.IsDefined((TrackMaker)t.Index) && Tracks.Read(world, t.Tile).Any(s => (int)s.Maker == t.Index),
            LocationFeatureKind.Sign => false,
            LocationFeatureKind.Item => false,
            LocationFeatureKind.Trodden => t.Index == 0 && IsTrodden(world, t.Tile),
        };
    }

    /// Throws: callers resolve a location feature (with their own refusal) before asking anything of it.
    internal static void Require(World world, LocationFeature t)
    {
        if (!Exists(world, t)) throw new ArgumentException($"No {t} in the world", nameof(t));
    }

    /// An empty pile is no pile.
    public static LocationFeature? PileAt(World world, TileId tile) =>
        world.Tiles.TryGetValue(tile, out var state) ? PileOf(state, tile) : null;

    static LocationFeature? PileOf(TileState state, TileId tile) =>
        state.Pile is { } s && (s.Kg.Count > 0 || s.Gear.Count > 0) ? new(LocationFeatureKind.Pile, tile, state.PileId ?? LocationFeature.NoId) : null;

    public static LocationFeature? ShelterAt(World world, TileId tile) =>
        Shelters.Peek(world, tile) is { } s ? new(LocationFeatureKind.Shelter, tile, s.Id) : null;

    static bool IsTrodden(World world, TileId tile) => Tracks.TroddenAt(world, tile) >= Tracks.TroddenTileAt;

    internal static TrailTier TrailTo(World world, TileId tile, Direction dir)
    {
        var to = Map.Step(tile, dir);
        // Most edges were never walked: nothing trodden to read.
        return Map.Of(world).InBounds(to) && world.Trails.ContainsKey(new EdgeId(tile, to)) ? Tracks.TierOf(world, tile, to) : TrailTier.None;
    }
}
