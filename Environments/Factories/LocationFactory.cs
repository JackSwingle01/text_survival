using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

public static class LocationFactory
{
    #region Site Factories

    public static Location MakeForest(Zone parent)
    {
        List<string> forestNames = ["Forest", "Woodland", "Grove", "Thicket", "Pine Stand", "Birch Grove"];
        List<string> forestAdjectives = [
            "Frost-bitten", "Snow-laden", "Ice-coated", "Silent", "Frozen", "Snowy",
            "Windswept", "Frigid", "Boreal", "Primeval", "Shadowy", "Ancient",
            "Frosty", "Dark", "Foggy", "Overgrown", "Dense", "Old", "Misty", "Quiet"
        ];

        string adjective = Utils.GetRandomFromList(forestAdjectives);
        string name = Utils.GetRandomFromList(forestNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, 
                                    tags: "[Shaded] [Resource-Dense]", // 1-3 player hints
                                    parent: parent, 
                                    traversalMinutes: 10, // radius
                                    terrainHazardLevel: .2,  // 0-1
                                    windFactor: .6, // 0-2
                                    overheadCoverLevel: .3, // 0-1
                                    visibilityFactor: .7); // 0-2

        // Forage feature - forest is rich in fuel
        var forageFeature = new ForageFeature(2.0)
            .AddLogs(1.5, 1.5, 3.5)        // good logs
            .AddSticks(3.0, 0.2, 0.6)      // plenty of sticks
            .AddTinder(2.0, 0.02, 0.08)    // bark, dry leaves
            .AddBerries(0.3, 0.05, 0.15)   // occasional berries
            .AddPlantFiber(0.5, 0.05, 0.15); // bark strips, roots for cordage
        location.Features.Add(forageFeature);

        // Animal territory - forests have good game
        var animalTerritory = new AnimalTerritoryFeature(1.2)
            .AddDeer(1.0)
            .AddRabbit(0.8)
            .AddFox(0.3);
        location.Features.Add(animalTerritory);

        // Harvestables (spawn chance)
        if (Utils.DetermineSuccess(0.3))
        {
            var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush")
            {
                Description = "A frost-hardy shrub with clusters of dark berries."
            };
            berryBush.AddResource(HarvestResourceType.Berries, maxQuantity: 5, weightPerUnit: 0.1,
                respawnHoursPerUnit: 168.0, displayName: "berries");
            location.Features.Add(berryBush);
        }

        if (Utils.DetermineSuccess(0.2))
        {
            var deadfall = new HarvestableFeature("deadfall", "Fallen Tree")
            {
                Description = "A wind-felled tree with dry, harvestable wood.",
                MinutesToHarvest = 10
            };
            deadfall.AddResource(HarvestResourceType.Log, maxQuantity: 4, weightPerUnit: 2.5,
                respawnHoursPerUnit: 720.0, displayName: "firewood");  // logs don't really respawn
            deadfall.AddResource(HarvestResourceType.Stick, maxQuantity: 8, weightPerUnit: 0.3,
                respawnHoursPerUnit: 168.0, displayName: "branches");
            deadfall.AddResource(HarvestResourceType.Tinder, maxQuantity: 3, weightPerUnit: 0.05,
                respawnHoursPerUnit: 168.0, displayName: "bark strips");
            location.Features.Add(deadfall);
        }

        if (Utils.DetermineSuccess(0.25))
        {
            var puddle = new HarvestableFeature("puddle", "Forest Puddle")
            {
                Description = "A shallow puddle fed by melting snow.",
                MinutesToHarvest = 2
            };
            puddle.AddResource(HarvestResourceType.Water, maxQuantity: 3, weightPerUnit: 0.5,
                respawnHoursPerUnit: 12.0, displayName: "water");
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeCave(Zone parent)
    {
        List<string> caveNames = ["Cave", "Cavern", "Grotto", "Hollow", "Shelter"];
        List<string> caveAdjectives = [
            "Icicle-lined", "Frost-rimmed", "Bone-strewn", "Winding", "Ancient",
            "Hidden", "Rocky", "Echoing", "Deep", "Dark", "Shadowy", "Damp",
            "Frozen", "Narrow", "Secluded", "Cold", "Protected"
        ];

        string adjective = Utils.GetRandomFromList(caveAdjectives);
        string name = Utils.GetRandomFromList(caveNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Sheltered] [Dark]",
                                    parent: parent,
                                    traversalMinutes: 5,
                                    terrainHazardLevel: 0.1,
                                    windFactor: 0.1,
                                    overheadCoverLevel: 1.0,
                                    visibilityFactor: 0.3);

        // Caves have very little forage - minimal fuel, no food
        var forageFeature = new ForageFeature(0.3)
            .AddTinder(0.2, 0.01, 0.03);  // occasional dry debris
        location.Features.Add(forageFeature);

        // Shelter feature - caves provide natural shelter
        location.Features.Add(new ShelterFeature("Cave", .5, 1, .9));

        return location;
    }

    public static Location MakeRiverbank(Zone parent)
    {
        List<string> riverNames = ["River", "Stream", "Creek", "Brook", "Rapids", "Ford", "Shallows"];
        List<string> riverAdjectives = [
            "Ice-rimmed", "Glacial", "Snowmelt", "Half-frozen", "Narrow",
            "Icy", "Cold", "Mist-shrouded", "Foggy", "Crystalline", "Frigid",
            "Rushing", "Flowing", "Clear", "Shallow", "Deep"
        ];

        string adjective = Utils.GetRandomFromList(riverAdjectives);
        string name = Utils.GetRandomFromList(riverNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Water] [Open]",
                                    parent: parent,
                                    traversalMinutes: 8,
                                    terrainHazardLevel: 0.3,
                                    windFactor: 1.0,
                                    overheadCoverLevel: 0.1,
                                    visibilityFactor: 1.2);

        // Riverbeds have driftwood, limited other resources, but good stone
        var forageFeature = new ForageFeature(1.2)
            .AddSticks(2.0, 0.2, 0.5)     // driftwood
            .AddLogs(1.0, 1.0, 2.0)       // occasional larger driftwood
            .AddStone(0.6, 0.2, 0.5);     // river-smoothed stones, good for knapping
        location.Features.Add(forageFeature);

        // Water source harvestable - always present at rivers
        var river = new HarvestableFeature("river", "Ice-Fed River")
        {
            Description = "A swift-flowing river fed by glacial meltwater. Cold but clear.",
            MinutesToHarvest = 1
        };
        river.AddResource(HarvestResourceType.Water, maxQuantity: 100, weightPerUnit: 1.0,
            respawnHoursPerUnit: 0.1, displayName: "water");  // effectively unlimited
        location.Features.Add(river);

        return location;
    }

