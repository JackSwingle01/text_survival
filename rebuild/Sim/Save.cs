using System.Text.Json;

namespace Survival;

/// The save is the World, as JSON.
public static class Save
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string ToJson(World world) => JsonSerializer.Serialize(world, Options);

    public static World FromJson(string json) =>
        JsonSerializer.Deserialize<World>(json, Options) ?? throw new JsonException("Save file holds no world");
}
