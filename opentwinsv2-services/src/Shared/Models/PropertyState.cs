using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shared.Converters;

namespace Shared.Models
{
    public class PropertyState
    {
        [JsonConverter(typeof(ObjectJsonConverter))]
        public JsonElement? Value { get; set; }
        public DateTime? LastUpdate { get; set; }

        public PropertyState()
        {
        }

        public PropertyState(JsonElement value)
        {
            Value = value;
            LastUpdate = DateTime.UtcNow;
        }

        public PropertyState(JsonElement value, DateTime lastUpdate)
        {
            Value = value;
            LastUpdate = lastUpdate;
        }

        public override string ToString()
        {
            string valueStr;

            try
            {
                // Serializa Value a JSON, para que muestre objetos complejos o listas correctamente
                valueStr = JsonSerializer.Serialize(Value, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    // Opcional: Ignorar valores nulos para más limpieza
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch
            {
                valueStr = Value?.ToString() ?? "null";
            }

            string lastUpdateStr = LastUpdate.HasValue
                ? LastUpdate.Value.ToString("o")
                : "null";

            return $"{{ Value = {valueStr}, LastUpdate = {lastUpdateStr} }}";
        }
    }
}