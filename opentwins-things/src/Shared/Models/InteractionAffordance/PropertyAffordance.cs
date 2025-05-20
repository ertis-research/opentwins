using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class PropertyAffordance : InteractionAffordance
    {
        [JsonPropertyName("observable")]
        public bool Observable { get; set; } = false;
    }
}