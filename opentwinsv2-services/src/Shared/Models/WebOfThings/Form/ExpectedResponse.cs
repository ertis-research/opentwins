using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class ExpectedResponse
    {
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = null!;
    }
}