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
        //public static void Write(string str, int delay = 1000, ConsoleColor color = ConsoleColor.White)
        //{
        //    Console.ForegroundColor = color;
        //    Console.WriteLine(str);
        //    Thread.Sleep(delay);
        //}


        public static ConsoleColor DetermineTextColor(object x)
        {
            if (x is string)
            {
                return ConsoleColor.Gray;
            }
            else if (x is int || x is float || x is double)
            {
                return ConsoleColor.Green;
            }
            else if (x is NPC)
            {
                return ConsoleColor.Red;
            }
            else if (x is FoodItem)
            {
                return ConsoleColor.Cyan;
            }
            else if (x is EquipableItem)
            {
                return ConsoleColor.DarkCyan;
            }
            else if (x is Item)
            {
                return ConsoleColor.Blue;
            }
            else if (x is Container)
            {
                return ConsoleColor.Yellow;
            }
            else if (x is Player)
            {
                return ConsoleColor.DarkYellow;
            }
            else
            {
                return ConsoleColor.White;
            }
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
            Random rand = new Random();
            return rand.Next(1, sides + 1);
        }
        public static int Rand(int low, int high)
        {
            Random rand = new Random();
            return rand.Next(low, high + 1);
        }
        public static bool FlipCoin()
        {
            Random rand = new Random();
            return rand.Next(2) == 0;
        }
    }
}
