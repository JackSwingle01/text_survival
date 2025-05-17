using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Environments;

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
        
        // Create a ForageFeature with high resource density for forests (1.2)
        ForageFeature forageFeature = new ForageFeature(location, 1.2);
        
        // Add natural resources to the forage feature - more forest-appropriate items
        forageFeature.AddResource(ItemFactory.MakeBerry, 5.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeWater, 2.0);     // Available but not as common
        forageFeature.AddResource(ItemFactory.MakeMushroom, 4.0);  // Common in forests
        forageFeature.AddResource(ItemFactory.MakeStick, 8.0);     // Very common
        forageFeature.AddResource(ItemFactory.MakeFirewood, 4.0);   // Common
        forageFeature.AddResource(ItemFactory.MakeRoots, 3.0);     // Fairly common
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.5);     // Rare
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for forest
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Forest));
        
        // Add initial visible items
        int itemCount = Utils.RandInt(2, 4);
        for (int i = 0; i < itemCount; i++)
        {
            Item item = GetRandomForestItem();
            location.Items.Add(item);
        }
        
        // Configure the NPC spawner
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 3.0);   // Common
        npcSpawner.AddActor(NpcFactory.MakeBear, 1.0);   // Rare
        
        // Determine if we should add NPCs initially (40% chance)
        if (Utils.RandInt(0, 9) < 4)
        {
            // Add 1-2 NPCs from the spawner
            int npcCount = Utils.RandInt(1, 2);
            for (int i = 0; i < npcCount; i++)
            {
                location.Npcs.Add(npcSpawner.GenerateRandom());
            }
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
        forageFeature.AddResource(ItemFactory.MakeStone, 5.0);     // Very common
        forageFeature.AddResource(ItemFactory.MakeFlint, 2.0);     // More common in caves
        forageFeature.AddResource(ItemFactory.MakeClay, 1.0);      // Near cave entrances
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.3); // Rare but valuable
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for cave
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Cave));
        
        // Add initial visible items
        int itemCount = Utils.RandInt(1, 3);
        for (int i = 0; i < itemCount; i++)
        {
            Item item = GetRandomCaveItem();
            location.Items.Add(item);
        }
        
        // Configure the NPC spawner
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeSpider, 3.0);
        npcSpawner.AddActor(NpcFactory.MakeRat, 3.0);
        npcSpawner.AddActor(NpcFactory.MakeSnake, 1.0);
        npcSpawner.AddActor(NpcFactory.MakeBat, 4.0);
        npcSpawner.AddActor(NpcFactory.MakeCaveBear, 0.5);
        
        // Determine if we should add NPCs initially (50% chance)
        if (Utils.RandInt(0, 9) < 5)
        {
            // Add 1-2 NPCs from the spawner
            int npcCount = Utils.RandInt(1, 2);
            for (int i = 0; i < npcCount; i++)
            {
                location.Npcs.Add(npcSpawner.GenerateRandom());
            }
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
        forageFeature.AddResource(ItemFactory.MakeStone, 5.0);     // River stones
        forageFeature.AddResource(ItemFactory.MakeFlint, 1.0);     // Occasionally found
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for riverbank
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.RiverBank));
        
        // Add initial visible items
        int itemCount = Utils.RandInt(2, 4);
        for (int i = 0; i < itemCount; i++)
        {
            Item item = GetRandomRiverbankItem();
            location.Items.Add(item);
        }
        
        // Configure the NPC spawner for riverbanks
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 2.0);   // Predators come to water
        npcSpawner.AddActor(NpcFactory.MakeBear, 1.0);   // Bears fish at rivers
        
        // Determine if we should add NPCs initially (30% chance)
        if (Utils.RandInt(0, 9) < 3)
        {
            location.Npcs.Add(npcSpawner.GenerateRandom());
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
        forageFeature.AddResource(ItemFactory.MakeStone, 4.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeFlint, 0.5);     // Rare
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for open plain
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.OpenPlain));
        
        // Add initial visible items
        int itemCount = Utils.RandInt(1, 3);
        for (int i = 0; i < itemCount; i++)
        {
            Item item = GetRandomPlainsItem();
            location.Items.Add(item);
        }
        
        // Configure the NPC spawner - plains have megafauna!
        var npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 3.0);               // Common
        npcSpawner.AddActor(NpcFactory.MakeWoollyMammoth, 0.5);      // Rare but possible
        npcSpawner.AddActor(NpcFactory.MakeSaberToothTiger, 0.7);    // Uncommon
        
        // Determine if we should add NPCs initially (40% chance)
        if (Utils.RandInt(0, 9) < 4)
        {
            location.Npcs.Add(npcSpawner.GenerateRandom());
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
        forageFeature.AddResource(ItemFactory.MakeStone, 8.0);        // Very common
        forageFeature.AddResource(ItemFactory.MakeFlint, 3.0);        // More common on hillsides
        forageFeature.AddResource(ItemFactory.MakeObsidianShard, 0.5); // Rare but possible
        forageFeature.AddResource(ItemFactory.MakeRoots, 2.0);         // Less common
        forageFeature.AddResource(ItemFactory.MakeOchrePigment, 1.0);  // Sometimes found on hills
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for hillside (using cliff as closest match)
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Cliff));
        
        // Add initial visible items
        int itemCount = Utils.RandInt(2, 4);
        for (int i = 0; i < itemCount; i++)
        {
            Item item = GetRandomHillsideItem();
            location.Items.Add(item);
        }
        
        // Configure the NPC spawner for hillsides
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddActor(NpcFactory.MakeWolf, 1.0);
        npcSpawner.AddActor(NpcFactory.MakeSnake, 2.0);    // Snakes like rocky areas
        
        // Determine if we should add NPCs initially (30% chance)
        if (Utils.RandInt(0, 9) < 3)
        {
            location.Npcs.Add(npcSpawner.GenerateRandom());
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
            { ItemFactory.MakeTorch, 0.5 },
            { ItemFactory.MakeSpear, 0.2 },
            { ItemFactory.MakeHealingHerbs, 1.0 }
        };
        
        return Utils.GetRandomWeighted(options)();
    }
    
    private static Item GetRandomCaveItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeMushroom, 4.0 },
            { ItemFactory.MakeStone, 8.0 },
            { ItemFactory.MakeFlint, 3.0 },
            { ItemFactory.MakeTorch, 1.0 },
            { ItemFactory.MakeBone, 4.0 },
            { ItemFactory.MakeObsidianShard, 0.5 },
            { ItemFactory.MakeOchrePigment, 0.2 }
        };
        
        return Utils.GetRandomWeighted(options)();
    }
    
    private static Item GetRandomRiverbankItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeWater, 8.0 },
            { ItemFactory.MakeFish, 5.0 },
            { ItemFactory.MakeClay, 6.0 },
            { ItemFactory.MakeStone, 8.0 },
            { ItemFactory.MakeFlint, 2.0 },
            { ItemFactory.MakeRoots, 3.0 }
        };
        
        return Utils.GetRandomWeighted(options)();
    }
    
    private static Item GetRandomPlainsItem()
    {
        var options = new Dictionary<Func<Item>, double> {
            { ItemFactory.MakeRoots, 6.0 },
            { ItemFactory.MakeStone, 5.0 },
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
            { ItemFactory.MakeStone, 8.0 },
            { ItemFactory.MakeFlint, 5.0 },
            { ItemFactory.MakeObsidianShard, 1.0 },
            { ItemFactory.MakeOchrePigment, 2.0 },
            { ItemFactory.MakeHandAxe, 0.2 }
        };
        
        return Utils.GetRandomWeighted(options)();
    }
}