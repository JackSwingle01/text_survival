namespace Survival;

/// The whole game state and the save format: public members are saved, internal ones are caches derived
/// from Seed or the sky (the serializer skips them). Entities refer to each other by ID.
public sealed class World
{
    public required int Seed { get; init; }
    /// Tests pin the sky here; null (the game) means Weather.At derives it. Fixed before the clock starts:
    /// Ground and Tracks derive the past from it. Declared before Minute so a load sets it while Minute is 0.
    public WeatherSample? FixedWeather
    {
        get;
        set
        {
            if (Minute != 0 && !Nullable.Equals(field, value))
                throw new InvalidOperationException($"FixedWeather changed at minute {Minute}: the sky is pinned before the clock starts");
            if (!Nullable.Equals(field, value)) (DecayCache, GroundCache, SurfaceCache) = (null, null, null);
            field = value;
        }
    }
    public long Minute { get; set; }
    internal WeatherTimeline? WeatherCache;
    /// Owned by Tracks.
    internal SkyTimeline? ErosionCache, WeatheringCache;
    /// Owned by Tracks.
    internal SkyTimeline? SnowfallCache;
    internal GeneratedMap? GeneratedMap;
    // Sorted: a Dictionary's order after Remove-then-Add differs from freshly loaded, so a loaded save would diverge.
    public SortedDictionary<TileId, TileState> Tiles { get; init; } = []; // only tiles that differ from generation
    public SortedDictionary<AgentId, Agent> Agents { get; init; } = [];
    public SortedDictionary<StreamName, Rng> Rngs { get; init; } = [];
    /// Owned by Fire.
    public SortedDictionary<FireId, FireState> Fires { get; init; } = [];
    public int NextFireId { get; set; }
    public int NextAgentId { get; set; } = 1;
    /// Owned by Combat; a fight stays here from Combat.Create until its aftermath.
    public List<Encounter> Encounters { get; init; } = [];
    /// Each edge's trodden amount, only on edges something has walked. Owned by Tracks.
    public SortedDictionary<EdgeId, EdgeTrodden> Trails { get; init; } = [];
    /// The clock hour of the last sweep of faded tracks. Owned by Tracks.
    public long TracksSweptHour { get; set; } = -1;
    public int NextCarcassId { get; set; }
    /// Never reused, so a ref held to one never points at a later one on the same tile (LocationFeature). Owned by Items and Shelters.
    public int NextPileId { get; set; }
    public int NextShelterId { get; set; }
    /// Owned by Shelters.
    public SortedDictionary<TileId, ShelterState> Shelters { get; init; } = [];
    /// Owned by Shelters.
    public SortedDictionary<TileId, BeddingKind> Beddings { get; init; } = [];
    /// Placed recipes' unfinished work. Owned by Making.
    public SortedDictionary<TileId, TileProject> Projects { get; init; } = [];
    /// Shelter finds waiting for a taken tile: the index of the pool (Pools.At). Owned by Shelters.
    public SortedDictionary<TileId, int> WaitingShelters { get; init; } = [];
    /// Owned by Journal.
    public SortedDictionary<TileId, List<WorldEvent>> Journal { get; init; } = [];
    /// The tutorials the player has been shown (Tutorials.Show): each once a game.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedSet<Tutorial>? Tutorials { get; set; }
    /// Owned by Pools.
    internal Dictionary<TileId, IReadOnlyList<PoolDef>>? PoolCache;
    /// The access conditions in force; tests switch on the unwired ones to show every refusal asks Access.
    internal Access.Gate AccessGates = Access.Wired;
    internal Dictionary<double, DecayTimeline>? DecayCache;
    internal Dictionary<GroundClass, GroundState>? GroundCache;
    internal (long Minute, Dictionary<GroundClass, Surface> ByClass)? SurfaceCache;
    /// The stored-plan benchmark's other arm: animals plan again at every decision (Fauna.Needs).
    internal bool ReplanEveryDecision;
    internal Dictionary<Diet, double>? ValleyForageCache;
    internal List<TileId>? OpenLandCache;
    internal double? MeanForageDensityCache;
    internal SortedSet<TileId>? CarcassTiles;
    internal NearbyIndex? Nearby;
    /// Owned by Trip.Routes: one search's working state, reused by the next.
    internal Trip.RouteScratch? RouteScratch;
    internal (long Minute, Dictionary<(AgentId Who, TileId From, TileId To, Pace Pace), (int Minutes, double EncumbrancePct, double Speed)> Minutes)? StepMinutesCache;

    public static World New(int seed)
    {
        var map = Map.Generate(seed);
        var world = new World { Seed = seed, GeneratedMap = map };
        world.Agents[AgentId.Player] = new Agent
        {
            Id = AgentId.Player,
            Tile = map.Camp,
            Members = [SpeciesTable.NewBody(Species.Human, world.Birth("person", AgentId.Player))],
        };
        Survival.Shelters.Place(world, map.Camp, Survival.Shelters.CampOverhang);
        Survival.Shelters.PlaceBedding(world, map.Camp, BeddingKind.Camp);
        Map.PlaceSiteShelters(world);
        Fauna.Populate(world);
        // The map opens on what is in view, not on nothing. A burning fire placed by generation would report events here, and this throws to say so.
        var events = new List<WorldEvent>();
        Memories.Glance(world, world.Agents[AgentId.Player], events);
        if (events.Count > 0) throw new InvalidOperationException($"The first look at a new world reported {events.Count} events");
        return world;
    }

    public Agent Agent(AgentId id) =>
        Agents.TryGetValue(id, out var agent) ? agent : throw new KeyNotFoundException($"No agent {id} in world");

    /// The named system's stream, created from Seed on first use; for making the world.
    public Rng Rng(StreamName system)
    {
        if (!Rngs.TryGetValue(system, out var rng))
            Rngs[system] = rng = Survival.Rng.ForSystem(Seed, system.Value);
        return rng;
    }

    /// Own stream per agent, so one agent's rolls never hang on another's (a far herd fighting leaves the player's injuries as they were).
    public Rng Rng(string system, AgentId who) =>
        Agents.ContainsKey(who) ? Rng(StreamName.Of(system, who)) : throw new InvalidOperationException($"{who} is not in the world to roll on {system}");

    /// Derived from Seed and the agent, never saved, since no id is made twice.
    public Rng Birth(string system, AgentId who) => Survival.Rng.ForSystem(Seed, "born " + StreamName.Of(system, who).Value);
}
