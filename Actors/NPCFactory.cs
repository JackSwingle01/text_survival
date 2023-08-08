using text_survival.Items;

namespace text_survival.Actors
{
    public static class NpcFactory
    {
        public static Npc MakeRat()
        {
            Npc rat = new Animal("Rat");
            rat.Attributes = new Attributes(5, 10, 15, 50, 40, 5, 0, 70);
            rat.Description = "A rat with fleas.";
            rat.Loot.Add(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static Npc MakeWolf()
        {
            Npc wolf = new Animal("Wolf");

            wolf.Loot.Add(ItemFactory.MakeLargeMeat());
            wolf.Description = "A wolf.";
            return wolf;
        }

        public static Npc MakeBear()
        {
            Npc bear = new Animal("Bear");
            bear.Loot.Add(ItemFactory.MakeLargeMeat());
            bear.Description = "A bear.";
            return bear;
        }

        public static Npc MakeSnake()
        {
            Npc snake = new Animal("Snake");
            snake.Loot.Add(ItemFactory.MakeSmallMeat());
            snake.Loot.Add(ItemFactory.MakeSnakeSkin());
            snake.Loot.Add(ItemFactory.MakeVenomVial());
            return snake;
        }

        public static Npc MakeBat()
        {
            Npc bat = new Animal("Bat");
            bat.Loot.Add(ItemFactory.MakeBatWing());
            bat.Loot.Add(ItemFactory.MakeGuano());
            return bat;
        }

        public static Npc MakeSpider()
        {
            Npc spider = new Animal("Spider");
            spider.Loot.Add(ItemFactory.MakeSpiderSilk());
            spider.Loot.Add(ItemFactory.MakeVenomVial());
            return spider;
        }

        public static Npc MakeGoblin()
        {
            Npc goblin = new("Goblin");
            goblin.Loot.Add(ItemFactory.MakeGoblinSword());
            goblin.Loot.Add(ItemFactory.MakeCoin());
            //goblin.Loot.Add(ItemFactory.MakeTatteredCloth());
            return goblin;
        }

        public static Npc MakeDragon()
        {
            Npc dragon = new("Dragon");
            dragon.Loot.Add(ItemFactory.MakeDragonScale());
            dragon.Loot.Add(ItemFactory.MakeDragonTooth());
            dragon.Loot.Add(ItemFactory.MakeLargeCoinPouch());
            return dragon;
        }

        public static Npc MakeSkeleton()
        {
            Npc skeleton = new("Skeleton");
            skeleton.Loot.Add(ItemFactory.MakeBoneFragments());
            skeleton.Loot.Add(ItemFactory.MakeRustySword());
            return skeleton;
        }

        public static Npc MakeCrocodile()
        {
            Npc crocodile = new Animal("Crocodile");
            crocodile.Loot.Add(ItemFactory.MakeCrocodileSkin());
            crocodile.Loot.Add(ItemFactory.MakeCrocodileTooth());
            return crocodile;
        }
    }

}
