using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace text_survival.GameCli;

public static class CliProtocol
{
    public static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static (string command, string? value) Parse(string line)
    {
        if (!line.TrimStart().StartsWith('{')) return (line, null);
        using var doc = JsonDocument.Parse(line);
        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty("command", out var cmd) || cmd.ValueKind != JsonValueKind.String)
            throw new ArgumentException("JSON input needs a string command property.");
        string? value = null;
        if (doc.RootElement.TryGetProperty("value", out var v))
            value = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        return (cmd.GetString()!, value);
    }

    public static bool IsInputError(Exception ex) => ex is ArgumentException or JsonException or KeyNotFoundException or FormatException or OverflowException;
}
