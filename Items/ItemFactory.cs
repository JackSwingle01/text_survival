using text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;
using text_survival.Magic;

namespace text_survival.Items
{
    public class ItemFactory
    {

        public static readonly Dictionary<string, Func<Item>> ItemDefinitions = new()
        {
            { "Mushroom", MakeMushroom },
            { "Apple", MakeApple },
            { "Bread", MakeBread },
            { "Berry", MakeBerry },
            { "Carrot", MakeCarrot },
            { "Water", MakeWater },
            { "Stick", MakeStick },
            { "Wood", MakeWood },
            { "Rock", MakeRock },
            { "Gemstone", MakeGemstone },
            { "Coin", MakeCoin },
            { "Sword", MakeSword },
            { "Shield", MakeShield },
            { "Armor", MakeArmor },
            { "Health Potion", MakeHealthPotion },
            { "Bandage", MakeBandage },
            { "Torch", MakeTorch },
            { "Fish", MakeFish },
            { "RandomWeapon", Weapon.GenerateRandomWeapon }
        };

        public static Weapon MakeFists()
        {
            return new Weapon(WeaponType.Unarmed, WeaponMaterial.Other, "Fists");
        }
        public static FoodItem MakeMushroom()
        {
            var mushroom = new FoodItem("Mushroom", 25, 5)
            {
                Description = "A mushroom of unknown origin. It looks edible, but you're not sure.",
                Weight = 0.1F
            };
            // instead of making the effect dynamic, just have the factory return two types of mushroom with the same name
            if (Utils.FlipCoin())
            {
                mushroom.HealthEffect = 5;
            }
            else
            {
                mushroom.HealthEffect = -5;
            }
            return mushroom;
        }

        public static FoodItem MakeApple()
        {
            var apple = new FoodItem("Apple", 90, 50)
            {
                Description = "A red apple. It looks delicious.",
                Weight = 0.2F
            };
            return apple;
        }

        public static FoodItem MakeBread()
        {
            var item = new FoodItem("Bread", 300, -10)
            {
                Description = "A loaf of bread. Looks kind of stale.",
                Weight = 0.5F
            };
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
            var item = new FoodItem("Carrot", 50, 30)
            {
                Description = "A carrot. It looks like it was just pulled from the ground.",
                Weight = 0.1F
            };
            return item;
        }

        public static FoodItem MakeWater()
        {
            var item = new FoodItem("Water", 0, 1000)
            {
                Description = "Some water. You store it in your waterskin.",
                Weight = 1
            };
            return item;
        }

