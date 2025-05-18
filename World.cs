﻿using text_survival.Environments;

namespace text_survival
{
    public static class World
    {
        public static TimeOnly Time { get; set; } = new TimeOnly(hour: 9, minute: 0);

        public static Player? Player { get; set; }
        public static void Update(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                Player?.Update();
                Player?.CurrentZone.Update();
                Time = Time.AddMinutes(1);
            }
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
