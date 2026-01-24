using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
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
    public RelationshipMemory Relationships { get; set; } = new();
    public ResourceMemory ResourceMemory { get; set; } = new();
    public Location? Camp { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NPCAction? CurrentAction { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    private SurvivalContext? _currentContext;

    // Context for threat detection (set during Update)
    [System.Text.Json.Serialization.JsonIgnore]
    private HerdRegistry? _currentHerds;
    [System.Text.Json.Serialization.JsonIgnore]
    private IEnumerable<NPC>? _currentNPCs;

    // Combat cooldown prevents re-detection immediately after combat
    [System.Text.Json.Serialization.JsonIgnore]
    private int _combatCooldownMinutes = 0;

    // Pending threat detected during ShouldInterrupt (handled in Update)
    [System.Text.Json.Serialization.JsonIgnore]
    private Actor? _pendingThreat;

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
    public override double BaseCohesion => Personality.Sociability + 1;

    // For JSON deserialization
    public NPC() : base("", Body.BaselineHumanStats, null!, null!)
    {
        Personality = new Personality();
        Inventory = new Inventory();
    }

    public NPC(string name, Personality personality, Location currentLocation, GameMap map)
        : base(name, Body.BaselineHumanStats, currentLocation, map)
    {
        Personality = personality;
        Inventory = new Inventory();
    }

    // pending suggestion?
    // following?

    /// <summary>
    /// Update NPC with optional context for threat detection.
    /// </summary>
    public void Update(int minutes, SurvivalContext context,
        HerdRegistry? herds = null, IEnumerable<NPC>? npcs = null)
    {
        base.Update(minutes, context);
        _currentContext = context;
        _currentHerds = herds;
        _currentNPCs = npcs;

        for (int i = 0; i < minutes; i++)
        {
            // Tick combat cooldown
            if (_combatCooldownMinutes > 0)
                _combatCooldownMinutes--;

            // Console.WriteLine($"[NPC:{Name}] Tick {i + 1}/{minutes} - Action={CurrentAction?.Name ?? "none"}, Need={CurrentNeed?.ToString() ?? "none"}");

            // if interrupt, clear action and let DetermineNeed pick it up
            if (ShouldInterrupt())
            {
                CurrentAction?.Interrupt(this);

                // Handle pending threat from ShouldInterrupt
                if (_pendingThreat != null)
                {
                    bool fight = DecideFlightOrFight(_pendingThreat);
                    CurrentAction = fight ? new NPCFight(_pendingThreat) : new NPCFlee(_pendingThreat);
                    _pendingThreat = null;
                }
                else
                {
                    CurrentAction = null;
                }
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
            AddLog(CurrentAction.LogMessage);
            ContinueAction();
        }
    }
    internal bool ShouldInterrupt()
    {
        // Threat response - highest priority (but not if already fighting/fleeing)
        if (CurrentAction is not NPCFight && CurrentAction is not NPCFlee)
        {
            var threat = GetPriorityThreat();
            if (threat != null)
            {
                _pendingThreat = threat;
                return true;
            }
        }

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
            var warm = HandleWarmthNeed(context);
            if (warm != null) return warm;
        }
        else if (CurrentNeed == NeedType.Water)
        {
            var drink = HandleWaterNeed(context);
            if (drink != null) return drink;
        }
        else if (CurrentNeed == NeedType.Rest)
        {
            var sleep = DecideSleep();
            if (sleep != null)
                return sleep;

            // Can't sleep - check if blocked by cold
            var coldAction = HandleColdPrerequisite(context);
            if (coldAction != null) return coldAction;
        }
        else if (CurrentNeed == NeedType.Food)
        {
            var eat = HandleFoodNeed(context);
            if (eat != null) return eat;
        }

        var action = DetermineWork();
        action ??= DetermineCraft();
        action ??= DetermineIdle(context);

        return action;
    }
    private NPCAction? HandleWarmthNeed(SurvivalContext context)
    {
        bool atActiveFire = CurrentLocation.HasActiveHeatSource();
        bool atDeadFire = !atActiveFire && CurrentLocation.HasFeature<HeatSourceFeature>();
        bool notAtFire = !(atActiveFire || atDeadFire);

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

        // if at fire, check if it's warming effectively
        if (atActiveFire)
        {
            bool warmingEffectively = IsFireWarmingEffectively();
            if (warmingEffectively)
            {
                // Fire is good, rest and warm up
                Console.WriteLine($"  [Warmth] At fire, warming effectively, resting");
                return new NPCRest(Utils.RandInt(5, 15));
            }
            else
            {
                // Fire is too weak - need to improve it
                Console.WriteLine($"  [Warmth] Fire too weak, need to tend");
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
                Console.WriteLine($"  [Warmth] Getting fuel (urgent)");
                var get = DetermineGetResource(ResourceCategory.Fuel, urgent: true);
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

        // If we get here, warmth handling exhausted normal options
        // (tried nearby fire, known fires, starting fire, crafting tools)
        // Explore to find fire or materials
        var exploreAction = TryExplore(context, "No known fire/tools");
        if (exploreAction != null) return exploreAction;

        // If even exploration fails (low boldness), fall through to idle as last resort
        Console.WriteLine($"  [Warmth] Falling through to work/craft");
        return null;
    }
    private NPCAction? HandleWaterNeed(SurvivalContext context)
    {
        Console.WriteLine($"  [Water] Determining action for water need");

        // Check for water in inventory first - drink it
        if (Inventory.WaterLiters > 0.1)
        {
            Console.WriteLine($"  [Water] Drinking from inventory ({Inventory.WaterLiters:F1}L)");
            return new NPCDrinkWater();
        }

        // At active fire? Melt snow for water
        if (CookingHandler.CanMeltSnow(CurrentLocation))
        {
            Console.WriteLine($"  [Water] Melting snow at fire");
            return new NPCMeltSnow();
        }

        // Check for collectible water resources (streams, etc.)
        if (HasResource(ResourceCategory.Water))
        {
            var water = Inventory.FindAnyResourceInCategory(ResourceCategory.Water);
            return new NPCEat(water, Inventory.Pop(water));
        }

        // Try to get water from environment (water sources)
        var get = DetermineGetResource(ResourceCategory.Water);
        if (get != null) return get;

        // No water source found - go to fire to melt snow
        var knownActiveFire = GetKnownActiveFire();
        if (knownActiveFire != null && knownActiveFire != CurrentLocation)
        {
            Console.WriteLine($"  [Water] Going to fire at {knownActiveFire.Name} to melt snow");
            var move = DecideToMove(knownActiveFire);
            if (move != null) return move;
        }

        // Check if blocked by cold prerequisite
        var coldAction = HandleColdPrerequisite(context);
        if (coldAction != null) return coldAction;

        // No fire available - need to start one first, then melt snow
        Console.WriteLine($"  [Water] Need fire to melt snow - switching to warmth");
        CurrentNeed = NeedType.Warmth;
        return DetermineActionForNeed(context);
    }
    private NPCAction? HandleFoodNeed(SurvivalContext context)
    {
        Console.WriteLine($"  [Food] Determining action for food need");

        // Priority 1: Eat cooked meat (ready to consume)
        if (Inventory.Count(Resource.CookedMeat) > 0)
        {
            Console.WriteLine($"  [Food] Eating cooked meat");
            return new NPCEat(Resource.CookedMeat, Inventory.Pop(Resource.CookedMeat));
        }

        // Priority 2: Cook raw meat if at fire
        if (Inventory.Count(Resource.RawMeat) > 0)
        {
            if (CookingHandler.CanCookMeat(Inventory, CurrentLocation))
            {
                Console.WriteLine($"  [Food] Cooking raw meat at fire");
                return new NPCCookMeat();
            }

            // Have raw meat but need fire - go to known fire
            var knownActiveFire = GetKnownActiveFire();
            if (knownActiveFire != null && knownActiveFire != CurrentLocation)
            {
                Console.WriteLine($"  [Food] Going to fire at {knownActiveFire.Name} to cook meat");
                var move = DecideToMove(knownActiveFire);
                if (move != null) return move;
            }

            // No fire available - start one (warmth need handles fire creation)
            Console.WriteLine($"  [Food] Need fire to cook - switching to warmth");
            CurrentNeed = NeedType.Warmth;
            return DetermineActionForNeed(context);
        }

        // Priority 3: Eat other ready food (berries, etc.)
        if (HasResource(ResourceCategory.Food))
        {
            var food = Inventory.FindAnyResourceInCategory(ResourceCategory.Food);
            Console.WriteLine($"  [Food] Eating {food}");
            return new NPCEat(food, Inventory.Pop(food));
        }

        // Priority 4: Get food
        var get = DetermineGetResource(ResourceCategory.Food);
        if (get != null) return get;

        // Check if blocked by cold prerequisite
        var coldAction = HandleColdPrerequisite(context);
        if (coldAction != null) return coldAction;

        // Resource not found - explore to find it
        var exploreAction = TryExplore(context, "Food not found");
        if (exploreAction != null) return exploreAction;

        return null;
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
    /// Check if the current fire is warming the NPC fast enough to reach target warmth
    /// within a reasonable time (2 hours). If not, the fire needs more fuel.
    /// </summary>
    private bool IsFireWarmingEffectively()
    {
        const double TARGET_WARMTH = 0.7;
        const int MAX_ACCEPTABLE_MINUTES = 90;

        // If already warm enough, fire is fine
        if (Body.WarmPct >= TARGET_WARMTH) return true;

        double warmingRate = Body.LastTemperatureDelta; // °F per minute

        // If cooling or not warming at all, definitely need more fire
        if (warmingRate <= 0) return false;

        // Calculate minutes to reach target warmth at current rate
        // WarmPct is based on body temp relative to hypothermia threshold
        double currentTemp = Body.BodyTemperature;
        // targetTemp such that WarmPct = TARGET_WARMTH
        // WarmPct = (T - Threshold) / (BaseTemp - Threshold)
        // T = WarmPct * (BaseTemp - Threshold) + Threshold
        double tempRange = Body.BASE_BODY_TEMP - SurvivalProcessor.HypothermiaThreshold;
        double targetTemp = TARGET_WARMTH * tempRange + SurvivalProcessor.HypothermiaThreshold;
        double degreesNeeded = targetTemp - currentTemp;

        if (degreesNeeded <= 0) return true; // Already there

        double minutesToTarget = degreesNeeded / warmingRate;

        Console.WriteLine($"  [Warmth] Rate: {warmingRate:F2}°F/min, need {degreesNeeded:F1}°F, ETA: {minutesToTarget:F0}min");

        return minutesToTarget <= MAX_ACCEPTABLE_MINUTES;
    }

    /// <summary>
    /// Check if NPC can survive an activity away from fire for the specified duration.
    /// Returns false if the activity would drop warmth to dangerous levels.
    /// </summary>
    private bool CanSurviveAwayFromFire(int durationMinutes)
    {
        if (_currentContext == null) return true;

        double projectedTemp = SurvivalProcessor.ProjectTemperatureAwayFromFire(
            Body, _currentContext, durationMinutes);

        double projectedWarmPct = Math.Clamp(
            (projectedTemp - SurvivalProcessor.HypothermiaThreshold)
            / (Body.BASE_BODY_TEMP - SurvivalProcessor.HypothermiaThreshold), 0, 1);

        bool canSurvive = projectedWarmPct > 0.3;

        if (!canSurvive)
        {
            Console.WriteLine($"  [Survival] {durationMinutes}min away would drop warmth to {projectedWarmPct:P0} - too dangerous");
        }

        return canSurvive;
    }

    /// <summary>
    /// Check if the NPC's low warmth is blocking productive activities.
    /// Uses same projection logic as CanSurviveAwayFromFire for consistency.
    /// </summary>
    /// <param name="estimatedMinutes">How long the activity would take (default 30 min)</param>
    private bool IsBlockedByCold(int estimatedMinutes = 30)
    {
        if (_currentContext == null) return false;

        // Project warmth after estimated time away from fire
        double projectedWarmth = SurvivalProcessor.ProjectTemperatureAwayFromFire(
            Body, _currentContext, estimatedMinutes);

        double projectedWarmPct = Math.Clamp(
            (projectedWarmth - SurvivalProcessor.HypothermiaThreshold)
            / (Body.BASE_BODY_TEMP - SurvivalProcessor.HypothermiaThreshold), 0, 1);

        bool blocked = projectedWarmPct <= 0.2;

        if (blocked)
        {
            Console.WriteLine($"  [Prerequisite] Blocked by cold (projected warmth after {estimatedMinutes}min: {projectedWarmPct:P0})");
        }

        return blocked;
    }

    /// <summary>
    /// Check if current need is blocked by cold, and if so, switch to warmth.
    /// Returns warmth action if blocked, null otherwise.
    /// </summary>
    private NPCAction? HandleColdPrerequisite(SurvivalContext context)
    {
        if (!IsBlockedByCold())
            return null;

        Console.WriteLine($"  [Prerequisite] {CurrentNeed} blocked by cold → switching to Warmth");
        CurrentNeed = NeedType.Warmth;
        return DetermineActionForNeed(context);
    }

    /// <summary>
    /// Try to explore when normal resource acquisition fails.
    /// Returns exploration action or null if exploration not possible.
    /// </summary>
    private NPCAction? TryExplore(SurvivalContext context, string reason)
    {
        // Don't explore if it would be dangerous
        int estimatedTravelMinutes = 10;
        if (!CanSurviveAwayFromFire(estimatedTravelMinutes))
            return null;

        // Check boldness - only explore if brave enough
        if (!Utils.DetermineSuccess(Personality.Boldness))
            return null;

        // Get adjacent locations and pick a random one
        var adjacentLocations = Map?.GetTravelOptionsFrom(CurrentLocation)?.ToList();
        if (adjacentLocations == null || adjacentLocations.Count == 0)
            return null;

        var destination = Utils.GetRandomFromList(adjacentLocations);
        Console.WriteLine($"  [Exploration] {reason} → exploring to {destination.Name}");
        return new NPCMove(destination, this);
    }

    private void DetermineNeed()
    {
        if (DetermineCriticalNeed())
            return;
        if (DecideSatisfyNeed())
            return;
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

    private NPCAction? DetermineGetResource(ResourceCategory category, bool allowCamp = true, bool urgent = false)
    {
        Console.WriteLine($"    [GetResource] Looking for category: {category}{(urgent ? " (URGENT)" : "")}");

        // can't gather if inv already full
        var invFull = DealWithFullInventory();
        if (invFull != null)
            return invFull;

        // in tile -> work
        var work = GetResourceAtCurrentLocation(category, urgent);
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
            // Skip this check if urgent - we need the resource even if risky
            int estimatedTravelMinutes = 10;
            if (!urgent && !CanSurviveAwayFromFire(estimatedTravelMinutes))
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

    private NPCAction? GetResourceAtCurrentLocation(ResourceCategory category, bool urgent = false)
    {
        var targetResources = ResourceCategories.Items[category];

        // ForageFeature - always accessible
        var forage = CurrentLocation.GetFeature<ForageFeature>();
        if (forage != null && !forage.IsNearlyDepleted() &&
            forage.ProvidedResources().Any(r => targetResources.Contains(r)))
        {
            int forageTime = Utils.RandInt(15, 60);
            // Check if we can survive foraging in current conditions (skip if urgent)
            if (!urgent && !CanSurviveAwayFromFire(forageTime))
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
                // Check if we can survive harvesting in current conditions (skip if urgent)
                if (!urgent && !CanSurviveAwayFromFire(workTime))
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

            // Check if we can survive chopping in current conditions (skip if urgent)
            if (!urgent && !CanSurviveAwayFromFire(workTime))
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

        // Improve shelter if possible
        var shelterAction = TryImproveShelter();
        if (shelterAction != null)
            return shelterAction;

        return null;
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

    #region Shelter Improvement

    private bool ShouldImproveShelter()
    {
        if (CurrentLocation != Camp) return false;
        var shelter = Camp?.GetFeature<ShelterFeature>();
        if (shelter == null || shelter.IsDestroyed) return false;
        if (shelter.Quality >= 0.8) return false;  // Good enough
        return HasAnyShelterMaterials();
    }

    private bool HasAnyShelterMaterials()
    {
        return MaterialProperties.ShelterMaterials.Any(m => Inventory.Count(m) > 0);
    }

    private NPCAction? TryImproveShelter()
    {
        if (!ShouldImproveShelter()) return null;

        var shelter = Camp!.GetFeature<ShelterFeature>()!;

        // Find weakest stat that can still be improved
        var type = GetWeakestImprovableType(shelter);
        if (type == null) return null;

        // Find best material for that type
        var material = GetBestMaterialFor(type.Value);
        if (material == null) return null;

        Console.WriteLine($"[NPC:{Name}] Improving shelter ({type.Value.ToString().ToLower()}) with {material.Value.ToDisplayName()}");
        return new NPCImproveShelter(type.Value, material.Value);
    }

    private ShelterImprovementType? GetWeakestImprovableType(ShelterFeature shelter)
    {
        // Return the type with most room for improvement
        var options = new[]
        {
            (type: ShelterImprovementType.Insulation, gap: shelter.InsulationCap - shelter.TemperatureInsulation),
            (type: ShelterImprovementType.Overhead, gap: shelter.OverheadCap - shelter.OverheadCoverage),
            (type: ShelterImprovementType.Wind, gap: shelter.WindCap - shelter.WindCoverage)
        };

        var best = options.Where(o => o.gap > 0.05).OrderByDescending(o => o.gap).FirstOrDefault();
        return best.gap > 0 ? best.type : null;
    }

    private Resource? GetBestMaterialFor(ShelterImprovementType type)
    {
        return MaterialProperties.ShelterMaterials
            .Where(m => Inventory.Count(m) > 0)
            .OrderByDescending(m => MaterialProperties.GetEffectiveness(m, type))
            .Cast<Resource?>()
            .FirstOrDefault();
    }

    #endregion

    private NPCAction? TryCraftSpecificTool(ToolType toolType)
    {
        Console.WriteLine($"    [Craft] TryCraftSpecificTool({toolType})");

        // Get all craft options that produce this specific tool type
        var options = CraftingSystem.AllOptions
            .Where(o => o.GearFactory != null && o.GearFactory(1).ToolType == toolType)
            .ToList();

        Console.WriteLine($"    [Craft] Options for {toolType}: {options.Count}");

        // Try to craft if we can
        var craftable = options.FirstOrDefault(o => o.CanCraft(Inventory));
        if (craftable != null)
        {
            Console.WriteLine($"    [Craft] Can craft: {craftable.Name}");
            return new NPCCraft(craftable);
        }

        // Can't craft - find missing materials (but NOT tools - that causes recursion)
        foreach (var option in options)
        {
            Console.WriteLine($"    [Craft] Checking option: {option.Name}");
            foreach (var req in option.Requirements)
            {
                var needed = GetMissingCount(req);
                Console.WriteLine($"    [Craft]   Req: {req.Material}, need {req.Count}, missing {needed}");
                if (needed > 0)
                {
                    if (req.Material is MaterialSpecifier.Specific(var resource))
                    {
                        Console.WriteLine($"    [Craft]   Getting specific resource: {resource}");
                        return DetermineGetSpecificResource(resource);
                    }
                    else if (req.Material is MaterialSpecifier.Category(var resCat))
                    {
                        Console.WriteLine($"    [Craft]   Getting category: {resCat}");
                        return DetermineGetResource(resCat);
                    }
                }
            }
        }

        Console.WriteLine($"    [Craft] No options found for {toolType}");
        return null;
    }

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
            {
                return new NPCTakeToolFromCache(toolType);
            }
            else if (Camp != null)
            {
                var move = DecideToMove(Camp);
                if (move != null) return move;
            }
        }

        // Not in cache, try to craft the specific tool type
        Console.WriteLine($"    [GetTool] Not in cache, trying to craft {toolType}");
        return TryCraftSpecificTool(toolType);
    }

    /// <summary>
    /// Check if the NPC can safely idle/rest without addressing critical needs.
    /// Idle is only safe if: no critical needs OR currently warming by a fire
    /// </summary>
    private bool CanSafelyIdle()
    {
        // If no current need, idling is safe
        if (CurrentNeed == null) return true;

        // If current need is warmth AND we're near an active fire, idling (warming) is safe
        if (CurrentNeed == NeedType.Warmth)
        {
            bool atActiveFire = CurrentLocation.HasActiveHeatSource();
            if (atActiveFire)
            {
                Console.WriteLine($"  [Idle] Safe to idle - warming by fire");
                return true;
            }
        }

        // Otherwise, we have a critical need that requires action
        return false;
    }

    private NPCAction DetermineIdle(SurvivalContext context)
    {
        // Check if idling is safe
        if (!CanSafelyIdle())
        {
            Console.WriteLine($"  [Idle] Cannot safely idle - warmth critical");
            // Force warmth need
            CurrentNeed = NeedType.Warmth;
            return DetermineActionForNeed(context);
        }

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
        // No threats nearby (predators OR hostile NPCs)
        if (GetThreatsHere().Any())
            return false;

        return true;
    }

    public double GetRelationship(Actor other)
    {
        return Math.Clamp(Relationships.GetOpinion(other), -1, 1);
    }

    #region Unified Actor Assessment

    /// <summary>
    /// Determines if this NPC considers another actor hostile.
    /// Used for: threat detection, combat decisions, sleep checks.
    /// </summary>
    internal bool IsHostileTo(Actor other)
    {
        if (other == this || !other.IsAlive) return false;

        return other switch
        {
            Animal animal => animal.AnimalType.IsPredator(),
            NPC npc => GetRelationship(npc) <= -1.0,
            Player.Player player => GetRelationship(player) <= -1.0,  // Future: hostile to player
            _ => false
        };
    }

    /// <summary>
    /// Determines if this NPC considers another actor prey (huntable).
    /// Future: enables NPC hunting behavior.
    /// </summary>
    internal bool IsPreyTo(Actor other)
    {
        if (other == this || !other.IsAlive) return false;

        return other switch
        {
            Animal animal => !animal.AnimalType.IsPredator(),  // Non-predators are prey
            _ => false
        };
    }

    /// <summary>
    /// Determines if this NPC would help defend an ally against a threat.
    /// Generalizes DecideToHelpInCombat to work with any Actor threat.
    /// </summary>
    internal bool WouldDefend(Actor ally, Actor threat)
    {
        if (!IsHostileTo(threat)) return false;
        if (IsHostileTo(ally)) return false;  // Won't help enemies

        double relationship = GetRelationship(ally);
        if (relationship < -0.3) return false;

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
    /// Get all hostile actors at NPC's current location.
    /// Returns predators AND hostile NPCs.
    /// </summary>
    internal IEnumerable<Actor> GetThreatsHere()
    {
        if (_currentHerds == null || Map == null)
            yield break;

        var position = Map.GetPosition(CurrentLocation);

        // Predators from herds
        foreach (var herd in _currentHerds.GetHerdsAt(position))
        {
            if (herd.IsPredator)
            {
                foreach (var animal in herd.Members.Where(m => m.IsAlive))
                    yield return animal;
            }
        }

        // Hostile NPCs at same location
        if (_currentNPCs != null)
        {
            foreach (var npc in _currentNPCs)
            {
                if (npc != this && npc.IsAlive && npc.CurrentLocation == CurrentLocation)
                {
                    if (IsHostileTo(npc))
                        yield return npc;
                }
            }
        }
    }

    /// <summary>
    /// Get the most dangerous threat at location (for fight/flee decisions).
    /// Returns null during combat cooldown.
    /// </summary>
    internal Actor? GetPriorityThreat()
    {
        // Don't detect threats during cooldown
        if (_combatCooldownMinutes > 0)
            return null;

        return GetThreatsHere()
            .OrderByDescending(t => t.Body.WeightKG * t.Vitality)  // Biggest, healthiest = priority
            .FirstOrDefault();
    }

    /// <summary>
    /// Set combat cooldown (called after combat to prevent re-detection).
    /// </summary>
    internal void SetCombatCooldown(int minutes = 5)
    {
        _combatCooldownMinutes = minutes;
    }

    #endregion

    #region Combat Decisions

    /// <summary>
    /// Decides if NPC will join combat to help another actor.
    /// Uses relationship + self-assessment + threat assessment.
    /// </summary>
    internal bool DecideToHelpInCombat(Actor ally, Actor threat)
    {
        return WouldDefend(ally, threat);
    }

    /// <summary>
    /// Decides fight vs flee when NPC faces any threat.
    /// Returns true for fight, false for flee.
    /// </summary>
    internal bool DecideFlightOrFight(Actor threat)
    {
        double fightChance = Personality.Boldness;

        // Equipment check
        bool hasWeapon = Inventory.Weapon != null;
        bool isInjured = Vitality < 0.7;

        if (hasWeapon && !isInjured) fightChance += 0.2;
        if (isInjured) fightChance -= 0.2;

        // Threat comparison (works for any Actor)
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
    [Obsolete("Use GetPriorityThreat() instead - handles all threat types")]
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
public enum NeedType
{
    Warmth = 0,
    Water = 1,
    Rest = 2,
    Food = 3,
    None = 4,
}
