using System.Text.Json.Serialization;

namespace Survival;

/// An unmet need wearing a body down: read from its stores each minute, never stored (GLOSSARY: Deprivation).
[JsonConverter(typeof(JsonStringEnumConverter<Deprivation>))]
public enum Deprivation { Hungry, Thirsty, Tired }

/// Severities 0 to 1 from the stores as they stand: a deprivation lifts the moment its need is met.
public static class Deprivations
{
    internal const double ThresholdPct = 0.30;

    const double EmptyStorePangLoss = 0.05, StarvingRampDays = 20;

    /// What each takes off each capacity at full severity.
    static readonly (Capacity Capacity, double Delta)[] HungryLosses =
        [(Capacity.Moving, -0.35), (Capacity.Manipulation, -0.35), (Capacity.Consciousness, -0.15)];
    static readonly (Capacity Capacity, double Delta)[] ThirstyLosses =
        [(Capacity.Moving, -0.40), (Capacity.Manipulation, -0.25), (Capacity.Consciousness, -0.45)];
    static readonly (Capacity Capacity, double Delta)[] TiredLosses =
        [(Capacity.Moving, -0.45), (Capacity.Manipulation, -0.30), (Capacity.Consciousness, -0.45), (Capacity.Sight, -0.25), (Capacity.Hearing, -0.15)];

    static (Capacity Capacity, double Delta)[] LossesOf(Deprivation d) => d switch
    {
        Deprivation.Hungry => HungryLosses,
        Deprivation.Thirsty => ThirstyLosses,
        Deprivation.Tired => TiredLosses,
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "No losses for this deprivation"),
    };

    static readonly double EmptyStoreHungerSeverity = EmptyStorePangLoss / -HungryLosses.Single(l => l.Capacity == Capacity.Moving).Delta;

    internal static readonly Deprivation[] All = Enum.GetValues<Deprivation>();

    /// Animals live outside the person's survival model (Fauna), and carry none yet.
    static bool FeelsAny(BodyState body) => body.Kind == BodyKind.Human;

    public static double Severity(BodyState body, Deprivation d) => !FeelsAny(body) ? 0 : d switch
    {
        Deprivation.Hungry => HungerSeverity(body),
        Deprivation.Thirsty => BelowThreshold(body.HydrationMl / Body.MaxHydrationMl),
        Deprivation.Tired => BelowThreshold(body.EnergyMinutes / Body.MaxEnergyMinutes),
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

    static double BelowThreshold(double pct) => Math.Max(0, (ThresholdPct - Math.Clamp(pct, 0, 1)) / ThresholdPct);

    /// Follows the reserves: an empty store is a pang; strong hunger rises over the last StarvingRampDays they can feed.
    static double HungerSeverity(BodyState body)
    {
        double pang = EmptyStoreHungerSeverity * BelowThreshold(body.CaloriesKcal / Body.CaloriesCapacityKcal(body));
        double reserves = Body.ReservesKcal(body);
        // Most bodies have far more than the ramp's worth: skip the metabolism's walk over every organ.
        if (reserves >= StarvingRampDays * Body.WholeOrgansBasalKcalPerDay(body)) return pang;
        return Math.Max(pang, Math.Clamp(1 - reserves / (StarvingRampDays * Body.MetabolismKcalPerDay(body, 1.0)), 0, 1));
    }

    /// Summed across the three per capacity, then bent toward MaxLoss: never a cliff, never a relief, never below about a fifth.
    internal static void AddCapacityDeltas(BodyState body, Span<double> deltas)
    {
        if (!FeelsAny(body)) return;
        Span<double> loss = stackalloc double[deltas.Length];
        foreach (var d in All)
        {
            double s = Severity(body, d);
            foreach (var (cap, delta) in LossesOf(d)) loss[(int)cap] -= delta * s;
        }
        for (int i = 0; i < deltas.Length; i++) if (loss[i] > 0) deltas[i] -= StackedLoss(loss[i]);
    }

    internal const double MaxLoss = 0.8;

    internal static double StackedLoss(double summedLoss) =>
        summedLoss / Math.Pow(1 + Math.Pow(summedLoss / MaxLoss, 4), 0.25);
}
