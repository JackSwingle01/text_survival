namespace text_survival
{
    public static class Config
    {
        public enum IOType
        {
            Console,
            Web,
            AI_Enhanced
        }
        public static IOType io = IOType.Console;
        public static string APIKey = "";
    }
}
