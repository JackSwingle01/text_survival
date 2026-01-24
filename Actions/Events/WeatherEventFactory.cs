using static Weather;

namespace text_survival.Actions.Events;

/// <summary>
/// Creates events for weather condition transitions.
/// Calls existing GameEventRegistry events but triggers them intentionally on transitions
/// rather than through random polling.
/// </summary>
public static class WeatherEventFactory
{
    /// <summary>
    /// Create an event for a weather transition. Returns null if no event is appropriate.
    /// Calls the existing GameEventRegistry event factory methods for full gameplay choices.
    /// </summary>
    public static GameEvent? OnWeatherChange(
        WeatherCondition? previous,
        WeatherCondition current,
        GameContext ctx)
    {
        // Skip if player is not in an activity that should see weather events
        if (ctx.CurrentActivity == ActivityType.Sleeping)
            return null;

        // Skip if not on expedition (most weather events require expedition)
        if (!ctx.Check(EventCondition.IsExpedition))
            return null;

        // Transitions TO dangerous conditions - call existing GameEventRegistry events
        return current switch
        {
            // Blizzard/Whiteout during travel gets Whiteout event
            WeatherCondition.Blizzard when ctx.Check(EventCondition.Traveling) =>
                GameEventRegistry.GetWhiteout(ctx),
            WeatherCondition.Whiteout when ctx.Check(EventCondition.Traveling) =>
                GameEventRegistry.GetWhiteout(ctx),

            // Fog arrival during travel
            WeatherCondition.Misty when ctx.Check(EventCondition.Traveling) &&
                                        previous != WeatherCondition.Misty =>
                GameEventRegistry.GetLostInFog(ctx),

            // Weather clearing after dangerous conditions
            WeatherCondition.Clear when IsFromDangerousCondition(previous) =>
                GameEventRegistry.GetSuddenClearing(ctx),

            _ => null
        };
    }

    /// <summary>
    /// Create an event for the "Calm Before The Storm" phase of prolonged blizzards.
    /// Called separately from OnWeatherChange when front type indicates it.
    /// </summary>
    public static GameEvent? OnCalmBeforeStorm(GameContext ctx)
    {
        return GameEventRegistry.GetMassiveStormApproaching(ctx);
    }

    private static bool IsFromDangerousCondition(WeatherCondition? previous)
    {
        return previous is
            WeatherCondition.Blizzard or
            WeatherCondition.Whiteout or
            WeatherCondition.HeavySnow or
            WeatherCondition.FreezingRain or
            WeatherCondition.Stormy;
    }
}
