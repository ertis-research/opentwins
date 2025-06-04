using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class Rule
    {
        [JsonPropertyName("@id")]
        public required string Id { get; set; }                 // Identificador de la regla
        [JsonPropertyName("otv2:description")]
        public string? Description { get; set; }        // Texto descriptivo
        [JsonPropertyName("otv2:if")]
        public required JsonElement If { get; set; }     // Condición JSONLogic (almacenada como JObject)
        [JsonPropertyName("otv2:then")]
        public required List<Then> Then  { get; set; }  // Lista de acciones a ejecutar

    }
/*
    public abstract class ActionItem
    {
        public required string ActionType { get; set; }                // "updateProperty", "emitEvent", "callAction", etc.

        // Propiedades para updateProperty
        public string Property { get; set; }            // Nombre de la propiedad a actualizar
        public JToken Value { get; set; }               // Valor nuevo (JToken para flexibilidad)

        // Propiedades para emitEvent
        public string Event { get; set; }                // Nombre del evento a emitir
        public JsonElement Payload { get; set; }             // Datos del evento

        // Propiedades para callAction
        public string Action { get; set; }                // Nombre de la acción a llamar
        public JsonElement Parameters { get; set; }           // Parámetros para la acción
    }

    public enum ActionType
    {
        UpdateProperty,
        EmitEvent,
        CallAction
    }
*/
}