using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actions;

public enum PredatorSceneKind { PackSigns, EyesInTreeline, Circling, ThePackCommits, SomethingWatching, StalkerCircling, PredatorRevealed, Ambush, ShadowMovement, CutOff, SpottedInOpen, MutualVisibility, EscapeIntoThicket, BearAtFishingHole, WolvesCirclingNets, RustleAtCampEdge, PredatorAtTrapLine, WolvesSmellBlood, ScavengersGambit, TheFollowers }

public enum PredatorReaction { Observe, Follow, Withdraw }

/// <summary>
/// A predator-scene choice that is resolved by the world rather than by its authored outcome
/// weights: walking away, dropping food, making or using fire, taking meat, or waiting.
/// Choices that move you or light something should author a single lead-in result; the
/// binder appends what actually happened.
/// </summary>
public enum SceneAction
{
    None,
    /// <summary>Walk to the neighbouring tile farthest from the animals.</summary>
    Retreat,
    /// <summary>Walk to the neighbouring tile closest to camp.</summary>
    RetreatTowardCamp,
    /// <summary>Put all carried fish on the ground, then retreat.</summary>
    DropFishAndRun,
    /// <summary>Put all carried meat on the ground, then retreat.</summary>
    DropMeatAndRun,
    /// <summary>Put all carried meat on the ground and stay.</summary>
    LeaveMeat,
    /// <summary>Try to start a fire here with your own tools.</summary>
    StartFire,
    LightTorch,
    /// <summary>Threaten with a torch or the fire already here.</summary>
    UseFire,
    /// <summary>Feed the fire here, then threaten with it.</summary>
    BuildUpFire,
    /// <summary>Cut meat from the carcass for the outcome's time, then retreat.</summary>
    HarvestAndRun,
    /// <summary>Stay put and watch; time passes as rest.</summary>
    Wait
}

/// <summary>Source and situation rules for authored wildlife scenes. No progression meter.</summary>
public static class AuthoredPredatorScenes
{
    public static EventResult When(this EventResult result, Func<GameContext, bool> condition)
    {
        var prior = result.Validate;
        result.Validate = c => (prior?.Invoke(c) ?? true) && condition(c);
        return result;
    }

    public static EventResult ObservesPredator(this EventResult result, AnimalType? species = null)
    {
        result.PredatorReaction = PredatorReaction.Observe;
        result.PredatorSpecies = species;
        return result;
    }
    public static EventResult PredatorFollows(this EventResult result)
    { result.PredatorReaction = PredatorReaction.Follow; return result; }
    public static EventResult PredatorWithdraws(this EventResult result)
    { result.PredatorReaction = PredatorReaction.Withdraw; return result; }
    public static EventResult ConfrontPredator(this EventResult result, AnimalType species, int distance, double boldness)
        => result.ObservesPredator(species).Encounter(species, distance, boldness);

    public static Herd? Source(GameContext ctx, PredatorSceneKind kind) => ctx.Herds.FirstOrDefault(h =>
        PredatorInteractions.CanObserve(ctx, h) && h.IsPredator &&
        (kind switch
        {
            PredatorSceneKind.BearAtFishingHole => h.AnimalType == AnimalType.Bear,
            PredatorSceneKind.WolvesCirclingNets or PredatorSceneKind.WolvesSmellBlood or PredatorSceneKind.ScavengersGambit or PredatorSceneKind.TheFollowers => h.AnimalType == AnimalType.Wolf,
            PredatorSceneKind.PackSigns or PredatorSceneKind.EyesInTreeline or PredatorSceneKind.Circling or PredatorSceneKind.ThePackCommits => h.Count >= 2 && h.AnimalType == AnimalType.Wolf,
            _ => h.AnimalType != AnimalType.SaberTooth && h == PredatorInteractions.Observed(ctx)
        }));

    public static CarcassFeature? Carcass(GameContext ctx) => ctx.CurrentLocation.Features.OfType<CarcassFeature>()
        .FirstOrDefault(c => c.MeatRemainingKg > 0 && !ctx.CurrentLocation.IsCovered(c));

