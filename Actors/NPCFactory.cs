using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors
{
    public static class NpcFactory
    {
        public static Animal MakeRat()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 0.5, // 0.5 kg for a rat
                fatPercent = 0.15,   // 15% fat
                musclePercent = 0.40 // 40% muscle
            };

            // Create a natural weapon for the rat
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Rodent Teeth", 100)
            {
                Damage = 2,
                Accuracy = 1.2
            };

            Animal rat = new("Rat", weapon, bodyStats)
            {
                Description = "A rat with fleas."
            };
            rat.AddLoot(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Animal MakeWolf()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 40,   // 40 kg - average wolf
                fatPercent = 0.20,    // 20% fat
                musclePercent = 0.60  // 60% muscle - wolves are muscular
            };

            // Create a natural weapon for the wolf
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Wolf Fangs", 100)
            {
                Damage = 10,
                Accuracy = 1.1
            };

            Animal wolf = new("Wolf", weapon, bodyStats)
            {
                Description = "A wolf."
            };
            wolf.AddLoot(ItemFactory.MakeLargeMeat());
            return wolf;
        }

        public static Animal MakeBear()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 250,  // 250 kg - large bear
                fatPercent = 0.30,    // 30% fat - bears have more fat reserves
                musclePercent = 0.55  // 55% muscle
            };

            // Create a natural weapon for the bear
            var weapon = new Weapon(WeaponType.Claws, WeaponMaterial.Organic, "Bear Claws", 100)
            {
                Damage = 20,
                Accuracy = 0.9
            };

            Animal bear = new("Bear", weapon, bodyStats)
            {
                Description = "A bear."
            };
            bear.AddLoot(ItemFactory.MakeLargeMeat());
            return bear;
        }

        public static Animal MakeSnake()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Serpentine,
                overallWeight = 5,    // 5 kg - medium sized snake
                fatPercent = 0.10,    // 10% fat
                musclePercent = 0.80  // 80% muscle - snakes are almost all muscle
            };

            // Create a natural weapon for the snake
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Venomous Fangs", 100)
            {
                Damage = 10,
                Accuracy = 1.0
            };

            Animal snake = new("Snake", weapon, bodyStats)
            {
                Description = "A venomous snake."
            };

            LootTable loot = new LootTable();
            loot.AddItem(ItemFactory.MakeSmallMeat, 2);
            loot.AddItem(ItemFactory.MakeVenomSac);
            snake.AddLoot(loot.GenerateRandomItem());

            // TODO: Apply venom effect
            // snake.ApplyEffect(new PoisonEffect("venom", "natural", 0.8, 2, 180));

            return snake;
        }

        public static Animal MakeBat()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Flying,
                overallWeight = 0.2,   // 200g - small bat
                fatPercent = 0.20,     // 20% fat 
                musclePercent = 0.65   // 65% muscle - flying requires strong muscles
            };

            // Create a natural weapon for the bat
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Tiny Teeth", 100)
            {
                Damage = 2,
                Accuracy = 0.9
            };

            Animal bat = new("Bat", weapon, bodyStats)
            {
                Description = "A small bat with leathery wings."
            };

            return bat;
        }

        public static Animal MakeSpider()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Arachnid,
                overallWeight = 0.1,   // 100g - large spider
                fatPercent = 0.05,     // 5% fat
                musclePercent = 0.45   // 45% muscle
            };

            // Create a natural weapon for the spider
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Venomous Mandibles", 100)
            {
                Damage = 5,
                Accuracy = 1.2
            };

            Animal spider = new("Spider", weapon, bodyStats)
            {
                Description = "A venomous spider with long hairy legs."
            };

            // TODO: Apply venom effect
            // spider.ApplyEffect(new PoisonEffect("venom", "natural", 0.6, 1, 120));

            var loot = new LootTable();
            loot.AddItem(ItemFactory.MakeSpiderSilk);
            loot.AddItem(ItemFactory.MakeVenomSac);
            spider.AddLoot(loot.GenerateRandomItem());

            return spider;
        }

        public static Animal MakeCaveBear()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 350,  // 350 kg - larger than a regular bear
                fatPercent = 0.35,    // 35% fat - more for cave survival
                musclePercent = 0.55  // 55% muscle
            };

            // Create a natural weapon for the cave bear - stronger than regular bear
            var weapon = new Weapon(WeaponType.Claws, WeaponMaterial.Organic, "Massive Cave Bear Claws", 100)
            {
                Damage = 25,
                Accuracy = 0.85
            };

            Animal caveBear = new("Cave Bear", weapon, bodyStats)
            {
                Description = "An enormous cave bear with massive claws. It's adapted to cave dwelling and hunting in darkness."
            };

            // Add more meat due to larger size
            caveBear.AddLoot(ItemFactory.MakeLargeMeat());
            caveBear.AddLoot(ItemFactory.MakeLargeMeat());

            return caveBear;
        }

        public static Animal MakeWoollyMammoth()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 6000,  // 6 tons - enormous
                fatPercent = 0.35,     // 35% fat for cold protection
                musclePercent = 0.50   // 50% muscle
            };

            // Create a natural weapon for the mammoth
            var weapon = new Weapon(WeaponType.Horns, WeaponMaterial.Organic, "Mammoth Tusks", 100)
            {
                Damage = 35,
                Accuracy = 0.7
            };

            Animal mammoth = new("Woolly Mammoth", weapon, bodyStats)
            {
                Description = "A massive woolly mammoth with long curved tusks and a thick fur coat."
            };

            // Add large amount of meat and other rare resources
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());

            return mammoth;
        }

        public static Animal MakeSaberToothTiger()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Quadruped,
                overallWeight = 300,   // 300 kg - large cat
                fatPercent = 0.15,     // 15% fat
                musclePercent = 0.70   // 70% muscle - extremely powerful
            };

            // Create a natural weapon for the saber-tooth
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Massive Fangs", 100)
            {
                Damage = 30,
                Accuracy = 1.0
            };

            Animal saberTooth = new("Saber-Tooth Tiger", weapon, bodyStats)
            {
                Description = "A fearsome predator with long saber-like canine teeth."
            };

            saberTooth.AddLoot(ItemFactory.MakeLargeMeat());
            saberTooth.AddLoot(ItemFactory.MakeLargeMeat());

            return saberTooth;
        }

        // Human NPCs with various weapons
        public static Npc MakeTribalHunter()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Human,
                overallWeight = 65,
                fatPercent = 0.15,
                musclePercent = 0.60
            };

            // Create a hunting spear
            var weapon = new Weapon(WeaponType.Spear, WeaponMaterial.Wood, "Hunter's Wooden Spear", 75)
            {
                Damage = 8,
                Accuracy = 1.2
            };

            // Create hunter with spear
            Npc hunter = new("Tribal Hunter", weapon, bodyStats)
            {
                Description = "A lean, muscular hunter from a nearby tribe."
            };

            // Add some basic equipment to loot
            hunter.AddLoot(ItemFactory.MakeSmallMeat());

            return hunter;
        }

        public static Npc MakeTribalWarrior()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Human,
                overallWeight = 70,
                fatPercent = 0.15,
                musclePercent = 0.65
            };

            // Create a war club
            var weapon = new Weapon(WeaponType.Club, WeaponMaterial.Stone, "Warrior's War-Club", 80)
            {
                Damage = 12,
                Accuracy = 0.9
            };

            // Create warrior with club
            Npc warrior = new("Tribal Warrior", weapon, bodyStats)
            {
                Description = "A fierce warrior with ritual paint markings."
            };

            // Add some loot
            warrior.AddLoot(new Weapon(WeaponType.Knife, WeaponMaterial.Flint, "Knapped-Flint Scraper", 60));

            return warrior;
        }

        public static Npc MakeTribalShaman()
        {
            var bodyStats = new BodyStats
            {
                type = BodyPartFactory.BodyTypes.Human,
                overallWeight = 60,
                fatPercent = 0.20,
                musclePercent = 0.45
            };

            // Create a ritual staff
            var weapon = new Weapon(WeaponType.Knife, WeaponMaterial.Bone, "Shamanic Bone-Knife", 90)
            {
                Damage = 6,
                Accuracy = 1.1
            };

            // Create shaman with staff
            Npc shaman = new("Tribal Shaman", weapon, bodyStats)
            {
                Description = "An elderly shaman adorned with animal bones and feathers."
            };

            // Add some rare loot
            shaman.AddLoot(new Weapon(WeaponType.Knife, WeaponMaterial.Obsidian, "Night-Glass Ritual Knife", 95));

            return shaman;
        }
    }
}