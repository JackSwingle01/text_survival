using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using text_survival.Environments;

namespace text_survival
{
    public static class World
    {
        public static List<Area> Areas { get; set; }
        public static TimeOnly Time { get; set; }
        public static int Days { get; set; }

        static World()
        {
            Areas = new List<Area>();
            Areas.Add(AreaFactory.GetForest());
            Areas.Add(AreaFactory.GetShack());
            Areas.Add(AreaFactory.GetCave());
            Areas.Add(AreaFactory.GetRiver());

        }

        public static void Update(int minutes)
        {
            if (Time.AddMinutes(minutes).Hour < Time.Hour)
            {
                Days++;
            }
            Time = Time.AddMinutes(minutes);
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
            if (Time.Hour < 5)
            {
                return TimeOfDay.Night;
            }
            else if (Time.Hour < 12)
            {
                return TimeOfDay.Morning;
            }
            else if (Time.Hour < 18)
            {
                return TimeOfDay.Afternoon;
            }
            else if (Time.Hour < 23)
            {
                return TimeOfDay.Evening;
            }
            else
            {
                return TimeOfDay.Night;
            }
        }

    }
}
