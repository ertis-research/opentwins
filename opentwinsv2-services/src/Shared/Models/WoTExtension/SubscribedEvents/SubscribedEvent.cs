using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class SubscribedEvent
    {
        [JsonPropertyName("otv2:event")]
        public required string Event { get; set; }

        [JsonPropertyName("otv2:type")]
        public string Type { get; set; } = "application/json";

        [JsonPropertyName("otv2:source")]
        public List<string>? Source { get; set; }

        [JsonPropertyName("otv2:autoEmitState")]
        public bool AutoEmitState { get; set; } = false;
    }
}