using Survival;
using Xunit;
using Xunit.Abstractions;

/// Bed beside a cold pit with nothing to light it (seed 11, day 30): it slept two hours in the cold at
/// two in the morning, core 96.3 to 93.8, and was dead by four.
public class NpcColdBedTests(ITestOutputHelper output)
{
    /// Seed 11's last night: -2 C, a breeze, no cloud; Weather.At gave about this.
    static readonly WeatherSample FrostyNight = new(0, WeatherCondition.Clear, -12, 0.2, 0, 0, WindDirection.North);
    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 20, 0.0, 0, 1, WindDirection.North);
    static readonly WeatherSample BitterNight = new(0, WeatherCondition.Clear, -25, 0.3, 0, 0, WindDirection.North);

    static long At(int day, int hour, int minute = 0) => day * 1440L + (hour - 9) * 60 + minute;

    /// Seed 11's state at 00:03 on day 30: at camp beside a cold pit, a hand drill and a pinch of
    /// tinder too small to strike with, nothing on the stash, core 96.3, spent.
    static (World World, AgentId Npc, NpcMind Mind) ColdCamp(WeatherSample sky, double energyPct = 0.10, double coreF = 96.3, long? minute = null)
    {
        var w = World.New(11);
        w.FixedWeather = sky;
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        w.Minute = minute ?? At(30, 0, 3);
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg.Clear();
        mind.Pack.Kg[Resource.Tinder] = 0.01;
        mind.Pack.Kg[Resource.Amadou] = 0.02;
        mind.Pack.Kg[Resource.Water] = 2;
        var body = w.Agent(npc).Body;
        body.EnergyMinutes = energyPct * Body.MaxEnergyMinutes;
        body.CoreTempF = coreF;
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute };
        return (w, npc, mind);
    }

    /// The night it died: no two hours asleep in the frost. Whatever it does, it is back on its feet
    /// before the cold has its core under the freezing line.
    [Fact]
    public void ColdPit_NothingToLight_FrostyNight_DoesNotSleepItOut()
    {
        var (w, npc, _) = ColdCamp(FrostyNight, energyPct: 0.05);
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min");
        Assert.False(a is Sleep && minutes >= 120, $"slept {minutes} minutes beside a cold pit in a frost");
        // And the night as it plays out: never a stretch asleep that takes it past the freezing line.
        var body = w.Agent(npc).Body;
        double lowest = body.CoreTempF;
        for (int hour = 0; hour < 4 && Body.IsAlive(body); hour++)
        {
            Sim.Advance(w, 60);
            lowest = Math.Min(lowest, body.CoreTempF);
        }
        output.WriteLine($"lowest core over four hours {lowest:0.0}");
        Assert.True(lowest > 93.8, $"core fell to {lowest:0.0}, as low as the two hours' sleep took it");
    }

    /// Where the night is mild, a cold pit is no reason not to sleep: two hours, as by a fire.
    [Fact]
    public void ColdPit_NothingToLight_MildNight_SleepsAsUsual()
    {
        var (w, npc, _) = ColdCamp(Mild, coreF: 98.6);
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min");
        Assert.IsType<Sleep>(a);
        Assert.Equal(120, minutes);
    }

    /// The makings of a fire left one tile off (seed 11 moved camp a tile from its own stash of tinder and
    /// wood): with the legs and the warmth for the walk, it goes back to them. Its camp is an overhang, so it
    /// fetches them home and makes the fire there, rather than move home off the shelter (Npc.FireMakings).
    [Fact]
    public void ColdPit_NothingToLight_GoesBackToTheTinderAndWoodItLeft()
    {
        var (w, npc, mind) = ColdCamp(BitterNight, energyPct: 0.45, coreF: 98.2);
        var camp = mind.Camp;
        var m = Map.Of(w);
        var dir = Enum.GetValues<Direction>().First(d => m.InBounds(Map.Step(camp, d)) && m.TerrainAt(Map.Step(camp, d)).IsPassable()
                                                          && Traversal.Refusal(w, new Travel(npc, d)) == null);
        var next = Map.Step(camp, dir);
        var stash = new Inventory();
        Items.Add(stash, Resource.Tinder, 2);
        Items.Add(stash, Resource.Kindling, 1); // tinder and birch alone are not makings: a tinder fire never takes birch
        Items.Add(stash, Resource.Birch, 9);
        (w.Tiles.TryGetValue(next, out var there) ? there : w.Tiles[next] = new TileState()).Stash = stash;
        mind.Knows.Places[next] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Tinder] = 2, [Resource.Kindling] = 1, [Resource.Birch] = 9 } };
        mind.SeeingTo = Stat.Rest;
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min; camp {mind.Camp}");
        Assert.Equal(new Travel(npc, dir, Traversal.IsHazardous(w, camp) || Traversal.IsHazardous(w, next) ? Pace.Careful : Pace.Quick), a);
        Assert.Equal(camp, mind.Camp);
        Assert.Equal(next, mind.WoodGoal);
        Assert.NotNull(mind.Knows.Places[camp].Shelter);
        // There and back with the makings, and a fire going under the overhang within the hour.
        var lit = NpcDriver.Live(w, npc, w.Minute + 60).OfType<FireLit>().Where(f => f.By == npc).Select(f => f.Tile).ToList();
        output.WriteLine($"fires lit at {string.Join(", ", lit)}; camp {mind.Camp}");
        Assert.Contains(camp, lit);
    }

    /// Freezing (warmth Critical, over rest's Critical in the hierarchy) and spent, at a cold pit with nothing to light
    /// and no makings anywhere: Warm groped the camp ground for fuel at any energy, where ColdBed lies down once spent
    /// (RestCriticalPct). The grope is held to the legs in both: it sleeps, waking before the cold has its core at the line.
    [Fact]
    public void Freezing_AndSpent_ColdPit_DoesNotGrope()
    {
        var (w, npc, mind) = ColdCamp(FrostyNight, energyPct: 0.05, coreF: 94.8);
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min, seeing to {mind.SeeingTo}");
        Assert.IsNotType<Gather>(a);
        Assert.IsType<Sleep>(a);
    }

    /// Traced (seed 11, day 13): home at dusk to a cold pit with tinder, 0.6 kg of kindling and a stash of oak. It lit
    /// the kindling, then fed the rest on to get the oak going - but a kindling fire that small never gets hot enough
    /// for oak - and groped the dark camp the rest of the night with tinder and oak and nothing to light from, core
    /// 98 to 92 by two in the morning. The kindling a fire is lit with is kept for lighting: by dawn there is still
    /// enough to light from.
    [Fact(Skip = "Waits on the fire model decision (Q9) and the oak-camp kindling design; dev/active/rebuild-deferred.md")]
    public void ColdPit_TinderOakAndAlittleKindling_KeepsKindlingToLightFromAtDawn()
    {
        var (w, npc, mind) = ColdCamp(FrostyNight, energyPct: 0.25, coreF: 98.0, minute: At(13, 20));
        mind.Pack.Kg[Resource.Tinder] = 0.3;
        mind.Pack.Kg[Resource.Kindling] = 0.6;
        var camp = mind.Camp;
        Items.Add(Items.StashAt(w, camp), Resource.Oak, 4.2);
        PickedBare(w, camp);
        long dawn = At(14, Weather.SunriseHour(At(14, 0)));
        var log = NpcDriver.Live(w, npc, dawn);
        double kindling = mind.Pack.Kg.GetValueOrDefault(Resource.Kindling) + Items.Have(Items.StashAt(w, camp), Resource.Kindling);
        output.WriteLine($"fires lit {log.OfType<FireLit>().Count(l => l.By == npc)}, kindling fed {log.OfType<FuelAdded>().Where(f => f.By == npc && f.Fuel == Resource.Kindling).Sum(f => f.Kg):0.00} kg, "
                         + $"kindling at dawn {kindling:0.00} kg, core {w.Agent(npc).Body.CoreTempF:0.0}, alive {Body.IsAlive(w.Agent(npc).Body)}");
        Assert.True(kindling >= 0.3, $"{kindling:0.00} kg of kindling left at dawn: not a light's worth");
    }

    /// Every forage pool on a tile picked over to nothing, for now.
    static void PickedBare(World w, TileId tile)
    {
        var ts = w.Tiles.TryGetValue(tile, out var t) ? t : w.Tiles[tile] = new TileState();
        ts.Pools = Enumerable.Range(0, Pools.At(w, tile).Count).Where(i => Pools.At(w, tile)[i] is ForagePool)
            .ToDictionary(i => i, i => (PoolState)new ForageState { HoursForaged = 1000, LastForagedMinute = w.Minute });
    }
}
