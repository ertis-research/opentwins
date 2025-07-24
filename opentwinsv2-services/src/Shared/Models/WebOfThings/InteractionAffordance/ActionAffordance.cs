using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Things.Models
{
    public class ActionAffordance : InteractionAffordance
    {
        [JsonPropertyName("input")]
        public DataSchema? Input { get; set; }

        [JsonPropertyName("output")]
        public DataSchema? Output { get; set; }

        [JsonPropertyName("safe")]
        public bool Safe { get; set; } = false;

        [JsonPropertyName("idempotent")]
        public bool Idempotent { get; set; } = false;

        [JsonPropertyName("synchronous")]
        public bool Synchronous { get; set; } = false;
    }
}