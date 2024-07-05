using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;

namespace text_survival.Actors
{
    public static class NpcFactory
    {
        public static Humanoid MakeBandit()
        {
            Humanoid bandit = new("Bandit");
            bandit.AddLoot(BanditLootTable.GenerateRandomItem());
            return bandit;
        }
        private static readonly LootTable BanditLootTable = new LootTable([
            ItemFactory.MakeCoin,
            ItemFactory.MakeApple,
            ItemFactory.MakeCopperCoin,
            ItemFactory.MakeBread
        ]);
        
        public static Npc MakeRat()
        {
            Animal rat = new("Rat", 2, new Attributes(5, 10, 15, 50, 40, 5, 0, 70))
            {
                Description = "A rat with fleas."
            };
            rat.AddLoot(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Animal MakeWolf()
        {
            Animal wolf = new("Wolf", 10, new Attributes(50, 15, 50, 50, 50, 50, 5, 50))
            {
                Description = "A wolf."
            };
            wolf.AddLoot(ItemFactory.MakeLargeMeat());
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear", 20, new Attributes(80, 10, 50, 20, 40, 80, 1, 50));
            bear.AddLoot(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake", 10, new Attributes(20, 5, 20, 50, 40, 20, 0, 55));
            snake.AddLoot(SnakeLootTable.GenerateRandomItem());
            CommonBuffs.Venomous(2, 3, .5).ApplyTo(snake);
            
            return snake;
        }
        private static readonly LootTable SnakeLootTable = new([
            ItemFactory.MakeSmallMeat,
            ItemFactory.MakeVenomVial
         ]);
        
        

        public static Npc MakeBat()
        {
            Npc bat = new Animal("Bat", 2, new Attributes(10, 5, 5, 70, 60, 40, 1, 50));
            //bat.Loot.Add(ItemFactory.MakeBatWing());
            //bat.Loot.Add(ItemFactory.MakeGuano());
            return bat;
        }

        public static Npc MakeSpider()
        {
            Npc spider = new Animal("Spider", 5, new Attributes(15, 3, 10, 35, 30, 15, 0, 55));
            CommonBuffs.Venomous(1, 3, .4).ApplyTo(spider);
            spider.AddLoot(SpiderLT.GenerateRandomItem());
            return spider;
        }
        private static readonly LootTable SpiderLT = new([
            ItemFactory.MakeSpiderSilk,
            ItemFactory.MakeVenomVial
        ]);

        public static Humanoid MakeGoblin()
        {
            Humanoid goblin = new("Goblin", ItemFactory.MakeGoblinSword(),
                attributes: new Attributes(35, 25, 30, 40, 45, 35, 10, 60));
            goblin.AddLoot(ItemFactory.MakeCoin());
            //goblin.Loot.Add(ItemFactory.MakeTatteredCloth());
            return goblin;
        }

        public static Animal MakeDragon()
        {
            Animal dragon = new("Dragon", 50, new Attributes(100, 90, 100, 40, 70, 100, 30, 100));
            dragon.AddLoot(DragonLT.GenerateRandomItem()); 
            return dragon;
        }
        private static readonly LootTable DragonLT = new([
            ItemFactory.MakeDragonScale,
            ItemFactory.MakeDragonTooth,
            ItemFactory.MakeLargeCoinPouch
        ]);

        public static Humanoid MakeSkeleton()
        {
            Humanoid skeleton = new("Skeleton", ItemFactory.MakeRustySword(),
                attributes: new Attributes(30, 5, 50, 40, 35, 70, 1, 20));
            //skeleton.Loot.Add(ItemFactory.MakeBoneFragments());
            skeleton.AddLoot(ItemFactory.MakeRustySword());
            return skeleton;
        }

        public static Animal MakeCrocodile()
        {
            Animal crocodile = new Animal("Crocodile", 30, new Attributes(70, 5, 70, 30, 40, 75, 1, 20));
            crocodile.AddLoot(CrocodileLT.GenerateRandomItem());
            return crocodile;
        }
        private static readonly LootTable CrocodileLT = new([
            ItemFactory.MakeCrocodileTooth,
            ItemFactory.MakeCrocodileSkin,
        ]);
    }

}