        public static Item MakeStick()
        {
            Item stick = new Item("Stick")
            {
                Description = "A stick. Useful for crafting.",
                Weight = 1
            };
            return stick;
        }
        public static Item MakeSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Wooden)
            {
                Description = "A makeshift spear.",
                Weight = 1
            };
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
            var item = new Item("Gemstone")
            {
                Description = "A shiny gemstone.",
                Weight = .1F
            };
            return item;
        }

        public static Item MakeCoin()
        {
            var item = new Item("Coin")
            {
                Weight = 0.01F
            };
            return item;
        }

        public static Weapon MakeSword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Steel)
            {
                Description = "A steel sword.",
                Weight = 2
            };
            return sword;
        }

        public static Armor MakeShield()
        {
            Armor shield = new Armor("Shield", .1, EquipSpots.Hands)
            {
                EquipSpot = EquipSpots.Hands,
                Description = "A shield that blocks 10% damage",
                Weight = 2.5F
            };
            return shield;
        }

        public static Armor MakeArmor()
        {
            Armor armor = new Armor("Armor", .5, EquipSpots.Chest, 1)
            {
                EquipSpot = EquipSpots.Chest,
                Description = "Heavy armor that blocks 50% damage but slows you down",
                Weight = 5
            };
            return armor;
        }

        public static Item MakeHealthPotion()
        {
            var potion = new FoodItem("Health Potion", 5, 200)
            {
                Description = "A potent healing potion",

                Weight = 0.4F,
                NumUses = 1,
                HealthEffect = 50
            };
            return potion;
        }

        public static Item MakeBandage()
        {
            var bandage = new ConsumableItem("Bandage")
            {
                Description = "A cloth bandage. It might help a bit.",
                Weight = 0.1F,
                Effects = [new RemoveBleedEffect(), new HealEffect(10)]
            };
            return bandage;
        }

        public static Gear MakeTorch()
        {
            Gear torch = new Gear("Torch", 1)
            {
                Description = "A torch that warms you",
            };
            torch.AddEquipBuff(CommonBuffs.Warmth(5));
            return torch;
        }

        public static FoodItem MakeFish()
        {
            var item = new FoodItem("Fish", 200, 0)
            {
                Weight = 0.3F
            };
            return item;
        }

        public static FoodItem MakeLargeMeat()
        {
            var item = new FoodItem("Large Meat", 600, 0)
            {
                Weight = .6F
            };
            return item;
        }
        public static FoodItem MakeSmallMeat()
        {
            var item = new FoodItem("Small Meat", 200, 0)
            {
                Weight = .2F
            };
            return item;
        }
        public static FoodItem MakeCheese()
        {
            var item = new FoodItem("Cheese", 150, 30)
            {
                Weight = .1F
            };
            return item;
        }

        public static Item MakeCopperCoin()
        {
            var item = new Item("Copper Coin")
            {
                Weight = 0.01F
            };
            return item;
        }


        public static Item MakeVenomSac()
        {
            Item venom = new Item("Venom Sac");
            // venom.UseEffect = (player) =>
            // {
            //     if (player.IsArmed)
            //     {
            //         Output.Write("You use ", venom, " to poison your weapon. (TODO)\n");
            //         // player.Weapon.AddEquipBuff(CommonBuffs.PoisionedWeapon(2, 3));
            //     }
            //     else
            //     {
            //         Output.Write("You don't have any weapons to poison.\n");
            //     }
            // };
            venom.NumUses = 2;
            venom.Description = "An organ extracted from a venomous animal.";
            venom.Weight = 0.1F;
            return venom;
        }

        //public static Item MakeBatWing()
        //{
        //    var item = new Item("Bat Wing");
        //    return item;
        //}

        //public static Item MakeGuano()
        //{
        //    var item = new Item("Guano");
        //    return item;
        //}

        public static Item MakeSpiderSilk()
        {
            Item silk = new ArmorModifierItem("Spider Silk", [EquipSpots.Hands, EquipSpots.Feet, EquipSpots.Head])
            {
                Weight = .1,
                Description = "A bundle of spider silk.",
                Warmth = .5
            };
            // silk.UseEffect = (player) =>
            // {
            //     Output.Write("You use this to improve the warmth of your clothing.\n");
            //     if (!player.ModifyArmor(EquipSpots.Chest, warmth: 1))
            //     {
            //         Output.Write("You don't have any clothing to improve.\n");
            //         silk.NumUses += 1;
            //     }
            // };
            return silk;

        }

        public static Weapon MakeGoblinSword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Iron)
            {
                Name = "Goblin Sword"
            };
            return sword;
        }

        public static Armor MakeTatteredCloth()
        {
            Armor cloth = new Armor("Tattered Cloth", .3, EquipSpots.Head, .5);
            return cloth;
        }

        public static Item MakeDragonScale()
        {
            Item scale = new ArmorModifierItem("Dragon Scale", [EquipSpots.Chest])
            {
                Description = "A large scale from a dragon. Can be used to improve armor.",
                NumUses = 1,
                Rating = 6
            };

            return scale;
        }

        public static Item MakeDragonTooth()
        {
            Item tooth = new WeaponModifierItem("Dragon Tooth")
            {
                Description = "A large tooth from a dragon. Can be used to improve a weapon.",
                NumUses = 1,
                Damage = 6
            };
            return tooth;
        }

        // public static Item MakeLargeCoinPouch()
        // {
        //     Item item = new Item("Large Coin Pouch");
        //     item.UseEffect = (player) =>
        //     {
        //         int num = Utils.RandInt(3, 5);
        //         Output.Write("It contained " + num + " coins\n");
        //         for (int i = 0; i < num; i++)
        //         {
        //             player.TakeItem(MakeCoin());
        //         }
        //     };
        //     item.NumUses = 1;
        //     return item;
        // }

        //public static Item MakeBoneFragments()
        //{
        //    var item = new Item("Bone Fragments");
        //    return item;
        //}

        public static Weapon MakeRustySword()
        {
            Weapon sword = new Weapon(WeaponType.Sword, WeaponMaterial.Iron, "Rusty Sword", 20)
            {
                Description = "A rusty sword.",
                Weight = 2.0F
            };
            return sword;
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
            Armor pants = new Armor("Cloth Pants", .02, EquipSpots.Legs, 2)
            {
                EquipSpot = EquipSpots.Legs
            };
            return pants;
        }

        public static Armor MakeBoots()
        {
            Armor shoes = new Armor("Boots", .01, EquipSpots.Feet, 1)
            {
                EquipSpot = EquipSpots.Feet
            };
            return shoes;
        }
    }

}
