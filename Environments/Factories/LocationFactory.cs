using text_survival.Actions;
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
            Terrain = terrain
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
        TerrainType.Clearing => Utils.FlipCoin() ? FeatureFactory.CreateBerryBush() : FeatureFactory.CreateForestPuddle(),
        TerrainType.Plain => FeatureFactory.CreateMeltwaterPuddle(),
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
            name: "Forest",
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

        if (Utils.DetermineSuccess(0.25))
            location.Features.Add(FeatureFactory.CreateForestPuddle());

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
        location.Features.Add(new ShelterFeature("Cave", .5, 1, .9));

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
            name: "Plain",
            tags: "[Exposed] [Open]",
            weather: weather,
            terrainHazardLevel: 0.1,
            windFactor: 1.4,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5);

        location.Features.Add(FeatureFactory.CreateOpenForage(density: 0.5));
        location.Features.Add(FeatureFactory.CreateSmallGameAnimals(density: 0.8));

        if (Utils.DetermineSuccess(0.2))
            location.Features.Add(FeatureFactory.CreateMeltwaterPuddle());

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
            name: "Clearing",
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
            name: "Marsh",
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
            MegafaunaType = "SaberTooth",
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
        location.Features.Add(new ShelterFeature("Overhang", 0.3, 0.7, 0.5));

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

        location.Features.Add(FeatureFactory.CreateOldGrowthForage(density: 0.4));

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
            MegafaunaType = "SaberTooth",
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
            MegafaunaType = "SaberTooth",
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
            MegafaunaType = "Mammoth",
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
