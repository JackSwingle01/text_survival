﻿using text_survival.Actors;
using text_survival.Crafting;
using text_survival.Effects;


namespace text_survival.Items
{
    public class ItemFactory
    {
        public static Weapon MakeFists()
        {
            return new Weapon(WeaponType.Unarmed, WeaponMaterial.Organic, "Bare Hands");
        }

        public static FoodItem MakeMushroom()
        {
            var mushroom = new FoodItem("Wild Mushroom", 25, 5)
            {
                Description = "A forest mushroom. Some varieties are nutritious, others are deadly.",
                Weight = 0.1F
            };

            double strength = Utils.RandDouble(1, 15);
            string targetOrgan = Utils.GetRandomFromList(["Stomach", "Liver", "Kidney"]);

            if (Utils.FlipCoin())
            {
                mushroom.HealthEffect = new()
                {
                    Amount = strength,
                    Type = "herbal",
                    TargetPart = targetOrgan,
                    Quality = Utils.RandDouble(0, 1.5)
                };
            }
            else
            {
                mushroom.DamageEffect = new()
                {
                    Amount = strength * .66,
                    Type = Bodies.DamageType.Poison,
                    TargetPartName = targetOrgan,
                };
            }
            return mushroom;
        }

        public static FoodItem MakeBerry()
        {
            var item = new FoodItem("Wild Berries", 120, 100);
            string color = Utils.GetRandomFromList(["red", "blue", "black", "purple"]);
            string season = Utils.GetRandomFromList(["autumn", "summer"]);
            item.Description = $"A handful of {color} {season} berries. Sweet and juicy.";
            item.Weight = 0.1F;
            return item;
        }

        public static FoodItem MakeRoots()
        {
            var item = new FoodItem("Foraged Roots", 100, 20)
            {
                Description = "Starchy roots dug from the ground. Tough but nutritious.",
                Weight = 0.3F,
            };
            return item;
        }

        public static FoodItem MakeWater()
        {
            var item = new FoodItem("Fresh Water", 0, 1000)
            {
                Description = "Clear water collected from a stream. Stored in a water skin made from animal bladder.",
                Weight = 1
            };
            return item;
        }

        public static Item MakeStick()
        {
            Item stick = new Item("Sturdy Stick")
            {
                Description = "A strong branch, useful for making tools and weapons.",
                Weight = 0.5,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Wood]
            };
            return stick;
        }

        public static Item MakeFirewood()
        {
            var wood = new Item("Firewood")
            {
                Description = "Dry wood gathered for making fires. Essential for warmth and cooking.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Wood]
            };
            return wood;
        }

        public static Item MakeFlint()
        {
            var flint = new Item("Flint")
            {
                Description = "Sharp-edged stone perfect for making cutting tools and starting fires.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Stone, ItemProperty.Firestarter, ItemProperty.Sharp]
            };
            return flint;
        }

        public static Item MakeClay()
        {
            var clay = new Item("River Clay")
            {
                Description = "Malleable clay gathered from a riverbank. Could be shaped into vessels.",
                Weight = 1.0,
                CraftingProperties = [ItemProperty.Clay]
            };
            return clay;
        }

        public static Item MakeStone()
        {
            var item = new Item("River Stone")
            {
                Description = "A smooth river stone. Useful for tools or cooking.",
                Weight = 0.5,
                CraftingProperties = [ItemProperty.Stone]
            };
            return item;
        }

        public static Item MakeBone()
        {
            var bone = new Item("Animal Bone")
            {
                Description = "A sturdy bone from a large animal. Good material for tools and weapons.",
                Weight = 0.3,
                CraftingProperties = [ItemProperty.Bone]
            };
            return bone;
        }

