using text_survival.Crafting;

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
            var mushroom = new FoodItem("Wild Mushroom", 120, 5)
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

        public static FoodItem MakeNuts()
        {
            var item = new FoodItem("Nuts", 100, 5)
            {
                Description = "A handful of nuts gathered from forest trees. High in fats and energy.",
                Weight = 0.15F,
            };
            return item;
        }

        public static FoodItem MakeEggs()
        {
            var item = new FoodItem("Bird Eggs", 150, 30)
            {
                Description = "Fresh eggs found in a bird's nest. Rich in protein and nutrients.",
                Weight = 0.2F,
            };
            return item;
        }

        public static FoodItem MakeGrubs()
        {
            var item = new FoodItem("Grubs", 80, 10)
            {
                Description = "Plump insect larvae found under bark or in rotting logs. Surprisingly nutritious.",
                Weight = 0.05F,
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
            Item stick = new Item("Large Stick")
            {
                Description = "A strong branch, useful for making tools and weapons.",
                Weight = 0.5,
                FuelMassKg = 0.5,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Wood, ItemProperty.Fuel_Kindling]
            };
            return stick;
        }

        public static Item MakeFirewood()
        {
            var wood = new Item("Firewood")
            {
                Description = "Dry softwood gathered for making fires. Essential for warmth and cooking.",
                Weight = 1.5,
                FuelMassKg = 1.5,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Wood, ItemProperty.Fuel_Softwood]
            };
            return wood;
        }

        public static Item MakeFlint()
        {
            var flint = new Item("Flint")
            {
                Description = "Sharp-edged stone perfect for making cutting tools and starting fires.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Flint, ItemProperty.Firestarter, ItemProperty.Sharp]
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

        // --- Phase 2: Tinder and Fire-Starting Materials ---

        public static Item MakeDryGrass()
        {
            var item = new Item("Dry Grass")
            {
                Description = "Dried grass stems, brittle and sun-bleached. Catches fire easily when bundled.",
                Weight = 0.02,
                FuelMassKg = 0.02,
                CraftingProperties = [ItemProperty.Tinder, ItemProperty.Fuel_Tinder, ItemProperty.Flammable, ItemProperty.Insulation]
            };
            return item;
        }

        public static Item MakeBarkStrips()
        {
            var item = new Item("Bark Strips")
            {
                Description = "Papery inner bark peeled from birch or cedar. Burns well and can be twisted into cordage.",
                Weight = 0.05,
                FuelMassKg = 0.05,
                CraftingProperties = [ItemProperty.Tinder, ItemProperty.Fuel_Tinder, ItemProperty.Binding, ItemProperty.Flammable]
            };
            return item;
        }

        public static Item MakeTinderBundle()
        {
            var item = new Item("Tinder Bundle")
            {
                Description = "A carefully prepared nest of dry grass, bark shavings, and plant fluff. Ready to catch the smallest spark.",
                Weight = 0.03,
                FuelMassKg = 0.03,
                CraftingProperties = [ItemProperty.Tinder, ItemProperty.Fuel_Tinder, ItemProperty.Flammable]
            };
            return item;
        }

        public static Item MakePineSap()
        {
            var item = new Item("Pine Sap")
            {
                Description = "Thick golden resin oozing from tree bark. Sticky and fragrant, it hardens in cold air and burns hot. Can be used for waterproofing and adhesives.",
                Weight = 0.1,
                CraftingProperties = [ItemProperty.Adhesive, ItemProperty.Waterproofing, ItemProperty.Flammable]
            };
            return item;
        }

        // --- Fire-Making Tools ---

        public static Item MakeHandDrill()
        {
            var tool = new Item("Hand Drill", 0.3)
            {
                Description = "A simple friction fire starter made from a straight wooden spindle and flat board. Requires skill and patience.",
                CraftingProperties = [ItemProperty.Wood]
            };
            tool.SetDurability(5);
            return tool;
        }

        public static Item MakeBowDrill()
        {
            var tool = new Item("Bow Drill", 0.5)
            {
                Description = "An improved friction fire starter using a bow to spin the drill. More efficient than a hand drill.",
                CraftingProperties = [ItemProperty.Wood]
            };
            tool.SetDurability(8);
            return tool;
        }

        public static Item MakeFlintAndSteel()
        {
            var tool = new Item("Flint and Steel", 0.4)
            {
                Description = "A piece of flint struck against steel creates hot sparks. The most reliable fire-starting method.",
                CraftingProperties = [ItemProperty.Flint, ItemProperty.Stone]
            };
            tool.SetDurability(15);
            return tool;
        }

        // --- Phase 2: Plant Fiber Materials ---

        public static Item MakePlantFibers()
        {
            var item = new Item("Plant Fibers")
            {
                Description = "Tough fibers stripped from plant stems and bark. Can be twisted into serviceable cordage.",
                Weight = 0.04,
                CraftingProperties = [ItemProperty.PlantFiber, ItemProperty.Binding]
            };
            return item;
        }

        public static Item MakeRushes()
        {
            var item = new Item("Rushes")
            {
                Description = "Long, fibrous wetland plants. Useful for weaving, binding, and insulation when dried.",
                Weight = 0.06,
                CraftingProperties = [ItemProperty.PlantFiber, ItemProperty.Binding, ItemProperty.Insulation]
            };
            return item;
        }

        // --- Phase 2: Processed Materials ---

        public static Item MakeCharcoal()
        {
            var item = new Item("Charcoal")
            {
                Description = "Blackened wood from incomplete burning. Can be used to harden other wood in fire or as fuel.",
                Weight = 0.05,
                FuelMassKg = 0.05,
                CraftingProperties = [ItemProperty.Charcoal, ItemProperty.Flammable, ItemProperty.Fuel_Softwood]
            };
            return item;
        }

        // --- Phase 2: Stone Variety ---

        public static Item MakeSmallStone()
        {
            var item = new Item("Small Stone")
            {
                Description = "A small, rounded stone. Good for smashing or as a hammer.",
                Weight = 0.3,
                CraftingProperties = [ItemProperty.Stone]
            };
            return item;
        }

        public static Item MakeSharpStone()
        {
            var item = new Item("Sharp Stone")
            {
                Description = "A jagged stone broken to reveal sharp edges. Can be used as a crude cutting tool.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Stone, ItemProperty.Sharp]
            };
            return item;
        }

        public static Item MakeHandstone()
        {
            var item = new Item("Handstone")
            {
                Description = "A dense, fist-sized stone that fits comfortably in hand. Ideal for pounding and hammering.",
                Weight = 0.4,
                CraftingProperties = [ItemProperty.Stone]
            };
            return item;
        }

        public static Item MakeBone()
        {
            var bone = new Item("Animal Bone")
            {
                Description = "A sturdy bone from a large animal. Good material for tools and weapons, or slow-burning fuel.",
                Weight = 0.3,
                FuelMassKg = 0.3,
                CraftingProperties = [ItemProperty.Bone, ItemProperty.Fuel_Bone]
            };
            return bone;
        }

        // --- Fuel Types ---

        public static Item MakeHardwoodLog()
        {
            var log = new Item("Hardwood Log")
            {
                Description = "Dense hardwood from oak or ash. Burns hot and slow, ideal for long-lasting fires.",
                Weight = 2.0,
                FuelMassKg = 2.0,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flammable, ItemProperty.Fuel_Hardwood]
            };
            return log;
        }

        public static Item MakePeatBlock()
        {
            var peat = new Item("Peat Block")
            {
                Description = "Compressed organic material from ancient bogs. Burns slowly with a distinctive smoky smell.",
                Weight = 0.8,
                FuelMassKg = 0.8,
                CraftingProperties = [ItemProperty.Flammable, ItemProperty.Fuel_Peat]
            };
            return peat;
        }

        // ===== SPEAR PROGRESSION (Tier 1-5) =====

        public static Weapon MakeSharpenedStick()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Wood, "Sharpened Stick")
            {
                Description = "A simple wooden stick sharpened to a point. Crude but better than nothing.",
                Weight = 1.0,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return spear;
        }

        public static Weapon MakeFireHardenedSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Wood, "Fire-Hardened Spear")
            {
                Description = "A wooden spear with its tip hardened in fire. Significantly more durable than a sharpened stick.",
                Weight = 1.2,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return spear;
        }

        public static Weapon MakeFlintTippedSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Flint, "Flint-Tipped Spear")
            {
                Description = "A sturdy spear with a sharp flint point lashed to the shaft. A proper hunting weapon.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flint, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return spear;
        }

        public static Weapon MakeBoneTippedSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Bone, "Bone-Tipped Spear")
            {
                Description = "A fire-hardened bone point attached to a wooden shaft with sinew. Excellent penetration.",
                Weight = 1.4,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Bone, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return spear;
        }

        public static Weapon MakeObsidianSpear()
        {
            Weapon spear = new Weapon(WeaponType.Spear, WeaponMaterial.Obsidian, "Obsidian Spear")
            {
                Description = "A razor-sharp obsidian point lashed to a carefully balanced shaft. The finest hunting spear an Ice Age hunter could craft.",
                Weight = 1.6,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Obsidian, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return spear;
        }

        // ===== CLUB PROGRESSION (Tier 1-3) =====

        public static Weapon MakeHeavyStick()
        {
            Weapon club = new Weapon(WeaponType.Club, WeaponMaterial.Wood, "Heavy Stick")
            {
                Description = "A thick, heavy branch. Can be found naturally or used as a crude weapon.",
                Weight = 1.5,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flammable]
            };
            return club;
        }

        public static Weapon MakeStoneWeightedClub()
        {
            Weapon club = new Weapon(WeaponType.Club, WeaponMaterial.Stone, "Stone-Weighted Club")
            {
                Description = "A heavy stick with a stone lashed to one end. Delivers crushing blows that can break bones and skulls.",
                Weight = 2.0,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Stone, ItemProperty.Binding, ItemProperty.Flammable]
            };
            return club;
        }

        public static Weapon MakeBoneStuddedClub()
        {
            Weapon club = new Weapon(WeaponType.Club, WeaponMaterial.Bone, "Bone-Studded Club")
            {
                Description = "A heavy club with sharpened bone spikes embedded along its length. Each strike crushes and tears flesh.",
                Weight = 2.2,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Bone, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return club;
        }

        // ===== HAND AXE PROGRESSION (Tier 1-2) =====

        public static Weapon MakeStoneHandAxe()
        {
            Weapon axe = new Weapon(WeaponType.HandAxe, WeaponMaterial.Stone, "Stone Hand Axe")
            {
                Description = "A sharp stone blade bound to a wooden handle. Useful for chopping wood and harvesting.",
                Weight = 1.8,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Stone, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return axe;
        }

        public static Weapon MakeFlintHandAxe()
        {
            Weapon axe = new Weapon(WeaponType.HandAxe, WeaponMaterial.Flint, "Flint Hand Axe")
            {
                Description = "A carefully shaped flint blade bound to a sturdy handle with sinew. Bites deep into wood with each strike.",
                Weight = 1.6,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flint, ItemProperty.Binding, ItemProperty.Flammable, ItemProperty.Sharp]
            };
            return axe;
        }

        // ===== KNIFE PROGRESSION (Tier 1-4) =====

        public static Weapon MakeSharpRock()
        {
            Weapon rock = new Weapon(WeaponType.SharpStone, WeaponMaterial.Stone, "Sharp Rock")
            {
                Description = "A crude stone with a sharp edge created by smashing rocks together. Barely functional as a cutting tool.",
                Weight = 0.3,
                CraftingProperties = [ItemProperty.Stone, ItemProperty.Sharp]
            };
            return rock;
        }

        public static Weapon MakeFlintKnife()
        {
            Weapon knife = new Weapon(WeaponType.Knife, WeaponMaterial.Flint, "Flint Knife")
            {
                Description = "A razor-sharp flint blade lashed to a wooden handle. Excellent for skinning and cutting.",
                Weight = 0.4,
                CraftingProperties = [ItemProperty.Flint, ItemProperty.Wood, ItemProperty.Binding, ItemProperty.Sharp]
            };
            return knife;
        }

        public static Weapon MakeBoneKnife()
        {
            Weapon knife = new Weapon(WeaponType.Knife, WeaponMaterial.Bone, "Bone Knife")
            {
                Description = "A fire-hardened bone blade that holds its edge well. Excellent for skinning and butchering game.",
                Weight = 0.3,
                CraftingProperties = [ItemProperty.Bone, ItemProperty.Binding, ItemProperty.Sharp]
            };
            return knife;
        }

        public static Weapon MakeObsidianBlade()
        {
            Weapon blade = new Weapon(WeaponType.Knife, WeaponMaterial.Obsidian, "Obsidian Blade")
            {
                Description = "A wickedly sharp blade of volcanic glass mounted on an antler handle. The finest cutting tool known to Ice Age hunters.",
                Weight = 0.35,
                CraftingProperties = [ItemProperty.Obsidian, ItemProperty.Antler, ItemProperty.Binding, ItemProperty.Sharp]
            };
            return blade;
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
            Item silk = new Item("Spider Silk")
            {
                Weight = 0.1,
                Description = "Fine, strong threads painstakingly collected from spider webs. Surprisingly useful for binding and insulation.",
                CraftingProperties = [ItemProperty.Binding, ItemProperty.Insulation]
            };
            return silk;
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
                Description = "A long, curved fang from a saber-toothed cat. The apex predator's tooth makes a formidable weapon component.",
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

        public static Item MakeObsidianShard()
        {
            Item obsidian = new Item("Obsidian Shard")
            {
                Description = "A piece of naturally occurring volcanic glass. Can be knapped into extremely sharp tools.",
                Weight = 0.2,
                CraftingProperties = [ItemProperty.Obsidian, ItemProperty.Sharp]
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

        // ===== HUNTING SYSTEM - RANGED WEAPONS (MVP Phase 3) =====

        public static RangedWeapon MakeSimpleBow()
        {
            RangedWeapon bow = RangedWeapon.CreateSimpleBow(craftsmanship: 50);
            bow.Description = "A simple hunting bow crafted from flexible wood and animal sinew. Deadly in skilled hands at medium range.";
            bow.CraftingProperties = [ItemProperty.Wood, ItemProperty.Binding, ItemProperty.Ranged];
            return bow;
        }

        public static Item MakeStoneArrow()
        {
            Item arrow = new Item("Stone Arrow")
            {
                Description = "A simple arrow with a sharpened stone tip and feather fletching. Basic ammunition for hunting.",
                Weight = 0.05,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Stone, ItemProperty.Ammunition]
            };
            return arrow;
        }

        public static Item MakeFlintArrow()
        {
            Item arrow = new Item("Flint Arrow")
            {
                Description = "An arrow with a knapped flint tip. Sharp and reliable for bringing down game.",
                Weight = 0.06,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Flint, ItemProperty.Ammunition]
            };
            return arrow;
        }

        public static Item MakeBoneArrow()
        {
            Item arrow = new Item("Bone Arrow")
            {
                Description = "An arrow tipped with sharpened bone. Excellent penetration against thick hides.",
                Weight = 0.05,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Bone, ItemProperty.Ammunition]
            };
            return arrow;
        }

        public static Item MakeObsidianArrow()
        {
            Item arrow = new Item("Obsidian Arrow")
            {
                Description = "An arrow with a razor-sharp obsidian tip. The finest hunting ammunition available.",
                Weight = 0.06,
                CraftingProperties = [ItemProperty.Wood, ItemProperty.Obsidian, ItemProperty.Ammunition]
            };
            return arrow;
        }
    }
}