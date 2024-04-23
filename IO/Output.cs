using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.IO
{
    public static class Output
    {
        public static int SleepTime = 100;
        public static ConsoleColor DetermineTextColor(object x)
        {
            return x switch
            {
                string => ConsoleColor.Gray,
                int or float or double => ConsoleColor.Green,
                Npc => ConsoleColor.Red,
                Item => ConsoleColor.Cyan,
                Container => ConsoleColor.Yellow,
                Player => ConsoleColor.Green,
                Area => ConsoleColor.Blue,
                Enum => ConsoleColor.White,
                null => ConsoleColor.Red,
                _ => ConsoleColor.White,
            };
        }

        public static void Write(params object[] args)
        {
            foreach (var arg in args)
            {
                string text = GetFormattedText(arg);
                if (Config.io == Config.IOType.Console)
                {
                    Console.ForegroundColor = DetermineTextColor(arg);
                    Console.Write(text);
                }
                else if (Config.io == Config.IOType.Web)
                {
                    throw new NotImplementedException();
                    //EventHandler.Publish(new WriteEvent(text));
                }
            }
            Thread.Sleep(SleepTime);
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
    }
}
