using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Factories;

/// <summary>
/// Centralized factory for creating common feature patterns.
/// Consolidates feature creation logic from LocationFactory with reusable patterns.
/// </summary>
public static class FeatureFactory
{
    #region ForageFeature Factories

    /// <summary>
    /// Mixed forest forage - wood, fungi, conifer products.
    /// Used by: Forest (2.0), Clearing (1.3), Sheltered Valley (1.6)
    /// </summary>
    public static ForageFeature CreateMixedForestForage(double density = 2.0)
    {
        return new ForageFeature(density)
            .AddMixedWood(1.5, 1.5, 3.5)
            .AddSticks(3.0, 0.2, 0.6)
            .AddTinder(2.0, 0.02, 0.08)
            .AddBerries(0.6, 0.05, 0.15)
            .AddPlantFiber(0.5, 0.05, 0.15)
            .AddBirchPolypore(0.15)
            .AddChaga(0.1)
            .AddAmadou(0.12)
            .AddPineNeedles(0.25)
            .AddPineResin(0.1)
            .AddUsnea(0.15);
    }

    /// <summary>
    /// Deadwood forage - exceptional fuel resources, fungi.
    /// Used by: Deadwood Grove (3.0)
    /// </summary>
    public static ForageFeature CreateDeadwoodForage(double density = 3.0)
    {
        return new ForageFeature(density)
            .AddPine(2.5, 1.5, 4.0)
            .AddBirch(0.5, 1.5, 3.5)
            .AddSticks(4.0, 0.2, 0.7)
            .AddTinder(3.0, 0.02, 0.1)
            .AddAmadou(0.25)
            .AddBirchPolypore(0.2);
    }

    /// <summary>
    /// Burnt stand forage - charcoal, dry fuel.
    /// Used by: Burnt Stand (2.0)
    /// </summary>
    public static ForageFeature CreateBurntStandForage(double density = 2.0)
    {
        return new ForageFeature(density)
            .AddCharcoal(0.8, 0.05, 0.2)
            .AddTinder(3.0, 0.02, 0.08)
            .AddSticks(2.5, 0.2, 0.5)
            .AddPine(1.5, 1.0, 2.5);
    }

    /// <summary>
    /// Old growth forage - sparse, fungi only.
    /// Used by: Ancient Grove (0.4)
    /// </summary>
    public static ForageFeature CreateOldGrowthForage(double density = 0.4)
    {
        return new ForageFeature(density)
            .AddTinder(0.3, 0.01, 0.04)
            .AddBirchPolypore(0.15, 0.05, 0.15)
            .AddChaga(0.1, 0.05, 0.2);
    }

    /// <summary>
    /// Wetland forage - birch, willow, plant fiber.
    /// Used by: Riverbank (1.2), Marsh (1.8), Beaver Dam (2.5)
    /// </summary>
    public static ForageFeature CreateWetlandForage(double density = 1.2)
    {
        return new ForageFeature(density)
            .AddSticks(2.0, 0.2, 0.5)
            .AddBirch(1.0, 1.0, 2.0)
            .AddStone(0.6, 0.2, 0.5)
            .AddWillowBark(0.25)
            .AddSphagnum(0.15);
    }

    /// <summary>
    /// Open/sparse forage - grass, minimal resources.
    /// Used by: Plain (0.5), Game Trail (0.6)
    /// </summary>
    public static ForageFeature CreateOpenForage(double density = 0.5)
    {
        return new ForageFeature(density)
            .AddTinder(0.7, 0.03, 0.1)
            .AddSticks(0.2, 0.1, 0.3)
            .AddBerries(0.15, 0.03, 0.1)
            .AddPlantFiber(0.6, 0.05, 0.12);
    }

    /// <summary>
    /// Rocky forage - stone/flint focused.
    /// Used by: Hillside (0.4), Boulder Field (0.8), Granite Outcrop (0.8), Rocky Ridge (0.3), Overlook (0.4)
    /// </summary>
    public static ForageFeature CreateRockyForage(double density = 0.6)
    {
        return new ForageFeature(density)
            .AddStone(1.5, 0.25, 0.7)
            .AddTinder(0.2, 0.01, 0.04)
            .AddFlint(0.2, 0.1, 0.25);
    }

