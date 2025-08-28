using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class NullSchema : DataSchema
    {
        public NullSchema() { }

        [JsonIgnore]
        public override string Type => "null";
    }
}