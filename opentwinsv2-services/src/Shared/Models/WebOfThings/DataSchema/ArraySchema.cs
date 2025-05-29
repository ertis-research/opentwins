using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class ArraySchema : DataSchema
    {
        [JsonPropertyName("items")]
        [JsonConverter(typeof(SingleOrArrayConverter<DataSchema>))]
        public List<DataSchema>? Items { get; set; } // Puede ser un DataSchema o una lista de DataSchema

        [JsonPropertyName("minItems")]
        public uint? MinItems { get; set; }

        [JsonPropertyName("maxItems")]
        public uint? MaxItems { get; set; }

        [JsonPropertyName("type")]
        public new string Type => "array";
    }
}