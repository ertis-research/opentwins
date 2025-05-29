using System.Text.Json;

namespace Shared.Models
{
    public class Rule
    {
        public required string Id { get; set; }                 // Identificador de la regla
        public string? Description { get; set; }        // Texto descriptivo
        public required JsonElement Condition { get; set; }     // Condición JSONLogic (almacenada como JObject)
        public required List<object> Actions { get; set; }  // Lista de acciones a ejecutar

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