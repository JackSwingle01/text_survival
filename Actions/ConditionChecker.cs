using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Environments.Features;

namespace text_survival.Actions;

/// <summary>
/// Stateless service for checking event conditions against GameContext.
/// Extracted from GameContext to reduce god object complexity.
/// </summary>
public static class ConditionChecker
{
    /// <summary>
    /// Check if a specific event condition is true for the given game context.
    /// </summary>
    public static bool Check(GameContext ctx, EventCondition condition)
    {
        return condition switch
        {
            EventCondition.IsDaytime => ctx.GetTimeOfDay() == GameContext.TimeOfDay.Morning ||
                         ctx.GetTimeOfDay() == GameContext.TimeOfDay.Afternoon ||
                         ctx.GetTimeOfDay() == GameContext.TimeOfDay.Evening || ctx.GetTimeOfDay() == GameContext.TimeOfDay.Noon,
            EventCondition.Traveling => ctx.CurrentActivity == ActivityType.Traveling,
            EventCondition.HasFood => ctx.Inventory.HasFood,
            EventCondition.HasMeat => ctx.Inventory.HasMeat,
            EventCondition.Injured => ctx.player.Body.Parts.Any(p => p.Condition < 1.0),
            EventCondition.Slow => CapacityCalculator.GetCapacities(ctx.player.Body, ctx.player.GetEffectModifiers()).Moving < 0.7,
            EventCondition.FireBurning => ctx.Camp.HasActiveHeatSource(),
            EventCondition.Inside => ctx.CurrentLocation.HasFeature<ShelterFeature>() && !Check(ctx, EventCondition.FieldWork),
            EventCondition.Outside => !Check(ctx, EventCondition.Inside),
            EventCondition.InAnimalTerritory => ctx.CurrentLocation.HasFeature<AnimalTerritoryFeature>(),
            EventCondition.HasPredators => ctx.CurrentLocation.GetFeature<AnimalTerritoryFeature>()?.HasPredators() ?? false,

            // Location visibility and darkness
            EventCondition.HighVisibility => ctx.CurrentLocation.VisibilityFactor > 0.7,
            EventCondition.LowVisibility => ctx.CurrentLocation.VisibilityFactor < 0.3,
            EventCondition.InDarkness => ctx.CurrentLocation.IsDark && !Check(ctx, EventCondition.HasLightSource),
            EventCondition.HasLightSource => ctx.CurrentLocation.HasActiveHeatSource() || ctx.Inventory.HasLitTorch,
            EventCondition.NearWater => ctx.CurrentLocation.HasFeature<WaterFeature>(),
            EventCondition.HazardousTerrain => ctx.CurrentLocation.GetEffectiveTerrainHazard() >= 0.5,
            EventCondition.HasFuelForage => ctx.CurrentLocation.GetFeature<ForageFeature>()?.HasFuelResources() ?? false,
            EventCondition.HighOverheadCover =>
                ctx.CurrentLocation.OverheadCoverLevel >= 0.6 ||
                (ctx.CurrentLocation.GetFeature<ShelterFeature>()?.OverheadCoverage ?? 0) >= 0.6,
            EventCondition.AtDisturbedSource =>
                ctx.Tensions.GetTension("Disturbed")?.SourceLocation == ctx.CurrentLocation,

            // Water/Ice conditions
            EventCondition.FrozenWater => ctx.CurrentLocation.GetFeature<WaterFeature>()?.IsFrozen ?? false,
            EventCondition.OnThinIce => ctx.CurrentLocation.GetFeature<WaterFeature>()?.HasThinIce ?? false,
            EventCondition.HasIceHole => ctx.CurrentLocation.GetFeature<WaterFeature>()?.HasIceHole ?? false,

            // Weather conditions
            EventCondition.IsSnowing => ctx.Weather.CurrentCondition is Weather.WeatherCondition.LightSnow
                                     or Weather.WeatherCondition.Blizzard,
            EventCondition.IsBlizzard => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Blizzard,
            EventCondition.IsRaining => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Rainy || ctx.Weather.CurrentCondition == Weather.WeatherCondition.Stormy,
            EventCondition.IsStormy => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Stormy,
            EventCondition.HighWind => ctx.Weather.WindSpeed > 0.6,
            EventCondition.IsClear => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Clear,
            EventCondition.IsMisty => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Misty,
            EventCondition.ExtremelyCold => ctx.Weather.BaseTemperature < -25,
            EventCondition.WeatherWorsening => ctx.Weather.WeatherJustChanged && IsWeatherWorsening(ctx.Weather),

            // Resource availability
            EventCondition.HasFuel => ctx.Inventory.HasFuel,
            EventCondition.HasTinder => ctx.Inventory.Count(Resource.Tinder) > 0,

            EventCondition.HasFuelPlenty => ctx.Inventory.GetWeight(ResourceCategory.Fuel) >= 3,
            EventCondition.HasFoodPlenty => ctx.Inventory.GetWeight(ResourceCategory.Food) >= 1.0,

            EventCondition.LowOnFuel => ctx.Inventory.GetWeight(ResourceCategory.Fuel) <= 1.0,
            EventCondition.LowOnFood => ctx.Inventory.GetWeight(ResourceCategory.Food) <= 0.5,
            EventCondition.NoFuel => ctx.Inventory.GetWeight(ResourceCategory.Fuel) <= 0.0,
            EventCondition.NoFood => ctx.Inventory.GetWeight(ResourceCategory.Food) <= 0.0,

            // Tension conditions
            EventCondition.Stalked => ctx.Tensions.HasTension("Stalked"),
            EventCondition.StalkedHigh => ctx.Tensions.HasTensionAbove("Stalked", 0.5),
            EventCondition.StalkedCritical => ctx.Tensions.HasTensionAbove("Stalked", 0.7),
            EventCondition.SmokeSpotted => ctx.Tensions.HasTension("SmokeSpotted"),
            EventCondition.Infested => ctx.Tensions.HasTension("Infested"),
            EventCondition.WoundUntreated => ctx.Tensions.HasTension("WoundUntreated"),
            EventCondition.WoundUntreatedHigh => ctx.Tensions.HasTensionAbove("WoundUntreated", 0.6),
            EventCondition.ShelterWeakened => ctx.Tensions.HasTension("ShelterWeakened"),
            EventCondition.FoodScentStrong => ctx.Tensions.HasTension("FoodScentStrong"),
            EventCondition.Hunted => ctx.Tensions.HasTension("Hunted"),
            EventCondition.Disturbed => ctx.Tensions.HasTension("Disturbed"),
            EventCondition.DisturbedHigh => ctx.Tensions.HasTensionAbove("Disturbed", 0.5),
            EventCondition.DisturbedCritical => ctx.Tensions.HasTensionAbove("Disturbed", 0.7),

            // New tension arc conditions
            EventCondition.WoundedPrey => ctx.Tensions.HasTension("WoundedPrey"),
            EventCondition.WoundedPreyHigh => ctx.Tensions.HasTensionAbove("WoundedPrey", 0.5),
            EventCondition.WoundedPreyCritical => ctx.Tensions.HasTensionAbove("WoundedPrey", 0.7),

            EventCondition.PackNearby => ctx.Tensions.HasTension("PackNearby"),
            EventCondition.PackNearbyHigh => ctx.Tensions.HasTensionAbove("PackNearby", 0.4),
            EventCondition.PackNearbyCritical => ctx.Tensions.HasTensionAbove("PackNearby", 0.7),

            EventCondition.ClaimedTerritory => ctx.Tensions.HasTension("ClaimedTerritory"),
            EventCondition.ClaimedTerritoryHigh => ctx.Tensions.HasTensionAbove("ClaimedTerritory", 0.5),

            EventCondition.HerdNearby => ctx.Tensions.HasTension("HerdNearby"),
            EventCondition.HerdNearbyUrgent => ctx.Tensions.HasTensionAbove("HerdNearby", 0.6),

            EventCondition.DeadlyCold => ctx.Tensions.HasTension("DeadlyCold"),
            EventCondition.DeadlyColdCritical => ctx.Tensions.HasTensionAbove("DeadlyCold", 0.6),

            EventCondition.FeverRising => ctx.Tensions.HasTension("FeverRising"),
            EventCondition.FeverHigh => ctx.Tensions.HasTensionAbove("FeverRising", 0.4),
            EventCondition.FeverCritical => ctx.Tensions.HasTensionAbove("FeverRising", 0.7),

            // Camp/expedition state
            EventCondition.AtCamp => ctx.IsAtCamp,
            EventCondition.OnExpedition => !ctx.IsAtCamp,
            EventCondition.NearFire => ctx.CurrentLocation.HasActiveHeatSource(),
            EventCondition.HasShelter => ctx.CurrentLocation.HasFeature<ShelterFeature>(),
            EventCondition.NoShelter => !ctx.CurrentLocation.HasFeature<ShelterFeature>(),

            // Time of day
            EventCondition.Night => ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night,

            // Additional resource conditions
            EventCondition.HasWater => ctx.Inventory.HasWater,
            EventCondition.HasPlantFiber => ctx.Inventory.Count(Resource.PlantFiber) > 0,

            // Body state conditions
            EventCondition.LowCalories => ctx.player.Body.CalorieStore < 500,
            EventCondition.LowHydration => ctx.player.Body.Hydration < 1000,
            EventCondition.LowTemperature => ctx.player.Body.BodyTemperature < 96.0,
            EventCondition.Impaired => AbilityCalculator.IsConsciousnessImpaired(
                ctx.player.GetCapacities().Consciousness),
            EventCondition.Limping => AbilityCalculator.IsMovingImpaired(
                ctx.player.GetCapacities().Moving),
            EventCondition.Clumsy => AbilityCalculator.IsManipulationImpaired(
                ctx.player.GetCapacities().Manipulation),
            EventCondition.Foggy => AbilityCalculator.IsPerceptionImpaired(
                AbilityCalculator.CalculatePerception(ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers())),
            EventCondition.Winded => AbilityCalculator.IsBreathingImpaired(
                ctx.player.GetCapacities().Breathing),

            // Activity conditions (for event filtering)
            EventCondition.IsSleeping => ctx.CurrentActivity == ActivityType.Sleeping,
            EventCondition.Awake => ctx.CurrentActivity != ActivityType.Sleeping,
            EventCondition.IsCampWork => ctx.CurrentActivity is ActivityType.TendingFire or ActivityType.Eating or ActivityType.Cooking or ActivityType.Crafting,
            EventCondition.IsExpedition => !ctx.IsAtCamp,
            EventCondition.Eating => ctx.CurrentActivity == ActivityType.Eating,
            EventCondition.FieldWork => (ctx.CurrentActivity is ActivityType.Traveling or ActivityType.Foraging or ActivityType.Hunting or ActivityType.Exploring),
            EventCondition.Working => Check(ctx, EventCondition.IsCampWork) || Check(ctx, EventCondition.FieldWork),

            // Trapping conditions
            EventCondition.HasActiveSnares => HasActiveSnares(ctx),
            EventCondition.SnareHasCatch => HasSnareCatch(ctx),
            EventCondition.SnareBaited => HasBaitedSnares(ctx),
            EventCondition.TrapLineActive => ctx.Tensions.HasTension("TrapLineActive"),
            _ => false,
        };
    }

