using text_survival.Actions.Tensions;
using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web.Dto;

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

/// <summary>
/// Configuration for creating a carcass at the current location.
/// If AnimalType is null, uses territory-based selection.
/// </summary>
public record CarcassCreation(string? AnimalType, double? WeightKg);

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
    public GearDamage? DamageGear;
    public GearRepair? RepairGear;
    public ToolType? BreakTool;  // Legacy - completely destroy a tool

    // Feature modification
    public FeatureCreation? AddFeature;
    public Func<EnvironmentalDetail>? AddDetail;  // Factory for environmental details
    public FeatureModification? ModifyFeature;
    public Type? RemoveFeature;  // Remove feature of this type from location

    // Snare destruction
    public int DestroysSnareCount;

    // Direct stat drains (for vomiting, etc)
    public (double calories, double hydration)? StatDrain;

    // Carcass creation
    public CarcassCreation? CarcassCreation;

    // Carcass modification (scavenger loss as percentage, e.g. 0.5 = 50% loss)
    public double? CarcassScavengerLoss;

    // === Fluent builder methods ===

    public EventResult Aborts() { AbortsExpedition = true; return this; }
    public EventResult Rewards(RewardPool pool) { RewardPool = pool; return this; }
    public EventResult Costs(ResourceType type, int amount) { Cost = new ResourceCost(type, amount); return this; }

    public EventResult WithEffects(params Effect[] effects) { Effects.AddRange(effects); return this; }
    public EventResult Damage(int amount, DamageType type, BodyTarget target = BodyTarget.Random)
    {
        NewDamage = new DamageInfo(amount, type, target);
        return this;
    }

    /// <summary>
    /// Apply damage from an InjuryVariant, ensuring text and mechanics match.
    /// Uses the variant's target, type, amount, and any auto-triggered effects.
    /// </summary>
    public EventResult DamageWithVariant(InjuryVariant variant)
    {
        NewDamage = new DamageInfo(variant.Amount, variant.Type, target: variant.Target);
        if (variant.Effects != null)
            Effects.AddRange(variant.Effects);
        return this;
    }

    /// <summary>
    /// Apply rewards from a DiscoveryVariant, ensuring text and mechanics match.
    /// Uses the variant's reward pool and optional time cost.
    /// </summary>
    public EventResult WithDiscovery(DiscoveryVariant variant)
    {
        RewardPool = variant.Pool;
        if (variant.TimeMinutes > 0)
            TimeAddedMinutes += variant.TimeMinutes;
        return this;
    }

    /// <summary>
    /// Apply effects from an IllnessOnsetVariant, ensuring text and mechanics match.
    /// Uses the variant's initial effects and severity multiplier.
    /// </summary>
    public EventResult WithIllnessOnset(IllnessOnsetVariant variant)
    {
        Effects.AddRange(variant.InitialEffects);
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
    public EventResult AddsDetail(Func<EnvironmentalDetail> detailFactory)
    {
        AddDetail = detailFactory;
        return this;
    }
    public EventResult ModifiesFeature(Type featureType, double? depleteAmount = null)
    {
        ModifyFeature = new FeatureModification(featureType, depleteAmount);
        return this;
    }
    public EventResult RemovesFeature(Type featureType) { RemoveFeature = featureType; return this; }
    public EventResult DestroysSnare(int count = 1) { DestroysSnareCount = count; return this; }
    public EventResult WithScavengerLoss(double lossPct) { CarcassScavengerLoss = lossPct; return this; }

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
    /// Returns outcome data for UI display.
    /// </summary>
    public EventOutcomeDto Apply(GameContext ctx)
    {
        var summary = new OutcomeSummary();

        ApplyTimeAndMessage(ctx);
        ApplyEffects(ctx, summary);
        ApplyDamage(ctx, summary);
        ApplyRewards(ctx, summary);
        ApplyCosts(ctx, summary);
        ApplyStatDrains(ctx, summary);
        ApplyTensions(ctx, summary);
        ApplyEquipmentDamage(ctx, summary);
        ApplyFeatureModifications(ctx);
        ApplyCarcassCreation(ctx, summary);
        DisplaySummary(ctx, summary);

        return new EventOutcomeDto(
            Message: Message,
            TimeAddedMinutes: TimeAddedMinutes,
            EffectsApplied: summary.EffectsApplied,
            DamageTaken: summary.DamageTaken,
            ItemsGained: summary.ItemsGained,
            ItemsLost: summary.ItemsLost,
            TensionsChanged: summary.TensionsChanged
        );
    }

    private void ApplyTimeAndMessage(GameContext ctx)
    {
        if (TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative(ctx, $"(+{TimeAddedMinutes} minutes)");
            // Apply time without progress animation (time shows in outcome popup)
            // This prevents the progress frame from blocking the event overlay
            ctx.Update(TimeAddedMinutes, ctx.CurrentActivity);
        }

        GameDisplay.AddNarrative(ctx, Message);
    }

    private void ApplyEffects(GameContext ctx, OutcomeSummary summary)
    {
        foreach (var effect in Effects)
        {
            ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
            summary.EffectsApplied.Add(effect.EffectKind);
        }
    }

    private void ApplyDamage(GameContext ctx, OutcomeSummary summary)
    {
        if (NewDamage is not null)
        {
            var dmgResult = ctx.player.Body.Damage(NewDamage);

            // Track damage taken
            var targetName = BodyTargetResolver.GetDisplayName(NewDamage.Target);
            summary.DamageTaken.Add($"{NewDamage.Amount:F0} {NewDamage.Type} to {targetName}");

            // Track triggered effects (bleeding, pain, etc.)
            foreach (var effect in dmgResult.TriggeredEffects)
            {
                ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
                summary.EffectsApplied.Add(effect.EffectKind);
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
                summary.ItemsGained.Add(desc);
                InventoryCapacityHelper.CombineAndReport(ctx, resources);
            }
        }
    }

    private void ApplyCosts(GameContext ctx, OutcomeSummary summary)
    {
        if (Cost is not null)
        {
            DeductResources(ctx.Inventory, Cost);
            summary.ItemsLost.Add($"{Cost.Amount} {Cost.Type}");
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
                summary.ItemsLost.Add($"{calories:F0} calories");
            }
            if (hydration > 0)
            {
                ctx.player.Body.DrainHydration(hydration);
                GameDisplay.AddDanger(ctx, $"  - Lost {hydration:F0}ml hydration");
                summary.ItemsLost.Add($"{hydration:F0}ml hydration");
            }
        }
    }

    private void ApplyTensions(GameContext ctx, OutcomeSummary summary)
    {
        if (CreatesTension is not null)
        {
            var tension = CreateTensionFromConfig(CreatesTension);
            ctx.Tensions.AddTension(tension);
            summary.TensionsChanged.Add($"+{CreatesTension.Type}");
        }

        if (ResolvesTension is not null)
        {
            ctx.Tensions.ResolveTension(ResolvesTension);
            summary.TensionsChanged.Add($"-{ResolvesTension}");
        }

        if (EscalateTension is not null)
        {
            var (type, amount) = EscalateTension.Value;
            ctx.Tensions.EscalateTension(type, amount);
            summary.TensionsChanged.Add($"↑{type}");
        }
    }

    private void ApplyEquipmentDamage(GameContext ctx, OutcomeSummary summary)
    {
        // Unified gear damage - applies to tools, equipment, and accessories
        if (DamageGear is not null)
        {
            Gear? target = DamageGear.Category switch
            {
                GearCategory.Tool when DamageGear.ToolType.HasValue =>
                    ctx.Inventory.GetTool(DamageGear.ToolType.Value),
                GearCategory.Equipment when DamageGear.Slot.HasValue =>
                    ctx.Inventory.GetEquipment(DamageGear.Slot.Value),
                _ => null
            };

            if (target != null && target.Durability > 0)
            {
                target.Durability = Math.Max(0, target.Durability - DamageGear.DurabilityLoss);

                if (target.IsBroken)
                {
                    GameDisplay.AddDanger(ctx, $"  - {target.Name} breaks!");
                    summary.ItemsLost.Add($"{target.Name} destroyed");
                }
                else
                {
                    string conditionInfo = target.Category == GearCategory.Equipment
                        ? $" (now {target.ConditionPct:P0})"
                        : "";
                    GameDisplay.AddWarning(ctx, $"  - {target.Name} damaged{conditionInfo}");
                    summary.ItemsLost.Add($"{target.Name} damaged");
                }
            }
        }

        // Gear repair - restores durability
        if (RepairGear is not null)
        {
            Gear? target = RepairGear.Category switch
            {
                GearCategory.Tool when RepairGear.ToolType.HasValue =>
                    ctx.Inventory.GetTool(RepairGear.ToolType.Value),
                GearCategory.Equipment when RepairGear.Slot.HasValue =>
                    ctx.Inventory.GetEquipment(RepairGear.Slot.Value),
                _ => null
            };

            if (target != null && target.MaxDurability > 0)
            {
                target.Durability = Math.Min(target.MaxDurability, target.Durability + RepairGear.DurabilityGain);
                string conditionInfo = target.Category == GearCategory.Equipment
                    ? $" (now {target.ConditionPct:P0})"
                    : $" (+{RepairGear.DurabilityGain} uses)";
                GameDisplay.AddSuccess(ctx, $"  + {target.Name} repaired{conditionInfo}");
                summary.ItemsGained.Add($"{target.Name} repaired");
            }
        }

        // Tool break - completely destroy the tool (legacy support)
        if (BreakTool is not null)
        {
            var tool = ctx.Inventory.GetTool(BreakTool.Value);
            if (tool != null)
            {
                tool.Durability = 0;
                GameDisplay.AddDanger(ctx, $"  - {tool.Name} breaks!");
                summary.ItemsLost.Add($"{tool.Name} destroyed");
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

        if (AddDetail is not null)
        {
            var detail = AddDetail();
            ctx.CurrentLocation.AddFeature(detail);
            GameDisplay.AddNarrative(ctx, $"Noted: {detail.DisplayName}");
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

        // Snare destruction
        if (DestroysSnareCount > 0)
        {
            var snareLine = ctx.CurrentLocation.GetFeature<SnareLineFeature>();
            if (snareLine != null)
            {
                int destroyed = snareLine.DestroySnares(DestroysSnareCount);
                if (destroyed > 0)
                {
                    GameDisplay.AddDanger(ctx, destroyed == 1
                        ? "  - A snare is destroyed"
                        : $"  - {destroyed} snares destroyed");
                }
            }
        }

        // Carcass scavenger loss
        if (CarcassScavengerLoss is not null)
        {
            var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
            if (carcass != null)
            {
                double lossPct = CarcassScavengerLoss.Value;
                carcass.MeatRemainingKg *= (1 - lossPct);
                carcass.FatRemainingKg *= (1 - lossPct);
            }
        }
    }

    private void ApplyCarcassCreation(GameContext ctx, OutcomeSummary summary)
    {
        if (CarcassCreation is null) return;

        string animalType = CarcassCreation.AnimalType ?? GetTerritoryAnimal(ctx);
        double weightKg = CarcassCreation.WeightKg ?? CarcassFeature.GetDefaultWeight(animalType);

        var carcass = new CarcassFeature(animalType, weightKg);
        ctx.CurrentLocation.AddFeature(carcass);

        GameDisplay.AddSuccess(ctx, $"  + Found a {animalType.ToLower()} carcass");
        summary.ItemsGained.Add($"{animalType} carcass");
    }

    /// <summary>
    /// Get a random animal type from the current location's territory feature.
    /// Falls back to "deer" if no territory feature exists.
    /// </summary>
    private static string GetTerritoryAnimal(GameContext ctx)
    {
        var territory = ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>();
        if (territory != null)
        {
            // Use the territory's spawn entries to pick a random animal
            var animal = territory.SearchForGame(0);  // 0 minutes = just get random type
            if (animal != null)
                return animal.Name;
        }

        // Default fallback
        return "Deer";
    }

    private void DisplaySummary(GameContext ctx, OutcomeSummary summary)
    {
        if (summary.HasContent)
        {
            GameDisplay.AddNarrative(ctx, "");
            foreach (var gain in summary.ItemsGained)
                GameDisplay.AddSuccess(ctx, $"  + {gain}");
            foreach (var loss in summary.ItemsLost)
                GameDisplay.AddDanger(ctx, $"  - {loss}");
            foreach (var damage in summary.DamageTaken)
                GameDisplay.AddDanger(ctx, $"  - {damage}");
            foreach (var effect in summary.EffectsApplied)
                GameDisplay.AddWarning(ctx, $"  - {effect}");
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

        if (config.FeatureType == typeof(WaterFeature))
        {
            // Config is (min, typical, max) ice thickness - pick randomly in range
            var thickness = 0.6;
            if (config.Config is (double min, double typical, double max))
            {
                // Weight toward typical value
                thickness = typical + (Random.Shared.NextDouble() - 0.5) * (max - min) * 0.5;
                thickness = Math.Clamp(thickness, min, max);
            }
            return new WaterFeature("water", "Stream")
                .WithIceThickness(thickness)
                .WithDescription("A frozen stream you marked earlier.");
        }

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

    /// <summary>Helper class to track outcome data for summary display and DTO generation.</summary>
    private class OutcomeSummary
    {
        public List<string> EffectsApplied { get; } = [];
        public List<string> DamageTaken { get; } = [];
        public List<string> ItemsGained { get; } = [];
        public List<string> ItemsLost { get; } = [];
        public List<string> TensionsChanged { get; } = [];

        public bool HasContent => ItemsGained.Count + ItemsLost.Count + EffectsApplied.Count + DamageTaken.Count + TensionsChanged.Count > 0;
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
    public readonly List<Func<GameContext, bool>> RequiredSituations = [];

    public double BaseWeight = weight;  // Selection weight (not trigger chance)
    public readonly Dictionary<EventCondition, double> WeightFactors = [];
    public readonly List<(Func<GameContext, bool> Condition, double Multiplier)> SituationFactors = [];
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

    /// <summary>
    /// Get choices available to the player (filtered by conditions).
    /// </summary>
    public List<EventChoice> GetAvailableChoices(GameContext ctx)
        => _choices.Where(c => c.RequiredConditions.All(ctx.Check)).ToList();

    // === Fluent builder methods ===

    public GameEvent Requires(params EventCondition[] conditions)
    {
        RequiredConditions.AddRange(conditions);
        return this;
    }

    /// <summary>
    /// Add a required situation predicate. Event only triggers if situation is true.
    /// Use with Situations.* methods for compound conditions.
    /// </summary>
    public GameEvent RequiresSituation(Func<GameContext, bool> situation)
    {
        RequiredSituations.Add(situation);
        return this;
    }

    public GameEvent WithConditionFactor(EventCondition condition, double multiplier)
    {
        WeightFactors[condition] = multiplier;
        return this;
    }

    /// <summary>
    /// Add a weight factor based on a Situation predicate.
    /// Use with Situations.* methods for compound conditions.
    /// </summary>
    public GameEvent WithSituationFactor(Func<GameContext, bool> situation, double multiplier)
    {
        SituationFactors.Add((situation, multiplier));
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


