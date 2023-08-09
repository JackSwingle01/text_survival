namespace text_survival.Items
{
    public class ItemFactory
    {
        public static Weapon MakeFists()
        {
            return new Weapon(WeaponType.Unarmed, WeaponMaterial.Other, "Fists");
        }
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
            item.Description = "A carrot. It looks like it was just pulled from the ground.";
            item.Weight = 0.1F;
            return item;
        }

        public static FoodItem MakeWater()
        {
            var item = new FoodItem("Water", 0, 1000);
            item.Description = "Some water. You store it in your waterskin.";
            item.Weight = 1;
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
            stick.Description = "A stick. Useful.";
            stick.Weight = 1;
            return stick;
        }
        public static Item MakeSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Wooden);
            spear.Description = "A makeshift spear.";
            spear.Weight = 1;
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
            item.Description = "A shiny gemstone.";
            item.Weight = .1F;
            return item;
        }

        public static Item MakeCoin()
        {
            var item = new Item("Coin");
            item.Weight = 0.01F;
            return item;
        }

        public static Weapon MakeSword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Steel);
            sword.Description = "A steel sword.";
            sword.Weight = 2;
            return sword;
        }

        public static Armor MakeShield()
        {
            Armor shield = new Armor("Shield", .1, EquipSpots.Hands);
            shield.EquipSpot = EquipSpots.Hands;
            shield.Description = "A shield that blocks 10% damage";
            shield.Weight = 2.5F;
            return shield;
        }

        public static Armor MakeArmor()
        {
            Armor armor = new Armor("Armor", .5, EquipSpots.Chest, 1);
            armor.EquipSpot = EquipSpots.Chest;
            armor.Description = "Heavy armor that blocks 50% damage but slows you down";
            armor.Weight = 5;
            return armor;
        }

        public static Item MakeHealthPotion()
        {
            var potion = new Item("Health Potion");
            potion.UseEffect = (player) =>
            {
                player.Heal(50);
                Utils.Write("You feel better\n");
            };
            potion.Description = "A potent healing potion";
            potion.Weight = 0.4F;
            return potion;
        }

        public static Item MakeBandage()
        {
            var bandage = new Item("Bandage");
            bandage.UseEffect = (player) =>
            {
                player.Heal(10);
                Utils.Write("You feel better\n");
            };
            bandage.Description = "A cloth bandage. It might help a bit.";
            bandage.Weight = 0.1F;
            return bandage;
        }

        public static Armor MakeTorch()
        {
            Armor torch = new Armor("Torch", 0, EquipSpots.Hands, 5);
            torch.EquipSpot = EquipSpots.Hands;
            torch.Description = "A torch that warms you";
            return torch;
        }

        public static FoodItem MakeFish()
        {
            var item = new FoodItem("Fish", 200, 0);
            item.Weight = 0.3F;
            return item;
        }

        public static FoodItem MakeLargeMeat()
        {
            var item = new FoodItem("Large Meat", 600, 0);
            item.Weight = .6F;
            return item;
        }
        public static FoodItem MakeSmallMeat()
        {
            var item = new FoodItem("Small Meat", 200, 0);
            item.Weight = .2F;
            return item;
        }
        public static FoodItem MakeCheese()
        {
            var item = new FoodItem("Cheese", 150, 30);
            item.Weight = .1F;
            return item;
        }

        public static Item MakeCopperCoin()
        {
            var item = new Item("Copper Coin");
            item.Weight = 0.01F;
            return item;
        }

        public static Item MakeSnakeSkin()
        {
            var item = new Item("Snake Skin");
            item.Weight = 0.1F;
            return item;
        }

        public static Item MakeVenomVial()
        {
            Item vial = new Item("Venom Vial");
            vial.UseEffect = (player) =>
            {
                Utils.Write("You can use this to poison your weapon.\n");
                if (player.Weapon is not null)
                {
                    player.Weapon.Damage += 2;
                    player.Inventory.Remove(vial);
                }
                else
                {
                    Utils.Write("You don't have any weapons to poison.\n");
                }
            };
            vial.Description = "A vial of snake venom.";
            vial.Weight = 0.1F;
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
                if (player.Armor.Any(i => i.EquipSpot == EquipSpots.Chest))
                {
                    Armor armor = player.Armor.First(i => i.EquipSpot == EquipSpots.Chest) as Armor;
                    armor.Warmth += 1;
                    player.Inventory.Remove(silk);
                }
                else
                {
                    Utils.Write("You don't have any clothing to improve.\n");
                }
            };
            return silk;

        }

        public static Weapon MakeGoblinSword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Iron);
            sword.Name = "Goblin Sword";
            return sword;
        }

        public static Armor MakeTatteredCloth()
        {
            Armor cloth = new Armor("Tattered Cloth", .3, EquipSpots.Head, .5);
            return cloth;
        }

        public static Item MakeDragonScale()
        {
            Item scale = new Item("Dragon Scale");
            scale.UseEffect = (player) =>
            {
                Utils.Write("You can use this to improve your armor.\n");
                if (player.Armor.Any(i => i.EquipSpot == EquipSpots.Chest))
                {
                    Armor armor = player.Armor.First(i => i.EquipSpot == EquipSpots.Chest);
                    armor.Rating += 6;
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
                if (player.Weapon is not null)
                {
                    player.Weapon.Damage += 6;
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
                int num = Utils.RandInt(3, 5);
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

        public static Weapon MakeRustySword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Iron);
            sword.Description = "A rusty sword.";
            sword.Weight = 2.0F;
            return sword;
        }

        public static Item MakeCrocodileSkin()
        {
            Item skin = new Item("Crocodile Skin");
            skin.UseEffect = (player) =>
            {
                Utils.Write("You use this to improve your armor.\n");
                if (player.Armor.Any(i => i.EquipSpot == EquipSpots.Chest))
                {
                    Armor armor = player.Armor.Select(i => i as Armor)
                                            .First(i => i.EquipSpot == EquipSpots.Chest);
                    armor.Rating += .2;
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
                if (player.Armor.Any(i => i.EquipSpot == EquipSpots.Weapon))
                {
                    Utils.Write("You use this to improve your weapon.\n");
                    player.Weapon.Damage += 2;
                    player.Inventory.Remove(tooth);
                }
                else
                {
                    Utils.Write("You don't have any weapons to improve.\n");
                }
            };
            return tooth;
        }

        public static Armor MakeClothShirt()
        {
            Armor shirt = new Armor("Cloth Shirt", .03, EquipSpots.Chest, 3)
            {
                EquipSpot = EquipSpots.Chest
            };
            return shirt;
        }
        public static Armor MakeClothPants()
        {
            Armor pants = new Armor("Cloth Pants", .02, EquipSpots.Legs, 2);
            pants.EquipSpot = EquipSpots.Legs;
            return pants;
        }

        public static Armor MakeBoots()
        {
            Armor shoes = new Armor("Boots", .01, EquipSpots.Feet, 1);
            shoes.EquipSpot = EquipSpots.Feet;
            return shoes;
        }
    }

}
