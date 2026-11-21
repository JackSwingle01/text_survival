using Survival;
using Xunit;

/// Convection past a gale grows with the square root of the wind (decision 2026-11-21): linear and
/// unchanged up to 1, continuous at 1, and well short of linear at Wind Gap's 2.5.
public class ConvectionTests
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
}
