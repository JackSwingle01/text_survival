using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors.NPCs
{
    public static class NpcFactory
    {
        public static Animal MakeRat()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 2, // large rat
                fatPercent = 0.15,   // 15% fat
                musclePercent = 0.40 // 40% muscle
            };

            // Create a natural weapon for the rat
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "teeth", 100)
            {
                Damage = 2,
                Accuracy = 1.2
            };

            Animal rat = new("Rat", weapon, bodyStats, AnimalBehaviorType.Prey)
            {
                Description = "A rat with fleas.",
                TrackingDifficulty = 3 // Easy to track
            };
            rat.AddLoot(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Animal MakeWolf()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 40,   // 40 kg - average wolf
                fatPercent = 0.20,    // 20% fat
                musclePercent = 0.60  // 60% muscle - wolves are muscular
            };

            // Create a natural weapon for the wolf
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "fangs", 100)
            {
                Damage = 10,
                Accuracy = 1.1
            };

            Animal wolf = new("Wolf", weapon, bodyStats, AnimalBehaviorType.Predator)
            {
                Description = "A wolf.",
                TrackingDifficulty = 6 // Medium difficulty - intelligent predator
            };
            wolf.AddLoot(ItemFactory.MakeLargeMeat());
            return wolf;
        }

        public static Animal MakeBear()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 250,  // 250 kg - large bear
                fatPercent = 0.30,    // 30% fat - bears have more fat reserves
                musclePercent = 0.55  // 55% muscle
            };

            // Create a natural weapon for the bear
            var weapon = new Weapon(WeaponType.Claws, WeaponMaterial.Organic, "claws", 100)
            {
                Damage = 20,
                Accuracy = 0.9
            };

            Animal bear = new("Bear", weapon, bodyStats, AnimalBehaviorType.Predator)
            {
                Description = "A bear.",
                TrackingDifficulty = 5 // Medium difficulty
            };
            bear.AddLoot(ItemFactory.MakeLargeMeat());
            return bear;
        }

        public static Animal MakeSnake()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Serpentine,
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

            Animal snake = new("Snake", weapon, bodyStats, AnimalBehaviorType.Predator)
            {
                Description = "A venomous snake.",
                TrackingDifficulty = 7 // Hard to track - leaves minimal trail
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
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Flying,
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

            Animal bat = new("Bat", weapon, bodyStats, AnimalBehaviorType.Prey)
            {
                Description = "A small bat with leathery wings.",
                TrackingDifficulty = 8 // Very hard to track - flies away
            };

            return bat;
        }

        public static Animal MakeSpider()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Arachnid,
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

            Animal spider = new("Spider", weapon, bodyStats, AnimalBehaviorType.Prey)
            {
                Description = "A venomous spider with long hairy legs.",
                TrackingDifficulty = 7 // Hard to track - small and stealthy
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
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 350,  // 350 kg - larger than a regular bear
                fatPercent = 0.35,    // 35% fat - more for cave survival
                musclePercent = 0.55  // 55% muscle
            };

            // Create a natural weapon for the cave bear - stronger than regular bear
            var weapon = new Weapon(WeaponType.Claws, WeaponMaterial.Organic, "Massive Claws", 100)
            {
                Damage = 25,
                Accuracy = 0.85
            };

            Animal caveBear = new("Cave Bear", weapon, bodyStats, AnimalBehaviorType.Predator)
            {
                Description = "An enormous cave bear with massive claws. It's adapted to cave dwelling and hunting in darkness.",
                TrackingDifficulty = 4 // Easier to track - large and heavy
            };

            // Add more meat due to larger size
            caveBear.AddLoot(ItemFactory.MakeLargeMeat());
            caveBear.AddLoot(ItemFactory.MakeLargeMeat());

            return caveBear;
        }

        public static Animal MakeWoollyMammoth()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 6000,  // 6 tons - enormous
                fatPercent = 0.35,     // 35% fat for cold protection
                musclePercent = 0.50   // 50% muscle
            };

            // Create a natural weapon for the mammoth
            var weapon = new Weapon(WeaponType.Horns, WeaponMaterial.Organic, "Tusks", 100)
            {
                Damage = 35,
                Accuracy = 0.7
            };

            Animal mammoth = new("Woolly Mammoth", weapon, bodyStats, AnimalBehaviorType.DangerousPrey)
            {
                Description = "A massive woolly mammoth with long curved tusks and a thick fur coat.",
                TrackingDifficulty = 2 // Very easy to track - enormous and heavy
            };

            // Add large amount of meat and other rare resources
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());
            mammoth.AddLoot(ItemFactory.MakeLargeMeat());

            return mammoth;
        }

        public static Animal MakeSaberToothTiger()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 300,   // 300 kg - large cat
                fatPercent = 0.10,
                musclePercent = 0.70   // 70% muscle - extremely powerful
            };

            // Create a natural weapon for the saber-tooth
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Massive Fangs", 100)
            {
                Damage = 30,
                Accuracy = 1.0
            };

            Animal saberTooth = new("Saber-Tooth Tiger", weapon, bodyStats, AnimalBehaviorType.Predator)
            {
                Description = "A fearsome predator with long saber-like canine teeth.",
                TrackingDifficulty = 7 // Hard to track - stealthy apex predator
            };

            saberTooth.AddLoot(ItemFactory.MakeLargeMeat());
            saberTooth.AddLoot(ItemFactory.MakeLargeMeat());

            return saberTooth;
        }

        // Prey Animals (MVP Hunting System)
        public static Animal MakeDeer()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 80,    // 80 kg - medium deer
                fatPercent = 0.10,     // 10% fat
                musclePercent = 0.65   // 65% muscle - built for speed
            };

            // Deer natural weapons (hooves/antlers) - primarily defensive
            var weapon = new Weapon(WeaponType.Horns, WeaponMaterial.Organic, "Antlers", 100)
            {
                Damage = 5,  // Low damage - prey animals
                Accuracy = 0.8
            };

            Animal deer = new("Deer", weapon, bodyStats, AnimalBehaviorType.Prey, isHostile: false)
            {
                Description = "A graceful deer with large ears alert for danger.",
                TrackingDifficulty = 4 // Moderate - leaves clear tracks but moves fast
            };

            deer.AddLoot(ItemFactory.MakeLargeMeat());
            deer.AddLoot(ItemFactory.MakeLargeMeat());
            return deer;
        }

        public static Animal MakeRabbit()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 2,     // 2 kg - small rabbit
                fatPercent = 0.10,     // 10% fat
                musclePercent = 0.50   // 50% muscle - quick reflexes
            };

            // Rabbit has minimal defense
            var weapon = new Weapon(WeaponType.Unarmed, WeaponMaterial.Organic, "Teeth", 100)
            {
                Damage = 1,  // Minimal damage
                Accuracy = 0.5
            };

            Animal rabbit = new("Rabbit", weapon, bodyStats, AnimalBehaviorType.Prey, isHostile: false)
            {
                Description = "A quick brown rabbit with long ears, ready to bolt at any sign of danger.",
                TrackingDifficulty = 6 // Hard to track - small and erratic movements
            };

            rabbit.AddLoot(ItemFactory.MakeSmallMeat());
            return rabbit;
        }

        public static Animal MakePtarmigan()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Flying,
                overallWeight = 0.5,   // 500g - game bird
                fatPercent = 0.15,     // 15% fat
                musclePercent = 0.60   // 60% muscle - flight muscles
            };

            // Ptarmigan has no real defense
            var weapon = new Weapon(WeaponType.Unarmed, WeaponMaterial.Organic, "Beak", 100)
            {
                Damage = 1,  // Minimal damage
                Accuracy = 0.4
            };

            Animal ptarmigan = new("Ptarmigan", weapon, bodyStats, AnimalBehaviorType.Prey, isHostile: false)
            {
                Description = "A plump game bird with white winter plumage, nearly invisible against the snow.",
                TrackingDifficulty = 7 // Very hard to track - flies away, good camouflage
            };

            ptarmigan.AddLoot(ItemFactory.MakeSmallMeat());
            return ptarmigan;
        }

        public static Animal MakeFox()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Quadruped,
                overallWeight = 6,     // 6 kg - small fox
                fatPercent = 0.20,     // 20% fat - winter coat
                musclePercent = 0.55   // 55% muscle
            };

            // Fox can defend itself but primarily flees
            var weapon = new Weapon(WeaponType.Fangs, WeaponMaterial.Organic, "Sharp Teeth", 100)
            {
                Damage = 4,  // Low damage - scavenger
                Accuracy = 1.0
            };

            Animal fox = new("Fox", weapon, bodyStats, AnimalBehaviorType.Scavenger, isHostile: false)
            {
                Description = "A cunning red fox with alert eyes, weighing whether you're a threat or opportunity.",
                TrackingDifficulty = 6 // Medium-hard - intelligent and cautious (will flee if outmatched)
            };

            fox.AddLoot(ItemFactory.MakeSmallMeat());
            return fox;
        }

        // Human NPCs with various weapons
        public static Npc MakeTribalHunter()
        {
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Human,
                overallWeight = 65,
                fatPercent = 0.15,
                musclePercent = 0.60
            };

            // Create a hunting spear
            var weapon = new Weapon(WeaponType.Spear, WeaponMaterial.Wood, "Wooden Spear", 75)
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
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Human,
                overallWeight = 70,
                fatPercent = 0.15,
                musclePercent = 0.65
            };

            // Create a war club
            var weapon = new Weapon(WeaponType.Club, WeaponMaterial.Stone, "War-Club", 80)
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
            var bodyStats = new BodyCreationInfo
            {
                type = BodyTypes.Human,
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