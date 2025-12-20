using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Resource types that can be consumed by event outcomes.
/// Maps to high-level inventory categories.
/// </summary>
public enum ResourceType { Fuel, Tinder, Food }

/// <summary>
/// Represents resources consumed by an event outcome.
/// </summary>
public record ResourceCost(ResourceType Type, int Amount);

public class EventResult(string message, double weight = 1)
{
    public string Message = message;
    public double Weight = weight;
    public int TimeAddedMinutes;
    public bool AbortsExpedition;
    public Effect? NewEffect;
    public DamageInfo? NewDamage;
    public RewardPool RewardPool = RewardPool.None;
    public ResourceCost? Cost;  // Resources consumed by this outcome
}
public class EventChoice(string label, string description, List<EventResult> results, List<EventCondition>? conditions = null)
{
    public string Label = label;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = conditions ?? [];
    public List<EventResult> Result = results;
    public EventResult DetermineResult() => Utils.GetRandomWeighted(Result.ToDictionary(x => x, x => x.Weight));
}

public class GameEvent(string name, string description)
{
    public string Name = name;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = [];

    public double BaseWeight = 1.0;  // Selection weight (not trigger chance)
    public readonly Dictionary<EventCondition, double> WeightModifiers = [];

    public Choice<EventChoice> Choices = new("What do you do?");
    public void AddChoice(EventChoice c) => Choices.AddOption(c.Label, c);
}


