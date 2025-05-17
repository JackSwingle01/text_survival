using text_survival.Items;
using text_survival.Level;

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
            Animal rat = new("Rat", 2, new Attributes(5, 40, 5, 70))
            {
                Description = "A rat with fleas."
            };
            rat.AddLoot(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Animal MakeWolf()
        {
            Animal wolf = new("Wolf", 10, new Attributes(50, 50, 50, 50))
            {
                Description = "A wolf."
            };
            wolf.AddLoot(ItemFactory.MakeLargeMeat());
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear", 20, new Attributes(80, 40, 80, 50));
            bear.AddLoot(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake", 10, new Attributes(20, 40, 20, 55));
            LootTable loot = new LootTable();
            loot.AddItem(ItemFactory.MakeSmallMeat, 2);
            loot.AddItem(ItemFactory.MakeVenomSac);
            snake.AddLoot(loot.GenerateRandomItem());
            // CommonBuffs.Venomous(2, 3, .5).ApplyTo(snake); // todo make effect for this
            return snake;
        }



        public static Npc MakeBat()
        {
            Npc bat = new Animal("Bat", 2, new Attributes(10, 60, 40, 50));
            return bat;
        }

        public static Npc MakeSpider()
        {
            Npc spider = new Animal("Spider", 5, new Attributes(15, 30, 15, 55));
            // CommonBuffs.Venomous(1, 3, .4).ApplyTo(spider); // todo - make effect for this
            var loot = new LootTable();
            loot.AddItem(ItemFactory.MakeSpiderSilk);
            loot.AddItem(ItemFactory.MakeVenomSac);
            spider.AddLoot(loot.GenerateRandomItem());
            return spider;
        }

        public static Npc MakeCaveBear()
        {
            Npc bear = MakeBear();
            bear.Name = "Cave Bear";
            return bear;
        }


    }

}
