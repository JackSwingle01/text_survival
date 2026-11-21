using Survival;
using Xunit;

/// Whole-valley settles, years long: their own class, so they run beside the rest of the suite.
public class SettleTests
{
    /// The valley over two years on the seed whose mammoths the packs ate out: they live.
    [Fact]
    public void Seed123_Mammoths_OutliveTwoYearsOfPacks()
    {
        var w = World.New(123);
        int start = w.Agents.Values.Where(a => a.Species == Species.Mammoth).Sum(a => a.Members.Count);
        var events = Sim.Advance(w, 730 * 1440);
        Assert.DoesNotContain(events.OfType<Killed>(), k => k.Species == Species.Mammoth); // at 9cbe8cc: 13, all to wolves
        Assert.True(w.Agents.Values.Where(a => a.Species == Species.Mammoth).Sum(a => a.Members.Count(Body.IsAlive)) >= start);
    }
}
