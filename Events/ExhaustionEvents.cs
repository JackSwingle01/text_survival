
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Events;
public class ExhaustionEvent(Actor target, bool isNew) : IGameEvent
{
    public Actor Target = target;
    public bool IsNew = isNew;
}
public class ExhaustionEventHandler : IEventHandler<ExhaustionEvent>
{
    public void Handle(ExhaustionEvent gameEvent)
    {
        if (gameEvent.IsNew)
        {
            Output.WriteDanger($"{gameEvent.Target.Name} is exhausted!\n");
        }
        else if (Utils.DetermineSuccess(Config.NOTIFY_EXISTING_STATUS_CHANCE))
        {
            Output.WriteWarning($"{gameEvent.Target.Name} is still exhausted.\n");
        }
        var target = gameEvent.Target;
        var damage = new DamageInfo()
        {
            Amount = 1,
            Type = "exhaustion",
            IsPenetrating = true
        };
        target.Damage(damage);
    }
}

public class StoppedExhaustionEvent(Actor target) : IGameEvent
{
    public Actor Target = target;
}
public class StoppedExhaustionEventHandler : IEventHandler<StoppedExhaustionEvent>
{
    public void Handle(StoppedExhaustionEvent gameEvent)
    {
        Output.WriteSuccess($"{gameEvent.Target.Name} is no longer exhausted!\n");
    }
}