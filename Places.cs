namespace text_survival
{
    public static class Places
    {
        private enum EnvironmentType
        {
            Shack,
            Forest,
            Cave,
            River,
        }
        public static Place GetShack()
        {
            Place shack = new Place("Shack", "An abandoned shack.");
            shack.Items = CreateItemPool(EnvironmentType.Shack);
            shack.NPCs = CreateNPCPool(EnvironmentType.Shack);
            shack.BaseTemperature = 75.0F;
            return shack;
        }

        public static Place GetForest()
        {
            Place forest = new Place("Forest", "A forest with dense vegitation");
            forest.Items = CreateItemPool(EnvironmentType.Forest);
            forest.NPCs = CreateNPCPool(EnvironmentType.Forest);
            forest.BaseTemperature = 70.0F;
            return forest;
        }
        public static Place GetCave()
        {
            Place cave = new Place("Cave", "A dark cold cave.");
            cave.Items = CreateItemPool(EnvironmentType.Cave);
            cave.NPCs = CreateNPCPool(EnvironmentType.Cave);
            cave.BaseTemperature = 50.0F;
            return cave;
        }
        public static Place GetRiver()
        {
            Place river = new Place("River", "A river with fresh water.");
            river.Items = CreateItemPool(EnvironmentType.River);
            river.NPCs = CreateNPCPool(EnvironmentType.River);
            river.BaseTemperature = 60.0F;
            return river;
        }


        private static Dictionary<string, Item> itemDefinitions = new()
        {
            { "Mushroom", ItemFactory.MakeMushroom() },
            { "Apple", ItemFactory.MakeApple() },
            { "Bread", ItemFactory.MakeBread() },
            { "Berry", ItemFactory.MakeBerry() },
            { "Carrot", ItemFactory.MakeCarrot() },
            { "Water", ItemFactory.MakeWater() },
            { "Stick", ItemFactory.MakeStick() },
            { "Wood", ItemFactory.MakeWood() },
            { "Rock", ItemFactory.MakeRock() },
            { "Gemstone", ItemFactory.MakeGemstone() },
            { "Coin", ItemFactory.MakeCoin() },
            { "Sword", ItemFactory.MakeSword() },
            { "Shield", ItemFactory.MakeShield() },
            { "Armor", ItemFactory.MakeArmor() },
            { "Health Potion", ItemFactory.MakeHealthPotion() },
            { "Bandage", ItemFactory.MakeBandage() },
            { "Torch", ItemFactory.MakeTorch() },
            { "Fish", ItemFactory.MakeFish() }
        };



        private static Dictionary<EnvironmentType, List<string>> environmentItems = new()
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

        private static Dictionary<string, NPC> npcDefinitions = new()
        {
            { "Rat", NPCFactory.MakeRat() },
            { "Wolf", NPCFactory.MakeWolf() },
            { "Bear", NPCFactory.MakeBear() },
            { "Snake", NPCFactory.MakeSnake() },
            { "Bat", NPCFactory.MakeBat() },
            { "Spider", NPCFactory.MakeSpider() },
            { "Goblin", NPCFactory.MakeGoblin() },
            { "Dragon", NPCFactory.MakeDragon() },
            { "Skeleton", NPCFactory.MakeSkeleton() },
            { "Crocodile", NPCFactory.MakeCrocodile() },
            
        };


        private static Dictionary<EnvironmentType, List<string>> environmentNPCs = new()
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

        private static NPCPool CreateNPCPool(EnvironmentType environment)
        {
            NPCPool npcs = new();
            var npcList = environmentNPCs[environment];
            foreach (string npcName in npcList)
            {
                NPC npc = npcDefinitions[npcName];
                npcs.Add(npc);
            }
            return npcs;
        }
        private static ItemPool CreateItemPool(EnvironmentType environment)
        {
            ItemPool items = new ItemPool();
            var itemList = environmentItems[environment];
            foreach (string itemName in itemList)
            {
                Item item = itemDefinitions[itemName];
                items.Add(item);
            }
            return items;
        }
    }
}
