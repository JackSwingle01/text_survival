
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

        string applicationMessage;
        string removalMessage;
        if (target.IsPlayer)
        {
            applicationMessage = $"Your core is getting very cold, you feel like you're starting to get hypothermia... Severity = {severity}";
            removalMessage = $"You're warming up enough and starting to feel better, the hypothermia has passed...";
        }
        else
        {
            applicationMessage = $"DEBUG: {target} has hypothermia. Severity = {severity}";
            removalMessage = $"DEBUG: {target} no longer has hypothermia.";
        }
        // Apply to whole body (will handle stacking through EffectRegistry)
        var hypothermia = EffectBuilderExtensions
            .CreateEffect("Hypothermia")
            .Temperature(TemperatureType.Hypothermia)
            .WithApplyMessage(applicationMessage)
            .WithSeverity(severity)
            .AllowMultiple(false)
            .WithRemoveMessage(removalMessage)
            .Build();

        target.EffectRegistry.AddEffect(hypothermia);

    }

    private void ApplyFrostbite(Body target)
    {
        // Get extremities (hands and feet)
        var extremities = target.Parts
            .Where(p => p.Name.Contains("Arm") || p.Name.Contains("Leg"))
            .ToList();

        foreach (var extremity in extremities)
        {
            // Calculate severity based on temperature
            double severity = Math.Clamp((SevereHypothermiaThreshold - target.BodyTemperature) / 5.0, 0.01, 1.0);

            string applicationMessage;
            string removalMessage;

            if (target.IsPlayer)
            {
                applicationMessage = $"Your {extremity.Name.ToLower()} is getting dangerously cold, you're developing frostbite! Severity = {severity}";
                removalMessage = $"The feeling is returning to your {extremity.Name.ToLower()}, the frostbite is healing...";
            }
            else
            {
                applicationMessage = $"DEBUG: {target} has frostbite on {extremity.Name}. Severity = {severity}";
                removalMessage = $"DEBUG: {target} no longer has frostbite on {extremity.Name}.";
            }

            // Apply frostbite to extremity using builder pattern
            var frostbite = EffectBuilderExtensions
                .CreateEffect("Frostbite")
                .Temperature(TemperatureType.Frostbite)
                .WithApplyMessage(applicationMessage)
                .WithSeverity(severity)
                .Targeting(extremity)
                .AllowMultiple(true) // Allow multiple frostbite effects on different extremities
                .WithRemoveMessage(removalMessage)
                .Build();

            target.EffectRegistry.AddEffect(frostbite);
        }
    }
}

