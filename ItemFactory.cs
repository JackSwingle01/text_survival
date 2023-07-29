namespace text_survival
{
    public class ItemFactory
    {
        public static FoodItem MakeMushroom()
        {
            var mushroom = new FoodItem("Mushroom", 25, 5);
            mushroom.UseEffect = (player) =>
            {
                player.Eat(mushroom);
                if (Utils.flipCoin())
                {
                    player.Heal(1);
                    Utils.Write("You feel better");
                }
                else
                {
                    player.Damage(1);
                    Utils.Write("You feel sick");
                }
            };
            return mushroom;
        }

        public static FoodItem MakeApple()
        {
            return new FoodItem("Apple", 90, 50);
        }

        public static FoodItem MakeBread()
        {
            return new FoodItem("Bread", 300, -10);
        }

        public static FoodItem MakeBerry()
        {
            return new FoodItem("Berry", 50, 20);
        }

        public static FoodItem MakeCarrot()
        {
            return new FoodItem("Carrot", 50, 30);
        }

        public static FoodItem MakeWater()
        {
            return new FoodItem("Water", 0, 500);
        }

        public static Item MakeStick()
        {
            return new Item("Stick");
        }

        public static Item MakeWood()
        {
            return new Item("Wood");
        }

        public static Item MakeRock()
        {
            return new Item("Rock");
        }

        public static Item MakeGemstone()
        {
            return new Item("Gemstone");
        }

        public static Item MakeCoin()
        {
            return new Item("Coin");
        }

        public static EquipableItem MakeSword()
        {
            return new EquipableItem("Sword", 20, 2, 0);
        }

        public static EquipableItem MakeShield()
        {
            return new EquipableItem("Shield", 0, 10, -1);
        }

        public static EquipableItem MakeArmor()
        {
            return new EquipableItem("Armor", 0, 20, -5);
        }

        public static Item MakeHealthPotion()
        {
            var potion = new Item("Health Potion");
            potion.UseEffect = (player) =>
            {
                player.Heal(10);
                Utils.Write("You feel better");
            };
            return potion;
        }

        public static Item MakeBandage()
        {
            var bandage = new Item("Bandage");
            bandage.UseEffect = (player) =>
            {
                player.Heal(5);
                Utils.Write("You feel better");
            };
            return bandage;
        }

        public static Item MakeTorch()
        {
            var torch = new Item("Torch");
            torch.UseEffect = (player) =>
            {
                player.ClothingInsulation += 5;
                Utils.Write("You feel warmer");
            };
            return torch;
        }

        public static FoodItem MakeFish()
        {
            return new FoodItem("Fish", 200, 0);
        }

        public static FoodItem MakeLargeMeat()
        {
            return new FoodItem("Large Meat", 600, 0);
        }
        public static FoodItem MakeSmallMeat()
        {
            return new FoodItem("Small Meat", 200, 0);
        }
        public static FoodItem MakeCheese()
        {
            return new FoodItem("Cheese", 150, 30);
        }

        public static Item MakeCopperCoin()
        {
            return new Item("Copper Coin");
        }

        public static Item MakeSnakeSkin()
        {
            return new Item("Snake Skin");
        }

        public static Item MakeVenomVial()
        {
            return new Item("Venom Vial");
        }

        public static Item MakeBatWing()
        {
            return new Item("Bat Wing");
        }

        public static Item MakeGuano()
        {
            return new Item("Guano");
        }

        public static Item MakeSpiderSilk()
        {
            return new Item("Spider Silk");
        }

        public static EquipableItem MakeGoblinSword()
        {
            EquipableItem sword = new EquipableItem("Goblin Sword", 5, 1, 0);
            sword.EquipSpot = EquipableItem.EquipSpots.Weapon;
            return sword;
        }

        public static EquipableItem MakeTatteredCloth()
        {
            EquipableItem cloth = new EquipableItem("Tattered Cloth", 0, 1, 0);
            cloth.EquipSpot = EquipableItem.EquipSpots.Chest;
            return cloth;
        }

        public static Item MakeDragonScale()
        {
            return new Item("Dragon Scale");
        }

        public static Item MakeDragonTooth()
        {
            return new Item("Dragon Tooth");
        }

        public static Item MakeLargeCoinPouch()
        {
            Item item = new Item("Large Coin Pouch");
            item.UseEffect = (player) =>
            {
                int num = Utils.Rand(3, 5);
                Utils.Write("It contained " + num + " coins");
                for (int i = 0; i < num; i++)
                {
                    player.Inventory.Add(MakeCoin());
                }
                player.Inventory.Remove(item);
            };
            return item;
        }

        public static Item MakeBoneFragments()
        {
            return new Item("Bone Fragments");
        }

        public static EquipableItem MakeRustySword()
        {
            EquipableItem sword = new EquipableItem("Rusty Sword", 3, 1, 0);
            sword.EquipSpot = EquipableItem.EquipSpots.Weapon;
            return sword;
        }

        public static Item MakeCrocodileSkin()
        {
            return new Item("Crocodile Skin");
        }

        public static Item MakeCrocodileTooth()
        {
            return new Item("Crocodile Tooth");
        }

    }

}
