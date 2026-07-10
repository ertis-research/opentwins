using System.Text.Json;
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

        [JsonPropertyName("otv2:jsonLogic")]
        public JsonElement? JsonLogic { get; set; }
                
        [JsonPropertyName("default")]
        public object? Default { get; set; }

        //new WOT 2 propety const
        [JsonPropertyName("const")]
        public object? Const {get; set;}
    }
}