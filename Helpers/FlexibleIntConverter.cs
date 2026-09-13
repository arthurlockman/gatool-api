using System.Text.Json;
using System.Text.Json.Serialization;

namespace GAToolAPI.Helpers;

/// <summary>
///     Some historic FIRST Global seasons encode flag fields as JSON booleans (e.g. <c>false</c>)
///     instead of the 0/1 integers used in newer seasons. This reads either representation as an int.
/// </summary>
public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => 1,
            JsonTokenType.False => 0,
            _ => reader.GetInt32()
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}