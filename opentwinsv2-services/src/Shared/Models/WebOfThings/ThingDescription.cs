using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTwinsV2.Shared.Converters;
using OpenTwinsV2.Shared.Models;

namespace OpenTwinsV2.Shared.Models
{
    public class ThingDescription
    {
        [JsonPropertyName("@context")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Context { get; set; } = new();

        [JsonPropertyName("@type")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? TypeAnnotation { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("titles")]
        public Dictionary<string, string>? Titles { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; }

        [JsonPropertyName("version")]
        public VersionInfo? Version { get; set; }

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("modified")]
        public DateTime? Modified { get; set; }

        [JsonPropertyName("support")]
        public Uri? Support { get; set; }

        [JsonPropertyName("base")]
        public Uri? Base { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, PropertyAffordance>? Properties { get; set; }

        [JsonPropertyName("actions")]
        public Dictionary<string, ActionAffordance>? Actions { get; set; }

        [JsonPropertyName("events")]
        public Dictionary<string, EventAffordance>? Events { get; set; }

        [JsonPropertyName("links")]
        public List<Link>? Links { get; set; }

        [JsonPropertyName("forms")]
        public List<Form>? Forms { get; set; }

        [JsonPropertyName("security")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Security { get; set; } = new();
        /*
                [JsonPropertyName("securityDefinitions")]
                public Dictionary<string, SecurityScheme> SecurityDefinitions { get; set; } = new();
        */
        [JsonPropertyName("profile")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? Profile { get; set; }

        [JsonPropertyName("schemaDefinitions")]
        public Dictionary<string, DataSchema>? SchemaDefinitions { get; set; }

        [JsonPropertyName("uriVariables")]
        public Dictionary<string, DataSchema>? UriVariables { get; set; }

        [JsonPropertyName("otv2:rules")]
        public Dictionary<string, ThingLogic>? Rules { get; set; }

        [JsonPropertyName("otv2:subscribedEvents")]
        public List<SubscribedEvent>? SubscribedEvents { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }
    }
}

