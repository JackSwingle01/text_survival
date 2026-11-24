namespace Survival;

/// What the body itself produces; event-only effects (fear, fever, sprains) arrive with events, as new rows.
public enum EffectKind
{
    Hungry, Thirsty, Tired,
    Shivering, Hypothermia, Frostbite, Hyperthermia, Sweating,
    Wet, Bloody,
    Bleeding, Pain, Dazed, Shock,
    // Appended: saves write the kind as its number.
    Asleep, WellFed,
}

/// An ongoing process on a body. Distinct from tissue damage: stopping a bleed does not mend the leg.
public sealed class BodyEffect
{
    public required EffectKind Kind { get; init; }
    public double Severity { get; set; }
    /// Latches once severity passes the definition's threshold, even by worsening; such effects fade to a floor, never away, until treated.
    public bool RequiresTreatment { get; set; }
}

/// One row of Content/effects.json; TreatmentAboveSeverity null means never needs treatment.
public sealed record EffectDef(double SeverityPerHour, double? TreatmentAboveSeverity, double CoreTempFPerHour, double BleedMlPerHour,
    IReadOnlyList<(Capacity Capacity, double Delta)> Capacities, bool Discomfort = false, double MetabolicHeatDelta = 0, double HealingDelta = 0,
    double? HungerFreeHours = null);

public static class Effects
{
    internal const double TreatmentFloorPct = 0.05;

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

    /// Summed capacity deltas, each scaled by severity, except that the discomforts' losses saturate
    /// first: hunger, thirst and tiredness wear a body down without leaving it helpless.
    public static IReadOnlyList<double> CapacityDeltas(BodyState body)
    {
        if (body.Effects.Count == 0) return NoDeltas;
        var deltas = new double[CapacityCount];
        CapacityDeltas(body, deltas);
        return deltas;
    }

    /// Fills deltas (zeroed, one per Capacity): what Body.GetCapacities reads for every body every minute, so on the caller's stack.
    internal static void CapacityDeltas(BodyState body, Span<double> deltas)
    {
        if (body.Effects.Count == 0) return;
        Span<double> discomfortLoss = stackalloc double[CapacityCount];
        foreach (var e in body.Effects)
        {
            var def = DefOf(e.Kind);
            for (int i = 0; i < def.Capacities.Count; i++)
            {
                var (cap, delta) = def.Capacities[i];
                if (def.Discomfort) discomfortLoss[(int)cap] -= delta * e.Severity;
                else deltas[(int)cap] += delta * e.Severity;
            }
        }
        for (int i = 0; i < CapacityCount; i++) deltas[i] -= DiscomfortLoss(discomfortLoss[i]);
    }

    static readonly int CapacityCount = Enum.GetValues<Capacity>().Length;
    static readonly double[] NoDeltas = new double[CapacityCount];

    internal const double DiscomfortMaxLoss = 0.8;

    /// The sum while it is small, bending smoothly toward DiscomfortMaxLoss: never a cliff, never a relief.
    internal static double DiscomfortLoss(double summedLoss) =>
        summedLoss / Math.Pow(1 + Math.Pow(summedLoss / DiscomfortMaxLoss, 4), 0.25);

    internal static double CapacityLoss(EffectKind kind, Capacity capacity) =>
        DefOf(kind).Capacities.Single(c => c.Capacity == capacity).Delta;

    /// Content checks the row carries it, so the ! cannot throw.
    internal static double WellFedAfterHours => DefOf(EffectKind.WellFed).HungerFreeHours!.Value;

    public static double CoreTempFPerHour(BodyState body) => body.Effects.Sum(e => DefOf(e.Kind).CoreTempFPerHour * e.Severity);

    public static double BleedMlPerHour(BodyState body) =>
        body.Effects.Sum(e => DefOf(e.Kind).BleedMlPerHour * e.Severity) * Math.Pow(body.WeightKg / Body.BaselineWeightKg, BloodFlowPerWeightExponent);

    const double BloodFlowPerWeightExponent = 0.75;

    /// A forecast: the bleeding rate after the hours given of Decay, nothing else changing.
    public static double BleedMlPerHourIn(BodyState body, double hours) =>
        body.Effects.Sum(e => DefOf(e.Kind).BleedMlPerHour * FadedSeverity(e, hours)) * Math.Pow(body.WeightKg / Body.BaselineWeightKg, BloodFlowPerWeightExponent);

    /// Infinity for bleeding a treatment floor holds (Decay's clamp, as FadedSeverity reads it), or that does not fade.
    public static double HoursUntilBleedingStops(BodyState body)
    {
        double hours = 0;
        foreach (var e in body.Effects.Where(e => DefOf(e.Kind).BleedMlPerHour > 0))
            hours = Math.Max(hours, e.RequiresTreatment || DefOf(e.Kind).SeverityPerHour >= 0 ? double.PositiveInfinity : e.Severity / -DefOf(e.Kind).SeverityPerHour);
        return hours;
    }

    static double FadedSeverity(BodyEffect e, double hours) =>
        Math.Clamp(e.Severity + DefOf(e.Kind).SeverityPerHour * hours, e.RequiresTreatment ? Math.Min(TreatmentFloorPct, e.Severity) : 0, 1);

    public static double MetabolicHeatFactor(BodyState body) => 1 + body.Effects.Sum(e => DefOf(e.Kind).MetabolicHeatDelta * e.Severity);

    public static double HealingFactor(BodyState body) => 1 + body.Effects.Sum(e => DefOf(e.Kind).HealingDelta * e.Severity);
}
