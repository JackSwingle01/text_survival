using System.Text.Json.Serialization;

namespace Survival;

/// Who decides an agent's next activity. Kind-specific state lives here, never as optional fields on Agent.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerMind), "player")]
[JsonDerivedType(typeof(NpcMind), "npc")]
[JsonDerivedType(typeof(HerdMind), "herd")]
public abstract record Mind;

/// A person: carries a pack, remembers, and learns skills. The player and NPCs share this so
/// work, memory and skills run the same code for both. A herd's mind carries none of it.
public abstract record PersonMind : Mind
{
    public Inventory Pack { get; init; } = Items.StartingPack();
    public Memory Memory { get; init; } = new();
    /// Only the skills practised so far. Here because only a person learns.
    public SortedDictionary<Skill, SkillProgress> Skills { get; init; } = [];
}

/// The UI picks the activities.
public sealed record PlayerMind : PersonMind;

/// The NPC utility AI picks the activities (state and decider live in Npc.cs).
public sealed partial record NpcMind : PersonMind;

/// One body that moves and decides: the player, an NPC, an animal.
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
            Index?.Moved(this, from, value);
        }
    }
    /// The Nearby index this agent was last put in; never saved.
    [JsonIgnore] internal NearbyIndex? Index { get; set; }
    public Species Species { get; init; } = Species.Human;
    /// Its one body, in a list because a fight names a unit's body by index (Fighter.Member, always 0).
    public List<BodyState> Members { get; init; } = [BodyState.BaselineHuman()];
    public Mind Mind { get; init; } = new PlayerMind();
    [JsonIgnore] public BodyState Body => Members.Single();
    /// A tile crossing under way; the agent stays on Tile until it completes. Owned by Travel.
    public Crossing? Crossing { get; set; }
    /// An animal's last finished crossing, so kin can follow one too short to see under way (Fauna.Following).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Departure? LastLeft { get; set; }
    /// What an NPC or herd is doing until its mind decides again (Scheduler). Null for the player,
    /// whose activities live inside one Sim.Step, and for an agent about to decide.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Doing? Doing { get; set; }
    [JsonIgnore] internal Activity? Stepping { get; set; }
}

/// Making agents. Every animal is its own agent with one body; a herd is the animals of one species
/// on one tile (Combat.Side), never stored.
public static class Agents
{
    /// One animal of this species on this tile, at its own random size (Species.NewBody). A predator dens at
    /// the den given, by default this tile (Fauna.NewMind).
    public static AgentId Spawn(World world, Species species, TileId tile, TileId? den = null)
    {
        var id = NewId(world);
        var rng = world.Birth("fauna", id);
        world.Agents[id] = new Agent
        {
            Id = id,
            Tile = tile,
            Species = species,
            Mind = Fauna.NewMind(world, species, tile, den),
            Members = [SpeciesTable.NewBody(species, rng)],
        };
        return id;
    }

    /// Animals that come into the world together, in id order: a closed-group species' start familiar with each
    /// other (an existing pack, an arriving one), and all denning at the den given (Spawn).
    public static List<AgentId> SpawnGroup(World world, Species species, TileId tile, int count, TileId? den = null)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A group has at least one animal");
        var ids = Enumerable.Range(0, count).Select(_ => Spawn(world, species, tile, den)).ToList();
        Fauna.Acquaint(world, ids);
        return ids;
    }

    /// The agent leaves the world, and every stream it rolled on with it: ids are never made twice
    /// (NewId), so nothing will roll on those streams again, and a save need not carry them.
    public static void Remove(World world, AgentId id)
    {
        if (!world.Agents.Remove(id)) throw new InvalidOperationException($"No agent {id} to remove");
        Fauna.Forget(world, id);
        foreach (var name in world.Rngs.Keys.Where(k => k.IsFor(id)).ToList()) world.Rngs.Remove(name);
    }

    static AgentId NewId(World world)
    {
        var id = new AgentId(world.NextAgentId++);
        if (world.Agents.ContainsKey(id)) throw new InvalidOperationException($"Agent id {id} is already taken");
        return id;
    }
}
