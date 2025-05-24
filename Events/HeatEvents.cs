using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Events;

public class BodyHotEvent(Body target, bool isNew) : IGameEvent
{
    public Body Target = target;
    public bool IsNew = isNew;
}


public class BodyHotEventHandler : IEventHandler<BodyHotEvent>
{
    private const double SweatingThreshold = 100.0; // °F
    private const double HyperthermiaThreshold = 99.5; // °F  
    public void Handle(BodyHotEvent evt)
    {
        var target = evt.Target;
        var bodyTemperature = target.BodyTemperature;


        if (evt.IsNew)
        {
            Output.WriteDanger($"{target.OwnerName} is hot!\n");
        }
        else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
        {
            Output.WriteWarning($"{target.OwnerName} is still hot.\n");
        }

        if (bodyTemperature > SweatingThreshold)
        {
            ApplySweating(target); // naturally resolves if not refreshed in 30 min
        }

        if (bodyTemperature > HyperthermiaThreshold)
        {
            ApplyHyperthermia(target);
        }
    }

    private void ApplyHyperthermia(Body target)
    {
        // Calculate severity based on temperature
        double severity = Math.Clamp((target.BodyTemperature - HyperthermiaThreshold) / 10.0, 0.01, 1.00);

        // Apply to whole body (will handle stacking through EffectRegistry)
        var hyperthermia = new TemperatureInjury(
            TemperatureInjury.TemperatureInjuryType.Hyperthermia,
            "Heat exposure",
            null,
            severity);

        target.EffectRegistry.AddEffect(hyperthermia);
    }

    private void ApplySweating(Body target)
    {
        // Calculate severity based on temperature
        double severity = Math.Clamp((target.BodyTemperature - SweatingThreshold) / 4.0, 0.10, 1.00);

        // Apply to whole body (will handle stacking through EffectRegistry)
        var sweatingEffect = new SweatingEffect(severity);

        target.EffectRegistry.AddEffect(sweatingEffect);
    }

}