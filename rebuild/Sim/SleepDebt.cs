namespace Survival;

/// Minutes awake not yet repaid by sleep (GLOSSARY: Sleep debt), read against a species' sleep hours and onsets.
public static class SleepDebt
{
    /// From one at a waking day's debt to this at the full hour, so any debt clears in one long night.
    const double RepayScaleAtFull = 10;
    const double CollapseOnsetShareOfRamp = 0.6, CollapseAtFullPctPerMinute = 0.01;

    public static double WakingDayMinutes(SpeciesStats s) => (24 - s.SleepHoursPerDay) * 60;
    public static double OnsetMinutes(SpeciesStats s) => s.TiredOnsetHours * 60;
    public static double FullMinutes(SpeciesStats s) => s.TiredFullHours * 60;
    public static double CollapseOnsetMinutes(SpeciesStats s) => OnsetMinutes(s) + CollapseOnsetShareOfRamp * (FullMinutes(s) - OnsetMinutes(s));

    public static double TiredSeverity(SpeciesStats s, double debtMinutes) => Ramp(OnsetMinutes(s), FullMinutes(s), debtMinutes);

    public static double RepayPerMinute(SpeciesStats s, double debtMinutes) =>
        (24 - s.SleepHoursPerDay) / s.SleepHoursPerDay * (1 + (RepayScaleAtFull - 1) * Ramp(WakingDayMinutes(s), FullMinutes(s), debtMinutes));

    public static double CollapsePctPerMinute(SpeciesStats s, double debtMinutes) =>
        CollapseAtFullPctPerMinute * Ramp(CollapseOnsetMinutes(s), FullMinutes(s), debtMinutes);

    public static double DeltaPerMinute(SpeciesStats s, double debtMinutes, bool sleeping, double sleepDepthPct = 1) =>
        sleeping ? -RepayPerMinute(s, debtMinutes) * sleepDepthPct : 1;

    static double Ramp(double from, double to, double x) => Math.Clamp((x - from) / (to - from), 0, 1);
}
