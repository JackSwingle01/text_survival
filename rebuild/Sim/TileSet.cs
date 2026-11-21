using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// A set of tiles on one map as a bitset, saved as "WxH:base64" (about 3 KB for the valley).
[JsonConverter(typeof(TileSetConverter))]
public sealed class TileSet
{
    readonly ulong[] _bits;
    public int Width { get; }
    public int Height { get; }
    public int Count { get; private set; }

    public TileSet(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"{width}x{height} is no map");
        (Width, Height) = (width, height);
        _bits = new ulong[(width * height + 63) / 64];
    }

    int Index(TileId t) =>
        t.X >= 0 && t.X < Width && t.Y >= 0 && t.Y < Height ? t.Y * Width + t.X : throw new ArgumentOutOfRangeException(nameof(t), t, $"Off a {Width}x{Height} map");

    public bool Contains(TileId t) => (_bits[Index(t) >> 6] & 1UL << (Index(t) & 63)) != 0;

    /// Adds the tile; true if it was not there before.
    public bool Add(TileId t)
    {
        int i = Index(t);
        ulong bit = 1UL << (i & 63);
        if ((_bits[i >> 6] & bit) != 0) return false;
        _bits[i >> 6] |= bit;
        Count++;
        return true;
    }

    internal string Format() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Width}x{Height}:{Convert.ToBase64String(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_bits.AsSpan()))}");

    internal static TileSet Parse(string? s)
    {
        if (s?.Split(':') is not [var size, var data] || size.Split('x') is not [var w, var h])
            throw new JsonException($"TileSet '{s}' is not WxH:base64");
        var set = new TileSet(int.Parse(w, System.Globalization.CultureInfo.InvariantCulture), int.Parse(h, System.Globalization.CultureInfo.InvariantCulture));
        var bytes = Convert.FromBase64String(data);
        if (bytes.Length != set._bits.Length * 8) throw new JsonException($"TileSet holds {bytes.Length} bytes, a {set.Width}x{set.Height} map needs {set._bits.Length * 8}");
        bytes.CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes(set._bits.AsSpan()));
        set.Count = set._bits.Sum(System.Numerics.BitOperations.PopCount);
        return set;
    }
}

sealed class TileSetConverter : JsonConverter<TileSet>
{
    public override TileSet Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => TileSet.Parse(r.GetString());
    public override void Write(Utf8JsonWriter w, TileSet set, JsonSerializerOptions o) => w.WriteStringValue(set.Format());
}
