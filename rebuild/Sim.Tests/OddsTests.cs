namespace Survival.Tests;

/// Chance functions return their factors (short-loop plan, feature 1). The terms add up to the
/// total exactly, and the totals are the old numbers, bit for bit: each reference below is the
/// formula as it stood before the factors were broken out.
public class OddsTests
{
    static readonly AgentId P = AgentId.Player;

    // ---- the references: the formulas as they were ----

    static double OldFireChance(ToolType tool, Resource tinder, bool impaired, double dexterity)
    {
        double chance = tool switch { ToolType.FireStriker => 0.90, ToolType.BowDrill => 0.50, _ => 0.35 };
        chance += 0 * 0.1 + Fire.Props[tinder].IgnitionBonusPct;
        if (impaired) chance -= 0.2;
        chance -= (1.0 - dexterity) * 0.5;
        return Math.Clamp(chance, 0.05, 0.95);
    }

    static double OldThrown(double distanceM, double maxRangeM, double baseAccuracy, bool small, double penalty)
    {
        if (distanceM > maxRangeM) return 0;
        double accuracy = baseAccuracy * (1.0 - distanceM / maxRangeM);
        if (small) accuracy *= 0.66;
        accuracy -= penalty;
        return Math.Clamp(accuracy, 0.05, 0.95);
    }

    /// The old terms (weather, bad legs) make the footing worse in proportion; the footing curve turns it into a chance.
    static double ExpectedInjury(World world, TileId tile, BodyState body, WeatherSample sky, Pace pace)
    {
        double weather = sky.Condition is WeatherCondition.Blizzard or WeatherCondition.Stormy ? 0.25
            : sky.PrecipitationPct > 0.3 || sky.Condition == WeatherCondition.LightSnow ? 0.15 : 0;
        double impaired = (1 - Body.GetCapacities(body).Moving) * 0.3;
        double footing = Math.Min(1, Traversal.EffectiveHazardLevel(world, tile) * (1 + weather + impaired));
        return Math.Pow(footing, Math.E) / 2 * (pace == Pace.Careful ? 0.25 : 1);
    }

    static readonly ToolType[] Starters = [ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill];
    static readonly Resource[] StrikeFuels = [Resource.Tinder, Resource.BirchBark, Resource.Amadou, Resource.ResinTinder, Resource.Usnea, Resource.Chaga, Resource.Kindling];
    static readonly double[] Dexterities = [0, 0.13, 0.3, 0.5, 0.77, 0.9999, 1];

    /// The fold is the formula: the total is base plus each term in order, clamped, exactly.
    static void AssertAddsUp(Odds o)
    {
        double v = o.BasePct;
        foreach (var t in o.Terms) v = t.Op == OddsOp.Add ? v + t.Value : v * t.Value;
        Assert.Equal(Math.Clamp(v, o.MinPct, o.MaxPct), o.TotalPct);
        Assert.Equal(v != o.TotalPct, o.Capped);
        if (o.Terms.All(t => t.Op == OddsOp.Add))
        {
            double sum = o.BasePct;
            foreach (var (_, delta) in o.Contributions) sum += delta;
            Assert.Equal(o.RawPct, sum); // additive odds: the contributions are the terms, exactly
        }
        Assert.DoesNotContain(o.Terms, t => t.Op == OddsOp.Add ? t.Value == 0 : t.Value == 1);
    }

    [Fact]
    public void FireOdds_AreTheOldNumbers_AndAddUp()
    {
        foreach (var tool in Starters)
            foreach (var tinder in StrikeFuels)
                foreach (bool impaired in new[] { false, true })
                    foreach (double dex in Dexterities)
                    {
                        var odds = Fire.StartOdds(tool, tinder, 0, impaired, dex);
                        Assert.Equal(OldFireChance(tool, tinder, impaired, dex), odds.TotalPct);
                        Assert.Equal(odds.TotalPct, Fire.StartChancePct(tool, tinder, 0, impaired, dex));
                        AssertAddsUp(odds);
                    }
    }

