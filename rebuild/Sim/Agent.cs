using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// Who decides an agent's next activity. Kind-specific state lives here, never as optional fields on Agent.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerMind), "player")]
[JsonDerivedType(typeof(HerdMind), "herd")]
public abstract record Mind;

/// A person: carries a pack and knows what they have found. The player and (phase 3d) NPCs share
/// this, so every work activity runs the same code for both. A herd's mind carries nothing.
public abstract record PersonMind : Mind
{
    public Inventory Pack { get; init; } = Items.StartingPack();
    public Knowledge Knows { get; init; } = new();
}

/// The UI picks the activities.
public sealed record PlayerMind : PersonMind;

/// A herd: carries nothing. Empty until phase 3b gives it territory, state, hunger and fear.
public sealed record HerdMind : Mind;

/// An agent's beliefs about the world (plan decision 7), kept minimal: which hidden pools it has
/// found, and the search effort it has put into the ones it has not found yet.
public sealed class Knowledge
{
    public List<PoolId> Discovered { get; init; } = [];
    /// Perception-weighted hours spent searching for each hidden pool still unfound.
    /// Sorted for the same reason as Inventory.Kg: found pools are removed from it.
    public SortedDictionary<PoolId, double> SearchHours { get; init; } = [];
}

/// One or more bodies that move and decide as a unit: the player, an NPC, a herd (plan decision 2).
public sealed class Agent
{
    public required AgentId Id { get; init; }
    public required TileId Tile { get; set; }
    public Species Species { get; init; } = Species.Human;
    public List<BodyState> Members { get; init; } = [BodyState.BaselineHuman()];
    public Mind Mind { get; init; } = new PlayerMind();
    /// The one body of a one-body agent. Throws for a herd, which has no single body.
    [JsonIgnore] public BodyState Body => Members.Single();
    /// A tile crossing under way; the agent stays on Tile until it completes. Owned by Travel.
    public Crossing? Crossing { get; set; }
}

/// Making and splitting agents. A herd sends members, not itself, into a fight; a wounded member
/// that leaves becomes an agent of its own (plan decision 2).
public static class Agents
{
    /// A new herd of this species on this tile, each member at its own random size (Species.NewBody).
    public static AgentId Spawn(World world, Species species, TileId tile, int members)
    {
        if (members < 1) throw new ArgumentOutOfRangeException(nameof(members), members, "An agent has at least one member");
        var rng = world.Rng("fauna");
        var id = NewId(world);
        world.Agents[id] = new Agent
        {
            Id = id, Tile = tile, Species = species, Mind = new HerdMind(),
            Members = Enumerable.Range(0, members).Select(_ => SpeciesTable.NewBody(species, rng)).ToList(),
        };
        return id;
    }

    /// Moves one member out into a new one-member agent on the same tile and returns its id. A
    /// member that is already alone is already its own agent: its id comes back unchanged.
    /// Refused while the agent has members in a fight: units name members by index.
    public static AgentId SplitOff(World world, AgentId from, int memberIndex)
    {
        var source = world.Agent(from);
        if (memberIndex < 0 || memberIndex >= source.Members.Count)
            throw new ArgumentOutOfRangeException(nameof(memberIndex), memberIndex, $"{from} has {source.Members.Count} members");
        if (world.Encounters.Any(e => e.Units.Any(u => u.Agent == from)))
            throw new InvalidOperationException($"{from} is in a fight; its members cannot be reordered until it ends");
        if (source.Members.Count == 1) return from;
        // A person is one body; splitting a pack would mint what is in it.
        if (source.Mind is PersonMind) throw new InvalidOperationException($"{from} is a person and cannot split");
        var body = source.Members[memberIndex];
        source.Members.RemoveAt(memberIndex);
        var id = NewId(world);
        // Its own copy of what the herd knew, not the herd's: one mind shared by two agents would
        // change both whenever either learned anything, and a save would load them as two.
        var mind = JsonSerializer.Deserialize<Mind>(JsonSerializer.Serialize(source.Mind))!;
        world.Agents[id] = new Agent { Id = id, Tile = source.Tile, Species = source.Species, Mind = mind, Members = [body] };
        return id;
    }

    static AgentId NewId(World world)
    {
        var id = new AgentId(world.NextAgentId++);
        if (world.Agents.ContainsKey(id)) throw new InvalidOperationException($"Agent id {id} is already taken");
        return id;
    }
}
