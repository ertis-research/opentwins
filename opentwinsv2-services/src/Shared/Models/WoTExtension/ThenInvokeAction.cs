using System.Text.Json.Serialization;

namespace Shared.Models
{
    public abstract class ThenInvokeAction : Then
    {
        [JsonPropertyName("otv2:thing")]
        public string? Thing { get; set; }  // Si es null, se asume que es el mismo Thing

        [JsonPropertyName("otv2:action")]
        public required string Action { get; set; }

        [JsonPropertyName("otv2:input")]
        public Dictionary<string, object>? Input { get; set; }
    }
}