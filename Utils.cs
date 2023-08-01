using text_survival.Actors;
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

        public static string Read()
        {
            string? input = Console.ReadLine();
            return input ?? "";
        }
        public static int ReadInt()
        {
            while (true)
            {
                string? input = Console.ReadLine();
                if (int.TryParse(input, out int result))
                {
                    return result;
                }
                else
                {
                    Write("Invalid input. Please enter a number.\n");
                }
            }
        }
        public static int ReadInt(int low, int high)
        {
            while (true)
            {
                int input = ReadInt();
                if (input >= low && input <= high)
                {
                    return input;
                }
                else
                {
                    Write("Invalid input. Please enter a number between ", low, " and ", high, ".\n");
                }
            }
        }

        public static ConsoleColor DetermineTextColor(object x)
        {
            return x switch
            {
                string => ConsoleColor.Gray,
                int or float or double => ConsoleColor.Green,
                Npc => ConsoleColor.Red,
                FoodItem or EquipableItem or Item => ConsoleColor.Cyan,
                Container => ConsoleColor.Yellow,
                Player => ConsoleColor.Green,
                _ => ConsoleColor.White,
            };
        }
        public static void Write(params object[] args)
        {
            foreach (var arg in args)
            {
                Console.ForegroundColor = DetermineTextColor(arg);
                Console.Write(arg.ToString());
            }
            // Reset color to default after writing
            Console.ResetColor();
            Thread.Sleep(100);
        }
        public static void WriteWarning(string str)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = oldColor;
        }
        public static void WriteDanger(string str)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(str);
            Console.ForegroundColor = oldColor;
        }


        public static int Roll(int sides)
        {
            return Random.Next(1, sides + 1);
        }
        public static int Rand(int low, int high)
        {
            return Random.Next(low, high + 1);
        }
        public static bool FlipCoin()
        {
            return Random.Next(2) == 0;
        }
    }
}
