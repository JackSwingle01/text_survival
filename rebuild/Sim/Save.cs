using System.Text.Json;
using System.Text.Json.Nodes;

namespace Survival;

/// A save in any other format is refused, never half-read. Format 25 is the one older format still read: it
/// is migrated (Migrate25), so a save keeps what lies on its tiles. Otherwise no migrations until the first
/// release (documentation/decisions/2026-11-21-save-compatibility-from-first-release.md).
/// Changing it: documentation/save-format.md.
public static class Save
{
    /// The format this build writes and reads. Raise it with any change to what a save holds.
    public const int FormatVersion = 26;

    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    sealed record SaveFile(int Version, World World);

    public static string ToJson(World world) => JsonSerializer.Serialize(new SaveFile(FormatVersion, world), Options);

    public static World FromJson(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject;
        if (root == null || root["Version"] is not JsonValue version || version.GetValueKind() != JsonValueKind.Number)
            throw new JsonException("The save has no format version: it predates versioned saves and cannot be read");
        int format = version.GetValue<int>();
        if (format == 25) format = Migrate25(root);
        if (format != FormatVersion)
            throw new JsonException($"The save is in format {format}; this build reads format {FormatVersion} only");
        if (root["World"] is not JsonObject world) throw new JsonException("The save holds no world");
        return world.Deserialize<World>(Options) ?? throw new JsonException("The save holds no world");
    }

    /// Format 25 named the pile the stash: in field names, activity and event kinds, the text the game wrote
    /// (a pile's cause, a refusal, a find's name) and the activity a refusal names. Nothing else differs.
    static readonly Dictionary<string, string> PileFields = new()
    {
        ["Stash"] = "Pile", ["StashCause"] = "PileCause", ["StashId"] = "PileId", ["NextStashId"] = "NextPileId",
    };
    static readonly Dictionary<string, string> PileKinds = new()
    {
        ["stash"] = "putdown", ["unstash"] = "pickup", ["stashed"] = "putonpile", ["unstashed"] = "pickedup",
    };
    const string OldCause = "stashed by ", NewCause = "put down by ";
    static readonly (string Old, string New)[] PileWords =
    [
        (" stashed here", " in the pile here"), ("no stash here", "no pile here"), ("the stash", "the pile"),
        ("Medicine Stash", "Medicine Bundle"), ("Trapper's Stash", "Trapper's Bundle"),
    ];
    static readonly Dictionary<string, string> PileActivities = new() { ["Stash"] = "PutDown", ["Unstash"] = "PickUp" };

    static int Migrate25(JsonObject root)
    {
        Rename(root);
        root["Version"] = 26;
        return 26;
    }

    static void Rename(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array) Rename(item);
                break;
            case JsonObject obj:
                foreach (var (oldName, newName) in PileFields)
                    if (obj.Remove(oldName, out var moved))
                    {
                        if (oldName == "StashCause" && moved?["What"] is JsonValue what && what.TryGetValue(out string? text)
                            && text.StartsWith(OldCause, StringComparison.Ordinal))
                            moved["What"] = NewCause + text[OldCause.Length..];
                        obj[newName] = moved;
                    }
                if (obj["kind"] is JsonValue kind && kind.TryGetValue(out string? k))
                {
                    if (PileKinds.TryGetValue(k, out var renamed)) obj["kind"] = renamed;
                    if (k == "planrefused" && obj["Activity"] is JsonValue a && a.TryGetValue(out string? name) && PileActivities.TryGetValue(name, out var activity))
                        obj["Activity"] = activity;
                    if (k is "planrefused" or "pooldiscovered")
                        foreach (var field in new[] { "Reason", "Name" })
                            if (obj[field] is JsonValue v && v.TryGetValue(out string? said))
                                obj[field] = PileWords.Aggregate(said, (t, w) => t.Replace(w.Old, w.New, StringComparison.Ordinal));
                }
                foreach (var (_, child) in obj.ToList()) Rename(child);
                break;
        }
    }
}
