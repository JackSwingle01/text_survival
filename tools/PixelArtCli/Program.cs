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
                "sheet" => CmdSheet(args[1..]),
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

              pixelart sheet <sourceDir> <output.png> [--scale N] [--cols N]
                  Tile every *.pxa under sourceDir onto one upscaled contact sheet over a
                  neutral grey ground. Use it to review a whole set at once and catch
                  style drift between assets.

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

    /// <summary>
    /// Contact sheet: every .pxa under a directory, upscaled and tiled onto one PNG over a
    /// neutral grey ground. Reviewing a whole set at once is how style drift gets caught -
    /// assets that each look fine alone can still disagree on outline weight, palette or scale.
    /// </summary>
    private static int CmdSheet(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: pixelart sheet <sourceDir> <output.png> [--scale N] [--cols N]");
            return 1;
        }

        string sourceDir = args[0];
        string outputPath = args[1];
        int scale = 8, cols = 0;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--scale" && i + 1 < args.Length) scale = int.Parse(args[++i]);
            else if (args[i] == "--cols" && i + 1 < args.Length) cols = int.Parse(args[++i]);
        }

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"error: source directory '{sourceDir}' does not exist");
            return 1;
        }

        var docs = new List<PxaDocument>();
        foreach (string file in Directory.EnumerateFiles(sourceDir, "*.pxa", SearchOption.AllDirectories).Order())
            docs.Add(PxaDocument.Parse(File.ReadAllText(file), file));

        if (docs.Count == 0)
        {
            Console.Error.WriteLine($"error: no .pxa files found under '{sourceDir}'");
            return 1;
        }

        const int pad = 8;
        int cellW = docs.Max(d => d.Width) * scale;
        int cellH = docs.Max(d => d.Height) * scale;
        if (cols <= 0) cols = (int)Math.Ceiling(Math.Sqrt(docs.Count));
        int rows = (docs.Count + cols - 1) / cols;

        int sheetW = cols * (cellW + pad) + pad;
        int sheetH = rows * (cellH + pad) + pad;
        var sheet = new byte[sheetW * sheetH * 4];

        // Neutral mid-grey ground so both light and dark art stays visible.
        for (int i = 0; i < sheetW * sheetH; i++)
        {
            sheet[i * 4] = 0x60;
            sheet[i * 4 + 1] = 0x64;
            sheet[i * 4 + 2] = 0x6a;
            sheet[i * 4 + 3] = 0xFF;
        }

        for (int idx = 0; idx < docs.Count; idx++)
        {
            int originX = pad + idx % cols * (cellW + pad);
            int originY = pad + idx / cols * (cellH + pad);
            var (w, h, rgba) = docs[idx].ToUpscaledRgba(scale);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int src = (y * w + x) * 4;
                    if (rgba[src + 3] == 0) continue; // transparent: let the ground show through
                    int dst = ((originY + y) * sheetW + originX + x) * 4;
                    sheet[dst] = rgba[src];
                    sheet[dst + 1] = rgba[src + 1];
                    sheet[dst + 2] = rgba[src + 2];
                    sheet[dst + 3] = 255;
                }
        }

        PngWriter.EncodeToFile(outputPath, sheetW, sheetH, sheet);
        Console.WriteLine($"sheet: {docs.Count} asset(s) -> {outputPath} ({sheetW}x{sheetH}, {cols} cols, {scale}x)");
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
