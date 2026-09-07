using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actions;

public enum PredatorSceneKind { PackSigns, EyesInTreeline, Circling, ThePackCommits, SomethingWatching, StalkerCircling, PredatorRevealed, Ambush, ShadowMovement, CutOff, SpottedInOpen, MutualVisibility, EscapeIntoThicket, BearAtFishingHole, WolvesCirclingNets, RustleAtCampEdge, PredatorAtTrapLine, WolvesSmellBlood, ScavengersGambit, TheFollowers }

public enum PredatorReaction { Observe, Follow, Withdraw }

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
                    (result.Cost?.Type != ResourceType.Food || PredatorInteractions.WithinReach(c, source));
            }
        BindActions(ctx, evt, source);
    }

    private static void BindActions(GameContext ctx, GameEvent evt, Herd? source)
    {
        foreach (var choice in evt.AuthoredChoices)
        foreach (var result in choice.Results)
        {
            var label = choice.Label;
            bool retreat = label is "Back Away Slowly" or "Calculated Retreat" or "Fall Back" or "Flee" or "Break and Run" or
                "Create Distance" or "Retreat" or "Abandon the Nets" or "Let Them Have It" or "Backtrack" or
                "Pick Up Pace Toward Camp" or "Keep Moving Steadily" or "Keep Moving, Stay Alert" or "Move Quickly, Stay Alert" or
                "Descend Carefully" or "Push Deeper" or "Find Defensible Ground" or "Finish and Leave";
            bool drop = label is "Drop Fish and Run" or "Drop All Meat and Flee" or "Leave Some Meat Behind";
            bool fire = label is "Light a Fire" or "Start Fire Here";
            bool torch = label == "Light a Torch";
            bool useFire = label is "Fire Drives Them Off" or "Build Up Fire" or "Build Up the Fire";
            if (useFire)
            {
                var prior = result.Validate;
                result.Validate = c => (prior?.Invoke(c) ?? true) && (c.Inventory.HasLitTorch || c.CurrentLocation.HasActiveHeatSource()) &&
                    PredatorInteractions.WithinReach(c, source);
                result.PredatorReaction = null;
                int fuelCount = result.Cost?.Amount ?? 1;
                result.Cost = null;
                result.WorldAction = c =>
                {
                    if (label is "Build Up Fire" or "Build Up the Fire" && c.CurrentLocation.GetFeature<HeatSourceFeature>() is { } existing)
                    {
                        foreach (var fuel in new[] { Resource.Stick, Resource.Pine, Resource.Birch, Resource.Oak, Resource.Charcoal })
                            if (c.Inventory.Count(fuel) > 0)
                            { Handlers.FireHandler.AddFuel(c.Inventory, existing, fuel, fuelCount); break; }
                    }
                    PredatorInteractions.Deter(c, source!, fire: true);
                    return Task.CompletedTask;
                };
                result.DescribeAfterAction = c => "You use the flame to keep the animals back. " + Events.PredatorEventFactory.ObserveResult(c, source);
            }
            bool harvest = label is "Work Faster, Grab What You Can" or "Steal While They're Distracted";
            bool wait = label is "Wait Them Out" or "Wait It Out" or "Wait for Wolves to Leave" or "Hold Position and Assess" or "Stop and Observe";
            if (wait) result.TimeActivity = ActivityType.Resting;
            if (retreat || drop)
            {
                var origin = ctx.CurrentLocation;
                var destination = ctx.Map == null || source == null ? null : ctx.Map.CurrentPosition.GetCardinalNeighbors()
                    .Where(p => ctx.Map.GetLocationAt(p)?.IsPassable == true && !ctx.Map.IsEdgeBlocked(ctx.Map.CurrentPosition, p, ctx.Weather.CurrentSeason))
                    .OrderByDescending(p => p.ManhattanDistance(source.Position)).Select(p => ctx.Map.GetLocationAt(p)).FirstOrDefault();
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && (!retreat || destination != null) &&
                    (!drop || (label == "Drop Fish and Run" ? c.Inventory.HasFish : c.Inventory.Weight(Resource.RawMeat) + c.Inventory.Weight(Resource.CookedMeat) > 0));
                // A moving response is resolved by travel/AI, never by a scripted escape or attack.
                result.PredatorReaction = null;
                result.SpawnEncounter = null;
                result.Cost = null;
                result.TimeAddedMinutes = 0;
                result.AbortsAction = true;
                result.WorldAction = async c =>
                {
                    if (drop)
                    {
                        var food = new Inventory();
                        var resources = label == "Drop Fish and Run" ? new[] { Resource.RawFish, Resource.CookedFish } :
                            new[] { Resource.RawMeat, Resource.CookedMeat };
                        foreach (var resource in resources)
                            while (c.Inventory.Count(resource) > 0) food.Add(resource, c.Inventory.Pop(resource));
                        origin.AddGroundItems(food);
                    }
                    if (destination != null && (retreat || label != "Leave Some Meat Behind"))
                        await new Expeditions.TravelRunner(c).TravelToLocation(destination);
                };
                result.DescribeAfterAction = c => (drop ? "You leave food on the ground. " : "") +
                    (c.CurrentLocation != origin ? $"You reach {c.CurrentLocation.Name}. " : "You are still on the same ground. ") +
                    Events.PredatorEventFactory.ObserveResult(c, source);
            }
            if (fire || torch)
            {
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && (torch ? Handlers.TorchHandler.CanLightTorch(c) :
                    Handlers.FireHandler.GetBestTool(c.Inventory) != null && Handlers.FireHandler.GetBestTinder(c.Inventory) != null && c.Inventory.Count(Resource.Stick) > 0);
                result.Cost = null;
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
                            c.player.Skills.GetSkill("Firecraft").Level, c.CurrentLocation.GetFeature<HeatSourceFeature>());
                        attempt = started.Message;
                    }
                    if (c.Inventory.HasLitTorch || c.CurrentLocation.HasActiveHeatSource()) PredatorInteractions.Deter(c, source!, fire: true);
                };
                result.DescribeAfterAction = c => attempt + " " + Events.PredatorEventFactory.ObserveResult(c, source);
            }
            if (harvest || evt.PredatorScene == PredatorSceneKind.WolvesSmellBlood && result.RewardPool != Items.RewardPool.None)
            {
                var carcass = Carcass(ctx);
                var old = result.Validate;
                result.Validate = c => (old?.Invoke(c) ?? true) && carcass != null && ReferenceEquals(Carcass(c), carcass);
                result.RewardPool = Items.RewardPool.None;
                result.TimeActivity = ActivityType.Butchering;
                result.AfterTimeAction = c =>
                {
                    var meat = carcass!.Harvest(result.TimeAddedMinutes, c.Inventory.HasCuttingTool, c.Check(EventCondition.Clumsy), ButcheringMode.QuickStrip);
                    var excess = c.Inventory.CombineWithCapacity(meat);
                    c.CurrentLocation.AddGroundItems(excess);
                };
                if (harvest)
                {
                    var origin = ctx.CurrentLocation;
                    var destination = ctx.Map!.CurrentPosition.GetCardinalNeighbors()
                        .Where(p => ctx.Map.GetLocationAt(p)?.IsPassable == true && !ctx.Map.IsEdgeBlocked(ctx.Map.CurrentPosition, p, ctx.Weather.CurrentSeason))
                        .OrderByDescending(p => source == null ? 0 : p.ManhattanDistance(source.Position))
                        .Select(p => ctx.Map.GetLocationAt(p)).FirstOrDefault();
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
