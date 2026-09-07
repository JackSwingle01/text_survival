using text_survival.Combat;

using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Actors;

public abstract class NPCAction(string name, int durationMin, ActivityType activityType)
{
    public string Name = name;
    public abstract string LogMessage { get; }
    public int DurationMinutes = durationMin;
    public int MinutesSpent = 0;
    public bool IsComplete() => MinutesSpent >= DurationMinutes;
    public bool Settled { get; set; }
    public bool IsFollowingPursuit { get; set; }
    public void Complete(NPC npc)
    {
        if (Settled) return;
        Settled = true;
        OnComplete(npc);
    }
    public void Interrupt(NPC npc)
    {
        if (Settled) return;
        OnInterrupt(npc);
        Settled = true;
    }
    protected abstract void OnComplete(NPC npc);
    protected virtual void OnInterrupt(NPC npc) { } // Atomic actions earn no result until completed.
    public ActivityType ActivityType = activityType;
}



public class NPCEat(Resource food, double amount) : NPCAction($"Eating {food.ToDisplayName()}", 5, ActivityType.Eating)
{
    protected override void OnInterrupt(NPC npc) => npc.Inventory.Add(food, amount);

    public Resource Food => food;
    public double Amount => amount;

    public override string LogMessage => $"Eating {food.ToDisplayName()}";
    protected override void OnComplete(NPC npc)
    {
        ConsumptionHandler.EatDrink(npc, food, amount);
    }
}

public class NPCMove(Location destination, NPC npc) :
    NPCAction($"Traveling to {destination.Name}", TravelProcessor.GetTraversalMinutes(npc.CurrentLocation, destination, npc, npc.Inventory, npc.Map), ActivityType.Traveling)
{
    public Location Destination => destination;

    public override string LogMessage => $"Traveling to {destination.Name}";
    protected override void OnComplete(NPC npc)
    {
        ActorMovement.CompleteCrossing(npc, destination);
    }

}

public class NPCForage(int minutes) : NPCAction("Foraging", minutes, ActivityType.Foraging)
{
    protected override void OnInterrupt(NPC npc) { if (MinutesSpent > 0) Complete(npc); }

    public override string LogMessage => "Foraging";
    protected override void OnComplete(NPC npc)
    {
        bool hasLight = true; // todo
        var found = WorkHandler.Forage(npc, npc.Inventory, npc.CurrentLocation, MinutesSpent, hasLight);
        _ = npc.Inventory.CombineWithCapacity(found); // discard overflow
    }
}
public class NPCHarvest : NPCAction
{
    protected override void OnInterrupt(NPC npc) { if (MinutesSpent > 0) Complete(npc); }

    public IReadOnlyCollection<Resource>? Wanted => _wanted;

    /// <summary>
    /// What the NPC came here for. Carried from the decision through to the work so it cannot
    /// walk to a marsh needing water and harvest the berry bush it finds standing in front.
    /// </summary>
    private readonly IReadOnlyCollection<Resource>? _wanted;

    public override string LogMessage => "Harvesting";
    public NPCHarvest(int minutes, IReadOnlyCollection<Resource>? wanted = null)
        : base("Harvesting", minutes, ActivityType.Foraging) => _wanted = wanted;

    protected override void OnComplete(NPC npc)
    {
        var feature = WorkHandler.GetAvailableHarvestable(npc.CurrentLocation, _wanted);
        if (feature == null)
        {
            npc.Trace($"[NPC:{npc.Name}] No harvestable at {npc.CurrentLocation.Name}");
            return;
        }

        // Check tool requirements (some harvestables need tools)
        if (feature.RequiredToolType != null)
        {
            var tool = npc.Inventory!.GetTool(feature.RequiredToolType.Value);
            if (!feature.MeetsToolRequirement(tool))
            {
                npc.Trace($"[NPC:{npc.Name}] Missing tool: {feature.GetToolRequirementDescription()}");
                return;
            }
        }

        // Execute harvest via WorkHandler
        var found = WorkHandler.Harvest(npc.CurrentLocation, MinutesSpent, _wanted);

        // Add to NPC inventory (discard overflow)
        _ = npc.Inventory!.CombineWithCapacity(found);

        npc.Trace($"[NPC:{npc.Name}] Harvested {found.GetDescription()} from {feature.DisplayName}");
    }
}
public class NPCChopWood : NPCAction
{
    protected override void OnInterrupt(NPC npc) { if (MinutesSpent > 0) Complete(npc); }

