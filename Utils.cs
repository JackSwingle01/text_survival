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

        public static int GetSelectionFromList<T>(List<T> list, bool cancelOption = false)
        {
            list.ForEach(i =>
            {
                if (i != null) Utils.WriteLine(list.IndexOf(i) + 1, ". ", i);
            });
            int input;
            if (cancelOption)
            {
                Utils.WriteLine(0, ". Cancel");
                input = Utils.ReadInt(0, list.Count);
            }
            else
            {
                input = Utils.ReadInt(1, list.Count);
            }
            return input;
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
                Area => ConsoleColor.Blue,
                Enum => ConsoleColor.White,
                _ => ConsoleColor.White,
            };
        }
        public static void Write(params object[] args)
        {
            foreach (var arg in args)
            {
                Console.ForegroundColor = DetermineTextColor(arg);
                switch (arg)
                {
                    case float f:
                        {
                            Console.Write($"{f:F1}");
                            break;
                        }
                    case double d:
                        {
                            Console.Write($"{d:F1}");
                            break;
                        }
                    default:
                        Console.Write(arg.ToString());
                        break;
                }

            }
            // Reset color to default after writing
            Console.ResetColor();
            Thread.Sleep(100);
        }

        public static void WriteLine(params object[] args)
        {
            foreach (var arg in args)
            {
                Console.ForegroundColor = DetermineTextColor(arg);
                switch (arg)
                {
                    case float f:
                        {
                            Console.Write($"{f:F1}");
                            break;
                        }
                    case double d:
                        {
                            Console.Write($"{d:F1}");
                            break;
                        }
                    default:
                        Console.Write(arg.ToString());
                        break;
                }
            }
            // Reset color to default after writing
            Console.ResetColor();
            Console.WriteLine();
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
        public static int RandInt(int low, int high)
        {
            return Random.Next(low, high + 1);
        }

        public static float RandFloat(float low, float high)
        {
            return (float)Random.NextDouble() * (high - low) + low;
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
