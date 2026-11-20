namespace Survival;

public sealed record StepResult(List<WorldEvent> Events, int MinutesElapsed, string? InterruptedReason);

public static class Sim
{
    /// Runs an activity minute by minute until done or interrupted. Mutates the world in place.
    /// The same path runs for every agent.
    // ponytail: only the acting agent ticks; other agents catch up lazily once phase 3 adds them (plan decision 3).
    public static StepResult Step(World world, Activity activity, int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Step needs at least one minute");
        var agent = world.Agent(activity.Who);
        var events = new List<WorldEvent>();

        for (int elapsed = 1; elapsed <= minutes; elapsed++)
        {
            world.Minute++;
            string? interrupt = activity switch
            {
                Wait => Needs.Tick(world, agent, resting: false, events),
                Rest => Needs.Tick(world, agent, resting: true, events),
                _ => throw new NotSupportedException($"No step rule for activity {activity.GetType().Name}"),
            };
            if (interrupt != null) return new StepResult(events, elapsed, interrupt);
        }
        return new StepResult(events, minutes, null);
    }
}
