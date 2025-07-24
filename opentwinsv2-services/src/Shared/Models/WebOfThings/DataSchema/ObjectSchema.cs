using System.Text.Json.Serialization;

namespace OpenTwinsV2.Things.Models
{
    public class ObjectSchema : DataSchema
    {
        [JsonPropertyName("properties")]
        public Dictionary<string, DataSchema>? Properties { get; set; }

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }

        [JsonIgnore]
        public override string Type => "object";
    }
}