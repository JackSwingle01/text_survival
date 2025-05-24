
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Events;

public class StarvingEvent(Body target, double calories, bool isNew) : IGameEvent
{
    public Body Target = target;
    public bool IsNew = isNew;
    public double Calories = calories;
}

public class StarvingEventHandler : IEventHandler<StarvingEvent>
{
    private const double CALORIES_PER_KG_MUSCLE = 5500.0; // Calories in 1kg of muscle (less than fat)
    private const double CALORIES_PER_KG_FAT = 7700.0;
    public void Handle(StarvingEvent gameEvent)
    {
        DisplayMessage(gameEvent);


        HandleBodyStarvation(gameEvent.Target, gameEvent.Calories);
        if (!ShouldApplyDamage(gameEvent.Target)) return; // has enough fat/muscle


        ApplyDamage(gameEvent.Target);
        // todo add more effects
    }

    // private methods //
    private static void DisplayMessage(StarvingEvent gameEvent)
    {
        if (gameEvent.IsNew)
        {
            Output.WriteDanger($"{gameEvent.Target.OwnerName} is starving!\n");
        }
        else if (Utils.DetermineSuccess(.1))
        {
            Output.WriteWarning($"{gameEvent.Target.OwnerName} is still starving.\n");
        }
    }

    private static void HandleBodyStarvation(Body body, double calories)
    {
        if (body.BodyFat > 0)
        {
            // Convert fat to energy
            double fatBurnRate = calories / CALORIES_PER_KG_FAT;
            body.BodyFat -= fatBurnRate;
        }
        else if (body.Muscle > 0)
        {
            // Convert muscle to energy
            double muscleBurnRate = calories / CALORIES_PER_KG_MUSCLE * 1.2; // Muscle burns less efficiently
            body.Muscle -= muscleBurnRate;
        }
    }

    private static bool ShouldApplyDamage(Body body)
    {
        // apply damage if fat and muscle are dangerously low
        return body.BodyFatPercentage <= 0.05 && body.MusclePercentage <= 0.05;
    }

    private static void ApplyDamage(Body target)
    {
        var damage = new DamageInfo()
        {
            Amount = 1,
            Type = "starvation",
            IsPenetrating = true
        };
        target.Damage(damage);
    }
}

public class StoppedStarvingEvent(Body target) : IGameEvent
{
    public Body Target = target;
}
public class StoppedStarvingEventHandler : IEventHandler<StoppedStarvingEvent>
{
    public void Handle(StoppedStarvingEvent gameEvent)
    {
        Output.WriteSuccess($"{gameEvent.Target.OwnerName} is no longer starving!\n");
    }
}