    /// <summary>
    /// Flint-rich forage - premium tool stone.
    /// Used by: Flint Seam (1.5)
    /// </summary>
    public static ForageFeature CreateFlintForage(double density = 1.5)
    {
        return new ForageFeature(density)
            .AddFlint(1.5, 0.15, 0.4)
            .AddStone(0.5, 0.2, 0.4);
    }

    /// <summary>
    /// Barren forage - minimal stone and tinder.
    /// Used by: Cave (0.3), Ice Crevasse (0.1), Hot Spring (0.3), Rock Overhang (0.3), Meltwater Pool (0.3)
    /// </summary>
    public static ForageFeature CreateBarrenForage(double density = 0.3)
    {
        return new ForageFeature(density)
            .AddStone(0.4, 0.1, 0.3)
            .AddTinder(0.2, 0.01, 0.04);
    }

    /// <summary>
    /// Small game haven forage - fiber + berries.
    /// Used by: Thicket (1.2)
    /// </summary>
    public static ForageFeature CreateSmallGameHavenForage(double density = 1.2)
    {
        return new ForageFeature(density)
            .AddPlantFiber(1.5, 0.1, 0.25)
            .AddSticks(1.0, 0.15, 0.4)
            .AddBerries(0.8, 0.05, 0.15);
    }

    /// <summary>
    /// Picked over forage - area depleted by previous habitation.
    /// Used by: Abandoned Camp (0.4), Wolf Den (0.6)
    /// </summary>
    public static ForageFeature CreatePickedOverForage(double density = 0.4)
    {
        return new ForageFeature(density)
            .AddSticks(0.5, 0.1, 0.3)
            .AddTinder(0.3, 0.01, 0.04);
    }

    /// <summary>
    /// Frozen creek forage - limited driftwood and stone.
    /// Used by: Frozen Creek (0.6)
    /// </summary>
    public static ForageFeature CreateFrozenCreekForage(double density = 0.6)
    {
        return new ForageFeature(density)
            .AddSticks(1.5, 0.15, 0.4)
            .AddStone(0.3, 0.2, 0.4);
    }

    /// <summary>
    /// Create forage appropriate for a terrain type with randomized density.
    /// Uses normal distribution: most tiles moderate, few abundant, few sparse.
    /// </summary>
    public static ForageFeature CreateTerrainForage(TerrainType terrain)
    {
        var (baseDensity, factory) = terrain switch
        {
            TerrainType.Forest => (0.6, (Func<double, ForageFeature>)CreateMixedForestForage),
            TerrainType.Clearing => (0.5, (Func<double, ForageFeature>)CreateMixedForestForage),
            TerrainType.Marsh => (0.5, (Func<double, ForageFeature>)CreateWetlandForage),
            TerrainType.Water => (0.3, (Func<double, ForageFeature>)CreateFrozenCreekForage),
            TerrainType.Plain => (0.25, (Func<double, ForageFeature>)CreateOpenForage),
            TerrainType.Hills => (0.35, (Func<double, ForageFeature>)CreateRockyForage),
            TerrainType.Rock => (0.4, (Func<double, ForageFeature>)CreateRockyForage),
            _ => (0.3, (Func<double, ForageFeature>)CreateBarrenForage)
        };

        double density = Utils.RandomNormal(baseDensity, 0.2);
        density = Math.Clamp(density, 0.1, 1.0);

        return factory(density);
    }

    #endregion

    #region AnimalTerritoryFeature Factories

    /// <summary>
    /// Mixed prey animals - deer + small game.
    /// Used by: Forest (1.2), Clearing (1.0), Sheltered Valley (1.3), Ancient Grove (0.5), Hot Spring (0.6)
    /// </summary>
    public static AnimalTerritoryFeature CreateMixedPreyAnimals(double density = 1.0)
    {
        return new AnimalTerritoryFeature(density)
            .AddDeer(1.0)
            .AddRabbit(0.6)
            .AddFox(0.3);
    }