    public static bool Eligible(GameContext ctx, PredatorSceneKind kind, Herd? source)
    {
        if (!PredatorInteractions.CanObserve(ctx, source)) return false;
        return kind switch
        {
            PredatorSceneKind.BearAtFishingHole => ctx.CurrentActivity == ActivityType.Fishing && ctx.Check(EventCondition.HasIceHole),
            PredatorSceneKind.WolvesCirclingNets => ctx.CurrentActivity == ActivityType.Fishing && ctx.CurrentLocation.HasFeature<NetFishingFeature>() && source!.Count >= 3,
            PredatorSceneKind.WolvesSmellBlood => ctx.CurrentActivity == ActivityType.Butchering && Carcass(ctx)?.AnimalType == AnimalType.Mammoth,
            PredatorSceneKind.ScavengersGambit => Carcass(ctx) != null && source!.State == HerdState.Feeding && PredatorInteractions.WithinReach(ctx, source) &&
                ctx.Herds.Any(h => h.AnimalType == AnimalType.Hyena && PredatorInteractions.CanObserve(ctx, h)),
            PredatorSceneKind.RustleAtCampEdge => ctx.IsAtCamp && ctx.Check(EventCondition.Awake),
            PredatorSceneKind.PredatorAtTrapLine => ctx.CurrentLocation.GetFeature<SnareLineFeature>() is { } traps && !ctx.CurrentLocation.IsCovered(traps) && (traps.HasBaitedSnares || traps.HasCatchWaiting),
            PredatorSceneKind.PackSigns => ctx.Map!.Tracks.ReadPassages(ctx.Map.CurrentPosition).Any(p => p.Maker == TrackMaker.Paw),
            PredatorSceneKind.ThePackCommits or PredatorSceneKind.Ambush => PredatorInteractions.WithinReach(ctx, source) && PredatorInteractions.FollowingPlayer(ctx, source!) && source!.BoldnessToward(ctx.player, ctx) >= 0.7,
            PredatorSceneKind.Circling or PredatorSceneKind.StalkerCircling or PredatorSceneKind.PredatorRevealed => PredatorInteractions.WithinReach(ctx, source) && PredatorInteractions.FollowingPlayer(ctx, source!),
            PredatorSceneKind.EyesInTreeline or PredatorSceneKind.TheFollowers => PredatorInteractions.FollowingPlayer(ctx, source!),
            PredatorSceneKind.CutOff => PredatorInteractions.WithinReach(ctx, source) && Situations.TrappedByTerrain(ctx),
            PredatorSceneKind.SpottedInOpen or PredatorSceneKind.MutualVisibility => PredatorInteractions.CanDetect(ctx, source!, ctx.player),
            _ => true
        };
    }

    public static void Bind(GameContext ctx, GameEvent evt)
    {
        if (evt.PredatorScene is not { } kind) return;
        var source = Source(ctx, kind);
        var location = ctx.CurrentLocation;
        var prior = evt.Validate;
        evt.SourceHerd = source;
        evt.Validate = c => (prior?.Invoke(c) ?? true) && c.CurrentLocation == location && Eligible(c, kind, source);
        // Every branch belongs to the same scene source, including non-combat outcomes.
        foreach (var choice in evt.AuthoredChoices)
            foreach (var result in choice.Results)
            {
                result.SourceHerd = source;
                var valid = result.Validate;
                result.Validate = c => (valid?.Invoke(c) ?? true) && c.CurrentLocation == location && PredatorInteractions.CanObserve(c, source) &&
                    (result.PredatorSpecies == null || source?.AnimalType == result.PredatorSpecies) &&
                    (result.NewDamage == null || evt.PredatorScene == PredatorSceneKind.WolvesSmellBlood || PredatorInteractions.WithinReach(c, source)) &&
                    (!result.ResourceCosts.Any(cost => cost.Type is ResourceType.Food or ResourceType.Meat or ResourceType.Fish) || PredatorInteractions.WithinReach(c, source));
            }
        BindActions(ctx, evt, source);
    }

