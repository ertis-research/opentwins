using System.Text.Json.Serialization;

namespace OpenTwinsv2.Things.Models
{
    public class NullSchema : DataSchema
    {
        [JsonIgnore]
        public override string Type => "null";
    }
}