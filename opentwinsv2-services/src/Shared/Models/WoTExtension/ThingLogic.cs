using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenTwinsV2.Shared.Converters;

namespace OpenTwinsV2.Shared.Models
{
    public class ThingLogic
    {
        [JsonPropertyName("otv2:description")]
        public string? Description { get; set; }        // Texto descriptivo
        [JsonPropertyName("otv2:if")]
        //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement? If { get; set; }  // Condición JSONLogic (almacenada como JObject)
        [JsonPropertyName("otv2:then")]
        public Then? Then { get; set; }  // Lista de acciones a ejecutar
    }
}