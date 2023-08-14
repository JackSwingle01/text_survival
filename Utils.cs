using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    public static class Utils
    {
        private static readonly Random Random;

        static Utils()
        {
            Random = new Random();
        }


        public static int Roll(int sides)
        {
            return Random.Next(1, sides + 1);
        }
        public static int RandInt(int low, int high)
        {
            return Random.Next(low, high + 1);
        }

        public static float RandFloat(float low, float high)
        {
            return (float)Random.NextDouble() * (high - low) + low;
        }

        public static double RandDouble(double low, double high)
        {
            return Random.NextDouble() * (high - low) + low;
        }

        public static bool FlipCoin()
        {
            return Random.Next(2) == 0;
        }

        public static T GetRandomEnum<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            return (T)values.GetValue(RandInt(0, values.Length - 1));
        }

    }
}
