using System.Text.Json;

namespace Survival;

/// The save is the World as JSON, inside an envelope that names its format:
/// {"Version":N,"World":{...}}. A save in any other format is refused, never half-read: until the
/// first release there are no migrations (documentation/decisions/2026-11-21-save-compatibility-from-first-release.md).
/// Changing the format: documentation/save-format.md.
public static class Save
{
    /// The format this build writes and reads. Raise it with any change to what a save holds.
    public const int FormatVersion = 9;

    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    sealed record SaveFile(int Version, World World);

    public static string ToJson(World world) => JsonSerializer.Serialize(new SaveFile(FormatVersion, world), Options);

    public static World FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("Version", out var version)
            || version.ValueKind != JsonValueKind.Number)
            throw new JsonException("The save has no format version: it predates versioned saves and cannot be read");
        if (version.GetInt32() != FormatVersion)
            throw new JsonException($"The save is in format {version.GetInt32()}; this build reads format {FormatVersion} only");
        if (!doc.RootElement.TryGetProperty("World", out var world)) throw new JsonException("The save holds no world");
        return world.Deserialize<World>(Options) ?? throw new JsonException("The save holds no world");
    }
}
