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
        
        // Generate a more descriptive name
        string[] forestNames = { "Forest", "Clearing", "Grove", "Woods", "Hollow" };
        string[] forestAdjectives = { 
            "Old Growth", "Overgrown", "", "Old", "Dusty", "Cool", "Breezy", "Quiet", "Ancient", 
            "Ominous", "Sullen", "Forlorn", "Desolate", "Secret", "Hidden", "Forgotten", "Cold", 
            "Dark", "Damp", "Wet", "Dry", "Warm", "Icy", "Snowy", "Frozen", "Dense", "Sparse",
            "Misty", "Foggy", "Pine", "Oak", "Maple", "Ash", "Birch", "Evergreen", "Hardwood"
        };
        
        // Pick a random adjective and name
        string adjective = forestAdjectives[Utils.RandInt(0, forestAdjectives.Length - 1)];
        string name = forestNames[Utils.RandInt(0, forestNames.Length - 1)];
        location.Name = (adjective + " " + name).Trim();
        
        // Create a ForageFeature with high resource density for forests (1.2)
        ForageFeature forageFeature = new ForageFeature(location, 1.2);
        
        // Add resources to the forage feature
        forageFeature.AddResource(ItemFactory.MakeBerry, 5.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeWater, 4.0);     // Common
        forageFeature.AddResource(ItemFactory.MakeMushroom, 3.0);  // Moderately common
        forageFeature.AddResource(ItemFactory.MakeStick, 8.0);     // Very common
        forageFeature.AddResource(ItemFactory.MakeWood, 4.0);      // Common
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for forest
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Forest));
        
        // Configure the loot table for initial items
        LootTable lootTable = new LootTable();
        lootTable.AddItem(ItemFactory.MakeBerry, 5.0);
        lootTable.AddItem(ItemFactory.MakeWater, 4.0);
        lootTable.AddItem(ItemFactory.MakeMushroom, 3.0);
        lootTable.AddItem(ItemFactory.MakeStick, 8.0);
        lootTable.AddItem(ItemFactory.MakeWood, 4.0);
        
        // Generate 2-5 random items from the loot table for initial population
        int itemCount = Utils.RandInt(2, 5);
        for (int i = 0; i < itemCount; i++)
        {
            location.Items.Add(lootTable.GenerateRandomItem());
        }
        
        // Configure the NPC spawner
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddNpc(NpcFactory.MakeWolf, 3.0);  // More common
        npcSpawner.AddNpc(NpcFactory.MakeBear, 1.0);  // Less common
        
        // Determine if we should add NPCs initially (50% chance)
        if (Utils.RandInt(0, 1) == 1)
        {
            // Add 1-2 NPCs from the spawner
            int npcCount = Utils.RandInt(1, 2);
            for (int i = 0; i < npcCount; i++)
            {
                location.Npcs.Add(npcSpawner.GenerateRandomNpc());
            }
        }
        
        return location;
    }
    
    public static Location MakeCave(Zone parent)
    {
        // Create a base cave location
        Location location = new Location("Cave", parent);
        
        // Generate a more descriptive name
        string[] caveNames = { "Cave", "Cavern", "Ravine" };
        string[] caveAdjectives = { 
            "", "Abandoned", "Collapsed", "Shallow", "Deep", "Echoing", "Painted", "Sparkling", 
            "Dim", "Icy", "Dark", "Damp", "Narrow", "Vast", "Ancient", "Forgotten", "Hidden", 
            "Crystal", "Limestone", "Granite", "Marble", "Sandstone", "Glowing", "Silent"
        };
        
        // Pick a random adjective and name
        string adjective = caveAdjectives[Utils.RandInt(0, caveAdjectives.Length - 1)];
        string name = caveNames[Utils.RandInt(0, caveNames.Length - 1)];
        location.Name = (adjective + " " + name).Trim();
        
        // Create a ForageFeature with moderate resource density for caves (0.8)
        ForageFeature forageFeature = new ForageFeature(location, 0.8);
        
        // Add resources to the forage feature
        forageFeature.AddResource(ItemFactory.MakeMushroom, 0.5);
        forageFeature.AddResource(ItemFactory.MakeRock, 0.5);
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for cave
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Cave));
        
        // Configure the loot table for initial items
        LootTable lootTable = new LootTable();
        lootTable.AddItem(ItemFactory.MakeMushroom, 3.0);
        lootTable.AddItem(ItemFactory.MakeRock, 3.0);
        lootTable.AddItem(ItemFactory.MakeGemstone, 1.0);
        lootTable.AddItem(ItemFactory.MakeTorch, 2.0);
        lootTable.AddItem(Weapon.GenerateRandomWeapon, 1.0);
        
        // Generate 2-4 random items from the loot table for initial population
        int itemCount = Utils.RandInt(2, 4);
        for (int i = 0; i < itemCount; i++)
        {
            location.Items.Add(lootTable.GenerateRandomItem());
        }
        
        // Configure the NPC spawner
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddNpc(NpcFactory.MakeSpider, 3.0);
        npcSpawner.AddNpc(NpcFactory.MakeRat, 3.0);
        npcSpawner.AddNpc(NpcFactory.MakeSnake, 2.0);
        npcSpawner.AddNpc(NpcFactory.MakeBat, 2.0);
        npcSpawner.AddNpc(NpcFactory.MakeCaveBear, 1.0);
        
        // Determine if we should add NPCs initially (40% chance)
        if (Utils.RandInt(0, 9) < 4)
        {
            // Add 1-3 NPCs from the spawner
            int npcCount = Utils.RandInt(1, 3);
            for (int i = 0; i < npcCount; i++)
            {
                location.Npcs.Add(npcSpawner.GenerateRandomNpc());
            }
        }
        
        return location;
    }
    
    public static Location MakeTrail(Zone parent)
    {
        // Create a base trail location
        Location location = new Location("Trail", parent);
        
        // Generate a more descriptive name
        string[] trailNames = { "Path", "Trail", "Pass" };
        string[] trailAdjectives = { 
            "Dirt", "Gravel", "Stone", "Animal", "Hunter's", "Winding", "Straight", "Curved", 
            "Twisting", "Bumpy", "Smooth", "Narrow", "Wide", "Long", "Short", "Steep", "Flat", 
            "Sloping", "Rough", "Smooth", "Muddy", "Rocky", "Worn", "Abandoned", "Hidden",
            "Overgrown", "Mountain", "Forest", "Ridge", "Valley"
        };
        
        // Pick a random adjective and name
        string adjective = trailAdjectives[Utils.RandInt(0, trailAdjectives.Length - 1)];
        string name = trailNames[Utils.RandInt(0, trailNames.Length - 1)];
        location.Name = (adjective + " " + name).Trim();
        
        // Create a ForageFeature with low resource density for trails (0.6)
        ForageFeature forageFeature = new ForageFeature(location, 0.6);
        
        // Add resources to the forage feature
        forageFeature.AddResource(ItemFactory.MakeStick, 2.0);
        forageFeature.AddResource(ItemFactory.MakeRock, 1.0);
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature based on surrounding terrain
        if (Utils.RandInt(0, 1) == 0)
        {
            // Forested trail
            location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.Forest));
        }
        else
        {
            // Open/highground trail
            location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.HighGround));
        }
        
        // Configure the loot table for initial items
        LootTable lootTable = new LootTable();
        lootTable.AddItem(ItemFactory.MakeRock, 3.0);
        lootTable.AddItem(ItemFactory.MakeStick, 4.0);
        lootTable.AddItem(ItemFactory.MakeBandage, 1.0);
        
        // Generate 1-3 random items from the loot table for initial population
        int itemCount = Utils.RandInt(1, 3);
        for (int i = 0; i < itemCount; i++)
        {
            location.Items.Add(lootTable.GenerateRandomItem());
        }
        
        // Configure the NPC spawner - trails mainly have snakes
        NpcTable npcSpawner = new NpcTable();
        npcSpawner.AddNpc(NpcFactory.MakeSnake, 1.0);
        
        // Determine if we should add NPCs initially (30% chance)
        if (Utils.RandInt(0, 9) < 3)
        {
            // Add just 1 NPC from the spawner
            location.Npcs.Add(npcSpawner.GenerateRandomNpc());
        }
        
        return location;
    }
    
    public static Location MakeRiver(Zone parent)
    {
        // Create a base river location
        Location location = new Location("River", parent);
        
        // Generate a more descriptive name
        string[] riverNames = { "River", "Stream", "Creek", "Waterfall", "Brook", "Rapids" };
        string[] riverAdjectives = { 
            "", "Shallow", "Deep", "Still", "Quiet", "Calm", "Rippling", "Misty", "Foggy", 
            "Murky", "Dark", "Shimmering", "Quick", "Loud", "Slow", "Lazy", "Rushing", "Roaring", 
            "Gurgling", "Babbling", "Crystal", "Rocky", "Sandy", "Muddy", "Wide", "Narrow"
        };
        
        // Pick a random adjective and name
        string adjective = riverAdjectives[Utils.RandInt(0, riverAdjectives.Length - 1)];
        string name = riverNames[Utils.RandInt(0, riverNames.Length - 1)];
        location.Name = (adjective + " " + name).Trim();
        
        // Create a ForageFeature with good resource density for rivers (1.0)
        ForageFeature forageFeature = new ForageFeature(location, 1.0);
        
        // Add resources to the forage feature
        forageFeature.AddResource(ItemFactory.MakeWater, 8.0);  // Very common
        forageFeature.AddResource(ItemFactory.MakeFish, 2.0);   // Moderately common
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add an environment feature for riverbank
        location.Features.Add(new EnvironmentFeature(location, EnvironmentFeature.LocationType.RiverBank));
        
        // Configure the loot table for initial items
        LootTable lootTable = new LootTable();
        lootTable.AddItem(ItemFactory.MakeWater, 5.0);
        lootTable.AddItem(ItemFactory.MakeFish, 2.0);
        
        // Generate 1-3 random items from the loot table for initial population
        int itemCount = Utils.RandInt(1, 3);
        for (int i = 0; i < itemCount; i++)
        {
            location.Items.Add(lootTable.GenerateRandomItem());
        }
        
        // No NPCs in river locations typically
        
        return location;
    }
    
    public static Location MakeFrozenLake(Zone parent)
    {
        // Create a base frozen lake location
        Location location = new Location("Frozen Lake", parent);
        
        // Generate a more descriptive name
        string[] frozenLakeNames = { "Lake", "Pond", "Water" };
        string[] frozenLakeAdjectives = { 
            "", "Shallow", "Deep", "Still", "Quiet", "Calm", "Misty", "Foggy", "Murky", 
            "Dark", "Shimmering", "Glassy", "Cracked", "Solid", "Slippery", "Crystal", 
            "Frozen", "Icy", "Frosty", "Snow-covered", "Clear", "Opaque", "Blue", "White" 
        };
        
        // Pick a random adjective and name
        string adjective = frozenLakeAdjectives[Utils.RandInt(0, frozenLakeAdjectives.Length - 1)];
        string name = frozenLakeNames[Utils.RandInt(0, frozenLakeNames.Length - 1)];
        location.Name = (adjective + " " + name).Trim();
        
        // Create a ForageFeature with low resource density for frozen lakes (0.3)
        ForageFeature forageFeature = new ForageFeature(location, 0.3);
        
        // Add resources to the forage feature (very limited)
        forageFeature.AddResource(ItemFactory.MakeWater, 0.5);  // Ice can be melted for water
        
        // Add the forage feature to the location's features
        location.Features.Add(forageFeature);
        
        // Add a custom environment feature for frozen lake (open with cold modifier)
        EnvironmentFeature envFeature = new EnvironmentFeature(
            location,
            -3.0,  // Temperature modifier from original code
            0.0,   // No overhead coverage
            0.0    // No wind protection (exposed)
        );
        location.Features.Add(envFeature);
        
        // Frozen lakes typically don't have many items
        // And often no NPCs

        // Potential to add ice fishing as a special feature
        
        return location;
    }
}