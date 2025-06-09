using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Converters;

namespace Shared.Models
{
    public class Then
    {
        [JsonPropertyName("otv2:updateState")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement? UpdateState { get; set; }

        [JsonPropertyName("otv2:emitEvent")]
        public List<ThenEmitEvent>? EmitEvent { get; set; }

        [JsonPropertyName("otv2:invokeAction")]
        public List<ThenInvokeAction>? InvokeAction { get; set; }
    }
}