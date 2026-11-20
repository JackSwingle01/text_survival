namespace Survival;

/// The whole game state, and the save format. Plain data: entities refer to each other by ID.
public sealed class World
{
    public required int Seed { get; init; }
    /// Tests pin the sky here; null (the game) means Weather.At derives it from Seed and the minute.
    /// Fixed before the clock starts: Ground and Tracks derive the past from the sky, so changing it
    /// later would rewrite history. Declared before Minute so a load sets it while Minute is still 0.
    public WeatherSample? FixedWeather
    {
        get;
        set
        {
            if (Minute != 0 && !Nullable.Equals(field, value))
                throw new InvalidOperationException($"FixedWeather changed at minute {Minute}: the sky is pinned before the clock starts");
            field = value;
        }
    }
    public long Minute { get; set; }
    /// Derived from Seed alone, so never saved (internal: the serializer skips it).
    internal WeatherTimeline? WeatherCache;
    /// Erosion prefix sums over the seed's weather; derived, never saved. Owned by Tracks.
    internal ErosionTimeline? ErosionCache;
    /// Generated from Seed alone, so never saved (internal: the serializer skips it). See Map.Of.
    internal GeneratedMap? GeneratedMap;
    // Keyed world state is sorted: a Dictionary's order after Remove-then-Add differs from its order
    // freshly loaded, so a save, loaded and run on, came out with the same state in another order.
    /// Only tiles that differ from generation (plan decision 1). Ground and tracks write here.
    public SortedDictionary<TileId, TileState> Tiles { get; init; } = [];
    public SortedDictionary<AgentId, Agent> Agents { get; init; } = [];
    /// One RNG stream per system, keyed by system name. Lives here so a load resumes every stream exactly.
    public Dictionary<string, Rng> Rngs { get; init; } = [];
    /// Fire pits and carried torches. Owned by the Fire system.
    public SortedDictionary<FireId, FireState> Fires { get; init; } = [];
    public int NextFireId { get; set; }
    /// Ids for agents made after the start (herds, split-off members). The player is 0.
    public int NextAgentId { get; set; } = 1;
    /// Fights under way. Owned by Combat; a fight stays here from Combat.Create until its aftermath.
    public List<Encounter> Encounters { get; init; } = [];
    /// Desire-path wear per edge, only edges something has walked. Owned by Tracks.
    public SortedDictionary<EdgeId, TrailWear> Trails { get; init; } = [];
    public int NextCarcassId { get; set; }
    /// The pools generation put on each tile looked at so far; derived from Seed, never saved. Owned by Pools.
    internal Dictionary<TileId, IReadOnlyList<PoolDef>>? PoolCache;

    public static World New(int seed)
    {
        var map = Map.Generate(seed);
        var world = new World { Seed = seed, GeneratedMap = map };
        world.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = map.Camp };
        return world;
    }

    public Agent Agent(AgentId id) =>
        Agents.TryGetValue(id, out var agent) ? agent : throw new KeyNotFoundException($"No agent {id} in world");

    /// The named system's stream, created from Seed on first use.
    public Rng Rng(string system)
    {
        if (!Rngs.TryGetValue(system, out var rng))
            Rngs[system] = rng = Survival.Rng.ForSystem(Seed, system);
        return rng;
    }
}
