using text_survival.Bodies;

namespace text_survival.Effects;

// Use Body.Damage() for structural injuries, EffectFactory for ongoing processes.
public static class EffectFactory
{
    public static Effect Cold(double degreesPerHour, int durationMinutes) => new()
    {
        EffectKind = "Cold",
        HourlySeverityChange = -60.0 / durationMinutes,
        StatsDelta = new() { TemperatureDelta = degreesPerHour / 60.0 }
    };

    public static Effect Hyperthermia(double severity) => new()
    {
        EffectKind = "Hyperthermia",
        Severity = severity,
        HourlySeverityChange = -0.5,
        CapacityModifiers = Capacities(
            (CapacityNames.Consciousness, -0.5),
            (CapacityNames.Moving, -0.3),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = "You are overheating!",
        RemovalMessage = "You have cooled down, the overheating has passed."
    };

    public static Effect Sweating(double severity) => new()
    {
        EffectKind = "Sweating",
        Severity = severity,
        StatsDelta = new() { HydrationDelta = -1000.0 / 60.0 }
    };

    public static Effect Shivering(double intensity) => new()
    {
        EffectKind = "Shivering",
        Severity = intensity,
        HourlySeverityChange = -2,
        StatsDelta = new() { TemperatureDelta = 3.0 / 60.0 },
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.2))
    };

    public static Effect Hypothermia(double severity) => new()
    {
        EffectKind = "Hypothermia",
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

    public static Effect Frostbite(BodyTarget bodyPart, double severity) => new()
    {
        EffectKind = "Frostbite",
        TargetBodyPart = bodyPart,
        Severity = severity,
        HourlySeverityChange = -0.02,
        RequiresTreatment = true,
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.5),
            (CapacityNames.Moving, -0.5),
            (CapacityNames.BloodPumping, -0.2)),
        ApplicationMessage = $"Your {bodyPart.ToString().ToLower()} is developing frostbite!",
        RemovalMessage = $"The feeling is returning to your {bodyPart.ToString().ToLower()}."
    };

    public static Effect Frostbite(double severity) => new()
    {
        EffectKind = "Frostbite",
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
        EffectKind = "Fear",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Fades relatively quickly
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.15)),
        ApplicationMessage = "Fear grips you."
    };

    public static Effect Shaken(double severity) => new()
    {
        EffectKind = "Shaken",
        Severity = severity,
        HourlySeverityChange = -0.2,  // Fades faster than Fear
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.1)),
        ApplicationMessage = "Your hands are trembling."
    };

    public static Effect Exhausted(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Exhausted",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.2),
            (CapacityNames.Manipulation, -0.1)),
        ApplicationMessage = "You're exhausted."
    };

    public static Effect Nauseous(double severity, int durationMinutes = 60, bool fromContamination = false) => new()
    {
        EffectKind = "Nauseous",
        Severity = severity,
        // Contamination-based nausea worsens without treatment; mild nausea fades
        HourlySeverityChange = fromContamination ? 0.05 : -60.0 / durationMinutes,
        RequiresTreatment = fromContamination || severity > 0.5,
        // High severity causes hydration loss from vomiting/cramping
        StatsDelta = new() { HydrationDelta = severity > 0.5 ? -300.0 / 60.0 : 0 },  // 300ml/hour at severe
        CapacityModifiers = Capacities(
            (CapacityNames.Digestion, -0.3),
            (CapacityNames.Consciousness, -0.1),
            (CapacityNames.Moving, -0.15)),
        ApplicationMessage = fromContamination
            ? "Your stomach lurches. Something you ate is fighting back."
            : "You feel sick to your stomach.",
        RemovalMessage = "The nausea has passed.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.5, "The nausea is getting worse. Your stomach cramps.", true),
            new Effect.ThresholdMessage(0.7, "You can barely keep anything down. You need to rest.", true),
        ]
    };

    public static Effect GutSickness(double severity) => Nauseous(severity, fromContamination: true);

    public static Effect Coughing(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Coughing",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Breathing, -0.25)),
        ApplicationMessage = "You can't stop coughing."
    };

    public static Effect Bleeding(double severity) => new()
    {
        EffectKind = "Bleeding",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Decays slowly; minor wounds stabilize before death
        RequiresTreatment = true,     // Stops at 0.05 floor until treated
        Damage = new(3000, DamageType.Bleed),  // 3000 ml/hour at severity 1.0
        TargetBodyPart = BodyTarget.Blood,
        ApplicationMessage = "You're bleeding.",
        RemovalMessage = "The bleeding has stopped."
    };

    public static Effect Pain(double severity) => new()
    {
        EffectKind = "Pain",
        Severity = severity,
        HourlySeverityChange = -0.15,  // Fades in ~5-6 hours
        RequiresTreatment = false,     // Pain fades naturally
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.25),
            (CapacityNames.Consciousness, -0.15),
            (CapacityNames.Sight, -0.15),      // Pain makes it hard to focus
            (CapacityNames.Hearing, -0.10)),   // Throbbing distracts from sounds
        ApplicationMessage = "You're in pain.",
        RemovalMessage = "The pain has subsided.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.5, "The pain is intense, making it hard to focus.", true),
            new Effect.ThresholdMessage(0.75, "Agonizing pain threatens to overwhelm you.", true),
        ]
    };

    public static Effect Hungry(double severity) => new()
    {
        EffectKind = "Hungry",
        Severity = severity,
        HourlySeverityChange = -1.0,  // Decays quickly when not refreshed
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.35),
            (CapacityNames.Manipulation, -0.35),
            (CapacityNames.Consciousness, -0.15)),
        ApplicationMessage = "Your stomach growls. You're getting hungry.",
        RemovalMessage = "You feel better after eating."
    };

    public static Effect Thirsty(double severity) => new()
    {
        EffectKind = "Thirsty",
        Severity = severity,
        HourlySeverityChange = -1.0,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.40),
            (CapacityNames.Manipulation, -0.25),
            (CapacityNames.Consciousness, -0.45)),
        ApplicationMessage = "Your mouth is dry. You need water.",
        RemovalMessage = "Your thirst is quenched."
    };

    public static Effect Tired(double severity) => new()
    {
        EffectKind = "Tired",
        Severity = severity,
        HourlySeverityChange = -1.0,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.45),
            (CapacityNames.Manipulation, -0.30),
            (CapacityNames.Consciousness, -0.45),
            (CapacityNames.Sight, -0.25),      // Droopy eyes, hard to focus
            (CapacityNames.Hearing, -0.15)),   // Drowsy, less alert to sounds
        ApplicationMessage = "You're getting tired. Your eyelids feel heavy.",
        RemovalMessage = "You feel refreshed after resting."
    };

    public static Effect Sore(double severity, int durationMinutes = 120) => new()
    {
        EffectKind = "Sore",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Moving, -0.1)),
        ApplicationMessage = "Your muscles ache."
    };

    public static Effect Paranoid(double severity) => new()
    {
        EffectKind = "Paranoid",
        Severity = severity,
        HourlySeverityChange = -0.05,  // Fades slower than Fear
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.2),
            (CapacityNames.Consciousness, -0.1)),
        ApplicationMessage = "You can't shake the feeling you're being watched."
    };

    public static Effect Warmed(double severity, int durationMinutes = 30) => new()
    {
        EffectKind = "Warmed",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        StatsDelta = new() { TemperatureDelta = 2.0 / 60.0 },  // +2 degrees per hour
        ApplicationMessage = "You feel warm and comfortable."
    };

    public static Effect Rested(double severity, int durationMinutes = 120) => new()
    {
        EffectKind = "Rested",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, 0.2),
            (CapacityNames.Manipulation, 0.1)),
        ApplicationMessage = "You feel well-rested."
    };

    public static Effect Focused(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Focused",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, 0.15),
            (CapacityNames.Consciousness, 0.1)),
        ApplicationMessage = "Your mind is sharp and focused."
    };

    public static Effect Hardened(double severity, int durationMinutes = 360) => new()
    {
        EffectKind = "Hardened",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, 0.05),
            (CapacityNames.Manipulation, 0.05),
            (CapacityNames.Breathing, 0.05)),
        ApplicationMessage = "You feel toughened by your experiences."
    };

    public static Effect Energized(double severity, int durationMinutes = 20) => new()
    {
        EffectKind = "Energized",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, 0.1),
            (CapacityNames.Consciousness, 0.1)),
        ApplicationMessage = "Sugar energy courses through you."
    };

    public static Effect Nourished(double severity = 1.0, int durationMinutes = 180) => new()
    {
        EffectKind = "Nourished",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        HealingMultiplier = 1.5,  // +50% healing rate
        ApplicationMessage = "You feel nourished and healthy.",
        RemovalMessage = "The nourishing warmth fades."
    };

    public static Effect Burn(double severity, int durationMinutes = 180) => new()
    {
        EffectKind = "Burn",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        RequiresTreatment = severity > 0.3,  // Severe burns need treatment
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.2)),
        ApplicationMessage = "You've been burned.",
        RemovalMessage = "The burn has healed."
    };

    public static Effect Stiff(double severity, int durationMinutes = 360) => new()
    {
        EffectKind = "Stiff",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Moving, -0.25)),
        ApplicationMessage = "Your joints ache and your muscles are stiff.",
        RemovalMessage = "The stiffness has faded."
    };

    public static Effect Inflamed(double severity, int durationMinutes = 480) => new()
    {
        EffectKind = "Inflamed",
        Severity = severity,
        // Slow natural decay OR can escalate to Fever if untreated in cold/dirty conditions
        HourlySeverityChange = -60.0 / durationMinutes,
        RequiresTreatment = severity > 0.4,  // Moderate inflammation needs treatment
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.1),
            (CapacityNames.Moving, -0.05)),
        ApplicationMessage = "The wound is hot to the touch. Red lines spreading from the edges.",
        RemovalMessage = "The inflammation has subsided.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.5, "The wound is getting worse. Swelling and heat.", true),
            new Effect.ThresholdMessage(0.7, "Serious infection. Fever may follow if untreated.", true),
        ]
    };

    public static Effect Fever(double severity) => new()
    {
        EffectKind = "Fever",
        Severity = severity,
        HourlySeverityChange = 0.02,  // Slowly worsens without treatment
        RequiresTreatment = true,
        StatsDelta = new() { HydrationDelta = -500.0 / 60.0 },  // 500ml/hour from fever sweating
        CapacityModifiers = Capacities(
            (CapacityNames.Consciousness, -0.4),
            (CapacityNames.Moving, -0.3),
            (CapacityNames.Manipulation, -0.25)),
        ApplicationMessage = "You're burning up with fever.",
        RemovalMessage = "The fever has broken.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.5, "The fever is getting worse. You feel delirious.", true),
            new Effect.ThresholdMessage(0.8, "Critical fever. Your body is failing.", true),
        ]
    };

    public static Effect Clumsy(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Clumsy",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.3)),
        ApplicationMessage = "Your hands are clumsy and uncoordinated."
    };

    public static Effect Dazed(double severity) => new()
    {
        EffectKind = "Dazed",
        Severity = severity,
        HourlySeverityChange = -0.15,  // ~6-7 hours to clear
        CapacityModifiers = Capacities(
            (CapacityNames.Sight, -0.4),
            (CapacityNames.Hearing, -0.3),
            (CapacityNames.Consciousness, -0.2)),
        ApplicationMessage = "Your vision swims. Everything seems distant.",
        RemovalMessage = "Your head finally clears.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.5, "The ringing in your ears is deafening.", true),
        ]
    };

    public static Effect Wet(double severity) => new()
    {
        EffectKind = "Wet",
        Severity = severity,
        HourlySeverityChange = 0, // Drying handled by SurvivalProcessor
        StatsDelta = new() { TemperatureDelta = -1.0 / 60.0 },  // Mild direct cooling
        ApplicationMessage = "You're getting wet. Your clothing clings to your skin.",
        RemovalMessage = "You've dried off.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.3, "Your clothes are damp. The cold seeps in.", true),
            new Effect.ThresholdMessage(0.6, "You're soaked through. The cold cuts deep.", true),
            new Effect.ThresholdMessage(0.9, "You're completely drenched. Every movement feels ice-cold.", true),
        ]
    };

    public static Effect Bloody(double severity) => new()
    {
        EffectKind = "Bloody",
        Severity = severity,
        HourlySeverityChange = -0.02,  // Dries very slowly
        RequiresTreatment = true,       // Only washing removes it properly
        ApplicationMessage = "Blood covers you.",
        RemovalMessage = "You've cleaned off the blood.",
        ThresholdMessages = [
            new Effect.ThresholdMessage(0.15, "Blood has stained your clothing.", true),
            new Effect.ThresholdMessage(0.35, "You're covered in blood. Predators will smell this.", true),
            new Effect.ThresholdMessage(0.60, "Blood saturates your clothes. Everything reeks of it.", true),
        ]
    };

    private static CapacityModifierContainer Capacities(params (string name, double value)[] modifiers)
    {
        var container = new CapacityModifierContainer();
        foreach (var (name, value) in modifiers)
            container.SetCapacityModifier(name, value);
        return container;
    }

    public static Effect? Create(string effectKind, double severity = 1.0)
    {
        return effectKind.ToLower() switch
        {
            "nourished" => Nourished(severity),
            "warmed" => Warmed(severity),
            "rested" => Rested(severity),
            "focused" => Focused(severity),
            "hardened" => Hardened(severity),
            _ => null
        };
    }
}