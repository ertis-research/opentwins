using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Converters
{
    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(List<T>);
        }

        public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Si comienza con un array
            var customOptions = new JsonSerializerOptions(options);
            customOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            customOptions.PropertyNameCaseInsensitive = true;

            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<List<T>>(ref reader, customOptions);
            }

            // Si es un solo valor
            var singleValue = JsonSerializer.Deserialize<T>(ref reader, customOptions);
            return singleValue != null ? [singleValue] : null;
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNullValue();
            }
            else if (value.Count == 1)
            {
                JsonSerializer.Serialize(writer, value[0], options);
            }
            else
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }

    }
}