    public static Location MakePlain(Zone parent)
    {
        List<string> plainNames = ["Plain", "Steppe", "Tundra", "Grassland", "Prairie", "Meadow"];
        List<string> plainAdjectives = [
            "Windswept", "Frozen", "Vast", "Rolling", "Endless",
            "Snow-covered", "Desolate", "Frosty", "Exposed",
            "Bleak", "Stark", "Harsh", "Flat", "Open", "Windy", "Cold", "Barren", "Wild"
        ];

        string adjective = Utils.GetRandomFromList(plainAdjectives);
        string name = Utils.GetRandomFromList(plainNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Exposed] [Open]",
                                    parent: parent,
                                    traversalMinutes: 15,
                                    terrainHazardLevel: 0.1,
                                    windFactor: 1.4,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 1.5);

        // Plains are sparse - mainly dry grass for tinder
        var forageFeature = new ForageFeature(0.5)
            .AddTinder(0.7, 0.03, 0.1)    // dry grass is common
            .AddSticks(0.2, 0.1, 0.3)     // occasional scrub
            .AddBerries(0.15, 0.03, 0.1)  // sparse berries
            .AddPlantFiber(0.6, 0.05, 0.12); // dry grass for cordage
        location.Features.Add(forageFeature);

        // Animal territory - plains have small game and birds
        var animalTerritory = new AnimalTerritoryFeature(0.8)
            .AddRabbit(1.0)
            .AddPtarmigan(0.7);
        location.Features.Add(animalTerritory);

