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
    /// RNG streams by name: world-making systems by system name, everything an agent rolls by system
    /// and agent (Rng(system, who)). Lives here so a load resumes every stream exactly.
    public SortedDictionary<StreamName, Rng> Rngs { get; init; } = [];
    /// Fire pits and carried torches. Owned by the Fire system.
    public SortedDictionary<FireId, FireState> Fires { get; init; } = [];
    public int NextFireId { get; set; }
    /// Ids for agents made after the start (herds, split-off members). The player is 0.
    public int NextAgentId { get; set; } = 1;
    /// Fights under way. Owned by Combat; a fight stays here from Combat.Create until its aftermath.
    public List<Encounter> Encounters { get; init; } = [];
    /// Desire-path wear per edge, only edges something has walked. Owned by Tracks.
    public SortedDictionary<EdgeId, TrailWear> Trails { get; init; } = [];
    /// The clock hour of the last sweep of faded tracks. Owned by Tracks.
    public long TracksSweptHour { get; set; } = -1;
    public int NextCarcassId { get; set; }
    /// Shelters standing, natural or built, by tile. Owned by Shelters.
    public SortedDictionary<TileId, ShelterState> Shelters { get; init; } = [];
    /// Bedding laid down, by tile. Owned by Shelters.
    public SortedDictionary<TileId, BeddingKind> Beddings { get; init; } = [];
    /// Building under way, one per tile. Owned by Shelters.
    public SortedDictionary<TileId, Project> Projects { get; init; } = [];
    /// A shelter find that was found while the tile was taken, by tile: the index of its pool
    /// (Pools.At). It stands once the tile is free. Owned by Shelters.
    public SortedDictionary<TileId, int> WaitingShelters { get; init; } = [];
    /// The pools generation put on each tile looked at so far; derived from Seed, never saved. Owned by Pools.
    internal Dictionary<TileId, IReadOnlyList<PoolDef>>? PoolCache;

    public static World New(int seed)
    {
        var map = Map.Generate(seed);
        var world = new World { Seed = seed, GeneratedMap = map };
        world.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = map.Camp };
        // The old camp's Overhang and bedding (old GridWorldGenerator.CreateCampLocation).
        Survival.Shelters.Place(world, map.Camp, Survival.Shelters.CampOverhang);
        Survival.Shelters.PlaceBedding(world, map.Camp, BeddingKind.Camp);
        Map.PlaceSiteShelters(world);
        Fauna.Populate(world);
        return world;
    }

    public Agent Agent(AgentId id) =>
        Agents.TryGetValue(id, out var agent) ? agent : throw new KeyNotFoundException($"No agent {id} in world");

    /// The named system's stream, created from Seed on first use. For making the world; what an
    /// agent rolls comes from its own stream.
    public Rng Rng(StreamName system)
    {
        if (!Rngs.TryGetValue(system, out var rng))
            Rngs[system] = rng = Survival.Rng.ForSystem(Seed, system.Value);
        return rng;
    }

    /// This agent's own stream for the system, so one agent's rolls never hang on another's: a herd
    /// across the valley bleeding, fighting or wandering leaves the player's injuries as they were.
    public Rng Rng(string system, AgentId who) => Rng(StreamName.Of(system, who));

    /// A stream used once, as an agent is made (its bodies, its temper): derived from Seed and the
    /// agent, never saved, since no id is made twice. Named apart from the agent's own streams.
    public Rng Birth(string system, AgentId who) => Survival.Rng.ForSystem(Seed, "born " + StreamName.Of(system, who).Value);
}
