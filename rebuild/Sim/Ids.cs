using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// IDs, never object references, link entities in the World.
[JsonConverter(typeof(AgentIdConverter))]
public readonly record struct AgentId(int Value)
{
    public static readonly AgentId Player = new(0);
    public override string ToString() => $"agent{Value}";
}

[JsonConverter(typeof(TileIdConverter))]
public readonly record struct TileId(int X, int Y);

/// TileId keys World.Tiles, so it serializes as "x,y" everywhere, key or value.
sealed class TileIdConverter : JsonConverter<TileId>
{
    public override TileId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void Write(Utf8JsonWriter w, TileId id, JsonSerializerOptions o) => w.WriteStringValue(Format(id));
    public override TileId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void WriteAsPropertyName(Utf8JsonWriter w, TileId id, JsonSerializerOptions o) => w.WritePropertyName(Format(id));
    internal static string Format(TileId id) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{id.X},{id.Y}");
    internal static TileId Parse(string? s)
    {
        var parts = (s ?? throw new JsonException("TileId is null")).Split(',');
        if (parts.Length != 2) throw new JsonException($"TileId '{s}' is not x,y");
        return new(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// AgentId keys World.Agents, so it must serialize as a JSON property name, not an object.
sealed class AgentIdConverter : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt32());
    public override void Write(Utf8JsonWriter w, AgentId id, JsonSerializerOptions o) => w.WriteNumberValue(id.Value);
    public override AgentId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
        new(int.Parse(r.GetString() ?? throw new JsonException("AgentId key is null")));
    public override void WriteAsPropertyName(Utf8JsonWriter w, AgentId id, JsonSerializerOptions o) =>
        w.WritePropertyName(id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

[JsonConverter(typeof(FireIdConverter))]
public readonly record struct FireId(int Value);

/// FireId keys World.Fires; same reason as AgentIdConverter.
sealed class FireIdConverter : JsonConverter<FireId>
{
    public override FireId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt32());
    public override void Write(Utf8JsonWriter w, FireId id, JsonSerializerOptions o) => w.WriteNumberValue(id.Value);
    public override FireId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
        new(int.Parse(r.GetString() ?? throw new JsonException("FireId key is null")));
    public override void WriteAsPropertyName(Utf8JsonWriter w, FireId id, JsonSerializerOptions o) =>
        w.WritePropertyName(id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// The unordered edge between two adjacent tiles: both directions of travel are one edge.
[JsonConverter(typeof(EdgeIdConverter))]
public readonly record struct EdgeId
{
    public TileId A { get; }
    public TileId B { get; }
    public EdgeId(TileId a, TileId b) => (A, B) = MapGen.EdgeKey(a, b);
}

/// EdgeId keys World.Trails, so it serializes as "x,y|x,y".
sealed class EdgeIdConverter : JsonConverter<EdgeId>
{
    public override EdgeId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void Write(Utf8JsonWriter w, EdgeId id, JsonSerializerOptions o) => w.WriteStringValue(Format(id));
    public override EdgeId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void WriteAsPropertyName(Utf8JsonWriter w, EdgeId id, JsonSerializerOptions o) => w.WritePropertyName(Format(id));
    static string Format(EdgeId id) => $"{TileIdConverter.Format(id.A)}|{TileIdConverter.Format(id.B)}";
    static EdgeId Parse(string? s)
    {
        var parts = (s ?? throw new JsonException("EdgeId is null")).Split('|');
        if (parts.Length != 2) throw new JsonException($"EdgeId '{s}' is not x,y|x,y");
        return new(TileIdConverter.Parse(parts[0]), TileIdConverter.Parse(parts[1]));
    }
}
