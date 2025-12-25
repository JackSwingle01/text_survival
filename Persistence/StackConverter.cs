using System.Text.Json;
using System.Text.Json.Serialization;

namespace text_survival.Persistence;

/// <summary>
/// Factory that creates StackConverter<T> for any Stack type.
/// Fixes the Stack serialization reversal issue for all Stack<T> types.
/// </summary>
public class StackConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Stack<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(StackConverter<>).MakeGenericType(elementType);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

/// <summary>
/// Custom JSON converter for Stack<T> that preserves order during serialization round-trips.
///
/// Without this converter, Stack<T> serialization reverses order because:
/// - Serialize: Enumerates top-to-bottom → writes [top, ..., bottom]
/// - Deserialize: Pushes in array order → [bottom, ..., top] (reversed!)
///
/// This converter writes bottom-to-top so deserialization reconstructs correctly.
/// </summary>
public class StackConverter<T> : JsonConverter<Stack<T>>
{
    public override Stack<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = JsonSerializer.Deserialize<List<T>>(ref reader, options);
        if (list == null) return null;

        // List is in bottom-to-top order (as written by Write)
        // Stack constructor pushes each item, so last item ends up on top
        // ["bottom", "middle", "top"] → Push(bottom), Push(middle), Push(top) → top on top ✓
        return new Stack<T>(list);
    }

    public override void Write(Utf8JsonWriter writer, Stack<T> value, JsonSerializerOptions options)
    {
        // Write bottom-to-top so deserialization works correctly
        var list = value.ToList();  // Top-to-bottom
        list.Reverse();              // Now bottom-to-top
        JsonSerializer.Serialize(writer, list, options);
    }
}
