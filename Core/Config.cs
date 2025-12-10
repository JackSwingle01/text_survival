using text_survival.IO;

namespace text_survival.Core
{
    public static class Config
    {
        public enum IOType
        {
            Console,
            Web
        }
        public static IOType io = IOType.Console;

        public static double NOTIFY_EXISTING_STATUS_CHANCE = .05;
    }
}
