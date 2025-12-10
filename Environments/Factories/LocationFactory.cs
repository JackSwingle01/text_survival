using text_survival.Actors.NPCs;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Environments.Factories;

public static class LocationFactory
{
    public static Location MakeForest(Zone parent)
    {
        // Create a base forest location
        Location location = new Location("Forest", parent);

        // Generate a more descriptive name with ice age theme
        List<string> forestNames = ["Forest", "Woodland", "Grove", "Thicket", "Pine Stand", "Birch Grove"];
        List<string> forestAdjectives = [
            "Frost-bitten", "Snow-laden", "Ice-coated", "Permafrost", "Glacial", "Silent", "Frozen", "Snowy",
            "Windswept", "Frigid", "Boreal", "Primeval", "Shadowy", "Ancient", "Taiga",
             "Frosty", "Dark", "Foggy", "Overgrown",
            "Dense", "Old", "Misty", "Quiet", "Pristine", "Forgotten", "Cold", "Verdant", "Mossy", "Wet"
        ];

        // Pick a random adjective and name
        string adjective = Utils.GetRandomFromList(forestAdjectives);
        string name = Utils.GetRandomFromList(forestNames);
        location.Name = (adjective + " " + name).Trim();

        // Create a ForageFeature with high resource density for forests (1.6 - buffed for better yields)
        ForageFeature forageFeature = new ForageFeature(location, 1.6);

        // Add natural resources to the forage feature - more forest-appropriate items
        forageFeature.AddResource(ItemFactory.MakeBerry, .5);     // Common
        forageFeature.AddResource(ItemFactory.MakeWater, .5);     // Available but not as common
        forageFeature.AddResource(ItemFactory.MakeMushroom, .4);  // Common in forests
        forageFeature.AddResource(ItemFactory.MakeStick, 1.0);    // Very common (buffed)
        forageFeature.AddResource(ItemFactory.MakeFirewood, .4);   // Common
        forageFeature.AddResource(ItemFactory.MakeRoots, .3);     // Fairly common
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.1);     // Rare
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 0.3);  // Uncommon (for Sharp Rock crafting)
        // Phase 2: New fire-starting and cordage materials
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.7);      // Common (buffed)
        forageFeature.AddResource(ItemFactory.MakeBarkStrips, 0.9);    // Very common (trees!) (buffed)
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.8);   // Common (buffed)
        forageFeature.AddResource(ItemFactory.MakeTinderBundle, 0.2);  // Rare (prepared)
        // Phase 3: Additional food items for improved early-game survival
        forageFeature.AddResource(ItemFactory.MakeNuts, 0.3);    // Fairly common in forests
        forageFeature.AddResource(ItemFactory.MakeGrubs, 0.4);   // Common under bark/logs
        forageFeature.AddResource(ItemFactory.MakeEggs, 0.2);    // Uncommon (bird nests)

        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);

        // Add an environment feature for forest
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Forest));

        // Add harvestable features (discoverable resource nodes)
        // Berry Bush - reliable food source (30% spawn chance)
        if (Utils.DetermineSuccess(0.3))
        {
            var berryBush = new HarvestableFeature("berry_bush", "Wild Berry Bush", location)
            {
                Description = "A frost-hardy shrub with clusters of dark berries. The branches are thin but flexible."
            };
            berryBush.AddResource(ItemFactory.MakeBerry, maxQuantity: 5, respawnHoursPerUnit: 168.0); // 1 week
            berryBush.AddResource(ItemFactory.MakeStick, maxQuantity: 2, respawnHoursPerUnit: 72.0);  // 3 days
            location.Features.Add(berryBush);
        }

        // Willow Stand - crafting materials hub (20% spawn chance)
        if (Utils.DetermineSuccess(0.2))
        {
            var willowStand = new HarvestableFeature("willow_stand", "Arctic Willow Stand", location)
            {
                Description = "A dense cluster of low-growing willow shrubs. The thin branches are remarkably flexible, and the bark has medicinal properties."
            };
            willowStand.AddResource(ItemFactory.MakePlantFibers, maxQuantity: 8, respawnHoursPerUnit: 48.0);   // 2 days
            willowStand.AddResource(ItemFactory.MakeBarkStrips, maxQuantity: 4, respawnHoursPerUnit: 72.0);     // 3 days
            willowStand.AddResource(ItemFactory.MakeHealingHerbs, maxQuantity: 2, respawnHoursPerUnit: 96.0);  // 4 days
            location.Features.Add(willowStand);
        }

        // Pine Sap Seep - advanced crafting resource (15% spawn chance)
        if (Utils.DetermineSuccess(0.15))
        {
            var sapSeep = new HarvestableFeature("pine_sap_seep", "Pine Sap Seep", location)
            {
                Description = "Thick golden resin oozes from a crack in the pine bark. The air smells sharp and piney."
            };
            sapSeep.AddResource(ItemFactory.MakePineSap, maxQuantity: 4, respawnHoursPerUnit: 168.0);         // 7 days
            sapSeep.AddResource(ItemFactory.MakeTinderBundle, maxQuantity: 1, respawnHoursPerUnit: 240.0);    // 10 days
            location.Features.Add(sapSeep);
        }

        // Puddle - small water source (30% spawn chance)
        if (Utils.DetermineSuccess(0.3))
        {
            var puddle = new HarvestableFeature("puddle", "Forest Puddle", location)
            {
                Description = "A shallow puddle fed by melting snow. The water is clear and cold."
            };
            puddle.AddResource(ItemFactory.MakeWater, maxQuantity: 2, respawnHoursPerUnit: 12.0);  // 12 hours
            location.Features.Add(puddle);
        }

        // Configure the NPC spawner - MVP Hunting System
        // Prey animals more common than predators for viable hunting
        NpcTable npcSpawner = new NpcTable();

        // Prey animals (common) - primary hunting targets
        npcSpawner.AddActor(NpcFactory.MakeDeer, 4.0);       // Common - good meat, moderate difficulty
        npcSpawner.AddActor(NpcFactory.MakeRabbit, 5.0);     // Very common - easy target for beginners
        npcSpawner.AddActor(NpcFactory.MakePtarmigan, 3.0);  // Common - small game
        npcSpawner.AddActor(NpcFactory.MakeFox, 2.0);        // Uncommon - scavenger behavior

        // Predators (less common) - dangerous when hunting fails
        npcSpawner.AddActor(NpcFactory.MakeWolf, 2.0);       // Uncommon - pack hunter
        npcSpawner.AddActor(NpcFactory.MakeBear, 0.5);       // Rare - very dangerous

        location.NpcSpawner = npcSpawner;

        // Determine if we should add NPCs initially (60% chance - increased for hunting)
        if (Utils.DetermineSuccess(.6))
        {
            // Add 1-3 NPCs from the spawner (increased range for more hunting opportunities)
            int npcCount = Utils.RandInt(1, 3);
            location.SpawnNpcs(npcCount);
        }

        return location;
    }

    public static Location MakeCave(Zone parent)
    {
        // Create a base cave location
        Location location = new Location("Cave", parent);

        // Generate a more descriptive name with ice age theme
        List<string> caveNames = ["Cave", "Cavern", "Grotto", "Hollow", "Shelter"];
        List<string> caveAdjectives = [
            "Icicle-lined", "Frost-rimmed", "Ice-floored", "Bone-strewn", "Mammoth-bone", "Winding", "Ancient",
            "Hidden", "Ancestral", "Painted", "Rocky", "Echoing", "Ice-walled", "Hibernation",
            "Crystal-ice", "Glacier-carved", "Frosty", "Icy",
            "Dark", "Shadowy", "Damp", "Deep", "Frozen", "Narrow", "Secluded",
            "Granite",  "Glowing",  "Cold", "Crystal", "Protected"
        ];

        // Pick a random adjective and name
        string adjective = Utils.GetRandomFromList(caveAdjectives);
        string name = Utils.GetRandomFromList(caveNames);
        location.Name = (adjective + " " + name).Trim();

        // Create a ForageFeature with moderate resource density for caves (0.8)
        ForageFeature forageFeature = new ForageFeature(location, 0.8);

        // Add resources to the forage feature - cave-appropriate items
        forageFeature.AddResource(ItemFactory.MakeMushroom, 3.0);  // Can find mushrooms in caves
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 5.0);     // Very common
        forageFeature.AddResource(ItemFactory.MakeFlint, 2.0);     // More common in caves
        forageFeature.AddResource(ItemFactory.MakeClay, 1.0);      // Near cave entrances
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.3); // Rare but valuable
        // Phase 2: Stone variety - NO organics (intentional challenge)
        forageFeature.AddResource(ItemFactory.MakeHandstone, 0.4);     // Moderate
        forageFeature.AddResource(ItemFactory.MakeSharpStone, 0.3);    // Uncommon

        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);

        // Add an environment feature for cave
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Cave));

        // Initial items hidden until foraging - items only revealed through Forage action
        // int itemCount = Utils.RandInt(1, 3);
        // for (int i = 0; i < itemCount; i++)
        // {
        //     Item item = GetRandomCaveItem();
        //     location.Items.Add(item);
        // }

        // Configure the NPC spawner
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeSpider, 3.0);
        npcSpawner.AddActor(NpcFactory.MakeRat, 3.0);
        npcSpawner.AddActor(NpcFactory.MakeSnake, 1.0);
        npcSpawner.AddActor(NpcFactory.MakeBat, 4.0);
        npcSpawner.AddActor(NpcFactory.MakeCaveBear, 0.5);
        location.NpcSpawner = npcSpawner;

        // Determine if we should add NPCs initially (50% chance)
        if (Utils.RandInt(0, 9) < 5)
        {
            // Add 1-2 NPCs from the spawner
            int npcCount = Utils.RandInt(1, 2);
            location.SpawnNpcs(npcCount);
        }

        return location;
    }

    public static Location MakeRiverbank(Zone parent)
    {
        // Create a base riverbank location
        Location location = new Location("Riverbank", parent);

        // Generate a more descriptive name with ice age theme
        List<string> riverNames = ["River", "Stream", "Creek", "Brook", "Rapids", "Ford", "Ice-Melt", "Waterfall", "Shallows"];
        List<string> riverAdjectives = [
            "Ice-rimmed", "Glacial", "Snowmelt", "Half-frozen", "Ice-flow", "Narrow", "Mammoth-crossing",
            "Frozen-edged", "Icy", "Slush-filled", "Ice-bridged", "Cold", "Mist-shrouded", "Foggy", "Glacier-fed",
            "Thawing", "Crystalline", "Ice-dammed", "Frigid", "Quiet", "Thundering", "Bone-strewn", "Glistening",
            "Rushing", "Flowing", "Clear", "Muddy", "Wide", "Rocky", "Sandy", "Shallow", "Deep",
            "Misty", "Meandering", "Winding", "Fast-flowing", "Gentle", "Noisy", "Bubbling"
        ];

        // Pick a random adjective and name
        string adjective = Utils.GetRandomFromList(riverAdjectives);
        string name = Utils.GetRandomFromList(riverNames);
        location.Name = (adjective + " " + name).Trim();

        // Create a ForageFeature with good resource density for riverbanks (1.1)
        ForageFeature forageFeature = new ForageFeature(location, 1.1);

        // Add resources to the forage feature - river-appropriate items
        forageFeature.AddResource(ItemFactory.MakeWater, 10.0);    // Very abundant
        forageFeature.AddResource(ItemFactory.MakeFish, 6.0);      // Common
        forageFeature.AddResource(ItemFactory.MakeRoots, 4.0);     // Common near water
        forageFeature.AddResource(ItemFactory.MakeClay, 5.0);      // Common at riverbanks
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 5.0);     // River stones
        forageFeature.AddResource(ItemFactory.MakeFlint, 1.0);     // Occasionally found
        // Phase 2: Wetland plants
        forageFeature.AddResource(ItemFactory.MakeRushes, 0.8);        // Very common (water plants)
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.3);      // Uncommon (wet area)

        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);

        // Add an environment feature for riverbank
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.RiverBank));

        // Water sources - stream or river (mutually exclusive)
        if (Utils.DetermineSuccess(0.7))
        {
            // River - large water source (70% chance)
            var river = new HarvestableFeature("river", "Ice-Fed River", location)
            {
                Description = "A wide, swift-flowing river fed by glacial meltwater. The current is strong but the water is clear and cold."
            };
            river.AddResource(ItemFactory.MakeWater, maxQuantity: 100, respawnHoursPerUnit: 0.1);  // Effectively infinite
            river.AddResource(ItemFactory.MakeFish, maxQuantity: 8, respawnHoursPerUnit: 24.0);    // 1 day
            river.AddResource(ItemFactory.MakeClay, maxQuantity: 6, respawnHoursPerUnit: 48.0);    // 2 days (silt deposits)
            location.Features.Add(river);
        }
        else if (Utils.DetermineSuccess(0.3))
        {
            // Stream - moderate water source (30% chance of remaining 30%)
            var stream = new HarvestableFeature("stream", "Mountain Stream", location)
            {
                Description = "A narrow stream tumbling over smooth stones. The water is crystal clear and icy cold."
            };
            stream.AddResource(ItemFactory.MakeWater, maxQuantity: 10, respawnHoursPerUnit: 1.0);     // 1 hour
            stream.AddResource(ItemFactory.MakeFish, maxQuantity: 3, respawnHoursPerUnit: 48.0);      // 2 days
            stream.AddResource(ItemFactory.MakeSmallStone, maxQuantity: 5, respawnHoursPerUnit: 72.0); // 3 days (river stones)
            location.Features.Add(stream);
        }

        // Initial items hidden until foraging - items only revealed through Forage action
        // int itemCount = Utils.RandInt(2, 4);
        // for (int i = 0; i < itemCount; i++)
        // {
        //     Item item = GetRandomRiverbankItem();
        //     location.Items.Add(item);
        // }

        // Configure the NPC spawner for riverbanks
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 2.0);   // Predators come to water
        npcSpawner.AddActor(NpcFactory.MakeBear, 1.0);   // Bears fish at rivers
        location.NpcSpawner = npcSpawner;

        // Determine if we should add NPCs initially (30% chance)
        if (Utils.DetermineSuccess(.3))
        {
            location.SpawnNpcs(1);
        }

        return location;
    }

    public static Location MakePlain(Zone parent)
    {
        // Create a base plains location
        Location location = new Location("Plain", parent);

        // Generate a more descriptive name with ice age theme
        List<string> plainNames = ["Plain", "Steppe", "Tundra", "Mammoth Grounds", "Permafrost", "Glacier-edge", "Grassland", "Prairie", "Meadow"];
        List<string> plainAdjectives = [
            "Windswept", "Permafrost", "Glacial", "Frozen", "Vast", "Rolling", "Endless", "Mammoth-trampled",
            "Snow-covered", "Ice-plain", "Desolate", "Frosty", "Exposed", "Bison-grazed",
            "Bleak", "Stark", "Harsh", "Woolly", "Flat", "Frost-cracked", "Mammoth",
            "Open", "Windy", "Cold", "Barren", "Grassy", "Empty", "Rocky", "Wild"
        ];

        // Pick a random adjective and name
        string adjective = Utils.GetRandomFromList(plainAdjectives);
        string name = Utils.GetRandomFromList(plainNames);
        location.Name = (adjective + " " + name).Trim();

        // Create a ForageFeature with low-moderate resource density for plains (0.7)
        ForageFeature forageFeature = new ForageFeature(location, 0.7);

        // Add resources to the forage feature - plains-appropriate items
        forageFeature.AddResource(ItemFactory.MakeRoots, 6.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeBerry, 2.0);     // Less common
        forageFeature.AddResource(ItemFactory.MakeStick, 1.0);     // Rare (few trees)
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 4.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.5);     // Rare
        // Phase 2: Grassland materials
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.8);      // Very common (grassland!)
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.4);   // Moderate

        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);

        // Add an environment feature for open plain
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.OpenPlain));

        // Puddle - small water source (30% spawn chance)
        if (Utils.DetermineSuccess(0.3))
        {
            var puddle = new HarvestableFeature("puddle", "Meltwater Puddle", location)
            {
                Description = "A shallow depression filled with fresh meltwater. Frozen at the edges but drinkable in the center."
            };
            puddle.AddResource(ItemFactory.MakeWater, maxQuantity: 2, respawnHoursPerUnit: 12.0);  // 12 hours
            location.Features.Add(puddle);
        }

        // Initial items hidden until foraging - items only revealed through Forage action
        // int itemCount = Utils.RandInt(1, 3);
        // for (int i = 0; i < itemCount; i++)
        // {
        //     Item item = GetRandomPlainsItem();
        //     location.Items.Add(item);
        // }

        // Configure the NPC spawner - plains have megafauna!
        var npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 3.0);               // Common
        npcSpawner.AddActor(NpcFactory.MakeWoollyMammoth, 0.5);      // Rare but possible
        npcSpawner.AddActor(NpcFactory.MakeSaberToothTiger, 0.7);    // Uncommon
        location.NpcSpawner = npcSpawner;

        // Determine if we should add NPCs initially (40% chance)
        if (Utils.DetermineSuccess(.4))
        {
            location.SpawnNpcs(1);
        }

        return location;
    }

    public static Location MakeHillside(Zone parent)
    {
        // Create a base hillside location
        Location location = new Location("Hillside", parent);

        // Generate a more descriptive name with ice age theme
        List<string> hillNames = ["Ridge", "Moraine", "Slope", "Drift", "Crag", "Bluff", "Outcrop", "Hill", "Hillside", "Knoll"];
        List<string> hillAdjectives = [
            "Glacier-carved", "Ice-cracked", "Snow-swept", "Wind-scoured", "Ice-exposed", "Frost-heaved", "Craggy",
            "Rugged", "Snow-capped", "Icy", "Ice-scarred", "Stone", "High", "Misty", "Frost-shattered",
            "Eroded", "Ancient", "Mammoth-trail", "Granite", "Shaded", "Splintered",
            "Rocky", "Steep", "Gentle", "Windswept", "Exposed", "Barren", "Weathered",
            "Protected", "Treacherous", "Cold", "Foggy"
        ];

        // Pick a random adjective and name
        string adjective = Utils.GetRandomFromList(hillAdjectives);
        string name = Utils.GetRandomFromList(hillNames);
        location.Name = (adjective + " " + name).Trim();

        // Create a ForageFeature with moderate resource density for hillsides (0.9)
        ForageFeature forageFeature = new ForageFeature(location, 0.9);

        // Add resources to the forage feature - hillside-appropriate items
        forageFeature.AddResource(ItemFactory.MakeSmallStone, 8.0);        // Very common
        forageFeature.AddResource(ItemFactory.MakeFlint, 3.0);        // More common on hillsides
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.5); // Rare but possible
        forageFeature.AddResource(ItemFactory.MakeRoots, 2.0);         // Less common
        forageFeature.AddResource(ItemFactory.MakeOchrePigment, 1.0);  // Sometimes found on hills
        // Phase 2: Balanced stone/organic mix
        forageFeature.AddResource(ItemFactory.MakeHandstone, 0.5);     // Common
        forageFeature.AddResource(ItemFactory.MakeDryGrass, 0.4);      // Moderate
        forageFeature.AddResource(ItemFactory.MakePlantFibers, 0.3);   // Uncommon

        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);

        // Add an environment feature for hillside (using cliff as closest match)
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Cliff));

        // Initial items hidden until foraging - items only revealed through Forage action
        // int itemCount = Utils.RandInt(2, 4);
        // for (int i = 0; i < itemCount; i++)
        // {
        //     Item item = GetRandomHillsideItem();
        //     location.Items.Add(item);
        // }

        // Configure the NPC spawner for hillsides
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 1.0);
        npcSpawner.AddActor(NpcFactory.MakeSnake, 2.0);    // Snakes like rocky areas
        location.NpcSpawner = npcSpawner;

        // Determine if we should add NPCs initially (30% chance)
        if (Utils.DetermineSuccess(.3))
        {
            location.SpawnNpcs(1);
        }

        return location;
    }

    // Helper methods to generate random location-appropriate items

    private static Item GetRandomForestItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeMushroom, 5.0 },
            { ItemFactory.MakeBerry, 4.0 },
            { ItemFactory.MakeStick, 8.0 },
            { ItemFactory.MakeFirewood, 5.0 },
            { ItemFactory.MakeHealingHerbs, 1.0 }
            // Removed: MakeTorch (crafted), MakeSpear (crafted weapon)
        };

        return Utils.GetRandomWeighted(options)();
    }

    private static Item GetRandomCaveItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeMushroom, 4.0 },
            { ItemFactory.MakeSmallStone, 8.0 },
            { ItemFactory.MakeFlint, 3.0 },
            { ItemFactory.MakeBone, 4.0 },
            { ItemFactory.MakeObsidianShard, 0.5 },
            { ItemFactory.MakeOchrePigment, 0.2 }
            // Removed: MakeTorch (crafted)
        };

        return Utils.GetRandomWeighted(options)();
    }

    private static Item GetRandomRiverbankItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeWater, 8.0 },
            { ItemFactory.MakeFish, 5.0 },
            { ItemFactory.MakeClay, 6.0 },
            { ItemFactory.MakeSmallStone, 8.0 },
            { ItemFactory.MakeFlint, 2.0 },
            { ItemFactory.MakeRoots, 3.0 }
        };

        return Utils.GetRandomWeighted(options)();
    }

    private static Item GetRandomPlainsItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeRoots, 6.0 },
            { ItemFactory.MakeSmallStone, 5.0 },
            { ItemFactory.MakeBone, 3.0 },
            { ItemFactory.MakeSinew, 1.0 },
            { ItemFactory.MakeBerry, 2.0 },
            { ItemFactory.MakeMammothTusk, 0.1 } // Very rare find
        };

        return Utils.GetRandomWeighted(options)();
    }

    private static Item GetRandomHillsideItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeSmallStone, 8.0 },
            { ItemFactory.MakeFlint, 5.0 },
            { ItemFactory.MakeObsidianShard, 1.0 },
            { ItemFactory.MakeOchrePigment, 2.0 }
            // Removed: MakeHandAxe (crafted tool)
        };

        return Utils.GetRandomWeighted(options)();
    }
}