using System.Text.Json.Serialization;
using Dgraph4Net.ActiveRecords;

namespace OpenTwinsv2.Twins.Models
{
    public class ThingNode : AEntity<ThingNode>
    {
        //uid te lo incluye AEntity
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("twins")]
        public List<ThingNode> Twins { get; set; } = [];

        [JsonPropertyName("events")]
        public List<EventNode> Events { get; set; } = [];

        [JsonPropertyName("types")]
        public List<ThingNode> Types { get; set; } = [];

        [JsonPropertyName("thingId")]
        public string? ThingId { get; set; }
    }

    internal sealed class ThingNodeMapping : ClassMap<ThingNode>
    {
        protected override void Map()
        {
            SetType("Thing"); // Ajusta el tipo al nombre de la entidad, si es necesario

            String(x => x.Name, "name");
            HasMany(x => x.Twins, "twins");
            HasMany(x => x.Events, "events", x => x.Publishers);
            HasMany(x => x.Types, "types");
            String(x => x.ThingId, "thingId");
        }
    }
}