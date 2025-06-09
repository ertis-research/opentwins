using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Converters
{
    public class ObjectJsonConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Dependiendo del token JSON, devuelve el tipo adecuado
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long l))
                        return l;
                    return reader.GetDouble();
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return reader.GetBoolean();
                case JsonTokenType.StartObject:
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return doc.RootElement.Clone(); // devuelve JsonElement
                    }
                case JsonTokenType.StartArray:
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return doc.RootElement.Clone(); // devuelve JsonElement del array
                    }
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Token no soportado: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case JsonElement je:
                    je.WriteTo(writer);
                    break;
                case null:
                    writer.WriteNullValue();
                    break;
                default:
                    // Si tienes otros tipos complejos, serialízalos como JSON
                    JsonSerializer.Serialize(writer, value, value.GetType(), options);
                    break;
            }
        }
    }
}