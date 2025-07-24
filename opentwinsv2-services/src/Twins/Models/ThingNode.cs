using System.Text.Json.Serialization;
using Dgraph4Net.ActiveRecords;

namespace OpenTwinsV2.Twins.Models
{
    public class ThingNode : AEntity<ThingNode>
    {
        //uid te lo incluye AEntity
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("thingId")]
        public string? ThingId { get; set; }

        [JsonPropertyName("isTwin")]
        public bool IsTwin { get; set; } = false;

        [JsonPropertyName("contains")]
        public List<ThingNode> Contains { get; set; } = [];

        [JsonPropertyName("events")]
        public List<EventNode> Events { get; set; } = [];

        [JsonPropertyName("types")]
        public List<ThingNode> Types { get; set; } = [];

        public ThingNode() { }

        public ThingNode(string thingId, string? name, bool isTwin)
        {
            Name = name;
            ThingId = thingId;
            IsTwin = isTwin;
        }
    }

    internal sealed class ThingNodeMapping : ClassMap<ThingNode>
    {
        protected override void Map()
        {
            SetType("Thing"); // Ajusta el tipo al nombre de la entidad, si es necesario

            String(x => x.Name, "name");
            Boolean(x => x.IsTwin, "isTwin"); 
            HasMany(x => x.Contains, "contains");
            HasMany(x => x.Events, "events", x => x.Publishers);
            HasMany(x => x.Types, "types");
            String(x => x.ThingId, "thingId");
        }
    }
}