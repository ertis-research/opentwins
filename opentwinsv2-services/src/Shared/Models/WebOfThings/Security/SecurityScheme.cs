using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public abstract class SecurityScheme
    {
        [JsonPropertyName("@type")]
        public object? TypeAnnotation { get; set; } // Puede ser string o array de string

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; } // Multi-language

        [JsonPropertyName("proxy")]
        public Uri? Proxy { get; set; }

        [JsonPropertyName("scheme")]
        public string Scheme { get; set; } = string.Empty; // Obligatorio
    }
}