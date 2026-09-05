using text_survival.Actions;
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

    /// <summary>
    /// Longest an "urgent" gathering trip (one taken without checking
    /// <see cref="CanSurviveAwayFromFire"/>) is allowed to run. Urgent bypasses the safety
    /// check because the NPC needs the resource regardless of risk, but that isn't licence
    /// to commit to a full-length session - a critically cold NPC can die of exposure
    /// before a 60-minute forage even finishes.
    /// </summary>
    private const int UrgentGatherCapMinutes = 15;

#pragma warning disable CS8765 // NPCs always have an inventory; the base Actor permits animals to omit one.
    public override Inventory Inventory { get; set; } = new();
#pragma warning restore CS8765

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
    private List<Herd>? _currentHerds;
    [System.Text.Json.Serialization.JsonIgnore]
    private IEnumerable<NPC>? _currentNPCs;

    // The game this NPC lives in (set during Update); fights need it for their aftermath
    [System.Text.Json.Serialization.JsonIgnore]
    private GameContext? _game;
    [System.Text.Json.Serialization.JsonIgnore]
    internal GameContext Game => _game ?? throw new InvalidOperationException($"{Name} has no game context; NPC.Update must receive one");

    // Combat cooldown prevents re-detection immediately after combat
    [System.Text.Json.Serialization.JsonIgnore]
    private int _combatCooldownMinutes = 0;

    // Pending threat detected during ShouldInterrupt (handled in Update)
    [System.Text.Json.Serialization.JsonIgnore]
    private Actor? _pendingThreat;

    // Tool types currently being resolved up a prerequisite chain, so crafting a tool that
    // requires a tool cannot recurse forever.
    [System.Text.Json.Serialization.JsonIgnore]
    private readonly HashSet<ToolType> _toolsBeingResolved = [];

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
        List<Herd>? herds = null, IEnumerable<NPC>? npcs = null, GameContext? game = null)
    {
        base.Update(minutes, context);
        _currentContext = context;
        _currentHerds = herds;
        _currentNPCs = npcs;
        _game = game ?? _game;

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
            Trace($"[NPC:{Name}] Picked: {CurrentAction?.Name} for need {CurrentNeed}");
            AddLog(CurrentAction?.LogMessage);
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
        var need = GetCriticalNeed();
        if (need == null || need == CurrentNeed) return false;

        // Only a strictly higher-priority need may cut in (NeedType is ordered
        // Warmth < Water < Rest < Food). This stops critical-vs-critical thrash without
        // blocking every interrupt, which is what the previous guard did by comparing
        // against NeedType.Food - every real need is <= Food, so nothing could ever
        // interrupt anything and an NPC would keep foraging while dying of thirst.
        if (CurrentNeed != null && need >= CurrentNeed)
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
            Trace($"[NPC:{Name}] Completed: {CurrentAction.Name} ({CurrentAction.MinutesSpent}/{CurrentAction.DurationMinutes} min)");
            CurrentAction.Complete(this);
            int minutesLeftover = CurrentAction.MinutesSpent - CurrentAction.DurationMinutes;
            CurrentAction = null;
        }
    }
    private NPCAction DetermineActionForNeed(SurvivalContext context)
    {
        if (IsTracing) Trace($"  [Warmth] Determining action for need: {CurrentNeed}");
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
            if (IsTracing) Trace($"  [Warmth] Known fire: {knownActiveFire?.Name ?? "none"}");

            // If the known fire is weak enough that this NPC left it to find fuel,
            // marching straight back empty-handed just repeats the trip. Gather here
            // first if this tile can provide fuel.
            bool haveFuelToTend = FireHandler.GetFireMaterials(Inventory).HasKindling;
            if (knownActiveFire != null && !haveFuelToTend)
            {
                var gatherHere = GetResourceAtCurrentLocation(ResourceCategory.Fuel, urgent: true);
                if (gatherHere != null)
                {
                    if (IsTracing) Trace($"  [Warmth] No fuel for {knownActiveFire.Name} - gathering here first");
                    return gatherHere;
                }
            }

            // known fire -> go there
            if (knownActiveFire != null)
            {
                if (IsTracing) Trace($"  [Warmth] Going to fire at {knownActiveFire.Name}");
                var move = DecideToMove(knownActiveFire);
                if (move != null) return move;
            }
            else if (Camp != null) // if no known fire prefer to make it at camp
            {
                if (IsTracing) Trace($"  [Warmth] Going to camp");
                var move = DecideToMove(Camp, maxTiles: 16); // only if close
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
                // Warming is not a task, it is waiting for the fire to do its work - and the
                // fire is the only place in the world snow can be melted. Anything that keeps
                // you sitting at it for free should happen now rather than costing a second
                // trip later. Cooking (which melting snow is) and crafting all share Resting's
                // ActivityLevel and FireProximity, so each of these warms exactly as fast and
                // burns exactly the same calories as staring at the flames.
                //
                // Only a craft the NPC can do right now counts: DetermineCraft falls through
                // to fetching missing materials, and walking off to find flint is the one
                // thing a cold NPC at a good fire must not do. The cast drops that case, and
                // the normal work loop picks it up again once warm.
                NPCAction? whileWarming = TryBuildWaterReserve();
                whileWarming ??= CookingHandler.CanCookMeat(Inventory, CurrentLocation)
                    ? new NPCCookMeat()
                    : null;
                whileWarming ??= DetermineCraft() as NPCCraft;
                if (whileWarming != null)
                {
                    if (IsTracing) Trace($"  [Warmth] At fire, warming - doing {whileWarming.Name} meanwhile");
                    return whileWarming;
                }

                if (IsTracing) Trace($"  [Warmth] At fire, warming effectively, resting");
                return new NPCRest(Utils.RandInt(5, 15));
            }
            else
            {
                // Fire is too weak - need to improve it
                if (IsTracing) Trace($"  [Warmth] Fire too weak, need to tend");
                needToTendFire = true;
            }
        }

        if (needToTendFire)
        {
            // at fire but still cooling -> add fuel if you can
            var fireFeature = CurrentLocation.GetFeature<HeatSourceFeature>()!;
            if (FireHandler.CanTendFire(Inventory, fireFeature))
            {
                if (IsTracing) Trace($"  [Warmth] Tending fire");
                return new NPCTendFire();
            }

            if (!FireHandler.CanTendFire(Inventory, fireFeature))
            {
                // Prefer the camp cache over wandering off and leaving a dying fire
                // unattended - it's a two-minute trip instead of a round trip to forage.
                if (CurrentLocation == Camp && CampHas(ResourceCategory.Fuel))
                {
                    if (IsTracing) Trace($"  [Warmth] Getting fuel from cache (urgent)");
                    return new NPCTakeResourceFromCache(ResourceCategory.Fuel);
                }

                if (IsTracing) Trace($"  [Warmth] Getting fuel (urgent)");
                var get = DetermineGetResource(ResourceCategory.Fuel, urgent: true);
                if (get != null) return get;
            }
        }

        // at fire but not lit -> light it
        if (needToStartFire)
        {
            var hasTool = FireHandler.GetBestTool(Inventory) != null;
            if (IsTracing) Trace($"  [Warmth] Has fire tool: {hasTool}");
            if (FireHandler.CanStartFire(Inventory))
            {
                if (IsTracing) Trace($"  [Warmth] Can start fire, starting");
                return new NPCStartFire();
            }
            // No fire-starting tool? Try to craft one before gathering resources
            if (!hasTool)
            {
                if (IsTracing) Trace($"  [Warmth] No tool, trying to craft");
                var craft = DetermineCraft();
                if (craft != null)
                {
                    if (IsTracing) Trace($"  [Warmth] Crafting: {craft.Name}");
                    return craft;
                }
                if (IsTracing) Trace($"  [Warmth] Can't craft, skipping fire materials");
                // Can't craft tool - don't gather fire materials we can't use
            }
            else
            {
                if (FireHandler.GetFireMaterials(Inventory).Tinders.Count < 1)
                {
                    if (IsTracing) Trace($"  [Warmth] Getting tinder");
                    var get = DetermineGetResource(ResourceCategory.Tinder);
                    if (get != null) return get;
                }
                if (!FireHandler.GetFireMaterials(Inventory).HasKindling)
                {
                    if (IsTracing) Trace($"  [Warmth] Getting fuel");
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
        if (IsTracing) Trace($"  [Warmth] Falling through to work/craft");
        return null;
    }
    private NPCAction? HandleWaterNeed(SurvivalContext context)
    {
        if (IsTracing) Trace($"  [Water] Determining action for water need");

        // Check for water in inventory first - drink it
        double waterAvailable = Inventory.Weight(Resource.Water);
        if (waterAvailable > 0.1)
        {
            if (IsTracing) Trace($"  [Water] Drinking from inventory ({waterAvailable:F1}L)");
            return new NPCDrinkWater();
        }

        // At active fire? Melt snow for water
        if (CookingHandler.CanMeltSnow(CurrentLocation))
        {
            if (IsTracing) Trace($"  [Water] Melting snow at fire");
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
            if (IsTracing) Trace($"  [Water] Going to fire at {knownActiveFire.Name} to melt snow");
            var move = DecideToMove(knownActiveFire);
            if (move != null) return move;
        }

        // Check if blocked by cold prerequisite
        var coldAction = HandleColdPrerequisite(context);
        if (coldAction != null) return coldAction;

        // No fire available - need to start one first, then melt snow
        if (IsTracing) Trace($"  [Water] Need fire to melt snow - switching to warmth");
        CurrentNeed = NeedType.Warmth;
        return DetermineActionForNeed(context);
    }
    private NPCAction? HandleFoodNeed(SurvivalContext context)
    {
        if (IsTracing) Trace($"  [Food] Determining action for food need");

        // Priority 1: Eat cooked meat (ready to consume)
        if (Inventory.Count(Resource.CookedMeat) > 0)
        {
            if (IsTracing) Trace($"  [Food] Eating cooked meat");
            return new NPCEat(Resource.CookedMeat, Inventory.Pop(Resource.CookedMeat));
        }

        // Priority 2: Cook raw meat if at fire
        if (Inventory.Count(Resource.RawMeat) > 0)
        {
            if (CookingHandler.CanCookMeat(Inventory, CurrentLocation))
            {
                if (IsTracing) Trace($"  [Food] Cooking raw meat at fire");
                return new NPCCookMeat();
            }

            // Have raw meat but need fire - go to known fire
            var knownActiveFire = GetKnownActiveFire();
            if (knownActiveFire != null && knownActiveFire != CurrentLocation)
            {
                if (IsTracing) Trace($"  [Food] Going to fire at {knownActiveFire.Name} to cook meat");
                var move = DecideToMove(knownActiveFire);
                if (move != null) return move;
            }

            // No fire available - start one (warmth need handles fire creation)
            if (IsTracing) Trace($"  [Food] Need fire to cook - switching to warmth");
            CurrentNeed = NeedType.Warmth;
            return DetermineActionForNeed(context);
        }

        // Priority 3: Eat other ready food (berries, etc.)
        if (HasResource(ResourceCategory.Food))
        {
            var food = Inventory.FindAnyResourceInCategory(ResourceCategory.Food);
            if (IsTracing) Trace($"  [Food] Eating {food}");
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

    private NPCMove? DecideToMove(Location destination, int maxTiles = 400)
    {
        if (destination == CurrentLocation) throw new Exception("You can't move to current location");
        int distanceTo = Map.DistanceBetween(CurrentLocation, destination);
        if (distanceTo <= maxTiles) // tiles
        {
            var nextLoc = Map.GetNextInPath(CurrentLocation, destination);
            if (nextLoc != null)
            {
                if (IsTracing) Trace($"  [Moving] Going to {destination.Name}");
                return new NPCMove(nextLoc, this);
            }
            if (IsTracing) Trace($"Can't move to {destination} - no path");
        }
        else
        {
            if (IsTracing) Trace($"Can't move to {destination} - too far");
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
            // Deliberately well above the .5 that triggers thirst in DecideSatisfyNeed. When
            // the two matched, an NPC drank until it crossed the threshold and stopped on the
            // same tick, so it lived permanently pinned at the line - never using the water it
            // was already carrying, and always one interruption from a crisis. Every other
            // need here already leaves itself headroom; water was the odd one out.
            NeedType.Water => Body.HydratedPct > .9,
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

        if (IsTracing) Trace($"  [Warmth] Rate: {warmingRate:F2}°F/min, need {degreesNeeded:F1}°F, ETA: {minutesToTarget:F0}min");

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
            if (IsTracing) Trace($"  [Survival] {durationMinutes}min away would drop warmth to {projectedWarmPct:P0} - too dangerous");
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
            if (IsTracing) Trace($"  [Prerequisite] Blocked by cold (projected warmth after {estimatedMinutes}min: {projectedWarmPct:P0})");
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

        if (IsTracing) Trace($"  [Prerequisite] {CurrentNeed} blocked by cold → switching to Warmth");
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

        // Boldness sets how far this NPC is willing to wander from safety before giving
        // up, not a coin flip re-rolled every single hop - a coin flip has no memory that
        // this is hop 3 of the same search, so a search that "should" continue can just
        // stop by chance while the need that started it hasn't gone anywhere.
        if (IsBeyondExploreLeash()) return null;

        // Get adjacent locations and head for whichever has gone longest without a visit -
        // an actual outward walk instead of a uniform random pick that can walk straight
        // back to the tile just left.
        var adjacentLocations = Map?.GetTravelOptionsFrom(CurrentLocation)?.ToList();
        if (adjacentLocations == null || adjacentLocations.Count == 0)
            return null;

        var destination = ResourceMemory.LeastRecentlyVisited(adjacentLocations)!;
        if (IsTracing) Trace($"  [Exploration] {reason} → exploring to {destination.Name}");
        return new NPCMove(destination, this);
    }

    /// <summary>
    /// True once this NPC has wandered further from its safe base (camp, or a known active
    /// fire) than its boldness allows. Bolder NPCs range further before an exploring search
    /// gives up and falls back to whatever else is available.
    /// </summary>
    private bool IsBeyondExploreLeash()
    {
        var safeBase = Camp ?? GetKnownActiveFire();
        if (safeBase == null || safeBase == CurrentLocation) return false;

        int leashTiles = (int)(2 + Personality.Boldness * 8); // ~2-10 tiles
        return Map.DistanceBetween(CurrentLocation, safeBase) >= leashTiles;
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
        // Priority order matters: this returns the FIRST match, not the last, so Warmth
        // always outranks Water outranks Rest outranks Food - matching the documented
        // need hierarchy instead of always resolving to whichever check happens to run
        // last in an if-chain.
        NeedType? need = Body.WarmPct switch
        {
            < .5 => NeedType.Warmth,
            _ => Body.HydratedPct switch
            {
                < .5 => NeedType.Water,
                _ => Body.EnergyPct switch // todo check for night
                {
                    < .3 => NeedType.Rest,
                    _ => Body.FullPct < .3 ? NeedType.Food : null
                }
            }
        };

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
        if (IsTracing) Trace($"    [GetResource] Looking for specific: {resource} at {CurrentLocation.Name}");

        // can't gather if inv already full
        var invFull = DealWithFullInventory();
        if (invFull != null)
        {
            if (IsTracing) Trace($"    [GetResource] Inventory full, returning early");
            return invFull;
        }

        // in tile -> work (if this location has this specific resource)
        var forage = CurrentLocation.GetFeature<ForageFeature>();
        if (IsTracing) Trace($"    [GetResource] ForageFeature: {(forage != null ? "yes" : "no")}, NearlyDepleted: {forage?.IsNearlyDepleted()}");
        if (forage != null)
        {
            var provided = forage.ProvidedResources();
            if (IsTracing) Trace($"    [GetResource] Provided resources: [{string.Join(", ", provided)}]");
        }
        if (forage != null && forage.CanForage() &&
            forage.ProvidedResources().Contains(resource))
        {
            int forageTime = Utils.RandInt(15, 60);
            // Check if we can survive foraging in current conditions
            if (!CanSurviveAwayFromFire(forageTime))
                return null;
            if (IsTracing) Trace($"    [GetResource] Found {resource} at current location, foraging");
            return new NPCForage(forageTime);
        }

        // in adjacent -> move to location that has this specific resource
        var adjacentLocations = Map.GetTravelOptionsFrom(CurrentLocation).ToList();
        if (IsTracing) Trace($"    [GetResource] Adjacent locations: {string.Join(", ", adjacentLocations.Select(l => l.Name))}");
        foreach (var adj in adjacentLocations)
        {
            var adjResources = GetAccessibleResources(adj);
            if (IsTracing) Trace($"    [GetResource]   {adj.Name} has: [{string.Join(", ", adjResources)}]");
        }
        var adjacentWithResource = adjacentLocations
            .Where(loc => GetAccessibleResources(loc).Contains(resource))
            .ToList();
        if (IsTracing) Trace($"    [GetResource] Adjacent with {resource}: {adjacentWithResource.Count}");

        var locWithResource = adjacentWithResource.Count > 0
            ? Utils.GetRandomFromList(adjacentWithResource)
            : null;

        // in memory -> move towards remembered location with this resource
        var remembered = ResourceMemory.WhereIs(resource).FirstOrDefault();
        if (remembered != null)
        {
            if (IsTracing) Trace($"    [GetResource] Remembered location with {resource}: {remembered.Name}");
            locWithResource ??= remembered;
        }

        // unknown? -> explore outward, as far as boldness allows
        if (locWithResource == null && !IsBeyondExploreLeash())
        {
            if (IsTracing) Trace($"    [GetResource] No known location, exploring");
            locWithResource = ResourceMemory.LeastRecentlyVisited(Map.GetTravelOptionsFrom(CurrentLocation).ToList());
        }

        if (locWithResource != null && locWithResource != CurrentLocation)
        {
            // Check if we can survive traveling in current conditions
            int estimatedTravelMinutes = 10;
            if (!CanSurviveAwayFromFire(estimatedTravelMinutes))
            {
                if (IsTracing) Trace($"    [GetResource] Too dangerous to travel");
                return null;
            }

            if (IsTracing) Trace($"    [GetResource] Moving to {locWithResource.Name}");
            var move = DecideToMove(locWithResource);
            if (move != null) return move;
        }

        if (IsTracing) Trace($"    [GetResource] Could not find {resource}");
        return null;
    }

    private NPCAction? DetermineGetResource(ResourceCategory category, bool allowCamp = true, bool urgent = false)
    {
        if (IsTracing) Trace($"    [GetResource] Looking for category: {category}{(urgent ? " (URGENT)" : "")}");

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

        // unknown? -> explore outward, as far as boldness allows
        if (locWithResource == null && !IsBeyondExploreLeash())
            locWithResource = ResourceMemory.LeastRecentlyVisited(Map.GetTravelOptionsFrom(CurrentLocation).ToList());

        if (locWithResource != null)
        {
            // Check if we can survive traveling in current conditions
            // Skip this check if urgent - we need the resource even if risky
            int estimatedTravelMinutes = 10;
            if (!urgent && !CanSurviveAwayFromFire(estimatedTravelMinutes))
            {
                if (IsTracing) Trace($"    [GetResource] Too dangerous to travel");
                return null;
            }

            if (IsTracing) Trace($"    [GetResource] Moving to {locWithResource.Name}");
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
            .Where(loc => loc != CurrentLocation)
            .Distinct()
            .ToList();

        if (knownLocations.Count == 0) return null;

        var here = Map.GetPosition(CurrentLocation);
        return knownLocations
            .Select(loc => (loc, pos: Map.GetPosition(loc)))
            .OrderBy(x => here.ManhattanDistance(x.pos))
            .ThenBy(x => x.pos.X).ThenBy(x => x.pos.Y) // ties resolve by map position, never by list order
            .FirstOrDefault().loc;
    }
    private List<Resource> GetAccessibleResources(Location location)
    {
        var resources = new List<Resource>();

        // ForageFeature - always accessible
        var forage = location.GetFeature<ForageFeature>();
        if (forage != null && forage.CanForage())
            resources.AddRange(forage.ProvidedResources());

        // HarvestableFeature - every one of them, not just whichever sorts first. This scan
        // decides where to travel, so a tile has to report everything it can give.
        foreach (var harvestable in location.Features.OfType<HarvestableFeature>())
        {
            if (!harvestable.CanBeHarvested()) continue;

            if (harvestable.RequiredToolType != null)
            {
                var tool = Inventory.GetTool(harvestable.RequiredToolType.Value);
                if (!harvestable.MeetsToolRequirement(tool))
                    continue; // can't harvest this one without the tool - others may be fine
            }
            resources.AddRange(harvestable.ProvidedResources());
        }

        // WoodedAreaFeature - requires working axe
        var wooded = location.GetFeature<WoodedAreaFeature>();
        if (wooded != null && wooded.HasTrees)
        {
            var axe = Inventory.GetTool(ToolType.Axe);
            if (axe?.Works == true)
                resources.AddRange(wooded.ProvidedResources());
        }

        // WaterFeature deliberately does NOT contribute water here. It reports Resource.Water
        // from ProvidedResources, but nothing in the game can take water out of one: its work
        // options are fishing, netting and cutting an ice hole. Believing it sent NPCs walking
        // to frozen lakes to stand at the edge of water they had no way to collect. Drinkable
        // water comes from HarvestableFeatures (rivers, meltwater, marsh water) and from
        // melting snow at a fire; if an ice hole is ever meant to fill a waterskin, that wants
        // a real work option and this scan will pick it up through the loop above.

        return resources.Distinct().ToList();
    }

    private NPCAction? GetResourceAtCurrentLocation(ResourceCategory category, bool urgent = false)
    {
        var targetResources = ResourceCategories.Items[category];

        // ForageFeature - always accessible
        var forage = CurrentLocation.GetFeature<ForageFeature>();
        if (forage != null && forage.CanForage() &&
            forage.ProvidedResources().Any(r => targetResources.Contains(r)))
        {
            int forageTime = Utils.RandInt(15, 60);
            // Urgent still means a quick grab, not license to stay out however long it
            // takes - a critically cold NPC that commits to a full session can die of
            // exposure before it ever finishes gathering.
            if (urgent) forageTime = Math.Min(forageTime, UrgentGatherCapMinutes);
            else if (!CanSurviveAwayFromFire(forageTime))
                return null;
            return new NPCForage(forageTime);
        }

        // HarvestableFeature - ask for one that yields what we actually came for
        var harvestable = WorkHandler.GetAvailableHarvestable(CurrentLocation, targetResources);
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
                if (urgent) workTime = Math.Min(workTime, UrgentGatherCapMinutes);
                else if (!CanSurviveAwayFromFire(workTime))
                    return null;
                return new NPCHarvest(workTime, targetResources);
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

            if (urgent) workTime = Math.Min(workTime, UrgentGatherCapMinutes);
            else if (!CanSurviveAwayFromFire(workTime))
                return null;

            return new NPCChopWood(workTime);
        }

        return null;
    }
    internal NPCAction? DetermineWork()
    {
        // Keep the camp fire alive proactively - otherwise it only gets attention once
        // warmth becomes a pressing need, by which point it's already gone out.
        var campFireAction = TryMaintainCampFire();
        if (campFireAction != null)
            return campFireAction;

        var water = TryBuildWaterReserve();
        if (water != null)
            return water;

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

    /// <summary>
    /// Melt snow into a carried reserve while standing at a fire, so leaving camp does not
    /// mean leaving without water. Snow can only be melted at a fire, so an NPC that only
    /// melts once it is already thirsty has to be at a fire at the moment it gets thirsty.
    /// </summary>
    private NPCAction? TryBuildWaterReserve()
    {
        if (!CookingHandler.CanMeltSnow(CurrentLocation)) return null;
        if (Inventory.Weight(Resource.Water) >= WaterReserveLiters) return null;
        if (!Inventory.CanCarry(CookingHandler.MeltSnowWaterLiters)) return null;

        if (IsTracing) Trace($"  [Water] Building reserve ({Inventory.Weight(Resource.Water):F1}/{WaterReserveLiters:F1}L)");
        return new NPCMeltSnow();
    }

    /// <summary>
    /// How much water an NPC tries to have on them before leaving a fire: one day's
    /// non-sweat water loss. Derived rather than picked so it cannot drift away from the
    /// physics - it was a flat 2.0L, which was under half a day's supply against a demand
    /// the survival model had already changed underneath it.
    /// </summary>
    private static double WaterReserveLiters =>
        SurvivalProcessor.BaseWaterLossMlPerDay / ConsumptionHandler.WaterHydrationPerLiter;

    /// <summary>
    /// Tend or relight the camp fire before it becomes a Warmth-need emergency. Only acts
    /// while standing at camp - fetching fuel from afar is HandleWarmthNeed's job.
    /// </summary>
    private NPCAction? TryMaintainCampFire()
    {
        if (Camp == null || CurrentLocation != Camp) return null;
        var fire = Camp.GetFeature<HeatSourceFeature>();
        if (fire == null) return null;

        if (fire.IsActive)
        {
            if (fire.TotalHoursRemaining >= 2) return null;

            if (FireHandler.CanTendFire(Inventory, fire))
            {
                if (IsTracing) Trace($"  [Fire] Proactively tending camp fire ({fire.TotalHoursRemaining:F1}h left)");
                return new NPCTendFire();
            }
            if (CampHas(ResourceCategory.Fuel))
            {
                if (IsTracing) Trace($"  [Fire] Getting fuel from cache to tend camp fire");
                return new NPCTakeResourceFromCache(ResourceCategory.Fuel);
            }
            return null;
        }

        if (FireHandler.CanStartFire(Inventory))
        {
            if (IsTracing) Trace($"  [Fire] Camp fire is out - relighting");
            return new NPCStartFire();
        }

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
        if (IsTracing) Trace($"    [InvCheck] Current: {Inventory.CurrentWeightKg:F2}kg, Max: {Inventory.MaxWeightKg:F2}kg, Threshold: {Inventory.MaxWeightKg * .9:F2}kg");
        // if inv full -> return to camp
        if (Inventory.CurrentWeightKg > Inventory.MaxWeightKg * .9)
        {
            if (IsTracing) Trace($"    [InvCheck] Inventory full! At camp: {CurrentLocation == Camp}");
            if (Camp != null && CurrentLocation != Camp)
            {
                if (IsTracing) Trace($"    [InvCheck] Returning to camp");
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
                        if (IsTracing) Trace($"    [InvCheck] Stashing {heaviest}");
                        return new NPCStash((ResourceCategory)heaviest);
                    }
                }
                // Try stashing water if no resources left
                double waterWeight = Inventory.Weight(Resource.Water);
                if (waterWeight > 0)
                {
                    if (IsTracing) Trace($"    [InvCheck] Stashing Water ({waterWeight:F1}L)");
                    return new NPCStashWater();
                }
                // Only tools/equipment remain - can't stash, continue with tasks
                if (IsTracing) Trace($"    [InvCheck] At camp, inv full, only tools/equipment - continuing");
                return null;
            }
        }
        return null;
    }
    private bool CampHas(ResourceCategory resourceCat) => Cache?.Has(resourceCat) ?? false;
    private Inventory? Cache => Camp?.GetFeature<CacheFeature>()?.Storage;
    /// <summary>
    /// Fuel's stockpile target in kg, in place of DAYS_RESERVE * PEOPLE_AT_CAMP *
    /// per-day-rate below. Overridable so the NPC simulation harness can sweep it - default
    /// (40) matches the original DAYS_RESERVE(2) * 20/day formula exactly, so gameplay
    /// behavior is unchanged unless a test explicitly sets this.
    /// </summary>
    /// <remarks>
    /// [ThreadStatic] so a parameter sweep can vary it without one parallel run's value
    /// leaking into another's, matching how <see cref="Utils.Rng"/> is already isolated.
    /// </remarks>
    [ThreadStatic] private static double _fuelStockpileTargetKg;
    internal static double FuelStockpileTargetKg
    {
        get => _fuelStockpileTargetKg <= 0 ? 40 : _fuelStockpileTargetKg;
        set => _fuelStockpileTargetKg = value;
    }

    /// <summary>
    /// Per-decision tracing. Off by default: these lines are debug narration nothing reads
    /// in a normal game, and building them (several interpolations and a string.Join over
    /// neighbouring tiles, every tick, per NPC) was most of the simulation harness's runtime.
    /// The action-level "Picked:"/"Completed:" lines are always emitted - they are one per
    /// action, and the harness counts behaviour from them.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    internal Action<string>? TraceSink { get; set; }

    /// <summary>
    /// True when something is listening. Guard interpolated trace strings with this - see
    /// the note above about their cost dominating simulation runtime.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool IsTracing => TraceSink != null;

    /// <summary>Narrate a decision to whoever attached a sink. No-op in a normal game.</summary>
    internal void Trace(string line) => TraceSink?.Invoke(line);

    /// <summary>
    /// How much of a resource the camp wants stockpiled. Fuel bypasses the per-day-rate
    /// formula entirely so it can be overridden directly; every other category keeps the
    /// original (int) truncation, which some categories rely on (Tinder's 0.2 truncates to
    /// a target of 0, meaning any amount at all counts as enough).
    /// </summary>
    private static double StockpileTarget(ResourceCategory resource)
    {
        if (resource == ResourceCategory.Fuel) return FuelStockpileTargetKg;

        const int DAYS_RESERVE = 2;
        const int PEOPLE_AT_CAMP = 1; // todo add property to location
        double neededPerPersonDay = resource switch
        {
            ResourceCategory.Tinder => .1,
            ResourceCategory.Food => 1,
            ResourceCategory.Water => 3, // 3 liters
            ResourceCategory.Medicine => .1,
            ResourceCategory.Material => 0,
            ResourceCategory.Log => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
        return (int)(DAYS_RESERVE * PEOPLE_AT_CAMP * neededPerPersonDay);
    }

    internal bool IsEnoughStockpiled(ResourceCategory resource)
    {
        if (Cache is null) return false;

        // Check if resource exists in cache
        bool hasResource = CampHas(resource);
        if (!hasResource) return false;

        double target = StockpileTarget(resource);
        double currentAmount = Cache.GetWeight(resource);

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
        if (IsTracing) Trace($"Need {category}");
        if (category == null) return null;

        return TryCraftFromCategory(category.Value);
    }

    private NPCAction? TryCraftFromCategory(NeedCategory category)
    {
        if (IsTracing) Trace($"    [Craft] TryCraftFromCategory({category})");
        var options = CraftingSystem.GetOptionsForNeed(category, Inventory, true);
        if (IsTracing) Trace($"    [Craft] Options count: {options.Count()}");

        // Try to craft if we can
        var craftable = options.FirstOrDefault(o => o.CanCraft(Inventory));
        if (craftable != null)
        {
            if (IsTracing) Trace($"    [Craft] Can craft: {craftable.Name}");
            return new NPCCraft(craftable);
        }

        // Can't craft - find first missing thing and resolve it
        foreach (var option in options)
        {
            if (IsTracing) Trace($"    [Craft] Checking option: {option.Name}");

            // Check missing tools FIRST - need tools before gathering materials
            foreach (var toolType in option.RequiredTools)
            {
                var tool = Inventory.GetTool(toolType);
                if (IsTracing) Trace($"    [Craft]   Required tool: {toolType}, have: {tool?.Name ?? "none"}");
                if (tool == null || tool.Durability < 1)
                {
                    return DetermineGetTool(toolType);
                }
            }

            // Then check missing materials
            foreach (var req in option.Requirements)
            {
                var needed = GetMissingCount(req);
                if (IsTracing) Trace($"    [Craft]   Req: {req.Material}, need {req.Count}, missing {needed}");
                if (needed > 0)
                {
                    // Specific resource → search for that exact resource
                    if (req.Material is MaterialSpecifier.Specific(var resource))
                    {
                        if (IsTracing) Trace($"    [Craft]   Getting specific resource: {resource}");
                        return DetermineGetSpecificResource(resource);
                    }
                    // Category → search for any resource in that category
                    else if (req.Material is MaterialSpecifier.Category(var resCat))
                    {
                        if (IsTracing) Trace($"    [Craft]   Getting category: {resCat}");
                        return DetermineGetResource(resCat);
                    }
                }
            }
        }
        if (IsTracing) Trace($"    [Craft] No craftable options found");
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

        if (IsTracing) Trace($"[NPC:{Name}] Improving shelter ({type.Value.ToString().ToLower()}) with {material.Value.ToDisplayName()}");
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
        if (IsTracing) Trace($"    [Craft] TryCraftSpecificTool({toolType})");

        // Get all craft options that produce this specific tool type
        var options = CraftingSystem.AllOptions
            .Where(o => o.GearFactory != null && o.GearFactory(1).ToolType == toolType)
            .ToList();

        if (IsTracing) Trace($"    [Craft] Options for {toolType}: {options.Count}");

        // Try to craft if we can
        var craftable = options.FirstOrDefault(o => o.CanCraft(Inventory));
        if (craftable != null)
        {
            if (IsTracing) Trace($"    [Craft] Can craft: {craftable.Name}");
            return new NPCCraft(craftable);
        }

        // Can't craft - find the first missing thing and resolve it. Prerequisite tools
        // come first: a Stone Axe needs a KnappingStone, and an NPC that only ever looks at
        // materials sees every material present, concludes nothing is missing, and gives up
        // - which is why it could never build the axe that unlocks felling trees.
        // _toolsBeingResolved breaks the cycle a tool-requires-a-tool chain would otherwise
        // create, which is why this check was originally left out entirely.
        foreach (var option in options)
        {
            if (IsTracing) Trace($"    [Craft] Checking option: {option.Name}");

            foreach (var prerequisite in option.RequiredTools)
            {
                var held = Inventory.GetTool(prerequisite);
                if (held != null && held.Durability >= 1) continue;
                if (_toolsBeingResolved.Contains(prerequisite)) continue;

                if (IsTracing) Trace($"    [Craft]   Missing prerequisite tool: {prerequisite}");
                _toolsBeingResolved.Add(prerequisite);
                try
                {
                    var getPrerequisite = DetermineGetTool(prerequisite);
                    if (getPrerequisite != null) return getPrerequisite;
                }
                finally
                {
                    _toolsBeingResolved.Remove(prerequisite);
                }
            }

            foreach (var req in option.Requirements)
            {
                var needed = GetMissingCount(req);
                if (IsTracing) Trace($"    [Craft]   Req: {req.Material}, need {req.Count}, missing {needed}");
                if (needed > 0)
                {
                    if (req.Material is MaterialSpecifier.Specific(var resource))
                    {
                        if (IsTracing) Trace($"    [Craft]   Getting specific resource: {resource}");
                        return DetermineGetSpecificResource(resource);
                    }
                    else if (req.Material is MaterialSpecifier.Category(var resCat))
                    {
                        if (IsTracing) Trace($"    [Craft]   Getting category: {resCat}");
                        return DetermineGetResource(resCat);
                    }
                }
            }
        }

        if (IsTracing) Trace($"    [Craft] No options found for {toolType}");
        return null;
    }

    private NPCAction? DetermineGetTool(ToolType toolType)
    {
        if (IsTracing) Trace($"    [GetTool] Looking for {toolType}");

        // Check cache for this tool
        var cache = Camp?.GetFeature<CacheFeature>()?.Storage;
        var cachedTool = cache?.Tools.FirstOrDefault(t => t.ToolType == toolType && t.Works);

        if (cachedTool != null)
        {
            if (IsTracing) Trace($"    [GetTool] Found {cachedTool.Name} in cache");
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
        if (IsTracing) Trace($"    [GetTool] Not in cache, trying to craft {toolType}");
        return TryCraftSpecificTool(toolType);
    }

    private NPCAction DetermineIdle(SurvivalContext context)
    {
        // DetermineIdle is the final fallback - it must always return an action
        // without calling back to DetermineActionForNeed (which would cause recursion)

        if (context.IsNight && Body.EnergyPct < .8)
        {
            var sleep = DecideSleep();
            if (sleep != null) return sleep;
        }

        // todo
        // follow high relationship actors

        // Don't idle-rest while cold and away from a fire - resting here just keeps
        // cooling. Head for the fire, or failing that go get fuel, instead.
        if (Body.WarmPct < .5 && !CurrentLocation.HasActiveHeatSource())
        {
            if (Camp != null && CurrentLocation != Camp)
            {
                var moveToCamp = DecideToMove(Camp);
                if (moveToCamp != null) return moveToCamp;
            }
            var getFuel = DetermineGetResource(ResourceCategory.Fuel, urgent: true);
            if (getFuel != null) return getFuel;
        }

        // Weighted random idle action
        // options = WeightedList:
        //     { REST_NEAR_FIRE,   50 }
        //     { SIT_AND_WATCH,    20 }
        //     { TEND_FIRE_ANYWAY, 15 }
        //     { WANDER_NEARBY,    10 }
        //     { CHECK_ON_OTHERS,   5 }

        // if context.Time.IsNight and CAN_SLEEP(npc, context):
        //     options.Add(SLEEP, 40)

        // A need still unmet should re-evaluate soon rather than commit to a long rest.
        bool anyNeedUnmet = Body.WarmPct < .5 || Body.HydratedPct < .5 || Body.EnergyPct < .3 || Body.FullPct < .3;
        int restMinutes = anyNeedUnmet ? Utils.RandInt(3, 5) : Utils.RandInt(5, 30);
        return new NPCRest(restMinutes);
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
        foreach (var herd in _currentHerds.At(position))
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
