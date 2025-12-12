using text_survival.Actions;
using text_survival.Actors.NPCs;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.IO
{
    public static class Output
    {
        // Test mode: set TEST_MODE=1 to skip sleeps and readkeys
        public static bool TestMode = Environment.GetEnvironmentVariable("TEST_MODE") == "1";
        public static int SleepTime = 200;

        // Message batching for deduplication during long actions
        private static bool IsBatching = false;
        private static List<string> MessageBuffer = new List<string>();
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
            // If batching is enabled, collect message instead of displaying
            if (IsBatching)
            {
                string message = GetFormattedText(args);
                MessageBuffer.Add(message);
                return;
            }

            Write(args);
            Write("\n");
        }

        /// <summary>
        /// Write text with a specific color, automatically saving and restoring the previous color
        /// </summary>
        public static void WriteColored(ConsoleColor color, params object[] args)
        {
            if (TestMode)
            {
                // In test mode, just write without color
                Write(args);
                return;
            }

            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            foreach (var arg in args)
            {
                string text = GetFormattedText(arg);
                Console.Write(text);
                Thread.Sleep(SleepTime);
            }

            Console.ForegroundColor = oldColor;
        }

        /// <summary>
        /// Write text with a specific color and newline, automatically saving and restoring the previous color
        /// </summary>
        public static void WriteLineColored(ConsoleColor color, params object[] args)
        {
            WriteColored(color, args);

            if (TestMode)
            {
                TestModeIO.WriteOutput("\n");
            }
            else
            {
                Console.Write("\n");
            }
        }

        /// <summary>
        /// Start batching messages (collect instead of display)
        /// </summary>
        public static void StartBatching()
        {
            IsBatching = true;
            MessageBuffer.Clear();
        }

        /// <summary>
        /// Stop batching and flush deduplicated messages
        /// </summary>
        public static void FlushMessages()
        {
            IsBatching = false;

            if (MessageBuffer.Count == 0)
                return;

            // Group identical messages and count occurrences
            var messageCounts = new Dictionary<string, int>();
            foreach (var msg in MessageBuffer)
            {
                if (messageCounts.ContainsKey(msg))
                    messageCounts[msg]++;
                else
                    messageCounts[msg] = 1;
            }

            // Display deduplicated messages
            foreach (var kvp in messageCounts)
            {
                string message = kvp.Key;
                int count = kvp.Value;

                // Detect critical messages that should always show immediately
                bool isCritical = message.Contains("leveled up")
                    || message.Contains("developing")
                    || message.Contains("damage")
                    || message.Contains("Frostbite")
                    || message.Contains("Hypothermia")
                    || message.Contains("freezing")
                    || message.Contains("health");

                if (isCritical)
                {
                    // Critical messages always show, even if repeated
                    for (int i = 0; i < count; i++)
                    {
                        WriteLine(message);
                    }
                }
                else if (count == 1)
                {
                    // Unique messages show once
                    WriteLine(message);
                }
                else if (count > 1 && count <= 3)
                {
                    // Show a few times (not too spammy)
                    WriteLine(message);
                }
                else
                {
                    // Heavily repeated - summarize
                    WriteLine($"{message} (occurred {count} times)");
                }
            }

            MessageBuffer.Clear();
        }

        public static void WriteAll(List<string> lines)
        {
            lines.ForEach(l => WriteLine(l));
        }


        public static void WriteWarning(string str)
        {
            WriteLineColored(ConsoleColor.Yellow, str);
        }

        public static void WriteDanger(string str)
        {
            WriteLineColored(ConsoleColor.Red, str);
        }

        internal static void WriteSuccess(string str)
        {
            WriteLineColored(ConsoleColor.Green, str);
        }
    }
}
