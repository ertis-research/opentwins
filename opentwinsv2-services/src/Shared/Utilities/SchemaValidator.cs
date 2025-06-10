using System.Text.Json;
using OpenTwinsv2.Things.Models;

namespace Shared.Utilities
{
    public static class SchemaValidator
    {
        private static readonly HashSet<string> AllowedJsonTypes = new()
        {
            "string", "number", "integer", "boolean", "object", "array", "null"
        };

        public static JsonElement? StringToJsonElement(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return null;

            try
            {
                using var document = JsonDocument.Parse(str);
                return document.RootElement.Clone(); // Clonamos para que no se deseche al salir del using
            }
            catch (JsonException)
            {
                return null; // En caso de que el string no sea un JSON válido
            }
        }

        public static string DetectJsonElementType(string element)
        {
            JsonElement data;
            try
            {
                data = JsonDocument.Parse(element).RootElement;
            }
            catch
            {
                return "undefined";
            }
            switch (data.ValueKind)
            {
                case JsonValueKind.String:
                    return "string";

                case JsonValueKind.Number:
                    // Diferenciamos entre integer y number (float)
                    if (data.TryGetInt64(out _))
                        return "integer";
                    else
                        return "number";

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return "boolean";

                case JsonValueKind.Object:
                    return "object";

                case JsonValueKind.Array:
                    return "array";

                case JsonValueKind.Null:
                    return "null";

                default:
                    return "undefined"; // por si acaso
            }
        }

        public static bool IsTypeCompatible(string? expectedType, JsonElement? value)
        {
            if (expectedType == null) return true;
            if (value == null || value?.ValueKind == JsonValueKind.Null) return expectedType == "null";

            if (value.HasValue)
            {
                var val = value.Value;

                return expectedType switch
                {
                    "string" => val.ValueKind == JsonValueKind.String,
                    "number" => val.ValueKind == JsonValueKind.Number,
                    "integer" => val.ValueKind == JsonValueKind.Number && val.TryGetInt64(out _),
                    "boolean" => val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False,
                    "object" => val.ValueKind == JsonValueKind.Object,
                    "array" => val.ValueKind == JsonValueKind.Array,
                    "null" => val.ValueKind == JsonValueKind.Null,
                    _ => false
                };
            }
            else
            {
                return false;
            }

        }

        /*
                                        public static bool IsTypeCompatible(string? expectedType, object? value)
                                        {
                                            if (expectedType == null) return true;
                                            if (value == null) return expectedType == "null";

                                            return expectedType switch
                                            {
                                                "string" => value is string,
                                                "number" => value is float || value is double || value is decimal,
                                                "integer" => value is int || value is long,
                                                "boolean" => value is bool,
                                                "object" => value is IDictionary<string, object> || value is JsonElement e && e.ValueKind == JsonValueKind.Object,
                                                "array" => value is IEnumerable<object> || value is JsonElement e && e.ValueKind == JsonValueKind.Array,
                                                "null" => value == null,
                                                _ => false
                                            };
                                        }
                                */
    }
}