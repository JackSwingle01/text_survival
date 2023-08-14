using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival
{
    public static class Output
    {
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
                Console.ForegroundColor = DetermineTextColor(arg);
                string text = GetFormattedText(arg);
                Console.Write(text);
            }
            // Reset color to default after writing
            Console.ResetColor();
            Thread.Sleep(100);
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
            //Thread.Sleep(100);
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
