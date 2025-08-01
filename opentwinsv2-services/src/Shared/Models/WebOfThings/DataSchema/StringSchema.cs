using System.Text.Json.Serialization;

namespace OpenTwinsV2.Things.Models
{
    public class StringSchema : DataSchema
    {
        public StringSchema() { }

        [JsonPropertyName("minLength")]
        public uint? MinLength { get; set; }

        [JsonPropertyName("maxLength")]
        public uint? MaxLength { get; set; }

        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [JsonPropertyName("contentEncoding")]
        public string? ContentEncoding { get; set; }

        [JsonPropertyName("contentMediaType")]
        public string? ContentMediaType { get; set; }

        [JsonIgnore]
        public override string? Type => "string";
    }
}