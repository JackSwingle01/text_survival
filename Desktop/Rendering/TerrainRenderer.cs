using System.Numerics;
using Raylib_cs;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>World-aligned terrain at twice the asset scale, with blended biome edges.</summary>
public static class TerrainRenderer
{
    // A 96px source spans two map cells: each cell draws a 48px quadrant.
    internal const int Resolution = 48;
    internal readonly record struct Art(int Width, int Height, Color[] Pixels);
    internal readonly record struct Neighborhood(ulong Packed)
    {
        public TerrainType At(int dx, int dy) =>
            (TerrainType)((Packed >> (((dy+1)*3+dx+1)*4)) & 15);

        public static Neighborhood Create(TerrainType center, Func<int, int, TerrainType?>? neighbor = null)
        {
            ulong packed = 0;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                // Unknown and off-map neighbors never contribute their terrain.
                var terrain = x == 0 && y == 0 ? center : neighbor?.Invoke(x, y) ?? center;
                packed |= (ulong)terrain << (((y+1)*3+x+1)*4);
            }
            return new Neighborhood(packed);
        }
    }

    private readonly record struct Key(int X, int Y, Neighborhood Neighbors, bool CaveFloor);
    private sealed class Cached(Texture2D texture, long frame)
    {
        public Texture2D Texture = texture;
        public long Frame = frame;
    }
    private static readonly Dictionary<TerrainType, Art[]> _art = new();
    private static readonly Dictionary<Key, Cached> _cache = new();
    private static Art[] _caveFloor = [];
    private static long _frame;

    public static unsafe void Load(string path)
    {
        Unload();
        foreach (var group in Directory.GetFiles(path, "*_tile*.png").Order(StringComparer.Ordinal)
                     .GroupBy(file => Path.GetFileName(file).Split("_tile")[0]))
        {
            bool caveFloor = group.Key.Equals("cavefloor", StringComparison.OrdinalIgnoreCase);
            if (!Enum.TryParse<TerrainType>(group.Key, true, out var terrain) && !caveFloor) continue;
            var variants = new List<Art>();
            foreach (string file in group)
            {
                var image = Raylib.LoadImage(file);
                if (image.Data == null) throw new IOException($"Cannot load terrain: {file}");
                try
                {
                    var colors = Raylib.LoadImageColors(image);
                    try
                    {
                        variants.Add(new Art(image.Width, image.Height,
                            new ReadOnlySpan<Color>(colors, image.Width*image.Height).ToArray()));
                    }
                    finally { Raylib.UnloadImageColors(colors); }
                }
                finally { Raylib.UnloadImage(image); }
            }
            if (caveFloor) _caveFloor = variants.ToArray();
            else _art.Add(terrain, variants.ToArray());
        }
        if (_caveFloor.Length == 0) throw new IOException("Missing cave floor art.");
        CaveEntranceRenderer.Load(path);
        foreach (var terrain in Enum.GetValues<TerrainType>())
            if (!_art.ContainsKey(terrain)) throw new IOException($"Missing terrain art: {terrain}");
    }

    // Called before any terrain is queued this frame. Never unload textures that
    // Raylib's current batch still references; retain the previous frame's working set.
    public static void BeginFrame()
    {
        if (_cache.Count > 1024)
            foreach (var key in _cache.Where(pair => pair.Value.Frame < _frame).Select(pair => pair.Key).ToArray())
            {
                Raylib.UnloadTexture(_cache[key].Texture);
                _cache.Remove(key);
            }
        _frame++;
    }

    public static void Unload()
    {
        foreach (var item in _cache.Values) Raylib.UnloadTexture(item.Texture);
        _cache.Clear();
        _art.Clear();
        _caveFloor = [];
        CaveEntranceRenderer.Unload();
    }

    internal static unsafe void Draw(float x, float y, float size, int worldX, int worldY,
        float timeFactor, Neighborhood neighbors, bool caveFloor = false)
    {
        var key = new Key(worldX, worldY, neighbors, caveFloor);
        if (!_cache.TryGetValue(key, out var cached))
        {
            var pixels = Rasterize(worldX, worldY, neighbors, _art, caveFloor: caveFloor);
            Texture2D texture;
            fixed (Color* data = pixels)
            {
                var image = new Image { Data = data, Width = Resolution, Height = Resolution,
                    Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
                texture = Raylib.LoadTextureFromImage(image);
            }
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            _cache[key] = cached = new Cached(texture, _frame);
        }
        cached.Frame = _frame;
        byte brightness = (byte)(255 * (0.4f + Math.Clamp(timeFactor, 0, 1) * 0.6f));
        Raylib.DrawTexturePro(cached.Texture, new Rectangle(0, 0, Resolution, Resolution),
            new Rectangle(x, y, size, size), Vector2.Zero, 0,
            new Color(brightness, brightness, brightness, (byte)255));
    }

    internal static Color[] Rasterize(int worldX, int worldY, Neighborhood neighbors,
        IReadOnlyDictionary<TerrainType, Art[]> art, bool blend = true, bool caveFloor = false)
    {
        var pixels = new Color[Resolution*Resolution];
        for (int y = 0; y < Resolution; y++)
        for (int x = 0; x < Resolution; x++)
        {
            int px = worldX*Resolution+x, py = worldY*Resolution+y;
            if (caveFloor)
            {
                pixels[y*Resolution+x] = SampleArt(_caveFloor, px, py);
                continue;
            }
            float u = (x+0.5f)/Resolution, v = (y+0.5f)/Resolution;
            // The same continuous world-space displacement on both sides of an
            // edge prevents straight seams, including four-terrain junctions.
            float wx = (px+0.5f)/Resolution, wy = (py+0.5f)/Resolution;
            float du = MathF.Sin(wy*11 + MathF.Sin(wx*7))*0.025f;
            float dv = MathF.Sin(wx*13 + MathF.Sin(wy*5))*0.025f;
            var (dx, ax) = EdgeWeight(u+du);
            var (dy, ay) = EdgeWeight(v+dv);
            if (!blend) ax = ay = 0;
            var a = Sample(neighbors.At(0, 0), px, py, art);
            var b = Sample(neighbors.At(dx, 0), px, py, art);
            var c = Sample(neighbors.At(0, dy), px, py, art);
            var d = Sample(neighbors.At(dx, dy), px, py, art);
            pixels[y*Resolution+x] = Mix(Mix(a, b, ax), Mix(c, d, ax), ay);
        }
        return pixels;
    }

    private static (int Direction, float Weight) EdgeWeight(float position)
    {
        const float width = 0.16f;
        int direction = position < 0.5f ? -1 : 1;
        float distance = direction < 0 ? position : 1-position;
        float t = Math.Clamp((distance+width)/(2*width), 0, 1);
        return (direction, 1-t*t*(3-2*t));
    }

    private static Color Mix(Color a, Color b, float weight) => new(
        (byte)MathF.Round(a.R+(b.R-a.R)*weight),
        (byte)MathF.Round(a.G+(b.G-a.G)*weight),
        (byte)MathF.Round(a.B+(b.B-a.B)*weight), (byte)255);

    internal static Color Sample(TerrainType terrain, int px, int py, IReadOnlyDictionary<TerrainType, Art[]> art)
    {
        return SampleArt(art[terrain], px, py);
    }

    private static Color SampleArt(Art[] variants, int px, int py)
    {
        const int span = Resolution*2;
        int tx = (int)Math.Floor((double)px/span), ty = (int)Math.Floor((double)py/span);
        var tile = variants[VariantIndex(tx, ty, variants.Length)];
        int x = (px-tx*span)*tile.Width/span, y = (py-ty*span)*tile.Height/span;
        return tile.Pixels[y*tile.Width+x];
    }

    // Keep the authoring tool's world-position hash in sync.
    internal static int VariantIndex(int x, int y, int count)
    {
        if (count <= 1) return 0;
        unchecked
        {
            int h = x*73856093 ^ y*19349663;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (int)((uint)h % (uint)count);
        }
    }
}
