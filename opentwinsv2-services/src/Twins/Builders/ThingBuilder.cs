using System.Text.Json.Nodes;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Twins.Builders
{
    public static class ThingBuilder
    {
        public static JsonObject BuildThing(string thingId, string? name = null)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = thingId,
                ["name"] = name ?? thingId,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = new JsonArray(),
                ["domains"] = new JsonArray()
            };
        }

        private static JsonObject InitAndAddType(string thingId, string type, string? name = null)
        {
            JsonObject obj = BuildThing(thingId, name);

            if (obj["dgraph.type"] is JsonArray types)
            {
                types.Add(type);
            }

            return obj;
        }

        public static JsonObject BuildTwin(string twinId)
        {
            return InitAndAddType(twinId, "Twin");
        }

        public static JsonObject BuildROThing(string roThingId)
        {
            return InitAndAddType(roThingId, "RealObject");
        }

        public static JsonObject BuildResources(string resourceId)
        {
            return InitAndAddType(resourceId, "Resource");
        }

        public static JsonObject MapToThing(ThingDescription td)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = td.Id,
                ["name"] = td.Title,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = new JsonArray(),
                ["domains"] = new JsonArray()
            };
        }

        public static JsonObject AddTwinToThing(JsonObject thing, string twinUid)
        {
            if (thing["twins"] is JsonArray twins)
            {
                twins.Add(new { uid = twinUid });
            }

            return thing;
        }
        
    }
}