using text_survival.Combat;

using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actors;

public abstract class NPCAction(string name, int durationMin, ActivityType activityType)
{
    public string Name = name;
    public abstract string LogMessage { get; }
    public int DurationMinutes = durationMin;
    public int MinutesSpent = 0;
    public bool IsComplete() => MinutesSpent >= DurationMinutes;
    public abstract void Complete(NPC npc);
    public virtual void Interrupt(NPC npc) => Complete(npc); // can override for partial completion
    public ActivityType ActivityType = activityType;
}



public class NPCEat(Resource food, double amount) : NPCAction($"Eating {food.ToDisplayName()}", 5, ActivityType.Eating)
{
    public override string LogMessage => $"Eating {food.ToDisplayName()}";
    public override void Complete(NPC npc)
    {
        ConsumptionHandler.EatDrink(npc, food, amount);
    }
}

public class NPCMove(Location destination, NPC npc) :
    NPCAction($"Traveling to {destination.Name}", TravelProcessor.GetTraversalMinutes(npc.CurrentLocation, destination, npc, npc.Inventory), ActivityType.Traveling)
{
    public override string LogMessage => $"Traveling to {destination.Name}";
    public override void Complete(NPC npc)
    {
        // first update destination memory as leaving
        npc.ResourceMemory.RememberLocation(npc.CurrentLocation);
        npc.CurrentLocation = destination;
        npc.ResourceMemory.RememberLocation(destination);
    }
    public override void Interrupt(NPC npc)
    {
        if (MinutesSpent > (.5 * DurationMinutes)) // rough estimate
        {
            Complete(npc);
        }
        // otherwise they stay in the current location
    }
}

public class NPCForage(int minutes) : NPCAction("Foraging", minutes, ActivityType.Foraging)
{
    public override string LogMessage => "Foraging";
    public override void Complete(NPC npc)
    {
        bool hasLight = true; // todo
        var found = WorkHandler.Forage(npc, npc.Inventory, npc.CurrentLocation, MinutesSpent, hasLight);
        _ = npc.Inventory.CombineWithCapacity(found); // discard overflow
    }
}
public class NPCHarvest : NPCAction
{
    public override string LogMessage => "Harvesting";
    public NPCHarvest(int minutes) : base("Harvesting", minutes, ActivityType.Foraging) { }

    public override void Complete(NPC npc)
    {
        // Get first available harvestable (NPCs auto-select)
        var feature = WorkHandler.GetAvailableHarvestable(npc.CurrentLocation);
        if (feature == null)
        {
            Console.WriteLine($"[NPC:{npc.Name}] No harvestable at {npc.CurrentLocation.Name}");
            return;
        }

        // Check tool requirements (some harvestables need tools)
        if (feature.RequiredToolType != null)
        {
            var tool = npc.Inventory!.GetTool(feature.RequiredToolType.Value);
            if (!feature.MeetsToolRequirement(tool))
            {
                Console.WriteLine($"[NPC:{npc.Name}] Missing tool: {feature.GetToolRequirementDescription()}");
                return;
            }
        }

        // Execute harvest via WorkHandler
        var found = WorkHandler.Harvest(npc.CurrentLocation, MinutesSpent);

        // Add to NPC inventory (discard overflow)
        _ = npc.Inventory!.CombineWithCapacity(found);

        Console.WriteLine($"[NPC:{npc.Name}] Harvested {found.GetDescription()} from {feature.DisplayName}");
    }
}
public class NPCChopWood : NPCAction
{
    public override string LogMessage => "Chopping wood";
    public NPCChopWood(int minutes) : base("Chopping wood", minutes, ActivityType.Chopping) { }

    public override void Complete(NPC npc)
    {
        var feature = npc.CurrentLocation.GetFeature<WoodedAreaFeature>();
        if (feature == null || !feature.HasTrees)
        {
            Console.WriteLine($"[NPC:{npc.Name}] No trees at {npc.CurrentLocation.Name}");
            return;
        }

        // Check for working axe (required)
        var axe = npc.Inventory!.GetTool(ToolType.Axe);
        if (axe == null || axe.IsBroken)
        {
            Console.WriteLine($"[NPC:{npc.Name}] No working axe");
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
                Console.WriteLine($"[NPC:{npc.Name}] Inventory overflow - dropped {overflow.GetDescription()}");

            Console.WriteLine($"[NPC:{npc.Name}] Felled tree! Got {yield.GetDescription()}");
        }
        else
        {
            Console.WriteLine($"[NPC:{npc.Name}] Chopping: {feature.ProgressPct:P0}");
        }

        if (!axeStillWorks)
            Console.WriteLine($"[NPC:{npc.Name}] Axe broke!");
    }
}
public class NPCStartFire() : NPCAction("Starting Fire", 10, ActivityType.TendingFire)
{
    public override string LogMessage => "Starting fire";
    public override void Complete(NPC npc) => FireHandler.StartFire(npc, npc.Inventory!, npc.CurrentLocation);
}
public class NPCTendFire() : NPCAction("Tending Fire", 1, ActivityType.TendingFire)
{
    public override string LogMessage => "Tending fire";
    public override void Complete(NPC npc)
    {
        if (!npc.CurrentLocation.HasFeature<HeatSourceFeature>())
        {
            Console.WriteLine("Looks like the AI is broke! Trying to tend a fire where there is none!");
            return;
        }
        FireHandler.TendFire(npc.Inventory!, npc.CurrentLocation.GetFeature<HeatSourceFeature>()!);
    }
}
public class NPCRest(int minutes) : NPCAction("Resting", minutes, ActivityType.Resting)
{
    public override string LogMessage => "Resting";
    public override void Complete(NPC npc) { } // do nothing
}

