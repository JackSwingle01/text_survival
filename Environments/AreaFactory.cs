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
                Area.EnvironmentType.Forest =>
                [
                    "Forest", "Clearing", "Grove", "Lake", "Trail", "Abandoned Campsite", "Dark Forest", "Dark Grove",
                    "Dark Trail", "Open Field", "Field", "Meadow", "Grassland", "Plains", "Woods", "Dark Woods",
                    "Dark Forest", "Hollow", "Dark Hollow", "Hill", "Valley", "Dark Valley", "Ominous Forest",
                    "Shady Forest", "Lonely Tree", "Ancient Tree", "Ancient Forest", "Ancient Woods", "Ancient Grove",
                    "Old Growth Forest", "Abandoned Camp", "Abandoned Cornfield", "Grassy Field", "Dirt Trail",
                    "Gravel Trail",
                ],
                _ => ["Location"],
            };
            string name = names[Utils.RandInt(0, names.Count - 1)];
            return name;
        }


        

        private static readonly Dictionary<Area.EnvironmentType, List<string>> EnvironmentItems = new()
        {
            
            { Area.EnvironmentType.Forest, new List<string> {
                "Berry",
                "Carrot",
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
            Area area = new(GetRandomAreaName((type)), "", GetAreaBaseTemperature(type));
            LootTable itemPool = CreateLootTable(type);
            NpcPool npcPool = CreateNpcPool(type);
            for (int i = 0; i < numItems; i++)
            {
                area.PutThing(itemPool.GenerateRandomItem());
            }
            for (int i = 0; i < numNpcs; i++)
            {
                area.PutThing(npcPool.GenerateRandomNpc());
            }
            return area;
        }

        

        private static NpcPool CreateNpcPool(Area.EnvironmentType environment)
        {
            NpcPool npcs = new();
            var npcList = EnvironmentNpcs[environment];
            foreach (string npcName in npcList)
            {
                npcs.Add(NpcFactory.NpcDefinitions[npcName]);
            }
            return npcs;
        }
        private static LootTable CreateLootTable(Area.EnvironmentType environment)
        {
            LootTable items = new();
            var itemList = EnvironmentItems[environment];
            foreach (string itemName in itemList)
            {
                items.AddLoot(ItemFactory.ItemDefinitions[itemName]);
            }
            return items;
        }
    }
}
