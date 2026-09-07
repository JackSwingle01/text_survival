using text_survival.Actions;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Perception;
using text_survival.Items;

namespace text_survival.Actors.Animals;

public enum PredatorIntent { Following, Searching }

/// <summary>Memory belonging to an animal group, never a global danger meter.</summary>
public class PredatorInteraction
{
    public Actor? Target { get; set; }
    public Location? LastDetectedLocation { get; set; }
    public int LastDetectedMinute { get; set; }
    public int LastDecisionMinute { get; set; } = -9999;
    public PredatorIntent Intent { get; set; }
}

/// <summary>What the player last saw. Loss of sight does not imply safety.</summary>
public class PredatorObservation
{
    public Herd Source { get; set; } = null!;
    public Location LastSeenLocation { get; set; } = null!;
    public int LastSeenMinute { get; set; }
    public string Behavior { get; set; } = "watching";
    public int LastAnnouncedMinute { get; set; } = -9999;
    public string? LastAnnouncedBehavior { get; set; }
}

public static class PredatorInteractions
{
    // Detection ranges are map tiles. Combat distance is metres within a shared tile;
    // there is deliberately no conversion from distant map tiles to combat metres.
    public static bool IsLiving(GameContext ctx, Herd? herd) => ctx.Map != null && herd != null &&
        ctx.Herds.Contains(herd) && herd.Members.Any(a => a.IsAlive) && herd.Map == ctx.Map;

    public static bool CanObserve(GameContext ctx, Herd? herd) => IsLiving(ctx, herd) &&
        ctx.CurrentActivity != ActivityType.Sleeping &&
        ctx.player.GetCapacities().Consciousness > 0 && ctx.player.GetCapacities().Sight > 0 &&
        Sight.CanSeeTile(ctx.Map!, ctx.Map!.CurrentPosition, herd!.Position, ctx.player.GetCapacities().Sight);

    public static Herd? Observed(GameContext ctx, bool packOnly = false) => ctx.Herds
        .Where(h => h.IsPredator && (!packOnly || h.Count > 1) && CanObserve(ctx, h))
        .OrderByDescending(h => h.Pursuit?.Target == ctx.player)
        .ThenBy(h => h.Position.ManhattanDistance(ctx.Map!.CurrentPosition)).FirstOrDefault();

    public static bool FollowingPlayer(GameContext ctx, Herd h) => IsLiving(ctx, h) &&
        h.Pursuit?.Target == ctx.player && h.Pursuit.Intent == PredatorIntent.Following;
    public static bool ObservedFollowing(GameContext ctx) => ctx.Herds.Any(h => FollowingPlayer(ctx, h) && CanObserve(ctx, h));
    public static bool WithinReach(GameContext ctx, Herd? herd) => IsLiving(ctx, herd) &&
        !herd!.IsTraveling && herd.CurrentLocation == ctx.CurrentLocation;
    public static bool ImmediateThreat(GameContext ctx) => ctx.Herds.Any(h =>
        FollowingPlayer(ctx, h) && WithinReach(ctx, h) && CanObserve(ctx, h));
    public static bool ObservedPack(GameContext ctx) => ctx.Herds.Any(h => h.Count > 1 && FollowingPlayer(ctx, h) && CanObserve(ctx, h));

    public static bool CanDetect(GameContext ctx, Herd herd, Actor target)
    {
        var pos = ctx.GetActorPosition(target);
        if (pos == null || !target.IsAlive || herd.Map != ctx.Map) return false;
        int distance = herd.Position.ManhattanDistance(pos.Value);
        if (distance > herd.BaseDetectionRange) return false;
        return Sight.CanSeeTile(herd.Map, herd.Position, pos.Value) ||
            (distance <= 1 && ((target.Inventory?.HasMeat == true || target.Inventory?.HasFish == true) || target.EffectRegistry.HasEffect("Bleeding") ||
                target.EffectRegistry.HasEffect("Bloody")));
    }

    private static IEnumerable<Inventory> ExposedFood(Location location)
    {
        foreach (var ground in location.Features.OfType<GroundItemsFeature>().Where(g => !location.IsCovered(g))) yield return ground.Storage;
        foreach (var cache in location.Features.OfType<CacheFeature>().Where(c => !c.ProtectsFromPredators && !location.IsCovered(c))) yield return cache.Storage;
    }
    public static double FoodAt(Location location) => ExposedFood(location).Sum(i => i.Weight(Resource.RawMeat) + i.Weight(Resource.CookedMeat) + i.Weight(Resource.RawFish) + i.Weight(Resource.CookedFish)) +
        location.Features.OfType<CarcassFeature>().Where(c => !location.IsCovered(c)).Sum(c => c.MeatRemainingKg);

