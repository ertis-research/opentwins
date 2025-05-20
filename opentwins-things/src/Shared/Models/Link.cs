using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class Link
    {
        [JsonPropertyName("href")]
        public Uri Href { get; set; } = null!; // Obligatorio

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("rel")]
        public string? Rel { get; set; }

        [JsonPropertyName("anchor")]
        public Uri? Anchor { get; set; }

        [JsonPropertyName("sizes")]
        public string? Sizes { get; set; }

        [JsonPropertyName("hreflang")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Hreflang { get; set; }
    }
}