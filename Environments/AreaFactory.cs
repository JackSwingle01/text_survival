using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public static class AreaFactory
    {
        private enum EnvironmentType
        {
            Shack,
            Forest,
            Cave,
            River,
        }
        public static Area GetShack()
        {
            Area shack = new Area("Shack", "An abandoned shack.");
            shack.ItemPool = CreateItemPool(EnvironmentType.Shack);
            shack.NpcPool = CreateNpcPool(EnvironmentType.Shack);
            shack.BaseTemperature = 75.0F;
            return shack;
        }

        public static Area GetForest()
        {
            Area forest = new Area("Forest", "A forest with dense vegitation");
            forest.ItemPool = CreateItemPool(EnvironmentType.Forest);
            forest.NpcPool = CreateNpcPool(EnvironmentType.Forest);
            forest.BaseTemperature = 70.0F;
            Location grove = new Location("Grove", "A small clearing in the forest.");
            grove.Items.Add(ItemFactory.MakeMushroom());
            grove.NpcPool.Add(NpcFactory.MakeWolf);
            forest.Locations.Add(grove);
            return forest;
        }
        public static Area GetCave()
        {
            Area cave = new Area("Cave", "A dark cold cave.");
            cave.ItemPool = CreateItemPool(EnvironmentType.Cave);
            cave.NpcPool = CreateNpcPool(EnvironmentType.Cave);
            cave.BaseTemperature = 50.0F;
            return cave;
        }
        public static Area GetRiver()
        {
            Area river = new Area("River", "A river with fresh water.");
            river.ItemPool = CreateItemPool(EnvironmentType.River);
            river.NpcPool = CreateNpcPool(EnvironmentType.River);
            river.BaseTemperature = 60.0F;
            return river;
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
            { EnvironmentType.Shack, new List<string> {
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
            { EnvironmentType.Shack, new List<string> {
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
                //Item item = _itemDefinitions[itemName];
                items.Add(ItemDefinitions[itemName]);
            }
            return items;
        }
    }
}