public class NPCSleep(int minutes) : NPCAction("Sleeping", minutes, ActivityType.Sleeping)
{
    public override string LogMessage => "Sleeping";
    public override void Complete(NPC npc) => npc.Body.Rest(MinutesSpent, npc.CurrentLocation, null);
}

public class NPCStash(ResourceCategory resourceCategory) : NPCAction($"Storing {resourceCategory}", 2, ActivityType.Crafting)
{
    public override string LogMessage => $"Stashing {resourceCategory.ToString().ToLower()}";
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            Console.WriteLine("AI's BROKE. Trying to store items where there's no cache!");
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
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            Console.WriteLine("AI's BROKE. Trying to store water where there's no cache!");
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
    public override string LogMessage => $"Getting {toolType.ToString().ToLower()}";
    public override void Complete(NPC npc)
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

public class NPCCraft : NPCAction
{
    private readonly CraftOption _recipe;

    public override string LogMessage => $"Crafting {_recipe.Name.ToLower()}";

    public NPCCraft(CraftOption recipe) : base($"Crafting {recipe.Name}", recipe.CraftingTimeMinutes, ActivityType.Crafting)
    {
        _recipe = recipe;
    }

    public override void Complete(NPC npc)
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

    public override void Complete(NPC npc)
    {
        // Set combat cooldown to prevent re-detection
        npc.SetCombatCooldown(5);

        var ctx = npc.Game;
        List<Actor> enemies = _threat is Animal animal
            ? CombatOrchestrator.AnimalSide(ctx, animal)
            : [_threat];

        var result = CombatOrchestrator.ResolveHeadless(
            ctx, [npc], enemies, npc.CurrentLocation,
            startDistanceM: 5, AwarenessState.Engaged, AwarenessState.Engaged);
        Console.WriteLine($"[NPC] {npc.Name} vs {_threat.Name}: {result}");
    }

    public Actor Threat => _threat;
}

public class NPCFlee : NPCAction
{
    private readonly Actor _threat;

    public override string LogMessage => $"Fleeing from {_threat.Name}";

    public NPCFlee(Actor threat) : base($"Fleeing from {threat.Name}", 5, ActivityType.Traveling)
    {
        _threat = threat;
    }

    public override void Complete(NPC npc)
    {
        // Set combat cooldown to prevent immediate re-detection
        npc.SetCombatCooldown(5);

        // Move toward camp if known, else random adjacent
        Location? retreat = null;

        if (npc.Camp != null && npc.CurrentLocation != npc.Camp)
        {
            retreat = npc.Map.GetNextInPath(npc.CurrentLocation, npc.Camp);
        }

        if (retreat == null)
        {
            var options = npc.Map.GetTravelOptionsFrom(npc.CurrentLocation).ToList();
            if (options.Count > 0)
                retreat = Utils.GetRandomFromList(options);
        }

        if (retreat != null)
        {
            Console.WriteLine($"[NPC:{npc.Name}] Fleeing to {retreat.Name}");
            npc.CurrentLocation = retreat;
        }
        else
        {
            Console.WriteLine($"[NPC:{npc.Name}] Cannot flee - no escape route!");
        }
    }
}

public class NPCCookMeat : NPCAction
{
    public override string LogMessage => "Cooking meat";
    public NPCCookMeat() : base("Cooking meat", CookingHandler.CookMeatTimeMinutes, ActivityType.Cooking) { }

    public override void Complete(NPC npc)
    {
        CookingHandler.CookMeatNPC(npc, npc.Inventory!, npc.CurrentLocation);
    }
}
public class NPCMeltSnow : NPCAction
{
    public override string LogMessage => "Melting snow";
    public NPCMeltSnow() : base("Melting snow", CookingHandler.MeltSnowTimeMinutes, ActivityType.Cooking) { }

    public override void Complete(NPC npc)
    {
        CookingHandler.MeltSnowNPC(npc, npc.Inventory!, npc.CurrentLocation);
    }
}

public class NPCDrinkWater : NPCAction
{
    private readonly double _amount;

    public override string LogMessage => "Drinking water";

    public NPCDrinkWater(double amount = 0.5) : base("Drinking water", 2, ActivityType.Eating)
    {
        _amount = amount;
    }

    public override void Complete(NPC npc)
    {
        double consumed = npc.Inventory!.ConsumeByWeight(Resource.Water, _amount);
        if (consumed > 0)
        {
            npc.Body.AddHydration(consumed);
            Console.WriteLine($"[NPC:{npc.Name}] Drank {consumed:F1}L water");
        }
    }
}

#endregion

#region Shelter Actions

public class NPCImproveShelter : NPCAction
{
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

    public override void Complete(NPC npc)
    {
        var shelter = npc.CurrentLocation.GetFeature<ShelterFeature>();
        if (shelter == null)
        {
            Console.WriteLine($"[NPC:{npc.Name}] No shelter to improve!");
            return;
        }

        // Consume materials
        for (int i = 0; i < _quantity; i++)
        {
            if (npc.Inventory!.Count(_material) > 0)
                npc.Inventory.Pop(_material);
        }

        // Apply improvement
        double improvement = shelter.Improve(_type, _material, _quantity);
        Console.WriteLine($"[NPC:{npc.Name}] Improved shelter {_type.ToString().ToLower()} by {improvement:P1} using {_material.ToDisplayName()}");
    }
}

#endregion