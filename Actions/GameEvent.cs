using text_survival.Actions.Tensions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

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

    // === Outcome Application ===

    /// <summary>
    /// Apply this event outcome to the game context.
    /// Processes time, effects, damage, rewards, costs, tensions, equipment, and features.
    /// </summary>
    public void Apply(GameContext ctx)
    {
        var summary = new OutcomeSummary();

        ApplyTimeAndMessage(ctx);
        ApplyEffects(ctx, summary);
        ApplyDamage(ctx, summary);
        ApplyRewards(ctx, summary);
        ApplyCosts(ctx);
        ApplyStatDrains(ctx, summary);
        ApplyTensions(ctx);
        ApplyEquipmentDamage(ctx, summary);
        ApplyFeatureModifications(ctx);
        DisplaySummary(ctx, summary);
    }

    private void ApplyTimeAndMessage(GameContext ctx)
    {
        if (TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative(ctx, $"(+{TimeAddedMinutes} minutes)");
            GameDisplay.UpdateAndRenderProgress(ctx, "Acting", TimeAddedMinutes, ctx.CurrentActivity);
        }

        GameDisplay.AddNarrative(ctx, Message);
    }

    private void ApplyEffects(GameContext ctx, OutcomeSummary summary)
    {
        foreach (var effect in Effects)
        {
            ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
            GameDisplay.AddDanger(ctx, $"  - {effect.EffectKind}");
            summary.Losses.Add(effect.EffectKind);
        }
    }

    private void ApplyDamage(GameContext ctx, OutcomeSummary summary)
    {
        if (NewDamage is not null)
        {
            var dmgResult = ctx.player.Body.Damage(NewDamage);
            GameDisplay.AddDanger(ctx, $"  - Injured ({NewDamage.Source})");
            summary.Losses.Add($"Injury: {NewDamage.Source}");
            foreach (var effect in dmgResult.TriggeredEffects)
            {
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
                GameDisplay.AddDanger(ctx, $"  - {effect.EffectKind}");
                summary.Losses.Add(effect.EffectKind);
            }
        }
    }

    private void ApplyRewards(GameContext ctx, OutcomeSummary summary)
    {
        if (RewardPool != RewardPool.None)
        {
            var resources = RewardGenerator.Generate(RewardPool);
            if (!resources.IsEmpty)
            {
                var desc = resources.GetDescription();
                GameDisplay.AddSuccess(ctx, $"  + {desc}");
                summary.Gains.Add(desc);
                InventoryCapacityHelper.CombineAndReport(ctx, resources);
            }
        }
    }

    private void ApplyCosts(GameContext ctx)
    {
        if (Cost is not null)
        {
            DeductResources(ctx.Inventory, Cost);
        }
    }

    private void ApplyStatDrains(GameContext ctx, OutcomeSummary summary)
    {
        if (StatDrain is not null)
        {
            var (calories, hydration) = StatDrain.Value;
            if (calories > 0)
            {
                ctx.player.Body.DrainCalories(calories);
                GameDisplay.AddDanger(ctx, $"  - Lost {calories:F0} calories");
                summary.Losses.Add($"{calories:F0} calories");
            }
            if (hydration > 0)
            {
                ctx.player.Body.DrainHydration(hydration);
                GameDisplay.AddDanger(ctx, $"  - Lost {hydration:F0}ml hydration");
                summary.Losses.Add($"{hydration:F0}ml hydration");
            }
        }
    }

    private void ApplyTensions(GameContext ctx)
    {
        if (CreatesTension is not null)
        {
            var tension = CreateTensionFromConfig(CreatesTension);
            ctx.Tensions.AddTension(tension);
        }

        if (ResolvesTension is not null)
        {
            ctx.Tensions.ResolveTension(ResolvesTension);
        }

        if (EscalateTension is not null)
        {
            var (type, amount) = EscalateTension.Value;
            ctx.Tensions.EscalateTension(type, amount);
        }
    }

    private void ApplyEquipmentDamage(GameContext ctx, OutcomeSummary summary)
    {
        // Tool damage - reduce durability
        if (DamageTool is not null)
        {
            var tool = ctx.Inventory.GetTool(DamageTool.Type);
            if (tool != null && tool.Durability > 0)
            {
                tool.Durability = Math.Max(0, tool.Durability - DamageTool.UsesLost);
                if (tool.IsBroken)
                {
                    GameDisplay.AddDanger(ctx, $"  - {tool.Name} breaks!");
                    summary.Losses.Add($"{tool.Name} destroyed");
                }
                else
                {
                    GameDisplay.AddWarning(ctx, $"  - {tool.Name} damaged");
                    summary.Losses.Add($"{tool.Name} damaged");
                }
            }
        }

        // Tool break - completely destroy the tool
        if (BreakTool is not null)
        {
            var tool = ctx.Inventory.GetTool(BreakTool.Value);
            if (tool != null)
            {
                tool.Durability = 0;
                GameDisplay.AddDanger(ctx, $"  - {tool.Name} breaks!");
                summary.Losses.Add($"{tool.Name} destroyed");
            }
        }

        // Clothing damage - reduce insulation
        if (DamageClothing is not null)
        {
            var equipment = ctx.Inventory.GetEquipment(DamageClothing.Slot);
            if (equipment != null)
            {
                equipment.Insulation = Math.Max(0, equipment.Insulation - DamageClothing.InsulationLoss);
                GameDisplay.AddWarning(ctx, $"  - {equipment.Name} damaged");
                summary.Losses.Add($"{equipment.Name} damaged");
            }
        }
    }

    private void ApplyFeatureModifications(GameContext ctx)
    {
        if (AddFeature is not null)
        {
            var feature = CreateFeatureFromConfig(AddFeature);
            if (feature != null)
            {
                ctx.CurrentLocation.AddFeature(feature);
                GameDisplay.AddNarrative(ctx, $"Added {feature.Name} to this location.");
            }
        }

        if (ModifyFeature is not null)
        {
            if (ModifyFeature.FeatureType == typeof(ForageFeature) && ModifyFeature.DepleteAmount is not null)
            {
                var feature = ctx.CurrentLocation.GetFeature<ForageFeature>();
                if (feature != null)
                {
                    feature.Deplete(ModifyFeature.DepleteAmount.Value);
                }
            }
            else if (ModifyFeature.FeatureType == typeof(ShelterFeature) && ModifyFeature.DepleteAmount is not null)
            {
                var feature = ctx.CurrentLocation.GetFeature<ShelterFeature>();
                if (feature != null)
                {
                    feature.Damage(ModifyFeature.DepleteAmount.Value);
                }
            }
        }

        if (RemoveFeature is not null)
        {
            if (RemoveFeature == typeof(ShelterFeature))
            {
                var feature = ctx.CurrentLocation.GetFeature<ShelterFeature>();
                if (feature != null)
                {
                    ctx.CurrentLocation.RemoveFeature(feature);
                    GameDisplay.AddDanger(ctx, $"  - {feature.Name} destroyed");
                }
            }
            else if (RemoveFeature == typeof(HeatSourceFeature))
            {
                var feature = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                if (feature != null)
                {
                    ctx.CurrentLocation.RemoveFeature(feature);
                    GameDisplay.AddDanger(ctx, $"  - {feature.Name} destroyed");
                }
            }
        }
    }

    private void DisplaySummary(GameContext ctx, OutcomeSummary summary)
    {
        if (summary.HasContent)
        {
            GameDisplay.AddNarrative(ctx, "");
            foreach (var gain in summary.Gains)
                GameDisplay.AddSuccess(ctx, $"  + {gain}");
            foreach (var loss in summary.Losses)
                GameDisplay.AddDanger(ctx, $"  - {loss}");
        }
    }

    /// <summary>
    /// Create a tension instance from TensionCreation config.
    /// </summary>
    private static ActiveTension CreateTensionFromConfig(TensionCreation tc)
    {
        return tc.Type switch
        {
            "Stalked" => ActiveTension.Stalked(tc.Severity, tc.AnimalType, tc.RelevantLocation),
            "SmokeSpotted" => ActiveTension.SmokeSpotted(tc.Severity, tc.Direction, tc.RelevantLocation),
            "Infested" => ActiveTension.Infested(tc.Severity, tc.RelevantLocation),
            "WoundUntreated" => ActiveTension.WoundUntreated(tc.Severity, tc.Description),
            "ShelterWeakened" => ActiveTension.ShelterWeakened(tc.Severity, tc.RelevantLocation),
            "FoodScentStrong" => ActiveTension.FoodScentStrong(tc.Severity),
            "Hunted" => ActiveTension.Hunted(tc.Severity, tc.AnimalType),
            "MarkedDiscovery" => ActiveTension.MarkedDiscovery(tc.Severity, tc.RelevantLocation, tc.Description),
            "Disturbed" => ActiveTension.Disturbed(tc.Severity, tc.RelevantLocation, tc.Description),
            "WoundedPrey" => ActiveTension.WoundedPrey(tc.Severity, tc.AnimalType, tc.RelevantLocation),
            "PackNearby" => ActiveTension.PackNearby(tc.Severity, tc.AnimalType),
            "ClaimedTerritory" => ActiveTension.ClaimedTerritory(tc.Severity, tc.AnimalType, tc.RelevantLocation),
            "HerdNearby" => ActiveTension.HerdNearby(tc.Severity, tc.AnimalType, tc.Direction),
            "DeadlyCold" => ActiveTension.DeadlyCold(tc.Severity),
            "FeverRising" => ActiveTension.FeverRising(tc.Severity, tc.Description),
            "TrapLineActive" => ActiveTension.TrapLineActive(tc.Severity, tc.RelevantLocation),
            _ => ActiveTension.Custom(tc.Type, tc.Severity, 0.05, true, tc.RelevantLocation, tc.AnimalType, tc.Direction, tc.Description)
        };
    }

    /// <summary>
    /// Create a feature instance from FeatureCreation config.
    /// </summary>
    private static LocationFeature? CreateFeatureFromConfig(FeatureCreation config)
    {
        if (config.FeatureType == typeof(ShelterFeature))
        {
            // Default shelter stats if no config provided
            if (config.Config is (double temp, double overhead, double wind))
                return new ShelterFeature("Improvised Shelter", temp, overhead, wind);
            else
                return new ShelterFeature("Improvised Shelter", 0.3, 0.4, 0.5);
        }

        // Add other feature types as needed
        return null;
    }

    /// <summary>
    /// Deduct resources from inventory based on cost type.
    /// </summary>
    private static void DeductResources(Inventory inv, ResourceCost cost)
    {
        for (int i = 0; i < cost.Amount; i++)
        {
            switch (cost.Type)
            {
                case ResourceType.Fuel:
                    // Prefer sticks over logs (less wasteful)
                    if (inv.Count(Resource.Stick) > 0)
                        inv.Pop(Resource.Stick);
                    else if (inv.HasLogs)
                        inv.PopLog();
                    break;

                case ResourceType.Tinder:
                    inv.Pop(Resource.Tinder);
                    break;

                case ResourceType.Food:
                    // Prefer berries, then cooked, then raw
                    if (inv.Count(Resource.Berries) > 0)
                        inv.Pop(Resource.Berries);
                    else if (inv.Count(Resource.CookedMeat) > 0)
                        inv.Pop(Resource.CookedMeat);
                    else if (inv.Count(Resource.RawMeat) > 0)
                        inv.Pop(Resource.RawMeat);
                    break;

                case ResourceType.Water:
                    // Deduct 0.25L per unit
                    inv.WaterLiters = Math.Max(0, inv.WaterLiters - 0.25);
                    break;

                case ResourceType.PlantFiber:
                    inv.Pop(Resource.PlantFiber);
                    break;
            }
        }
    }

    /// <summary>Helper class to track outcome gains and losses for summary display.</summary>
    private class OutcomeSummary
    {
        public List<string> Gains { get; } = [];
        public List<string> Losses { get; } = [];
        public bool HasContent => Gains.Count > 0 || Losses.Count > 0;
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
    public int CooldownHours = 24;  // Default: 1 day before event can trigger again

    // Location-specific filtering
    public string? RequiredLocationName;  // Exact match on location name

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

    public GameEvent WithCooldown(int hours)
    {
        CooldownHours = hours;
        return this;
    }

    public GameEvent WithLocationNameRequirement(string locationName)
    {
        RequiredLocationName = locationName;
        return this;
    }

    /// <summary>Shortcut for WithLocationNameRequirement.</summary>
    public GameEvent OnlyAt(string locationName) => WithLocationNameRequirement(locationName);

    public GameEvent Choice(string label, string description, List<EventResult> results,
        List<EventCondition>? requires = null)
    {
        _choices.Add(new EventChoice(label, description, results, requires));
        return this;
    }

    // Static helper for cleaner EventResult creation
    public static EventResult Result(string message, double weight = 1, int time = 0) => new(message, weight, time);
}