    public override string LogMessage => "Chopping wood";
    public NPCChopWood(int minutes) : base("Chopping wood", minutes, ActivityType.Chopping) { }

    protected override void OnComplete(NPC npc)
    {
        var feature = npc.CurrentLocation.GetFeature<WoodedAreaFeature>();
        if (feature == null || !feature.HasTrees)
        {
            npc.Trace($"[NPC:{npc.Name}] No trees at {npc.CurrentLocation.Name}");
            return;
        }

        // Check for working axe (required)
        var axe = npc.Inventory!.GetTool(ToolType.Axe);
        if (axe == null || axe.IsBroken)
        {
            npc.Trace($"[NPC:{npc.Name}] No working axe");
            return;
        }

        // Use axe (durability cost)
        bool axeStillWorks = axe.Use();

        // Add progress to feature (persists across sessions)
        feature.AddProgress(MinutesSpent);

        // Check if tree is ready to fell
        if (feature.IsTreeReady)
        {
            var yield = feature.FellTree();
            var overflow = npc.Inventory.CombineWithCapacity(yield);

            if (!overflow.IsEmpty)
                npc.Trace($"[NPC:{npc.Name}] Inventory overflow - dropped {overflow.GetDescription()}");

            npc.Trace($"[NPC:{npc.Name}] Felled tree! Got {yield.GetDescription()}");
        }
        else
        {
            npc.Trace($"[NPC:{npc.Name}] Chopping: {feature.ProgressPct:P0}");
        }

        if (!axeStillWorks)
            npc.Trace($"[NPC:{npc.Name}] Axe broke!");
    }
}
public class NPCStartFire() : NPCAction("Starting Fire", 10, ActivityType.TendingFire)
{
    public override string LogMessage => "Starting fire";
    protected override void OnComplete(NPC npc)
    {
        if (FireHandler.StartFire(npc, npc.Inventory!, npc.CurrentLocation))
            RelationshipEvents.TendedFire(npc.Game, npc);
    }
}
public class NPCTendFire() : NPCAction("Tending Fire", 1, ActivityType.TendingFire)
{
    public override string LogMessage => "Tending fire";
    protected override void OnComplete(NPC npc)
    {
        if (!npc.CurrentLocation.HasFeature<HeatSourceFeature>())
        {
            npc.Trace("Looks like the AI is broke! Trying to tend a fire where there is none!");
            return;
        }
        FireHandler.TendFire(npc.Inventory!, npc.CurrentLocation.GetFeature<HeatSourceFeature>()!);
        RelationshipEvents.TendedFire(npc.Game, npc);
    }
}
public class NPCRest(int minutes) : NPCAction("Resting", minutes, ActivityType.Resting)
{
    public override string LogMessage => "Resting";
    protected override void OnComplete(NPC npc) { } // do nothing
}

public class NPCSleep(int minutes) : NPCAction("Sleeping", minutes, ActivityType.Sleeping)
{
    public override string LogMessage => "Sleeping";
    protected override void OnComplete(NPC npc) { } // the survival tick handles rest while asleep
}

public class NPCStash(ResourceCategory resourceCategory) : NPCAction($"Storing {resourceCategory}", 2, ActivityType.Crafting)
{
    public ResourceCategory Category => resourceCategory;

    public override string LogMessage => $"Stashing {resourceCategory.ToString().ToLower()}";
    protected override void OnComplete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            npc.Trace("AI's BROKE. Trying to store items where there's no cache!");
            return;
        }
        while (npc.Inventory!.GetCount(resourceCategory) > 0)
        {
            var item = npc.Inventory.FindAnyResourceInCategory(resourceCategory);
            cache.Storage.Add(item, npc.Inventory.Pop(item));
        }
    }
}