        // Occasional meltwater
        if (Utils.DetermineSuccess(0.2))
        {
            var puddle = new HarvestableFeature("puddle", "Meltwater Puddle")
            {
                Description = "A shallow depression with fresh meltwater. Frozen at edges.",
                MinutesToHarvest = 2
            };
            puddle.AddResource(HarvestResourceType.Water, maxQuantity: 2, weightPerUnit: 0.5,
                respawnHoursPerUnit: 24.0, displayName: "water");
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeHillside(Zone parent)
    {
        List<string> hillNames = ["Ridge", "Slope", "Crag", "Bluff", "Outcrop", "Hill", "Knoll"];
        List<string> hillAdjectives = [
            "Glacier-carved", "Ice-cracked", "Snow-swept", "Wind-scoured", "Craggy",
            "Rugged", "Snow-capped", "Icy", "Stone", "High", "Misty",
            "Rocky", "Steep", "Windswept", "Exposed", "Barren", "Weathered"
        ];

        string adjective = Utils.GetRandomFromList(hillAdjectives);
        string name = Utils.GetRandomFromList(hillNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Steep] [Rocky]",
                                    parent: parent,
                                    traversalMinutes: 12,
                                    terrainHazardLevel: 0.5,
                                    windFactor: 1.3,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 1.3);

        // Hills have sparse vegetation but exposed stone
        var forageFeature = new ForageFeature(0.4)
            .AddTinder(0.3, 0.02, 0.05)   // limited dry material
            .AddSticks(0.15, 0.1, 0.25)   // scrub brush
            .AddStone(0.5, 0.25, 0.6);    // exposed rock for tools
        location.Features.Add(forageFeature);

        return location;
    }

    public static Location MakeClearing(Zone parent)
    {
        List<string> clearingNames = ["Clearing", "Glade", "Opening", "Break", "Gap"];
        List<string> clearingAdjectives = [
            "Sheltered", "Sunny", "Quiet", "Hidden", "Small", "Wide", "Mossy",
            "Snow-covered", "Frost-rimmed", "Protected", "Ancient", "Wind-sheltered"
        ];

        string adjective = Utils.GetRandomFromList(clearingAdjectives);
        string name = Utils.GetRandomFromList(clearingNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Sheltered] [Clearing]",
                                    parent: parent,
                                    traversalMinutes: 8,
                                    terrainHazardLevel: 0.1,
                                    windFactor: 0.6,
                                    overheadCoverLevel: 0.2,
                                    visibilityFactor: 1.1);

        // Clearings have moderate resources
        var forageFeature = new ForageFeature(1.3)
            .AddSticks(2.5, 0.15, 0.4)
            .AddLogs(1.0, 1.0, 2.5)
            .AddTinder(1.8, 0.02, 0.06)
            .AddBerries(0.25, 0.05, 0.12)
            .AddPlantFiber(0.4, 0.05, 0.1); // undergrowth for cordage
        location.Features.Add(forageFeature);

        // Animal territory - clearings attract deer for grazing
        var animalTerritory = new AnimalTerritoryFeature(1.0)
            .AddDeer(1.2)
            .AddRabbit(0.6);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Hot spring - thermal pull. Warmth bonus draws players here for shelter.
    /// </summary>
    public static Location MakeHotSpring(Zone parent)
    {
        List<string> springNames = ["Hot Spring", "Thermal Pool", "Steaming Pool", "Warm Springs"];
        List<string> springAdjectives = [
            "Misty", "Steaming", "Bubbling", "Hidden", "Sulfurous",
            "Ancient", "Sacred", "Warm", "Sheltered"
        ];

        string adjective = Utils.GetRandomFromList(springAdjectives);
        string name = Utils.GetRandomFromList(springNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Warm] [Water]",
                                    parent: parent,
                                    traversalMinutes: 20,
                                    terrainHazardLevel: 0.3,
                                    windFactor: 0.4,
                                    overheadCoverLevel: 0.1,
                                    visibilityFactor: 0.8)
        {
            DiscoveryText = "Steam rises from a pool of warm water. The air here is noticeably warmer."
        };

        // Hot springs provide significant warmth bonus (+20F)
        // Note: TemperatureDeltaF is readonly, so we set via primary constructor
        // For now, use shelter feature to represent the warmth

        // Very limited forage - mostly just mineral deposits
        var forageFeature = new ForageFeature(0.3)
            .AddStone(0.4, 0.1, 0.3);
        location.Features.Add(forageFeature);

        // Water harvestable - warm water is always available
        var spring = new HarvestableFeature("hot_spring", "Thermal Pool")
        {
            Description = "Warm water bubbles up from deep below. Safe to drink once cooled.",
            MinutesToHarvest = 2
        };
        spring.AddResource(HarvestResourceType.Water, maxQuantity: 50, weightPerUnit: 1.0,
            respawnHoursPerUnit: 0.1, displayName: "warm water");
        location.Features.Add(spring);

