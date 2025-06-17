using System.Text.Json.Serialization;
using Dgraph4Net.ActiveRecords;

namespace OpenTwinsv2.Twins.Models
{
    public class EventNode : AEntity<EventNode>
    {
        //uid te lo incluye AEntity
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("publishers")]
        public List<ThingNode> Publishers { get; set; } = [];
        [JsonPropertyName("subscribers")]
        public List<ThingNode> Subscribers { get; set; } = [];
    }

    internal sealed class EventNodeMapping : ClassMap<EventNode>
    {
        protected override void Map()
        {
            SetType("Event"); // Ajusta el tipo al nombre de la entidad, si es necesario

            String(x => x.Name, "name");

        }
    }
}