    private static void BindActions(GameContext ctx, GameEvent evt, Herd? source)
    {
        foreach (var choice in evt.AuthoredChoices)
        foreach (var result in choice.Results)
        {
            var action = choice.Action;
            string lead = result.Message + " ";
            if (action is SceneAction.UseFire or SceneAction.BuildUpFire)
            {
                var prior = result.Validate;
                result.Validate = c => (prior?.Invoke(c) ?? true) && (c.Inventory.HasLitTorch || c.CurrentLocation.HasActiveHeatSource()) &&
                    PredatorInteractions.WithinReach(c, source);
                result.PredatorReaction = null;
                int fuelCount = result.ResourceCosts.FirstOrDefault(cost => cost.Type == ResourceType.Fuel)?.Amount ?? 1;
                result.ResourceCosts.Clear();
                result.WorldAction = c =>
                {
                    if (action == SceneAction.BuildUpFire && c.CurrentLocation.GetFeature<HeatSourceFeature>() is { } existing)
                    {
                        foreach (var fuel in new[] { Resource.Stick, Resource.Pine, Resource.Birch, Resource.Oak, Resource.Charcoal })
                            if (c.Inventory.Count(fuel) > 0)
                            { Handlers.FireHandler.AddFuel(c.Inventory, existing, fuel, fuelCount); break; }
                    }
                    PredatorInteractions.Deter(c, source!, fire: true);
                    return Task.CompletedTask;
                };
                result.DescribeAfterAction = c => lead + Events.PredatorEventFactory.ObserveResult(c, source);
            }
            if (action == SceneAction.Wait) result.TimeActivity = ActivityType.Resting;
            bool dropFish = action == SceneAction.DropFishAndRun;
            bool dropMeat = action is SceneAction.DropMeatAndRun or SceneAction.LeaveMeat;
            bool moves = action is SceneAction.Retreat or SceneAction.RetreatTowardCamp or SceneAction.DropFishAndRun or SceneAction.DropMeatAndRun;
            if (moves || dropMeat)
            {
                var origin = ctx.CurrentLocation;
                var destination = moves ? RetreatDestination(ctx, source, towardCamp: action == SceneAction.RetreatTowardCamp) : null;
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && (!moves || dropFish || dropMeat || destination != null) &&
                    (!dropFish || c.Inventory.HasFish) &&
                    (!dropMeat || c.Inventory.Weight(Resource.RawMeat) + c.Inventory.Weight(Resource.CookedMeat) > 0);
                // A moving response is resolved by travel/AI, never by a scripted escape or attack.
                result.PredatorReaction = null;
                result.SpawnEncounter = null;
                result.ResourceCosts.Clear();
                result.TimeAddedMinutes = 0;
                result.AbortsAction = true;
                result.WorldAction = async c =>
                {
                    if (dropFish || dropMeat)
                    {
                        var food = new Inventory();
                        var resources = dropFish ? new[] { Resource.RawFish, Resource.CookedFish } :
                            new[] { Resource.RawMeat, Resource.CookedMeat };
                        foreach (var resource in resources)
                            while (c.Inventory.Count(resource) > 0) food.Add(resource, c.Inventory.Pop(resource));
                        origin.AddGroundItems(food);
                    }
                    if (destination != null)
                        await new Expeditions.TravelRunner(c).TravelToLocation(destination);
                };
                result.DescribeAfterAction = c => lead + (dropFish || dropMeat ? "You leave food on the ground. " : "") +
                    (!moves ? "" : c.CurrentLocation != origin ? $"You reach {c.CurrentLocation.Name}. " : "You are still on the same ground. ") +
                    Events.PredatorEventFactory.ObserveResult(c, source);
            }
            if (action is SceneAction.StartFire or SceneAction.LightTorch)
            {
                bool torch = action == SceneAction.LightTorch;
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && (torch ? Handlers.TorchHandler.CanLightTorch(c) :
                    Handlers.FireHandler.GetBestTool(c.Inventory) != null && Handlers.FireHandler.GetBestTinder(c.Inventory) != null && c.Inventory.Count(Resource.Stick) > 0);
                result.ResourceCosts.Clear();
                result.PredatorReaction = null;
                result.SpawnEncounter = null;
                result.NewDamage = null;
                result.TimeActivity = ActivityType.TendingFire;
                if (torch) result.TimeAddedMinutes = 0; // Torch handler owns its time.
                string attempt = "You prepare your fire-making supplies.";
                result.WorldAction = async c =>
                {
                    if (torch)
                    {
                        await Handlers.TorchHandler.LightTorch(c);
                        attempt = c.Inventory.HasLitTorch ? "Your torch is burning." : "You have no burning torch.";
                    }
                    else
                    {
                        var started = Handlers.FireHandler.AttemptStartFire(c.player, c.Inventory, c.CurrentLocation,
                            Handlers.FireHandler.GetBestTool(c.Inventory)!, Handlers.FireHandler.GetBestTinder(c.Inventory)!.Value,
                            c.player.Skills.GetSkill("Firecraft").Level, c.GameTime.Hour, c.CurrentLocation.GetFeature<HeatSourceFeature>());
                        attempt = started.Message;
                    }
                    if (c.Inventory.HasLitTorch || c.CurrentLocation.HasActiveHeatSource()) PredatorInteractions.Deter(c, source!, fire: true);
                };
                result.DescribeAfterAction = c => lead + attempt + " " + Events.PredatorEventFactory.ObserveResult(c, source);
            }
            bool harvest = action == SceneAction.HarvestAndRun;
            if (harvest || evt.PredatorScene == PredatorSceneKind.WolvesSmellBlood && result.RewardPools.Count > 0)
            {
                var carcass = Carcass(ctx);
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && carcass != null && ReferenceEquals(Carcass(c), carcass);
                result.RewardPools.Clear();
                result.TimeActivity = ActivityType.Butchering;
                result.AfterTimeAction = c =>
                {
                    var meat = carcass!.Harvest(result.TimeAddedMinutes, c.Inventory.HasCuttingTool, c.Check(EventCondition.Clumsy), ButcheringMode.QuickStrip);
                    var excess = c.Inventory.CombineWithCapacity(meat);
                    c.CurrentLocation.AddGroundItems(excess);
                };
                if (harvest)
                {
                    var destination = RetreatDestination(ctx, source, towardCamp: false);
                    var allowed = result.Validate;
                    result.Validate = c => (allowed?.Invoke(c) ?? true) && destination != null;
                    result.AbortsAction = true;
                    result.AfterTimeWorldAction = c => new Expeditions.TravelRunner(c).TravelToLocation(destination!);
                }
                result.DescribeAfterAction = c => "You cut what you can from the carcass. What you cannot carry remains on the ground. " +
                    (harvest ? $"You are now at {c.CurrentLocation.Name}. " : "") + Events.PredatorEventFactory.ObserveResult(c, source);
            }
        }
    }

