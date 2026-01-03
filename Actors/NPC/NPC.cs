using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actors;

public class NPC(string name, Personality personality, Location currentLocation, GameMap map) : Actor(name, Body.BaselineHumanStats, currentLocation, map)
{
    public Personality Personality = personality;
    public Inventory Inventory = new();
    public Dictionary<Actor, double> Relationships = new();
    public ResourceMemory ResourceMemory = new();
    public Location? Camp;
    public NPCAction? CurrentAction;
    public NeedType? CurrentNeed;
    public override double AttackDamage => Inventory.Weapon?.Damage ?? .1;

    public override double BlockChance => throw new NotImplementedException();

    public override string AttackName => throw new NotImplementedException();

    public override DamageType AttackType => throw new NotImplementedException();

    // pending suggestion?
    // following?

    public override void Update(int minutes, SurvivalContext context)
    {
        base.Update(minutes, context);
        for (int i = 0; i < minutes; i++)
        {
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
            ContinueAction();
        }
    }
    private bool ShouldInterrupt()
    {
        if (DecideRespondToThreat())
            return true;

        // critical needs interrupt if higher priority 
        var minimumCriticalNeed = NeedType.Food;
        var need = GetCriticalNeed();
        if (need >= minimumCriticalNeed && CurrentNeed != null && need > CurrentNeed)
            return true;

        // todo check player interrupts/suggestions

        return false;
    }
    private void ContinueAction()
    {
        if (CurrentAction == null) throw new NullReferenceException("Something's fucked"); // should never happen
        CurrentAction.MinutesSpent++;
        // check for completion
        if (CurrentAction.IsComplete())
        {
            CurrentAction.Complete(this);
            int minutesLeftover = CurrentAction.MinutesSpent - CurrentAction.DurationMinutes;
            CurrentAction = null;
        }
    }
    private NPCAction DetermineActionForNeed(SurvivalContext context)
    {
        if (CurrentNeed == NeedType.Warmth)
        {
            // known fire -> go there
            var fire = GetKnownFire();
            if (fire != null && fire != CurrentLocation)
            {
                var nextLoc = Map.GetNextInPath(CurrentLocation, fire);
                if (nextLoc != null)
                    return new NPCMove(nextLoc, this);
            }
            // no known fire -> make one
            // at fire but not lit -> light it
            if (fire == null || !fire.GetFeature<HeatSourceFeature>()!.IsActive)
            {
                return new NPCStartFire();
            }
            // at fire but not hot enough -> add fuel
            if (fire != null && CurrentLocation.HasActiveHeatSource()
                    && context.LocationTemperature < 32) // todo add temp delta tracking instead - for now use feels like
            {
                if (Inventory.Has(ResourceCategory.Fuel))
                {
                    return new NPCTendFire();
                }
                // need to add fuel or start fire but no resources? 
                // get resources if needed
                foreach (ResourceCategory cat in new List<ResourceCategory> { ResourceCategory.Tinder, ResourceCategory.Fuel })
                {
                    if (!HasResource(cat))
                    {
                        var get = DetermineGetResource(cat);
                        if (get != null) return get;
                    }
                }
            }
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
    private Location? GetKnownFire()
    {
        if (CurrentLocation.HasActiveHeatSource()) return CurrentLocation;
        if (Camp?.HasActiveHeatSource() ?? false) return Camp;
        // todo - track other fires
        return null;
    }

    private bool IsCriticalNeedSatisfied()
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

    private void DetermineNeed()
    {
        if (DetermineCriticalNeed())
            return;
        if (DecideSatisfyNeed())
            return;
    }

    private bool DecideRespondToThreat()
    {
        return false;
    }
    private bool DetermineCriticalNeed()
    {
        var need = GetCriticalNeed();
        if (need == null) return false;
        CurrentNeed = need;
        return true;
    }
    private NeedType? GetCriticalNeed()
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
    private NPCAction? DetermineGetResource(ResourceCategory category, bool allowCamp = true)
    {
        // todo
        // in tile -> work
        var work = GetResourceAtCurrentLocation(category);
        if (work != null) return work;
        // in adjacent -> move to
        var locWithResource = Map.GetTravelOptionsFrom(CurrentLocation)
           .FirstOrDefault(x => x.ListResourcesHere().Any(x => ResourceCategories.Items[category].Contains(x)));

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
            return new NPCMove(locWithResource, this);
        }
        // otherwise wait
        return null;
    }
    private Location? GetClosestKnownResource(ResourceCategory category)
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
    private NPCAction? GetResourceAtCurrentLocation(ResourceCategory category)
    {
        if (CurrentLocation.ListResourcesHere().Any(x => ResourceCategories.Items[category].Contains(x)))
        {
            // if has axe and need wood or logs, forage
            // if has weapon and needs food hunt
            // else if foragefeature has resource, forage
            var forage = CurrentLocation.GetFeature<ForageFeature>();
            if (forage != null && !forage.IsDepleted() && forage.ProvidedResources().Any(x => ResourceCategories.Items[category].Contains(x)))
            {
                return new NPCForage(Utils.RandInt(15, 60));
            }
        }
        return null;
    }
    private NPCAction? DetermineWork()
    {
        // for now just stockpile resources, maybe in future improve camp etc?
        if (IsEnoughStockpiled(ResourceCategory.Fuel) && Utils.FlipCoin())
        {
            // stockpile wood 50% of the time
            return Stockpile(ResourceCategory.Fuel);
        }
        else if (IsEnoughStockpiled(ResourceCategory.Water) && Utils.FlipCoin())
        {
            // stockpile water 25% of the time
            return Stockpile(ResourceCategory.Water);
        }
        else if (IsEnoughStockpiled(ResourceCategory.Food))
        {
            return Stockpile(ResourceCategory.Food);
        }
        else return null;
    }
    private NPCAction? Stockpile(ResourceCategory resource)
    {
        // if at camp and have stuff -> store in cache
        if (CurrentLocation == Camp && Inventory.GetWeight(resource) >= 1.0)
        {
            return new NPCStash(resource);
        }
        // if inv full -> return to camp
        if (Inventory.CurrentWeightKg > Inventory.MaxWeightKg * .9)
        {
            if (Camp != null)
            {
                var nextLoc = Map.GetNextInPath(CurrentLocation, Camp);
                if (nextLoc != null)
                    return new NPCMove(nextLoc, this);
            }
        }
        // else -> get resource ! at camp
        return DetermineGetResource(resource, allowCamp: false);
    }
    private bool CampHas(ResourceCategory resourceCat) => Cache?.Has(resourceCat) ?? false;
    private Inventory? Cache => Camp?.GetFeature<CacheFeature>()?.Storage;
    private bool IsEnoughStockpiled(ResourceCategory resource)
    {
        int DAYS_RESERVE = 2;
        int PEOPLE_AT_CAMP = 1; // todo add property to location
        if (Cache is null) return false;
        if (!CampHas(resource)) return false;
        double kgNeededPerPersonDay = resource switch
        {
            ResourceCategory.Fuel => 20,
            ResourceCategory.Tinder => .1,
            ResourceCategory.Food => 1,
            ResourceCategory.Water => 3, // 3l
            ResourceCategory.Medicine => .1,
            ResourceCategory.Material => 0,
            ResourceCategory.Log => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };
        int targetKg = (int)(DAYS_RESERVE * PEOPLE_AT_CAMP * kgNeededPerPersonDay);
        if (Cache.GetWeight(resource) >= targetKg) return true;
        return false;
    }
    private NPCAction? DetermineCraft()
    {
        return null; // todo - phase 2
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

        return new NPCIdle(Utils.RandInt(5, 30));
    }
    private NPCSleep? DecideSleep()
    {
        if (CanSleep())
        {
            return new NPCSleep(Utils.RandInt(30, 90)); // sleep in segments to wake up to tend to fire
        }
        return null;
    }
    private bool CanSleep()
    {
        // Must be at camp
        if (CurrentLocation != Camp) return false;
        // Not freezing
        if (Body.WarmPct < .2) return false;
        // Fire has runway of 2 hours
        if (CurrentLocation.HasActiveHeatSource() && CurrentLocation.GetFeature<HeatSourceFeature>()!.BurningHoursRemaining < 2)
            return false;
        // No threats
        if (DecideRespondToThreat()) return false;

        return true;
    }
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

public class NPCIdle(int minutes) : NPCAction("Idling", minutes, ActivityType.Idle)
{
    public override void Complete(NPC npc) { } // does nothing
}

public class NPCMove(Location destination, NPC npc) :
    NPCAction($"Traveling to {destination.Name}", TravelProcessor.GetTraversalMinutes(npc.CurrentLocation, destination, npc, npc.Inventory), ActivityType.Traveling)
{
    public override void Complete(NPC npc)
    {
        npc.CurrentLocation = destination;
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
public class NPCHarvest
{
    // todo
}
public class NCPChopWood
{
    // todo
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