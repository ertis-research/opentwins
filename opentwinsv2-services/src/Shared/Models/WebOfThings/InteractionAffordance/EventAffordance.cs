using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class EventAffordance : InteractionAffordance
    {
        [JsonPropertyName("subscription")]
        public DataSchema? Subscription { get; set; }

        [JsonPropertyName("data")]
        public DataSchema? Data { get; set; }

        [JsonPropertyName("dataResponse")]
        public DataSchema? DataResponse { get; set; }

        [JsonPropertyName("cancellation")]
        public DataSchema? Cancellation { get; set; }
    }
}
