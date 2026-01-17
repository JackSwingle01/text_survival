using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Environments.Factories;

public static class LocationFactory
{
    #region Terrain Location Factory

    /// <summary>
    /// Create a terrain-only location for a grid tile.
    /// These locations have basic features derived from terrain type.
    /// Named locations with rich features replace these when placed on the grid.
    /// </summary>
    public static Location MakeTerrainLocation(TerrainType terrain, Weather weather, int? positionSeed = null)
    {
        var location = new Location(
            name: GetTerrainDisplayName(terrain),
            tags: "",
            weather: weather,
            terrainHazardLevel: terrain.BaseHazardLevel(),
            windFactor: terrain.BaseWindFactor(),
            overheadCoverLevel: terrain.BaseOverheadCover(),
            visibilityFactor: terrain.BaseVisibility())
        {
            Terrain = terrain,
            IsTerrainOnly = true
        };

        // Generate random ±20% modifier for variation (if position seed provided)
        if (positionSeed.HasValue)
        {
            Random rng = new Random(positionSeed.Value);
            location.TraversalModifier = 0.8 + (rng.NextDouble() * 0.4);  // Range: 0.8-1.2

            // Add environmental details for terrain flavor
            var details = EnvironmentalDetailFactory.GenerateForTerrain(terrain, positionSeed.Value);
            foreach (var detail in details)
            {
                location.Features.Add(detail);
            }
        }

        // Add terrain-appropriate forage with randomized density
        if (terrain.IsPassable())
        {
            location.Features.Add(FeatureFactory.CreateTerrainForage(terrain));

            // 10% chance for terrain-appropriate harvestable
            if (Utils.DetermineSuccess(0.1))
            {
                var harvestable = GetTerrainHarvestable(terrain);
                if (harvestable != null)
                    location.Features.Add(harvestable);
            }

            // 10% chance for terrain-appropriate animals
            if (Utils.DetermineSuccess(0.1))
            {
                var animals = GetTerrainAnimals(terrain);
                if (animals != null)
                    location.Features.Add(animals);
            }

            // Add wooded area to forest terrain
            if (terrain == TerrainType.Forest)
            {
                location.Features.Add(new WoodedAreaFeature("Forest Trees", null, 100));
            }
        }

        return location;
    }

    private static HarvestableFeature? GetTerrainHarvestable(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => Utils.FlipCoin() ? FeatureFactory.CreateBerryBush() : FeatureFactory.CreateMixedDeadfall(),
        TerrainType.Clearing => FeatureFactory.CreateBerryBush(),
        TerrainType.Plain => null, // Water via EnvironmentalDetail
        TerrainType.Marsh => Utils.FlipCoin() ? FeatureFactory.CreateCattails() : FeatureFactory.CreateMarshWater(),
        TerrainType.Water => FeatureFactory.CreateIceSource(),
        _ => null
    };

    private static AnimalTerritoryFeature? GetTerrainAnimals(TerrainType terrain)
    {
        var (baseDensity, factory) = terrain switch
        {
            TerrainType.Forest => (0.4, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateMixedPreyAnimals),
            TerrainType.Clearing => (0.35, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateMixedPreyAnimals),
            TerrainType.Plain => (0.3, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateSmallGameAnimals),
            TerrainType.Hills => (0.25, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateSmallGameAnimals),
            TerrainType.Marsh => (0.35, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateWaterfowlAnimals),
            TerrainType.Rock => (0.2, (Func<double, AnimalTerritoryFeature>)FeatureFactory.CreateSmallGameAnimals),
            _ => (0.0, (Func<double, AnimalTerritoryFeature>?)null)
        };

        if (factory == null) return null;

        double density = Utils.RandomNormal(baseDensity, 0.15);
        density = Math.Clamp(density, 0.1, 0.8);

        return factory(density);
    }

    /// <summary>
    /// Get a display name for terrain types.
    /// </summary>
    private static string GetTerrainDisplayName(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => "Dense Forest",
        TerrainType.Clearing => "Forest Clearing",
        TerrainType.Plain => "Snowy Plain",
        TerrainType.Hills => "Rocky Hills",
        TerrainType.Water => "Frozen Lake",
        TerrainType.Marsh => "Frozen Marsh",
        TerrainType.Rock => "Rocky Ground",
        TerrainType.Mountain => "Mountain",
        TerrainType.DeepWater => "Deep Water",
        _ => terrain.ToString()
    };

    #endregion

    #region Site Factories

