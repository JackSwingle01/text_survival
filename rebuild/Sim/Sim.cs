namespace Survival;

public sealed record StepResult(List<WorldEvent> Events, int MinutesElapsed, string? InterruptedReason);

public static class Sim
{
    /// Runs an activity minute by minute until done or interrupted. Mutates the world in place.
    /// The same path runs for every agent. A refused activity returns at once: no time passes.
    // ponytail: only the acting agent ticks; other agents catch up lazily once phase 3 adds them (plan decision 3).
    public static StepResult Step(World world, Activity activity, int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Step needs at least one minute");
        var agent = world.Agent(activity.Who);
        if (!Body.IsAlive(agent.Body)) throw new InvalidOperationException($"{agent.Id} is dead and cannot act");
        var events = new List<WorldEvent>();

        string? acted = null;
        if (activity is StartFire or AddFuel or LightTorch)
        {
            Fire.CatchUpAround(world, agent, events); // the refusal reads the fire as it is now
            if (Fire.Refusal(world, activity) is string refused) return new StepResult(events, 0, refused);
            acted = Fire.Act(world, activity, events); // a failed ignition still costs its minute
        }
        else if (activity is not (Wait or Rest or Sleep))
            throw new NotSupportedException($"No step rule for activity {activity.GetType().Name}");

        for (int elapsed = 1; elapsed <= minutes; elapsed++)
        {
            world.Minute++;
            Fire.CatchUpAround(world, agent, events); // before anything reads the fire this minute
            string? interrupt = Body.Tick(world, agent, Surroundings(world, agent, activity), events) ?? acted;
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
        var warmth = Fire.WarmthFor(world, agent.Id);
        return new SurvivalContext
        {
            // ponytail: no NWS wind chill on the air yet (old Location.CalculateWindChillNWS); Phase 2 Weather owns it.
            AirTempF = world.Weather.TemperatureC * 9 / 5 + 32 + warmth.AirHeatF,
            WindLevel = Math.Clamp(world.Weather.WindMps / MaxWindMps, 0, 1) * (1 - warmth.WindProtectionLevel),
            FireBonusF = warmth.PitHeatF * fireProximityFactor,
            TorchHeatF = warmth.TorchHeatF,
            ClothingClo = agent.ClothingClo,
            ClothingWeightKg = agent.ClothingWeightKg,
            ActivityFactor = activityFactor,
            IsSleeping = sleeping,
        };
    }

    /// A 45 mph gale: WindLevel 1.
    const double MaxWindMps = 20.1;
}
