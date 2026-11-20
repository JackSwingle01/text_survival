namespace Survival;

/// Placeholder need drain so Step has something real to do. The Body port replaces this module.
public static class Needs
{
    public const double TiredThresholdPct = 0.25;
    const double AwakeEnergyDeltaPerMinute = -1.0 / (16 * 60); // full to empty in 16 waking hours
    const double RestEnergyDeltaPerMinute = 1.0 / (8 * 60);    // empty to full in 8 hours
    const double AwakeCaloriesDeltaPerMinute = -2000.0 / (24 * 60);
    const double RestCaloriesDeltaPerMinute = -1600.0 / (24 * 60);

    /// Advances one agent one minute. Returns the reason to stop, if any.
    public static string? Tick(World world, Agent agent, bool resting, List<WorldEvent> events)
    {
        double energyBeforePct = agent.EnergyPct;
        agent.EnergyPct = Math.Clamp(energyBeforePct + (resting ? RestEnergyDeltaPerMinute : AwakeEnergyDeltaPerMinute), 0, 1);
        agent.CaloriesKcal = Math.Max(0, agent.CaloriesKcal + (resting ? RestCaloriesDeltaPerMinute : AwakeCaloriesDeltaPerMinute));

        if (energyBeforePct >= TiredThresholdPct && agent.EnergyPct < TiredThresholdPct)
            events.Add(new BecameTired(world.Minute, agent.Id));
        if (energyBeforePct > 0 && agent.EnergyPct == 0)
        {
            events.Add(new Exhausted(world.Minute, agent.Id));
            return "exhausted";
        }
        if (energyBeforePct < 1 && agent.EnergyPct == 1)
        {
            events.Add(new FullyRested(world.Minute, agent.Id));
            return "fully rested";
        }
        return null;
    }
}
