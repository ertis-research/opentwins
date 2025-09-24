using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class ThenInvokeAction
    {
        [JsonPropertyName("thing")]
        public string? Thing { get; set; }  // Si es null, se asume que es el mismo Thing

        [JsonPropertyName("action")]
        public required string Action { get; set; }

        [JsonPropertyName("input")]
        public JsonElement? Input { get; set; }
    }
}