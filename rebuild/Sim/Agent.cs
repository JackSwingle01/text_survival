using System.Text.Json.Serialization;

namespace Survival;

/// Kind-specific state lives here, never as optional fields on Agent.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerMind), "player")]
[JsonDerivedType(typeof(NpcMind), "npc")]
[JsonDerivedType(typeof(HerdMind), "herd")]
public abstract record Mind;

/// Shared by the player and NPCs so work, memory and skills run the same code for both.
public abstract record PersonMind : Mind
{
    public Inventory Pack { get; init; } = Items.StartingPack();
    public Memory Memory { get; init; } = new();
    public SortedDictionary<Skill, SkillProgress> Skills { get; init; } = [];
}

public sealed record PlayerMind : PersonMind;

public sealed partial record NpcMind : PersonMind;

public sealed class Agent
{
    public required AgentId Id { get; init; }
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
    [JsonIgnore] internal NearbyIndex? Index { get; set; }
    public Species Species { get; init; } = Species.Human;
    /// A list because a fight names a unit's body by index (Fighter.Member, always 0).
    public List<BodyState> Members { get; init; } = [BodyState.BaselineHuman()];
    public Mind Mind { get; init; } = new PlayerMind();
    [JsonIgnore] public BodyState Body => Members.Single();
    /// The agent stays on Tile until the crossing completes (Travel).
    public Crossing? Crossing { get; set; }
    /// So kin can follow a crossing too short to see under way (Fauna.Following).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Departure? LastLeft { get; set; }
    /// Null for the player, whose activities live inside one Sim.Step, and for an agent about to decide.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Doing? Doing { get; set; }
    [JsonIgnore] internal Activity? Stepping { get; set; }
}

/// A herd is never stored: it is the animals of one species on one tile (Combat.Side).
public static class Agents
{
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

    public static List<AgentId> SpawnGroup(World world, Species species, TileId tile, int count, TileId? den = null)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A group has at least one animal");
        var ids = Enumerable.Range(0, count).Select(_ => Spawn(world, species, tile, den)).ToList();
        Fauna.Acquaint(world, ids);
        return ids;
    }

    /// Drops its rng streams too: ids are never made twice (NewId), so a save need not carry them.
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
