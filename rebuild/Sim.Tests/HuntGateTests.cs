using CsCheck;
using Survival;
using Xunit;

/// The gate on a hunt (Fauna.HuntPays): a kill chance of at least 0.10 at any hunger, and an injury chance no more than
/// the animal will risk, 0.15 fed to 0.75 desperate (Fauna.Desperation, from its hunger and its species' temperament).
/// The rule is checked with chances given to it; the measured tables, through a hunt's forecast (Fauna.HuntSource).
public class HuntGateTests
{
    static TileId T(int x, int y = 2) => new(x, y);

    /// An animal of the species on the planner's strip, its hunger set so its desperation is this.
    static Agent At(Species species, double desperation)
    {
        var w = PlannerTests.Strip();
        var animal = w.Agent(Agents.Spawn(w, species, T(20)));
        TestKit.SetHunger(animal, HungerFor(species, desperation));
        return animal;
    }

    static double HungerFor(Species species, double desperation)
    {
        var t = SpeciesTable.Of(species).Temperament;
        return t.HungryAt + desperation * (t.StarvingAt - t.HungryAt);
    }

    [Theory]
    [InlineData(0.20, 0.00, 0.0, true)]
    [InlineData(0.20, 0.00, 1.0, true)]
    [InlineData(0.09, 0.00, 0.0, false)]
    [InlineData(0.09, 0.00, 1.0, false)]
    [InlineData(0.90, 0.30, 0.0, false)]
    [InlineData(0.90, 0.30, 1.0, true)]
    [InlineData(0.90, 0.45, 0.4, false)] // tolerance 0.39
    [InlineData(0.90, 0.45, 0.6, true)] // tolerance 0.51
    [InlineData(0.90, 0.80, 0.0, false)]
    [InlineData(0.90, 0.80, 0.5, false)]
    [InlineData(0.90, 0.80, 1.0, false)]
    public void AHunt_IsASource_ByItsKillChance_AndTheInjuryItsDesperationTolerates(double kill, double injury, double desperation, bool source) =>
        Assert.Equal(source, Fauna.HuntPays(At(Species.Wolf, desperation), kill, injury));

    [Theory]
    [InlineData(Species.Wolf)]
    [InlineData(Species.SaberTooth)]
    public void Desperation_IsNoneAtHungryAt_FullAtStarvingAt_AndHalfHalfway(Species species)
    {
        Assert.Equal(0, Fauna.Desperation(At(species, 0)), 6);
        Assert.Equal(0.5, Fauna.Desperation(At(species, 0.5)), 6);
        Assert.Equal(1, Fauna.Desperation(At(species, 1)), 6);
        var fed = At(species, 0);
        TestKit.SetHunger(fed, 0);
        Assert.Equal(0, Fauna.Desperation(fed));
    }

    [Fact]
    public void RaisingHunger_NeverTurnsASourceIntoNone()
    {
        var w = PlannerTests.Strip();
        var animals = Enum.GetValues<Species>().Select(s => w.Agent(Agents.Spawn(w, s, T(20)))).ToArray();
        Gen.Select(Gen.Int[0, animals.Length - 1], Gen.Double[0, 1], Gen.Double[0, 1], Gen.Double[0, 1], Gen.Double[0, 1]).Sample((i, kill, injury, a, b) =>
        {
            var animal = animals[i];
            TestKit.SetHunger(animal, Math.Min(a, b));
            bool before = Fauna.HuntPays(animal, kill, injury);
            TestKit.SetHunger(animal, Math.Max(a, b));
            return !before || Fauna.HuntPays(animal, kill, injury);
        });
    }

    /// Sensed as given: a herd of the quarry on the tile, as Sense records one.
    static void Senses(Agent animal, Species quarry, int count) =>
        (Fauna.MindOf(animal).Sightings ??= []).Add(new Sensed(null, animal.Tile, 0, Quarry: quarry, Count: count));

    /// A pack of this many on the strip, each at this hunger, sensing a herd of the quarry on its tile: whether the first lists
    /// the hunt as a food source, and how many would hunt with it (Fauna.Hunters).
    static (bool Listed, int Hunters) ListsTheHunt(Species predator, int pack, double hunger, Species quarry, int herd)
    {
        var w = PlannerTests.Strip();
        var hunters = Agents.SpawnGroup(w, predator, T(20), pack).Select(w.Agent).ToList();
        foreach (var a in hunters) TestKit.SetHunger(a, hunger);
        Senses(hunters[0], quarry, herd);
        return (Fauna.HuntSource(w, hunters[0], hunters[0].Tile) > 0, Fauna.Hunters(w, hunters[0]).Count);
    }

    [Fact]
    public void AFedLoneSabreTooth_ListsACaribouHerdAsAFoodSource()
    {
        Assert.True(ListsTheHunt(Species.SaberTooth, 1, 0, Species.Caribou, 3).Listed);
        Assert.True(ListsTheHunt(Species.SaberTooth, 1, 0, Species.Caribou, 1).Listed);
    }

    [Fact]
    public void ThreeWolves_LeaveFourBisonAlone_UntilEveryOneIsStarving()
    {
        // Fed, only the one that starts it would hunt (Fauna.Hunters); were all three to, the chances are still more than it risks.
        Assert.False(ListsTheHunt(Species.Wolf, 3, 0, Species.Bison, 4).Listed);
        Assert.False(Fauna.HuntPays(At(Species.Wolf, 0), Combat.KillChance(Species.Wolf, 3, Species.Bison, 4), Combat.InjuryChance(Species.Wolf, 3, Species.Bison, 4)));
        Assert.Equal((false, 3), ListsTheHunt(Species.Wolf, 3, HungerFor(Species.Wolf, 0.5), Species.Bison, 4));
        Assert.Equal((true, 3), ListsTheHunt(Species.Wolf, 3, HungerFor(Species.Wolf, 1), Species.Bison, 4));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ALoneHyena_NeverListsMegaloceros(double hunger)
    {
        for (int herd = 1; herd <= Combat.KillChanceHerds; herd++)
            Assert.False(ListsTheHunt(Species.Hyena, 1, hunger, Species.Megaloceros, herd).Listed);
    }

    /// Two hunts that both pass the gate, a tile either side of a lone wolf: it sets out for the one worth more by kill chance x
    /// feeding rate x its share (three megaloceros, 0.78 and an injury chance of 0.12), not the one that risks less (a lone bison,
    /// 0.18 and 0.02).
    [Fact]
    public void OfTwoHuntsThatPass_ALoneWolfTakesTheOneWorthMore()
    {
        var w = PlannerTests.Strip();
        var wolf = w.Agent(Agents.Spawn(w, Species.Wolf, T(20)));
        TestKit.SetHunger(wolf, 0.8);
        wolf.Body.EnergyMinutes = Body.MaxEnergyMinutes;
        Agents.SpawnGroup(w, Species.Bison, T(17), 1);
        Agents.SpawnGroup(w, Species.Megaloceros, T(23), 3);
        Sim.Advance(w, 1);
        Assert.True(Fauna.HuntSource(w, wolf, T(17)) > 0);
        Assert.True(Fauna.HuntSource(w, wolf, T(23)) > Fauna.HuntSource(w, wolf, T(17)));
        Assert.Equal(T(23), Fauna.MindOf(wolf).Plan?.At);
    }
}
