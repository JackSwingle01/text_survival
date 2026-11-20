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
            Fire.CatchUpAround(world, agent, events); // before anything reads the fire this minute
            string? interrupt = activity switch
            {
                StartFire or AddFuel or LightTorch =>
                    (elapsed == 1 ? Fire.Act(world, activity, events) : null) ?? Body.Tick(world, agent, Surroundings(world, agent, activity), events),
                Wait or Rest or Sleep => Body.Tick(world, agent, Surroundings(world, agent, activity), events),
                _ => throw new NotSupportedException($"No step rule for activity {activity.GetType().Name}"),
            };
            if (interrupt != null) return new StepResult(events, elapsed, interrupt);
        }
        return new StepResult(events, minutes, null);
    }

    /// What the world and this activity do to the agent's body this minute.
    static SurvivalContext Surroundings(World world, Agent agent, Activity activity)
    {
        // Every activity so far keeps the agent put beside whatever fire there is: full radiant heat.
        var (activityFactor, fireProximityFactor, sleeping) = activity switch
        {
            Sleep => (0.5, 2.0, true),
            Wait or Rest or StartFire or AddFuel or LightTorch => (1.0, 2.0, false),
            _ => throw new NotSupportedException($"No survival profile for activity {activity.GetType().Name}"),
        };
        // Old SurvivalContext rules: a pit warms only while it has flames, scaled by proximity;
        // a carried torch warms its carrier unscaled; the pit's walls cut the wind.
        double pitHeatF = 0, windProtectionLevel = 0;
        if (Fire.PitAt(world, agent.Tile) is FireId pitId)
        {
            var pit = world.Fires[pitId];
            windProtectionLevel = Fire.WindProtectionLevel(pit.Pit);
            if (Fire.IsActive(pit)) pitHeatF = Fire.HeatOutputAt(world, agent.Tile) * 9 / 5;
        }
        double torchHeatF = Fire.HeatFor(world, agent.Id) * 9 / 5 - Fire.HeatOutputAt(world, agent.Tile) * 9 / 5;
        return new SurvivalContext
        {
            AirTempF = world.Weather.TemperatureC * 9 / 5 + 32,
            WindLevel = Math.Clamp(world.Weather.WindMps / MaxWindMps, 0, 1) * (1 - windProtectionLevel),
            FireBonusF = pitHeatF * fireProximityFactor + torchHeatF,
            ClothingClo = agent.ClothingClo,
            ClothingWeightKg = agent.ClothingWeightKg,
            ActivityFactor = activityFactor,
            IsSleeping = sleeping,
        };
    }

    /// A 45 mph gale: WindLevel 1.
    const double MaxWindMps = 20.1;
}
