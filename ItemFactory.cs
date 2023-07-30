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
                if (Utils.FlipCoin())
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
            return new FoodItem("Berries", 120, 100);
        }

        public static FoodItem MakeCarrot()
        {
            return new FoodItem("Carrot", 50, 30);
        }

        public static FoodItem MakeWater()
        {
            return new FoodItem("Water", 0, 1000);
        }

        public static Item MakeStick()
        {
            Item stick = new Item("Stick");
            stick.UseEffect = (Player) =>
            {
                Utils.Write("You can make this into a torch or a spear.");
                Utils.Write("What would you like to make?");
                Utils.Write("1. Torch");
                Utils.Write("2. Spear");
                Utils.Write("3. Nothing");
                int choice = Utils.ReadInt(1, 3);
                if (choice == 1)
                {
                    Player.Inventory.Remove(stick);
                    Player.Inventory.Add(MakeTorch());
                    Utils.Write("You made a torch!");
                }
                else if (choice == 2)
                {
                    Player.Inventory.Remove(stick);
                    Player.Inventory.Add(MakeSpear());
                    Utils.Write("You made a spear!");
                }
                else
                {
                    Utils.Write("You decide to keep the stick.");
                }
            };
            return stick;
        }
        public static Item MakeSpear()
        {
            EquipableItem spear = new EquipableItem("Spear", 7, 0, -1, 0);
            return spear;
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

        public static EquipableItem MakeTorch()
        {
            EquipableItem torch = new EquipableItem("Torch", 0, 0, 0, 5);
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
            Item vial = new Item("Venom Vial");
            vial.UseEffect = (player) =>
            {
                Utils.Write("You can use this to poison your weapon.");
                if (player.EquipedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon))
                {
                    EquipableItem weapon = player.EquipedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon);
                    weapon.Strength += 2;
                    player.Inventory.Remove(vial);
                }
                else
                {
                    Utils.Write("You don't have any weapons to poison.");
                }
            };
            return vial;
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
            Item silk = new Item("Spider Silk");
            silk.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your clothing.");
                if (player.EquipedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Chest))
                {
                    EquipableItem armor = player.EquipedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Chest);
                    armor.Warmth += 2;
                    player.Inventory.Remove(silk);
                } else
                {
                    Utils.Write("You don't have any clothing to improve.");
                }
            };
            return silk;

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
            Item scale = new Item("Dragon Scale");
            scale.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your armor.");
                if (player.EquipedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Chest))
                {
                    EquipableItem armor = player.EquipedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Chest);
                    armor.Defense += 6;
                    player.Inventory.Remove(scale);
                }
            };
            return scale;
        }

        public static Item MakeDragonTooth()
        {
            Item tooth = new Item("Dragon Tooth");
            tooth.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your weapon.");
                if (player.EquipedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon))
                {
                    EquipableItem weapon = player.EquipedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon);
                    weapon.Strength += 6;
                    player.Inventory.Remove(tooth);
                }
            };
            return tooth;
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

        public static EquipableItem MakeClothShirt()
        {
            EquipableItem shirt = new EquipableItem("Cloth Shirt", 0, 1, 0, 1);
            shirt.EquipSpot = EquipableItem.EquipSpots.Chest;
            return shirt;
        }
        public static EquipableItem MakeClothPants()
        {
            EquipableItem pants = new EquipableItem("Cloth Pants", 0, 1, 0, .5F);
            pants.EquipSpot = EquipableItem.EquipSpots.Legs;
            return pants;
        }

        public static EquipableItem MakeBoots()
        {
            EquipableItem shoes = new EquipableItem("Boots", 0, 1, 0, .5F);
            shoes.EquipSpot = EquipableItem.EquipSpots.Feet;
            return shoes;
        }
    }

}