    public static double EatAt(Location location, double kilograms)
    {
        double eaten = 0;
        foreach (var inventory in ExposedFood(location).ToList())
            foreach (var resource in new[] { Resource.RawMeat, Resource.CookedMeat, Resource.RawFish, Resource.CookedFish })
                eaten += inventory.ConsumeByWeight(resource, Math.Max(0, kilograms - eaten));
        foreach (var carcass in location.Features.OfType<CarcassFeature>().Where(c => !location.IsCovered(c)))
            eaten += carcass.ConsumeMeat(Math.Max(0, kilograms - eaten));
        location.CleanupGroundItems();
        return eaten;
    }

    /// <summary>Returns null when ordinary species behavior should run instead.</summary>
    public static HerdUpdateResult? Update(Herd herd, int minutes, GameContext ctx)
    {
        if (!herd.IsPredator || herd.AnimalType == AnimalType.SaberTooth || ctx.Map == null) return null;
        if (herd.State == HerdState.Fleeing || herd.Fear >= 0.6 || ctx.TotalMinutesElapsed - herd.LastCombatMinutes < 30)
        {
            herd.Pursuit = null;
            return null;
        }
        // Preserve a bear's den defense and ongoing animal/NPC fights.
        if (herd.AtDen && herd.BehaviorType == HerdBehaviorType.SolitaryPredator && herd.IsPlayerHere) return null;
        if (herd.State == HerdState.Hunting && herd.Pursuit == null) return null;

        // Existing kill defense remains the species behavior's responsibility.
        bool hasCarcass = herd.CurrentLocation.Features.OfType<CarcassFeature>().Any(c => c.MeatRemainingKg > 0);
        if (herd.State == HerdState.Feeding && hasCarcass) return null;
        if (herd.State == HerdState.Feeding && (herd.Hunger <= 0.1 || FoodAt(herd.CurrentLocation) <= 0))
        {
            herd.Pursuit = null;
            herd.TransitionTo(HerdState.Resting);
            return HerdUpdateResult.None;
        }
        Location? food = herd.State == HerdState.Feeding ? herd.CurrentLocation : null;
        if (food == null && herd.Hunger > 0.5)
            food = herd.Position.GetCardinalNeighbors().Append(herd.Position).Select(p => ctx.Map.GetLocationAt(p))
                .OfType<Location>().Where(l => FoodAt(l) > 0)
                .OrderBy(l => herd.Position.ManhattanDistance(ctx.Map.GetPosition(l))).FirstOrDefault();

        bool detects = CanDetect(ctx, herd, ctx.player);
        if (food == null && herd.Pursuit == null && (!detects || herd.Hunger <= 0.5)) return null;
        if (herd.Pursuit?.Target != null && !herd.Pursuit.Target.IsAlive) herd.Pursuit = null;
        if (food == null && herd.Hunger <= 0.3) { herd.Pursuit = null; return null; }

        herd.Hunger = Math.Min(1, herd.Hunger + minutes * 0.001);
        if (herd.IsTraveling) herd.UpdateTravel(minutes);
        if (food != null)
        {
            herd.Pursuit = null;
            if (herd.CurrentLocation == food && !herd.IsTraveling)
            {
                herd.TransitionTo(HerdState.Feeding);
                // One meal is 3% of group body mass. Hunger changes only for food eaten.
                double mealKg = Math.Max(1, herd.TotalMassKg * 0.03);
                double eaten = EatAt(food, Math.Min(mealKg * herd.Hunger, minutes * 0.1 * herd.Count));
                herd.Hunger = Math.Max(0, herd.Hunger - eaten / mealKg);
            }
            else MoveToward(herd, food, ctx);
            return HerdUpdateResult.None;
        }

        var memory = herd.Pursuit ??= new PredatorInteraction { Target = ctx.player, LastDecisionMinute = ctx.TotalMinutesElapsed };
        var target = memory.Target;
        if (target == null) { herd.Pursuit = null; return HerdUpdateResult.None; }
        detects = CanDetect(ctx, herd, target);
        if (detects)
        {
            memory.LastDetectedLocation = target.CurrentLocation;
            memory.LastDetectedMinute = ctx.TotalMinutesElapsed;
            memory.Intent = PredatorIntent.Following;
        }
        else memory.Intent = PredatorIntent.Searching;

        // This timer represents how long the animal searches its last known contact.
        if (memory.LastDetectedLocation == null || ctx.TotalMinutesElapsed - memory.LastDetectedMinute >= 20)
        {
            herd.Pursuit = null;
            herd.TransitionTo(HerdState.Patrolling);
            return HerdUpdateResult.None;
        }
        if (detects && WithinReach(ctx, herd) && target == ctx.player)
        {
            // Decisions happen on game-time intervals, never in read-only situation queries.
            if (ctx.TotalMinutesElapsed - memory.LastDecisionMinute >= 5)
            {
                memory.LastDecisionMinute = ctx.TotalMinutesElapsed;
                if (ctx.Inventory.HasLitTorch || ctx.CurrentLocation.HasActiveHeatSource())
                    Deter(ctx, herd, fire: true);
                if (herd.Fear < 0.6 && Utils.Rng.NextDouble() < herd.BoldnessToward(target, ctx))
                    return HerdUpdateResult.WithEncounter(herd);
            }
        }
        else MoveToward(herd, memory.LastDetectedLocation, ctx);
        return HerdUpdateResult.None;
    }

