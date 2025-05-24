
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Events;

public class CalorieSurplusEvent(Body target, double calories) : IGameEvent
{
    public Body Target = target;
    public double Calories = calories;
}

public class CalorieSurplusEventHandler : IEventHandler<CalorieSurplusEvent>
{
    private const double CALORIES_PER_KG_FAT = 7700.0; // Standard calories stored in 1kg of fat

    public void Handle(CalorieSurplusEvent gameEvent)
    {

        double fatGain = gameEvent.Calories / CALORIES_PER_KG_FAT;

        gameEvent.Target.BodyFat += fatGain;

        int gFatGain = (int)(fatGain * 1000);
        Output.WriteLine($"{gameEvent.Target.OwnerName} gains {gFatGain}g of body fat from excess calories.");

    }

}
