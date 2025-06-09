using System.Text.Json.Serialization;
using Shared.Converters;

namespace OpenTwinsv2.Things.Models
{
    public abstract class InteractionAffordance
    {
    [JsonPropertyName("@type")]
    [JsonConverter(typeof(SingleOrArrayConverter<string>))]
    public List<string>? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("titles")]
    public Dictionary<string, string>? Titles { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("descriptions")]
    public Dictionary<string, string>? Descriptions { get; set; }

    [JsonPropertyName("forms")]
    public List<Form> Forms { get; set; } = []; // Mandatory and non-empty

    [JsonPropertyName("uriVariables")]
    public Dictionary<string, DataSchema>? UriVariables { get; set; }
    }
}