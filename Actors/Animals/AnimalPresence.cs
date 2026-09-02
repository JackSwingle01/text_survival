using text_survival.Actions;
using text_survival.Environments.Features;

namespace text_survival.Actors.Animals;

/// <summary>
/// Single source of truth for "what animals are around the player".
/// Large animals live in herds. Small game lives in a tile's SmallGameFeature density.
/// Events, conditions, and work options that need to know what is nearby ask here,
/// so a wiped-out pack stops showing up in events and a wandering pack starts to.
/// </summary>
public static class AnimalPresence
{
    /// <summary>Herds with living members on the player's tile, or whose home territory includes it.</summary>
    public static IReadOnlyList<Herd> Near(GameContext ctx)
    {
        if (ctx.Map == null) return [];
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds
            .Where(h => h.Count > 0 && (h.Position == pos || h.HomeTerritory.Contains(pos)))
            .ToList();
    }

    /// <summary>Herds with living members standing on the player's tile.</summary>
    public static IReadOnlyList<Herd> Here(GameContext ctx)
    {
        if (ctx.Map == null) return [];
        var pos = ctx.Map.CurrentPosition;
        return ctx.Herds.Where(h => h.Count > 0 && h.Position == pos).ToList();
    }

    public static bool AnyNear(GameContext ctx) => Near(ctx).Count > 0;
    public static bool PredatorsNear(GameContext ctx) => Near(ctx).Any(h => h.IsPredator);
    public static bool PreyNear(GameContext ctx) => Near(ctx).Any(h => !h.IsPredator);
    public static bool PackPredatorsNear(GameContext ctx) => Near(ctx).Any(h => h.BehaviorType == HerdBehaviorType.PackPredator);
    public static bool SolitaryPredatorsNear(GameContext ctx) => Near(ctx).Any(h => h.BehaviorType == HerdBehaviorType.SolitaryPredator);
    public static bool ScavengersNear(GameContext ctx) => Near(ctx).Any(h => h.BehaviorType == HerdBehaviorType.Scavenger);
    public static bool ScavengersHere(GameContext ctx) => Here(ctx).Any(h => h.BehaviorType == HerdBehaviorType.Scavenger);
    public static bool OfTypeNear(GameContext ctx, AnimalType type) => Near(ctx).Any(h => h.AnimalType == type);
    public static bool OfTypeHere(GameContext ctx, AnimalType type) => Here(ctx).Any(h => h.AnimalType == type);

    /// <summary>The tile's small game feature if it still has game to hunt.</summary>
    public static SmallGameFeature? SmallGameHere(GameContext ctx)
    {
        var feature = ctx.CurrentLocation.GetFeature<SmallGameFeature>();
        return feature != null && feature.CanHunt() ? feature : null;
    }

    /// <summary>Anything to hunt or track here: a herd near, or small game on the tile.</summary>
    public static bool AnyGame(GameContext ctx) => AnyNear(ctx) || SmallGameHere(ctx) != null;

    /// <summary>A predator type near the player, weighted toward herds on the tile. Null if none.</summary>
    public static AnimalType? PickPredator(GameContext ctx) => Pick(ctx, h => h.IsPredator);

    /// <summary>A prey type near the player, weighted toward herds on the tile. Null if none.</summary>
    public static AnimalType? PickPrey(GameContext ctx) => Pick(ctx, h => !h.IsPredator);

    /// <summary>Any animal type near the player: herds first, then the tile's small game. Null if none.</summary>
    public static AnimalType? PickAnimal(GameContext ctx) =>
        Pick(ctx, _ => true) ?? SmallGameHere(ctx)?.RandomSmallGame();

    private static AnimalType? Pick(GameContext ctx, Func<Herd, bool> filter)
    {
        if (ctx.Map == null) return null;
        var pos = ctx.Map.CurrentPosition;
        var candidates = Near(ctx)
            .Where(filter)
            .Select(h => (h.AnimalType, Weight: h.Count * (h.Position == pos ? 3.0 : 1.0)))
            .ToList();
        if (candidates.Count == 0) return null;

        double roll = Utils.Rng.NextDouble() * candidates.Sum(c => c.Weight);
        foreach (var (type, weight) in candidates)
        {
            roll -= weight;
            if (roll <= 0) return type;
        }
        return candidates[^1].AnimalType;
    }
}
