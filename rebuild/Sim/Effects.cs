namespace Survival;

/// The effects the body itself produces: survival thresholds, temperature, wetness, wounds.
/// Event-only effects (fear, fever, sprains...) arrive with the events port as new rows.
public enum EffectKind
{
    Hungry, Thirsty, Tired,
    Shivering, Hypothermia, Frostbite, Hyperthermia, Sweating,
    Wet, Bloody,
    Bleeding, Pain, Dazed, Shock,
}

/// An ongoing process on a body. Distinct from tissue damage: stopping a bleed does not mend the leg.
public sealed class BodyEffect
{
    public required EffectKind Kind { get; init; }
    public double Severity { get; set; }
    /// Set once the effect has been worse than its definition's threshold, whether it started there or
    /// was worsened there. Such effects fade to a floor, never away, until treated.
    public bool RequiresTreatment { get; set; }
}

/// One effect's definition (Content/effects.json): its hourly drift, whether it needs treatment once it
/// starts above a severity (null never), and what it does to the body at full severity.
public sealed record EffectDef(double SeverityPerHour, double? TreatmentAboveSeverity, double CoreTempFPerHour, double BleedMlPerHour,
    IReadOnlyList<(Capacity Capacity, double Delta)> Capacities);

public static class Effects
{
    internal const double TreatmentFloorPct = 0.05;

    /// Every effect's row (Content/effects.json).
    static readonly IReadOnlyDictionary<EffectKind, EffectDef> Defs = Content.Tables.Effects;

    static EffectDef DefOf(EffectKind kind) =>
        Defs.TryGetValue(kind, out var def) ? def : throw new KeyNotFoundException($"No definition for effect {kind}");

    public static double Severity(BodyState body, EffectKind kind) =>
        body.Effects.FirstOrDefault(e => e.Kind == kind)?.Severity ?? 0;

    public static bool Has(BodyState body, EffectKind kind) => body.Effects.Any(e => e.Kind == kind);

    static bool NeedsTreatment(EffectKind kind, double severity) =>
        DefOf(kind).TreatmentAboveSeverity is double above && severity > above;

    static double ClampSeverity(double severity) => double.IsNaN(severity)
        ? throw new ArgumentOutOfRangeException(nameof(severity), "Effect severity is NaN")
        : Math.Clamp(severity, 0, 1);

    /// Starts an effect or worsens it: the worse of old and new wins, except bleeding, where
    /// two wounds bleed more than one. Either way the order two injuries land in changes nothing.
    public static void Add(BodyState body, EffectKind kind, double severity)
    {
        severity = ClampSeverity(severity);
        var existing = body.Effects.FirstOrDefault(e => e.Kind == kind);
        if (existing != null)
        {
            existing.Severity = kind == EffectKind.Bleeding
                ? Math.Min(existing.Severity + severity, 1.0)
                : Math.Max(existing.Severity, severity);
            existing.RequiresTreatment |= NeedsTreatment(kind, existing.Severity);
            return;
        }
        if (severity <= 0) return;
        body.Effects.Add(new BodyEffect { Kind = kind, Severity = severity, RequiresTreatment = NeedsTreatment(kind, severity) });
    }

    /// For effects whose severity is a level that rises and falls (wetness, blood on you). Zero removes.
    public static void Set(BodyState body, EffectKind kind, double severity)
    {
        severity = ClampSeverity(severity);
        var existing = body.Effects.FirstOrDefault(e => e.Kind == kind);
        if (existing == null) { Add(body, kind, severity); return; }
        if (severity <= 0) body.Effects.Remove(existing);
        else
        {
            existing.Severity = severity;
            existing.RequiresTreatment |= NeedsTreatment(kind, severity);
        }
    }

    /// Every effect drifts at its own hourly rate; ones that need treatment stop at a floor.
    public static void Decay(BodyState body, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        foreach (var e in body.Effects)
        {
            // The floor stops decay; it never raises a faint effect up to it.
            double floor = e.RequiresTreatment ? Math.Min(TreatmentFloorPct, e.Severity) : 0;
            e.Severity = Math.Clamp(e.Severity + DefOf(e.Kind).SeverityPerHour / 60 * minutes, floor, 1);
        }
        body.Effects.RemoveAll(e => e.Severity <= 0);
    }

    /// Summed capacity deltas, indexed by Capacity, each scaled by severity.
    public static double[] CapacityDeltas(BodyState body)
    {
        var deltas = new double[Enum.GetValues<Capacity>().Length];
        foreach (var e in body.Effects)
            foreach (var (cap, delta) in DefOf(e.Kind).Capacities)
                deltas[(int)cap] += delta * e.Severity;
        return deltas;
    }

    public static double CoreTempFPerHour(BodyState body) => body.Effects.Sum(e => DefOf(e.Kind).CoreTempFPerHour * e.Severity);

    public static double BleedMlPerHour(BodyState body) => body.Effects.Sum(e => DefOf(e.Kind).BleedMlPerHour * e.Severity);
}
