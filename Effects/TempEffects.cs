// Temperature injury effect class 
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects;

public class TemperatureInjury : Effect
{
    public enum TemperatureInjuryType
    {
        Burn,
        Frostbite,
        Hypothermia,
        Hyperthermia
    }

    public TemperatureInjuryType InjuryType { get; }

    // Store environment data to use in OnUpdate
    private double _environmentalTemperature;

    public TemperatureInjury(TemperatureInjuryType type, string source, BodyPart bodyPart, double severity)
        : base(type.ToString(), source, bodyPart, severity, GetSeverityChangeRate(type))
    {
        InjuryType = type;
        RequiresTreatment = type == TemperatureInjuryType.Hypothermia || type == TemperatureInjuryType.Hyperthermia;

        ConfigureCapacityModifiers();
    }

    private static double GetSeverityChangeRate(TemperatureInjuryType type)
    {
        // Rate per hour, negative means healing
        return type switch
        {
            TemperatureInjuryType.Burn => -0.03,       // Heals slowly
            TemperatureInjuryType.Frostbite => -0.02,  // Heals very slowly
            TemperatureInjuryType.Hypothermia => -0.01, // Very slow recovery without treatment
            TemperatureInjuryType.Hyperthermia => -0.01, // Very slow recovery without treatment
            _ => -0.05
        };
    }

    protected override void OnUpdate(Actor target)
    {
        if (target is Player player)
        {
            // these effects shouldn't be on npcs yet 
            _environmentalTemperature = player.CurrentLocation.GetTemperature();
        }

        // Apply additional environmental effects to recovery rate
        // apply an additional base heal rate for every 20 degrees cooler/warmer per hour
        double bonusRecoveryPerDegreePerMinute = GetSeverityChangeRate(InjuryType) / 25.0 / 60.0; // should be negative
        if (bonusRecoveryPerDegreePerMinute < 0) // negative is healing
        {
            if (InjuryType == TemperatureInjuryType.Hypothermia && _environmentalTemperature > 65.0)
            {
                Output.WriteLine($"The heat is helping your {EffectKind} recover faster.");
                double recoveryBoost = (_environmentalTemperature - 65.0) * bonusRecoveryPerDegreePerMinute;
                UpdateSeverity(target, -recoveryBoost); // Negative to reduce severity
            }
            else if (InjuryType == TemperatureInjuryType.Hyperthermia && _environmentalTemperature < 70.0)
            {
                Output.WriteLine($"The cool is helping your {EffectKind} recover faster.");
                double recoveryBoost = (70.0 - _environmentalTemperature) * bonusRecoveryPerDegreePerMinute;
                UpdateSeverity(target, recoveryBoost); // Negative to reduce severity
            }
        }

        // Update capacity modifiers based on current severity
        ConfigureCapacityModifiers();
    }

    private void ConfigureCapacityModifiers()
    {
        CapacityModifiers.Clear();

        switch (InjuryType)
        {
            case TemperatureInjuryType.Burn:
                CapacityModifiers["Manipulation"] = -0.4 * Severity;
                CapacityModifiers["Moving"] = -0.1 * Severity;
                CapacityModifiers["BloodPumping"] = -0.1 * Severity; // Fluid loss
                break;

            case TemperatureInjuryType.Frostbite:
                CapacityModifiers["Manipulation"] = -0.5 * Severity;
                CapacityModifiers["Moving"] = -0.5 * Severity;
                CapacityModifiers["BloodPumping"] = -0.2 * Severity; // Reduced circulation
                break;

            case TemperatureInjuryType.Hypothermia:
                CapacityModifiers["Moving"] = -0.3 * Severity;
                CapacityModifiers["Manipulation"] = -0.3 * Severity;
                CapacityModifiers["Consciousness"] = -0.5 * Severity;
                CapacityModifiers["BloodPumping"] = -0.2 * Severity;
                break;

            case TemperatureInjuryType.Hyperthermia:
                CapacityModifiers["Consciousness"] = -0.5 * Severity;
                CapacityModifiers["Moving"] = -0.3 * Severity;
                CapacityModifiers["BloodPumping"] = -0.2 * Severity;
                CapacityModifiers["BloodFiltration"] = -0.2 * Severity; // Kidney strain
                break;
        }
    }

    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
    {
        // Update capacity modifiers when severity changes
        ConfigureCapacityModifiers();

        // Notify of significant changes
        if (Math.Abs(oldSeverity - updatedSeverity) > 0.3)
        {
            string direction = updatedSeverity > oldSeverity ? "worsening" : "improving";

            string message = InjuryType switch
            {
                TemperatureInjuryType.Burn => $"Your burn is {direction}.",
                TemperatureInjuryType.Frostbite => $"Your frostbite is {direction}.",
                TemperatureInjuryType.Hypothermia => $"Your hypothermia is {direction}.",
                TemperatureInjuryType.Hyperthermia => $"Your hyperthermia is {direction}.",
                _ => $"Your {EffectKind} is {direction}."
            };

            if (direction == "worsening")
                Output.WriteWarning(message);
            else
                Output.WriteLine(message);
        }
    }

    protected override void OnApply(Actor target)
    {
        // Notify player of the injury
        string message = InjuryType switch
        {
            TemperatureInjuryType.Burn => "Your skin burns from the heat!",
            TemperatureInjuryType.Frostbite => "Your extremities are beginning to freeze!",
            TemperatureInjuryType.Hypothermia => "Your body temperature is dangerously low.",
            TemperatureInjuryType.Hyperthermia => "Your body temperature is dangerously high.",
            _ => $"You've suffered {EffectKind}, a temperature-related injury."
        };

        Output.WriteWarning(message);
    }

    protected override void OnRemove(Actor target)
    {
        string message = InjuryType switch
        {
            TemperatureInjuryType.Burn => "Your burn has healed.",
            TemperatureInjuryType.Frostbite => "Your frostbite has healed.",
            TemperatureInjuryType.Hypothermia => "Your body temperature has returned to normal.",
            TemperatureInjuryType.Hyperthermia => "Your body temperature has returned to normal.",
            _ => $"Your {EffectKind} has resolved."
        };

        Output.WriteLine(message);
    }

    public override string Describe()
    {
        string severityDesc = GetSeverityDescription();
        string locationDesc = TargetBodyPart != null ? $" on {TargetBodyPart.Name}" : "";

        return $"{severityDesc} {InjuryType}{locationDesc}";
    }
}