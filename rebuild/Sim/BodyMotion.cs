namespace Survival;

/// One stored value, the sprint reserve, so running and fighting tire on one scale (documentation/species-stats-research.md §5).
public static partial class Body
{
    const double RecoveryAtRestSeconds = 360; // Skiba 2012
    /// So all-out fight after fight makes you tired.
    public const double SpentReserveEnergyMinutes = 5;
    const double BurstMetabolicFactor = 20;
    const double JoulesPerKcal = 4184;
    /// An exponential refill never quite ends.
    const double FullAgainLevel = 1e-4;

    public static double SprintReservePct(BodyState body) => 1 - body.SprintSpentLevel;

    /// Physiological time: a wolf recovers in about five and a half minutes, a mammoth in eighteen.
    public static double RecoverySeconds(BodyState body) => RecoveryAtRestSeconds * Math.Pow(body.WeightKg / BaselineWeightKg, 0.25);

    /// Most of the top speed holds until the reserve is nearly spent.
    public static double TopSpeedMps(BodyState body, Running running) =>
        (running.Motion.EnduranceSpeedMps + (running.Motion.TopSpeedMps - running.Motion.EnduranceSpeedMps) * Math.Sqrt(SprintReservePct(body)))
        * running.SpeedPct;

    public static double EnduranceSpeedMps(Running running) => running.Motion.EnduranceSpeedMps * running.SpeedPct;

    public static double BurstCostPct(Running running) => Math.Min(1, running.Motion.BurstPct / running.SpeedPct);

    public static bool CanBurst(BodyState body, Running running) => SprintReservePct(body) >= BurstCostPct(running);

    public static void Burst(BodyState body, Running running)
    {
        Spend(body, BurstCostPct(running));
        double seconds = running.Motion.BurstPct * running.Motion.SprintSeconds;
        SpendKcal(body, BurstMetabolicFactor * MetabolismKcalPerDay(body, 1.0) / 86_400 * seconds);
    }

    public static double SprintUntilSpent(BodyState body, Running running) => Sprint(body, running, SprintSecondsLeft(body, running));

    public static double Sprint(BodyState body, Running running, double seconds)
    {
        var (metres, after) = SprintPlan(body, running, seconds);
        Spend(body, SprintReservePct(body) - after);
        Cover(body, running, metres);
        return metres;
    }

    public static (double Metres, double ReserveAfterPct) SprintPlan(BodyState body, Running running, double seconds)
    {
        var m = running.Motion;
        double f = running.SpeedPct;
        double reserve = SprintReservePct(body);
        double drainSeconds = Math.Min(seconds, SprintSecondsLeft(body, running));
        double after = reserve - drainSeconds / (m.SprintSeconds * f);
        if (after < 1e-12) after = 0;
        double metres = m.EnduranceSpeedMps * f * seconds
            + (m.TopSpeedMps - m.EnduranceSpeedMps) * f * (2.0 / 3.0) * (Math.Pow(reserve, 1.5) - Math.Pow(after, 1.5)) * m.SprintSeconds * f;
        return (metres, after);
    }

    public static double SprintSecondsLeft(BodyState body, Running running) =>
        SprintReservePct(body) * running.Motion.SprintSeconds * running.SpeedPct;

    public static void Cover(BodyState body, Running running, double metres) =>
        SpendKcal(body, running.Motion.RunCostJPerKgM * metres * (body.WeightKg + running.CarriedKg) / JoulesPerKcal);

    /// Returns what the store and reserves could not pay; the heart answers for it the next minute lived.
    public static double SpendKcal(BodyState body, double kcal)
    {
        if (kcal <= 0) return 0;
        double fromStore = Math.Min(kcal, body.CaloriesKcal);
        body.CaloriesKcal -= fromStore;
        if (kcal - fromStore <= 0) return 0;
        var (fatKg, muscleKg, unpaid) = ReserveBurn(body, kcal - fromStore);
        body.FatKg -= fatKg;
        body.MuscleKg -= muscleKg;
        return unpaid;
    }

    static void Spend(BodyState body, double pct)
    {
        if (pct <= 0) return;
        bool had = body.SprintSpentLevel < 1;
        body.SprintSpentLevel = Math.Min(1, body.SprintSpentLevel + pct);
        if (had && body.SprintSpentLevel >= 1) body.EnergyMinutes = Math.Max(0, body.EnergyMinutes - SpentReserveEnergyMinutes);
    }

    static void Recover(BodyState body, double seconds)
    {
        if (body.SprintSpentLevel <= 0) return;
        body.SprintSpentLevel *= Math.Exp(-seconds / RecoverySeconds(body));
        if (body.SprintSpentLevel < FullAgainLevel) body.SprintSpentLevel = 0;
    }
}
