using System.Globalization;

namespace PixelArtCli;

using Rgba = (byte R, byte G, byte B, byte A);

/// <summary>
/// A parsed and fully-executed .pxa source file, resolved to a final RGBA canvas.
///
/// Two authoring styles, freely mixable:
///  - Direct: SIZE + PALETTE + (PIXELS grid and/or PIXEL/RECT/LINE/CIRCLE/FILL/MIRRORX/MIRRORY
///    commands) drawn straight onto the output canvas. Good for small, simple, single-shape icons.
///  - Composed: PART blocks build small named sub-drawings in their own local coordinate space,
///    then a COMPOSE block stitches them onto the output canvas using STACK (position computed
///    from each part's *actual drawn content*, so e.g. "logs BELOW flame" is guaranteed to touch,
///    not eyeballed as absolute coordinates). A CHECKS block can assert TOUCHES/CONNECTED and
///    fails the parse (so render refuses to write a bad PNG) if violated.
/// See tools/PixelArtCli/README.md for the full command reference.
/// </summary>
public class PxaDocument
{
    public readonly int Width;
    public readonly int Height;
    public readonly int PaletteCount;
    private readonly Canvas _output;

    private PxaDocument(int width, int height, int paletteCount, Canvas output)
    {
        Width = width;
        Height = height;
        PaletteCount = paletteCount;
        _output = output;
    }

    public byte[] ToRgba() => _output.Rgba;

