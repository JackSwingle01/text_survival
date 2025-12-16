using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Environments.Factories;

public static class LocationFactory
{
    #region Site Factories

    public static Location MakeForest(Zone parent)
    {
        List<string> forestNames = ["Forest", "Woodland", "Grove", "Thicket", "Pine Stand", "Birch Grove"];
        List<string> forestAdjectives = [
            "Frost-bitten", "Snow-laden", "Ice-coated", "Permafrost", "Glacial", "Silent", "Frozen", "Snowy",
            "Windswept", "Frigid", "Boreal", "Primeval", "Shadowy", "Ancient", "Taiga",
            "Frosty", "Dark", "Foggy", "Overgrown", "Dense", "Old", "Misty", "Quiet",
            "Pristine", "Forgotten", "Cold", "Verdant", "Mossy", "Wet"
        ];

        string adjective = Utils.GetRandomFromList(forestAdjectives);
        string name = Utils.GetRandomFromList(forestNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, parent)
        {
            Description = "A stretch of snow-covered trees offering some shelter from the wind.",
            Exposure = 0.4,
            Terrain = TerrainType.Clear,
            
        };

        // Forage feature
        var forageFeature = new ForageFeature(1.6);
        forageFeature.AddResource(ItemFactory.MakeBerry, .5);
        forageFeature.AddResource(ItemFactory.MakeWater, .5);
        forageFeature.AddResource(ItemFactory.MakeMushroom, .4);
        forageFeature.AddResource(ItemFactory.MakeStick, 1.0);
        forageFeature.AddResource(ItemFactory.MakeFirewood, .4);
        forageFeature.AddResource(ItemFactory.MakeRoots, .3);
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.1);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 0.3);
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.7);
        forageFeature.AddResource(ItemFactory.MakeBarkStrips, 0.9);
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.8);
        forageFeature.AddResource(ItemFactory.MakeTinderBundle, 0.2);
        forageFeature.AddResource(ItemFactory.MakeNuts, 0.3);
        forageFeature.AddResource(ItemFactory.MakeGrubs, 0.4);
        forageFeature.AddResource(ItemFactory.MakeEggs, 0.2);
        location.Features.Add(forageFeature);

        // Environment feature
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Forest));

        // Harvestables (spawn chance)
        if (Utils.DetermineSuccess(0.3))
        {
            var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush")
            {
                Description = "A frost-hardy shrub with clusters of dark berries."
            };
            berryBush.AddResource(ItemFactory.MakeBerry, maxQuantity: 5, respawnHoursPerUnit: 168.0);
            berryBush.AddResource(ItemFactory.MakeStick, maxQuantity: 2, respawnHoursPerUnit: 72.0);
            location.Features.Add(berryBush);
        }

        if (Utils.DetermineSuccess(0.2))
        {
            var willowStand = new HarvestableFeature("willow_stand", "Arctic Willow Stand")
            {
                Description = "A dense cluster of low-growing willow shrubs with flexible branches."
            };
            willowStand.AddResource(ItemFactory.MakePlantFibers, maxQuantity: 8, respawnHoursPerUnit: 48.0);
            willowStand.AddResource(ItemFactory.MakeBarkStrips, maxQuantity: 4, respawnHoursPerUnit: 72.0);
            willowStand.AddResource(ItemFactory.MakeHealingHerbs, maxQuantity: 2, respawnHoursPerUnit: 96.0);
            location.Features.Add(willowStand);
        }

        if (Utils.DetermineSuccess(0.15))
        {
            var sapSeep = new HarvestableFeature("pine_sap_seep", "Pine Sap Seep")
            {
                Description = "Thick golden resin oozes from a crack in the pine bark."
            };
            sapSeep.AddResource(ItemFactory.MakePineSap, maxQuantity: 4, respawnHoursPerUnit: 168.0);
            sapSeep.AddResource(ItemFactory.MakeTinderBundle, maxQuantity: 1, respawnHoursPerUnit: 240.0);
            location.Features.Add(sapSeep);
        }

        if (Utils.DetermineSuccess(0.3))
        {
            var puddle = new HarvestableFeature("puddle", "Forest Puddle")
            {
                Description = "A shallow puddle fed by melting snow."
            };
            puddle.AddResource(ItemFactory.MakeWater, maxQuantity: 2, respawnHoursPerUnit: 12.0);
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeCave(Zone parent)
    {
        List<string> caveNames = ["Cave", "Cavern", "Grotto", "Hollow", "Shelter"];
        List<string> caveAdjectives = [
            "Icicle-lined", "Frost-rimmed", "Ice-floored", "Bone-strewn", "Mammoth-bone", "Winding", "Ancient",
            "Hidden", "Ancestral", "Painted", "Rocky", "Echoing", "Ice-walled", "Hibernation",
            "Crystal-ice", "Glacier-carved", "Frosty", "Icy", "Dark", "Shadowy", "Damp", "Deep",
            "Frozen", "Narrow", "Secluded", "Granite", "Glowing", "Cold", "Crystal", "Protected"
        ];

        string adjective = Utils.GetRandomFromList(caveAdjectives);
        string name = Utils.GetRandomFromList(caveNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, parent)
        {
            Description = "A sheltered opening in the rock, protected from the worst of the elements.",
            Exposure = 0.1,
            Terrain = TerrainType.Clear,
            
        };

        // Forage feature - caves have fewer organics
        var forageFeature = new ForageFeature(0.8);
        forageFeature.AddResource(ItemFactory.MakeMushroom, 3.0);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 5.0);
        forageFeature.AddResource(ItemFactory.MakeFlint, 2.0);
        forageFeature.AddResource(ItemFactory.MakeClay, 1.0);
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.3);
        forageFeature.AddResource(ItemFactory.MakeHandstone, 0.4);
        forageFeature.AddResource(ItemFactory.MakeSharpStone, 0.3);
        location.Features.Add(forageFeature);

        // Environment feature
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Cave));

        // Shelter feature - caves provide natural shelter
        location.Features.Add(new ShelterFeature(.5, 1, .9));

        return location;
    }

    public static Location MakeRiverbank(Zone parent)
    {
        List<string> riverNames = ["River", "Stream", "Creek", "Brook", "Rapids", "Ford", "Shallows"];
        List<string> riverAdjectives = [
            "Ice-rimmed", "Glacial", "Snowmelt", "Half-frozen", "Ice-flow", "Narrow", "Mammoth-crossing",
            "Frozen-edged", "Icy", "Slush-filled", "Ice-bridged", "Cold", "Mist-shrouded", "Foggy",
            "Glacier-fed", "Thawing", "Crystalline", "Frigid", "Quiet", "Thundering", "Glistening",
            "Rushing", "Flowing", "Clear", "Muddy", "Wide", "Rocky", "Sandy", "Shallow", "Deep"
        ];

        string adjective = Utils.GetRandomFromList(riverAdjectives);
        string name = Utils.GetRandomFromList(riverNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, parent)
        {
            Description = "A riverbank with access to water and good foraging along the muddy shore.",
            Exposure = 0.6,
            Terrain = TerrainType.Clear,
            
        };

        // Forage feature
        var forageFeature = new ForageFeature(1.1);
        forageFeature.AddResource(ItemFactory.MakeWater, 10.0);
        forageFeature.AddResource(ItemFactory.MakeFish, 6.0);
        forageFeature.AddResource(ItemFactory.MakeRoots, 4.0);
        forageFeature.AddResource(ItemFactory.MakeClay, 5.0);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 5.0);
        forageFeature.AddResource(ItemFactory.MakeFlint, 1.0);
        forageFeature.AddResource(ItemFactory.MakeRushes, 0.8);
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.3);
        location.Features.Add(forageFeature);

        // Environment feature
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.RiverBank));

        // Water source harvestable
        if (Utils.DetermineSuccess(0.7))
        {
            var river = new HarvestableFeature("river", "Ice-Fed River")
            {
                Description = "A swift-flowing river fed by glacial meltwater. Cold but clear."
            };
            river.AddResource(ItemFactory.MakeWater, maxQuantity: 100, respawnHoursPerUnit: 0.1);
            river.AddResource(ItemFactory.MakeFish, maxQuantity: 8, respawnHoursPerUnit: 24.0);
            river.AddResource(ItemFactory.MakeClay, maxQuantity: 6, respawnHoursPerUnit: 48.0);
            location.Features.Add(river);
        }
        else
        {
            var stream = new HarvestableFeature("stream", "Mountain Stream")
            {
                Description = "A narrow stream over smooth stones. Crystal clear and icy cold."
            };
            stream.AddResource(ItemFactory.MakeWater, maxQuantity: 10, respawnHoursPerUnit: 1.0);
            stream.AddResource(ItemFactory.MakeFish, maxQuantity: 3, respawnHoursPerUnit: 48.0);
            stream.AddResource(ItemFactory.MakeSmallStone, maxQuantity: 5, respawnHoursPerUnit: 72.0);
            location.Features.Add(stream);
        }

        return location;
    }

    public static Location MakePlain(Zone parent)
    {
        List<string> plainNames = ["Plain", "Steppe", "Tundra", "Grassland", "Prairie", "Meadow"];
        List<string> plainAdjectives = [
            "Windswept", "Permafrost", "Glacial", "Frozen", "Vast", "Rolling", "Endless", "Mammoth-trampled",
            "Snow-covered", "Ice-plain", "Desolate", "Frosty", "Exposed", "Bison-grazed",
            "Bleak", "Stark", "Harsh", "Woolly", "Flat", "Frost-cracked",
            "Open", "Windy", "Cold", "Barren", "Grassy", "Empty", "Rocky", "Wild"
        ];

        string adjective = Utils.GetRandomFromList(plainAdjectives);
        string name = Utils.GetRandomFromList(plainNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, parent)
        {
            Description = "Open terrain with little shelter from the wind, but good visibility.",
            Exposure = 0.9,
            Terrain = TerrainType.Clear,
            
        };

        // Forage feature - sparse
        var forageFeature = new ForageFeature(0.7);
        forageFeature.AddResource(ItemFactory.MakeRoots, 6.0);
        forageFeature.AddResource(ItemFactory.MakeBerry, 2.0);
        forageFeature.AddResource(ItemFactory.MakeStick, 1.0);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 4.0);
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.5);
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.8);
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.4);
        location.Features.Add(forageFeature);

        // Environment feature
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.OpenPlain));

        // Occasional water
        if (Utils.DetermineSuccess(0.3))
        {
            var puddle = new HarvestableFeature("puddle", "Meltwater Puddle")
            {
                Description = "A shallow depression with fresh meltwater. Frozen at edges."
            };
            puddle.AddResource(ItemFactory.MakeWater, maxQuantity: 2, respawnHoursPerUnit: 12.0);
            location.Features.Add(puddle);
        }

        return location;
    }

    public static Location MakeHillside(Zone parent)
    {
        List<string> hillNames = ["Ridge", "Moraine", "Slope", "Drift", "Crag", "Bluff", "Outcrop", "Hill", "Knoll"];
        List<string> hillAdjectives = [
            "Glacier-carved", "Ice-cracked", "Snow-swept", "Wind-scoured", "Ice-exposed", "Frost-heaved", "Craggy",
            "Rugged", "Snow-capped", "Icy", "Ice-scarred", "Stone", "High", "Misty", "Frost-shattered",
            "Eroded", "Ancient", "Granite", "Shaded", "Splintered",
            "Rocky", "Steep", "Gentle", "Windswept", "Exposed", "Barren", "Weathered"
        ];

        string adjective = Utils.GetRandomFromList(hillAdjectives);
        string name = Utils.GetRandomFromList(hillNames);
        name = (adjective + " " + name).Trim();

        var location = new Location(name, parent)
        {
            Description = "Rocky terrain with good stone resources and a view of the surroundings.",
            Exposure = 0.7,
            Terrain = TerrainType.Steep,
            
        };

        // Forage feature - stone-heavy
        var forageFeature = new ForageFeature(0.9);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 8.0);
        forageFeature.AddResource(ItemFactory.MakeFlint, 3.0);
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.5);
        forageFeature.AddResource(ItemFactory.MakeRoots, 2.0);
        forageFeature.AddResource(ItemFactory.MakeOchrePigment, 1.0);
        forageFeature.AddResource(ItemFactory.MakeHandstone, 0.5);
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.4);
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.3);
        location.Features.Add(forageFeature);

        // Environment feature
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Cliff));

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

        var location = new Location(name, parent)
        {
            Description = "A natural clearing among the trees, somewhat sheltered from the wind.",
            Exposure = 0.4,
            Terrain = TerrainType.Clear,
        };

        // Moderate foraging
        var forageFeature = new ForageFeature(1.0);
        forageFeature.AddResource(ItemFactory.MakeBerry, 0.4);
        forageFeature.AddResource(ItemFactory.MakeStick, 0.8);
        forageFeature.AddResource(ItemFactory.MakeFirewood, 0.5);
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.6);
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.5);
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 0.3);
        location.Features.Add(forageFeature);

        // Environment - use Forest since clearings are typically in forests
        location.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Forest));

        return location;
    }

    #endregion

    #region Path Factories

    public static Location MakeForestPath(
        Zone parent,
        string? name = null,
        int traversalMinutes = 10,
        TerrainType terrain = TerrainType.Clear,
        double exposure = 0.5)
    {
        if (name == null)
        {
            List<string> pathNames = ["Trail", "Path", "Track", "Way", "Route"];
            List<string> pathAdjectives = [
                "Winding", "Narrow", "Overgrown", "Animal", "Deer", "Mossy",
                "Snow-covered", "Icy", "Muddy", "Rocky", "Faint", "Well-worn"
            ];
            string adjective = Utils.GetRandomFromList(pathAdjectives);
            string baseName = Utils.GetRandomFromList(pathNames);
            name = (adjective + " " + baseName).Trim();
        }

        var location = new Location(name, parent)
        {
            Description = "A path winding through the trees.",
            BaseTraversalMinutes = traversalMinutes,
            Exposure = exposure,
            Terrain = terrain,
            
        };

        return location;
    }

    public static Location MakeOpenPath(
        Zone parent,
        string? name = null,
        int traversalMinutes = 15,
        TerrainType terrain = TerrainType.Clear,
        double exposure = 0.8)
    {
        if (name == null)
        {
            List<string> pathNames = ["Stretch", "Expanse", "Crossing", "Run", "Passage"];
            List<string> pathAdjectives = [
                "Open", "Windswept", "Exposed", "Snowy", "Icy", "Frozen",
                "Bleak", "Long", "Treacherous", "Featureless"
            ];
            string adjective = Utils.GetRandomFromList(pathAdjectives);
            string baseName = Utils.GetRandomFromList(pathNames);
            name = (adjective + " " + baseName).Trim();
        }

        var location = new Location(name, parent)
        {
            Description = "An exposed stretch with little shelter from the elements.",
            BaseTraversalMinutes = traversalMinutes,
            Exposure = exposure,
            Terrain = terrain,
            
        };

        return location;
    }

    public static Location MakeSteepPath(
        Zone parent,
        string? name = null,
        int traversalMinutes = 12,
        double exposure = 0.7)
    {
        if (name == null)
        {
            List<string> pathNames = ["Climb", "Ascent", "Descent", "Scramble", "Slope"];
            List<string> pathAdjectives = [
                "Steep", "Rocky", "Treacherous", "Slippery", "Icy",
                "Narrow", "Dangerous", "Loose", "Crumbling"
            ];
            string adjective = Utils.GetRandomFromList(pathAdjectives);
            string baseName = Utils.GetRandomFromList(pathNames);
            name = (adjective + " " + baseName).Trim();
        }

        var location = new Location(name, parent)
        {
            Description = "A difficult climb over rocky terrain.",
            BaseTraversalMinutes = traversalMinutes,
            Exposure = exposure,
            Terrain = TerrainType.Steep,
        };

        return location;
    }

    public static Location MakeRoughPath(
        Zone parent,
        string? name = null,
        int traversalMinutes = 12,
        double exposure = 0.5)
    {
        if (name == null)
        {
            List<string> pathNames = ["Trail", "Path", "Way", "Passage", "Route"];
            List<string> pathAdjectives = [
                "Overgrown", "Rough", "Tangled", "Blocked", "Difficult",
                "Brush-choked", "Root-tangled", "Boulder-strewn"
            ];
            string adjective = Utils.GetRandomFromList(pathAdjectives);
            string baseName = Utils.GetRandomFromList(pathNames);
            name = (adjective + " " + baseName).Trim();
        }

        var location = new Location(name, parent)
        {
            Description = "Difficult terrain that slows progress.",
            BaseTraversalMinutes = traversalMinutes,
            Exposure = exposure,
            Terrain = TerrainType.Rough,
            
        };

        return location;
    }

    #endregion
}