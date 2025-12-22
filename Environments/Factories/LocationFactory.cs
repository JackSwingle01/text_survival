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
