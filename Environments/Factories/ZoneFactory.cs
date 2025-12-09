namespace text_survival.Environments.Factories;

public static class ZoneFactory
{
    public static Zone MakeForestZone(string name = "", string description = "", double baseTemp = 20)
    {
        // Create a location table specifically for forest locations
        LocationTable forestLocationTable = new LocationTable();
        
        // Add various forest location types with appropriate weights
        forestLocationTable.AddFactory(LocationFactory.MakeForest, 3.0); // Most common
        
        // You can add more forest-related locations here when implemented
        // forestLocationTable.AddFactory(LocationFactory.MakeTrail, 2.0);
        // forestLocationTable.AddFactory(LocationFactory.MakeRiverbank, 1.5);
        // forestLocationTable.AddFactory(LocationFactory.MakeHillside, 1.0);
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            List<string> forestZoneNames = ["Forest", "Woods", "Woodland", "Taiga", "Wildwood", "Timberland", "Pine Forest", "Birch Forest"];
            List<string> forestZoneAdjectives = [
                "", "Deep", "Ancient", "Verdant", "Mysterious", "Shadowy", "Enchanted", "Wild", "Dark", "Dense", 
                "Northern", "Southern", "Eastern", "Western", "Frozen", "Boreal", "Glacial", "Snowy", "Frost-rimmed", 
                "Ice-laden", "Primeval", "Rime-covered", "Mammoth", "Misty", "Foggy"
            ];
            
            // Pick a random adjective and name
            string adjective = Utils.GetRandomFromList(forestZoneAdjectives);
            string zoneName = Utils.GetRandomFromList(forestZoneNames);
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            List<string> forestDescriptions = [
                "A vast expanse of snow-covered trees stretching as far as the eye can see.",
                "Frost-covered trees with icicles hanging from branches, filtering weak sunlight to the forest floor.",
                "A quiet forest with the occasional sounds of wildlife echoing through the icy stillness.",
                "Evergreen trees stand tall amidst the snow, creating a sanctuary for cold-adapted creatures.",
                "A sprawling woodland with ice-crusted paths winding between ancient trees and frozen undergrowth.",
                "Mammoth trails wind between the massive tree trunks, where herds seek shelter from the harsh winds.",
                "Rime-covered trees glisten in the pale light, their branches laden with snow and ice.",
                "The forest floor is covered with a thick blanket of snow, punctuated by animal tracks.",
                "Shadows stretch long across the pristine snow as sunlight filters through the dense canopy.",
                "Ancient pines stand as sentinels, their needles heavy with frost and snow."
            ];
            
            description = Utils.GetRandomFromList(forestDescriptions);
        }
        
