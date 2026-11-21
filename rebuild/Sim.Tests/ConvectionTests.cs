using Survival;
using Xunit;
using Xunit.Abstractions;

/// Convection past a gale grows with the square root of the wind (decision 2026-11-21): linear and
/// unchanged up to 1, continuous at 1, and well short of linear at Wind Gap's 2.5.
public class ConvectionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.72)]
    [InlineData(1.0)]
    public void UpToAGale_TheWindIsAsBefore(double wind) =>
        Assert.Equal(wind, Body.ConvectiveWind(wind));

    [Fact]
    public void PastAGale_ItGrowsWithTheSquareRoot_AndNeverDrops()
    {
        Assert.Equal(1.0, Body.ConvectiveWind(1.0 + 1e-12), 9); // continuous at 1
        double previous = 1.0;
        for (double w = 1.05; w <= 3.0; w += 0.05)
        {
            double c = Body.ConvectiveWind(w);
            Assert.True(c > previous, $"convective wind fell at {w}");
            Assert.True(c < w, $"convective wind at {w} should be sublinear");
            previous = c;
        }
        double gale = Body.ConvectiveCoefficient(new SurvivalContext { WindLevel = 1.0 });
        double windGap = Body.ConvectiveCoefficient(new SurvivalContext { WindLevel = 2.5 });
        Assert.InRange(windGap / gale, 1.3, 1.45); // linear would be 2.0
    }

    /// Wind through the clothing rides the same curve (Body.ConvectiveWind), so past a gale the body's heat loss grows
    /// with about the square root of the wind: Wind Gap's 2.5 strips about 1.4x a gale's heat. Old (this rebuild): the
    /// clothing term was linear in the wind and floored past a gale, so at 2.5 the body cooled 2.0x as fast as in a
    /// gale (-9.98 against -4.98 F/h). The cooling rate itself rises a little more than the loss (about 1.5-1.6x), the
    /// metabolism being the same in both winds. At a gale and under, ConvectiveWind is the wind itself: nothing changes.
    [Theory]
    [InlineData(1.33)]
    [InlineData(2.0)]
    public void PastAGale_TheWholeBodyLosesHeatOnTheSquareRoot(double clo)
    {
        var w = World.New(17);
        var p = w.Agent(AgentId.Player);
        var still = Sim.Surroundings(w, p, new Wait(AgentId.Player), []) with { AirTempF = -10, FireBonusF = 0, TorchHeatF = 0, ClothingClo = clo, FireDryingWindLevel = null };
        HeatBalance At(double wind) => Body.CalculateHeatBalance(p.Body, still with { WindLevel = wind });
        var (gale, windGap) = (At(1.0), At(2.5));
        double lossRatio = windGap.SensibleLossKcalPerHour / gale.SensibleLossKcalPerHour, rateRatio = windGap.DegreesFPerHour / gale.DegreesFPerHour;
        output.WriteLine($"clo {clo}: loss {gale.SensibleLossKcalPerHour:0} -> {windGap.SensibleLossKcalPerHour:0} kcal/h ({lossRatio:0.000}x); " +
                         $"dT/dt {gale.DegreesFPerHour:0.00} -> {windGap.DegreesFPerHour:0.00} F/h ({rateRatio:0.000}x)");
        Assert.True(gale.DegreesFPerHour < 0, "a gale at -10F should cool the body");
        Assert.InRange(lossRatio, 1.3, 1.5);
        Assert.InRange(rateRatio, 1.3, 1.7); // the old clothing term had it at 2.0
    }
}
