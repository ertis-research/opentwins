using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class BooleanSchema : DataSchema
    {
        // No propiedades adicionales específicas para boolean, 
        // pero se indica su tipo en JSON-LD con "@type": "boolean"

        [JsonPropertyName("type")]
        public new string Type => "boolean";
    }
}