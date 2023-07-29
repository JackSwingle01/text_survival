namespace text_survival
{
    public static class Places
    {
        private enum EnvironmentType
        {
            Shack,
            Forest,
            Cave,
        }
        public static Place GetShack()
        {
            Place shack = new Place("Shack", "An abandoned shack. Doesn't look like there's much here");
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
            Place cave = new Place("Cave", "A cave. Something sparkles in the dark");
            cave.Items = CreateItemPool(EnvironmentType.Cave);
            cave.NPCs = CreateNPCPool(EnvironmentType.Cave);
            cave.BaseTemperature = 50.0F;
            return cave;
        }

        private static Dictionary<string, Item> itemDefinitions = new()
        {
            { "Mushroom", new FoodItem("Mushroom", 25, 5){
                UseEffect = (player) =>{
                    player.Heal(2);
                    Utils.Write("You feel better");
                }}},
            { "Apple", new FoodItem("Apple", 90, 50) },
            { "Bread", new FoodItem("Bread", 300, -10) },
            { "Berry", new FoodItem("Berry", 50, 20) },
            { "Carrot", new FoodItem("Carrot", 50, 30) },
            { "Water", new FoodItem("Water", 0, 500) },
            { "Stick", new Item("Stick") },
            { "Wood", new Item("Wood") },
            { "Rock", new Item("Rock"){
                UseEffect =  (player) =>{
                    player.Strength += 5;
                    Utils.Write("This will make a good weapon");
                }}},
            { "Gemstone", new Item("Gemstone") },
            { "Coin", new Item("Coin") },
            { "Sword", new Item("Sword")
            {
                UseEffect = (player) =>
                {
                    player.Strength += 10;
                    Utils.Write("You feel stronger");
                }}},
            { "Shield", new Item("Shield")
            {
                UseEffect = (player) =>
                {
                    player.Defense += 10;
                    Utils.Write("You feel more protected");
                }}},
            { "Armor", new Item("Armor")
            {
                UseEffect = (player) =>
                {
                    player.Defense += 10;
                    player.ClothingInsulation += 1;
                    Utils.Write("You feel more protected");
                }}},
            { "Potion", new Item("Health Potion")
            {
                UseEffect = (player) =>
                {
                    player.Heal(10);
                    Utils.Write("You feel better");
                }}},
            { "Bandage", new Item("Bandage")
            {
                UseEffect = (player) =>
                {
                    player.Heal(5);
                    Utils.Write("You feel better");
                }}},
            { "Torch", new Item("Torch")
            {
                UseEffect = (player) =>
                {
                    player.ClothingInsulation += 5;
                    Utils.Write("You feel warmer");
                }}},

            };

        private static Dictionary<EnvironmentType, List<string>> environmentItems = new()
        {
            { EnvironmentType.Shack, new List<string> { "Apple", "Bread", "Coin", "Sword", "Shield", "Bandage", "Health Potion" } },
            { EnvironmentType.Forest, new List<string> { "Berry", "Carrot", "Water", "Mushroom", "Stick", "Wood" } },
            { EnvironmentType.Cave, new List<string> { "Mushroom", "Rock", "Gemstone" } },


        };

        private static Dictionary<string, NPC> npcDefinitions = new()
        {
            // ("Name", Health, Strength, Defense, Speed)
            { "Rat", new NPC("Rat", 5, 5, 2, 10) },
            { "Wolf", new NPC("Wolf", 10, 10, 5, 18) },
            { "Bear", new NPC("Bear", 20, 20, 20, 7) },
            { "Snake", new NPC("Snake", 5, 5, 2, 11) },
            { "Bat", new NPC("Bat", 5, 5, 2, 16) },
            { "Spider", new NPC("Spider", 1, 1, 0, 5) },
            { "Goblin", new NPC("Goblin", 10, 10, 5, 10) },
            { "Dragon", new NPC("Dragon", 50, 50, 50, 3) },
            { "Skeleton", new NPC("Skeleton", 10, 10, 10, 10) }

        };

        private static Dictionary<EnvironmentType, List<string>> environmentNPCs = new()
        {
            { EnvironmentType.Forest, new List<string> { "Wolf", "Bear", "Snake" , "Goblin" } },
            { EnvironmentType.Cave, new List<string> { "Bat", "Spider", "Snake", "Dragon", "Skeleton" } },
            { EnvironmentType.Shack, new List<string> { "Rat" } },
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
