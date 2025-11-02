namespace text_survival.IO;

/// <summary>
/// File-based I/O for testing. Reads commands from input file, writes output to output file.
/// </summary>
public static class TestModeIO
{
    private static readonly string TmpDir = Path.Combine(Directory.GetCurrentDirectory(), ".test_game_io");
    private static readonly string InputFile = Path.Combine(TmpDir, "game_input.txt");
    private static readonly string OutputFile = Path.Combine(TmpDir, "game_output.txt");
    private static readonly string ReadyFile = Path.Combine(TmpDir, "game_ready.txt");

    public static void Initialize()
    {
        // Create tmp directory if it doesn't exist
        if (!Directory.Exists(TmpDir))
        {
            Directory.CreateDirectory(TmpDir);
        }

        // Clear files on startup
        File.WriteAllText(InputFile, "");
        File.WriteAllText(OutputFile, "");
        File.WriteAllText(ReadyFile, "");
    }

    public static void WriteOutput(string text)
    {
        File.AppendAllText(OutputFile, text);
    }

    public static void WriteOutputLine(string text)
    {
        File.AppendAllText(OutputFile, text + "\n");
    }

    public static void SignalReady()
    {
        // Signal that we're waiting for input
        File.WriteAllText(ReadyFile, "READY");
    }

    public static string ReadInput()
    {
        // Wait for input file to have content
        while (true)
        {
            if (File.Exists(InputFile))
            {
                var content = File.ReadAllText(InputFile).Trim();
                if (!string.IsNullOrEmpty(content))
                {
                    // Clear input file after reading
                    File.WriteAllText(InputFile, "");
                    File.WriteAllText(ReadyFile, "");
                    return content;
                }
            }
            Thread.Sleep(100); // Poll every 100ms
        }
    }

    public static bool IsReady()
    {
        return File.Exists(ReadyFile) && File.ReadAllText(ReadyFile) == "READY";
    }
}
