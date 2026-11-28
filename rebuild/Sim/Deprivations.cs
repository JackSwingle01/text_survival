using System.Text.Json.Serialization;

namespace Survival;

/// An unmet need wearing a body down: read from its stores each minute, never stored (GLOSSARY: Deprivation).
[JsonConverter(typeof(JsonStringEnumConverter<Deprivation>))]
public enum Deprivation { Hungry, Thirsty, Tired }

/// Severities 0 to 1 from the stores as they stand: a deprivation lifts the moment its need is met.
public static class Deprivations
{
    public const double ThirstOnsetPct = 0.50, HungerOnsetPct = 0.30, TiredOnsetPct = 0.30;

    /// What each multiplies at full: never zero, so no mix takes a faculty there. Thirst reads blood as if 15% were lost.
    const double TiredConsciousnessPct = 0.5, ThirstyConsciousnessPct = 0.7, HungryConsciousnessPct = 0.9;

    const double ThirstyBloodReadPct = 0.85, HungryHealingPct = 0.5, ThirstyHealingPct = 0.5;

    internal static readonly Deprivation[] All = Enum.GetValues<Deprivation>();

    /// Animals live outside the person's survival model (Fauna), and carry none yet.
    static bool FeelsAny(BodyState body) => body.Kind == BodyKind.Human;

    public static double Severity(BodyState body, Deprivation d) => !FeelsAny(body) ? 0 : d switch
    {
        Deprivation.Hungry => Below(HungerOnsetPct, body.CaloriesKcal / Body.CaloriesCapacityKcal(body)),
        Deprivation.Thirsty => Below(ThirstOnsetPct, body.HydrationMl / Body.MaxHydrationMlOf(body)),
        Deprivation.Tired => Below(TiredOnsetPct, body.EnergyMinutes / Body.MaxEnergyMinutes),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "No store for this deprivation"),
    };

    public static IReadOnlyList<(Deprivation Kind, double Severity)> Of(BodyState body)
    {
        if (!FeelsAny(body)) return [];
        var felt = new List<(Deprivation, double)>(All.Length);
        foreach (var d in All)
            if (Severity(body, d) is var s && s > 0) felt.Add((d, s));
        return felt;
    }

    public static bool Has(BodyState body, Deprivation d) => Severity(body, d) > 0;

    internal static int Felt(BodyState body)
    {
        int felt = 0;
        if (FeelsAny(body)) foreach (var d in All) if (Has(body, d)) felt |= 1 << (int)d;
        return felt;
    }

    internal static bool In(int felt, Deprivation d) => (felt & (1 << (int)d)) != 0;

    static double Below(double onsetPct, double pct) => Math.Max(0, (onsetPct - Math.Clamp(pct, 0, 1)) / onsetPct);

    static double Toward(double atFullPct, double severity) => 1 - (1 - atFullPct) * severity;

    internal static double ConsciousnessPct(BodyState body) => !FeelsAny(body) ? 1
        : Toward(TiredConsciousnessPct, Severity(body, Deprivation.Tired))
            * Toward(ThirstyConsciousnessPct, Severity(body, Deprivation.Thirsty))
            * Toward(HungryConsciousnessPct, Severity(body, Deprivation.Hungry));

    internal static double HealingPct(BodyState body) =>
        Toward(HungryHealingPct, Severity(body, Deprivation.Hungry)) * Toward(ThirstyHealingPct, Severity(body, Deprivation.Thirsty));

    internal static double BloodReadPct(BodyState body) => Toward(ThirstyBloodReadPct, Severity(body, Deprivation.Thirsty));
}
