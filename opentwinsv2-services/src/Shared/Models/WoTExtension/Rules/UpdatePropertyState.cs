using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class UpdatePropertyState
    {
        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement? NewValue { get; set; }

        [JsonPropertyName("timestamp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement? Timestamp { get; set; }
    }
}