using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;


public class EventResult(string message, double weight = 1)
{
    public string Message = message;
    public double Weight = weight;
    public int TimeAddedMinutes;
    public bool AbortsExpedition;
    public Effect? NewEffect;
    public RewardPool RewardPool = RewardPool.None;
}
public class EventChoice(string label, string description, List<EventResult> results, List<EventCondition>? conditions = null)
{
    public string Label = label;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = conditions ?? [];
    public List<EventResult> Result = results;
    public EventResult DetermineResult() => Utils.GetRandomWeighted(Result.ToDictionary(x => x, x => x.Weight));
}

public class GameEvent(string name, string description, double baseChancePerHour = .01)
{
    public string Name = name;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = [];
    public double BaseChancePerMinute = RateToChancePerMinute(baseChancePerHour);
    public readonly Dictionary<EventCondition, double> ChanceModifiers = [];
    public Choice<EventChoice> Choices = new("What do you do?");
    public void AddChoice(EventChoice c) => Choices.AddOption(c.Label, c);

    /// <summary>
    /// Helper since percent per hour in minutes in is NOT equal to percent/60
    /// </summary>
    /// <param name="eventsPerHour">1 means "on average, 1 event per hour"</param>
    /// <returns></returns>
    private static double RateToChancePerMinute(double eventsPerHour) 
    {
        double ratePerMinute = eventsPerHour / 60.0;
        return 1 - Math.Exp(-ratePerMinute);
    }
}


