namespace text_survival.Items
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
                    Utils.Write("You feel better\n");
                }
                else
                {
                    player.Damage(1);
                    Utils.Write("You feel sick\n");
                }
            };
            mushroom.Description = "A mushroom of unknown origin. It looks edible, but you're not sure.";
            mushroom.Weight = 0.1F;
            return mushroom;
        }

        public static FoodItem MakeApple()
        {
            var apple = new FoodItem("Apple", 90, 50);
            apple.Description = "A red apple. It looks delicious.";
            apple.Weight = 0.2F;
            return apple;
        }

        public static FoodItem MakeBread()
        {
            var item = new FoodItem("Bread", 300, -10);
            item.Description = "A loaf of bread. Looks kind of stale.";
            item.Weight = 0.5F;
            return item;
        }

        public static FoodItem MakeBerry()
        {
            var item = new FoodItem("Berries", 120, 100);
            string color = Utils.FlipCoin() ? "red" : "blue";
            item.Description = "A handful of " + color + " berries.";
            item.Weight = 0.1F;
            return item;
        }

        public static FoodItem MakeCarrot()
        {
            var item = new FoodItem("Carrot", 50, 30);
            return item;
        }

        public static FoodItem MakeWater()
        {
            var item = new FoodItem("Water", 0, 1000);
            item.Description = "Some water. You store it in your waterskin.";
            return item;
        }

        public static Item MakeStick()
        {
            Item stick = new Item("Stick");
            stick.UseEffect = (player) =>
            {
                Utils.Write("You can make this into a torch or a spear.\n");
                Utils.Write("What would you like to make?\n");
                Utils.Write("1. Torch\n");
                Utils.Write("2. Spear\n");
                Utils.Write("3. Nothing\n");
                int choice = Utils.ReadInt(1, 3);
                if (choice == 1)
                {
                    player.Inventory.Remove(stick);
                    player.Inventory.Add(MakeTorch());
                    Utils.Write("You made a torch!\n");
                }
                else if (choice == 2)
                {
                    player.Inventory.Remove(stick);
                    player.Inventory.Add(MakeSpear());
                    Utils.Write("You made a spear!\n");
                }
                else
                {
                    Utils.Write("You decide to keep the stick.\n");
                }
            };
            return stick;
        }
        public static Item MakeSpear()
        {
            EquipableItem spear = new EquipableItem("Spear", 7, 0, -1, 0);
            spear.EquipSpot = EquipableItem.EquipSpots.Weapon;
            return spear;
        }

        public static Item MakeWood()
        {
            var item = new Item("Wood");
            return item;
        }

        public static Item MakeRock()
        {
            var item = new Item("Rock");
            return item;
        }

        public static Item MakeGemstone()
        {
            var item = new Item("Gemstone");
            return item;
        }

        public static Item MakeCoin()
        {
            var item = new Item("Coin");
            return item;
        }

        public static EquipableItem MakeSword()
        {
            EquipableItem sword = new EquipableItem("Sword", 20, 2, 0);
            sword.EquipSpot = EquipableItem.EquipSpots.Weapon;
            sword.Description = "A sword that does 20 damage";
            return sword;
        }

        public static EquipableItem MakeShield()
        {
            EquipableItem shield =  new EquipableItem("Shield", 0, 10, -1);
            shield.EquipSpot = EquipableItem.EquipSpots.Hands;
            shield.Description = "A shield that blocks 10% damage";
            return shield;
        }

        public static EquipableItem MakeArmor()
        {
            EquipableItem armor = new EquipableItem("Armor", 0, 20, -5);
            armor.EquipSpot = EquipableItem.EquipSpots.Chest;
            armor.Description = "Armor that blocks 20% damage but slows you down";
            return armor;
        }

        public static Item MakeHealthPotion()
        {
            var potion = new Item("Health Potion");
            potion.UseEffect = (player) =>
            {
                player.Heal(10);
                Utils.Write("You feel better\n");
            };
            potion.Description = "A potion that heals you for 10 health";
            return potion;
        }

        public static Item MakeBandage()
        {
            var bandage = new Item("Bandage");
            bandage.UseEffect = (player) =>
            {
                player.Heal(5);
                Utils.Write("You feel better\n");
            };
            bandage.Description = "A bandage that heals you for 5 health";
            return bandage;
        }

        public static EquipableItem MakeTorch()
        {
            EquipableItem torch = new EquipableItem("Torch", 0, 0, 0, 5);
            torch.EquipSpot = EquipableItem.EquipSpots.Hands;
            torch.Description = "A torch that warms you";
            return torch;
        }

        public static FoodItem MakeFish()
        {
            var item = new FoodItem("Fish", 200, 0);
            return item;
        }

        public static FoodItem MakeLargeMeat()
        {
            var item = new FoodItem("Large Meat", 600, 0);
            return item;
        }
        public static FoodItem MakeSmallMeat()
        {
            var item = new FoodItem("Small Meat", 200, 0);
            return item;
        }
        public static FoodItem MakeCheese()
        {
            var item = new FoodItem("Cheese", 150, 30);
            return item;
        }

        public static Item MakeCopperCoin()
        {
            var item = new Item("Copper Coin");
            return item;
        }

        public static Item MakeSnakeSkin()
        {
            var item = new Item("Snake Skin");
            return item;
        }

        public static Item MakeVenomVial()
        {
            Item vial = new Item("Venom Vial");
            vial.UseEffect = (player) =>
            {
                Utils.Write("You can use this to poison your weapon.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon))
                {
                    EquipableItem weapon = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon);
                    weapon.Strength += 2;
                    player.Inventory.Remove(vial);
                }
                else
                {
                    Utils.Write("You don't have any weapons to poison.\n");
                }
            };
            return vial;
        }

        public static Item MakeBatWing()
        {
            var item = new Item("Bat Wing");
            return item;
        }

        public static Item MakeGuano()
        {
            var item = new Item("Guano");
            return item;
        }

        public static Item MakeSpiderSilk()
        {
            Item silk = new Item("Spider Silk");
            silk.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your clothing.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Chest))
                {
                    EquipableItem armor = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Chest);
                    armor.Warmth += 2;
                    player.Inventory.Remove(silk);
                }
                else
                {
                    Utils.Write("You don't have any clothing to improve.\n");
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
                Utils.Write("You can use this to improve your armor.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Chest))
                {
                    EquipableItem armor = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Chest);
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
                Utils.Write("You can use this to improve your weapon.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon))
                {
                    EquipableItem weapon = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon);
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
                Utils.Write("It contained " + num + " coins\n");
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
            var item = new Item("Bone Fragments");
            return item;
        }

        public static EquipableItem MakeRustySword()
        {
            EquipableItem sword = new EquipableItem("Rusty Sword", 3, 1, 0);
            sword.EquipSpot = EquipableItem.EquipSpots.Weapon;
            return sword;
        }

        public static Item MakeCrocodileSkin()
        {
            Item skin = new Item("Crocodile Skin");
            skin.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your armor.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Chest))
                {
                    EquipableItem armor = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Chest);
                    armor.Defense += 2;
                    player.Inventory.Remove(skin);
                }
            };
            return skin;
        }

        public static Item MakeCrocodileTooth()
        {
            Item tooth = new Item("Crocodile Tooth");
            tooth.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your weapon.\n");
                if (player.EquippedItems.Any(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon))
                {
                    EquipableItem weapon = player.EquippedItems.First(i => i.EquipSpot == EquipableItem.EquipSpots.Weapon);
                    weapon.Strength += 2;
                    player.Inventory.Remove(tooth);
                }
            };
            return tooth;
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
