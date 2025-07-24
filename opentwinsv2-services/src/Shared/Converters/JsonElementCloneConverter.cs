using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Converters
{
    public class JsonElementCloneConverter : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.Clone(); // ← esto evita que se quede sin contexto
        }

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
                    // Solo escribe si tiene contenido válido
        if (value.ValueKind != JsonValueKind.Undefined && value.ValueKind != JsonValueKind.Null)
        {
            value.WriteTo(writer);
        }
        else
        {
                Console.WriteLine("NULL VALUE JSONELEMENT");
            writer.WriteNullValue(); // evita excepción si está vacío
        }
        }
    }
}