    [Fact]
    public void FireOdds_AtTheExtremes_AreCappedAndSayWhy()
    {
        var best = Fire.StartOdds(ToolType.FireStriker, Resource.BirchBark, skillLevel: Skills.MaxLevel);
        Assert.Equal(0.95, best.TotalPct);
        Assert.True(best.Capped);
        AssertAddsUp(best);

        var worst = Fire.StartOdds(ToolType.HandDrill, Resource.Kindling, 0, consciousnessImpaired: true, dexterityPct: 0, handsWhy: "dark, wet");
        Assert.Equal(0.05, worst.TotalPct);
        Assert.True(worst.Capped);
        Assert.Equal(["Dazed", "Hands (dark, wet)"], worst.Terms.Select(t => t.Factor));
        Assert.Equal([-0.2, -0.5], worst.Contributions.Select(c => c.DeltaPct));
        AssertAddsUp(worst);

        var plain = Fire.StartOdds(ToolType.BowDrill, Resource.Amadou);
        Assert.False(plain.Capped);
        Assert.Equal(("Bow drill", 0.5), (plain.Base, plain.BasePct)); // the player's words (names.json), not the code key
        Assert.Equal(0.5 + 0.40, plain.TotalPct, 12); // amadou +40% (#144)
    }

    /// Fire.cs called StartChancePct with no skill: the player's practice counted for nothing
    /// (bug, fixed). The strike now rolls on the odds the sheet shows, skill and hands and all.
    [Fact]
    public void LightingAFire_RollsOnSkillAndDexterity()
    {
        double FailedAt(int level, bool wet)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var w = TestWorlds.Bare(3);
                ((PersonMind)w.Agent(P).Mind).Skills[Skill.Firecraft] = new SkillProgress { Level = level };
                if (wet) Effects.Set(w.Agent(P).Body, EffectKind.Wet, 1.0);
                TestKit.Give(Items.Pack(w, P), Resource.Tinder, 1);
                w.Rngs[StreamName.Of("fire", P)] = Rng.ForSystem(seed, "ignition trial");
                double sheet = Fire.StartOdds(w, w.Agent(P), ToolType.HandDrill, Resource.Tinder).TotalPct;
                var failed = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 0), 1).Events.OfType<FireStartFailed>().SingleOrDefault();
                if (failed == null) continue;
                Assert.Equal(sheet, failed.ChancePct);
                return failed.ChancePct;
            }
            throw new InvalidOperationException("never failed");
        }
        double novice = FailedAt(0, false), practised = FailedAt(5, false), wetHands = FailedAt(5, true);
        Assert.Equal(novice + 5 * Skills.FirecraftPctPerLevel, practised, 12);
        Assert.True(wetHands < practised, $"wet hands {wetHands} should be worse than dry {practised}");
    }

    [Fact]
    public void ThrowOdds_AreTheOldNumbers_AndAddUp()
    {
        foreach (double range in new[] { 15.0, 20, 25 })
            foreach (double acc in new[] { 0.7, 0.75, 0.9 })
                foreach (bool small in new[] { false, true })
                    foreach (double penalty in new[] { 0, 0.1, 0.33 })
                        for (double d = 0; d <= 27; d += 1.7)
                        {
                            var odds = Combat.ThrownOdds(d, range, acc, small, penalty);
                            Assert.Equal(OldThrown(d, range, acc, small, penalty), odds.TotalPct);
                            Assert.Equal(odds.TotalPct, Combat.ThrownAccuracy(d, range, acc, small, penalty));
                            AssertAddsUp(odds);
                        }
    }

    [Fact]
    public void InjuryOdds_FollowTheFootingCurve_AndAddUp()
    {
        var w = TestWorlds.Bare(17);
        var m = Map.Of(w);
        var hurt = BodyState.BaselineHuman();
        Body.Damage(hurt, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.AnyLeg), Rng.ForSystem(1, "legs"));
        var skies = new[]
        {
            new WeatherSample(0, WeatherCondition.Clear, -5, 0.1, 0, 0.1, WindDirection.North),
            new WeatherSample(0, WeatherCondition.LightSnow, -5, 0.3, 0.2, 0.9, WindDirection.North),
            new WeatherSample(0, WeatherCondition.Blizzard, -20, 0.9, 0.9, 1, WindDirection.North),
        };
        int hazardous = 0;
        for (int x = 0; x < m.Width; x += 3)
            for (int y = 0; y < m.Height; y += 3)
            {
                var t = new TileId(x, y);
                if (!m.TerrainAt(t).IsPassable()) continue;
                foreach (var body in new[] { BodyState.BaselineHuman(), hurt })
                    foreach (var sky in skies)
                        foreach (var pace in new[] { Pace.Quick, Pace.Careful })
                        {
                            var odds = Traversal.InjuryOdds(w, t, body, sky, pace);
                            Assert.Equal(ExpectedInjury(w, t, body, sky, pace), odds.TotalPct, 12);
                            AssertAddsUp(odds);
                            if (odds.TotalPct > 0) hazardous++;
                        }
            }
        Assert.True(hazardous > 0, "no hazardous ground sampled");
    }

    /// A quick crossing's chance of injury is hazard^e / 2, a smooth curve: easy ground nearly safe,
    /// the worst a coin flip, and no step anywhere along it (the old game's 0.3 threshold went from 0 to 30%).
    [Fact]
    public void InjuryOdds_AreHazardToTheEOverTwo_WithNoCliff()
    {
        var calm = new WeatherSample(0, WeatherCondition.Clear, -5, 0, 0, 0, WindDirection.North);
        double Quick(double hazard) => Traversal.InjuryOdds(hazard, BodyState.BaselineHuman(), calm, Pace.Quick).TotalPct;
        Assert.Equal(0, Quick(0));
        Assert.Equal(0.0190, Quick(0.3), 4);
        Assert.Equal(0.0760, Quick(0.5), 4);
        Assert.Equal(0.2287, Quick(0.75), 4);
        Assert.Equal(0.5, Quick(1.0), 12);
        Assert.InRange(Quick(0.29), 0.0170, Quick(0.3));
        for (double h = 0.01; h <= 1; h += 0.01)
            Assert.InRange(Quick(h) - Quick(h - 0.01), 0, 0.014); // steepest at the top: 1.0 vs 0.99
    }

    /// The weather term is named for what earned it (review: a cloudy sky got the +15% that heavy
    /// precipitation earned, and was labelled Cloudy).
    [Fact]
    public void InjuryOdds_WeatherTerm_IsNamedForItsCause()
    {
        var w = TestWorlds.Bare(17);
        var m = Map.Of(w);
        var tile = Enumerable.Range(0, m.Width * m.Height).Select(i => new TileId(i % m.Width, i / m.Width))
            .First(t => m.TerrainAt(t).IsPassable() && Traversal.IsHazardous(w, t));
        var calm = new WeatherSample(0, WeatherCondition.Clear, -5, 0.1, 0, 0.1, WindDirection.North);
        string Why(WeatherSample sky) => Traversal.InjuryOdds(w, tile, BodyState.BaselineHuman(), sky).Terms.Single().Factor;
        Assert.Empty(Traversal.InjuryOdds(w, tile, BodyState.BaselineHuman(), calm).Terms);
        Assert.Equal("Heavy rain", Why(new WeatherSample(0, WeatherCondition.Rainy, 5, 0.1, 0.5, 1, WindDirection.North)));
        Assert.Equal("Heavy snow", Why(new WeatherSample(0, WeatherCondition.HeavySnow, -5, 0.1, 0.5, 1, WindDirection.North)));
        Assert.Equal("Light snow", Why(new WeatherSample(0, WeatherCondition.LightSnow, -5, 0.1, 0.2, 1, WindDirection.North)));
        Assert.DoesNotContain("Cloudy", Why(new WeatherSample(0, WeatherCondition.Cloudy, -5, 0.1, 0.5, 1, WindDirection.North)));
    }

    [Fact]
    public void ForageYield_IsTheOldProduct_AndAddsUp()
    {
        var w = TestWorlds.Bare(17);
        var me = w.Agent(P);
        foreach (bool axe in new[] { false, true })
            foreach (bool shovel in new[] { false, true })
                foreach (double perception in new[] { 0.0, 0.42, 0.97, 1.0, 1.3 })
                {
                    var pack = new Inventory();
                    if (axe) pack.Gear.Add(Items.Make("stone_axe"));
                    if (shovel) pack.Gear.Add(Items.Make("bone_shovel"));
                    double old = Math.Min(perception, 1.0) * (axe ? 1.10 : 1.0) * (shovel ? 1.10 : 1.0);
                    var odds = Work.ForageYield(me, pack, perception);
                    Assert.Equal(old, odds.TotalPct);
                    AssertAddsUp(odds);
                }
    }
}
