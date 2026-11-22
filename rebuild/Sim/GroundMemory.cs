using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// A tile's ground as a person last saw it (Ground.Look, to the millimetre and the hundredth), and
/// the minute they saw it.
public readonly record struct RememberedGround(SurfaceSummary Surface, long SeenMinute);

/// The tiles a person has seen on one map, each with its ground as last seen (Memory.Explored). Held
/// to millimetres and hundredths of mud, which is all a view shows, so the remembered ground and the
/// ground in sight read alike. Saved as
/// "WxH:seen:records", both base64: a bitset of the tiles seen, then ten bytes per tile seen, in
/// tile order (minute, snow mm, water mm, mud hundredths, crust and ice flags).
[JsonConverter(typeof(GroundMemoryConverter))]
public sealed class GroundMemory
{
    const int RecordBytes = 10;
    const byte Seen = 1, Crust = 2, Ice = 4;

    readonly int[] _minute;
    readonly ushort[] _snowMm, _waterMm;
    readonly byte[] _mudPct, _flags;

    public int Width { get; }
    public int Height { get; }
    public int Count { get; private set; }

    public GroundMemory(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"{width}x{height} is no map");
        (Width, Height) = (width, height);
        int n = width * height;
        (_minute, _snowMm, _waterMm, _mudPct, _flags) = (new int[n], new ushort[n], new ushort[n], new byte[n], new byte[n]);
    }

    int Index(TileId t) =>
        t.X >= 0 && t.X < Width && t.Y >= 0 && t.Y < Height ? t.Y * Width + t.X : throw new ArgumentOutOfRangeException(nameof(t), t, $"Off a {Width}x{Height} map");

    public bool Contains(TileId t) => (_flags[Index(t)] & Seen) != 0;

    /// The ground as last seen, or null for a tile never seen.
    public RememberedGround? At(TileId t)
    {
        int i = Index(t);
        if ((_flags[i] & Seen) == 0) return null;
        return new RememberedGround(new SurfaceSummary(_snowMm[i] / 1000.0, (_flags[i] & Crust) != 0, _mudPct[i] / 100.0, _waterMm[i] / 1000.0,
            (_flags[i] & Ice) != 0), _minute[i]);
    }

    public void Remember(TileId t, SurfaceSummary s, long minute)
    {
        int i = Index(t);
        if (minute is < int.MinValue or > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(minute), minute, "A minute ground memory cannot hold");
        // Every value is checked before any is written, so a refused one leaves the tile as it was.
        ushort snow = Quantised(s.SnowDepthM * 1000, ushort.MaxValue, nameof(s.SnowDepthM));
        ushort water = Quantised(s.WaterDepthM * 1000, ushort.MaxValue, nameof(s.WaterDepthM));
        byte mud = (byte)Quantised(s.MudLevel * 100, 100, nameof(s.MudLevel));
        if ((_flags[i] & Seen) == 0) Count++;
        (_minute[i], _snowMm[i], _waterMm[i], _mudPct[i]) = ((int)minute, snow, water, mud);
        _flags[i] = (byte)(Seen | (s.Crusted ? Crust : 0) | (s.Iced ? Ice : 0));
    }

    static ushort Quantised(double value, int max, string what)
    {
        double rounded = Math.Round(value);
        if (double.IsNaN(value) || rounded < 0 || rounded > max) throw new ArgumentOutOfRangeException(what, value, $"Ground memory holds 0 to {max}");
        return (ushort)rounded;
    }

    internal string Format()
    {
        var seen = new byte[(_flags.Length + 7) / 8];
        var records = new byte[Count * RecordBytes];
        int r = 0;
        for (int i = 0; i < _flags.Length; i++)
        {
            if ((_flags[i] & Seen) == 0) continue;
            seen[i >> 3] |= (byte)(1 << (i & 7));
            var rec = records.AsSpan(r, RecordBytes);
            BinaryPrimitives.WriteInt32LittleEndian(rec, _minute[i]);
            BinaryPrimitives.WriteUInt16LittleEndian(rec[4..], _snowMm[i]);
            BinaryPrimitives.WriteUInt16LittleEndian(rec[6..], _waterMm[i]);
            rec[8] = _mudPct[i];
            rec[9] = _flags[i];
            r += RecordBytes;
        }
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Width}x{Height}:{Convert.ToBase64String(seen)}:{Convert.ToBase64String(records)}");
    }

    static string? Start(string? s) => s?.Length > 40 ? s[..40] + "..." : s;

    internal static GroundMemory Parse(string? s)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (s?.Split(':') is not [var size, var seenData, var recordData] || size.Split('x') is not [var w, var h]
            || !int.TryParse(w, inv, out int width) || !int.TryParse(h, inv, out int height) || width <= 0 || height <= 0)
            throw new JsonException($"GroundMemory '{Start(s)}' is not WxH:base64:base64");
        var memory = new GroundMemory(width, height);
        byte[] seen, records;
        try { (seen, records) = (Convert.FromBase64String(seenData), Convert.FromBase64String(recordData)); }
        catch (FormatException e) { throw new JsonException($"GroundMemory '{Start(s)}' is not base64", e); }
        if (seen.Length != (memory._flags.Length + 7) / 8) throw new JsonException($"GroundMemory's seen tiles take {seen.Length} bytes, a {memory.Width}x{memory.Height} map needs {(memory._flags.Length + 7) / 8}");
        int r = 0;
        for (int i = 0; i < memory._flags.Length; i++)
        {
            if ((seen[i >> 3] & 1 << (i & 7)) == 0) continue;
            if (r + RecordBytes > records.Length) throw new JsonException("GroundMemory has fewer records than seen tiles");
            var rec = records.AsSpan(r, RecordBytes);
            memory._minute[i] = BinaryPrimitives.ReadInt32LittleEndian(rec);
            memory._snowMm[i] = BinaryPrimitives.ReadUInt16LittleEndian(rec[4..]);
            memory._waterMm[i] = BinaryPrimitives.ReadUInt16LittleEndian(rec[6..]);
            memory._mudPct[i] = rec[8];
            memory._flags[i] = rec[9];
            if ((rec[9] & Seen) == 0 || rec[9] > (Seen | Crust | Ice) || rec[8] > 100) throw new JsonException($"GroundMemory record {r / RecordBytes} is not a seen tile's ground");
            memory.Count++;
            r += RecordBytes;
        }
        if (r != records.Length) throw new JsonException("GroundMemory has more records than seen tiles");
        return memory;
    }
}

sealed class GroundMemoryConverter : JsonConverter<GroundMemory>
{
    public override GroundMemory Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => GroundMemory.Parse(r.GetString());
    public override void Write(Utf8JsonWriter w, GroundMemory memory, JsonSerializerOptions o) => w.WriteStringValue(memory.Format());
}
