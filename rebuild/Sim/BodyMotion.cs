namespace Survival;

/// Running and the fight energy model (documentation/species-stats-research.md §5; species round,
/// decided 2026-11-21). A body has one stored value, its sprint reserve (BodyState.SprintSpentLevel):
/// sprinting above endurance speed drains it, every strike, dodge or shove takes a slice, and it
/// refills every minute lived over a recovery time that grows with the body's weight. Wounds,
/// fatigue and load cut a body's speeds and stretch what each burst costs (Running.SpeedPct). What
/// running and striking burn comes out of the calorie store as metabolic work, then out of fat.
/// Long-term tiredness stays EnergyMinutes: only emptying the reserve costs a little of it.
public static partial class Body
{
    /// A person's reserve refills with a time constant of about six minutes at rest (Skiba 2012, R).
    const double RecoveryAtRestSeconds = 360;
    /// Sleep that emptying the reserve costs, so all-out fight after fight makes you tired (D).
    public const double SpentReserveEnergyMinutes = 5;
    /// A strike, a grapple or a dodge works at about twenty times the resting rate for the seconds
    /// of sprint it costs (research §5.2, D).
    const double BurstMetabolicFactor = 20;
    const double JoulesPerKcal = 4184;
    /// A reserve this close to full is full: an exponential refill otherwise never quite ends.
    const double FullAgainLevel = 1e-4;

    public static double SprintReservePct(BodyState body) => 1 - body.SprintSpentLevel;

    /// The reserve's recovery time constant: a person's six minutes, scaled by weight^0.25
    /// (physiological time), so a wolf recovers in about five and a half and a mammoth in eighteen.
    public static double RecoverySeconds(BodyState body) => RecoveryAtRestSeconds * Math.Pow(body.WeightKg / BaselineWeightKg, 0.25);

    /// Flat out now: endurance speed plus what the reserve still adds (its square root: most of the top
    /// speed holds until the reserve is nearly spent), at the share wounds and load leave.
    public static double TopSpeedMps(BodyState body, Running running) =>
        (running.Motion.EnduranceSpeedMps + (running.Motion.TopSpeedMps - running.Motion.EnduranceSpeedMps) * Math.Sqrt(SprintReservePct(body)))
        * running.SpeedPct;

    public static double EnduranceSpeedMps(Running running) => running.Motion.EnduranceSpeedMps * running.SpeedPct;

    /// The share of a full reserve one burst takes: the species' slice, stretched by wounds and load.
    public static double BurstCostPct(Running running) => Math.Min(1, running.Motion.BurstPct / running.SpeedPct);

    /// Whether there is breath left for a burst: a spent fighter cannot dodge.
    public static bool CanBurst(BodyState body, Running running) => SprintReservePct(body) >= BurstCostPct(running);

    /// One strike, dodge or shove: its slice of the reserve, and its seconds of work.
    public static void Burst(BodyState body, Running running)
    {
        Spend(body, BurstCostPct(running));
        double seconds = running.Motion.BurstPct * running.Motion.SprintSeconds;
        SpendKcal(body, BurstMetabolicFactor * MetabolismKcalPerDay(body, 1.0) / 86_400 * seconds);
    }

    /// Runs flat out until the reserve is spent, as anything running for its life does. Returns the metres run.
    public static double SprintUntilSpent(BodyState body, Running running) =>
        Sprint(body, running, SprintReservePct(body) * running.Motion.SprintSeconds * running.SpeedPct);

    /// Runs flat out for these seconds: the reserve drains in SprintSeconds at full strength (sooner
    /// hurt or laden) and the pace falls with it, then holds at endurance speed. Returns the metres run.
    public static double Sprint(BodyState body, Running running, double seconds)
    {
        var m = running.Motion;
        double f = running.SpeedPct;
        double reserve = SprintReservePct(body);
        // The reserve falls linearly at 1 / (SprintSeconds x f) a second; speed rides its square root.
        double drainSeconds = Math.Min(seconds, reserve * m.SprintSeconds * f);
        double after = reserve - drainSeconds / (m.SprintSeconds * f);
        if (after < 1e-12) after = 0;
        double metres = m.EnduranceSpeedMps * f * seconds
            + (m.TopSpeedMps - m.EnduranceSpeedMps) * f * (2.0 / 3.0) * (Math.Pow(reserve, 1.5) - Math.Pow(Math.Max(0, after), 1.5)) * m.SprintSeconds * f;
        Spend(body, reserve - Math.Max(0, after));
        Cover(body, running, metres);
        return metres;
    }

    /// The metabolic work of moving this far with what is carried (net cost of transport, Taylor 1982).
    public static void Cover(BodyState body, Running running, double metres) =>
        SpendKcal(body, running.Motion.RunCostJPerKgM * metres * (body.WeightKg + running.CarriedKg) / JoulesPerKcal);

    /// Calories spent on work: out of the store first, then the reserves in the order starvation burns
    /// them (ReserveBurn). Past both floors nothing is left to pay with: a person's heart answers for
    /// that on the next minute lived (ProcessLethal), and an animal that far gone starves through
    /// Fauna.Renew. Returns what could not be paid.
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

    /// Spends this share of the reserve; emptying it costs a little sleep, once.
    static void Spend(BodyState body, double pct)
    {
        if (pct <= 0) return;
        bool had = body.SprintSpentLevel < 1;
        body.SprintSpentLevel = Math.Min(1, body.SprintSpentLevel + pct);
        if (had && body.SprintSpentLevel >= 1) body.EnergyMinutes = Math.Max(0, body.EnergyMinutes - SpentReserveEnergyMinutes);
    }

    /// The reserve refills toward full: what is still spent falls by e over each recovery time.
    static void Recover(BodyState body, double seconds)
    {
        if (body.SprintSpentLevel <= 0) return;
        body.SprintSpentLevel *= Math.Exp(-seconds / RecoverySeconds(body));
        if (body.SprintSpentLevel < FullAgainLevel) body.SprintSpentLevel = 0; // full again: nothing left to refill or save
    }
}
