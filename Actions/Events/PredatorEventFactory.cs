using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actions.Events;

/// <summary>Authored decisions around a specific, observable group of animals.</summary>
public static class PredatorEventFactory
{
    public static GameEvent Create(GameContext ctx, Herd? source = null, string name = "Movement Nearby")
    {
        source ??= PredatorInteractions.Observed(ctx);
        var herd = source;
        var following = herd != null && PredatorInteractions.FollowingPlayer(ctx, herd);
        var state = herd?.State;
        var location = ctx.CurrentLocation;
        var scene = Scene(ctx);
        var feature = scene switch
        {
            "carcass" => location.Features.OfType<CarcassFeature>().FirstOrDefault(c => c.MeatRemainingKg > 0 && !location.IsCovered(c)) as LocationFeature,
            "traps" => location.GetFeature<SnareLineFeature>(),
            _ => null
        };
        string animals = herd == null ? "An animal" : Subject(herd);
        string description = scene switch
        {
            "carcass" => $"There is still meat on the carcass. {animals} is visible nearby. Taking more will mean spending more time here.",
            "fishing" => $"Your fishing can wait. {animals} is in sight, and for a moment your attention leaves the water.",
            "traps" => $"You pause at the trap line. {animals} is visible nearby. The traps will still need your attention when this is over.",
            "camp" => $"Even here, you are not alone. {animals} is visible from camp. You take a moment to watch before deciding what to do.",
            _ => $"{animals} is in sight."
        };
        description += following ? " As you watch, the movement follows yours." :
            state == HerdState.Feeding ? " For now, the animal's attention is on feeding." : "";
        if (name == "Movement Nearby") name = scene switch
        {
            "carcass" => "Company at the Carcass", "fishing" => "Movement by the Water",
            "traps" => "At the Trap Line", "camp" => "Movement at Camp Edge",
            _ => following ? "The Followers" : "A Nearby Animal"
        };
        bool Valid(GameContext c) => PredatorInteractions.CanObserve(c, herd) && herd!.State == state &&
            PredatorInteractions.FollowingPlayer(c, herd) == following && c.CurrentLocation == location && Scene(c) == scene &&
            (feature == null || location.Features.Contains(feature) && !location.IsCovered(feature) &&
                (feature is not CarcassFeature carcass || carcass.MeatRemainingKg > 0));
        var evt = new GameEvent(name, description, 1).RequiresSituation(Valid).WithCooldown(10);
        evt.SourceHerd = herd;
        evt.Validate = Valid;

        EventResult Response(string message, Func<GameContext, Task> action, int minutes = 0,
            Func<GameContext, bool>? available = null, ActivityType? activity = null) => new EventResult(message, minutes: minutes)
            { SourceHerd = herd, WorldAction = action, TimeActivity = activity,
                DescribeAfterAction = c => message + " " + ObserveResult(c, herd),
                Validate = c => Valid(c) && (available?.Invoke(c) ?? true) };

        evt.Choice("Watch", scene == "carcass" ? "Leave the meat untouched for five minutes and watch. The animals can keep moving." :
                scene == "fishing" ? "Pause fishing for five minutes and watch the animals." :
                "Stay here and watch for five minutes. The animals can keep moving.",
            [Response("You watch the animals and your surroundings.", _ => Task.CompletedTask, 5,
                activity: ActivityType.Resting)]);
        if (ctx.CurrentActivity is not (ActivityType.Idle or ActivityType.Resting or ActivityType.Sleeping or ActivityType.Traveling))
        evt.Choice("Stop working", scene switch
            {
                "carcass" => "Leave the unfinished carcass and choose your next move on the map.",
                "fishing" => "Put fishing aside and choose your next move on the map.",
                "traps" => "Leave the traps for now and choose your next move on the map.",
                _ => "Put the task aside so you can choose your next move on the map."
            },
            [Response("You stop what you were doing and look for a route away.", _ => Task.CompletedTask).Aborts()]);
        evt.Choice("Shout and stand tall", "Try to intimidate this group. Different animals respond differently.",
            [Response("You stand tall and shout toward the animals.", c =>
            { PredatorInteractions.Deter(c, herd!, fire: false); return Task.CompletedTask; }, 1,
                c => PredatorInteractions.WithinReach(c, herd))]);
        evt.Choice("Raise your torch", "Show the animals your burning torch.",
            [Response("You raise the burning torch toward the animals.", c =>
            { PredatorInteractions.Deter(c, herd!, fire: true); return Task.CompletedTask; }, 1,
                c => c.Inventory.HasLitTorch && PredatorInteractions.WithinReach(c, herd))]);
        evt.Choice("Light a torch", "Use your torch and available fire-making supplies.",
            [Response("You try to light your torch.", c => TorchHandler.LightTorch(c),
                available: TorchHandler.CanLightTorch)]);
        evt.Choice("Leave your meat", "Put all carried raw and cooked meat on the ground. The animals may eat it.",
            [Response("You leave your meat on the ground.", c =>
            {
                var dropped = new Inventory();
                foreach (var resource in new[] { Resource.RawMeat, Resource.CookedMeat })
                    while (c.Inventory.Count(resource) > 0) dropped.Add(resource, c.Inventory.Pop(resource));
                c.CurrentLocation.AddGroundItems(dropped);
                return Task.CompletedTask;
            }, 1, c => c.Inventory.Weight(Resource.RawMeat) + c.Inventory.Weight(Resource.CookedMeat) > 0)]);

        Location? retreat = null;
        if (ctx.Map != null && herd != null)
            retreat = ctx.Map.CurrentPosition.GetCardinalNeighbors()
                .Where(p => ctx.Map.GetLocationAt(p)?.IsPassable == true &&
                    !ctx.Map.IsEdgeBlocked(ctx.Map.CurrentPosition, p, ctx.Weather.CurrentSeason))
                .OrderByDescending(p => p.ManhattanDistance(herd.Position))
                .Select(p => ctx.Map.GetLocationAt(p)).FirstOrDefault();
        if (retreat != null)
        {
            var destination = retreat;
            evt.Choice($"Retreat toward {destination.Name}", "Walk to the neighboring ground. They may follow.",
                [Response("You attempt to retreat to the neighboring ground.", c =>
                    new TravelRunner(c).TravelToLocation(destination), available: c => c.Map != null &&
                        c.player.GetCapacities().Moving > 0.1 &&
                        c.Map.CurrentPosition.ManhattanDistance(c.Map.GetPosition(destination)) == 1 &&
                        !c.Map.IsEdgeBlocked(c.Map.CurrentPosition, c.Map.GetPosition(destination), c.Weather.CurrentSeason)).Aborts()]);
        }
        evt.Choice("Confront them", "Approach the animals on this ground and force a confrontation.",
            [Response("You turn to confront the animals.", c =>
            {
                var member = herd!.Members.FirstOrDefault(a => a.IsAlive);
                if (member != null) c.QueueEncounter(new EncounterConfig(herd.AnimalType, 20,
                    herd.BoldnessToward(c.player, c), member));
                return Task.CompletedTask;
            }, available: c => PredatorInteractions.WithinReach(c, herd) && c.TotalMinutesElapsed - herd!.LastCombatMinutes >= 30).Aborts()]);
        return evt;
    }
    public static string Scene(GameContext ctx)
    {
        if (ctx.CurrentActivity == ActivityType.Butchering && ctx.CurrentLocation.Features.OfType<CarcassFeature>()
            .Any(c => c.MeatRemainingKg > 0 && !ctx.CurrentLocation.IsCovered(c))) return "carcass";
        if (ctx.CurrentActivity == ActivityType.Fishing &&
            (ctx.CurrentLocation.HasFeature<WaterFeature>() || ctx.CurrentLocation.HasFeature<NetFishingFeature>())) return "fishing";
        if (ctx.CurrentActivity is ActivityType.Foraging or ActivityType.Hunting or ActivityType.Exploring &&
            ctx.CurrentLocation.GetFeature<SnareLineFeature>() is { } traps && !ctx.CurrentLocation.IsCovered(traps)) return "traps";
        return ctx.IsAtCamp ? "camp" : "nearby";
    }

    private static string Subject(Herd herd) => herd.Count == 1 ?
        $"A {herd.AnimalType.DisplayName().ToLowerInvariant()}" :
        herd.AnimalType == AnimalType.Wolf ? "A wolf pack" :
        $"A group of {herd.AnimalType.DisplayName().ToLowerInvariant()}s";

    public static string ObserveResult(GameContext ctx, Herd? herd)
    {
        if (!PredatorInteractions.CanObserve(ctx, herd))
            return "You can no longer see the animal. That does not tell you where it went.";
        if (ctx.HasPendingEncounter) return "The distance is no longer reassuring. A confrontation is beginning.";
        if (herd!.State == HerdState.Fleeing) return "You see the animal retreating. For now, you have some room.";
        if (herd.State == HerdState.Feeding) return "You can see it feeding. For now, its attention is on the food.";
        if (PredatorInteractions.FollowingPlayer(ctx, herd)) return "It is still following your movements.";
        return "It remains in sight. You watch for its next move.";
    }

}