    private static void MoveToward(Herd herd, Location target, GameContext ctx)
    {
        if (herd.IsTraveling || herd.CurrentLocation == target) return;
        // Only traversable adjacent steps; the animal searches rather than teleporting
        // through blocked edges. The ordinary patrol can take over after lost contact.
        var dest = herd.Position.GetCardinalNeighbors()
            .Where(p => ctx.Map!.GetLocationAt(p)?.IsPassable == true &&
                !ctx.Map.IsEdgeBlocked(herd.Position, p, ctx.Weather.CurrentSeason))
            .OrderBy(p => p.ManhattanDistance(ctx.Map!.GetPosition(target))).FirstOrDefault();
        if (ctx.Map!.GetLocationAt(dest)?.IsPassable == true &&
            herd.Position.ManhattanDistance(dest) == 1 &&
            !ctx.Map.IsEdgeBlocked(herd.Position, dest, ctx.Weather.CurrentSeason)) herd.StartTravelTo(dest, ctx.Map);
    }

    public static void Deter(GameContext ctx, Herd herd, bool fire)
    {
        if (!WithinReach(ctx, herd)) return;
        var variant = Actions.Variants.AnimalSelector.GetVariant(herd.AnimalType);
        double effectiveness = fire ? variant.FireEffectiveness : variant.NoiseEffectiveness;
        herd.Fear = Math.Clamp(herd.Fear + effectiveness * 0.5, 0, 1);
        if (herd.Fear >= 0.6)
        {
            herd.Pursuit = null;
            herd.Behavior?.TriggerFlee(herd, ctx.Map!.CurrentPosition, ctx);
        }
    }

    public static void Observe(GameContext ctx)
    {
        foreach (var herd in ctx.Herds.Where(h => h.IsPredator && CanObserve(ctx, h)))
        {
            string behavior = herd.State == HerdState.Feeding ? "feeding" : herd.State == HerdState.Fleeing ? "retreating" :
                FollowingPlayer(ctx, herd) ? "following" : "nearby";
            var seen = ctx.PredatorObservations.FirstOrDefault(o => o.Source == herd);
            if (seen == null)
            {
                seen = new PredatorObservation { Source = herd };
                ctx.PredatorObservations.Add(seen);
            }
            bool reacquired = ctx.TotalMinutesElapsed - seen.LastSeenMinute >= 10;
            seen.LastSeenLocation = herd.CurrentLocation;
            seen.LastSeenMinute = ctx.TotalMinutesElapsed;
            seen.Behavior = behavior;
            if ((!reacquired && seen.LastAnnouncedBehavior == behavior) || ctx.TotalMinutesElapsed - seen.LastAnnouncedMinute < 10) continue;
            seen.LastAnnouncedBehavior = behavior;
            seen.LastAnnouncedMinute = ctx.TotalMinutesElapsed;
            if (ctx.IsHandlingEvent) continue;
            if (behavior == "following" || Actions.Events.PredatorEventFactory.Scene(ctx) != "nearby" && behavior == "nearby")
                ctx.EventQueue.Enqueue(GameEventRegistry.GetPredatorObservationEvent(ctx, herd) ?? Actions.Events.PredatorEventFactory.Create(ctx, herd));
            else UI.GameDisplay.AddNarrative(ctx,
                $"You catch sight of {herd.AnimalType.DisplayName().ToLowerInvariant()} movement. " +
                Actions.Events.PredatorEventFactory.ObserveResult(ctx, herd));
        }
    }

    public static bool IsLegacyTension(string type) => new[] { "Stalked", "Hunted", "PackNearby" }.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static void Restore(GameContext ctx)
    {
        // Old meters have no reliable physical counterpart. Never invent pursuit on load.
        foreach (var type in new[] { "Stalked", "Hunted", "PackNearby" }) ctx.Tensions.ResolveTension(type);
        foreach (var herd in ctx.Herds)
            if (herd.Pursuit?.Target is not { IsAlive: true } target || !ctx.AllActors.Contains(target) ||
                herd.Pursuit.LastDetectedLocation == null || ctx.Map == null ||
                !ctx.Map.AllLocations.Contains(herd.Pursuit.LastDetectedLocation)) herd.Pursuit = null;
        ctx.PredatorObservations.RemoveAll(o => !IsLiving(ctx, o.Source) || ctx.Map == null || !ctx.Map.AllLocations.Contains(o.LastSeenLocation));
    }
}
