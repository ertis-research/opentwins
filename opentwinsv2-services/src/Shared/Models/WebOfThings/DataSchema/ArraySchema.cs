using System.Text.Json.Serialization;
using OpenTwinsV2.Shared.Converters;

namespace OpenTwinsV2.Shared.Models
{
    public class ArraySchema : DataSchema
    {
        public ArraySchema() { }

        [JsonPropertyName("items")]
        [JsonConverter(typeof(SingleOrArrayConverter<DataSchema>))]
        public List<DataSchema>? Items { get; set; } // Puede ser un DataSchema o una lista de DataSchema

        [JsonPropertyName("minItems")]
        public uint? MinItems { get; set; }

        [JsonPropertyName("maxItems")]
        public uint? MaxItems { get; set; }

        [JsonIgnore]
        public override string Type => "array";
    }
}