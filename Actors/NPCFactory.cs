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
            ItemFactory.MakeCoin(),
            ItemFactory.MakeApple(),
            ItemFactory.MakeCopperCoin(),
            ItemFactory.MakeBread()
        ]);

        public static Npc MakeRat()
        {
            Animal rat = new("Rat", 2, new Attributes(5, 40, 5, 70))
            {
                Description = "A rat with fleas."
            };
            rat.AddLoot(ItemFactory.MakeSmallMeat());
            rat.Clone = MakeRat;
            return rat;
        }

        public static Animal MakeWolf()
        {
            Animal wolf = new("Wolf", 10, new Attributes(50, 50, 50, 50))
            {
                Description = "A wolf."
            };
            wolf.AddLoot(ItemFactory.MakeLargeMeat());
            wolf.Clone = MakeWolf;
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear", 20, new Attributes(80, 40, 80, 50));
            bear.AddLoot(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            bear.Clone = MakeBear;
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake", 10, new Attributes(20, 40, 20, 55));
            snake.AddLoot(SnakeLootTable.GenerateRandomItem());
            CommonBuffs.Venomous(2, 3, .5).ApplyTo(snake);
            snake.Clone = MakeSnake;
            return snake;
        }
        private static readonly LootTable SnakeLootTable = new([
            ItemFactory.MakeSmallMeat(),
            ItemFactory.MakeVenomSac()
         ]);



        public static Npc MakeBat()
        {
            Npc bat = new Animal("Bat", 2, new Attributes(10, 60, 40, 50));
            bat.Clone = MakeBat;
            return bat;
        }

        public static Npc MakeSpider()
        {
            Npc spider = new Animal("Spider", 5, new Attributes(15, 30, 15, 55));
            CommonBuffs.Venomous(1, 3, .4).ApplyTo(spider);
            spider.AddLoot(SpiderLT.GenerateRandomItem());
            spider.Clone = MakeSpider;
            return spider;
        }

        public static Npc MakeCaveBear()
        {
            Npc bear = MakeBear();
            bear.Name = "Cave Bear";
            return bear;
        }

        private static readonly LootTable SpiderLT = new([
            ItemFactory.MakeSpiderSilk(),
            ItemFactory.MakeVenomSac()
        ]);






    }

}
