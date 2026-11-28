namespace Survival;

/// Minutes awake not yet repaid by sleep (GLOSSARY: Sleep debt), read against a species' sleep hours and onsets.
public static class SleepDebt
{
    /// From one at a waking day's debt to this at the full hour, so any debt clears in one long night.
    const double RepayFactorAtFull = 10;
    const double CollapseOnsetPctOfRamp = 0.6, CollapseAtFullPctPerMinute = 0.01;
    const double SeenToBandMinutes = 120;

    public static double WakingDayMinutes(SpeciesStats s) => (24 - s.SleepHoursPerDay) * 60;
    public static double OnsetMinutes(SpeciesStats s) => s.TiredOnsetHours * 60;
    public static double SeenToFloorMinutes(SpeciesStats s) => Math.Min(WakingDayMinutes(s), OnsetMinutes(s) - SeenToBandMinutes);
    public static double FullMinutes(SpeciesStats s) => s.TiredFullHours * 60;
    public static double CollapseOnsetMinutes(SpeciesStats s) => OnsetMinutes(s) + CollapseOnsetPctOfRamp * (FullMinutes(s) - OnsetMinutes(s));

    public static double TiredSeverity(SpeciesStats s, double debtMinutes) => Ramp(OnsetMinutes(s), FullMinutes(s), debtMinutes);

    public static double RepayPerMinute(SpeciesStats s, double debtMinutes) =>
        WakingDayMinutes(s) / (s.SleepHoursPerDay * 60) * (1 + (RepayFactorAtFull - 1) * Ramp(WakingDayMinutes(s), FullMinutes(s), debtMinutes));

    public static double CollapsePctPerMinute(SpeciesStats s, double debtMinutes) =>
        CollapseAtFullPctPerMinute * Ramp(CollapseOnsetMinutes(s), FullMinutes(s), debtMinutes);

    public static double DeltaPerMinute(SpeciesStats s, double debtMinutes, bool sleeping, double sleepFactor = 1) =>
        sleeping ? -RepayPerMinute(s, debtMinutes) * sleepFactor : 1;

    public static double RestedPct(SpeciesStats s, double debtMinutes) => Math.Clamp(1 - debtMinutes / FullMinutes(s), 0, 1);

    static double Ramp(double from, double to, double x) => Math.Clamp((x - from) / (to - from), 0, 1);
}
