using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 1: items are neither made nor lost. Across any run of the player's activities, with a dead
/// person scattering beside them, the kilograms of each resource held as items (ConservationWorlds.ItemKg)
/// change by exactly what the declared sources and sinks say, step by step:
///
///   Δ items[r] = butchered[r] + melted[r] + charcoal[r] + cooked[r]
///              - eaten[r] - drunk[r] - put on a fire[r] - tinder spent on a failed strike[r]
///              - raw cooked[r] - cut pieces weathered away with their carcass[r]
///
/// Every term is read off something other than the inventories: events (Ate, Drank, FuelAdded,
/// FireStartFailed), the carcasses' own YieldKg, the fire's charcoal, the pit's room. Moves (put down,
/// pick up, a full pack dropping to the ground, cut pieces waiting on a carcass, wearing, a skeleton
/// scattering into a pile) have no term: they must net to zero. Gear by kind is constant.
/// Raids and work-pool gathering are separate properties below.
public class ConservationItemsTests
{
    static readonly Resource[] PackResources =
        [Resource.RawMeat, Resource.RawFish, Resource.Berries, Resource.Water, Resource.BirchBranch, Resource.PineBranch, Resource.Stick, Resource.Tinder, Resource.Stone, Resource.Hide];
    static readonly Resource[] Foods = [Resource.RawMeat, Resource.RawFish, Resource.Berries];
    static readonly Resource[] Fuels = [Resource.BirchBranch, Resource.PineBranch, Resource.Stick, Resource.Tinder];
    static readonly Species[] Prey = [Species.Caribou, Species.Hare, Species.Fox, Species.Wolf, Species.Bison];

    public enum OpKind { PutDown, PickUp, Eat, Drink, Butcher, AddFuel, StartFire, Wear, Wait, GatherCharcoal }

    public sealed record Op(OpKind Kind, int Res, double Kg, int Minutes, int Mode);

    /// The scene: sky, what the player carries and has put down, a carcass, a fire, a dead person.
    public sealed record Scene(
        double TempC, double[] PackKg, double[] PileKg, bool Knife, int CarcassSpecies, int CarcassSeed,
        int CarcassAgeMinutes, double PitBirchKg, int DeadDays, Op[] Ops);

    static readonly Gen<Op> OpGen = Gen.Select(
        Gen.Enum<OpKind>(), Gen.Int[0, 9], Gen.Double[0.01, 4], Gen.Int[1, 90], Gen.Int[0, 3],
        (k, r, kg, m, mode) => new Op(k, r, kg, m, mode));

