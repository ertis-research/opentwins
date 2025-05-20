using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class AdditionalExpectedResponse
    {
        [JsonPropertyName("success")]
        public bool? Success { get; set; }  // Con default, pero opcional en JSON

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; } // Con default, pero opcional en JSON

        [JsonPropertyName("schema")]
        public string? Schema { get; set; }  // Referencia por nombre a schemaDefinitions
    }

}
