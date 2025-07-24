using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class ThenEmitEvent
    {
        [JsonPropertyName("event")]
        public required string Event { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }
}