using Survival;

/// Gives an agent the gear and fuel the fire tests used to take for granted.
static class TestKit
{
    /// An NPC at the lean 12% baseline build, its fat target matched to it: the NPC food scenarios were
    /// written against that body, before people started the run well fed for the autumn.
    public static void Lean(World world, AgentId npc)
    {
        var agent = world.Agent(npc);
        agent.Members[0] = BodyState.BaselineHuman();
        ((NpcMind)agent.Mind).TargetFatKg = agent.Body.FatKg;
    }

    /// Changes the ground a tile reads from now on. Nothing stores ground per tile, so this changes its whole ground
    /// class (Ground.SetClassState): every tile sharing it reads the change.
    public static void EditGround(World world, TileId tile, Action<GroundState, SubstrateProfile> edit)
    {
        var state = Ground.At(world, tile);
        edit(state, Ground.Substrate(Map.Of(world), tile));
        Ground.SetClassState(world, tile, state);
    }

    /// 15 January, 09:00: a minute when the lakes and rivers are ice (Map.IsFrozen). World minute 0, 1 July, is open water.
    public const long JanuaryMinute = 198 * 1440;

    /// Sets a herd's hunger (Fauna.Hunger) by emptying each member's calorie store that far.
    public static void SetHunger(Agent herd, double hunger)
    {
        foreach (var b in herd.Members) b.CaloriesKcal = Body.CaloriesCapacityKcal(b) * (1 - hunger);
    }

    /// The old game's baseline adult (75 kg, 15% fat, 30% muscle) in the starting clothes: the body
    /// parity goldens were measured on. The rebuild's baseline is the species round's 68 kg man.
    public static BodyState OldBaselineHuman()
    {
        var b = BodyState.Create(BodyKind.Human, 75, 0.15, 0.30);
        b.Worn.AddRange(Items.StartingClothes());
        return b;
    }

    public static World Supply(World world, AgentId? who = null)
    {
        var pack = Items.Pack(world, who ?? AgentId.Player);
        pack.Gear.Add(Items.Make("fire_striker"));
        for (int i = 0; i < 3; i++) pack.Gear.Add(Items.Make("torch"));
        foreach (var (r, kg) in new[] { (Resource.BirchBark, 1.0), (Resource.Kindling, 10.0), (Resource.Birch, 10.0), (Resource.Pine, 5.0), (Resource.Bone, 2.0) })
            Items.Add(pack, r, kg);
        return world;
    }
}

/// A warm-up world several tests build alike, built once per test run (even under parallel tests) and
/// handed to each test as its own save/load copy, so one test's changes never reach another. The copy loads
/// through TestWorlds.Load, onto the seed's shared map.
sealed class SharedWorld(Func<World> build)
{
    readonly Lazy<string> json = new(() => Save.ToJson(build()));

    public World Copy() => TestWorlds.Load(json.Value);
}

/// Animals a test spawned together, in id order. Where one id is wanted, they stand for the first.
readonly record struct Animals(List<AgentId> Ids)
{
    public AgentId this[int i] => Ids[i];
    public int Count => Ids.Count;
    public static implicit operator AgentId(Animals a) => a.Ids[0];
    public static Animals Spawn(World world, Species species, TileId tile, int count) =>
        new(Agents.SpawnGroup(world, species, tile, count));
    /// How many of them are still in the world.
    public int Living(World world) => Ids.Count(world.Agents.ContainsKey);
    public IEnumerable<Agent> In(World world) => Ids.Where(world.Agents.ContainsKey).Select(world.Agent);
}
