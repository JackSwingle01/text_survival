using Survival;
using Xunit;

/// Animals find food through the shared planner and starve from their bodies (spec 2026-11-22-animal-life,
/// "Grazing", "Cohesion and crowding", "Bodies"): driven through the world on PlannerTests' strip, read from
/// where animals went, what their bodies did and who died of what.
public class FoodTests
{
    static TileId T(int x, int y = 1) => new(x, y);

    static double KcalHeld(BodyState b) => b.CaloriesKcal + b.FatKg * Body.KcalPerKgFat + b.MuscleKg * 1320;

    /// Lichen, grass and berries on every tile of the strip: what browsers, grazers and bears eat.
    internal static World Pasture(params TileId[] water)
    {
        var w = PlannerTests.Strip(water: water);
        var pool = new ForagePool("pasture", 1.0, [new(Resource.Usnea, 1, 0.1, 0.2), new(Resource.Tinder, 1, 0.1, 0.2), new(Resource.Berries, 1, 0.1, 0.2)]);
        foreach (var t in w.PoolCache!.Keys.ToList()) w.PoolCache[t] = [pool];
        return w;
    }

    static Agent Hungry(World w, Species species, TileId at)
    {
        var a = PlannerTests.Animal(w, species, at);
        a.Body.CaloriesKcal = 0;
        return a;
    }

    [Fact]
    public void AnAnimal_OnATileWithNothingItsDietEats_GetsNothingThere()
    {
        var w = PlannerTests.Strip();
        var caribou = Hungry(w, Species.Caribou, T(20));
        double held = KcalHeld(caribou.Body);
        Sim.Advance(w, 120);
        Assert.True(KcalHeld(caribou.Body) < held, $"kcal held {held:F0} -> {KcalHeld(caribou.Body):F0} on bare ground");
        Assert.Equal(0, caribou.Body.CaloriesKcal);
    }

    /// A pair of hungry caribou on a lichen tile: grazed down, going on beats staying; fresh, staying does.
    [Theory]
    [InlineData(0.9, true)]
    [InlineData(0.0, false)]
    public void AHerd_LeavesATile_OnceItIsGrazedDown(double grazed, bool leaves)
    {
        var w = Pasture();
        Pools.SetGrazed(w, new PoolId(T(20), 0), Resource.Usnea, grazed);
        var herd = new[] { Hungry(w, Species.Caribou, T(20)), Hungry(w, Species.Caribou, T(20)) };
        bool left = false;
        for (int i = 0; i < 120; i++)
        {
            Sim.Advance(w, 1);
            left |= herd.All(a => a.Tile != T(20));
        }
        Assert.Equal(leaves, left);
        Assert.All(herd, a => Assert.True(a.Body.CaloriesKcal > 0, "it never ate"));
    }

    /// Two caribou beside a lake on bare ground, one leaner: nothing to eat, so both starve from their bodies, the leaner first.
    [Fact]
    public void StarvingAnimals_DieFromTheirBodies_TheLeanestFirst()
    {
        var w = PlannerTests.Strip(water: [T(20, 2)]);
        Agent Lean(double muscleKg)
        {
            var a = Hungry(w, Species.Caribou, T(20));
            a.Body.MuscleKg = 0.15 * a.Body.WeightKg + muscleKg;
            a.Body.FatKg = 0.03 * a.Body.WeightKg;
            return a;
        }
        var leaner = Lean(0.5);
        var other = Lean(1.5);
        var deaths = new List<AgentId>();
        for (int hour = 0; hour < 20 * 24 && deaths.Count < 2; hour++)
            deaths.AddRange(Sim.Advance(w, 60).OfType<Killed>().Select(k => k.Agent));
        Assert.Equal([leaner.Id, other.Id], deaths);
        var causes = w.Tiles.Values.SelectMany(t => t.Carcasses ?? []).Select(c => c.Cause?.What).ToList();
        Assert.Equal(["starvation", "starvation"], causes);
    }

