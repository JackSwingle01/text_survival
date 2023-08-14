using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public static class Input
    {
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
