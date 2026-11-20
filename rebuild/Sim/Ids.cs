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

public readonly record struct TileId(int X, int Y);

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
