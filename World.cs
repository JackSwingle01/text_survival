using text_survival.Environments;

namespace text_survival
{
    public static class World
    {
        public static TimeOnly Time { get; set; } = new TimeOnly(hour: 9, minute: 0);
        public static DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking

        public static Player? Player { get; set; }
        public static void Update(int minutes, bool suppressMessages = false)
        {
            // Enable message batching for multi-minute updates to prevent spam
            if (minutes > 1 && !suppressMessages)
            {
                IO.Output.StartBatching();
            }

            for (int i = 0; i < minutes; i++)
            {
                Player?.Update(suppressMessages);
                Player?.CurrentZone.Update();
                Time = Time.AddMinutes(1);
                GameTime = GameTime.AddMinutes(1); // Keep GameTime in sync
            }

            // Flush and deduplicate messages after multi-minute update
            if (minutes > 1 && !suppressMessages)
            {
                IO.Output.FlushMessages();
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
