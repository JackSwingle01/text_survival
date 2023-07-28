using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public static class Utils
    {
   
        public static string? Read()
        {
            return Console.ReadLine();
        }
        public static void Write(string str, int delay = 1000)
        {
            Console.WriteLine(str + "\n");
            System.Threading.Thread.Sleep(delay);
        }
    }
}