        // Animals come here to drink
        var animalTerritory = new AnimalTerritoryFeature(0.6)
            .AddDeer(0.8)
            .AddRabbit(0.4);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Frozen creek - water source but hazardous and slippery.
    /// </summary>
    public static Location MakeFrozenCreek(Zone parent)
    {
        List<string> creekNames = ["Creek", "Stream", "Brook", "Run", "Channel"];
        List<string> creekAdjectives = [
            "Frozen", "Ice-locked", "Silent", "Narrow", "Winding",
            "Treacherous", "Slick", "Glassy"
        ];

        string adjective = Utils.GetRandomFromList(creekAdjectives);
        string name = Utils.GetRandomFromList(creekNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Ice] [Water] [Slippery]",
                                    parent: parent,
                                    traversalMinutes: 12,
                                    terrainHazardLevel: 0.5,
                                    windFactor: 0.9,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 1.0)
        {
            DiscoveryText = "The creek is frozen solid. Dark shapes move beneath the ice."
        };

        // Limited forage - some driftwood frozen in ice
        var forageFeature = new ForageFeature(0.6)
            .AddSticks(1.5, 0.15, 0.4)
            .AddStone(0.3, 0.2, 0.4);
        location.Features.Add(forageFeature);

        // Ice can be harvested for water
        var iceSource = new HarvestableFeature("ice", "Creek Ice")
        {
            Description = "Thick ice covers the creek. Can be broken and melted for water.",
            MinutesToHarvest = 5
        };
        iceSource.AddResource(HarvestResourceType.Water, maxQuantity: 20, weightPerUnit: 1.0,
            respawnHoursPerUnit: 48.0, displayName: "ice chunks");
        location.Features.Add(iceSource);

