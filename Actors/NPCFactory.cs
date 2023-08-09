using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public static class NpcFactory
    {
        public static Humanoid MakeBandit()
        {
            Humanoid bandit = new("Bandit");
            bandit.Loot.AddRange(new List<Item>
            {
                ItemFactory.MakeCoin(),
                ItemFactory.MakeApple(),
                ItemFactory.MakeCopperCoin(),
                ItemFactory.MakeBread()
            });
            return bandit;

        }
        public static Npc MakeRat()
        {
            Npc rat = new Animal("Rat", 2,new Attributes(5, 10, 15, 50, 40, 5, 0, 70));
            rat.Description = "A rat with fleas.";
            rat.Loot.Add(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Animal MakeWolf()
        {
            Animal wolf = new Animal("Wolf", 10, new Attributes(50, 15, 50, 50, 50, 50, 5, 50));
            wolf.Loot.Add(ItemFactory.MakeLargeMeat());
            wolf.Description = "A wolf.";
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear", 20, new Attributes(80, 10, 50, 20, 40, 80, 1, 50));
            bear.Loot.Add(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake", 10,new Attributes(20, 5, 20, 50, 40, 20, 0, 55));
            snake.Loot.Add(ItemFactory.MakeSmallMeat());
            //snake.Loot.Add(ItemFactory.MakeSnakeSkin());
            snake.Loot.Add(ItemFactory.MakeVenomVial());
            return snake;
        }

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
            spider.Loot.Add(ItemFactory.MakeSpiderSilk());
            spider.Loot.Add(ItemFactory.MakeVenomVial());
            return spider;
        }

        public static Humanoid MakeGoblin()
        {
            Humanoid goblin = new("Goblin", ItemFactory.MakeGoblinSword(),
                attributes: new Attributes(35, 25, 30, 40, 45, 35, 10, 60));
            goblin.Loot.Add(ItemFactory.MakeGoblinSword());
            goblin.Loot.Add(ItemFactory.MakeCoin());
            //goblin.Loot.Add(ItemFactory.MakeTatteredCloth());
            return goblin;
        }

        public static Animal MakeDragon()
        {
            Animal dragon = new("Dragon", 50, new Attributes(100, 90, 100, 40, 70, 100, 30, 100));
            dragon.Loot.Add(ItemFactory.MakeDragonScale());
            dragon.Loot.Add(ItemFactory.MakeDragonTooth());
            dragon.Loot.Add(ItemFactory.MakeLargeCoinPouch());
            return dragon;
        }

        public static Humanoid MakeSkeleton()
        {
            Humanoid skeleton = new("Skeleton", ItemFactory.MakeRustySword(),
                attributes: new Attributes(30, 5, 50, 40, 35, 70, 1, 20));
            //skeleton.Loot.Add(ItemFactory.MakeBoneFragments());
            skeleton.Loot.Add(ItemFactory.MakeRustySword());
            return skeleton;
        }

        public static Animal MakeCrocodile()
        {
            Animal crocodile = new Animal("Crocodile", 30,new Attributes(70, 5, 70, 30, 40, 75, 1, 20));
            crocodile.Loot.Add(ItemFactory.MakeCrocodileSkin());
            crocodile.Loot.Add(ItemFactory.MakeCrocodileTooth());
            return crocodile;
        }
    }

}
