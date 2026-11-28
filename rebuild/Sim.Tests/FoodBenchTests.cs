using Survival;
using Xunit;

namespace Survival.Tests;

public class FoodBenchTests
{
    static World WolvesAndCaribou()
    {
        var w = PlannerTests.Strip(rows: 5);
        w.PoolCache = null;
        for (int i = 0; i < 6; i++) w.Agent(Agents.Spawn(w, Species.Wolf, new TileId(20, 2))).Body.CaloriesKcal = 0;
        for (int i = 0; i < 4; i++) Agents.Spawn(w, Species.Caribou, new TileId(24, 2));
        return w;
    }

    [Fact]
    public void EveryKilledEvent_LandsInOneCellOfTheKillMatrix()
    {
        var w = WolvesAndCaribou();
        int killed = 0;
        var seed = FoodBench.Run(w, seed: 0, days: 3, observe: e => { if (e is Killed) killed++; });
        Assert.True(killed > 0, "nothing was killed in three days");
        Assert.Equal(killed, seed.Kills.Values.Sum());
        Assert.All(seed.Kills.Keys, k => Assert.InRange(k.Day, 0, 2));
    }

    [Fact]
    public void WhoKilledWhom_IsReadFromTheKillersSpecies()
    {
        var w = WolvesAndCaribou();
        var seed = FoodBench.Run(w, seed: 0, days: 3);
        Assert.Contains(seed.Kills.Keys, k => k.Killer == "Wolf" && k.Victim == Species.Caribou);
    }

    [Fact]
    public void FeedingAtAKill_IsHunted_AndAtAnotherKinds_IsScavenged()
    {
        var w = PlannerTests.Strip();
        var tile = new TileId(20, 1);
        var hunter = w.Agent(Agents.Spawn(w, Species.Wolf, new TileId(30, 1)));
        Combat.LeaveCarcass(w, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 110, 0.08, 0.47), new Cause(hunter.Id, "a wolf"));
        var wolf = w.Agent(Agents.Spawn(w, Species.Wolf, tile));
        wolf.Body.CaloriesKcal = 0;
        var seed = FoodBench.Run(w, seed: 0, days: 1);
        Assert.True(seed.KcalIn.GetValueOrDefault((Species.Wolf, FoodSource.Hunted, 0)) > 100);
        Assert.Equal(0, seed.KcalIn.GetValueOrDefault((Species.Wolf, FoodSource.Scavenged, 0)));

        var w2 = PlannerTests.Strip();
        Combat.LeaveCarcass(w2, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 110, 0.08, 0.47), new Cause(null, "the cold"));
        w2.Agent(Agents.Spawn(w2, Species.Wolf, tile)).Body.CaloriesKcal = 0;
        var seed2 = FoodBench.Run(w2, seed: 0, days: 1);
        Assert.True(seed2.KcalIn.GetValueOrDefault((Species.Wolf, FoodSource.Scavenged, 0)) > 100);
    }

    [Fact]
    public void ARestingCaribou_BurnsAboutItsMetabolismADay()
    {
        var w = PlannerTests.Strip();
        w.PoolCache = null;
        var caribou = w.Agent(Agents.Spawn(w, Species.Caribou, new TileId(20, 1)));
        double perDay = 1440 * Fauna.BurnKcalPerMinute(caribou.Body);
        var seed = FoodBench.Run(w, seed: 0, days: 1);
        Assert.InRange(seed.NetBurn[(Species.Caribou, 0)], 0.9 * perDay, 1.3 * perDay);
    }

    [Fact]
    public void TheReport_ShowsMeanAndSpreadOverSeeds_PerDayAndPerWeek()
    {
        static FoodBenchSeed Seed(int wolfKillsDayZero, int days)
        {
            var s = new FoodBenchSeed(1, days);
            s.Kills[("Wolf", Species.Caribou, 0)] = wolfKillsDayZero;
            s.Encounters[(Species.Wolf, 0)] = 5;
            s.AnimalDays[(Species.Wolf, 0)] = 6;
            s.AnimalDays[(Species.Hyena, 0)] = 4;
            s.AnimalDays[(Species.Caribou, 0)] = 20;
            s.Encounters[(Species.Hyena, 0)] = 2;
            s.KcalIn[(Species.Wolf, FoodSource.Hunted, 0)] = 9000;
            s.NetBurn[(Species.Wolf, 0)] = 8000;
            return s;
        }
        string text = FoodBenchText.Format([Seed(2, 14), Seed(4, 14)], days: 14);
        Assert.Contains("Wolf -> Caribou", text);
        Assert.Contains("0.21 (0.14-0.29)", text); // 3 kills on day 0 over 14 days: the mean of 2/14 and 4/14, min and max
        Assert.Contains("Wolf", text.Split("== Energy", 2)[1]);
        Assert.Matches(@"Hyena -> Caribou +0\.00 \(0\.00-0\.00\)", text); // a predator that never killed it still has its cell
        Assert.Contains("Hyena encounters", text);
        Assert.Contains("week 1", text);
        Assert.Contains("week 2", text);
    }
}
