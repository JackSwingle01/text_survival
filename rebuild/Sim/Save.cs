using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Survival;

/// A save in any other format is refused, never half-read, and none is migrated: documentation/save-format.md.
public static class Save
{
    /// The last change of what a saved value means that kept the types' shape (a unit, a scale, an encoding), as its
    /// issue and a few words. Shape changes need nothing here.
    const string MeaningChange = "none since formats were derived";

    static readonly JsonSerializerOptions Options = new() { WriteIndented = false, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    /// A hash of the saved types (Describe) and MeaningChange: no branch picks a number, so no two clash.
    public static string Format { get; } = Fingerprint(Describe(typeof(World), Options), MeaningChange);

    sealed record SaveFile(string Format, World World);

    public static string ToJson(World world) => JsonSerializer.Serialize(new SaveFile(Format, world), Options);

    public static World FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("The save has no format: it is not a JSON object");
        if (root.TryGetProperty("Version", out var number))
            throw new JsonException($"The save is in numbered format {number}, from before formats were derived from the saved types; this build reads format {Format} only");
        if (!root.TryGetProperty("Format", out var format) || format.ValueKind != JsonValueKind.String)
            throw new JsonException("The save has no format: it predates versioned saves and cannot be read");
        if (format.GetString() != Format)
            throw new JsonException($"The save is in format {format.GetString()}; this build reads format {Format} only");
        if (!root.TryGetProperty("World", out var world)) throw new JsonException("The save holds no world");
        return WorldFromJson(world);
    }

    /// The world without the envelope, as the fixture keeps it: no Format line for two branches to rewrite.
    internal static string WorldToJson(World world) => JsonSerializer.Serialize(world, Options);

    internal static World WorldFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return WorldFromJson(doc.RootElement);
    }

    static World WorldFromJson(JsonElement world) => world.Deserialize<World>(Options) ?? throw new JsonException("The save holds no world");

    internal static string Fingerprint(string description, string meaningChange) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(description + "\n" + meaningChange)))[..12];

    /// Every type reachable from the root as the serializer reads and writes it, one sorted line each.
    internal static string Describe(Type root, JsonSerializerOptions options)
    {
        var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var pending = new Stack<Type>([root]);
        while (pending.Count > 0)
        {
            var type = pending.Pop();
            string name = Name(type);
            if (lines.ContainsKey(name)) continue;
            lines[name] = DescribeOne(type, options, pending);
        }
        return string.Join("\n", lines.Select(kv => kv.Key + " " + kv.Value));
    }

    static string DescribeOne(Type type, JsonSerializerOptions options, Stack<Type> pending)
    {
        if (Nullable.GetUnderlyingType(type) is Type inner)
        {
            pending.Push(inner);
            return "nullable " + Name(inner);
        }
        if (type.IsEnum)
            return "enum " + string.Join(",", Enum.GetNames(type).Select(n => $"{n}={Convert.ToInt64(Enum.Parse(type, n))}"))
                + (type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is Type enumConverter ? " via " + Name(enumConverter) : "");
        if (type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is Type own)
            return "converter " + Name(own);

        var info = options.GetTypeInfo(type);
        switch (info.Kind)
        {
            case JsonTypeInfoKind.Object:
                var parts = new List<string>();
                if (info.PolymorphismOptions is JsonPolymorphismOptions poly)
                {
                    parts.Add("by " + poly.TypeDiscriminatorPropertyName);
                    foreach (var derived in poly.DerivedTypes)
                    {
                        pending.Push(derived.DerivedType);
                        parts.Add($"{derived.TypeDiscriminator}={Name(derived.DerivedType)}");
                    }
                }
                foreach (var p in info.Properties)
                {
                    pending.Push(p.PropertyType);
                    parts.Add($"{p.Name}:{Name(p.PropertyType)}"
                        + (p.CustomConverter is { } c ? " via " + Name(c.GetType()) : "")
                        + (p.IsRequired ? " required" : ""));
                }
                return "object " + string.Join(",", parts);
            case JsonTypeInfoKind.Enumerable:
                pending.Push(info.ElementType!);
                return "list of " + Name(info.ElementType!);
            case JsonTypeInfoKind.Dictionary:
                pending.Push(info.KeyType!);
                pending.Push(info.ElementType!);
                return $"map {Name(info.KeyType!)} to {Name(info.ElementType!)}";
            default:
                return "value";
        }
    }

    /// A type's name without assembly versions, so a runtime upgrade does not change the format.
    static string Name(Type type) =>
        type.IsGenericType
            ? $"{type.Namespace}.{type.Name}[{string.Join(",", type.GetGenericArguments().Select(Name))}]"
            : type.FullName ?? type.Name;
}
