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

    /// The pieces today's recipes were written in (recipes.json's header); a resource left out is its range's middle.
    static readonly Dictionary<Resource, double> NominalKg = new()
    {
        [Resource.Stone] = 0.3,
        [Resource.Flint] = 0.3,
        [Resource.Pyrite] = 0.1,
        [Resource.Bone] = 0.3,
        [Resource.Stick] = 0.3,
        [Resource.PineBranch] = 2.0,
        [Resource.BirchBranch] = 2.0,
        [Resource.OakBranch] = 2.0,
        [Resource.PineFirewood] = 2.0,
        [Resource.BirchFirewood] = 2.0,
        [Resource.OakFirewood] = 2.0,
        [Resource.Sinew] = 0.1,
        [Resource.Rope] = 0.2,
        [Resource.Hide] = 1.0,
        [Resource.ScrapedHide] = 0.8,
        [Resource.CuredHide] = 0.7,
        [Resource.HideStrip] = 0.3,
        [Resource.MammothHide] = 10,
        [Resource.ScrapedMammothHide] = 8,
        [Resource.CuredMammothHide] = 7.2,
        [Resource.RawMeat] = 0.5,
        [Resource.CookedMeat] = 0.5,
        [Resource.DriedMeat] = 0.5,
        [Resource.RawFish] = 0.5,
        [Resource.CookedFish] = 0.5,
        [Resource.DriedFish] = 0.5,
        [Resource.RawFat] = 0.5,
    };

    /// Kilos of r for setting a scene: an amount of a continuous resource, or as many nominal pieces of a discrete one
    /// as make those kilos, split evenly (each inside the kind's range; less than the smallest piece gives nothing).
    public static void Give(Inventory inv, Resource r, double kg)
    {
        if (!Items.IsDiscrete(r)) { Items.Add(inv, r, kg); return; }
        if (kg <= 0) return;
        var def = Items.Resources[r];
        double nominal = NominalKg.GetValueOrDefault(r, (def.MinPieceKg + def.MaxPieceKg) / 2);
        int n = Math.Max(1, (int)Math.Round(kg / nominal, MidpointRounding.AwayFromZero));
        while (kg / n > def.MaxPieceKg) n++;
        while (n > 1 && kg / n < def.MinPieceKg) n--;
        if (kg / n < def.MinPieceKg) return; // less than one piece is none (the property tests draw any kilos)
        for (int i = 0; i < n; i++) Items.AddPiece(inv, r, kg / n);
    }

    /// count nominal pieces of a discrete r.
    public static void GivePieces(Inventory inv, Resource r, int count)
    {
        var def = Items.Resources[r];
        double nominal = Math.Clamp(NominalKg.GetValueOrDefault(r, (def.MinPieceKg + def.MaxPieceKg) / 2), def.MinPieceKg, def.MaxPieceKg);
        for (int i = 0; i < count; i++) Items.AddPiece(inv, r, nominal);
    }

    /// What a recipe input asks for, of one of its alternatives.
    public static void GiveFor(Inventory inv, Resource r, MaterialInput m)
    {
        if (Items.IsDiscrete(r)) GivePieces(inv, r, m.Count);
        else Items.Add(inv, r, m.Kg);
    }

    /// Whether the pack holds enough for the input, in either of its forms.
    public static bool Enough(Inventory inv, MaterialInput m) =>
        m.Discrete().Any() && m.Discrete().Sum(x => Items.Count(inv, x)) >= m.Count
        || m.Continuous().Any() && m.Continuous().Sum(x => Items.Have(inv, x)) >= m.Kg - 1e-9;

    /// Every resource gone, gear kept.
    public static void ClearResources(Inventory inv)
    {
        inv.Amounts.Clear();
        inv.Pieces.Clear();
    }

    public static void Remove(Inventory inv, Resource r)
    {
        inv.Amounts.Remove(r);
        inv.Pieces.Remove(r);
    }

    public static World Supply(World world, AgentId? who = null)
    {
        var pack = Items.Pack(world, who ?? AgentId.Player);
        pack.Gear.Add(Items.Make("fire_striker"));
        for (int i = 0; i < 3; i++) pack.Gear.Add(Items.Make("torch"));
        foreach (var (r, kg) in new[] { (Resource.BirchBark, 1.0), (Resource.Stick, 10.0), (Resource.BirchBranch, 10.0), (Resource.PineBranch, 5.0), (Resource.Bone, 2.0) })
            Give(pack, r, kg);
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
