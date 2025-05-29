using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class IntegerSchema : DataSchema
    {
        [JsonPropertyName("minimum")]
        public int? Minimum { get; set; }

        [JsonPropertyName("exclusiveMinimum")]
        public int? ExclusiveMinimum { get; set; }

        [JsonPropertyName("maximum")]
        public int? Maximum { get; set; }

        [JsonPropertyName("exclusiveMaximum")]
        public int? ExclusiveMaximum { get; set; }

        [JsonPropertyName("multipleOf")]
        public int? MultipleOf { get; set; }

        [JsonPropertyName("type")]
        public new string Type => "integer";
    }
}