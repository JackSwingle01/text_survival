using CsCheck;
using Survival.Tests.Verification;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests;

/// The forecasts the NPC planner reads (GLOSSARY: Forecast): each moves the right way with what it reads,
/// and agrees with what its own system then does.
public class ForecastTests
{
    static readonly StockPool Bush = new("bush", StockKind.Harvest, 5, [new StockItem(Resource.Berries, 10_000, 0.01, 168)], RevealAtHours: 0);

    static World At(params PoolDef[] pools)
    {
        var w = Bare(-5);
        TestKit.ClearResources(Items.Pack(w, P)); // the starting pack's tinder and kindling would answer for what a test gives
        if (pools.Length > 0) w.PoolCache = new() { [Camp(w)] = pools };
        return w;
    }

    static Agent Me(World w) => w.Agent(P);

    /// The world minute of this time of day on the first day (minute 0 is 09:00).
    static long Clock(int minuteOfDay) => ((minuteOfDay - Weather.MinuteOfDay(0)) % 1440 + 1440) % 1440;

    static void Give(World w, params (Resource Resource, double Kg)[] kgs)
    {
        foreach (var (r, kg) in kgs) TestKit.Give(Items.Pack(w, P), r, kg);
    }

    static FireId PitWith(World w, Resource fuel, double burningKg)
    {
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute, BurningKg = { [fuel] = burningKg } };
        return id;
    }

    // ---- Fire: lighting ----

    static StartFire Strike(double tinderKg, double kindlingKg) => new(P, ToolType.FireStriker, Resource.BirchBark, tinderKg, kindlingKg);

    static World WithStriker()
    {
        var w = At();
        Items.Pack(w, P).Gear.Add(Items.Make("fire_striker"));
        return w;
    }

    [Fact]
    public void MoreTinder_KeepsALightGoingLongerAndHotter()
    {
        var w = WithStriker();
        Give(w, (Resource.BirchBark, 3), (Resource.OakBranch, 4));
        var little = Fire.ForecastLight(w, Strike(0.05, 0));
        var more = Fire.ForecastLight(w, Strike(2.5, 0));
        Assert.True(more.FlameHours > little.FlameHours);
        Assert.False(little.Takes(Resource.OakBranch));
        Assert.True(more.Takes(Resource.OakBranch));
        Assert.Equal(0, little.KeepsGoingPct);
        Assert.True(more.KeepsGoingPct > 0);
    }

    [Fact]
    public void WetHands_LowerTheOddsOfTheStrike()
    {
        var w = WithStriker();
        Give(w, (Resource.BirchBark, 1));
        Items.Pack(w, P).Gear.Add(Items.Make("hand_drill"));
        var drill = Strike(0.05, 0) with { Tool = ToolType.HandDrill }; // the striker's odds sit at the cap
        var dry = Fire.ForecastLight(w, drill);
        Effects.Set(Me(w).Body, EffectKind.Wet, 1.0);
        var wet = Fire.ForecastLight(w, drill);
        Assert.True(wet.StrikePct < dry.StrikePct);
    }

    [Fact]
    public void ARefusedStrike_ForecastsNothing()
    {
        var w = At();
        Give(w, (Resource.BirchBark, 1));
        Assert.Equal(new Fire.LightForecast(0, 0, 0, 0), Fire.ForecastLight(w, Strike(0.05, 0)));
    }

    /// The flame lasts the minutes forecast, and a log goes on the minute after exactly when the forecast says it takes.
    [Fact]
    public void TheLightForecast_AgreesWithTheFireStruck()
    {
        Gen.Select(Gen.Double[0.02, 2.5], Gen.Double[0, 3], Gen.Int[0, 3]).Check(c =>
        {
            var (tinderKg, kindlingKg, log) = c;
            if (kindlingKg < Items.Resources[Resource.Stick].MinPieceKg) kindlingKg = 0; // less than a stick is none
            var fuel = new[] { Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch, Resource.Bone }[log];
            var w = WithStriker();
            Give(w, (Resource.BirchBark, tinderKg * 5), (Resource.Stick, kindlingKg), (fuel, 2));
            var forecast = Fire.ForecastLight(w, Strike(tinderKg, kindlingKg));
            for (int tries = 0; Sim.Step(w, Strike(tinderKg, kindlingKg), 1).Reason == Fire.DidNotCatch; tries++)
            {
                Assert.True(tries < 4, "the striker kept failing");
                forecast = Fire.ForecastLight(w, Strike(tinderKg, kindlingKg));
            }
            var pit = w.Fires[Fire.PitAt(w, Camp(w))!.Value];
            Assert.Equal(forecast.Takes(fuel), Sim.Refusal(w, new AddFuel(P, fuel, 1)) == null);
            int burned = 1;
            while (Fire.IsActive(pit)) { Sim.Advance(w, 1); burned++; }
            Assert.Equal((int)Math.Round(forecast.FlameHours * 60), burned);
        }, iter: 40);
    }

    /// Burning, down to embers, or cold: the forecast is what ticking the fire forward finds.
    [Fact]
    public void WillCatch_AgreesWithTheFireTickedForward()
    {
        Gen.Select(Gen.Int[0, 4], Gen.Double[0.1, 3], Gen.Double[0.2, 3], Gen.Int[0, 2]).Check(c =>
        {
            var (log, kindlingKg, logKg, state) = c;
            var fuel = new[] { Resource.Stick, Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch, Resource.Bone }[log];
            var f = new FireState { Tile = new TileId(0, 0), LastUpdatedMinute = 0 };
            long minute = 0;
            if (state < 2)
            {
                Fire.AddFuel(f, Resource.Stick, kindlingKg);
                Fire.IgniteAll(f);
                while (state == 1 && Fire.BurningKg(f) > 0) Fire.Tick(f, default, ++minute, []);
            }
            bool forecast = Fire.WillCatch(f, fuel, logKg);
            bool caught = Fire.AddFuel(f, fuel, logKg) > 0 && f.BurningKg.ContainsKey(fuel);
            while (!caught && Fire.BurningKg(f) > 0)
            {
                Fire.Tick(f, default, ++minute, []);
                caught = f.BurningKg.ContainsKey(fuel);
            }
            Assert.Equal(caught, forecast);
        });
    }

    // ---- Fire: warmth ----

    [Fact]
    public void WoodWithNoWayToLightIt_IsNoWarmth()
    {
        var oak = new Dictionary<Resource, double> { [Resource.OakBranch] = 8 };
        var oakAndTinder = new Dictionary<Resource, double> { [Resource.OakBranch] = 8, [Resource.BirchBark] = 0.05 };
        var oakTinderKindling = new Dictionary<Resource, double> { [Resource.OakBranch] = 8, [Resource.BirchBark] = 0.05, [Resource.Stick] = 1 };
        Assert.Equal(0, Fire.UsableHours(null, PitType.Open, oak, canStrike: true));
        Assert.Equal(0, Fire.UsableHours(null, PitType.Open, oakTinderKindling, canStrike: false));
        double tinderOnly = Fire.UsableHours(null, PitType.Open, oakAndTinder, canStrike: true);
        double withKindling = Fire.UsableHours(null, PitType.Open, oakTinderKindling, canStrike: true);
        Assert.True(tinderOnly > 0);
        Assert.True(withKindling > tinderOnly + Fire.FuelHours(Resource.Stick, 1)); // the kindling brings the oak in
        Assert.Equal(Priority.Need, Priorities.WarmthPriority(98.6, false, Fire.UsableHours(null, PitType.Open, oak, canStrike: true), tonightHours: 12));
        Assert.Equal(Priority.None, Priorities.WarmthPriority(98.6, false, Fire.FuelHours(Resource.OakBranch, 8) * 3, tonightHours: 2)); // raw hours would read warm
    }

    [Fact]
    public void ABurningPit_TakesTheWoodItIsHotEnoughFor()
    {
        var w = At();
        var pit = w.Fires[PitWith(w, Resource.BirchBranch, 3)];
        var oak = new Dictionary<Resource, double> { [Resource.OakBranch] = 4 };
        Assert.True(Fire.UsableHours(pit, PitType.Open, oak, canStrike: false) > Fire.HoursItWillBurn(pit));
        var embersOnly = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute };
        Assert.Equal(0, Fire.UsableHours(embersOnly, PitType.Open, oak, canStrike: false));
    }

    /// A piece of tinder goes into the pit whole, so a strike is held to the pit's room as a top-up is (#142 review).
    [Fact]
    public void AStrike_NeverPutsMoreTinderInThePit_ThanItHasRoomFor()
    {
        var w = WithStriker();
        double max = Fire.MaxFuelKg(PitType.Open);
        var id = new FireId(w.NextFireId++);
        var pit = w.Fires[id] = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute, UnburnedKg = { [Resource.BirchBranch] = max - 0.2 } };
        for (int i = 0; i < 20; i++) Items.AddPiece(Items.Pack(w, P), Resource.Chaga, 0.15);
        // Two pieces are asked for and only one fits: the strike spends one.
        var strike = new StartFire(P, ToolType.FireStriker, Resource.Chaga, 0.3, 0);
        Assert.Null(Sim.Refusal(w, strike));
        string? missed = Fire.DidNotCatch;
        for (int i = 0; i < 10 && missed != null; i++)
        {
            int had = Items.Count(Items.Pack(w, P), Resource.Chaga);
            missed = Fire.Act(w, strike, []);
            Assert.Equal(had - 1, Items.Count(Items.Pack(w, P), Resource.Chaga));
        }
        Assert.Null(missed);
        Assert.True(Fire.TotalKg(pit) <= max + 1e-9, $"the pit holds {Fire.TotalKg(pit)} kg of {max}");
        // With room for less than the lightest piece, the strike is refused before any tinder is spent.
        var full = w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = new TileId(Camp(w).X + 1, Camp(w).Y), LastUpdatedMinute = w.Minute, UnburnedKg = { [Resource.BirchBranch] = max - 0.05 } };
        Me(w).Tile = full.Tile;
        var refused = Sim.Refusal(w, strike);
        Assert.StartsWith("the fire has no room for a whole ", refused?.Reason);
        var room = Assert.IsType<MissingState>(refused!.Missing);
        Assert.Equal((Lack.FireRoomKg, 0.15), (room.Lack, room.Need));
    }

    // ---- Fire: a sliver is not a top-up ----

    [Fact]
    public void AddFuel_RefusesASliver_WithTheRoomItNeeds()
    {
        var w = At();
        Give(w, (Resource.BirchBranch, 5), (Resource.Stick, 0.05), (Resource.Tinder, 1));
        var pit = w.Fires[PitWith(w, Resource.BirchBranch, Fire.MaxFuelKg(PitType.Open) - 0.1)];
        // A log goes on whole or not at all: a nearly full fire refuses it, naming the room a log needs (#142).
        var refused = Sim.Refusal(w, new AddFuel(P, Resource.BirchBranch, 1));
        Assert.Equal("the fire has no room for a whole birch branch", refused?.Reason);
        var room = Assert.IsType<MissingState>(refused!.Missing);
        Assert.Equal((Lack.FireRoomKg, Items.LightestKg(Items.Pack(w, P), Resource.BirchBranch)), (room.Lack, room.Need));
        Assert.Equal(0.1, room.Have!.Value, 6);
        // An amount that would only fill a sliver of the room is refused as before.
        var sliver = Sim.Refusal(w, new AddFuel(P, Resource.Tinder, 1));
        Assert.Equal("the fire is full", sliver?.Reason);
        Assert.Equal((Lack.FireRoomKg, Fire.SliverKg), (((MissingState)sliver!.Missing!).Lack, ((MissingState)sliver.Missing!).Need));
        Assert.Null(Sim.Refusal(w, new AddFuel(P, Resource.Stick, 0.05))); // a small stick that fits is no sliver
        Assert.Equal(Fire.MaxFuelKg(PitType.Open) - 0.1, Fire.TotalKg(pit), 9);
    }

    // ---- Work: darkness ----

    /// The forecast minute is the minute light-needing work stops for the dark: by the clock, a pit burning
    /// out or a torch guttering, whichever comes last.
    [Fact]
    public void TheDarknessForecast_AgreesWithWhenWorkStopsForTheDark()
    {
        Gen.Select(Gen.Int[13 * 60, 28 * 60], Gen.Double[0, 4], Gen.Int[0, 60]).Check(c =>
        {
            var (startMinute, pitKg, torchMinutes) = c;
            var w = At(Bush);
            w.Minute = Clock(startMinute % 1440);
            if (pitKg > 0.05) PitWith(w, Resource.Stick, pitKg);
            if (torchMinutes > 0)
                w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = Camp(w), LastUpdatedMinute = w.Minute, CarriedBy = P, TorchMinutesLeft = torchMinutes };
            int forecast = Work.MinutesUntilTooDark(w, Me(w));
            if (forecast > 8 * 60) return; // a day's work: the body, not the light, ends it
            var gather = new Gather(P, 0);
            if (forecast == 0)
            {
                Assert.Equal(Work.Dark, Sim.Refusal(w, gather));
                return;
            }
            var r = Sim.Step(w, gather, forecast + 60);
            Assert.Equal((Work.TooDark, forecast), (r.Reason, r.MinutesElapsed));
        }, iter: 40);
    }

    // ---- Pools and Work: expected yield ----

    static ForagePool Forage(double density) => new("forage", density, Pools.MixedForest);

    static double Total(IReadOnlyDictionary<Resource, double> kg) => kg.Values.Sum();

    [Fact]
    public void PickedOverGround_ForecastsLessThanFresh()
    {
        var w = At(Forage(1.0));
        var id = new PoolId(Camp(w), 0);
        double fresh = Total(Pools.ExpectedForage(w, id, 60));
        var rng = Rng.ForSystem(1, "picking over");
        for (int m = 0; m < 4 * 60; m++) Pools.Forage(w, id, 1 / 60.0, rng);
        double picked = Total(Pools.ExpectedForage(w, id, 60));
        Assert.True(picked < fresh * 0.9, $"picked over {picked:0.###} kg against fresh {fresh:0.###}");
        Assert.True(Total(Pools.ExpectedForage(w, id, 120)) < 2 * picked); // and it goes on thinning
        Assert.Equal(fresh, Total(Pools.ExpectedForage(w, id, 60, rememberedDensity: 1.0)), 9); // remembered as it was
    }

    /// Over many searches, what Forage finds averages to the forecast, on fresh ground and picked-over.
    [Fact]
    public void TheForageForecast_AgreesWithWhatSearchingFinds()
    {
        Gen.Select(Gen.Double[0.1, 1.5], Gen.Int[0, 300], Gen.Int[30, 180]).Check(c =>
        {
            var (density, pickedMinutes, searchMinutes) = c;
            double forecast = 0, found = 0;
            const int runs = 200;
            var w = At([.. Enumerable.Repeat(Forage(density), runs)]); // a pool a run: one world, untouched ground each time
            for (int run = 0; run < runs; run++)
            {
                var id = new PoolId(Camp(w), run);
                var rng = Rng.ForSystem(run, $"forage {density} {pickedMinutes} {searchMinutes}");
                for (int m = 0; m < pickedMinutes; m++) Pools.Forage(w, id, 1 / 60.0, rng);
                forecast += Total(Pools.ExpectedForage(w, id, searchMinutes));
                for (int m = 0; m < searchMinutes; m++) found += Pools.Forage(w, id, 1 / 60.0, rng).Sum(x => x.Kg);
            }
            Assert.InRange(found, forecast * 0.85, forecast * 1.15);
        }, iter: 8);
    }

    [Fact]
    public void TheGatherForecast_AgreesWithAnHourOfForaging()
    {
        foreach (var focus in new[] { ForageFocus.General, ForageFocus.Fuel, ForageFocus.Food })
        {
            double forecast = 0, gathered = 0;
            for (int run = 0; run < 60; run++)
            {
                var w = At(Forage(1.0));
                w.Minute = Clock(9 * 60 + run * 7);
                foreach (var stream in new[] { "work", "focus" })
                {
                    var rng = w.Rng(stream, P);
                    for (int draw = 0; draw < run * 5000; draw++) rng.NextDouble(); // each run its own rolls, an hour's worth apart
                }
                var g = new Gather(P, 0, focus);
                forecast += Total(Work.ExpectedGather(w, Me(w), g, 60));
                double before = Items.CarriedKg(Me(w));
                Sim.Step(w, g, 60);
                gathered += Items.CarriedKg(Me(w)) - before + (w.Tiles.GetValueOrDefault(Camp(w))?.Pile is { } s ? Items.WeightKg(s) : 0);
            }
            Assert.InRange(gathered, forecast * 0.8, forecast * 1.2);
        }
    }

    [Fact]
    public void TheGatherForecast_CountsATreeOnlyOnceItComesDown_ItsFirewoodByThePiece_AndAHarvestByItsCycles()
    {
        var w = At(new TreePool("trees", Resource.BirchFirewood, 100), Bush);
        Items.Pack(w, P).Gear.Add(Items.Make("stone_axe"));
        w.Minute = Clock(9 * 60);
        Assert.Empty(Work.ExpectedGather(w, Me(w), new Gather(P, 0), 30)); // it comes down at 40 minutes
        var down = Work.ExpectedGather(w, Me(w), new Gather(P, 0), 45);
        Assert.False(down.ContainsKey(Resource.BirchFirewood)); // down, but no piece cut yet (60 minutes over 9 pieces)
        Assert.True(down[Resource.Stick] > 0);
        var felled = Work.ExpectedGather(w, Me(w), new Gather(P, 0), 120);
        Assert.Equal(Pools.ExpectedTree(new TreePool("trees", Resource.BirchFirewood, 100)), felled);
        Assert.Equal(0.12, Work.ExpectedGather(w, Me(w), new Gather(P, 1), 60)[Resource.Berries], 9); // 12 cycles of 5 minutes
        w.Minute = Clock(22 * 60);
        Assert.Empty(Work.ExpectedGather(w, Me(w), new Gather(P, 1), 60)); // too dark to start
    }

    [Fact]
    public void TheMixedTreeForecast_SharesItsWoodAsChopDrawsIt()
    {
        var mixed = Pools.ExpectedTree(new TreePool("trees", null, 100));
        var w = At(new TreePool("trees", null, 1));
        var id = new PoolId(Camp(w), 0);
        var got = new SortedDictionary<Resource, double>();
        const int trees = 2000;
        for (int i = 0; i < trees; i++)
            foreach (var (r, kg) in Items.Totals(Pools.Chop(w, id, 1, Rng.ForSystem(i, "felling")).Got)) got[r] = got.GetValueOrDefault(r) + kg / trees;
        Assert.Equal(mixed.Keys, got.Keys);
        foreach (var (r, kg) in mixed) Assert.InRange(got[r], kg * 0.9, kg * 1.1);
    }

    // ---- Trip: a crossing's minutes, kept for the minute ----

    [Fact]
    public void StepMinutes_AgreeWithTheCrossingMade_AndFollowTheLoad()
    {
        var w = Seeded();
        var m = Map.Of(w);
        w.Minute = Clock(10 * 60);
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            var from = Camp(w);
            var to = Map.Step(from, dir);
            if (Map.Blocked(m, from, to) != null) continue;
            var copy = TestWorlds.Load(Save.ToJson(w));
            int forecast = Trip.StepMinutes(copy, P, from, to, Pace.Quick);
            var r = Sim.Step(copy, new Travel(P, dir), Verbs.UntilDoneMinutes);
            Assert.Equal((Traversal.Arrived, forecast), (r.Reason, r.MinutesElapsed));
        }
        var east = Map.Step(Camp(w), Direction.East);
        int light = Trip.StepMinutes(w, P, Camp(w), east, Pace.Quick);
        TestKit.Give(Items.Pack(w, P), Resource.Stone, 60);
        Assert.True(Trip.StepMinutes(w, P, Camp(w), east, Pace.Quick) > light); // a load picked up this minute counts
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(Trip.StepMinutes(w, P, Camp(w), east, Pace.Quick), Trip.StepMinutes(loaded, P, Camp(loaded), east, Pace.Quick));
    }

    // ---- Trip.Routes: the one route search ----

    /// Walks the route to the farthest tile within a few steps, one Travel a step, and checks each step's
    /// forecast at the minute it begins, the route's total and its calories against what the walk took.
    static void RouteAgrees(World w, AgentId who, double kcalTolerancePct)
    {
        var routes = Trip.Routes(w, who, maxMinutes: 60);
        var target = routes.Reached.Where(t => routes.RouteTo(t).Count is >= 3 and <= 5)
            .OrderByDescending(t => routes.RouteTo(t).Count).ThenBy(t => t).First();
        double Held() => w.Agent(who).Members.Sum(b => b.CaloriesKcal + b.FatKg * Body.KcalPerKgFat);
        double before = Held();
        int walked = 0;
        var at = w.Agent(who).Tile;
        foreach (var next in routes.RouteTo(target))
        {
            int forecast = Trip.StepMinutes(w, who, at, next, Pace.Quick);
            var dir = Enum.GetValues<Direction>().Single(d => Map.Step(at, d) == next);
            var r = Sim.Step(w, new Travel(who, dir), Verbs.UntilDoneMinutes);
            Assert.Equal((Traversal.Arrived, forecast), (r.Reason, r.MinutesElapsed));
            walked += r.MinutesElapsed;
            at = next;
        }
        Assert.Equal(target, w.Agent(who).Tile);
        Assert.Equal(routes.Minutes[target], walked);
        Assert.InRange(before - Held(), routes.Kcal(target)!.Value * (1 - kcalTolerancePct), routes.Kcal(target)!.Value * (1 + kcalTolerancePct));
    }

    /// A day into heavy snow, with every east-west edge near camp trodden hard: deep loose snow, where a trail pays.
    static World TroddenSnow()
    {
        var w = Bare(-10);
        w.FixedWeather = new(0, WeatherCondition.HeavySnow, -10, 0.2, 0.6, 1, WindDirection.North);
        w.Minute = 1440;
        var camp = Camp(w);
        for (int dx = -6; dx < 6; dx++)
            for (int dy = -6; dy <= 6; dy++)
                w.Trails[new EdgeId(new TileId(camp.X + dx, camp.Y + dy), new TileId(camp.X + dx + 1, camp.Y + dy))] = new EdgeTrodden(0.9, w.Minute);
        return w;
    }

    [Fact]
    public void Routes_OverTroddenEdges_AgreeWithTheWalkMade_ForAPersonAndAnAnimal()
    {
        var person = TroddenSnow();
        Assert.True(Ground.Underfoot(person, Camp(person)).SnowDepthM > 0.2);
        RouteAgrees(person, P, 0.02);
        var w = TroddenSnow();
        var caribou = Agents.Spawn(w, Species.Caribou, Map.Step(Camp(w), Direction.South));
        RouteAgrees(w, caribou, 0.001);
    }

    [Fact]
    public void Routes_ForAPerson_AgreeWithTheWalkMade()
    {
        var w = Bare(-5);
        RouteAgrees(w, P, 0.02);
    }

    [Fact]
    public void Routes_ForAnAnimal_AgreeWithTheWalkMade()
    {
        var w = Bare(-5);
        var herd = Agents.SpawnGroup(w, Species.Caribou, Map.Step(Camp(w), Direction.South), 3)[0];
        RouteAgrees(w, herd, 0.001);
    }

    [Fact]
    public void Routes_ForAPerson_StayOnGroundItHasSeen()
    {
        var w = Bare(-5);
        var explored = Memories.Of(Me(w)).Explored!;
        var routes = Trip.Routes(w, P);
        Assert.Contains(routes.Reached, t => t != Camp(w));
        Assert.All(routes.Reached, t => Assert.True(t == Camp(w) || explored.Contains(t), $"{t} is unexplored"));
    }

    [Fact]
    public void Routes_StopAtTheCutoff()
    {
        var w = Bare(-5);
        var herd = Agents.Spawn(w, Species.Caribou, Camp(w));
        var routes = Trip.Routes(w, herd, maxMinutes: 30);
        Assert.All(routes.Reached, t => Assert.InRange(routes.Minutes[t], 0, 30));
        Assert.True(Trip.Routes(w, herd, maxMinutes: 120).Minutes.Count > routes.Minutes.Count);
    }

    [Fact]
    public void Routes_UntilStopsAtTheFirstTileItAccepts_InMinutesThenTileOrder()
    {
        var w = Bare(-5);
        var herd = Agents.Spawn(w, Species.Caribou, Camp(w));
        var settled = new List<(int Minutes, TileId Tile)>();
        var routes = Trip.Routes(w, herd, until: (t, minutes) => { settled.Add((minutes, t)); return settled.Count == 20; });
        Assert.Equal(20, settled.Count);
        Assert.Equal(settled.OrderBy(s => s.Minutes).ThenBy(s => s.Tile), settled);
        Assert.Equal(20, routes.Minutes.Count);
    }

    /// A lake row with land on either side: the shore, a run of water tiles, the far shore.
    static (TileId West, TileId East) AcrossALake(World w)
    {
        var m = Map.Of(w);
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x + 5 < m.Width; x++)
            {
                var row = Enumerable.Range(x, 6).Select(i => new TileId(i, y)).ToList();
                bool land(TileId t) => m.TerrainAt(t) is not (Terrain.Water or Terrain.DeepWater or Terrain.Mountain);
                if (land(row[0]) && land(row[5]) && row.Skip(1).Take(4).All(t => m.TerrainAt(t) is Terrain.Water or Terrain.DeepWater)) return (row[0], row[5]);
            }
        throw new InvalidOperationException("No lake four tiles wide on this map");
    }

    [Fact]
    public void Routes_AFlyerCrossesALakeInAStraightLine_AndStopsAtTheMountainWall()
    {
        var w = Bare(-5);
        var m = Map.Of(w);
        var (west, east) = AcrossALake(w);
        var raven = Agents.SpawnGroup(w, Species.Raven, west, 3)[0];
        var routes = Trip.Routes(w, raven);
        Assert.Equal(Enumerable.Range(west.X + 1, 5).Select(x => new TileId(x, west.Y)), routes.RouteTo(east));
        Assert.Equal(5 * Traversal.FlightMinutesPerTile(w.Agent(raven))!.Value, routes.Minutes[east]);
        Assert.DoesNotContain(routes.Reached, t => m.TerrainAt(t) == Terrain.Mountain);
        Assert.Contains(routes.Reached, t => MapGen.Neighbors(t).Any(n => m.InBounds(n) && m.TerrainAt(n) == Terrain.Mountain));

        var herd = Agents.SpawnGroup(w, Species.Caribou, west, 3)[0];
        var walking = Trip.Routes(w, herd);
        Assert.True(!walking.Reaches(east) || walking.Minutes[east] > routes.Minutes[east]);
    }

    [Fact]
    public void Routes_OfTwoEqualWays_TakeTheOneThroughTheLowerTile_EveryRun()
    {
        var w = Bare(-5);
        // Flat minutes a tile on the wing: south-then-west and west-then-south cost the same, and south is
        // looked at first, so only the tie-break sends it west.
        var start = Camp(w);
        var corner = new TileId(start.X - 1, start.Y + 1);
        var raven = Agents.SpawnGroup(w, Species.Raven, start, 3)[0];
        var path = Trip.Routes(w, raven).RouteTo(corner);
        Assert.Equal([new TileId(start.X - 1, start.Y), corner], path); // (x-1, y) sorts before (x, y+1)
        var again = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(path, Trip.Routes(again, raven).RouteTo(corner));
    }

    // ---- Work and Fauna: what an animal's water and rest pay ----

    static double KcalHeld(BodyState b) => b.CaloriesKcal + b.FatKg * Body.KcalPerKgFat;

    /// A thirsty caribou beside a lake drinks what Fauna.WaterHere says, for nothing, less the minute's loss.
    [Fact]
    public void Water_FromALake_AgreesWithWhatTheAnimalDrinks()
    {
        var w = PlannerTests.Strip(water: [new TileId(10, 2)]);
        var caribou = PlannerTests.Animal(w, Species.Caribou, new TileId(10, 1), hydrationPct: 0.4);
        var water = Assert.IsType<Source>(Fauna.WaterHere(w, caribou));
        Assert.Equal(0, water.KcalPerUnit);
        double before = caribou.Body.HydrationMl;
        Sim.Advance(w, 1);
        Assert.Equal(before - Body.WaterLossMlPerMinute(caribou.Body, sleeping: false) + water.PerMinute, caribou.Body.HydrationMl, 6);
    }

    /// Snow pays water slower, and every ml costs what melting and warming it costs (Work.SnowKcalPerMl): a wolf, which
    /// does not graze, loses its burn and the snow's kcal and nothing else.
    [Fact]
    public void Water_FromSnow_AgreesWithWhatTheAnimalDrinks_AndWhatItCosts()
    {
        var w = PlannerTests.Strip(sky: new WeatherSample(0, WeatherCondition.LightSnow, -10, 0.1, 0.8, 0.8, WindDirection.North));
        Sim.Advance(w, 1440);
        var wolf = PlannerTests.Animal(w, Species.Wolf, new TileId(20, 1), hydrationPct: 0.4);
        var snow = Assert.IsType<Source>(Fauna.WaterHere(w, wolf));
        Assert.Equal(0.117, snow.KcalPerUnit, 9);
        var lake = PlannerTests.Strip(water: [new TileId(10, 2)]);
        Assert.True(snow.PerMinute < Fauna.WaterHere(lake, PlannerTests.Animal(lake, Species.Wolf, new TileId(10, 1)))!.Value.PerMinute);
        double ml = wolf.Body.HydrationMl, kcal = KcalHeld(wolf.Body), burn = Fauna.BurnKcalPerMinute(wolf.Body);
        Sim.Advance(w, 1);
        Assert.Equal(ml - Body.WaterLossMlPerMinute(wolf.Body, sleeping: false) + snow.PerMinute, wolf.Body.HydrationMl, 6);
        Assert.Equal(kcal - burn - snow.PerMinute * snow.KcalPerUnit, KcalHeld(wolf.Body), 3);
    }

    /// Fourteen hungry caribou on the only half-grazed lichen tile, over its capacity: each takes what Fauna.GrazeHere says,
    /// its full rate x what is left x capacity ÷ load (decision on #83, 2026-11-24: twenty caribou's intake on an average
    /// tile, x what is left; load by intake), less its minute's burn.
    [Fact]
    public void Grazing_WithCrowding_AgreesWithWhatTheAnimalEats()
    {
        var w = FoodTests.Pasture();
        var tile = new TileId(20, 1);
        foreach (var t in w.PoolCache!.Keys.Where(t => t != tile).ToList()) w.PoolCache[t] = []; // nowhere better to go
        Pools.SetGrazed(w, new PoolId(tile, 0), Resource.Usnea, 0.5);
        var herd = Enumerable.Range(0, 14).Select(_ => PlannerTests.Animal(w, Species.Caribou, tile)).ToList();
        foreach (var a in herd) a.Body.CaloriesKcal = 0;
        var row = SpeciesTable.Of(Species.Caribou);
        double load = herd.Sum(a => row.GrazeKcalPerMinute * Math.Pow(a.Body.WeightKg / row.WeightKg, 0.75));
        double capacity = 20 * row.GrazeKcalPerMinute * 0.5;
        Assert.True(load > capacity, $"load {load:F1} under capacity {capacity:F1}");
        var caribou = herd[0];
        var graze = Assert.IsType<Source>(Fauna.GrazeHere(w, caribou));
        Assert.Equal(row.GrazeKcalPerMinute * 0.5 * capacity / load, graze.PerMinute, 9);
        double before = KcalHeld(caribou.Body), burn = Fauna.BurnKcalPerMinute(caribou.Body);
        Sim.Advance(w, 1);
        Assert.Equal(before - burn + graze.PerMinute, KcalHeld(caribou.Body), 2);
    }

    /// A tired wolf's minute asleep buys what Fauna.RestPerMinute says: at its den in full, elsewhere a lighter sleep.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rest_AgreesWithWhatAMinuteAsleepBuys(bool atItsDen)
    {
        var w = PlannerTests.Strip();
        Sim.Advance(w, 3 * 60); // noon: outside a wolf's hours
        var at = new TileId(20, 1);
        var wolf = w.Agent(Agents.Spawn(w, Species.Wolf, at, atItsDen ? at : new TileId(30, 1)));
        wolf.Body.EnergyMinutes = 0.2 * Body.MaxEnergyMinutes;
        double rest = Fauna.RestPerMinute(wolf, at), before = wolf.Body.EnergyMinutes;
        Assert.Equal(Body.EnergyDeltaPerMinute(sleeping: true, SpeciesTable.Of(Species.Wolf).SleepHoursPerDay) * (atItsDen ? 1 : 0.75), rest, 9);
        Sim.Advance(w, 1);
        Assert.Equal(before + rest, wolf.Body.EnergyMinutes, 6);
    }

    /// Combat.KillChance against fresh headless pack hunts (trials the table was not measured on): within three standard
    /// deviations of 100 hunts. A pair against a herd of bison, a pack of six against it, a lone wolf on caribou, hyenas on megaloceros.
    [Theory]
    [InlineData(Species.Wolf, 2, Species.Bison, 4)]
    [InlineData(Species.Wolf, 6, Species.Bison, 4)]
    [InlineData(Species.Wolf, 1, Species.Caribou, 1)]
    [InlineData(Species.Hyena, 2, Species.Megaloceros, 3)]
    public void KillChance_AgreesWithHeadlessPackHunts(Species predator, int hunters, Species quarry, int herd)
    {
        double forecast = Combat.KillChance(predator, hunters, quarry, herd);
        double measured = KillOdds.Measure(predator, hunters, quarry, herd, trials: 100, firstSeed: 5000);
        double tolerance = Math.Max(0.05, 3 * Math.Sqrt(forecast * (1 - forecast) / 100));
        Assert.True(Math.Abs(forecast - measured) <= tolerance, $"forecast {forecast:F2}, measured {measured:F2} (within {tolerance:F2})");
    }

    // ---- Body: health ----

    static BodyState Bleeding(double severity)
    {
        var b = BodyState.BaselineHuman();
        Effects.Add(b, EffectKind.Bleeding, severity);
        return b;
    }

    [Fact]
    public void TheHealthPriority_RanksHarmByWhereItIsGoing()
    {
        var whole = BodyState.BaselineHuman();
        var scraped = BodyState.BaselineHuman();
        scraped.BloodPct = 0.97;
        Assert.Equal(Priority.None, Priorities.HealthPriority(whole));
        Assert.Equal(Priority.Want, Priorities.HealthPriority(scraped));
        Assert.Equal(Priority.Need, Priorities.HealthPriority(Bleeding(0.05)));
        var deep = Bleeding(0.5);
        Assert.True(deep.BloodPct > 0.99);
        Assert.Equal(Priority.Critical, Priorities.HealthPriority(deep)); // the blood is still full, but not for long
        Assert.True(Body.LowestBloodPct(Bleeding(0.3)) < Body.LowestBloodPct(Bleeding(0.1)));
    }

    /// The blood the bleeding leaves is about the lowest the blood falls, living it out by a fire: healing only lifts it.
    [Fact]
    public void TheBleedingForecast_AgreesWithTheBloodLost()
    {
        Gen.Double[0.02, 0.3].Check(severity =>
        {
            var w = Bare(15);
            var body = Me(w).Body;
            Effects.Add(body, EffectKind.Bleeding, severity);
            double forecast = Body.LowestBloodPct(body), lowest = body.BloodPct;
            while (Effects.Severity(body, EffectKind.Bleeding) > 0)
            {
                Sim.Advance(w, 10);
                lowest = Math.Min(lowest, body.BloodPct);
            }
            Assert.InRange(lowest, forecast - 0.01, forecast + 0.03);
        }, iter: 20);
    }

    // ---- refusals name what is missing ----

    [Fact]
    public void ARefusalNamesWhatIsMissing_AndHowMuch()
    {
        var w = WithStriker();
        Give(w, (Resource.BirchBark, 0.02), (Resource.Stick, 0.4));
        var tinder = Sim.Refusal(w, Strike(0.05, 0))!;
        Assert.Equal("not enough birch bark", tinder.Reason);
        var short1 = Assert.IsType<MissingResource>(tinder.Missing);
        Assert.Equal([Resource.BirchBark], short1.AnyOf);
        Assert.Equal(0.03, short1.Kg, 9);
        var kindling = Assert.IsType<MissingResource>(Sim.Refusal(w, Strike(0.02, 1))!.Missing);
        Assert.Equal(0.6, kindling.Kg, 9);

        var noTool = At();
        Give(noTool, (Resource.BirchBark, 1));
        Assert.Equal(new MissingTool(ToolType.FireStriker), Sim.Refusal(noTool, Strike(0.05, 0))!.Missing);
    }

    [Fact]
    public void AFireTooCoolForAFuel_NamesTheHeatItNeeds()
    {
        var w = At();
        Give(w, (Resource.OakBranch, 2));
        var pit = w.Fires[PitWith(w, Resource.Tinder, 0.2)];
        var hot = Assert.IsType<MissingState>(Sim.Refusal(w, new AddFuel(P, Resource.OakBranch, 1))!.Missing);
        Assert.Equal((Lack.FireHeatF, Fire.Props[Resource.OakBranch].MinFireTempF, Fire.CatchTemperatureF(pit)), (hot.Lack, hot.Need!.Value, hot.Have!.Value));
    }

    [Fact]
    public void WorkMakeAndTravel_NameWhatTheyLack()
    {
        var w = At(new TreePool("trees", Resource.BirchFirewood, 100));
        w.Minute = Clock(10 * 60);
        Assert.Equal(new MissingTool(ToolType.Axe), Sim.Refusal(w, new Gather(P, 0))!.Missing);
        var stone = Assert.IsType<MissingResource>(Sim.Refusal(w, new Make(P, "sharp_rock"))!.Missing);
        Assert.Equal([Resource.Stone], stone.AnyOf);
        Assert.Equal(1, stone.Count); // one stone short, whatever it weighs
        Assert.Equal((Items.Resources[Resource.Stone].MinPieceKg + Items.Resources[Resource.Stone].MaxPieceKg) / 2, stone.Kg, 9);
        Assert.Equal(new MissingResource(Resource.Honey, 1).AnyOf, ((MissingResource)Sim.Refusal(w, new Eat(P, Resource.Honey, 1))!.Missing!).AnyOf);
        var body = Me(w).Body;
        foreach (var leg in new[] { BodyTarget.LeftLeg, BodyTarget.RightLeg })
            for (int i = 0; i < 6 && Body.GetCapacities(body).Moving > Traversal.LameMovingPct; i++)
                Body.Damage(body, new DamageInfo(1.0, DamageType.Blunt, leg), Rng.ForSystem(i, "lame"));
        var lame = Assert.IsType<MissingState>(Sim.Refusal(w, new Travel(P, Direction.East))!.Missing);
        Assert.Equal((Lack.MovingPct, Traversal.LameMovingPct, Body.GetCapacities(body).Moving), (lame.Lack, lame.Need, lame.Have));
    }
}
