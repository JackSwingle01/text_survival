using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    public static class World
    {
        public static TimeOnly Time { get; set; }
        //public static int Days { get; set; }
        public static Player Player { get; set; }
        public static Area CurrentArea => Player.CurrentArea;

        static World()
        {
            Area startingArea = new Area("Clearing", "A small clearing in the forest.");
            startingArea.Items.Add(new Weapon(WeaponType.Dagger, WeaponMaterial.Iron, "Old dagger", 40));
            Player = new Player(startingArea);
            Time = new TimeOnly(hour: 9, minute: 0);
        }



        public static void Update(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                Player.Update();
                foreach (var npc in CurrentArea.Npcs)
                {
                    npc.Update();
                }   
                Time = Time.AddMinutes(1);
            }
            //if (Time.AddMinutes(minutes).Hour < Time.Hour)
            //{
            //    Days++;
            //}
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
