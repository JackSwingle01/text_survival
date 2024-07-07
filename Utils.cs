namespace text_survival
{
    public static class Utils
    {
        private static readonly Random random = new Random(DateTime.Now.Millisecond);

        static Utils()
        {
            random = new Random(DateTime.Now.Millisecond);
        }

        public static int Roll(int sides)
        {
            return random.Next(1, sides + 1);
        }

        public static bool DetermineSucess(double chance)
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
            return list[Roll(list.Count) - 1];
        }

    }
}