        // Create and return the forest zone
        var zone = new Zone(name, description, forestLocationTable, baseTemp);
        zone.Type = ZoneType.Forest;
        return zone;
    }
    
    public static Zone MakeCaveSystemZone(string name = "", string description = "", double baseTemp = 10)
    {
        // Create a location table specifically for cave system locations
        LocationTable caveLocationTable = new LocationTable();
        
        // Add various cave location types with appropriate weights
        caveLocationTable.AddFactory(LocationFactory.MakeCave, 3.0); // Most common
        
        // You can add more cave-related locations here when implemented
        // caveLocationTable.AddFactory(LocationFactory.MakeTrail, 1.0); // Cave entrance trails
        // caveLocationTable.AddFactory(LocationFactory.MakeHillside, 1.5); // Cave entrances in hillsides
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            List<string> caveZoneNames = [
                "Cave System", "Caverns", "Underground Complex", "Grotto Network", "Subterranean Labyrinth",
                "Cave Refuge", "Ice Caverns", "Shelter System", "Hibernation Caves", "Painted Caves"
            ];
            List<string> caveZoneAdjectives = [
                "", "Deep", "Ancient", "Crystal", "Mysterious", "Dark", "Echoing", "Forgotten", "Hidden", "Vast", "Winding",
                "Frost-lined", "Ice-walled", "Glacial", "Ancestral", "Bone-filled", "Mammoth", "Protected", "Ritual",
                "Clan", "Fur-lined", "Warm", "Painted", "Firelit"
            ];
            
            // Pick a random adjective and name
            string adjective = Utils.GetRandomFromList(caveZoneAdjectives);
            string zoneName = Utils.GetRandomFromList(caveZoneNames);
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            List<string> caveDescriptions = [
                "A maze of dark tunnels and chambers offering refuge from the harsh ice age climate.",
                "A network of interconnected caves with icicles and ice formations hanging from the ceiling.",
                "Warm, sheltered caverns with the sound of meltwater dripping echoing in the darkness.",
                "An intricate system of underground passages formed by ancient glacial movements.",
                "A sprawling subterranean network with chambers used by clans for shelter and ritual.",
                "Cave walls adorned with ancient paintings depicting mammoth hunts and clan ceremonies.",
                "Floors littered with bones and artifacts from generations of human habitation.",
                "Narrow passages opening to large chambers where fires have burned for countless seasons.",
                "Ancestral shelters where generations have found protection from the deadly cold.",
                "Ice-rimmed entrances leading to surprisingly warm chambers deep within the earth."
            ];
            
            description = Utils.GetRandomFromList(caveDescriptions);
        }
        
        // Create and return the cave system zone with cooler base temperature
        var zone = new Zone(name, description, caveLocationTable, baseTemp);
        zone.Type = ZoneType.CaveSystem;
        return zone;
    }
    
    public static Zone MakeTundraZone(string name = "", string description = "", double baseTemp = 0)
    {
        // Create a location table specifically for tundra locations
        LocationTable tundraLocationTable = new LocationTable();
        
        // Add various tundra location types with appropriate weights
        tundraLocationTable.AddFactory(LocationFactory.MakePlain, 4.0); // Most common
        // tundraLocationTable.AddFactory(LocationFactory.MakeHillside, 2.0);
        // tundraLocationTable.AddFactory(LocationFactory.MakeRiverbank, 1.5);
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            List<string> tundraZoneNames = [
                "Tundra", "Steppe", "Plains", "Mammoth Plains", "Permafrost", "Ice Fields", 
                "Frozen Expanse", "Glacier Edge", "Frost Plains", "Hunting Grounds"
            ];
            List<string> tundraZoneAdjectives = [
                "", "Vast", "Windswept", "Endless", "Frozen", "Desolate", "Barren", "Ancient", 
                "Mammoth", "Glacial", "Northern", "Pristine", "Inhospitable", "Snow-covered", 
                "Primal", "Harsh", "Woolly", "Thundering", "Game-rich", "Megafauna"
            ];
            
            // Pick a random adjective and name
            string adjective = Utils.GetRandomFromList(tundraZoneAdjectives);
            string zoneName = Utils.GetRandomFromList(tundraZoneNames);
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            List<string> tundraDescriptions = [
                "An endless expanse of snow and ice, where mighty herds of woolly mammoth roam.",
                "Windswept plains stretching to the horizon, where only the hardiest plants survive.",
                "Wide open spaces where great herds of ice age megafauna gather to graze.",
                "A harsh landscape dominated by permafrost and spotted with patches of tough grasses.",
                "Snow-covered plains where saber-toothed predators stalk their mammoth prey.",
                "Vast open tundra where the wind howls unimpeded across the frozen landscape.",
                "The thunder of mammoth herds can be heard across these ancient hunting grounds.",
                "A stark but beautiful landscape of ice, snow, and occasional hardy vegetation.",
                "Glacial plains carved by the retreating ice sheet, leaving a harsh but life-filled realm.",
                "The domain of the woolly mammoth, where these giants travel in family groups across the snow."
            ];
            
            description = Utils.GetRandomFromList(tundraDescriptions);
        }
        
        // Create and return the tundra zone with much colder base temperature
        var zone = new Zone(name, description, tundraLocationTable, baseTemp);
        zone.Type = ZoneType.Tundra;
        return zone;
    }
    
    public static Zone MakeRiverValleyZone(string name = "", string description = "", double baseTemp = 15)
    {
        // Create a location table specifically for river valley locations
        LocationTable riverValleyLocationTable = new LocationTable();
        
        // Add various river valley location types with appropriate weights
        riverValleyLocationTable.AddFactory(LocationFactory.MakeRiverbank, 4.0); // Most common
        // riverValleyLocationTable.AddFactory(LocationFactory.MakeForest, 2.0); // Riverside forests
        // riverValleyLocationTable.AddFactory(LocationFactory.MakeHillside, 1.5); // Valley sides
        
        // Generate a name if one wasn't provided
        if (string.IsNullOrEmpty(name))
        {
            List<string> riverZoneNames = [
                "River Valley", "Waterway", "Glacial Valley", "River Basin", "Floodplain", 
                "Stream Network", "River Lands", "Meltwater Valley", "River Territory"
            ];
            List<string> riverZoneAdjectives = [
                "", "Winding", "Frozen", "Ancient", "Deep", "Fertile", "Ice-carved", "Glacier-fed", 
                "Protected", "Sheltered", "Resource-rich", "Fish-filled", "Life-giving", "Abundant",
                "Clay-rich", "Game-rich", "Meandering", "Mammoth-crossed"
            ];
            
            // Pick a random adjective and name
            string adjective = Utils.GetRandomFromList(riverZoneAdjectives);
            string zoneName = Utils.GetRandomFromList(riverZoneNames);
            name = (adjective + " " + zoneName).Trim();
        }
        
        // Generate a description if one wasn't provided
        if (string.IsNullOrEmpty(description))
        {
            List<string> riverDescriptions = [
                "A network of glacier-fed rivers and streams cutting through the icy landscape.",
                "Partially frozen waterways that provide essential resources for all life in the region.",
                "A river valley carved by ancient glacial movements, now home to diverse ice age life.",
                "Ice-rimmed waters flowing through a sheltered valley, attracting animals from miles around.",
                "A life-giving river system where clay, fish, and fresh water can be harvested.",
                "Mammoth herds gather along these banks to drink and bathe in the cold waters.",
                "The sound of rushing water breaks the winter silence as the river cuts through ice and snow.",
                "A critical resource in the frozen world, this river network sustains countless creatures.",
                "Ancient humans have left traces of their camps along these fertile riverbanks for generations.",
                "Where ice meets flowing water, creating a unique ecosystem in the frozen landscape."
            ];
            
            description = Utils.GetRandomFromList(riverDescriptions);
        }
        
        // Create and return the river valley zone with slightly warmer base temperature
        var zone = new Zone(name, description, riverValleyLocationTable, baseTemp);
        zone.Type = ZoneType.RiverValley;
        return zone;
    }
    
    // // Method to create a complete ice age world with multiple zones
    // public static List<Zone> CreateIceAgeWorld()
    // {
    //     List<Zone> world = new List<Zone>();
        
    //     // Create various zones with ice age appropriate base temperatures
    //     Zone forestZone = MakeForestZone(baseTemp: 10); // Colder than default
    //     world.Add(forestZone);
        
    //     Zone caveZone = MakeCaveSystemZone(baseTemp: 5); // Even colder caves
    //     world.Add(caveZone);
        
    //     Zone tundraZone = MakeTundraZone(); // Already very cold (0)
    //     world.Add(tundraZone);
        
    //     Zone riverValleyZone = MakeRiverValleyZone(baseTemp: 8); // Slightly warmer than surroundings but still cold
    //     world.Add(riverValleyZone);
        
    //     return world;
    // }
}