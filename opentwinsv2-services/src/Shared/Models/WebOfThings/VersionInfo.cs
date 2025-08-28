using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class VersionInfo
    {
        [JsonPropertyName("instance")]
        public string Instance { get; set; } = string.Empty; // Obligatorio

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}