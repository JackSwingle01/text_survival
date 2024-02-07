using text_survival.Environments;
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

        }

        public enum TimeOfDay
        {
            Night,
            Morning,
            Afternoon,
            Evening
        }

        public static TimeOfDay GetTimeOfDay()
        {
            return Time.Hour switch
            {
                < 5 => TimeOfDay.Night,
                < 12 => TimeOfDay.Morning,
                < 18 => TimeOfDay.Afternoon,
                < 23 => TimeOfDay.Evening,
                _ => TimeOfDay.Night
            };
        }

    }
}
