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
        EffectKind = "Fear",
        Source = "fear",
        Severity = severity,
        HourlySeverityChange = -0.1,  // Fades relatively quickly
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.15)),
        ApplicationMessage = "Fear grips you."
    };

    /// <summary>
    /// Shaken - milder psychological effect from unsettling events.
    /// Less severe than Fear, fades faster.
    /// </summary>
    public static Effect Shaken(double severity) => new()
    {
        EffectKind = "Shaken",
        Source = "fear",
        Severity = severity,
        HourlySeverityChange = -0.2,  // Fades faster than Fear
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.1)),
        ApplicationMessage = "Your hands are trembling."
    };

    /// <summary>
    /// Exhausted - from prolonged exertion or stress.
    /// Reduces movement and manipulation capacity.
    /// </summary>
    public static Effect Exhausted(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Exhausted",
        Source = "exertion",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.2),
            (CapacityNames.Manipulation, -0.1)),
        ApplicationMessage = "You're exhausted."
    };

    /// <summary>
    /// Nauseous - from bad food, dehydration, or stress.
    /// Affects digestion and consciousness.
    /// </summary>
    public static Effect Nauseous(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Nauseous",
        Source = "sickness",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Digestion, -0.3),
            (CapacityNames.Consciousness, -0.1)),
        ApplicationMessage = "You feel sick to your stomach."
    };

    /// <summary>
    /// Coughing - from smoke inhalation or illness.
    /// Affects breathing capacity.
    /// </summary>
    public static Effect Coughing(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Coughing",
        Source = "smoke",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Breathing, -0.25)),
        ApplicationMessage = "You can't stop coughing."
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

    /// <summary>
    /// Pain - triggered automatically by physical damage.
    /// Reduces Manipulation, Consciousness, and Perception. Fades naturally.
    /// </summary>
    public static Effect Pain(double severity) => new()
    {
        EffectKind = "Pain",
        Source = "injury",
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

    // === SURVIVAL STAT EFFECTS ===

    /// <summary>
    /// Hungry - triggered when calories drop below 30%.
    /// At full severity (0% calories): -35% Moving, -35% Manipulation, -15% Consciousness
    /// </summary>
    public static Effect Hungry(double severity) => new()
    {
        EffectKind = "Hungry",
        Source = "survival",
        Severity = severity,
        HourlySeverityChange = -1.0,  // Decays quickly when not refreshed
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.35),
            (CapacityNames.Manipulation, -0.35),
            (CapacityNames.Consciousness, -0.15)),
        ApplicationMessage = "Your stomach growls. You're getting hungry.",
        RemovalMessage = "You feel better after eating."
    };

    /// <summary>
    /// Thirsty - triggered when hydration drops below 30%.
    /// At full severity (0% hydration): -40% Moving, -25% Manipulation, -45% Consciousness
    /// </summary>
    public static Effect Thirsty(double severity) => new()
    {
        EffectKind = "Thirsty",
        Source = "survival",
        Severity = severity,
        HourlySeverityChange = -1.0,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, -0.40),
            (CapacityNames.Manipulation, -0.25),
            (CapacityNames.Consciousness, -0.45)),
        ApplicationMessage = "Your mouth is dry. You need water.",
        RemovalMessage = "Your thirst is quenched."
    };

    /// <summary>
    /// Tired - triggered when energy drops below 30%.
    /// At full severity (0% energy): -45% Moving, -30% Manipulation, -45% Consciousness, reduced Perception
    /// </summary>
    public static Effect Tired(double severity) => new()
    {
        EffectKind = "Tired",
        Source = "survival",
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

    /// <summary>
    /// Sore - minor muscle pain from exertion.
    /// Mild movement penalty, fades relatively quickly.
    /// </summary>
    public static Effect Sore(double severity, int durationMinutes = 120) => new()
    {
        EffectKind = "Sore",
        Source = "exertion",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Moving, -0.1)),
        ApplicationMessage = "Your muscles ache."
    };

    /// <summary>
    /// Paranoid - heightened psychological stress.
    /// More severe and longer-lasting than Fear.
    /// </summary>
    public static Effect Paranoid(double severity) => new()
    {
        EffectKind = "Paranoid",
        Source = "fear",
        Severity = severity,
        HourlySeverityChange = -0.05,  // Fades slower than Fear
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.2),
            (CapacityNames.Consciousness, -0.1)),
        ApplicationMessage = "You can't shake the feeling you're being watched."
    };

    // === POSITIVE EFFECTS (BUFFS) ===

    /// <summary>
    /// Warmed - positive temperature effect from fire or shelter.
    /// Helps maintain body temperature.
    /// </summary>
    public static Effect Warmed(double severity, int durationMinutes = 30) => new()
    {
        EffectKind = "Warmed",
        Source = "comfort",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        StatsDelta = new() { TemperatureDelta = 2.0 / 60.0 },  // +2 degrees per hour
        ApplicationMessage = "You feel warm and comfortable."
    };

    /// <summary>
    /// Rested - positive effect from adequate sleep.
    /// Reduces fatigue penalties.
    /// </summary>
    public static Effect Rested(double severity, int durationMinutes = 120) => new()
    {
        EffectKind = "Rested",
        Source = "comfort",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, 0.1),
            (CapacityNames.Manipulation, 0.05)),
        ApplicationMessage = "You feel well-rested."
    };

    /// <summary>
    /// Focused - mental clarity from successful actions.
    /// Improves manipulation and consciousness.
    /// </summary>
    public static Effect Focused(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Focused",
        Source = "morale",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, 0.15),
            (CapacityNames.Consciousness, 0.1)),
        ApplicationMessage = "Your mind is sharp and focused."
    };

    /// <summary>
    /// Hardened - toughened from surviving hardship.
    /// Minor bonus to all physical capacities.
    /// </summary>
    public static Effect Hardened(double severity, int durationMinutes = 180) => new()
    {
        EffectKind = "Hardened",
        Source = "experience",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities(
            (CapacityNames.Moving, 0.05),
            (CapacityNames.Manipulation, 0.05),
            (CapacityNames.Breathing, 0.05)),
        ApplicationMessage = "You feel toughened by your experiences."
    };

    /// <summary>
    /// Burn - tissue damage from heat.
    /// Causes pain and manipulation penalty. Heals slowly.
    /// </summary>
    public static Effect Burn(double severity, int durationMinutes = 180) => new()
    {
        EffectKind = "Burn",
        Source = "heat",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        RequiresTreatment = severity > 0.3,  // Severe burns need treatment
        CapacityModifiers = Capacities(
            (CapacityNames.Manipulation, -0.2)),
        ApplicationMessage = "You've been burned.",
        RemovalMessage = "The burn has healed."
    };

    /// <summary>
    /// Stiff - joint and muscle stiffness from cold, strain, or old injuries.
    /// Reduces movement speed. Common in prolonged cold exposure.
    /// </summary>
    public static Effect Stiff(double severity, int durationMinutes = 360) => new()
    {
        EffectKind = "Stiff",
        Source = "strain",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Moving, -0.25)),
        ApplicationMessage = "Your joints ache and your muscles are stiff.",
        RemovalMessage = "The stiffness has faded."
    };

    /// <summary>
    /// Fever - systemic infection response.
    /// Severe penalties to consciousness, movement, and manipulation.
    /// Requires treatment; can be fatal if untreated.
    /// </summary>
    public static Effect Fever(double severity) => new()
    {
        EffectKind = "Fever",
        Source = "infection",
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

    /// <summary>
    /// Clumsy - reduced fine motor control from cold, shaking, or other impairment.
    /// Affects manipulation capacity.
    /// </summary>
    public static Effect Clumsy(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Clumsy",
        Source = "impairment",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        CapacityModifiers = Capacities((CapacityNames.Manipulation, -0.3)),
        ApplicationMessage = "Your hands are clumsy and uncoordinated."
    };

    /// <summary>
    /// Dazed - head trauma causing disorientation and sensory impairment.
    /// Triggered by blunt damage to head or severe impacts (falls, accidents).
    /// Reduces perception (sight/hearing) and consciousness.
    /// </summary>
    public static Effect Dazed(double severity) => new()
    {
        EffectKind = "Dazed",
        Source = "concussion",
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

    /// <summary>
    /// Wet - soaked clothing and skin multiplies cold effects.
    /// Makes you more vulnerable to hypothermia and cold damage.
    /// Severity determines the multiplier: (1 + severity) applied to cold effects.
    /// </summary>
    public static Effect Wet(double severity, int durationMinutes = 60) => new()
    {
        EffectKind = "Wet",
        Source = "environmental",
        Severity = severity,
        HourlySeverityChange = -60.0 / durationMinutes,
        StatsDelta = new() { TemperatureDelta = -1.0 / 60.0 },  // Mild direct cooling
        ApplicationMessage = "You're soaked through. The cold cuts deeper.",
        RemovalMessage = "You've dried off."
    };

    private static CapacityModifierContainer Capacities(params (string name, double value)[] modifiers)
    {
        var container = new CapacityModifierContainer();
        foreach (var (name, value) in modifiers)
            container.SetCapacityModifier(name, value);
        return container;
    }
}