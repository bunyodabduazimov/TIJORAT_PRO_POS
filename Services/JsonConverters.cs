using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFPOS.Services;

internal sealed class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value,
            JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
            JsonTokenType.True => 1,
            JsonTokenType.False => 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

internal sealed class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value == 1,
            JsonTokenType.String when bool.TryParse(reader.GetString(), out var parsed) => parsed,
            JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed == 1,
            _ => false
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
