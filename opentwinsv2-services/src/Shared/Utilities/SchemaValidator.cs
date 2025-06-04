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
    }
}