    /// <summary>
    /// Game trail animals - peak activity at dawn.
    /// Used by: Game Trail (0.6)
    /// </summary>
    public static AnimalTerritoryFeature CreateGameTrailAnimals(double density = 0.6)
    {
        return new AnimalTerritoryFeature(density)
            .AddDeer(1.5)
            .AddRabbit(1.0)
            .AddFox(0.3)
            .WithPeakHours(5, 8, 2.5);
    }

    /// <summary>
    /// Small game animals - rabbit + ptarmigan.
    /// Used by: Plain (0.8), Thicket (1.2), Deadwood (0.7), Overlook (0.4)
    /// </summary>
    public static AnimalTerritoryFeature CreateSmallGameAnimals(double density = 0.8)
    {
        return new AnimalTerritoryFeature(density)
            .AddRabbit(1.2)
            .AddPtarmigan(1.0);
    }

    /// <summary>
    /// Waterfowl animals - ptarmigan-heavy.
    /// Used by: Marsh (0.9), Beaver Dam (0.7)
    /// </summary>
    public static AnimalTerritoryFeature CreateWaterfowlAnimals(double density = 0.9)
    {
        return new AnimalTerritoryFeature(density)
            .AddPtarmigan(1.5)
            .AddRabbit(0.5);
    }

    /// <summary>
    /// Wolf den animals - predator territory.
    /// Used by: Wolf Den (1.5)
    /// </summary>
    public static AnimalTerritoryFeature CreateWolfDenAnimals(double density = 1.5)
    {
        return new AnimalTerritoryFeature(density)
            .AddWolf(2.0)
            .AddRabbit(0.3);
    }

    /// <summary>
    /// Bear cave animals - bear territory.
    /// Used by: Bear Cave (0.8)
    /// </summary>
    public static AnimalTerritoryFeature CreateBearCaveAnimals(double density = 0.8)
    {
        return new AnimalTerritoryFeature(density)
            .AddBear(2.0)
            .AddAnimal("cave bear", 0.3);
    }

    /// <summary>
    /// Sparse animals - minimal game.
    /// Used by: Burnt Stand (0.3)
    /// </summary>
    public static AnimalTerritoryFeature CreateSparseAnimals(double density = 0.3)
    {
        return new AnimalTerritoryFeature(density)
            .AddRabbit(1.0);
    }

    #endregion

    #region HarvestableFeature Factories

    // Water Sources

    public static HarvestableFeature CreateRiver()
    {
        var river = new HarvestableFeature("river", "Ice-Fed River")
        {
            Description = "A swift-flowing river fed by glacial meltwater. Cold but clear.",
            MinutesToHarvest = 1
        };
        river.AddWater("water", maxQuantity: 100, litersPerUnit: 1.0, respawnHoursPerUnit: 0.1);
        return river;
    }

    public static HarvestableFeature CreateForestPuddle()
    {
        var puddle = new HarvestableFeature("puddle", "Forest Puddle")
        {
            Description = "A shallow puddle fed by melting snow.",
            MinutesToHarvest = 2
        };
        puddle.AddWater("water", maxQuantity: 3, litersPerUnit: 0.5, respawnHoursPerUnit: 12.0);
        return puddle;
    }

    public static HarvestableFeature CreateMeltwaterPuddle()
    {
        var puddle = new HarvestableFeature("puddle", "Meltwater Puddle")
        {
            Description = "A shallow depression with fresh meltwater. Frozen at edges.",
            MinutesToHarvest = 2
        };
        puddle.AddWater("water", maxQuantity: 2, litersPerUnit: 0.5, respawnHoursPerUnit: 24.0);
        return puddle;
    }

    public static HarvestableFeature CreateIceSource()
    {
        var ice = new HarvestableFeature("ice", "Creek Ice")
        {
            Description = "Thick ice covers the creek. Can be broken and melted for water.",
            MinutesToHarvest = 5
        };
        ice.AddWater("ice chunks", maxQuantity: 20, litersPerUnit: 1.0, respawnHoursPerUnit: 48.0);
        return ice;
    }

