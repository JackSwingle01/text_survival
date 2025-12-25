using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Environments.Factories;

public static class LocationFactory
{
    #region Site Factories

    public static Location MakeForest(Weather weather)
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
                                    weather: weather, 
                                    traversalMinutes: 10, // radius
                                    terrainHazardLevel: .2,  // 0-1
                                    windFactor: .6, // 0-2
                                    overheadCoverLevel: .3, // 0-1
                                    visibilityFactor: .7); // 0-2

        // Forage feature - forest is rich in fuel and medicinals
        var forageFeature = new ForageFeature(2.0)
            .AddLogs(1.5, 1.5, 3.5)        // good logs
            .AddSticks(3.0, 0.2, 0.6)      // plenty of sticks
            .AddTinder(2.0, 0.02, 0.08)    // bark, dry leaves
            .AddBerries(0.3, 0.05, 0.15)   // occasional berries
            .AddPlantFiber(0.5, 0.05, 0.15) // bark strips, roots for cordage
            // Fungi on birch/dead trees
            .AddBirchPolypore(0.15)        // wound treatment
            .AddChaga(0.1)                 // anti-inflammatory
            .AddAmadou(0.12)               // fire-starting, wound dressing
            // Conifer products
            .AddPineNeedles(0.25)          // vitamin C tea
            .AddPineResin(0.1)             // wound sealing
            .AddUsnea(0.15);               // old man's beard lichen
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
            berryBush.AddBerries("berries", maxQuantity: 5, weightPerUnit: 0.1, respawnHoursPerUnit: 168.0);
            location.Features.Add(berryBush);
        }

        if (Utils.DetermineSuccess(0.2))
        {
            var deadfall = new HarvestableFeature("deadfall", "Fallen Tree")
            {
                Description = "A wind-felled tree with dry, harvestable wood.",
                MinutesToHarvest = 10
            };
            deadfall.AddLogs("firewood", maxQuantity: 4, weightPerUnit: 2.5, respawnHoursPerUnit: 720.0);  // logs don't really respawn
            deadfall.AddSticks("branches", maxQuantity: 8, weightPerUnit: 0.3, respawnHoursPerUnit: 168.0);
            deadfall.AddTinder("bark strips", maxQuantity: 3, weightPerUnit: 0.05, respawnHoursPerUnit: 168.0);
            location.Features.Add(deadfall);
        }

        if (Utils.DetermineSuccess(0.25))
        {
            var puddle = new HarvestableFeature("puddle", "Forest Puddle")
            {
                Description = "A shallow puddle fed by melting snow.",
                MinutesToHarvest = 2
            };
            puddle.AddWater("water", maxQuantity: 3, litersPerUnit: 0.5, respawnHoursPerUnit: 12.0);
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeCave(Weather weather)
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
                                    weather: weather,
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

    public static Location MakeRiverbank(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 8,
                                    terrainHazardLevel: 0.3,
                                    windFactor: 1.0,
                                    overheadCoverLevel: 0.1,
                                    visibilityFactor: 1.2);

        // Riverbeds have driftwood, willows along banks
        var forageFeature = new ForageFeature(1.2)
            .AddSticks(2.0, 0.2, 0.5)     // driftwood
            .AddLogs(1.0, 1.0, 2.0)       // occasional larger driftwood
            .AddStone(0.6, 0.2, 0.5)      // river-smoothed stones, good for knapping
            .AddWillowBark(0.25)          // willows grow along water - pain relief
            .AddSphagnum(0.15);           // peat moss in boggy spots
        location.Features.Add(forageFeature);

        // Water source harvestable - always present at rivers
        var river = new HarvestableFeature("river", "Ice-Fed River")
        {
            Description = "A swift-flowing river fed by glacial meltwater. Cold but clear.",
            MinutesToHarvest = 1
        };
        river.AddWater("water", maxQuantity: 100, litersPerUnit: 1.0, respawnHoursPerUnit: 0.1);  // effectively unlimited
        location.Features.Add(river);

        // Water feature for ice hazard - flowing water has moderate/thin ice
        var waterFeature = new WaterFeature("river_water", "River")
            .WithDescription("Fast-flowing sections stay open, but ice forms at the edges.")
            .WithIceThickness(0.5);  // Moderate ice - contributes +0.15 hazard
        location.Features.Add(waterFeature);

        return location;
    }

    public static Location MakePlain(Weather weather)
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
                                    weather: weather,
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
            puddle.AddWater("water", maxQuantity: 2, litersPerUnit: 0.5, respawnHoursPerUnit: 24.0);
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeHillside(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 12,
                                    terrainHazardLevel: 0.5,
                                    windFactor: 1.3,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 1.3);

        // Hills have sparse vegetation but exposed stone and hardy shrubs
        var forageFeature = new ForageFeature(0.4)
            .AddTinder(0.3, 0.02, 0.05)   // limited dry material
            .AddSticks(0.15, 0.1, 0.25)   // scrub brush
            .AddStone(0.5, 0.25, 0.6)     // exposed rock for tools
            .AddJuniperBerries(0.2);      // hardy juniper shrubs on rocky slopes
        location.Features.Add(forageFeature);

        return location;
    }

    public static Location MakeClearing(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 8,
                                    terrainHazardLevel: 0.1,
                                    windFactor: 0.6,
                                    overheadCoverLevel: 0.2,
                                    visibilityFactor: 1.1);

        // Clearings have moderate resources + forest edge plants
        var forageFeature = new ForageFeature(1.3)
            .AddSticks(2.5, 0.15, 0.4)
            .AddLogs(1.0, 1.0, 2.5)
            .AddTinder(1.8, 0.02, 0.06)
            .AddBerries(0.25, 0.05, 0.12)
            .AddPlantFiber(0.4, 0.05, 0.1)  // undergrowth for cordage
            .AddRoseHips(0.25)              // vitamin C, persist into winter
            .AddRoots(0.2);                 // edible roots in forest floor
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
    public static Location MakeHotSpring(Weather weather)
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
                                    weather: weather,
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
        spring.AddWater("warm water", maxQuantity: 50, litersPerUnit: 1.0, respawnHoursPerUnit: 0.1);
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
    public static Location MakeFrozenCreek(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 12,
                                    terrainHazardLevel: 0.35,  // Reduced - WaterFeature adds ice hazard
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
        iceSource.AddWater("ice chunks", maxQuantity: 20, litersPerUnit: 1.0, respawnHoursPerUnit: 48.0);
        location.Features.Add(iceSource);

        // Water feature for ice hazard - creek is solidly frozen
        var waterFeature = new WaterFeature("creek_water", "Frozen Creek")
            .WithDescription("The creek is frozen solid. Safe to cross if you're careful.")
            .AsSolidIce();  // 0.7 thickness - contributes +0.15 hazard, total ~0.50
        location.Features.Add(waterFeature);

        return location;
    }

    /// <summary>
    /// Deadwood grove - excellent fuel source but dangerous footing from tangled logs.
    /// </summary>
    public static Location MakeDeadwoodGrove(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 15,
                                    terrainHazardLevel: 0.7,
                                    windFactor: 0.5,
                                    overheadCoverLevel: 0.2,
                                    visibilityFactor: 0.4)
        {
            DiscoveryText = "A tangle of fallen trees, bleached and dry. Fuel everywhere - but one wrong step could snap an ankle."
        };

        // Exceptional fuel resources - the pull. Dead trees have fungi
        var forageFeature = new ForageFeature(3.0)
            .AddLogs(3.0, 1.5, 4.0)
            .AddSticks(4.0, 0.2, 0.7)
            .AddTinder(3.0, 0.02, 0.1)
            .AddAmadou(0.25)              // tinder fungus on dead trees
            .AddBirchPolypore(0.2);       // bracket fungus on dead birch
        location.Features.Add(forageFeature);

        // Large harvestable deadfall
        var deadfall = new HarvestableFeature("massive_deadfall", "Massive Fallen Pine")
        {
            Description = "A huge pine, wind-felled and bone dry. Enough fuel to last days.",
            MinutesToHarvest = 20
        };
        deadfall.AddLogs("dry logs", maxQuantity: 8, weightPerUnit: 2.5, respawnHoursPerUnit: 0);
        deadfall.AddSticks("branches", maxQuantity: 15, weightPerUnit: 0.3, respawnHoursPerUnit: 0);
        deadfall.AddTinder("bark strips", maxQuantity: 5, weightPerUnit: 0.05, respawnHoursPerUnit: 0);
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
    public static Location MakeOverlook(Weather weather)
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
                                    weather: weather,
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
    public static Location MakeMarsh(Weather weather)
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
                                    weather: weather,
                                    traversalMinutes: 20,
                                    terrainHazardLevel: 0.4,  // Reduced - WaterFeature adds thin ice hazard
                                    windFactor: 0.7,
                                    overheadCoverLevel: 0.0,
                                    visibilityFactor: 0.6)
        {
            DiscoveryText = "The ground gives way to frozen marsh. Cattails poke through the ice. Rich foraging if you're careful."
        };

        // Rich in plant resources - sphagnum thrives here
        var forageFeature = new ForageFeature(1.8)
            .AddPlantFiber(2.0, 0.08, 0.2)
            .AddBerries(0.4, 0.05, 0.15)
            .AddSticks(0.5, 0.1, 0.3)
            .AddSphagnum(0.4);            // peat moss - absorbent, antiseptic
        location.Features.Add(forageFeature);

        // Cattails are excellent harvestable
        var cattails = new HarvestableFeature("cattails", "Cattail Stand")
        {
            Description = "Dense cattails at the marsh edge. Edible roots, fluffy seed heads for tinder.",
            MinutesToHarvest = 10
        };
        cattails.AddPlantFiber("cattail fiber", maxQuantity: 10, weightPerUnit: 0.1, respawnHoursPerUnit: 168.0);
        cattails.AddTinder("cattail fluff", maxQuantity: 6, weightPerUnit: 0.03, respawnHoursPerUnit: 168.0);
        location.Features.Add(cattails);

        // Water source
        var water = new HarvestableFeature("marsh_water", "Open Water")
        {
            Description = "Dark water between ice sheets. Needs to be boiled.",
            MinutesToHarvest = 3
        };
        water.AddWater("marsh water", maxQuantity: 30, litersPerUnit: 1.0, respawnHoursPerUnit: 6.0);
        location.Features.Add(water);

        // Water feature for ice hazard - marsh has THIN ice (very dangerous!)
        var waterFeature = new WaterFeature("marsh_ice", "Marsh Ice")
            .WithDescription("Thin ice between tussocks. One wrong step and you're through.")
            .AsThinIce();  // 0.3 thickness - contributes +0.35 hazard, total ~0.75
        location.Features.Add(waterFeature);

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
    public static Location MakeIceCrevasse(Weather weather)
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
                                    weather: weather,
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
    public static Location MakeAbandonedCamp(Weather weather)
    {
        var location = new Location("Old Campsite",
                                    tags: "[Salvage] [Shelter]",
                                    weather: weather,
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
    public static Location MakeWolfDen(Weather weather)
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
                                    weather: weather,
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
    public static Location MakeShelteredValley(Weather weather)
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
                                    weather: weather,
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
            .AddPlantFiber(0.6, 0.05, 0.12)
            .AddNuts(0.25)                // hardwood trees in sheltered areas
            .AddRoots(0.2)                // edible roots
            .AddRoseHips(0.2);            // shrubs at edges
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

    /// <summary>
    /// Burnt stand - fire-damaged forest. Abundant dry fuel, charcoal, exposed sightlines.
    /// </summary>
    public static Location MakeBurntStand(Weather weather)
    {
        var location = new Location(
            name: "Burnt Stand",
            tags: "[Fuel] [Exposed] [Charcoal]",
            weather: weather,
            traversalMinutes: 10,
            terrainHazardLevel: 0.20,
            windFactor: 0.9,        // No canopy protection
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.1)  // Open sightlines
        {
            DiscoveryText = "Fire came through here. Blackened trunks stand like pillars. Ash pads your footsteps."
        };

        // Rich in dry fuel and charcoal - the pull
        var forageFeature = new ForageFeature(2.0)
            .AddCharcoal(0.8, 0.05, 0.2)    // Abundant charcoal
            .AddTinder(3.0, 0.02, 0.08)     // Dry debris everywhere
            .AddSticks(2.5, 0.2, 0.5)       // Dry branches
            .AddLogs(1.5, 1.0, 2.5);        // Standing dead wood
        location.Features.Add(forageFeature);

        // Very sparse game - little cover
        var animalTerritory = new AnimalTerritoryFeature(0.3)
            .AddRabbit(1.0);
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 12,
            terrainHazardLevel: 0.20,
            windFactor: 0.4,        // Partial wind block
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.7)
        {
            DiscoveryText = "A stone lip juts from a cliff face. The ground beneath is dry. Wind passes over but the space below is calm."
        };

        // Sparse forage - mostly stone
        var forageFeature = new ForageFeature(0.3)
            .AddStone(0.5, 0.2, 0.5)
            .AddTinder(0.2, 0.01, 0.04);
        location.Features.Add(forageFeature);

        // Partial shelter - not as good as a cave but immediate protection
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
            traversalMinutes: 14,
            terrainHazardLevel: 0.35,
            windFactor: 1.0,        // Completely exposed
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.5)  // Excellent sightlines
        {
            DiscoveryText = "Bare stone breaks through the landscape. Wind-scoured and exposed, the view is commanding. Stone flakes litter the base."
        };

        // Good stone for tools - the pull
        var forageFeature = new ForageFeature(0.8)
            .AddStone(2.0, 0.25, 0.6)
            .AddFlint(0.3, 0.1, 0.3);
        location.Features.Add(forageFeature);

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
            traversalMinutes: 22,   // Remote, high location
            terrainHazardLevel: 0.25,
            windFactor: 1.0,        // Completely exposed
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.3)
        {
            DiscoveryText = "A depression where glacial meltwater collects. Crystal clear and painfully cold. Ice rings the edges."
        };

        // Sparse alpine forage
        var forageFeature = new ForageFeature(0.3)
            .AddStone(0.3, 0.1, 0.3);
        location.Features.Add(forageFeature);

        // Water source with thin ice at edges
        var waterFeature = new WaterFeature("meltwater", "Meltwater Pool")
            .WithDescription("Glacial meltwater. Pure but frigid.")
            .WithIceThickness(0.2);  // Thin ice at edges
        location.Features.Add(waterFeature);

        // Harvestable water
        var pool = new HarvestableFeature("meltwater_pool", "Glacial Pool")
        {
            Description = "Crystal-clear meltwater. Pure but painfully cold.",
            MinutesToHarvest = 3
        };
        pool.AddWater("meltwater", maxQuantity: 20, litersPerUnit: 1.0, respawnHoursPerUnit: 24.0);
        location.Features.Add(pool);

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
            traversalMinutes: 18,
            terrainHazardLevel: 0.10,
            windFactor: 0.3,        // Dense canopy blocks wind
            overheadCoverLevel: 0.9,
            visibilityFactor: 0.4)  // Dark under canopy
        {
            DiscoveryText = "Old growth. Massive trunks, cathedral spacing, deep silence. The canopy blocks snow and light alike."
        };

        // Sparse forage - healthy forest means little deadfall
        var forageFeature = new ForageFeature(0.4)
            .AddTinder(0.3, 0.01, 0.04)
            .AddBirchPolypore(0.15, 0.05, 0.15)
            .AddChaga(0.1, 0.05, 0.2);
        location.Features.Add(forageFeature);

        // Premium hardwood - requires axe
        var hardwood = new HarvestableFeature("ancient_hardwood", "Ancient Hardwood")
        {
            Description = "Massive oak and ash. Dense, long-burning wood — if you can cut it.",
            MinutesToHarvest = 20
        };
        hardwood.AddLogs("hardwood logs", maxQuantity: 8, weightPerUnit: 4.0, respawnHoursPerUnit: 168.0);
        hardwood.RequiresTool(ToolType.Axe, ToolTier.Basic);
        location.Features.Add(hardwood);

        // Light game - deer pass through
        var animalTerritory = new AnimalTerritoryFeature(0.5)
            .AddDeer(1.0)
            .AddRabbit(0.5);
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 20,
            terrainHazardLevel: 0.30,
            windFactor: 0.9,
            overheadCoverLevel: 0.0,
            visibilityFactor: 1.2)
        {
            DiscoveryText = "Dark stripe cutting across exposed rock. Nodules of flint embedded in limestone. Sharp flakes litter the ground."
        };

        // Rich in premium flint
        var forageFeature = new ForageFeature(1.5)
            .AddFlint(1.5, 0.15, 0.4)  // High quality, abundant flint
            .AddStone(0.5, 0.2, 0.4);
        location.Features.Add(forageFeature);

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
            traversalMinutes: 8,     // Well-worn path, easy travel
            terrainHazardLevel: 0.05,
            windFactor: 0.6,
            overheadCoverLevel: 0.5,
            visibilityFactor: 0.8)
        {
            DiscoveryText = "A worn path through the brush. Hoofprints overlap in the mud. They pass through here regularly."
        };

        // Light forage
        var forageFeature = new ForageFeature(0.6)
            .AddSticks(0.5, 0.1, 0.3)
            .AddPlantFiber(0.4, 0.05, 0.15);
        location.Features.Add(forageFeature);

        // Peak hunting at dawn (5-8) and dusk (17-20)
        var animalTerritory = new AnimalTerritoryFeature(0.6)
            .AddDeer(1.5)
            .AddRabbit(1.0)
            .AddFox(0.3)
            .WithPeakHours(5, 8, 2.5);  // Dawn peak
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 20,    // Very slow movement
            terrainHazardLevel: 0.25,
            windFactor: 0.2,         // Excellent wind block
            overheadCoverLevel: 0.7,
            visibilityFactor: 0.3)   // Can't see far
        {
            DiscoveryText = "Young growth so thick you can barely push through. Branches grab at you. Small animals scatter.",
            IsEscapeTerrain = true   // Large predators can't follow
        };

        // Good small game and fiber
        var forageFeature = new ForageFeature(1.2)
            .AddPlantFiber(1.5, 0.1, 0.25)
            .AddSticks(1.0, 0.15, 0.4)
            .AddBerries(0.8, 0.05, 0.15);
        location.Features.Add(forageFeature);

        // Excellent small game - safe from large predators
        var animalTerritory = new AnimalTerritoryFeature(1.2)
            .AddRabbit(2.0)
            .AddPtarmigan(1.5);
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 18,
            terrainHazardLevel: 0.45,  // High injury risk
            windFactor: 0.7,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.9)
        {
            DiscoveryText = "Massive boulders tumbled across the slope. Gaps and crevices between them. Hard going, but wolves can't follow into the gaps.",
            IsEscapeTerrain = true,
            ClimbRiskFactor = 0.3
        };

        // Good stone
        var forageFeature = new ForageFeature(0.8)
            .AddStone(1.5, 0.3, 0.7)
            .AddFlint(0.2, 0.1, 0.25);
        location.Features.Add(forageFeature);

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
            traversalMinutes: 22,
            terrainHazardLevel: 0.35,
            windFactor: 1.2,         // Wind accelerates over ridge
            overheadCoverLevel: 0.0,
            visibilityFactor: 2.0)   // Maximum visibility
        {
            DiscoveryText = "Spine of broken stone above the treeline. Wind never stops. You can see for miles — both valley sides visible.",
            IsVantagePoint = true,
            ClimbRiskFactor = 0.4
        };

        // Sparse stone, nothing else grows here
        var forageFeature = new ForageFeature(0.3)
            .AddStone(0.8, 0.2, 0.5);
        location.Features.Add(forageFeature);

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
            traversalMinutes: 20,
            terrainHazardLevel: 0.15,
            windFactor: 0.1,         // Deep cave blocks all wind
            overheadCoverLevel: 1.0,
            visibilityFactor: 0.2)
        {
            DiscoveryText = "A deep cave mouth. Dry floor, wind-sheltered depths. But there's a smell — musk and old kills. Something lives here.",
            IsDark = true
        };

        // Bones from bear kills - the loot pull
        var forageFeature = new ForageFeature(0.8)
            .AddBone(2.0, 0.15, 0.5)     // Abundant bones from kills
            .AddTinder(0.2, 0.01, 0.03);  // Dry debris
        location.Features.Add(forageFeature);

        // Bear territory - triggers Den arc events
        // Use high-weight bear with occasional cave bear variant
        var animalTerritory = new AnimalTerritoryFeature(0.8)
            .AddBear(2.0)                // Primary occupant
            .AddAnimal("cave bear", 0.3); // Rare but terrifying
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 15,
            terrainHazardLevel: 0.25,
            windFactor: 0.6,
            overheadCoverLevel: 0.0,
            visibilityFactor: 0.9)
        {
            DiscoveryText = "A beaver dam spans the stream. The pond behind it is still and deep. Gnawed stumps litter the banks."
        };

        // Abundant chewed sticks and logs - easy fuel
        var forageFeature = new ForageFeature(2.5)
            .AddSticks(3.0, 0.2, 0.6)    // Gnawed branches everywhere
            .AddLogs(1.5, 0.8, 2.0)      // Beaver-felled logs
            .AddWillowBark(0.3);          // Willows on banks
        location.Features.Add(forageFeature);

        // Beaver pond water
        var pond = new HarvestableFeature("beaver_pond", "Beaver Pond")
        {
            Description = "Still water backed up behind the dam. Deep and clear.",
            MinutesToHarvest = 2
        };
        pond.AddWater("pond water", maxQuantity: 50, litersPerUnit: 1.0, respawnHoursPerUnit: 0.5);
        location.Features.Add(pond);

        // The dam itself - destructive harvest with consequences
        var dam = new HarvestableFeature("beaver_dam", "Beaver Dam")
        {
            Description = "Woven branches packed with mud. Easy fuel — if you don't mind the consequences.",
            MinutesToHarvest = 15
        };
        dam.AddLogs("dam logs", maxQuantity: 12, weightPerUnit: 2.0, respawnHoursPerUnit: 0);  // Never respawns
        dam.AddSticks("dam sticks", maxQuantity: 20, weightPerUnit: 0.25, respawnHoursPerUnit: 0);
        location.Features.Add(dam);

        // Small game around the pond
        var animalTerritory = new AnimalTerritoryFeature(0.7)
            .AddRabbit(1.0)
            .AddPtarmigan(0.8);
        location.Features.Add(animalTerritory);

        // Water hazard - pond edge has moderate ice
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
    /// </summary>
    public static Location MakeTheLookout(Weather weather)
    {
        var location = new Location(
            name: "The Lookout",
            tags: "[Vantage] [Climb] [Landmark]",
            weather: weather,
            traversalMinutes: 16,
            terrainHazardLevel: 0.30,
            windFactor: 0.8,
            overheadCoverLevel: 0.4,
            visibilityFactor: 1.0)   // Normal at ground, exceptional from top
        {
            DiscoveryText = "A massive lone pine stands on a rise. Its branches form a natural ladder. From up there, you could see everything — including the mountain pass.",
            IsVantagePoint = true,
            ClimbRiskFactor = 0.25   // Moderate climb risk
        };

        // Pine resources at base
        var forageFeature = new ForageFeature(0.8)
            .AddSticks(1.0, 0.15, 0.4)
            .AddTinder(0.5, 0.02, 0.06)
            .AddPineNeedles(0.3)
            .AddPineResin(0.15);
        location.Features.Add(forageFeature);

        // Some small game shelter under the tree
        var animalTerritory = new AnimalTerritoryFeature(0.4)
            .AddRabbit(0.8)
            .AddPtarmigan(0.5);
        location.Features.Add(animalTerritory);

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
            traversalMinutes: 12,
            terrainHazardLevel: 0.15,
            windFactor: 0.5,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.7)
        {
            DiscoveryText = $"A clearing where the snow is depressed. Charcoal scattered. A collapsed lean-to. Someone was here. {narrativeHook}"
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
                    salvage.Tools.Add(new Tool("Bloodied Knife", ToolType.Knife, 0.2) { Durability = 4 });
                break;
            case "fled":
                if (Utils.DetermineSuccess(0.5))
                    salvage.Tools.Add(new Tool("Dropped Hand Axe", ToolType.Axe, 0.8) { Durability = 6 });
                salvage.Resources.Add(Resource.PlantFiber,0.2);
                break;
            case "never_returned":
                salvage.Resources.Add(Resource.Log,1.5);
                salvage.Resources.Add(Resource.Log,1.5);
                salvage.Resources.Add(Resource.Log,1.5);
                if (Utils.DetermineSuccess(0.4))
                    salvage.Equipment.Add(new Equipment("Cached Coat", EquipSlot.Chest, 1.8, 0.12));
                break;
            case "counted_days":
                salvage.Resources.Add(Resource.Stone,0.3);
                if (Utils.DetermineSuccess(0.3))
                    salvage.Tools.Add(new Tool("Worn Hand Drill", ToolType.HandDrill, 0.25) { Durability = 5 });
                break;
            default:
                salvage.Resources.Add(Resource.PlantFiber,0.15);
                break;
        }
        location.Features.Add(salvage);

        // Collapsed shelter can be repaired
        location.Features.Add(new ShelterFeature("Collapsed Lean-to", 0.15, 0.3, 0.25));

        // Sparse forage - area picked over
        var forageFeature = new ForageFeature(0.3)
            .AddSticks(0.3, 0.1, 0.2)
            .AddTinder(0.2, 0.01, 0.03);
        location.Features.Add(forageFeature);

        return location;
    }

    #endregion


    #region Path Factories

    // public static Location MakeForestPath(
    //     Weather weather,
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
    //     Weather weather,
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
    //     Weather weather,
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
    //     Weather weather,
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
