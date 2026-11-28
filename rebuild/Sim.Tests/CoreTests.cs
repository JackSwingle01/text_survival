namespace Survival.Tests;

public class CoreTests
{
    static World Play(int seed)
    {
        var world = TestWorlds.New(seed);
        // Real weather can kill an unsheltered agent with no fire; determinism holds either way.
        bool Alive() => Body.IsAlive(world.Agent(AgentId.Player).Body);
        Sim.Step(world, new Wait(AgentId.Player), 600);
        if (Alive()) Sim.Step(world, new Rest(AgentId.Player), 120);
        world.Rng("test").NextUlong(); // stand-in for a system drawing from its stream
        if (Alive()) Sim.Step(world, new Wait(AgentId.Player), 900);
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
        Assert.NotEqual(TestWorlds.New(1).Rng("fire").NextUlong(), TestWorlds.New(2).Rng("fire").NextUlong());
        Assert.NotEqual(TestWorlds.New(1).Rng("fire").NextUlong(), TestWorlds.New(1).Rng("body").NextUlong());
    }

    [Fact]
    public void SaveRoundTrips_AndResumesRngExactly()
    {
        var world = Play(42);
        string json = Save.ToJson(world);
        var loaded = TestWorlds.Load(json);

        Assert.Equal(json, Save.ToJson(loaded));
        Assert.True(json.Length < 1_000_000, $"Save is {json.Length} bytes");
        Assert.Equal(world.Rng("test").NextUlong(), loaded.Rng("test").NextUlong());
        Assert.Equal(world.Agent(AgentId.Player).Body.CoreTempF, loaded.Agent(AgentId.Player).Body.CoreTempF);
    }

    [Fact]
    public void Wait_DrainsAndAdvancesClock()
    {
        var world = TestWorlds.New(1);
        var result = Sim.Step(world, new Wait(AgentId.Player), 60);

        Assert.Equal(60, world.Minute);
        Assert.Equal(60, result.MinutesElapsed);
        Assert.Null(result.InterruptedReason);
        var body = world.Agent(AgentId.Player).Body;
        Assert.Equal(120, body.SleepDebtMinutes, 9);
        Assert.True(body.CaloriesKcal < 1500);
        Assert.True(body.HydrationMl < 3000);
    }

    [Fact]
    public void WaitingTooLong_EmitsTiredAtTheOnset_ThenInterruptsOnExhaustionAtTheFullHour()
    {
        var world = TestWorlds.New(1);
        world.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North); // mild: this is about sleep, not cold
        var body = world.Agent(AgentId.Player).Body;
        body.SleepDebtMinutes = 17 * 60;
        var onset = Sim.Step(world, new Wait(AgentId.Player), 120);
        var tired = Assert.Single(onset.Events.OfType<DeprivationStarted>(), e => e.Kind == Deprivation.Tired);
        Assert.Equal(61, tired.Minute); // the minute the debt passes 18 hours

