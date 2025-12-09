using text_survival.Core;

namespace text_survival.IO
{
    public static class Input
    {
        private static ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        // private static string userInput;
        // public static void OnUserInputReceived(string input)
        // {
        //     userInput = input;
        //     manualResetEvent.Set();
        // }
        public static string Read()
        {
            string? input = "";
            if (Output.TestMode)
            {
                TestModeIO.SignalReady();
                input = TestModeIO.ReadInput();
            }
            else if (Config.io == Config.IOType.Console)
            {
                input = Console.ReadLine();
            }
            else if (Config.io == Config.IOType.Web)
            {
                // await user input from web
                //input = AwaitInput();
                throw new NotImplementedException();
            }

            return input ?? "";
        }

        // public static string AwaitInput()
        // {
        //     manualResetEvent.WaitOne();
        //     manualResetEvent.Reset();
        //     return userInput;
        // }

        /// <summary>
        /// Read a single key press from the user
        /// </summary>
        /// <param name="intercept">If true, the pressed key will not be displayed in the console</param>
        /// <returns>ConsoleKeyInfo representing the key that was pressed</returns>
        public static ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (Output.TestMode)
            {
                // In test mode, simulate a key press by reading a character from input
                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();

                // Convert the first character to a ConsoleKey (or use Enter if empty)
                if (string.IsNullOrEmpty(input))
                {
                    return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
                }

                char keyChar = input[0];
                ConsoleKey key = char.IsLetter(keyChar) ? (ConsoleKey)Enum.Parse(typeof(ConsoleKey), keyChar.ToString().ToUpper()) : ConsoleKey.Enter;
                return new ConsoleKeyInfo(keyChar, key, false, false, false);
            }
            else if (Config.io == Config.IOType.Console)
            {
                return Console.ReadKey(intercept);
            }
            else if (Config.io == Config.IOType.Web)
            {
                throw new NotImplementedException();
            }

            // Fallback (shouldn't reach here)
            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }

        public static int ReadInt()
        {
            while (true)
            {
                string? input = Read();
                if (int.TryParse(input, out int result))
                {
                    return result;
                }
                else
                {
                    Output.Write("Invalid input. Please enter a number.\n");
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
                    Output.WriteLine();
                    return input;
                }
                else
                {
                    Output.Write("Invalid input. Please enter a number between ", low, " and ", high, ".\n");
                }
            }
        }

        public static bool ReadYesNo()
        {
            while (true)
            {
                string? input = Read().Trim().ToLower();
                if (input == "y" || input == "yes")
                {
                    return true;
                }
                else if (input == "n" || input == "no")
                {
                    return false;
                }
                else
                {
                    Output.Write("Invalid input. Please enter 'y' or 'n'.\n");
                }
            }
        }

        public static T? GetSelectionFromList<T>(List<T> list, bool cancelOption = false, string cancelMessage = "Cancel")
        {
            list.ForEach(i =>
            {
                if (i != null) Output.WriteLine(list.IndexOf(i) + 1, ". ", i);
            });

            int input;
            if (cancelOption)
            {
                Output.WriteLine(0, ". ", cancelMessage);
                input = ReadInt(0, list.Count);
                if (input == 0)
                {
                    return default;
                }
            }
            else
            {
                input = ReadInt(1, list.Count);
            }

            return list[input - 1];
        }
    }
}
