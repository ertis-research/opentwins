using System.Text.Json.Serialization;

namespace Shared.Models
{
    public abstract class ThenEmitEvent : Then
    {
        [JsonPropertyName("otv2:event")]
        public required string Event { get; set; }

        [JsonPropertyName("otv2:data")]
        public Dictionary<string, object>? Data { get; set; }
    }
}