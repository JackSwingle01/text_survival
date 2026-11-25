namespace Survival.Tests;

/// Dirty and washing (spec 2026-11-23-predators-and-items, "Dirty and washing"; GLOSSARY "Dirty").
public class DirtyTests
{
    static Rng TestRng() => Rng.ForSystem(1, "test");

    static void Live(BodyState b, SurvivalContext ctx, int minutes)
    {
        var rng = TestRng();
        for (int i = 0; i < minutes; i++) Body.Update(b, ctx, rng);
    }

    static SurvivalContext Resting => new() { AirTempF = 50, ClothingClo = 2.0, ActivityFactor = 1.0 };
    /// Chopping in heavy furs on a mild day: enough to sweat.
    static SurvivalContext HardWork => Resting with { ActivityFactor = 1.8 };

    [Fact]
    public void ADayOfHardWork_LeavesAPersonDirtierThanADayOfRest()
    {
        var rested = BodyState.BaselineHuman();
        var worked = BodyState.BaselineHuman();
        Live(rested, Resting, 12 * 60);
        Live(worked, HardWork, 12 * 60);
        double rest = Effects.Severity(rested, EffectKind.Dirty), work = Effects.Severity(worked, EffectKind.Dirty);
        Assert.InRange(rest, 0.02, 0.1); // a day's living shows, and is far from filthy
        Assert.True(work > rest * 1.5, $"hard work {work}, rest {rest}");
    }

    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World Bare()
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        return w;
    }

    [Fact]
    public void ButcheringACaribou_LeavesAPersonDirtierThanRestingAsLong()
    {
        var butchered = Bare();
        var c = KillChainTests.Caribou(butchered, butchered.Agent(P).Tile);
        Items.Pack(butchered, P).Gear.Add(Items.Make("flint_knife"));
        var cut = Sim.Step(butchered, new Butcher(P, c.Id), 3 * 60);
        var rested = Bare();
        Sim.Step(rested, new Rest(P), cut.MinutesElapsed);
        double butcher = Effects.Severity(butchered.Agent(P).Body, EffectKind.Dirty), rest = Effects.Severity(rested.Agent(P).Body, EffectKind.Dirty);
        Assert.True(butcher > rest + 0.1, $"after {cut.MinutesElapsed} min: butchering {butcher}, rest {rest}");
    }

    [Fact]
    public void Dirty_DoesNotFadeOnItsOwn()
    {
        var b = BodyState.BaselineHuman();
        Effects.Set(b, EffectKind.Dirty, 0.5);
        Live(b, Resting with { FireBonusF = 20 }, 6 * 60);
        Assert.True(Effects.Severity(b, EffectKind.Dirty) >= 0.5);
    }

    [Fact]
    public void Dirty_TakesNothingOffAnyCapacity()
    {
        var b = BodyState.BaselineHuman();
        Effects.Set(b, EffectKind.Dirty, 1);
        Assert.All(Effects.CapacityDeltas(b), d => Assert.Equal(0, d));
    }

    /// A wolf's nose (2.5) three tiles off, in calm air, out of sight: a clean person's base scent reaches
    /// 1.5 tiles, a filthy one's three times as far.
    [Fact]
    public void ADirtyPerson_IsSmelledFromFurtherThanACleanOne()
    {
        var w = Bare();
        var me = w.Agent(P);
        foreach (var r in Items.Pack(w, P).Kg.Keys.ToList()) Items.Take(Items.Pack(w, P), r, double.MaxValue);
        var nose = new Senses(new TileId(me.Tile.X + 3, me.Tile.Y), SightBudgetTiles: 0, SpeciesTable.Of(Species.Wolf).SmellFactor, WindDirection.Calm);
        Effects.Set(me.Body, EffectKind.Dirty, 0);
        Assert.False(nose.Finds(w, me.Tile, Scent.Of(w, me)), "a clean person smelled three tiles off");
        Effects.Set(me.Body, EffectKind.Dirty, 1);
        Assert.True(nose.Finds(w, me.Tile, Scent.Of(w, me)), "a filthy person not smelled three tiles off");
    }

    // ---- washing, through Verbs and Sim.Step ----

    static Verb WashVerb(World w) => Verbs.At(w, P).Tile.Single(v => v.Activity is Wash);

    static TileId ARiver(World w)
    {
        var m = Map.Of(w);
        return Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y))).First(m.BesideRiver);
    }

    static void Soil(World w, double dirty = 0.6, double bloody = 0.4)
    {
        Effects.Set(w.Agent(P).Body, EffectKind.Dirty, dirty);
        Effects.Set(w.Agent(P).Body, EffectKind.Bloody, bloody);
    }

    static double Wet(World w) => Effects.Severity(w.Agent(P).Body, EffectKind.Wet);

    [Fact]
    public void WashingInARiver_TakesFiveMinutes_RemovesDirtAndBlood_AndLeavesYouWet()
    {
        var w = Bare();
        w.Agent(P).Tile = ARiver(w);
        Soil(w);
        double wetBefore = Wet(w);
        Assert.True(WashVerb(w).Allowed);
        var r = Sim.Step(w, WashVerb(w).Activity, 30);
        Assert.Equal(("washed", 5), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(StepOutcome.Finished, r.Outcome);
        Assert.False(Effects.Has(w.Agent(P).Body, EffectKind.Dirty));
        Assert.False(Effects.Has(w.Agent(P).Body, EffectKind.Bloody));
        Assert.True(Wet(w) > wetBefore + 0.1, $"wet {Wet(w)} after washing, {wetBefore} before");
        Assert.Contains(r.Events, e => e is Washed);
    }

    [Fact]
    public void TheDirtierTheWash_TheWetterItLeavesYou_ButEvenACleanOneWets()
    {
        double WetAfter(double dirty, double bloody)
        {
            var w = Bare();
            w.Agent(P).Tile = ARiver(w);
            Soil(w, dirty, bloody);
            double before = Wet(w);
            Sim.Step(w, new Wash(P), 30);
            return Wet(w) - before;
        }
        double clean = WetAfter(0, 0), filthy = WetAfter(1, 1);
        Assert.True(clean >= 0.03, $"a clean wash wets {clean}");
        Assert.True(filthy > clean + 0.2, $"filthy {filthy}, clean {clean}");
    }

    [Fact]
    public void WashingWithSnow_WorksWhereThereIsNoOpenWater()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Assert.False(Work.CanDrinkHere(w, camp));
        TestKit.EditGround(w, camp, (g, sub) => Ground.AddSnow(g, sub, 0.1));
        Soil(w);
        double carried = Items.Have(Items.Pack(w, P), Resource.Water);
        Assert.True(WashVerb(w).Allowed);
        Assert.Equal("washed", Sim.Step(w, new Wash(P), 30).InterruptedReason);
        Assert.False(Effects.Has(w.Agent(P).Body, EffectKind.Dirty) || Effects.Has(w.Agent(P).Body, EffectKind.Bloody));
        Assert.Equal(carried, Items.Have(Items.Pack(w, P), Resource.Water));
    }

    [Fact]
    public void WithNeitherWaterNorSnow_WashingIsRefused_NamingWhatIsMissing_UntilWaterIsCarried()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Assert.True(Ground.Underfoot(w, camp).SnowDepthM < Work.MinSnowM && !Work.CanDrinkHere(w, camp));
        var pack = Items.Pack(w, P);
        Items.Take(pack, Resource.Water, double.MaxValue);
        Soil(w);
        var refused = WashVerb(w).Refused;
        Assert.NotNull(refused);
        Assert.Equal("no water or snow here", refused.Reason);
        Assert.Equal(new MissingResource(Resource.Water, Work.WashLiters).AnyOf, ((MissingResource)refused.Missing!).AnyOf);
        Assert.Equal(refused.Reason, Sim.Step(w, new Wash(P), 30).InterruptedReason);

        Items.Add(pack, Resource.Water, 0.3); // not enough to wash with
        var tooLittle = WashVerb(w).Refused!;
        Assert.Equal(Work.WashLiters - 0.3, ((MissingResource)tooLittle.Missing!).Kg, 9);
        Items.Add(pack, Resource.Water, 0.7);
        Assert.Equal("washed", Sim.Step(w, new Wash(P), 30).InterruptedReason);
        Assert.Equal(1 - Work.WashLiters, Items.Have(pack, Resource.Water), 9);
        Assert.False(Effects.Has(w.Agent(P).Body, EffectKind.Dirty));
    }

    [Fact]
    public void Dirty_SurvivesSaveAndLoad()
    {
        var w = Bare();
        Effects.Set(w.Agent(P).Body, EffectKind.Dirty, 0.37);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(0.37, Effects.Severity(loaded.Agent(P).Body, EffectKind.Dirty), 9);
    }
}
