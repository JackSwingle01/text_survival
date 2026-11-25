using Xunit.Abstractions;

namespace Survival.Tests;

/// Sticks, branches and firewood (#143): where each comes from, what it burns like, and the wood a new run starts with.
public class WoodTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    static readonly Resource[] Branches = [Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch];
    static readonly Resource[] Firewood = [Resource.PineFirewood, Resource.BirchFirewood, Resource.OakFirewood];

    static World WithPools(params PoolDef[] pools)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        w.Minute = 9 * 60;
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        TestKit.ClearResources(Items.Pack(w, P));
        return w;
    }

    static void Know(World w, int pool) => ((PersonMind)w.Agent(P).Mind).Memory.Discovered.Add(new PoolId(Map.Of(w).Camp, pool));
    static void Axe(World w) => Items.Pack(w, P).Gear.Add(Items.Make("stone_axe"));
    static double Have(World w, IEnumerable<Resource> rs) => rs.Sum(r => Items.Have(Items.Pack(w, P), r));

    static StockPool Deadfall(int seed = 1) => (StockPool)Pools.Make(Content.Tables.Discoveries.Finds["Deadfall"], Rng.ForSystem(seed, "deadfall"), 0);

    [Fact]
    public void NoResource_IsNamedForALog()
    {
        foreach (var r in Enum.GetValues<Resource>())
        {
            Assert.DoesNotContain("Log", r.ToString());
            Assert.DoesNotContain("log", Names.Of(r));
            if (Items.IsDiscrete(r)) Assert.DoesNotContain("log", Names.PieceWord(r));
        }
        Assert.DoesNotContain("Kindling", Enum.GetNames<Resource>());
    }

    [Fact]
    public void ATableThatNamesALog_IsRefusedAtLoad()
    {
        var files = Content.EmbeddedFiles();
        files["forage.json"] = files["forage.json"].Replace("\"resource\": \"PineBranch\"", "\"resource\": \"Pine\"");
        Assert.Contains("no Resource 'Pine'", Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message);
    }

    [Theory]
    [InlineData("forage.json", "\"resource\": \"PineBranch\"", "\"resource\": \"PineFirewood\"")]
    [InlineData("loot.json", "{ \"resource\": \"PineBranch\"", "{ \"resource\": \"PineFirewood\"")]
    [InlineData("recipes.json", "\"of\": \"PineBranch|BirchBranch|OakBranch\", \"count\": 1 } ], \"tools\": []", "\"of\": \"PineFirewood\", \"count\": 1 } ], \"tools\": []")]
    [InlineData("discoveries.json", ", \"respawnHoursPerUnit\": 0, \"requiresTool\": \"Axe\" },\n   { \"resource\": \"Stick\", \"units\": 4, \"unitsMax\": 8", ", \"respawnHoursPerUnit\": 0 },\n   { \"resource\": \"Stick\", \"units\": 4, \"unitsMax\": 8")]
    public void Firewood_OnlyComesFromAxeWork_AndIsOnlyEverFuel(string file, string from, string to)
    {
        var files = Content.EmbeddedFiles();
        Assert.Contains(from, files[file]);
        int at = files[file].IndexOf(from, StringComparison.Ordinal);
        files[file] = files[file][..at] + to + files[file][(at + from.Length)..];
        var message = Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message;
        Assert.True(message.Contains("firewood comes only from axe work") || message.Contains("is only ever fuel"), message);
    }

    [Fact]
    public void ForagingAForestTile_GivesSticksAndBranches_NeverFirewood()
    {
        var w = WithPools(new ForagePool("forage", 1.0, Pools.MixedForest));
        for (int hour = 0; hour < 6; hour++) Sim.Step(w, new Gather(P, 0, ForageFocus.Fuel), 60);
        Assert.True(Items.Count(Items.Pack(w, P), Resource.Stick) > 0);
        Assert.True(Branches.Sum(r => Items.Count(Items.Pack(w, P), r)) > 0);
        Assert.Equal(0, Have(w, Firewood));
    }

    [Fact]
    public void FellingATree_GivesFirewood_AndLeavingAndComingBack_KeepsItsProgress()
    {
        var w = WithPools(new TreePool("Trees", Resource.BirchFirewood, 100));
        Axe(w);
        var camp = Map.Of(w).Camp;
        Assert.Null(Sim.Step(w, new Gather(P, 0), 60).InterruptedReason); // down at 40 minutes, and some firewood cut
        double progress = Pools.ChopProgressPct(w, new PoolId(camp, 0));
        Assert.True(progress > 0.55, $"{progress}");

        // Walk away and back, over a save.
        var east = new TileId(camp.X + 1, camp.Y);
        var trip = Sim.Step(w, new Travel(P, Direction.East), 24 * 60);
        Assert.Equal(Traversal.Arrived, trip.InterruptedReason);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        loaded.PoolCache = w.PoolCache;
        Assert.Equal(east, loaded.Agent(P).Tile);
        Assert.Equal(Traversal.Arrived, Sim.Step(loaded, new Travel(P, Direction.West), 24 * 60).InterruptedReason);
        Assert.Equal(progress, Pools.ChopProgressPct(loaded, new PoolId(camp, 0)), 9);

        TestKit.ClearResources(Items.Pack(loaded, P)); // carried home
        Items.Pack(loaded, P).Gear.Add(Items.Make("large_bag")); // room for the rest of the trunk
        var rest = Sim.Step(loaded, new Gather(P, 0), 120);
        Assert.Equal(Work.TreeCutUp, rest.InterruptedReason);
        Assert.InRange(rest.MinutesElapsed, 39, 41); // the 100 minutes, less the 60 already worked
        Assert.True(Items.Count(Items.Pack(loaded, P), Resource.BirchFirewood) > 0);
        Assert.Equal(0, Have(loaded, Branches));
    }

    [Fact]
    public void CuttingATrunk_StopsWhenThePackIsFull_AndThePieceStaysOnTheTrunk()
    {
        var w = WithPools(new TreePool("Trees", Resource.OakFirewood, 100)); // 3.6-4.4 kg pieces, more than a pack
        Axe(w);
        var r = Sim.Step(w, new Gather(P, 0), 200);
        Assert.Equal(Work.PackFull, r.InterruptedReason);
        Assert.Null(w.Tiles[Map.Of(w).Camp].Pile); // nothing spilled
        var id = new PoolId(Map.Of(w).Camp, 0);
        Assert.NotNull(Pools.NextPieceKg(w, id));
        Assert.Equal(Work.PackFull, Sim.Refusal(w, new Gather(P, 0))?.Reason);
        Assert.True(Pools.HasTrees(w, id));
    }

    [Fact]
    public void Deadfall_IsACommonFind()
    {
        var forest = Content.Tables.Discoveries.Terrains[Terrain.Forest];
        var deadfall = forest.Single(d => d.Find == "Deadfall");
        Assert.Equal(forest.Max(d => d.SpawnChance), deadfall.SpawnChance);
        Assert.Contains(Content.Tables.Discoveries.Terrains[Terrain.Clearing], d => d.Find == "Deadfall");
    }

    [Fact]
    public void Deadfall_GivesBranchesByHand_AndFirewoodOnlyWithAnAxe_AndRunsOut()
    {
        var w = WithPools(new ForagePool("forage", 1.0, Pools.MixedForest), Deadfall());
        Know(w, 1);
        var byHand = Sim.Step(w, new Gather(P, 1), 6 * 60);
        Assert.Equal("the Deadfall is picked clean", byHand.InterruptedReason);
        Assert.True(Have(w, Branches) > 0 && Items.Have(Items.Pack(w, P), Resource.Stick) > 0);
        Assert.Equal(0, Have(w, Firewood));
        Assert.Equal("you need an axe", Sim.Refusal(w, new Gather(P, 1))?.Reason); // the firewood waits for an axe

        TestKit.ClearResources(Items.Pack(w, P));
        Axe(w);
        Items.Pack(w, P).Gear.Add(Items.Make("large_bag"));
        var withAxe = Sim.Step(w, new Gather(P, 1), 6 * 60);
        Assert.Equal("the Deadfall is picked clean", withAxe.InterruptedReason);
        Assert.True(Have(w, Firewood) > 0);
        Assert.Equal("the Deadfall is picked clean", Sim.Refusal(w, new Gather(P, 1))?.Reason);
        w.Minute += 365L * 24 * 60;
        Assert.Equal("the Deadfall is picked clean", Sim.Refusal(w, new Gather(P, 1))?.Reason); // wood never regrows
    }

    [Fact]
    public void DeadfallFirewood_ComesFasterThanFelling()
    {
        var tree = new TreePool("Trees", null, Content.Tables.Terrains[Terrain.Forest].Trees!.MinutesToFell);
        double fellingKgPerMinute = Firewood.Sum(r => Pools.ExpectedTree(tree).GetValueOrDefault(r)) / tree.MinutesToFell;
        var deadfall = Deadfall();
        var firewood = deadfall.Items.Single(i => Firewood.Contains(i.Resource));
        double deadfallKgPerMinute = firewood.KgPerUnit / deadfall.MinutesPerUnit;
        Assert.InRange(deadfallKgPerMinute / fellingKgPerMinute, 2.5, 3.5); // about three times
    }

    [Fact]
    public void EveryWoodFind_RunsOut()
    {
        var wood = Branches.Concat(Firewood).Append(Resource.Stick).ToHashSet();
        foreach (var (name, find) in Content.Tables.Discoveries.Finds)
            foreach (var item in find.Items ?? [])
                if (item.RespawnHoursPerUnit is double hours && (item.AnyOf ?? [item.Resource!.Value]).Any(wood.Contains))
                    Assert.True(hours == 0, $"{name}: its wood regrows");
        foreach (var l in Content.Tables.Locations)
            foreach (var s in l.All.OfType<SiteStock>())
                foreach (var item in s.Items ?? [])
                    if (wood.Contains(item.Resource)) Assert.True(item.RespawnHoursPerUnit == 0, $"{l.Id} {s.Name}: its wood regrows");
    }

    [Fact]
    public void ADeadfallsBranchesAndFirewood_AreOneWood()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var d = Deadfall(seed);
            var branch = d.Items.Single(i => Branches.Contains(i.Resource)).Resource;
            var firewood = d.Items.Single(i => Firewood.Contains(i.Resource)).Resource;
            Assert.Equal(Array.IndexOf(Branches, branch), Array.IndexOf(Firewood, firewood));
        }
    }

    [Fact]
    public void ANewRunsCamp_HoldsFirewood()
    {
        var w = TestWorlds.New(17);
        var pile = w.Tiles[Map.Of(w).Camp].Pile!;
        Assert.True(Firewood.Sum(r => Items.Count(pile, r)) >= 3);
        Assert.Equal(Items.StartingCampCause, w.Tiles[Map.Of(w).Camp].PileCause!.Value.What);
    }

    [Theory]
    [InlineData(Resource.PineBranch, Resource.PineFirewood)]
    [InlineData(Resource.BirchBranch, Resource.BirchFirewood)]
    [InlineData(Resource.OakBranch, Resource.OakFirewood)]
    public void ABranchAndFirewood_OfOneWood_BurnTheSameTime(Resource branch, Resource firewood)
    {
        double Burns(Resource r)
        {
            var f = new FireState { Tile = new TileId(0, 0), Pit = PitType.Open, LastUpdatedMinute = 0 };
            Fire.AddFuel(f, Resource.Stick, 0.5);
            Fire.AddFuel(f, r, 2.0);
            Fire.IgniteAll(f);
            return Fire.HoursItWillBurn(f);
        }
        Assert.Equal(Fire.Props[branch], Fire.Props[firewood]);
        Assert.Equal(Burns(branch), Burns(firewood), 9);
    }

    /// The acceptance report: a player with no axe, foraging for fuel on seed 17 and dropping it where they stand, until it
    /// holds a night's fire (12 hours, Report.MinFuelFireHours) of sticks and branches: on the camp's thin ground, and on
    /// the nearest open forest.
    [Fact]
    public void WithNoAxe_ANightsFireOfBranches_TakesHoursOfForaging()
    {
        double Hours(bool forest)
        {
            var w = TestWorlds.Bare(17);
            w.FixedWeather = Mild;
            w.Minute = 9 * 60;
            var m = Map.Of(w);
            var tile = !forest ? m.Camp : MapGen.Neighbors(m.Camp).Concat(MapGen.Neighbors(m.Camp).SelectMany(MapGen.Neighbors))
                .First(t => m.InBounds(t) && m.TerrainAt(t) == Terrain.Forest && m.SiteAt(t) == null && t != m.Camp);
            w.Agent(P).Tile = tile;
            var forage = Pools.ForageAt(w, tile)!.Value;
            var pack = Items.Pack(w, P);
            double FireHours(Inventory? inv) => inv == null ? 0 : Items.Totals(inv).Where(x => x.Resource == Resource.Stick || Branches.Contains(x.Resource)).Sum(x => Fire.FuelHours(x.Resource, x.Kg));
            double energy = w.Agent(P).Body.EnergyMinutes;
            int minutes = 0;
            while (FireHours(w.Tiles.GetValueOrDefault(tile)?.Pile) + FireHours(pack) < Report.MinFuelFireHours && minutes < 24 * 60)
            {
                minutes += Sim.Step(w, new Gather(P, forage.Index, ForageFocus.Fuel), 60).MinutesElapsed;
                foreach (var r in Branches.Append(Resource.Stick))
                    if (Items.Have(pack, r) > 0) Sim.Step(w, new PutDown(P, r, Items.Have(pack, r)), 1);
                w.Agent(P).Body.EnergyMinutes = energy; // the hours of foraging are the measure, not whether one body lasts them
            }
            Assert.True(minutes < 24 * 60, $"a night's fire took more than a day's foraging ({minutes} min)");
            return minutes / 60.0;
        }
        output.WriteLine($"seed 17, no axe, {Report.MinFuelFireHours} h of fire from sticks and branches: {Hours(false):F1} h of fuel foraging at camp, {Hours(true):F1} h on the nearest forest");
    }
}
