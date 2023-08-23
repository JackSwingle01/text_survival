using text_survival_rpg_web.Environments;
using text_survival_rpg_web.Items;

namespace text_survival_rpg_web
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
            oldBag.Add(ItemFactory.MakeApple());
            oldBag.Add(ItemFactory.MakeClothShirt());
            oldBag.Add(ItemFactory.MakeClothPants());
            oldBag.Add(ItemFactory.MakeBoots());
            oldBag.Add(new Weapon(WeaponType.Dagger, WeaponMaterial.Iron, "Old dagger", 40));
            startingArea.PutThing(oldBag);
            Player = new Player(startingArea);
            Time = new TimeOnly(hour: 9, minute: 0);
        }

        public static void Update(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                Player.Update();
                List<IUpdateable> updateables = new List<IUpdateable>(CurrentArea.GetUpdateables);
                foreach (var updateable in updateables)
                {
                    updateable.Update();
                }
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
