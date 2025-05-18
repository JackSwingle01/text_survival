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
        : base(GetEffectKind(type), source, bodyPart, severity, GetSeverityChangeRate(type))
    {
        InjuryType = type;
        RequiresTreatment = (type == TemperatureInjuryType.Hypothermia || type == TemperatureInjuryType.Hyperthermia);

        // Configure capacity modifiers based on injury type and severity
        ConfigureCapacityModifiers();
    }

    private static string GetEffectKind(TemperatureInjuryType type)
    {
        return type.ToString();
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

    // Method to let the temperature module inform this effect about the environmental temperature
    public void SetEnvironmentalTemperature(double temperature)
    {
        _environmentalTemperature = temperature;
    }

    protected override void OnUpdate(Actor target)
    {
        // Let the base class handle the regular severity change first
        base.OnUpdate(target);

        // Apply additional environmental effects to recovery rate
        if (InjuryType == TemperatureInjuryType.Hypothermia && _environmentalTemperature > 75.0)
        {
            double recoveryBoost = (_environmentalTemperature - 75.0) / 25.0 * 0.05 / 60.0; // Per minute
            UpdateSeverity(target, -recoveryBoost); // Negative to reduce severity
        }
        else if (InjuryType == TemperatureInjuryType.Hyperthermia && _environmentalTemperature < 75.0)
        {
            double recoveryBoost = (75.0 - _environmentalTemperature) / 25.0 * 0.05 / 60.0; // Per minute
            UpdateSeverity(target, -recoveryBoost); // Negative to reduce severity
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
                _ => $"Your condition is {direction}."
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
            _ => "You've suffered a temperature-related injury."
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
            _ => "Your condition has resolved."
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