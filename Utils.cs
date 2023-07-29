namespace text_survival
{
    public static class Utils
    {

        public static string? Read()
        {
            return Console.ReadLine();
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
                    Write("Invalid input. Please enter a number.");
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
                    Write("Invalid input. Please enter a number between " + low + " and " + high + ".");
                }
            }
        }
        public static void Write(string str, int delay = 1000)
        {
            Console.WriteLine(str + "\n");
            System.Threading.Thread.Sleep(delay);
        }

    }
}
