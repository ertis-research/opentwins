using System.Text.Json.Serialization;

namespace OpenTwinsV2.Things.Models
{
    public class NumberSchema : DataSchema
    {
        [JsonPropertyName("minimum")]
        public double? Minimum { get; set; }

        [JsonPropertyName("exclusiveMinimum")]
        public double? ExclusiveMinimum { get; set; }

        [JsonPropertyName("maximum")]
        public double? Maximum { get; set; }

        [JsonPropertyName("exclusiveMaximum")]
        public double? ExclusiveMaximum { get; set; }

        [JsonPropertyName("multipleOf")]
        public double? MultipleOf { get; set; }
        
        [JsonIgnore]
        public override string Type => "number";
    }
}
