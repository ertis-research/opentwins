using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class ObjectSchema : DataSchema
    {
        public ObjectSchema() { }

        [JsonPropertyName("properties")]
        public Dictionary<string, DataSchema>? Properties { get; set; }

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }

        [JsonIgnore]
        public override string Type => "object";
    }
}