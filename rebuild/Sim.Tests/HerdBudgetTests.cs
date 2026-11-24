using Survival;
using Xunit;

/// The landscape of fear (species round, decided 2026-11-21): a herd's hunger is its members' calorie
/// stores. Grazing earns its species' intake; fleeing and fighting earn nothing; every minute lived
/// burns its field metabolism.
public class HerdBudgetTests
{
    static TileId Open(World w)
    {
        var m = Map.Of(w);
        var claimed = w.Agents.Values.Where(a => a.Mind is HerdMind).SelectMany(h => ((HerdMind)h.Mind).Territory).ToHashSet();
        return Enumerable.Range(3, m.Width - 6).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && !claimed.Contains(t) && Pools.At(w, t) is [ForagePool, ..]
                && MapGen.Manhattan(t, m.Camp) > 30 && MapGen.Neighbors(t).All(n => Map.Blocked(m, t, n) == null && !claimed.Contains(n)));
    }

    static (World, Agent) Caribou(double hunger)
    {
        var w = TestWorlds.New(3);
        var herd = w.Agent(Agents.Spawn(w, Species.Caribou, Open(w)));
        TestKit.SetHunger(herd, hunger);
        return (w, herd);
    }

    static void Minutes(World w, Agent herd, Activity a, int n)
    {
        for (int i = 0; i < n; i++)
        {
            w.Minute++;
            Fauna.Tick(w, herd, []);
            Assert.True(Sim.Start(w, a, 1, [], out var doing) == null);
            Sim.Live(w, herd, doing, []);
        }
    }

    [Fact]
    public void Grazing_FillsTheStores()
    {
        var (w, herd) = Caribou(0.6);
        Minutes(w, herd, new Graze(herd.Id), 30);
        Assert.True(Fauna.Hunger(herd) < 0.6, $"thirty minutes of grazing left it at {Fauna.Hunger(herd):0.000}");
    }

    [Fact]
    public void Standing_ForgoesTheIntake_AndBurns()
    {
        var (w, herd) = Caribou(0.6);
        double held = herd.Members.Sum(b => b.CaloriesKcal), burn = herd.Members.Sum(Fauna.BurnKcalPerMinute);
        Minutes(w, herd, new Wait(herd.Id), 30);
        Assert.Equal(held - 30 * burn, herd.Members.Sum(b => b.CaloriesKcal), 6);
        Assert.True(Fauna.Hunger(herd) > 0.6);
    }

    /// Field metabolism, 1.6 x Kleiber's 70 M^0.75 a day: a 110 kg caribou burns about 2.64 kcal a minute,
    /// a 45 kg wolf about 1.35 (research §1).
    [Fact]
    public void AnAnimal_BurnsItsFieldMetabolism()
    {
        Assert.InRange(Fauna.BurnKcalPerMinute(SpeciesTable.Average(Species.Caribou)), 2.60, 2.68);
        Assert.InRange(Fauna.BurnKcalPerMinute(SpeciesTable.Average(Species.Wolf)), 1.33, 1.37);
    }

    /// A starved wolf gorges on a kill and puts its lost fat back on; old (before review) feeding stopped at
    /// a full store, so fat burned in a lean spell was gone for good.
    [Fact]
    public void AStarvedWolf_PutsItsFatBackOn_AtAKill()
    {
        var w = TestWorlds.New(3);
        var tile = Open(w);
        var pack = w.Agent(Agents.Spawn(w, Species.Wolf, tile));
        foreach (var b in pack.Members) { b.CaloriesKcal = 0; b.FatKg *= 0.6; }
        double lean = pack.Members.Sum(b => b.FatKg);
        for (int i = 0; i < 3; i++) Combat.LeaveCarcass(w, tile, Species.Caribou, SpeciesTable.Average(Species.Caribou), new Cause(null, "a test"));
        Minutes(w, pack, new Feed(pack.Id), 120);
        Assert.True(pack.Members.Sum(b => b.FatKg) > lean + 0.5, $"fat {lean:0.00} → {pack.Members.Sum(b => b.FatKg):0.00} kg");
        Assert.True(pack.Members.All(b => b.FatPct <= SpeciesTable.Of(Species.Wolf).FatPct + 1e-9), "gorged past its build");
    }

    /// The lone cat lives on small game between kills, as the old solitary forage let it.
    [Fact]
    public void SaberTooth_DoesNotStarveWhileItForages()
    {
        var w = TestWorlds.New(3);
        var cat = w.Agent(Agents.Spawn(w, Species.SaberTooth, Open(w)));
        TestKit.SetHunger(cat, 0.8);
        Minutes(w, cat, new Graze(cat.Id), 60);
        Assert.True(Fauna.Hunger(cat) < 0.8);
    }

    /// Winter lichen at about 13 kcal a minute (research §4.1) against a burn of under 3: a caribou eats
    /// several times what it burns while it grazes, so most of its day is free for other things.
    [Fact]
    public void ACaribou_EatsSeveralTimesItsBurn_OnFreshGround()
    {
        var body = SpeciesTable.Average(Species.Caribou);
        double ratio = SpeciesTable.Of(Species.Caribou).GrazeKcalPerMinute / Fauna.BurnKcalPerMinute(body);
        Assert.InRange(ratio, 3, 8);
    }

    [Fact]
    public void AWolf_FeedingOnACarcass_FillsItsStoresFromTheMeat()
    {
        var w = TestWorlds.New(3);
        var tile = Open(w);
        var pack = w.Agent(Agents.Spawn(w, Species.Wolf, tile));
        TestKit.SetHunger(pack, 0.9);
        Combat.LeaveCarcass(w, tile, Species.Caribou, SpeciesTable.Average(Species.Caribou), new Cause(null, "a test"));
        Minutes(w, pack, new Feed(pack.Id), 30);
        Assert.True(Fauna.Hunger(pack) < 0.1, $"half an hour on a carcass left the wolf at {Fauna.Hunger(pack):0.000}");
    }

    /// The cave bear is a den defender (species round): nothing makes it come at a person but its den.
    [Fact]
    public void TheCaveBear_IsBoldOnlyDefending()
    {
        var w = TestWorlds.New(3);
        w.Minute = 15 * 60; // midnight: a cave bear's active hours would add their own boldness
        var bear = Agents.Spawn(w, Species.CaveBear, Open(w));
        var person = BodyState.BaselineHuman();
        Assert.Equal(0, Combat.Boldness(w, bear, person, hunger: 1.0), 9);
        Assert.True(Combat.Boldness(w, bear, person, defending: true) >= 0.5);
    }
}
