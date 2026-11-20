namespace Survival.Tests;

public class CoreTests
{
    static World Play(int seed)
    {
        var world = World.New(seed);
        Sim.Step(world, new Wait(AgentId.Player), 600);
        Sim.Step(world, new Rest(AgentId.Player), 120);
        world.Rng("test").NextUlong(); // stand-in for a system drawing from its stream
        Sim.Step(world, new Wait(AgentId.Player), 900);
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
        Assert.Equal(world.Agent(AgentId.Player).EnergyPct, loaded.Agent(AgentId.Player).EnergyPct);
    }

    [Fact]
    public void Wait_DrainsAndAdvancesClock()
    {
        var world = World.New(1);
        var result = Sim.Step(world, new Wait(AgentId.Player), 60);

        Assert.Equal(60, world.Minute);
        Assert.Equal(60, result.MinutesElapsed);
        Assert.Null(result.InterruptedReason);
        Assert.True(world.Agent(AgentId.Player).EnergyPct < 1);
        Assert.True(world.Agent(AgentId.Player).CaloriesKcal < 1500);
    }

    [Fact]
    public void WaitingTooLong_EmitsTiredThenInterruptsOnExhaustion()
    {
        var world = World.New(1);
        var result = Sim.Step(world, new Wait(AgentId.Player), 24 * 60);

        Assert.Equal("exhausted", result.InterruptedReason);
        Assert.Equal(16 * 60, result.MinutesElapsed);
        Assert.Collection(result.Events,
            e => Assert.IsType<BecameTired>(e),
            e => Assert.IsType<Exhausted>(e));
    }

    [Fact]
    public void Rest_StopsWhenFullyRested()
    {
        var world = World.New(1);
        Sim.Step(world, new Wait(AgentId.Player), 120);
        var result = Sim.Step(world, new Rest(AgentId.Player), 600);

        Assert.Equal("fully rested", result.InterruptedReason);
        Assert.IsType<FullyRested>(Assert.Single(result.Events));
        Assert.Equal(1.0, world.Agent(AgentId.Player).EnergyPct);
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        var world = World.New(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Sim.Step(world, new Wait(AgentId.Player), 0));
        Assert.Throws<KeyNotFoundException>(() => Sim.Step(world, new Wait(new AgentId(99)), 1));
    }
}
