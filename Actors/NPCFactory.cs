using text_survival.Items;

namespace text_survival.Actors
{
    public static class NpcFactory
    {
        public static Npc MakeRat()
        {
            Npc rat = new Animal("Rat", 5, 5, 2, 12);
            rat.Description = "A rat with fleas.";
            rat.Loot.Add(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Npc MakeWolf()
        {
            Npc wolf = new Animal("Wolf", 10, 10, 5, 18);
            wolf.Loot.Add(ItemFactory.MakeLargeMeat());
            wolf.Description = "A wolf.";
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear", 20, 20, 20, 7);
            bear.Loot.Add(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake", 5, 5, 2, 11);
            snake.Loot.Add(ItemFactory.MakeSmallMeat());
            snake.Loot.Add(ItemFactory.MakeSnakeSkin());
            snake.Loot.Add(ItemFactory.MakeVenomVial());
            return snake;
        }

        public static Npc MakeBat()
        {
            Npc bat = new Animal("Bat", 5, 5, 2, 16);
            bat.Loot.Add(ItemFactory.MakeBatWing());
            bat.Loot.Add(ItemFactory.MakeGuano());
            return bat;
        }

        public static Npc MakeSpider()
        {
            Npc spider = new Animal("Spider", 1, 1, 0, 5);
            spider.Loot.Add(ItemFactory.MakeSpiderSilk());
            spider.Loot.Add(ItemFactory.MakeVenomVial());
            return spider;
        }

        public static Npc MakeGoblin()
        {
            Npc goblin = new("Goblin", 10, 10, 5, 10);
            goblin.Loot.Add(ItemFactory.MakeGoblinSword());
            goblin.Loot.Add(ItemFactory.MakeCoin());
            //goblin.Loot.Add(ItemFactory.MakeTatteredCloth());
            return goblin;
        }

        public static Npc MakeDragon()
        {
            Npc dragon = new("Dragon", 50, 50, 50, 3);
            dragon.Loot.Add(ItemFactory.MakeDragonScale());
            dragon.Loot.Add(ItemFactory.MakeDragonTooth());
            dragon.Loot.Add(ItemFactory.MakeLargeCoinPouch());
            return dragon;
        }

        public static Npc MakeSkeleton()
        {
            Npc skeleton = new("Skeleton", 10, 10, 10, 10);
            skeleton.Loot.Add(ItemFactory.MakeBoneFragments());
            skeleton.Loot.Add(ItemFactory.MakeRustySword());
            return skeleton;
        }

        public static Npc MakeCrocodile()
        {
            Npc crocodile = new Animal("Crocodile", 20, 20, 20, 5);
            crocodile.Loot.Add(ItemFactory.MakeCrocodileSkin());
            crocodile.Loot.Add(ItemFactory.MakeCrocodileTooth());
            return crocodile;
        }
    }

}