    /// <summary>The passable neighbour to walk to: farthest from the animals, or closest to camp.</summary>
    private static Environments.Location? RetreatDestination(GameContext ctx, Herd? source, bool towardCamp)
    {
        if (ctx.Map == null || source == null) return null;
        var map = ctx.Map;
        var campPos = map.GetPosition(ctx.Camp);
        var options = map.CurrentPosition.GetCardinalNeighbors()
            .Where(p => map.GetLocationAt(p)?.IsPassable == true && !map.IsEdgeBlocked(map.CurrentPosition, p, ctx.Weather.CurrentSeason));
        var ordered = towardCamp
            ? options.OrderBy(p => p.ManhattanDistance(campPos)).ThenByDescending(p => p.ManhattanDistance(source.Position))
            : options.OrderByDescending(p => p.ManhattanDistance(source.Position));
        return ordered.Select(p => map.GetLocationAt(p)).FirstOrDefault();
    }

    public static void ApplyReaction(GameContext ctx, EventResult result)
    {
        var source = result.SourceHerd;
        if (source == null || !PredatorInteractions.CanObserve(ctx, source)) return;
        switch (result.PredatorReaction)
        {
            case PredatorReaction.Follow:
                if (!PredatorInteractions.CanDetect(ctx, source, ctx.player)) return;
                source.Pursuit = new PredatorInteraction { Target = ctx.player, LastDetectedLocation = ctx.CurrentLocation,
                    LastDetectedMinute = ctx.TotalMinutesElapsed };
                break;
            case PredatorReaction.Withdraw:
                source.Pursuit = null;
                source.Fear = Math.Max(source.Fear, 0.6);
                source.Behavior?.TriggerFlee(source, ctx.Map!.CurrentPosition, ctx);
                break;
        }
    }
}
