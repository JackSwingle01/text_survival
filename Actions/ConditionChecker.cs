using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actions;

public static class ConditionChecker
{
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
            EventCondition.NotNearWater => !ctx.CurrentLocation.HasFeature<WaterFeature>()
                && !(ctx.Map?.GetTravelOptions().Any(loc => loc.HasFeature<WaterFeature>()) ?? false),
            EventCondition.HazardousTerrain => ctx.CurrentLocation.GetEffectiveTerrainHazard() >= 0.5,
            EventCondition.HasFuelForage => ctx.CurrentLocation.GetFeature<ForageFeature>()?.HasFuelResources() ?? false,
            EventCondition.HighOverheadCover =>
                ctx.CurrentLocation.OverheadCoverLevel >= 0.6 ||
                (ctx.CurrentLocation.GetFeature<ShelterFeature>()?.OverheadCoverage ?? 0) >= 0.6,
            EventCondition.AtDisturbedSource =>
                ctx.Tensions.GetTension("Disturbed")?.SourceLocation == ctx.CurrentLocation,
            EventCondition.AtClaimedTerritory =>
                ctx.Tensions.GetTension("ClaimedTerritory")?.SourceLocation == ctx.CurrentLocation,

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
            EventCondition.HighWind => ctx.Weather.WindSpeedPct > 0.6,
            EventCondition.IsClear => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Clear,
            EventCondition.IsMisty => ctx.Weather.CurrentCondition == Weather.WeatherCondition.Misty,
            EventCondition.ExtremelyCold => ctx.Weather.BaseTemperature < -25,
            EventCondition.WeatherWorsening => ctx.Weather.WeatherJustChanged && IsWeatherWorsening(ctx.Weather),
            EventCondition.CalmBeforeTheStorm => ctx.Weather.CurrentFront?.Type == FrontType.ProlongedBlizzard
                                               && ctx.Weather.CurrentFront.CurrentStateIndex == 0,

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
            EventCondition.HerdOnTile => HasPreyHerdOnTile(ctx),

            EventCondition.DeadlyCold => ctx.Tensions.HasTension("DeadlyCold"),
            EventCondition.DeadlyColdCritical => ctx.Tensions.HasTensionAbove("DeadlyCold", 0.6),

            EventCondition.FeverRising => ctx.Tensions.HasTension("FeverRising"),
            EventCondition.FeverHigh => ctx.Tensions.HasTensionAbove("FeverRising", 0.4),
            EventCondition.FeverCritical => ctx.Tensions.HasTensionAbove("FeverRising", 0.7),

            EventCondition.MammothTracked => ctx.Tensions.HasTension("MammothTracked"),
            EventCondition.MammothTrackedHigh => ctx.Tensions.HasTensionAbove("MammothTracked", 0.6),

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
            EventCondition.HasMedicine => ctx.Inventory.GetCount(ResourceCategory.Medicine) > 0,
            EventCondition.HasSticks => ctx.Inventory.Count(Resource.Stick) > 0,
            EventCondition.HasCookedMeat => ctx.Inventory.Count(Resource.CookedMeat) > 0,

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
            EventCondition.Foraging => ctx.CurrentActivity == ActivityType.Foraging,

            // Trapping conditions
            EventCondition.HasActiveSnares => HasActiveSnares(ctx),
            EventCondition.SnareHasCatch => HasSnareCatch(ctx),
            EventCondition.SnareBaited => HasBaitedSnares(ctx),
            EventCondition.TrapLineActive => ctx.Tensions.HasTension("TrapLineActive"),

            // Spatial/grid conditions
            EventCondition.FarFromCamp => GetDistanceFromCamp(ctx) > 8,
            EventCondition.VeryFarFromCamp => GetDistanceFromCamp(ctx) > 15,
            EventCondition.NearMountains => HasAdjacentTerrain(ctx, TerrainType.Mountain),
            EventCondition.SurroundedByWater => CountAdjacentTerrain(ctx, TerrainType.Water) >= 2,
            EventCondition.IsForest => ctx.CurrentLocation?.Terrain == TerrainType.Forest,
            EventCondition.DeepInForest =>
                ctx.CurrentLocation?.Terrain == TerrainType.Forest &&
                CountAdjacentTerrain(ctx, TerrainType.Forest) >= 3,
            EventCondition.OnBoundary =>
                ctx.Map?.CurrentPosition.GetCardinalNeighbors()
                    .Select(p => ctx.Map.GetLocationAt(p)?.Terrain)
                    .Distinct()
                    .Count() > 2,
            EventCondition.Cornered => CountPassableExits(ctx) <= 2,
            EventCondition.AtTerrainBottleneck => CountPassableExits(ctx) <= 2 && GetDistanceFromCamp(ctx) > 5,
            EventCondition.JustRevealedLocation => ctx.Map?.RevealedNewLocations ?? false,

