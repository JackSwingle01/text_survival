using System.Text.Json;
using System.Text.Json.Serialization;

namespace Survival;

/// IDs, never object references, link entities in the World.
[JsonConverter(typeof(AgentIdConverter))]
public readonly record struct AgentId(int Value) : IComparable<AgentId>
{
    public int CompareTo(AgentId o) => Value.CompareTo(o.Value);
    public static readonly AgentId Player = new(0);
    public override string ToString() => $"agent{Value}";
}

[JsonConverter(typeof(TileIdConverter))]
public readonly record struct TileId(int X, int Y) : IComparable<TileId>
{
    // x-major, then y; field by field, since a tuple compare goes through the generic comparers.
    public int CompareTo(TileId o) => X != o.X ? (X < o.X ? -1 : 1) : Y < o.Y ? -1 : Y > o.Y ? 1 : 0;
}

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
public readonly record struct FireId(int Value) : IComparable<FireId>
{
    public int CompareTo(FireId o) => Value.CompareTo(o.Value);
}

sealed class FireIdConverter : JsonConverter<FireId>
{
    public override FireId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt32());
    public override void Write(Utf8JsonWriter w, FireId id, JsonSerializerOptions o) => w.WriteNumberValue(id.Value);
    public override FireId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
        new(int.Parse(r.GetString() ?? throw new JsonException("FireId key is null")));
    public override void WriteAsPropertyName(Utf8JsonWriter w, FireId id, JsonSerializerOptions o) =>
        w.WritePropertyName(id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// A curing rack (Stations): never reused (World.NextRackId).
[JsonConverter(typeof(RackIdConverter))]
public readonly record struct RackId(int Value) : IComparable<RackId>
{
    public int CompareTo(RackId o) => Value.CompareTo(o.Value);
}

sealed class RackIdConverter : JsonConverter<RackId>
{
    public override RackId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt32());
    public override void Write(Utf8JsonWriter w, RackId id, JsonSerializerOptions o) => w.WriteNumberValue(id.Value);
    public override RackId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
        new(int.Parse(r.GetString() ?? throw new JsonException("RackId key is null")));
    public override void WriteAsPropertyName(Utf8JsonWriter w, RackId id, JsonSerializerOptions o) =>
        w.WritePropertyName(id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// The unordered edge between two adjacent tiles: both directions of travel are one edge.
[JsonConverter(typeof(EdgeIdConverter))]
public readonly record struct EdgeId : IComparable<EdgeId>
{
    public int CompareTo(EdgeId o) => A.CompareTo(o.A) is int c && c != 0 ? c : B.CompareTo(o.B);
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

/// An RNG stream's name, keying World.Rngs: "body", or "body:12" for agent 12's own. Compared
/// ordinally, so a save lists its streams in one order on any machine.
[JsonConverter(typeof(StreamNameConverter))]
public readonly record struct StreamName(string Value) : IComparable<StreamName>
{
    public int CompareTo(StreamName o) => string.CompareOrdinal(Value, o.Value);
    public static implicit operator StreamName(string system) => new(system);
    public static StreamName Of(string system, AgentId who) => new(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{system}:{who.Value}"));
    /// Whether this is one of the agent's own streams (Of(any system, who)): "travel:1" is agent 1's, "travel:10" is not.
    public bool IsFor(AgentId who)
    {
        int colon = Value.LastIndexOf(':');
        return colon >= 0 && Value.AsSpan(colon + 1).SequenceEqual(who.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public override string ToString() => Value;
}

sealed class StreamNameConverter : JsonConverter<StreamName>
{
    public override StreamName Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetString() ?? throw new JsonException("StreamName is null"));
    public override void Write(Utf8JsonWriter w, StreamName id, JsonSerializerOptions o) => w.WriteStringValue(id.Value);
    public override StreamName ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Read(ref r, t, o);
    public override void WriteAsPropertyName(Utf8JsonWriter w, StreamName id, JsonSerializerOptions o) => w.WritePropertyName(id.Value);
}

[JsonConverter(typeof(PoolIdConverter))]
public readonly record struct PoolId(TileId Tile, int Index) : IComparable<PoolId>
{
    public int CompareTo(PoolId o) => Tile.CompareTo(o.Tile) is int c && c != 0 ? c : Index.CompareTo(o.Index);
    public override string ToString() => $"{TileIdConverter.Format(Tile)}#{Index}";
}

/// PoolId keys Memory.SearchHours, so it serializes as "x,y#i".
sealed class PoolIdConverter : JsonConverter<PoolId>
{
    public override PoolId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void Write(Utf8JsonWriter w, PoolId id, JsonSerializerOptions o) => w.WriteStringValue(id.ToString());
    public override PoolId ReadAsPropertyName(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Parse(r.GetString());
    public override void WriteAsPropertyName(Utf8JsonWriter w, PoolId id, JsonSerializerOptions o) => w.WritePropertyName(id.ToString());
    static PoolId Parse(string? s)
    {
        var parts = (s ?? throw new JsonException("PoolId is null")).Split('#');
        if (parts.Length != 2) throw new JsonException($"PoolId '{s}' is not x,y#i");
        return new(TileIdConverter.Parse(parts[0]), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