public class NPCStashWater() : NPCAction("Storing Water", 2, ActivityType.Crafting)
{
    public override string LogMessage => "Stashing water";
    protected override void OnComplete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            npc.Trace("AI's BROKE. Trying to store water where there's no cache!");
            return;
        }
        // Transfer all water from NPC inventory to cache storage
        while (npc.Inventory!.Count(Resource.Water) > 0)
        {
            double water = npc.Inventory.Pop(Resource.Water);
            cache.Storage.Add(Resource.Water, water);
        }
    }
}

public class NPCTakeToolFromCache(ToolType toolType) : NPCAction($"Taking {toolType}", 2, ActivityType.Crafting)
{
    public ToolType Tool => toolType;

    public override string LogMessage => $"Getting {toolType.ToString().ToLower()}";
    protected override void OnComplete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null) return;

        var tool = cache.Storage.Tools.FirstOrDefault(t => t.ToolType == toolType && t.Works);
        if (tool != null)
        {
            cache.Storage.Tools.Remove(tool);
            npc.Inventory!.Tools.Add(tool);
        }
    }
}

public class NPCTakeResourceFromCache(ResourceCategory category, double targetWeightKg = 5.0)
    : NPCAction($"Taking {category}", 2, ActivityType.Crafting)
{
    public ResourceCategory Category => category;
    public double TargetWeight => targetWeightKg;

    public override string LogMessage => $"Getting {category.ToString().ToLower()} from cache";
    protected override void OnComplete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null) return;

        double taken = 0;
        while (taken < targetWeightKg && cache.Storage.GetWeight(category) > 0)
        {
            var resource = cache.Storage.FindAnyResourceInCategory(category);
            double amount = cache.Storage.Pop(resource);
            if (!npc.Inventory!.CanCarry(amount))
            {
                cache.Storage.Add(resource, amount);
                break;
            }
            npc.Inventory.Add(resource, amount);
            taken += amount;
        }
    }
}

public class NPCCraft : NPCAction
{
    public CraftOption Recipe => _recipe;

    private readonly CraftOption _recipe;

    public override string LogMessage => $"Crafting {_recipe.Name.ToLower()}";

    public NPCCraft(CraftOption recipe) : base($"Crafting {recipe.Name}", recipe.CraftingTimeMinutes, ActivityType.Crafting)
    {
        _recipe = recipe;
    }

    protected override void OnComplete(NPC npc)
    {
        var result = _recipe.Craft(npc.Inventory!);
        if (result != null)
            npc.Inventory!.Tools.Add(result);
    }
}

#region Combat Actions

public class NPCFight : NPCAction
{
    private readonly Actor _threat;

    public override string LogMessage => $"Fighting {_threat.Name}";

    public NPCFight(Actor threat) : base($"Fighting {threat.Name}", 1, ActivityType.Fighting)
    {
        _threat = threat;
    }

    protected override void OnComplete(NPC npc)
    {
        // Set combat cooldown to prevent re-detection
        npc.SetCombatCooldown(5);

        var ctx = npc.Game;
        List<Actor> enemies = _threat is Animal animal
            ? CombatOrchestrator.AnimalSide(ctx, animal)
            : [_threat];

        CompanionCombat.StartDefense(ctx, npc, enemies);
    }

    public Actor Threat => _threat;
}

public class NPCFlee : NPCAction
{
    public Actor Threat => _threat;

    private readonly Actor _threat;

    public override string LogMessage => $"Fleeing from {_threat.Name}";

    public NPCFlee(Actor threat) : base($"Fleeing from {threat.Name}", 5, ActivityType.Traveling)
    {
        _threat = threat;
    }

