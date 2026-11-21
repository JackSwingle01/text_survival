using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// Who decides an agent's next activity. Kind-specific state lives here, never as optional fields on Agent.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerMind), "player")]
[JsonDerivedType(typeof(NpcMind), "npc")]
[JsonDerivedType(typeof(HerdMind), "herd")]
public abstract record Mind;

/// A person: carries a pack, knows what they have seen, and gets better at what they do. The
/// player and NPCs share this, so every work activity, memory and skill runs the same code for
/// both. A herd's mind carries nothing.
public abstract record PersonMind : Mind
{
    public Inventory Pack { get; init; } = Items.StartingPack();
    public Beliefs Knows { get; init; } = new();
    /// Only the skills practised so far (Skills). They live here because only a person learns, and
    /// kind-specific state lives in the mind.
    public SortedDictionary<Skill, SkillProgress> Skills { get; init; } = [];
}

/// The UI picks the activities.
public sealed record PlayerMind : PersonMind;

/// The NPC utility AI picks the activities (state and decider live in Npc.cs).
public sealed partial record NpcMind : PersonMind;

/// One or more bodies that move and decide as a unit: the player, an NPC, a herd.
public sealed class Agent
{
    public required AgentId Id { get; init; }
    /// Where it stands. A move is reported to the Nearby index that holds it, so the index stays exact.
    public required TileId Tile
    {
        get;
        set
        {
            if (field == value) return;
            var from = field;
            field = value;
            Index?.Moved(Id, from, value);
        }
    }
    /// The Nearby index this agent was last put in; never saved.
    [JsonIgnore] internal NearbyIndex? Index { get; set; }
    public Species Species { get; init; } = Species.Human;
    public List<BodyState> Members { get; init; } = [BodyState.BaselineHuman()];
    public Mind Mind { get; init; } = new PlayerMind();
    /// The one body of a one-body agent. Throws for a herd, which has no single body.
    [JsonIgnore] public BodyState Body => Members.Single();
    /// A tile crossing under way; the agent stays on Tile until it completes. Owned by Travel.
    public Crossing? Crossing { get; set; }
    /// What an NPC or herd is doing until its mind decides again (Scheduler). Null for the player,
    /// whose activities live inside one Sim.Step, and for an agent about to decide.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Doing? Doing { get; set; }
}

/// Making and splitting agents. A herd sends members, not itself, into a fight; a wounded member
/// that leaves becomes an agent of its own.
public static class Agents
{
    /// A new herd of this species on this tile, each member at its own random size (Species.NewBody).
    /// Its territory is the tiles it patrols and grazes, its den first; by default just this tile.
    public static AgentId Spawn(World world, Species species, TileId tile, int members, List<TileId>? territory = null)
    {
        if (members < 1) throw new ArgumentOutOfRangeException(nameof(members), members, "An agent has at least one member");
        var id = NewId(world);
        var rng = world.Birth("fauna", id);
        world.Agents[id] = new Agent
        {
            Id = id, Tile = tile, Species = species, Mind = Fauna.NewMind(world, id, species, territory ?? [tile]),
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
        if (Combat.EncounterOf(world, from) != null)
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

    /// The agent leaves the world, and every stream it rolled on with it: ids are never made twice
    /// (NewId), so nothing will roll on those streams again, and a save need not carry them.
    public static void Remove(World world, AgentId id)
    {
        if (!world.Agents.Remove(id)) throw new InvalidOperationException($"No agent {id} to remove");
        foreach (var name in world.Rngs.Keys.Where(k => k.IsFor(id)).ToList()) world.Rngs.Remove(name);
    }

    static AgentId NewId(World world)
    {
        var id = new AgentId(world.NextAgentId++);
        if (world.Agents.ContainsKey(id)) throw new InvalidOperationException($"Agent id {id} is already taken");
        return id;
    }
}
