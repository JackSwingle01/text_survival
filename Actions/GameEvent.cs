using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

public enum EventCondtion
{
    IsDaytime,
    AtCamp,
    Sleeping,
    Traveling,
    Resting,
    Working,
    HasFood,
    HasMeat,
    HasFirewood,
    HasStones,
    Injured,
    Bleeding,
    Slow,
    FireBurning,
    

}
public class EventResult(string message)
{
    public int TimeAddedMinutes;
    public bool AbortsExpedition;
    public Effect? NewEffect;
    public Item? NewItem;
    public string Message = message;
}
public class EventChoice(string label, string description, EventResult result, List<EventCondtion>? conditions = null)
{
    public string Label = label;
    public string Description = description;
    public readonly List<EventCondtion> RequiredConditions = conditions ?? [];
    public EventResult Result = result;
}

public class GameEvent(string name, string description, double baseChancePerHour = .01)
{
    public string Name = name;
    public string Description = description;
    public readonly List<EventCondtion> RequiredConditions = [];
    public double BaseChancePerMinute = ChanceHourToMinute(baseChancePerHour);
    public readonly Dictionary<EventCondtion, double> ChanceModifiers = [];
    public Choice<EventChoice> Choices = new("What do you do?");

    // Helper since percent per hour in minutes in is NOT equal to percent/60
    private static double ChanceHourToMinute(double chancePerHour) => 1 - Math.Pow(1 - chancePerHour, 1.0 / 60);

    public bool CheckCondition(EventCondtion condtition, GameContext ctx)
    {

        return false;
    }
}