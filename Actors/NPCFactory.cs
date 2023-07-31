using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Items;

namespace text_survival.Actors
{
    public static class NPCFactory
    {
        public static NPC MakeRat()
        {
            NPC rat = new NPC("Rat", 5, 5, 2, 12);
            rat.Loot.Add(ItemFactory.MakeSmallMeat());
            return rat;
        }

        public static NPC MakeWolf()
        {
            NPC wolf = new NPC("Wolf", 10, 10, 5, 18);
            wolf.Loot.Add(ItemFactory.MakeLargeMeat());
            return wolf;
        }

        public static NPC MakeBear()
        {
            NPC bear = new NPC("Bear", 20, 20, 20, 7);
            bear.Loot.Add(ItemFactory.MakeLargeMeat());
            return bear;
        }

        public static NPC MakeSnake()
        {
            NPC snake = new NPC("Snake", 5, 5, 2, 11);
            snake.Loot.Add(ItemFactory.MakeSmallMeat());
            snake.Loot.Add(ItemFactory.MakeSnakeSkin());
            snake.Loot.Add(ItemFactory.MakeVenomVial());
            return snake;
        }

        public static NPC MakeBat()
        {
            NPC bat = new NPC("Bat", 5, 5, 2, 16);
            bat.Loot.Add(ItemFactory.MakeBatWing());
            bat.Loot.Add(ItemFactory.MakeGuano());
            return bat;
        }

        public static NPC MakeSpider()
        {
            NPC spider = new NPC("Spider", 1, 1, 0, 5);
            spider.Loot.Add(ItemFactory.MakeSpiderSilk());
            spider.Loot.Add(ItemFactory.MakeVenomVial());
            return spider;
        }

        public static NPC MakeGoblin()
        {
            NPC goblin = new NPC("Goblin", 10, 10, 5, 10);
            goblin.Loot.Add(ItemFactory.MakeGoblinSword());
            goblin.Loot.Add(ItemFactory.MakeCoin());
            //goblin.Loot.Add(ItemFactory.MakeTatteredCloth());
            return goblin;
        }

        public static NPC MakeDragon()
        {
            NPC dragon = new NPC("Dragon", 50, 50, 50, 3);
            dragon.Loot.Add(ItemFactory.MakeDragonScale());
            dragon.Loot.Add(ItemFactory.MakeDragonTooth());
            dragon.Loot.Add(ItemFactory.MakeLargeCoinPouch());
            return dragon;
        }

        public static NPC MakeSkeleton()
        {
            NPC skeleton = new NPC("Skeleton", 10, 10, 10, 10);
            skeleton.Loot.Add(ItemFactory.MakeBoneFragments());
            skeleton.Loot.Add(ItemFactory.MakeRustySword());
            return skeleton;
        }

        public static NPC MakeCrocodile()
        {
            NPC crocodile = new NPC("Crocodile", 20, 20, 20, 5);
            crocodile.Loot.Add(ItemFactory.MakeCrocodileSkin());
            crocodile.Loot.Add(ItemFactory.MakeCrocodileTooth());
            return crocodile;
        }

    }

}
