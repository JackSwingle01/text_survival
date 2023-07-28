using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public static class Places
    {
        public static Place GetShack()
        {
            return new Place("Shack", "An abandoned shac. Doesn't look like there's much here.", CreateShackItemPool());
        }

        public static Place GetForest()
        {
            return new Place("Forest", "A forest with dense vegitation", CreateForestItemPool());
        }
        public static Place GetCave()
        {
            return new Place("Cave", "A cave. Something sparkles in the dark", CreateCaveItemPool());
        }

        private static ItemPool CreateShackItemPool()
        {
            ItemPool items = new ItemPool();
            items.Add(new FoodItem("Apple", 90, 50));
            items.Add(new FoodItem("Bread", 300, -10));
            items.Add(new Item("Coin"));
            return items;
      
        }

        private static ItemPool CreateForestItemPool()
        {
            ItemPool items = new ItemPool();
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
            ItemPool items = new ItemPool();
            items.Add(new FoodItem("Mushroom", 25, 5));
            items.Add(new Item("Rock"));
            items.Add(new Item("Gemstone"));
            return items;
        }
    }

}