    /// <summary>Nearest-neighbor upscale, for human-readable previews only.</summary>
    public (int Width, int Height, byte[] Rgba) ToUpscaledRgba(int scale)
    {
        if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale));

        byte[] native = _output.Rgba;
        int outW = Width * scale, outH = Height * scale;
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

    public static PxaDocument Parse(string text, string sourceName = "<input>") => new Parser(text, sourceName).Run();

    private class Parser(string text, string sourceName)
    {
        private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "SIZE", "PALETTE", "PIXELS", "PART", "ENDPART", "COMPOSE", "ENDCOMPOSE", "CHECKS", "ENDCHECKS",
            "GRID", "PIXEL", "RECT", "LINE", "CIRCLE", "FILL", "MIRRORX", "MIRRORY", "PLACE", "STACK",
            "TOUCHES", "CONNECTED"
        };

        private readonly string[] _lines = text.Replace("\r\n", "\n").Split('\n');
        private int _i;

        private readonly Dictionary<char, Rgba> _palette = new();
        private readonly Dictionary<string, Canvas> _parts = new();
        private readonly Dictionary<string, (int X, int Y)> _placements = new();
        private readonly List<string> _checkFailures = new();

        private Canvas? _output;
        private int _width, _height;

        public PxaDocument Run()
        {
            while (true)
            {
                string? line = PeekLine();
                if (line == null) break;

                string[] tokens = Tokenize(line);
                switch (tokens[0].ToUpperInvariant())
                {
                    case "SIZE":
                        ConsumeLine();
                        RequireArgs(tokens, 2, "SIZE WxH");
                        (_width, _height) = ParseWxH(tokens[1]);
                        _output = new Canvas(_width, _height);
                        break;

                    case "PALETTE":
                        ConsumeLine();
                        ParsePaletteBlock();
                        break;

                    case "PIXELS":
                        ConsumeLine();
                        RequireOutput();
                        ApplyGrid(_output!, _width, _height);
                        break;

                    case "PART":
                        ConsumeLine();
                        ParsePart(tokens);
                        break;

                    case "COMPOSE":
                        ConsumeLine();
                        RequireOutput();
                        ParseCompose();
                        break;

                    case "CHECKS":
                        ConsumeLine();
                        ParseChecks();
                        break;

                    case "PIXEL": case "RECT": case "LINE": case "CIRCLE": case "FILL":
                    case "MIRRORX": case "MIRRORY":
                        ConsumeLine();
                        RequireOutput();
                        ExecuteDrawCommand(_output!, tokens);
                        break;

                    default:
                        throw Error($"unexpected line '{line}'");
                }
            }

            if (_output == null)
                throw Error("missing SIZE line");
            if (_checkFailures.Count > 0)
                throw Error("check(s) failed:\n  " + string.Join("\n  ", _checkFailures));

            return new PxaDocument(_width, _height, _palette.Count, _output);
        }

        // === Line cursor ===

        private string? PeekLine()
        {
            while (_i < _lines.Length)
            {
                string trimmed = _lines[_i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("//")) { _i++; continue; }
                return trimmed;
            }
            return null;
        }

        private string ConsumeLine()
        {
            string line = PeekLine() ?? throw Error("unexpected end of file");
            _i++;
            return line;
        }

        private static string[] Tokenize(string line) => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        private PxaFormatException Error(string message) => new($"{sourceName}: {message}");

        private void RequireOutput()
        {
            if (_output == null) throw Error("SIZE line must come before drawing commands");
        }

        private void RequireArgs(string[] tokens, int expected, string usage)
        {
            if (tokens.Length != expected) throw Error($"invalid line, expected '{usage}'");
        }

        // === SIZE / PALETTE ===

        private (int, int) ParseWxH(string spec)
        {
            var parts = spec.Split('x', 'X');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
                throw Error($"invalid size '{spec}', expected WxH");
            return (w, h);
        }

        private void ParsePaletteBlock()
        {
            while (true)
            {
                string? line = PeekLine();
                if (line == null) break;
                if (ReservedKeywords.Contains(Tokenize(line)[0])) break;
                ConsumeLine();
                ParsePaletteEntry(line);
            }
        }

        private void ParsePaletteEntry(string entry)
        {
            int eq = entry.IndexOf('=');
            if (eq < 0) throw Error($"invalid palette line '{entry}', expected '<char> = #RRGGBB[AA]'");

            string keyPart = entry[..eq].Trim();
            string colorPart = entry[(eq + 1)..].Trim();
            if (keyPart.Length != 1) throw Error($"palette key must be exactly one character, got '{keyPart}'");

            char key = keyPart[0];
            Rgba color = ParseHexColor(colorPart);
            if (!_palette.TryAdd(key, color)) throw Error($"duplicate palette key '{key}'");
        }

        private Rgba ParseHexColor(string hex)
        {
            if (!hex.StartsWith('#')) throw Error($"color '{hex}' must start with '#'");
            string digits = hex[1..];
            if (digits.Length != 6 && digits.Length != 8) throw Error($"color '{hex}' must be #RRGGBB or #RRGGBBAA");

            byte r = ParseHexByte(digits, 0, hex);
            byte g = ParseHexByte(digits, 2, hex);
            byte b = ParseHexByte(digits, 4, hex);
            byte a = digits.Length == 8 ? ParseHexByte(digits, 6, hex) : (byte)255;
            return (r, g, b, a);
        }

        private byte ParseHexByte(string digits, int offset, string hex)
        {
            if (!byte.TryParse(digits.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                throw Error($"invalid hex color '{hex}'");
            return value;
        }

        private Rgba ResolveColor(string token)
        {
            if (token.Length != 1 || !_palette.TryGetValue(token[0], out var color))
                throw Error($"undefined palette key '{token}'");
            return color;
        }

        private int ParseInt(string s)
        {
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw Error($"invalid integer '{s}'");
            return value;
        }

        // === Grids and draw commands (shared by top-level canvas and PART canvases) ===

        private void ApplyGrid(Canvas target, int width, int height)
        {
            for (int row = 0; row < height; row++)
            {
                string pixelLine = ConsumeLine();
                if (pixelLine.Length != width)
                    throw Error($"pixel row {row + 1} has {pixelLine.Length} characters, expected {width}");
                for (int col = 0; col < width; col++)
                    target.SetPixel(col, row, ResolveColor(pixelLine[col].ToString()));
            }
        }

        private void ExecuteDrawCommand(Canvas canvas, string[] tokens)
        {
            switch (tokens[0].ToUpperInvariant())
            {
                case "PIXEL":
                    RequireArgs(tokens, 4, "PIXEL x y color");
                    canvas.SetPixel(ParseInt(tokens[1]), ParseInt(tokens[2]), ResolveColor(tokens[3]));
                    break;
                case "RECT":
                    RequireArgs(tokens, 6, "RECT x y w h color");
                    canvas.Rect(ParseInt(tokens[1]), ParseInt(tokens[2]), ParseInt(tokens[3]), ParseInt(tokens[4]), ResolveColor(tokens[5]));
                    break;
                case "LINE":
                    RequireArgs(tokens, 6, "LINE x0 y0 x1 y1 color");
                    canvas.Line(ParseInt(tokens[1]), ParseInt(tokens[2]), ParseInt(tokens[3]), ParseInt(tokens[4]), ResolveColor(tokens[5]));
                    break;
                case "CIRCLE":
                    RequireArgs(tokens, 5, "CIRCLE cx cy r color");
                    canvas.Circle(ParseInt(tokens[1]), ParseInt(tokens[2]), ParseInt(tokens[3]), ResolveColor(tokens[4]));
                    break;
                case "FILL":
                    RequireArgs(tokens, 4, "FILL x y color");
                    canvas.Fill(ParseInt(tokens[1]), ParseInt(tokens[2]), ResolveColor(tokens[3]));
                    break;
                case "MIRRORX":
                    RequireArgs(tokens, 1, "MIRRORX");
                    canvas.MirrorX();
                    break;
                case "MIRRORY":
                    RequireArgs(tokens, 1, "MIRRORY");
                    canvas.MirrorY();
                    break;
                default:
                    throw Error($"unknown draw command '{tokens[0]}'");
            }
        }

        // === PART ===

        private void ParsePart(string[] headerTokens)
        {
            RequireArgs(headerTokens, 3, "PART name WxH");
            string name = headerTokens[1];
            var (pw, ph) = ParseWxH(headerTokens[2]);

            if (_parts.ContainsKey(name)) throw Error($"duplicate PART name '{name}'");

            var canvas = new Canvas(pw, ph);
            while (true)
            {
                string line = ConsumeLine();
                string[] tokens = Tokenize(line);
                string keyword = tokens[0].ToUpperInvariant();

                if (keyword == "ENDPART") break;
                if (keyword == "GRID") { ApplyGrid(canvas, pw, ph); continue; }
                if (keyword is "PIXEL" or "RECT" or "LINE" or "CIRCLE" or "FILL" or "MIRRORX" or "MIRRORY")
                {
                    ExecuteDrawCommand(canvas, tokens);
                    continue;
                }
                throw Error($"unexpected line inside PART '{name}': '{line}'");
            }

            _parts[name] = canvas;
        }

        private Canvas GetPart(string name) =>
            _parts.TryGetValue(name, out var c) ? c : throw Error($"unknown part '{name}' (PART blocks must appear before they're used)");

        // === COMPOSE ===

        private void ParseCompose()
        {
            while (true)
            {
                string line = ConsumeLine();
                string[] tokens = Tokenize(line);
                switch (tokens[0].ToUpperInvariant())
                {
                    case "ENDCOMPOSE": return;
                    case "PLACE": ExecutePlace(tokens); break;
                    case "STACK": ExecuteStack(tokens); break;
                    default: throw Error($"unexpected line inside COMPOSE: '{line}'");
                }
            }
        }

        private void ExecutePlace(string[] tokens)
        {
            if (tokens.Length < 3) throw Error("invalid PLACE line, expected 'PLACE name AT x y' or 'PLACE name CENTERED'");
            string name = tokens[1];
            Canvas part = GetPart(name);
            if (_placements.ContainsKey(name)) throw Error($"part '{name}' already placed");

            int px, py;
            switch (tokens[2].ToUpperInvariant())
            {
                case "AT":
                    RequireArgs(tokens, 5, "PLACE name AT x y");
                    px = ParseInt(tokens[3]);
                    py = ParseInt(tokens[4]);
                    break;
                case "CENTERED":
                    RequireArgs(tokens, 3, "PLACE name CENTERED");
                    var bbox = part.BoundingBox() ?? throw Error($"part '{name}' is empty, nothing to place");
                    px = (int)Math.Round(_width / 2.0 - (bbox.MinX + bbox.MaxX) / 2.0);
                    py = (int)Math.Round(_height / 2.0 - (bbox.MinY + bbox.MaxY) / 2.0);
                    break;
                default:
                    throw Error($"unknown PLACE mode '{tokens[2]}', expected AT or CENTERED");
            }

            _output!.BlitFrom(part, px, py);
            _placements[name] = (px, py);
        }

        private void ExecuteStack(string[] tokens)
        {
            if (tokens.Length < 4)
                throw Error("invalid STACK line, expected 'STACK name BELOW|ABOVE|LEFTOF|RIGHTOF other [CENTERED|DX n|DY n] [OVERLAP n]'");

            string name = tokens[1];
            string direction = tokens[2].ToUpperInvariant();
            string otherName = tokens[3];

            Canvas part = GetPart(name);
            if (_placements.ContainsKey(name)) throw Error($"part '{name}' already placed");
            Canvas other = GetPart(otherName);
            if (!_placements.TryGetValue(otherName, out var otherPos))
                throw Error($"part '{otherName}' must be placed before '{name}' can stack against it");

            var partBBox = part.BoundingBox() ?? throw Error($"part '{name}' is empty, nothing to place");
            var otherBBox = other.BoundingBox() ?? throw Error($"part '{otherName}' is empty, cannot stack against it");

            int otherAbsMinX = otherPos.X + otherBBox.MinX, otherAbsMaxX = otherPos.X + otherBBox.MaxX;
            int otherAbsMinY = otherPos.Y + otherBBox.MinY, otherAbsMaxY = otherPos.Y + otherBBox.MaxY;
            double otherAbsCenterX = otherPos.X + (otherBBox.MinX + otherBBox.MaxX) / 2.0;
            double otherAbsCenterY = otherPos.Y + (otherBBox.MinY + otherBBox.MaxY) / 2.0;
            double partLocalCenterX = (partBBox.MinX + partBBox.MaxX) / 2.0;
            double partLocalCenterY = (partBBox.MinY + partBBox.MaxY) / 2.0;

            int overlap = 0;
            bool centered = true;
            int crossOffset = 0;
            for (int i = 4; i < tokens.Length; i++)
            {
                switch (tokens[i].ToUpperInvariant())
                {
                    case "CENTERED":
                        centered = true;
                        break;
                    case "DX":
                    case "DY":
                        centered = false;
                        if (i + 1 >= tokens.Length) throw Error($"{tokens[i]} requires a value");
                        crossOffset = ParseInt(tokens[++i]);
                        break;
                    case "OVERLAP":
                        if (i + 1 >= tokens.Length) throw Error("OVERLAP requires a value");
                        overlap = ParseInt(tokens[++i]);
                        break;
                    default:
                        throw Error($"unknown STACK option '{tokens[i]}'");
                }
            }

            int px, py;
            switch (direction)
            {
                case "BELOW":
                    py = otherAbsMaxY + 1 - overlap - partBBox.MinY;
                    px = centered ? (int)Math.Round(otherAbsCenterX - partLocalCenterX) : otherAbsMinX - partBBox.MinX + crossOffset;
                    break;
                case "ABOVE":
                    py = otherAbsMinY - 1 + overlap - partBBox.MaxY;
                    px = centered ? (int)Math.Round(otherAbsCenterX - partLocalCenterX) : otherAbsMinX - partBBox.MinX + crossOffset;
                    break;
                case "RIGHTOF":
                    px = otherAbsMaxX + 1 - overlap - partBBox.MinX;
                    py = centered ? (int)Math.Round(otherAbsCenterY - partLocalCenterY) : otherAbsMinY - partBBox.MinY + crossOffset;
                    break;
                case "LEFTOF":
                    px = otherAbsMinX - 1 + overlap - partBBox.MaxX;
                    py = centered ? (int)Math.Round(otherAbsCenterY - partLocalCenterY) : otherAbsMinY - partBBox.MinY + crossOffset;
                    break;
                default:
                    throw Error($"unknown STACK direction '{tokens[2]}', expected BELOW/ABOVE/LEFTOF/RIGHTOF");
            }

            _output!.BlitFrom(part, px, py);
            _placements[name] = (px, py);
        }

        // === CHECKS ===

        private void ParseChecks()
        {
            while (true)
            {
                string line = ConsumeLine();
                string[] tokens = Tokenize(line);
                switch (tokens[0].ToUpperInvariant())
                {
                    case "ENDCHECKS": return;
                    case "TOUCHES":
                        RequireArgs(tokens, 3, "TOUCHES partA partB");
                        CheckTouches(tokens[1], tokens[2]);
                        break;
                    case "CONNECTED":
                        if (tokens.Length == 1) CheckConnected(_output, "image");
                        else if (tokens.Length == 2) CheckConnected(GetPart(tokens[1]), tokens[1]);
                        else throw Error("invalid CONNECTED line, expected 'CONNECTED' or 'CONNECTED partName'");
                        break;
                    default:
                        throw Error($"unexpected line inside CHECKS: '{line}'");
                }
            }
        }

        private void CheckTouches(string nameA, string nameB)
        {
            Canvas a = GetPart(nameA), b = GetPart(nameB);
            if (!_placements.TryGetValue(nameA, out var posA)) throw Error($"TOUCHES: part '{nameA}' has not been placed via COMPOSE");
            if (!_placements.TryGetValue(nameB, out var posB)) throw Error($"TOUCHES: part '{nameB}' has not been placed via COMPOSE");

            if (!Canvas.AreTouching(a, posA.X, posA.Y, b, posB.X, posB.Y))
                _checkFailures.Add($"TOUCHES {nameA} {nameB}: parts do not touch after composition (there is a gap)");
        }

        private void CheckConnected(Canvas? canvas, string label)
        {
            var (count, extras) = canvas!.CountConnectedComponents();
            if (count > 1)
            {
                string where = string.Join(", ", extras.Select(e => $"({e.MinX},{e.MinY})-({e.MaxX},{e.MaxY})"));
                _checkFailures.Add($"CONNECTED {label}: {count} disconnected regions, expected 1 (extra region(s) at: {where})");
            }
        }
    }
}

public class PxaFormatException(string message) : Exception(message);