        return location;
    }

    /// <summary>
    /// Deadwood grove - excellent fuel source but dangerous footing from tangled logs.
    /// </summary>
    public static Location MakeDeadwoodGrove(Zone parent)
    {
        List<string> groveNames = ["Grove", "Stand", "Tangle", "Deadfall", "Blowdown"];
        List<string> groveAdjectives = [
            "Deadwood", "Tangled", "Storm-felled", "Rotting", "Bone-dry",
            "Jumbled", "Chaotic", "Treacherous"
        ];

        string adjective = Utils.GetRandomFromList(groveAdjectives);
        string name = Utils.GetRandomFromList(groveNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Fuel] [Treacherous]",
                                    parent: parent,
                                    traversalMinutes: 15,
                                    terrainHazardLevel: 0.7,
                                    windFactor: 0.5,
                                    overheadCoverLevel: 0.2,
                                    visibilityFactor: 0.4)
        {
            DiscoveryText = "A tangle of fallen trees, bleached and dry. Fuel everywhere - but one wrong step could snap an ankle."
        };

        // Exceptional fuel resources - the pull
        var forageFeature = new ForageFeature(3.0)
            .AddLogs(3.0, 1.5, 4.0)
            .AddSticks(4.0, 0.2, 0.7)
            .AddTinder(3.0, 0.02, 0.1);
        location.Features.Add(forageFeature);

        // Large harvestable deadfall
        var deadfall = new HarvestableFeature("massive_deadfall", "Massive Fallen Pine")
        {
            Description = "A huge pine, wind-felled and bone dry. Enough fuel to last days.",
            MinutesToHarvest = 20
        };
        deadfall.AddResource(HarvestResourceType.Log, maxQuantity: 8, weightPerUnit: 2.5,
            respawnHoursPerUnit: 0, displayName: "dry logs");
        deadfall.AddResource(HarvestResourceType.Stick, maxQuantity: 15, weightPerUnit: 0.3,
            respawnHoursPerUnit: 0, displayName: "branches");
        deadfall.AddResource(HarvestResourceType.Tinder, maxQuantity: 5, weightPerUnit: 0.05,
            respawnHoursPerUnit: 0, displayName: "bark strips");
        location.Features.Add(deadfall);

        // Small game hides in the deadfall
        var animalTerritory = new AnimalTerritoryFeature(0.7)
            .AddRabbit(1.2)
            .AddFox(0.4);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Rocky overlook - high visibility for scouting, exposed, good stone.
    /// </summary>
    public static Location MakeOverlook(Zone parent)
    {
        List<string> overlookNames = ["Overlook", "Viewpoint", "Lookout", "Vantage", "Summit"];
        List<string> overlookAdjectives = [
            "Rocky", "Wind-scoured", "High", "Exposed", "Commanding",
            "Stark", "Barren", "Open"
        ];

        string adjective = Utils.GetRandomFromList(overlookAdjectives);
        string name = Utils.GetRandomFromList(overlookNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Scout] [Exposed] [Stone]",
                                    parent: parent,
                                    traversalMinutes: 18,
                                    terrainHazardLevel: 0.4,
                                    windFactor: 1.6,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 1.8)
        {
            DiscoveryText = "The view stretches for miles. You can see smoke from distant fires, animal trails below."
        };

        // Excellent stone, very limited vegetation
        var forageFeature = new ForageFeature(0.4)
            .AddStone(1.5, 0.3, 0.8)
            .AddTinder(0.1, 0.01, 0.03);
        location.Features.Add(forageFeature);

        // Birds nest on high places
        var animalTerritory = new AnimalTerritoryFeature(0.4)
            .AddPtarmigan(1.0);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Marsh - treacherous but resource-rich. Waterfowl, medicinal plants, cattails.
    /// </summary>
    public static Location MakeMarsh(Zone parent)
    {
        List<string> marshNames = ["Marsh", "Bog", "Wetland", "Fen", "Mire"];
        List<string> marshAdjectives = [
            "Frozen", "Murky", "Reedy", "Treacherous", "Foggy",
            "Ice-crusted", "Silent", "Misty"
        ];

        string adjective = Utils.GetRandomFromList(marshAdjectives);
        string name = Utils.GetRandomFromList(marshNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Water] [Treacherous] [Plants]",
                                    parent: parent,
                                    traversalMinutes: 20,
                                    terrainHazardLevel: 0.6,
                                    windFactor: 0.7,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 0.6)
        {
            DiscoveryText = "The ground gives way to frozen marsh. Cattails poke through the ice. Rich foraging if you're careful."
        };

        // Rich in plant resources
        var forageFeature = new ForageFeature(1.8)
            .AddPlantFiber(2.0, 0.08, 0.2)
            .AddBerries(0.4, 0.05, 0.15)
            .AddSticks(0.5, 0.1, 0.3);
        location.Features.Add(forageFeature);

        // Cattails are excellent harvestable
        var cattails = new HarvestableFeature("cattails", "Cattail Stand")
        {
            Description = "Dense cattails at the marsh edge. Edible roots, fluffy seed heads for tinder.",
            MinutesToHarvest = 10
        };
        cattails.AddResource(HarvestResourceType.PlantFiber, maxQuantity: 10, weightPerUnit: 0.1,
            respawnHoursPerUnit: 168.0, displayName: "cattail fiber");
        cattails.AddResource(HarvestResourceType.Tinder, maxQuantity: 6, weightPerUnit: 0.03,
            respawnHoursPerUnit: 168.0, displayName: "cattail fluff");
        location.Features.Add(cattails);

        // Water source
        var water = new HarvestableFeature("marsh_water", "Open Water")
        {
            Description = "Dark water between ice sheets. Needs to be boiled.",
            MinutesToHarvest = 3
        };
        water.AddResource(HarvestResourceType.Water, maxQuantity: 30, weightPerUnit: 1.0,
            respawnHoursPerUnit: 6.0, displayName: "marsh water");
        location.Features.Add(water);

        // Waterfowl
        var animalTerritory = new AnimalTerritoryFeature(0.9)
            .AddPtarmigan(1.5)
            .AddRabbit(0.5);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Ice crevasse - natural cache site. Dangerous to reach but preserves food.
    /// </summary>
    public static Location MakeIceCrevasse(Zone parent)
    {
        List<string> crevasseNames = ["Crevasse", "Ice Cleft", "Glacier Crack", "Ice Fissure"];
        List<string> crevasseAdjectives = [
            "Deep", "Blue", "Narrow", "Ancient", "Hidden",
            "Treacherous", "Dark"
        ];

        string adjective = Utils.GetRandomFromList(crevasseAdjectives);
        string name = Utils.GetRandomFromList(crevasseNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Ice] [Cache] [Dangerous]",
                                    parent: parent,
                                    traversalMinutes: 25,
                                    terrainHazardLevel: 0.8,
                                    windFactor: 0.2,
                                    overheadCoverLevel: 0.8,
                                    visibilityFactor: 0.2)
        {
            DiscoveryText = "A deep crack in ancient ice. Freezing cold, but perfect for storing meat.",
            IsDark = true
        };

        // Very limited forage
        var forageFeature = new ForageFeature(0.1)
            .AddStone(0.2, 0.1, 0.3);
        location.Features.Add(forageFeature);

        // Natural cache - food preserving
        location.Features.Add(CacheFeature.CreateIceCache());

        return location;
    }

    /// <summary>
    /// Abandoned camp - salvage site with one-time loot.
    /// </summary>
    public static Location MakeAbandonedCamp(Zone parent)
    {
        var location = new Location("Old Campsite",
                                    tags: "[Salvage] [Shelter]",
                                    parent: parent,
                                    traversalMinutes: 15,
                                    terrainHazardLevel: 0.2,
                                    windFactor: 0.5,
                                    overheadCoverLevel: 0.3,
                                    visibilityFactor: 0.7)
        {
            DiscoveryText = "Signs of an old camp. The fire pit is cold, shelter collapsed. Someone was here before you."
        };

        // Minimal forage - area picked over
        var forageFeature = new ForageFeature(0.4)
            .AddSticks(0.5, 0.1, 0.3)
            .AddTinder(0.3, 0.01, 0.04);
        location.Features.Add(forageFeature);

        // Salvage site with one-time loot
        location.Features.Add(SalvageFeature.CreateAbandonedCamp());

        // Collapsed shelter still provides some protection
        location.Features.Add(new ShelterFeature("Collapsed Lean-to", 0.2, 0.4, 0.3));

        return location;
    }

    /// <summary>
    /// Wolf den - dangerous but rewarding hunting grounds.
    /// </summary>
    public static Location MakeWolfDen(Zone parent)
    {
        List<string> denNames = ["Den", "Lair", "Hollow", "Haunt"];
        List<string> denAdjectives = [
            "Wolf", "Predator", "Wild", "Dangerous", "Bone-strewn"
        ];

        string adjective = Utils.GetRandomFromList(denAdjectives);
        string name = Utils.GetRandomFromList(denNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Wolves] [Dangerous] [Bones]",
                                    parent: parent,
                                    traversalMinutes: 18,
                                    terrainHazardLevel: 0.3,
                                    windFactor: 0.4,
                                    overheadCoverLevel: 0.6,
                                    visibilityFactor: 0.5)
        {
            DiscoveryText = "Wolf tracks everywhere. Bones scattered around a rocky hollow. The smell of predator is strong."
        };

        // Bones from wolf kills
        var forageFeature = new ForageFeature(0.6)
            .AddBone(1.5, 0.1, 0.4)
            .AddSticks(0.3, 0.1, 0.3);
        location.Features.Add(forageFeature);

        // Wolf territory - dangerous but rewarding
        var animalTerritory = new AnimalTerritoryFeature(1.5)
            .AddWolf(2.0)
            .AddRabbit(0.3);
        location.Features.Add(animalTerritory);

        return location;
    }

    /// <summary>
    /// Sheltered valley - protected from wind, good for extended stays.
    /// </summary>
    public static Location MakeShelteredValley(Zone parent)
    {
        List<string> valleyNames = ["Valley", "Hollow", "Dell", "Basin", "Glen"];
        List<string> valleyAdjectives = [
            "Sheltered", "Hidden", "Protected", "Quiet", "Secluded",
            "Wind-shadowed", "Peaceful"
        ];

        string adjective = Utils.GetRandomFromList(valleyAdjectives);
        string name = Utils.GetRandomFromList(valleyNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name,
                                    tags: "[Sheltered] [Camp-worthy]",
                                    parent: parent,
                                    traversalMinutes: 22,
                                    terrainHazardLevel: 0.15,
                                    windFactor: 0.2,
                                    overheadCoverLevel: 0.4,
                                    visibilityFactor: 0.5)
        {
            DiscoveryText = "A natural hollow, shielded from the worst winds. This would make a good camp."
        };

        // Good forage - sheltered areas support more life
        var forageFeature = new ForageFeature(1.6)
            .AddSticks(2.5, 0.2, 0.5)
            .AddLogs(1.2, 1.2, 2.8)
            .AddTinder(1.5, 0.02, 0.07)
            .AddBerries(0.4, 0.05, 0.15)
            .AddPlantFiber(0.6, 0.05, 0.12);
        location.Features.Add(forageFeature);

        // Good game - animals shelter here too
        var animalTerritory = new AnimalTerritoryFeature(1.3)
            .AddDeer(1.0)
            .AddRabbit(1.0)
            .AddFox(0.3);
        location.Features.Add(animalTerritory);

        // Natural rock cache
        location.Features.Add(CacheFeature.CreateRockCache());

        return location;
    }

    #endregion


    #region Path Factories

    // public static Location MakeForestPath(
    //     Zone parent,
    //     string? name = null,
    //     int traversalMinutes = 10,
    //     double exposure = 0.5)
    // {
    //     if (name == null)
    //     {
    //         List<string> pathNames = ["Trail", "Path", "Track", "Way", "Route"];
    //         List<string> pathAdjectives = [
    //             "Winding", "Narrow", "Overgrown", "Animal", "Deer", "Mossy",
    //             "Snow-covered", "Icy", "Muddy", "Rocky", "Faint", "Well-worn"
    //         ];
    //         string adjective = Utils.GetRandomFromList(pathAdjectives);
    //         string baseName = Utils.GetRandomFromList(pathNames);
    //         name = (adjective + " " + baseName).Trim();
    //     }

    //     var location = new Location(name, parent)
    //     {
    //         BaseTraversalMinutes = traversalMinutes,
    //         WindCoverFactor = exposure,
    //     };

    //     return location;
    // }

    // public static Location MakeOpenPath(
    //     Zone parent,
    //     string? name = null,
    //     int traversalMinutes = 15,
    //     double exposure = 0.8)
    // {
    //     if (name == null)
    //     {
    //         List<string> pathNames = ["Stretch", "Expanse", "Crossing", "Run", "Passage"];
    //         List<string> pathAdjectives = [
    //             "Open", "Windswept", "Exposed", "Snowy", "Icy", "Frozen",
    //             "Bleak", "Long", "Treacherous", "Featureless"
    //         ];
    //         string adjective = Utils.GetRandomFromList(pathAdjectives);
    //         string baseName = Utils.GetRandomFromList(pathNames);
    //         name = (adjective + " " + baseName).Trim();
    //     }

    //     var location = new Location(name, parent)
    //     {
    //         BaseTraversalMinutes = traversalMinutes,
    //         WindCoverFactor = exposure,
    //         Terrain = terrain,
    //     };

    //     return location;
    // }

    // public static Location MakeSteepPath(
    //     Zone parent,
    //     string? name = null,
    //     int traversalMinutes = 12,
    //     double exposure = 0.7)
    // {
    //     if (name == null)
    //     {
    //         List<string> pathNames = ["Climb", "Ascent", "Descent", "Scramble", "Slope"];
    //         List<string> pathAdjectives = [
    //             "Steep", "Rocky", "Treacherous", "Slippery", "Icy",
    //             "Narrow", "Dangerous", "Loose", "Crumbling"
    //         ];
    //         string adjective = Utils.GetRandomFromList(pathAdjectives);
    //         string baseName = Utils.GetRandomFromList(pathNames);
    //         name = (adjective + " " + baseName).Trim();
    //     }

    //     var location = new Location(name, parent)
    //     {
    //         BaseTraversalMinutes = traversalMinutes,
    //         WindCoverFactor = exposure,
    //         Terrain = TerrainType.Steep,
    //     };

    //     return location;
    // }

    // public static Location MakeRoughPath(
    //     Zone parent,
    //     string? name = null,
    //     int traversalMinutes = 12,
    //     double exposure = 0.5)
    // {
    //     if (name == null)
    //     {
    //         List<string> pathNames = ["Trail", "Path", "Way", "Passage", "Route"];
    //         List<string> pathAdjectives = [
    //             "Overgrown", "Rough", "Tangled", "Blocked", "Difficult",
    //             "Brush-choked", "Root-tangled", "Boulder-strewn"
    //         ];
    //         string adjective = Utils.GetRandomFromList(pathAdjectives);
    //         string baseName = Utils.GetRandomFromList(pathNames);
    //         name = (adjective + " " + baseName).Trim();
    //     }

    //     var location = new Location(name, parent)
    //     {
    //         BaseTraversalMinutes = traversalMinutes,
    //         WindCoverFactor = exposure,
    //         Terrain = TerrainType.Rough,
    //     };

    //     return location;
    // }



    #endregion
}
