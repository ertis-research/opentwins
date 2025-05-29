using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class NullSchema : DataSchema
    {
        [JsonPropertyName("type")]
        public new string Type => "null";
    }
}