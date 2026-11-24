using Survival;
using Xunit;

/// Phase-1 gate: the fire's heat actually reaches the body through Step.
public class FireBodyTests
{
    static readonly AgentId P = AgentId.Player;

    static World LitCamp(int seed, WeatherSample? sky = null)
    {
        var world = TestWorlds.Bare(seed);
        TestKit.Supply(world);
        world.FixedWeather = sky;
        for (int i = 0; i < 20 && Fire.PitAt(world, Map.Of(world).Camp) == null; i++)
            Sim.Step(world, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), 1);
        Assert.NotNull(Fire.PitAt(world, Map.Of(world).Camp));
        Sim.Step(world, new AddFuel(P, Resource.Birch, 3.0), 1);
        return world;
    }

    [Fact]
    public void RestingByAFire_StaysWarmerThanRestingWithout()
    {
        // -10 °C pinned: the seeded sky on July 1 may be mild, and this is about the fire's heat.
        var sky = new WeatherSample(0, WeatherCondition.Clear, -10, 0.15, 0, 0.3, WindDirection.North);
        var cold = TestWorlds.New(1);
        cold.FixedWeather = sky;
        var warm = LitCamp(1, sky);
        double coldStartF = cold.Agent(P).Body.CoreTempF;
        Sim.Step(cold, new Rest(P), 240);
        Sim.Step(warm, new Rest(P), 240);

        Assert.True(warm.Agent(P).Body.CoreTempF > cold.Agent(P).Body.CoreTempF + 0.5,
            $"fire {warm.Agent(P).Body.CoreTempF:F2}F vs no fire {cold.Agent(P).Body.CoreTempF:F2}F");
        Assert.True(cold.Agent(P).Body.CoreTempF < coldStartF, "-10C with no fire should cool you");
    }

    [Fact]
    public void FireAndBodyState_RoundTripThroughSave()
    {
        var world = LitCamp(2);
        Sim.Step(world, new Rest(P), 60);
        string json = Save.ToJson(world);
        Assert.True(json.Length < 1_000_000);
        var loaded = TestWorlds.Load(json);
        Sim.Step(world, new Rest(P), 120);
        Sim.Step(loaded, new Rest(P), 120);
        Assert.Equal(Save.ToJson(world), Save.ToJson(loaded));
    }
}