        public static Weapon MakeSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Wood, "Hunting Spear")
            {
                Description = "A long wooden shaft with a sharpened flint point. Good for hunting and defense.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flammable, ItemProperty.Stone, ItemProperty.Sharp]
            };
            return spear;
        }

        public static Weapon MakeClub()
        {
            Weapon club = new Weapon(WeaponType.Club, WeaponMaterial.Wood, "War Club")
            {
                Description = "A heavy wooden club reinforced with stone. Brutal but effective.",
                Weight = 2.0,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Stone, ItemProperty.Flammable]
            };
            return club;
        }

        public static Weapon MakeHandAxe()
        {
            Weapon axe = new Weapon(WeaponType.HandAxe, WeaponMaterial.Stone, "Stone Hand Axe")
            {
                Description = "A sharp stone blade bound to a wooden handle with animal sinew.",
                Weight = 1.8,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Stone, ItemProperty.Flammable, ItemProperty.Binding, ItemProperty.Sharp]
            };
            return axe;
        }

        public static Weapon MakeKnife()
        {
            Weapon knife = new Weapon(WeaponType.Knife, WeaponMaterial.Flint, "Flint Knife")
            {
                Description = "A razor-sharp flint blade with a bone handle. Essential for skinning and cutting.",
                Weight = 0.4,
                CraftingProperties = [ItemProperty.Stone, ItemProperty.Bone, ItemProperty.Sharp]
            };
            return knife;
        }

        public static Armor MakeHideShield()
        {
            Armor shield = new Armor("Hide Shield", .15, EquipSpots.Hands, 1)
            {
                Description = "A wooden frame covered with animal hide. Offers basic protection.",
                Weight = 2.0,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Hide, ItemProperty.Binding]
            };
            return shield;
        }

        public static Armor MakeFurArmor()
        {
            Armor armor = new Armor("Fur Armor", .25, EquipSpots.Chest, 8)
            {
                Description = "A thick fur pelt worn as protection. Offers warmth and some defense against attacks.",
                Weight = 3.0,
                CraftingProperties = [ItemProperty.Hide, ItemProperty.Insulation, ItemProperty.Binding]
            };
            return armor;
        }

        public static FoodItem MakeLargeMeat()
        {
            var item = new FoodItem("Large Game Meat", 600, 0)
            {
                Description = "A substantial cut of meat from a large animal. Will need to be cooked.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.RawMeat, ItemProperty.Fat]
            };
            return item;
        }

        public static FoodItem MakeSmallMeat()
        {
            var item = new FoodItem("Small Game Meat", 200, 0)
            {
                Description = "A modest portion of meat from a small animal. Best cooked before eating.",
                Weight = 0.5,
                CraftingProperties = [ItemProperty.RawMeat, ItemProperty.Fat]
            };
            return item;
        }

        public static Item MakeHealingHerbs()
        {
            var herbs = new FoodItem("Healing Herbs", 10, 5)
            {
                Description = "A bundle of medicinal plants known for their healing properties.",
                Weight = 0.2,
                NumUses = 1,
                HealthEffect = new()
                {
                    Amount = 15,
                    Type = "herbal",
                    Quality = 0.7,
                }
            };
            return herbs;
        }

        public static Item MakeBandage()
        {
            var bandage = new ConsumableItem("Bark Bandage")
            {
                Description = "Strips of inner tree bark pounded soft. Can bind wounds and stop bleeding.",
                Weight = 0.1,
                CraftingProperties = [ItemProperty.Binding],
                Effects = [
                    EffectBuilderExtensions.CreateEffect("stop bleed").From("bandage").ClearsEffectType("bleeding").AsInstantEffect().Build()
                ]
            };
            return bandage;
        }

        public static Gear MakeTorch()
        {
            Gear torch = new Gear("Pine Torch", 0.8)
            {
                Description = "A branch wrapped with resin-soaked pine needles. Provides light and warmth.",
                Insulation = 0.2,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Wood]
            };
            return torch;
        }

        public static FoodItem MakeFish()
        {
            var item = new FoodItem("River Fish", 200, 0)
            {
                Description = "A freshly caught fish. Rich in nutrients and relatively easy to obtain near water.",
                Weight = 0.4,
                CraftingProperties = [ItemProperty.RawMeat, ItemProperty.Fat]
            };
            return item;
        }

        public static Item MakeVenomSac()
        {
            Item venom = new WeaponModifierItem("Venom Sac")
            {
                Description = "A fragile sac of venom extracted from a poisonous creature. Could coat a weapon.",
                Weight = 0.1,
                NumUses = 2,
                Damage = 2,
                CraftingProperties = [ItemProperty.Poison]
            };
            return venom;
        }

        public static Item MakeSpiderSilk()
        {
            Item silk = new ArmorModifierItem("Spider Silk", [EquipSpots.Hands, EquipSpots.Feet, EquipSpots.Head])
            {
                Weight = 0.1,
                Description = "Fine, strong threads collected from giant spider webs. Useful for binding and insulation.",
                Warmth = 0.5,
                CraftingProperties = [ItemProperty.Binding, ItemProperty.Insulation]
            };
            return silk;
        }

        public static Armor MakeFurHood()
        {
            Armor hood = new Armor("Fur Hood", .05, EquipSpots.Head, .8)
            {
                Description = "A hood made from animal fur. Keeps the head and ears warm in frigid weather.",
                Weight = 0.3,
                CraftingProperties = [ItemProperty.Hide, ItemProperty.Insulation]
            };
            return hood;
        }

        public static Armor MakeLeatherTunic()
        {
            Armor tunic = new Armor("Leather Tunic", .10, EquipSpots.Chest, .12)
            {
                Description = "A simple tunic made from tanned animal hide. Basic protection from the elements.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.Hide, ItemProperty.Binding, ItemProperty.Insulation]
            };
            return tunic;
        }

        public static Armor MakeLeatherPants()
        {
            Armor leggings = new Armor("Leather Pants", .08, EquipSpots.Legs, .1)
            {
                Description = "Pants made from tanned animal hide. Protects the legs from brush and minor injuries.",
                Weight = 1.0,
                CraftingProperties = [ItemProperty.Hide, ItemProperty.Binding, ItemProperty.Insulation]
            };
            return leggings;
        }

        public static Armor MakeMoccasins()
        {
            Armor shoes = new Armor("Hide Moccasins", .03, EquipSpots.Feet, .06)
            {
                Description = "Soft footwear made from animal hide. More durable than bare feet on rough terrain.",
                Weight = 0.4,
                CraftingProperties = [ItemProperty.Hide, ItemProperty.Binding, ItemProperty.Insulation]
            };
            return shoes;
        }

        public static Item MakeMammothTusk()
        {
            Item tusk = new WeaponModifierItem("Mammoth Tusk")
            {
                Description = "A massive curved tusk from a woolly mammoth. Extremely valuable and rare.",
                Weight = 10.0,
                NumUses = 1,
                Damage = 5,
                CraftingProperties = [ItemProperty.Bone, ItemProperty.Sharp]
            };
            return tusk;
        }

        public static Item MakeSaberToothFang()
        {
            Item fang = new WeaponModifierItem("Saber-Tooth Fang")
            {
                Description = "A long, curved fang from a saber-tooth tiger. Could be fashioned into a deadly weapon.",
                Weight = 0.3,
                NumUses = 1,
                Damage = 4,
                CraftingProperties = [ItemProperty.Bone, ItemProperty.Sharp]
            };
            return fang;
        }

        public static Item MakeAntlerTine()
        {
            Item antler = new Item("Antler Tine")
            {
                Description = "A prong from a deer or elk antler. Useful for punching holes in hide or as a tool.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Bone, ItemProperty.Sharp]
            };
            return antler;
        }

        public static Item MakeSinew()
        {
            Item sinew = new Item("Animal Sinew")
            {
                Description = "Tough fibrous tissue from animal tendons. Essential for binding, sewing and bowstrings.",
                Weight = 0.1,
                CraftingProperties = [ItemProperty.Binding]
            };
            return sinew;
        }

        public static Armor MakeBoneNecklace()
        {
            Armor necklace = new Armor("Bone Talisman", 0, EquipSpots.Chest, 0.5)
            {
                Description = "A primitive necklace made from small bones and stones. Said to bring good fortune.",
                Weight = 0.1,
                CraftingProperties = [ItemProperty.Bone]
            };
            return necklace;
        }

        public static Item MakeObsidianShard()
        {
            Item obsidian = new Item("Obsidian Shard")
            {
                Description = "A piece of naturally occurring volcanic glass. Can be knapped into extremely sharp tools.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Stone, ItemProperty.Sharp]
            };
            return obsidian;
        }

        public static Item MakeOchrePigment()
        {
            Item ochre = new Item("Red Ochre")
            {
                Description = "Earthy clay pigment used for body decoration, cave paintings, and hide treatment.",
                Weight = 0.3
                // No crafting properties - purely decorative/ceremonial item
            };
            return ochre;
        }
    }
}