    public static Location MakeForest(Weather weather)
    {
        var location = new Location(
            name: "Deep Woods",
            tags: "[Shaded] [Resource-Dense]",
            weather: weather,
            terrainHazardLevel: .2,
            windFactor: .6,
            overheadCoverLevel: .3,
            visibilityFactor: .7);

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 2.0));
        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 1.2));

        if (Utils.DetermineSuccess(0.3))
            location.Features.Add(FeatureFactory.CreateBerryBush());

        if (Utils.DetermineSuccess(0.2))
            location.Features.Add(FeatureFactory.CreateMixedDeadfall());

        location.Features.Add(new WoodedAreaFeature("Standing Timber", null, 150));

        return location;
    }

    public static Location MakeCave(Weather weather)
    {
        var location = new Location(
            name: "Cave",
            tags: "[Sheltered] [Dark]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.1,
            overheadCoverLevel: 1.0,
            visibilityFactor: 0.3);

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));
        location.Features.Add(ShelterFeature.CreateCave());

        return location;
    }

    public static Location MakeRiverbank(Weather weather)
    {
        var location = new Location(
            name: "Riverbank",
            tags: "[Water] [Open]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 1.0,
            overheadCoverLevel: 0.1,
            visibilityFactor: 1.2);

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 1.2));
        location.Features.Add(FeatureFactory.CreateRiver());

        var waterFeature = new WaterFeature("river_water", "River")
            .WithDescription("Fast-flowing sections stay open, but ice forms at the edges.")
            .WithIceThickness(0.5);
        location.Features.Add(waterFeature);

        return location;
    }

    public static Location MakePlain(Weather weather)
    {
        var location = new Location(
            name: "Windswept Flats",
            tags: "[Exposed] [Open]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 1.4,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5);

        location.Features.Add(FeatureFactory.CreateOpenForage(density: 0.5));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.8));

        return location;
    }

    public static Location MakeHillside(Weather weather)
    {
        var location = new Location(
            name: "Hillside",
            tags: "[Steep] [Rocky]",
            weather: weather,
            terrainHazardLevel: 0.5,
            windFactor: 1.3,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3);

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.4));

        return location;
    }

    public static Location MakeClearing(Weather weather)
    {
        var location = new Location(
            name: "Sheltered Glade",
            tags: "[Sheltered] [Clearing]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.6,
            overheadCoverLevel: 0.2,
            visibilityFactor: 1.1);

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 1.3));
        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 1.0));
        location.Features.Add(new WoodedAreaFeature("Forest Edge", null, 120));

        return location;
    }

    /// <summary>
    /// Hot spring - thermal pull. Warmth bonus draws players here for shelter.
    /// </summary>
    public static Location MakeHotSpring(Weather weather)
    {
        var location = new Location(
            name: "Hot Spring",
            tags: "[Warm] [Water]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 0.4,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.8)
        {
            DiscoveryText = "Steam rises from a pool of warm water. The air here is noticeably warmer.",
            FirstVisitEvent = GameEventRegistry.FirstVisitHotSpring
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));
        location.Features.Add(FeatureFactory.CreateHotSpring());
        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 0.6));

        return location;
    }

    /// <summary>
    /// Frozen creek - water source but hazardous and slippery.
    /// </summary>
    public static Location MakeFrozenCreek(Weather weather)
    {
        var location = new Location(
            name: "Frozen Creek",
            tags: "[Ice] [Water] [Slippery]",
            weather: weather,
            terrainHazardLevel: 0.35,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.0)
        {
            DiscoveryText = "The creek is frozen solid. Dark shapes move beneath the ice."
        };

        location.Features.Add(FeatureFactory.CreateFrozenCreekForage(density: 0.6));
        location.Features.Add(FeatureFactory.CreateIceSource());

        var waterFeature = new WaterFeature("creek_water", "Frozen Creek")
            .WithDescription("The creek is frozen solid. Safe to cross if you're careful.")
            .AsSolidIce();
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Deadwood grove - excellent fuel source but dangerous footing from tangled logs.
    /// </summary>
    public static Location MakeDeadwoodGrove(Weather weather)
    {
        var location = new Location(
            name: "Deadwood Grove",
            tags: "[Fuel] [Treacherous]",
            weather: weather,
            terrainHazardLevel: 0.7,
            windFactor: 0.5,
            overheadCoverLevel: 0.2,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "A tangle of fallen trees, bleached and dry. Fuel everywhere - but one wrong step could snap an ankle."
        };

        location.Features.Add(FeatureFactory.CreateDeadwoodForage(density: 3.0));
        location.Features.Add(FeatureFactory.CreateMassiveDeadfall());
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.7));

        return location;
    }

    /// <summary>
    /// Rocky overlook - high visibility for scouting, exposed, good stone.
    /// </summary>
    public static Location MakeOverlook(Weather weather)
    {
        var location = new Location(
            name: "Overlook",
            tags: "[Scout] [Exposed] [Stone]",
            weather: weather,
            terrainHazardLevel: 0.4,
            windFactor: 1.6,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.8)
        {
            DiscoveryText = "The view stretches for miles. You can see smoke from distant fires, animal trails below."
        };

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.4));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.4));

        return location;
    }

    /// <summary>
    /// Marsh - treacherous but resource-rich. Waterfowl, medicinal plants, cattails.
    /// </summary>
    public static Location MakeMarsh(Weather weather)
    {
        var location = new Location(
            name: "Frozen Muskeg",
            tags: "[Water] [Treacherous] [Plants]",
            weather: weather,
            terrainHazardLevel: 0.4,
            windFactor: 0.7,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.6)
        {
            DiscoveryText = "The ground gives way to frozen marsh. Cattails poke through the ice. Rich foraging if you're careful."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 1.8));
        location.Features.Add(FeatureFactory.CreateCattails());
        location.Features.Add(FeatureFactory.CreateMarshWater());

        var waterFeature = new WaterFeature("marsh_ice", "Marsh Ice")
            .WithDescription("Thin ice between tussocks. One wrong step and you're through.")
            .AsThinIce();
        location.Features.Add(waterFeature);

        location.Features.Add(FeatureFactory.CreateWaterfowlAnimals(density: 0.9));

        return location;
    }

    /// <summary>
    /// Ice crevasse - natural cache site. Dangerous to reach but preserves food.
    /// </summary>
    public static Location MakeIceCrevasse(Weather weather)
    {
        var location = new Location(
            name: "Ice Crevasse",
            tags: "[Ice] [Cache] [Dangerous]",
            weather: weather,
            terrainHazardLevel: 0.8,
            windFactor: 0.2,
            overheadCoverLevel: 0.8,
            visibilityFactor: 0.2)
        {
            DiscoveryText = "A deep crack in ancient ice. Freezing cold, but perfect for storing meat.",
            IsDark = true
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.1));
        location.Features.Add(CacheFeature.CreateIceCache());

        return location;
    }

    /// <summary>
    /// Abandoned camp - salvage site with one-time loot.
    /// </summary>
    public static Location MakeAbandonedCamp(Weather weather)
    {
        var location = new Location(
            name: "Old Campsite",
            tags: "[Salvage] [Shelter]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.5,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.7)
        {
            DiscoveryText = "Signs of an old camp. The fire pit is cold, shelter collapsed. Someone was here before you."
        };

        location.Features.Add(FeatureFactory.CreatePickedOverForage(density: 0.4));
        location.Features.Add(SalvageFeature.CreateAbandonedCamp());
        location.Features.Add(new ShelterFeature("Collapsed Lean-to", 0.2, 0.4, 0.3));

        return location;
    }

    /// <summary>
    /// Wolf den - dangerous but rewarding hunting grounds.
    /// </summary>
    public static Location MakeWolfDen(Weather weather)
    {
        var location = new Location(
            name: "Wolf Den",
            tags: "[Wolves] [Dangerous] [Bones]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 0.4,
            overheadCoverLevel: 0.6,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "Wolf tracks everywhere. Bones scattered around a rocky hollow. The smell of predator is strong."
        };

        location.Features.Add(FeatureFactory.CreatePickedOverForage(density: 0.6));
        location.Features.Add(FeatureFactory.CreateWolfDenAnimals(density: 1.5));

        return location;
    }

    /// <summary>
    /// Sheltered valley - protected from wind, good for extended stays.
    /// </summary>
    public static Location MakeShelteredValley(Weather weather)
    {
        var location = new Location(
            name: "Sheltered Valley",
            tags: "[Sheltered] [Camp-worthy]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.2,
            overheadCoverLevel: 0.4,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "A natural hollow, shielded from the worst winds. This would make a good camp."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 1.6));
        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 1.3));
        location.Features.Add(CacheFeature.CreateRockCache());
        location.Features.Add(new WoodedAreaFeature("Hardwood Stand", Resource.Oak, 180));

        // Saber-tooth drawn by abundant prey
        location.Features.Add(new MegafaunaPresenceFeature
        {
            MegafaunaType = AnimalType.SaberTooth,
            ActivityLevel = 0.45,    // More active where prey gathers
            RespawnHours = 480
        });

        return location;
    }

    /// <summary>
    /// Burnt stand - fire-damaged forest. Abundant dry fuel, charcoal, exposed sightlines.
    /// </summary>
    public static Location MakeBurntStand(Weather weather)
    {
        var location = new Location(
            name: "Burnt Stand",
            tags: "[Fuel] [Exposed] [Charcoal]",
            weather: weather,
            terrainHazardLevel: 0.20,
            windFactor: 0.9,        // No canopy protection
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)  // Open sightlines
        {
            DiscoveryText = "Fire came through here. Blackened trunks stand like pillars. Ash pads your footsteps."
        };

        location.Features.Add(FeatureFactory.CreateBurntStandForage(density: 2.0));
        location.Features.Add(FeatureFactory.CreateSparseAnimals(density: 0.3));

        return location;
    }

    /// <summary>
    /// Rock overhang - natural partial shelter with fire-efficient stone backing.
    /// </summary>
    public static Location MakeRockOverhang(Weather weather)
    {
        var location = new Location(
            name: "Rock Overhang",
            tags: "[Shelter] [Stone]",
            weather: weather,
            terrainHazardLevel: 0.20,
            windFactor: 0.4,        // Partial wind block
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.7)
        {
            DiscoveryText = "A stone lip juts from a cliff face. The ground beneath is dry. Wind passes over but the space below is calm."
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));
        location.Features.Add(ShelterFeature.CreateRockOverhang());

        return location;
    }

    /// <summary>
    /// Granite outcrop - exposed stone with tool materials and commanding view.
    /// </summary>
    public static Location MakeGraniteOutcrop(Weather weather)
    {
        var location = new Location(
            name: "Granite Outcrop",
            tags: "[Stone] [Exposed] [Vantage]",
            weather: weather,
            terrainHazardLevel: 0.35,
            windFactor: 1.0,        // Completely exposed
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5)  // Excellent sightlines
        {
            DiscoveryText = "Bare stone breaks through the landscape. Wind-scoured and exposed, the view is commanding. Stone flakes litter the base."
        };

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.8));

        return location;
    }

    /// <summary>
    /// Meltwater pool - remote glacial water source. Pure but exposed and cold.
    /// </summary>
    public static Location MakeMeltwaterPool(Weather weather)
    {
        var location = new Location(
            name: "Meltwater Pool",
            tags: "[Water] [Exposed] [Remote]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 1.0,        // Completely exposed
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "A depression where glacial meltwater collects. Crystal clear and painfully cold. Ice rings the edges."
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));

        var waterFeature = new WaterFeature("meltwater", "Meltwater Pool")
            .WithDescription("Glacial meltwater. Pure but frigid.")
            .WithIceThickness(0.2);
        location.Features.Add(waterFeature);

        location.Features.Add(FeatureFactory.CreateMeltwaterPool());

        return location;
    }

    // === TIER 2 LOCATIONS ===

    /// <summary>
    /// Ancient grove - old growth forest with premium hardwood. Requires axe to harvest.
    /// </summary>
    public static Location MakeAncientGrove(Weather weather)
    {
        var location = new Location(
            name: "Ancient Grove",
            tags: "[Forest] [Fuel] [Quiet]",
            weather: weather,
            terrainHazardLevel: 0.10,
            windFactor: 0.3,        // Dense canopy blocks wind
            overheadCoverLevel: 0.9,
            visibilityFactor: 0.4)  // Dark under canopy
        {
            DiscoveryText = "Old growth. Massive trunks, cathedral spacing, deep silence. The canopy blocks snow and light alike.",
            FirstVisitEvent = GameEventRegistry.FirstVisitAncientGrove
        };

        location.Features.Add(FeatureFactory.CreateOldGrowthForage(density: 1));

        // Premium hardwood - requires axe (unique to this location, keep inline)
        var hardwood = new HarvestableFeature("ancient_hardwood", "Ancient Hardwood")
        {
            Description = "Massive oak and ash. Dense, long-burning wood — if you can cut it.",
            MinutesToHarvest = 20
        };
        hardwood.AddLogs("hardwood logs", Resource.Oak, maxQuantity: 8, weightPerUnit: 4.0, respawnHoursPerUnit: 168.0);
        hardwood.RequiresTool(ToolType.Axe, ToolTier.Basic);
        location.Features.Add(hardwood);

        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 0.5));

        return location;
    }

    /// <summary>
    /// Flint seam - premium tool stone embedded in limestone.
    /// </summary>
    public static Location MakeFlintSeam(Weather weather)
    {
        var location = new Location(
            name: "Flint Seam",
            tags: "[Stone] [Exposed] [Remote]",
            weather: weather,
            terrainHazardLevel: 0.30,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            DiscoveryText = "Dark stripe cutting across exposed rock. Nodules of flint embedded in limestone. Sharp flakes litter the ground."
        };

        location.Features.Add(FeatureFactory.CreateFlintForage(density: 1.5));

        return location;
    }

    /// <summary>
    /// Game trail - worn path where animals move. Peak activity at dawn and dusk.
    /// </summary>
    public static Location MakeGameTrail(Weather weather)
    {
        var location = new Location(
            name: "Game Trail",
            tags: "[Forest] [Hunting] [Trail]",
            weather: weather,
            terrainHazardLevel: 0.05,
            windFactor: 0.6,
            overheadCoverLevel: 0.5,
            visibilityFactor: 0.8)
        {
            Terrain = TerrainType.Forest,
            TraversalModifier = 0.7,  // Well-worn path, 30% faster than regular forest
            DiscoveryText = "A worn path through the brush. Hoofprints overlap in the mud. They pass through here regularly."
        };

        location.Features.Add(FeatureFactory.CreateOpenForage(density: 0.6));
        location.Features.Add(FeatureFactory.CreateGameTrailAnimals(density: 0.6));

        // Saber-tooth ambush territory - stalks prey along the trail
        location.Features.Add(new MegafaunaPresenceFeature
        {
            MegafaunaType = AnimalType.SaberTooth,
            ActivityLevel = 0.4,
            RespawnHours = 480
        });

        return location;
    }

    /// <summary>
    /// Dense thicket - young growth so thick predators can't follow. Escape terrain.
    /// </summary>
    public static Location MakeDenseThicket(Weather weather)
    {
        var location = new Location(
            name: "Dense Thicket",
            tags: "[Forest] [Difficult] [Safe]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.2,         // Excellent wind block
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.3)   // Can't see far
        {
            Terrain = TerrainType.Forest,
            TraversalModifier = 1.5,  // Very slow movement - 50% slower than regular forest
            DiscoveryText = "Young growth so thick you can barely push through. Branches grab at you. Small animals scatter.",
            IsEscapeTerrain = true   // Large predators can't follow
        };

        location.Features.Add(FeatureFactory.CreateSmallGameHavenForage(density: 1.2));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 1.2));
        location.Features.Add(ShelterFeature.CreateDenseThicket());

        return location;
    }

    /// <summary>
    /// Boulder field - jumbled rocks provide escape routes from predators.
    /// </summary>
    public static Location MakeBoulderField(Weather weather)
    {
        var location = new Location(
            name: "Boulder Field",
            tags: "[Stone] [Difficult] [Safe]",
            weather: weather,
            terrainHazardLevel: 0.45,  // High injury risk
            windFactor: 0.7,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.9)
        {
            Terrain = TerrainType.Rock,
            TraversalModifier = 1.4,  // Difficult terrain - 40% slower than regular rock
            DiscoveryText = "Massive boulders tumbled across the slope. Gaps and crevices between them. Hard going, but wolves can't follow into the gaps.",
            IsEscapeTerrain = true
        };

        // Climb edges on all sides - scrambling over rocks
        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.8));

        return location;
    }

    /// <summary>
    /// Rocky ridge - spine of stone above treeline with commanding views.
    /// </summary>
    public static Location MakeRockyRidge(Weather weather)
    {
        var location = new Location(
            name: "Rocky Ridge",
            tags: "[Stone] [Exposed] [Vantage]",
            weather: weather,
            terrainHazardLevel: 0.35,
            windFactor: 1.2,         // Wind accelerates over ridge
            overheadCoverLevel: 0.0,
            visibilityFactor: 2.0)   // Maximum visibility
        {
            DiscoveryText = "Spine of broken stone above the treeline. Wind never stops. You can see for miles — both valley sides visible.",
            IsVantagePoint = true,
            FirstVisitEvent = GameEventRegistry.FirstVisitRockyRidge
        };

        // Climb edges with 100% first-climb narrative event
        location.WithEdgesOnAllSides(Grid.EdgeType.Climb, [
            new Grid.EdgeEvent(1.0, Actions.Events.EdgeEvents.FirstClimb("Rocky Ridge",
                "The ridge rises sharply. Broken stone and loose scree. Good handholds if you pick your route."))
        ]);

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.3));

        // Saber-tooth hunting ground - commands view of prey below
        location.Features.Add(new MegafaunaPresenceFeature
        {
            MegafaunaType = AnimalType.SaberTooth,
            ActivityLevel = 0.35,    // Elusive apex predator
            RespawnHours = 480       // 20 days between encounters
        });

        return location;
    }

    // === TIER 3 LOCATIONS ===

    /// <summary>
    /// Bear cave - occupied shelter. Superior protection if you can clear it.
    /// Works with existing Den arc events (TheFind, AssessingTheClaim, etc.)
    /// </summary>
    public static Location MakeBearCave(Weather weather)
    {
        var location = new Location(
            name: "Bear Cave",
            tags: "[Sheltered] [Dark] [Dangerous] [Bones]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.1,         // Deep cave blocks all wind
            overheadCoverLevel: 1.0,
            visibilityFactor: 0.2)
        {
            DiscoveryText = "A deep cave mouth. Dry floor, wind-sheltered depths. But there's a smell — musk and old kills. Something lives here.",
            IsDark = true,
            FirstVisitEvent = GameEventRegistry.FirstVisitBearCave
        };

        // Bones from bear kills - unique forage pattern, keep inline
        var forageFeature = new ForageFeature(0.8)
            .AddBone(2.0, 0.15, 0.5)
            .AddTinder(0.2, 0.01, 0.03);
        location.Features.Add(forageFeature);

        location.Features.Add(FeatureFactory.CreateBearCaveAnimals(density: 0.8));

        // Note: ShelterFeature intentionally NOT added at creation
        // It gets added by Den arc events when claimed (AddsFeature)

        return location;
    }

    /// <summary>
    /// Beaver dam - ecosystem resource. Harvestable wood and water but
    /// destroying it has consequences (flooding, ecosystem collapse).
    /// </summary>
    public static Location MakeBeaverDam(Weather weather)
    {
        var location = new Location(
            name: "Beaver Dam",
            tags: "[Water] [Fuel] [Wildlife]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.6,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.9)
        {
            DiscoveryText = "A beaver dam spans the stream. The pond behind it is still and deep. Gnawed stumps litter the banks."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 2.5));
        location.Features.Add(FeatureFactory.CreateBeaverPond());

        // The dam itself - destructive harvest with consequences (unique, keep inline)
        var dam = new HarvestableFeature("beaver_dam", "Beaver Dam")
        {
            Description = "Woven branches packed with mud. Easy fuel — if you don't mind the consequences.",
            MinutesToHarvest = 15
        };
        dam.AddLogs("dam logs", Resource.Birch, maxQuantity: 12, weightPerUnit: 2.0, respawnHoursPerUnit: 0);
        dam.AddSticks("dam sticks", maxQuantity: 20, weightPerUnit: 0.25, respawnHoursPerUnit: 0);
        location.Features.Add(dam);

        location.Features.Add(FeatureFactory.CreateWaterfowlAnimals(density: 0.7));

        var waterFeature = new WaterFeature("pond_water", "Beaver Pond")
            .WithDescription("Deep water. Ice at the edges, thin in places.")
            .WithIceThickness(0.4);
        location.Features.Add(waterFeature);

        return location;
    }

    // === TIER 4 LOCATIONS ===

    /// <summary>
    /// The Lookout - massive lone pine with climbing opportunity.
    /// From the top, see the mountain pass (win condition).
    /// High risk, high reward vantage point.
    /// Note: Tree climbing is a choice via FirstVisitEvent, not an approach hazard.
    /// </summary>
    public static Location MakeTheLookout(Weather weather)
    {
        var location = new Location(
            name: "The Lookout",
            tags: "[Vantage] [Climb] [Landmark]",
            weather: weather,
            terrainHazardLevel: 0.30,
            windFactor: 0.8,
            overheadCoverLevel: 0.4,
            visibilityFactor: 1.0)   // Normal at ground, exceptional from top
        {
            DiscoveryText = "A massive lone pine stands on a rise. Its branches form a natural ladder. From up there, you could see everything — including the mountain pass.",
            IsVantagePoint = true,
            FirstVisitEvent = GameEventRegistry.FirstVisitLookout
        };

        // Pine resources - unique mix for this location, keep inline
        var forageFeature = new ForageFeature(0.8)
            .AddSticks(1.0, 0.15, 0.4)
            .AddTinder(0.5, 0.02, 0.06)
            .AddPineNeedles(0.3)
            .AddPineResin(0.15);
        location.Features.Add(forageFeature);

        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.4));

        return location;
    }

    /// <summary>
    /// Old Campsite - narrative-rich salvage location.
    /// What happened here? Story unfolds through investigation.
    /// </summary>
    public static Location MakeOldCampsite(Weather weather)
    {
        // Determine the story of this camp
        var storyType = Utils.RandInt(0, 4);
        var (narrativeHook, extraLoot) = storyType switch
        {
            0 => ("Claw marks on the collapsed shelter. Blood, long dried. They didn't leave by choice.",
                  "predator_attack"),
            1 => ("A trail of belongings leads away, as if they left in a hurry. Or were dragged.",
                  "fled"),
            2 => ("Everything is orderly. Fire properly banked. They meant to come back.",
                  "never_returned"),
            3 => ("Carved into a tree: a tally of days. The marks stop at thirty-seven.",
                  "counted_days"),
            _ => ("The shelter is intact but empty. Snow has filled it. Whoever was here, they're long gone.",
                  "abandoned")
        };

        var location = new Location(
            name: "Old Campsite",
            tags: "[Salvage] [Shelter] [Story]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.5,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.7)
        {
            DiscoveryText = $"A clearing where the snow is depressed. Charcoal scattered. A collapsed lean-to. Someone was here. {narrativeHook}",
            FirstVisitEvent = GameEventRegistry.FirstVisitOldCampsite
        };

        // Create enhanced salvage based on story
        var salvage = new SalvageFeature("old_camp_salvage", "Camp Remnants")
        {
            DiscoveryText = "Signs of habitation. Might be something useful left.",
            NarrativeHook = narrativeHook,
            MinutesToSalvage = 30
        };

        // Base loot
        salvage.Resources.Add(Resource.Stick,0.4);
        salvage.Resources.Add(Resource.Tinder,0.08);
        salvage.Resources.Add(Resource.Charcoal,0.1);

        // Story-specific loot
        switch (extraLoot)
        {
            case "predator_attack":
                salvage.Resources.Add(Resource.Bone,0.3);
                salvage.Resources.Add(Resource.Hide,0.4);
                if (Utils.DetermineSuccess(0.3))
                    salvage.Tools.Add(Gear.Knife("Bloodied Knife", 4));
                break;
            case "fled":
                if (Utils.DetermineSuccess(0.5))
                    salvage.Tools.Add(Gear.Axe("Dropped Hand Axe", 6));
                salvage.Resources.Add(Resource.PlantFiber,0.2);
                break;
            case "never_returned":
                salvage.Resources.Add(Resource.Pine, 1.5);
                salvage.Resources.Add(Resource.Birch, 1.5);
                salvage.Resources.Add(Resource.Oak, 1.5);
                if (Utils.DetermineSuccess(0.4))
                {
                    var coat = new Gear
                    {
                        Name = "Cached Coat",
                        Category = GearCategory.Equipment,
                        Slot = EquipSlot.Chest,
                        Weight = 1.8,
                        BaseInsulation = 0.12,
                        Durability = 60,
                        MaxDurability = 80
                    };
                    salvage.Equipment.Add(coat);
                }
                break;
            case "counted_days":
                salvage.Resources.Add(Resource.Stone,0.3);
                if (Utils.DetermineSuccess(0.3))
                    salvage.Tools.Add(Gear.HandDrill("Worn Hand Drill", 5));
                break;
            default:
                salvage.Resources.Add(Resource.PlantFiber,0.15);
                break;
        }
        location.Features.Add(salvage);

        // Collapsed shelter can be repaired
        location.Features.Add(new ShelterFeature("Collapsed Lean-to", 0.15, 0.3, 0.25));

        location.Features.Add(FeatureFactory.CreatePickedOverForage(density: 0.3));

        return location;
    }

    // === NEW NATURAL LOCATIONS ===

    /// <summary>
    /// Peat Bog - resource hub with finite slow-burning fuel.
    /// High hazard terrain with exceptional fuel reward.
    /// </summary>
    public static Location MakePeatBog(Weather weather)
    {
        var location = new Location(
            name: "Peat Bog",
            tags: "[Marsh] [Fuel] [Treacherous]",
            weather: weather,
            terrainHazardLevel: 0.55,
            windFactor: 0.5,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.7)
        {
            Terrain = TerrainType.Marsh,
            TraversalModifier = 1.3,  // Treacherous footing - 30% slower than regular marsh
            DiscoveryText = "Dark water glints between mounds of spongy earth. The smell is old — ancient rot preserved in cold. Beneath the surface, centuries of compressed plant matter.",
            FirstVisitEvent = GameEventRegistry.FirstVisitPeatBog
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 1.0));
        location.Features.Add(FeatureFactory.CreateWaterfowlAnimals(density: 0.6));

        // Peat extraction - finite exceptional fuel (unique, keep inline)
        var peatBed = new HarvestableFeature("peat_bed", "Peat Bed")
        {
            Description = "Dark compressed plant matter. Burns slow and hot — if you can dig it out.",
            MinutesToHarvest = 20
        };
        peatBed.AddResource("peat blocks", Resource.Peat, maxQuantity: 15, weightPerUnit: 2.0, respawnHoursPerUnit: 0);
        location.Features.Add(peatBed);

        var bogPools = new WaterFeature("bog_pools", "Bog Pools")
            .WithDescription("Dark water between the mounds. Thin ice, uncertain depth.")
            .WithIceThickness(0.15);
        location.Features.Add(bogPools);

        return location;
    }

    /// <summary>
    /// Ice Shelf - tactical location combining escape terrain and vantage.
    /// Brutal wind exposure creates tradeoff: safe from predators but freezing.
    /// </summary>
    public static Location MakeIceShelf(Weather weather)
    {
        var location = new Location(
            name: "Ice Shelf",
            tags: "[Ice] [Vantage] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.40,
            windFactor: 1.4,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.8)
        {
            DiscoveryText = "A massive tongue of ice projects from the hillside — a frozen waterfall locked in time. The top is flat, wind-scoured. Nothing can approach unseen from below.",
            IsEscapeTerrain = true,
            IsVantagePoint = true,
            FirstVisitEvent = GameEventRegistry.FirstVisitIceShelf
        };

        // Climb edges on all sides - scrambling up frozen waterfall
        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.2));

        var iceFace = new WaterFeature("ice_face", "Ice Face")
            .WithDescription("Solid ice. Harvestable for water if you have tools.")
            .AsSolidIce();
        location.Features.Add(iceFace);

        return location;
    }

    /// <summary>
    /// Bone Hollow - narrative anchor with mammoth graveyard.
    /// Premium materials (bone, ivory) and natural shelter create forward camp potential.
    /// </summary>
    public static Location MakeBoneHollow(Weather weather)
    {
        var location = new Location(
            name: "Bone Hollow",
            tags: "[Bones] [Sheltered] [Ancient]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.3,
            overheadCoverLevel: 0.2,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "The ravine narrows, then opens into a hollow. Bones everywhere — tusks, ribs, skulls larger than your torso. Mammoths. A dozen of them, maybe more. They came here to die.",
            FirstVisitEvent = GameEventRegistry.FirstVisitBoneHollow
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));

        // Ancient bones - premium crafting materials (unique, keep inline)
        var ancientBones = new HarvestableFeature("ancient_bones", "Ancient Bones")
        {
            Description = "Mammoth remains. Large bones for tools, ivory for crafting. The ground is thick with them.",
            MinutesToHarvest = 15
        };
        ancientBones.AddResource("large bones", Resource.Bone, maxQuantity: 20, weightPerUnit: 1.5, respawnHoursPerUnit: 0);
        ancientBones.AddResource("ivory", Resource.Ivory, maxQuantity: 8, weightPerUnit: 2.0, respawnHoursPerUnit: 0);
        location.Features.Add(ancientBones);

        // Natural shelter from mammoth ribs
        location.Features.Add(new ShelterFeature("Rib Arch", 0.35, 0.4, 0.5));

        // Megafauna presence - woolly mammoths still pass through this ancient graveyard
        location.Features.Add(new MegafaunaPresenceFeature
        {
            MegafaunaType = AnimalType.Mammoth,
            ActivityLevel = 0.7,
            RespawnHours = 720  // 30 days between hunts
        });

        return location;
    }

    /// <summary>
    /// Wind Gap - environmental challenge. Dangerous shortcut through mountain notch.
    /// Risk/reward based entirely on player condition.
    /// </summary>
    public static Location MakeWindGap(Weather weather)
    {
        var location = new Location(
            name: "Wind Gap",
            tags: "[Mountain] [Exposed] [Shortcut]",
            weather: weather,
            terrainHazardLevel: 0.70,
            windFactor: 2.5,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.3)
        {
            DiscoveryText = "A notch in the ridge where wind funnels through with terrifying force. The snow is scoured to bare rock. But beyond — a whole section of the valley you couldn't reach otherwise.",
            TemperatureDeltaF = -8,
            FirstVisitEvent = GameEventRegistry.FirstVisitWindGap
        };

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.2));

        return location;
    }

    /// <summary>
    /// Snowfield Hollow - natural funnel for small game. Trapping-focused location.
    /// Exceptional small game density in exposed conditions.
    /// </summary>
    public static Location MakeSnowfieldHollow(Weather weather)
    {
        var location = new Location(
            name: "Snowfield Hollow",
            tags: "[Trapping] [Open] [Snow]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "A shallow bowl in the plain, ringed by low rises. Snow drifts pile against the windward side, leaving channels of bare ground. Small tracks crisscross everywhere — animals funnel through here to cross between sheltered areas.",
            FirstVisitEvent = GameEventRegistry.FirstVisitSnowfieldHollow
        };

        // Sparse foraging - animals strip this area clean
        var forage = new ForageFeature(0.4)
            .AddSticks(0.3, 0.1, 0.25)
            .AddPlantFiber(0.4, 0.05, 0.12)
            .AddBone(0.15, 0.1, 0.3)
            .AddTinder(0.2, 0.01, 0.04);
        location.Features.Add(forage);

        // Exceptional small game - the core draw for trappers
        var animals = new AnimalTerritoryFeature(1.4)
            .AddRabbit(2.0)
            .AddPtarmigan(1.5)
            .AddFox(0.6)
            .WithPeakHours(5, 8, 2.0);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Sun-Warmed Cliff - south-facing stone that absorbs solar heat.
    /// Conditional warmth based on time of day and weather. Ptarmigan habitat.
    /// </summary>
    public static Location MakeSunWarmedCliff(Weather weather)
    {
        var location = new Location(
            name: "Sun-Warmed Cliff",
            tags: "[Stone] [Warm] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.30,
            windFactor: 0.5,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "South-facing stone, dark and bare. The rock drinks sunlight and holds it. In the midday glare, the cliff radiates warmth — you can feel it from ten paces.",
            TemperatureDeltaF = 5,  // Thermal mass warmth (sun adds up to +10 via existing system)
            FirstVisitEvent = GameEventRegistry.FirstVisitSunWarmedCliff
        };

        // Climb edges on all sides - scrambling on cliff face
        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        // Sparse foraging - stone and bone from animals sheltering here
        var forage = new ForageFeature(0.6)
            .AddStone(0.8, 0.2, 0.4)
            .AddBone(0.2, 0.1, 0.3)
            .AddTinder(0.3, 0.02, 0.05);
        location.Features.Add(forage);

        // Ptarmigan-focused hunting - birds sun themselves on warm rocks
        var animals = new AnimalTerritoryFeature(0.7)
            .AddPtarmigan(1.5)
            .AddRabbit(1.0)
            .AddFox(0.4)
            .WithPeakHours(6, 9, 1.8);
        location.Features.Add(animals);

        // Quality stone slabs for tool-making
        var cliffStone = new HarvestableFeature("cliff_stone", "Cliff Face Stone")
        {
            Description = "Flat slabs of quality stone, fractured by freeze-thaw. Good for tools.",
            MinutesToHarvest = 15
        };
        cliffStone.AddResource("stone slabs", Resource.Stone, maxQuantity: 6, weightPerUnit: 1.5, respawnHoursPerUnit: 0);
        location.Features.Add(cliffStone);

        return location;
    }

    /// <summary>
    /// Stone Scatter - common rocky terrain with reliable stone access.
    /// Easy traversal, exposed to wind, but good visibility. The "what's nearby" stone source.
    /// </summary>
    public static Location MakeStoneScatter(Weather weather)
    {
        var location = new Location(
            name: "Stone Scatter",
            tags: "[Rocky] [Open]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            DiscoveryText = "Loose stones litter the ground, tumbled from some ancient rockfall. Dry grass grows between the gaps."
        };

        // Stone-focused foraging - the main draw
        var forage = new ForageFeature(0.8)
            .AddStone(1.2, 0.15, 0.4)      // Primary resource
            .AddTinder(0.3, 0.02, 0.05)    // Dry grass between rocks
            .AddFlint(0.1, 0.05, 0.15);    // Occasional better finds
        location.Features.Add(forage);

        // 25% chance for small game - rocks provide cover
        if (Utils.RandDouble(0, 1) < 0.25)
        {
            var animals = new AnimalTerritoryFeature(0.5)
                .AddRabbit(1.0)
                .AddPtarmigan(0.6);
            location.Features.Add(animals);
        }

        // 15% chance for meltwater puddle
        if (Utils.RandDouble(0, 1) < 0.15)
        {
            var puddle = new WaterFeature("meltwater", "Meltwater Puddle")
                .AsOpenWater()
                .WithDescription("A shallow puddle of snowmelt collected in a rock hollow.");
            location.Features.Add(puddle);
        }

        return location;
    }

    // === NEW FOREST LOCATIONS ===

    /// <summary>
    /// Fallen Giant - massive downed tree providing shelter and abundant fuel.
    /// </summary>
    public static Location MakeFallenGiant(Weather weather)
    {
        var location = new Location(
            name: "Fallen Giant",
            tags: "[Forest] [Fuel] [Shelter]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.4,
            overheadCoverLevel: 0.5,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "A massive tree, ancient and fallen. Its trunk creates a natural windbreak, branches reaching skyward like ribs."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 1.2));
        location.Features.Add(FeatureFactory.CreateMassiveDeadfall());
        location.Features.Add(ShelterFeature.CreateFallenTree());
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.6));

        return location;
    }

    /// <summary>
    /// Hollow Oak - standing hollow tree providing natural shelter.
    /// </summary>
    public static Location MakeHollowOak(Weather weather)
    {
        var location = new Location(
            name: "Hollow Oak",
            tags: "[Forest] [Shelter] [Dark]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.3,
            overheadCoverLevel: 0.8,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "A gnarled oak, hollow at the base. The cavity is large enough to crouch in, protected from wind and snow."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.8));
        location.Features.Add(new ShelterFeature("Tree Hollow", 0.35, 0.8, 0.7));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.5));
        location.Features.Add(new WoodedAreaFeature("Old Oak", Resource.Oak, 60));

        return location;
    }

    /// <summary>
    /// Fungal Grove - rotting logs rich with medicinal mushrooms.
    /// </summary>
    public static Location MakeFungalGrove(Weather weather)
    {
        var location = new Location(
            name: "Fungal Grove",
            tags: "[Forest] [Medicine] [Damp]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.5,
            overheadCoverLevel: 0.6,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "Rotting logs carpeted in shelf fungi and strange growths. The air smells of decay and earth. Medicinal, if you know what to look for."
        };

        // Enhanced mushroom foraging
        var forage = new ForageFeature(1.5)
            .AddTinder(0.3, 0.02, 0.05)
            .AddAmadou(0.25, 0.03, 0.08)
            .AddChaga(0.2, 0.02, 0.06)
            .AddBirchPolypore(0.2, 0.02, 0.06);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateMixedDeadfall());
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.4));

        return location;
    }

    /// <summary>
    /// Birch Stand - grove of birch trees with excellent bark for tinder.
    /// </summary>
    public static Location MakeBirchStand(Weather weather)
    {
        var location = new Location(
            name: "Birch Stand",
            tags: "[Forest] [Tinder] [Bright]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.7,
            overheadCoverLevel: 0.4,
            visibilityFactor: 0.9)
        {
            DiscoveryText = "White bark gleams through the forest. A stand of birch, their papery bark curling in sheets. Tinder everywhere."
        };

        // Birch-focused foraging
        var forage = new ForageFeature(1.3)
            .AddBirchBark(0.8, 0.08, 0.2)
            .AddSticks(0.6, 0.1, 0.3)
            .AddTinder(0.4, 0.03, 0.08);
        location.Features.Add(forage);

        location.Features.Add(new WoodedAreaFeature("Birch Stand", Resource.Birch, 120));
        location.Features.Add(FeatureFactory.CreateMixedPreyAnimals(density: 0.7));

        return location;
    }

    /// <summary>
    /// Mossy Hollow - damp sheltered depression in the forest.
    /// </summary>
    public static Location MakeMossyHollow(Weather weather)
    {
        var location = new Location(
            name: "Mossy Hollow",
            tags: "[Forest] [Sheltered] [Damp]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.3,
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "The ground dips into a natural hollow, carpeted in thick moss. Sheltered from wind, though perpetually damp."
        };

        var forage = new ForageFeature(1.0)
            .AddPlantFiber(0.5, 0.05, 0.12)
            .AddTinder(0.3, 0.02, 0.05)
            .AddSticks(0.4, 0.08, 0.2);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.6));
        location.Features.Add(new WoodedAreaFeature("Forest Trees", null, 80));

        return location;
    }

    /// <summary>
    /// Tangled Roots - upturned root ball creating escape terrain.
    /// </summary>
    public static Location MakeTangledRoots(Weather weather)
    {
        var location = new Location(
            name: "Tangled Roots",
            tags: "[Forest] [Escape] [Difficult]",
            weather: weather,
            terrainHazardLevel: 0.35,
            windFactor: 0.5,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.3)
        {
            Terrain = TerrainType.Forest,
            TraversalModifier = 1.3,
            DiscoveryText = "A massive tree torn from the ground, roots reaching like grasping fingers. The tangled mass creates a maze only you can navigate.",
            IsEscapeTerrain = true
        };

        var forage = new ForageFeature(0.8)
            .AddSticks(0.5, 0.1, 0.25)
            .AddPlantFiber(0.3, 0.04, 0.1)
            .AddBone(0.1, 0.05, 0.15);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.8));

        return location;
    }

    // === NEW PLAINS/CLEARING LOCATIONS ===

    /// <summary>
    /// Salt Lick - mineral deposit attracting prey animals.
    /// </summary>
    public static Location MakeSaltLick(Weather weather)
    {
        var location = new Location(
            name: "Salt Lick",
            tags: "[Plain] [Hunting] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.4)
        {
            DiscoveryText = "A bare patch where animals have licked the earth white. Tracks converge from every direction. They come here regularly."
        };

        location.Features.Add(FeatureFactory.CreateOpenForage(density: 0.4));

        // High animal density - the draw of this location
        var animals = new AnimalTerritoryFeature(1.6)
            .AddMegaloceros(1.5)
            .AddCaribou(1.2)
            .AddRabbit(0.8)
            .WithPeakHours(5, 8, 2.0);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Tall Grass - prairie grass providing hiding cover.
    /// </summary>
    public static Location MakeTallGrass(Weather weather)
    {
        var location = new Location(
            name: "Tall Grass",
            tags: "[Plain] [Cover] [Hunting]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.8,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "Grass taller than your waist, swaying in the wind. Small things rustle unseen. Good cover for stalking — or being stalked."
        };

        var forage = new ForageFeature(0.9)
            .AddPlantFiber(0.7, 0.08, 0.18)
            .AddTinder(0.4, 0.03, 0.08);
        location.Features.Add(forage);

        var animals = new AnimalTerritoryFeature(1.2)
            .AddRabbit(1.5)
            .AddPtarmigan(1.2)
            .AddFox(0.5);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Standing Stones - mysterious markers providing wind shelter.
    /// </summary>
    public static Location MakeStandingStones(Weather weather)
    {
        var location = new Location(
            name: "Standing Stones",
            tags: "[Plain] [Shelter] [Ancient]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.5,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "Stones stand upright in a rough circle, placed by hands long gone. The wind breaks around them. Someone worshipped here once."
        };

        var forage = new ForageFeature(0.5)
            .AddStone(0.4, 0.1, 0.25)
            .AddBone(0.15, 0.05, 0.15);
        location.Features.Add(forage);

        // Partial shelter from stones
        location.Features.Add(new ShelterFeature("Stone Circle", 0.15, 0.0, 0.5));

        return location;
    }

    /// <summary>
    /// Herd Crossing - migration path with high animal density.
    /// </summary>
    public static Location MakeHerdCrossing(Weather weather)
    {
        var location = new Location(
            name: "Herd Crossing",
            tags: "[Plain] [Hunting] [Trail]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            TraversalModifier = 0.8,
            DiscoveryText = "The ground is beaten flat by countless hooves. A migration path — herds pass through here on their seasonal journey."
        };

        location.Features.Add(FeatureFactory.CreateOpenForage(density: 0.5));

        // Exceptional hunting - migration route
        var animals = new AnimalTerritoryFeature(1.8)
            .AddCaribou(2.0)
            .AddMegaloceros(1.5)
            .AddBison(0.8)
            .WithPeakHours(6, 10, 1.8);
        location.Features.Add(animals);

        return location;
    }

    // === NEW ROCKY/HILLS LOCATIONS ===

    /// <summary>
    /// Talus Slope - jumbled rock debris, treacherous but stone-rich.
    /// </summary>
    public static Location MakeTalusSlope(Weather weather)
    {
        var location = new Location(
            name: "Talus Slope",
            tags: "[Rock] [Stone] [Treacherous]",
            weather: weather,
            terrainHazardLevel: 0.55,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.0)
        {
            TraversalModifier = 1.4,
            DiscoveryText = "Loose rock cascades down the slope. Every step threatens to start a slide. But the stone here is good — fractured and ready."
        };

        var forage = new ForageFeature(1.2)
            .AddStone(1.5, 0.2, 0.5)
            .AddFlint(0.2, 0.05, 0.15);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Split Rock - cracked boulder creating natural shelter.
    /// </summary>
    public static Location MakeSplitRock(Weather weather)
    {
        var location = new Location(
            name: "Split Rock",
            tags: "[Rock] [Shelter] [Landmark]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.4,
            overheadCoverLevel: 0.6,
            visibilityFactor: 0.8)
        {
            DiscoveryText = "A massive boulder, cleaved in two by ancient forces. The gap between is just wide enough to shelter in, dry and out of the wind."
        };

        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.6));
        location.Features.Add(new ShelterFeature("Rock Cleft", 0.3, 0.6, 0.7));

        return location;
    }

    /// <summary>
    /// Fossil Bed - exposed ancient bones for crafting materials.
    /// </summary>
    public static Location MakeFossilBed(Weather weather)
    {
        var location = new Location(
            name: "Fossil Bed",
            tags: "[Rock] [Bones] [Ancient]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.8,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)
        {
            DiscoveryText = "Ancient bones jut from the eroded hillside. Not mammoth — older. Strange shapes, but the bone is still good."
        };

        var forage = new ForageFeature(0.9)
            .AddBone(1.5, 0.2, 0.5)
            .AddStone(0.4, 0.1, 0.25);
        location.Features.Add(forage);

        // Harvestable fossil bones
        var fossils = new HarvestableFeature("fossil_bones", "Fossil Bones")
        {
            Description = "Ancient bones eroding from stone. Dense and well-preserved.",
            MinutesToHarvest = 15
        };
        fossils.AddResource("fossil bone", Resource.Bone, maxQuantity: 10, weightPerUnit: 1.2, respawnHoursPerUnit: 0);
        location.Features.Add(fossils);

        return location;
    }

    /// <summary>
    /// Shale Outcrop - layered stone good for tools.
    /// </summary>
    public static Location MakeShaleOutcrop(Weather weather)
    {
        var location = new Location(
            name: "Shale Outcrop",
            tags: "[Rock] [Stone] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            DiscoveryText = "Layered stone, splitting into thin sheets. The edges are sharp where frost has cleaved them. Good for scraping tools."
        };

        var forage = new ForageFeature(1.1)
            .AddShale(1.2, 0.15, 0.4)
            .AddStone(0.5, 0.1, 0.25);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Chimney Rock - landmark spire with vantage point.
    /// </summary>
    public static Location MakeChimneyRock(Weather weather)
    {
        var location = new Location(
            name: "Chimney Rock",
            tags: "[Rock] [Vantage] [Landmark]",
            weather: weather,
            terrainHazardLevel: 0.4,
            windFactor: 1.3,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.8)
        {
            DiscoveryText = "A lone spire of stone rises from the landscape. Visible for miles in every direction. From the top, you could see anything approaching.",
            IsVantagePoint = true
        };

        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);
        location.Features.Add(FeatureFactory.CreateRockyForage(density: 0.4));

        return location;
    }

    // === NEW WATER/MARSH LOCATIONS ===

    /// <summary>
    /// Spring Seep - unfrozen water bubbling from the ground.
    /// </summary>
    public static Location MakeSpringSeep(Weather weather)
    {
        var location = new Location(
            name: "Spring Seep",
            tags: "[Water] [Warm] [Sheltered]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.6,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.9)
        {
            DiscoveryText = "Water bubbles from the ground, too warm to freeze. A small pool collects before it soaks away. Animals come here to drink."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 0.8));

        var waterFeature = new WaterFeature("spring_water", "Spring")
            .AsOpenWater()
            .WithDescription("Clear water seeping from underground. Slightly warm, never freezes.");
        location.Features.Add(waterFeature);

        var animals = new AnimalTerritoryFeature(1.0)
            .AddMegaloceros(1.0)
            .AddRabbit(0.8)
            .WithPeakHours(5, 8, 1.5);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Reed Bed - dense reeds providing fiber and waterfowl habitat.
    /// </summary>
    public static Location MakeReedBed(Weather weather)
    {
        var location = new Location(
            name: "Reed Bed",
            tags: "[Marsh] [Fiber] [Hunting]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 0.5,
            overheadCoverLevel: 0.2,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "Dense reeds crowd the shallows, rattling in the wind. Ducks burst from cover as you approach. Good fiber, if you can reach it."
        };

        var forage = new ForageFeature(1.4)
            .AddPlantFiber(1.2, 0.1, 0.25)
            .AddTinder(0.3, 0.02, 0.05);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateCattails());
        location.Features.Add(FeatureFactory.CreateWaterfowlAnimals(density: 1.3));

        var waterFeature = new WaterFeature("reed_water", "Reed Shallows")
            .WithDescription("Shallow water between the reed stalks. Thin ice.")
            .WithIceThickness(0.2);
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Fish Run - shallow rapids where fish can be caught.
    /// </summary>
    public static Location MakeFishRun(Weather weather)
    {
        var location = new Location(
            name: "Fish Run",
            tags: "[Water] [Fishing] [Open]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)
        {
            DiscoveryText = "The stream narrows over rocks, forcing fish through a bottleneck. Dark shapes flash in the current. You could spear them here."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 0.6));
        location.Features.Add(FeatureFactory.CreateRiver());

        var waterFeature = new WaterFeature("rapids_water", "Rapids")
            .WithDescription("Fast-moving water over rocks. Too turbulent to freeze solid.")
            .WithIceThickness(0.1);
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Cranberry Bog - tart berries in treacherous footing.
    /// </summary>
    public static Location MakeCranberryBog(Weather weather)
    {
        var location = new Location(
            name: "Cranberry Bog",
            tags: "[Marsh] [Food] [Treacherous]",
            weather: weather,
            terrainHazardLevel: 0.45,
            windFactor: 0.6,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.7)
        {
            TraversalModifier = 1.3,
            DiscoveryText = "Red berries dot the spongy ground, frozen but still good. The footing is treacherous — one wrong step and you're through."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 1.0));
        location.Features.Add(FeatureFactory.CreateBerryBush());
        location.Features.Add(FeatureFactory.CreateWaterfowlAnimals(density: 0.6));

        var waterFeature = new WaterFeature("bog_pools", "Bog Pools")
            .WithDescription("Dark water hidden beneath thin ice and moss.")
            .AsThinIce();
        location.Features.Add(waterFeature);

        return location;
    }

    // === NEW ANIMAL-FOCUSED LOCATIONS ===

    /// <summary>
    /// Raven's Perch - scavenger birds indicating nearby food.
    /// </summary>
    public static Location MakeRavensPerch(Weather weather)
    {
        var location = new Location(
            name: "Raven's Perch",
            tags: "[Forest] [Scavenger] [Vantage]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.7,
            overheadCoverLevel: 0.4,
            visibilityFactor: 1.0)
        {
            DiscoveryText = "Ravens circle overhead, cawing. They've found something. Where there are ravens, there's often a kill nearby."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.9));
        location.Features.Add(new WoodedAreaFeature("Perch Trees", null, 60));

        // Bones from scavenged kills
        var forage = new ForageFeature(0.8)
            .AddBone(0.4, 0.1, 0.3);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Fox Earth - small predator den with fur opportunity.
    /// </summary>
    public static Location MakeFoxEarth(Weather weather)
    {
        var location = new Location(
            name: "Fox Earth",
            tags: "[Forest] [Hunting] [Den]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.5,
            overheadCoverLevel: 0.5,
            visibilityFactor: 0.6)
        {
            DiscoveryText = "A burrow entrance, well-used. The musky smell of fox. Small bones scattered around the opening. Valuable fur, if you're patient."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.7));

        var animals = new AnimalTerritoryFeature(0.9)
            .AddFox(2.0)
            .AddRabbit(0.6)
            .WithPeakHours(4, 7, 1.8);
        location.Features.Add(animals);

        var forage = new ForageFeature(0.6)
            .AddBone(0.3, 0.05, 0.15);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Owl Hollow - night hunter roost with bone pellets.
    /// </summary>
    public static Location MakeOwlHollow(Weather weather)
    {
        var location = new Location(
            name: "Owl Hollow",
            tags: "[Forest] [Dark] [Bones]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.4,
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.4)
        {
            DiscoveryText = "A hollow in a dead tree, streaked white. Pellets of compressed bone and fur litter the ground below. An owl's roost."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.6));
        location.Features.Add(new WoodedAreaFeature("Dead Snag", null, 40));

        // Owl pellets provide small bones
        var forage = new ForageFeature(0.7)
            .AddBone(0.6, 0.08, 0.2);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.5));

        return location;
    }

    /// <summary>
    /// Eagle's Crag - high nest with feathers and vantage.
    /// </summary>
    public static Location MakeEaglesCrag(Weather weather)
    {
        var location = new Location(
            name: "Eagle's Crag",
            tags: "[Rock] [Vantage] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.45,
            windFactor: 1.4,
            overheadCoverLevel: 0.0,
            visibilityFactor: 2.0)
        {
            DiscoveryText = "A massive nest crowns the cliff, built of sticks and bone. The eagle is gone, but feathers and remains litter the rock. The view is commanding.",
            IsVantagePoint = true
        };

        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        var forage = new ForageFeature(0.5)
            .AddBone(0.5, 0.1, 0.3)
            .AddStone(0.3, 0.08, 0.2);
        location.Features.Add(forage);

        return location;
    }

    // === LOCATIONS FROM DESIGN DOC ===

    /// <summary>
    /// Creek Falls - loud waterfall with fish opportunity. Sound covers approach.
    /// </summary>
    public static Location MakeCreekFalls(Weather weather)
    {
        var location = new Location(
            name: "Creek Falls",
            tags: "[Water] [Fishing] [Loud]",
            weather: weather,
            terrainHazardLevel: 0.45,
            windFactor: 0.7,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.8)
        {
            DiscoveryText = "Water roars over a rocky drop. The spray freezes on the stones. Fish leap in the pool below — and the noise covers your approach."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 0.7));
        location.Features.Add(FeatureFactory.CreateRiver());

        var waterFeature = new WaterFeature("falls_pool", "Falls Pool")
            .WithDescription("A churning pool at the base of the falls. Open water even in deep cold.")
            .AsOpenWater();
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Open Pines - fast travel, high visibility, good hunting sightlines.
    /// </summary>
    public static Location MakeOpenPines(Weather weather)
    {
        var location = new Location(
            name: "Open Pines",
            tags: "[Forest] [Open] [Fast]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.8,
            overheadCoverLevel: 0.5,
            visibilityFactor: 1.2)
        {
            TraversalModifier = 0.7,
            DiscoveryText = "Tall pines, widely spaced. The ground is clear of brush — you can move fast here, and see far. So can they."
        };

        var forage = new ForageFeature(0.9)
            .AddSticks(0.5, 0.1, 0.3)
            .AddPineNeedles(0.4)
            .AddPineResin(0.15)
            .AddTinder(0.3, 0.02, 0.06);
        location.Features.Add(forage);

        location.Features.Add(new WoodedAreaFeature("Pine Stand", Resource.Pine, 140));

        var animals = new AnimalTerritoryFeature(1.1)
            .AddCaribou(1.2)
            .AddMegaloceros(1.0)
            .AddWolf(0.4);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Young Growth - very dense regrowth, escape terrain, limited resources.
    /// </summary>
    public static Location MakeYoungGrowth(Weather weather)
    {
        var location = new Location(
            name: "Young Growth",
            tags: "[Forest] [Dense] [Escape]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.3,
            overheadCoverLevel: 0.6,
            visibilityFactor: 0.2)
        {
            Terrain = TerrainType.Forest,
            TraversalModifier = 1.4,
            DiscoveryText = "Dense saplings crowd together, competing for light. You can barely push through — but nothing large can follow.",
            IsEscapeTerrain = true
        };

        var forage = new ForageFeature(0.5)
            .AddSticks(0.3, 0.05, 0.15)
            .AddPlantFiber(0.3, 0.03, 0.08);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.9));

        return location;
    }

    /// <summary>
    /// Cliff Face - steep barrier with bird eggs and vantage point.
    /// </summary>
    public static Location MakeCliffFace(Weather weather)
    {
        var location = new Location(
            name: "Cliff Face",
            tags: "[Rock] [Vantage] [Climb]",
            weather: weather,
            terrainHazardLevel: 0.6,
            windFactor: 1.2,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.6)
        {
            DiscoveryText = "Sheer stone rises before you. Bird nests dot the ledges above. Climbable, if you're careful — and the view would be worth it.",
            IsVantagePoint = true
        };

        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        var forage = new ForageFeature(0.4)
            .AddStone(0.5, 0.15, 0.35)
            .AddBone(0.2, 0.05, 0.15);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Root Hollow - hidden shelter beneath upturned roots.
    /// </summary>
    public static Location MakeRootHollow(Weather weather)
    {
        var location = new Location(
            name: "Root Hollow",
            tags: "[Forest] [Shelter] [Hidden]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.2,
            overheadCoverLevel: 0.8,
            visibilityFactor: 0.2)
        {
            DiscoveryText = "A massive root ball, torn from the earth. The hollow beneath is dry and sheltered — barely room for one, but enough."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.6));
        location.Features.Add(new ShelterFeature("Root Hollow", 0.4, 0.8, 0.7));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.5));

        return location;
    }

    /// <summary>
    /// Deer Meadow - open grazing area with high animal activity at dawn/dusk.
    /// </summary>
    public static Location MakeDeerMeadow(Weather weather)
    {
        var location = new Location(
            name: "Deer Meadow",
            tags: "[Plain] [Hunting] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5)
        {
            DiscoveryText = "A broad meadow, grass poking through snow. Tracks everywhere — they graze here. But you'll be seen from a long way off."
        };

        var forage = new ForageFeature(0.6)
            .AddPlantFiber(0.5, 0.05, 0.12)
            .AddBone(0.1, 0.05, 0.15);
        location.Features.Add(forage);

        // High animal density with dawn/dusk peak
        var animals = new AnimalTerritoryFeature(1.5)
            .AddMegaloceros(1.8)
            .AddCaribou(1.5)
            .AddRabbit(0.6)
            .WithPeakHours(5, 8, 2.2);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Rabbit Warren - reliable small game hunting ground.
    /// </summary>
    public static Location MakeRabbitWarren(Weather weather)
    {
        var location = new Location(
            name: "Rabbit Warren",
            tags: "[Forest] [Hunting] [Small Game]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.6,
            overheadCoverLevel: 0.5,
            visibilityFactor: 0.6)
        {
            DiscoveryText = "Burrow entrances dot the slope, earth worn smooth by generations of rabbits. They're here — you just have to be patient."
        };

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.7));

        // Exceptional small game density
        var animals = new AnimalTerritoryFeature(1.3)
            .AddRabbit(2.5)
            .AddFox(0.6)
            .WithPeakHours(5, 7, 1.8);
        location.Features.Add(animals);

        return location;
    }

    // === BATCH 3: READY-NOW LOCATIONS ===

    /// <summary>
    /// Ice Shove Ridge - pressure ridges create shelter pockets, escape terrain.
    /// </summary>
    public static Location MakeIceShoveRidge(Weather weather)
    {
        var location = new Location(
            name: "Ice Shove Ridge",
            tags: "[Ice] [Shelter] [Escape]",
            weather: weather,
            terrainHazardLevel: 0.6,
            windFactor: 0.3,
            overheadCoverLevel: 0.4,
            visibilityFactor: 0.6)
        {
            TraversalModifier = 1.5,
            DiscoveryText = "Ice plates driven up into jagged ridges by wind and current. Treacherous to navigate, but sheltered pockets between the slabs.",
            IsEscapeTerrain = true
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.2));

        var waterFeature = new WaterFeature("pressure_ice", "Pressure Ice")
            .WithDescription("Jumbled ice plates. Hard to reach open water, but harvestable ice everywhere.")
            .AsSolidIce();
        location.Features.Add(waterFeature);

        location.Features.Add(new ShelterFeature("Ice Pocket", 0.25, 0.4, 0.6));

        return location;
    }

    /// <summary>
    /// Overflow Ice - smooth ice with water welling up, fast but soaks you.
    /// </summary>
    public static Location MakeOverflowIce(Weather weather)
    {
        var location = new Location(
            name: "Overflow Ice",
            tags: "[Ice] [Fast] [Wet]",
            weather: weather,
            terrainHazardLevel: 0.7,
            windFactor: 1.1,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5)
        {
            TraversalModifier = 0.6,
            DiscoveryText = "Mirror-smooth ice, water welling up through cracks and spreading in sheets. Fast travel — but your feet are soaked before you know it."
        };

        var waterFeature = new WaterFeature("overflow_water", "Overflow")
            .AsOpenWater()
            .WithDescription("Water bubbling up through ice cracks. Standing in it.");
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Mineral Spring - iron-rich medicinal water that attracts animals.
    /// </summary>
    public static Location MakeMineralSpring(Weather weather)
    {
        var location = new Location(
            name: "Mineral Spring",
            tags: "[Water] [Medicine] [Hunting]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.6,
            overheadCoverLevel: 0.1,
            visibilityFactor: 0.9)
        {
            TemperatureDeltaF = 3,
            DiscoveryText = "Water stained rust-red, smelling of iron. Warm from underground. Animals come for the minerals — and so do predators."
        };

        location.Features.Add(FeatureFactory.CreateWetlandForage(density: 0.6));

        var waterFeature = new WaterFeature("mineral_water", "Mineral Spring")
            .AsOpenWater()
            .WithDescription("Iron-rich water. Medicinal, if you can stomach the taste.");
        location.Features.Add(waterFeature);

        var animals = new AnimalTerritoryFeature(1.3)
            .AddCaribou(1.2)
            .AddMegaloceros(1.0)
            .AddWolf(0.4)
            .WithPeakHours(5, 8, 1.8);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Sinkhole Pool - deep dark water, fishing opportunity, dangerous approach.
    /// </summary>
    public static Location MakeSinkholePool(Weather weather)
    {
        var location = new Location(
            name: "Sinkhole Pool",
            tags: "[Water] [Dark] [Dangerous]",
            weather: weather,
            terrainHazardLevel: 0.55,
            windFactor: 0.4,
            overheadCoverLevel: 0.2,
            visibilityFactor: 0.5)
        {
            DiscoveryText = "A perfect circle of dark water where the ground collapsed. Underground warmth keeps it open. Fish move in the depths — if you can reach them safely."
        };

        location.WithEdgesOnAllSides(Grid.EdgeType.Climb);

        var waterFeature = new WaterFeature("sinkhole_water", "Sinkhole")
            .AsOpenWater()
            .WithDescription("Deep, dark water. Never freezes. Rumored to have fish.");
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Snowfield Basin - deep snow, exhausting but shows all tracks.
    /// </summary>
    public static Location MakeSnowfieldBasin(Weather weather)
    {
        var location = new Location(
            name: "Snowfield Basin",
            tags: "[Snow] [Exposed] [Tracking]",
            weather: weather,
            terrainHazardLevel: 0.15,
            windFactor: 0.7,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.4)
        {
            TraversalModifier = 2.0,
            DiscoveryText = "A high bowl filled with deep snow. Post-holing through it exhausts you — but every track is preserved. Nothing moves through here unseen."
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.1));

        return location;
    }

    /// <summary>
    /// Moraine Field - glacier debris with best stone but terrible footing.
    /// </summary>
    public static Location MakeMoraineField(Weather weather)
    {
        var location = new Location(
            name: "Moraine Field",
            tags: "[Rock] [Stone] [Treacherous]",
            weather: weather,
            terrainHazardLevel: 0.6,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            TraversalModifier = 1.4,
            DiscoveryText = "Where the glacier dumped its load — boulders, gravel, sand jumbled together. The stone variety is excellent. The footing is murderous.",
            IsEscapeTerrain = true
        };

        var forage = new ForageFeature(1.5)
            .AddStone(2.0, 0.2, 0.6)
            .AddFlint(0.3, 0.08, 0.2);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Scree Chute - fast downhill, slow uphill, announces your presence.
    /// </summary>
    public static Location MakeScreeChute(Weather weather)
    {
        var location = new Location(
            name: "Scree Chute",
            tags: "[Rock] [Slope] [Loud]",
            weather: weather,
            terrainHazardLevel: 0.45,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)
        {
            TraversalModifier = 1.2,  // Average - faster down, slower up
            DiscoveryText = "Loose rock in a steep channel. Going down is fast — going up is exhausting. Every step sends stones clattering."
        };

        var forage = new ForageFeature(0.8)
            .AddStone(1.0, 0.15, 0.4)
            .AddShale(0.5, 0.1, 0.3);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Krummholz Zone - stunted wind-twisted trees, abundant fuel, very slow.
    /// </summary>
    public static Location MakeKrummholzZone(Weather weather)
    {
        var location = new Location(
            name: "Krummholz Zone",
            tags: "[Forest] [Fuel] [Slow]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 0.4,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.4)
        {
            TraversalModifier = 1.8,
            DiscoveryText = "Stunted, wind-twisted trees barely knee-high. You're crawling through them — but there's firewood everywhere, and ptarmigan love it here."
        };

        var forage = new ForageFeature(2.0)
            .AddSticks(1.5, 0.15, 0.4)
            .AddTinder(0.5, 0.03, 0.08);
        location.Features.Add(forage);

        var animals = new AnimalTerritoryFeature(1.0)
            .AddPtarmigan(2.0)
            .AddRabbit(0.8);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Flint Knapping Site - ancient workshop with premium flint.
    /// </summary>
    public static Location MakeFlintKnappingSite(Weather weather)
    {
        var location = new Location(
            name: "Flint Knapping Site",
            tags: "[Rock] [Flint] [Ancient]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "The ground glitters with flakes and failed attempts. Someone stood here making tools, long ago. The flint is premium quality."
        };

        var forage = new ForageFeature(2.0)
            .AddFlint(2.0, 0.1, 0.3)
            .AddStone(0.4, 0.1, 0.25);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Kill Site - mass bone pile from ancient hunt, draws scavengers.
    /// </summary>
    public static Location MakeKillSite(Weather weather)
    {
        var location = new Location(
            name: "Kill Site",
            tags: "[Bones] [Hunting] [Scavengers]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.8,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)
        {
            DiscoveryText = "Bones piled where many animals died. A stampede? A coordinated hunt? The bones are old but usable — and scavengers remember this place."
        };

        var forage = new ForageFeature(1.5)
            .AddBone(2.5, 0.3, 0.8);
        location.Features.Add(forage);

        // Harvestable bone pile
        var boneHeap = new HarvestableFeature("bone_heap", "Bone Heap")
        {
            Description = "Jumbled bones from an ancient kill. Dense, quality material.",
            MinutesToHarvest = 20
        };
        boneHeap.AddResource("quality bone", Resource.Bone, maxQuantity: 15, weightPerUnit: 1.0, respawnHoursPerUnit: 0);
        location.Features.Add(boneHeap);

        var animals = new AnimalTerritoryFeature(0.8)
            .AddFox(1.5)
            .AddWolf(0.6);
        location.Features.Add(animals);

        return location;
    }

    /// <summary>
    /// Rock Shelter - painted cave with established fire pit.
    /// </summary>
    public static Location MakeRockShelter(Weather weather)
    {
        var location = new Location(
            name: "Rock Shelter",
            tags: "[Shelter] [Ancient] [Fire]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 0.2,
            overheadCoverLevel: 0.9,
            visibilityFactor: 0.3)
        {
            DiscoveryText = "Painted walls in the firelight. Someone lived here, seasons beyond counting. Their handprints are still on the stone. A fire pit is already dug."
        };

        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.3));
        location.Features.Add(new ShelterFeature("Rock Shelter", 0.5, 0.9, 0.8));
        location.Features.Add(SalvageFeature.CreateAbandonedCamp());

        return location;
    }

    /// <summary>
    /// Cairn Marker - stone stack revealing nearby locations.
    /// </summary>
    public static Location MakeCairnMarker(Weather weather)
    {
        var location = new Location(
            name: "Cairn Marker",
            tags: "[Landmark] [Exposed] [Ancient]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 1.2,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.6)
        {
            DiscoveryText = "Stacked stones on a rise, placed by hands long gone. A message you can't read — but it marks something. The view from here reveals the land.",
            IsVantagePoint = true
        };

        var forage = new ForageFeature(0.4)
            .AddStone(0.5, 0.1, 0.25);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Mammoth Wallow - where mammoths roll, churned mud, dung fuel.
    /// </summary>
    public static Location MakeMammothWallow(Weather weather)
    {
        var location = new Location(
            name: "Mammoth Wallow",
            tags: "[Plain] [Megafauna] [Messy]",
            weather: weather,
            terrainHazardLevel: 0.4,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            TraversalModifier = 1.3,
            DiscoveryText = "Churned mud and ice where something massive rolls to shed parasites. Fresh dung steams in the cold. They come here regularly."
        };

        // Dung as alternative fuel
        var forage = new ForageFeature(1.0)
            .AddTinder(0.8, 0.1, 0.3);  // Dried dung burns
        location.Features.Add(forage);

        location.Features.Add(new MegafaunaPresenceFeature
        {
            MegafaunaType = AnimalType.Mammoth,
            ActivityLevel = 0.9,
            RespawnHours = 480
        });

        return location;
    }

    /// <summary>
    /// Pyrite Outcrop - fire-starting material in exposed location.
    /// </summary>
    public static Location MakePyriteOutcrop(Weather weather)
    {
        var location = new Location(
            name: "Pyrite Outcrop",
            tags: "[Rock] [Fire] [Exposed]",
            weather: weather,
            terrainHazardLevel: 0.25,
            windFactor: 1.1,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "Fool's gold glitters in the rock face. Worthless for trade — but strike it against flint and you get sparks."
        };

        var forage = new ForageFeature(1.2)
            .AddPyrite(1.5, 0.05, 0.15)
            .AddStone(0.5, 0.1, 0.25);
        location.Features.Add(forage);

        return location;
    }

    /// <summary>
    /// Glacial Tongue - ancient ice with preserved materials, extreme cold.
    /// </summary>
    public static Location MakeGlacialTongue(Weather weather)
    {
        var location = new Location(
            name: "Glacial Tongue",
            tags: "[Ice] [Ancient] [Cold]",
            weather: weather,
            terrainHazardLevel: 0.5,
            windFactor: 1.0,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.0)
        {
            TemperatureDeltaF = -8,
            DiscoveryText = "Ancient ice pushing into the valley, moving inches per year. Old things emerge from its face — bone, ivory, frozen flesh from creatures long dead."
        };

        // Ancient materials melting out
        var ancientMaterials = new HarvestableFeature("glacial_melt", "Glacial Melt Zone")
        {
            Description = "The edge where ice meets air. Ancient materials emerge as the glacier melts.",
            MinutesToHarvest = 25
        };
        ancientMaterials.AddResource("ancient bone", Resource.Bone, maxQuantity: 8, weightPerUnit: 1.5, respawnHoursPerUnit: 0);
        ancientMaterials.AddResource("ancient ivory", Resource.Ivory, maxQuantity: 4, weightPerUnit: 2.0, respawnHoursPerUnit: 0);
        location.Features.Add(ancientMaterials);

        var waterFeature = new WaterFeature("glacier_ice", "Glacier Ice")
            .AsSolidIce()
            .WithDescription("Pure, ancient ice. Dense and cold.");
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Deadfall Maze - massive fuel source, very slow, escape terrain.
    /// </summary>
    public static Location MakeDeadfallMaze(Weather weather)
    {
        var location = new Location(
            name: "Deadfall Maze",
            tags: "[Forest] [Fuel] [Escape]",
            weather: weather,
            terrainHazardLevel: 0.55,
            windFactor: 0.5,
            overheadCoverLevel: 0.4,
            visibilityFactor: 0.3)
        {
            TraversalModifier = 1.6,
            DiscoveryText = "Forest flattened by some ancient storm. Logs stacked chaotically in every direction. Fuel everywhere — but nothing large can follow you through.",
            IsEscapeTerrain = true
        };

        var forage = new ForageFeature(3.0)
            .AddSticks(2.0, 0.2, 0.5)
            .AddMixedWood(1.5, 1.5, 4.0);
        location.Features.Add(forage);

        location.Features.Add(FeatureFactory.CreateMassiveDeadfall());
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 1.2));

        return location;
    }

    /// <summary>
    /// Smoke Tree - lightning-struck tree still smoldering, fire source.
    /// </summary>
    public static Location MakeSmokeTree(Weather weather)
    {
        var location = new Location(
            name: "Smoke Tree",
            tags: "[Forest] [Fire] [Landmark]",
            weather: weather,
            terrainHazardLevel: 0.2,
            windFactor: 0.7,
            overheadCoverLevel: 0.3,
            visibilityFactor: 1.0)
        {
            DiscoveryText = "Smoke rises from a massive tree, struck by lightning and still smoldering inside. The wood is charred but preserved. You could light a torch here without tools."
        };

        // Charcoal and punk wood (smoldering tinder)
        var smokeHarvest = new HarvestableFeature("smoke_tree", "Smoldering Tree")
        {
            Description = "Lightning-struck and still smoldering. Charcoal forms as it burns.",
            MinutesToHarvest = 15
        };
        smokeHarvest.AddResource("charcoal", Resource.Charcoal, maxQuantity: 10, weightPerUnit: 0.3, respawnHoursPerUnit: 0);
        location.Features.Add(smokeHarvest);

        location.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.6));

        return location;
    }

    /// <summary>
    /// Thermal Vent - warmth from underground, barren but warm.
    /// </summary>
    public static Location MakeThermalVent(Weather weather)
    {
        var location = new Location(
            name: "Thermal Vent",
            tags: "[Warm] [Barren] [Landmark]",
            weather: weather,
            terrainHazardLevel: 0.3,
            windFactor: 0.5,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            TemperatureDeltaF = 12,
            DiscoveryText = "Warm air rises from a crack in the earth. The snow never sticks here. Nothing grows — the sulfur kills everything — but the warmth is real."
        };

        // Barren - no forage
        location.Features.Add(FeatureFactory.CreateBarrenForage(density: 0.1));

        return location;
    }

    #endregion

    #region Mountain Pass Factories

    public static Location MakePassApproach(Weather weather) => new Location(
        name: "Pass Approach",
        tags: "[Mountain] [Exposed] [Rocky]",
        weather: weather,
        terrainHazardLevel: 0.6,
        windFactor: 1.3,
        overheadCoverLevel: 0.05,
        visibilityFactor: 1.2)
    {
        DiscoveryText = "The treeline ends. Above, the pass cuts through bare rock and ice.",
        TemperatureDeltaF = -5
    };

    public static Location MakeLowerPass(Weather weather) => new Location(
        name: "Lower Pass",
        tags: "[Mountain] [Exposed] [Ice]",
        weather: weather,
        terrainHazardLevel: 0.8,
        windFactor: 1.6,
        overheadCoverLevel: 0.0,
        visibilityFactor: 1.3)
    {
        DiscoveryText = "Exposed scree and ice. The wind cuts through everything.",
        TemperatureDeltaF = -10
    };

    public static Location MakePassProper(Weather weather) => new Location(
        name: "The Pass Proper",
        tags: "[Mountain] [Exposed] [Extreme]",
        weather: weather,
        terrainHazardLevel: 1.0,
        windFactor: 2.0,
        overheadCoverLevel: 0.0,
        visibilityFactor: 0.3)
    {
        DiscoveryText = "The highest point. Snow blows horizontal. You can barely see.",
        TemperatureDeltaF = -15
    };

    public static Location MakeUpperDescent(Weather weather) => new Location(
        name: "Upper Descent",
        tags: "[Mountain] [Exposed] [Rocky]",
        weather: weather,
        terrainHazardLevel: 0.7,
        windFactor: 1.5,
        overheadCoverLevel: 0.0,
        visibilityFactor: 1.4)
    {
        DiscoveryText = "Downhill but still exposed. The far valley glimmers below.",
        TemperatureDeltaF = -10
    };

    public static Location MakeLowerDescent(Weather weather) => new Location(
        name: "Lower Descent",
        tags: "[Mountain] [Forest Edge]",
        weather: weather,
        terrainHazardLevel: 0.5,
        windFactor: 1.0,
        overheadCoverLevel: 0.1,
        visibilityFactor: 1.0)
    {
        DiscoveryText = "The worst is behind you. Trees appear below.",
        TemperatureDeltaF = -5
    };

    public static Location MakeFarSide(Weather weather) => new Location(
        name: "Far Side",
        tags: "[Valley] [Sheltered] [Victory]",
        weather: weather,
        terrainHazardLevel: 0.3,
        windFactor: 0.6,
        overheadCoverLevel: 0.3,
        visibilityFactor: 1.2)
    {
        DiscoveryText = "You made it. The valley stretches before you, green and sheltered.",
        TemperatureDeltaF = 0
    };

    #endregion
}
