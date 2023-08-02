using text_survival.Actors;
using text_survival.Items;
using static text_survival.Environments.Area;

namespace text_survival.Environments
{
    public static class AreaFactory
    {
        public enum EnvironmentType
        {
            AbandonedBuilding,
            Forest,
            Cave,
            River,
            Road
        }

      

        public static string GetRandomAreaName(EnvironmentType environmentType)
        {
            string name = "";
            List<string> names;
            switch (environmentType)
            {
                case EnvironmentType.Forest:
                    names = new List<string>()
                    {
                        "Forest",
                        "Clearing",
                        "Grove",
                        "Lake",
                        "Trail",
                        "Abandoned Campsite",
                        "Dark Forest",
                    };
                    break;
                case EnvironmentType.Cave:
                    names = new List<string>()
                    {
                        "Cave",
                        "Cavern",
                        "Tunnel",
                        "Mine",
                        "Abandoned Mine",
                        "Dark Cave",
                    };
                    break;
                case EnvironmentType.AbandonedBuilding:
                    names = new List<string>()
                    {
                        "Abandoned Building",
                        "Abandoned House",
                        "Abandoned Shack",
                        "Abandoned Cabin",
                        "Abandoned Church",

                    };
                    break;
                case EnvironmentType.Road:
                    names = new List<string>()
                    {
                        "Road",
                        "Path",
                        "Trail",
                        "Dirt Road",
                        "Gravel Road",
                        "Paved Road",
                    };
                    break;
                default:
                    names = new List<string>()
                    {
                        "Location"
                    };
                    break;
            }
            name = names[Utils.Rand(0, names.Count - 1)];
            return name;
        }

        public static Area GenerateArea(EnvironmentType type, int numItems = 1, int numNpcs = 1)
        {
            Area area = new Area(GetRandomAreaName((type)),"");
            ItemPool itemPool = CreateItemPool(type);
            NpcPool npcPool = CreateNpcPool(type);
            for (int i = 0; i < numItems; i++)
            {
                area.Items.Add(itemPool.GenerateRandomItem());
            }
            for (int i = 0; i < numNpcs; i++)
            {
                area.Npcs.Add(npcPool.GenerateRandomNpc());
            }
            return area;
        }

    private static readonly Dictionary<string, Func<Item>> ItemDefinitions = new()
        {
            { "Mushroom", ItemFactory.MakeMushroom },
            { "Apple", ItemFactory.MakeApple },
            { "Bread", ItemFactory.MakeBread },
            { "Berry", ItemFactory.MakeBerry },
            { "Carrot", ItemFactory.MakeCarrot },
            { "Water", ItemFactory.MakeWater },
            { "Stick", ItemFactory.MakeStick },
            { "Wood", ItemFactory.MakeWood },
            { "Rock", ItemFactory.MakeRock },
            { "Gemstone", ItemFactory.MakeGemstone },
            { "Coin", ItemFactory.MakeCoin },
            { "Sword", ItemFactory.MakeSword },
            { "Shield", ItemFactory.MakeShield },
            { "Armor", ItemFactory.MakeArmor },
            { "Health Potion", ItemFactory.MakeHealthPotion },
            { "Bandage", ItemFactory.MakeBandage },
            { "Torch", ItemFactory.MakeTorch },
            { "Fish", ItemFactory.MakeFish }
        };



        private static readonly Dictionary<EnvironmentType, List<string>> EnvironmentItems = new()
        {
            { EnvironmentType.AbandonedBuilding, new List<string> {
                "Apple",
                "Bread",
                "Coin",
                "Sword",
                "Shield",
                "Bandage",
                "Health Potion",
                "Armor"
            } },
            { EnvironmentType.Forest, new List<string> {
                "Berry",
                //"Carrot", 
                "Water",
                "Mushroom",
                "Stick",
                //"Wood" 
            } },
            { EnvironmentType.Cave, new List<string> {
                "Mushroom",
                "Rock",
                "Gemstone",
                "Torch"
            } },
            { EnvironmentType.River, new List<string>
            {
                "Fish",
                "Water"
            } },

        };

        private static readonly Dictionary<string, Func<Npc>> NpcDefinitions = new()
        {
            { "Rat", NpcFactory.MakeRat },
            { "Wolf", NpcFactory.MakeWolf },
            { "Bear", NpcFactory.MakeBear },
            { "Snake", NpcFactory.MakeSnake },
            { "Bat", NpcFactory.MakeBat },
            { "Spider", NpcFactory.MakeSpider },
            { "Goblin", NpcFactory.MakeGoblin },
            { "Dragon", NpcFactory.MakeDragon },
            { "Skeleton", NpcFactory.MakeSkeleton },
            { "Crocodile", NpcFactory.MakeCrocodile },

        };


        private static readonly Dictionary<EnvironmentType, List<string>> EnvironmentNpcs = new()
        {
            { EnvironmentType.Forest, new List<string> {
                "Wolf",
                "Bear",
                "Snake",
                "Goblin"
            } },
            { EnvironmentType.Cave, new List<string> {
                "Bat",
                "Spider",
                "Snake",
                "Dragon",
                "Skeleton" } },
            { EnvironmentType.AbandonedBuilding, new List<string> {
                "Rat"
            } },
            { EnvironmentType.River, new List<string>
            {
                "Crocodile"
            } },
        };

        private static NpcPool CreateNpcPool(EnvironmentType environment)
        {
            NpcPool npcs = new();
            var npcList = EnvironmentNpcs[environment];
            foreach (string npcName in npcList)
            {
                npcs.Add(NpcDefinitions[npcName]);
            }
            return npcs;
        }
        private static ItemPool CreateItemPool(EnvironmentType environment)
        {
            ItemPool items = new ItemPool();
            var itemList = EnvironmentItems[environment];
            foreach (string itemName in itemList)
            {
                items.Add(ItemDefinitions[itemName]);
            }
            return items;
        }
    }
}
