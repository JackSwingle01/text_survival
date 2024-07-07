using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public static class AreaFactory
    {
        public static string GetRandomAreaName(Area.EnvironmentType environmentType)
        {
            List<string> names = environmentType switch
            {
                Area.EnvironmentType.Forest => forestNames,
                _ => ["Location"],
            };

            var descriptors = environmentType switch
            {
                Area.EnvironmentType.Forest => forestAdjectives,
                _ => [""]
            };
            descriptors.AddRange(genericAdjectives);

            string name = Utils.GetRandomFromList(descriptors) + " " + Utils.GetRandomFromList(names);
            return name;
        }

        private static readonly List<string> forestNames = ["Forest", "Clearing", "Grove", "Woods", "Hollow"];
        private static readonly List<string> forestAdjectives = ["Old Growth", "Overgrown"];
        private static readonly List<string> genericAdjectives = ["", "Open", "Dark", "Ominous", "Shady", "Lonely", "Ancient",];
        

        private static readonly Dictionary<Area.EnvironmentType, List<string>> EnvironmentItems = new()
        {
            
            { Area.EnvironmentType.Forest, new List<string> {
                "Berries",
                "Edible Root",
                "Water",
                "Mushroom",
                "Stick",
                "Wood"
            } },
            

        };

        private static readonly Dictionary<Area.EnvironmentType, List<string>> EnvironmentNpcs = new()
        {
            { Area.EnvironmentType.Forest, new List<string> {
                "Wolf",
                "Bear",
            } },
            
        };

        private static double GetAreaBaseTemperature(Area.EnvironmentType environment)
        {
            return environment switch
            {
                Area.EnvironmentType.Forest => 70,
                _ => 70,
            };
        }

        public static Area GenerateArea(Area.EnvironmentType type, int numItems = 1, int numNpcs = 1)
        {
            Area area = new(GetRandomAreaName(type), "", GetAreaBaseTemperature(type));
            return area;
        }

        

        //private static NpcSpawner CreateNpcPool(Area.EnvironmentType environment)
        //{
        //    NpcSpawner npcs = new();
        //    var npcList = EnvironmentNpcs[environment];
        //    foreach (string npcName in npcList)
        //    {
        //        npcs.Add(NpcFactory.NpcDefinitions[npcName]);
        //    }
        //    return npcs;
        //}
        //private static LootTable CreateLootTable(Area.EnvironmentType environment)
        //{
        //    LootTable items = new();
        //    var itemList = EnvironmentItems[environment];
        //    foreach (string itemName in itemList)
        //    {
        //        items.AddLoot(ItemFactory.ItemDefinitions[itemName]);
        //    }
        //    return items;
        //}
    }
}
