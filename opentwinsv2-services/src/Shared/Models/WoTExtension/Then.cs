using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTwinsV2.Shared.Converters;

namespace OpenTwinsV2.Shared.Models
{
    public class Then
    {
        [JsonPropertyName("otv2:updateState")]
        public Dictionary<string, UpdatePropertyState>? UpdateState { get; set; }

        [JsonPropertyName("otv2:emitEvent")]
        public List<ThenEmitEvent>? EmitEvent { get; set; }

        [JsonPropertyName("otv2:invokeAction")]
        public List<ThenInvokeAction>? InvokeAction { get; set; }
    }
}