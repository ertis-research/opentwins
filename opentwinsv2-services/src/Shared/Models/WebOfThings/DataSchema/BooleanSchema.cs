using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class BooleanSchema : DataSchema
    {
        // No propiedades adicionales específicas para boolean, 
        // pero se indica su tipo en JSON-LD con "@type": "boolean"
        public BooleanSchema() { }

        [JsonIgnore]
        public override string Type => "boolean";
    }
}