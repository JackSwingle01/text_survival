namespace PixelArtCli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "new" => CmdNew(args[1..]),
                "render" => CmdRender(args[1..]),
                "render-all" => CmdRenderAll(args[1..]),
                "validate" => CmdValidate(args[1..]),
                "-h" or "--help" or "help" => PrintUsageOk(),
                _ => UnknownCommand(args[0])
            };
        }
        catch (PxaFormatException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"error: unknown command '{cmd}'");
        PrintUsage();
        return 1;
    }

    private static int PrintUsageOk()
    {
        PrintUsage();
        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            pixelart - author 16x16 (or any size) pixel art as text, render to PNG

            Usage:
              pixelart new <file.pxa> [--width N] [--height N]
                  Scaffold a blank .pxa template (default 16x16), filled with the
                  transparent palette key '.'.

              pixelart render <file.pxa> <output.png> [--preview <path>] [--preview-scale N]
                  Render a .pxa file to a native-resolution PNG. With --preview, also
                  write a nearest-neighbor upscaled copy (default scale 16x) for easy
                  visual review - the game never loads the preview file.

              pixelart render-all <sourceDir> <outputDir>
                  Recursively render every *.pxa under sourceDir to a matching *.png
                  under outputDir, preserving subfolder structure.

              pixelart validate <file.pxa>
                  Parse a .pxa file and report errors without writing anything.

            .pxa format:
              SIZE 16x16
              PALETTE
              . = #00000000
              a = #2b2015
              b = #a4835a
              PIXELS
              ................
              ...   (one line per row, exactly WIDTH characters, HEIGHT rows)
              ................

              Every character used in PIXELS must be defined in PALETTE. Colors are
              #RRGGBB or #RRGGBBAA. Blank lines and lines starting with // are ignored
              outside the PIXELS block.
            """);
    }

    private static int CmdNew(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: pixelart new <file.pxa> [--width N] [--height N]");
            return 1;
        }

        string path = args[0];
        int width = 16, height = 16;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--width" && i + 1 < args.Length) width = int.Parse(args[++i]);
            else if (args[i] == "--height" && i + 1 < args.Length) height = int.Parse(args[++i]);
        }

        var lines = new List<string>
        {
            $"SIZE {width}x{height}",
            "PALETTE",
            ". = #00000000",
            "PIXELS"
        };
        for (int y = 0; y < height; y++)
            lines.Add(new string('.', width));

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllLines(path, lines);

        Console.WriteLine($"wrote {path} ({width}x{height} template)");
        return 0;
    }

    private static int CmdRender(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: pixelart render <file.pxa> <output.png> [--preview <path>] [--preview-scale N]");
            return 1;
        }

        string inputPath = args[0];
        string outputPath = args[1];
        string? previewPath = null;
        int previewScale = 16;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--preview" && i + 1 < args.Length) previewPath = args[++i];
            else if (args[i] == "--preview-scale" && i + 1 < args.Length) previewScale = int.Parse(args[++i]);
        }

        RenderOne(inputPath, outputPath, previewPath, previewScale);
        return 0;
    }

    private static void RenderOne(string inputPath, string outputPath, string? previewPath, int previewScale)
    {
        var doc = PxaDocument.Parse(File.ReadAllText(inputPath), inputPath);
        PngWriter.EncodeToFile(outputPath, doc.Width, doc.Height, doc.ToRgba());
        Console.WriteLine($"rendered {inputPath} -> {outputPath} ({doc.Width}x{doc.Height})");

        if (previewPath != null)
        {
            var (w, h, rgba) = doc.ToUpscaledRgba(previewScale);
            PngWriter.EncodeToFile(previewPath, w, h, rgba);
            Console.WriteLine($"preview  {inputPath} -> {previewPath} ({w}x{h})");
        }
    }

    private static int CmdRenderAll(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: pixelart render-all <sourceDir> <outputDir>");
            return 1;
        }

        string sourceDir = args[0];
        string outputDir = args[1];

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"error: source directory '{sourceDir}' does not exist");
            return 1;
        }

        int count = 0;
        foreach (string file in Directory.EnumerateFiles(sourceDir, "*.pxa", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string outputPath = Path.Combine(outputDir, Path.ChangeExtension(relative, ".png"));
            RenderOne(file, outputPath, previewPath: null, previewScale: 16);
            count++;
        }

        Console.WriteLine($"rendered {count} file(s) from {sourceDir} to {outputDir}");
        return 0;
    }

    private static int CmdValidate(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: pixelart validate <file.pxa>");
            return 1;
        }

        string path = args[0];
        var doc = PxaDocument.Parse(File.ReadAllText(path), path);
        Console.WriteLine($"ok: {path} ({doc.Width}x{doc.Height}, {doc.PaletteCount} palette colors)");
        return 0;
    }
}
