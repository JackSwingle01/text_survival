using text_survival.Actors;
using text_survival.Bodies;
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
        else if (Utils.DetermineSuccess(.1))
        {
            Output.WriteWarning($"{gameEvent.Target.Name} is still dehydrated.\n");

        }
        var target = gameEvent.Target;
        var damage = new DamageInfo()
        {
            Amount = 1,
            Type = "dehydration",
            IsPenetrating = true
        };
        target.Damage(damage);
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
