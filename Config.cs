namespace text_survival
{
    public static class Config
    {
        public enum IOType
        {
            Console,
            Web
        }
        public static IOType io = IOType.Console;
    }
}
