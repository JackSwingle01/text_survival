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
    /// Decided when the effect starts. Such effects fade to a floor, never away, until treated.
    public bool RequiresTreatment { get; init; }
}

public static class Effects
{
    const double Always = -1, Never = 2; // TreatmentAboveSeverity sentinels
    const double TreatmentFloorPct = 0.05;

    sealed record Def
    {
        public required double SeverityPerHour { get; init; }
        public double TreatmentAboveSeverity { get; init; } = Never;
        public double CoreTempFPerHour { get; init; }
        public double BleedMlPerHour { get; init; }
        public (Capacity Capacity, double Delta)[] Capacities { get; init; } = [];
    }

    static readonly Dictionary<EffectKind, Def> Defs = new()
    {
        [EffectKind.Hungry] = new() { SeverityPerHour = -1.0, Capacities = [(Capacity.Moving, -0.35), (Capacity.Manipulation, -0.35), (Capacity.Consciousness, -0.15)] },
        [EffectKind.Thirsty] = new() { SeverityPerHour = -1.0, Capacities = [(Capacity.Moving, -0.40), (Capacity.Manipulation, -0.25), (Capacity.Consciousness, -0.45)] },
        [EffectKind.Tired] = new()
        {
            SeverityPerHour = -1.0,
            Capacities = [(Capacity.Moving, -0.45), (Capacity.Manipulation, -0.30), (Capacity.Consciousness, -0.45), (Capacity.Sight, -0.25), (Capacity.Hearing, -0.15)],
        },
        [EffectKind.Shivering] = new() { SeverityPerHour = -2, CoreTempFPerHour = 3.0, Capacities = [(Capacity.Manipulation, -0.2)] },
        [EffectKind.Hypothermia] = new()
        {
            SeverityPerHour = -0.5,
            Capacities = [(Capacity.Moving, -0.3), (Capacity.Manipulation, -0.3), (Capacity.Consciousness, -0.5), (Capacity.BloodPumping, -0.2)],
        },
        [EffectKind.Frostbite] = new()
        {
            SeverityPerHour = -0.02, TreatmentAboveSeverity = Always,
            Capacities = [(Capacity.Manipulation, -0.5), (Capacity.Moving, -0.5), (Capacity.BloodPumping, -0.2)],
        },
        [EffectKind.Hyperthermia] = new() { SeverityPerHour = -0.5, Capacities = [(Capacity.Consciousness, -0.5), (Capacity.Moving, -0.3), (Capacity.BloodPumping, -0.2)] },
        // A signal only: the cooling and water it costs live in the heat balance, which can see the clothing.
        [EffectKind.Sweating] = new() { SeverityPerHour = -2 },
        // Severity IS the stored wetness; the survival tick sets it directly and owns drying.
        [EffectKind.Wet] = new() { SeverityPerHour = 0, CoreTempFPerHour = -1.0 },
        [EffectKind.Bloody] = new() { SeverityPerHour = -0.02, TreatmentAboveSeverity = Always },
        [EffectKind.Bleeding] = new() { SeverityPerHour = -0.1, BleedMlPerHour = 3000 },
        [EffectKind.Pain] = new()
        {
            SeverityPerHour = -0.15,
            Capacities = [(Capacity.Manipulation, -0.25), (Capacity.Consciousness, -0.15), (Capacity.Sight, -0.15), (Capacity.Hearing, -0.10)],
        },
        [EffectKind.Dazed] = new() { SeverityPerHour = -0.15, Capacities = [(Capacity.Sight, -0.4), (Capacity.Hearing, -0.3), (Capacity.Consciousness, -0.2)] },
        [EffectKind.Shock] = new()
        {
            SeverityPerHour = -0.05, TreatmentAboveSeverity = 0.5,
            Capacities = [(Capacity.Consciousness, -0.4), (Capacity.BloodPumping, -0.3), (Capacity.Moving, -0.2)],
        },
    };

    static Def DefOf(EffectKind kind) =>
        Defs.TryGetValue(kind, out var def) ? def : throw new KeyNotFoundException($"No definition for effect {kind}");

    public static double Severity(BodyState body, EffectKind kind) =>
        body.Effects.FirstOrDefault(e => e.Kind == kind)?.Severity ?? 0;

    public static bool Has(BodyState body, EffectKind kind) => body.Effects.Any(e => e.Kind == kind);

    /// Starts an effect or worsens it: the worse of old and new wins, except bleeding, where
    /// two wounds bleed more than one.
    public static void Add(BodyState body, EffectKind kind, double severity)
    {
        severity = Math.Clamp(severity, 0, 1);
        var existing = body.Effects.FirstOrDefault(e => e.Kind == kind);
        if (existing != null)
        {
            existing.Severity = kind == EffectKind.Bleeding
                ? Math.Min(existing.Severity + severity, 1.0)
                : Math.Max(existing.Severity, severity);
            return;
        }
        if (severity <= 0) return;
        body.Effects.Add(new BodyEffect { Kind = kind, Severity = severity, RequiresTreatment = severity > DefOf(kind).TreatmentAboveSeverity });
    }

    /// For effects whose severity is a level that rises and falls (wetness, blood on you). Zero removes.
    public static void Set(BodyState body, EffectKind kind, double severity)
    {
        severity = Math.Clamp(severity, 0, 1);
        var existing = body.Effects.FirstOrDefault(e => e.Kind == kind);
        if (existing == null) { Add(body, kind, severity); return; }
        if (severity <= 0) body.Effects.Remove(existing);
        else existing.Severity = severity;
    }

    /// Every effect drifts at its own hourly rate; ones that need treatment stop at a floor.
    public static void Decay(BodyState body, int minutes)
    {
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
