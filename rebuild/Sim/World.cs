namespace Survival;

/// The whole game state, and the save format. Plain data: entities refer to each other by ID.
public sealed class World
{
    public required int Seed { get; init; }
    public long Minute { get; set; }
    /// Tests pin the sky here; null (the game) means Weather.At derives it from Seed and the minute.
    public WeatherSample? FixedWeather { get; set; }
    /// Derived from Seed alone, so never saved (internal: the serializer skips it).
    internal WeatherTimeline? WeatherCache;
    public required TileId CampTile { get; set; }
    /// Generated from Seed alone, so never saved (internal: the serializer skips it). See Map.Of.
    internal GeneratedMap? GeneratedMap;
    /// Only tiles that differ from generation (plan decision 1). Ground and tracks write here.
    public Dictionary<TileId, TileState> Tiles { get; init; } = [];
    public Dictionary<AgentId, Agent> Agents { get; init; } = [];
    /// One RNG stream per system, keyed by system name. Lives here so a load resumes every stream exactly.
    public Dictionary<string, Rng> Rngs { get; init; } = [];
    /// Fire pits and carried torches. Owned by the Fire system.
    public Dictionary<FireId, FireState> Fires { get; init; } = [];
    public int NextFireId { get; set; }

    public static World New(int seed)
    {
        var map = Map.Generate(seed);
        var camp = map.Camp;
        var world = new World { Seed = seed, CampTile = camp, GeneratedMap = map };
        world.Agents[AgentId.Player] = new Agent { Id = AgentId.Player, Tile = camp };
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
