namespace text_survival
{
    public static class World
    {
        public static TimeOnly Time { get; set; }
        public static int Days { get; set; }

        static World()
        {
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
