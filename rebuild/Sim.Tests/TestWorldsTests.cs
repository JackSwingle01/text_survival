using Survival;

namespace Survival.Tests;

/// The shared test-world source (TestWorlds) gives the world World.New would, and builds each seed once.
public class TestWorldsTests
{
    static readonly AgentId P = AgentId.Player;

    /// A few hours of the player's work, then world time with the player idle.
    static IEnumerable<string> Live(World w)
    {
        yield return Save.ToJson(w);
        Sim.Step(w, new Gather(P, 0), 90);
        yield return Save.ToJson(w);
        Sim.Step(w, new Rest(P), 120);
        yield return Save.ToJson(w);
        Sim.Advance(w, 150);
        yield return Save.ToJson(w);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(1)]
    [InlineData(3)]
    public void ASourceWorld_SavesAsWorldNew_BeforeAndAfterHoursOfWork(int seed)
    {
        var fresh = Live(World.New(seed)).ToList();
        var shared = Live(TestWorlds.New(seed)).ToList();
        for (int i = 0; i < fresh.Count; i++)
            Assert.True(fresh[i] == shared[i], $"seed {seed}: the source's world saves differently from World.New at checkpoint {i}");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(1)]
    public void ABareWorld_IsTheSeedsWorld_WithEveryoneButThePlayerRemoved(int seed)
    {
        var emptied = TestWorlds.New(seed);
        foreach (var id in emptied.Agents.Keys.Where(id => id != P).ToList()) Agents.Remove(emptied, id);
        var bare = TestWorlds.Bare(seed);
        Assert.Equal(Save.ToJson(emptied), Save.ToJson(bare));
        Assert.Equal([P], bare.Agents.Keys);
        Assert.Same(TestWorlds.Map(seed), bare.GeneratedMap);
        Assert.NotSame(bare, TestWorlds.Bare(seed));
    }

    [Fact]
    public void SourceWorlds_ShareTheSeedsMap_AndALoadGetsItToo()
    {
        var a = TestWorlds.New(17);
        var b = TestWorlds.Load(Save.ToJson(a));
        Assert.Same(TestWorlds.Map(17), a.GeneratedMap);
        Assert.Same(TestWorlds.Map(17), b.GeneratedMap);
        Assert.NotSame(a, TestWorlds.New(17));
    }

    [Fact]
    public async Task ConcurrentAsks_ForOneSeed_WaitForOneBuild()
    {
        const int seed = 129_129; // no other test asks for it, so this test makes the first ask
        const int askers = 16;
        using var start = new Barrier(askers);
        // Own threads, not the pool's: every asker blocks at the barrier until all are there.
        var worlds = await Task.WhenAll(Enumerable.Range(0, askers).Select(_ => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            return TestWorlds.New(seed);
        }, TaskCreationOptions.LongRunning)));

        Assert.Equal(1, TestWorlds.Builds(seed));
        Assert.All(worlds, w => Assert.Same(TestWorlds.Map(seed), w.GeneratedMap));
    }
}
