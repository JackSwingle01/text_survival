using Survival;
using Xunit;

/// Herd behaviour pinned to the old game's output. The populator draws from System.Random and
/// placed herds on named dens the rebuild does not have yet, so its counts are checked against the
/// old ranges (HerdTests), not pinned; vigilance is a pure formula, so it is pinned here.
public class HerdGoldenTests
{
    // Old HerdVigilance.PreyNoticesPredator over 200,000 trials each (harness: tmp/p3b/golden-herd).
    [Theory]
    [InlineData(Species.Wolf, 5, 0.9, Species.Caribou, 3, HerdState.Grazing, 100, 0.6098)]
    [InlineData(Species.Wolf, 3, 0.5, Species.Caribou, 10, HerdState.Resting, 100, 0.5995)]
    [InlineData(Species.Wolf, 6, 0.95, Species.Bison, 6, HerdState.Resting, 10, 0.5085)]
    [InlineData(Species.Wolf, 4, 0.75, Species.Megaloceros, 1, HerdState.Fleeing, 5, 0.6090)]
    public void PreyVigilance_MatchesOld(Species pred, int packSize, double hunger, Species preySpecies, int herdSize, HerdState state, int stateMinutes, double oldRate)
    {
        var w = new World { Seed = 1 };
        var tile = new TileId(5, 5);
        // The pack and the herd are derived: that many animals of one species on the tile (Combat.Side).
        var wolves = Agents.SpawnGroup(w, pred, tile, packSize).Select(w.Agent).ToList();
        var herd = Agents.SpawnGroup(w, preySpecies, tile, herdSize).Select(w.Agent).ToList();
        foreach (var wolf in wolves) TestKit.SetHunger(wolf, hunger);
        var (pack, prey) = (wolves[0], herd[0]);
        w.Minute = 1000;
        ((HerdMind)prey.Mind).State = state;
        ((HerdMind)prey.Mind).StateSinceMinute = w.Minute - stateMinutes;
        const int N = 200_000;
        int hits = 0;
        for (int i = 0; i < N; i++) if (Fauna.PreyNotices(w, pack, prey)) hits++;
        Assert.Equal(oldRate, (double)hits / N, 0.006);
    }
}