    static readonly Gen<Scene> Scenes = Gen.Select(
        Gen.Double[-15, 18],
        Gen.Frequency((1, Gen.Const(0.0)), (3, Gen.Double[0, 2.5])).Array[PackResources.Length],
        Gen.Frequency((2, Gen.Const(0.0)), (1, Gen.Double[0, 3])).Array[PackResources.Length],
        Gen.Bool,
        Gen.Int[-1, Prey.Length - 1],
        Gen.Int[0, 1000],
        Gen.Int[0, 4 * 1440],
        Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0.5, 8])),
        Gen.Frequency((2, Gen.Const(0)), (1, Gen.Int[3, 25])),
        OpGen.Array[1, 25],
        (t, pack, pile, knife, sp, seed, age, pit, dead, ops) => new Scene(t, pack, pile, knife, sp, seed, age, pit, dead, ops));

    static World Build(Scene s)
    {
        var w = Bare(s.TempC);
        var camp = Camp(w);
        var pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        for (int i = 0; i < PackResources.Length; i++) if (s.PackKg[i] > 0) TestKit.Give(pack, PackResources[i], s.PackKg[i]);
        pack.Gear.Add(Items.Make("fire_striker"));
        if (s.Knife) pack.Gear.Add(Items.Make("flint_knife"));
        // A spare garment for Wear to swap with what is worn.
        pack.Gear.Add(Items.Make(Items.GearKinds.First(kv => kv.Value.Slot != null).Key));
        for (int i = 0; i < PackResources.Length; i++)
            if (s.PileKg[i] > 0) TestKit.Give(Items.PileAt(w, camp, new Cause(P, "put down by the player")), PackResources[i], s.PileKg[i]);

        long start = Math.Max(s.CarcassAgeMinutes, s.DeadDays * 1440L);
        if (s.DeadDays > 0)
        {
            // A person dead this many days, on the next tile over: scatters into a pile when their time comes.
            var npc = Npc.Spawn(w, Map.Step(camp, Direction.East));
            var body = w.Agent(npc).Body;
            body.BloodPct = 0;
            Body.MarkDead(body, start - s.DeadDays * 1440L, new Cause(null, "the cold"));
        }
        w.Minute = start;
        if (s.CarcassSpecies >= 0)
        {
            var species = Prey[s.CarcassSpecies];
            Carcasses.Create(w, camp, species.ToString(), SpeciesTable.NewBody(species, Survival.Rng.ForSystem(s.CarcassSeed, "carcass")),
                w.Minute - s.CarcassAgeMinutes);
        }
        if (s.PitBirchKg > 0)
            w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.BirchBranch] = s.PitBirchKg } };
        return w;
    }

    static Activity ToActivity(World w, Op op)
    {
        var pack = Items.Pack(w, P);
        var r = PackResources[op.Res];
        return op.Kind switch
        {
            OpKind.PutDown => new PutDown(P, r, op.Kg),
            OpKind.PickUp => new PickUp(P, r, op.Kg),
            OpKind.Eat => new Eat(P, Foods[op.Res % Foods.Length], op.Kg),
            OpKind.Drink => new Drink(P, Math.Clamp(op.Kg / 4, 0.05, 1)),
            OpKind.Butcher => w.Tiles.GetValueOrDefault(Camp(w))?.Carcasses?.FirstOrDefault() is { } c
                ? new Butcher(P, c.Id, (ButcherMode)(op.Mode % 3)) : new Wait(P),
            OpKind.AddFuel => new AddFuel(P, Fuels[op.Res % Fuels.Length], op.Kg),
            OpKind.StartFire => new StartFire(P, ToolType.FireStriker, Resource.Tinder, Math.Min(0.05, op.Kg / 40), op.Kg / 4, (PitType)(op.Mode % 3)),
            OpKind.Wear => pack.Gear.FirstOrDefault(g => Items.Def(g).Slot != null) is { } g ? new Wear(P, g.Kind) : new Wait(P),
            OpKind.Wait => new Wait(P),
            OpKind.GatherCharcoal => new GatherCharcoal(P),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    /// What one step should have done to the items, from everything but the items.
    static SortedDictionary<Resource, double> Declared(World w, Activity a, StepResult step, Before before,
        SortedDictionary<Resource, double> itemsAfter, List<string> notes)
    {
        var d = new SortedDictionary<Resource, double>();
        foreach (var e in step.Events)
            switch (e)
            {
                case Ate ate when ate.Who == P: AddTo(d, ate.Food, -ate.Kg); break;
                case Drank drank when drank.Who == P && before.PackWaterKg > 0: AddTo(d, Resource.Water, -drank.Liters); break;
                case FuelAdded fuel when fuel.By == P: AddTo(d, fuel.Fuel, -fuel.Kg); break;
                case FireStartFailed failed when failed.Who == P && a is StartFire sf: AddTo(d, sf.Tinder, -sf.TinderKg); break;
            }
        if (a is StartFire start && step.Events.Any(e => e is FireLit lit && lit.By == P))
        {
            // Into the pit what it has room for; the rest stays in the pack (Fire.Start).
            double max = Fire.MaxFuelKg(start.Pit), t0 = before.PitTotalKg;
            double tinder = t0 < max ? Math.Min(start.TinderKg, max - t0) : 0;
            // Kindling in whole sticks, as many as the pit has room for (Items.Move).
            double kindling = start.KindlingKg > 0 && t0 + tinder < max ? Items.WouldMoveKg(before.Pack, Resource.Stick, start.KindlingKg, max - t0 - tinder) : 0;
            AddTo(d, start.Tinder, -tinder);
            AddTo(d, Resource.Stick, -kindling);
        }
        if (a is GatherCharcoal && step.MinutesElapsed > 0) AddTo(d, Resource.Charcoal, before.PitCharcoalKg);
        foreach (var (c, yieldBefore) in before.Carcasses)
        {
            foreach (var r in c.YieldKg.Keys.Union(yieldBefore.Keys))
                AddTo(d, r, c.YieldKg.GetValueOrDefault(r) - yieldBefore.GetValueOrDefault(r));
            bool inWorld = w.Tiles.GetValueOrDefault(before.CarcassTile[c])?.Carcasses?.Contains(c) == true;
            if (!inWorld && c.Pending.Count > 0)
            {
                // Gone with its carcass: only bones weathering away may take cut pieces with them.
                double hours = c.EffectiveHours + Remains.EffectiveHours(w, before.CarcassTile[c], c.LastUpdatedMinute, w.Minute);
                if (hours < Remains.GoneHours - 1)
                    notes.Add($"carcass {c.Id} left the world at {hours:0.#} effective hours with {Show(c.Pending)} pending");
                foreach (var (r, kg) in c.Pending) AddTo(d, r, -kg);
            }
        }
        return d;
    }

    sealed record Before(SortedDictionary<Resource, double> Items, SortedDictionary<string, int> Gear, double PackWaterKg,
        double PitTotalKg, double PitCharcoalKg, List<(Carcass C, Dictionary<Resource, double> Yield)> Carcasses,
        Dictionary<Carcass, TileId> CarcassTile, Inventory Pack);

    static Before Snapshot(World w)
    {
        var player = w.Agent(P);
        Fire.CatchUpAround(w, player, []); // what Sim.Start would see: the pit as of now
        var pit = Fire.PitAt(w, player.Tile) is FireId id ? w.Fires[id] : null;
        var carcasses = new List<(Carcass, Dictionary<Resource, double>)>();
        var tiles = new Dictionary<Carcass, TileId>();
        foreach (var (tile, ts) in w.Tiles)
            foreach (var c in ts.Carcasses ?? [])
            {
                carcasses.Add((c, new Dictionary<Resource, double>(c.YieldKg)));
                tiles[c] = tile;
            }
        return new Before(ItemKg(w), GearCount(w), Items.Have(Items.Pack(w, P), Resource.Water),
            pit == null ? 0 : Fire.TotalKg(pit), pit?.CharcoalKg ?? 0, carcasses, tiles, Items.CopyResources(Items.Pack(w, P)));
    }

    /// Floating-point summation, and one Items.SliverKg a strike: Fire.Start takes a last sliver of
    /// tinder with what was asked, and this ledger declares what was asked.
    const double Tolerance = Items.SliverKg + 1e-11;

    static void Run(Scene s)
    {
        var w = Build(s);
        var player = w.Agent(P);
        var notes = new List<string>();
        for (int i = 0; i < s.Ops.Length; i++)
        {
            var op = s.Ops[i];
            TopUp(player.Body);
            var a = ToActivity(w, op);
            var before = Snapshot(w);
            var step = Sim.Step(w, a, op.Kind == OpKind.Butcher ? op.Minutes * 2 : op.Minutes);
            var after = ItemKg(w);
            var declared = Declared(w, a, step, before, after, notes);
            foreach (var r in after.Keys.Union(before.Items.Keys).Union(declared.Keys))
            {
                double delta = after.GetValueOrDefault(r) - before.Items.GetValueOrDefault(r);
                double want = declared.GetValueOrDefault(r);
                Assert.True(Math.Abs(delta - want) <= Tolerance,
                    $"op {i} {a} ({step.MinutesElapsed} min, {step.InterruptedReason}): {r} moved {delta:R}, declared {want:R}, leak {delta - want:R}");
            }
            Assert.Equal(before.Gear, GearCount(w));
            Assert.Null(BrokenPiece(w)); // after any take, drop, burn, eat, cook or butchering, every piece is whole (#142)
            Assert.Empty(notes);
            if (!Sim.IsAlive(player)) return;
        }
    }

    [Fact]
    public void Items_AreNeitherMadeNorLost() => Scenes.Check(Run, iter: 25);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Items_AreNeitherMadeNorLost_Many() => Scenes.Check(Run, iter: 1500);

    // ---- raids: the ravens' take is the only sink, and it is bounded ----

    public sealed record RaidScene(int Members, double[] PileKg, int Minutes);

    static readonly Resource[] RaidPile =
        [Resource.RawMeat, Resource.RawFish, Resource.CookedMeat, Resource.CookedFish, Resource.DriedMeat, Resource.Berries, Resource.BirchBranch, Resource.Hide];

    static readonly Gen<RaidScene> RaidScenes = Gen.Select(Gen.Int[1, 8],
        Gen.Frequency((1, Gen.Const(0.0)), (3, Gen.Double[0, 2])).Array[RaidPile.Length], Gen.Int[60, 3000],
        (m, kg, min) => new RaidScene(m, kg, min));

    /// A pile nobody keeps, ravens beside it: only what the ravens eat leaves, never more
    /// than RaidKgPerMember (0.05 kg) a member a minute, and nothing else moves.
    static void Raid(RaidScene s)
    {
        var w = Bare(5);
        var tile = Map.Step(Map.Step(Camp(w), Direction.East), Direction.East);
        for (int i = 0; i < RaidPile.Length; i++)
            if (s.PileKg[i] > 0) TestKit.Give(Items.PileAt(w, tile, new Cause(P, "put down by the player")), RaidPile[i], s.PileKg[i]);
        Agents.SpawnGroup(w, Species.Raven, tile, s.Members);
        var before = ItemKg(w);
        var gear = GearCount(w);
        Sim.Advance(w, s.Minutes);
        var after = ItemKg(w);
        Resource[] food = [Resource.RawMeat, Resource.RawFish, Resource.CookedMeat, Resource.CookedFish, Resource.DriedMeat];
        double taken = 0;
        foreach (var r in after.Keys.Union(before.Keys))
        {
            double delta = after.GetValueOrDefault(r) - before.GetValueOrDefault(r);
            if (food.Contains(r)) { Assert.True(delta <= Tolerance, $"{r} grew {delta:R} in a raid"); taken -= delta; }
            else Assert.True(Math.Abs(delta) <= Tolerance, $"{r} moved {delta:R}: the ravens eat only meat and fish");
        }
        Assert.True(taken <= 0.05 * s.Members * s.Minutes + Tolerance, $"took {taken:R} kg in {s.Minutes} min with {s.Members} members");
        Assert.Equal(gear, GearCount(w));
        Assert.Null(BrokenPiece(w));
    }

    [Fact]
    public void Raids_TakeOnlyWhatTheFlockEats() => RaidScenes.Check(Raid, iter: 10);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Raids_TakeOnlyWhatTheFlockEats_Many() => RaidScenes.Check(Raid, iter: 300);

    // ---- gathering: the haul is the source, and every kilo of it is in the pack or on the ground ----

    public sealed record GatherScene(int PoolKind, double FillKg, int Minutes, int Focus);

    static readonly Gen<GatherScene> GatherScenes = Gen.Select(Gen.Int[0, 2], Gen.Double[0, 14], Gen.Int[1, 240], Gen.Int[0, 4],
        (k, fill, m, f) => new GatherScene(k, fill, m, f));

    /// Work.Minute's haul (WorkSession.Got, the declared source) lands whole: in the pack up to its
    /// room, the rest on the tile's pile (Items.Receive), and nothing else moves.
    static void Gathering(GatherScene s)
    {
        var w = Bare(5);
        var camp = Camp(w);
        PoolDef pool = s.PoolKind switch
        {
            0 => new ForagePool("forage", 1.0, Pools.MixedForest),
            1 => new TreePool("Trees", Resource.BirchFirewood, 100),
            _ => new StockPool("Bush", StockKind.Harvest, 5, [new StockItem(Resource.Berries, 5, 0.4, 168)], RevealAtHours: 0),
        };
        w.PoolCache = new() { [camp] = [pool] };
        ((PersonMind)w.Agent(P).Mind).Memory.Discovered.Add(new PoolId(camp, 0));
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("stone_axe"));
        if (s.FillKg > 0) TestKit.Give(pack, Resource.Stone, s.FillKg);
        w.Minute = 10 * 60; // daylight
        var before = ItemKg(w);
        var gear = GearCount(w);
        var activity = new Gather(P, 0, (ForageFocus)s.Focus);
        Assert.Null(Work.Refusal(w, activity));
        var events = new List<WorldEvent>();
        var session = Work.Begin(w, activity, events);
        for (int m = 0; m < s.Minutes; m++)
        {
            w.Minute++;
            if (Work.Minute(w, w.Agent(P), session, events) != null) break;
        }
        Work.End(w, w.Agent(P), session, events);
        var after = ItemKg(w);
        foreach (var r in after.Keys.Union(before.Keys).Union(Items.KgByResource(session.Got).Keys))
        {
            double delta = after.GetValueOrDefault(r) - before.GetValueOrDefault(r);
            Assert.True(Math.Abs(delta - Items.Have(session.Got, r)) <= Tolerance, $"{r}: got {Items.Have(session.Got, r):R}, items moved {delta:R}");
        }
        Assert.Equal(gear, GearCount(w));
        Assert.Null(BrokenPiece(w)); // forage, felling and stock yield whole pieces in range (#142)
    }

    [Fact]
    public void Gathering_LandsEveryKiloItGets() => GatherScenes.Check(Gathering, iter: 20);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Gathering_LandsEveryKiloItGets_Many() => GatherScenes.Check(Gathering, iter: 1000);
}
