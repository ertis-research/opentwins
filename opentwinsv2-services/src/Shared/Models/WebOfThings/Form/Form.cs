using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Shared.Converters;

namespace OpenTwinsv2.Things.Models
{
    public class Form
    {
        [JsonPropertyName("href")]
        public Uri Href { get; set; } = null!; // Obligatorio

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = "application/json"; // Default razonable

        [JsonPropertyName("contentCoding")]
        public string? ContentCoding { get; set; }

        [JsonPropertyName("security")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Security { get; set; }

        [JsonPropertyName("scopes")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Scopes { get; set; }

        [JsonPropertyName("response")]
        public ExpectedResponse? Response { get; set; }

        [JsonPropertyName("additionalResponses")]
        public List<AdditionalExpectedResponse>? AdditionalResponses { get; set; }

        [JsonPropertyName("subprotocol")]
        public string? Subprotocol { get; set; }

        [JsonPropertyName("op")]
        [JsonConverter(typeof(SingleOrArrayConverter<OperationType>))]
        public List<OperationType>? Op { get; set; } // Valores posibles según el estándar
    }
}