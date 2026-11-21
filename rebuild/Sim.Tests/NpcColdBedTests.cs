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
    static (World World, AgentId Npc, NpcMind Mind) ColdCamp(WeatherSample sky, double energyPct = 0.10, double coreF = 96.3)
    {
        var w = World.New(11);
        w.FixedWeather = sky;
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        w.Minute = At(30, 0, 3);
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
    /// wood): with the legs and the warmth for the walk, it goes back to them, and home is there again.
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
        mind.Need = Need.Rest;
        var (a, minutes) = Npc.Decide(w, w.Agent(npc), []);
        output.WriteLine($"{a} for {minutes} min; camp {mind.Camp}");
        Assert.Equal(new Travel(npc, dir, Traversal.IsHazardous(w, camp) || Traversal.IsHazardous(w, next) ? Pace.Careful : Pace.Quick), a);
        Assert.Equal(next, mind.Camp);
    }
}
