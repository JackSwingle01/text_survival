namespace text_survival.Environments;

public static class ZoneFactory
{
    public static Zone MakeForestZone(string name = "", string description = "", double baseTemp = 20)
    {
        // Create a location table specifically for forest locations
        LocationTable forestLocationTable = new LocationTable();
        
        // Add various forest location types with appropriate weights
        forestLocationTable.AddFactory(LocationFactory.MakeForest, 3.0); // Most common
        
        // You can add more forest-related locations here when implemented
        // forestLocationTable.AddLocation(() => LocationFactory.MakeClearing(null), 2.0);
        // forestLocationTable.AddLocation(() => LocationFactory.MakeGrove(null), 1.5);
        // forestLocationTable.AddLocation(() => LocationFactory.MakeThicket(null), 1.0);
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            string[] forestZoneNames = { "Forest", "Woods", "Woodland", "Timberland", "Wildwood" };
            string[] forestZoneAdjectives = { "", "Deep", "Ancient", "Verdant", "Mysterious", "Shadowy", "Enchanted", "Wild", "Dark", "Dense", "Northern", "Southern", "Eastern", "Western" };
            
            // Pick a random adjective and name
            string adjective = forestZoneAdjectives[Utils.RandInt(0, forestZoneAdjectives.Length - 1)];
            string zoneName = forestZoneNames[Utils.RandInt(0, forestZoneNames.Length - 1)];
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            string[] forestDescriptions = 
            {
                "A vast expanse of trees stretching as far as the eye can see.",
                "Tall trees with a dense canopy above, filtering sunlight to the forest floor.",
                "A quiet forest with the occasional sounds of wildlife echoing through the trees.",
                "Trees of various species create a diverse ecosystem rich with life.",
                "A sprawling woodland with paths winding between ancient trees and undergrowth."
            };
            
            description = forestDescriptions[Utils.RandInt(0, forestDescriptions.Length - 1)];
        }
        
        // Create and return the forest zone
        return new Zone(name, description, forestLocationTable, baseTemp);
    }
    
    public static Zone MakeCaveSystemZone(string name = "", string description = "", double baseTemp = 10)
    {
        // Create a location table specifically for cave system locations
        LocationTable caveLocationTable = new LocationTable();
        
        // Add various cave location types with appropriate weights
        caveLocationTable.AddFactory(LocationFactory.MakeCave, 3.0); // Most common
        
        // You can add more cave-related locations here when implemented
        // caveLocationTable.AddLocation(() => LocationFactory.MakeCavern(null), 2.0);
        // caveLocationTable.AddLocation(() => LocationFactory.MakeRavine(null), 1.5);
        // caveLocationTable.AddLocation(() => LocationFactory.MakeCrystalCave(null), 1.0);
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            string[] caveZoneNames = { "Cave System", "Caverns", "Underground Complex", "Grotto Network", "Subterranean Labyrinth" };
            string[] caveZoneAdjectives = { "", "Deep", "Ancient", "Crystal", "Mysterious", "Dark", "Echoing", "Forgotten", "Hidden", "Vast", "Winding" };
            
            // Pick a random adjective and name
            string adjective = caveZoneAdjectives[Utils.RandInt(0, caveZoneAdjectives.Length - 1)];
            string zoneName = caveZoneNames[Utils.RandInt(0, caveZoneNames.Length - 1)];
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            string[] caveDescriptions = 
            {
                "A maze of dark tunnels and chambers stretching deep into the earth.",
                "A network of interconnected caves with stalactites hanging from the ceiling.",
                "Cool, damp caverns with the sound of water dripping echoing in the darkness.",
                "An intricate system of underground passages formed over thousands of years.",
                "A sprawling subterranean network with chambers of varying sizes and depths."
            };
            
            description = caveDescriptions[Utils.RandInt(0, caveDescriptions.Length - 1)];
        }
        
        // Create and return the cave system zone with cooler base temperature
        return new Zone(name, description, caveLocationTable, baseTemp);
    }
    
    // // Method to create a complete world with multiple zones
    // public static List<Zone> CreateWorld()
    // {
    //     List<Zone> world = new List<Zone>();
        
    //     // Create various zones
    //     Zone forestZone = MakeForestZone();
    //     world.Add(forestZone);
        
    //     Zone caveZone = MakeCaveSystemZone();
    //     world.Add(caveZone);
        
    //     // Fix parent references for all zones
    //     foreach (var zone in world)
    //     {
    //         FixParentReferences(zone);
    //     }
        
    //     return world;
    // }
}