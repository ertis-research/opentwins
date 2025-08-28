using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class PropertyAffordance : InteractionAffordance
    {
        [JsonPropertyName("observable")]
        public bool Observable { get; set; } = false;

        // De DataSchema
        [JsonPropertyName("type")]
        public required string DataType { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("readOnly")]
        public bool? ReadOnly { get; set; }

        [JsonPropertyName("writeOnly")]
        public bool? WriteOnly { get; set; }
    }
}