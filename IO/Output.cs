namespace text_survival.IO
{
    public static class Output
    {
        // Test mode: set TEST_MODE=1 to skip sleeps and use file I/O
        public static bool TestMode = Environment.GetEnvironmentVariable("TEST_MODE") == "1";
    }
}