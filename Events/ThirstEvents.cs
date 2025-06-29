using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.Events;


public class DehydrationEvent(Actor target, bool isNew) : IGameEvent
{
    public Actor Target = target;
    public bool IsNew = isNew;
}
public class DehydrationEventHandler : IEventHandler<DehydrationEvent>
{
    public void Handle(DehydrationEvent gameEvent)
    {
        if (gameEvent.IsNew)
        {
            Output.WriteDanger($"{gameEvent.Target.Name} is dehydrated!\n");
        }
        else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
        {
            Output.WriteWarning($"{gameEvent.Target.Name} is still dehydrated.\n");

        }
        var effect = EffectBuilderExtensions.CreateEffect("Dehydration")
        .CausesDehydration(1)
        .ReducesCapacity(CapacityNames.Consciousness, .2) // head ache
        .ReducesCapacity(CapacityNames.BloodPumping, .1) // low fluids
        .ReducesCapacity(CapacityNames.Digestion, .4) // low fluids
        .ReducesCapacity(CapacityNames.Moving, .05) // sluggish
        .ReducesCapacity(CapacityNames.Breathing, .1) // dry throat
        .Build();
        gameEvent.Target.ApplyEffect(effect);
    }
}

public class StoppedDehydrationEvent(Actor target) : IGameEvent
{
    public Actor Target = target;
}
public class StoppedDehydrationEventHandler : IEventHandler<StoppedDehydrationEvent>
{
    public void Handle(StoppedDehydrationEvent gameEvent)
    {
        Output.WriteSuccess($"{gameEvent.Target.Name} is no longer dehydrated!\n");
    }
}
