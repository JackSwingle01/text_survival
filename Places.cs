namespace text_survival
{
    public static class Places
    {
        public static Place GetShack()
        {
            Place shack = new Place("Shack", "An abandoned shack. Doesn't look like there's much here");
            shack.Items = CreateShackItemPool();
            shack.BaseTemperature = 75.0F;
            return shack;
        }

        public static Place GetForest()
        {
            Place forest = new Place("Forest", "A forest with dense vegitation");
            forest.Items = CreateForestItemPool();
            forest.NPCs = CreateForestNPCPool();
            forest.BaseTemperature = 70.0F;
            return forest;
        }
        public static Place GetCave()
        {
            Place cave = new Place("Cave", "A cave. Something sparkles in the dark");
            cave.Items = CreateCaveItemPool();
            cave.NPCs = CreateCaveNPCPool();
            cave.BaseTemperature = 50.0F;
            return cave;
        }

        private static ItemPool CreateShackItemPool()
        {
            ItemPool items = new();
            items.Add(new FoodItem("Apple", 90, 50));
            items.Add(new FoodItem("Bread", 300, -10));
            items.Add(new Item("Coin"));
            return items;

        }

        private static ItemPool CreateForestItemPool()
        {
            ItemPool items = new();
            items.Add(new FoodItem("Berry", 50, 20));
            items.Add(new FoodItem("Carrot", 50, 30));
            items.Add(new FoodItem("Water", 0, 500));
            items.Add(new Item("Stick"));
            items.Add(new FoodItem("Mushroom", 25, 5));
            items.Add(new Item("Wood"));
            return items;
        }

        private static ItemPool CreateCaveItemPool()
        {
            ItemPool items = new();
            items.Add(new FoodItem("Mushroom", 25, 5));
            items.Add(new Item("Rock"));
            items.Add(new Item("Gemstone"));
            return items;
        }

        private static NPCPool CreateForestNPCPool()
        {
            NPCPool npcs = new();
            npcs.Add(new NPC("Wolf", 10, 10, 5, 18));
            npcs.Add(new NPC("Bear", 20, 20, 20, 7));
            npcs.Add(new NPC("Snake", 5, 15, 2, 11));
            return npcs;
        }
        private static NPCPool CreateCaveNPCPool()
        {
            NPCPool npcs = new();
            npcs.Add(new NPC("Bat", 5, 5, 2, 16));
            npcs.Add(new NPC("Spider", 1, 5, 0, 5));
            npcs.Add(new NPC("Snake", 5, 15, 2, 11));
            return npcs;
        }
    }

}
