using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Resource types that can be consumed by event outcomes.
/// Maps to high-level inventory categories.
/// </summary>
public enum ResourceType { Fuel, Tinder, Food, Water, PlantFiber }

/// <summary>
/// Represents resources consumed by an event outcome.
/// </summary>
public record ResourceCost(ResourceType Type, int Amount);

public class EventResult(string message, double weight = 1, int minutes = 0)
{
    public string Message = message;
    public double Weight = weight;
    public int TimeAddedMinutes = minutes;
    public bool AbortsExpedition;
    public List<Effect> Effects = [];
    public DamageInfo? NewDamage;
    public RewardPool RewardPool = RewardPool.None;
    public ResourceCost? Cost;  // Resources consumed by this outcome

    // Tension fields
    public TensionCreation? CreatesTension;
    public string? ResolvesTension;
    public (string type, double amount)? EscalateTension;

    // Chaining fields
    public Func<GameContext, GameEvent>? ChainEvent;
    public EncounterConfig? SpawnEncounter;

    // Equipment targeting
    public ToolDamage? DamageTool;
    public ToolType? BreakTool;
    public ClothingDamage? DamageClothing;

    // Feature modification
    public FeatureCreation? AddFeature;
    public FeatureModification? ModifyFeature;
    public Type? RemoveFeature;  // Remove feature of this type from location

    // Direct stat drains (for vomiting, etc)
    public (double calories, double hydration)? StatDrain;

    // === Fluent builder methods ===

    public EventResult Aborts() { AbortsExpedition = true; return this; }
    public EventResult Rewards(RewardPool pool) { RewardPool = pool; return this; }
    public EventResult Costs(ResourceType type, int amount) { Cost = new ResourceCost(type, amount); return this; }

    public EventResult WithEffects(params Effect[] effects) { Effects.AddRange(effects); return this; }
    public EventResult Damage(int amount, DamageType type, string source)
    {
        NewDamage = new DamageInfo(amount, type, source);
        return this;
    }

    // Tension operations
    public EventResult CreateTension(string type, double severity, Environments.Location? location = null,
        string? animalType = null, string? direction = null, string? description = null)
    {
        CreatesTension = new TensionCreation(type, severity, location, animalType, direction, description);
        return this;
    }
    public EventResult ResolveTension(string type) { ResolvesTension = type; return this; }
    public EventResult Escalate(string type, double amount) { EscalateTension = (type, amount); return this; }

    // Encounter spawning
    public EventResult Encounter(string animal, int distance, double boldness)
    {
        SpawnEncounter = new EncounterConfig(animal, distance, boldness);
        return this;
    }

    // Event chaining - immediately triggers a follow-up event
    public EventResult Chain(Func<GameContext, GameEvent> eventFactory)
    {
        ChainEvent = eventFactory;
        return this;
    }

    // Feature operations
    public EventResult AddsFeature(Type featureType, (double, double, double) qualityRange)
    {
        AddFeature = new FeatureCreation(featureType, qualityRange);
        return this;
    }
    public EventResult ModifiesFeature(Type featureType, double? depleteAmount = null)
    {
        ModifyFeature = new FeatureModification(featureType, depleteAmount);
        return this;
    }
    public EventResult RemovesFeature(Type featureType) { RemoveFeature = featureType; return this; }

    // Stat drains
    public EventResult DrainsStats(double calories = 0, double hydration = 0)
    {
        StatDrain = (calories, hydration);
        return this;
    }
}
public class EventChoice(string label, string description, List<EventResult> results, List<EventCondition>? conditions = null)
{
    public string Label = label;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = conditions ?? [];
    public List<EventResult> Result = results;
    public EventResult DetermineResult() => Utils.GetRandomWeighted(Result.ToDictionary(x => x, x => x.Weight));
}

public class GameEvent(string name, string description, double weight)
{
    public string Name = name;
    public string Description = description;
    public readonly List<EventCondition> RequiredConditions = [];

    public double BaseWeight = weight;  // Selection weight (not trigger chance)
    public readonly Dictionary<EventCondition, double> WeightFactors = [];

    private List<EventChoice> _choices = [];
    public EventChoice GetChoice(GameContext ctx)
    {
        // filter to only ones that meet conditions todo
        var choices = new Choice<EventChoice>("What do you do?");
        _choices.Where(x => x.RequiredConditions.All(ctx.Check)).ToList().ForEach(x => choices.AddOption(x.Label, x));
        return choices.GetPlayerChoice(ctx);
    }
    public void AddChoice(EventChoice c) => _choices.Add(c);

    // === Fluent builder methods ===

    public GameEvent Requires(params EventCondition[] conditions)
    {
        RequiredConditions.AddRange(conditions);
        return this;
    }

    public GameEvent WithConditionFactor(EventCondition condition, double multiplier)
    {
        WeightFactors[condition] = multiplier;
        return this;
    }

    public GameEvent Choice(string label, string description, List<EventResult> results,
        List<EventCondition>? requires = null)
    {
        _choices.Add(new EventChoice(label, description, results, requires));
        return this;
    }

    // Static helper for cleaner EventResult creation
    public static EventResult Result(string message, double weight = 1, int time = 0) => new(message, weight, time);
}


