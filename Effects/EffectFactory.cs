using text_survival.Bodies;

namespace text_survival.Effects;

/// <summary>
/// Effects represent ongoing PROCESSES, not structural damage.
/// - Body.Damage() for physical injuries (cuts, bruises, mauling)
/// - Effects for processes (bleeding, infection, temperature conditions, sprains)
///
/// Structural injuries (cuts, bruises, mauling) are body part damage, not effects.
/// Effects are things that happen over time: bleeding, frostbite, hypothermia, fear.
/// </summary>
public static class EffectFactory
{
    public static Effect Cold(double degreesPerHour, int durationMinutes) => new()
    {
        EffectKind = "Cold",
        Source = "environment",
        HourlySeverityChange = -60.0 / durationMinutes,
        StatsDelta = new() { TemperatureDelta = degreesPerHour / 60.0 }
    };

    public static Effect Hyperthermia(double severity) => new()
    {
        EffectKind = "Hyperthermia",
        Source = "temperature",
        Severity = severity,
        HourlySeverityChange = -0.01,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Consciousness, -0.5),
            (CapacityNames.Moving, -0.3),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = "You are overheating!"
    };

    public static Effect Sweating(double severity) => new()
    {
        EffectKind = "Sweating",
        Source = "temperature",
        Severity = severity,
        StatsDelta = new() { HydrationDelta = -1000.0 / 60.0 }
    };

    public static Effect Shivering(double intensity) => new()
    {
        EffectKind = "Shivering",
        Source = "temperature",
        Severity = intensity,
        HourlySeverityChange = -2,
        StatsDelta = new() { TemperatureDelta = 3.0 / 60.0 },
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.2))
    };

    public static Effect Hypothermia(double severity) => new()
    {
        EffectKind = "Hypothermia",
        Source = "temperature",
        Severity = severity,
        HourlySeverityChange = -0.5,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.3),
            (CapacityNames.Manipulation, -0.3),
            (CapacityNames.Consciousness, -0.5),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = "You are getting dangerously cold...",
        RemovalMessage = "You are warming up, the hypothermia has passed.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.33, "The cold is getting dangerous. Your movements are sluggish.", true),
            new Effect.ThresholdMessage(0.67, "Severe hypothermia setting in. You need warmth NOW.", true),
        ]
    };

    public static Effect Frostbite(string bodyPart, double severity) => new()
    {
        EffectKind = "Frostbite",
        Source = "temperature",
        TargetBodyPart = bodyPart,
        Severity = severity,
        HourlySeverityChange = -0.02,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.5),
            (CapacityNames.Moving, -0.5),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = $"Your {bodyPart.ToLower()} is developing frostbite!",
        RemovalMessage = $"The feeling is returning to your {bodyPart.ToLower()}."
    };

    /// <summary>
    /// Consolidated frostbite effect for extremities (no specific body part).
    /// Uses escalating threshold messages instead of 4 separate effects.
    /// </summary>
    public static Effect Frostbite(double severity) => new()
    {
        EffectKind = "Frostbite",
        Source = "temperature",
        Severity = severity,
        HourlySeverityChange = -0.02,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.5),
            (CapacityNames.Moving, -0.5),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = "Your extremities are going numb from the cold.",
        RemovalMessage = "Feeling is returning to your extremities.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.33, "Your fingers and toes are turning white.", true),
            new Effect.ThresholdMessage(0.67, "Severe frostbite - tissue damage is spreading!", true),
        ]
    };

    public static Effect SprainedAnkle(double severity) => new()
    {
        EffectKind = "Sprained Ankle",
        Source = "injury",
        Severity = severity,
        HourlySeverityChange = -0.01,
        RequiresTreatment = true,
        CapacityModifiers = Capacities((CapacityNames.Moving, -0.4)),
        ApplicationMessage = "You've twisted your ankle.",
        RemovalMessage = "Your ankle feels stable again."
    };

    // MinorCut and Bruised removed - these are now body part damage, not effects.
    // Use Body.Damage() with DamageType.Sharp or DamageType.Blunt instead.

    public static Effect Fear(double severity) => new()
    {
        EffectKind = "Shaken",
        Source = "fear",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Fades relatively quickly
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.15)),
        ApplicationMessage = "Your hands are shaking."
    };

    // AnimalAttack (Mauled) removed - this is now body part damage.
    // Use Body.Damage() with DamageType.Sharp and high damage amount.
    // Bleeding is triggered automatically from the damage if skin is broken.

    /// <summary>
    /// Bleeding effect - triggered automatically by sharp/pierce damage to skin.
    /// Drains Blood via DamageType.Bleed at 3000ml/hour at full severity (~50 min to death).
    /// </summary>
    public static Effect Bleeding(double severity) => new()
    {
        EffectKind = "Bleeding",
        Source = "wound",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Decays slowly; minor wounds stabilize before death
        RequiresTreatment = true,     // Stops at 0.05 floor until treated
        Damage = new(3000, DamageType.Bleed),  // 3000 ml/hour at severity 1.0
        TargetBodyPart = "Blood",
        ApplicationMessage = "You're bleeding.",
        RemovalMessage = "The bleeding has stopped."
    };

    private static CapacityModifierContainer Capacities(params (string name, double value)[] modifiers)
    {
        var container = new CapacityModifierContainer();
        foreach (var (name, value) in modifiers)
            container.SetCapacityModifier(name, value);
        return container;
    }
}