using System.Text.Json.Serialization;

namespace OpenTwinsV2.Twins.Models
{
    public class GenericNode
    {
        //uid te lo incluye AEntity
        [JsonPropertyName("uid")]
        public required string Uid { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = [];
    }
}