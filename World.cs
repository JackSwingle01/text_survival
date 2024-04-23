using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival
{
    public static class World
    {
        public static TimeOnly Time { get; set; }

        public static Player Player { get; set; }
        public static Area CurrentArea => Player.CurrentArea;

        static World()
        {
            Area startingArea = new Area("Clearing", "A small clearing in the forest.");
            Container oldBag = new Container("Old bag", 10);
            Location log = new Location("Hollow log");
            oldBag.Add(ItemFactory.MakeApple());
            oldBag.Add(ItemFactory.MakeClothShirt());
            oldBag.Add(ItemFactory.MakeClothPants());
            oldBag.Add(ItemFactory.MakeBoots());
            oldBag.Add(new Weapon(WeaponType.Dagger, WeaponMaterial.Iron, "Old dagger", 40));
            log.PutThing(oldBag);
            startingArea.PutThing(log);
            Player = new Player(startingArea);
            Time = new TimeOnly(hour: 9, minute: 0);
        }

        public static void Update(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                Player.Update();
                CurrentArea.Update();
                Time = Time.AddMinutes(1);
            }
            Output.WriteLine($"{Time:hh:mm}");
        }

        public enum TimeOfDay
        {
            Night,
            Dawn,
            Morning,
            Afternoon,
            Noon,
            Evening,
            Dusk
        }

        public static TimeOfDay GetTimeOfDay()
        {
            return Time.Hour switch
            {
                < 5 => TimeOfDay.Night,
                < 6 => TimeOfDay.Dawn,
                < 11 => TimeOfDay.Morning,
                < 13 => TimeOfDay.Noon,
                < 17 => TimeOfDay.Afternoon,
                < 20 => TimeOfDay.Evening,
                < 21 => TimeOfDay.Dusk,
                _ => TimeOfDay.Night
            };
        }

    }
}
