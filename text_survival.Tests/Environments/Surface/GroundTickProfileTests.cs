using System.Diagnostics;
using text_survival.Actions;

namespace text_survival.Tests.Environments.Surface;

/// <summary>
/// Every tile on the map ticks, so the cost of a tick is a design constraint. A guard rail,
/// not a benchmark.
/// </summary>
public class GroundTickProfileTests
{
    [Fact]
    public void AFullDayOfWorldTimeStaysAffordable()
    {
        var ctx = GameContext.CreateNewGame(seed: 4242);
        var map = ctx.Map!;

        int tiles = map.AllLocations.Count();
        var weather = ctx.Weather;

        // Warm up JIT and any lazily built surfaces.
        map.AdvanceWorld(1, weather, map.CurrentLocation);

        var watch = Stopwatch.StartNew();
        for (int minute = 0; minute < 60 * 24; minute++)
            map.AdvanceWorld(1, weather, map.CurrentLocation);
        watch.Stop();

        int layers = map.AllLocations.Sum(l => l.Surface.Layers.Count);

        Assert.True(watch.ElapsedMilliseconds < 4_000,
            $"{tiles} tiles x 1440 minutes took {watch.ElapsedMilliseconds} ms ({layers} layers total)");

        // For the record: 9216 tiles, ~0.8 s per simulated day, one layer per bare tile.
        // Per-minute updates of every tile cost sixteen times that.
    }
}
