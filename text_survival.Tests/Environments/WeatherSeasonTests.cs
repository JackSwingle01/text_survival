using text_survival.Actions;

namespace text_survival.Tests.Environments;

/// <summary>
/// Guards the class of bug that is dangerous precisely because nothing crashes: the world
/// quietly reports a plausible but wrong number. A game starting on July 1 once spent its
/// first two days around 0F - roughly 40F below its own summer range - because Weather
/// derived its season from a clock that had not been set yet, so every front built in the
/// constructor was a winter front. Nothing threw; NPCs simply froze to death and the AI got
/// the blame.
/// </summary>
public class WeatherSeasonTests
{
    /// <summary>Air temperature ranges the weather generator itself declares per season, in Celsius.</summary>
    private static (double Min, double Max) SeasonalRangeC(Weather.Season season) => season switch
    {
        Weather.Season.Winter => (-30, -5),
        Weather.Season.Spring => (-15, 5),
        Weather.Season.Summer => (-5, 15),
        Weather.Season.Fall => (-10, 5),
        _ => throw new ArgumentOutOfRangeException(nameof(season)),
    };

    // Fronts may legitimately push a few degrees past the plain seasonal band (a cold snap
    // is supposed to bite), so allow headroom. The bug this guards against was 20C+ out.
    private const double ToleranceC = 8;

    [Fact]
    public void NewGame_StartsInItsOwnSeasonsTemperatureRange()
    {
        var ctx = GameContext.CreateNewGame(seed: 1);
        var weather = ctx.Weather;

        var (min, max) = SeasonalRangeC(weather.CurrentSeason);

        Assert.True(
            weather.BaseTemperature >= min - ToleranceC && weather.BaseTemperature <= max + ToleranceC,
            $"A new game starting {GameContext.StartTime:MMM d} is in {weather.CurrentSeason}, whose range is " +
            $"{min}..{max}C, but the weather opened at {weather.BaseTemperature:F1}C " +
            $"({weather.TemperatureInFahrenheit:F1}F).");
    }

    [Fact]
    public void SeasonMatchesStartDate()
    {
        var ctx = GameContext.CreateNewGame(seed: 1);

        Assert.Equal(GameContext.StartTime.Date, ctx.Weather.Time.Date);
        Assert.Equal(Weather.Season.Summer, ctx.Weather.CurrentSeason);
    }

    /// <summary>
    /// The first days are the ones every new player and every simulated NPC actually lives
    /// through, so they are the ones that must not be secretly another season.
    /// </summary>
    [Fact]
    public void FirstThreeDays_StayWithinSeasonalRange()
    {
        var ctx = GameContext.CreateNewGame(seed: 1);
        var (min, max) = SeasonalRangeC(ctx.Weather.CurrentSeason);

        var offenders = new List<string>();
        for (int hour = 0; hour < 72; hour++)
        {
            ctx.UpdateWithoutEvents(60, ActivityType.Idle);
            double c = ctx.Weather.BaseTemperature;
            if (c < min - ToleranceC || c > max + ToleranceC)
                offenders.Add($"hour {hour}: {c:F1}C ({ctx.Weather.TemperatureInFahrenheit:F1}F)");
        }

        Assert.True(offenders.Count == 0,
            $"{ctx.Weather.CurrentSeason} range is {min}..{max}C (+/-{ToleranceC} for fronts), but:\n  " +
            string.Join("\n  ", offenders.Take(10)));
    }
}