    /// A hungry wolf goes to a carcass it can see; thirsty at it, it goes to drink and comes back to its kill.
    [Fact]
    public void AWolf_GoesToACarcassItSees_AndComesBackToItAfterDrinking()
    {
        var w = PlannerTests.Strip(water: [T(4, 2)]);
        Combat.LeaveCarcass(w, T(12), Species.Caribou, SpeciesTable.Average(Species.Caribou), new Cause(null, "a test"));
        var wolf = Hungry(w, Species.Wolf, T(18));
        wolf.Body.FatKg *= 0.7;
        bool fed = false, drank = false, back = false, thirsty = false;
        for (int i = 0; i < 12 * 60; i++)
        {
            double kcal = wolf.Body.CaloriesKcal;
            Sim.Advance(w, 1);
            if (wolf.Body.CaloriesKcal > kcal && wolf.Tile == T(12)) { fed = true; back |= drank; }
            if (fed && !thirsty) (thirsty, wolf.Body.HydrationMl) = (true, 0.3 * Body.MaxHydrationMlOf(wolf.Body));
            if (fed && wolf.Tile.X <= 5) drank = true;
        }
        Assert.True(fed, "it never fed at the carcass");
        Assert.True(drank, "it never went to drink");
        Assert.True(back, "it never came back to its kill");
    }

    /// What a minute's grazing pays the animal where it stands, read from its body: kcal held after, less before, plus its burn.
    static double AteInAMinute(World w, Agent a)
    {
        double before = KcalHeld(a.Body), burn = Fauna.BurnKcalPerMinute(a.Body);
        Sim.Advance(w, 1);
        return KcalHeld(a.Body) - before + burn;
    }

    /// Five hungry caribou on a fresh tile that holds seven: company below capacity costs nothing, so each eats its full rate.
    [Fact]
    public void FiveCaribou_OnAFreshTileForSeven_AllEatAtTheFullRate()
    {
        var w = Pasture();
        var herd = Enumerable.Range(0, 5).Select(_ => Hungry(w, Species.Caribou, T(20))).ToList();
        double full = SpeciesTable.Of(Species.Caribou).GrazeKcalPerMinute;
        Assert.All(herd, a => Assert.Equal(full, Fauna.GrazeHere(w, a)!.Value.PerMinute, 9));
        double before = herd.Sum(a => KcalHeld(a.Body)), burn = herd.Sum(a => Fauna.BurnKcalPerMinute(a.Body));
        Sim.Advance(w, 1);
        Assert.All(herd, a => Assert.Equal(T(20), a.Tile));
        Assert.Equal(5 * full, herd.Sum(a => KcalHeld(a.Body)) - before + burn, 1);
    }

    /// A mammoth and a caribou on one fresh tile: together they load it past its seven caribou, and each loses the same share
    /// of its own rate, so the mammoth still eats about seven times what the caribou does (by intake, not by head).
    [Fact]
    public void AMammothAndACaribou_ShareATile_ByIntake_NotByHead()
    {
        var w = Pasture();
        var mammoth = Hungry(w, Species.Mammoth, T(20));
        var caribou = Hungry(w, Species.Caribou, T(20));
        double m = Fauna.GrazeHere(w, mammoth)!.Value.PerMinute, c = Fauna.GrazeHere(w, caribou)!.Value.PerMinute;
        double mFull = SpeciesTable.Of(Species.Mammoth).GrazeKcalPerMinute, cFull = SpeciesTable.Of(Species.Caribou).GrazeKcalPerMinute;
        Assert.True(c < cFull, $"the caribou eats {c:F2}, its full rate: the tile is not over capacity");
        Assert.Equal(m / mFull, c / cFull, 9);
        Assert.Equal(m, AteInAMinute(w, mammoth), 2);
    }

    /// A hungry caribou on bare ground between a near pasture with no kin and a farther one with kin on the tile beside it:
    /// kin within one tile spare it the cohesion cost, so it goes to the farther one (decision on #83, 2026-11-24).
    [Fact]
    public void ACaribou_NextToATileWithKin_PaysNoCohesionCostThere()
    {
        var w = PlannerTests.Strip();
        var pool = new ForagePool("pasture", 1.0, [new(Resource.Usnea, 1, 0.1, 0.2)]);
        w.PoolCache![T(13)] = [pool];
        w.PoolCache[T(18)] = [pool];
        foreach (var _ in Enumerable.Range(0, 2)) PlannerTests.Animal(w, Species.Caribou, T(18, 2)); // fed, on bare ground beside it
        var caribou = Hungry(w, Species.Caribou, T(10));
        Sim.Advance(w, 1);
        Assert.Equal(T(18), Fauna.MindOf(caribou).Plan?.At);
    }
}
