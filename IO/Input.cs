namespace text_survival.IO
{
    public static class Input
    {
        private static ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        private static string userInput;
        public static void OnUserInputReceived(string input)
        {
            userInput = input;
            manualResetEvent.Set();
        }
        public static string Read()
        {
            string? input = "";
            if (Config.io == Config.IOType.Console)
            {
                input = Console.ReadLine();
            }
            else if (Config.io == Config.IOType.Web)
            {
                // await user input from web
                //input = AwaitInput();
                throw new NotImplementedException();
            }
            else if (Config.io == Config.IOType.AI_Enhanced)
            {
                // input = AI.GetInput();
                throw new NotImplementedException();
            }
            return input ?? "";
        }

        public static string AwaitInput()
        {
            manualResetEvent.WaitOne();
            manualResetEvent.Reset();
            return userInput;
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
                    return input;
                }
                else
                {
                    Output.Write("Invalid input. Please enter a number between ", low, " and ", high, ".\n");
                }
            }
        }

        /// <summary>
        /// Returns a 1-indexed selection from a list of choices.
        /// Returns 0 if the user selects the cancel option.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="cancelOption"></param>
        /// <param name="cancelMessage"></param>
        /// <returns></returns>
        public static int GetSelectionFromList<T>(List<T> list, bool cancelOption = false, string cancelMessage = "Cancel")
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
            }
            else
            {
                input = ReadInt(1, list.Count);
            }
            return input;
        }
    }
}
