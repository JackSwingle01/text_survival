using text_survival.Actions.Variants;
using text_survival.Effects;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Actions.Events;

/// <summary>
/// Events that trigger when crossing edges between tiles.
/// Migrated from GameEventRegistry.Expedition.cs (WaterCrossing) and new implementations.
/// </summary>
public static class EdgeEvents
{
    /// <summary>
    /// Get default events for an edge type. Called during TileEdge construction.
    /// </summary>
    public static List<EdgeEvent> DefaultEventsFor(EdgeType type) => type switch
    {
        EdgeType.River => [new EdgeEvent(0.6, WaterCrossing)],      // 60% chance
        EdgeType.Climb => [new EdgeEvent(0.4, ClimbingHazard)],     // 40% chance
        _ => []
    };

    /// <summary>
    /// Water crossing event - moved from GameEventRegistry.Expedition.cs
    /// Now triggers when crossing a River edge, not probabilistically while "near water".
    /// </summary>
    public static GameEvent? WaterCrossing(GameContext ctx)
    {
        var slipVariant = VariantSelector.SelectSlipVariant(ctx);

        return new GameEvent("Water Crossing",
            "Water blocks your path. Moving water, or still water with thin ice at the edges.", 0.8)
            .Choice("Wade Across",
                "Straight through. Get wet, get it over with.",
                [
                    new EventResult("Cold but quick. You're through.", 0.50, 8)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30)),
                    new EventResult("Deeper than expected. Soaked to the waist.", 0.30, 12)
                        .WithEffects(EffectFactory.Wet(0.8), EffectFactory.Cold(-15, 45)),
                    new EventResult("Current stronger than it looked. You fight to stay upright.", 0.15, 15)
                        .WithEffects(EffectFactory.Wet(0.9), EffectFactory.Cold(-18, 60), EffectFactory.Exhausted(0.2, 30)),
                    new EventResult("You slip. Water closes over your head for a terrifying moment.", 0.05, 18)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(1.0), EffectFactory.Cold(-25, 90))
                        .Frightening()
                ])
            .Choice("Find Another Route",
                "Look for a better crossing point. May cancel the trip.",
                [
                    new EventResult("You find a narrow point. Easy crossing.", 0.30, 20),
                    new EventResult("Long detour but you stay dry.", 0.30, 30),
                    new EventResult("No good options. You end up crossing anyway.", 0.20, 25)
                        .WithEffects(EffectFactory.Wet(0.5), EffectFactory.Cold(-10, 30)),
                    new EventResult("No safe crossing. You turn back.", 0.20, 15)
                        .Aborts()  // Cancel the travel
                ])
            .Choice("Drink First",
                "You're here. Might as well hydrate.",
                [
                    new EventResult("Fresh and cold. You drink your fill before crossing.", 0.80, 10)
                        .Rewards(RewardPool.WaterSource),
                    new EventResult("Water tastes off. You drink anyway, then cross.", 0.15, 12)
                        .Rewards(RewardPool.WaterSource)
                        .WithEffects(EffectFactory.Nauseous(0.15, 45)),
                    new EventResult("Ice breaks. You're in the water.", 0.05, 15)
                        .DamageWithVariant(slipVariant)
                        .WithEffects(EffectFactory.Wet(0.9))
                        .SevereCold()
                ]);
    }

    /// <summary>
    /// Climbing hazard event - triggers when crossing a Climb edge.
    /// Uses existing ClimbingFall variants.
    /// </summary>
    public static GameEvent? ClimbingHazard(GameContext ctx)
    {
        var fallVariant = AccidentVariants.ClimbingFall[
            Random.Shared.Next(AccidentVariants.ClimbingFall.Length)];

        return new GameEvent("Difficult Terrain",
            "The way forward requires scrambling over rough ground.", 0.9)
            .Choice("Move Carefully",
                "Take your time. Test each handhold.",
                [
                    new EventResult("Slow but safe. You make it.", 0.70, 20),
                    new EventResult("A handhold crumbles. You recover.", 0.25, 25)
                        .WithEffects(EffectFactory.Shaken(0.2)),
                    new EventResult(fallVariant.Description, 0.05, 15)
                        .DamageWithVariant(fallVariant)
                ])
            .Choice("Move Quickly",
                "Speed over caution. Get it done.",
                [
                    new EventResult("Momentum carries you through.", 0.50, 10),
                    new EventResult(fallVariant.Description, 0.35, 12)
                        .DamageWithVariant(fallVariant),
                    new EventResult("Bad fall. You tumble hard.", 0.15, 15)
                        .DamageWithVariant(fallVariant)
                        .WithEffects(EffectFactory.Dazed(0.3))
                ])
            .Choice("Find Another Way",
                "There might be an easier route. May cancel the trip.",
                [
                    new EventResult("You find a gentler path.", 0.35, 25),
                    new EventResult("Long way around, but safer.", 0.35, 35),
                    new EventResult("No good options. You climb.", 0.20, 20),
                    new EventResult("No safe route. You turn back.", 0.10, 15)
                        .Aborts()  // Cancel the travel
                ]);
    }

    /// <summary>
    /// First-time climb event for named locations (100% trigger).
    /// More narrative than the generic hazard.
    /// </summary>
    public static Func<GameContext, GameEvent?> FirstClimb(string locationName, string description)
    {
        return ctx =>
        {
            var fallVariant = AccidentVariants.ClimbingFall[
                Random.Shared.Next(AccidentVariants.ClimbingFall.Length)];

            return new GameEvent($"The Approach to {locationName}", description, 1.0)
                .Choice("Make the Climb",
                    "Commit. Find your route.",
                    [
                        new EventResult("Challenging but doable. You reach the top.", 0.60, 25),
                        new EventResult("Harder than it looked. You're breathing hard at the top.", 0.30, 35)
                            .WithEffects(EffectFactory.Exhausted(0.2, 30)),
                        new EventResult(fallVariant.Description, 0.10, 20)
                            .DamageWithVariant(fallVariant)
                    ])
                .Choice("Look for Another Way",
                    "Circle the approach. There might be an easier route.",
                    [
                        new EventResult("You find a gentler slope.", 0.40, 30),
                        new EventResult("No luck. You climb anyway.", 0.40, 25),
                        new EventResult("No safe approach. You turn back.", 0.20, 15)
                            .Aborts()
                    ]);
        };
    }
}
