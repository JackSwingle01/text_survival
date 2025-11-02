using text_survival.Actions;
using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.IO
{
    public static class Output
    {
        // Test mode: set TEST_MODE=1 to skip sleeps and readkeys
        public static bool TestMode = Environment.GetEnvironmentVariable("TEST_MODE") == "1";
        public static int SleepTime = 200;
        public static ConsoleColor DetermineTextColor(object x)
        {
            return x switch
            {
                string => ConsoleColor.White,
                int or float or double => ConsoleColor.Green,
                Npc => ConsoleColor.Red,
                Item => ConsoleColor.Cyan,
                Container => ConsoleColor.Yellow,
                Player => ConsoleColor.Green,
                Zone => ConsoleColor.Blue,
                Location => ConsoleColor.DarkBlue,
                Enum => ConsoleColor.Gray,
                IGameAction => ConsoleColor.White,
                null => ConsoleColor.Red,
                _ => ConsoleColor.Red,
            };
        }

        public static void Write(params object[] args)
        {
            foreach (var arg in args)
            {
                string text = GetFormattedText(arg);

                if (TestMode)
                {
                    // In test mode, write to file instead of console with colors
                    TestModeIO.WriteOutput(text);
                }
                else
                {
                    if (Console.ForegroundColor == ConsoleColor.White)
                    {
                        Console.ForegroundColor = DetermineTextColor(arg);
                    }
                    Console.Write(text);
                    Console.ForegroundColor = ConsoleColor.White;
                    Thread.Sleep(SleepTime);
                }
            }
        }

        private static string GetFormattedText(params object[] args)
        {
            string result = string.Empty;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case float f:
                        result += $"{f:F1}";
                        break;
                    case double d:
                        result += $"{d:F1}";
                        break;
                    case null:
                        result += "[NULL]";
                        break;
                    default:
                        result += arg.ToString();
                        break;
                }
            }
            return result;

        }

        public static void WriteLine(params object[] args)
        {
            Write(args);
            Write("\n");
        }


        public static void WriteWarning(string str)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            WriteLine(str);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteDanger(string str)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            WriteLine(str);
            Console.ForegroundColor = oldColor;
        }

        internal static void WriteSuccess(string str)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            WriteLine(str);
            Console.ForegroundColor = oldColor;
        }
    }
}