        body.SleepDebtMinutes = 48 * 60 - 30; // the collapse odds would likely stop a wait from 18 hours first
        var result = Sim.Step(world, new Wait(AgentId.Player), 24 * 60);
        Assert.Equal("exhausted", result.InterruptedReason);
        Assert.Equal(30, result.MinutesElapsed);
        Assert.IsType<Exhausted>(result.Events[^1]);
    }

    [Fact]
    public void Sleep_StopsWhenFullyRested()
    {
        var world = TestWorlds.New(1);
        Sim.Step(world, new Wait(AgentId.Player), 120); // 180 minutes owed; a shallow debt repays 2/min
        var result = Sim.Step(world, new Sleep(AgentId.Player), 600);

        Assert.Equal("fully rested", result.InterruptedReason);
        Assert.Equal(90, result.MinutesElapsed);
        Assert.IsType<FullyRested>(result.Events[^1]);
        Assert.Equal(0, world.Agent(AgentId.Player).Body.SleepDebtMinutes);
    }

    /// Past the collapse onset, the player drops asleep where they are. The step stops there;
    /// until the sleep is out they can wait, rest or sleep, which their body lives as sleep, and nothing
    /// else; then they are awake and free again.
    [Fact]
    public void AtTheFullHour_ThePlayerDropsAsleep_AndCanOnlySleepItOut()
    {
        var world = TestWorlds.New(1);
        world.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
        var body = world.Agent(AgentId.Player).Body;
        body.SleepDebtMinutes = 48 * 60;
        var walk = new Travel(AgentId.Player, Direction.North);

        var dropped = Sim.Step(world, new Wait(AgentId.Player), 2000);
        Assert.Equal("fell asleep", dropped.InterruptedReason);
        Assert.Contains(dropped.Events, e => e is EffectStarted { Kind: EffectKind.Asleep });
        int left = Body.AsleepMinutesLeft(body);
        Assert.InRange(left, 15, 90);
        Assert.Equal(Sim.Asleep, Sim.Refusal(world, walk)?.Reason);
        Assert.Equal(Sim.Asleep, Sim.Step(world, walk, 10).InterruptedReason);

        double owed = body.SleepDebtMinutes;
        var slept = Sim.Step(world, new Wait(AgentId.Player), left);
        Assert.Null(slept.InterruptedReason);
        Assert.True(owed - body.SleepDebtMinutes > 10 * left, $"repaid {owed - body.SleepDebtMinutes} in {left}"); // a deep debt repays fast
        Assert.False(Body.IsAsleep(body));
        Assert.Contains(slept.Events, e => e is EffectEnded { Kind: EffectKind.Asleep });
        Assert.NotEqual(Sim.Asleep, Sim.Refusal(world, walk)?.Reason);
    }

    /// The roll to drop asleep is one a minute lived, on the body's own stream, so ten
    /// hours far past the full hour (each drop repays hours) come out the same minute by minute, in one go, in chunks with a save and load
    /// between them, or as the player's own waiting steps (which stop each time they drop).
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void FallingAsleep_IsTheSameHoweverTheMinutesAreStepped()
    {
        const int minutes = 600;
        World Make()
        {
            var w = TestWorlds.New(3);
            w.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
            w.Agent(AgentId.Player).Body.SleepDebtMinutes = 100 * 60;
            return w;
        }
        int[] chunks = [7, 60, 1, 133];
        var byMinute = Make();
        var drops = new List<WorldEvent>();
        for (int i = 0; i < minutes; i++)
            drops.AddRange(Sim.Advance(byMinute, 1).Where(e => e is EffectStarted { Kind: EffectKind.Asleep, Who: var who } && who == AgentId.Player));
        Assert.True(drops.Count >= 2, $"{drops.Count} drops in ten hours past the full hour");

        var once = Make();
        Sim.Advance(once, minutes);
        var saved = Make();
        for (int k = 0; saved.Minute < minutes; k++)
        {
            Sim.Advance(saved, (int)Math.Min(chunks[k % chunks.Length], minutes - saved.Minute));
            saved = Save.FromJson(Save.ToJson(saved));
        }
        var stepped = Make();
        for (int k = 0; stepped.Minute < minutes; k++)
            Sim.Step(stepped, new Wait(AgentId.Player), (int)Math.Min(chunks[k % chunks.Length], minutes - stepped.Minute));

        string expected = Save.ToJson(byMinute);
        Assert.Equal(expected, Save.ToJson(once));
        Assert.Equal(expected, Save.ToJson(saved));
        Assert.Equal(expected, Save.ToJson(stepped));
    }

    [Fact]
    public void Rest_DoesNotRepaySleepDebt_OnlySleepDoes()
    {
        var world = TestWorlds.New(1);
        Sim.Step(world, new Rest(AgentId.Player), 60);
        Assert.Equal(120, world.Agent(AgentId.Player).Body.SleepDebtMinutes);
    }

    [Fact]
    public void BleedingOut_EmitsDiedAndInterrupts_ThenTheDeadCannotAct()
    {
        var world = TestWorlds.New(1);
        Effects.Add(world.Agent(AgentId.Player).Body, EffectKind.Bleeding, 1.0);
        var result = Sim.Step(world, new Wait(AgentId.Player), 600);

        Assert.Equal("died", result.InterruptedReason);
        Assert.IsType<Died>(result.Events[^1]);
        Assert.Throws<InvalidOperationException>(() => Sim.Step(world, new Wait(AgentId.Player), 1));
    }

    [Fact]
    public void Cold_ReachesTheBodyThroughWeather()
    {
        var warm = TestWorlds.New(1);
        warm.FixedWeather = new WeatherSample(0, WeatherCondition.Clear, 20, 0, 0, 0, WindDirection.North);
        var cold = TestWorlds.New(1);
        cold.FixedWeather = new WeatherSample(0, WeatherCondition.Clear, -30, 0.5, 0, 0, WindDirection.North);
        Sim.Step(warm, new Wait(AgentId.Player), 120);
        Sim.Step(cold, new Wait(AgentId.Player), 120);
        Assert.True(cold.Agent(AgentId.Player).Body.CoreTempF < warm.Agent(AgentId.Player).Body.CoreTempF);
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        var world = TestWorlds.New(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Sim.Step(world, new Wait(AgentId.Player), 0));
        Assert.Throws<KeyNotFoundException>(() => Sim.Step(world, new Wait(new AgentId(world.NextAgentId)), 1)); // never made
    }
}
