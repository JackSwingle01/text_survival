using System.Globalization;

namespace PixelArtCli;

/// <summary>
/// A parsed .pxa (pixel art) source file: a fixed-size grid of single-character
/// palette keys, plus a palette mapping each key to an RGBA color.
/// </summary>
public class PxaDocument
{
    public required int Width;
    public required int Height;
    public required Dictionary<char, (byte R, byte G, byte B, byte A)> Palette;
    public required char[,] Grid; // [row, col], row 0 = top

    /// <summary>
    /// Render the document to a flat top-to-bottom RGBA8 byte buffer.
    /// </summary>
    public byte[] ToRgba()
    {
        var rgba = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char key = Grid[y, x];
                if (!Palette.TryGetValue(key, out var color))
                    throw new PxaFormatException($"Pixel at row {y + 1}, col {x + 1} uses undefined palette key '{key}'");

                int i = (y * Width + x) * 4;
                rgba[i] = color.R;
                rgba[i + 1] = color.G;
                rgba[i + 2] = color.B;
                rgba[i + 3] = color.A;
            }
        }
        return rgba;
    }

    /// <summary>
    /// Produce an upscaled (nearest-neighbor) RGBA8 buffer, for human-readable previews only.
    /// </summary>
    public (int Width, int Height, byte[] Rgba) ToUpscaledRgba(int scale)
    {
        if (scale < 1)
            throw new ArgumentOutOfRangeException(nameof(scale));

        byte[] native = ToRgba();
        int outW = Width * scale;
        int outH = Height * scale;
        var outBuf = new byte[outW * outH * 4];

        for (int y = 0; y < outH; y++)
        {
            int srcY = y / scale;
            for (int x = 0; x < outW; x++)
            {
                int srcX = x / scale;
                int srcI = (srcY * Width + srcX) * 4;
                int dstI = (y * outW + x) * 4;
                outBuf[dstI] = native[srcI];
                outBuf[dstI + 1] = native[srcI + 1];
                outBuf[dstI + 2] = native[srcI + 2];
                outBuf[dstI + 3] = native[srcI + 3];
            }
        }

        return (outW, outH, outBuf);
    }

    public static PxaDocument Parse(string text, string sourceName = "<input>")
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        int width = 16, height = 16;
        bool sizeSet = false;
        var palette = new Dictionary<char, (byte, byte, byte, byte)>();
        char[,]? grid = null;

        int i = 0;
        int lineNo = 0;

        string? PeekLine()
        {
            while (i < lines.Length)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//"))
                {
                    i++;
                    lineNo++;
                    continue;
                }
                return trimmed;
            }
            return null;
        }

        string ConsumeLine()
        {
            string? line = PeekLine() ?? throw new PxaFormatException($"{sourceName}: unexpected end of file");
            i++;
            lineNo++;
            return line;
        }

        while (true)
        {
            string? line = PeekLine();
            if (line == null) break;

            if (line.StartsWith("SIZE", StringComparison.OrdinalIgnoreCase))
            {
                ConsumeLine();
                string spec = line[4..].Trim();
                var parts = spec.Split('x', 'X');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
                {
                    throw new PxaFormatException($"{sourceName}: invalid SIZE line '{line}', expected 'SIZE WxH'");
                }
                sizeSet = true;
            }
            else if (line.Equals("PALETTE", StringComparison.OrdinalIgnoreCase))
            {
                ConsumeLine();
                while (true)
                {
                    string? entry = PeekLine();
                    if (entry == null || entry.Equals("PIXELS", StringComparison.OrdinalIgnoreCase))
                        break;
                    ConsumeLine();
                    ParsePaletteEntry(entry, palette, sourceName);
                }
            }
            else if (line.Equals("PIXELS", StringComparison.OrdinalIgnoreCase))
            {
                ConsumeLine();
                if (!sizeSet)
                    throw new PxaFormatException($"{sourceName}: PIXELS block requires a SIZE line first");

                grid = new char[height, width];
                for (int row = 0; row < height; row++)
                {
                    string pixelLine = ConsumeLine();
                    if (pixelLine.Length != width)
                        throw new PxaFormatException(
                            $"{sourceName}: pixel row {row + 1} has {pixelLine.Length} characters, expected {width}");
                    for (int col = 0; col < width; col++)
                        grid[row, col] = pixelLine[col];
                }
            }
            else
            {
                throw new PxaFormatException($"{sourceName}: unexpected line '{line}' (expected SIZE, PALETTE, or PIXELS)");
            }
        }

        if (grid == null)
            throw new PxaFormatException($"{sourceName}: missing PIXELS block");

        return new PxaDocument
        {
            Width = width,
            Height = height,
            Palette = palette,
            Grid = grid
        };
    }

    private static void ParsePaletteEntry(string entry, Dictionary<char, (byte, byte, byte, byte)> palette, string sourceName)
    {
        int eq = entry.IndexOf('=');
        if (eq < 0)
            throw new PxaFormatException($"{sourceName}: invalid palette line '{entry}', expected '<char> = #RRGGBB[AA]'");

        string keyPart = entry[..eq].Trim();
        string colorPart = entry[(eq + 1)..].Trim();

        if (keyPart.Length != 1)
            throw new PxaFormatException($"{sourceName}: palette key must be exactly one character, got '{keyPart}'");

        char key = keyPart[0];
        var color = ParseHexColor(colorPart, sourceName);

        if (!palette.TryAdd(key, color))
            throw new PxaFormatException($"{sourceName}: duplicate palette key '{key}'");
    }

    private static (byte, byte, byte, byte) ParseHexColor(string hex, string sourceName)
    {
        if (!hex.StartsWith('#'))
            throw new PxaFormatException($"{sourceName}: color '{hex}' must start with '#'");

        string digits = hex[1..];
        if (digits.Length != 6 && digits.Length != 8)
            throw new PxaFormatException($"{sourceName}: color '{hex}' must be #RRGGBB or #RRGGBBAA");

        byte r = ParseHexByte(digits, 0, sourceName, hex);
        byte g = ParseHexByte(digits, 2, sourceName, hex);
        byte b = ParseHexByte(digits, 4, sourceName, hex);
        byte a = digits.Length == 8 ? ParseHexByte(digits, 6, sourceName, hex) : (byte)255;

        return (r, g, b, a);
    }

    private static byte ParseHexByte(string digits, int offset, string sourceName, string hex)
    {
        if (!byte.TryParse(digits.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
            throw new PxaFormatException($"{sourceName}: invalid hex color '{hex}'");
        return value;
    }
}

public class PxaFormatException(string message) : Exception(message);
