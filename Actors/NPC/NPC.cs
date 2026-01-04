using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Actors;

public class NPC : Actor
{
    private static readonly NeedCraftingSystem CraftingSystem = new();

    public Personality Personality { get; set; }
    public Inventory Inventory { get; set; } = new();
    public Dictionary<Actor, double> Relationships { get; set; } = new();
    public ResourceMemory ResourceMemory { get; set; } = new();
    public Location? Camp { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NPCAction? CurrentAction { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    private SurvivalContext? _currentContext;

    public NeedType? CurrentNeed { get; set; }

    public override double AttackDamage => Inventory.Weapon?.Damage ?? .1;
    public override double BlockChance => Inventory.Weapon?.BlockChance ?? 0.05;
    public override string AttackName => Inventory.Weapon?.Name ?? "fists";
    public override DamageType AttackType => Inventory.Weapon?.WeaponClass switch
    {
        WeaponClass.Blade => DamageType.Sharp,
        WeaponClass.Pierce => DamageType.Pierce,
        WeaponClass.Blunt => DamageType.Blunt,
        _ => DamageType.Blunt
    };

    // For JSON deserialization
    public NPC() : base("", Body.BaselineHumanStats, null!, null!)
    {
        Personality = new Personality();
    }

    public NPC(string name, Personality personality, Location currentLocation, GameMap map)
        : base(name, Body.BaselineHumanStats, currentLocation, map)
    {
        Personality = personality;
    }

    // pending suggestion?
    // following?

    public override void Update(int minutes, SurvivalContext context)
    {
        base.Update(minutes, context);
        _currentContext = context; // Store for decision-making methods
        for (int i = 0; i < minutes; i++)
        {
            // Console.WriteLine($"[NPC:{Name}] Tick {i + 1}/{minutes} - Action={CurrentAction?.Name ?? "none"}, Need={CurrentNeed?.ToString() ?? "none"}");

            // if interrupt, clear action and let DetermineNeed pick it up
            if (ShouldInterrupt())
            {
                CurrentAction?.Interrupt(this);
                CurrentAction = null;
                CurrentNeed = null;
            }

            // continue action if there is one
            if (CurrentAction != null)
            {
                ContinueAction();
                return;
            }
            // otherwise we need to pick a new action

            // check if need is met
            if (IsCriticalNeedSatisfied())
            {
                CurrentNeed = null;
            }

            // if we have no need - pick one
            if (CurrentNeed == null)
            {
                DetermineNeed();
            }

            // pick action and do it
            CurrentAction = DetermineActionForNeed(context);
            Console.WriteLine($"[NPC:{Name}] Picked: {CurrentAction?.Name} for need {CurrentNeed}");
            ContinueAction();
        }
    }
    internal bool ShouldInterrupt()
    {
        // TODO: Fix threat response - needs threatTarget and threat parameters
        // if (DecideRespondToThreat())
        //     return true;

        // critical needs interrupt if higher priority 
        var minimumCriticalNeed = NeedType.Food;
        var need = GetCriticalNeed();
        if (need == null || need == CurrentNeed) return false;

        // Already handling a critical need? Don't interrupt with another critical.
        if (CurrentNeed != null && CurrentNeed <= minimumCriticalNeed)
            return false;

        // todo check player interrupts/suggestions

        return true;
    }
    private void ContinueAction()
    {
        if (CurrentAction == null) throw new NullReferenceException("Something's fucked"); // should never happen
        CurrentAction.MinutesSpent++;
        // check for completion
        if (CurrentAction.IsComplete())
        {
            Console.WriteLine($"[NPC:{Name}] Completed: {CurrentAction.Name} ({CurrentAction.MinutesSpent}/{CurrentAction.DurationMinutes} min)");
            CurrentAction.Complete(this);
            int minutesLeftover = CurrentAction.MinutesSpent - CurrentAction.DurationMinutes;
            CurrentAction = null;
        }
    }
    private NPCAction DetermineActionForNeed(SurvivalContext context)
    {
        Console.WriteLine($"  [Warmth] Determining action for need: {CurrentNeed}");
        if (CurrentNeed == NeedType.Warmth)
        {
            bool atActiveFire = CurrentLocation.HasActiveHeatSource();
            bool atDeadFire = !atActiveFire && CurrentLocation.HasFeature<HeatSourceFeature>();
            bool notAtFire = !(atActiveFire || atDeadFire);

            bool isWarming = Body.LastTemperatureDelta > 0;
            bool needToStartFire = atDeadFire;
            bool needToTendFire = false;

            // either at active fire
            // at dead fire
            // not at fire
            if (notAtFire)
            {
                var knownActiveFire = GetKnownActiveFire();
                Console.WriteLine($"  [Warmth] Known fire: {knownActiveFire?.Name ?? "none"}");
                // known fire -> go there
                if (knownActiveFire != null)
                {
                    Console.WriteLine($"  [Warmth] Going to fire at {knownActiveFire.Name}");
                    var move = DecideToMove(knownActiveFire);
                    if (move != null) return move;
                }
                else if (Camp != null) // if no known fire prefer to make it at camp
                {
                    Console.WriteLine($"  [Warmth] Going to camp");
                    var move = DecideToMove(Camp, maxTiles: 4); // only if close
                    if (move != null) return move;
                }
                // no known fire and camp is far
                needToStartFire = true;
            }

            // if at fire then wait
            if (atActiveFire)
            {
                if (isWarming)
                {
                    // Rest near fire to get fire proximity heat bonus
                    Console.WriteLine($"  [Warmth] At fire, resting");
                    return new NPCRest(Utils.RandInt(5, 15));
                }
                else
                {
                    needToTendFire = true;
                }
            }

            if (needToTendFire)
            {
                // at fire but still cooling -> add fuel if you can
                var fireFeature = CurrentLocation.GetFeature<HeatSourceFeature>()!;
                if (FireHandler.CanTendFire(Inventory, fireFeature))
                {
                    Console.WriteLine($"  [Warmth] Tending fire");
                    return new NPCTendFire();
                }

                if (!FireHandler.CanTendFire(Inventory, fireFeature))
                {
                    Console.WriteLine($"  [Warmth] Getting fuel");
                    var get = DetermineGetResource(ResourceCategory.Fuel);
                    if (get != null) return get;
                }
            }

            // at fire but not lit -> light it
            if (needToStartFire)
            {
                var hasTool = FireHandler.GetBestTool(Inventory) != null;
                Console.WriteLine($"  [Warmth] Has fire tool: {hasTool}");
                if (FireHandler.CanStartFire(Inventory))
                {
                    Console.WriteLine($"  [Warmth] Can start fire, starting");
                    return new NPCStartFire();
                }
                // No fire-starting tool? Try to craft one before gathering resources
                if (!hasTool)
                {
                    Console.WriteLine($"  [Warmth] No tool, trying to craft");
                    var craft = DetermineCraft();
                    if (craft != null)
                    {
                        Console.WriteLine($"  [Warmth] Crafting: {craft.Name}");
                        return craft;
                    }
                    Console.WriteLine($"  [Warmth] Can't craft, skipping fire materials");
                    // Can't craft tool - don't gather fire materials we can't use
                }
                else
                {
                    if (FireHandler.GetFireMaterials(Inventory).Tinders.Count < 1)
                    {
                        Console.WriteLine($"  [Warmth] Getting tinder");
                        var get = DetermineGetResource(ResourceCategory.Tinder);
                        if (get != null) return get;
                    }
                    if (!FireHandler.GetFireMaterials(Inventory).HasKindling)
                    {
                        Console.WriteLine($"  [Warmth] Getting fuel");
                        var get = DetermineGetResource(ResourceCategory.Fuel);
                        if (get != null) return get;
                    }
                }
            }
            Console.WriteLine($"  [Warmth] Falling through to work/craft");
        }
        else if (CurrentNeed == NeedType.Water)
        {
            if (HasResource(ResourceCategory.Water))
            {
                var water = Inventory.FindAnyResourceInCategory(ResourceCategory.Water);
                return new NPCEat(water, Inventory.Pop(water));
            }

            var get = DetermineGetResource(ResourceCategory.Water);
            if (get != null) return get;
        }
        else if (CurrentNeed == NeedType.Rest)
        {
            var sleep = DecideSleep();
            if (sleep != null)
                return sleep;
        }
        else if (CurrentNeed == NeedType.Food)
        {
            if (HasResource(ResourceCategory.Food))
            {
                var food = Inventory.FindAnyResourceInCategory(ResourceCategory.Food);
                return new NPCEat(food, Inventory.Pop(food));
            }
            var get = DetermineGetResource(ResourceCategory.Food);
            if (get != null) return get;
        }

        var action = DetermineWork();
        action ??= DetermineCraft();
        action ??= DetermineIdle(context);

        return action;
    }

    private NPCMove? DecideToMove(Location destination, int maxTiles = 100)
    {
        if (destination == CurrentLocation) throw new Exception("You can't move to current location");
        int distanceTo = Map.DistanceBetween(CurrentLocation, destination);
        if (distanceTo <= maxTiles) // tiles
        {
            var nextLoc = Map.GetNextInPath(CurrentLocation, destination);
            if (nextLoc != null)
            {
                Console.WriteLine($"  [Moving] Going to {destination.Name}");
                return new NPCMove(nextLoc, this);
            }
            Console.WriteLine($"Can't move to {destination} - no path");
        }
        else
        {
            Console.WriteLine($"Can't move to {destination} - too far");
        }
        return null;
    }

    private Location? GetKnownActiveFire()
    {
        if (CurrentLocation.HasActiveHeatSource()) return CurrentLocation;
        if (Camp?.HasActiveHeatSource() ?? false) return Camp;
        var remembered = ResourceMemory.GetClosestActiveFire(CurrentLocation, Map);
        if (remembered != null) return remembered;
        return null;
    }

    internal bool IsCriticalNeedSatisfied()
    {
        return CurrentNeed switch
        {
            NeedType.Warmth => Body.WarmPct > .7,
            NeedType.Water => Body.HydratedPct > .5,
            NeedType.Rest => Body.EnergyPct > .5,
            NeedType.Food => Body.FullPct > .3,
            _ => true,
        };
    }

    /// <summary>
    /// Check if NPC can survive an activity away from fire for the specified duration.
    /// Returns false if the activity would drop warmth to dangerous levels.
    /// </summary>
    private bool CanSurviveAwayFromFire(int durationMinutes)
    {
        if (_currentContext == null) return true;

        // Simulate being away from fire by zeroing the fire bonus
        double originalFireBonus = _currentContext.FireProximityBonus;
        _currentContext.FireProximityBonus = 0;

        double tempChangePerHour = SurvivalProcessor.CalculateTemperatureChangePerHour(Body, _currentContext);

        // Restore original value
        _currentContext.FireProximityBonus = originalFireBonus;

        // If warming or stable even without fire, always safe
        if (tempChangePerHour >= 0) return true;

        // Project temperature after activity
        double hoursNeeded = durationMinutes / 60.0;
        double tempLoss = Math.Abs(tempChangePerHour) * hoursNeeded;
        double projectedTemp = Body.BodyTemperature - tempLoss;

        // Calculate projected warmth percentage
        double projectedWarmPct = (projectedTemp - SurvivalProcessor.HypothermiaThreshold)
            / (Body.BASE_BODY_TEMP - SurvivalProcessor.HypothermiaThreshold);

        // Require 30% warmth buffer
        bool canSurvive = projectedWarmPct > 0.3;

        if (!canSurvive)
        {
            Console.WriteLine($"  [Survival] {durationMinutes}min away from fire would drop warmth to {projectedWarmPct:P0} - too dangerous");
        }

        return canSurvive;
    }

    private void DetermineNeed()
    {
        if (DetermineCriticalNeed())
            return;
        if (DecideSatisfyNeed())
            return;
    }

    private bool DecideRespondToThreat(Actor threatTarget, Actor threat)
    {
        bool canFight = Inventory.HasWeapon && Vitality > .5;
        double fightChance = Personality.Boldness;
        double baseWillingness = ((threatTarget == this? 2 : Relationships[threatTarget]) + 1.0) / 2.0;

        return false;
    }
    private bool DetermineCriticalNeed()
    {
        var need = GetCriticalNeed();
        if (need == null) return false;
        CurrentNeed = need;
        return true;
    }
    internal NeedType? GetCriticalNeed()
    {
        if (Body.WarmPct < .25)
            return NeedType.Warmth;
        if (Body.HydratedPct < .2)
            return NeedType.Water;
        if (Body.EnergyPct < .1)
            return NeedType.Rest;
        if (Body.FullPct < .05)
            return NeedType.Food;
        return null;
    }
    private bool DecideSatisfyNeed()
    {
        NeedType? need = null;
        if (Body.WarmPct < .5)
            need = NeedType.Warmth;
        if (Body.HydratedPct < .5)
            need = NeedType.Water;
        if (Body.EnergyPct < .3) // todo check for night
            need = NeedType.Rest;
        if (Body.FullPct < .05)
            need = NeedType.Food;

        if (need == null) return false;
        CurrentNeed = need;
        return true;
    }
    private bool HasResource(ResourceCategory category) => Inventory.Has(category);

    /// <summary>
    /// Search for a SPECIFIC resource (e.g., Stone, not just any Material).
    /// </summary>
    private NPCAction? DetermineGetSpecificResource(Resource resource)
    {
        Console.WriteLine($"    [GetResource] Looking for specific: {resource} at {CurrentLocation.Name}");

        // can't gather if inv already full
        var invFull = DealWithFullInventory();
        if (invFull != null)
        {
            Console.WriteLine($"    [GetResource] Inventory full, returning early");
            return invFull;
        }

        // in tile -> work (if this location has this specific resource)
        var forage = CurrentLocation.GetFeature<ForageFeature>();
        Console.WriteLine($"    [GetResource] ForageFeature: {(forage != null ? "yes" : "no")}, NearlyDepleted: {forage?.IsNearlyDepleted()}");
        if (forage != null)
        {
            var provided = forage.ProvidedResources();
            Console.WriteLine($"    [GetResource] Provided resources: [{string.Join(", ", provided)}]");
        }
        if (forage != null && !forage.IsNearlyDepleted() &&
            forage.ProvidedResources().Contains(resource))
        {
            int forageTime = Utils.RandInt(15, 60);
            // Check if we can survive foraging in current conditions
            if (!CanSurviveAwayFromFire(forageTime))
                return null;
            Console.WriteLine($"    [GetResource] Found {resource} at current location, foraging");
            return new NPCForage(forageTime);
        }

        // in adjacent -> move to location that has this specific resource
        var adjacentLocations = Map.GetTravelOptionsFrom(CurrentLocation).ToList();
        Console.WriteLine($"    [GetResource] Adjacent locations: {string.Join(", ", adjacentLocations.Select(l => l.Name))}");
        foreach (var adj in adjacentLocations)
        {
            var adjResources = GetAccessibleResources(adj);
            Console.WriteLine($"    [GetResource]   {adj.Name} has: [{string.Join(", ", adjResources)}]");
        }
        var adjacentWithResource = adjacentLocations
            .Where(loc => GetAccessibleResources(loc).Contains(resource))
            .ToList();
        Console.WriteLine($"    [GetResource] Adjacent with {resource}: {adjacentWithResource.Count}");

        var locWithResource = adjacentWithResource.Count > 0
            ? Utils.GetRandomFromList(adjacentWithResource)
            : null;

        // in memory -> move towards remembered location with this resource
        var remembered = ResourceMemory.WhereIs(resource).FirstOrDefault();
        if (remembered != null)
        {
            Console.WriteLine($"    [GetResource] Remembered location with {resource}: {remembered.Name}");
            locWithResource ??= remembered;
        }

        // unknown? -> (bold check?) move random (explore)
        if (locWithResource == null && Utils.DetermineSuccess(Personality.Boldness))
        {
            Console.WriteLine($"    [GetResource] No known location, exploring");
            locWithResource = Utils.GetRandomFromList(Map.GetTravelOptionsFrom(CurrentLocation).ToList());
        }

        if (locWithResource != null)
        {
            // Check if we can survive traveling in current conditions
            int estimatedTravelMinutes = 10;
            if (!CanSurviveAwayFromFire(estimatedTravelMinutes))
            {
                Console.WriteLine($"    [GetResource] Too dangerous to travel");
                return null;
            }

            Console.WriteLine($"    [GetResource] Moving to {locWithResource.Name}");
            var move = DecideToMove(locWithResource);
            if (move != null) return move;
        }

        Console.WriteLine($"    [GetResource] Could not find {resource}");
        return null;
    }

    private NPCAction? DetermineGetResource(ResourceCategory category, bool allowCamp = true)
    {
        Console.WriteLine($"    [GetResource] Looking for category: {category}");

        // can't gather if inv already full
        var invFull = DealWithFullInventory();
        if (invFull != null)
            return invFull;

        // in tile -> work
        var work = GetResourceAtCurrentLocation(category);
        if (work != null) return work;
        // in adjacent -> move to (filter by accessible resources, pick random)
        var adjacentWithResource = Map.GetTravelOptionsFrom(CurrentLocation)
           .Where(loc => GetAccessibleResources(loc).Any(r => ResourceCategories.Items[category].Contains(r)))
           .ToList();
        var locWithResource = adjacentWithResource.Count > 0
            ? Utils.GetRandomFromList(adjacentWithResource)
            : null;

        // in sight -> move towards - should get handled by memory
        // in memory -> move towards
        var remembered = GetClosestKnownResource(category);
        if (remembered != null)
            locWithResource ??= Map.GetNextInPath(CurrentLocation, remembered);

        // unknown? -> (bold check?) move random (explore)
        if (Utils.DetermineSuccess(Personality.Boldness))
            locWithResource ??= Utils.GetRandomFromList(Map.GetTravelOptionsFrom(CurrentLocation).ToList());

        if (locWithResource != null)
        {
            // Check if we can survive traveling in current conditions
            // Estimate travel time based on distance (rough estimate: 10 min per tile)
            int estimatedTravelMinutes = 10;
            if (!CanSurviveAwayFromFire(estimatedTravelMinutes))
            {
                Console.WriteLine($"    [GetResource] Too dangerous to travel");
                return null;
            }

            Console.WriteLine($"    [GetResource] Moving to {locWithResource.Name}");
            return new NPCMove(locWithResource, this);
        }
        // otherwise wait
        return null;
    }
    internal Location? GetClosestKnownResource(ResourceCategory category)
    {
        if (Map == null) return null;

        var resources = ResourceCategories.Items[category];
        var knownLocations = resources
            .SelectMany(r => ResourceMemory.WhereIs(r))
            .Distinct()
            .ToList();

        if (knownLocations.Count == 0) return null;

        return knownLocations
            .Select(loc => (loc, pos: Map.GetPosition(loc)))
            .OrderBy(x => Map.GetPosition(CurrentLocation).ManhattanDistance(x.pos))
            .FirstOrDefault().loc;
    }
    private List<Resource> GetAccessibleResources(Location location)
    {
        var resources = new List<Resource>();

        // ForageFeature - always accessible
        var forage = location.GetFeature<ForageFeature>();
        if (forage != null && !forage.IsNearlyDepleted())
            resources.AddRange(forage.ProvidedResources());

        // HarvestableFeature - check tool requirements
        var harvestable = WorkHandler.GetAvailableHarvestable(location);
        if (harvestable != null && harvestable.CanBeHarvested())
        {
            // Check if NPC has required tool (if any)
            if (harvestable.RequiredToolType != null)
            {
                var tool = Inventory.GetTool(harvestable.RequiredToolType.Value);
                if (!harvestable.MeetsToolRequirement(tool))
                    goto skipHarvest; // Skip - can't harvest without tool
            }
            resources.AddRange(harvestable.ProvidedResources());
        }
    skipHarvest:

        // WoodedAreaFeature - requires working axe
        var wooded = location.GetFeature<WoodedAreaFeature>();
        if (wooded != null && wooded.HasTrees)
        {
            var axe = Inventory.GetTool(ToolType.Axe);
            if (axe?.Works == true)
                resources.AddRange(wooded.ProvidedResources());
        }

        // WaterFeature - can collect water if conditions allow
        var water = location.GetFeature<WaterFeature>();
        if (water != null)
            resources.AddRange(water.ProvidedResources());

        return resources.Distinct().ToList();
    }

    private NPCAction? GetResourceAtCurrentLocation(ResourceCategory category)
    {
        var targetResources = ResourceCategories.Items[category];

        // ForageFeature - always accessible
        var forage = CurrentLocation.GetFeature<ForageFeature>();
        if (forage != null && !forage.IsNearlyDepleted() &&
            forage.ProvidedResources().Any(r => targetResources.Contains(r)))
        {
            int forageTime = Utils.RandInt(15, 60);
            // Check if we can survive foraging in current conditions
            if (!CanSurviveAwayFromFire(forageTime))
                return null;
            return new NPCForage(forageTime);
        }

        // HarvestableFeature - check tool requirements
        var harvestable = WorkHandler.GetAvailableHarvestable(CurrentLocation);
        if (harvestable != null && harvestable.CanBeHarvested() &&
            harvestable.ProvidedResources().Any(r => targetResources.Contains(r)))
        {
            // Check tool requirement
            if (harvestable.RequiredToolType != null)
            {
                var tool = Inventory.GetTool(harvestable.RequiredToolType.Value);
                if (!harvestable.MeetsToolRequirement(tool))
                {
                    // Need tool - try to craft/find it
                    return DetermineGetTool(harvestable.RequiredToolType.Value);
                }
            }

            // Calculate work time (harvest may complete in one session or require multiple)
            int workTime = Math.Min(60, harvestable.GetTotalMinutesToHarvest());
            if (workTime > 0)
            {
                // Check if we can survive harvesting in current conditions
                if (!CanSurviveAwayFromFire(workTime))
                    return null;
                return new NPCHarvest(workTime);
            }
        }

        // WoodedAreaFeature - requires working axe
        var wooded = CurrentLocation.GetFeature<WoodedAreaFeature>();
        if (wooded != null && wooded.HasTrees &&
            wooded.ProvidedResources().Any(r => targetResources.Contains(r)))
        {
            var axe = Inventory.GetTool(ToolType.Axe);
            if (axe == null || !axe.Works)
            {
                // Need axe - try to craft/find it
                return DetermineGetTool(ToolType.Axe);
            }

            // Calculate work time based on remaining progress
            double remainingMinutes = wooded.MinutesToFell - wooded.MinutesWorked;
            int workTime = (int)Math.Min(60, Math.Max(30, remainingMinutes));

            // Check if we can survive chopping in current conditions
            if (!CanSurviveAwayFromFire(workTime))
                return null;

            return new NPCChopWood(workTime);
        }

        return null;
    }
    internal NPCAction? DetermineWork()
    {
        // Stockpile resources if camp doesn't have enough
        if (!IsEnoughStockpiled(ResourceCategory.Fuel))
        {
            return Stockpile(ResourceCategory.Fuel);
        }
        else if (!IsEnoughStockpiled(ResourceCategory.Water))
        {
            return Stockpile(ResourceCategory.Water);
        }
        else if (!IsEnoughStockpiled(ResourceCategory.Food))
        {
            return Stockpile(ResourceCategory.Food);
        }
        else return null;
    }
    internal NPCAction? Stockpile(ResourceCategory resource)
    {
        // if at camp and have stuff -> store in cache
        if (CurrentLocation == Camp && Inventory.GetWeight(resource) >= 1.0)
        {
            return new NPCStash(resource);
        }
        // if inv full empty it first
        var invFull = DealWithFullInventory();
        if (invFull != null)
        {
            return invFull;
        }
        // else -> get resource ! at camp
        return DetermineGetResource(resource, allowCamp: false);
    }
    internal NPCAction? DealWithFullInventory()
    {
        Console.WriteLine($"    [InvCheck] Current: {Inventory.CurrentWeightKg:F2}kg, Max: {Inventory.MaxWeightKg:F2}kg, Threshold: {Inventory.MaxWeightKg * .9:F2}kg");
        // if inv full -> return to camp
        if (Inventory.CurrentWeightKg > Inventory.MaxWeightKg * .9)
        {
            Console.WriteLine($"    [InvCheck] Inventory full! At camp: {CurrentLocation == Camp}");
            if (Camp != null && CurrentLocation != Camp)
            {
                Console.WriteLine($"    [InvCheck] Returning to camp");
                var move = DecideToMove(Camp);
                if (move != null) return move;
            }
            else if (Camp != null && CurrentLocation == Camp)
            {
                var resourceTypes = Inventory.GetResourceTypes();
                if (resourceTypes.Count > 0)
                {
                    var heaviestResource = resourceTypes.OrderByDescending(x => Inventory.Weight(x)).First();
                    var heaviest = heaviestResource.GetCategory();
                    if (heaviest != null)
                    {
                        Console.WriteLine($"    [InvCheck] Stashing {heaviest}");
                        return new NPCStash((ResourceCategory)heaviest);
                    }
                }
                // Try stashing water if no resources left
                if (Inventory.WaterLiters > 0)
                {
                    Console.WriteLine($"    [InvCheck] Stashing Water ({Inventory.WaterLiters:F1}L)");
                    return new NPCStashWater();
                }
                // Only tools/equipment remain - can't stash, continue with tasks
                Console.WriteLine($"    [InvCheck] At camp, inv full, only tools/equipment - continuing");
                return null;
            }
        }
        return null;
    }
    private bool CampHas(ResourceCategory resourceCat) => Cache?.Has(resourceCat) ?? false;
    private Inventory? Cache => Camp?.GetFeature<CacheFeature>()?.Storage;
    internal bool IsEnoughStockpiled(ResourceCategory resource)
    {
        int DAYS_RESERVE = 2;
        int PEOPLE_AT_CAMP = 1; // todo add property to location
        if (Cache is null) return false;

        // Water is tracked via WaterLiters, not in resource stacks
        bool hasResource = resource == ResourceCategory.Water
            ? Cache.WaterLiters > 0
            : CampHas(resource);
        if (!hasResource) return false;

        double neededPerPersonDay = resource switch
        {
            ResourceCategory.Fuel => 20,
            ResourceCategory.Tinder => .1,
            ResourceCategory.Food => 1,
            ResourceCategory.Water => 3, // 3 liters
            ResourceCategory.Medicine => .1,
            ResourceCategory.Material => 0,
            ResourceCategory.Log => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
        int target = (int)(DAYS_RESERVE * PEOPLE_AT_CAMP * neededPerPersonDay);

        double currentAmount = resource == ResourceCategory.Water
            ? Cache.WaterLiters
            : Cache.GetWeight(resource);

        return currentAmount >= target;
    }
    internal NPCAction? DetermineCraft()
    {
        // Priority based on current need (per npc-plan.md GET_CRAFT_DESIRE)
        NeedCategory? category = CurrentNeed switch
        {
            NeedType.Warmth => NeedCategory.FireStarting,
            NeedType.Food => NeedCategory.HuntingWeapon,
            _ => null
        };
        Console.WriteLine($"Need {category}");
        if (category == null) return null;

        return TryCraftFromCategory(category.Value);
    }

    private NPCAction? TryCraftFromCategory(NeedCategory category)
    {
        Console.WriteLine($"    [Craft] TryCraftFromCategory({category})");
        var options = CraftingSystem.GetOptionsForNeed(category, Inventory, true);
        Console.WriteLine($"    [Craft] Options count: {options.Count()}");

        // Try to craft if we can
        var craftable = options.FirstOrDefault(o => o.CanCraft(Inventory));
        if (craftable != null)
        {
            Console.WriteLine($"    [Craft] Can craft: {craftable.Name}");
            return new NPCCraft(craftable);
        }

        // Can't craft - find first missing thing and resolve it
        foreach (var option in options)
        {
            Console.WriteLine($"    [Craft] Checking option: {option.Name}");

            // Check missing tools FIRST - need tools before gathering materials
            foreach (var toolType in option.RequiredTools)
            {
                var tool = Inventory.GetTool(toolType);
                Console.WriteLine($"    [Craft]   Required tool: {toolType}, have: {tool?.Name ?? "none"}");
                if (tool == null || tool.Durability < 1)
                {
                    return DetermineGetTool(toolType);
                }
            }

            // Then check missing materials
            foreach (var req in option.Requirements)
            {
                var needed = GetMissingCount(req);
                Console.WriteLine($"    [Craft]   Req: {req.Material}, need {req.Count}, missing {needed}");
                if (needed > 0)
                {
                    // Specific resource → search for that exact resource
                    if (req.Material is MaterialSpecifier.Specific(var resource))
                    {
                        Console.WriteLine($"    [Craft]   Getting specific resource: {resource}");
                        return DetermineGetSpecificResource(resource);
                    }
                    // Category → search for any resource in that category
                    else if (req.Material is MaterialSpecifier.Category(var resCat))
                    {
                        Console.WriteLine($"    [Craft]   Getting category: {resCat}");
                        return DetermineGetResource(resCat);
                    }
                }
            }
        }
        Console.WriteLine($"    [Craft] No craftable options found");
        return null;
    }

    private int GetMissingCount(MaterialRequirement req) => req.Material switch
    {
        MaterialSpecifier.Specific(var r) => Math.Max(0, req.Count - Inventory.Count(r)),
        MaterialSpecifier.Category(var c) => Math.Max(0, req.Count - Inventory.GetCount(c)),
        _ => 0
    };

    private static NeedCategory? GetCategoryForTool(ToolType toolType) => toolType switch
    {
        ToolType.Knife => NeedCategory.CuttingTool,
        ToolType.KnappingStone => NeedCategory.CuttingTool,
        ToolType.Axe => NeedCategory.CuttingTool,
        _ => null
    };

    private NPCAction? DetermineGetTool(ToolType toolType)
    {
        Console.WriteLine($"    [GetTool] Looking for {toolType}");

        // Check cache for this tool
        var cache = Camp?.GetFeature<CacheFeature>()?.Storage;
        var cachedTool = cache?.Tools.FirstOrDefault(t => t.ToolType == toolType && t.Works);

        if (cachedTool != null)
        {
            Console.WriteLine($"    [GetTool] Found {cachedTool.Name} in cache");
            if (CurrentLocation == Camp)
                return new NPCTakeToolFromCache(toolType);
            else if (Camp != null)
            {
                var move = DecideToMove(Camp);
                if (move != null) return move;
            }
        }

        // Not in cache, try to craft
        var toolCategory = GetCategoryForTool(toolType);
        Console.WriteLine($"    [GetTool] Not in cache, crafting category: {toolCategory}");
        if (toolCategory != null)
            return TryCraftFromCategory(toolCategory.Value);

        return null;
    }

    private NPCAction DetermineIdle(SurvivalContext context)
    {
        if (context.IsNight && Body.EnergyPct < .8)
        {
            var sleep = DecideSleep();
            if (sleep != null) return sleep;
        }

        // todo
        // follow high relationship actors

        // Weighted random idle action
        // options = WeightedList:
        //     { REST_NEAR_FIRE,   50 }
        //     { SIT_AND_WATCH,    20 }
        //     { TEND_FIRE_ANYWAY, 15 }
        //     { WANDER_NEARBY,    10 }
        //     { CHECK_ON_OTHERS,   5 }

        // if context.Time.IsNight and CAN_SLEEP(npc, context):
        //     options.Add(SLEEP, 40)

        return new NPCRest(Utils.RandInt(5, 30));
    }
    private NPCSleep? DecideSleep()
    {
        if (CanSleep())
        {
            return new NPCSleep(Utils.RandInt(30, 90)); // sleep in segments to wake up to tend to fire
        }
        return null;
    }
    internal bool CanSleep()
    {
        // Must be at camp
        if (CurrentLocation != Camp) return false;
        // Not freezing
        if (Body.WarmPct < .2) return false;
        // Fire has runway of 2 hours
        if (CurrentLocation.HasActiveHeatSource() && CurrentLocation.GetFeature<HeatSourceFeature>()!.BurningHoursRemaining < 2)
            return false;
        // TODO: Fix threat response - needs threatTarget and threat parameters
        // No threats
        // if (DecideRespondToThreat()) return false;

        return true;
    }

    public double GetRelationship(Actor other)
    {
        return Relationships.TryGetValue(other, out var value) ? value : 0;
    }
    public void ModifyRelationship(Actor other, double delta)
    {
        double current = GetRelationship(other);
        Relationships[other] = Math.Clamp(current + delta, -1, 1);
    }

    #region Combat Decisions

    /// <summary>
    /// Decides if NPC will join combat to help another actor.
    /// Uses relationship + self-assessment + threat assessment.
    /// </summary>
    internal bool DecideToHelpInCombat(Actor ally, Animal threat)
    {
        double relationship = GetRelationship(ally);
        if (relationship < -0.3) return false;  // Hostile NPCs won't help

        // Base willingness from relationship: -1..1 → 0..1
        double baseWillingness = (relationship + 1) / 2;

        // Self-assessment
        bool hasWeapon = Inventory.Weapon != null;
        bool isInjured = Vitality < 0.7;
        double selfFactor = (hasWeapon ? 1.2 : 0.6) * (isInjured ? 0.7 : 1.0);

        // Personality
        double boldnessFactor = 0.5 + (Personality.Boldness * 0.5);

        // Threat assessment (bigger/healthier = scarier)
        double threatFactor = 1.0 / (1.0 + threat.Vitality * threat.Body.WeightKG / 100);

        double joinScore = baseWillingness * selfFactor * boldnessFactor * threatFactor;

        // Threshold: 0.35 means neutral + armed + bold = likely joins
        return joinScore > 0.35;
    }

    /// <summary>
    /// Decides fight vs flee when NPC faces threat alone.
    /// Returns true for fight, false for flee.
    /// </summary>
    internal bool DecideFlightOrFight(Animal threat)
    {
        double fightChance = Personality.Boldness;

        // Equipment check
        bool hasWeapon = Inventory.Weapon != null;
        bool isInjured = Vitality < 0.7;

        if (hasWeapon && !isInjured) fightChance += 0.2;
        if (isInjured) fightChance -= 0.2;

        // Threat comparison
        double npcStrength = Vitality * (hasWeapon ? 2.0 : 1.0);
        double threatStrength = threat.Vitality * (threat.Body.WeightKG / 30.0);

        if (threatStrength > npcStrength * 1.5) fightChance -= 0.3;  // Much stronger
        if (threatStrength < npcStrength * 0.5) fightChance += 0.3;  // Much weaker

        fightChance = Math.Clamp(fightChance, 0.1, 0.9);
        return Utils.DetermineSuccess(fightChance);
    }

    /// <summary>
    /// Check for hostile predators at NPC's current location.
    /// </summary>
    internal Animal? GetThreatAtLocation(HerdRegistry herds)
    {
        var position = Map.GetPosition(CurrentLocation);
        var herdsHere = herds.GetHerdsAt(position);
        var predatorHerd = herdsHere.FirstOrDefault(h => h.IsPredator);
        return predatorHerd?.GetRandomMember();
    }

    #endregion
}

public class Personality
{
    public double Boldness;
    public double Selfishness;
    public double Sociability;
}

public abstract class NPCAction(string name, int durationMin, ActivityType activityType)
{
    public string Name = name;
    public int DurationMinutes = durationMin;
    public int MinutesSpent = 0;
    public bool IsComplete() => MinutesSpent >= DurationMinutes;
    public abstract void Complete(NPC npc);
    public virtual void Interrupt(NPC npc) => Complete(npc); // can override for partial completion
    public ActivityType ActivityType = activityType;
}

public enum NeedType
{
    Warmth = 0,
    Water = 1,
    Rest = 2,
    Food = 3,
    None = 4,
}


public class NPCEat(Resource food, double amount) : NPCAction($"Eating {food.ToDisplayName()}", 5, ActivityType.Eating)
{
    public override void Complete(NPC npc)
    {
        ConsumptionHandler.EatDrink(npc, food, amount);
    }
}

public class NPCMove(Location destination, NPC npc) :
    NPCAction($"Traveling to {destination.Name}", TravelProcessor.GetTraversalMinutes(npc.CurrentLocation, destination, npc, npc.Inventory), ActivityType.Traveling)
{
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
    public override void Complete(NPC npc)
    {
        bool hasLight = true; // todo
        var found = WorkHandler.Forage(npc, npc.Inventory, npc.CurrentLocation, MinutesSpent, hasLight);
        _ = npc.Inventory.CombineWithCapacity(found); // discard overflow
    }
}
public class NPCHarvest : NPCAction
{
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
            var tool = npc.Inventory.GetTool(feature.RequiredToolType.Value);
            if (!feature.MeetsToolRequirement(tool))
            {
                Console.WriteLine($"[NPC:{npc.Name}] Missing tool: {feature.GetToolRequirementDescription()}");
                return;
            }
        }

        // Execute harvest via WorkHandler
        var found = WorkHandler.Harvest(npc.CurrentLocation, MinutesSpent);

        // Add to NPC inventory (discard overflow)
        _ = npc.Inventory.CombineWithCapacity(found);

        Console.WriteLine($"[NPC:{npc.Name}] Harvested {found.GetDescription()} from {feature.DisplayName}");
    }
}
public class NPCChopWood : NPCAction
{
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
        var axe = npc.Inventory.GetTool(ToolType.Axe);
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
    public override void Complete(NPC npc) => FireHandler.StartFire(npc, npc.Inventory, npc.CurrentLocation);
}
public class NPCTendFire() : NPCAction("Tending Fire", 1, ActivityType.TendingFire)
{
    public override void Complete(NPC npc)
    {
        if (!npc.CurrentLocation.HasFeature<HeatSourceFeature>())
        {
            Console.WriteLine("Looks like the AI is broke! Trying to tend a fire where there is none!");
            return;
        }
        FireHandler.TendFire(npc.Inventory, npc.CurrentLocation.GetFeature<HeatSourceFeature>()!);
    }
}
public class NPCRest(int minutes) : NPCAction("Resting", minutes, ActivityType.Resting)
{
    public override void Complete(NPC npc) { } // do nothing
}

public class NPCSleep(int minutes) : NPCAction("Sleeping", minutes, ActivityType.Sleeping)
{
    public override void Complete(NPC npc) => npc.Body.Rest(MinutesSpent, npc.CurrentLocation, null);
}

public class NPCStash(ResourceCategory resourceCategory) : NPCAction($"Storing {resourceCategory}", 2, ActivityType.Crafting)
{
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            Console.WriteLine("AI's BROKE. Trying to store items where there's no cache!");
            return;
        }
        while (npc.Inventory.GetCount(resourceCategory) > 0)
        {
            var item = npc.Inventory.FindAnyResourceInCategory(resourceCategory);
            cache.Storage.Add(item, npc.Inventory.Pop(item));
        }
    }
}

