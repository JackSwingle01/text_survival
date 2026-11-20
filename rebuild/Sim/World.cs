namespace Survival;

/// The whole game state, and the save format. Plain data: entities refer to each other by ID.
public sealed class World
{
    public required int Seed { get; init; }
    public long Minute { get; set; }
    public required Weather Weather { get; set; }
    public required TileId CampTile { get; set; }
    public Dictionary<AgentId, Agent> Agents { get; init; } = [];
    /// One RNG stream per system, keyed by system name. Lives here so a load resumes every stream exactly.
    public Dictionary<string, Rng> Rngs { get; init; } = [];

    public static World New(int seed)
    {
        var camp = new TileId(48, 48); // ponytail: fixed camp until the phase-2 map generator picks one
        var world = new World { Seed = seed, Weather = new Weather(TemperatureC: -10, WindMps: 3), CampTile = camp };
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

/// Placeholder until phase 2 makes weather real.
public sealed record Weather(double TemperatureC, double WindMps);

public sealed class Agent
{
    public required AgentId Id { get; init; }
    public required TileId Tile { get; set; }
    // Placeholder needs; the Body port replaces these.
    public double EnergyPct { get; set; } = 1.0;
    public double CaloriesKcal { get; set; } = 1500;
}