    // === TRAPPING HELPERS ===

    private static bool AnyLocationHasSnare(GameContext ctx, Func<SnareLineFeature, bool> predicate)
    {
        if (ctx.Map == null) return false;
        foreach (var location in ctx.Map.NamedLocations)
        {
            var snare = location.GetFeature<SnareLineFeature>();
            if (snare != null && predicate(snare))
                return true;
        }
        return false;
    }

    // Use feature's CanBeChecked property
    private static bool HasActiveSnares(GameContext ctx) =>
        AnyLocationHasSnare(ctx, s => s.CanBeChecked);

    // Use feature's HasCatch property
    private static bool HasSnareCatch(GameContext ctx) =>
        AnyLocationHasSnare(ctx, s => s.HasCatch);

    private static bool HasBaitedSnares(GameContext ctx) =>
        AnyLocationHasSnare(ctx, s => s.HasBaitedSnares);

    /// <summary>
    /// Determines if weather transitioned to a worse condition.
    /// </summary>
    private static bool IsWeatherWorsening(Weather w)
    {
        if (w.PreviousCondition == null) return false;

        return (w.PreviousCondition, w.CurrentCondition) switch
        {
            // Any non-clear is worse than clear
            (Weather.WeatherCondition.Clear, _) when w.CurrentCondition != Weather.WeatherCondition.Clear => true,
            // Precipitation is worse than cloudy
            (Weather.WeatherCondition.Cloudy, Weather.WeatherCondition.LightSnow or Weather.WeatherCondition.Rainy
                or Weather.WeatherCondition.Blizzard or Weather.WeatherCondition.Stormy) => true,
            // Blizzard is worse than light snow
            (Weather.WeatherCondition.LightSnow, Weather.WeatherCondition.Blizzard) => true,
            // Storm is worse than rain
            (Weather.WeatherCondition.Rainy, Weather.WeatherCondition.Stormy) => true,
            _ => false
        };
    }
}
