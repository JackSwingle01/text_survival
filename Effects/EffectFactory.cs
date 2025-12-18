using text_survival.Bodies;

namespace text_survival.Effects;

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
        HourlySeverityChange = -0.02,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.3),
            (CapacityNames.Manipulation, -0.3),
            (CapacityNames.Consciousness, -0.5),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = "You are getting dangerously cold...",
        RemovalMessage = "You are warming up, the hypothermia has passed."
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

    public static Effect MinorCut(double severity) => new()
    {
        EffectKind = "Cut",
        Source = "injury",
        Severity = severity,
        HourlySeverityChange = -0.05,
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.1)),
        ApplicationMessage = "You've cut yourself."
    };

    public static Effect Bruised(double severity) => new()
    {
        EffectKind = "Bruise",
        Source = "injury",
        Severity = severity,
        HourlySeverityChange = -0.02,
        ApplicationMessage = "You're going to have a nasty bruise."
    };

    public static Effect Fear(double severity) => new()
    {
        EffectKind = "Shaken",
        Source = "fear",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Fades relatively quickly
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.15)),
        ApplicationMessage = "Your hands are shaking."
    };

    public static Effect AnimalAttack(double severity) => new()
    {
        EffectKind = "Mauled",
        Source = "injury",
        Severity = severity,
        HourlySeverityChange = -0.02,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.25),
            (CapacityNames.Manipulation, -0.15)),
        ApplicationMessage = "You've been mauled. Blood runs down your arm."
    };

    private static CapacityModifierContainer Capacities(params (string name, double value)[] modifiers)
    {
        var container = new CapacityModifierContainer();
        foreach (var (name, value) in modifiers)
            container.SetCapacityModifier(name, value);
        return container;
    }
}