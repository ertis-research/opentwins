using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class ThenEmitEvent : Then
    {
        [JsonPropertyName("event")]
        public required string Event { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }
}