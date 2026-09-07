using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Environments.Grid;
using Xunit;

namespace text_survival.Tests.Environments.Grid;

/// <summary>
/// The settling pass runs a month of world before the player's first turn. It exists to
/// leave worn trails and herds that have already lived somewhere; everything else it
/// touches decays or regrows before day 0.
/// </summary>
public class SettlingPassTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(7)]
    public void HerdTrafficWearsTrailsIn(int seed)
    {
        var ctx = GameContext.CreateNewGame(seed);

        // Wear thresholds are 5/14/35 and linear in head count, so a month of herd traffic
        // should leave a network, not a handful of edges. If this drops toward zero the tick
        // has been coarsened past the point where TryPatrolTerritory still completes moves.
        int walkable = ctx.Map!.Trails.Wear.Count(w => TrailWear.TierFor(w.Wear) >= TrailTier.Path);
        Assert.True(walkable >= 50, $"only {walkable} edges reached Path tier");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(7)]
    public void PredationDoesNotEmptyTheWorld(int seed)
    {
        var ctx = GameContext.CreateNewGame(seed);

        // Nothing in Actors/Animals breeds, so a month of predation is open-loop: prey can
        // only ever go down. If the pass is ever lengthened, this is what breaks first.
        Assert.Contains(ctx.Herds, h => h.AnimalType == AnimalType.Caribou);
        Assert.Contains(ctx.Herds, h => h.AnimalType == AnimalType.Wolf);
        Assert.All(ctx.Herds, h => Assert.False(h.IsEmpty));
    }

    [Fact]
    public void SettlingLandsExactlyOnStartTime()
    {
        var ctx = GameContext.CreateNewGame(42);

        // Weather is constructed SettleDays in the past and ticked forward, so the player's
        // first turn opens on the intended date with a month of real fronts behind it.
        Assert.Equal(GameContext.StartTime, ctx.GameTime);
        Assert.Equal(GameContext.StartTime, ctx.Weather.Time);
        Assert.Equal(0, ctx.TotalMinutesElapsed);
    }
}