    protected override void OnComplete(NPC npc)
    {
        // Set combat cooldown to prevent immediate re-detection
        npc.SetCombatCooldown(5);

        // Move toward camp if known, else random adjacent
        Location? retreat = null;

        if (npc.Camp != null && npc.CurrentLocation != npc.Camp)
        {
            retreat = text_survival.Environments.Navigation.Navigation.NextStep(npc, npc.Camp);
        }

        if (retreat == null)
        {
            var options = npc.Map.GetTravelOptionsFrom(npc.CurrentLocation).ToList();
            if (options.Count > 0)
                retreat = Utils.GetRandomFromList(options);
        }

        if (retreat != null)
        {
            npc.Trace($"[NPC:{npc.Name}] Fleeing to {retreat.Name}");
            ActorMovement.CompleteCrossing(npc, retreat);
        }
        else
        {
            npc.Trace($"[NPC:{npc.Name}] Cannot flee - no escape route!");
        }
    }
}

public class NPCCookMeat : NPCAction
{
    public override string LogMessage => "Cooking meat";
    public NPCCookMeat() : base("Cooking meat", CookingHandler.CookMeatTimeMinutes, ActivityType.Cooking) { }

    protected override void OnComplete(NPC npc)
    {
        CookingHandler.CookMeatNPC(npc, npc.Inventory!, npc.CurrentLocation);
    }
}
public class NPCMeltSnow : NPCAction
{
    public override string LogMessage => "Melting snow";
    public NPCMeltSnow() : base("Melting snow", CookingHandler.MeltSnowTimeMinutes, ActivityType.Cooking) { }

    protected override void OnComplete(NPC npc)
    {
        CookingHandler.MeltSnowNPC(npc, npc.Inventory!, npc.CurrentLocation);
    }
}

public class NPCDrinkWater : NPCAction
{
    public override string LogMessage => "Drinking water";

    public NPCDrinkWater() : base("Drinking water", 2, ActivityType.Eating) { }

    protected override void OnComplete(NPC npc)
    {
        // Drink to fill the room actually available, the way the player does, rather than a
        // fixed sip. Hydration is measured in milliliters, so litres must be converted -
        // without that an NPC gains 0.5ml from half a litre and can never rehydrate.
        double roomLiters = (SurvivalProcessor.MAX_HYDRATION - npc.Body.Hydration)
            / ConsumptionHandler.WaterHydrationPerLiter;
        double toDrink = Math.Min(ConsumptionHandler.MaxDrinkLiters, Math.Max(0, roomLiters));

        double consumed = npc.Inventory!.ConsumeByWeight(Resource.Water, toDrink);
        if (consumed > 0)
        {
            npc.Body.AddHydration(consumed * ConsumptionHandler.WaterHydrationPerLiter);
            npc.Trace($"[NPC:{npc.Name}] Drank {consumed:F1}L water");
        }
    }
}

#endregion

#region Shelter Actions

public class NPCImproveShelter : NPCAction
{
    public ShelterImprovementType Improvement => _type;
    public Resource Material => _material;
    public int Quantity => _quantity;

    private readonly ShelterImprovementType _type;
    private readonly Resource _material;
    private readonly int _quantity;

    public override string LogMessage => $"Improving shelter ({_type.ToString().ToLower()})";

    public NPCImproveShelter(ShelterImprovementType type, Resource material, int quantity = 1)
        : base($"Improving shelter ({type.ToString().ToLower()})", quantity * 10, ActivityType.Crafting)
    {
        _type = type;
        _material = material;
        _quantity = quantity;
    }

    protected override void OnComplete(NPC npc)
    {
        var shelter = npc.CurrentLocation.GetFeature<ShelterFeature>();
        if (shelter == null)
        {
            npc.Trace($"[NPC:{npc.Name}] No shelter to improve!");
            return;
        }

        if (npc.Inventory.Count(_material) < _quantity) return;
        // Consume materials
        for (int i = 0; i < _quantity; i++)
        {
            if (npc.Inventory!.Count(_material) > 0)
                npc.Inventory.Pop(_material);
        }

        // Apply improvement
        double improvement = shelter.Improve(_type, _material, _quantity);
        npc.Trace($"[NPC:{npc.Name}] Improved shelter {_type.ToString().ToLower()} by {improvement:P1} using {_material.ToDisplayName()}");
    }
}

#endregion