    public static HarvestableFeature CreateHotSpring()
    {
        var spring = new HarvestableFeature("hot_spring", "Thermal Pool")
        {
            Description = "Warm water bubbles up from deep below. Safe to drink once cooled.",
            MinutesToHarvest = 2
        };
        spring.AddWater("warm water", maxQuantity: 50, litersPerUnit: 1.0, respawnHoursPerUnit: 0.1);
        return spring;
    }

    public static HarvestableFeature CreateMeltwaterPool()
    {
        var pool = new HarvestableFeature("meltwater_pool", "Glacial Pool")
        {
            Description = "Crystal-clear meltwater. Pure but painfully cold.",
            MinutesToHarvest = 3
        };
        pool.AddWater("meltwater", maxQuantity: 20, litersPerUnit: 1.0, respawnHoursPerUnit: 24.0);
        return pool;
    }

    public static HarvestableFeature CreateMarshWater()
    {
        var water = new HarvestableFeature("marsh_water", "Open Water")
        {
            Description = "Dark water between ice sheets. Needs to be boiled.",
            MinutesToHarvest = 3
        };
        water.AddWater("marsh water", maxQuantity: 30, litersPerUnit: 1.0, respawnHoursPerUnit: 6.0);
        return water;
    }

    public static HarvestableFeature CreateBeaverPond()
    {
        var pond = new HarvestableFeature("beaver_pond", "Beaver Pond")
        {
            Description = "Still water backed up behind the dam. Deep and clear.",
            MinutesToHarvest = 2
        };
        pond.AddWater("pond water", maxQuantity: 50, litersPerUnit: 1.0, respawnHoursPerUnit: 0.5);
        return pond;
    }

    // Vegetation

    public static HarvestableFeature CreateBerryBush()
    {
        var bush = new HarvestableFeature("berry_bush", "Wild Berry Bush")
        {
            Description = "A frost-hardy shrub with clusters of dark berries."
        };
        bush.AddBerries("berries", maxQuantity: 5, weightPerUnit: 0.1, respawnHoursPerUnit: 168.0);
        return bush;
    }

    public static HarvestableFeature CreateCattails()
    {
        var cattails = new HarvestableFeature("cattails", "Cattail Stand")
        {
            Description = "Dense cattails at the marsh edge. Edible roots, fluffy seed heads for tinder.",
            MinutesToHarvest = 10
        };
        cattails.AddPlantFiber("cattail fiber", maxQuantity: 10, weightPerUnit: 0.1, respawnHoursPerUnit: 168.0);
        cattails.AddTinder("cattail fluff", maxQuantity: 6, weightPerUnit: 0.03, respawnHoursPerUnit: 168.0);
        return cattails;
    }

    // Deadfall

    public static HarvestableFeature CreateMixedDeadfall()
    {
        var deadfall = new HarvestableFeature("deadfall", "Fallen Tree")
        {
            Description = "A wind-felled tree with dry, harvestable wood.",
            MinutesToHarvest = 10
        };
        deadfall.AddLogs("firewood", Resource.Pine, maxQuantity: 4, weightPerUnit: 2.5, respawnHoursPerUnit: 720.0);
        deadfall.AddSticks("branches", maxQuantity: 8, weightPerUnit: 0.3, respawnHoursPerUnit: 168.0);
        deadfall.AddTinder("bark strips", maxQuantity: 3, weightPerUnit: 0.05, respawnHoursPerUnit: 168.0);
        return deadfall;
    }

    public static HarvestableFeature CreateMassiveDeadfall()
    {
        var deadfall = new HarvestableFeature("massive_deadfall", "Massive Fallen Pine")
        {
            Description = "A huge pine, wind-felled and bone dry. Enough fuel to last days.",
            MinutesToHarvest = 20
        };
        deadfall.AddLogs("dry pine logs", Resource.Pine, maxQuantity: 8, weightPerUnit: 2.5, respawnHoursPerUnit: 0);
        deadfall.AddSticks("branches", maxQuantity: 15, weightPerUnit: 0.3, respawnHoursPerUnit: 0);
        deadfall.AddTinder("bark strips", maxQuantity: 5, weightPerUnit: 0.05, respawnHoursPerUnit: 0);
        return deadfall;
    }

    #endregion
}