            // Carcass conditions
            EventCondition.HasCarcass => ctx.CurrentLocation.HasFeature<CarcassFeature>(),
            EventCondition.HasFreshCarcass => GetCarcassScentIntensity(ctx) > 0.5,
            EventCondition.HasStrongScent => GetCarcassScentIntensity(ctx) > 0.6,

            // Player scent conditions
            EventCondition.PlayerBloody => ctx.player.EffectRegistry.HasEffect("Bloody"),
            EventCondition.PlayerBloodyHigh => ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.2,

            // Equipment state conditions
            EventCondition.Waterproofed => ctx.Inventory.CalculateWaterproofingLevel() >= 0.15,
            EventCondition.FullyWaterproofed => ctx.Inventory.CalculateWaterproofingLevel() >= 0.4,

            // Equipment possession
            EventCondition.HasWeapon => ctx.Inventory.HasWeapon,
            EventCondition.HasFirestarter => ctx.Inventory.HasFirestarter,

            // Saber-tooth arc
            EventCondition.SaberToothStalked => ctx.Tensions.HasTension("SaberToothStalked"),

            // Terrain escape options - trees or rocky terrain provide escape routes
            EventCondition.HasEscapeTerrain => ctx.CurrentLocation.Terrain == TerrainType.Forest
                                            || ctx.CurrentLocation.Terrain == TerrainType.Mountain
                                            || ctx.CurrentLocation.Terrain == TerrainType.Hills,

            // Butchering activity
            EventCondition.IsButchering => ctx.CurrentActivity == ActivityType.Butchering,

            // Visibility alias
            EventCondition.GoodVisibility => ctx.CurrentLocation.VisibilityFactor > 0.7,

            _ => false,
        };
    }

    // === CARCASS HELPERS ===

    private static double GetCarcassScentIntensity(GameContext ctx)
    {
        var carcass = ctx.CurrentLocation.GetFeature<CarcassFeature>();
        return carcass?.ScentIntensity ?? 0;
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
        AnyLocationHasSnare(ctx, s => s.HasCatchWaiting);

    private static bool HasBaitedSnares(GameContext ctx) =>
        AnyLocationHasSnare(ctx, s => s.HasBaitedSnares);

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

    // === HERD HELPERS ===

    /// <summary>
    /// Check if a prey herd is on the player's current tile.
    /// </summary>
    private static bool HasPreyHerdOnTile(GameContext ctx)
    {
        if (ctx.Map == null) return false;
        var herds = ctx.Herds.GetHerdsAt(ctx.Map.CurrentPosition);
        return herds.Any(h => !h.IsPredator && h.Count > 0);
    }

    // === SPATIAL HELPERS ===

    private static int GetDistanceFromCamp(GameContext ctx)
    {
        if (ctx.Map == null) return 0;
        var campPos = ctx.Map.GetPosition(ctx.Camp);
        return ctx.Map.CurrentPosition.ManhattanDistance(campPos);
    }

    private static bool HasAdjacentTerrain(GameContext ctx, TerrainType terrain)
    {
        if (ctx.Map == null) return false;
        foreach (var neighbor in ctx.Map.CurrentPosition.GetCardinalNeighbors())
        {
            var loc = ctx.Map.GetLocationAt(neighbor);
            if (loc?.Terrain == terrain) return true;
        }
        return false;
    }

    private static int CountAdjacentTerrain(GameContext ctx, TerrainType terrain)
    {
        if (ctx.Map == null) return 0;
        int count = 0;
        foreach (var neighbor in ctx.Map.CurrentPosition.GetCardinalNeighbors())
        {
            var loc = ctx.Map.GetLocationAt(neighbor);
            if (loc?.Terrain == terrain) count++;
        }
        return count;
    }

    private static int CountPassableExits(GameContext ctx)
    {
        if (ctx.Map == null) return 4;
        return ctx.Map.CurrentPosition.GetCardinalNeighbors()
            .Count(pos => ctx.Map.GetLocationAt(pos)?.IsPassable ?? false);
    }
}
