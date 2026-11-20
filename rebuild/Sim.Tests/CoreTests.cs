namespace Survival.Tests;

public class CoreTests
{
    static World Play(int seed)
    {
        var world = World.New(seed);
        Sim.Step(world, new Wait(AgentId.Player), 600);
        Sim.Step(world, new Rest(AgentId.Player), 120);
        world.Rng("test").NextUlong(); // stand-in for a system drawing from its stream
        // Real weather can kill an unsheltered agent with no fire; determinism holds either way.
        if (Body.IsAlive(world.Agent(AgentId.Player).Body)) Sim.Step(world, new Wait(AgentId.Player), 900);
        return world;
    }

    [Fact]
    public void SameSeedAndActions_GiveIdenticalWorld()
    {
        Assert.Equal(Save.ToJson(Play(7)), Save.ToJson(Play(7)));
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentRngStreams()
    {
        Assert.NotEqual(World.New(1).Rng("fire").NextUlong(), World.New(2).Rng("fire").NextUlong());
        Assert.NotEqual(World.New(1).Rng("fire").NextUlong(), World.New(1).Rng("body").NextUlong());
    }

    [Fact]
    public void SaveRoundTrips_AndResumesRngExactly()
    {
        var world = Play(42);
        string json = Save.ToJson(world);
        var loaded = Save.FromJson(json);

        Assert.Equal(json, Save.ToJson(loaded));
        Assert.True(json.Length < 1_000_000, $"Save is {json.Length} bytes");
        Assert.Equal(world.Rng("test").NextUlong(), loaded.Rng("test").NextUlong());
        Assert.Equal(world.Agent(AgentId.Player).Body.CoreTempF, loaded.Agent(AgentId.Player).Body.CoreTempF);
    }

    [Fact]
    public void Wait_DrainsAndAdvancesClock()
    {
        var world = World.New(1);
        var result = Sim.Step(world, new Wait(AgentId.Player), 60);

        Assert.Equal(60, world.Minute);
        Assert.Equal(60, result.MinutesElapsed);
        Assert.Null(result.InterruptedReason);
        var body = world.Agent(AgentId.Player).Body;
        Assert.True(body.EnergyMinutes < 900);
        Assert.True(body.CaloriesKcal < 1500);
        Assert.True(body.HydrationMl < 3000);
    }

    [Fact]
    public void WaitingTooLong_EmitsTiredThenInterruptsOnExhaustion()
    {
        var world = World.New(1);
        world.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North); // mild: this is about sleep, not cold
        var result = Sim.Step(world, new Wait(AgentId.Player), 24 * 60);

        Assert.Equal("exhausted", result.InterruptedReason);
        Assert.Equal(900, result.MinutesElapsed); // starts with 900 waking minutes left
        int tired = result.Events.FindIndex(e => e is EffectStarted { Kind: EffectKind.Tired });
        Assert.True(tired >= 0, "Tired should start before exhaustion");
        Assert.IsType<Exhausted>(result.Events[^1]);
    }

    [Fact]
    public void Sleep_StopsWhenFullyRested()
    {
        var world = World.New(1);
        Sim.Step(world, new Wait(AgentId.Player), 120); // 780 of 960 left; sleep refills 2/min
        var result = Sim.Step(world, new Sleep(AgentId.Player), 600);

        Assert.Equal("fully rested", result.InterruptedReason);
        Assert.Equal(90, result.MinutesElapsed);
        Assert.IsType<FullyRested>(result.Events[^1]);
        Assert.Equal(1.0, world.Agent(AgentId.Player).Body.EnergyPct);
    }

    [Fact]
    public void Rest_DoesNotRestoreEnergy_OnlySleepDoes()
    {
        var world = World.New(1);
        Sim.Step(world, new Rest(AgentId.Player), 60);
        Assert.Equal(840, world.Agent(AgentId.Player).Body.EnergyMinutes);
    }

    [Fact]
    public void BleedingOut_EmitsDiedAndInterrupts_ThenTheDeadCannotAct()
    {
        var world = World.New(1);
        Effects.Add(world.Agent(AgentId.Player).Body, EffectKind.Bleeding, 1.0);
        var result = Sim.Step(world, new Wait(AgentId.Player), 600);

        Assert.Equal("died", result.InterruptedReason);
        Assert.IsType<Died>(result.Events[^1]);
        Assert.Throws<InvalidOperationException>(() => Sim.Step(world, new Wait(AgentId.Player), 1));
    }

    [Fact]
    public void Cold_ReachesTheBodyThroughWeather()
    {
        var warm = World.New(1);
        warm.FixedWeather = new WeatherSample(0, WeatherCondition.Clear, 20, 0, 0, 0, WindDirection.North);
        var cold = World.New(1);
        cold.FixedWeather = new WeatherSample(0, WeatherCondition.Clear, -30, 0.5, 0, 0, WindDirection.North);
        Sim.Step(warm, new Wait(AgentId.Player), 120);
        Sim.Step(cold, new Wait(AgentId.Player), 120);
        Assert.True(cold.Agent(AgentId.Player).Body.CoreTempF < warm.Agent(AgentId.Player).Body.CoreTempF);
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        var world = World.New(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Sim.Step(world, new Wait(AgentId.Player), 0));
        Assert.Throws<KeyNotFoundException>(() => Sim.Step(world, new Wait(new AgentId(99)), 1));
    }
}
