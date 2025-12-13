using text_survival.Actors.Player;

namespace text_survival.Core
{
    public static class World
    {
        public static TimeOnly Time { get; set; } = new TimeOnly(hour: 9, minute: 0);
        public static DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking

        public static Player? Player { get; set; }
        public static void Update(int minutes)
        {

            Player?.Update(minutes);
            Player?.CurrentZone.Update(minutes);
            Time = Time.AddMinutes(minutes);
            GameTime = GameTime.AddMinutes(minutes); // Keep GameTime in sync

            var logs = Player?.GetFlushLogs();
            if (logs is not null && logs.Count != 0)
                IO.Output.WriteAll(logs);
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
