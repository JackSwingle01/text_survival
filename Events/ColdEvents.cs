
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Events;

public class BodyColdEvent(Body target, bool isNew) : IGameEvent
{
    public Body Target = target;
    public bool IsNew = isNew;

}

public class BodyColdEventHandler : IEventHandler<BodyColdEvent>
{
    private const double ShiveringThreshold = 97.0; // °F
    private const double HypothermiaThreshold = 95.0;  // °F
    private const double SevereHypothermiaThreshold = 89.6; // °F
    public void Handle(BodyColdEvent evt)
    {
        var target = evt.Target;
        var bodyTemperature = target.BodyTemperature;


        if (evt.IsNew)
        {
            Output.WriteDanger($"{target.OwnerName} is cold!\n");
        }
        else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
        {
            Output.WriteWarning($"{target.OwnerName} is still cold.\n");
        }

        if (bodyTemperature < ShiveringThreshold)
        {
            ApplyShivering(target);
        }
        else
        {
            // Remove shivering effects when temperature normalizes
            target.EffectRegistry.RemoveEffectsByKind("Shivering");
        }

        if (bodyTemperature < HypothermiaThreshold)
        {
            ApplyHypothermia(target);
        }

        if (bodyTemperature < SevereHypothermiaThreshold)
        {
            ApplyFrostbite(target);
        }
    }


    private void ApplyShivering(Body target)
    {
        // Calculate severity based on temperature
        double intensity = (ShiveringThreshold - target.BodyTemperature) / 5.0;
        intensity = Math.Clamp(intensity, 0.01, 1.0);

        // Apply to whole body (will handle stacking through EffectRegistry)
        var shiveringEffect = new ShiveringEffect(intensity);

        target.EffectRegistry.AddEffect(shiveringEffect);
    }


    private void ApplyHypothermia(Body target)
    {
        // Calculate severity based on temperature
        double severity = Math.Clamp((HypothermiaThreshold - target.BodyTemperature) / 10.0, 0.01, 1.0);

        // Apply to whole body (will handle stacking through EffectRegistry)
        var hypothermia = new TemperatureInjury(
            TemperatureInjury.TemperatureInjuryType.Hypothermia,
            "Cold exposure",
            null,
            severity);

        target.EffectRegistry.AddEffect(hypothermia);

    }


    private void ApplyFrostbite(Body target)
    {
        // Get extremities (hands and feet)
        var extremities = target.GetAllParts()
            .Where(p => p.Name.Contains("Hand") || p.Name.Contains("Foot"))
            .ToList();

        foreach (var extremity in extremities)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((SevereHypothermiaThreshold - target.BodyTemperature) / 5.0, 0.01, 1.0);

            // Apply frostbite to extremity (will handle stacking through EffectRegistry)
            var frostbite = new TemperatureInjury(
                TemperatureInjury.TemperatureInjuryType.Frostbite,
                "Extreme cold",
                extremity,
                severity);

            target.EffectRegistry.AddEffect(frostbite);
        }
    }

}