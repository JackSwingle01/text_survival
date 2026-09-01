using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// The one place that decides how fast game time flows on screen.
/// </summary>
public static class Pacing
{
    public const float SecondsPerMinute = 0.3f;

    /// <summary>How long a progress bar covering <paramref name="minutes"/> should take.</summary>
    public static float ProgressSeconds(int minutes) => Math.Clamp(minutes * SecondsPerMinute, 1f, 30f);

    /// <summary>How long the walk to the next tile should take on screen.</summary>
    public static float TravelSeconds(int minutes) => Math.Clamp(0.5f + minutes * 0.03f, 0.5f, 1.2f);

    /// <summary>
    /// Let <paramref name="minutes"/> of game time pass at screen pace, one frame at a
    /// time. Every timed activity runs through here: rest, sleep, work, camp setup,
    /// event time costs, incapacitation, travel.
    /// Returns how many minutes actually elapsed and whether an event cut it short.
    /// </summary>
    public static async Task<(int elapsed, bool interrupted)> PassTime(
        GameContext ctx, int minutes, ActivityType activity, ProgressView? view, bool allowEvents = true)
    {
        if (minutes <= 0) return (0, false);

        var run = new TimedRun(minutes, ProgressSeconds(minutes));

        if (view != null)
            view.TotalMinutes = minutes;

        while (!run.Done && ctx.player.IsAlive)
        {
            float dt = await ctx.Ui.NextFrame();
            int due = run.Advance(dt);

            for (int i = 0; i < due; i++)
            {
                if (allowEvents)
                    await ctx.Update(1, activity);
                else
                    ctx.UpdateWithoutEvents(1, activity);

                run.MarkSimulated(1);

                if (allowEvents && ctx.EventOccurredLastUpdate)
                    return (run.SimulatedMinutes, true);

                if (!ctx.player.IsAlive) break;
            }

            if (view != null)
            {
                view.Progress = run.Progress;
                view.SimulatedMinutes = run.SimulatedMinutes;
            }
        }

        return (run.SimulatedMinutes, false);
    }
}
