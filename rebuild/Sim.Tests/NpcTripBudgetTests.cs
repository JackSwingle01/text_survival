using Survival;
using Xunit;
using Xunit.Abstractions;

/// The NPC fire-keeping review (round three): one trip budget for every walk out - warmth, energy and the
/// hour it must be home by - and the fire, hunting and housekeeping findings that came with it.
public class NpcTripBudgetTests(ITestOutputHelper output)
{
    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 20, 0.0, 0, 1, WindDirection.North);

    /// Minute of a day this many days after the start, at this hour.
    static long At(int day, int hour, int minute = 0) => day * 1440L + (hour - 9) * 60 + minute;

    static void NoHerds(World w)
    {
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
    }

    /// A world on this seed under this sky, at this minute, with no herds and an NPC spawned at tile.
    static (World World, AgentId Npc, NpcMind Mind) Setup(int seed, WeatherSample sky, long minute, TileId? at = null, TileId? camp = null)
    {
        var w = World.New(seed);
        w.FixedWeather = sky;
        NoHerds(w);
        w.Minute = minute;
        var home = camp ?? Map.Of(w).Camp;
        var npc = Npc.Spawn(w, at ?? home, home, boldness: 0.5);
        return (w, npc, (NpcMind)w.Agent(npc).Mind);
    }

    // ---- 1. one chill check for every trip ----

    /// Seed 17, day 16, 10:30-17:04: ten walks from camp one tile out and straight back. The walk home's
    /// chill was reckoned without the heat its clothes had stored, so one tile out it was "cold" and turned
    /// home, and at camp, warm again by its own core, it set out again. Out from camp and straight back,
    /// having done nothing, because it turned cold on the way, no longer happens.
    [Fact]
    public void Seed17_NoLongerShuttlesOutAndBackFromCampInTheCold()
    {
        var w = World.New(17);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        var loops = new List<string>();
        (long Minute, TileId Tile)? outStep = null;
        bool did = false, chilled = false;
        while (w.Minute < 17 * 1440L && Body.IsAlive(w.Agent(npc).Body))
        {
            foreach (var e in Sim.Advance(w, 1))
            {
                if (e is Moved m && m.Who == npc)
                {
                    if (m.From == mind.Camp) (outStep, did, chilled) = ((m.Minute, m.To), false, false);
                    else if (m.To == mind.Camp && outStep is { } o && o.Tile == m.From && !did && chilled) loops.Add($"{m.From} at {o.Minute}-{m.Minute}");
                    else outStep = null;
                }
                else if (e is Gathered or Stashed or Unstashed or Ate or Drank or FuelAdded or TreeFelled or Dropped && e.ToString()!.Contains(npc + ",")) did = true;
            }
            if (outStep != null && w.Agent(npc).Tile != mind.Camp && mind.Need == Need.Warmth) chilled = true;
        }
        output.WriteLine($"{loops.Count} out-and-back walks turned by the cold: {string.Join("; ", loops.Take(8))}");
        Assert.True(loops.Count <= 1, $"{loops.Count} out-and-back walks turned by the cold");
    }

    /// Seed 7, day 22: wanting wood, one tile from camp at the trees, core 98.1 in a hard frost, it chopped
    /// for an hour and came home at 93.4 (the away branch of Warm checked nothing). Work out here is held
    /// to the trip budget: it comes home warm enough.
    [Fact]
    public void Seed7_WorkAtTheTreesInAFrost_ComesHomeWarmEnough()
    {
        var frost = new WeatherSample(0, WeatherCondition.Cloudy, -30, 0.8, 0, 0.8, WindDirection.North);
        var camp = new TileId(7, 42);
        var trees = new TileId(6, 42);
        var (w, npc, mind) = Setup(7, frost, At(22, 13, 45), trees, camp);
        Assert.Equal(camp, Map.Of(w).Camp);
        Assert.Contains(Pools.At(w, trees), p => p is TreePool);
        var body = w.Agent(npc).Body;
        body.CoreTempF = 98.1;
        mind.Pack.Kg.Remove(Resource.Kindling); // the camp short of fuel: what it is out here for
        var (first, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"chose {first} x{minutes}");
        double? homeCoreF = null;
        while (w.Minute < At(22, 18) && homeCoreF == null && Body.IsAlive(body))
            foreach (var e in Sim.Advance(w, 1))
                if (e is Moved m && m.Who == npc && m.To == camp) homeCoreF = body.CoreTempF;
        output.WriteLine($"home at minute {w.Minute} with core {homeCoreF:0.00}");
        Assert.NotNull(homeCoreF);
        Assert.True(homeCoreF >= 96.3, $"came home at {homeCoreF:0.00}F");
    }

    // ---- 2. a far trip budgets the walk back ----

    /// Seed 5, day 2: looking for timber, it walked out to 27 tiles from camp with its energy running out
    /// (0.63 to 0), then gathered fuel for a camp it could not reach until ten at night, lighting nothing.
    /// Every walk out now budgets the walk back: it is never out at nightfall far from camp, nor spent away from it.
    [Fact]
    public void Seed5_ExploringForTimber_IsNeverStrandedFarFromCamp()
    {
        var w = World.New(5);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        int spentAway = 0, nightFar = 0, maxOut = 0;
        while (w.Minute < 5 * 1440L && Body.IsAlive(w.Agent(npc).Body))
        {
            Sim.Advance(w, 1);
            var a = w.Agent(npc);
            int far = MapGen.Manhattan(a.Tile, mind.Camp);
            maxOut = Math.Max(maxOut, far);
            int mod = Weather.MinuteOfDay(w.Minute);
            if (far > 0 && a.Body.EnergyPct < 0.02) spentAway++;
            if (far > 2 && (mod >= 21 * 60 || mod < 5 * 60)) nightFar++;
        }
        output.WriteLine($"alive {Body.IsAlive(w.Agent(npc).Body)}; furthest {maxOut} tiles from camp; spent away {spentAway} min; night far out {nightFar} min; camp {mind.Camp}");
        Assert.Equal(0, spentAway);
        Assert.Equal(0, nightFar);
    }

    /// Out of reach of home - too spent to walk it - and carrying the makings of a fire, it makes camp where it
    /// stands, and the fire there, rather than gather fuel for a camp it cannot get to. (Without fuel enough to keep
    /// a fire it walks on: NpcWarmingWalkTests.FarOutAndCold_NothingToBurn_DoesNotMakeCampThere.)
    [Fact]
    public void TooSpentToWalkHome_MakesCampWhereItIs()
    {
        var w0 = World.New(5);
        var camp = Map.Of(w0).Camp;
        var far = Enumerable.Range(12, 10).Select(d => new TileId(camp.X + d, camp.Y)).First(t => Map.Of(w0).TerrainAt(t).IsPassable());
        var (w, npc, mind) = Setup(5, Mild, At(2, 15), far, camp);
        Items.Add(mind.Pack, Resource.Birch, 4);
        var body = w.Agent(npc).Body;
        body.EnergyMinutes = 0.05 * Body.MaxEnergyMinutes;
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, camp {mind.Camp}");
        Assert.Equal(far, mind.Camp);
        Assert.IsNotType<Travel>(a);
    }

    // ---- 3. one home-by hour for every trip ----

    /// Winter (sunset 16:00): the evening wood fetch went by 21:00 alone, so at 19:05 and 19:30 it walked out
    /// for wood three hours after dark. Every trip must be home by Npc.HomeMinuteOfDay.
    [Theory]
    [InlineData(11, 0, true)]
    [InlineData(15, 45, false)]
    [InlineData(19, 5, false)]
    [InlineData(19, 30, false)]
    public void WinterEvening_NoTripForWoodAfterSunset(int hour, int minute, bool travels)
    {
        long at = At(180, hour, minute);
        Assert.Equal(Season.Winter, Weather.SeasonAt(at));
        var (w, npc, mind) = Setup(17, Mild, at);
        var camp = mind.Camp;
        var tree = new TileId(camp.X, camp.Y - 3);
        mind.Knows.Places[camp] = new TileMemory { SeenMinute = at, VisitedMinute = at };
        mind.Knows.Places[tree] = new TileMemory { SeenMinute = at, VisitedMinute = at, Trees = true };
        var (a, n) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{hour}:{minute:00} (sunset {Weather.SunsetHour(at)}): {a} x{n}, need {mind.Need}");
        Assert.Equal(travels, a is Travel);
    }

    // ---- 4. bed beside logs that will never catch ----

    /// A kindling fire at 300F with 5 kg of oak on it (oak takes at 500F), nothing else to burn: it slept
    /// two hours by Fire.FuelHours, which counted the oak, and the fire died with the oak unburned. It
    /// reckons by the hours the fire will burn (Fire.HoursItWillBurn) and sees to it before sleeping.
    [Fact]
    public void Bed_BesideOakTheFireIsTooCoolFor_DoesNotSleepItOut()
    {
        var (w, npc, mind) = Setup(17, Mild, At(2, 23));
        var a = w.Agent(npc);
        a.Body.EnergyMinutes = Body.MaxEnergyMinutes * 0.4;
        foreach (var r in new[] { Resource.Kindling, Resource.Oak, Resource.Birch, Resource.Pine }) mind.Pack.Kg.Remove(r);
        var pit = new FireState { Tile = mind.Camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Kindling] = 0.3 }, UnburnedKg = { [Resource.Oak] = 5 } };
        w.Fires[new FireId(w.NextFireId++)] = pit;
        double willBurnMinutes = Fire.HoursItWillBurn(pit) * 60;
        var (act, n) = Npc.Decide(w, a, []);
        output.WriteLine($"fuel hours {Fire.FuelHours(pit):0.0}, will burn {willBurnMinutes:0} min: {act} x{n}");
        Assert.True(Fire.FuelHours(pit) > 5 && willBurnMinutes < 60);
        Assert.False(act is Sleep && n > willBurnMinutes, $"slept {n} minutes beside a fire that will burn {willBurnMinutes:0}");
    }

    /// The same with its reserve of kindling in hand and a little more on the stash: the reserve goes on with
    /// the rest to get the oak going before bed (old: the reserve was kept back, so the 0.3 kg it would spend
    /// left the fire too cool for oak), and the oak burns.
    [Fact]
    public void Bed_BesideOakTheFireIsTooCoolFor_StokesKindlingUntilTheOakTakes()
    {
        var (w, npc, mind) = Setup(17, Mild, At(2, 23));
        var a = w.Agent(npc);
        a.Body.EnergyMinutes = Body.MaxEnergyMinutes * 0.4;
        foreach (var r in new[] { Resource.Kindling, Resource.Oak, Resource.Birch, Resource.Pine }) mind.Pack.Kg.Remove(r);
        mind.Pack.Kg[Resource.Kindling] = 0.6;
        Items.Add(Items.StashAt(w, mind.Camp), Resource.Kindling, 0.6);
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = mind.Camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Kindling] = 0.3 }, UnburnedKg = { [Resource.Oak] = 5 } };
        var (act, _) = Npc.Decide(w, a, []);
        output.WriteLine($"{act}");
        Assert.Equal(new AddFuel(npc, Resource.Kindling, 0.6), act);
        var log = NpcDriver.Live(w, npc, w.Minute + 60);
        output.WriteLine(string.Join(", ", log.Where(e => e is FuelAdded or Unstashed or FireBurnedToEmbers or Gathered or FireLit).Select(e => e.ToString())));
        Fire.CatchUp(w, id, []);
        output.WriteLine($"an hour on: oak burning {w.Fires[id].BurningKg.GetValueOrDefault(Resource.Oak):0.00} kg, unburned {w.Fires[id].UnburnedKg.GetValueOrDefault(Resource.Oak):0.00}");
        Assert.True(w.Fires[id].BurningKg.GetValueOrDefault(Resource.Oak) > 0 || w.Fires[id].UnburnedKg.GetValueOrDefault(Resource.Oak) < 5, "the oak never took");
    }

    // ---- 5. hunting what it would run from ----

    /// Seed 123, day 5: a lone NPC with an axe, food only wanted (fat 10.7 kg), started a hunt on a mammoth
    /// herd and died in it; and on day 34.9, starving, again. A lone NPC never takes on a mammoth herd
    /// (Npc.WouldHunt; the other species in NpcWarmingWalkTests.WhichHerdsALoneNpcHunts).
    [Theory]
    [InlineData(0.5, false)]
    [InlineData(1.5, false)]
    public void WantingFood_LeavesAMammothHerdAlone(double shortKg, bool hunts)
    {
        var (w, npc, mind) = Setup(17, Mild, At(0, 11));
        var camp = mind.Camp;
        Items.Add(Items.StashAt(w, camp), Resource.Oak, 40); // firewood is not what it worries about
        mind.Knows.Places[camp] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Oak] = 40 } };
        var body = w.Agent(npc).Body;
        body.FatKg = mind.TargetFatKg - shortKg;
        body.CaloriesKcal = Body.MaxCaloriesKcal; // not hungry for what it carries: it is the fat it is short of
        var next = MapGen.Neighbors(camp).Where(t => Map.Of(w).TerrainAt(t).IsPassable() && Map.Blocked(Map.Of(w), camp, t) == null).OrderBy(t => t).First();
        var herd = Agents.Spawn(w, Species.Mammoth, next, 6);
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"short {shortKg} kg: {a}, need {mind.Need}");
        bool after = a is Travel t && Map.Step(camp, t.Dir) == next;
        Assert.Equal(hunts, after);
    }

    // ---- 6. smaller findings ----

    /// A pack with no room for a load: walking out to the trees would turn straight back (502 walks over 16
    /// traces, camp to the tree tile and home). It does not set out.
    [Fact]
    public void FullPack_DoesNotWalkOutForWood()
    {
        var (w, npc, mind) = Setup(17, Mild, At(0, 11));
        var a = w.Agent(npc);
        Items.Add(mind.Pack, Resource.Water, Items.CapacityKg(mind.Pack) - Items.CarriedKg(a)); // water stays in the pack at camp (Unload)
        a.Body.HydrationMl = Body.MaxHydrationMl;
        Assert.True(Items.CapacityKg(mind.Pack) - Items.CarriedKg(a) < 1.5);
        var (act, _) = Npc.Decide(w, a, []);
        output.WriteLine($"{act}");
        Assert.IsNotType<Travel>(act);
    }

    /// Banking the fire in place of a walk out kept what the walk would have had it keep in mind - the
    /// forage ground it was setting out for - though it never set out.
    [Fact]
    public void BankingTheFireInsteadOfGoing_KeepsNoGoalOfTheWalk()
    {
        var (w, npc, mind) = Setup(17, Mild, At(0, 11));
        var camp = mind.Camp;
        var m = Map.Of(w);
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 40);
        w.Agent(npc).Body.FatKg = mind.TargetFatKg - 0.5;
        (w.Tiles.TryGetValue(camp, out var ts) ? ts : w.Tiles[camp] = new TileState()).Pools = new() { [0] = new ForageState { HoursForaged = 1000, LastForagedMinute = w.Minute } };
        var near = Enumerable.Range(-6, 13).SelectMany(dx => Enumerable.Range(-6, 13).Select(dy => new TileId(camp.X + dx, camp.Y + dy)))
            .Where(t => m.InBounds(t) && MapGen.Manhattan(t, camp) <= 6).ToList();
        var ground = near.Where(t => MapGen.Manhattan(t, camp) == 3 && m.TerrainAt(t).IsPassable() && Pools.At(w, t) is [ForagePool { Density: > 0.2 }, ..]).OrderBy(t => t).First();
        foreach (var t in near)
            mind.Knows.Places[t] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, ForageDensity = t == ground ? ((ForagePool)Pools.At(w, t)[0]).Density : 0,
                Stash = t == camp ? new() { [Resource.Birch] = 40 } : null };
        // A small fire burning: going out, it banks it first.
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Birch] = 1.0 } };
        var (a, _) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a}, goal {mind.ForageGoal}");
        Assert.True(a is AddFuel or Unstash, $"chose {a}");
        Assert.Null(mind.ForageGoal);
    }

    /// Whether taking from a stash was a theft was read from whose camp it is when the journal weighed the
    /// entry, so an NPC's chores at its old camp became thefts once its camp moved. It is decided as filed.
    [Fact]
    public void UnstashingAtItsOwnCamp_StaysAChore_AfterTheCampMoves()
    {
        var (w, npc, mind) = Setup(17, Mild, At(0, 11));
        var camp = mind.Camp;
        Items.Add(Items.StashAt(w, camp), Resource.Birch, 5);
        Sim.Step(w, new Unstash(npc, Resource.Birch, 1), 1);
        var entry = Assert.Single(w.Journal[camp].OfType<Unstashed>(), u => u.Who == npc);
        Assert.False(Journal.IsNotable(w, entry));
        mind.Camp = new TileId(camp.X + 5, camp.Y);
        Assert.False(Journal.IsNotable(w, entry), "its own chore became a theft when its camp moved");
    }
}
