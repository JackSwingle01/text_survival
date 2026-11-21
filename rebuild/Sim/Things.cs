using System.Text.Json.Serialization;

namespace Survival;

/// Every kind of thing a sign, an access check or a verb can point at (emergent-world-design §3.1).
/// Every switch over it is a switch expression with no default arm, and CS8509 is an error in Sim:
/// a new kind fails the build wherever it is not handled, which is the interaction table's empty cell.
public enum ThingKind { Agent, Body, Carcass, Pool, Stash, Fire, Shelter, Track, Sign, Item }

/// One thing in the world: its kind, the tile it is on, and which one of that kind on the tile.
/// What Index means, per kind (every one survives a save and load, and none shifts when another
/// thing is removed):
/// - Agent: the AgentId of an agent with at least one living member.
/// - Body: the AgentId of an agent whose members are all dead. Only a person stays in the world dead
///   (Combat.Aftermath); a herd's dead leave it as carcasses, so a Body is a dead person.
/// - Carcass: Carcass.Id, unique in the world and never reused (World.NextCarcassId).
/// - Pool: the pool's index in Pools.At, generation's order; pools are never removed (PoolId.Index).
/// - Stash: the stash's id (TileState.StashId), a new one each time a stash is begun on the tile, so
///   a ref held to an emptied stash never names the next one. A stash set down by hand in a test,
///   with no id, is NoId.
/// - Fire: the FireId of a pit. Carried torches are not on a tile and are not things.
/// - Shelter: the shelter's id (ShelterState.Id), never reused; a find shown before it stands is
///   ShelterState.Unplaced.
/// - Track: (int)TrackMaker for that maker's prints on the tile; TrailIndexBase + (int)Direction for
///   the desire path along an edge, always named from the edge's first tile (EdgeId.A), whose other
///   end is east or south (Trail makes the canonical form, so one edge is one ref).
/// - Sign: reserved. No stored signs exist yet (scat, claw marks); when they do they need ids of
///   their own, since a position in a pruned list shifts.
/// - Item: reserved. Carried gear has no id, and a position in a pack or stash shifts on removal;
///   gear gets ids when the first verb acts on an item.
public readonly record struct ThingRef(ThingKind Kind, TileId Tile, int Index) : IComparable<ThingRef>
{
    public const int TrailIndexBase = 16;
    /// The index of a stash with no id: one set down directly, as tests set a scene.
    public const int NoId = -1;

    public int CompareTo(ThingRef o) =>
        Tile.CompareTo(o.Tile) is int c and not 0 ? c : Kind.CompareTo(o.Kind) is int k and not 0 ? k : Index.CompareTo(o.Index);

    /// The desire path along the edge leaving from this way, in its one canonical form: named from
    /// the edge's west or north end, so both ends of an edge give the same ref.
    public static ThingRef Trail(TileId from, Direction dir) => dir is Direction.West or Direction.North
        ? new(ThingKind.Track, Map.Step(from, dir), TrailIndexBase + (int)Opposite(dir))
        : new(ThingKind.Track, from, TrailIndexBase + (int)dir);

    static Direction Opposite(Direction d) => d switch
    {
        Direction.North => Direction.South, Direction.South => Direction.North,
        Direction.East => Direction.West, Direction.West => Direction.East,
    };

    /// The direction of the desire path this names, or null for prints (or any other kind).
    public Direction? TrailDirection => Kind == ThingKind.Track && Index >= TrailIndexBase ? (Direction)(Index - TrailIndexBase) : null;

    public override string ToString() => $"{Kind} {TileIdConverter.Format(Tile)}#{Index}";
}

/// One hop of cause (emergent-world-design §3.2), saved on things that persist: who and how, as a
/// label. Who may name an agent that has died or left the world: it is never dereferenced as live.
public readonly record struct Cause(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Who,
    string What)
{
    public override string ToString() => Who is AgentId who ? $"{What} ({who})" : What;

    public const string Natural = "natural";

    /// How someone or something reads as a cause, from what it is now: "the player", "a person",
    /// "a wolf pack", "a bear".
    public static string Describe(World world, AgentId who)
    {
        if (who == AgentId.Player) return "the player";
        if (!world.Agents.TryGetValue(who, out var a)) return "someone";
        if (a.Mind is PersonMind) return "a person";
        string species = a.Species.ToString().ToLowerInvariant();
        if (a.Members.Count <= 1) return $"a {species}";
        return Fauna.BehaviorOf(a.Species) == HerdBehavior.Prey ? $"a {species} herd" : $"a {species} pack";
    }

    /// Who did it, described as they are now.
    public static Cause By(World world, AgentId who) => new(who, Describe(world, who));
}

/// What is on a tile, as ThingRefs. Reads only: nothing here writes to the world.
public static class Things
{
    /// Every thing on the tile, sorted by kind and index: agents and bodies standing there, carcasses,
    /// every pool generation put there (hidden ones too: this is the world, not anyone's knowledge),
    /// the stash, fire pits, the shelter, readable prints and the visible desire paths named from
    /// this tile (those running east and south: Trail's canonical form).
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

    /// The agent as a thing: Agent while any member lives, Body once all are dead.
    public static ThingRef OfAgent(World world, AgentId id)
    {
        var a = world.Agent(id);
        return new(Sim.IsAlive(a) ? ThingKind.Agent : ThingKind.Body, a.Tile, id.Value);
    }

    /// Whether the thing is in the world now, where the ref says it is.
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
            ThingKind.Sign => false, // no stored signs yet: nothing to point at
            ThingKind.Item => false, // carried gear has no id yet: nothing to point at
        };
    }

    /// Throws unless the thing is there: callers resolve a thing (with their own refusal) before
    /// asking anything of it.
    internal static void Require(World world, ThingRef t)
    {
        if (!Exists(world, t)) throw new ArgumentException($"No {t} in the world", nameof(t));
    }

    /// The stash on the tile as a thing, or null: an empty pile is no stash.
    public static ThingRef? StashAt(World world, TileId tile) =>
        world.Tiles.TryGetValue(tile, out var state) ? StashOf(state, tile) : null;

    static ThingRef? StashOf(TileState state, TileId tile) =>
        state.Stash is { } s && (s.Kg.Count > 0 || s.Gear.Count > 0) ? new(ThingKind.Stash, tile, state.StashId ?? ThingRef.NoId) : null;

    /// The shelter standing on the tile as a thing (as Shelters.Peek shows it), or null.
    public static ThingRef? ShelterAt(World world, TileId tile) =>
        Shelters.Peek(world, tile) is { } s ? new(ThingKind.Shelter, tile, s.Id) : null;

    /// The desire path from tile one step this way, if the neighbour is on the map.
    internal static TrailTier TrailTo(World world, TileId tile, Direction dir)
    {
        var to = Map.Step(tile, dir);
        // Most edges were never walked: no wear to read.
        return Map.Of(world).InBounds(to) && world.Trails.ContainsKey(new EdgeId(tile, to)) ? Tracks.TierOf(world, tile, to) : TrailTier.None;
    }
}
