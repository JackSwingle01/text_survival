using CsCheck;
using Survival.Tests.Verification;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests;

/// Fire starting (#144; spec "Recipes that make sense", Fire): the strike-a-light paired by feel, each tinder's
/// catch bonus on the odds sheet, resin tinder made and struck, and no strike surer than 95%.
public class FireStartingTests
{
    static World Night(out Inventory pack)
    {
        var w = Bare(-2);
        w.Minute = 14 * 60; // eleven at night, and no fire
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    [Fact]
    public void TheStrikeALight_IsPairedInTheDark_WithNoTool()
    {
        var w = Night(out var pack);
        Assert.True(Weather.DarknessLevel(w, w.Agent(P).Tile) > 0.5);
        var r = Recipes.Get("fire_striker");
        Assert.Empty(r.Tools);
        Assert.Equal(5, r.Minutes);
        TestKit.GivePieces(pack, Resource.Flint, 1);
        TestKit.GivePieces(pack, Resource.Pyrite, 1);
        Assert.Null(Sim.Refusal(w, new Make(P, "fire_striker")));
        var step = Sim.Step(w, new Make(P, "fire_striker"), Verbs.UntilDoneMinutes);
        Assert.True(Assert.Single(step.Events.OfType<Made>()).Succeeded);
        Assert.NotNull(Items.Tool(pack, ToolType.FireStriker));
    }

    [Theory]
    [InlineData(Resource.Tinder, 0.15)]
    [InlineData(Resource.BirchBark, 0.30)]
    [InlineData(Resource.Amadou, 0.40)]
    [InlineData(Resource.ResinTinder, 0.45)]
    public void TheOddsSheet_ShowsEachTindersCatchBonus(Resource tinder, double bonusPct)
    {
        var w = Bare(-2);
        w.Minute = 3 * 60; // noon: no darkness on the hands
        var odds = Fire.StartOdds(w, w.Agent(P), ToolType.HandDrill, tinder);
        var term = Assert.Single(odds.Contributions, c => c.Factor == Names.Title(tinder));
        Assert.Equal(bonusPct, term.DeltaPct, 12);
        Assert.Contains(tinder, Fire.Tinders);
    }

    [Fact]
    public void ResinTinder_IsMadeFromTinderAndResin_AndLightsAFire()
    {
        var w = Night(out var pack);
        var r = Recipes.Get("resin_tinder");
        Assert.Empty(r.Tools);
        Assert.Equal(5, r.Minutes);
        Items.Add(pack, Resource.Tinder, 0.04);
        Items.Add(pack, Resource.PineResin, 0.01);
        Sim.Step(w, new Make(P, "resin_tinder"), Verbs.UntilDoneMinutes);
        Assert.Equal(0.05, Items.Have(pack, Resource.ResinTinder), 9);
        Assert.Equal(0, Items.Have(pack, Resource.Tinder), 9);
        Assert.Equal(0, Items.Have(pack, Resource.PineResin), 9);

        pack.Gear.Add(Items.Make("fire_striker"));
        var strike = new StartFire(P, ToolType.FireStriker, Resource.ResinTinder, Verbs.TinderKg, 0);
        Assert.Null(Sim.Refusal(w, strike));
        for (int tries = 0; !Sim.Step(w, strike, 1).Events.OfType<FireLit>().Any(); tries++)
        {
            Assert.True(tries < 4, "the striker kept failing");
            Items.Add(pack, Resource.ResinTinder, Verbs.TinderKg);
        }
        Assert.NotNull(Fire.PitAt(w, w.Agent(P).Tile));
    }

    static readonly Resource[] Fuels = [.. Fire.Props.Keys.Where(r => Fire.Props[r].MinFireTempF == 0).Order()];

    /// Any tool, any tinder, any practice, dazed or not, any time of day and its weather, any wetness: the
    /// forecast a person reads before striking never promises more than 95%.
    [Fact]
    public void NoStrike_IsForecastAbove95Pct()
    {
        Gen.Select(Gen.Int[0, Fire.Starters.Count - 1], Gen.Int[0, Fuels.Length - 1], Gen.Int[0, Skills.MaxLevel], Gen.Int[0, 2 * 1440 - 1],
            Gen.Double[0, 1], Gen.Bool).Check(c =>
        {
            var (tool, tinder, level, minute, wet, dazed) = c;
            var w = Bare(-5);
            w.Minute = minute;
            var me = w.Agent(P);
            var pack = Items.Pack(w, P);
            TestKit.ClearResources(pack);
            pack.Gear.Clear();
            pack.Gear.Add(Items.Make(Fire.Starters[tool] switch { ToolType.FireStriker => "fire_striker", ToolType.BowDrill => "bow_drill", _ => "hand_drill" }));
            TestKit.Give(pack, Fuels[tinder], 1);
            ((PersonMind)me.Mind).Skills[Skill.Firecraft] = new SkillProgress { Level = level };
            if (wet > 0) Effects.Set(me.Body, EffectKind.Wet, wet);
            if (dazed) Effects.Set(me.Body, EffectKind.Dazed, 1.0);
            var strike = new StartFire(P, Fire.Starters[tool], Fuels[tinder], Verbs.TinderKg, 0);
            Assert.InRange(Fire.ForecastLight(w, strike).StrikePct, 0.0, 0.95);
            Assert.InRange(Fire.StartOdds(w, me, strike.Tool, strike.Tinder).TotalPct, 0.0, 0.95);
        }, iter: 60);
    }

    /// The same bound over the formula alone, every combination of its inputs.
    [Fact]
    public void NoStartOdds_AreAbove95Pct()
    {
        Gen.Select(Gen.Int[0, Fire.Starters.Count - 1], Gen.Int[0, Fuels.Length - 1], Gen.Int[0, Skills.MaxLevel], Gen.Bool, Gen.Double[0, 1])
            .Check(c => Assert.InRange(Fire.StartOdds(Fire.Starters[c.Item1], Fuels[c.Item2], c.Item3, c.Item4, c.Item5).TotalPct, 0.05, 0.95), iter: 1000);
    }

    [Fact]
    public void ThereIsNoTinderBundle()
    {
        static bool Bundle(string s) => s.Replace("_", "").Replace(" ", "").Contains("tinderbundle", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Enum.GetNames<Resource>(), Bundle);
        Assert.DoesNotContain(Recipes.All, r => Bundle(r.Id) || Bundle(r.Name));
        Assert.DoesNotContain(Items.GearKinds.Keys, Bundle);
    }
}