public class NPCStashWater() : NPCAction("Storing Water", 2, ActivityType.Crafting)
{
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            Console.WriteLine("AI's BROKE. Trying to store water where there's no cache!");
            return;
        }
        var water = npc.Inventory.WaterLiters;
        cache.Storage.WaterLiters += water;
        npc.Inventory.WaterLiters = 0;
    }
}

public class NPCTakeFromCache(ResourceCategory resourceCategory) : NPCAction($"Taking {resourceCategory}", 2, ActivityType.Crafting)
{
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null)
        {
            Console.WriteLine("AI's BROKE. Trying to take items where there's no cache!");
            return;
        }
        // just take one and the action should get requeued if more needed
        var item = cache.Storage.FindAnyResourceInCategory(resourceCategory);
        npc.Inventory.Add(item, cache.Storage.Pop(item));
    }
}

public class NPCTakeToolFromCache(ToolType toolType) : NPCAction($"Taking {toolType}", 2, ActivityType.Crafting)
{
    public override void Complete(NPC npc)
    {
        var cache = npc.CurrentLocation.GetFeature<CacheFeature>();
        if (cache == null) return;

        var tool = cache.Storage.Tools.FirstOrDefault(t => t.ToolType == toolType && t.Works);
        if (tool != null)
        {
            cache.Storage.Tools.Remove(tool);
            npc.Inventory.Tools.Add(tool);
        }
    }
}

