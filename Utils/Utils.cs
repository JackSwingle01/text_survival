using text_survival.UI;

namespace text_survival
{
    public static class Utils
    {
        private static readonly Random random = new Random();

        public static int Roll(int sides)
        {
            return random.Next(1, sides + 1);
        }

        public static bool DetermineSuccess(double chance)
        {
            return (random.NextDouble() < chance);
        }

        public static int RandInt(int low, int high)
        {
            return random.Next(low, high + 1);
        }

        public static float RandFloat(float low, float high)
        {
            return (float)random.NextDouble() * (high - low) + low;
        }

        public static double RandDouble(double low, double high)
        {
            return random.NextDouble() * (high - low) + low;
        }

        public static bool FlipCoin()
        {
            return random.Next(2) == 0;
        }

        public static T? GetRandomEnum<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            return (T?)values.GetValue(Roll(values.Length) - 1);
        }

        public static T GetRandomFromList<T>(List<T> list)
        {
            if (list.Count == 0)
            {
                throw new Exception("List is empty.");
            }
            return list[Roll(list.Count) - 1];
        }
        
    public static T GetRandomWeighted<T>(IDictionary<T, double> choices)
    {
        if (choices == null || choices.Count == 0)
            throw new ArgumentException("Cannot select from an empty collection", nameof(choices));
            
        double totalWeight = choices.Sum(pair => pair.Value);
        if (totalWeight <= 0)
            throw new ArgumentException("Total weight must be positive", nameof(choices));
            
        double roll = random.NextDouble() * totalWeight;
        
        double cumulativeWeight = 0;
        foreach (var pair in choices)
        {
            cumulativeWeight += pair.Value;
            if (roll <= cumulativeWeight)
                {
                    // GameDisplay.AddNarrative($"Debug: Odds: {pair.Value / totalWeight * 100:F2}%");
                    return pair.Key;
                }
        }
        
        // This should never happen if weights are positive
        return choices.Keys.Last();
    }

    /// <summary>
    /// Format fire time display: shows minutes if under 60, hours (1 decimal) if 60+
    /// </summary>
    public static string FormatFireTime(int minutes)
    {
        if (minutes <= 59)
            return $"{minutes} minutes";

        double hours = minutes / 60.0;
        return $"{hours:F1} hours";
    }
    }
}