public class NPCCraft : NPCAction
{
    private readonly CraftOption _recipe;

    public NPCCraft(CraftOption recipe) : base($"Crafting {recipe.Name}", recipe.CraftingTimeMinutes, ActivityType.Crafting)
    {
        _recipe = recipe;
    }

    public override void Complete(NPC npc)
    {
        var result = _recipe.Craft(npc.Inventory);
        if (result != null)
            npc.Inventory.Tools.Add(result);
    }
}

#region Combat Actions

public class NPCFight : NPCAction
{
    private readonly Animal _threat;

    public NPCFight(Animal threat) : base($"Fighting {threat.Name}", 1, ActivityType.Fighting)
    {
        _threat = threat;
    }

    public override void Complete(NPC npc)
    {
        var outcome = ActorCombatResolver.ResolveCombat(_threat, npc, npc.CurrentLocation);

        switch (outcome)
        {
            case ActorCombatResolver.CombatOutcome.DefenderKilled:
                Console.WriteLine($"[NPC] {npc.Name} was killed by {_threat.Name}");
                break;
            case ActorCombatResolver.CombatOutcome.DefenderInjured:
                Console.WriteLine($"[NPC] {npc.Name} was injured fighting {_threat.Name}");
                break;
            case ActorCombatResolver.CombatOutcome.AttackerRepelled:
                Console.WriteLine($"[NPC] {npc.Name} drove off {_threat.Name}!");
                break;
            case ActorCombatResolver.CombatOutcome.DefenderEscaped:
                Console.WriteLine($"[NPC] {npc.Name} escaped from {_threat.Name}");
                break;
        }
    }

    public Animal Threat => _threat;
}

public class NPCFlee : NPCAction
{
    private readonly Animal _threat;

    public NPCFlee(Animal threat) : base($"Fleeing from {threat.Name}", 5, ActivityType.Traveling)
    {
        _threat = threat;
    }

    public override void Complete(NPC npc)
    {
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

#endregion